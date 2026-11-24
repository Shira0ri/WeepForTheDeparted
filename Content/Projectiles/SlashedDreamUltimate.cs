using System;
using System.Collections.Generic;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.GlobalNPCs;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.Systems;
using WeepForTheDeparted.Common.UI.NihilityLedger;
using WeepForTheDeparted.Common.Visuals;
using WeepForTheDeparted.Common.Visuals.Dismember;
using WeepForTheDeparted.Content.Items.Weapons;
using WeepForTheDeparted.Content.Projectiles.Charms;
using WeepForTheDeparted.Content.Shaders;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles
{
	public class SlashedDreamUltimate : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/NaughtUltBlade";

		public Player Owner => Main.player[Projectile.owner];

		public int Timer
		{
			get => (int)Projectile.ai[0];
			set => Projectile.ai[0] = value;
		}

		// Slashing stages:
		// 0: Unsheathing stance (1 second = 60 frames)
		// 1: Rainblade 1 executed
		// 2: Rainblade 2 executed
		// 3: Rainblade 3 executed
		// 4: Stygian Resurge (finale) executed
		// 5: Finished
		public int SlashStage
		{
			get => (int)Projectile.ai[1];
			set => Projectile.ai[1] = value;
		}

		// Unsheath animation duration: 240 frames (4.0 seconds total)
		public const int UnsheathDuration = 240;

		// 9 seconds to slash at leisure (540 ticks) starting AFTER unsheathing finishes
		public const int MaxSlashingTime = 540;
		private int remainingSlashingTime = MaxSlashingTime;

		private int slashCooldown = 0;
		private int holdTimer = 0;
		private int postResurgeTimer = 0;
		private float lastSlashAngle = -999f;

		// Swing animation state
		private bool isSwinging = false;
		private int swingTimer = 0;
		private int swingDuration = 34;
		private float swingStartAngle = 0f;
		private float swingEndAngle = 0f;
		private int swingType = 0; // 0 = diagonal, 1 = rising, 2 = horizontal sideways, 3 = resurge
		private float swingImpactProgress = 0.35f;

		// Pending delayed impact strike state
		private bool hasPendingStrike = false;
		private int pendingStrikeIndex = 0;
		private float pendingStrikeAngle = 0f;
		private float pendingStrikeTilt = 0f;
		private int impactDelayTimer = 0;
		private int hitstopFrames = 0;

		// Reverse sheathing animation state after final slash
		private bool isSheathing = false;
		private int sheathTimer = 0;
		public const int SheathDuration = 120; // 2.0 seconds authentic reverse sheathing
		public const int SheathPostDelay = 60; // 1.0 second delay after sheathing finishes before damage & snap-back
		public const int SheathFinishDelay = 25; // 0.4 seconds post-damage linger before concluding
		private bool sheathClickDone = false;
		private bool sheathDamageDone = false;

		// Absolute freeze anchor in world space
		private Vector2 freezePosition = Vector2.Zero;
		private bool hasSetFreezePos = false;

		private struct PendingResurgeHit
		{
			public int NpcWhoAmI;
			public int Damage;
			public int DetonatedKnots;
		}
		private readonly List<PendingResurgeHit> pendingResurgeHits = new List<PendingResurgeHit>();
		private int totalKnotsDetonatedInUlt = 0;

		// Track screen slash lines for rendering
		private readonly struct SlashRecord
		{
			public readonly Vector2 Start;
			public readonly Vector2 End;
			public readonly int SpawnFrame;
			public readonly Color Col;

			public SlashRecord(Vector2 start, Vector2 end, int spawnFrame, Color col)
			{
				Start = start;
				End = end;
				SpawnFrame = spawnFrame;
				Col = col;
			}
		}

		private readonly List<SlashRecord> activeSlashes = new List<SlashRecord>();

		// 3D Crescent Slash Arcs rendered via OniCrimsonSlash shader
		private struct ActiveSlashArc
		{
			public Vector2 Center;
			public float Rotation;
			public float Radius;
			public float ArcSpan;
			public int Type;
			public int Timer;
			public int MaxTime;
			public float Flip;
			public float TiltX;
		}

		private readonly List<ActiveSlashArc> activeArcs = new List<ActiveSlashArc>();

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
			Projectile.DamageType = WeepCompatibility.TrueMelee;
			Projectile.scale = 1f;
			Projectile.timeLeft = UnsheathDuration + MaxSlashingTime + 300;
		}

		public override void AI()
		{
			if (!Owner.active || Owner.dead || Owner.ghost)
			{
				Projectile.Kill();
				return;
			}

			// Strictly lock projectile scale to 1.0f so external accessories/modifiers cannot scale the ultimate sword
			Projectile.scale = 1f;

			Timer++;
			Owner.heldProj = Projectile.whoAmI;

			// Lock initial freeze position on activation
			if (!hasSetFreezePos)
			{
				hasSetFreezePos = true;
				freezePosition = Owner.position;
			}

			// Absolutely freeze player in place throughout the entire ultimate execution
			Owner.position = freezePosition;
			Owner.velocity = Vector2.Zero;
			Owner.gravity = 0f;
			Owner.fallStart = (int)(Owner.position.Y / 16f);
			// Damage immunity is handled cleanly via NaughtPlayer without vanilla hurt/i-frame flickering
			Owner.immune = false;
			Owner.immuneTime = 0;
			Owner.immuneAlpha = 0;
			Owner.immuneNoBlink = true;
			Owner.itemAnimation = 2;
			Owner.itemTime = 2;

			// Normal clean glow like vanilla Shine Potion (BuffID.Shine)
			Lighting.AddLight((int)(Owner.position.X + Owner.width * 0.5f) / 16, (int)(Owner.position.Y + Owner.height * 0.5f) / 16, 0.85f, 0.95f, 1.0f);

			// Physical Hitstop Micro-Pause (freezes frame on blade impact for heaviness)
			if (hitstopFrames > 0)
			{
				hitstopFrames--;
				return;
			}

			// Act 0: Stance Preparation (Frame 1)
			if (Timer == 1)
			{
				// Absolute screen/world time stop: freezes all NPCs, liquids, and projectiles on screen!
				WorldFreezeSystem.Activate("SlashedDreamUltimate");

				// Spawn atmospheric floating petals across the screen via InnoVault PRT
				for (int i = 0; i < 35; i++)
				{
					Vector2 randPos = Main.screenPosition + new Vector2(Main.rand.NextFloat(Main.screenWidth), Main.rand.NextFloat(Main.screenHeight));
					Vector2 randVel = new Vector2(Main.rand.NextFloat(-1.8f, -0.4f), Main.rand.NextFloat(0.6f, 1.8f));
					PRTLoader.NewParticle<CrimsonPetalPRT>(randPos, randVel, new Color(245, 30, 55, 230), Main.rand.NextFloat(0.7f, 1.2f));
				}

				// Initial resonant chime and gong
				SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 0.95f }, Owner.Center);
				SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f, Volume = 0.9f }, Owner.Center);

				// Screen punch
				Main.instance.CameraModifiers.Add(new PunchCameraModifier(Owner.Center, Main.rand.NextVector2Unit(), 10f, 8f, 20, 1000f, "SlashedDreamAwaken"));
			}

			// Unsheath sound cue on frame 35 (thumb clicks tsuba to unlock blade)
			if (Timer == 35)
			{
				SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.35f, Volume = 1.0f }, Owner.Center);
			}

			// Unsheath blade draw begins on frame 60 (1.0s in): blade slides out, reflection wave ripples out from player over 3.0s (180 ticks)!
			if (Timer == 60)
			{
				NihilityDomainSystem.StartReflectionRipple(Owner.Bottom);
				NihilityDomainSystem.TargetMusicSilence = 1f;
				SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.4f, Volume = 1.0f }, Owner.Center);
			}

			if (Timer == 130)
			{
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.25f, Volume = 0.9f }, Owner.Center);
			}

			// At frame 240 (4.0s mark): blade fully drawn, wave completes, camera punch before slashing phase!
			if (Timer == UnsheathDuration)
			{
				SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.35f, Volume = 1.0f }, Owner.Center);
				Main.instance.CameraModifiers.Add(new PunchCameraModifier(Owner.Center, new Vector2(Owner.direction, 0f), 8f, 6f, 12, 1000f, "BladeDrawn"));
			}

			// Update 3D shader slash arcs
			for (int i = activeArcs.Count - 1; i >= 0; i--)
			{
				ActiveSlashArc arc = activeArcs[i];
				arc.Timer++;
				if (arc.Timer >= arc.MaxTime)
				{
					activeArcs.RemoveAt(i);
				}
				else
				{
					activeArcs[i] = arc;
				}
			}

			// Process delayed impact strike (delivers the physical hit at the apex of the swing)
			if (hasPendingStrike)
			{
				impactDelayTimer--;
				if (impactDelayTimer <= 0)
				{
					hasPendingStrike = false;
					if (pendingStrikeIndex < 4)
					{
						ExecuteRainblade(pendingStrikeIndex, pendingStrikeAngle, pendingStrikeTilt);
					}
					else
					{
						ExecuteStygianResurge(pendingStrikeAngle, pendingStrikeTilt);
					}
				}
			}

			// Update swing animation
			if (isSwinging)
			{
				swingTimer++;
				if (swingTimer >= swingDuration)
				{
					isSwinging = false;
				}
			}

			if (slashCooldown > 0)
			{
				slashCooldown--;
			}

			// The 9-second window ONLY starts counting down after the 1-second unsheath animation completes!
			if (Timer > UnsheathDuration)
			{
				if (SlashStage < 4)
				{
					remainingSlashingTime--;

					bool isHolding = WeepKeybinds.UltimateKeybind != null && WeepKeybinds.UltimateKeybind.Current;
					bool justPressed = WeepKeybinds.UltimateKeybind != null && WeepKeybinds.UltimateKeybind.JustPressed;

					if (isHolding)
					{
						holdTimer++;
					}
					else
					{
						holdTimer = 0;
					}

					// Auto-advance cadence when holding down the button
					bool autoAdvance = false;
					if (SlashStage == 0 && holdTimer >= 22)
						autoAdvance = true;
					else if (SlashStage == 1 && holdTimer >= 22)
						autoAdvance = true;
					else if (SlashStage == 2 && holdTimer >= 24)
						autoAdvance = true;
					else if (SlashStage == 3 && holdTimer >= 30)
						autoAdvance = true;

					// Timeout safeguard: if 9 seconds have expired, auto-execute next slash so player is never trapped
					bool timedOut = remainingSlashingTime <= 0;

					if (((justPressed && slashCooldown <= 0 && !hasPendingStrike) || autoAdvance || timedOut) && !hasPendingStrike)
					{
						AdvanceSlash();
					}
				}
				else
				{
					// Post-Resurge: Wait for swing and impact to complete, then begin authentic reverse sheathing
					if (!isSwinging && !hasPendingStrike)
					{
						if (!isSheathing)
						{
							isSheathing = true;
							sheathTimer = 0;
							sheathClickDone = false;
							sheathDamageDone = false;
						}

						sheathTimer++;

						// Keep enemies completely severed during the entire 2-second sheathing animation and 1-second post-delay
						NihilityDismember.ReassembleFactor = 0f;

						// At the end of the 2-second sheathing slide (tick SheathDuration - 16 = 104):
						// Blade slides fully into scabbard, mouth gleam flashes, lock click sound plays!
						if (sheathTimer >= SheathDuration - 16 && !sheathClickDone)
						{
							sheathClickDone = true;
							SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.20f, Volume = 0.85f }, Owner.Center);
							SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.45f, Volume = 1.0f }, Owner.Center);
						}

						// Exactly 1 second (60 ticks) after sheathing finishes (tick SheathDuration + SheathPostDelay = 180):
						// Enemies snap back together and the massive delayed damage goes through!
						if (sheathTimer >= SheathDuration + SheathPostDelay && !sheathDamageDone)
						{
							sheathDamageDone = true;

							// 1. Put enemies back together
							NihilityDismember.SnapBackAll();

							// 2. Play delayed dimensional rupture impact sound
							SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.15f, Volume = 1.0f }, Owner.Center);
							SoundEngine.PlaySound(WeepSound.KatanaHitB with { Pitch = -0.20f, Volume = 1.0f }, Owner.Center);

							var np = Owner.GetModPlayer<NaughtPlayer>();
							bool hasKarma = np != null && np.HasKarmaSever;
							bool hasThunder = np != null && np.HasThunderCore;
							bool hasSync = np != null && np.HasSynchronizedExecution;

							NPC primaryTarget = null;

							// 3. Deliver delayed Stygian Resurge damage to all targets
							for (int i = 0; i < pendingResurgeHits.Count; i++)
							{
								var hit = pendingResurgeHits[i];
								if (hit.NpcWhoAmI >= 0 && hit.NpcWhoAmI < Main.maxNPCs)
								{
									NPC target = Main.npc[hit.NpcWhoAmI];
									if (target.active && !target.friendly && (!target.dontTakeDamage || target.type == NPCID.TargetDummy))
									{
										if (primaryTarget == null || target.boss) primaryTarget = target;

										int savedDef = target.defense;
										// Severance of Karma ignores an additional 20% defense (down to 30% defense)
										target.defense = hasKarma ? (int)(target.defense * 0.30f) : (int)(target.defense * 0.50f);
										target.SimpleStrikeNPC(hit.Damage, Owner.direction, true, 10f, DamageClass.Melee);
										target.defense = savedDef;

										SpawnImpactVisuals(target.Center, true);

										// Severance of Karma: Non-boss execution below 6% max HP
										if (hasKarma && !target.boss && !NPCID.Sets.ShouldBeCountedAsBoss[target.type] && target.life < target.lifeMax * 0.06f && target.type != NPCID.TargetDummy)
										{
											target.SimpleStrikeNPC(target.life + 1000, Owner.direction, true, 10f, DamageClass.Melee);
										}
									}
								}
							}
							pendingResurgeHits.Clear();

							// Thunder Core: Call down 9 dimensional thunderbolts dealing 30% of Stygian Resurge base damage
							if (hasThunder)
							{
								int boltDamage = (int)(Projectile.damage * 4.8f * 0.30f);
								Vector2 strikeOrigin = primaryTarget != null ? primaryTarget.Center : Owner.Center + new Vector2(Owner.direction * 200f, 0f);

								for (int b = 0; b < 9; b++)
								{
									Vector2 strikePos = strikeOrigin + new Vector2(Main.rand.NextFloat(-180f, 180f), Main.rand.NextFloat(-40f, 40f));
									int boltIdx = Projectile.NewProjectile(Owner.GetSource_FromThis(), strikePos, Vector2.Zero, ModContent.ProjectileType<ThunderCoreBolt>(), boltDamage, 4f, Owner.whoAmI);
									if (boltIdx >= 0 && boltIdx < Main.maxProjectiles && Main.projectile[boltIdx].ModProjectile is ThunderCoreBolt bolt)
									{
										bolt.Init(strikePos);
									}
								}
							}

							// Synchronized Execution: Clones converge in an intersecting sheathing cross-slash across primary target
							if (hasSync && primaryTarget != null)
							{
								for (int i = 0; i < Main.maxProjectiles; i++)
								{
									Projectile p = Main.projectile[i];
									if (p.active && p.owner == Owner.whoAmI && p.type == ModContent.ProjectileType<NaughtClone>() && p.ModProjectile is NaughtClone clone)
									{
										clone.PerformSheathingCrossSlash(primaryTarget);
									}
								}
							}

							Main.instance.CameraModifiers.Add(new PunchCameraModifier(Owner.Center, new Vector2(Owner.direction, 0f), 20f, 9f, 18, 1400f, "SheathDamageSnap"));
							hitstopFrames = 8;
						}

						// Conclude the ultimate 0.4s after the delayed damage goes through
						if (sheathTimer >= SheathDuration + SheathPostDelay + SheathFinishDelay)
						{
							Projectile.Kill();
						}
					}
				}
			}

			// Adjust player composite arm
			UpdatePlayerStance();
		}

		private void AdvanceSlash()
		{
			holdTimer = 0;
			bool isHolding = WeepKeybinds.UltimateKeybind != null && WeepKeybinds.UltimateKeybind.Current;
			var np = Owner.GetModPlayer<NaughtPlayer>();
			bool hasBladesong = np != null && np.HasBladesongRain;
			bool hasSync = np != null && np.HasSynchronizedExecution;

			// Synchronized Execution: clones awaken to mirror Rainblade strikes
			if (hasSync && SlashStage < 3)
			{
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (p.active && p.owner == Owner.whoAmI && p.type == ModContent.ProjectileType<NaughtClone>() && p.ModProjectile is NaughtClone clone)
					{
						clone.PerformMirrorRainblade(SlashStage, new Vector2(Owner.direction, 0f));
					}
				}
			}

			if (SlashStage == 0)
			{
				// Rainblade Strike 1: Overhead descending cleave with forward 3D depth tilt!
				float angle = GenerateNextSlashAngle();
				float tilt = MathHelper.ToRadians(38f);
				int duration = isHolding ? 22 : 34;
				int delay = isHolding ? 7 : 11;
				if (hasBladesong) { duration = (int)(duration * 0.65f); delay = (int)(delay * 0.65f); }

				StartSwingAnimation(angle, 0, tilt, duration, delay);
				ScheduleImpact(1, angle, tilt, delay);
				SlashStage = 1;
				slashCooldown = duration - 4;
			}
			else if (SlashStage == 1)
			{
				// Rainblade Strike 2: Rising diagonal reverse cleave with upward-receding 3D depth tilt!
				float angle = GenerateNextSlashAngle();
				float tilt = MathHelper.ToRadians(-42f);
				int duration = isHolding ? 22 : 34;
				int delay = isHolding ? 7 : 11;
				if (hasBladesong) { duration = (int)(duration * 0.65f); delay = (int)(delay * 0.65f); }

				StartSwingAnimation(angle, 1, tilt, duration, delay);
				ScheduleImpact(2, angle, tilt, delay);
				SlashStage = 2;
				slashCooldown = duration - 4;
			}
			else if (SlashStage == 2)
			{
				// Rainblade Strike 3: Horizontal sideways cleave with extreme 3D perspective foreshortening!
				float baseAngle = Owner.direction > 0 ? 0.05f : (float)Math.PI - 0.05f;
				float angle = baseAngle + Main.rand.NextFloat(-0.10f, 0.10f);
				float tilt = MathHelper.ToRadians(62f);
				int duration = isHolding ? 24 : 38;
				int delay = isHolding ? 8 : 13;
				if (hasBladesong) { duration = (int)(duration * 0.65f); delay = (int)(delay * 0.65f); }

				StartSwingAnimation(angle, 2, tilt, duration, delay);
				ScheduleImpact(3, angle, tilt, delay);
				SlashStage = 3;
				slashCooldown = duration - 4;

				// Silence / zanshin chime before grand rupture
				SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.45f, Volume = 0.75f }, Owner.Center);
			}
			else if (SlashStage == 3)
			{
				// Stygian Resurge (Screen cleave finale with spatial depth tilt)
				float angle = GenerateResurgeAngle();
				float tilt = MathHelper.ToRadians(30f);
				int duration = isHolding ? 28 : 46;
				int delay = isHolding ? 9 : 15;

				StartSwingAnimation(angle, 3, tilt, duration, delay);
				ScheduleImpact(4, angle, tilt, delay);
				SlashStage = 4;
				postResurgeTimer = 0;
			}
		}

		private void ScheduleImpact(int index, float angle, float tilt, int delay)
		{
			hasPendingStrike = true;
			pendingStrikeIndex = index;
			pendingStrikeAngle = angle;
			pendingStrikeTilt = tilt;
			impactDelayTimer = delay;

			// Play Acheron sub-bass windup / vacuum draw sound as the blade begins its swing
			SoundEngine.PlaySound(WeepSound.AcheronWindup, Owner.Center);
			SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.25f, Volume = 0.65f }, Owner.Center);
		}

		private void StartSwingAnimation(float angle, int type, float tiltX, int duration, int impactDelay)
		{
			isSwinging = true;
			swingTimer = 0;
			swingDuration = duration;
			swingType = type;
			swingImpactProgress = MathHelper.Clamp(impactDelay / (float)duration, 0.25f, 0.45f);

			float dir = Owner.direction;
			if (type == 0)
			{
				// Overhead descending cut: sweeps from high-back to low-front
				swingStartAngle = angle - dir * MathHelper.ToRadians(105f);
				swingEndAngle = angle + dir * MathHelper.ToRadians(80f);
			}
			else if (type == 1)
			{
				// Rising reverse cut: sweeps from low-back to high-front
				swingStartAngle = angle + dir * MathHelper.ToRadians(85f);
				swingEndAngle = angle - dir * MathHelper.ToRadians(95f);
			}
			else if (type == 2)
			{
				// Sideways horizontal cleave: cuts flat across camera plane
				swingStartAngle = angle - dir * MathHelper.ToRadians(68f);
				swingEndAngle = angle + dir * MathHelper.ToRadians(68f);
			}
			else
			{
				// Stygian Resurge: massive full-circle battou sweep
				swingStartAngle = angle - dir * MathHelper.ToRadians(120f);
				swingEndAngle = angle + dir * MathHelper.ToRadians(120f);
			}
		}

		private float GenerateNextSlashAngle()
		{
			float angle;
			float forwardAngle = Owner.direction > 0 ? 0f : (float)Math.PI;

			if (lastSlashAngle < -900f)
			{
				// First slash: random diagonal in front of the player (-45 to +45 deg relative to forward)
				angle = forwardAngle + Main.rand.NextFloat(-0.75f, 0.75f);
			}
			else
			{
				// Subsequent slashes: alternate or separate by 42 to 75 degrees in front of player
				float delta = Main.rand.NextFloat(0.72f, 1.25f);
				float rel = MathHelper.WrapAngle(lastSlashAngle - forwardAngle);
				if (rel > 0.15f)
					angle = forwardAngle - delta * 0.75f;
				else if (rel < -0.15f)
					angle = forwardAngle + delta * 0.75f;
				else
					angle = forwardAngle + (Main.rand.NextBool() ? delta : -delta);
			}

			lastSlashAngle = angle;
			return angle;
		}

		private float GenerateResurgeAngle()
		{
			float baseAngle = Main.rand.NextBool() ? MathHelper.ToRadians(28f) : MathHelper.ToRadians(-28f);
			if (Owner.direction < 0)
			{
				baseAngle = (float)Math.PI - baseAngle;
			}
			return baseAngle + Main.rand.NextFloat(-0.08f, 0.08f);
		}

		private static bool IsValidUltimateTarget(NPC npc)
		{
			if (npc == null || !npc.active || npc.friendly || npc.isLikeATownNPC)
				return false;

			if (npc.type == NPCID.TargetDummy)
				return true;

			if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || npc.realLife >= 0)
				return true;

			return npc.CanBeChasedBy();
		}

		public NPC FindPrimaryTarget()
		{
			NPC bestTarget = null;
			int highestPriority = -1;
			int highestKnots = -1;
			int highestHp = -1;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC target = Main.npc[i];
				if (IsValidUltimateTarget(target))
				{
					float dist = Vector2.Distance(Owner.MountedCenter, target.Center);
					if (dist < 2200f)
					{
						var knotNPC = target.GetGlobalNPC<CrimsonKnotGlobalNPC>();
						int knots = knotNPC != null ? knotNPC.CrimsonKnotStacks : 0;
						bool isBoss = target.boss || NPCID.Sets.ShouldBeCountedAsBoss[target.type] || (target.realLife >= 0 && Main.npc[target.realLife].boss);
						int priority = isBoss ? 2 : (target.type != NPCID.TargetDummy ? 1 : 0);

						if (priority > highestPriority || (priority == highestPriority && (knots > highestKnots || (knots == highestKnots && target.life > highestHp))))
						{
							highestPriority = priority;
							highestKnots = knots;
							highestHp = target.life;
							bestTarget = target;
						}
					}
				}
			}

			return bestTarget;
		}

		private void SpawnImpactVisuals(Vector2 impactPos, bool isResurge)
		{
			// 1. InnoVault Dimensional Explosion PRT (Additive)
			float expScale = (isResurge ? 1.8f : 1.25f) * Main.rand.NextFloat(0.95f, 1.30f);
			PRTLoader.NewParticle<DimensionalExplosionPRT>(impactPos, Vector2.Zero, new Color(255, 45, 75), expScale);

			// 2. Erupting crimson petals & void sparks
			int petalCount = isResurge ? 16 : 8;
			for (int i = 0; i < petalCount; i++)
			{
				Vector2 pVel = Main.rand.NextVector2Circular(6f, 5f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3.5f);
				PRTLoader.NewParticle<CrimsonPetalPRT>(impactPos, pVel, new Color(245, 30, 55, 230), Main.rand.NextFloat(0.75f, 1.25f));
			}

			int sparkCount = isResurge ? 24 : 10;
			for (int i = 0; i < sparkCount; i++)
			{
				Vector2 sVel = Main.rand.NextVector2Circular(8f, 8f);
				Color sCol = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
				PRTLoader.NewParticle<VoidSparkPRT>(impactPos, sVel, sCol, Main.rand.NextFloat(0.8f, 1.6f));
			}
		}

		private void ExecuteRainblade(int index, float angle, float tilt)
		{
			// 1. Play Acheron Heavy Impact Slash Sounds (crisp razor steel + deep sub-bass boom)
			if (index == 1)
				SoundEngine.PlaySound(WeepSound.AcheronSlash1, Owner.Center);
			else if (index == 2)
				SoundEngine.PlaySound(WeepSound.AcheronSlash2, Owner.Center);
			else
				SoundEngine.PlaySound(WeepSound.AcheronSlash3, Owner.Center);

			SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.18f * index, Volume = 0.85f }, Owner.Center);

			// 2. Heavy Directional Camera Punch & Physical Screen Shake
			Main.instance.CameraModifiers.Add(new PunchCameraModifier(Owner.Center, angle.ToRotationVector2(), 22f + index * 4f, 8f, 20, 1600f, $"Rainblade{index}"));
			NihilityDomainSystem.AnomalyPulse = 0.90f;
			NihilityDomainSystem.NegativeFlash = 0.38f;

			// 3. Physical Hitstop Micro-Pause (freezes frame for weighty blade bite)
			hitstopFrames = 3;

			// 4. Spawn 3D Crescent Slash Arc right now at the impact moment!
			Vector2 slashCenter = Owner.MountedCenter + angle.ToRotationVector2() * 65f;
			activeArcs.Add(new ActiveSlashArc
			{
				Center = slashCenter,
				Rotation = angle,
				Radius = index == 3 ? 320f : 280f,
				ArcSpan = index == 3 ? MathHelper.ToRadians(165f) : MathHelper.ToRadians(150f),
				Type = index - 1,
				Timer = 0,
				MaxTime = 24,
				Flip = index == 2 ? -Owner.direction : Owner.direction,
				TiltX = tilt
			});

			NPC primaryTarget = FindPrimaryTarget();
			Vector2 cutCenter = primaryTarget != null ? primaryTarget.Center : (Main.screenPosition + new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f));

			Vector2 dir = angle.ToRotationVector2();
			Vector2 slashStart = cutCenter - dir * (Main.screenWidth * 0.85f);
			Vector2 slashEnd = cutCenter + dir * (Main.screenWidth * 0.85f);
			activeSlashes.Add(new SlashRecord(slashStart, slashEnd, Timer, index == 3 ? new Color(255, 30, 45) : new Color(225, 90, 255)));

			// Spawn flower bloom and explosion on target!
			SpawnImpactVisuals(cutCenter, false);

			int baseDamage = Projectile.damage;
			int enemiesHit = 0;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC target = Main.npc[i];
				if (IsValidUltimateTarget(target))
				{
					float dist = Vector2.Distance(Owner.MountedCenter, target.Center);
					if (dist < 1800f)
					{
						var knotNPC = target.GetGlobalNPC<CrimsonKnotGlobalNPC>();
						int detonated = knotNPC != null ? knotNPC.DetonateKnots(3) : 0;
						totalKnotsDetonatedInUlt += detonated;

						// Bladesong of the Endless Rain: Detonated knots spawn infinitely-piercing homing blossom needles
						var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
						if (naughtPlayer != null && naughtPlayer.HasBladesongRain && detonated > 0)
						{
							for (int n = 0; n < detonated; n++)
							{
								Vector2 needleVel = Main.rand.NextVector2Circular(10f, 10f);
								Projectile.NewProjectile(Owner.GetSource_FromThis(), target.Center, needleVel, ModContent.ProjectileType<BloodBlossomNeedle>(), (int)(baseDamage * 0.70f), 2f, Owner.whoAmI);
							}
						}

						int strikeDamage = (int)(baseDamage * 1.65f + detonated * baseDamage * 0.55f);
						if (!target.dontTakeDamage || target.type == NPCID.TargetDummy)
						{
							target.SimpleStrikeNPC(strikeDamage, Owner.direction, false, 4f, DamageClass.Melee);
						}

						// Splitting Dismemberment along cut angle (with multi-segment group propagation)!
						NihilityDismember.TriggerGroup(target, target.Center, angle, duration: 240, holdFrames: 14);

						// Crimson bloom burst
						for (int d = 0; d < 12 + detonated * 6; d++)
						{
							int dustType = detonated > 0 ? DustID.CrimsonTorch : DustID.Shadowflame;
							Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
							Dust dust = Dust.NewDustPerfect(target.Center, dustType, vel, 100, default, 1.5f);
							dust.noGravity = true;
						}

						enemiesHit++;
					}
				}
			}

			// Rift of the Void Horizon: Rainblades leave lingering spatial fissures
			var np = Owner.GetModPlayer<NaughtPlayer>();
			if (np != null && np.HasVoidHorizon)
			{
				int fIdx = Projectile.NewProjectile(Owner.GetSource_FromThis(), (slashStart + slashEnd) * 0.5f, Vector2.Zero, ModContent.ProjectileType<VoidFissure>(), (int)(baseDamage * 0.85f), 1f, Owner.whoAmI);
				if (fIdx >= 0 && fIdx < Main.maxProjectiles && Main.projectile[fIdx].ModProjectile is VoidFissure vf)
				{
					vf.Init(slashStart, slashEnd);
				}
			}

			if (enemiesHit == 0)
			{
				for (int d = 0; d < 20; d++)
				{
					Vector2 pos = Vector2.Lerp(slashStart, slashEnd, Main.rand.NextFloat());
					Dust dust = Dust.NewDustPerfect(pos, DustID.CrimsonTorch, Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.4f);
					dust.noGravity = true;
				}
			}
		}

		private void ExecuteStygianResurge(float resurgeAngle, float tilt)
		{
			// 1. Acheron Ultimate Resurge Sound: Blinding spatial shatter + devastating sub-bass explosion
			SoundEngine.PlaySound(WeepSound.AcheronResurge, Owner.Center);
			SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.50f, Volume = 1.0f }, Owner.Center);

			// 2. Colossal Camera Punch & Spacetime Inversion
			Main.instance.CameraModifiers.Add(new PunchCameraModifier(Owner.Center, new Vector2(-1f, -0.6f).SafeNormalize(Vector2.UnitX), 36f, 12f, 34, 2400f, "StygianResurge"));
			NihilityDomainSystem.NegativeFlash = 1.0f;
			NihilityDomainSystem.AnomalyPulse = 1.0f;

			// 3. Ultimate Hitstop Freeze (5 frames of dramatic cinematic impact)
			hitstopFrames = 5;

			// 4. Spawn Grand Battou Slash Arc at impact!
			activeArcs.Add(new ActiveSlashArc
			{
				Center = Owner.MountedCenter + resurgeAngle.ToRotationVector2() * 85f,
				Rotation = resurgeAngle,
				Radius = 400f,
				ArcSpan = MathHelper.ToRadians(215f),
				Type = 3,
				Timer = 0,
				MaxTime = 32,
				Flip = Owner.direction,
				TiltX = tilt
			});

			NPC primaryTarget = FindPrimaryTarget();
			Vector2 cleaveCenter = primaryTarget != null ? primaryTarget.Center : (Main.screenPosition + new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f));
			Vector2 cleaveDir = resurgeAngle.ToRotationVector2();
			Vector2 cleaveStart = cleaveCenter - cleaveDir * (Main.screenWidth * 1.05f);
			Vector2 cleaveEnd = cleaveCenter + cleaveDir * (Main.screenWidth * 1.05f);
			activeSlashes.Add(new SlashRecord(cleaveStart, cleaveEnd, Timer, new Color(255, 255, 255)));

			// Grand bloom and explosion on Stygian Resurge!
			SpawnImpactVisuals(cleaveCenter, true);

			int baseDamage = Projectile.damage;
			int totalKnotsPurged = 0;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC target = Main.npc[i];
				if (IsValidUltimateTarget(target))
				{
					float dist = Vector2.Distance(Owner.MountedCenter, target.Center);
					if (dist < 2200f)
					{
						var knotNPC = target.GetGlobalNPC<CrimsonKnotGlobalNPC>();
						int remainingKnots = knotNPC?.CrimsonKnotStacks ?? 0;
						if (knotNPC != null)
						{
							knotNPC.ClearCrimsonKnot();
						}

						totalKnotsPurged += remainingKnots;
						totalKnotsDetonatedInUlt += remainingKnots;

						int resurgeDamage = (int)(baseDamage * 4.8f + remainingKnots * baseDamage * 0.35f);

						pendingResurgeHits.Add(new PendingResurgeHit
						{
							NpcWhoAmI = target.whoAmI,
							Damage = resurgeDamage,
							DetonatedKnots = remainingKnots
						});

						// Instant violent dismemberment with group propagation across multi-segment bosses!
						NihilityDismember.TriggerGroup(target, target.Center, resurgeAngle, duration: 280, holdFrames: 2);

						for (int d = 0; d < 25; d++)
						{
							Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
							Dust dust = Dust.NewDustPerfect(target.Center, DustID.CrimsonTorch, vel, 100, default, 1.8f);
							dust.noGravity = true;
						}
					}
				}
			}

			// Resource Reset & Overflow Charge Refund
			var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
			if (naughtPlayer != null)
			{
				int refundedCharges = naughtPlayer.OverflowDream;
				naughtPlayer.SlashedDream = refundedCharges;
				naughtPlayer.OverflowDream = 0;

				if (refundedCharges > 0)
				{
					SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.35f, Volume = 0.85f }, Owner.Center);
				}

				// Siphon of the Departed: Heal 1% max HP per knot detonated (up to 9%) and +15% move speed for 8s
				if (naughtPlayer.HasDepartedSiphon && totalKnotsDetonatedInUlt > 0)
				{
					int healAmount = Math.Min(9, totalKnotsDetonatedInUlt) * (int)(Owner.statLifeMax2 * 0.01f);
					if (healAmount > 0)
					{
						Owner.Heal(healAmount);
					}
					naughtPlayer.SiphonMoveSpeedTimer = 480; // 8s
				}

				// Emancipator's Severed Will: Refund 2 Slashed Dream per 9 knots detonated total, and +20% crit for 10s
				if (naughtPlayer.HasEmancipatorWill)
				{
					int knotRefund = (totalKnotsDetonatedInUlt / 9) * 2;
					if (knotRefund > 0)
					{
						naughtPlayer.AddSlashedDream(knotRefund);
					}
					naughtPlayer.EmancipatorCritBuffTimer = 600; // 10s
				}

				// Thunder Core: Post-Ult +15% True Melee damage for 15s
				if (naughtPlayer.HasThunderCore)
				{
					naughtPlayer.ThunderCoreBuffTimer = 900; // 15s
				}
			}
		}

		private void UpdatePlayerStance()
		{
			if (Timer < UnsheathDuration)
			{
				// Low iaijutsu unsheathing posture
				float progress = MathHelper.Clamp(Timer / (float)UnsheathDuration, 0f, 1f);
				float eased = VaultUtils.EaseQuadOut(progress);

				// Right arm grasps the handle and follows the forward-up draw
				float frontArmAngle = MathHelper.Lerp(-Owner.direction * 0.28f, -Owner.direction * 0.58f, eased) * Owner.gravDir;
				Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, frontArmAngle);

				// Left arm holds the scabbard mouth at the hip
				float backArmAngle = -Owner.direction * 0.22f * Owner.gravDir;
				Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, backArmAngle);
				return;
			}

			if (isSwinging)
			{
				float s = MathHelper.Clamp(swingTimer / (float)swingDuration, 0f, 1f);
				float eased;
				if (s < swingImpactProgress)
				{
					float u = s / swingImpactProgress;
					eased = (float)Math.Pow(u, 1.8) * 0.5f;
				}
				else
				{
					float u = (s - swingImpactProgress) / (1f - swingImpactProgress);
					eased = 0.5f + (1f - (float)Math.Pow(1f - u, 1.8)) * 0.5f;
				}
				float currentRot = MathHelper.Lerp(swingStartAngle, swingEndAngle, eased);
				float armAngle;
				if (Owner.direction > 0)
					armAngle = (currentRot - MathHelper.PiOver2) * Owner.gravDir;
				else
					armAngle = (3f * MathHelper.PiOver2 - currentRot) * Owner.gravDir;

				Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle);
				Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Quarter, -Owner.direction * 0.15f * Owner.gravDir);
				return;
			}

			if (isSheathing)
			{
				float p = MathHelper.Clamp(sheathTimer / (float)(SheathDuration - 16), 0f, 1f);
				float eased = VaultUtils.EaseQuadOut(p);

				// Right arm guides the handle smoothly backward-down towards the scabbard mouth
				float frontArmAngle = MathHelper.Lerp(-Owner.direction * 0.58f, -Owner.direction * 0.28f, eased) * Owner.gravDir;
				Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, frontArmAngle);

				// Left arm holds the scabbard mouth at the hip
				float backArmAngle = -Owner.direction * 0.22f * Owner.gravDir;
				Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, backArmAngle);
				return;
			}

			// Resting / waiting drawing stance
			float restAngle = -Owner.direction * 0.35f * Owner.gravDir;
			if (SlashStage == 3)
			{
				// Zanshin: slowly sliding back towards sheath
				restAngle = -Owner.direction * 0.20f * Owner.gravDir;
			}
			else if (SlashStage >= 4)
			{
				// Snapped into sheath
				restAngle = -Owner.direction * 0.28f * Owner.gravDir;
			}

			Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, restAngle);
			Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, -Owner.direction * 0.22f * Owner.gravDir);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// 1. Draw 3D Crescent Slash Arcs with OniCrimsonSlash Shader!
			DrawSlashArcs();

			// 2. Draw hanging screen slashes & dimensional scars
			Texture2D pixelTex = TextureAssets.MagicPixel.Value;
			if (pixelTex != null)
			{
				for (int i = activeSlashes.Count - 1; i >= 0; i--)
				{
					SlashRecord slash = activeSlashes[i];
					float age = Timer - slash.SpawnFrame;
					float maxAge = 50f;
					if (age > maxAge)
						continue;

					float slashAlpha = MathHelper.Clamp(1f - (age / maxAge), 0f, 1f);
					float thickness = (SlashStage >= 4 && slash.SpawnFrame == Timer - postResurgeTimer)
						? MathHelper.Lerp(14f, 2f, age / maxAge)
						: MathHelper.Lerp(6f, 1f, age / maxAge);

					Vector2 diff = slash.End - slash.Start;
					float length = diff.Length();
					float rot = diff.ToRotation();
					Vector2 drawStart = slash.Start - Main.screenPosition;

					Main.spriteBatch.Draw(pixelTex, drawStart, new Rectangle(0, 0, (int)length, (int)(thickness * 2.5f)), slash.Col * (slashAlpha * 0.5f), rot, new Vector2(0f, thickness * 1.25f), 1f, SpriteEffects.None, 0f);
					Main.spriteBatch.Draw(pixelTex, drawStart, new Rectangle(0, 0, (int)length, (int)thickness), Color.White * (slashAlpha * 0.95f), rot, new Vector2(0f, thickness * 0.5f), 1f, SpriteEffects.None, 0f);
				}
			}

			// 3. Draw Weapon (Authentic Iaido Unsheathing vs 3D Warped Swinging vs Stance)
			DrawWeaponSprites();

			return false;
		}

		private void DrawSlashArcs()
		{
			Effect fx = ShaderLoader.SlashShader;
			Texture2D brush = null;
			Texture2D noise = null;

			try
			{
				brush = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/SlashBrush01", AssetRequestMode.ImmediateLoad).Value;
				noise = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/RiftNoise", AssetRequestMode.ImmediateLoad).Value;
			}
			catch { }

			if (fx == null || brush == null || noise == null || activeArcs.Count == 0)
				return;

			GraphicsDevice device = Main.graphics.GraphicsDevice;
			SpriteBatch sb = Main.spriteBatch;

			var sbState = sb.Capture();
			sb.End();

			BlendState origBlend = device.BlendState;
			RasterizerState origRaster = device.RasterizerState;
			device.BlendState = BlendState.AlphaBlend;
			device.RasterizerState = RasterizerState.CullNone;

			Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -4000f, 4000f);
			Matrix view = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0)) * Main.GameViewMatrix.TransformationMatrix;
			Matrix transformMatrix = view * projection;

			fx.Parameters["transformMatrix"]?.SetValue(transformMatrix);
			fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			fx.Parameters["uBrushTex"]?.SetValue(brush);
			fx.Parameters["uNoiseTex"]?.SetValue(noise);
			fx.Parameters["uFarSel"]?.SetValue(0f);
			fx.Parameters["uRazorTailWiden"]?.SetValue(0.40f);
			fx.Parameters["uInk"]?.SetValue(0f);

			// Crimson Acheron palette: white-hot razor line, blazing crimson energy, dark wine edge
			fx.Parameters["uColHot"]?.SetValue(new Vector3(1.0f, 0.94f, 0.96f));
			fx.Parameters["uColBright"]?.SetValue(new Vector3(1.0f, 0.12f, 0.25f));
			fx.Parameters["uColDeep"]?.SetValue(new Vector3(0.70f, 0.04f, 0.14f));
			fx.Parameters["uColDark"]?.SetValue(new Vector3(0.20f, 0.01f, 0.04f));

			foreach (var arc in activeArcs)
			{
				float progress = arc.Timer / (float)arc.MaxTime;
				float sweep = Math.Min(1f, progress * 2.2f);
				float erode = Math.Max(0f, (progress - 0.35f) * 1.55f);
				float flash = MathHelper.Clamp((float)Math.Sin(progress * MathHelper.Pi) * 0.95f, 0f, 1f);
				float alpha = 1f - progress * progress;

				fx.Parameters["uMode"]?.SetValue(0f); // Polar crescent arc
				fx.Parameters["uSweep"]?.SetValue(sweep);
				fx.Parameters["uErode"]?.SetValue(erode);
				fx.Parameters["uTailErode"]?.SetValue(progress * 0.7f);
				fx.Parameters["uFlash"]?.SetValue(flash);
				fx.Parameters["uOpacity"]?.SetValue(alpha);
				fx.Parameters["uArcSpan"]?.SetValue(arc.ArcSpan);
				fx.Parameters["uThick"]?.SetValue(arc.Type == 3 ? 0.44f : 0.32f);
				fx.Parameters["uFlip"]?.SetValue(arc.Flip);

				// 3D Perspective Tilt Shading (dim far side for depth)
				float tiltAbs = Math.Abs(arc.TiltX);
				fx.Parameters["uFarDim"]?.SetValue(MathHelper.Clamp(tiltAbs * 0.75f, 0f, 0.55f));
				fx.Parameters["uFarDirLocal"]?.SetValue(new Vector2(0f, -1f));

				Vector2 dir = arc.Rotation.ToRotationVector2();
				Vector2 normal = new Vector2(-dir.Y, dir.X);

				// Calculate 3D perspective foreshortening & trapezoid keystone distortion:
				float cosTilt = (float)Math.Cos(arc.TiltX);
				float sinTilt = (float)Math.Sin(arc.TiltX);
				float persp = 0.44f;

				// Near edge expands in perspective, far edge contracts in perspective
				float nearScale = 1.0f + Math.Abs(sinTilt) * persp;
				float farScale = Math.Max(0.25f, 1.0f - Math.Abs(sinTilt) * persp);

				float hx = arc.Radius * 1.15f;
				float hy = arc.Radius * 1.15f * Math.Max(0.35f, cosTilt);
				Vector2 center = arc.Center + dir * (arc.Radius * 0.15f);

				// Vertices with 3D perspective keystone trapezoid:
				// v0: far-left (-dir * farScale, -normal * hy)
				// v1: far-right (+dir * farScale, -normal * hy)
				// v2: near-left (-dir * nearScale, +normal * hy)
				// v3: near-right (+dir * nearScale, +normal * hy)
				VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
				verts[0] = new VertexPositionColorTexture(new Vector3(center - dir * (hx * farScale) - normal * hy, 0f), Color.White, new Vector2(0f, 0f));
				verts[1] = new VertexPositionColorTexture(new Vector3(center + dir * (hx * farScale) - normal * hy, 0f), Color.White, new Vector2(1f, 0f));
				verts[2] = new VertexPositionColorTexture(new Vector3(center - dir * (hx * nearScale) + normal * hy, 0f), Color.White, new Vector2(0f, 1f));
				verts[3] = new VertexPositionColorTexture(new Vector3(center + dir * (hx * nearScale) + normal * hy, 0f), Color.White, new Vector2(1f, 1f));

				foreach (EffectPass pass in fx.CurrentTechnique.Passes)
				{
					pass.Apply();
					device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
				}
			}

			device.BlendState = origBlend;
			device.RasterizerState = origRaster;

			sb.Begin(in sbState);
		}

		private void DrawScabbardFlash(Vector2 flashPos, float flashAlpha, float bladeAxisAngle)
		{
			if (flashAlpha <= 0.002f)
				return;

			var sbState = Main.spriteBatch.Capture();
			Main.spriteBatch.End();

			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, sbState.Matrix);

			Texture2D flareTex = TextureAssets.Extra[ExtrasID.SharpTears]?.Value;
			Texture2D glowTex = TextureAssets.Extra[ExtrasID.ThePerfectGlow]?.Value;

			if (glowTex != null)
			{
				Vector2 glowOrigin = glowTex.Size() * 0.5f;
				// Outer vivid crimson / purple bloom aura
				Color auraColor = new Color(255, 30, 60, 0) * (0.85f * flashAlpha);
				Main.spriteBatch.Draw(glowTex, flashPos, null, auraColor, 0f, glowOrigin, 0.55f * flashAlpha, SpriteEffects.None, 0f);

				// Inner brilliant white-hot core
				Color coreColor = new Color(255, 240, 245, 0) * (1.0f * flashAlpha);
				Main.spriteBatch.Draw(glowTex, flashPos, null, coreColor, 0f, glowOrigin, 0.25f * flashAlpha, SpriteEffects.None, 0f);
			}

			if (flareTex != null)
			{
				Vector2 flareOrigin = flareTex.Size() * 0.5f;
				// 1. Blinding anamorphic ray along the katana blade axis
				Color rayColor = Color.White * flashAlpha;
				Main.spriteBatch.Draw(flareTex, flashPos, null, rayColor, bladeAxisAngle, flareOrigin, new Vector2(1.85f, 0.35f) * flashAlpha, SpriteEffects.None, 0f);

				// 2. Perpendicular diffraction cross ray in electric crimson
				Color perpColor = new Color(255, 60, 95, 0) * (0.85f * flashAlpha);
				Main.spriteBatch.Draw(flareTex, flashPos, null, perpColor, bladeAxisAngle + MathHelper.PiOver2, flareOrigin, new Vector2(0.95f, 0.25f) * flashAlpha, SpriteEffects.None, 0f);

				// 3. 45-degree star diffraction diamond in pure silver-white
				Color diagColor = Color.White * (0.65f * flashAlpha);
				Main.spriteBatch.Draw(flareTex, flashPos, null, diagColor, bladeAxisAngle + MathHelper.PiOver4, flareOrigin, new Vector2(0.65f, 0.20f) * flashAlpha, SpriteEffects.None, 0f);
			}

			Main.spriteBatch.End();
			Main.spriteBatch.Restart(in sbState);
		}

		private void DrawWeaponSprites()
		{
			Texture2D bladeTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtUltBlade", AssetRequestMode.ImmediateLoad).Value;
			Texture2D sheathTex = null;
			Texture2D hiltTex = null;

			try
			{
				sheathTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtSheathBody", AssetRequestMode.ImmediateLoad).Value;
			}
			catch
			{
				sheathTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtSheath", AssetRequestMode.ImmediateLoad).Value;
			}

			try
			{
				hiltTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtUnsheathHilt", AssetRequestMode.ImmediateLoad).Value;
			}
			catch
			{
				hiltTex = null;
			}

			if (bladeTex == null || sheathTex == null)
				return;

			SpriteEffects effects = Owner.direction < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

			// =========================================================================
			// PHASE 0: AUTHENTIC IAIDO UNSHEATHING ANIMATION (4 Seconds = 240 Frames)
			// =========================================================================
			if (Timer <= UnsheathDuration)
			{
				Vector2 handPos = Owner.MountedCenter - Main.screenPosition;
				Vector2 origin = new Vector2(90f, 90f);

				// Unsheath draw starts on Frame 60 (1.0s in) and finishes on Frame 240 (3.0s draw = 180 frames)
				float progress = MathHelper.Clamp((Timer - 60f) / 180f, 0f, 1f);
				float slideProgress = VaultUtils.EaseQuadOut(progress);
				float slideDist = MathHelper.Lerp(0f, 22f, slideProgress);

				// Base sheath resting angle at hip:
				// Facing Right: 205 deg (Handle in front pointing forward-up; scabbard pointing back-down)
				// Facing Left: -205 deg (Handle in front pointing forward-up; scabbard pointing back-down)
				float baseAngle = MathHelper.ToRadians(205f);
				float sheathRot = Owner.direction > 0 ? baseAngle : -baseAngle;

				// Forward & upward slide along the sword axis out of the scabbard mouth:
				Vector2 slideDir = new Vector2(Owner.direction * 0.94f, -0.34f * Owner.gravDir);
				Vector2 hipOffset = new Vector2(Owner.direction * -6f, 2f * Owner.gravDir);
				Vector2 sheathPos = handPos + hipOffset;

				// 1. Blade hilt & short exposed blade segment slides FORWARD-UP along sheath opening
				Vector2 hiltPos = sheathPos + slideDir * slideDist;
				Texture2D drawHilt = hiltTex ?? bladeTex;

				Color bladeAura = Color.Lerp(new Color(255, 30, 45, 0), new Color(255, 230, 245, 0), slideProgress);

				// Draw hilt sprite FIRST (underneath)
				Main.spriteBatch.Draw(drawHilt, hiltPos, null, Color.White, sheathRot, origin, 0.95f, effects, 0f);
				if (slideProgress > 0.05f)
				{
					Main.spriteBatch.Draw(drawHilt, hiltPos, null, bladeAura * (0.85f * slideProgress), sheathRot, origin, 1.05f, effects, 0f);
				}

				// 2. Sheath is drawn SECOND (on top) over the blade!
				// Scabbard mouth covers whatever blade is still inside, revealing only the emerging steel!
				Main.spriteBatch.Draw(sheathTex, sheathPos, null, Color.White, sheathRot, origin, 0.95f, effects, 0f);

				// 3. Exact scabbard mouth gleam flash:
				float mouthX = Owner.direction > 0 ? -16f : 16f;
				float mouthY = 32f;
				Vector2 mouthOffset = new Vector2(mouthX, mouthY) * 0.95f;
				Vector2 unsheathPoint = sheathPos + mouthOffset.RotatedBy(sheathRot);
				float bladeAxisAngle = slideDir.ToRotation();

				// Initial flash as thumb unlocks tsuba and blade unseals (Timer 35 to 85, peaking at frame 58)
				if (Timer >= 35 && Timer <= 85)
				{
					float t = (Timer - 35f) / 50f;
					float flashAlpha = t < 0.45f ? (t / 0.45f) : (1f - (t - 0.45f) / 0.55f);
					DrawScabbardFlash(unsheathPoint, flashAlpha, bladeAxisAngle);
				}

				// Final sharp gleam as the blade reaches full 100% draw at Frame 240
				if (Timer >= 220 && Timer <= 240)
				{
					float t = (Timer - 220f) / 20f;
					float flashAlpha = 1f - t;
					DrawScabbardFlash(unsheathPoint + slideDir * (slideDist * 0.4f), flashAlpha * 0.85f, bladeAxisAngle);
				}

				return;
			}

			// Tsuka Grip Origin for NaughtUltBlade (180x180):
			// Center of handle grip is located at (27, 155).
			// Under SpriteEffects.FlipHorizontally, texture X mirrors to (180 - 27) = 153.
			Vector2 gripOrigin = Owner.direction > 0
				? new Vector2(27f, 155f)
				: new Vector2(bladeTex.Width - 27f, 155f);

			// =========================================================================
			// PHASE 1: ACTIVE SWINGING ANIMATION (Firmly pinned to player's front hand!)
			// =========================================================================
			if (isSwinging)
			{
				float s = MathHelper.Clamp(swingTimer / (float)swingDuration, 0f, 1f);
				float eased;
				if (s < swingImpactProgress)
				{
					float u = s / swingImpactProgress;
					eased = (float)Math.Pow(u, 1.8) * 0.5f;
				}
				else
				{
					float u = (s - swingImpactProgress) / (1f - swingImpactProgress);
					eased = 0.5f + (1f - (float)Math.Pow(1f - u, 1.8)) * 0.5f;
				}
				float currentRot = MathHelper.Lerp(swingStartAngle, swingEndAngle, eased);

				float armAngle;
				if (Owner.direction > 0)
					armAngle = (currentRot - MathHelper.PiOver2) * Owner.gravDir;
				else
					armAngle = (3f * MathHelper.PiOver2 - currentRot) * Owner.gravDir;

				// Hand position from player's composite arm in screen coordinates:
				Vector2 handPos = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, armAngle) - Main.screenPosition;

				// Dynamic 3D Perspective Warping & Foreshortening (Strictly immune to outside size modifiers)
				Vector2 scale = new Vector2(1f, 1f);
				float depthBulge = (float)Math.Sin(s * Math.PI);

				if (swingType == 2)
				{
					// Sideways Horizontal Cleave: 3D perspective rotation!
					// Blade turns from edge-on (ultra-thin height) -> flat broadside facing camera -> edge-on
					scale.Y = MathHelper.Lerp(0.18f, 1.40f, depthBulge);  // Edge-on to wide broadside
					scale.X = MathHelper.Lerp(1.30f, 0.85f, depthBulge);  // Foreshortened length
				}
				else if (swingType == 1)
				{
					// Rising reverse cut: lifts up and toward camera
					scale.X = 1f + 0.38f * depthBulge;
					scale.Y = 1f - 0.20f * depthBulge;
				}
				else if (swingType == 0)
				{
					// Overhead descending cut: sweeps down reaching toward viewer
					scale.X = 1f + 0.40f * depthBulge;
					scale.Y = 1f - 0.20f * depthBulge;
				}
				else
				{
					// Stygian Resurge: massive expanding dimensional slice
					scale.X = 1f + 0.55f * depthBulge;
					scale.Y = 1f - 0.25f * depthBulge;
				}

				float bladeRot = currentRot + (Owner.direction > 0 ? MathHelper.PiOver4 : 3f * MathHelper.PiOver4);

				// Red phantom afterimage trail lagging slightly behind rotation
				float trailLag = (Owner.direction > 0 ? -0.12f : 0.12f) * (1f - s);
				float trailRot = bladeRot + trailLag;
				Color trailColor = new Color(255, 30, 45, 0) * (0.65f * (1f - s));
				Main.spriteBatch.Draw(bladeTex, handPos, null, trailColor, trailRot, gripOrigin, scale * 1.15f, effects, 0f);

				// Main awakened blade firmly locked in the player's palm
				Main.spriteBatch.Draw(bladeTex, handPos, null, Color.White, bladeRot, gripOrigin, scale, effects, 0f);
				return;
			}

			// =========================================================================
			// PHASE 3: AUTHENTIC REVERSE SHEATHING ANIMATION AFTER FINAL SLASH (Stygian Resurge)
			// =========================================================================
			if (isSheathing)
			{
				Vector2 handPos = Owner.MountedCenter - Main.screenPosition;
				Vector2 origin = new Vector2(90f, 90f);

				float p = MathHelper.Clamp(sheathTimer / (float)(SheathDuration - 16), 0f, 1f);
				float reverseProgress = 1f - VaultUtils.EaseQuadOut(p);
				float slideDist = MathHelper.Lerp(0f, 22f, reverseProgress);

				float baseAngle = MathHelper.ToRadians(205f);
				float sheathRot = Owner.direction > 0 ? baseAngle : -baseAngle;

				Vector2 slideDir = new Vector2(Owner.direction * 0.94f, -0.34f * Owner.gravDir);
				Vector2 hipOffset = new Vector2(Owner.direction * -6f, 2f * Owner.gravDir);
				Vector2 sheathPos = handPos + hipOffset;

				// 1. Blade hilt & short exposed blade segment slides BACKWARD-DOWN into the scabbard
				Vector2 hiltPos = sheathPos + slideDir * slideDist;
				Texture2D drawHilt = hiltTex ?? bladeTex;

				Color bladeAura = Color.Lerp(new Color(255, 30, 45, 0), new Color(255, 230, 245, 0), reverseProgress);

				// Draw hilt sprite FIRST (underneath)
				Main.spriteBatch.Draw(drawHilt, hiltPos, null, Color.White, sheathRot, origin, 0.95f, effects, 0f);
				if (reverseProgress > 0.05f)
				{
					Main.spriteBatch.Draw(drawHilt, hiltPos, null, bladeAura * (0.85f * reverseProgress), sheathRot, origin, 1.05f, effects, 0f);
				}

				// 2. Sheath is drawn SECOND (on top) over the blade
				Main.spriteBatch.Draw(sheathTex, sheathPos, null, Color.White, sheathRot, origin, 0.95f, effects, 0f);

				// 3. Exact scabbard mouth gleam flash when clicking shut!
				if (sheathTimer >= SheathDuration - 16 && sheathTimer <= SheathDuration + 4)
				{
					float flashAlpha = 1f - MathHelper.Clamp((sheathTimer - (SheathDuration - 16)) / 20f, 0f, 1f);
					float mouthX = Owner.direction > 0 ? -16f : 16f;
					float mouthY = 32f;
					Vector2 mouthOffset = new Vector2(mouthX, mouthY) * 0.95f;
					Vector2 sheathingPoint = sheathPos + mouthOffset.RotatedBy(sheathRot);
					float bladeAxisAngle = slideDir.ToRotation();

					DrawScabbardFlash(sheathingPoint, flashAlpha, bladeAxisAngle);
				}
				return;
			}

			// =========================================================================
			// PHASE 2: IDLE DRAWING / ZANSHIN STANCE (Firmly anchored in hand!)
			// =========================================================================
			float restAngle = -Owner.direction * 0.35f * Owner.gravDir;
			if (SlashStage == 3)
			{
				restAngle = -Owner.direction * 0.20f * Owner.gravDir;
			}
			else if (SlashStage >= 4)
			{
				restAngle = -Owner.direction * 0.28f * Owner.gravDir;
			}

			Vector2 stanceHandPos = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.ThreeQuarters, restAngle) - Main.screenPosition;

			float stanceRot;
			if (SlashStage < 4)
			{
				// Ready stance: blade angled forward-down (~35 deg)
				float worldAngle = Owner.direction > 0 ? MathHelper.ToRadians(35f) : MathHelper.ToRadians(145f);
				stanceRot = worldAngle + (Owner.direction > 0 ? MathHelper.PiOver4 : 3f * MathHelper.PiOver4);
			}
			else
			{
				// Sheathing zanshin stance: blade angled down-back (~140 deg)
				float worldAngle = Owner.direction > 0 ? MathHelper.ToRadians(140f) : MathHelper.ToRadians(40f);
				stanceRot = worldAngle + (Owner.direction > 0 ? MathHelper.PiOver4 : 3f * MathHelper.PiOver4);
			}

			Color auraColor = Color.Lerp(new Color(255, 30, 45, 0), new Color(255, 200, 220, 0), (float)Math.Sin(Timer * 0.2f) * 0.35f + 0.35f);
			Main.spriteBatch.Draw(bladeTex, stanceHandPos, null, auraColor * 0.85f, stanceRot, gripOrigin, 1.15f, effects, 0f);
			Main.spriteBatch.Draw(bladeTex, stanceHandPos, null, Color.White, stanceRot, gripOrigin, 1.0f, effects, 0f);
		}

		public override void OnKill(int timeLeft)
		{
			WorldFreezeSystem.Deactivate("SlashedDreamUltimate");
			NihilityDomainSystem.TargetMusicSilence = 0f;
			NihilityDomainSystem.ResetReflectionRipple();
			NihilityDismember.SnapBackAll();
			NihilityDismember.ReassembleFactor = 0f;
			pendingResurgeHits.Clear();
		}
	}
}
