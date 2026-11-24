using System;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles
{
	public class CrimsonKnotTransferBeam : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public const int BeamDuration = 22;

		private Vector2 startPos;
		private bool initialized = false;

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.aiStyle = -1;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = BeamDuration;
		}

		public static void SpawnBeam(IEntitySource source, Vector2 fromPos, int targetWhoAmI)
		{
			if (Main.netMode == NetmodeID.Server)
				return;

			int idx = Projectile.NewProjectile(source, fromPos, Vector2.Zero, ModContent.ProjectileType<CrimsonKnotTransferBeam>(), 0, 0f, Main.myPlayer, targetWhoAmI);
			if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].ModProjectile is CrimsonKnotTransferBeam beam)
			{
				beam.startPos = fromPos;
				beam.initialized = true;
			}
		}

		public override void AI()
		{
			if (!initialized)
			{
				initialized = true;
				startPos = Projectile.Center;
			}

			int targetIdx = (int)Projectile.ai[0];
			if (targetIdx < 0 || targetIdx >= Main.maxNPCs || !Main.npc[targetIdx].active)
			{
				Projectile.Kill();
				return;
			}

			NPC target = Main.npc[targetIdx];
			Vector2 targetPos = target.Center;

			// Particles traveling along the tether from source to target via InnoVault PRT
			if (!Main.dedServ && Main.rand.NextBool(2))
			{
				float progress = Main.rand.NextFloat(0.1f, 0.95f);
				Vector2 particlePos = Vector2.Lerp(startPos, targetPos, progress);
				Vector2 vel = startPos.To(targetPos).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(3f, 7f);
				Color col = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
				PRTLoader.NewParticle<VoidSparkPRT>(particlePos, vel, col, 0.85f);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			int targetIdx = (int)Projectile.ai[0];
			if (targetIdx < 0 || targetIdx >= Main.maxNPCs || !Main.npc[targetIdx].active)
				return false;

			NPC target = Main.npc[targetIdx];
			Vector2 targetPos = target.Center;
			Vector2 diff = startPos.To(targetPos);
			float length = diff.Length();
			if (length < 2f)
				return false;

			float rotation = diff.ToRotation();
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (pixel == null)
				return false;

			// Fade in fast, fade out towards end
			float lifeRatio = Projectile.timeLeft / (float)BeamDuration;
			float alpha = MathHelper.Clamp((float)Math.Sin(lifeRatio * MathHelper.Pi) * 1.25f, 0f, 1f);

			Vector2 screenStart = startPos - Main.screenPosition;
			Vector2 origin = new Vector2(0f, 0.5f);

			SpriteBatchState state = Main.spriteBatch.Capture();
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

			// 1. Wide outer Nihility crimson glow
			Color crimsonGlow = new Color(245, 25, 80) * (alpha * 0.70f);
			Main.spriteBatch.Draw(pixel, screenStart, new Rectangle(0, 0, 1, 1), crimsonGlow, rotation, origin, new Vector2(length, 10f), SpriteEffects.None, 0f);

			// 2. Mid violet resonance line
			Color violetLine = new Color(175, 45, 240) * (alpha * 0.85f);
			Main.spriteBatch.Draw(pixel, screenStart, new Rectangle(0, 0, 1, 1), violetLine, rotation, origin, new Vector2(length, 5f), SpriteEffects.None, 0f);

			// 3. Hot white core lightning wire
			Color whiteCore = new Color(255, 240, 255) * alpha;
			Main.spriteBatch.Draw(pixel, screenStart, new Rectangle(0, 0, 1, 1), whiteCore, rotation, origin, new Vector2(length, 2f), SpriteEffects.None, 0f);

			Main.spriteBatch.Restart(in state);
			return false;
		}
	}
}
