using System;
using System.Collections.Generic;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.GlobalNPCs;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Content.Items.Weapons;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles
{
	public class NaughtHeldSwing : ModProjectile
	{
		// Points to the drawn blade texture rather than the sheathed inventory item
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/NaughtBlade";

		public Player Owner => Main.player[Projectile.owner];

		public int ComboStep
		{
			get => (int)Projectile.ai[0];
			set => Projectile.ai[0] = value;
		}

		private float timer = 0f;
		private float speedMult = 1f;
		private int lockedDirection = 1;
		private int swingDirection = 1;

		private float startAngle = 0f;
		private float endAngle = 0f;
		private float beatAimAngle = 0f;
		private float currentRotation = 0f;
		private float lastRotation = 0f;

		private bool visualSpawned = false;
		private bool exWaveSpawned = false;
		private bool sweepDamageActive = false;
		private bool soundPlayed = false;

		private HashSet<int> hitNPCs = new HashSet<int>();

		// 4-Beat Odachi Sequence:
		// 0: Horizontal Draw Cut
		// 1: Downward Cross-Slash
		// 2: Rising Reverse Sweep
		// 3: Overhead Helm Splitter (Finisher)
		public bool IsFinisher => ComboStep == 3;

		private float WindupTime => IsFinisher ? 5f : 3f;
		private float SlashTime => IsFinisher ? 11f : 8f;
		private float RecoveryTime => IsFinisher ? 8f : 5f;
		private float TotalTime => WindupTime + SlashTime + RecoveryTime;

		private float SwingArc => ComboStep switch
		{
			0 => MathHelper.ToRadians(195f), // Broad sweeping horizontal draw cut
			1 => MathHelper.ToRadians(205f), // Wide diagonal cross-slash
			2 => MathHelper.ToRadians(205f), // Sweeping rising reverse slash
			_ => MathHelper.ToRadians(270f)  // Massive overhead execution cleave
		};

		private float BladeReach => ComboStep switch
		{
			0 => 140f,
			1 => 146f,
			2 => 150f,
			_ => 180f
		};

		public override void SetDefaults()
		{
			Projectile.width = 64;
			Projectile.height = 64;
			Projectile.aiStyle = -1;
			Projectile.friendly = true;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.ownerHitCheck = true;
			Projectile.DamageType = WeepCompatibility.TrueMelee;
		}

		// ==================== CHARM HOOKS ====================
		public virtual void OnBeatStart(int beat, ref float speed, ref float reach) { }
		public virtual void OnBeatSlash(int beat, float progress, Vector2 bladeTip) { }
		public virtual void OnBeatHit(int beat, NPC target, NPC.HitInfo hit, int damageDone) { }
		public virtual void OnFinisherRelease(int beat, Vector2 aimDir, int beatDamage) { }
		public virtual void OnComboInterrupt(Vector2 cancelPosition) { }

		public override void AI()
		{
			if (Owner == null || !Owner.active || Owner.dead || Owner.CCed || Owner.noItems)
			{
				Projectile.Kill();
				return;
			}

			if (Owner.HeldItem?.type != ModContent.ItemType<Naught>())
			{
				Projectile.Kill();
				return;
			}

			// Right-click Flash Step dash-cancel during combo
			if (Owner.whoAmI == Main.myPlayer && Main.mouseRight && Main.mouseRightRelease)
			{
				var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
				if (naughtPlayer.DashCooldown <= 0)
				{
					OnComboInterrupt(Owner.MountedCenter);
					Projectile.Kill();
					Projectile.NewProjectile(
						Projectile.GetSource_FromThis(),
						Owner.MountedCenter,
						(Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX * Owner.direction),
						ModContent.ProjectileType<NaughtDash>(),
						0,
						0f,
						Owner.whoAmI
					);
					return;
				}
			}

			// First tick initialization for this combo beat
			if (timer == 0f)
			{
				InitComboBeat();
			}

			lastRotation = currentRotation;
			timer += speedMult;

			// Handle Swing Progression
			float slashStart = WindupTime;
			float slashEnd = WindupTime + SlashTime;

			if (timer < slashStart)
			{
				// Windup phase: slight pull-back anticipation
				float t = timer / WindupTime;
				float easeT = (float)Math.Sin(t * MathHelper.PiOver2);
				currentRotation = MathHelper.Lerp(startAngle, startAngle - swingDirection * MathHelper.ToRadians(12f), easeT);
				sweepDamageActive = false;
			}
			else if (timer <= slashEnd)
			{
				// Active slash phase: high-speed sweep
				sweepDamageActive = true;
				float t = (timer - slashStart) / SlashTime;

				// InnoVault easing curve for explosive acceleration and crisp follow-through
				float easeT = VaultUtils.EaseInOutQuad(t);
				currentRotation = MathHelper.Lerp(startAngle, endAngle, easeT);

				Vector2 currentBladeTip = Owner.MountedCenter + currentRotation.ToRotationVector2() * (BladeReach * Projectile.scale);
				OnBeatSlash(ComboStep, easeT, currentBladeTip);

				// Trigger sound and visual arc at the moment the cut begins
				if (!visualSpawned)
				{
					visualSpawned = true;
					SpawnSlashVisual();

					if (IsFinisher)
					{
						OnFinisherRelease(ComboStep, currentRotation.ToRotationVector2(), Projectile.damage);
					}
				}

				// EX (Horizon's Severance): Launch projected crescent wave at slash apex sharing this beat's hit set
				if (!exWaveSpawned && easeT >= 0.45f)
				{
					exWaveSpawned = true;
					SpawnExWave();
				}

				if (!soundPlayed)
				{
					soundPlayed = true;
					PlayMelodicSlashSound();
				}

				// Ambient purple Nihility sparks & rose petals along blade edge via InnoVault PRT
				if (!Main.dedServ && Main.rand.NextBool(2))
				{
					Vector2 sparkPos = Owner.MountedCenter + currentRotation.ToRotationVector2() * Main.rand.NextFloat(BladeReach * 0.4f, BladeReach * Projectile.scale);
					Vector2 sparkVel = currentRotation.ToRotationVector2().RotatedBy(swingDirection * MathHelper.PiOver2) * Main.rand.NextFloat(2f, 4.5f);
					Color sCol = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
					PRTLoader.NewParticle<VoidSparkPRT>(sparkPos, sparkVel, sCol, 0.85f);

					if (IsFinisher && Main.rand.NextBool(3))
					{
						PRTLoader.NewParticle<CrimsonPetalPRT>(sparkPos, sparkVel * 0.5f, new Color(245, 30, 55, 220), 0.7f);
					}
				}
			}
			else
			{
				// Recovery / Zanshin phase: decelerating hold
				sweepDamageActive = false;
				currentRotation = endAngle;
			}

			UpdatePlayerPose();

			// Combo transition or exit
			if (timer >= TotalTime)
			{
				if (Owner.channel && !Owner.noItems && !Owner.CCed)
				{
					// Progress to next combo step (0 -> 1 -> 2 -> 3 -> 0)
					ComboStep = (ComboStep + 1) % 4;
					timer = 0f;
					visualSpawned = false;
					exWaveSpawned = false;
					soundPlayed = false;
					hitNPCs = new HashSet<int>();

					for (int i = 0; i < Projectile.localNPCImmunity.Length; i++)
					{
						Projectile.localNPCImmunity[i] = 0;
					}
				}
				else
				{
					Projectile.Kill();
				}
			}
		}

		private void InitComboBeat()
		{
			hitNPCs = new HashSet<int>();
			exWaveSpawned = false;

			// Face cursor when starting a swing using InnoVault .To() extension
			Vector2 aim = Owner.MountedCenter.To(Main.MouseWorld);
			if (aim == Vector2.Zero)
				aim = Vector2.UnitX * Owner.direction;

			lockedDirection = aim.X >= 0 ? 1 : -1;
			Owner.direction = lockedDirection;

			// Alternating directions across the 4 beats:
			// 0: Horizontal forward sweep (+1)
			// 1: Downward cross-slash (+1)
			// 2: Rising reverse sweep (-1)
			// 3: Overhead execution cleave (+1)
			int dirSign = ComboStep == 2 ? -1 : 1;
			swingDirection = dirSign * lockedDirection;

			speedMult = Math.Max(0.7f, Owner.GetTotalAttackSpeed(DamageClass.Melee));
			var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
			float reachMult = naughtPlayer?.GetOdachiReachMultiplier() ?? 1f;
			float reach = BladeReach * reachMult;
			OnBeatStart(ComboStep, ref speedMult, ref reach);

			float baseAngle = aim.ToRotation();
			beatAimAngle = baseAngle;
			startAngle = baseAngle - swingDirection * SwingArc * 0.5f;
			endAngle = baseAngle + swingDirection * SwingArc * 0.5f;

			currentRotation = lastRotation = startAngle;
			Projectile.scale = (IsFinisher ? 1.30f : 1.10f) * reachMult;

			Projectile.damage = naughtPlayer != null
				? naughtPlayer.GetNaughtDamage(IsFinisher ? 1.40f : 1.0f)
				: (int)(Owner.GetWeaponDamage(Owner.HeldItem) * (IsFinisher ? 1.40f : 1.0f));
			if (naughtPlayer != null)
			{
				Projectile.CritChance = naughtPlayer.GetNaughtCritChance();
			}

			// Windup air breath
			if (ComboStep == 0)
			{
				SoundEngine.PlaySound(WeepSound.KatanaA with { Volume = 0.28f, Pitch = 0.1f }, Owner.MountedCenter);
			}
			else if (ComboStep == 2)
			{
				SoundEngine.PlaySound(WeepSound.KatanaB with { Volume = 0.25f, Pitch = 0.2f }, Owner.MountedCenter);
			}
		}

		private void SpawnSlashVisual()
		{
			if (Main.netMode == NetmodeID.Server)
				return;

			Vector2 aim = Owner.MountedCenter.To(Main.MouseWorld);
			float aimAngle = aim.ToRotation();
			// Spawns arc at ~85% point of the blade, acting as a trailing crescent rather than an extension past the tip
			float radius = BladeReach * Projectile.scale * 0.85f;
			int duration = (int)(SlashTime / speedMult) + 6;

			Vector3 hot = IsFinisher ? new Vector3(1.0f, 0.95f, 1.0f) : new Vector3(1.0f, 0.90f, 1.0f);
			Vector3 bright = IsFinisher ? new Vector3(0.95f, 0.15f, 0.90f) : new Vector3(0.78f, 0.16f, 1.0f);
			Vector3 deep = new Vector3(0.42f, 0.02f, 0.65f);
			Vector3 dark = new Vector3(0.08f, 0.01f, 0.14f);

			var slash = NaughtSlashVisual.SpawnSlashArc(
				Projectile.GetSource_FromThis(),
				Owner.MountedCenter,
				aimAngle,
				radius,
				SwingArc,
				swingDirection * lockedDirection,
				duration,
				hot, bright, deep, dark,
				Projectile.damage,
				Projectile.knockBack,
				Projectile.whoAmI,
				Owner.whoAmI
			);

			if (slash != null)
			{
				slash.Projectile.CritChance = Projectile.CritChance;
			}
		}

		private void SpawnExWave()
		{
			if (Main.myPlayer != Projectile.owner)
				return;

			var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
			if (naughtPlayer == null || !naughtPlayer.HasEidolonEx)
				return;

			Vector2 aimDir = beatAimAngle.ToRotationVector2();
			Vector2 spawnPos = Owner.MountedCenter + aimDir * 32f;
			Vector2 waveVel = aimDir * NaughtExWave.InitialSpeed;
			int waveDamage = naughtPlayer.GetNaughtDamage(0.45f);

			Projectile waveProj = Projectile.NewProjectileDirect(
				Projectile.GetSource_FromThis(),
				spawnPos,
				waveVel,
				ModContent.ProjectileType<NaughtExWave>(),
				waveDamage,
				Projectile.knockBack * 0.65f,
				Owner.whoAmI,
				ai0: ComboStep,
				ai1: swingDirection);

			if (waveProj != null && waveProj.active)
			{
				waveProj.CritChance = naughtPlayer.GetNaughtCritChance();
				if (waveProj.ModProjectile is NaughtExWave exWave)
				{
					exWave.SharedBeatHits = hitNPCs;
				}
			}
		}

		private void PlayMelodicSlashSound()
		{
			// Melodic cadence progression matching Onikiri's musical tuning:
			// Beat 0: 0.18f (Lower crisp draw note)
			// Beat 1: 0.38f (Rising melodious step)
			// Beat 2: 0.56f (Higher rising note)
			// Beat 3: 0.80f (Crescendo high melodic chime note + deep resonant slice, NO explosion)
			(float swingPitch, float volume) = ComboStep switch
			{
				0 => (0.18f, 0.70f),
				1 => (0.38f, 0.68f),
				2 => (0.56f, 0.72f),
				_ => (0.80f, 0.85f)
			};

			// Play the crisp Katana swing slice
			SoundEngine.PlaySound(WeepSound.KatanaSwing with { Pitch = swingPitch, Volume = volume }, Owner.MountedCenter);

			// On the 4th beat (Finisher), accompany with deep resonant blade hum (NO explosion)
			if (IsFinisher)
			{
				SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.88f, Pitch = -0.50f }, Owner.MountedCenter);
			}
		}

		private void UpdatePlayerPose()
		{
			Owner.heldProj = Projectile.whoAmI;
			Owner.direction = lockedDirection;
			Owner.itemTime = Owner.itemAnimation = 2;
			Owner.itemRotation = currentRotation;

			// Synchronize player arm with blade angle
			Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);

			Projectile.Center = Owner.MountedCenter + currentRotation.ToRotationVector2() * (BladeReach * 0.45f * Projectile.scale);
			Projectile.timeLeft = 10;
		}

		public bool HasHitNPC(int npcWhoAmI) => hitNPCs.Contains(npcWhoAmI);

		public override bool? CanHitNPC(NPC target)
		{
			if (hitNPCs.Contains(target.whoAmI))
				return false;

			return base.CanHitNPC(target);
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (!sweepDamageActive)
				return false;

			Rectangle greedyBox = targetHitbox;
			greedyBox.Inflate(24, 24); // Calamity Overhaul GrazePad standard

			Vector2 hand = Owner.MountedCenter;
			Vector2 tip = hand + currentRotation.ToRotationVector2() * (BladeReach * Projectile.scale);
			float collisionPoint = 0f;

			if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(), hand, tip, 30f * Projectile.scale, ref collisionPoint))
				return true;

			// Check line from previous frame tip to current frame tip to prevent fast swing tunneling
			Vector2 lastTip = hand + lastRotation.ToRotationVector2() * (BladeReach * Projectile.scale);
			if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(), lastTip, tip, 25f * Projectile.scale, ref collisionPoint))
				return true;

			return false;
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			modifiers.HitDirectionOverride = currentRotation.ToRotationVector2().X > 0 ? 1 : -1;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			RegisterHitFromVisual(target, hit, damageDone);
		}

		public void RegisterHitFromVisual(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (hitNPCs.Add(target.whoAmI))
			{
				OnBeatHit(ComboStep, target, hit, damageDone);

				if (IsFinisher)
				{
					// Finisher execution impact: deep resonant void cleave
					SoundEngine.PlaySound(WeepSound.KatanaHitB with { Pitch = -0.10f }, target.Center);
				}
				else
				{
					// Flowing combo slice: smooth, silky, deeply satisfying katana cut
					SoundEngine.PlaySound(WeepSound.KatanaHit with { Pitch = -0.05f }, target.Center);
				}

				int sparkCount = IsFinisher ? 16 : 8;
				for (int i = 0; i < sparkCount; i++)
				{
					Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
					Color sCol = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
					PRTLoader.NewParticle<VoidSparkPRT>(target.Center, velocity, sCol, IsFinisher ? 1.25f : 0.90f);
				}

				if (IsFinisher)
				{
					for (int p = 0; p < 6; p++)
					{
						Vector2 pVel = Main.rand.NextVector2Circular(4f, 4f) - Vector2.UnitY * Main.rand.NextFloat(1f, 2.5f);
						PRTLoader.NewParticle<CrimsonPetalPRT>(target.Center, pVel, new Color(245, 30, 55, 220), 0.85f);
					}
				}
			}
		}

		private void GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset)
		{
			// With NaughtBlade oriented upright with sharp convex edge facing right (+clockwise):
			// Clockwise swings (swingDirection > 0) have the clockwise edge leading the cut naturally.
			// Counter-clockwise swings (swingDirection < 0) flip vertically so the sharp edge leads into the cut.
			bool flipVertically = swingDirection < 0;
			effect = flipVertically ? SpriteEffects.FlipVertically : SpriteEffects.None;
			rotOffset = flipVertically ? -MathHelper.PiOver4 : MathHelper.PiOver4;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// Load the drawn blade sprite (NaughtBlade.png)
			Texture2D tex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtBlade", AssetRequestMode.ImmediateLoad).Value;
			if (tex == null)
				return false;

			Vector2 origin = tex.GetOrig();
			Vector2 hand = Owner.MountedCenter;
			float dist = BladeReach * 0.46f * Projectile.scale;
			GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);

			// Motion smear / trailing blade ghosts during active slice frames
			if (sweepDamageActive)
			{
				float angleDelta = Math.Abs(currentRotation - lastRotation);
				float strength = MathHelper.Clamp(angleDelta / 0.4f, 0f, 1f);
				int smearCount = 3;

				for (int i = 1; i <= smearCount; i++)
				{
					float amount = i / (float)(smearCount + 1);
					float rot = MathHelper.Lerp(currentRotation, lastRotation, amount);
					Vector2 pos = hand + rot.ToRotationVector2() * dist - Main.screenPosition;
					Color smearColor = new Color(175, 45, 240, 0) * (0.35f * strength * (1f - amount));
					Main.EntitySpriteDraw(tex, pos, null, smearColor, rot + rotOffset, origin, Projectile.scale * 0.70f, effect, 0);
				}
			}

			// Main blade sprite
			Vector2 drawPos = hand + currentRotation.ToRotationVector2() * dist - Main.screenPosition;
			Color drawLight = Lighting.GetColor((int)(hand.X / 16f), (int)(hand.Y / 16f));
			Main.EntitySpriteDraw(tex, drawPos, null, drawLight, currentRotation + rotOffset, origin, Projectile.scale * 0.70f, effect, 0);

			// Finisher violet-magenta glow aura
			if (IsFinisher && sweepDamageActive)
			{
				Color glow = new Color(210, 60, 255, 0) * 0.45f;
				Main.EntitySpriteDraw(tex, drawPos, null, glow, currentRotation + rotOffset, origin, Projectile.scale * 0.73f, effect, 0);
			}

			return false;
		}
	}
}
