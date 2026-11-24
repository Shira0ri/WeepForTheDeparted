using System;
using System.Collections.Generic;
using System.Reflection;
using InnoVault;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.GlobalNPCs;
using WeepForTheDeparted.Content.Shaders;

namespace WeepForTheDeparted.Common.Visuals.Dismember
{
	public class NihilityDismemberNPC : GlobalNPC
	{
		public override bool InstancePerEntity => false;

		private static readonly List<VertexPositionColorTexture> vertexScratch = new List<VertexPositionColorTexture>();
		private static Vector4[] cutLineParams = new Vector4[16];
		private static Vector4[] cutGlowParams = new Vector4[16];

		public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			// Fallback PreDraw if VaultHook did not catch it
			return !TryDrawDismembered(npc, spriteBatch);
		}

		public static bool CanTakeOverDraw(NPC npc) => TryGetDrawData(npc, out _, out _, out _);

		public static bool TryDrawDismembered(NPC npc, SpriteBatch spriteBatch)
		{
			if (!TryGetDrawData(npc, out DismemberEntry entry, out RenderTarget2D rt, out Effect fx))
				return false;

			spriteBatch.End();
			DrawPieces(entry, rt, fx);
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
				Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
				null, Main.GameViewMatrix.TransformationMatrix);
			return true;
		}

		private static bool TryGetDrawData(NPC npc, out DismemberEntry entry, out RenderTarget2D rt, out Effect fx)
		{
			entry = NihilityDismember.GetEntry(npc.whoAmI);
			rt = null;
			fx = null;
			if (entry == null || entry.NpcType != npc.type || !entry.Captured || entry.SnapWidth <= 0)
				return false;

			if (!NihilityDismember.SnapRTs.TryGetValue(npc.whoAmI, out rt) || rt == null || rt.IsDisposed)
				return false;

			fx = ShaderLoader.DismemberShader;
			return fx != null;
		}

		private static void DrawPieces(DismemberEntry entry, RenderTarget2D rt, Effect fx)
		{
			GraphicsDevice gd = Main.instance.GraphicsDevice;

			BlendState prevBlend = gd.BlendState;
			RasterizerState prevRaster = gd.RasterizerState;
			DepthStencilState prevDepth = gd.DepthStencilState;

			gd.BlendState = BlendState.AlphaBlend;
			gd.RasterizerState = RasterizerState.CullNone;
			gd.DepthStencilState = DepthStencilState.None;

			BuildVertices(entry);

			int batchCapacity = EnsureCutParamBuffers(fx);
			if (vertexScratch.Count >= 3 && entry.Cuts.Count > 0 && batchCapacity > 0)
			{
				SetCommonShaderParams(entry, rt, fx);
				VertexPositionColorTexture[] verts = vertexScratch.ToArray();

				for (int start = 0; start < entry.Cuts.Count; start += batchCapacity)
				{
					SetCutBatchParams(entry, fx, start, batchCapacity);
					foreach (EffectPass pass in fx.CurrentTechnique.Passes)
					{
						pass.Apply();
						gd.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, verts.Length / 3);
					}
				}
			}

			gd.BlendState = prevBlend;
			gd.RasterizerState = prevRaster;
			gd.DepthStencilState = prevDepth;
		}

		private static int EnsureCutParamBuffers(Effect fx)
		{
			int lineCapacity = fx.Parameters["uCutLine"]?.Elements.Count ?? 0;
			int glowCapacity = fx.Parameters["uCutGlow"]?.Elements.Count ?? 0;
			int capacity = Math.Min(lineCapacity, glowCapacity);
			if (capacity <= 0)
				capacity = 16;

			if (cutLineParams.Length != capacity)
			{
				cutLineParams = new Vector4[capacity];
				cutGlowParams = new Vector4[capacity];
			}
			return capacity;
		}

		private static void SetCommonShaderParams(DismemberEntry entry, RenderTarget2D rt, Effect fx)
		{
			fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
			fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			fx.Parameters["uSnapSize"]?.SetValue(new Vector2(entry.SnapWidth, entry.SnapHeight));

			float coldIn = NihilityDismember.SeparationCurve(entry.Timer - entry.Cuts[0].Birth, entry.Cuts[0].Hold);
			fx.Parameters["uDesat"]?.SetValue(0.35f * coldIn);
			fx.Parameters["uDim"]?.SetValue(1f - 0.15f * coldIn);
			fx.Parameters["uColHot"]?.SetValue(new Vector3(1.85f, 1.62f, 1.30f)); // White-hot incision edge
			fx.Parameters["uColBright"]?.SetValue(new Vector3(1.55f, 0.28f, 0.14f)); // Scarlet crimson glow
			fx.Parameters["uSnapTex"]?.SetValue(rt);
		}

		private static void SetCutBatchParams(DismemberEntry entry, Effect fx, int start, int capacity)
		{
			int count = Math.Min(entry.Cuts.Count - start, capacity);
			for (int i = 0; i < count; i++)
			{
				DismemberCut cut = entry.Cuts[start + i];
				cutLineParams[i] = new Vector4(cut.PointLocal.X, cut.PointLocal.Y, cut.Normal.X, cut.Normal.Y);
				cutGlowParams[i] = new Vector4(GlowStrength(entry, in cut), GlowHalfWidth(entry, in cut), 0f, 0f);
			}

			fx.Parameters["uCutLine"]?.SetValue(cutLineParams);
			fx.Parameters["uCutGlow"]?.SetValue(cutGlowParams);
			fx.Parameters["uCutCount"]?.SetValue(count);
			fx.Parameters["uDrawBase"]?.SetValue(start == 0 ? 1f : 0f);
		}

		private static float GlowStrength(DismemberEntry entry, in DismemberCut cut)
		{
			int age = entry.Timer - cut.Birth;
			if (age < 0)
				return 0f;

			float strength;
			if (age <= 2)
			{
				strength = 1.35f;
			}
			else if (age < cut.Hold)
			{
				float breath = 0.5f + 0.5f * (float)Math.Sin(age * 0.55f - MathHelper.PiOver2);
				strength = 0.55f + 0.30f * breath;
			}
			else
			{
				float t = NihilityDismember.SeparationCurve(age, cut.Hold);
				strength = MathHelper.Lerp(1.15f, 0.72f, t) + 0.06f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5.1f + cut.Birth);
			}
			return strength * entry.FadeAlpha;
		}

		private static float GlowHalfWidth(DismemberEntry entry, in DismemberCut cut)
		{
			int age = Math.Max(entry.Timer - cut.Birth, 0);
			float snapScale = Math.Max(Math.Min(entry.SnapWidth, entry.SnapHeight) / 160f, 0.6f);
			if (age < cut.Hold)
				return (7f - 3f * age / (float)cut.Hold) * snapScale;

			return (4f - 1.6f * NihilityDismember.SeparationCurve(age, cut.Hold)) * snapScale;
		}

		private static void BuildVertices(DismemberEntry entry)
		{
			vertexScratch.Clear();
			float invW = 1f / entry.SnapWidth;
			float invH = 1f / entry.SnapHeight;

			Color col = Color.White * entry.FadeAlpha;
			Vector2 snapHalf = new Vector2(entry.SnapWidth * 0.5f, entry.SnapHeight * 0.5f);

			foreach (DismemberPiece piece in entry.Pieces)
			{
				if (piece.Verts.Length < 3)
					continue;

				NihilityDismember.GetPieceMotion(entry, piece, out Vector2 offset, out float rot);
				float sin = (float)Math.Sin(rot);
				float cos = (float)Math.Cos(rot);

				int vertCount = piece.Verts.Length;
				Vector2[] world = new Vector2[vertCount];
				Vector2[] uvs = new Vector2[vertCount];

				for (int i = 0; i < vertCount; i++)
				{
					Vector2 rel = piece.Verts[i] - piece.Centroid;
					Vector2 rotated = new Vector2(rel.X * cos - rel.Y * sin, rel.X * sin + rel.Y * cos);
					world[i] = entry.AnchorCenter + piece.Centroid + rotated + offset;
					uvs[i] = new Vector2((piece.Verts[i].X + snapHalf.X) * invW, (piece.Verts[i].Y + snapHalf.Y) * invH);
				}

				for (int i = 1; i < vertCount - 1; i++)
				{
					vertexScratch.Add(new VertexPositionColorTexture(new Vector3(world[0], 0f), col, uvs[0]));
					vertexScratch.Add(new VertexPositionColorTexture(new Vector3(world[i], 0f), col, uvs[i]));
					vertexScratch.Add(new VertexPositionColorTexture(new Vector3(world[i + 1], 0f), col, uvs[i + 1]));
				}
			}
		}
	}

	/// <summary>
	/// High-priority hook into NPCLoader.PreDraw and NPCLoader.PostDraw.
	/// Guarantees that boss drawing (including custom ModNPC.PreDraw and ModNPC.PostDraw
	/// for bosses like Calamity bosses, Moon Lord, etc.) is intercepted and suppressed
	/// in favor of the severed dismemberment rendering.
	/// </summary>
	internal sealed class NihilityDismemberDrawHook : ModSystem
	{
		private delegate bool PreDrawDelegate(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor);
		private delegate bool PreDrawHookDelegate(PreDrawDelegate orig, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor);
		private delegate void PostDrawDelegate(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor);
		private delegate void PostDrawHookDelegate(PostDrawDelegate orig, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor);

		private const int HookPriority = VaultHook.DefaultHookPriority + 100;
		private static readonly List<(MethodBase Method, Delegate Handler)> mountedHooks = new List<(MethodBase, Delegate)>();

		public override void Load()
		{
			if (Main.dedServ)
				return;

			MethodInfo preDraw = GetDrawMethod(nameof(NPCLoader.PreDraw));
			if (preDraw != null)
			{
				PreDrawHookDelegate handler = PreDrawHook;
				VaultHook.Add(preDraw, handler, HookPriority, "WeepForTheDeparted.NihilityDismember.PreDraw");
				mountedHooks.Add((preDraw, handler));
			}

			MethodInfo postDraw = GetDrawMethod(nameof(NPCLoader.PostDraw));
			if (postDraw != null)
			{
				PostDrawHookDelegate handler = PostDrawHook;
				VaultHook.Add(postDraw, handler, HookPriority, "WeepForTheDeparted.NihilityDismember.PostDraw");
				mountedHooks.Add((postDraw, handler));
			}
		}

		public override void Unload()
		{
			foreach (var (method, handler) in mountedHooks)
			{
				if (!VaultHook.Hooks.TryRemove((method, handler), out var hook))
					continue;

				try
				{
					if (hook.IsApplied)
						hook.Undo();
					hook.Dispose();
				}
				catch
				{
				}
			}
			mountedHooks.Clear();
		}

		private static MethodInfo GetDrawMethod(string name)
			=> typeof(NPCLoader).GetMethod(name, BindingFlags.Public | BindingFlags.Static, null,
				new Type[] { typeof(NPC), typeof(SpriteBatch), typeof(Vector2), typeof(Color) }, null);

		private static bool PreDrawHook(PreDrawDelegate orig, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (NihilityDismemberNPC.TryDrawDismembered(npc, spriteBatch))
				return false;
			return orig(npc, spriteBatch, screenPos, drawColor);
		}

		private static void PostDrawHook(PostDrawDelegate orig, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (NihilityDismemberNPC.CanTakeOverDraw(npc))
			{
				// While dismembered, suppress boss intact glowmasks/wings.
				// Crimson Knot rendering is centrally handled by CrimsonKnotRenderHandle.
				return;
			}
			orig(npc, spriteBatch, screenPos, drawColor);
		}
	}
}
