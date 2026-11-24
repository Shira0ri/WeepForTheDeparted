using System;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles.Charms
{
	public class PetalVortex : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public override void SetDefaults()
		{
			Projectile.width = 120;
			Projectile.height = 120;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 150; // 2.5 seconds
		}

		public override bool? CanDamage() => false;
		public override bool ShouldUpdatePosition() => false;

		public override void AI()
		{
			// Slow hostile projectiles within vortex radius
			float radius = 90f;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.hostile && Vector2.Distance(Projectile.Center, p.Center) <= radius)
				{
					// Slow projectile down to 50% speed
					if (p.velocity.Length() > 2f)
					{
						p.velocity *= 0.94f;
					}
				}
			}

			// Swirling petals and void sparks
			if (!Main.dedServ && Main.rand.NextBool(2))
			{
				float angle = Main.rand.NextFloat(MathHelper.TwoPi);
				float dist = Main.rand.NextFloat(20f, radius);
				Vector2 pos = Projectile.Center + angle.ToRotationVector2() * dist;
				Vector2 vel = (angle + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(1.5f, 3.5f);
				Color col = Main.rand.NextBool() ? new Color(238, 26, 56) : new Color(185, 45, 255);
				PRTLoader.NewParticle<CrimsonPetalPRT>(pos, vel, col, 0.7f);
			}

			Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.05f, 0.15f));
		}

		public override bool PreDraw(ref Color lightColor) => false;
	}
}
