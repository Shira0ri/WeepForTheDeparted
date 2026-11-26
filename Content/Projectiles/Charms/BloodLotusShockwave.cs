using System;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Content.Shaders;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles.Charms
{
	public class BloodLotusShockwave : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Visuals/BloodLotusBloom";

		private const int MaxLifetime = 35;
		private const float MinRadius = 10f;

		// Shared per-player / per-NPC hit timestamp (+1 so 0 strictly means "never hit")
		// preventing simultaneous Blood Lotus shockwaves from stacking damage on the same target.
		private static readonly uint[,] lastGlobalHitTick = new uint[Main.maxPlayers, Main.maxNPCs];

		public static void ResetGlobalHitCooldowns()
		{
			Array.Clear(lastGlobalHitTick, 0, lastGlobalHitTick.Length);
		}

		private float maxRadius = 184f;
		private float quadHalfRadius = 184f * 1.15f;
		private float currentRadius = MinRadius;
		private Vector2 anchorCenter;
		private bool initialized;
		private float baseRotation;

		private int KnotStacks => Math.Clamp((int)Projectile.ai[0], 1, 9);

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
		}

		public override void SetDefaults()
		{
			Projectile.width = 32;
			Projectile.height = 32;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = MaxLifetime;
			Projectile.DamageType = WeepCompatibility.TrueMelee;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = WeepBalance.Active.BloodLotusGlobalHitCooldownTicks;
		}

		public override bool ShouldUpdatePosition() => false;

		public override bool? CanDamage() => true;

		public override bool? CanHitNPC(NPC target)
		{
			if (target == null || !target.active || target.whoAmI < 0 || target.whoAmI >= Main.maxNPCs)
				return false;

			int ownerIdx = Projectile.owner;
			if (ownerIdx < 0 || ownerIdx >= Main.maxPlayers)
				return null;

			uint tickStamp = Main.GameUpdateCount + 1u;
			uint lastTick = lastGlobalHitTick[ownerIdx, target.whoAmI];
			int cooldown = WeepBalance.Active.BloodLotusGlobalHitCooldownTicks;
			if (lastTick != 0 && tickStamp - lastTick < (uint)cooldown)
				return false;

			return null;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Vector2 center = initialized ? anchorCenter : Projectile.Center;
			Vector2 closestPoint = Vector2.Clamp(center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
			return Vector2.DistanceSquared(center, closestPoint) <= currentRadius * currentRadius;
		}

		public override void AI()
		{
			if (!initialized)
			{
				initialized = true;
				anchorCenter = Projectile.Center;
				Projectile.velocity = Vector2.Zero;
				baseRotation = Main.rand.NextFloat(MathHelper.TwoPi);
				maxRadius = WeepBalance.Active.BloodLotusMaxRadiusPx;
				quadHalfRadius = maxRadius * 1.15f;

				if (!Main.dedServ)
				{
					SpawnInitialBloomBurst();
				}
			}

			float progress = 1f - (Projectile.timeLeft / (float)MaxLifetime);
			currentRadius = MathHelper.Lerp(MinRadius, maxRadius, MathF.Sqrt(progress));

			// Resize hitbox while keeping the center strictly locked to the dying enemy's center
			int diameter = Math.Max(20, (int)MathF.Round(currentRadius * 2f));
			Projectile.width = diameter;
			Projectile.height = diameter;
			Projectile.Center = anchorCenter;

			// Emit subtle lotus petals along the expanding wavefront during the main expansion
			if (!Main.dedServ && progress < 0.65f && Main.rand.NextBool(2))
			{
				float ang = Main.rand.NextFloat(MathHelper.TwoPi);
				Vector2 dir = ang.ToRotationVector2();
				Vector2 pos = anchorCenter + dir * currentRadius;
				Vector2 vel = dir * Main.rand.NextFloat(2.0f, 4.2f);
				Color col = Main.rand.NextBool() ? new Color(238, 26, 56, 230) : new Color(185, 45, 255, 220);
				PRTLoader.NewParticle<CrimsonPetalPRT>(pos, vel, col, Main.rand.NextFloat(0.75f, 0.95f));
			}

			float lightFade = 1f - progress;
			Lighting.AddLight(anchorCenter, new Vector3(0.55f, 0.08f, 0.25f) * lightFade);
		}

		private void SpawnInitialBloomBurst()
		{
			// Core spider-lily flash & radial dimensional burst
			PRTLoader.NewParticle<CrimsonBloomPRT>(anchorCenter, Vector2.Zero, Color.White, 0.72f)
				.Configure(24, new Color(255, 45, 85));
			PRTLoader.NewParticle<DimensionalExplosionPRT>(anchorCenter, Vector2.Zero, Color.White, 1.25f)
				.Configure(20, new Color(245, 35, 75));

			int stacks = KnotStacks;
			int petalCount = 10 + stacks;
			for (int i = 0; i < petalCount; i++)
			{
				float ang = MathHelper.TwoPi * (i / (float)petalCount) + Main.rand.NextFloat(-0.18f, 0.18f);
				Vector2 dir = ang.ToRotationVector2();
				Vector2 vel = dir * Main.rand.NextFloat(4.5f, 8.5f);
				Color col = i % 3 == 0 ? new Color(185, 45, 255, 225) : new Color(242, 28, 60, 235);
				PRTLoader.NewParticle<CrimsonPetalPRT>(anchorCenter + dir * 12f, vel, col, Main.rand.NextFloat(0.8f, 1.05f));
			}

			int sparkCount = 12 + stacks;
			for (int i = 0; i < sparkCount; i++)
			{
				float ang = MathHelper.TwoPi * (i / (float)sparkCount) + Main.rand.NextFloat(-0.2f, 0.2f);
				Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(5.5f, 11.5f);
				Color col = i % 2 == 0 ? new Color(255, 40, 75) : new Color(185, 45, 255);
				PRTLoader.NewParticle<VoidSparkPRT>(anchorCenter, vel, col, Main.rand.NextFloat(0.85f, 1.15f));
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			int ownerIdx = Projectile.owner;
			if (ownerIdx >= 0 && ownerIdx < Main.maxPlayers && target != null && target.whoAmI >= 0 && target.whoAmI < Main.maxNPCs)
			{
				lastGlobalHitTick[ownerIdx, target.whoAmI] = Main.GameUpdateCount + 1u;
			}

			for (int i = 0; i < 4; i++)
			{
				Dust d = Dust.NewDustPerfect(target.Center, DustID.LifeDrain, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.3f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;

			float progress = MathHelper.Clamp(1f - (Projectile.timeLeft / (float)MaxLifetime), 0f, 1f);
			if (progress >= 0.995f)
				return false;

			Vector2 worldCenter = initialized ? anchorCenter : Projectile.Center;
			float intensity = 0.90f + KnotStacks * 0.045f;

			SpriteBatch sb = Main.spriteBatch;
			SpriteBatchState sbState = sb.Capture();

			// Layer 1: Anti-aliased procedural shockwave ring + analytical Blood Lotus aura
			bool drewShaderShockwave = DrawProceduralShockwaveQuad(sb, in sbState, worldCenter, progress, intensity);

			// Layer 2: High-res 512x512 Blood Lotus bloom (LinearClamp + CrimsonKnot unsharp/sheen + FlowerBloom rim flash)
			DrawHighResLotusBloom(sb, in sbState, worldCenter - Main.screenPosition, progress);

			if (!drewShaderShockwave)
			{
				DrawFallbackRing(sb, worldCenter - Main.screenPosition, progress);
			}

			return false;
		}

		private bool DrawProceduralShockwaveQuad(SpriteBatch sb, in SpriteBatchState sbState, Vector2 worldCenter, float progress, float intensity)
		{
			Effect fx = ShaderLoader.BloodLotusShader;
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
			fx.Parameters["uProgress"]?.SetValue(progress);
			fx.Parameters["uRingNorm"]?.SetValue(MathHelper.Clamp(currentRadius / quadHalfRadius, 0.01f, 0.92f));
			fx.Parameters["uIntensity"]?.SetValue(intensity);
			fx.Parameters["uRotation"]?.SetValue(baseRotation + progress * 0.35f);
			fx.Parameters["uNoiseTex"]?.SetValue(noise);

			VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
			verts[0] = new VertexPositionColorTexture(new Vector3(worldCenter.X - quadHalfRadius, worldCenter.Y - quadHalfRadius, 0f), Color.White, new Vector2(0f, 0f));
			verts[1] = new VertexPositionColorTexture(new Vector3(worldCenter.X + quadHalfRadius, worldCenter.Y - quadHalfRadius, 0f), Color.White, new Vector2(1f, 0f));
			verts[2] = new VertexPositionColorTexture(new Vector3(worldCenter.X - quadHalfRadius, worldCenter.Y + quadHalfRadius, 0f), Color.White, new Vector2(0f, 1f));
			verts[3] = new VertexPositionColorTexture(new Vector3(worldCenter.X + quadHalfRadius, worldCenter.Y + quadHalfRadius, 0f), Color.White, new Vector2(1f, 1f));

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

		private void DrawHighResLotusBloom(SpriteBatch sb, in SpriteBatchState sbState, Vector2 screenCenter, float progress)
		{
			Texture2D lotusTex = ModContent.Request<Texture2D>(Texture, AssetRequestMode.ImmediateLoad).Value;
			if (lotusTex == null)
				return;

			float fadeIn = MathHelper.Clamp(progress / 0.12f, 0f, 1f);
			float fadeOut = 1f - MathHelper.Clamp((progress - 0.35f) / 0.60f, 0f, 1f);
			float lotusAlpha = fadeIn * fadeOut * 0.88f;
			if (lotusAlpha <= 0.01f)
				return;

			Vector2 origin = lotusTex.Size() * 0.5f;
			// Map 512x512 texture radius (256px) cleanly inside currentRadius so petals track the wavefront
			float scale = (currentRadius / (lotusTex.Width * 0.5f)) * 0.94f;
			float rot = baseRotation + progress * 0.22f;

			sb.End();

			Effect knotShader = ShaderLoader.CrimsonKnotShader;
			if (knotShader != null)
			{
				knotShader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
				knotShader.Parameters["uTexSize"]?.SetValue(new Vector2(lotusTex.Width, lotusTex.Height));
				knotShader.Parameters["uEdgeSharpness"]?.SetValue(0.32f);
				knotShader.Parameters["uSheenIntensity"]?.SetValue(0.30f);

				sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, knotShader, sbState.Matrix);
			}
			else
			{
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, sbState.Matrix);
			}

			// Preserve full RGB while scaling alpha cleanly (CrimsonKnotShader premultiplies RGB by finalCol.a)
			Color tint = new Color(255, 255, 255, (int)(lotusAlpha * 255f));
			sb.Draw(lotusTex, screenCenter, null, tint, rot, origin, scale, SpriteEffects.None, 0f);
			sb.End();

			// Early white-hot unfurling rim highlight using FlowerBloomShader
			if (progress < 0.65f)
			{
				Effect bloomShader = ShaderLoader.FlowerBloomShader;
				if (bloomShader != null)
				{
					float bloomProgress = MathHelper.Clamp(progress / 0.65f, 0f, 1f);
					float rimAlpha = (1f - bloomProgress) * 0.50f;
					bloomShader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
					bloomShader.Parameters["uProgress"]?.SetValue(bloomProgress);
					bloomShader.Parameters["uBloomColor"]?.SetValue(new Vector3(1f, 0.18f, 0.32f));

					sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, bloomShader, sbState.Matrix);
					sb.Draw(lotusTex, screenCenter, null, Color.White * rimAlpha, rot, origin, scale * 1.02f, SpriteEffects.None, 0f);
					sb.End();
				}
			}

			sb.Restart(in sbState);
		}

		private void DrawFallbackRing(SpriteBatch sb, Vector2 screenCenter, float progress)
		{
			float alpha = (1f - progress) * 0.85f;
			if (alpha <= 0.01f)
				return;

			Texture2D pixel = TextureAssets.MagicPixel.Value;
			int segments = 72;
			Color ringColor = new Color(238, 26, 56) * alpha;

			for (int i = 0; i < segments; i++)
			{
				float a1 = MathHelper.TwoPi * (i / (float)segments);
				float a2 = MathHelper.TwoPi * ((i + 1) / (float)segments);
				Vector2 p1 = screenCenter + a1.ToRotationVector2() * currentRadius;
				Vector2 p2 = screenCenter + a2.ToRotationVector2() * currentRadius;

				Vector2 diff = p2 - p1;
				sb.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), ringColor, diff.ToRotation(), Vector2.Zero, new Vector2(diff.Length(), 4f), SpriteEffects.None, 0f);
			}
		}
	}
}
