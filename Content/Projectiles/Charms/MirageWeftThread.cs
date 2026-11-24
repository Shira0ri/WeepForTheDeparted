using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.UI.NihilityLedger;

namespace WeepForTheDeparted.Content.Projectiles.Charms
{
	public class MirageWeftThread : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public Player Owner => Main.player[Projectile.owner];

		private readonly Dictionary<int, int> hitCooldowns = new Dictionary<int, int>();

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 120; // 2.0 seconds
			Projectile.DamageType = WeepCompatibility.TrueMelee;
		}

		public override bool? CanDamage() => false; // Custom damage checks in AI
		public override bool ShouldUpdatePosition() => false;

		public override void AI()
		{
			if (Owner == null || !Owner.active)
			{
				Projectile.Kill();
				return;
			}

			// Decrement hit cooldowns
			if (hitCooldowns.Count > 0)
			{
				var keys = new List<int>(hitCooldowns.Keys);
				foreach (int k in keys)
				{
					if (hitCooldowns[k] > 0)
						hitCooldowns[k]--;
					else
						hitCooldowns.Remove(k);
				}
			}

			// Find all active clones belonging to Owner
			List<Vector2> clonePositions = new List<Vector2>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == Projectile.owner && p.type == ModContent.ProjectileType<NaughtClone>())
				{
					clonePositions.Add(p.Center);
				}
			}

			if (clonePositions.Count == 0)
				return;

			Vector2 origin = Owner.Center;

			// Check collisions along line from Owner to each clone
			for (int c = 0; c < clonePositions.Count; c++)
			{
				Vector2 target = clonePositions[c];

				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC npc = Main.npc[i];
					if (npc.active && !npc.friendly && !npc.dontTakeDamage && !hitCooldowns.ContainsKey(npc.whoAmI))
					{
						float collisionPoint = 0f;
						if (Collision.CheckAABBvLineCollision(npc.Hitbox.TopLeft(), npc.Hitbox.Size(), origin, target, 16f, ref collisionPoint))
						{
							hitCooldowns[npc.whoAmI] = 60; // 1s cooldown per enemy
							int damage = (int)(Projectile.damage * 1.50f);
							npc.SimpleStrikeNPC(damage, Owner.direction, false, 4f, DamageClass.Melee);
							npc.AddBuff(BuffID.Slow, 120);

							// Spark burst
							for (int s = 0; s < 4; s++)
							{
								Dust d = Dust.NewDustPerfect(npc.Center, DustID.LifeDrain, Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.2f);
								d.noGravity = true;
							}
						}
					}
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Owner == null || !Owner.active)
				return false;

			float alpha = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
			Color threadCore = new Color(255, 230, 240) * alpha;
			Color threadGlow = new Color(238, 26, 56) * (0.85f * alpha);

			Vector2 origin = Owner.Center - Main.screenPosition;

			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == Projectile.owner && p.type == ModContent.ProjectileType<NaughtClone>())
				{
					Vector2 clonePos = p.Center - Main.screenPosition;
					NihilityLedgerTheme.DrawGlowLine(Main.spriteBatch, origin, clonePos, threadCore, threadGlow, 2.0f, 8f);
				}
			}

			return false;
		}
	}
}
