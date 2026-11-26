using System;
using System.Collections.Generic;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Content.Shaders;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles
{
	/// <summary>
	/// EX (Horizon's Severance) projected crimson spatial crescent wave emitted on every combo beat.
	/// Shares hit deduplication with its originating NaughtHeldSwing beat so targets hit by the
	/// odachi blade or crescent arc cannot be double-hit by the wave on the same beat.
	/// </summary>
	public class NaughtExWave : ModProjectile
	{
		public const int MaxLifetime = 20;
		public const float InitialSpeed = 26f;
		public const float VelocityDrag = 0.95f;
		public const float WaveHalfWidth = 55f;
		public const float WaveThickness = 24f;

		/// <summary>
		/// Active balance-profile damage multiplier for EX Spatial Waves.
		/// </summary>
		public static float DamageMult => WeepBalance.Active.ExWaveDamageMult;

		public override string Texture => "WeepForTheDeparted/Content/Projectiles/SlashBrush01";

		public int ComboStep
		{
			get => (int)Projectile.ai[0];
			set => Projectile.ai[0] = value;
		}

		public int SwingDir => Projectile.ai[1] < 0f ? -1 : 1;

		/// <summary>
		/// Shared per-beat hit set from the spawning NaughtHeldSwing (local owner only).
		/// </summary>
		public HashSet<int> SharedBeatHits;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.DrawScreenCheckFluff[Type] = 360;
		}

		public override void SetDefaults()
		{
			Projectile.width = 48;
			Projectile.height = 48;
			Projectile.aiStyle = -1;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.timeLeft = MaxLifetime;
			Projectile.DamageType = WeepCompatibility.TrueMelee;
		}

		public override void AI()
		{
			Projectile.velocity *= VelocityDrag;
			if (Projectile.velocity.LengthSquared() > 0.01f)
			{
				Projectile.rotation = Projectile.velocity.ToRotation();
			}

			if (!Main.dedServ && Main.rand.NextBool(2))
			{
				Vector2 dir = Projectile.rotation.ToRotationVector2();
				Vector2 perp = new Vector2(-dir.Y, dir.X);
				Vector2 pos = Projectile.Center + perp * Main.rand.NextFloat(-WaveHalfWidth * 0.75f, WaveHalfWidth * 0.75f);
				Vector2 vel = dir * Main.rand.NextFloat(1.5f, 4.0f) + Main.rand.NextVector2Circular(1f, 1f);
				if (Main.rand.NextBool(3))
				{
					PRTLoader.NewParticle<CrimsonPetalPRT>(pos, vel * 0.7f, new Color(245, 35, 70, 220), 0.65f);
				}
				else
				{
					Color col = Main.rand.NextBool() ? new Color(238, 26, 56) : new Color(185, 45, 255);
					PRTLoader.NewParticle<VoidSparkPRT>(pos, vel, col, 0.72f);
				}
			}

			float fade = MathHelper.Clamp(Projectile.timeLeft / 7f, 0f, 1f);
			Lighting.AddLight(Projectile.Center, new Vector3(0.75f, 0.14f, 0.45f) * fade);
		}

		private float GetTiltAngle()
		{
			return ComboStep switch
			{
				0 => 0.12f * SwingDir,
				1 => 0.35f * SwingDir,
				2 => -0.35f * SwingDir,
				_ => 0f
			};
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (SharedBeatHits != null && SharedBeatHits.Contains(target.whoAmI))
				return false;

			return base.CanHitNPC(target);
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Vector2 dir = Projectile.velocity.SafeNormalize(Projectile.rotation.ToRotationVector2());
			Vector2 perp = dir.RotatedBy(MathHelper.PiOver2 + GetTiltAngle());
			Vector2 start = Projectile.Center - perp * WaveHalfWidth;
			Vector2 end = Projectile.Center + perp * WaveHalfWidth;
			float collisionPoint = 0f;

			return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, WaveThickness, ref collisionPoint);
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			modifiers.HitDirectionOverride = Projectile.velocity.X >= 0f ? 1 : -1;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			SharedBeatHits?.Add(target.whoAmI);

			if (!Main.dedServ)
			{
				for (int i = 0; i < 6; i++)
				{
					Vector2 vel = Main.rand.NextVector2Circular(4.5f, 4.5f);
					Color col = i % 2 == 0 ? new Color(245, 35, 70) : new Color(185, 45, 255);
					PRTLoader.NewParticle<VoidSparkPRT>(target.Center, vel, col, 0.85f);
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;

			Effect fx = ShaderLoader.SlashShader;
			Texture2D brush = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/SlashBrush01", AssetRequestMode.ImmediateLoad).Value;
			Texture2D noise = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/RiftNoise", AssetRequestMode.ImmediateLoad).Value;
			if (fx == null || brush == null || noise == null)
				return false;

			float lifeNorm = 1f - MathHelper.Clamp(Projectile.timeLeft / (float)MaxLifetime, 0f, 1f);
			float fade = MathHelper.Clamp(Projectile.timeLeft / 7f, 0f, 1f);
			float opacity = fade * 0.92f;
			if (opacity <= 0.01f)
				return false;

			SpriteBatch sb = Main.spriteBatch;
			GraphicsDevice device = Main.graphics.GraphicsDevice;
			SpriteBatchState state = sb.Capture();
			sb.End();

			BlendState origBlend = device.BlendState;
			RasterizerState origRaster = device.RasterizerState;
			device.BlendState = BlendState.AlphaBlend;
			device.RasterizerState = RasterizerState.CullNone;

			Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -4000f, 4000f);
			Matrix view = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0)) * Main.GameViewMatrix.TransformationMatrix;

			fx.Parameters["transformMatrix"]?.SetValue(view * projection);
			fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			fx.Parameters["uBrushTex"]?.SetValue(brush);
			fx.Parameters["uNoiseTex"]?.SetValue(noise);
			fx.Parameters["uFarSel"]?.SetValue(0f);
			fx.Parameters["uFarDim"]?.SetValue(0f);
			fx.Parameters["uRazorTailWiden"]?.SetValue(0.28f);
			fx.Parameters["uInk"]?.SetValue(0f);
			fx.Parameters["uFeiBai"]?.SetValue(0f);
			fx.Parameters["uBleed"]?.SetValue(0f);
			fx.Parameters["uSplitTail"]?.SetValue(0f);
			fx.Parameters["uColorShift"]?.SetValue(0.25f);
			fx.Parameters["uFlowPhase"]?.SetValue(0.45f);
			fx.Parameters["uFrontGlow"]?.SetValue(1.8f);
			fx.Parameters["uSeed"]?.SetValue((Projectile.identity * 0.618f) % 1f);

			fx.Parameters["uColHot"]?.SetValue(new Vector3(1.0f, 0.94f, 0.96f));
			fx.Parameters["uColBright"]?.SetValue(new Vector3(0.98f, 0.14f, 0.34f));
			fx.Parameters["uColDeep"]?.SetValue(new Vector3(0.55f, 0.04f, 0.45f));
			fx.Parameters["uColDark"]?.SetValue(new Vector3(0.12f, 0.01f, 0.14f));

			fx.Parameters["uMode"]?.SetValue(0f);
			fx.Parameters["uSweep"]?.SetValue(1f);
			fx.Parameters["uErode"]?.SetValue(lifeNorm * 0.38f);
			fx.Parameters["uTailErode"]?.SetValue(lifeNorm * 0.55f);
			fx.Parameters["uFlash"]?.SetValue((1f - lifeNorm) * 0.65f);
			fx.Parameters["uOpacity"]?.SetValue(opacity);
			fx.Parameters["uArcSpan"]?.SetValue(MathHelper.ToRadians(135f));
			fx.Parameters["uThick"]?.SetValue(0.24f);
			fx.Parameters["uFlip"]?.SetValue((float)SwingDir);

			float rot = Projectile.rotation + GetTiltAngle();
			Vector2 dir = rot.ToRotationVector2();
			Vector2 normal = new Vector2(-dir.Y, dir.X);

			float hx = 56f;
			float hy = 72f;
			Vector2 drawCenter = Projectile.Center - dir * 18f;

			VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
			verts[0] = new VertexPositionColorTexture(new Vector3(drawCenter - dir * hx - normal * hy, 0f), Color.White, new Vector2(0f, 0f));
			verts[1] = new VertexPositionColorTexture(new Vector3(drawCenter + dir * hx - normal * hy, 0f), Color.White, new Vector2(1f, 0f));
			verts[2] = new VertexPositionColorTexture(new Vector3(drawCenter - dir * hx + normal * hy, 0f), Color.White, new Vector2(0f, 1f));
			verts[3] = new VertexPositionColorTexture(new Vector3(drawCenter + dir * hx + normal * hy, 0f), Color.White, new Vector2(1f, 1f));

			foreach (EffectPass pass in fx.CurrentTechnique.Passes)
			{
				pass.Apply();
				device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
			}

			device.BlendState = origBlend;
			device.RasterizerState = origRaster;
			sb.Restart(in state);
			return false;
		}
	}
}
