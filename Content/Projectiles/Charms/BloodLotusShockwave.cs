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
	public class BloodLotusShockwave : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		private const int MaxLifetime = 35;
		private float currentRadius = 10f;
		private const float MaxRadius = 240f;

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
			Projectile.localNPCHitCooldown = 30;
		}

		public override bool? CanDamage() => true;

		public override void AI()
		{
			float progress = 1f - (Projectile.timeLeft / (float)MaxLifetime);
			currentRadius = MathHelper.Lerp(10f, MaxRadius, (float)Math.Sqrt(progress));

			// Update hitbox size to match expanding shockwave
			Projectile.width = (int)(currentRadius * 2f);
			Projectile.height = (int)(currentRadius * 2f);
			Projectile.Center = Projectile.position + new Vector2(Projectile.width * 0.5f, Projectile.height * 0.5f);

			// Lotus petal rings
			if (!Main.dedServ && Main.rand.NextBool())
			{
				float ang = Main.rand.NextFloat(MathHelper.TwoPi);
				Vector2 pos = Projectile.Center + ang.ToRotationVector2() * currentRadius;
				Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2f, 4.5f);
				Color col = Main.rand.NextBool() ? new Color(238, 26, 56) : new Color(185, 45, 255);
				PRTLoader.NewParticle<CrimsonPetalPRT>(pos, vel, col, 0.85f);
			}

			Lighting.AddLight(Projectile.Center, new Vector3(0.45f, 0.05f, 0.20f));
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 4; i++)
			{
				Dust d = Dust.NewDustPerfect(target.Center, DustID.LifeDrain, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.3f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			float progress = 1f - (Projectile.timeLeft / (float)MaxLifetime);
			float alpha = (1f - progress) * 0.85f;
			if (alpha <= 0.01f)
				return false;

			// Draw expanding radiant shockwave ring
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			int segments = 48;
			Vector2 center = Projectile.Center - Main.screenPosition;
			Color ringColor = new Color(238, 26, 56) * alpha;

			for (int i = 0; i < segments; i++)
			{
				float a1 = MathHelper.TwoPi * (i / (float)segments);
				float a2 = MathHelper.TwoPi * ((i + 1) / (float)segments);
				Vector2 p1 = center + a1.ToRotationVector2() * currentRadius;
				Vector2 p2 = center + a2.ToRotationVector2() * currentRadius;

				Vector2 diff = p2 - p1;
				float len = diff.Length();
				float rot = diff.ToRotation();
				Main.spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), ringColor, rot, Vector2.Zero, new Vector2(len, 3.5f), SpriteEffects.None, 0f);
			}

			return false;
		}
	}
}
