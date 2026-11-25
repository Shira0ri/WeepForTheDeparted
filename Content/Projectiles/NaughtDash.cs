using System;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.GlobalNPCs;
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
		private bool fleetingStepTriggeredThisDash = false;

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

				var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();

				// 1. Leave the decoy clone at the dash origin!
				if (Main.myPlayer == Owner.whoAmI)
				{
					int cloneType = ModContent.ProjectileType<NaughtClone>();
					int existingRemaining = -1;

					if (naughtPlayer.HasShadowMarionette)
					{
						// Shadow Marionette: Cap concurrent clones at 4, silently evicting the oldest clone(s)
						var activeClones = new System.Collections.Generic.List<NaughtClone>(8);
						for (int i = 0; i < Main.maxProjectiles; i++)
						{
							Projectile p = Main.projectile[i];
							if (p.active && p.owner == Owner.whoAmI && p.type == cloneType && p.ModProjectile is NaughtClone existingClone)
							{
								activeClones.Add(existingClone);
							}
						}

						if (activeClones.Count >= NaughtClone.MaxMarionetteClones)
						{
							activeClones.Sort(static (a, b) =>
							{
								int seqCmp = a.SpawnSequence.CompareTo(b.SpawnSequence);
								return seqCmp != 0 ? seqCmp : a.Projectile.whoAmI.CompareTo(b.Projectile.whoAmI);
							});

							int toRemove = activeClones.Count - NaughtClone.MaxMarionetteClones + 1;
							for (int r = 0; r < toRemove; r++)
							{
								activeClones[r].SuppressCycleOnKill = true;
								activeClones[r].Projectile.Kill();
							}
						}
					}
					else
					{
						// Basekit & other charms: Account for Main.projectile update order so slot reuse never causes a 1-tick expiry desync
						for (int i = 0; i < Main.maxProjectiles; i++)
						{
							Projectile p = Main.projectile[i];
							if (p.active && p.owner == Owner.whoAmI && p.type == cloneType)
							{
								int rem = p.timeLeft - (i > Projectile.whoAmI ? 1 : 0);
								if (rem > 0)
								{
									existingRemaining = rem;
									break;
								}
							}
						}
					}

					int cloneIdx = Projectile.NewProjectile(
						Projectile.GetSource_FromThis(),
						Owner.position,
						Vector2.Zero,
						cloneType,
						0,
						0f,
						Owner.whoAmI
					);

					if (cloneIdx >= 0 && cloneIdx < Main.maxProjectiles && Main.projectile[cloneIdx].ModProjectile is NaughtClone clone)
					{
						if (naughtPlayer.HasShadowMarionette)
						{
							clone.IsMarionetteIndefinite = true;
							clone.Projectile.timeLeft = NaughtClone.DefaultLifetime;
						}
						else
						{
							int syncTime = existingRemaining > 0
								? existingRemaining + (cloneIdx > Projectile.whoAmI ? 1 : 0)
								: NaughtClone.DefaultLifetime;
							clone.Projectile.timeLeft = Math.Max(1, syncTime);
						}

						clone.SpawnSequence = naughtPlayer.AllocateCloneSequence();
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

				// Set dash cooldown on player (50% off with MirageWeft, 25% off with FleetingStep)
				naughtPlayer.DashCooldown = naughtPlayer.HasMirageWeft ? 30 : (naughtPlayer.HasFleetingStep ? 45 : 60);

				if (Main.myPlayer == Owner.whoAmI)
				{
					// Mirage Weft: Maintain a single active thread controller while clones persist
					if (naughtPlayer.HasMirageWeft && !HasActiveMirageWeftThread(Owner.whoAmI))
					{
						int threadDmg = naughtPlayer.GetNaughtDamage(0.25f);
						int threadIdx = Projectile.NewProjectile(
							Projectile.GetSource_FromThis(),
							Owner.Center,
							Vector2.Zero,
							ModContent.ProjectileType<MirageWeftThread>(),
							threadDmg,
							0f,
							Owner.whoAmI);
						if (threadIdx >= 0 && threadIdx < Main.maxProjectiles)
						{
							Main.projectile[threadIdx].CritChance = naughtPlayer.GetNaughtCritChance();
						}
					}

					// Fleeting Petal Step: Dashing through enemies restores +1 Slashed Dream & activates 3s global hostile projectile freeze/drop (30s shared cooldown)
					TryTriggerFleetingStep(naughtPlayer, checkSweptPath: true);
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

			// Catch moving bosses or worm segments that enter the player's path mid-dash
			if (!fleetingStepTriggeredThisDash)
			{
				TryTriggerFleetingStep(Owner.GetModPlayer<NaughtPlayer>(), checkSweptPath: false);
			}

			// Ambient PRT void sparks along dash path
			if (!Main.dedServ && Main.rand.NextBool(2))
			{
				Vector2 sparkVel = -dashDir * Main.rand.NextFloat(2f, 5f) + Main.rand.NextVector2Circular(2f, 2f);
				Color sCol = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
				PRTLoader.NewParticle<VoidSparkPRT>(Owner.MountedCenter, sparkVel, sCol, 0.85f);
			}

			Projectile.Center = Owner.MountedCenter;
		}

		private static bool IsFleetingStepTarget(NPC npc)
		{
			if (npc == null || !npc.active || npc.friendly || npc.isLikeATownNPC || npc.lifeMax <= 5)
				return false;

			return !npc.dontTakeDamage
				|| CrimsonKnotGlobalNPC.IsBossTarget(npc)
				|| npc.type == NPCID.TargetDummy;
		}

		private void TryTriggerFleetingStep(NaughtPlayer naughtPlayer, bool checkSweptPath)
		{
			if (naughtPlayer == null || Main.myPlayer != Owner.whoAmI || !naughtPlayer.HasFleetingStep)
				return;

			if (fleetingStepTriggeredThisDash || naughtPlayer.FleetingStepDreamCooldown > 0)
				return;

			Vector2 halfSize = Owner.Size * 0.5f;
			Vector2 startCenter = startPos + halfSize;
			Vector2 endCenter = targetPos + halfSize;
			float sweepWidth = Math.Max(Owner.width, Owner.height);

			Rectangle currentHitbox = Owner.Hitbox;
			Rectangle startHitbox = new Rectangle((int)startPos.X, (int)startPos.Y, Owner.width, Owner.height);
			Rectangle endHitbox = new Rectangle((int)targetPos.X, (int)targetPos.Y, Owner.width, Owner.height);

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!IsFleetingStepTarget(npc))
					continue;

				bool hit;
				if (checkSweptPath)
				{
					float collisionPoint = 0f;
					hit = startHitbox.Intersects(npc.Hitbox)
						|| endHitbox.Intersects(npc.Hitbox)
						|| (startCenter != endCenter && Collision.CheckAABBvLineCollision(
							npc.Hitbox.TopLeft(), npc.Hitbox.Size(), startCenter, endCenter, sweepWidth, ref collisionPoint));
				}
				else
				{
					hit = currentHitbox.Intersects(npc.Hitbox);
				}

				if (!hit)
					continue;

				fleetingStepTriggeredThisDash = true;
				naughtPlayer.AddSlashedDream(1);
				naughtPlayer.FleetingStepDreamCooldown = 1800; // 30.0s shared cooldown
				naughtPlayer.FleetingStepSlowTimer = 180;      // 3.0s global hostile projectile suspension/drop

				SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.65f, Volume = 0.7f }, Owner.MountedCenter);

				if (!Main.dedServ)
				{
					Vector2 burstCenter = checkSweptPath ? Vector2.Lerp(startCenter, endCenter, 0.5f) : Owner.MountedCenter;
					for (int p = 0; p < 12; p++)
					{
						Vector2 pVel = Main.rand.NextVector2Circular(4.5f, 4.5f);
						Color pCol = Main.rand.NextBool() ? new Color(238, 26, 56, 230) : new Color(185, 45, 255, 220);
						PRTLoader.NewParticle<CrimsonPetalPRT>(burstCenter + Main.rand.NextVector2Circular(16f, 16f), pVel, pCol, 0.8f);
					}
					for (int s = 0; s < 8; s++)
					{
						Vector2 sVel = Main.rand.NextVector2Circular(5.5f, 5.5f);
						Color sCol = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
						PRTLoader.NewParticle<VoidSparkPRT>(Owner.MountedCenter, sVel, sCol, 0.9f);
					}
				}

				break;
			}
		}

		private static bool HasActiveMirageWeftThread(int ownerWhoAmI)
		{
			int threadType = ModContent.ProjectileType<MirageWeftThread>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == ownerWhoAmI && p.type == threadType)
					return true;
			}
			return false;
		}

		public override bool PreDraw(ref Color lightColor) => false;
	}
}
