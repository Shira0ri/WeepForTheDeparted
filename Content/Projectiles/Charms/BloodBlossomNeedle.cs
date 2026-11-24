using System;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles.Charms
{
	public class BloodBlossomNeedle : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1; // Infinitely pierces!
			Projectile.timeLeft = 240; // 4 seconds
			Projectile.DamageType = WeepCompatibility.TrueMelee;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 15;
			Projectile.extraUpdates = 1;
		}

		public override void AI()
		{
			// Relentless homing onto nearest live enemy
			NPC target = null;
			float nearestDistSq = float.MaxValue;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.lifeMax > 5)
				{
					float dSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
					if (dSq < nearestDistSq && dSq < 1400f * 1400f)
					{
						nearestDistSq = dSq;
						target = npc;
					}
				}
			}

			if (target != null)
			{
				Vector2 desiredVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 20f;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.16f);
			}

			Projectile.rotation = Projectile.velocity.ToRotation();

			// Crimson flower needle trail
			if (!Main.dedServ && Main.rand.NextBool(3))
			{
				PRTLoader.NewParticle<CrimsonPetalPRT>(Projectile.Center, -Projectile.velocity * 0.2f, new Color(245, 30, 60), 0.55f);
			}

			Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.05f, 0.15f));
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 3; i++)
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.LifeDrain, Main.rand.NextVector2Circular(2f, 2f), 0, default, 1.1f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Vector2 drawPos = Projectile.Center - Main.screenPosition;
			Vector2 needleSize = new Vector2(28f, 3f);
			Color col = new Color(255, 230, 240);
			Color glow = new Color(238, 26, 56) * 0.9f;

			// Draw glowing needle blade
			Main.spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), glow, Projectile.rotation, new Vector2(0.5f, 0.5f), needleSize + new Vector2(6f, 3f), SpriteEffects.None, 0f);
			Main.spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), col, Projectile.rotation, new Vector2(0.5f, 0.5f), needleSize, SpriteEffects.None, 0f);

			return false;
		}
	}
}
