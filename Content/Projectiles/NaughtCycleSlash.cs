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
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Content.Shaders;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles
{
	/// <summary>
	/// Localized Onikiri-style orbital crimson slash anchored directly onto an auto-targeted enemy
	/// on Shadow Clone cycle completion. Uses ShaderLoader.SlashShader (OniCrimsonSlash.fxc)
	/// without screen-wide lines, camera punches, screen flashes, or domain darkening.
	/// </summary>
	public class NaughtCycleSlash : ModProjectile
	{
		public const int Lifetime = 28;
		private const int SweepFrames = 5;
		private const int ErodeStart = 9;
		private const int ErodeFrames = 15;
		private const float ColorShiftDelay = 8f;
		private const float ColorShiftFrames = 13f;

		public override string Texture => "WeepForTheDeparted/Content/Projectiles/SlashBrush01";

		public Player Owner => Main.player[Projectile.owner];

		public int TargetWhoAmI
		{
			get => (int)Projectile.ai[0];
			set => Projectile.ai[0] = value;
		}

		public float RollAngle => Projectile.ai[1];
		public float FlipSign => Projectile.ai[2] < 0f ? -1f : 1f;
		public float Escalate => MathHelper.Clamp(MathF.Abs(Projectile.ai[2]) - 0.01f, 0f, 1f);

		private bool initialized;
		private int timer;
		private float seed;
		private float arcSpan;
		private float baseHalfX;
		private float baseHalfY;
		private float thick;
		private Vector2 localJitter;

		private static float EaseOutCubic(float x)
		{
			x = MathHelper.Clamp(x, 0f, 1f);
			return 1f - MathF.Pow(1f - x, 3f);
		}

		private static float EaseOutBack(float x)
		{
			x = MathHelper.Clamp(x, 0f, 1f);
			const float c1 = 1.70158f;
			const float c3 = c1 + 1f;
			float xm = x - 1f;
			return 1f + c3 * xm * xm * xm + c1 * xm * xm;
		}

		private static float SmoothStep01(float x)
		{
			x = MathHelper.Clamp(x, 0f, 1f);
			return x * x * (3f - 2f * x);
		}

		public static NaughtCycleSlash Spawn(
			IEntitySource source,
			NPC target,
			float rollAngle,
			int flip,
			float escalate,
			int damage,
			float knockBack,
			int owner,
			int critChance = 0)
		{
			if (target == null || !target.active)
				return null;

			float packedAi2 = (flip < 0 ? -1f : 1f) * (0.01f + MathHelper.Clamp(escalate, 0f, 1f));
			int idx = Projectile.NewProjectile(
				source,
				target.Center,
				Vector2.Zero,
				ModContent.ProjectileType<NaughtCycleSlash>(),
				Math.Max(1, damage),
				knockBack,
				owner,
				target.whoAmI,
				MathHelper.WrapAngle(rollAngle),
				packedAi2);

			if (idx >= 0 && idx < Main.maxProjectiles)
			{
				Projectile proj = Main.projectile[idx];
				if (critChance > 0)
				{
					proj.CritChance = critChance;
				}
				return proj.ModProjectile as NaughtCycleSlash;
			}

			return null;
		}

		public override void SetDefaults()
		{
			Projectile.width = 32;
			Projectile.height = 32;
			Projectile.aiStyle = -1;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = Lifetime + 2;
			Projectile.DamageType = WeepCompatibility.TrueMelee;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
		}

		public override bool? CanDamage() => false;

		public override bool ShouldUpdatePosition() => false;

		private void Initialize(NPC target)
		{
			initialized = true;
			seed = (Projectile.identity * 0.6180339887f) % 1f;
			if (seed < 0f) seed += 1f;

			float targetSize = target != null ? Math.Max(target.width, target.height) : 64f;
			float escalate = Escalate;
			baseHalfX = MathHelper.Clamp(targetSize * 0.90f, 115f, 195f) * (1f + 0.14f * escalate);

			// Tilted elliptical foreshortening matching Onikiri's OniFinaleRing
			float squash = 0.32f + 0.24f * ((seed * 7.13f) % 1f);
			baseHalfY = baseHalfX * squash;
			arcSpan = 4.85f + 0.65f * ((seed * 3.71f) % 1f);
			thick = 0.30f + 0.06f * escalate;
			localJitter = new Vector2(MathF.Cos(seed * 41.3f), MathF.Sin(seed * 27.9f)) * 10f;
		}

		private NPC ResolveTarget()
		{
			if (TargetWhoAmI >= 0 && TargetWhoAmI < Main.maxNPCs)
			{
				NPC npc = Main.npc[TargetWhoAmI];
				if (IsValidTarget(npc))
					return npc;
			}
			return null;
		}

		private static bool IsValidTarget(NPC npc)
		{
			if (npc == null || !npc.active || npc.friendly || npc.isLikeATownNPC || npc.life <= 0)
				return false;

			if (npc.type == NPCID.TargetDummy)
				return true;

			return !npc.dontTakeDamage;
		}

		public override void AI()
		{
			if (Owner == null || !Owner.active)
			{
				Projectile.Kill();
				return;
			}

			NPC target = ResolveTarget();

			if (!initialized)
			{
				Initialize(target);
				if (target != null)
				{
					Projectile.Center = target.Center + localJitter;
				}
			}

			timer++;

			if (timer == 1)
			{
				ExecuteStrikeAndInitialAudio(ref target);
			}

			// Keep localized ring anchored onto target while alive
			if (target != null && target.active)
			{
				Projectile.Center = target.Center + localJitter;
			}

			if (!Main.dedServ && timer <= SweepFrames + 1)
			{
				SpawnSweepSparks();
			}

			float lightFade = 1f - MathHelper.Clamp(timer / (float)Lifetime, 0f, 1f);
			Vector3 lightCol = Vector3.Lerp(new Vector3(0.55f, 0.14f, 0.95f), new Vector3(0.85f, 0.28f, 1.25f), Escalate);
			Lighting.AddLight(Projectile.Center, lightCol * lightFade * 0.65f);

			if (timer >= Lifetime)
			{
				Projectile.Kill();
			}
		}

		private void ExecuteStrikeAndInitialAudio(ref NPC target)
		{
			float escalate = Escalate;
			SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.05f + 0.14f * escalate, Volume = 0.55f }, Projectile.Center);

			if (Main.myPlayer != Projectile.owner)
				return;

			if (target == null)
			{
				target = NaughtPlayer.FindHighestHpEnemy(Owner.MountedCenter, 1800f, ignoreMaxedKnots: false, ignoreInvulnerable: true);
				if (target != null)
				{
					TargetWhoAmI = target.whoAmI;
					Projectile.Center = target.Center + localJitter;
				}
			}

			if (target == null)
				return;

			var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
			if (naughtPlayer == null)
				return;

			Projectile.CritChance = naughtPlayer.GetNaughtCritChance();
			int hitDir = target.Center.X >= Owner.MountedCenter.X ? 1 : -1;

			int damageDone = naughtPlayer.StrikeNPC(
				target,
				Projectile.damage,
				hitDir,
				crit: null,
				knockBack: Projectile.knockBack,
				sourceProj: Projectile,
				isUltimate: naughtPlayer.HasEidolon6);

			if (damageDone > 0)
			{
				SoundEngine.PlaySound(WeepSound.KatanaHit with { Pitch = -0.05f + 0.10f * escalate, Volume = 0.50f }, target.Center);

				if (!Main.dedServ)
				{
					for (int i = 0; i < 6; i++)
					{
						Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
						Color col = Main.rand.NextBool() ? new Color(175, 55, 255) : new Color(220, 110, 255);
						PRTLoader.NewParticle<VoidSparkPRT>(target.Center, vel, col, 0.85f);
					}
				}
			}
		}

		private void SpawnSweepSparks()
		{
			float sweep = EaseOutCubic(timer / (float)SweepFrames);
			float prevSweep = timer > 1 ? EaseOutCubic((timer - 1) / (float)SweepFrames) : 0f;
			int count = (sweep - prevSweep) > 0.15f ? 3 : 2;

			float edgeU = MathHelper.Clamp(sweep * 1.05f, 0.06f, 0.94f);
			Vector2 pos = PointAt(edgeU, timer);
			Vector2 nextPos = PointAt(MathHelper.Clamp(edgeU + 0.03f, 0f, 1f), timer);
			Vector2 tangent = (nextPos - pos).SafeNormalize(RollAngle.ToRotationVector2());

			Color sparkCol = Color.Lerp(new Color(175, 55, 255), new Color(230, 165, 255), Escalate);
			for (int k = 0; k < count; k++)
			{
				Vector2 vel = tangent * Main.rand.NextFloat(3.5f, 8.5f) + Main.rand.NextVector2Circular(1.2f, 1.2f);
				PRTLoader.NewParticle<VoidSparkPRT>(pos, vel, sparkCol, Main.rand.NextFloat(0.65f, 0.95f));
			}
		}

		private void ComputeAnimState(int lt, out float sweep, out float erode, out float tailErode, out float colorShift,
			out float flash, out float opacity, out float frontGlow, out float flowPhase, out float scaleMul,
			out float rotOffset, out float thickMul)
		{
			float lifeT = MathHelper.Clamp(lt / (float)Lifetime, 0f, 1f);
			float burstT = MathHelper.Clamp(lt / (SweepFrames + 2f), 0f, 1f);
			scaleMul = MathHelper.Lerp(0.62f, 1f, EaseOutBack(burstT)) + 0.07f * lifeT;

			float followT = MathHelper.Clamp((lt - SweepFrames) / 14f, 0f, 1f);
			rotOffset = FlipSign * 0.13f * (1f - (1f - followT) * (1f - followT));

			float thickIn = EaseOutCubic(lt / (SweepFrames + 2f));
			thickMul = MathHelper.Lerp(0.68f, 1.12f, thickIn)
				* (1f - 0.42f * SmoothStep01((lifeT - 0.45f) / 0.55f));

			float ft = lt - SweepFrames;
			flash = ft < 0f ? 0f : ft <= 1f ? 1f : MathF.Pow(0.52f, ft - 1f);
			if (flash < 0.02f) flash = 0f;
			flash *= (0.55f + 0.28f * Escalate);

			sweep = EaseOutCubic(lt / (float)SweepFrames);
			erode = SmoothStep01((lt - ErodeStart) / (float)ErodeFrames);
			tailErode = 0.45f * SmoothStep01((lt - SweepFrames) / (Lifetime * 0.72f));
			colorShift = MathHelper.Clamp((lt - ColorShiftDelay) / ColorShiftFrames, 0f, 1f);
			opacity = 0.95f * (1f - MathHelper.Clamp((lt - (Lifetime - 6)) / 6f, 0f, 1f));

			float baseFrontGlow = 2.2f + 0.5f * Escalate;
			frontGlow = lt <= SweepFrames + 1
				? baseFrontGlow
				: baseFrontGlow * MathF.Max(0f, 1f - (lt - SweepFrames - 1) / 5f);

			flowPhase = 0.62f * EaseOutCubic(lt / 15f);
		}

		private Vector2 PointAt(float uc, int lt)
		{
			ComputeAnimState(lt, out _, out _, out _, out _, out _, out _, out _, out _, out float scaleMul, out float rotOffset, out float thickMul);
			Vector2 ax = (RollAngle + rotOffset).ToRotationVector2();
			Vector2 ay = new Vector2(-ax.Y, ax.X);
			float hx = baseHalfX * scaleMul;
			float hy = baseHalfY * scaleMul;

			float env = MathF.Sin(MathF.Pow(uc, 1.85f) * MathF.PI);
			float w = thick * thickMul * MathF.Pow(MathF.Max(env, 0.0001f), 0.72f);
			float rFrac = 0.90f - w * 0.5f;
			float phi = FlipSign * (uc - 0.5f) * arcSpan;
			return Projectile.Center + ax * MathF.Cos(phi) * rFrac * hx + ay * MathF.Sin(phi) * rFrac * hy;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (!initialized || timer <= 0 || timer >= Lifetime)
				return false;

			Effect fx = ShaderLoader.SlashShader;
			Texture2D brush = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/SlashBrush01", AssetRequestMode.ImmediateLoad).Value;
			Texture2D noise = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/RiftNoise", AssetRequestMode.ImmediateLoad).Value;
			if (fx == null || brush == null || noise == null)
				return false;

			ComputeAnimState(timer, out float sweep, out float erode, out float tailErode, out float colorShift,
				out float flash, out float opacity, out float frontGlow, out float flowPhase,
				out float scaleMul, out float rotOffset, out float thickMul);

			if (opacity <= 0.012f)
				return false;

			SpriteBatch sb = Main.spriteBatch;
			GraphicsDevice device = Main.graphics.GraphicsDevice;

			SpriteBatchState state = sb.Capture();
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
			fx.Parameters["uBrushTex"]?.SetValue(brush);
			fx.Parameters["uNoiseTex"]?.SetValue(noise);

			float escalateT = Escalate * 0.75f;
			Vector3 colHot = Vector3.Lerp(new Vector3(1.42f, 1.26f, 1.62f), new Vector3(1.72f, 1.52f, 1.90f), escalateT);
			Vector3 colBright = Vector3.Lerp(new Vector3(0.86f, 0.16f, 1.38f), new Vector3(1.18f, 0.28f, 1.72f), escalateT);
			Vector3 colDeep = Vector3.Lerp(new Vector3(0.44f, 0.04f, 0.78f), new Vector3(0.68f, 0.10f, 1.10f), escalateT);
			Vector3 colDark = Vector3.Lerp(new Vector3(0.11f, 0.015f, 0.22f), new Vector3(0.20f, 0.035f, 0.34f), escalateT);

			fx.Parameters["uColHot"]?.SetValue(colHot);
			fx.Parameters["uColBright"]?.SetValue(colBright);
			fx.Parameters["uColDeep"]?.SetValue(colDeep);
			fx.Parameters["uColDark"]?.SetValue(colDark);

			fx.Parameters["uMode"]?.SetValue(0f);
			fx.Parameters["uSweep"]?.SetValue(sweep);
			fx.Parameters["uErode"]?.SetValue(erode);
			fx.Parameters["uTailErode"]?.SetValue(tailErode);
			fx.Parameters["uFlash"]?.SetValue(flash);
			fx.Parameters["uFlowPhase"]?.SetValue(flowPhase);
			fx.Parameters["uFlip"]?.SetValue(FlipSign);
			fx.Parameters["uSeed"]?.SetValue(seed);
			fx.Parameters["uArcSpan"]?.SetValue(arcSpan);
			fx.Parameters["uRazorTailWiden"]?.SetValue(0.45f);
			fx.Parameters["uInk"]?.SetValue(0f);
			fx.Parameters["uFeiBai"]?.SetValue(0f);
			fx.Parameters["uBleed"]?.SetValue(0f);
			fx.Parameters["uSplitTail"]?.SetValue(0f);

			Vector2 axisX = (RollAngle + rotOffset).ToRotationVector2();
			Vector2 axisY = new Vector2(-axisX.Y, axisX.X);
			float hx = baseHalfX * scaleMul;
			float hy = baseHalfY * scaleMul;

			Vector2 worldUp = new Vector2(0f, -1f);
			Vector2 farDirLocal = new Vector2(
				Vector2.Dot(worldUp, axisX) / MathF.Max(hx, 1f),
				Vector2.Dot(worldUp, axisY) / MathF.Max(hy, 1f));
			if (farDirLocal.LengthSquared() > 1e-8f)
				farDirLocal.Normalize();

			fx.Parameters["uFarDim"]?.SetValue(0.52f);
			fx.Parameters["uFarDirLocal"]?.SetValue(farDirLocal);

			VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
			verts[0] = new VertexPositionColorTexture(new Vector3(Projectile.Center - axisX * hx - axisY * hy, 0f), Color.White, new Vector2(0f, 0f));
			verts[1] = new VertexPositionColorTexture(new Vector3(Projectile.Center + axisX * hx - axisY * hy, 0f), Color.White, new Vector2(1f, 0f));
			verts[2] = new VertexPositionColorTexture(new Vector3(Projectile.Center - axisX * hx + axisY * hy, 0f), Color.White, new Vector2(0f, 1f));
			verts[3] = new VertexPositionColorTexture(new Vector3(Projectile.Center + axisX * hx + axisY * hy, 0f), Color.White, new Vector2(1f, 1f));

			// Far half first (3D dimmed), then near half — each with crimson body + white-hot core layer
			DrawBladeLayers(device, fx, verts, -1f, opacity, colorShift, frontGlow, thick * thickMul);
			DrawBladeLayers(device, fx, verts, 1f, opacity, colorShift, frontGlow, thick * thickMul);

			device.BlendState = origBlend;
			device.RasterizerState = origRaster;
			device.DepthStencilState = origDepth;

			sb.Restart(in state);
			return false;
		}

		private static void DrawBladeLayers(
			GraphicsDevice device,
			Effect fx,
			VertexPositionColorTexture[] verts,
			float farSel,
			float opacity,
			float colorShift,
			float frontGlow,
			float activeThick)
		{
			fx.Parameters["uFarSel"]?.SetValue(farSel);

			// 1. Primary crimson slash band
			fx.Parameters["uColorShift"]?.SetValue(colorShift);
			fx.Parameters["uOpacity"]?.SetValue(opacity);
			fx.Parameters["uThick"]?.SetValue(activeThick);
			fx.Parameters["uFrontGlow"]?.SetValue(frontGlow);
			DrawQuad(device, fx, verts);

			// 2. Thin white-hot razor core layer
			fx.Parameters["uColorShift"]?.SetValue(0f);
			fx.Parameters["uOpacity"]?.SetValue(opacity * 0.92f);
			fx.Parameters["uThick"]?.SetValue(activeThick * 0.42f);
			fx.Parameters["uFrontGlow"]?.SetValue(frontGlow * 1.25f);
			DrawQuad(device, fx, verts);
		}

		private static void DrawQuad(GraphicsDevice device, Effect fx, VertexPositionColorTexture[] verts)
		{
			foreach (EffectPass pass in fx.CurrentTechnique.Passes)
			{
				pass.Apply();
				device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
			}
		}
	}
}
