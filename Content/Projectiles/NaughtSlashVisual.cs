using System;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Content.Shaders;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles
{
	public class NaughtSlashVisual : ModProjectile
	{
		// ai[0] = Direction / Flip (+1 or -1)
		public int FlipDirection
		{
			get => (int)Projectile.ai[0];
			set => Projectile.ai[0] = value;
		}

		public int ParentSwingWhoAmI
		{
			get => (int)Projectile.ai[1];
			set => Projectile.ai[1] = value;
		}

		public int MaxTime = 18;
		public float SlashRadius = 130f;
		public float SlashSpan = 2.8f;
		public bool AnchorToOwner = true;
		public bool VisualOnly = false;

		public Vector3 ColHot = new Vector3(1.0f, 0.92f, 1.0f);
		public Vector3 ColBright = new Vector3(0.82f, 0.15f, 1.0f);
		public Vector3 ColDeep = new Vector3(0.42f, 0.02f, 0.65f);
		public Vector3 ColDark = new Vector3(0.08f, 0.01f, 0.14f);

		public Player Owner => Main.player[Projectile.owner];

		public NaughtHeldSwing ParentSwing
		{
			get
			{
				if (ParentSwingWhoAmI >= 0 && ParentSwingWhoAmI < Main.maxProjectiles)
				{
					Projectile p = Main.projectile[ParentSwingWhoAmI];
					if (p.active && p.ModProjectile is NaughtHeldSwing swing)
						return swing;
				}
				return null;
			}
		}

		public override string Texture => "WeepForTheDeparted/Content/Projectiles/SlashBrush01";

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.aiStyle = -1;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.ownerHitCheck = true;
			Projectile.DamageType = WeepCompatibility.TrueMelee;
			Projectile.timeLeft = 18;
		}

		public void InitArc(Vector2 center, float angle, float radius, float span, int direction, int duration, Vector3 hot, Vector3 bright, Vector3 deep, Vector3 dark, int parentWhoAmI)
		{
			Projectile.Center = center;
			Projectile.rotation = angle;
			FlipDirection = direction;
			SlashRadius = radius;
			SlashSpan = span;
			Projectile.timeLeft = duration;
			MaxTime = duration;
			ColHot = hot;
			ColBright = bright;
			ColDeep = deep;
			ColDark = dark;
			ParentSwingWhoAmI = parentWhoAmI;
		}

		public static NaughtSlashVisual SpawnSlashArc(IEntitySource source, Vector2 center, float angle, float radius, float span, int direction, int duration, Vector3 hot, Vector3 bright, Vector3 deep, Vector3 dark, int damage, float knockback, int parentWhoAmI, int owner)
		{
			int p = Projectile.NewProjectile(source, center, Vector2.Zero, ModContent.ProjectileType<NaughtSlashVisual>(), damage, knockback, owner, (float)direction, parentWhoAmI);
			if (p >= 0 && p < Main.maxProjectiles && Main.projectile[p].ModProjectile is NaughtSlashVisual slash)
			{
				slash.InitArc(center, angle, radius, span, direction, duration, hot, bright, deep, dark, parentWhoAmI);
				return slash;
			}
			return null;
		}

		public override void AI()
		{
			// Keep centered on owner while active (unless detached for clone mirror slashes)
			if (AnchorToOwner && Owner != null && Owner.active)
			{
				Projectile.Center = Owner.MountedCenter;
			}
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (VisualOnly)
				return false;

			if (ParentSwing != null && ParentSwing.HasHitNPC(target.whoAmI))
				return false;

			return base.CanHitNPC(target);
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			float progress = MathHelper.Clamp(Projectile.timeLeft / (float)Math.Max(1, MaxTime), 0f, 1f);
			float lifeNorm = 1f - progress;
			float sweep = Math.Min(1f, lifeNorm * 1.7f);
			float erode = Math.Max(0f, (lifeNorm - 0.40f) * 1.8f);

			// Active while the crescent trail is visibly sweeping
			if (sweep <= 0.04f || erode >= 0.96f)
				return false;

			Rectangle greedyBox = targetHitbox;
			greedyBox.Inflate(24, 24); // GrazePad standard from Calamity Overhaul's Onikiri

			Vector2 pivot = Projectile.Center;
			float baseAngle = Projectile.rotation;
			float span = SlashSpan;
			int dir = FlipDirection;
			float rOuter = SlashRadius;
			float rInner = SlashRadius * 0.22f;

			const int samples = 18;
			Vector2 prevMid = Vector2.Zero;
			bool hasPrev = false;
			float cp = 0f;

			for (int k = 0; k < samples; k++)
			{
				float u = MathHelper.Lerp(erode, sweep, k / (float)(samples - 1));
				float angle = baseAngle - dir * span * 0.5f + dir * span * u;
				Vector2 outer = pivot + angle.ToRotationVector2() * rOuter;
				Vector2 inner = pivot + angle.ToRotationVector2() * rInner;
				Vector2 mid = (outer + inner) * 0.5f;
				float thick = Math.Max(36f, rOuter - rInner);

				// 1. Check midline polyline of the crescent glow trail
				if (hasPrev && Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(), prevMid, mid, thick, ref cp))
					return true;

				// 2. Check radial spokes from pivot to arc band (wedge coverage)
				if (k % 2 == 0 && Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(), pivot, mid, 40f, ref cp))
					return true;

				prevMid = mid;
				hasPrev = true;
			}

			return false;
		}

		public override void CutTiles()
		{
			DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
			Vector2 pivot = Projectile.Center;
			float baseAngle = Projectile.rotation;
			float span = SlashSpan;
			int dir = FlipDirection;
			float rOuter = SlashRadius;

			for (int k = 0; k < 12; k++)
			{
				float u = k / 11f;
				float angle = baseAngle - dir * span * 0.5f + dir * span * u;
				Vector2 tip = pivot + angle.ToRotationVector2() * rOuter;
				Utils.PlotTileLine(pivot, tip, 36f, DelegateMethods.CutTiles);
			}
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			modifiers.HitDirectionOverride = (target.Center.X >= Projectile.Center.X) ? 1 : -1;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Forward hit event to parent swing for unified combo rhythm and hitsounds
			if (ParentSwing != null)
			{
				ParentSwing.RegisterHitFromVisual(target, hit, damageDone);
			}
			else
			{
				SoundEngine.PlaySound(WeepSound.KatanaHit with { Pitch = -0.05f }, target.Center);

				for (int i = 0; i < 8; i++)
				{
					Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
					Color col = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
					PRTLoader.NewParticle<VoidSparkPRT>(target.Center, vel, col, 0.9f);
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Effect fx = ShaderLoader.SlashShader;
			Texture2D brush = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/SlashBrush01", AssetRequestMode.ImmediateLoad).Value;
			Texture2D noise = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/RiftNoise", AssetRequestMode.ImmediateLoad).Value;

			if (fx == null || brush == null || noise == null)
				return false;

			float progress = MathHelper.Clamp(Projectile.timeLeft / (float)Math.Max(1, MaxTime), 0f, 1f);
			float lifeNorm = 1f - progress;
			float sweep = Math.Min(1f, lifeNorm * 1.7f);
			float erode = Math.Max(0f, (lifeNorm - 0.40f) * 1.8f);
			float flash = MathHelper.Clamp((float)Math.Sin(lifeNorm * MathHelper.Pi) * 0.95f, 0f, 1f);

			SpriteBatch sb = Main.spriteBatch;
			GraphicsDevice device = Main.graphics.GraphicsDevice;

			SpriteBatchState state = sb.Capture();
			sb.End();

			BlendState origBlend = device.BlendState;
			RasterizerState origRaster = device.RasterizerState;
			device.BlendState = BlendState.AlphaBlend;
			device.RasterizerState = RasterizerState.CullNone;

			Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, 0, 1);
			Matrix view = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0)) * Main.GameViewMatrix.TransformationMatrix;
			Matrix transformMatrix = view * projection;

			fx.Parameters["transformMatrix"]?.SetValue(transformMatrix);
			fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			fx.Parameters["uBrushTex"]?.SetValue(brush);
			fx.Parameters["uNoiseTex"]?.SetValue(noise);
			fx.Parameters["uFarSel"]?.SetValue(0f);
			fx.Parameters["uFarDim"]?.SetValue(0f);
			fx.Parameters["uRazorTailWiden"]?.SetValue(0.35f);
			fx.Parameters["uInk"]?.SetValue(0f);
			fx.Parameters["uColHot"]?.SetValue(ColHot);
			fx.Parameters["uColBright"]?.SetValue(ColBright);
			fx.Parameters["uColDeep"]?.SetValue(ColDeep);
			fx.Parameters["uColDark"]?.SetValue(ColDark);

			Vector2 dir = Projectile.rotation.ToRotationVector2();
			Vector2 normal = new Vector2(-dir.Y, dir.X);

			float hx = SlashRadius * 1.15f;
			float hy = SlashRadius * 1.15f;
			Vector2 center = Projectile.Center + dir * (SlashRadius * 0.15f);

			fx.Parameters["uMode"]?.SetValue(0f);
			fx.Parameters["uSweep"]?.SetValue(sweep);
			fx.Parameters["uErode"]?.SetValue(erode);
			fx.Parameters["uTailErode"]?.SetValue(lifeNorm * 0.6f);
			fx.Parameters["uFlash"]?.SetValue(flash);
			fx.Parameters["uOpacity"]?.SetValue(1f - lifeNorm * lifeNorm);
			fx.Parameters["uArcSpan"]?.SetValue(SlashSpan);
			fx.Parameters["uThick"]?.SetValue(0.32f);
			fx.Parameters["uFlip"]?.SetValue((float)FlipDirection);

			VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
			verts[0] = new VertexPositionColorTexture(new Vector3(center - dir * hx - normal * hy, 0f), Color.White, new Vector2(0f, 0f));
			verts[1] = new VertexPositionColorTexture(new Vector3(center + dir * hx - normal * hy, 0f), Color.White, new Vector2(1f, 0f));
			verts[2] = new VertexPositionColorTexture(new Vector3(center - dir * hx + normal * hy, 0f), Color.White, new Vector2(0f, 1f));
			verts[3] = new VertexPositionColorTexture(new Vector3(center + dir * hx + normal * hy, 0f), Color.White, new Vector2(1f, 1f));

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
