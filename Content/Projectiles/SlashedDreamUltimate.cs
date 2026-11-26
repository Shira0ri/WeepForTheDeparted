using System;
using System.Collections.Generic;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.GlobalNPCs;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.Systems;
using WeepForTheDeparted.Common.Visuals;
using WeepForTheDeparted.Common.Visuals.Dismember;
using WeepForTheDeparted.Content.Projectiles.Charms;
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
		// 0: Unsheathing stance (240 frames) -> Ready Zanshin stance
		// 1: Rainblade 1 executed
		// 2: Rainblade 2 executed
		// 3: Rainblade 3 executed
		// 4: Stygian Resurge (finale) executed
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

		// Active 3D choreography state
		private bool isSwinging = false;
		private int swingTimer = 0;
		private int swingDuration = 34;
		private int swingType = 0; // 0 = Rainblade 1, 1 = Rainblade 2, 2 = Rainblade 3, 3 = Stygian Resurge
		private int prePhaseEndTick = 12;
		private int coilEndTick = 17;
		private int blurEndTick = 20;
		private bool preRibbonSpawned = false;
		private bool windupAudioPlayed = false;
		private bool realRibbonSpawned = false;
		private bool resurgeWindupFlashSpawned = false;
		private UltCoord3D stageStartPos3D = UltCoord3D.Zero;
		private float stageStartLean = 0f;

		// Pending delayed impact strike state
		private bool hasPendingStrike = false;
		private int pendingStrikeIndex = 0;
		private float pendingStrikeAngle = 0f;
		private float pendingStrikeTilt = 0f;
		private int impactDelayTimer = 0;
		private bool hasPendingPreemptiveStrike = false;
		private int preemptiveStrikeStage = 0;
		private float preemptiveStrikeAngle = 0f;
		private int preemptiveDelayTimer = 0;
		private bool preemptiveHitDone = false;
		private int hitstopFrames = 0;

		// Reverse sheathing animation state after final slash (synced to AcheronSheath.ogg: 3.74s total, middle silence 2.57s..3.10s, climax peak @ 3.57s = 214t)
		private bool isSheathing = false;
		private int sheathTimer = 0;
		public const int SheathSlideDuration = 154;
		public const int SheathGleamDuration = 20;
		public const int SheathPostDelay = 60;
		public const int SheathFinishDelay = 25;
		public const int MaxSiphonKnotsPerUlt = 9;
		private bool sheathClickDone = false;
		private bool sheathDamageDone = false;
		private UltCoord3D sheathStartPos3D = UltCoord3D.Zero;
		private float sheathStartLean = 0f;
		private const int UltMusicFadeOutTicks = 30;
		private SlotId ultMusicSlot = SlotId.Invalid;
		private float ultMusicFade = 1f;

		// Absolute freeze anchor in world space (player never moves physically into tiles)
		private Vector2 freezePosition = Vector2.Zero;
		private bool hasSetFreezePos = false;

		// 3D Coordinate Anchor & Virtual Transform (read by Ult3DRenderHandle)
		public Vector2 UltOrigin { get; private set; }
		public Vector2 SafeFrameOrigin { get; private set; }
		public int LockedFacingDir { get; private set; } = 1;

		public UltCoord3D PlayerPos3D { get; private set; } = UltCoord3D.Zero;
		public UltCoord3D PlayerVel3D { get; private set; } = UltCoord3D.Zero;
		public float PlayerLeanShear { get; private set; } = 0f;
		public float PlayerBodyTilt { get; private set; } = 0f;
		public float PlayerWidthScale { get; private set; } = 1f;
		public float PlayerSquashY { get; private set; } = 1f;
		public Vector2 PlayerStretchDir2D { get; private set; } = Vector2.UnitX;
		public float PlayerStretchAmount { get; private set; } = 0f;
		public int VisualDrawDirection { get; private set; } = 1;

		public UltCoord3D Hilt3D { get; private set; } = UltCoord3D.Zero;
		public UltCoord3D Tip3D { get; private set; } = new(120f, 0f, 0f);
		public UltCoord3D BladeBroadsideNormal3D { get; private set; } = new(0f, 1f, 0f);
		public float BladeMidZ => (Hilt3D.Z + Tip3D.Z) * 0.5f;
		public float BladeGlowIntensity { get; private set; } = 0.25f;
		public bool IsSheathPoseActive => Timer <= UnsheathDuration || isSheathing;
		public float CurrentSheathSlideDist { get; private set; } = 0f;

		private UltCoord3D prevHilt3D = UltCoord3D.Zero;
		private UltCoord3D prevTip3D = UltCoord3D.Zero;
		private bool hasPrevBladePose = false;
		private int readyStanceTicks = 0;

		public readonly List<UltAfterimage3D> Afterimages = new();
		public readonly List<UltBladeGhost3D> BladeGhosts = new();
		public readonly List<UltWaterRipple3D> WaterRipples = new();
		public readonly List<UltSlashRibbon3D> ActiveRibbons = new();
		public readonly List<UltCleaveSeam3D> ActiveSeams = new();
		public readonly List<UltGleamFlash3D> ActiveGleams = new();

		private const float BaseBladeLength3D = 156f;
		private const float WaterPlaneU = -21f;

		private struct PendingResurgeHit
		{
			public int NpcWhoAmI;
			public int Damage;
			public int DetonatedKnots;
		}

		private readonly List<PendingResurgeHit> pendingResurgeHits = new();
		private int totalKnotsDetonatedInUlt = 0;
		private int siphonHealedHpInUlt = 0;

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
			Projectile.timeLeft = UnsheathDuration + MaxSlashingTime + 400;
		}

		public override bool? CanDamage() => false;

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
			var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();

			// Lock initial freeze position, 3D origin, facing axis, and camera anchor on activation
			if (!hasSetFreezePos)
			{
				hasSetFreezePos = true;
				freezePosition = Owner.position;
				UltOrigin = Owner.MountedCenter;
				LockedFacingDir = ResolveInitialFacingDir();
				VisualDrawDirection = LockedFacingDir;
				Owner.direction = LockedFacingDir;

				Vector2 capturedScreenPos = Main.screenPosition;
				Vector2 baseAnchorScreenPos = ComputeCombatAnchorScreenPos();
				if (naughtPlayer != null)
				{
					if (Owner.whoAmI == Main.myPlayer && !Main.dedServ)
					{
						naughtPlayer.BeginUltCamera(capturedScreenPos, baseAnchorScreenPos);
					}
					naughtPlayer.IsUltVisualActive = true;
				}
				SafeFrameOrigin = Ult3DProjection.ComputeSafeFrameOrigin(UltOrigin, baseAnchorScreenPos);
			}

			// Keep logical Owner.position safely anchored at ultStartPos (zero tile clipping)
			Owner.position = freezePosition;
			Owner.velocity = Vector2.Zero;
			Owner.gravity = 0f;
			Owner.gfxOffY = 0f;
			Owner.fallStart = (int)(Owner.position.Y / 16f);
			Owner.immune = false;
			Owner.immuneTime = 0;
			Owner.immuneAlpha = 0;
			Owner.immuneNoBlink = true;
			Owner.itemAnimation = 2;
			Owner.itemTime = 2;
			Projectile.Center = UltOrigin;

			if (naughtPlayer != null)
			{
				naughtPlayer.IsUltVisualActive = true;
				naughtPlayer.ActiveUltProjIndex = Projectile.whoAmI;
			}

			// Keep player well-lit in dark caves and Nihility Domain dimness
			Lighting.AddLight((int)(Owner.position.X + Owner.width * 0.5f) / 16, (int)(Owner.position.Y + Owner.height * 0.5f) / 16, 0.95f, 1.0f, 1.0f);

			UpdateUltMusic();

			// Physical Hitstop Micro-Pause (freezes motion & ribbon erosion on blade impact for weighty bite)
			if (hitstopFrames > 0)
			{
				hitstopFrames--;
				return;
			}

			// Advance persistent 3D visual lifetimes
			UpdateVisualBuffers();

			// Act 0: Stance Preparation (Frame 1)
			if (Timer == 1)
			{
				WorldFreezeSystem.Activate("SlashedDreamUltimate");

				for (int i = 0; i < 35; i++)
				{
					Vector2 randPos = Main.screenPosition + new Vector2(Main.rand.NextFloat(Main.screenWidth), Main.rand.NextFloat(Main.screenHeight));
					Vector2 randVel = new(Main.rand.NextFloat(-1.8f, -0.4f), Main.rand.NextFloat(0.6f, 1.8f));
					PRTLoader.NewParticle<CrimsonPetalPRT>(randPos, randVel, new Color(245, 30, 55, 230), Main.rand.NextFloat(0.7f, 1.2f));
				}

				SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 0.95f }, Owner.Center);
				SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f, Volume = 0.9f }, Owner.Center);
				if (naughtPlayer != null && Owner.whoAmI == Main.myPlayer && !Main.dedServ)
				{
					naughtPlayer.AddUltCameraRecoil(new Vector2(-LockedFacingDir * 7f, 0f));
					naughtPlayer.PunchUltCameraZoom(0.10f);
				}

				SpawnWaterRipple(new UltCoord3D(0f, WaterPlaneU, 0f), 90f, 34, 0.55f, doubleRing: true);
			}

			// Unsheath sound cue on frame 35
			if (Timer == 35)
			{
				SoundEngine.PlaySound(WeepSound.AcheronUnsheath, Owner.Center);
				SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.35f, Volume = 1.0f }, Owner.Center);
			}

			// Unsheath blade draw begins on frame 60
			if (Timer == 60)
			{
				NihilityDomainSystem.StartReflectionRipple(Owner.Bottom);
				NihilityDomainSystem.TargetMusicSilence = 1f;
			}

			// At frame 240: blade fully drawn, enter Ready Zanshin stance
			if (Timer == UnsheathDuration)
			{
				SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.35f, Volume = 1.0f }, Owner.Center);
				if (naughtPlayer != null && Owner.whoAmI == Main.myPlayer && !Main.dedServ)
				{
					naughtPlayer.AddUltCameraRecoil(new Vector2(LockedFacingDir * 9f, 0f));
					naughtPlayer.PunchUltCameraZoom(-0.18f);
				}
				SpawnWaterRipple(new UltCoord3D(0f, WaterPlaneU, 0f), 130f, 32, 0.72f, doubleRing: true);
				StartUltMusic();
			}

			// Process dedicated preemptive slash hit before main dash-blur strike
			if (hasPendingPreemptiveStrike)
			{
				preemptiveDelayTimer--;
				if (preemptiveDelayTimer <= 0)
				{
					hasPendingPreemptiveStrike = false;
					if (!preemptiveHitDone)
					{
						preemptiveHitDone = true;
						ExecutePreemptiveRainblade(preemptiveStrikeStage, preemptiveStrikeAngle);
					}
				}
			}

			// Process delayed impact strike at the apex of the dash-blur
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

			// 9-second slashing window begins after unsheathing finishes
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

					// BladesongRain scales Stages 0-2 by 0.65f, leaving Stage 3 unscaled
					bool bladesongFast = naughtPlayer != null && naughtPlayer.HasBladesongRain;
					int stage1Hold = bladesongFast ? (int)(22 * 0.65f) : 22;
					int stage2Hold = bladesongFast ? (int)(22 * 0.65f) : 22;
					int stage3Hold = bladesongFast ? (int)(24 * 0.65f) : 24;
					const int resurgeHold = 30;

					bool autoAdvance =
						(SlashStage == 0 && holdTimer >= stage1Hold)
						|| (SlashStage == 1 && holdTimer >= stage2Hold)
						|| (SlashStage == 2 && holdTimer >= stage3Hold)
						|| (SlashStage == 3 && holdTimer >= resurgeHold);

					bool timedOut = remainingSlashingTime <= 0;

					if (((justPressed && slashCooldown <= 0 && !hasPendingStrike) || autoAdvance || timedOut) && !hasPendingStrike)
					{
						AdvanceSlash();
					}
				}
				else
				{
					// Post-Resurge: Wait for follow-through to settle, then begin reverse sheathing
					if (!isSwinging && !hasPendingStrike)
					{
						if (!isSheathing)
						{
							isSheathing = true;
							sheathTimer = 0;
							sheathClickDone = false;
							sheathDamageDone = false;
							sheathStartPos3D = PlayerPos3D;
							sheathStartLean = PlayerLeanShear;
							SoundEngine.PlaySound(WeepSound.AcheronSheath, Owner.Center);
							NihilityDomainSystem.StartReflectionRippleReverse(Owner.Bottom, SheathSlideDuration);
						}

						sheathTimer++;
						NihilityDismember.ReassembleFactor = 0f;

						if (sheathTimer >= SheathSlideDuration && !sheathClickDone)
						{
							sheathClickDone = true;
							SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.20f, Volume = 0.85f }, Owner.Center);
							SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.45f, Volume = 1.0f }, Owner.Center);
							SpawnWaterRipple(new UltCoord3D(0f, WaterPlaneU, 0f), 120f, 30, 0.70f, doubleRing: false);
						}

						if (sheathTimer >= SheathSlideDuration + SheathPostDelay && !sheathDamageDone)
						{
							sheathDamageDone = true;
							ExecuteSheathingSnap(naughtPlayer);
						}

						if (sheathTimer >= SheathSlideDuration + SheathPostDelay + SheathFinishDelay)
						{
							Projectile.Kill();
							return;
						}
					}
				}
			}

			// Evaluate current frame's 3D choreography pose, blade transform, gleams, ripples, & afterimages
			UpdateChoreographyAndBlade();
			Owner.direction = VisualDrawDirection >= 0 ? 1 : -1;
			UpdateUltCameraDirector(naughtPlayer);
			UpdatePlayerStance();
		}

		private Vector2 ComputeCombatAnchorScreenPos()
		{
			Vector2 halfScreen = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
			NPC primary = FindPrimaryTarget();
			Vector2 targetOffset = Vector2.Zero;
			if (primary != null)
			{
				Vector2 toTarget = (primary.Center - UltOrigin) * 0.26f;
				float len = toTarget.Length();
				if (len > 135f)
					toTarget *= 135f / len;
				targetOffset = toTarget;
			}
			return NaughtPlayer.ClampScreenPosition(UltOrigin + targetOffset - halfScreen);
		}

		private void UpdateUltCameraDirector(NaughtPlayer np)
		{
			if (np == null || !np.UltCameraLocked || Owner.whoAmI != Main.myPlayer || Main.dedServ)
				return;

			Vector2 halfScreen = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
			Vector2 baseAnchorScreen = ComputeCombatAnchorScreenPos();
			np.UpdateUltBaseAnchor(baseAnchorScreen);

			Vector2 projectedPlayer2D = Ult3DProjection.Project(PlayerPos3D, SafeFrameOrigin, LockedFacingDir, out _);
			Vector2 playerRel = projectedPlayer2D - SafeFrameOrigin;
			Vector2 softFollow = playerRel * 0.36f;
			float followLen = softFollow.Length();
			if (followLen > 165f)
				softFollow *= 165f / followLen;

			if (Timer <= UnsheathDuration)
			{
				float ease = Ult3DProjection.EaseInOutQuart(MathHelper.Clamp(Timer / (float)UnsheathDuration, 0f, 1f));
				np.SetUltCameraTarget(
					baseAnchorScreen,
					posStiffness: 5.5f,
					desiredZoom: MathHelper.Lerp(1.00f, 1.06f, ease),
					zoomStiffness: 5.0f,
					desiredVignette: MathHelper.Lerp(0.18f, 0.36f, ease));
				return;
			}

			if (isSheathing)
			{
				float returnEase = Ult3DProjection.EaseOutCubic(MathHelper.Clamp(sheathTimer / (float)(SheathSlideDuration * 0.85f), 0f, 1f));
				Vector2 restScreen = NaughtPlayer.ClampScreenPosition(UltOrigin - halfScreen + softFollow * (1f - returnEase));
				np.SetUltCameraTarget(
					restScreen,
					posStiffness: MathHelper.Lerp(5.8f, 8.2f, returnEase),
					desiredZoom: 1.00f,
					zoomStiffness: 6.5f,
					desiredVignette: 0.0f);
				return;
			}

			Vector2 lookAhead = new(LockedFacingDir * 18f, 0f);
			float posStiffness = 8.2f;
			float desiredZoom = 1.045f;
			float zoomStiffness = 6.0f;
			float desiredVignette = 0.32f;

			if (isSwinging)
			{
				if (swingType == 0 || swingType == 1)
				{
					Vector2 preLook = new(LockedFacingDir * 24f, swingType == 1 ? 14f : 0f);
					Vector2 realLook = swingType == 0 ? new Vector2(LockedFacingDir * 34f, 0f) : new Vector2(-30f, -20f);
					if (swingTimer <= prePhaseEndTick)
					{
						lookAhead = preLook;
						posStiffness = 7.2f;
						desiredZoom = 1.045f;
						zoomStiffness = 7.0f;
						desiredVignette = 0.34f;
					}
					else if (swingTimer < coilEndTick)
					{
						lookAhead = realLook;
						posStiffness = 9.5f;
						desiredZoom = 1.06f;
						zoomStiffness = 8.5f;
						desiredVignette = 0.42f;
					}
					else if (swingTimer <= blurEndTick)
					{
						lookAhead = realLook * 0.65f;
						posStiffness = 4.6f;
						desiredZoom = 1.005f;
						zoomStiffness = 10.5f;
						desiredVignette = 0.56f;
					}
					else
					{
						lookAhead = realLook * 0.75f;
						posStiffness = 8.6f;
						desiredZoom = 1.035f;
						zoomStiffness = 6.5f;
						desiredVignette = 0.36f;
					}
				}
				else if (swingType == 2)
				{
					Vector2 crashLook = new Vector2(24f, 26f);
					if (swingTimer < coilEndTick)
					{
						lookAhead = crashLook;
						posStiffness = 9.2f;
						desiredZoom = 1.065f;
						zoomStiffness = 8.5f;
						desiredVignette = 0.46f;
					}
					else if (swingTimer <= blurEndTick)
					{
						lookAhead = crashLook * 0.6f;
						posStiffness = 4.5f;
						desiredZoom = 0.995f;
						zoomStiffness = 11.0f;
						desiredVignette = 0.62f;
					}
					else
					{
						lookAhead = new Vector2(18f, 10f);
						posStiffness = 8.4f;
						desiredZoom = 1.04f;
						zoomStiffness = 6.5f;
						desiredVignette = 0.38f;
					}
				}
				else
				{
					if (swingTimer < coilEndTick)
					{
						lookAhead = new Vector2(-38f, 0f);
						posStiffness = 8.0f;
						desiredZoom = 1.085f;
						zoomStiffness = 5.5f;
						desiredVignette = 0.58f;
					}
					else if (swingTimer <= blurEndTick)
					{
						lookAhead = new Vector2(-24f, 0f);
						posStiffness = 4.2f;
						desiredZoom = 0.965f;
						zoomStiffness = 12.5f;
						desiredVignette = 0.68f;
					}
					else
					{
						lookAhead = new Vector2(-22f, 0f);
						posStiffness = 7.8f;
						desiredZoom = 1.00f;
						zoomStiffness = 6.5f;
						desiredVignette = 0.40f;
					}
				}
			}

			Vector2 desiredScreen = NaughtPlayer.ClampScreenPosition(baseAnchorScreen + softFollow + lookAhead);
			np.SetUltCameraTarget(desiredScreen, posStiffness, desiredZoom, zoomStiffness, desiredVignette);
		}

		private int ResolveInitialFacingDir()
		{
			NPC primary = FindPrimaryTarget();
			if (primary != null)
			{
				float dx = primary.Center.X - Owner.MountedCenter.X;
				if (Math.Abs(dx) > 4f)
					return dx >= 0f ? 1 : -1;
			}

			if (Owner.whoAmI == Main.myPlayer && !Main.dedServ)
			{
				float dx = Main.MouseWorld.X - Owner.MountedCenter.X;
				if (Math.Abs(dx) > 4f)
					return dx >= 0f ? 1 : -1;
			}

			return Owner.direction >= 0 ? 1 : -1;
		}

		private void AdvanceSlash()
		{
			holdTimer = 0;
			bool isHolding = WeepKeybinds.UltimateKeybind != null && WeepKeybinds.UltimateKeybind.Current;
			var np = Owner.GetModPlayer<NaughtPlayer>();
			bool hasBladesong = np != null && np.HasBladesongRain;
			bool hasSync = np != null && np.HasSynchronizedExecution;

			// Synchronized Execution: clones mirror Rainblade strikes 1-3
			if (hasSync && SlashStage < 3)
			{
				int cloneType = ModContent.ProjectileType<NaughtClone>();
				var syncClones = new List<NaughtClone>();
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (p.active && p.owner == Owner.whoAmI && p.type == cloneType && p.ModProjectile is NaughtClone clone)
					{
						syncClones.Add(clone);
					}
				}

				float syncScale = NaughtPlayer.GetSynchronizedCloneScale(syncClones.Count);
				Vector2 mirrorDir = new(LockedFacingDir, 0f);
				for (int i = 0; i < syncClones.Count; i++)
				{
					syncClones[i].PerformMirrorRainblade(SlashStage, mirrorDir, syncScale);
				}
			}

			stageStartPos3D = PlayerPos3D;
			stageStartLean = PlayerLeanShear;
			preRibbonSpawned = false;
			windupAudioPlayed = false;
			realRibbonSpawned = false;
			resurgeWindupFlashSpawned = false;
			hasPendingPreemptiveStrike = false;
			preemptiveHitDone = false;

			if (SlashStage == 0)
			{
				// Rainblade 1: Committed Forward-into-Background (+F, -Z) preemptive cut -> Explosive Forward-to-Screen (+F, +Z) cleave
				int duration = isHolding ? 22 : 34;
				if (hasBladesong)
					duration = Math.Max(12, (int)(duration * 0.65f));

				PartitionPreemptiveStageTiming(duration, out prePhaseEndTick, out coilEndTick, out blurEndTick);
				int preDelay = ComputePreemptiveImpactDelay(prePhaseEndTick);
				int impactDelay = blurEndTick - 1;
				float preAngle = LockedFacingDir > 0 ? MathHelper.ToRadians(8f) : MathHelper.ToRadians(172f);
				float angle = LockedFacingDir > 0 ? 0.04f : MathHelper.Pi - 0.04f;
				float tilt = MathHelper.ToRadians(58f);

				StartSwingState(0, duration);
				SchedulePreemptiveImpact(1, preAngle, preDelay);
				ScheduleImpact(1, angle, tilt, impactDelay, playWindupImmediately: false);
				SlashStage = 1;
				slashCooldown = Math.Max(impactDelay + 2, duration - 4);
			}
			else if (SlashStage == 1)
			{
				// Rainblade 2: Forward-Low-to-Screen (+F, -U, +Z) preemptive draw-slash -> Rising Diagonal Bottom-Right (-U, +Z) to Upper-Left (+U, -Z)
				int duration = isHolding ? 22 : 34;
				if (hasBladesong)
					duration = Math.Max(12, (int)(duration * 0.65f));

				PartitionPreemptiveStageTiming(duration, out prePhaseEndTick, out coilEndTick, out blurEndTick);
				int preDelay = ComputePreemptiveImpactDelay(prePhaseEndTick);
				int impactDelay = blurEndTick - 1;
				float preAngle = LockedFacingDir > 0 ? MathHelper.ToRadians(26f) : MathHelper.ToRadians(154f);
				float angle = MathHelper.ToRadians(-146f);
				float tilt = MathHelper.ToRadians(-46f);

				StartSwingState(1, duration);
				SchedulePreemptiveImpact(2, preAngle, preDelay);
				ScheduleImpact(2, angle, tilt, impactDelay, playWindupImmediately: false);
				SlashStage = 2;
				slashCooldown = Math.Max(impactDelay + 2, duration - 4);
			}
			else if (SlashStage == 2)
			{
				// Rainblade 3: Direct continuation from Upper-Left (+U, -Z) -> Big Descending Diagonal to Bottom-Right (-U, +Z)
				int duration = isHolding ? 24 : 38;
				if (hasBladesong)
					duration = Math.Max(13, (int)(duration * 0.65f));

				prePhaseEndTick = 0;
				coilEndTick = Math.Max(3, (int)MathF.Round(duration * 0.24f));
				blurEndTick = Math.Max(coilEndTick + 2, Math.Min(duration - 5, (int)MathF.Round(duration * 0.34f)));
				int impactDelay = blurEndTick - 1;
				float angle = MathHelper.ToRadians(34f);
				float tilt = MathHelper.ToRadians(46f);

				StartSwingState(2, duration);
				ScheduleImpact(3, angle, tilt, impactDelay, playWindupImmediately: true);
				SlashStage = 3;
				slashCooldown = Math.Max(impactDelay + 2, duration - 4);

				SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.45f, Volume = 0.75f }, Owner.Center);
			}
			else if (SlashStage == 3)
			{
				// Stygian Resurge (Unscaled): Calm Lethal Windup in Foreground Right (+Z) -> Instant Flash-Step Cleave Right (+Z) to Left (-Z)
				int duration = isHolding ? 28 : 46;
				prePhaseEndTick = 0;
				coilEndTick = isHolding ? 11 : 19;
				blurEndTick = coilEndTick + 2;
				int impactDelay = blurEndTick - 1;
				float angle = MathHelper.Pi;
				float tilt = MathHelper.ToRadians(24f);

				StartSwingState(3, duration);
				ScheduleImpact(4, angle, tilt, impactDelay, playWindupImmediately: true);
				SlashStage = 4;
			}
		}

		private static void PartitionPreemptiveStageTiming(int duration, out int preEnd, out int coilEnd, out int blurEnd)
		{
			preEnd = Math.Max(4, (int)MathF.Round(duration * 0.36f));
			coilEnd = Math.Max(preEnd + 2, (int)MathF.Round(duration * 0.50f));
			blurEnd = Math.Max(coilEnd + 2, Math.Min(duration - 4, (int)MathF.Round(duration * 0.59f)));
		}

		private static int ComputePreemptiveImpactDelay(int preEnd)
			=> Math.Max(2, Math.Min(preEnd - 1, (int)MathF.Round(preEnd * 0.60f)));

		private void SchedulePreemptiveImpact(int stageIndex, float angle, int delay)
		{
			hasPendingPreemptiveStrike = true;
			preemptiveStrikeStage = stageIndex;
			preemptiveStrikeAngle = angle;
			preemptiveDelayTimer = Math.Max(1, delay);
			preemptiveHitDone = false;
		}

		private void StartSwingState(int type, int duration)
		{
			isSwinging = true;
			swingTimer = 0;
			swingDuration = duration;
			swingType = type;
		}

		private void ScheduleImpact(int index, float angle, float tilt, int delay, bool playWindupImmediately)
		{
			hasPendingStrike = true;
			pendingStrikeIndex = index;
			pendingStrikeAngle = angle;
			pendingStrikeTilt = tilt;
			impactDelayTimer = delay;

			if (playWindupImmediately)
			{
				windupAudioPlayed = true;
				PlayWindupSounds();
			}
		}

		private void PlayWindupSounds()
		{
			SoundEngine.PlaySound(WeepSound.AcheronWindup, Owner.Center);
			SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.25f, Volume = 0.65f }, Owner.Center);
		}

		private void ExecuteSheathingSnap(NaughtPlayer np)
		{
			NihilityDismember.SnapBackAll();

			// Shatter persistent dimensional cleave seams upon sheathing snap
			for (int i = ActiveSeams.Count - 1; i >= 0; i--)
			{
				UltCleaveSeam3D seam = ActiveSeams[i];
				if (seam.PersistUntilSnap)
				{
					seam.PersistUntilSnap = false;
					seam.Age = 0;
					seam.MaxAge = 18;
					ActiveSeams[i] = seam;
				}
			}

			SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.15f, Volume = 1.0f }, Owner.Center);
			SoundEngine.PlaySound(WeepSound.KatanaHitB with { Pitch = -0.20f, Volume = 1.0f }, Owner.Center);

			bool hasThunder = np != null && np.HasThunderCore;
			bool hasSync = np != null && np.HasSynchronizedExecution;
			if (np != null)
			{
				Projectile.CritChance = np.GetNaughtCritChance();
			}

			NPC primaryTarget = null;

			for (int i = 0; i < pendingResurgeHits.Count; i++)
			{
				var hit = pendingResurgeHits[i];
				if (hit.NpcWhoAmI >= 0 && hit.NpcWhoAmI < Main.maxNPCs)
				{
					NPC target = Main.npc[hit.NpcWhoAmI];
					if (target.active && !target.friendly && (!target.dontTakeDamage || target.type == NPCID.TargetDummy))
					{
						if (primaryTarget == null || target.boss)
							primaryTarget = target;

						if (np != null)
						{
							np.StrikeNPC(
								target,
								hit.Damage,
								LockedFacingDir,
								crit: true,
								knockBack: 10f,
								sourceProj: Projectile,
								isUltimate: true);
						}
						else
						{
							target.SimpleStrikeNPC(hit.Damage, LockedFacingDir, true, 10f, WeepCompatibility.TrueMelee);
						}

						SpawnImpactVisuals(target.Center, true);
					}
				}
			}
			pendingResurgeHits.Clear();

			if (hasThunder)
			{
				Vector2 strikeOrigin = primaryTarget != null ? primaryTarget.Center : Owner.Center + new Vector2(LockedFacingDir * 200f, 0f);
				np.QueueThunderCoreBarrage(primaryTarget?.whoAmI ?? -1, strikeOrigin);
			}

			if (np != null && np.HasVoidHorizon)
			{
				int fissureDmg = np.GetNaughtDamage(WeepBalance.Active.VoidHorizonDamageMult);
				int portalCount = 0;
				int cloneType = ModContent.ProjectileType<NaughtClone>();
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (p.active && p.owner == Owner.whoAmI && p.type == cloneType)
					{
						int fIdx = Projectile.NewProjectile(Owner.GetSource_FromThis(), p.Center, Vector2.Zero, ModContent.ProjectileType<VoidFissure>(), fissureDmg, 0f, Owner.whoAmI, ai0: 0f);
						if (fIdx >= 0 && fIdx < Main.maxProjectiles && Main.projectile[fIdx].ModProjectile is VoidFissure vf)
						{
							vf.Init(p.Center, seed: 0.37f * (portalCount + 1), rotation: Main.rand.NextFloat(-0.35f, 0.35f), isFallbackPortal: false);
						}
						portalCount++;
					}
				}

				if (portalCount == 0)
				{
					Vector2 fallbackPos = primaryTarget != null
						? primaryTarget.Center
						: Owner.MountedCenter + new Vector2(LockedFacingDir * 160f, 0f);
					int fIdx = Projectile.NewProjectile(Owner.GetSource_FromThis(), fallbackPos, Vector2.Zero, ModContent.ProjectileType<VoidFissure>(), fissureDmg, 0f, Owner.whoAmI, ai0: 1f);
					if (fIdx >= 0 && fIdx < Main.maxProjectiles && Main.projectile[fIdx].ModProjectile is VoidFissure vf)
					{
						vf.Init(fallbackPos, seed: 1.15f, rotation: Main.rand.NextFloat(-0.25f, 0.25f), isFallbackPortal: true);
					}
				}
			}

			if (hasSync)
			{
				if (primaryTarget == null || !primaryTarget.active)
					primaryTarget = FindPrimaryTarget();

				Vector2 fallbackCrossCenter = primaryTarget != null && primaryTarget.active
					? primaryTarget.Center
					: Owner.Center + new Vector2(LockedFacingDir * 200f, 0f);

				int cloneType = ModContent.ProjectileType<NaughtClone>();
				var syncClones = new List<NaughtClone>();
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (p.active && p.owner == Owner.whoAmI && p.type == cloneType && p.ModProjectile is NaughtClone clone)
					{
						syncClones.Add(clone);
					}
				}

				float syncScale = NaughtPlayer.GetSynchronizedCloneScale(syncClones.Count);
				for (int i = 0; i < syncClones.Count; i++)
				{
					syncClones[i].PerformSheathingCrossSlash(primaryTarget, fallbackCrossCenter, syncScale, emitRuptureFlash: i == 0);
				}
			}

			if (np != null && np.HasEmancipatorWill)
			{
				np.EmancipatorCritBuffTimer = 600;
			}

			if (np != null && Owner.whoAmI == Main.myPlayer && !Main.dedServ)
			{
				np.AddUltCameraRecoil(new Vector2(LockedFacingDir * 14f, 2f));
				np.PunchUltCameraZoom(-0.24f);
			}
			NihilityDomainSystem.AnomalyPulse = 0.85f;
			SpawnWaterRipple(new UltCoord3D(0f, WaterPlaneU, 0f), 220f, 26, 0.85f, doubleRing: true);
			hitstopFrames = 8;
		}

		private void UpdateVisualBuffers()
		{
			for (int i = Afterimages.Count - 1; i >= 0; i--)
			{
				var g = Afterimages[i];
				g.Age++;
				if (g.Age >= g.MaxAge)
					Afterimages.RemoveAt(i);
				else
					Afterimages[i] = g;
			}

			for (int i = BladeGhosts.Count - 1; i >= 0; i--)
			{
				var bg = BladeGhosts[i];
				bg.Age++;
				if (bg.Age >= bg.MaxAge)
					BladeGhosts.RemoveAt(i);
				else
					BladeGhosts[i] = bg;
			}

			for (int i = WaterRipples.Count - 1; i >= 0; i--)
			{
				var wr = WaterRipples[i];
				wr.Age++;
				if (wr.Age >= wr.MaxAge)
					WaterRipples.RemoveAt(i);
				else
					WaterRipples[i] = wr;
			}

			for (int i = ActiveRibbons.Count - 1; i >= 0; i--)
			{
				var rib = ActiveRibbons[i];
				rib.Age++;
				if (rib.Age >= rib.MaxAge)
					ActiveRibbons.RemoveAt(i);
				else
					ActiveRibbons[i] = rib;
			}

			for (int i = ActiveSeams.Count - 1; i >= 0; i--)
			{
				var seam = ActiveSeams[i];
				seam.Age++;
				if (!seam.PersistUntilSnap && seam.Age >= seam.MaxAge)
					ActiveSeams.RemoveAt(i);
				else
					ActiveSeams[i] = seam;
			}

			ActiveGleams.Clear();
		}

		private void UpdateChoreographyAndBlade()
		{
			UltCoord3D prevPos = PlayerPos3D;

			// =========================================================================
			// PHASE 0: UNSHEATHING (4.0s at (0, 0, 0) with Iaido stance & mouth gleam)
			// =========================================================================
			if (Timer <= UnsheathDuration)
			{
				float progress = MathHelper.Clamp((Timer - 60f) / 180f, 0f, 1f);
				float slideProgress = VaultUtils.EaseQuadOut(progress);
				CurrentSheathSlideDist = MathHelper.Lerp(0f, 22f, slideProgress);

				PlayerPos3D = new UltCoord3D(0f, -3f * slideProgress, 0f);
				PlayerVel3D = PlayerPos3D - prevPos;
				VisualDrawDirection = LockedFacingDir;
				PlayerLeanShear = MathHelper.Lerp(0.10f, 0.22f, slideProgress) * LockedFacingDir;
				PlayerBodyTilt = 0f;
				PlayerWidthScale = 1f;
				PlayerSquashY = MathHelper.Lerp(0.98f, 0.93f, slideProgress);
				PlayerStretchAmount = 0f;
				BladeGlowIntensity = 0.25f;

				if (Timer >= 35 && Timer <= 85)
				{
					float t = (Timer - 35f) / 50f;
					float flashAlpha = t < 0.45f ? (t / 0.45f) : (1f - (t - 0.45f) / 0.55f);
					EmitSheathMouthGleam(slideAdvance: 0f, flashAlpha);
				}

				if (Timer >= 220 && Timer <= 240)
				{
					float t = (Timer - 220f) / 20f;
					float flashAlpha = 1f - t;
					EmitSheathMouthGleam(slideAdvance: CurrentSheathSlideDist * 0.4f, flashAlpha * 0.85f);
				}

				hasPrevBladePose = false;
				return;
			}

			// =========================================================================
			// PHASE 2: REVERSE SHEATHING (smoothly returns from -Z back to (0, 0, 0))
			// =========================================================================
			if (isSheathing)
			{
				float settleT = Ult3DProjection.EaseOutCubic(MathHelper.Clamp(sheathTimer / 42f, 0f, 1f));
				PlayerPos3D = UltCoord3D.Lerp(sheathStartPos3D, UltCoord3D.Zero, settleT);
				PlayerVel3D = PlayerPos3D - prevPos;
				VisualDrawDirection = LockedFacingDir;
				PlayerLeanShear = MathHelper.Lerp(sheathStartLean, 0.14f * LockedFacingDir, settleT);
				PlayerBodyTilt = 0f;
				PlayerWidthScale = 1f;
				PlayerSquashY = MathHelper.Lerp(0.94f, 0.98f, settleT);
				PlayerStretchAmount = 0f;

				float p = MathHelper.Clamp(sheathTimer / (float)SheathSlideDuration, 0f, 1f);
				float reverseProgress = 1f - VaultUtils.EaseQuadOut(p);
				CurrentSheathSlideDist = MathHelper.Lerp(0f, 22f, reverseProgress);

				if (sheathTimer >= SheathSlideDuration && sheathTimer <= SheathSlideDuration + SheathGleamDuration)
				{
					float flashAlpha = 1f - MathHelper.Clamp((sheathTimer - SheathSlideDuration) / (float)SheathGleamDuration, 0f, 1f);
					EmitSheathMouthGleam(slideAdvance: 0f, flashAlpha);
				}

				hasPrevBladePose = false;
				return;
			}

			// =========================================================================
			// PHASE 1: ACTIVE 3D SLASH CHOREOGRAPHY vs READY ZANSHIN STANCE
			// =========================================================================
			if (isSwinging)
			{
				UpdateActiveSlash3D(prevPos);
			}
			else
			{
				UpdateIdleZanshinStance(prevPos);
			}

			// Emit subtle blade ghost trail whenever blade swings rapidly through 3D space
			if (hasPrevBladePose)
			{
				float tipSpeed = (Tip3D - prevTip3D).Length();
				if (tipSpeed > 34f && (isSwinging || BladeGhosts.Count < 4))
				{
					BladeGhosts.Add(new UltBladeGhost3D
					{
						Hilt3D = prevHilt3D,
						Tip3D = prevTip3D,
						BroadsideNormal3D = BladeBroadsideNormal3D,
						DrawDirection = VisualDrawDirection,
						Age = 0,
						MaxAge = 7,
						Opacity = 0.55f
					});
				}
			}

			prevHilt3D = Hilt3D;
			prevTip3D = Tip3D;
			hasPrevBladePose = true;
		}

		private void EmitSheathMouthGleam(float slideAdvance, float flashAlpha)
		{
			if (flashAlpha <= 0.002f)
				return;

			Vector2 playerWorld = Ult3DProjection.Project(PlayerPos3D, SafeFrameOrigin, LockedFacingDir, out float persp);
			Vector2 slideDir = new(LockedFacingDir * 0.94f, -0.34f * Owner.gravDir);
			Vector2 hipOffset = new Vector2(LockedFacingDir * -6f, 2f * Owner.gravDir) * persp;
			Vector2 sheathPos = playerWorld + hipOffset;
			float baseAngle = MathHelper.ToRadians(205f);
			float sheathRot = LockedFacingDir > 0 ? baseAngle : -baseAngle;
			Vector2 mouthOffset = new Vector2(LockedFacingDir > 0 ? -16f : 16f, 32f) * 0.95f * persp;
			Vector2 gleamPos = sheathPos + mouthOffset.RotatedBy(sheathRot) + slideDir * slideAdvance;

			ActiveGleams.Add(new UltGleamFlash3D
			{
				WorldPos = gleamPos,
				AxisAngle = slideDir.ToRotation(),
				Alpha = flashAlpha,
				Scale = persp
			});
		}

		private UltCoord3D Screen3D(float right, float up, float z) => new(right * LockedFacingDir, up, z);

		private void UpdateIdleZanshinStance(UltCoord3D prevPos)
		{
			readyStanceTicks++;
			float breathe = MathF.Sin(readyStanceTicks * 0.08f);

			if (SlashStage == 0)
			{
				// Low, poised Zanshin ready stance right after unsheathing
				float settle = Ult3DProjection.EaseOutCubic(MathHelper.Clamp(readyStanceTicks / 12f, 0f, 1f));
				PlayerPos3D = new UltCoord3D(4f * settle, -5f * settle + breathe * 1.1f, 0f);
				VisualDrawDirection = LockedFacingDir;
				PlayerLeanShear = 0.24f * settle * LockedFacingDir;
				PlayerBodyTilt = 0f;
				PlayerWidthScale = 1f;
				PlayerSquashY = MathHelper.Lerp(0.97f, 0.90f, settle);
				PlayerStretchAmount = 0f;
				BladeGlowIntensity = 0.32f + 0.10f * breathe;

				// Blade trailed low and back into -Z behind wielder
				UltCoord3D handOffset = Screen3D(-4f * VisualDrawDirection, -4f, -4f);
				UltCoord3D bladeDir = Screen3D(-0.82f * VisualDrawDirection, -0.44f, -0.36f).SafeNormalize(Screen3D(-1f, -0.4f, -0.3f));
				SetBladeFromDirection(PlayerPos3D + handOffset, bladeDir, BaseBladeLength3D, Screen3D(0f, 1f, 0.25f));

				if (readyStanceTicks % 24 == 10)
				{
					SpawnWaterRipple(new UltCoord3D(PlayerPos3D.F, WaterPlaneU, PlayerPos3D.Z), 88f, 34, 0.45f, doubleRing: false);
				}

				if (!Main.dedServ && readyStanceTicks % 5 == 0)
				{
					Vector2 tipWorld = Ult3DProjection.Project(Tip3D, SafeFrameOrigin, LockedFacingDir, out _);
					PRTLoader.NewParticle<VoidSparkPRT>(tipWorld, new Vector2(-VisualDrawDirection * 0.8f, -0.6f), new Color(245, 35, 70), 0.55f);
				}
			}
			else
			{
				// Hold settled Zanshin posture from whichever slash just ended
				PlayerVel3D = PlayerPos3D - prevPos;
				PlayerStretchAmount = 0f;
				BladeGlowIntensity = 0.36f + 0.08f * breathe;

				if (!Main.dedServ && readyStanceTicks % 7 == 0)
				{
					Vector2 tipWorld = Ult3DProjection.Project(Tip3D, SafeFrameOrigin, LockedFacingDir, out _);
					PRTLoader.NewParticle<VoidSparkPRT>(tipWorld, new Vector2(0f, -0.7f), new Color(255, 50, 85), 0.50f);
				}
			}
		}

		private void UpdateActiveSlash3D(UltCoord3D prevPos)
		{
			if (swingType == 0)
			{
				EvaluateRainblade1(prevPos);
			}
			else if (swingType == 1)
			{
				EvaluateRainblade2(prevPos);
			}
			else if (swingType == 2)
			{
				EvaluateRainblade3(prevPos);
			}
			else
			{
				EvaluateStygianResurge(prevPos);
			}
		}

		private float ResolvePreemptiveForwardOffset(float fallbackF = 58f)
		{
			NPC primary = FindPrimaryTarget();
			if (primary != null)
			{
				float forwardProj = (primary.Center.X - UltOrigin.X) * LockedFacingDir;
				if (forwardProj > 12f)
					return MathHelper.Clamp(forwardProj * 0.55f, 44f, 96f);
			}
			return fallbackF;
		}

		private void EvaluateRainblade1(UltCoord3D prevPos)
		{
			float preFwd = ResolvePreemptiveForwardOffset(58f);
			UltCoord3D preEndPos = new(preFwd, -6f, -215f);
			UltCoord3D coilPos = new(preFwd * 0.72f, -12f, -230f);
			UltCoord3D realApexPos = Screen3D(40f, 2f, 166f);
			UltCoord3D zanshinEndPos = Screen3D(44f, -6f, 160f);

			if (swingTimer <= prePhaseEndTick)
			{
				// Sub-phase 1: Committed Forward-into-Background (+F, -Z) preemptive lunge across the enemy plane
				float p = swingTimer / (float)Math.Max(1, prePhaseEndTick);
				int preDashStart = Math.Max(1, prePhaseEndTick / 4);
				int preDashEnd = Math.Max(preDashStart + 1, (int)MathF.Round(prePhaseEndTick * 0.62f));

				if (swingTimer == 1)
				{
					SoundEngine.PlaySound(WeepSound.KatanaA with { Volume = 0.62f, Pitch = 0.25f }, Owner.Center);
					SpawnWaterRipple(new UltCoord3D(stageStartPos3D.F, WaterPlaneU, stageStartPos3D.Z), 95f, 24, 0.55f, doubleRing: false);
				}

				if (swingTimer < preDashStart)
				{
					PlayerPos3D = UltCoord3D.Lerp(stageStartPos3D, stageStartPos3D + new UltCoord3D(-8f, -5f, 18f), swingTimer / (float)preDashStart);
					VisualDrawDirection = LockedFacingDir;
					PlayerLeanShear = -0.14f * LockedFacingDir;
					PlayerSquashY = 0.88f;
					PlayerStretchAmount = 0f;
					SetBladeFromDirection(PlayerPos3D + new UltCoord3D(6f, -2f, 8f), new UltCoord3D(0.55f, 0.22f, 0.80f), BaseBladeLength3D, new UltCoord3D(0f, 1f, 0f));
				}
				else if (swingTimer <= preDashEnd)
				{
					float u = (swingTimer - preDashStart) / (float)Math.Max(1, preDashEnd - preDashStart);
					float dashEase = Ult3DProjection.EaseOutCubic(u);
					PlayerPos3D = UltCoord3D.Lerp(stageStartPos3D, preEndPos, dashEase);
					VisualDrawDirection = LockedFacingDir;
					PlayerLeanShear = 0.36f * LockedFacingDir;
					PlayerSquashY = 0.90f;
					ApplyDashStretch(prevPos, PlayerPos3D, 1.12f * MathF.Sin(u * MathHelper.Pi));
					EmitDashEchoes(prevPos, PlayerPos3D, count: 2, starkLead: false, initialAlpha: 0.44f);
					EmitWaterSprayAlongPath(prevPos, PlayerPos3D, 2);

					if (!preRibbonSpawned)
					{
						preRibbonSpawned = true;
						ActiveRibbons.Add(new UltSlashRibbon3D
						{
							Center3D = new UltCoord3D(preFwd * 0.78f, -3f, -85f),
							AxisU3D = new UltCoord3D(0.86f, -0.05f, 0.50f),
							AxisV3D = new UltCoord3D(-0.48f, 0.08f, 0.87f),
							StartAngle = MathHelper.ToRadians(30f),
							EndAngle = MathHelper.ToRadians(-140f),
							RadiusStart = 140f,
							RadiusEnd = 180f,
							WidthStart = 46f,
							WidthEnd = 62f,
							ZBiasEnd = -95f,
							Thick = 0.48f,
							Seed = 0.17f,
							Age = 0,
							MaxAge = Math.Max(10, prePhaseEndTick + 4),
							SweepFrames = Math.Max(2, preDashEnd - preDashStart + 1),
							Opacity = 0.82f,
							RazorWiden = 0.24f,
							IsPreemptive = true,
							IsResurge = false,
							DepthDimBias = 0.14f
						});
					}

					// Torso-first whip slicing across +F from foreground/neutral into -Z background
					float whipT = MathHelper.Clamp(u * 1.15f - 0.10f, 0f, 1f);
					UltCoord3D sweepDir = UltCoord3D.Lerp(new UltCoord3D(0.50f, 0.14f, 0.85f), new UltCoord3D(-0.35f, -0.10f, -0.93f), Ult3DProjection.EaseOutCubic(whipT))
						.SafeNormalize(new UltCoord3D(0.75f, 0f, -0.65f));
					SetBladeFromDirection(PlayerPos3D + new UltCoord3D(8f, -2f, -4f), sweepDir, BaseBladeLength3D * 1.02f, new UltCoord3D(0f, 1f, 0f));
				}
				else
				{
					PlayerPos3D = preEndPos;
					VisualDrawDirection = LockedFacingDir;
					PlayerLeanShear = 0.26f * LockedFacingDir;
					PlayerSquashY = 0.88f;
					PlayerStretchAmount = 0f;
					SetBladeFromDirection(PlayerPos3D + new UltCoord3D(6f, -4f, -6f), new UltCoord3D(-0.35f, -0.12f, -0.92f), BaseBladeLength3D, new UltCoord3D(0f, 1f, 0f));
				}

				BladeGlowIntensity = 0.42f;
				PlayerWidthScale = 1f;
				PlayerBodyTilt = 0.06f * LockedFacingDir * p;
			}
			else if (swingTimer < coilEndTick)
			{
				// Sub-phase 2: Grounded coiled stillness in -Z, telegraphing explosive reversal to +Z
				if (!windupAudioPlayed)
				{
					windupAudioPlayed = true;
					PlayWindupSounds();
					SpawnWaterRipple(new UltCoord3D(coilPos.F, WaterPlaneU, coilPos.Z), 110f, 26, 0.60f, doubleRing: true);
				}

				float c = (swingTimer - prePhaseEndTick) / (float)Math.Max(1, coilEndTick - prePhaseEndTick);
				PlayerPos3D = UltCoord3D.Lerp(preEndPos, coilPos, c);
				VisualDrawDirection = -LockedFacingDir;
				PlayerLeanShear = MathHelper.Lerp(0.18f * LockedFacingDir, -0.34f * LockedFacingDir, c);
				PlayerBodyTilt = -0.06f * LockedFacingDir;
				PlayerWidthScale = 1f;
				PlayerSquashY = MathHelper.Lerp(0.88f, 0.82f, c);
				PlayerStretchAmount = 0f;
				BladeGlowIntensity = 0.55f + 0.25f * c;

				SetBladeFromDirection(PlayerPos3D + new UltCoord3D(-4f, -6f, -6f), new UltCoord3D(-0.88f, -0.18f, -0.44f), BaseBladeLength3D, new UltCoord3D(0f, 0.9f, 0.35f));
			}
			else if (swingTimer <= blurEndTick)
			{
				// Sub-phase 3: Explosive 1-3f Dash-Blur reversing violently toward Screen (+Z, Right)
				float u = (swingTimer - coilEndTick) / (float)Math.Max(1, blurEndTick - coilEndTick);
				float dashEase = Ult3DProjection.EaseOutCubic(u);
				PlayerPos3D = UltCoord3D.Lerp(coilPos, realApexPos, dashEase);
				VisualDrawDirection = 1;
				PlayerLeanShear = MathHelper.Lerp(-0.15f, 0.46f, dashEase);
				PlayerBodyTilt = 0.08f * dashEase;
				PlayerWidthScale = 1.08f;
				PlayerSquashY = 0.90f;
				ApplyDashStretch(prevPos, PlayerPos3D, 1.55f * MathF.Sin(MathHelper.Clamp(u, 0.1f, 0.95f) * MathHelper.Pi));
				EmitDashEchoes(prevPos, PlayerPos3D, count: 3, starkLead: true, initialAlpha: 0.65f);
				EmitWaterSprayAlongPath(prevPos, PlayerPos3D, 3);

				if (!realRibbonSpawned)
				{
					realRibbonSpawned = true;
					ActiveRibbons.Add(new UltSlashRibbon3D
					{
						Center3D = Screen3D(18f, -2f, 20f),
						AxisU3D = Screen3D(0.88f, -0.04f, 0.47f),
						AxisV3D = Screen3D(-0.45f, 0.12f, 0.89f),
						StartAngle = MathHelper.ToRadians(-140f),
						EndAngle = MathHelper.ToRadians(28f),
						RadiusStart = 175f,
						RadiusEnd = 245f,
						WidthStart = 76f,
						WidthEnd = 132f,
						ZBiasEnd = 148f,
						Thick = 0.62f,
						Seed = 0.31f,
						Age = 0,
						MaxAge = 26,
						SweepFrames = Math.Max(2, blurEndTick - coilEndTick),
						Opacity = 1.0f,
						RazorWiden = 0.48f,
						IsPreemptive = false,
						IsResurge = false,
						DepthDimBias = 0f
					});
					SpawnWaterRipple(new UltCoord3D(realApexPos.F, WaterPlaneU, realApexPos.Z), 190f, 34, 0.85f, doubleRing: true);
				}

				// Torso-first lag: hilt leads while blade tip whips across the foreground
				float whipT = MathHelper.Clamp(u * 1.18f - 0.12f, 0f, 1f);
				UltCoord3D bladeDir = UltCoord3D.Lerp(Screen3D(-0.85f, -0.10f, -0.48f), Screen3D(0.92f, 0.08f, 0.38f), Ult3DProjection.EaseOutCubic(whipT))
					.SafeNormalize(Screen3D(1f, 0f, 0.4f));
				SetBladeFromDirection(PlayerPos3D + Screen3D(10f, -2f, 14f), bladeDir, BaseBladeLength3D * 1.06f, Screen3D(0f, 0.94f, -0.34f));
				BladeGlowIntensity = 1.05f;
			}
			else
			{
				// Sub-phase 4: Held Zanshin follow-through pivoting smoothly on lead foot in foreground +Z
				float z = (swingTimer - blurEndTick) / (float)Math.Max(1, swingDuration - blurEndTick);
				float settle = Ult3DProjection.EaseOutCubic(z);
				PlayerPos3D = UltCoord3D.Lerp(realApexPos, zanshinEndPos, settle);
				VisualDrawDirection = 1;
				PlayerLeanShear = MathHelper.Lerp(0.46f, 0.22f, settle);
				PlayerBodyTilt = MathHelper.Lerp(0.08f, 0.02f, settle);
				PlayerWidthScale = 1f;
				PlayerSquashY = MathHelper.Lerp(0.88f, 0.92f, settle);
				PlayerStretchAmount = 0f;

				UltCoord3D followDir = UltCoord3D.Lerp(Screen3D(0.92f, 0.08f, 0.38f), Screen3D(0.76f, -0.14f, -0.63f), settle)
					.SafeNormalize(Screen3D(0.8f, -0.1f, -0.5f));
				SetBladeFromDirection(PlayerPos3D + Screen3D(8f, -3f, 8f), followDir, BaseBladeLength3D, Screen3D(0.2f, 0.92f, -0.32f));
				BladeGlowIntensity = MathHelper.Lerp(0.85f, 0.38f, settle);
			}

			PlayerVel3D = PlayerPos3D - prevPos;
		}

		private void EvaluateRainblade2(UltCoord3D prevPos)
		{
			float preFwd = ResolvePreemptiveForwardOffset(56f);
			UltCoord3D preLowPos = new(preFwd, -68f, 172f);
			UltCoord3D coilLowPos = Screen3D(38f, -76f, 174f);
			UltCoord3D skyApexPos = Screen3D(-22f, 136f, -295f);
			UltCoord3D skyZanshinPos = Screen3D(-24f, 138f, -298f);

			if (swingTimer <= prePhaseEndTick)
			{
				// Sub-phase 1: Committed Forward-Low-to-Screen (+F, -U, +Z) preemptive draw-slash across enemy plane
				float p = swingTimer / (float)Math.Max(1, prePhaseEndTick);
				float dropEase = Ult3DProjection.EaseOutCubic(MathHelper.Clamp(p * 1.45f, 0f, 1f));

				if (swingTimer == 1)
				{
					SoundEngine.PlaySound(WeepSound.KatanaB with { Volume = 0.62f, Pitch = 0.28f }, Owner.Center);
					SpawnWaterRipple(new UltCoord3D(stageStartPos3D.F, WaterPlaneU, stageStartPos3D.Z), 110f, 24, 0.60f, doubleRing: false);
				}

				PlayerPos3D = UltCoord3D.Lerp(stageStartPos3D, preLowPos, dropEase);
				VisualDrawDirection = LockedFacingDir;
				PlayerLeanShear = MathHelper.Lerp(stageStartLean, 0.36f * LockedFacingDir, dropEase);
				PlayerBodyTilt = 0.06f * LockedFacingDir * dropEase;
				PlayerWidthScale = 1f;
				PlayerSquashY = MathHelper.Lerp(0.92f, 0.80f, dropEase);

				if (p < 0.72f)
				{
					ApplyDashStretch(prevPos, PlayerPos3D, 0.92f * MathF.Sin(MathHelper.Clamp(p / 0.72f, 0f, 1f) * MathHelper.Pi));
					if (swingTimer % 2 == 1)
						EmitDashEchoes(prevPos, PlayerPos3D, count: 1, starkLead: false, initialAlpha: 0.42f);
					EmitWaterSprayAlongPath(prevPos, PlayerPos3D, 2);
				}
				else
				{
					PlayerStretchAmount = 0f;
				}

				if (!preRibbonSpawned)
				{
					preRibbonSpawned = true;
					ActiveRibbons.Add(new UltSlashRibbon3D
					{
						Center3D = new UltCoord3D(preFwd * 0.74f, -38f, 118f),
						AxisU3D = new UltCoord3D(0.86f, -0.30f, 0.41f),
						AxisV3D = new UltCoord3D(-0.32f, -0.78f, 0.54f),
						StartAngle = MathHelper.ToRadians(-85f),
						EndAngle = MathHelper.ToRadians(42f),
						RadiusStart = 135f,
						RadiusEnd = 175f,
						WidthStart = 48f,
						WidthEnd = 64f,
						ZBiasEnd = 48f,
						Thick = 0.48f,
						Seed = 0.53f,
						Age = 0,
						MaxAge = Math.Max(10, prePhaseEndTick + 4),
						SweepFrames = Math.Max(2, prePhaseEndTick / 2),
						Opacity = 0.84f,
						RazorWiden = 0.28f,
						IsPreemptive = true,
						IsResurge = false,
						DepthDimBias = 0f
					});
				}

				float whipT = MathHelper.Clamp(p * 1.35f, 0f, 1f);
				UltCoord3D lowDir = UltCoord3D.Lerp(new UltCoord3D(0.55f, 0.34f, -0.15f), new UltCoord3D(0.84f, -0.38f, 0.38f), Ult3DProjection.EaseOutCubic(whipT))
					.SafeNormalize(new UltCoord3D(0.84f, -0.38f, 0.38f));
				SetBladeFromDirection(PlayerPos3D + new UltCoord3D(6f, -6f, 10f), lowDir, BaseBladeLength3D * 1.02f, new UltCoord3D(0f, 0.9f, 0.4f));
				BladeGlowIntensity = 0.48f;
			}
			else if (swingTimer < coilEndTick)
			{
				// Sub-phase 2: Tight coiled low-draw crouch in Bottom-Right (-U, +Z)
				if (!windupAudioPlayed)
				{
					windupAudioPlayed = true;
					PlayWindupSounds();
					SpawnWaterRipple(new UltCoord3D(coilLowPos.F, WaterPlaneU, coilLowPos.Z), 130f, 28, 0.68f, doubleRing: true);
				}

				float c = (swingTimer - prePhaseEndTick) / (float)Math.Max(1, coilEndTick - prePhaseEndTick);
				PlayerPos3D = UltCoord3D.Lerp(preLowPos, coilLowPos, c);
				VisualDrawDirection = -1;
				PlayerLeanShear = MathHelper.Lerp(0.30f * LockedFacingDir, 0.14f, c);
				PlayerBodyTilt = -0.10f * c;
				PlayerWidthScale = 1f;
				PlayerSquashY = MathHelper.Lerp(0.80f, 0.76f, c);
				PlayerStretchAmount = 0f;
				BladeGlowIntensity = 0.60f + 0.25f * c;

				SetBladeFromDirection(PlayerPos3D + Screen3D(4f, -8f, 8f), Screen3D(0.78f, -0.45f, 0.42f), BaseBladeLength3D, Screen3D(0f, 0.9f, 0.3f));
			}
			else if (swingTimer <= blurEndTick)
			{
				// Sub-phase 3: Explosive rising diagonal cleave from Bottom-Right (-U, +Z) up into Upper-Left (+U, -Z)
				float u = (swingTimer - coilEndTick) / (float)Math.Max(1, blurEndTick - coilEndTick);
				float dashEase = Ult3DProjection.EaseOutCubic(u);
				PlayerPos3D = UltCoord3D.Lerp(coilLowPos, skyApexPos, dashEase);
				VisualDrawDirection = -1;
				PlayerLeanShear = MathHelper.Lerp(0.10f, -0.38f, dashEase);
				PlayerBodyTilt = -0.22f * MathF.Sin(dashEase * MathHelper.PiOver2);
				PlayerWidthScale = 1.05f;
				PlayerSquashY = MathHelper.Lerp(0.82f, 1.06f, dashEase);
				ApplyDashStretch(prevPos, PlayerPos3D, 1.65f * MathF.Sin(MathHelper.Clamp(u, 0.1f, 0.95f) * MathHelper.Pi));
				EmitDashEchoes(prevPos, PlayerPos3D, count: 3, starkLead: true, initialAlpha: 0.68f);
				EmitWaterSprayAlongPath(prevPos, PlayerPos3D, 3);

				if (!realRibbonSpawned)
				{
					realRibbonSpawned = true;
					ActiveRibbons.Add(new UltSlashRibbon3D
					{
						Center3D = Screen3D(8f, 24f, -65f),
						AxisU3D = Screen3D(0.60f, -0.68f, 0.42f),
						AxisV3D = Screen3D(-0.55f, 0.56f, -0.62f),
						StartAngle = MathHelper.ToRadians(-25f),
						EndAngle = MathHelper.ToRadians(125f),
						RadiusStart = 215f,
						RadiusEnd = 265f,
						WidthStart = 115f,
						WidthEnd = 68f,
						ZBiasEnd = -230f,
						Thick = 0.64f,
						Seed = 0.67f,
						Age = 0,
						MaxAge = 28,
						SweepFrames = Math.Max(2, blurEndTick - coilEndTick),
						Opacity = 1.0f,
						RazorWiden = 0.48f,
						IsPreemptive = false,
						IsResurge = false,
						DepthDimBias = 0.12f
					});
					SpawnWaterRipple(new UltCoord3D(coilLowPos.F, WaterPlaneU, coilLowPos.Z), 175f, 30, 0.80f, doubleRing: true);
				}

				float whipT = MathHelper.Clamp(u * 1.15f - 0.10f, 0f, 1f);
				UltCoord3D bladeDir = UltCoord3D.Lerp(Screen3D(0.75f, -0.45f, 0.48f), Screen3D(-0.62f, 0.68f, -0.38f), Ult3DProjection.EaseOutCubic(whipT))
					.SafeNormalize(Screen3D(-0.6f, 0.7f, -0.4f));
				SetBladeFromDirection(PlayerPos3D + Screen3D(-6f, 6f, -8f), bladeDir, BaseBladeLength3D * 1.08f, Screen3D(0.6f, 0.4f, 0.6f));
				BladeGlowIntensity = 1.08f;
			}
			else
			{
				// Sub-phase 4: Held Upper-Left Zanshin end-pose storing gravity & rotational torque for Rainblade 3
				float z = (swingTimer - blurEndTick) / (float)Math.Max(1, swingDuration - blurEndTick);
				float settle = Ult3DProjection.EaseOutCubic(z);
				PlayerPos3D = UltCoord3D.Lerp(skyApexPos, skyZanshinPos, settle);
				VisualDrawDirection = -1;
				PlayerLeanShear = MathHelper.Lerp(-0.38f, -0.22f, settle);
				PlayerBodyTilt = MathHelper.Lerp(-0.20f, -0.10f, settle);
				PlayerWidthScale = 1f;
				PlayerSquashY = MathHelper.Lerp(1.04f, 0.96f, settle);
				PlayerStretchAmount = 0f;

				// Odachi angled high behind the opposite shoulder, ready to plunge downward in Stage 2 -> 3
				UltCoord3D poisedHighDir = UltCoord3D.Lerp(Screen3D(-0.62f, 0.68f, -0.38f), Screen3D(0.55f, 0.74f, -0.38f), settle)
					.SafeNormalize(Screen3D(0.55f, 0.74f, -0.38f));
				SetBladeFromDirection(PlayerPos3D + Screen3D(4f, 10f, -6f), poisedHighDir, BaseBladeLength3D, Screen3D(0.2f, 0.4f, 0.85f));
				BladeGlowIntensity = MathHelper.Lerp(0.85f, 0.42f, settle);
			}

			PlayerVel3D = PlayerPos3D - prevPos;
		}

		private void EvaluateRainblade3(UltCoord3D prevPos)
		{
			// Chains directly from Upper-Left (+U, -Z) end-pose of Rainblade 2 with ZERO preemptive slash!
			UltCoord3D coilHighPos = Screen3D(-20f, 144f, -296f);
			UltCoord3D crashLowPos = Screen3D(42f, -78f, 178f);
			UltCoord3D zanshinLowPos = Screen3D(44f, -82f, 174f);

			if (swingTimer < coilEndTick)
			{
				// Sub-phase 1: Tight torque spin/step-through high in Upper-Left (+U, -Z)
				float c = swingTimer / (float)Math.Max(1, coilEndTick);
				float easeC = Ult3DProjection.EaseOutCubic(c);
				PlayerPos3D = UltCoord3D.Lerp(stageStartPos3D, coilHighPos, easeC);
				VisualDrawDirection = 1;
				PlayerLeanShear = MathHelper.Lerp(stageStartLean, -0.24f, easeC);
				PlayerBodyTilt = MathHelper.Lerp(-0.10f, 0.14f, easeC);
				PlayerWidthScale = 1f;
				PlayerSquashY = MathHelper.Lerp(0.96f, 0.88f, easeC);
				PlayerStretchAmount = 0f;
				BladeGlowIntensity = 0.60f + 0.30f * easeC;

				UltCoord3D windDir = UltCoord3D.Lerp(Screen3D(0.55f, 0.74f, -0.38f), Screen3D(-0.55f, 0.78f, -0.28f), easeC)
					.SafeNormalize(Screen3D(-0.5f, 0.8f, -0.3f));
				SetBladeFromDirection(PlayerPos3D + Screen3D(-4f, 10f, -4f), windDir, BaseBladeLength3D, Screen3D(0.3f, 0.2f, 0.9f));
			}
			else if (swingTimer <= blurEndTick)
			{
				// Sub-phase 2: Blinding plunging dash-blur from Upper-Left (+U, -Z) crashing down-outward to Bottom-Right (-U, +Z)
				float u = (swingTimer - coilEndTick) / (float)Math.Max(1, blurEndTick - coilEndTick);
				float dashEase = Ult3DProjection.EaseOutCubic(u);
				PlayerPos3D = UltCoord3D.Lerp(coilHighPos, crashLowPos, dashEase);
				VisualDrawDirection = 1;
				PlayerLeanShear = MathHelper.Lerp(-0.20f, 0.50f, dashEase);
				PlayerBodyTilt = 0.24f * MathF.Sin(dashEase * MathHelper.PiOver2);
				PlayerWidthScale = 1.12f;
				PlayerSquashY = MathHelper.Lerp(0.92f, 0.82f, dashEase);
				ApplyDashStretch(prevPos, PlayerPos3D, 1.75f * MathF.Sin(MathHelper.Clamp(u, 0.1f, 0.95f) * MathHelper.Pi));
				EmitDashEchoes(prevPos, PlayerPos3D, count: 4, starkLead: true, initialAlpha: 0.74f);
				EmitWaterSprayAlongPath(prevPos, PlayerPos3D, 4);

				if (!realRibbonSpawned)
				{
					realRibbonSpawned = true;
					ActiveRibbons.Add(new UltSlashRibbon3D
					{
						Center3D = Screen3D(12f, 20f, -40f),
						AxisU3D = Screen3D(-0.52f, 0.65f, -0.55f),
						AxisV3D = Screen3D(0.68f, -0.55f, 0.48f),
						StartAngle = MathHelper.ToRadians(-10f),
						EndAngle = MathHelper.ToRadians(142f),
						RadiusStart = 200f,
						RadiusEnd = 275f,
						WidthStart = 72f,
						WidthEnd = 152f,
						ZBiasEnd = 225f,
						Thick = 0.68f,
						Seed = 0.81f,
						Age = 0,
						MaxAge = 32,
						SweepFrames = Math.Max(2, blurEndTick - coilEndTick),
						Opacity = 1.0f,
						RazorWiden = 0.55f,
						IsPreemptive = false,
						IsResurge = false,
						DepthDimBias = 0f
					});
					SpawnWaterRipple(new UltCoord3D(crashLowPos.F, WaterPlaneU, crashLowPos.Z), 220f, 36, 0.92f, doubleRing: true);
				}

				float whipT = MathHelper.Clamp(u * 1.18f - 0.12f, 0f, 1f);
				UltCoord3D bladeDir = UltCoord3D.Lerp(Screen3D(-0.55f, 0.78f, -0.28f), Screen3D(0.84f, -0.42f, 0.34f), Ult3DProjection.EaseOutCubic(whipT))
					.SafeNormalize(Screen3D(0.8f, -0.4f, 0.35f));
				SetBladeFromDirection(PlayerPos3D + Screen3D(12f, -6f, 14f), bladeDir, BaseBladeLength3D * 1.12f, Screen3D(0.35f, 0.82f, -0.45f));
				BladeGlowIntensity = 1.18f;
			}
			else
			{
				// Sub-phase 3: Hardest Rainblade hitstop & lower/wider crouched Zanshin in Bottom-Right foreground
				float z = (swingTimer - blurEndTick) / (float)Math.Max(1, swingDuration - blurEndTick);
				float settle = Ult3DProjection.EaseOutCubic(z);
				PlayerPos3D = UltCoord3D.Lerp(crashLowPos, zanshinLowPos, settle);
				VisualDrawDirection = 1;
				PlayerLeanShear = MathHelper.Lerp(0.50f, 0.36f, settle);
				PlayerBodyTilt = MathHelper.Lerp(0.20f, 0.08f, settle);
				PlayerWidthScale = 1.06f;
				PlayerSquashY = MathHelper.Lerp(0.80f, 0.78f, settle);
				PlayerStretchAmount = 0f;

				UltCoord3D extDir = Screen3D(0.86f, -0.36f, 0.35f).SafeNormalize(Screen3D(0.86f, -0.36f, 0.35f));
				SetBladeFromDirection(PlayerPos3D + Screen3D(12f, -6f, 12f), extDir, BaseBladeLength3D * 1.05f, Screen3D(0.35f, 0.82f, -0.45f));
				BladeGlowIntensity = MathHelper.Lerp(0.95f, 0.45f, settle);
			}

			PlayerVel3D = PlayerPos3D - prevPos;
		}

		private void EvaluateStygianResurge(UltCoord3D prevPos)
		{
			UltCoord3D windupPoisedPos = Screen3D(36f, -10f, 166f);
			UltCoord3D flashEndLeftPos = Screen3D(-34f, 12f, -328f);
			UltCoord3D finalSettleLeftPos = Screen3D(-36f, 12f, -330f);

			if (swingTimer < coilEndTick)
			{
				// Sub-phase 1: Calm Lethal Windup in Foreground Right (+Z)
				float c = swingTimer / (float)Math.Max(1, coilEndTick);
				float riseEase = Ult3DProjection.EaseOutCubic(MathHelper.Clamp(c * 1.4f, 0f, 1f));
				PlayerPos3D = UltCoord3D.Lerp(stageStartPos3D, windupPoisedPos, riseEase);
				VisualDrawDirection = -1;
				PlayerLeanShear = MathHelper.Lerp(stageStartLean, 0.08f, riseEase);
				PlayerBodyTilt = MathHelper.Lerp(0.08f, 0f, riseEase);
				PlayerWidthScale = 1f;
				PlayerSquashY = MathHelper.Lerp(0.78f, 1.0f, riseEase);
				PlayerStretchAmount = 0f;

				// Draw Naught cleanly back across body while crimson energy condenses to a razor-thin glow
				UltCoord3D drawBackDir = UltCoord3D.Lerp(Screen3D(0.75f, -0.32f, 0.35f), Screen3D(0.88f, -0.18f, 0.42f), riseEase)
					.SafeNormalize(Screen3D(0.88f, -0.18f, 0.42f));
				SetBladeFromDirection(PlayerPos3D + Screen3D(6f, -2f, 10f), drawBackDir, BaseBladeLength3D * 1.04f, Screen3D(0.15f, 0.92f, -0.35f));
				BladeGlowIntensity = 0.55f + 0.85f * c;

				if (swingTimer == 2)
				{
					SpawnWaterRipple(new UltCoord3D(windupPoisedPos.F, WaterPlaneU, windupPoisedPos.Z), 120f, 34, 0.45f, doubleRing: false);
				}

				if (!Main.dedServ && swingTimer % 2 == 0)
				{
					Vector2 tipWorld = Ult3DProjection.Project(Tip3D, SafeFrameOrigin, LockedFacingDir, out _);
					Vector2 hiltWorld = Ult3DProjection.Project(Hilt3D, SafeFrameOrigin, LockedFacingDir, out _);
					Vector2 edgePos = Vector2.Lerp(hiltWorld, tipWorld, Main.rand.NextFloat());
					PRTLoader.NewParticle<VoidSparkPRT>(edgePos + Main.rand.NextVector2Circular(12f, 12f), (hiltWorld - edgePos).SafeNormalize(Vector2.Zero) * 1.8f, new Color(255, 65, 95), 0.65f);
				}

				// Sharp razor condensation gleam right before the instantaneous flash-step
				if (c >= 0.70f)
				{
					Vector2 hiltWorld = Ult3DProjection.Project(Hilt3D, SafeFrameOrigin, LockedFacingDir, out float persp);
					Vector2 tipWorld = Ult3DProjection.Project(Tip3D, SafeFrameOrigin, LockedFacingDir, out _);
					float gProg = (c - 0.70f) / 0.30f;
					float gAlpha = MathF.Sin(gProg * MathHelper.Pi);
					ActiveGleams.Add(new UltGleamFlash3D
					{
						WorldPos = Vector2.Lerp(hiltWorld, tipWorld, 0.35f),
						AxisAngle = (tipWorld - hiltWorld).ToRotation(),
						Alpha = gAlpha * 0.95f,
						Scale = persp * 0.85f
					});
					if (!resurgeWindupFlashSpawned)
					{
						resurgeWindupFlashSpawned = true;
						SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.65f, Volume = 0.65f }, Owner.Center);
					}
				}
			}
			else if (swingTimer <= blurEndTick)
			{
				// Sub-phase 2: Instantaneous 2-frame Flash-Step Smear from Right (+Z) all the way to Left (-Z)
				float u = (swingTimer - coilEndTick) / (float)Math.Max(1, blurEndTick - coilEndTick);
				float dashEase = Ult3DProjection.EaseOutCubic(u);
				PlayerPos3D = UltCoord3D.Lerp(windupPoisedPos, flashEndLeftPos, dashEase);
				VisualDrawDirection = -1;
				PlayerLeanShear = -0.52f;
				PlayerBodyTilt = -0.14f;
				PlayerWidthScale = 1.0f;
				PlayerSquashY = 0.92f;
				ApplyDashStretch(prevPos, PlayerPos3D, 2.45f);
				EmitDashEchoes(prevPos, PlayerPos3D, count: 5, starkLead: true, initialAlpha: 0.85f);
				EmitWaterSprayAlongPath(prevPos, PlayerPos3D, 6);

				if (!realRibbonSpawned)
				{
					realRibbonSpawned = true;
					ActiveRibbons.Add(new UltSlashRibbon3D
					{
						Center3D = Screen3D(0f, 0f, -60f),
						AxisU3D = Screen3D(0.90f, 0.02f, 0.44f),
						AxisV3D = Screen3D(-0.44f, 0.05f, 0.90f),
						StartAngle = MathHelper.ToRadians(25f),
						EndAngle = MathHelper.ToRadians(185f),
						RadiusStart = 285f,
						RadiusEnd = 335f,
						WidthStart = 175f,
						WidthEnd = 105f,
						ZBiasEnd = -280f,
						Thick = 0.72f,
						Seed = 0.95f,
						Age = 0,
						MaxAge = 36,
						SweepFrames = 2,
						Opacity = 1.0f,
						RazorWiden = 0.60f,
						IsPreemptive = false,
						IsResurge = true,
						DepthDimBias = 0f
					});
					SpawnWaterRipple(new UltCoord3D(windupPoisedPos.F, WaterPlaneU, windupPoisedPos.Z), 230f, 36, 0.90f, doubleRing: true);
					SpawnWaterRipple(new UltCoord3D(flashEndLeftPos.F, WaterPlaneU, flashEndLeftPos.Z), 190f, 34, 0.75f, doubleRing: true);
				}

				UltCoord3D cleaveBladeDir = UltCoord3D.Lerp(Screen3D(0.88f, -0.18f, 0.42f), Screen3D(-0.92f, 0.06f, -0.38f), dashEase)
					.SafeNormalize(Screen3D(-0.92f, 0.06f, -0.38f));
				SetBladeFromDirection(PlayerPos3D + Screen3D(-8f, 0f, -6f), cleaveBladeDir, BaseBladeLength3D * 1.15f, Screen3D(0f, 1f, 0.15f));
				BladeGlowIntensity = 1.35f;
			}
			else
			{
				// Sub-phase 3: Composed follow-through in deep -Z Left
				float z = (swingTimer - blurEndTick) / (float)Math.Max(1, swingDuration - blurEndTick);
				float settle = Ult3DProjection.EaseOutCubic(z);
				PlayerPos3D = UltCoord3D.Lerp(flashEndLeftPos, finalSettleLeftPos, settle);
				VisualDrawDirection = -1;
				PlayerLeanShear = MathHelper.Lerp(-0.46f, -0.18f, settle);
				PlayerBodyTilt = MathHelper.Lerp(-0.12f, 0f, settle);
				PlayerWidthScale = 1f;
				PlayerSquashY = MathHelper.Lerp(0.90f, 0.94f, settle);
				PlayerStretchAmount = 0f;

				UltCoord3D followDir = Screen3D(-0.88f, -0.22f, -0.42f).SafeNormalize(Screen3D(-0.88f, -0.22f, -0.42f));
				SetBladeFromDirection(PlayerPos3D + Screen3D(-6f, -4f, -6f), followDir, BaseBladeLength3D, Screen3D(0f, 0.9f, 0.35f));
				BladeGlowIntensity = MathHelper.Lerp(1.0f, 0.40f, settle);
			}

			PlayerVel3D = PlayerPos3D - prevPos;
		}

		private void SetBladeFromDirection(UltCoord3D hilt, UltCoord3D dir3D, float length, UltCoord3D broadsideHint)
		{
			Hilt3D = hilt;
			Tip3D = hilt + dir3D * length;
			BladeBroadsideNormal3D = broadsideHint;
		}

		private void ApplyDashStretch(UltCoord3D fromPos, UltCoord3D toPos, float amount)
		{
			Vector2 p0 = Ult3DProjection.Project(fromPos, SafeFrameOrigin, LockedFacingDir, out _);
			Vector2 p1 = Ult3DProjection.Project(toPos, SafeFrameOrigin, LockedFacingDir, out _);
			Vector2 delta = p1 - p0;
			PlayerStretchDir2D = delta.LengthSquared() > 1f ? Vector2.Normalize(delta) : new Vector2(VisualDrawDirection, 0f);
			PlayerStretchAmount = Math.Max(0f, amount);
		}

		private void EmitDashEchoes(UltCoord3D fromPos, UltCoord3D toPos, int count, bool starkLead, float initialAlpha)
		{
			if (count <= 0)
				return;

			for (int i = 0; i < count; i++)
			{
				float t = i / (float)count;
				UltCoord3D samplePos = UltCoord3D.Lerp(fromPos, toPos, t);
				Afterimages.Add(new UltAfterimage3D
				{
					Pos3D = samplePos,
					LeanShear = PlayerLeanShear * MathHelper.Lerp(0.75f, 1.05f, t),
					BodyTilt = PlayerBodyTilt,
					WidthScale = PlayerWidthScale,
					SquashY = PlayerSquashY,
					StretchDir2D = PlayerStretchDir2D,
					StretchAmount = PlayerStretchAmount * MathHelper.Lerp(0.55f, 1.0f, t),
					DrawDirection = VisualDrawDirection,
					Age = 0,
					MaxAge = starkLead ? 15 : 11,
					InitialAlpha = initialAlpha * MathHelper.Lerp(0.55f, 1.0f, t),
					StarkWhiteLead = starkLead && i >= count - 2
				});
			}
		}

		private void EmitWaterSprayAlongPath(UltCoord3D fromPos, UltCoord3D toPos, int sprayCount)
		{
			if (Main.dedServ)
				return;

			Vector2 p0 = Ult3DProjection.Project(fromPos, SafeFrameOrigin, LockedFacingDir, out _);
			Vector2 p1 = Ult3DProjection.Project(toPos, SafeFrameOrigin, LockedFacingDir, out _);
			Vector2 velDir = (p1 - p0).SafeNormalize(new Vector2(VisualDrawDirection, 0f));

			for (int i = 0; i < sprayCount; i++)
			{
				float t = Main.rand.NextFloat();
				Vector2 pos = Vector2.Lerp(p0, p1, t) + Main.rand.NextVector2Circular(18f, 18f);
				Vector2 vel = velDir * Main.rand.NextFloat(3.5f, 9f) + new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, 1f));
				if (i % 2 == 0)
					PRTLoader.NewParticle<VoidSparkPRT>(pos, vel, new Color(255, 45, 80), Main.rand.NextFloat(0.7f, 1.25f));
				else
					PRTLoader.NewParticle<CrimsonPetalPRT>(pos, vel * 0.7f, new Color(245, 30, 55, 230), Main.rand.NextFloat(0.7f, 1.15f));
			}
		}

		private void SpawnWaterRipple(UltCoord3D center3D, float maxRadius, int maxAge, float opacity, bool doubleRing)
		{
			WaterRipples.Add(new UltWaterRipple3D
			{
				Center3D = center3D,
				MaxRadius = maxRadius,
				Age = 0,
				MaxAge = maxAge,
				Opacity = opacity,
				DoubleRing = doubleRing
			});
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
			float expScale = (isResurge ? 1.8f : 1.25f) * Main.rand.NextFloat(0.95f, 1.30f);
			PRTLoader.NewParticle<DimensionalExplosionPRT>(impactPos, Vector2.Zero, new Color(255, 45, 75), expScale);

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

		private void SpawnPreemptiveImpactVisuals(Vector2 impactPos, float angle)
		{
			if (Main.dedServ)
				return;

			Vector2 cutDir = angle.ToRotationVector2();
			for (int i = 0; i < 4; i++)
			{
				Vector2 sparkVel = cutDir * Main.rand.NextFloat(5.5f, 10.5f) + Main.rand.NextVector2Circular(2.5f, 2.5f);
				Color sparkCol = i % 2 == 0 ? new Color(255, 65, 95) : new Color(255, 200, 215);
				PRTLoader.NewParticle<VoidSparkPRT>(impactPos + Main.rand.NextVector2Circular(10f, 10f), sparkVel, sparkCol, Main.rand.NextFloat(0.65f, 1.05f));
			}

			for (int i = 0; i < 2; i++)
			{
				Vector2 petalVel = cutDir * Main.rand.NextFloat(3.5f, 6.5f) + Main.rand.NextVector2Circular(2f, 2f);
				PRTLoader.NewParticle<CrimsonPetalPRT>(impactPos, petalVel, new Color(245, 35, 70, 220), Main.rand.NextFloat(0.65f, 0.95f));
			}

			for (int d = 0; d < 5; d++)
			{
				Vector2 dustVel = cutDir * Main.rand.NextFloat(4f, 8f) + Main.rand.NextVector2Circular(2f, 2f);
				Dust dust = Dust.NewDustPerfect(impactPos, DustID.CrimsonTorch, dustVel, 100, default, 1.15f);
				dust.noGravity = true;
			}
		}

		private void ExecutePreemptiveRainblade(int preIndex, float angle)
		{
			SoundEngine.PlaySound(WeepSound.KatanaHit with { Volume = 0.72f, Pitch = 0.22f + preIndex * 0.08f }, Owner.Center);
			SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.50f, Volume = 0.55f }, Owner.Center);

			var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
			if (naughtPlayer != null && Owner.whoAmI == Main.myPlayer && !Main.dedServ)
			{
				// Subtle ~30% directional recoil along the forward-depth cut vector (zero random shake)
				Vector2 preRecoil = new(LockedFacingDir * 4.5f, preIndex == 2 ? 4f : -1.5f);
				naughtPlayer.AddUltCameraRecoil(preRecoil);
				naughtPlayer.PunchUltCameraZoom(-0.08f);
			}

			NihilityDomainSystem.AnomalyPulse = Math.Max(NihilityDomainSystem.AnomalyPulse, 0.36f);
			hitstopFrames = 1;

			NPC primaryTarget = FindPrimaryTarget();
			Vector2 forwardAnchor = UltOrigin + new Vector2(LockedFacingDir * 120f, preIndex == 2 ? 28f : -6f);
			Vector2 cutCenter = primaryTarget != null ? primaryTarget.Center : forwardAnchor;

			ActiveSeams.Add(new UltCleaveSeam3D
			{
				WorldCenter = cutCenter,
				Angle = angle,
				HalfLength = Main.screenWidth * 0.42f,
				HalfWidth = 8.5f,
				Seed = 0.13f * preIndex,
				Age = 0,
				MaxAge = 20,
				PersistUntilSnap = false
			});

			if (naughtPlayer != null)
			{
				Projectile.CritChance = naughtPlayer.GetNaughtCritChance();
			}

			NaughtBalanceProfile profile = WeepBalance.Active;
			int baseDamage = Projectile.damage > 0 ? Projectile.damage : (naughtPlayer?.GetNaughtDamage() ?? profile.BaseSwordDamage);
			float combinedBaseMult = (naughtPlayer != null && naughtPlayer.HasEidolon3) ? profile.E3RainbladeBaseMult : profile.RainbladeBaseMult;
			float preBaseMult = combinedBaseMult * 0.25f;
			int preDamage = Math.Max(1, (int)(baseDamage * preBaseMult));
			int enemiesHit = 0;

			// 25% base damage forward slash; consumes 0 Knots and avoids main-hit Ruin/Tide charm procs
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC target = Main.npc[i];
				if (IsValidUltimateTarget(target))
				{
					float dist = Vector2.Distance(Owner.MountedCenter, target.Center);
					if (dist < 1800f)
					{
						if (!target.dontTakeDamage || target.type == NPCID.TargetDummy)
						{
							if (naughtPlayer != null)
							{
								naughtPlayer.StrikeNPC(
									target,
									preDamage,
									LockedFacingDir,
									crit: null,
									knockBack: 2f,
									sourceProj: Projectile,
									isUltimate: true);
							}
							else
							{
								target.SimpleStrikeNPC(preDamage, LockedFacingDir, false, 2f, WeepCompatibility.TrueMelee);
							}
						}

						SpawnPreemptiveImpactVisuals(target.Center, angle);
						enemiesHit++;
					}
				}
			}

			if (enemiesHit == 0)
			{
				SpawnPreemptiveImpactVisuals(cutCenter, angle);
			}
		}

		private void ExecuteRainblade(int index, float angle, float tilt)
		{
			if (index == 1)
				SoundEngine.PlaySound(WeepSound.AcheronSlash1, Owner.Center);
			else if (index == 2)
				SoundEngine.PlaySound(WeepSound.AcheronSlash2, Owner.Center);
			else
				SoundEngine.PlaySound(WeepSound.AcheronSlash3, Owner.Center);

			SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.18f * index, Volume = 0.85f }, Owner.Center);

			var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
			if (naughtPlayer != null && Owner.whoAmI == Main.myPlayer && !Main.dedServ)
			{
				Vector2 recoilVec = index switch
				{
					1 => new Vector2(14f, 2f),
					2 => new Vector2(-12f, -15f),
					_ => new Vector2(16f, 22f)
				};
				naughtPlayer.AddUltCameraRecoil(recoilVec);
				naughtPlayer.PunchUltCameraZoom(index == 3 ? -0.45f : -0.28f);
			}

			NihilityDomainSystem.AnomalyPulse = index == 3 ? 1.0f : 0.90f;
			NihilityDomainSystem.NegativeFlash = index == 3 ? 0.50f : 0.38f;

			// Hardest hitstop of the 3 Rainblades on Rainblade 3
			hitstopFrames = index == 3 ? 5 : 3;

			NPC primaryTarget = FindPrimaryTarget();
			Vector2 lockedCenter = naughtPlayer != null && naughtPlayer.UltCameraLocked
				? naughtPlayer.UltLockedScreenPos + new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f)
				: Main.screenPosition + new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
			Vector2 cutCenter = primaryTarget != null ? primaryTarget.Center : lockedCenter;

			// Crisp shader-rendered dimensional scar across cut angle
			ActiveSeams.Add(new UltCleaveSeam3D
			{
				WorldCenter = cutCenter,
				Angle = angle,
				HalfLength = Main.screenWidth * 0.72f,
				HalfWidth = index == 3 ? 28f : 18f,
				Seed = 0.23f * index,
				Age = 0,
				MaxAge = 44,
				PersistUntilSnap = false
			});

			SpawnImpactVisuals(cutCenter, false);

			if (naughtPlayer != null)
			{
				Projectile.CritChance = naughtPlayer.GetNaughtCritChance();
			}

			NaughtBalanceProfile profile = WeepBalance.Active;
			int baseDamage = Projectile.damage > 0 ? Projectile.damage : (naughtPlayer?.GetNaughtDamage() ?? profile.BaseSwordDamage);
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
						if (detonated > 0)
						{
							TryTriggerDepartedSiphon(naughtPlayer, target.Center, detonated);
						}

						if (naughtPlayer != null && naughtPlayer.HasBladesongRain && detonated > 0)
						{
							int needleDamage = naughtPlayer.GetNaughtDamage(profile.BladesongNeedleMult);
							int needleCrit = naughtPlayer.GetNaughtCritChance();
							int originRootWhoAmI = BloodBlossomNeedle.GetRootNpcIndex(target);
							float encodedOrigin = originRootWhoAmI >= 0 ? originRootWhoAmI + 1f : 0f;
							float baseFanAngle = Main.rand.NextFloat(MathHelper.TwoPi);

							for (int n = 0; n < detonated; n++)
							{
								BloodBlossomNeedle.EnforceCap(Owner.whoAmI);
								float fanAngle = baseFanAngle + MathHelper.TwoPi * (n / (float)detonated) + Main.rand.NextFloat(-0.28f, 0.28f);
								Vector2 needleVel = fanAngle.ToRotationVector2() * Main.rand.NextFloat(12f, 16f);
								float launchUpdates = Main.rand.Next(30, 42);
								int nIdx = Projectile.NewProjectile(
									Owner.GetSource_FromThis(),
									target.Center,
									needleVel,
									ModContent.ProjectileType<BloodBlossomNeedle>(),
									needleDamage,
									2f,
									Owner.whoAmI,
									ai0: encodedOrigin,
									ai1: launchUpdates);
								if (nIdx >= 0 && nIdx < Main.maxProjectiles)
								{
									Main.projectile[nIdx].CritChance = needleCrit;
								}
							}
						}

						// Rainblade 1 & 2 deal 75% base + 100% knot bonus here (paired with 25% preemptive slash); Rainblade 3 deals 100% base + 100% knot bonus
						bool hasE3 = naughtPlayer != null && naughtPlayer.HasEidolon3;
						float combinedBaseMult = hasE3 ? profile.E3RainbladeBaseMult : profile.RainbladeBaseMult;
						bool hasPreSplit = index <= 2;
						float rainbladeBaseMult = hasPreSplit ? combinedBaseMult * 0.75f : combinedBaseMult;
						float rainbladeKnotMult = hasE3 ? profile.E3RainbladeKnotMult : profile.RainbladeKnotMult;
						int strikeDamage = (int)(baseDamage * rainbladeBaseMult + detonated * baseDamage * rainbladeKnotMult);
						if (!target.dontTakeDamage || target.type == NPCID.TargetDummy)
						{
							if (naughtPlayer != null)
							{
								naughtPlayer.StrikeNPC(
									target,
									strikeDamage,
									LockedFacingDir,
									crit: null,
									knockBack: 4f,
									sourceProj: Projectile,
									isUltimate: true);
							}
							else
							{
								target.SimpleStrikeNPC(strikeDamage, LockedFacingDir, false, 4f, WeepCompatibility.TrueMelee);
							}
						}

						NihilityDismember.TriggerGroup(target, target.Center, angle, duration: 240, holdFrames: 14);

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

			if (enemiesHit == 0)
			{
				Vector2 dir = angle.ToRotationVector2();
				Vector2 slashStart = cutCenter - dir * (Main.screenWidth * 0.65f);
				Vector2 slashEnd = cutCenter + dir * (Main.screenWidth * 0.65f);
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
			SoundEngine.PlaySound(WeepSound.AcheronResurge, Owner.Center);
			SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.50f, Volume = 1.0f }, Owner.Center);

			var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
			if (naughtPlayer != null && Owner.whoAmI == Main.myPlayer && !Main.dedServ)
			{
				naughtPlayer.AddUltCameraRecoil(new Vector2(-24f, -4f));
				naughtPlayer.PunchUltCameraZoom(-0.56f, -0.014f);
			}

			NihilityDomainSystem.NegativeFlash = 1.0f;
			NihilityDomainSystem.AnomalyPulse = 1.0f;
			hitstopFrames = 5;

			NPC primaryTarget = FindPrimaryTarget();
			Vector2 lockedCenter = naughtPlayer != null && naughtPlayer.UltCameraLocked
				? naughtPlayer.UltLockedScreenPos + new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f)
				: Main.screenPosition + new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
			Vector2 cleaveCenter = primaryTarget != null ? primaryTarget.Center : lockedCenter;

			// Monumental screen-splitting horizontal cleave seam held until sheathing snap
			ActiveSeams.Add(new UltCleaveSeam3D
			{
				WorldCenter = cleaveCenter,
				Angle = resurgeAngle,
				HalfLength = Main.screenWidth * 0.95f,
				HalfWidth = 52f,
				Seed = 0.91f,
				Age = 0,
				MaxAge = 240,
				PersistUntilSnap = true
			});

			SpawnImpactVisuals(cleaveCenter, true);

			NaughtBalanceProfile profile = WeepBalance.Active;
			int baseDamage = Projectile.damage > 0 ? Projectile.damage : (naughtPlayer?.GetNaughtDamage() ?? profile.BaseSwordDamage);

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

						totalKnotsDetonatedInUlt += remainingKnots;
						if (remainingKnots > 0)
						{
							TryTriggerDepartedSiphon(naughtPlayer, target.Center, remainingKnots);
						}

						bool hasE5 = naughtPlayer != null && naughtPlayer.HasEidolon5;
						bool hasKarma = naughtPlayer != null && naughtPlayer.HasKarmaSever;
						float resurgeBaseMult = hasE5 ? profile.E5ResurgeBaseMult : profile.ResurgeBaseMult;
						float resurgeKnotMult = hasE5 ? profile.E5ResurgeKnotMult : profile.ResurgeKnotMult;
						float resurgeKarmaScale = hasKarma ? profile.KarmaSeverResurgeBonusMult : 1f;
						int resurgeDamage = (int)((baseDamage * resurgeBaseMult + remainingKnots * baseDamage * resurgeKnotMult) * resurgeKarmaScale);

						pendingResurgeHits.Add(new PendingResurgeHit
						{
							NpcWhoAmI = target.whoAmI,
							Damage = resurgeDamage,
							DetonatedKnots = remainingKnots
						});

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

			if (naughtPlayer != null)
			{
				int refundedCharges = naughtPlayer.OverflowDream;
				naughtPlayer.SlashedDream = refundedCharges;
				naughtPlayer.OverflowDream = 0;

				if (refundedCharges > 0)
				{
					SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.35f, Volume = 0.85f }, Owner.Center);
				}

				if (naughtPlayer.HasEmancipatorWill)
				{
					int knotRefund = Math.Min(totalKnotsDetonatedInUlt / 3, profile.EmancipatorMaxDreamRefund);
					if (knotRefund > 0)
					{
						naughtPlayer.AddSlashedDream(knotRefund);
						if (refundedCharges == 0)
						{
							SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.45f, Volume = 0.85f }, Owner.Center);
						}
						if (Owner.whoAmI == Main.myPlayer && !Main.dedServ)
						{
							CombatText.NewText(Owner.Hitbox, new Color(255, 195, 75), $"+{knotRefund} Slashed Dream", dramatic: true);
						}
					}
				}
			}
		}

		private void TryTriggerDepartedSiphon(NaughtPlayer naughtPlayer, Vector2 sourceCenter, int knotsDetonated)
		{
			if (naughtPlayer == null || !naughtPlayer.HasDepartedSiphon || knotsDetonated <= 0)
				return;

			if (!Main.dedServ)
			{
				int trailCount = Math.Clamp(knotsDetonated * 3, 4, 14);
				Vector2 toOwner = Owner.MountedCenter - sourceCenter;
				float dist = toOwner.Length();
				Vector2 dir = dist > 1f ? toOwner / dist : Vector2.Zero;
				for (int i = 0; i < trailCount; i++)
				{
					float t = Main.rand.NextFloat(0.05f, 0.85f);
					Vector2 pos = Vector2.Lerp(sourceCenter, Owner.MountedCenter, t) + Main.rand.NextVector2Circular(14f, 14f);
					Vector2 vel = dir * Main.rand.NextFloat(5f, 10f) + Main.rand.NextVector2Circular(1.5f, 1.5f);
					Color col = i % 3 == 0 ? new Color(255, 95, 130) : new Color(235, 25, 65);
					PRTLoader.NewParticle<VoidSparkPRT>(pos, vel, col, Main.rand.NextFloat(0.65f, 1.05f));
				}

				for (int p = 0; p < Math.Min(knotsDetonated + 1, 5); p++)
				{
					Vector2 pVel = dir * Main.rand.NextFloat(3.5f, 7f) + Main.rand.NextVector2Circular(2f, 2f);
					PRTLoader.NewParticle<CrimsonPetalPRT>(sourceCenter + Main.rand.NextVector2Circular(16f, 16f), pVel, new Color(245, 35, 75, 230), Main.rand.NextFloat(0.65f, 0.95f));
				}
			}

			int cappedKnots = Math.Min(MaxSiphonKnotsPerUlt, totalKnotsDetonatedInUlt);
			int targetTotalHeal = (int)MathF.Round(Owner.statLifeMax2 * (cappedKnots * WeepBalance.Active.SiphonHealPerKnot));
			int healNow = targetTotalHeal - siphonHealedHpInUlt;
			if (healNow > 0)
			{
				siphonHealedHpInUlt += healNow;
				Owner.Heal(healNow);

				if (!Main.dedServ)
				{
					SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.55f, Volume = 0.55f }, Owner.Center);
					for (int i = 0; i < 6; i++)
					{
						Vector2 burstVel = Main.rand.NextVector2Circular(3.2f, 3.2f);
						PRTLoader.NewParticle<VoidSparkPRT>(Owner.MountedCenter + Main.rand.NextVector2Circular(8f, 12f), burstVel, new Color(255, 110, 145), 0.75f);
					}
				}
			}
		}

		private void UpdatePlayerStance()
		{
			int drawDir = VisualDrawDirection >= 0 ? 1 : -1;

			if (Timer <= UnsheathDuration)
			{
				float progress = MathHelper.Clamp(Timer / (float)UnsheathDuration, 0f, 1f);
				float eased = VaultUtils.EaseQuadOut(progress);

				float frontArmAngle = MathHelper.Lerp(-drawDir * 0.28f, -drawDir * 0.58f, eased) * Owner.gravDir;
				Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, frontArmAngle);
				Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, -drawDir * 0.22f * Owner.gravDir);
				return;
			}

			if (isSheathing)
			{
				float p = MathHelper.Clamp(sheathTimer / (float)SheathSlideDuration, 0f, 1f);
				float eased = VaultUtils.EaseQuadOut(p);

				float frontArmAngle = MathHelper.Lerp(-drawDir * 0.58f, -drawDir * 0.28f, eased) * Owner.gravDir;
				Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, frontArmAngle);
				Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, -drawDir * 0.22f * Owner.gravDir);
				return;
			}

			// Compute composite arm rotation dynamically from the projected 2D hilt->tip blade angle!
			Vector2 hilt2D = Ult3DProjection.Project(Hilt3D, SafeFrameOrigin, LockedFacingDir, out _);
			Vector2 tip2D = Ult3DProjection.Project(Tip3D, SafeFrameOrigin, LockedFacingDir, out _);
			float blade2DRot = (tip2D - hilt2D).ToRotation();

			float armAngle;
			if (drawDir > 0)
				armAngle = (blade2DRot - MathHelper.PiOver2) * Owner.gravDir;
			else
				armAngle = (3f * MathHelper.PiOver2 - blade2DRot) * Owner.gravDir;

			Owner.SetCompositeArmFront(true, isSwinging ? Player.CompositeArmStretchAmount.Full : Player.CompositeArmStretchAmount.ThreeQuarters, armAngle);
			Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Quarter, -drawDir * 0.20f * Owner.gravDir);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// 3D Ultimate visuals are rendered in Ult3DRenderHandle (DrawBeforePlayers / DrawAfterPlayers)
			// bracketing Terraria's native player pass with NaughtPlayer.TransformDrawData.
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			hasPendingPreemptiveStrike = false;
			hasPendingStrike = false;
			preemptiveHitDone = false;
			StopUltMusic();
			WorldFreezeSystem.Deactivate("SlashedDreamUltimate");

			if (Owner != null && Owner.TryGetModPlayer<NaughtPlayer>(out var np))
			{
				np.StartUltimateRecastCooldown();
				bool immediateCam = !Owner.active || Owner.dead || Owner.ghost;
				np.ResetUltVisualAndCameraState(immediateCamera: immediateCam);

				if (Owner.active && !Owner.dead && sheathDamageDone && np.HasThunderCore)
				{
					np.ThunderCoreBuffTimer = WeepBalance.Active.ThunderCoreBuffTicks;
				}
			}

			NihilityDomainSystem.TargetMusicSilence = 0f;
			NihilityDomainSystem.ResetReflectionRipple();
			NihilityDismember.SnapBackAll();
			NihilityDismember.ReassembleFactor = 0f;
			pendingResurgeHits.Clear();
			Afterimages.Clear();
			BladeGhosts.Clear();
			WaterRipples.Clear();
			ActiveRibbons.Clear();
			ActiveSeams.Clear();
			ActiveGleams.Clear();

			if (Owner != null && Owner.active && !Owner.dead && !Owner.ghost && totalKnotsDetonatedInUlt > 0)
			{
				var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
				if (naughtPlayer != null && naughtPlayer.HasDepartedSiphon)
				{
					naughtPlayer.SiphonMoveSpeedTimer = 480;
					if (!Main.dedServ && Owner.whoAmI == Main.myPlayer)
					{
						SoundEngine.PlaySound(SoundID.Run with { Pitch = 0.35f, Volume = 0.7f }, Owner.Center);
						for (int i = 0; i < 10; i++)
						{
							float ang = MathHelper.TwoPi * i / 10f;
							Vector2 dir = ang.ToRotationVector2();
							PRTLoader.NewParticle<VoidSparkPRT>(Owner.MountedCenter + dir * 8f, dir * 3.5f, new Color(255, 80, 120), 0.75f);
						}
					}
				}
			}
		}

		private void StartUltMusic()
		{
			if (Main.dedServ)
				return;

			StopUltMusic();
			ultMusicFade = 1f;
			ultMusicSlot = Owner.whoAmI == Main.myPlayer
				? SoundEngine.PlaySound(WeepSound.AcheronUltMusic)
				: SoundEngine.PlaySound(WeepSound.AcheronUltMusic, Owner.Center);
		}

		private void UpdateUltMusic()
		{
			if (Main.dedServ || !ultMusicSlot.IsValid)
				return;

			if (!SoundEngine.TryGetActiveSound(ultMusicSlot, out ActiveSound activeSound))
			{
				ultMusicSlot = SlotId.Invalid;
				return;
			}

			if (isSheathing)
			{
				ultMusicFade = Math.Max(0f, ultMusicFade - (1f / UltMusicFadeOutTicks));
				if (ultMusicFade <= 0f)
				{
					activeSound.Stop();
					ultMusicSlot = SlotId.Invalid;
					return;
				}
				activeSound.Volume = WeepSound.AcheronUltMusic.Volume * ultMusicFade;
			}
		}

		private void StopUltMusic()
		{
			if (!Main.dedServ && ultMusicSlot.IsValid && SoundEngine.TryGetActiveSound(ultMusicSlot, out ActiveSound activeSound))
			{
				activeSound.Stop();
			}
			ultMusicSlot = SlotId.Invalid;
		}
	}
}
