using System;
using System.Collections.Generic;
using System.IO;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.UI.NihilityLedger;
using WeepForTheDeparted.Content.Shaders;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles.Charms
{
	public class ThunderCoreBolt : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public const int MaxLifetime = 28;
		public const float StrikeRadius = 110f;
		private const float BoltHalfWidth = 90f;
		private const float ScorchHalfWidth = 115f;
		private const float ScorchHalfHeight = 60f;

		public Vector2 TargetPos;
		public Vector2 StrikeStart;
		private bool struck = false;
		private float seed = -1f;
		private readonly Dictionary<int, int> preHitDebuffs = new Dictionary<int, int>();

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
		}

		public override bool? CanDamage() => false;
		public override bool ShouldUpdatePosition() => false;

		public void Init(Vector2 targetPos)
		{
			TargetPos = targetPos;
			StrikeStart = targetPos - new Vector2(Main.rand.NextFloat(-52f, 52f), 720f);
			Projectile.Center = targetPos;
			seed = Main.rand.NextFloat();
		}

		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.WriteVector2(TargetPos);
			writer.WriteVector2(StrikeStart);
			writer.Write(seed);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			TargetPos = reader.ReadVector2();
			StrikeStart = reader.ReadVector2();
			seed = reader.ReadSingle();
		}

		public override void AI()
		{
			if (seed < 0f)
			{
				seed = (Projectile.identity * 0.6180339887f) % 1f;
			}

			if (!struck)
			{
				struck = true;
				ExecuteStrike();
			}

			float age = MathHelper.Clamp(1f - (Projectile.timeLeft / (float)MaxLifetime), 0f, 1f);
			float lightFade = 1f - age;
			Lighting.AddLight(TargetPos, new Vector3(0.55f, 0.16f, 0.78f) * lightFade);
		}

		private void ExecuteStrike()
		{
			SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.15f + Main.rand.NextFloat(-0.10f, 0.15f), Volume = 0.80f }, TargetPos);
			SoundEngine.PlaySound(SoundID.Thunder with { Pitch = 0.35f, Volume = 0.42f }, TargetPos);

			Player owner = Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers ? Main.player[Projectile.owner] : null;
			NaughtPlayer np = owner != null && owner.active ? owner.GetModPlayer<NaughtPlayer>() : null;

			if (np != null)
			{
				Projectile.CritChance = np.GetNaughtCritChance();
			}

			if (owner != null && owner.whoAmI == Main.myPlayer && !Main.dedServ)
			{
				Main.instance.CameraModifiers.Add(new PunchCameraModifier(TargetPos, Vector2.UnitY, 3.5f, 6f, 8, 1200f, "ThunderCoreBolt"));
			}

			// Deal damage around TargetPos via shared NaughtPlayer scaled TrueMelee StrikeNPC API
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || (npc.dontTakeDamage && npc.type != NPCID.TargetDummy))
					continue;

				if (Vector2.Distance(TargetPos, npc.Center) > StrikeRadius)
					continue;

				preHitDebuffs.Clear();
				for (int b = 0; b < NPC.maxBuffs; b++)
				{
					int bType = npc.buffType[b];
					int bTime = npc.buffTime[b];
					if (bType > 0 && bType < Main.debuff.Length && Main.debuff[bType] && bTime > 0)
					{
						preHitDebuffs[bType] = bTime;
					}
				}

				int hitDir = owner != null && npc.Center.X >= owner.Center.X ? 1 : -1;
				int damageDone;
				if (np != null)
				{
					damageDone = np.StrikeNPC(npc, Projectile.damage, hitDir, crit: true, knockBack: 6f, sourceProj: Projectile, isUltimate: true);
				}
				else
				{
					damageDone = npc.SimpleStrikeNPC(Projectile.damage, hitDir, true, 6f, WeepCompatibility.TrueMelee);
				}

				if (damageDone > 0 && npc.active)
				{
					npc.AddBuff(BuffID.Electrified, 180);

					// Gate all newly applied or refreshed debuffs through the per-barrage +1 Dream / +1 Knot cap
					if (np != null)
					{
						for (int b = 0; b < NPC.maxBuffs; b++)
						{
							int bType = npc.buffType[b];
							int bTime = npc.buffTime[b];
							if (bType > 0 && bType < Main.debuff.Length && Main.debuff[bType] && bTime > 0)
							{
								bool isNew = !preHitDebuffs.TryGetValue(bType, out int prevTime);
								bool isRefreshed = !isNew && bTime > prevTime + 60;
								if (isNew || isRefreshed)
								{
									np.ProcessThunderBarrageDebuff(bType, npc, bTime);
								}
							}
						}
					}
				}
			}

			if (!Main.dedServ)
			{
				for (int i = 0; i < 12; i++)
				{
					float side = Main.rand.NextBool() ? 1f : -1f;
					Vector2 vel = new Vector2(side * Main.rand.NextFloat(2.5f, 7.5f), -Main.rand.NextFloat(0.6f, 3.0f));
					Color col = i % 3 == 0 ? new Color(255, 215, 255) : (i % 2 == 0 ? new Color(185, 45, 255) : new Color(255, 40, 75));
					PRTLoader.NewParticle<VoidSparkPRT>(TargetPos + Main.rand.NextVector2Circular(12f, 6f), vel, col, Main.rand.NextFloat(0.75f, 1.05f));
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;

			float age = MathHelper.Clamp(1f - (Projectile.timeLeft / (float)MaxLifetime), 0f, 1f);
			if (DrawShaderLightning(age))
				return false;

			// Fallback if shader fails to load
			float alpha = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
			Color core = new Color(255, 245, 255) * alpha;
			Color glow = new Color(185, 45, 255) * (0.9f * alpha);

			Vector2 p1 = StrikeStart - Main.screenPosition;
			Vector2 p2 = TargetPos - Main.screenPosition;
			NihilityLedgerTheme.DrawGlowLine(Main.spriteBatch, p1, p2, core, glow, 3.5f, 16f);
			return false;
		}

		private bool DrawShaderLightning(float age)
		{
			Effect fx = ShaderLoader.ThunderCoreShader;
			Texture2D noise = null;
			try
			{
				noise = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/RiftNoise", AssetRequestMode.ImmediateLoad).Value;
			}
			catch
			{
				noise = null;
			}

			if (fx == null || noise == null)
				return false;

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
			fx.Parameters["uSeed"]?.SetValue(seed);
			fx.Parameters["uAge"]?.SetValue(age);
			fx.Parameters["uJitter"]?.SetValue(0.52f);
			fx.Parameters["uBranch"]?.SetValue(0.90f);
			fx.Parameters["uOpacity"]?.SetValue(1.0f);
			fx.Parameters["uColHot"]?.SetValue(new Vector3(1.85f, 1.45f, 1.75f));
			fx.Parameters["uColCrimson"]?.SetValue(new Vector3(1.45f, 0.10f, 0.28f));
			fx.Parameters["uColViolet"]?.SetValue(new Vector3(0.72f, 0.16f, 1.25f));
			fx.Parameters["uNoiseTex"]?.SetValue(noise);

			Vector2 axis = TargetPos - StrikeStart;
			Vector2 perp = axis.LengthSquared() > 1f
				? Vector2.Normalize(new Vector2(-axis.Y, axis.X)) * BoltHalfWidth
				: new Vector2(BoltHalfWidth, 0f);

			fx.CurrentTechnique = fx.Techniques["BoltTech"];
			DrawQuad(device, fx,
				StrikeStart - perp, StrikeStart + perp,
				TargetPos - perp, TargetPos + perp);

			fx.CurrentTechnique = fx.Techniques["ScorchTech"];
			DrawQuad(device, fx,
				TargetPos + new Vector2(-ScorchHalfWidth, -ScorchHalfHeight),
				TargetPos + new Vector2(ScorchHalfWidth, -ScorchHalfHeight),
				TargetPos + new Vector2(-ScorchHalfWidth, ScorchHalfHeight),
				TargetPos + new Vector2(ScorchHalfWidth, ScorchHalfHeight));

			device.BlendState = origBlend;
			device.RasterizerState = origRaster;
			device.DepthStencilState = origDepth;

			sb.Restart(in sbState);
			return true;
		}

		private static void DrawQuad(GraphicsDevice device, Effect fx, Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft, Vector2 bottomRight)
		{
			VertexPositionColorTexture[] verts =
			{
				new VertexPositionColorTexture(new Vector3(topLeft.X, topLeft.Y, 0f), Color.White, new Vector2(0f, 0f)),
				new VertexPositionColorTexture(new Vector3(topRight.X, topRight.Y, 0f), Color.White, new Vector2(1f, 0f)),
				new VertexPositionColorTexture(new Vector3(bottomLeft.X, bottomLeft.Y, 0f), Color.White, new Vector2(0f, 1f)),
				new VertexPositionColorTexture(new Vector3(bottomRight.X, bottomRight.Y, 0f), Color.White, new Vector2(1f, 1f))
			};

			foreach (EffectPass pass in fx.CurrentTechnique.Passes)
			{
				pass.Apply();
				device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
			}
		}
	}
}
