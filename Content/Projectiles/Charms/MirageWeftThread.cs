using System;
using System.Collections.Generic;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.GlobalNPCs;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.Systems;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles.Charms
{
	public class MirageWeftThread : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		private const float ThreadHitWidth = 16f;
		private const float HitboxPadding = 16f;
		private const int StripSamples = 18;

		public Player Owner => Main.player[Projectile.owner];

		private readonly struct ThreadSegment
		{
			public readonly Vector2 Start;
			public readonly Vector2 End;
			public readonly float Alpha;

			public ThreadSegment(Vector2 start, Vector2 end, float alpha)
			{
				Start = start;
				End = end;
				Alpha = alpha;
			}
		}

		private readonly List<NaughtClone> activeClones = new List<NaughtClone>(16);
		private readonly List<ThreadSegment> segments = new List<ThreadSegment>(16);
		private Rectangle segmentBounds;

		// Reusable strip buffers to avoid per-frame allocations
		private readonly VertexStrip outerStrip = new VertexStrip();
		private readonly VertexStrip coreStrip = new VertexStrip();
		private readonly Vector2[] samplePositions = new Vector2[StripSamples];
		private readonly float[] sampleRotations = new float[StripSamples];
		private float activeDrawAlpha = 1f;

		// Pre-hit debuff snapshots for isolating Mirage Weft debuff applications
		private static int pendingProjWhoAmI = -1;
		private static int pendingOwnerWhoAmI = -1;
		private static int pendingTargetWhoAmI = -1;
		private static int pendingAnchorWhoAmI = -1;
		private static readonly Dictionary<int, int> pendingTargetPreDebuffs = new Dictionary<int, int>();
		private static readonly Dictionary<int, int> pendingAnchorPreDebuffs = new Dictionary<int, int>();

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4800;
		}

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 2;
			Projectile.DamageType = WeepCompatibility.TrueMelee;
			Projectile.noEnchantments = false;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = WeepBalance.Active.MirageWeftHitCooldownTicks;
			Projectile.netImportant = true;
		}

		public override bool ShouldUpdatePosition() => false;

		public override bool? CanDamage()
		{
			if (WorldFreezeSystem.IsActive || segments.Count == 0)
				return false;

			return null;
		}

		public override void AI()
		{
			if (Owner == null || !Owner.active || Owner.dead || Owner.ghost)
			{
				Projectile.Kill();
				return;
			}

			var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
			if (naughtPlayer == null || !naughtPlayer.HasMirageWeft)
			{
				Projectile.Kill();
				return;
			}

			RebuildChainSegments();
			if (segments.Count == 0)
			{
				Projectile.Kill();
				return;
			}

			var profile = WeepBalance.Active;
			Projectile.timeLeft = 2;
			Projectile.damage = naughtPlayer.GetNaughtDamage(profile.MirageWeftDamageMult);
			Projectile.CritChance = naughtPlayer.GetNaughtCritChance();
			Projectile.knockBack = 0f;
			Projectile.localNPCHitCooldown = profile.MirageWeftHitCooldownTicks;

			UpdateSegmentBoundingBox();
		}

		private void RebuildChainSegments()
		{
			activeClones.Clear();
			segments.Clear();

			int cloneType = ModContent.ProjectileType<NaughtClone>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == Projectile.owner && p.type == cloneType && p.ModProjectile is NaughtClone clone)
				{
					activeClones.Add(clone);
				}
			}

			if (activeClones.Count == 0)
				return;

			activeClones.Sort(static (a, b) =>
			{
				int seqCmp = a.SpawnSequence.CompareTo(b.SpawnSequence);
				if (seqCmp != 0)
					return seqCmp;

				int ageCmp = b.Age.CompareTo(a.Age);
				if (ageCmp != 0)
					return ageCmp;

				return a.Projectile.whoAmI.CompareTo(b.Projectile.whoAmI);
			});

			for (int i = 0; i < activeClones.Count - 1; i++)
			{
				NaughtClone from = activeClones[i];
				NaughtClone to = activeClones[i + 1];
				float alpha = MathF.Min(GetCloneAlpha(from), GetCloneAlpha(to));
				segments.Add(new ThreadSegment(from.Projectile.Center, to.Projectile.Center, alpha));
			}

			NaughtClone latest = activeClones[activeClones.Count - 1];
			segments.Add(new ThreadSegment(latest.Projectile.Center, Owner.MountedCenter, GetCloneAlpha(latest)));
		}

		private static float GetCloneAlpha(NaughtClone clone)
		{
			float fadeIn = MathHelper.Clamp(clone.Age / 10f, 0f, 1f);
			float fadeOut = MathHelper.Clamp(clone.Projectile.timeLeft / 25f, 0f, 1f);
			return fadeIn * fadeOut;
		}

		private void UpdateSegmentBoundingBox()
		{
			float minX = float.MaxValue;
			float minY = float.MaxValue;
			float maxX = float.MinValue;
			float maxY = float.MinValue;

			for (int i = 0; i < segments.Count; i++)
			{
				Vector2 a = segments[i].Start;
				Vector2 b = segments[i].End;

				if (a.X < minX) minX = a.X;
				if (b.X < minX) minX = b.X;
				if (a.Y < minY) minY = a.Y;
				if (b.Y < minY) minY = b.Y;

				if (a.X > maxX) maxX = a.X;
				if (b.X > maxX) maxX = b.X;
				if (a.Y > maxY) maxY = a.Y;
				if (b.Y > maxY) maxY = b.Y;
			}

			int x = (int)MathF.Floor(minX - HitboxPadding);
			int y = (int)MathF.Floor(minY - HitboxPadding);
			int width = Math.Max(16, (int)MathF.Ceiling(maxX + HitboxPadding) - x);
			int height = Math.Max(16, (int)MathF.Ceiling(maxY + HitboxPadding) - y);

			segmentBounds = new Rectangle(x, y, width, height);
			Projectile.position = new Vector2(x, y);
			Projectile.width = width;
			Projectile.height = height;
		}

		public override void ModifyDamageHitbox(ref Rectangle hitbox)
		{
			if (segments.Count > 0)
			{
				hitbox = segmentBounds;
			}
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (segments.Count == 0 || !projHitbox.Intersects(targetHitbox))
				return false;

			for (int i = 0; i < segments.Count; i++)
			{
				float collisionPoint = 0f;
				if (Collision.CheckAABBvLineCollision(
					targetHitbox.TopLeft(),
					targetHitbox.Size(),
					segments[i].Start,
					segments[i].End,
					ThreadHitWidth,
					ref collisionPoint))
				{
					return true;
				}
			}

			return false;
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (Owner != null && Owner.active)
			{
				modifiers.HitDirectionOverride = target.Center.X >= Owner.MountedCenter.X ? 1 : -1;
			}

			BeginPendingDebuffCheck(Projectile, target);
		}

		public static void ResetPendingDebuffCheck()
		{
			pendingProjWhoAmI = -1;
			pendingOwnerWhoAmI = -1;
			pendingTargetWhoAmI = -1;
			pendingAnchorWhoAmI = -1;
			pendingTargetPreDebuffs.Clear();
			pendingAnchorPreDebuffs.Clear();
		}

		private static void BeginPendingDebuffCheck(Projectile proj, NPC target)
		{
			FlushPendingDebuffCheck();

			if (Main.netMode == NetmodeID.Server || proj == null || target == null || !target.active)
				return;

			if (Main.netMode == NetmodeID.MultiplayerClient && proj.owner != Main.myPlayer)
				return;

			pendingProjWhoAmI = proj.whoAmI;
			pendingOwnerWhoAmI = proj.owner;
			pendingTargetWhoAmI = target.whoAmI;
			SnapshotDebuffs(target, pendingTargetPreDebuffs);

			NPC anchor = CrimsonKnotGlobalNPC.ResolveKnotAnchor(target);
			if (anchor != null && anchor.active && anchor.whoAmI != target.whoAmI)
			{
				pendingAnchorWhoAmI = anchor.whoAmI;
				SnapshotDebuffs(anchor, pendingAnchorPreDebuffs);
			}
			else
			{
				pendingAnchorWhoAmI = -1;
				pendingAnchorPreDebuffs.Clear();
			}
		}

		public static void ProcessPendingDebuffCheck(Projectile proj, NPC hitNpc)
		{
			if (proj == null || hitNpc == null || pendingProjWhoAmI != proj.whoAmI || pendingTargetWhoAmI != hitNpc.whoAmI)
				return;

			InspectPendingDebuffCheck();
		}

		public static void FlushPendingDebuffCheck()
		{
			if (pendingProjWhoAmI < 0)
				return;

			InspectPendingDebuffCheck();
			ResetPendingDebuffCheck();
		}

		private static void InspectPendingDebuffCheck()
		{
			if (pendingOwnerWhoAmI < 0 || pendingOwnerWhoAmI >= Main.maxPlayers)
			{
				ResetPendingDebuffCheck();
				return;
			}

			Player owner = Main.player[pendingOwnerWhoAmI];
			if (owner == null || !owner.active || owner.dead)
			{
				ResetPendingDebuffCheck();
				return;
			}

			if (!owner.TryGetModPlayer<NaughtPlayer>(out var naughtPlayer))
			{
				ResetPendingDebuffCheck();
				return;
			}

			NPC target = (pendingTargetWhoAmI >= 0 && pendingTargetWhoAmI < Main.maxNPCs) ? Main.npc[pendingTargetWhoAmI] : null;
			if (target != null && target.active)
			{
				InspectNpcDebuffs(naughtPlayer, target, target, pendingTargetPreDebuffs);
			}

			if (pendingAnchorWhoAmI >= 0 && pendingAnchorWhoAmI < Main.maxNPCs)
			{
				NPC anchor = Main.npc[pendingAnchorWhoAmI];
				if (anchor != null && anchor.active)
				{
					InspectNpcDebuffs(naughtPlayer, anchor, target, pendingAnchorPreDebuffs);
				}
			}
		}

		private static void SnapshotDebuffs(NPC npc, Dictionary<int, int> dest)
		{
			dest.Clear();
			for (int b = 0; b < NPC.maxBuffs; b++)
			{
				int bType = npc.buffType[b];
				int bTime = npc.buffTime[b];
				if (bType > 0 && bType < Main.debuff.Length && Main.debuff[bType] && bTime > 0)
				{
					dest[bType] = bTime;
				}
			}
		}

		private static void InspectNpcDebuffs(NaughtPlayer naughtPlayer, NPC debuffNpc, NPC hitTarget, Dictionary<int, int> preDebuffs)
		{
			for (int b = 0; b < NPC.maxBuffs; b++)
			{
				int bType = debuffNpc.buffType[b];
				int bTime = debuffNpc.buffTime[b];
				if (bType <= 0 || bType >= Main.debuff.Length || !Main.debuff[bType] || bTime <= 0)
					continue;

				bool isNew = !preDebuffs.TryGetValue(bType, out int prevTime);
				bool isRefreshed = !isNew && bTime > prevTime + 60;
				if (isNew || isRefreshed)
				{
					naughtPlayer.ProcessMirageWeftDebuff(bType, debuffNpc, hitTarget);
				}

				preDebuffs[bType] = bTime;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Main.dedServ)
				return;

			for (int s = 0; s < 5; s++)
			{
				Vector2 vel = Main.rand.NextVector2Circular(3.8f, 3.8f);
				Color col = s % 2 == 0 ? new Color(238, 26, 56) : new Color(185, 45, 255);
				PRTLoader.NewParticle<VoidSparkPRT>(target.Center, vel, col, 0.8f);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ || Owner == null || !Owner.active || segments.Count == 0)
				return false;

			MiscShaderData stripShader = null;
			if (!GameShaders.Misc.TryGetValue("MagicMissile", out stripShader))
			{
				GameShaders.Misc.TryGetValue("RainbowRod", out stripShader);
			}

			if (stripShader == null)
				return false;

			SpriteBatchState sbState = Main.spriteBatch.Capture();
			Main.spriteBatch.End();

			GraphicsDevice device = Main.graphics.GraphicsDevice;
			BlendState origBlend = device.BlendState;
			RasterizerState origRaster = device.RasterizerState;
			DepthStencilState origDepth = device.DepthStencilState;

			device.BlendState = BlendState.Additive;
			device.RasterizerState = RasterizerState.CullNone;
			device.DepthStencilState = DepthStencilState.None;

			stripShader.UseSaturation(-2.4f);
			stripShader.UseOpacity(2.2f);
			stripShader.Apply();

			float time = Main.GlobalTimeWrappedHourly * 7.5f;
			for (int i = 0; i < segments.Count; i++)
			{
				ThreadSegment seg = segments[i];
				if (seg.Alpha <= 0.01f)
					continue;

				Vector2 diff = seg.End - seg.Start;
				float len = diff.Length();
				if (len < 2f)
					continue;

				activeDrawAlpha = seg.Alpha;
				BuildSegmentSamples(seg.Start, seg.End, diff, len, time + i * 1.15f);

				outerStrip.PrepareStrip(samplePositions, sampleRotations, OuterStripColor, OuterStripWidth, -Main.screenPosition, includeBacksides: true);
				outerStrip.DrawTrail();

				coreStrip.PrepareStrip(samplePositions, sampleRotations, CoreStripColor, CoreStripWidth, -Main.screenPosition, includeBacksides: true);
				coreStrip.DrawTrail();
			}

			Main.pixelShader.CurrentTechnique.Passes[0].Apply();

			device.BlendState = origBlend;
			device.RasterizerState = origRaster;
			device.DepthStencilState = origDepth;

			DrawAnchorFlares(sbState);

			Main.spriteBatch.Begin(in sbState);
			return false;
		}

		private void BuildSegmentSamples(Vector2 start, Vector2 end, Vector2 diff, float length, float phase)
		{
			Vector2 tangent = diff / length;
			Vector2 normal = new Vector2(-tangent.Y, tangent.X);
			float amp = MathHelper.Clamp(length * 0.012f, 1.2f, 3.2f);

			for (int i = 0; i < StripSamples; i++)
			{
				float u = i / (float)(StripSamples - 1);
				float envelope = MathF.Sin(u * MathHelper.Pi);
				float ripple = MathF.Sin(u * MathHelper.TwoPi * 2.5f - phase) * amp * envelope;
				samplePositions[i] = Vector2.Lerp(start, end, u) + normal * ripple;
			}

			for (int i = 0; i < StripSamples; i++)
			{
				Vector2 dir = i < StripSamples - 1
					? samplePositions[i + 1] - samplePositions[i]
					: samplePositions[i] - samplePositions[i - 1];
				sampleRotations[i] = dir.LengthSquared() > 0.0001f ? dir.ToRotation() : tangent.ToRotation();
			}
		}

		private Color OuterStripColor(float progressOnStrip)
		{
			float wave = 0.5f + 0.5f * MathF.Sin(progressOnStrip * MathHelper.TwoPi * 2f - Main.GlobalTimeWrappedHourly * 8f);
			Color violet = new Color(175, 45, 245);
			Color crimson = new Color(240, 26, 62);
			return Color.Lerp(violet, crimson, wave) * (0.75f * activeDrawAlpha);
		}

		private float OuterStripWidth(float progressOnStrip)
		{
			float endTaper = 0.35f + 0.65f * MathF.Sin(progressOnStrip * MathHelper.Pi);
			return 10f * endTaper;
		}

		private Color CoreStripColor(float progressOnStrip)
		{
			return new Color(255, 225, 242) * (0.95f * activeDrawAlpha);
		}

		private float CoreStripWidth(float progressOnStrip)
		{
			float endTaper = 0.45f + 0.55f * MathF.Sin(progressOnStrip * MathHelper.Pi);
			return 3.2f * endTaper;
		}

		private void DrawAnchorFlares(in SpriteBatchState sbState)
		{
			Texture2D glowTex = TextureAssets.Extra[ExtrasID.ThePerfectGlow]?.Value;
			Texture2D flareTex = TextureAssets.Extra[ExtrasID.SharpTears]?.Value;
			if (glowTex == null && flareTex == null)
				return;

			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, sbState.Matrix);

			for (int i = 0; i < activeClones.Count; i++)
			{
				NaughtClone clone = activeClones[i];
				float alpha = GetCloneAlpha(clone);
				if (alpha <= 0.01f)
					continue;

				DrawSingleAnchor(glowTex, flareTex, clone.Projectile.Center - Main.screenPosition, alpha * 0.85f);
			}

			DrawSingleAnchor(glowTex, flareTex, Owner.MountedCenter - Main.screenPosition, 0.7f);

			Main.spriteBatch.End();
		}

		private static void DrawSingleAnchor(Texture2D glowTex, Texture2D flareTex, Vector2 screenPos, float alpha)
		{
			if (glowTex != null)
			{
				Vector2 glowOrigin = glowTex.Size() * 0.5f;
				Main.spriteBatch.Draw(glowTex, screenPos, null, new Color(185, 45, 255, 0) * (0.55f * alpha), 0f, glowOrigin, 0.24f, SpriteEffects.None, 0f);
			}

			if (flareTex != null)
			{
				Vector2 flareOrigin = flareTex.Size() * 0.5f;
				Color flareCol = new Color(255, 190, 215, 0) * (0.65f * alpha);
				Main.spriteBatch.Draw(flareTex, screenPos, null, flareCol, 0f, flareOrigin, new Vector2(0.45f, 0.14f), SpriteEffects.None, 0f);
				Main.spriteBatch.Draw(flareTex, screenPos, null, flareCol, MathHelper.PiOver2, flareOrigin, new Vector2(0.45f, 0.14f), SpriteEffects.None, 0f);
			}
		}
	}
}
