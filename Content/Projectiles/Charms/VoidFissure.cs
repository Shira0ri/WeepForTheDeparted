using System;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.UI.NihilityLedger;
using WeepForTheDeparted.Common.Visuals;
using WeepForTheDeparted.Content.Shaders;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles.Charms
{
	public class VoidFissure : ModProjectile
	{
		public const int PortalLifetime = 540; // 9.0 seconds @ 60 ticks/sec
		public const float PullRadius = 220f;
		public const float DamageRadius = 115f;
		public const float CoreHoldRadius = 26f;
		public const float QuadHalfSize = 155f;
		public const int HitCooldownTicks = 24; // 2.5 hits/sec (22 hits over 9s)
		public const float DamageMultiplier = 0.45f;
		public const float SlowRatio = 0.35f;

		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		private static readonly uint[] lastPullTick = new uint[Main.maxNPCs];
		private static readonly uint[] lastDamageTick = new uint[Main.maxNPCs];

		private Vector2 anchorCenter;
		private float portalSeed;
		private float portalRotation;
		private int age;
		private bool initialized;

		public Player Owner => Main.player[Projectile.owner];

		public override void SetDefaults()
		{
			Projectile.width = 32;
			Projectile.height = 32;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = PortalLifetime;
			Projectile.DamageType = WeepCompatibility.TrueMelee;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = HitCooldownTicks;
		}

		public override bool? CanDamage() => false;
		public override bool ShouldUpdatePosition() => false;

		public void Init(Vector2 portalCenter, float seed = 0f, float rotation = 0f)
		{
			anchorCenter = portalCenter;
			Projectile.Center = portalCenter;
			portalSeed = seed > 0f ? seed : Main.rand.NextFloat(0.25f, 12f);
			portalRotation = rotation;
			initialized = true;
		}

		public override void AI()
		{
			if (!initialized)
			{
				Init(Projectile.Center);
			}
			else
			{
				Projectile.Center = anchorCenter;
			}

			age++;

			NaughtPlayer naughtPlayer = Owner != null && Owner.active ? Owner.GetModPlayer<NaughtPlayer>() : null;
			bool canStrikeOwner = Main.netMode != NetmodeID.MultiplayerClient || Main.myPlayer == Projectile.owner;

			float pullRadiusSq = PullRadius * PullRadius;
			float dmgRadiusSq = DamageRadius * DamageRadius;
			uint currentTick = Main.GameUpdateCount;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly)
					continue;

				bool isDummy = npc.type == NPCID.TargetDummy;
				if (npc.dontTakeDamage && !isDummy)
					continue;

				Vector2 closestPoint = npc.Hitbox.ClosestPointInRect(anchorCenter);
				float closestDistSq = Vector2.DistanceSquared(anchorCenter, closestPoint);
				float centerDistSq = Vector2.DistanceSquared(anchorCenter, npc.Center);

				// 1. Vortex Crowd Control & Movement Slow (strictly non-boss, non-town, non-immortal, worm-safe)
				if (centerDistSq <= pullRadiusSq && IsValidCrowdControlTarget(npc))
				{
					// Prevent multiple overlapping portals from multiplying pull velocity
					if (lastPullTick[i] != currentTick)
					{
						lastPullTick[i] = currentTick;
						ApplyVortexPull(npc, (float)Math.Sqrt(centerDistSq));
					}

					if (closestDistSq <= dmgRadiusSq)
					{
						ApplyMovementSlow(npc);
					}
				}

				// 2. Continuous True Melee Damage (shared per-NPC cooldown prevents runaway overlap DPS)
				if (canStrikeOwner && closestDistSq <= dmgRadiusSq)
				{
					if (lastDamageTick[i] == 0 || currentTick - lastDamageTick[i] >= HitCooldownTicks)
					{
						lastDamageTick[i] = currentTick;
						StrikePortalTarget(npc, naughtPlayer, closestPoint);
					}
				}
			}

			// Ambient inward-spiraling void sparks
			if (!Main.dedServ && Main.rand.NextBool(3))
			{
				float angle = Main.rand.NextFloat(MathHelper.TwoPi);
				float spawnDist = Main.rand.NextFloat(DamageRadius * 0.55f, PullRadius * 0.85f);
				Vector2 offset = angle.ToRotationVector2() * spawnDist;
				Vector2 inwardVel = (-offset).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1.8f, 3.6f)
					+ new Vector2(-offset.Y, offset.X).SafeNormalize(Vector2.Zero) * 1.1f;
				Color col = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(238, 26, 56);
				PRTLoader.NewParticle<VoidSparkPRT>(anchorCenter + offset, inwardVel, col, 0.65f);
			}

			Lighting.AddLight(anchorCenter, new Vector3(0.38f, 0.06f, 0.30f));
		}

		private static bool IsValidCrowdControlTarget(NPC npc)
		{
			if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || npc.townNPC || npc.isLikeATownNPC || npc.immortal || npc.type == NPCID.TargetDummy || npc.lifeMax <= 5)
				return false;

			if (npc.knockBackResist <= 0f)
				return false;

			// Multi-segment worm safety: never pull body/tail segments or any segment of a boss worm
			if (npc.realLife >= 0)
			{
				if (npc.realLife != npc.whoAmI)
					return false;

				NPC head = Main.npc[npc.realLife];
				if (!head.active || head.boss || NPCID.Sets.ShouldBeCountedAsBoss[head.type])
					return false;
			}

			return true;
		}

		private void ApplyVortexPull(NPC npc, float dist)
		{
			if (dist > CoreHoldRadius)
			{
				Vector2 dir = (anchorCenter - npc.Center) / dist;
				Vector2 tangent = new Vector2(-dir.Y, dir.X);
				float proximity = 1f - MathHelper.Clamp(dist / PullRadius, 0f, 1f);
				float pullSpeed = MathHelper.Lerp(1.6f, 4.6f, proximity);
				float swirlSpeed = 1.25f * proximity;
				float kbFactor = MathHelper.Clamp(npc.knockBackResist, 0.35f, 1f);

				Vector2 desiredVel = dir * (pullSpeed * kbFactor) + tangent * swirlSpeed;
				npc.velocity = Vector2.Lerp(npc.velocity, desiredVel, 0.14f);
			}
			else
			{
				npc.velocity *= 0.82f;
			}
		}

		private static void ApplyMovementSlow(NPC npc)
		{
			Vector2 moveDelta = npc.position - npc.oldPosition;
			if (moveDelta.LengthSquared() < 1600f)
			{
				npc.position.X -= moveDelta.X * SlowRatio;
				if (npc.noGravity)
				{
					npc.position.Y -= moveDelta.Y * SlowRatio;
				}
			}
		}

		private void StrikePortalTarget(NPC npc, NaughtPlayer naughtPlayer, Vector2 hitPos)
		{
			int strikeDamage = naughtPlayer != null
				? naughtPlayer.GetNaughtDamage(DamageMultiplier)
				: Math.Max(1, (int)MathF.Round(Projectile.damage * DamageMultiplier));

			if (naughtPlayer != null)
			{
				Projectile.CritChance = naughtPlayer.GetNaughtCritChance();
				naughtPlayer.StrikeNPC(npc, strikeDamage, hitDirection: 0, crit: null, knockBack: 0f, sourceProj: Projectile, isUltimate: true);
			}
			else
			{
				npc.SimpleStrikeNPC(strikeDamage, 0, false, 0f, WeepCompatibility.TrueMelee);
			}

			// BuffID.Slow has no vanilla NPC speed penalty, but is preserved for debuff-synergy triggers (AbyssGrip / E1)
			npc.AddBuff(BuffID.Slow, 120);

			if (!Main.dedServ)
			{
				PRTLoader.NewParticle<VoidSparkPRT>(hitPos, Main.rand.NextVector2Circular(3f, 3f), new Color(238, 26, 56), 0.72f);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 drawCenter = initialized ? anchorCenter : Projectile.Center;

			float openIn = age == 0 ? 0.24f : MathHelper.Clamp(age / 18f, 0f, 1f);
			openIn = openIn * openIn * (3f - 2f * openIn);
			float closeOut = MathHelper.Clamp(Projectile.timeLeft / 22f, 0f, 1f);
			float openScale = openIn * closeOut;
			float fade = (age == 0 ? 0.6f : MathHelper.Clamp(age / 10f, 0f, 1f)) * closeOut;

			if (openScale <= 0.01f || fade <= 0.01f)
				return false;

			SpriteBatch sb = Main.spriteBatch;
			SpriteBatchState sbState = sb.Capture();

			if (!DrawPortalShaderQuad(sb, in sbState, drawCenter, openScale, fade))
			{
				Vector2 screenCenter = drawCenter - Main.screenPosition;
				NihilityLedgerTheme.DrawCircle(sb, screenCenter, DamageRadius * openScale, new Color(185, 45, 255) * (0.65f * fade), 2.5f);
				NihilityLedgerTheme.DrawCircle(sb, screenCenter, DamageRadius * 0.65f * openScale, new Color(238, 26, 56) * (0.8f * fade), 2f);
			}

			return false;
		}

		private bool DrawPortalShaderQuad(SpriteBatch sb, in SpriteBatchState sbState, Vector2 worldCenter, float openScale, float fade)
		{
			Effect fx = ShaderLoader.VoidHorizonShader;
			Texture2D noise = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/RiftNoise", AssetRequestMode.ImmediateLoad).Value;
			if (fx == null || noise == null)
				return false;

			GraphicsDevice device = Main.graphics.GraphicsDevice;
			sb.End();

			BlendState origBlend = device.BlendState;
			RasterizerState origRaster = device.RasterizerState;
			DepthStencilState origDepth = device.DepthStencilState;

			device.BlendState = BlendState.AlphaBlend;
			device.RasterizerState = RasterizerState.CullNone;
			device.DepthStencilState = DepthStencilState.None;

			Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -4000f, 4000f);
			Matrix view = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0)) * Main.GameViewMatrix.TransformationMatrix;
			Matrix transformMatrix = view * projection;

			fx.Parameters["transformMatrix"]?.SetValue(transformMatrix);
			fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			fx.Parameters["uOpen"]?.SetValue(openScale);
			fx.Parameters["uFade"]?.SetValue(fade);
			fx.Parameters["uAspect"]?.SetValue(0.78f);
			fx.Parameters["uSeed"]?.SetValue(portalSeed);
			fx.Parameters["uNoiseTex"]?.SetValue(noise);

			float cosR = (float)Math.Cos(portalRotation);
			float sinR = (float)Math.Sin(portalRotation);
			Vector2 right = new Vector2(cosR, sinR) * QuadHalfSize;
			Vector2 down = new Vector2(-sinR, cosR) * QuadHalfSize;

			Vector2 pTL = worldCenter - right - down;
			Vector2 pTR = worldCenter + right - down;
			Vector2 pBL = worldCenter - right + down;
			Vector2 pBR = worldCenter + right + down;

			VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
			verts[0] = new VertexPositionColorTexture(new Vector3(pTL.X, pTL.Y, 0f), Color.White, new Vector2(0f, 0f));
			verts[1] = new VertexPositionColorTexture(new Vector3(pTR.X, pTR.Y, 0f), Color.White, new Vector2(1f, 0f));
			verts[2] = new VertexPositionColorTexture(new Vector3(pBL.X, pBL.Y, 0f), Color.White, new Vector2(0f, 1f));
			verts[3] = new VertexPositionColorTexture(new Vector3(pBR.X, pBR.Y, 0f), Color.White, new Vector2(1f, 1f));

			foreach (EffectPass pass in fx.CurrentTechnique.Passes)
			{
				pass.Apply();
				device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
			}

			device.BlendState = origBlend;
			device.RasterizerState = origRaster;
			device.DepthStencilState = origDepth;

			sb.Restart(in sbState);
			return true;
		}
	}
}
