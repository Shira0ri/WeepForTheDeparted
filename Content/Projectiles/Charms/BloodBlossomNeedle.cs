using System;
using System.Collections.Generic;
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
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Content.Shaders;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles.Charms
{
	public class BloodBlossomNeedle : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public const int MaxActivePerPlayer = 36;
		private const int MaxTrailPoints = 16;
		private const int DefaultLaunchUpdates = 36; // 18 real ticks (with extraUpdates = 1)
		private const float HomingRange = 1400f;
		private const float CruiseSpeed = 18f;

		private readonly List<Vector2> trailPoints = new List<Vector2>(MaxTrailPoints);
		private bool initialized;
		private int ageUpdates;
		private int launchBurstUpdates = DefaultLaunchUpdates;
		private float shaderSeed;

		/// <summary>
		/// Encoded in ai[0] as (rootNpcWhoAmI + 1) so default 0f safely means no origin (-1).
		/// </summary>
		private int OriginRootWhoAmI => (int)Projectile.ai[0] - 1;

		public static int GetRootNpcIndex(NPC npc)
		{
			if (npc == null)
				return -1;
			return (npc.realLife >= 0 && npc.realLife < Main.maxNPCs) ? npc.realLife : npc.whoAmI;
		}

		/// <summary>
		/// Enforces a per-player active needle cap by retiring the oldest needle when at limit.
		/// </summary>
		public static void EnforceCap(int ownerWhoAmI, int maxActive = MaxActivePerPlayer)
		{
			int activeCount = 0;
			Projectile oldest = null;
			int minTimeLeft = int.MaxValue;
			int needleType = ModContent.ProjectileType<BloodBlossomNeedle>();

			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == ownerWhoAmI && p.type == needleType)
				{
					activeCount++;
					if (p.timeLeft < minTimeLeft)
					{
						minTimeLeft = p.timeLeft;
						oldest = p;
					}
				}
			}

			if (activeCount >= maxActive && oldest != null)
			{
				oldest.Kill();
			}
		}

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;
		}

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1; // Infinitely pierces
			Projectile.timeLeft = 240; // 120 real ticks (2.0s active flight with extraUpdates = 1)
			Projectile.DamageType = WeepCompatibility.TrueMelee;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 15; // 7.5 real ticks per NPC
			Projectile.extraUpdates = 1;
		}

		public override bool? CanHitNPC(NPC target)
		{
			// Prevent tagging the originating enemy on spawn before clearing the outward blossom arc
			if (ageUpdates < launchBurstUpdates && OriginRootWhoAmI >= 0 && GetRootNpcIndex(target) == OriginRootWhoAmI)
				return false;

			return null;
		}

		public override void AI()
		{
			if (!initialized)
			{
				initialized = true;
				shaderSeed = Main.rand.NextFloat(10f);
				launchBurstUpdates = Projectile.ai[1] > 0f
					? Math.Clamp((int)Projectile.ai[1], 28, 44)
					: DefaultLaunchUpdates;

				for (int i = 0; i < MaxTrailPoints; i++)
				{
					trailPoints.Add(Projectile.Center);
				}

				if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
				{
					Player owner = Main.player[Projectile.owner];
					if (owner.active && owner.TryGetModPlayer<NaughtPlayer>(out var np))
					{
						Projectile.CritChance = np.GetNaughtCritChance();
					}
				}
			}

			ageUpdates++;

			if (ageUpdates < launchBurstUpdates)
			{
				// Initial outward blossom burst before homing engages
				float speed = Projectile.velocity.Length();
				if (speed > 8f)
				{
					Projectile.velocity *= 0.975f;
				}
				else if (speed < 0.1f)
				{
					Projectile.velocity = Vector2.UnitX.RotatedBy(Main.rand.NextFloat(MathHelper.TwoPi)) * 10f;
				}
			}
			else
			{
				// Prioritize other valid enemies first; fall back to origin target only in 1v1 fights
				NPC target = FindHomingTarget();
				if (target != null)
				{
					Vector2 toTarget = target.Center - Projectile.Center;
					float dist = toTarget.Length();
					float targetAngle = toTarget.ToRotation();
					float currentAngle = Projectile.velocity.LengthSquared() > 0.01f
						? Projectile.velocity.ToRotation()
						: targetAngle;

					bool passedThroughClose = dist < 180f && Vector2.Dot(Projectile.velocity, toTarget) < 0f;
					float maxTurn = passedThroughClose
						? 0.30f
						: MathHelper.Lerp(0.14f, 0.24f, 1f - MathHelper.Clamp(dist / 600f, 0f, 1f));

					float desiredSpeed = passedThroughClose ? 13.5f : CruiseSpeed;
					float currentSpeed = Projectile.velocity.Length();
					float newSpeed = MathHelper.Lerp(currentSpeed, desiredSpeed, 0.18f);

					float newAngle = currentAngle.AngleTowards(targetAngle, maxTurn);
					Projectile.velocity = newAngle.ToRotationVector2() * newSpeed;
				}
			}

			Projectile.rotation = Projectile.velocity.LengthSquared() > 0.01f
				? Projectile.velocity.ToRotation()
				: Projectile.rotation;

			// Record trail points (head at index 0)
			trailPoints.Insert(0, Projectile.Center);
			if (trailPoints.Count > MaxTrailPoints)
			{
				trailPoints.RemoveAt(trailPoints.Count - 1);
			}

			// Crimson petal trail
			if (!Main.dedServ && Main.rand.NextBool(4))
			{
				PRTLoader.NewParticle<CrimsonPetalPRT>(Projectile.Center, -Projectile.velocity * 0.18f, new Color(245, 30, 60), 0.50f);
			}

			Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.05f, 0.15f));
		}

		private NPC FindHomingTarget()
		{
			int originRoot = OriginRootWhoAmI;
			float maxDistSq = HomingRange * HomingRange;

			NPC bestOther = null;
			float bestOtherDistSq = maxDistSq;

			NPC bestOrigin = null;
			float bestOriginDistSq = maxDistSq;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
					continue;

				if (!npc.CanBeChasedBy() && npc.type != NPCID.TargetDummy)
					continue;

				float dSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
				if (dSq >= maxDistSq)
					continue;

				bool isOrigin = originRoot >= 0 && GetRootNpcIndex(npc) == originRoot;
				if (isOrigin)
				{
					if (dSq < bestOriginDistSq)
					{
						bestOriginDistSq = dSq;
						bestOrigin = npc;
					}
				}
				else
				{
					if (dSq < bestOtherDistSq)
					{
						bestOtherDistSq = dSq;
						bestOther = npc;
					}
				}
			}

			return bestOther ?? bestOrigin;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Main.dedServ)
				return;

			for (int i = 0; i < 3; i++)
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.LifeDrain, Main.rand.NextVector2Circular(2.2f, 2.2f), 0, default, 1.1f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ || trailPoints.Count < 2)
				return false;

			float lifeFade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
			if (lifeFade <= 0.01f)
				return false;

			Effect fx = ShaderLoader.BloodBlossomNeedleShader;
			Texture2D noise = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/NoiseSoft01", AssetRequestMode.ImmediateLoad).Value;

			if (fx != null && noise != null)
			{
				DrawShaderRibbon(fx, noise, lifeFade);
			}
			else
			{
				DrawFallbackNeedle(lifeFade);
			}

			return false;
		}

		private void DrawShaderRibbon(Effect fx, Texture2D noise, float lifeFade)
		{
			SpriteBatch sb = Main.spriteBatch;
			GraphicsDevice device = Main.graphics.GraphicsDevice;
			SpriteBatchState sbState = sb.Capture();
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
			fx.Parameters["uSeed"]?.SetValue(shaderSeed);
			fx.Parameters["uIntensity"]?.SetValue(1.15f);
			fx.Parameters["uNoiseTex"]?.SetValue(noise);

			int count = trailPoints.Count;
			// Extend the needle tip slightly ahead of Projectile.Center along velocity/rotation
			Vector2 headDir = Projectile.rotation.ToRotationVector2();
			Vector2 tipPos = trailPoints[0] + headDir * 16f;

			int totalNodes = count + 1;
			VertexPositionColorTexture[] verts = new VertexPositionColorTexture[totalNodes * 2];
			Color vertColor = new Color(255, 255, 255, (int)(lifeFade * 255f));

			for (int i = 0; i < totalNodes; i++)
			{
				// Index 0 = tail, totalNodes - 1 = tip
				float u = i / (float)(totalNodes - 1);
				Vector2 pos = i == totalNodes - 1 ? tipPos : trailPoints[count - 1 - i];

				Vector2 tangent;
				if (i == totalNodes - 1)
				{
					tangent = headDir;
				}
				else if (i == 0)
				{
					Vector2 nextPos = i + 1 == totalNodes - 1 ? tipPos : trailPoints[count - 2];
					tangent = (nextPos - pos).SafeNormalize(headDir);
				}
				else
				{
					Vector2 prevPos = trailPoints[count - i];
					Vector2 nextPos = i + 1 == totalNodes - 1 ? tipPos : trailPoints[count - 2 - i];
					tangent = (nextPos - prevPos).SafeNormalize(headDir);
				}

				Vector2 normal = new Vector2(-tangent.Y, tangent.X);
				float halfWidth = MathHelper.Lerp(8f, 13f, MathF.Sin(u * MathHelper.Pi * 0.85f));

				verts[i * 2] = new VertexPositionColorTexture(new Vector3(pos - normal * halfWidth, 0f), vertColor, new Vector2(u, 0f));
				verts[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(pos + normal * halfWidth, 0f), vertColor, new Vector2(u, 1f));
			}

			foreach (EffectPass pass in fx.CurrentTechnique.Passes)
			{
				pass.Apply();
				device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, totalNodes * 2 - 2);
			}

			device.BlendState = origBlend;
			device.RasterizerState = origRaster;
			device.DepthStencilState = origDepth;

			sb.Restart(in sbState);
		}

		private void DrawFallbackNeedle(float lifeFade)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Vector2 drawPos = Projectile.Center - Main.screenPosition;
			Vector2 needleSize = new Vector2(28f, 3f);
			Color col = new Color(255, 230, 240) * lifeFade;
			Color glow = new Color(238, 26, 56) * (0.9f * lifeFade);

			Main.spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), glow, Projectile.rotation, new Vector2(0.5f, 0.5f), needleSize + new Vector2(6f, 3f), SpriteEffects.None, 0f);
			Main.spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), col, Projectile.rotation, new Vector2(0.5f, 0.5f), needleSize, SpriteEffects.None, 0f);
		}
	}
}
