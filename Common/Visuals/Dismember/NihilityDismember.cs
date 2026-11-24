using System;
using System.Collections.Generic;
using InnoVault;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace WeepForTheDeparted.Common.Visuals.Dismember
{
	public struct DismemberCut
	{
		public Vector2 PointLocal;
		public Vector2 Normal;
		public int Birth;
		public int Hold;
	}

	public class DismemberPiece
	{
		public Vector2[] Verts;
		public Vector2 Centroid;
		public readonly List<sbyte> CutSides = new List<sbyte>();
		public readonly List<float> CutSpins = new List<float>();
	}

	public class DismemberEntry
	{
		public int NpcIndex;
		public int NpcType;
		public int Timer;
		public int Duration;
		public Vector2 AnchorCenter;
		public bool VisualOnly;
		public bool BehindTiles;
		public int SnapWidth;
		public int SnapHeight;
		public bool Captured;
		public int CaptureFailures;
		public readonly List<DismemberCut> Cuts = new List<DismemberCut>();
		public readonly List<DismemberPiece> Pieces = new List<DismemberPiece>();

		public const int FadeFrames = 25;

		public float FadeAlpha => 1f - MathHelper.Clamp((Timer - (Duration - FadeFrames)) / (float)FadeFrames, 0f, 1f);
	}

	public class NihilityDismember : ModSystem
	{
		public const int DefaultDuration = 240;
		public const int DefaultHoldFrames = 14;
		public const int SeparateFrames = 24;
		public const float MinPieceArea = 20f;
		public const int MaxPieces = 64;

		private const float CapturePaddingRatio = 0.25f;
		private const float MinCapturePadding = 24f;
		private const float MaxCapturePadding = 160f;
		private const int MinCaptureDimension = 64;
		private const int MaxCaptureDimension = 1600;

		public static readonly List<DismemberEntry> Entries = new List<DismemberEntry>();
		public static readonly Dictionary<int, RenderTarget2D> SnapRTs = new Dictionary<int, RenderTarget2D>();

		/// <summary>
		/// Normalized reassembly progress [0, 1] during ultimate sheathing animation.
		/// As this advances to 1, severed pieces converge smoothly back to zero offset.
		/// </summary>
		public static float ReassembleFactor { get; set; } = 0f;

		public static void SnapBackAll()
		{
			ReassembleFactor = 0f;
			DisposeAllSnapshots();
			Entries.Clear();
		}

		public static DismemberEntry GetEntry(int npcIndex)
		{
			for (int i = 0; i < Entries.Count; i++)
			{
				if (Entries[i].NpcIndex == npcIndex)
					return Entries[i];
			}
			return null;
		}

		public static bool IsDismembered(int npcIndex) => GetEntry(npcIndex) != null;

		public static bool Trigger(NPC npc, Vector2 cutPointWorld, float cutAngle, int duration = DefaultDuration, int holdFrames = DefaultHoldFrames)
		{
			if (npc == null || !npc.active)
				return false;

			bool isBossOrMoving = npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || npc.realLife >= 0 || npc.type == NPCID.TargetDummy || !npc.noGravity || npc.velocity != Vector2.Zero;

			DismemberEntry entry = GetEntry(npc.whoAmI);
			if (entry == null || entry.NpcType != npc.type)
			{
				if (entry != null)
				{
					Entries.Remove(entry);
				}

				entry = CreateEntry(npc, npc.Center, duration, holdFrames);
				entry.VisualOnly = isBossOrMoving;
				entry.BehindTiles = npc.behindTiles;
				Entries.Add(entry);
			}
			else
			{
				entry.Duration = Math.Max(entry.Duration, entry.Timer + duration);
				if (isBossOrMoving)
				{
					entry.VisualOnly = true;
					entry.AnchorCenter = npc.Center;
				}
			}

			AddCut(entry, cutPointWorld, cutAngle, holdFrames);
			return true;
		}

		public static void CollectGroup(NPC root, List<NPC> output)
		{
			output.Clear();
			if (root == null || !root.active)
				return;

			int anchor = root.realLife >= 0 && root.realLife < Main.maxNPCs && Main.npc[root.realLife].active
				? root.realLife
				: root.whoAmI;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active)
					continue;

				if (n.whoAmI == anchor || (n.realLife >= 0 && n.realLife == anchor) || n.whoAmI == root.whoAmI)
				{
					output.Add(n);
				}
			}
		}

		private static readonly List<NPC> groupScratch = new List<NPC>();

		public static bool TriggerGroup(NPC npc, Vector2 cutPointWorld, float cutAngle, int duration = DefaultDuration, int holdFrames = DefaultHoldFrames)
		{
			if (npc == null || !npc.active)
				return false;

			CollectGroup(npc, groupScratch);
			if (groupScratch.Count <= 1)
			{
				return Trigger(npc, cutPointWorld, cutAngle, duration, holdFrames);
			}

			bool any = false;
			foreach (NPC member in groupScratch)
			{
				any |= Trigger(member, member.Center, cutAngle, duration, holdFrames);
			}
			groupScratch.Clear();
			return any;
		}

		private static DismemberEntry CreateEntry(NPC npc, Vector2 anchorCenter, int duration, int holdFrames)
		{
			DismemberEntry entry = new DismemberEntry
			{
				NpcIndex = npc.whoAmI,
				NpcType = npc.type,
				Duration = duration,
				AnchorCenter = anchorCenter
			};

			if (!Main.dedServ)
			{
				ComputeSnapSize(npc, out int snapW, out int snapH);
				entry.SnapWidth = snapW;
				entry.SnapHeight = snapH;

				float hw = snapW * 0.5f;
				float hh = snapH * 0.5f;
				entry.Pieces.Add(new DismemberPiece
				{
					Verts = new Vector2[] { new Vector2(-hw, -hh), new Vector2(hw, -hh), new Vector2(hw, hh), new Vector2(-hw, hh) },
					Centroid = Vector2.Zero
				});
			}

			return entry;
		}

		public static Vector2 ComputeBodySize(NPC npc)
		{
			if (npc == null)
				return Vector2.Zero;

			Vector2 hitboxSize = new Vector2(Math.Max(npc.width, 0f), Math.Max(npc.height, 0f));
			Vector2 frameSize = ResolveCurrentFrameSize(npc, hitboxSize);

			float scale = Math.Abs(npc.scale);
			if (!float.IsFinite(scale))
				scale = 1f;

			float spriteWidth = frameSize.X * scale;
			float spriteHeight = frameSize.Y * scale;

			float rotation = float.IsFinite(npc.rotation) ? npc.rotation : 0f;
			float cos = Math.Abs((float)Math.Cos(rotation));
			float sin = Math.Abs((float)Math.Sin(rotation));
			float rotatedWidth = spriteWidth * cos + spriteHeight * sin;
			float rotatedHeight = spriteWidth * sin + spriteHeight * cos;

			float width = Math.Max(rotatedWidth, hitboxSize.X);
			float height = Math.Max(rotatedHeight, hitboxSize.Y);
			if (!float.IsFinite(width))
				width = hitboxSize.X;
			if (!float.IsFinite(height))
				height = hitboxSize.Y;

			return new Vector2(Math.Max(width, 1f), Math.Max(height, 1f));
		}

		private static Vector2 ResolveCurrentFrameSize(NPC npc, Vector2 hitboxSize)
		{
			float width = Math.Max(npc.frame.Width, 0f);
			float height = Math.Max(npc.frame.Height, 0f);

			if (width <= 0f)
				width = hitboxSize.X;
			if (height <= 0f)
				height = hitboxSize.Y;

			if ((width <= 0f || height <= 0f) && !Main.dedServ && npc.type >= NPCID.None && npc.type < TextureAssets.Npc.Length)
			{
				Main.instance.LoadNPC(npc.type);
				Texture2D texture = TextureAssets.Npc[npc.type].Value;
				int frames = Math.Max(Main.npcFrameCount[npc.type], 1);
				if (width <= 0f)
					width = texture.Width;
				if (height <= 0f)
					height = texture.Height / (float)frames;
			}

			return new Vector2(Math.Max(width, 1f), Math.Max(height, 1f));
		}

		private static int ClampCaptureDimension(float size)
		{
			if (!float.IsFinite(size))
				return MaxCaptureDimension;

			int pixels = (int)Math.Ceiling(Math.Max(size, MinCaptureDimension));
			pixels = (pixels + 1) & ~1;
			return Math.Clamp(pixels, MinCaptureDimension, MaxCaptureDimension);
		}

		public static void ComputeSnapSize(NPC npc, out int width, out int height)
		{
			Vector2 bodySize = ComputeBodySize(npc);
			float padX = MathHelper.Clamp(bodySize.X * CapturePaddingRatio, MinCapturePadding, MaxCapturePadding);

			float drawOffsetY = npc?.gfxOffY ?? 0f;
			if (npc?.ModNPC != null)
				drawOffsetY += npc.ModNPC.DrawOffsetY;
			if (!float.IsFinite(drawOffsetY))
				drawOffsetY = 0f;

			float padY = MathHelper.Clamp(bodySize.Y * CapturePaddingRatio + Math.Abs(drawOffsetY), MinCapturePadding, MaxCapturePadding);

			width = ClampCaptureDimension(bodySize.X + padX * 2f);
			height = ClampCaptureDimension(bodySize.Y + padY * 2f);
		}

		private static void AddCut(DismemberEntry entry, Vector2 cutPointWorld, float cutAngle, int holdFrames)
		{
			if (entry.SnapWidth <= 0 || entry.Cuts.Count >= 12)
				return;

			Vector2 local = cutPointWorld - entry.AnchorCenter;
			local.X = MathHelper.Clamp(local.X, -entry.SnapWidth * 0.35f, entry.SnapWidth * 0.35f);
			local.Y = MathHelper.Clamp(local.Y, -entry.SnapHeight * 0.35f, entry.SnapHeight * 0.35f);

			Vector2 dir = cutAngle.ToRotationVector2();
			DismemberCut cut = new DismemberCut
			{
				PointLocal = local,
				Normal = new Vector2(-dir.Y, dir.X),
				Birth = entry.Timer,
				Hold = holdFrames
			};

			SplitPieces(entry, in cut);
			entry.Cuts.Add(cut);
		}

		private static void SplitPieces(DismemberEntry entry, in DismemberCut cut)
		{
			List<DismemberPiece> next = new List<DismemberPiece>(entry.Pieces.Count * 2);
			foreach (DismemberPiece piece in entry.Pieces)
			{
				if (next.Count >= MaxPieces)
				{
					next.Add(piece);
					continue;
				}

				List<Vector2> posSide = ClipHalfPlane(piece.Verts, cut.PointLocal, cut.Normal, 1f);
				List<Vector2> negSide = ClipHalfPlane(piece.Verts, cut.PointLocal, cut.Normal, -1f);

				if (posSide.Count >= 3 && negSide.Count >= 3 && PolyArea(posSide) >= MinPieceArea && PolyArea(negSide) >= MinPieceArea)
				{
					next.Add(MakeChild(piece, posSide, 1));
					next.Add(MakeChild(piece, negSide, -1));
				}
				else
				{
					piece.CutSides.Add(0);
					piece.CutSpins.Add(0f);
					next.Add(piece);
				}
			}

			entry.Pieces.Clear();
			entry.Pieces.AddRange(next);
		}

		private static DismemberPiece MakeChild(DismemberPiece parent, List<Vector2> verts, sbyte side)
		{
			DismemberPiece child = new DismemberPiece
			{
				Verts = verts.ToArray(),
				Centroid = PolyCentroid(verts)
			};
			child.CutSides.AddRange(parent.CutSides);
			child.CutSpins.AddRange(parent.CutSpins);
			child.CutSides.Add(side);
			child.CutSpins.Add(side * Main.rand.NextFloat(0.015f, 0.045f));
			return child;
		}

		public static List<Vector2> ClipHalfPlane(Vector2[] poly, Vector2 p, Vector2 n, float side)
		{
			List<Vector2> result = new List<Vector2>(poly.Length + 2);
			for (int i = 0; i < poly.Length; i++)
			{
				Vector2 a = poly[i];
				Vector2 b = poly[(i + 1) % poly.Length];
				float da = Vector2.Dot(a - p, n) * side;
				float db = Vector2.Dot(b - p, n) * side;

				if (da >= 0f)
					result.Add(a);

				if ((da >= 0f) != (db >= 0f))
				{
					result.Add(Vector2.Lerp(a, b, da / (da - db)));
				}
			}
			return result;
		}

		public static float PolyArea(List<Vector2> poly)
		{
			float area = 0f;
			for (int i = 0; i < poly.Count; i++)
			{
				Vector2 p0 = poly[i];
				Vector2 p1 = poly[(i + 1) % poly.Count];
				area += p0.X * p1.Y - p1.X * p0.Y;
			}
			return Math.Abs(area) * 0.5f;
		}

		public static Vector2 PolyCentroid(List<Vector2> poly)
		{
			float signedArea = 0f;
			Vector2 acc = Vector2.Zero;
			for (int i = 0; i < poly.Count; i++)
			{
				Vector2 p0 = poly[i];
				Vector2 p1 = poly[(i + 1) % poly.Count];
				float cross = p0.X * p1.Y - p1.X * p0.Y;
				signedArea += cross;
				acc += (p0 + p1) * cross;
			}

			if (Math.Abs(signedArea) < 1e-4f)
			{
				Vector2 avg = Vector2.Zero;
				foreach (Vector2 v in poly)
					avg += v;
				return avg / Math.Max(poly.Count, 1);
			}

			return acc / (3f * signedArea);
		}

		public static float SeparationCurve(int age, int hold)
		{
			if (age < hold)
				return 0f;

			float progress = MathHelper.Clamp((age - hold) / (float)SeparateFrames, 0f, 1f);
			return 1f - (float)Math.Pow(1f - progress, 3); // EaseOutCubic
		}

		public static void GetPieceMotion(DismemberEntry entry, DismemberPiece piece, out Vector2 offset, out float rotation)
		{
			offset = Vector2.Zero;
			rotation = 0f;

			for (int i = 0; i < entry.Cuts.Count; i++)
			{
				sbyte side = i < piece.CutSides.Count ? piece.CutSides[i] : (sbyte)0;
				if (side == 0)
					continue;

				DismemberCut cut = entry.Cuts[i];
				int age = entry.Timer - cut.Birth;
				float curve = SeparationCurve(age, cut.Hold);
				float maxSlide = 22f; // slide distance in pixels

				offset += cut.Normal * (side * maxSlide * curve);
				if (i < piece.CutSpins.Count)
				{
					rotation += piece.CutSpins[i] * curve;
				}
			}

			// Smoothly converge severed pieces back to zero offset during ultimate sheathing
			if (ReassembleFactor > 0f)
			{
				float remain = MathHelper.Clamp(1f - ReassembleFactor, 0f, 1f);
				offset *= remain;
				rotation *= remain;
			}
		}

		public static RenderTarget2D EnsureSnapshotRT(GraphicsDevice gd, DismemberEntry entry)
		{
			if (SnapRTs.TryGetValue(entry.NpcIndex, out RenderTarget2D existing) && existing != null && !existing.IsDisposed)
			{
				if (existing.Width == entry.SnapWidth && existing.Height == entry.SnapHeight)
					return existing;

				existing.SafeDispose();
				SnapRTs.Remove(entry.NpcIndex);
			}

			try
			{
				RenderTarget2D rt = new RenderTarget2D(gd, entry.SnapWidth, entry.SnapHeight, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
				SnapRTs[entry.NpcIndex] = rt;
				return rt;
			}
			catch
			{
				return null;
			}
		}

		public static void DisposeAllSnapshots()
		{
			foreach (var rt in SnapRTs.Values)
			{
				rt?.SafeDispose();
			}
			SnapRTs.Clear();
		}

		private static uint lastBurstTick;

		public static void SeparationBurst(DismemberEntry entry, in DismemberCut cut)
		{
			if (Main.GameUpdateCount != lastBurstTick)
			{
				lastBurstTick = Main.GameUpdateCount;
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.4f, Volume = 0.65f }, entry.AnchorCenter);
			}

			Vector2 tangent = new Vector2(-cut.Normal.Y, cut.Normal.X);
			float halfLen = Math.Min(entry.SnapWidth, entry.SnapHeight) * 0.35f;
			for (int k = 0; k < 6; k++)
			{
				Vector2 pos = entry.AnchorCenter + cut.PointLocal + tangent * Main.rand.NextFloat(-1f, 1f) * halfLen;
				Vector2 vel = cut.Normal * Main.rand.NextFloat(2f, 5f) * (Main.rand.NextBool() ? 1f : -1f) + tangent * Main.rand.NextFloat(-1f, 1f);
				Dust d = Dust.NewDustPerfect(pos, DustID.CrimsonTorch, vel, 100, default, 1.4f);
				d.noGravity = true;
			}
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ)
				return;

			for (int i = Entries.Count - 1; i >= 0; i--)
			{
				DismemberEntry entry = Entries[i];
				entry.Timer++;

				if (entry.NpcIndex < 0 || entry.NpcIndex >= Main.maxNPCs)
				{
					Entries.RemoveAt(i);
					continue;
				}

				NPC npc = Main.npc[entry.NpcIndex];
				if (!npc.active || npc.type != entry.NpcType || entry.Timer >= entry.Duration)
				{
					if (SnapRTs.TryGetValue(entry.NpcIndex, out RenderTarget2D rt))
					{
						rt?.SafeDispose();
						SnapRTs.Remove(entry.NpcIndex);
					}
					Entries.RemoveAt(i);
					continue;
				}

				// Dynamic tracking for bosses and mobile targets!
				if (entry.VisualOnly)
				{
					entry.AnchorCenter = npc.Center;
				}

				// Separation burst sound and dust when cut reaches hold frame
				foreach (var cut in entry.Cuts)
				{
					if (cut.Hold > 0 && entry.Timer - cut.Birth == cut.Hold)
					{
						SeparationBurst(entry, in cut);
					}
				}
			}
		}

		public override void Unload()
		{
			ReassembleFactor = 0f;
			DisposeAllSnapshots();
			Entries.Clear();
		}
	}
}
