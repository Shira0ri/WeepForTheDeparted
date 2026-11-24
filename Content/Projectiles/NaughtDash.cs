using System;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Content.Projectiles.Charms;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles
{
	public class NaughtDash : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public Player Owner => Main.player[Projectile.owner];

		private const int DashDuration = 7;
		private Vector2 startPos;
		private Vector2 targetPos;
		private Vector2 dashDir;
		private float totalDist;
		private bool initialized = false;

		public override void SetDefaults()
		{
			Projectile.width = 32;
			Projectile.height = 32;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = DashDuration;
		}

		public override bool? CanDamage() => false;

		public override void AI()
		{
			if (Owner == null || !Owner.active || Owner.dead)
			{
				Projectile.Kill();
				return;
			}

			if (!initialized)
			{
				initialized = true;
				startPos = Owner.position;

				Vector2 toMouse = Owner.MountedCenter.To(Main.MouseWorld);
				if (toMouse == Vector2.Zero)
					toMouse = Vector2.UnitX * Owner.direction;

				dashDir = Vector2.Normalize(toMouse);
				Owner.direction = dashDir.X >= 0 ? 1 : -1;

				// Calculate safe dash distance stopping at solid terrain
				float maxReach = MathHelper.Clamp(toMouse.Length(), 140f, 320f);
				totalDist = maxReach;
				targetPos = startPos;

				for (float d = 16f; d <= maxReach; d += 12f)
				{
					Vector2 checkPos = startPos + dashDir * d;
					Vector2 resolvedVel = Collision.TileCollision(checkPos, dashDir * 0.1f, Owner.width, Owner.height, true, true);
					if (resolvedVel != dashDir * 0.1f)
					{
						totalDist = Math.Max(0f, d - 12f);
						targetPos = startPos + dashDir * totalDist;
						break;
					}
					targetPos = checkPos;
				}

				// 1. Leave the 9-second decoy clone at the dash origin (synchronized timer)!
				if (Main.myPlayer == Owner.whoAmI)
				{
					// Look up existing clone's remaining time so all clones expire together
					int syncTime = NaughtClone.DefaultLifetime;
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i];
						if (p.active && p.owner == Owner.whoAmI && p.type == ModContent.ProjectileType<NaughtClone>())
						{
							syncTime = p.timeLeft;
							break;
						}
					}

					int cloneIdx = Projectile.NewProjectile(
						Projectile.GetSource_FromThis(),
						Owner.position,
						Vector2.Zero,
						ModContent.ProjectileType<NaughtClone>(),
						0,
						0f,
						Owner.whoAmI
					);

					if (cloneIdx >= 0 && cloneIdx < Main.maxProjectiles && Main.projectile[cloneIdx].ModProjectile is NaughtClone clone)
					{
						clone.Projectile.timeLeft = Math.Max(2, syncTime);
						clone.CapturePose(Owner);
					}

					// 2. Spawn the OniKamui flowing spatial ribbon trail!
					int trailIdx = Projectile.NewProjectile(
						Projectile.GetSource_FromThis(),
						(startPos + targetPos) * 0.5f,
						Vector2.Zero,
						ModContent.ProjectileType<NaughtDashTrail>(),
						0,
						0f,
						Owner.whoAmI
					);

					if (trailIdx >= 0 && trailIdx < Main.maxProjectiles && Main.projectile[trailIdx].ModProjectile is NaughtDashTrail trail)
					{
						Vector2 pStart = startPos + new Vector2(Owner.width * 0.5f, Owner.height * 0.5f);
						Vector2 pEnd = targetPos + new Vector2(Owner.width * 0.5f, Owner.height * 0.5f);
						trail.InitTrail(pStart, pEnd);
					}
				}

				// 3. Play Onikiri's katana sprint audio!
				SoundEngine.PlaySound(WeepSound.KatanaSprint with { Volume = 0.85f, Pitch = 0.05f }, Owner.MountedCenter);

				// Set dash cooldown on player (reduced by 25% if FleetingStep is equipped)
				var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
				naughtPlayer.DashCooldown = naughtPlayer.HasFleetingStep ? 45 : 60;

				if (Main.myPlayer == Owner.whoAmI)
				{
					// Mirage Weft: Spawn cutting thread net connecting player to all active clones
					if (naughtPlayer.HasMirageWeft)
					{
						int weaponDmg = Owner.HeldItem?.damage ?? 100;
						Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center, Vector2.Zero, ModContent.ProjectileType<MirageWeftThread>(), weaponDmg, 0f, Owner.whoAmI);

						// Dashing through an active clone resets Dash cooldown (3s internal cooldown)
						if (naughtPlayer.MirageWeftResetCooldown <= 0)
						{
							for (int i = 0; i < Main.maxProjectiles; i++)
							{
								Projectile p = Main.projectile[i];
								if (p.active && p.owner == Owner.whoAmI && p.type == ModContent.ProjectileType<NaughtClone>())
								{
									float collisionPoint = 0f;
									if (Collision.CheckAABBvLineCollision(p.Hitbox.TopLeft(), p.Hitbox.Size(), startPos, targetPos, 32f, ref collisionPoint))
									{
										naughtPlayer.DashCooldown = 0;
										naughtPlayer.MirageWeftResetCooldown = 180;
										SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.35f, Volume = 0.85f }, Owner.Center);
										break;
									}
								}
							}
						}
					}

					// Fleeting Petal Step: Dashing through enemies grants Slashed Dream & spawns slowing Petal Vortex
					if (naughtPlayer.HasFleetingStep)
					{
						for (int i = 0; i < Main.maxNPCs; i++)
						{
							NPC npc = Main.npc[i];
							if (npc.active && !npc.friendly && !npc.dontTakeDamage)
							{
								float collisionPoint = 0f;
								if (Collision.CheckAABBvLineCollision(npc.Hitbox.TopLeft(), npc.Hitbox.Size(), startPos, targetPos, 24f, ref collisionPoint))
								{
									if (naughtPlayer.FleetingStepDreamCooldown <= 0)
									{
										naughtPlayer.AddSlashedDream(1);
										naughtPlayer.FleetingStepDreamCooldown = 300; // 5s cooldown
									}
									Projectile.NewProjectile(Projectile.GetSource_FromThis(), targetPos, Vector2.Zero, ModContent.ProjectileType<PetalVortex>(), 0, 0f, Owner.whoAmI);
									break;
								}
							}
						}
					}
				}
			}

			// Evasion & invulnerability during flash step
			Owner.immune = true;
			Owner.immuneTime = 16;
			Owner.immuneNoBlink = true;

			// Smooth interpolation from start to destination using InnoVault easing
			float progress = 1f - (Projectile.timeLeft / (float)DashDuration);
			float ease = VaultUtils.EaseInOutQuad(progress);
			Owner.position = Vector2.Lerp(startPos, targetPos, ease);
			Owner.velocity = dashDir * 3f;

			// Ambient PRT void sparks along dash path
			if (!Main.dedServ && Main.rand.NextBool(2))
			{
				Vector2 sparkVel = -dashDir * Main.rand.NextFloat(2f, 5f) + Main.rand.NextVector2Circular(2f, 2f);
				Color sCol = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
				PRTLoader.NewParticle<VoidSparkPRT>(Owner.MountedCenter, sparkVel, sCol, 0.85f);
			}

			Projectile.Center = Owner.MountedCenter;
		}

		public override bool PreDraw(ref Color lightColor) => false;
	}
}
