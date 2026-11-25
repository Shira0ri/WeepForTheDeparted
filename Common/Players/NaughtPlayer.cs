using System;
using System.Collections.Generic;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using WeepForTheDeparted.Common.Configs;
using WeepForTheDeparted.Common.GlobalNPCs;
using WeepForTheDeparted.Common.Systems;
using WeepForTheDeparted.Common.UI.NihilityLedger;
using WeepForTheDeparted.Common.Visuals;
using WeepForTheDeparted.Content.Buffs;
using WeepForTheDeparted.Content.Items.Weapons;
using WeepForTheDeparted.Content.Projectiles;
using WeepForTheDeparted.Content.Projectiles.Charms;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Common.Players
{
	public class NaughtPlayer : ModPlayer
	{
		public int DashCooldown = 0;

		// Slashed Dream Resource (0 to 9, max 9)
		public int SlashedDream = 0;
		public const int MaxSlashedDream = 9;

		// Basekit Overflow Dream points (up to 3 base, 5 with E5, refunded after Ultimate concludes)
		public int OverflowDream = 0;
		public const int BaseMaxOverflowDream = 3;
		public const int E5MaxOverflowDream = 5;
		public int CurrentMaxOverflowDream => HasEidolon5 ? E5MaxOverflowDream : BaseMaxOverflowDream;

		// Nihility Ledger: 3 Charm Sockets (0: Soul, 1: Tide, 2: Ruin)
		public string[] EquippedCharms = new string[3] { "RedOni", "AbyssGrip", "ThunderCore" };

		// Nihility Ledger: 7 Eidolons (E1..E6 + EX). Newly unlocked Eidolons default to active.
		public const int EidolonCount = 7;
		public bool[] EnabledEidolons = new bool[7] { true, true, true, true, true, true, true };

		public bool IsEidolonUnlocked(int index) =>
			index >= 0 && index < EidolonCount && BossTrackerSystem.IsEidolonUnlocked(index);

		public bool IsEidolonActive(int index) =>
			IsEidolonUnlocked(index) && EnabledEidolons != null && index < EnabledEidolons.Length && EnabledEidolons[index];

		public bool HasEidolon1 => IsEidolonActive(0);
		public bool HasEidolon2 => IsEidolonActive(1);
		public bool HasEidolon3 => IsEidolonActive(2);
		public bool HasEidolon4 => IsEidolonActive(3);
		public bool HasEidolon5 => IsEidolonActive(4);
		public bool HasEidolon6 => IsEidolonActive(5);
		public bool HasEidolonEx => IsEidolonActive(6);

		public float GetOdachiReachMultiplier()
		{
			float mult = 1f;
			if (HasEidolon3) mult += 0.15f;
			if (HasEidolonEx) mult += 0.15f;
			return mult;
		}

		// Charm helper accessors (world-gated non-destructively via BossTrackerSystem)
		public bool IsCharmSlotActive(int slot, string key) =>
			EquippedCharms != null && slot >= 0 && slot < EquippedCharms.Length
			&& EquippedCharms[slot] == key && BossTrackerSystem.IsCharmUnlocked(key);

		public bool HasCharm(string key)
		{
			if (EquippedCharms == null || string.IsNullOrEmpty(key)) return false;
			for (int i = 0; i < EquippedCharms.Length; i++)
			{
				if (EquippedCharms[i] == key && BossTrackerSystem.IsCharmUnlocked(key)) return true;
			}
			return false;
		}

		public bool HasSoulCharm(string key) => IsCharmSlotActive(0, key);
		public bool HasTideCharm(string key) => IsCharmSlotActive(1, key);
		public bool HasRuinCharm(string key) => IsCharmSlotActive(2, key);

		// Slot 0 (Soul) Charms
		public bool HasRedOniTrace => HasSoulCharm("RedOni");
		public bool HasNamelessDecoy => HasSoulCharm("NamelessDecoy");
		public bool HasFleetingStep => HasSoulCharm("FleetingStep");
		public bool HasMirageWeft => HasSoulCharm("MirageWeft");
		public bool HasRainleafPhasing => HasSoulCharm("Rainleaf");
		public bool HasStillWater => HasSoulCharm("StillWater");

		// Slot 1 (Tide) Charms
		public bool HasAbyssGrip => HasTideCharm("AbyssGrip");
		public bool HasBloodLotus => HasTideCharm("BloodLotus");
		public bool HasShadowMarionette => HasTideCharm("ShadowMarionette");
		public bool HasWitheredRiver => HasTideCharm("WitheredRiver");
		public bool HasShadowCrucible => HasTideCharm("ShadowCrucible");
		public bool HasDepartedSiphon => HasTideCharm("DepartedSiphon");

		// Slot 2 (Ruin) Charms
		public bool HasThunderCore => HasRuinCharm("ThunderCore");
		public bool HasKarmaSever => HasRuinCharm("KarmaSever");
		public bool HasVoidHorizon => HasRuinCharm("VoidHorizon");
		public bool HasBladesongRain => HasRuinCharm("BladesongRain");
		public bool HasSynchronizedExecution => HasRuinCharm("SynchronizedExecution");
		public bool HasEmancipatorWill => HasRuinCharm("EmancipatorWill");

		// Charm Dynamic Buff Timers & States
		public int VoidPhasingTimer = 0;
		public int VoidPhasingCooldown = 0;
		public const int VoidPhasingDuration = 180; // 3.0 seconds
		public const int VoidPhasingCooldownDuration = 5400; // 90 seconds
		private int prevStatLife = -1;
		public const int StillWaterFocusDuration = 900; // 15.0 seconds
		public int StillWaterFocusTimer = 0;
		public bool IsStillWaterActive => HasStillWater && IsNaughtEquipped && StillWaterFocusTimer >= StillWaterFocusDuration;
		public int ThunderCoreBuffTimer = 0;
		public bool IsThunderCoreActive => ThunderCoreBuffTimer > 0;
		public const int ThunderBoltTotalCount = 9;
		public const int ThunderBoltIntervalTicks = 18; // 0.3s
		private int pendingThunderBolts = 0;
		private int thunderBoltCooldown = 0;
		private int thunderPrimaryTargetWhoAmI = -1;
		private Vector2 thunderFallbackOrigin = Vector2.Zero;
		private bool thunderBarrageDebuffRewardClaimed = false;
		public int EmancipatorCritBuffTimer = 0;
		public bool IsEmancipatorCritActive => EmancipatorCritBuffTimer > 0;
		public int SiphonMoveSpeedTimer = 0;
		public bool IsSiphonMoveSpeedActive => SiphonMoveSpeedTimer > 0;
		public int FleetingStepDreamCooldown = 0;
		public int FleetingStepSlowTimer = 0;
		public bool IsFleetingStepSlowActive => HasFleetingStep && FleetingStepSlowTimer > 0;

		private uint nextCloneSequence = 0;
		public uint AllocateCloneSequence() => ++nextCloneSequence;

		// Ultimate 3D choreography damped cinematic camera, dynamic zoom, & offscreen warped player state
		public bool UltCameraLocked { get; private set; }
		public Vector2 UltLockedScreenPos => ClampScreenPosition(ultCamCurrentScreenPos);
		public Vector2 UltBaseAnchorScreenPos => ultBaseAnchorScreenPos;
		public Vector2 UltCameraVelocity => ultCamVelocity;
		public Vector2 UltCameraRecoil { get; private set; }
		public float UltCameraZoom { get; private set; } = 1f;
		public float UltCameraVignetteIntensity { get; private set; }

		private Vector2 ultBaseAnchorScreenPos;
		private Vector2 ultCamCurrentScreenPos;
		private Vector2 ultCamDesiredScreenPos;
		private Vector2 ultCamVelocity;
		private float ultCamStiffness = 7.5f;
		private float ultCamDesiredZoom = 1f;
		private float ultCamZoomVel = 0f;
		private float ultCamZoomStiffness = 6.5f;
		private float ultCamDesiredVignette = 0f;
		private Vector2 ultReleaseStartScreenPos;
		private float ultReleaseStartZoom = 1f;
		private float ultReleaseStartVignette = 0f;
		private int ultCameraReleaseTicks = 0;
		private int ultCameraReleaseDuration = 18;
		private ulong lastUltCameraTick = ulong.MaxValue;

		public bool IsUltVisualActive { get; set; }
		public int ActiveUltProjIndex { get; set; } = -1;

		internal Content.Projectiles.SlashedDreamUltimate FindActiveUltimate()
		{
			if (ActiveUltProjIndex >= 0 && ActiveUltProjIndex < Main.maxProjectiles)
			{
				Projectile cached = Main.projectile[ActiveUltProjIndex];
				if (cached.active && cached.ModProjectile is Content.Projectiles.SlashedDreamUltimate cachedUlt && cachedUlt.Owner == Player)
					return cachedUlt;
			}

			int ultType = ModContent.ProjectileType<Content.Projectiles.SlashedDreamUltimate>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.type == ultType && p.ModProjectile is Content.Projectiles.SlashedDreamUltimate scannedUlt && scannedUlt.Owner == Player)
				{
					ActiveUltProjIndex = i;
					return scannedUlt;
				}
			}

			return null;
		}

		public static Vector2 ClampScreenPosition(Vector2 pos)
		{
			float maxX = Math.Max(0f, Main.maxTilesX * 16f - Main.screenWidth);
			float maxY = Math.Max(0f, Main.maxTilesY * 16f - Main.screenHeight);
			return new Vector2(MathHelper.Clamp(pos.X, 0f, maxX), MathHelper.Clamp(pos.Y, 0f, maxY));
		}

		public void BeginUltCamera(Vector2 initialScreenPos, Vector2 baseAnchorScreenPos)
		{
			Ult3DRenderHandle.ResetSessionHealth();
			UltCameraLocked = true;
			ultBaseAnchorScreenPos = ClampScreenPosition(baseAnchorScreenPos);
			ultCamCurrentScreenPos = ClampScreenPosition(initialScreenPos);
			ultCamDesiredScreenPos = ultBaseAnchorScreenPos;
			ultCamVelocity = Vector2.Zero;
			ultCamStiffness = 6.2f;
			UltCameraRecoil = Vector2.Zero;
			UltCameraZoom = 1f;
			ultCamDesiredZoom = 1.04f;
			ultCamZoomVel = 0f;
			ultCamZoomStiffness = 5.5f;
			UltCameraVignetteIntensity = 0f;
			ultCamDesiredVignette = 0.28f;
			ultCameraReleaseTicks = 0;
			lastUltCameraTick = Main.GameUpdateCount;
		}

		public void SetUltCameraTarget(
			Vector2 desiredScreenPos,
			float posStiffness,
			float desiredZoom,
			float zoomStiffness,
			float desiredVignette)
		{
			ultCamDesiredScreenPos = ClampScreenPosition(desiredScreenPos);
			ultCamStiffness = MathHelper.Clamp(posStiffness, 2.5f, 16f);
			ultCamDesiredZoom = MathHelper.Clamp(desiredZoom, 0.95f, 1.09f);
			ultCamZoomStiffness = MathHelper.Clamp(zoomStiffness, 2.5f, 16f);
			ultCamDesiredVignette = MathHelper.Clamp(desiredVignette, 0f, 1f);
		}

		public void UpdateUltBaseAnchor(Vector2 baseAnchorScreenPos)
		{
			ultBaseAnchorScreenPos = ClampScreenPosition(baseAnchorScreenPos);
		}

		public void PunchUltCameraZoom(float zoomVelocityKick, float immediateDelta = 0f)
		{
			UltCameraZoom = MathHelper.Clamp(UltCameraZoom + immediateDelta, 0.94f, 1.10f);
			ultCamZoomVel += zoomVelocityKick;
		}

		public void LockUltCamera(Vector2 screenPos)
		{
			BeginUltCamera(screenPos, screenPos);
		}

		public void AddUltCameraRecoil(Vector2 impulse)
		{
			UltCameraRecoil += impulse;
			if (UltCameraRecoil.LengthSquared() > 34f * 34f)
			{
				UltCameraRecoil = Vector2.Normalize(UltCameraRecoil) * 34f;
			}
		}

		private static Vector2 StepCriticallyDampedSpring(Vector2 current, ref Vector2 velocity, Vector2 target, float omega, float dt)
		{
			float f = 1f + 2f * dt * omega;
			float oo = omega * omega;
			float hoo = dt * oo;
			float hhoo = dt * hoo;
			float detInv = 1f / (f + hhoo);
			Vector2 nextPos = (f * current + dt * velocity + hhoo * target) * detInv;
			velocity = (velocity + hoo * (target - current)) * detInv;
			return nextPos;
		}

		private static float StepCriticallyDampedSpring(float current, ref float velocity, float target, float omega, float dt)
		{
			float f = 1f + 2f * dt * omega;
			float oo = omega * omega;
			float hoo = dt * oo;
			float hhoo = dt * hoo;
			float detInv = 1f / (f + hhoo);
			float nextVal = (f * current + dt * velocity + hhoo * target) * detInv;
			velocity = (velocity + hoo * (target - current)) * detInv;
			return nextVal;
		}

		public void ReleaseUltCamera(bool immediate = false)
		{
			if (immediate || !UltCameraLocked)
			{
				UltCameraLocked = false;
				UltCameraRecoil = Vector2.Zero;
				ultCamVelocity = Vector2.Zero;
				ultCamZoomVel = 0f;
				UltCameraZoom = 1f;
				ultCamDesiredZoom = 1f;
				UltCameraVignetteIntensity = 0f;
				ultCamDesiredVignette = 0f;
				ultCameraReleaseTicks = 0;
				return;
			}

			ultReleaseStartScreenPos = ClampScreenPosition(ultCamCurrentScreenPos + UltCameraRecoil);
			ultReleaseStartZoom = UltCameraZoom;
			ultReleaseStartVignette = UltCameraVignetteIntensity;
			UltCameraLocked = false;
			UltCameraRecoil = Vector2.Zero;
			ultCamVelocity = Vector2.Zero;
			ultCamZoomVel = 0f;
			ultCameraReleaseDuration = 18;
			ultCameraReleaseTicks = ultCameraReleaseDuration;
			lastUltCameraTick = Main.GameUpdateCount;
		}

		public void ResetUltVisualAndCameraState(bool immediateCamera = true)
		{
			IsUltVisualActive = false;
			ActiveUltProjIndex = -1;
			ReleaseUltCamera(immediateCamera);
		}

		public static bool IsAnyFleetingStepSlowActive()
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				Player p = Main.LocalPlayer;
				return p != null && p.active && !p.dead && p.TryGetModPlayer<NaughtPlayer>(out var np) && np.IsFleetingStepSlowActive;
			}

			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p != null && p.active && !p.dead && p.TryGetModPlayer<NaughtPlayer>(out var np) && np.IsFleetingStepSlowActive)
					return true;
			}

			return false;
		}

		public delegate void HitModifierCallback(ref NPC.HitModifiers modifiers);

		public bool IsNaughtEquipped => Player.HeldItem?.type == ModContent.ItemType<Naught>();

		/// <summary>
		/// Returns true if the player currently carries Naught in hand, on the cursor, or in their inventory.
		/// Used by passive combat triggers (e.g. Red Oni boss spawn opener) that should fire even while holding a boss summon item.
		/// </summary>
		public bool HasNaughtInInventory()
		{
			int naughtType = ModContent.ItemType<Naught>();

			if (Player.HeldItem != null && !Player.HeldItem.IsAir && Player.HeldItem.type == naughtType)
				return true;

			if (Player.whoAmI == Main.myPlayer && Main.mouseItem != null && !Main.mouseItem.IsAir && Main.mouseItem.type == naughtType)
				return true;

			if (Player.inventory != null)
			{
				for (int i = 0; i < Player.inventory.Length; i++)
				{
					Item inv = Player.inventory[i];
					if (inv != null && !inv.IsAir && inv.type == naughtType)
						return true;
				}
			}

			return false;
		}

		private Item lastKnownNaughtItem;
		private Item fallbackNaughtItem;
		private bool suppressNaughtHitModifiers = false;
		private bool activeStrikeIsUltimate = false;
		private bool isInsideStrikeNPC = false;
		private bool isPropagatingMarionette = false;
		public bool IsPropagatingMarionette => isPropagatingMarionette;

		// E2 (Mute Thunder): passive +1 Slashed Dream every 9.0s while wielding Naught in combat
		public const int E2CombatTimeoutTicks = 360;   // 6.0s
		public const int E2SlashedDreamInterval = 540; // 9.0s
		private int e2CombatActivityTimer = 0;
		private int e2PassiveDreamTimer = 0;

		public const int MarionetteCycleIntervalTicks = 900; // 15 seconds while ShadowMarionette is active
		private int marionetteCycleTimer = 0;

		/// <summary>
		/// Resolves the player's Naught item instance (HeldItem -> cursor/inventory -> cached -> default fallback)
		/// so lingering clones and projectiles preserve prefix and boss-scaling stats even during brief weapon swaps.
		/// </summary>
		public Item ResolveNaughtItem()
		{
			int naughtType = ModContent.ItemType<Naught>();

			if (Player.HeldItem != null && !Player.HeldItem.IsAir && Player.HeldItem.type == naughtType)
			{
				lastKnownNaughtItem = Player.HeldItem;
				return Player.HeldItem;
			}

			if (Player.whoAmI == Main.myPlayer && Main.mouseItem != null && !Main.mouseItem.IsAir && Main.mouseItem.type == naughtType)
			{
				lastKnownNaughtItem = Main.mouseItem;
				return Main.mouseItem;
			}

			if (Player.inventory != null)
			{
				for (int i = 0; i < Player.inventory.Length; i++)
				{
					Item inv = Player.inventory[i];
					if (inv != null && !inv.IsAir && inv.type == naughtType)
					{
						lastKnownNaughtItem = inv;
						return inv;
					}
				}
			}

			if (lastKnownNaughtItem != null && !lastKnownNaughtItem.IsAir && lastKnownNaughtItem.type == naughtType)
			{
				return lastKnownNaughtItem;
			}

			if (fallbackNaughtItem == null || fallbackNaughtItem.type != naughtType)
			{
				fallbackNaughtItem = new Item();
				fallbackNaughtItem.SetDefaults(naughtType);
			}
			else
			{
				int cfgBase = WeepConfig.Instance?.BaseSwordDamage ?? 25;
				if (fallbackNaughtItem.damage != cfgBase)
				{
					fallbackNaughtItem.SetDefaults(naughtType);
				}
			}

			return fallbackNaughtItem;
		}

		/// <summary>
		/// Computes Naught's progression- and buff-scaled weapon damage via Player.GetWeaponDamage.
		/// </summary>
		/// <summary>
		/// Diminishing-returns per-clone scale for Synchronized Execution:
		/// 1-3 clones deal 100% per clone; 4+ clones smoothly approach a 5.0 effective-clone cap.
		/// </summary>
		public static float GetSynchronizedCloneScale(int participatingCloneCount)
		{
			if (participatingCloneCount <= 3)
				return 1f;

			float effectiveClones = 3f + 2f * (1f - MathF.Exp(-(participatingCloneCount - 3) / 4f));
			return effectiveClones / participatingCloneCount;
		}

		public int GetNaughtDamage(float mult = 1f)
		{
			Item naughtItem = ResolveNaughtItem();
			int baseScaled = Player.GetWeaponDamage(naughtItem);
			return Math.Max(1, (int)MathF.Round(baseScaled * mult));
		}

		/// <summary>
		/// Computes Naught's full crit chance (item base + boss scaling + True Melee/Melee crit + NaughtPlayer buffs).
		/// </summary>
		public int GetNaughtCritChance()
		{
			Item naughtItem = ResolveNaughtItem();
			return Player.GetWeaponCrit(naughtItem);
		}

		/// <summary>
		/// Rolls crit using standard Terraria crit rules (Main.rand.Next(100) &lt; critChance).
		/// </summary>
		public bool RollNaughtCrit(float bonusCritChance = 0f)
		{
			Item naughtItem = ResolveNaughtItem();
			DamageClass damageType = naughtItem?.DamageType ?? WeepCompatibility.TrueMelee;
			if (!damageType.UseStandardCritCalcs)
				return false;

			float totalCrit = GetNaughtCritChance() + bonusCritChance;
			return Main.rand.Next(100) < totalCrit;
		}

		// Cooldown tracking per debuff type: debuffId -> remaining cooldown ticks
		private readonly Dictionary<int, int> debuffCooldowns = new Dictionary<int, int>();
		public const int DebuffCooldownTicks = 540; // 9 seconds per specific debuff type
		public const int MirageWeftApplicationCooldownTicks = DebuffCooldownTicks; // 9 seconds for Mirage Weft thread debuff triggers
		public int MirageWeftApplicationCooldown = 0;
		public const int AbyssGripDebuffDuration = 360; // 6.0s base (9.0s on bosses if Withered River were also active)
		public const int AbyssGripRefreshThreshold = 270; // Only refresh once >= 90 ticks (> 60) have elapsed so UpdateFromSystem detects isRefreshed
		public const int WitheredRiverDebuffDuration = 360; // 6.0s base (9.0s on bosses with +50% duration)
		public const int WitheredRiverRefreshThreshold = 270; // Only refresh once >= 90 ticks (> 60) have elapsed so UpdateFromSystem detects isRefreshed

		public const int BaseCycleSlashCount = 4;
		public const float CycleSlashDamageMult = 0.50f;
		public const int CycleSlashIntervalTicks = 4;

		public int LastCloneCycleFrame = -1;

		private int pendingCycleSlashes = 0;
		private int totalCycleSlashes = 0;
		private int issuedCycleSlashes = 0;
		private int cycleSlashCooldown = 0;
		private int cycleSlashTargetWhoAmI = -1;
		private float lastCycleSlashAngle = -999f;
		private int cycleSlashFlip = 1;

		public override void OnEnterWorld()
		{
			ResetUltVisualAndCameraState(immediateCamera: true);
			ClearPendingCycleVolley();
			LastCloneCycleFrame = -1;
			marionetteCycleTimer = 0;
			FleetingStepSlowTimer = 0;
			VoidPhasingTimer = 0;
			VoidPhasingCooldown = 0;
			StillWaterFocusTimer = 0;
			EmancipatorCritBuffTimer = 0;
			SiphonMoveSpeedTimer = 0;
			MirageWeftApplicationCooldown = 0;
			e2CombatActivityTimer = 0;
			e2PassiveDreamTimer = 0;
			prevStatLife = Player.statLife;

			if (Player.whoAmI == Main.myPlayer && HasRedOniTrace)
			{
				TryTriggerRedOniOpener(applyKnots: HasNaughtInInventory());
			}
		}

		public override void OnRespawn()
		{
			ResetUltVisualAndCameraState(immediateCamera: true);
			ClearPendingCycleVolley();
			LastCloneCycleFrame = -1;
			marionetteCycleTimer = 0;
			FleetingStepSlowTimer = 0;
			VoidPhasingTimer = 0;
			VoidPhasingCooldown = 0;
			StillWaterFocusTimer = 0;
			EmancipatorCritBuffTimer = 0;
			SiphonMoveSpeedTimer = 0;
			MirageWeftApplicationCooldown = 0;
			e2CombatActivityTimer = 0;
			e2PassiveDreamTimer = 0;
			prevStatLife = Player.statLife;

			if (Player.whoAmI == Main.myPlayer && HasRedOniTrace)
			{
				TryTriggerRedOniOpener(applyKnots: HasNaughtInInventory());
			}
		}

		public override void UpdateDead()
		{
			ResetUltVisualAndCameraState(immediateCamera: true);
		}

		public override void PlayerDisconnect()
		{
			ResetUltVisualAndCameraState(immediateCamera: true);
		}

		public override void ModifyScreenPosition()
		{
			if (Main.dedServ || Main.gameMenu || Player.whoAmI != Main.myPlayer)
				return;

			if (UltCameraLocked)
			{
				Main.screenPosition = ClampScreenPosition(ultCamCurrentScreenPos + UltCameraRecoil);
			}
			else if (ultCameraReleaseTicks > 0)
			{
				if (lastUltCameraTick != Main.GameUpdateCount && !Main.gamePaused)
				{
					lastUltCameraTick = Main.GameUpdateCount;
					ultCameraReleaseTicks--;
					float releaseProg = 1f - (ultCameraReleaseTicks / (float)Math.Max(1, ultCameraReleaseDuration));
					float releaseEase = Ult3DProjection.EaseOutCubic(releaseProg);
					UltCameraZoom = MathHelper.Lerp(ultReleaseStartZoom, 1f, releaseEase);
					UltCameraVignetteIntensity = MathHelper.Lerp(ultReleaseStartVignette, 0f, releaseEase);
				}

				float progress = 1f - (ultCameraReleaseTicks / (float)Math.Max(1, ultCameraReleaseDuration));
				float eased = Ult3DProjection.EaseOutCubic(progress);
				Vector2 blended = Vector2.Lerp(ultReleaseStartScreenPos, Main.screenPosition, eased);
				Main.screenPosition = ClampScreenPosition(blended);

				if (ultCameraReleaseTicks <= 0)
				{
					UltCameraZoom = 1f;
					UltCameraVignetteIntensity = 0f;
				}
			}
		}

		/// <summary>
		/// Mark of the Red Oni opener: floors Slashed Dream to 5 and floors Crimson Knots on the highest-HP
		/// valid target within 1800f px to 5 stacks.
		/// </summary>
		public void TryTriggerRedOniOpener(bool applyKnots = true)
		{
			if (Player.whoAmI != Main.myPlayer || Main.netMode == NetmodeID.Server || !HasRedOniTrace)
				return;

			bool gainedDream = false;
			if (SlashedDream < 5)
			{
				SlashedDream = 5;
				gainedDream = true;
			}

			bool appliedKnots = false;
			if (applyKnots)
			{
				NPC target = FindHighestHpEnemy(Player.MountedCenter, 1800f, ignoreMaxedKnots: true);
				if (target != null && target.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData))
				{
					int toAdd = Math.Max(0, 5 - knotData.CrimsonKnotStacks);
					if (toAdd > 0)
					{
						CrimsonKnotGlobalNPC.AddKnot(target, toAdd, target.Center);
						CrimsonKnotTransferBeam.SpawnBeam(Player.GetSource_FromThis(), Player.MountedCenter, target.whoAmI);
						appliedKnots = true;
					}
				}
			}

			if (gainedDream || appliedKnots)
			{
				SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.25f, Volume = 0.8f }, Player.MountedCenter);
			}
		}

		public override void SetControls()
		{
			// Completely freeze the player in place during the Ultimate!
			if (Player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] > 0)
			{
				Player.controlLeft = false;
				Player.controlRight = false;
				Player.controlUp = false;
				Player.controlDown = false;
				Player.controlJump = false;
				Player.controlHook = false;
				Player.controlMount = false;
				Player.controlUseItem = false;
				Player.controlUseTile = false;
			}

			if (Player.whoAmI == Main.myPlayer
				&& NihilityLedgerUI.Instance != null
				&& NihilityLedgerUI.Instance.IsOpen)
			{
				Main.playerInventory = false;
				Player.releaseInventory = false;
				Player.controlInv = false;
				Player.controlUseItem = false;
				Player.controlUseTile = false;
			}
		}

		public bool IsInUltimate => Player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] > 0;

		public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable)
		{
			// True invincibility during the ultimate or during Void Phasing without i-frame transparency blinking
			if (IsInUltimate || VoidPhasingTimer > 0)
				return true;
			return base.ImmuneTo(damageSource, cooldownCounter, dodgeable);
		}

		public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
		{
			if (IsInUltimate || VoidPhasingTimer > 0)
				return false;
			return base.CanBeHitByNPC(npc, ref cooldownSlot);
		}

		public override bool CanBeHitByProjectile(Projectile proj)
		{
			if (IsInUltimate || VoidPhasingTimer > 0)
				return false;
			return base.CanBeHitByProjectile(proj);
		}

		private int GetRainleafThresholdHp() => Math.Max(1, (int)(Player.statLifeMax2 * 0.25f));

		private void TriggerVoidPhasing()
		{
			VoidPhasingTimer = VoidPhasingDuration;
			VoidPhasingCooldown = VoidPhasingCooldownDuration;
			AddSlashedDream(2);
			Player.AddBuff(ModContent.BuffType<RainleafCooldownBuff>(), VoidPhasingCooldown);

			if (!Main.dedServ)
			{
				SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.95f }, Player.Center);

				for (int i = 0; i < 16; i++)
				{
					float ang = MathHelper.TwoPi * i / 16f + Main.rand.NextFloat(-0.12f, 0.12f);
					Vector2 dir = ang.ToRotationVector2();
					Color sparkCol = i % 2 == 0 ? new Color(185, 95, 255) : new Color(255, 65, 110);
					PRTLoader.NewParticle<VoidSparkPRT>(Player.MountedCenter + dir * 10f, dir * Main.rand.NextFloat(3.5f, 7.5f), sparkCol, 0.9f);
				}

				for (int i = 0; i < 8; i++)
				{
					Vector2 vel = Main.rand.NextVector2Circular(3.2f, 3.2f) + new Vector2(0f, -1.0f);
					Color petalCol = Main.rand.NextBool() ? new Color(210, 120, 255, 225) : new Color(245, 45, 85, 225);
					PRTLoader.NewParticle<CrimsonPetalPRT>(Player.MountedCenter + Main.rand.NextVector2Circular(16f, 22f), vel, petalCol, 0.75f);
				}
			}
		}

		private void ResetStillWaterOnDamage()
		{
			bool wasActive = StillWaterFocusTimer >= StillWaterFocusDuration;
			StillWaterFocusTimer = 0;

			if (wasActive && Player.whoAmI == Main.myPlayer && !Main.dedServ)
			{
				SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.25f, Volume = 0.55f }, Player.Center);
				for (int i = 0; i < 8; i++)
				{
					float ang = MathHelper.TwoPi * i / 8f + Main.rand.NextFloat(-0.2f, 0.2f);
					Vector2 dir = ang.ToRotationVector2();
					Color col = i % 2 == 0 ? new Color(140, 220, 255) : new Color(195, 165, 255);
					PRTLoader.NewParticle<VoidSparkPRT>(Player.MountedCenter + dir * 8f, dir * Main.rand.NextFloat(2.5f, 4.5f), col, 0.65f);
				}
			}
		}

		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			if (IsInUltimate || VoidPhasingTimer > 0)
			{
				modifiers.FinalDamage *= 0f;
				modifiers.Cancel();
				return;
			}

			// Rainleaf Phasing: Trigger Void Phasing when incoming damage drops HP to or below 25% max HP
			if (HasRainleafPhasing && VoidPhasingCooldown <= 0)
			{
				modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
				{
					if (VoidPhasingCooldown <= 0 && VoidPhasingTimer <= 0 && Player.statLife - info.Damage <= GetRainleafThresholdHp())
					{
						TriggerVoidPhasing();
					}
				};
			}

			// Still Water Basin resets only upon taking actual non-negated damage
			if (HasStillWater && StillWaterFocusTimer > 0)
			{
				modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
				{
					if (info.Damage > 0)
					{
						ResetStillWaterOnDamage();
					}
				};
			}

			base.ModifyHurt(ref modifiers);
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			if (info.Damage > 0)
			{
				e2CombatActivityTimer = E2CombatTimeoutTicks;
			}
		}

		public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
		{
			if (IsInUltimate || VoidPhasingTimer > 0)
			{
				if (Player.statLife <= 0)
				{
					Player.statLife = VoidPhasingTimer > 0 ? GetRainleafThresholdHp() : 1;
				}
				return false;
			}

			// Rainleaf Phasing: Fallback safety net for extreme hits / Calamity bypasses
			if (HasRainleafPhasing && VoidPhasingCooldown <= 0)
			{
				int thresholdHp = GetRainleafThresholdHp();
				if (Player.statLife < thresholdHp)
				{
					Player.statLife = thresholdHp;
				}
				TriggerVoidPhasing();
				return false;
			}

			return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genDust, ref damageSource);
		}

		public override void UpdateBadLifeRegen()
		{
			if (IsInUltimate || VoidPhasingTimer > 0)
			{
				if (Player.lifeRegen < 0)
					Player.lifeRegen = 0;
				if (Player.lifeRegenCount < 0)
					Player.lifeRegenCount = 0;
			}
		}

		public override void PreUpdateMovement()
		{
			// Eliminate all velocity and gravity during the Ultimate
			if (IsInUltimate)
			{
				Player.velocity = Vector2.Zero;
				Player.gravity = 0f;
			}
		}

		public override void PostUpdate()
		{
			if (!Main.gamePaused)
			{
				if (UltCameraRecoil.LengthSquared() > 0.01f)
				{
					UltCameraRecoil *= 0.80f;
					if (UltCameraRecoil.LengthSquared() < 0.04f)
						UltCameraRecoil = Vector2.Zero;
				}

				if (UltCameraLocked)
				{
					const float dt = 1f / 60f;
					ultCamCurrentScreenPos = ClampScreenPosition(
						StepCriticallyDampedSpring(ultCamCurrentScreenPos, ref ultCamVelocity, ultCamDesiredScreenPos, ultCamStiffness, dt));
					UltCameraZoom = MathHelper.Clamp(
						StepCriticallyDampedSpring(UltCameraZoom, ref ultCamZoomVel, ultCamDesiredZoom, ultCamZoomStiffness, dt),
						0.94f,
						1.10f);
					UltCameraVignetteIntensity = MathHelper.Lerp(UltCameraVignetteIntensity, ultCamDesiredVignette, 0.14f);
				}
			}

			if (!IsInUltimate && (IsUltVisualActive || UltCameraLocked || ActiveUltProjIndex >= 0))
			{
				ResetUltVisualAndCameraState(immediateCamera: false);
			}

			if (DashCooldown > 0)
			{
				DashCooldown--;
			}

			if (VoidPhasingTimer > 0)
			{
				VoidPhasingTimer--;
				Player.immuneNoBlink = true;

				if (!Main.dedServ && VoidPhasingTimer % 4 == 0)
				{
					Vector2 offset = Main.rand.NextVector2Circular(14f, 20f);
					Vector2 vel = -Player.velocity * 0.25f + Main.rand.NextVector2Circular(0.8f, 0.8f);
					Color col = Main.rand.NextBool() ? new Color(185, 95, 255, 210) : new Color(245, 55, 110, 200);
					PRTLoader.NewParticle<VoidSparkPRT>(Player.MountedCenter + offset, vel, col, 0.7f);
				}
			}

			int rainleafBuffType = ModContent.BuffType<RainleafCooldownBuff>();
			int rainleafBuffIdx = Player.FindBuffIndex(rainleafBuffType);
			if (VoidPhasingCooldown > 0)
			{
				VoidPhasingCooldown--;
				if (VoidPhasingCooldown > 0)
				{
					if (rainleafBuffIdx >= 0)
					{
						Player.buffTime[rainleafBuffIdx] = VoidPhasingCooldown;
					}
					else if (Player.active && !Player.dead)
					{
						Player.AddBuff(rainleafBuffType, VoidPhasingCooldown);
					}
				}
				else
				{
					if (rainleafBuffIdx >= 0)
						Player.DelBuff(rainleafBuffIdx);

					if (HasRainleafPhasing && Player.whoAmI == Main.myPlayer && !Main.dedServ)
						SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.25f, Volume = 0.65f }, Player.Center);
				}
			}
			else if (rainleafBuffIdx >= 0)
			{
				Player.DelBuff(rainleafBuffIdx);
			}

			// Rainleaf Phasing: Trigger if DoT or life-drain drops the player to or below 25% max HP
			if (HasRainleafPhasing && VoidPhasingCooldown <= 0 && VoidPhasingTimer <= 0 && !IsInUltimate
				&& Player.active && !Player.dead && !Player.ghost && prevStatLife > 0)
			{
				int thresholdHp = GetRainleafThresholdHp();
				bool crossedThreshold = prevStatLife > thresholdHp && Player.statLife <= thresholdHp;
				bool drainingAtThreshold = Player.statLife <= thresholdHp && Player.statLife < prevStatLife;
				if (crossedThreshold || drainingAtThreshold)
				{
					TriggerVoidPhasing();
				}
			}
			if (ThunderCoreBuffTimer > 0 && !WorldFreezeSystem.IsActive) ThunderCoreBuffTimer--;

			if (!Player.active || Player.dead || Player.ghost || !HasEmancipatorWill)
			{
				EmancipatorCritBuffTimer = 0;
			}
			else if (EmancipatorCritBuffTimer > 0 && !IsInUltimate)
			{
				EmancipatorCritBuffTimer--;
				if (!Main.dedServ && Player.whoAmI == Main.myPlayer && EmancipatorCritBuffTimer % 10 == 0)
				{
					Vector2 offset = Main.rand.NextVector2Circular(26f, 34f);
					Vector2 vel = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1.4f, -0.5f));
					Color col = Main.rand.NextBool() ? new Color(255, 45, 75) : new Color(255, 195, 75);
					PRTLoader.NewParticle<VoidSparkPRT>(Player.MountedCenter + offset, vel, col, 0.58f);
				}
			}

			int emancipatorBuffType = ModContent.BuffType<EmancipatorWillBuff>();
			int emancipatorBuffIdx = Player.FindBuffIndex(emancipatorBuffType);
			if (IsEmancipatorCritActive && Player.active && !Player.dead && !Player.ghost)
			{
				int displayTime = Math.Max(2, EmancipatorCritBuffTimer);
				if (emancipatorBuffIdx >= 0)
				{
					Player.buffTime[emancipatorBuffIdx] = displayTime;
				}
				else
				{
					Player.AddBuff(emancipatorBuffType, displayTime);
				}
			}
			else if (emancipatorBuffIdx >= 0)
			{
				Player.DelBuff(emancipatorBuffIdx);
			}

			int siphonBuffType = ModContent.BuffType<SiphonHasteBuff>();
			int siphonBuffIdx = Player.FindBuffIndex(siphonBuffType);
			if (!Player.active || Player.dead || Player.ghost || !HasDepartedSiphon)
			{
				SiphonMoveSpeedTimer = 0;
			}
			else if (SiphonMoveSpeedTimer > 0 && !IsInUltimate && !WorldFreezeSystem.IsActive)
			{
				SiphonMoveSpeedTimer--;
				if (!Main.dedServ && Player.whoAmI == Main.myPlayer && Player.velocity.LengthSquared() > 4f && SiphonMoveSpeedTimer % 5 == 0)
				{
					Vector2 offset = Main.rand.NextVector2Circular(12f, 16f);
					Vector2 vel = -Player.velocity * 0.2f + Main.rand.NextVector2Circular(0.6f, 0.6f);
					Color col = Main.rand.NextBool() ? new Color(255, 75, 115, 220) : new Color(235, 30, 65, 210);
					PRTLoader.NewParticle<VoidSparkPRT>(Player.MountedCenter + offset, vel, col, 0.65f);
				}
			}

			if (IsSiphonMoveSpeedActive)
			{
				if (siphonBuffIdx >= 0)
				{
					Player.buffTime[siphonBuffIdx] = SiphonMoveSpeedTimer;
				}
				else if (Player.active && !Player.dead)
				{
					Player.AddBuff(siphonBuffType, SiphonMoveSpeedTimer);
				}
			}
			else if (siphonBuffIdx >= 0)
			{
				Player.DelBuff(siphonBuffIdx);
			}
			if (FleetingStepDreamCooldown > 0) FleetingStepDreamCooldown--;

			if (!Player.active || Player.dead || Player.ghost || !HasFleetingStep)
			{
				FleetingStepSlowTimer = 0;
			}
			else if (FleetingStepSlowTimer > 0 && !WorldFreezeSystem.IsActive)
			{
				FleetingStepSlowTimer--;

				if (!Main.dedServ && Player.whoAmI == Main.myPlayer && FleetingStepSlowTimer % 8 == 0)
				{
					Vector2 offset = Main.rand.NextVector2Circular(34f, 38f);
					Vector2 vel = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-1.2f, -0.3f));
					Color col = Main.rand.NextBool() ? new Color(238, 26, 56, 220) : new Color(185, 45, 255, 210);
					PRTLoader.NewParticle<CrimsonPetalPRT>(Player.MountedCenter + offset, vel, col, 0.65f);
				}
			}

			// Still Water Basin focus: builds over 15s while equipped and holding Naught without taking damage
			int stillWaterBuffType = ModContent.BuffType<StillWaterFocusBuff>();
			int stillWaterBuffIdx = Player.FindBuffIndex(stillWaterBuffType);
			if (HasStillWater && Player.active && !Player.dead && !Player.ghost)
			{
				if (IsNaughtEquipped)
				{
					bool wasActive = StillWaterFocusTimer >= StillWaterFocusDuration;
					if (StillWaterFocusTimer < StillWaterFocusDuration)
					{
						StillWaterFocusTimer++;
					}

					if (!wasActive && StillWaterFocusTimer >= StillWaterFocusDuration
						&& Player.whoAmI == Main.myPlayer && !Main.dedServ && !IsInUltimate)
					{
						SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.45f, Volume = 0.55f }, Player.Center);
						for (int i = 0; i < 12; i++)
						{
							float ang = MathHelper.TwoPi * i / 12f;
							Vector2 dir = ang.ToRotationVector2();
							Color col = i % 2 == 0 ? new Color(150, 230, 255) : new Color(210, 175, 255);
							PRTLoader.NewParticle<VoidSparkPRT>(Player.MountedCenter + dir * 10f, dir * 3.2f, col, 0.70f);
						}
					}
				}
			}
			else
			{
				StillWaterFocusTimer = 0;
			}

			if (IsStillWaterActive)
			{
				if (stillWaterBuffIdx >= 0)
				{
					Player.buffTime[stillWaterBuffIdx] = 2;
				}
				else
				{
					Player.AddBuff(stillWaterBuffType, 2);
				}
			}
			else if (stillWaterBuffIdx >= 0)
			{
				Player.DelBuff(stillWaterBuffIdx);
			}

			// Decrement per-debuff cooldown timers and Mirage Weft's separate application cooldown
			if (MirageWeftApplicationCooldown > 0)
			{
				MirageWeftApplicationCooldown--;
			}

			if (debuffCooldowns.Count > 0)
			{
				var keys = new List<int>(debuffCooldowns.Keys);
				foreach (int key in keys)
				{
					if (debuffCooldowns[key] > 0)
					{
						debuffCooldowns[key]--;
					}
					else
					{
						debuffCooldowns.Remove(key);
					}
				}
			}

			// Shadow Marionette: Periodic 15-second cycle pulse while indefinite clones remain active
			if (!Player.active || Player.dead || Player.ghost || !HasShadowMarionette)
			{
				marionetteCycleTimer = 0;
			}
			else if (Player.whoAmI == Main.myPlayer)
			{
				int activeClones = 0;
				uint latestSeq = 0;
				Vector2 pulseOrigin = Player.MountedCenter;
				int cloneType = ModContent.ProjectileType<NaughtClone>();
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (p.active && p.owner == Player.whoAmI && p.type == cloneType && p.ModProjectile is NaughtClone clone)
					{
						activeClones++;
						if (clone.SpawnSequence >= latestSeq)
						{
							latestSeq = clone.SpawnSequence;
							pulseOrigin = clone.Projectile.Center;
						}
					}
				}

				if (activeClones <= 0)
				{
					marionetteCycleTimer = 0;
				}
				else if (!WorldFreezeSystem.IsActive)
				{
					marionetteCycleTimer++;
					if (marionetteCycleTimer >= MarionetteCycleIntervalTicks)
					{
						marionetteCycleTimer = 0;
						OnCloneCycleComplete(pulseOrigin, activeClones);
					}
				}
			}

			// Dispatch staggered clone-cycle localized slashes & Thunder Core bolts (paused cleanly during WorldFreeze time-stop)
			if (!Player.active || Player.dead || Player.ghost)
			{
				ClearPendingCycleVolley();
				ClearPendingThunderBarrage();
			}
			else
			{
				if (pendingCycleSlashes > 0 && !WorldFreezeSystem.IsActive && Player.whoAmI == Main.myPlayer)
				{
					if (cycleSlashCooldown > 0)
						cycleSlashCooldown--;

					if (cycleSlashCooldown <= 0)
						TryDispatchNextCycleSlash();
				}

				if (pendingThunderBolts > 0 && !WorldFreezeSystem.IsActive && Player.whoAmI == Main.myPlayer)
				{
					if (thunderBoltCooldown > 0)
						thunderBoltCooldown--;

					if (thunderBoltCooldown <= 0)
						TryDispatchNextThunderBolt();
				}
			}

			prevStatLife = Player.active && !Player.dead ? Player.statLife : -1;

			// E5 cap enforcement when toggling E5 off
			if (OverflowDream > CurrentMaxOverflowDream)
			{
				OverflowDream = CurrentMaxOverflowDream;
			}

			// E2 (Mute Thunder): passive +1 Slashed Dream every 9.0s while wielding Naught in active combat
			if (!Player.active || Player.dead || Player.ghost || !HasEidolon2)
			{
				e2CombatActivityTimer = 0;
				e2PassiveDreamTimer = 0;
			}
			else if (!WorldFreezeSystem.IsActive)
			{
				if (e2CombatActivityTimer > 0)
					e2CombatActivityTimer--;

				bool inCombat = e2CombatActivityTimer > 0 || IsAnyBossAliveNearby(3600f);
				if (IsNaughtEquipped && inCombat)
				{
					e2PassiveDreamTimer++;
					if (e2PassiveDreamTimer >= E2SlashedDreamInterval)
					{
						e2PassiveDreamTimer = 0;
						if (Player.whoAmI == Main.myPlayer)
						{
							AddSlashedDream(1);
							if (!Main.dedServ)
							{
								SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f, Volume = 0.35f }, Player.MountedCenter);
								for (int i = 0; i < 6; i++)
								{
									Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
									Color col = i % 2 == 0 ? new Color(185, 65, 255) : new Color(245, 35, 75);
									PRTLoader.NewParticle<VoidSparkPRT>(Player.MountedCenter + dir * 10f, dir * Main.rand.NextFloat(1.8f, 3.8f), col, 0.72f);
								}
							}
						}
					}
				}
				else
				{
					e2PassiveDreamTimer = 0;
				}
			}
		}

		private bool IsAnyBossAliveNearby(float maxDist)
		{
			float maxDistSq = maxDist * maxDist;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (npc.active && CrimsonKnotGlobalNPC.IsBossTarget(npc) && Vector2.DistanceSquared(Player.MountedCenter, npc.Center) <= maxDistSq)
					return true;
			}
			return false;
		}

		private void MarkE2CombatActivity(NPC target, int damageDone)
		{
			if (damageDone <= 0)
				return;

			if (target != null && (target.type == NPCID.TargetDummy || target.friendly))
				return;

			e2CombatActivityTimer = E2CombatTimeoutTicks;
		}

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (WeepKeybinds.DomainKeybind != null && WeepKeybinds.DomainKeybind.JustPressed)
			{
				ToggleDomain();
			}

			if (WeepKeybinds.UltimateKeybind != null && WeepKeybinds.UltimateKeybind.JustPressed)
			{
				// If ultimate is not running, attempt to start it
				if (Player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] == 0)
				{
					TryActivateUltimate();
				}
			}

			if (WeepKeybinds.LedgerKeybind != null && WeepKeybinds.LedgerKeybind.JustPressed)
			{
				ToggleLedgerUI();
			}
		}

		public void ToggleLedgerUI()
		{
			if (NihilityLedgerUI.Instance == null)
				return;

			if (NihilityLedgerUI.Instance.IsOpen)
				NihilityLedgerUI.Instance.Close();
			else
				NihilityLedgerUI.Instance.Open();
		}

		public override void SaveData(TagCompound tag)
		{
			if (EquippedCharms != null)
			{
				tag["EquippedCharms"] = new List<string>(EquippedCharms);
			}

			if (EnabledEidolons != null)
			{
				tag["EnabledEidolons"] = new List<bool>(EnabledEidolons);
			}
		}

		public override void LoadData(TagCompound tag)
		{
			if (tag.ContainsKey("EquippedCharms"))
			{
				var list = tag.Get<List<string>>("EquippedCharms");
				for (int i = 0; i < 3 && i < list.Count; i++)
				{
					EquippedCharms[i] = list[i];
				}
			}

			EnabledEidolons = new bool[EidolonCount] { true, true, true, true, true, true, true };
			if (tag.ContainsKey("EnabledEidolons"))
			{
				var list = tag.Get<List<bool>>("EnabledEidolons");
				int copyCount = Math.Min(EidolonCount, list.Count);
				for (int i = 0; i < copyCount; i++)
				{
					EnabledEidolons[i] = list[i];
				}
			}
			// Legacy "UnlockedEidolons" (bool[6]) intentionally migrates to all-true EnabledEidolons
			// so existing characters automatically inherit their world-progressed Eidolons.
		}

		public void ToggleDomain()
		{
			if (Player.whoAmI != Main.myPlayer || !Player.active || Player.dead || Player.ghost)
				return;

			if (!IsNaughtEquipped)
				return;

			if (NihilityDomainSystem.IsDomainActive)
			{
				// Do not close domain during active ultimate execution
				if (Player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] > 0)
					return;

				// If already in closing reverse wave, re-triggering re-opens the domain
				if (NihilityDomainSystem.DomainClosingActive)
				{
					NihilityDomainSystem.ActivateDomain(Player.Center);
					SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 0.95f }, Player.Center);
					SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f, Volume = 0.9f }, Player.Center);
				}
				else
				{
					// Start reverse wave that collapses back into the player and restores the normal world!
					NihilityDomainSystem.StartDomainClosing(Player.Center);
				}
			}
			else
			{
				NihilityDomainSystem.ActivateDomain(Player.Center);
				SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 0.95f }, Player.Center);
				SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f, Volume = 0.9f }, Player.Center);
			}
		}

		public bool TryActivateUltimate()
		{
			if (Player.whoAmI != Main.myPlayer)
				return false;

			if (!Player.active || Player.dead || Player.ghost || Player.CCed)
				return false;

			if (!IsNaughtEquipped)
				return false;

			// Ultimate is strictly restricted to being cast inside the Nihility Domain!
			if (!NihilityDomainSystem.IsDomainActive)
			{
				return false;
			}

			if (SlashedDream < MaxSlashedDream)
			{
				return false;
			}

			if (Player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] > 0)
				return false;

			// Spawn Ultimate execution projectile
			int damage = GetNaughtDamage();
			float knockback = 8f;
			int projIdx = Projectile.NewProjectile(Player.GetSource_FromThis(), Player.MountedCenter, Vector2.Zero, ModContent.ProjectileType<SlashedDreamUltimate>(), damage, knockback, Player.whoAmI);
			if (projIdx >= 0 && projIdx < Main.maxProjectiles)
			{
				Main.projectile[projIdx].CritChance = GetNaughtCritChance();
			}
			return true;
		}

		public void AddSlashedDream(int amount = 1)
		{
			if (amount <= 0)
				return;

			int prevDream = SlashedDream;

			for (int i = 0; i < amount; i++)
			{
				if (SlashedDream < MaxSlashedDream)
				{
					SlashedDream++;
				}
				else if (OverflowDream < CurrentMaxOverflowDream)
				{
					OverflowDream++;
				}
			}

			// Chime sound when reaching full 9 points
			if (prevDream < MaxSlashedDream && SlashedDream >= MaxSlashedDream)
			{
				SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.85f, Pitch = 0.15f }, Player.MountedCenter);
			}
		}

		public bool CanDebuffTrigger(int debuffType)
		{
			if (debuffCooldowns.TryGetValue(debuffType, out int cd) && cd > 0)
				return false;

			return true;
		}

		public void TryTriggerDebuff(int debuffType, NPC target)
		{
			if (!CanDebuffTrigger(debuffType))
				return;

			// Put this specific debuff on its own cooldown
			debuffCooldowns[debuffType] = DebuffCooldownTicks;

			// Gain 1 Slashed Dream
			AddSlashedDream(1);

			// Apply 1 Crimson Knot to target's resolved anchor
			if (target != null && target.active)
			{
				CrimsonKnotGlobalNPC.AddKnot(target, 1, target.Center);
			}
		}

		public void ClearPendingThunderBarrage()
		{
			pendingThunderBolts = 0;
			thunderBoltCooldown = 0;
			thunderPrimaryTargetWhoAmI = -1;
			thunderFallbackOrigin = Vector2.Zero;
		}

		public void QueueThunderCoreBarrage(int primaryTargetWhoAmI, Vector2 fallbackOrigin)
		{
			pendingThunderBolts = ThunderBoltTotalCount;
			thunderBoltCooldown = 0;
			thunderPrimaryTargetWhoAmI = primaryTargetWhoAmI;
			thunderFallbackOrigin = fallbackOrigin;
			thunderBarrageDebuffRewardClaimed = false;
		}

		public void ProcessThunderBarrageDebuff(int debuffType, NPC target, int buffDuration)
		{
			if (!thunderBarrageDebuffRewardClaimed && CanDebuffTrigger(debuffType))
			{
				thunderBarrageDebuffRewardClaimed = true;
				TryTriggerDebuff(debuffType, target);
			}

			if (target != null && target.active && target.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData))
			{
				knotData.RecordThunderBarrageDebuff(debuffType, buffDuration);
			}
		}

		/// <summary>
		/// Handles debuffs applied or refreshed by Mirage Weft threads.
		/// Grants at most 1 Slashed Dream and 1 Crimson Knot every 9 seconds on a dedicated cooldown
		/// separate from normal per-debuff cooldowns, while suppressing normal debuff-scan triggers.
		/// </summary>
		public void ProcessMirageWeftDebuff(int debuffType, NPC debuffNpc, NPC hitTarget = null)
		{
			if (debuffNpc == null || !debuffNpc.active)
				return;

			if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI != Main.myPlayer)
				return;

			if (HasMirageWeft && MirageWeftApplicationCooldown <= 0)
			{
				MirageWeftApplicationCooldown = MirageWeftApplicationCooldownTicks;
				AddSlashedDream(1);

				NPC knotTarget = (hitTarget != null && hitTarget.active) ? hitTarget : debuffNpc;
				CrimsonKnotGlobalNPC.AddKnot(knotTarget, 1, knotTarget.Center);
			}

			if (debuffNpc.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData))
			{
				knotData.RecordMirageWeftDebuff(debuffType);
			}
		}

		private void TryDispatchNextThunderBolt()
		{
			if (pendingThunderBolts <= 0)
				return;

			NPC target = null;
			if (thunderPrimaryTargetWhoAmI >= 0 && thunderPrimaryTargetWhoAmI < Main.maxNPCs)
			{
				NPC candidate = Main.npc[thunderPrimaryTargetWhoAmI];
				if (candidate.active && !candidate.friendly && (!candidate.dontTakeDamage || candidate.type == NPCID.TargetDummy))
				{
					target = candidate;
				}
			}

			if (target == null)
			{
				float bestDistSq = 1600f * 1600f;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC candidate = Main.npc[i];
					if (candidate.active && !candidate.friendly && (!candidate.dontTakeDamage || candidate.type == NPCID.TargetDummy))
					{
						float dSq = Vector2.DistanceSquared(Player.MountedCenter, candidate.Center);
						if (dSq < bestDistSq)
						{
							bestDistSq = dSq;
							target = candidate;
						}
					}
				}
				thunderPrimaryTargetWhoAmI = target?.whoAmI ?? -1;
			}

			Vector2 baseOrigin = target != null
				? target.Center
				: (thunderFallbackOrigin != Vector2.Zero ? thunderFallbackOrigin : Player.Center + new Vector2(Player.direction * 200f, 0f));

			Vector2 strikePos = baseOrigin + new Vector2(Main.rand.NextFloat(-68f, 68f), Main.rand.NextFloat(-24f, 24f));
			int boltDamage = GetNaughtDamage(1.44f);
			int boltIdx = Projectile.NewProjectile(Player.GetSource_FromThis(), strikePos, Vector2.Zero, ModContent.ProjectileType<ThunderCoreBolt>(), boltDamage, 4f, Player.whoAmI);
			if (boltIdx >= 0 && boltIdx < Main.maxProjectiles && Main.projectile[boltIdx].ModProjectile is ThunderCoreBolt bolt)
			{
				bolt.Init(strikePos);
			}

			pendingThunderBolts--;
			thunderBoltCooldown = pendingThunderBolts > 0 ? ThunderBoltIntervalTicks : 0;
		}

		public void OnCloneCycleComplete(Vector2 clonePosition, int expiringCloneCount = 1)
		{
			// Prevent multiple clones expiring in the same frame from rewarding duplicate cycles
			if (Main.GameUpdateCount == LastCloneCycleFrame)
				return;

			LastCloneCycleFrame = (int)Main.GameUpdateCount;
			expiringCloneCount = Math.Max(1, expiringCloneCount);

			// 1. Gain 1 Slashed Dream
			AddSlashedDream(1);

			// 2. Apply 1 Crimson Knot (+1 flat bonus with Nameless Decoy) to highest-health valid enemy
			NPC knotTarget = FindHighestHpEnemy(clonePosition, 1800f, ignoreMaxedKnots: true);
			if (knotTarget != null)
			{
				int knotStacks = 1 + (HasNamelessDecoy ? 1 : 0);
				CrimsonKnotGlobalNPC.AddKnot(knotTarget, knotStacks, knotTarget.Center);

				// Spawn tether beam from clone to enemy
				CrimsonKnotTransferBeam.SpawnBeam(Player.GetSource_FromThis(), clonePosition, knotTarget.whoAmI);
			}

			// 3. Basekit localized auto-targeting cycle slashes (4 x 50% base, +1 slash with E6, 55% with E5)
			//    + Nameless Decoy adds +1 extra cut per expiring clone
			int baseSlashCount = BaseCycleSlashCount + (HasEidolon6 ? 1 : 0);
			int totalSlashes = baseSlashCount + (HasNamelessDecoy ? expiringCloneCount : 0);
			NPC slashTarget = FindCycleSlashTarget(clonePosition);
			if (slashTarget != null)
			{
				QueueCycleSlashVolley(totalSlashes, slashTarget.whoAmI);

				if (!WorldFreezeSystem.IsActive && Player.whoAmI == Main.myPlayer)
				{
					TryDispatchNextCycleSlash();
				}
			}

			// Ethereal clone expiration audio chime
			SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.65f, Pitch = 0.35f }, clonePosition);

			// 4. Shadow Crucible: Detonate accumulated DoT heat on all debuff-branded enemies
			if (HasShadowCrucible)
			{
				CrimsonKnotGlobalNPC.DetonateShadowCrucibles(this);
			}
		}

		private void ClearPendingCycleVolley()
		{
			pendingCycleSlashes = 0;
			totalCycleSlashes = 0;
			issuedCycleSlashes = 0;
			cycleSlashCooldown = 0;
			cycleSlashTargetWhoAmI = -1;
		}

		private void QueueCycleSlashVolley(int slashCount, int targetWhoAmI)
		{
			if (slashCount <= 0)
				return;

			if (pendingCycleSlashes <= 0)
			{
				issuedCycleSlashes = 0;
				cycleSlashCooldown = 0;
			}

			pendingCycleSlashes += slashCount;
			totalCycleSlashes = issuedCycleSlashes + pendingCycleSlashes;
			cycleSlashTargetWhoAmI = targetWhoAmI;
		}

		private bool TryDispatchNextCycleSlash()
		{
			if (pendingCycleSlashes <= 0)
				return false;

			NPC target = null;
			if (cycleSlashTargetWhoAmI >= 0 && cycleSlashTargetWhoAmI < Main.maxNPCs)
			{
				NPC candidate = Main.npc[cycleSlashTargetWhoAmI];
				if (IsValidCycleSlashTarget(candidate))
				{
					target = candidate;
				}
			}

			if (target == null)
			{
				target = FindCycleSlashTarget(Player.MountedCenter);
				cycleSlashTargetWhoAmI = target?.whoAmI ?? -1;
			}

			if (target == null)
			{
				ClearPendingCycleVolley();
				return false;
			}

			float rollAngle = PickNextCycleSlashAngle();
			cycleSlashFlip = -cycleSlashFlip;
			float escalate = totalCycleSlashes > 1
				? MathHelper.Clamp(issuedCycleSlashes / (float)(totalCycleSlashes - 1), 0f, 1f)
				: 0f;

			float slashMult = HasEidolon5 ? 0.55f : CycleSlashDamageMult;
			int damage = GetNaughtDamage(slashMult);
			int critChance = GetNaughtCritChance();

			NaughtCycleSlash.Spawn(
				Player.GetSource_FromThis(),
				target,
				rollAngle,
				cycleSlashFlip,
				escalate,
				damage,
				knockBack: 3.5f,
				Player.whoAmI,
				critChance);

			issuedCycleSlashes++;
			pendingCycleSlashes--;
			if (pendingCycleSlashes > 0)
			{
				cycleSlashCooldown = CycleSlashIntervalTicks;
			}
			else
			{
				ClearPendingCycleVolley();
			}

			return true;
		}

		private float PickNextCycleSlashAngle()
		{
			float angle;
			if (lastCycleSlashAngle < -900f)
			{
				angle = Main.rand.NextFloat(-MathHelper.Pi, MathHelper.Pi);
			}
			else
			{
				float step = Main.rand.NextFloat(0.95f, 1.85f) * (Main.rand.NextBool() ? 1f : -1f);
				angle = MathHelper.WrapAngle(lastCycleSlashAngle + step);
			}

			lastCycleSlashAngle = angle;
			return angle;
		}

		private static bool IsValidCycleSlashTarget(NPC npc)
		{
			if (npc == null || !npc.active || npc.friendly || npc.isLikeATownNPC || npc.life <= 0)
				return false;

			if (npc.type == NPCID.TargetDummy)
				return true;

			if (npc.dontTakeDamage)
				return false;

			bool isBoss = CrimsonKnotGlobalNPC.IsBossTarget(npc);
			return isBoss || npc.CanBeChasedBy();
		}

		private NPC FindCycleSlashTarget(Vector2 preferredOrigin)
		{
			NPC target = FindHighestHpEnemy(preferredOrigin, 1800f, ignoreMaxedKnots: false, ignoreInvulnerable: true);
			if (IsValidCycleSlashTarget(target))
				return target;

			if (preferredOrigin != Player.MountedCenter)
			{
				target = FindHighestHpEnemy(Player.MountedCenter, 1800f, ignoreMaxedKnots: false, ignoreInvulnerable: true);
				if (IsValidCycleSlashTarget(target))
					return target;
			}

			return null;
		}

		public static NPC FindHighestHpEnemy(Vector2 searchOrigin, float maxDistance = 1800f, bool ignoreMaxedKnots = false, bool ignoreInvulnerable = false)
		{
			NPC target = null;
			int highestPriority = -1;
			int highestHp = 0;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC other = Main.npc[i];
				if (other.active && !other.friendly && !other.isLikeATownNPC && other.lifeMax > 5)
				{
					bool isDummy = other.type == NPCID.TargetDummy;
					bool isBoss = CrimsonKnotGlobalNPC.IsBossTarget(other);

					if (!other.CanBeChasedBy() && !isDummy && !isBoss)
						continue;

					if (ignoreInvulnerable && other.dontTakeDamage && !isDummy)
						continue;

					NPC knotAnchor = CrimsonKnotGlobalNPC.ResolveKnotAnchor(other);
					if (ignoreInvulnerable && knotAnchor != null && knotAnchor.dontTakeDamage && knotAnchor.type != NPCID.TargetDummy)
						continue;

					if (ignoreMaxedKnots)
					{
						var knotNPC = knotAnchor.GetGlobalNPC<CrimsonKnotGlobalNPC>();
						if (knotNPC != null && knotNPC.CrimsonKnotStacks >= CrimsonKnotGlobalNPC.MaxKnotStacks)
							continue;
					}

					float dist = Vector2.Distance(searchOrigin, other.Center);
					if (dist < maxDistance)
					{
						int priority = isBoss ? 2 : (!isDummy ? 1 : 0);
						if (priority > highestPriority || (priority == highestPriority && other.life > highestHp))
						{
							highestPriority = priority;
							highestHp = other.life;
							target = other;
						}
					}
				}
			}

			return target != null ? CrimsonKnotGlobalNPC.ResolveKnotAnchor(target) : null;
		}

		public override void PostUpdateRunSpeeds()
		{
			if (VoidPhasingTimer > 0)
			{
				Player.runAcceleration *= 1.5f;
				Player.maxRunSpeed *= 1.5f;
			}
			else if (IsSiphonMoveSpeedActive)
			{
				Player.runAcceleration *= 1.15f;
				Player.maxRunSpeed *= 1.15f;
			}
		}

		public override void ModifyWeaponDamage(Item item, ref StatModifier damage)
		{
			if (item.type == ModContent.ItemType<Naught>())
			{
				if (IsThunderCoreActive)
				{
					damage += 0.15f; // +15% True Melee damage for 15s post-ult
				}
			}
		}

		public override void ModifyWeaponCrit(Item item, ref float crit)
		{
			if (item.type == ModContent.ItemType<Naught>())
			{
				if (IsStillWaterActive)
				{
					crit += 20f; // +20% True Melee Crit from Still Water
				}
				if (IsEmancipatorCritActive)
				{
					crit += 20f; // +20% True Melee Crit post-ult
				}
			}
		}

		public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (WeepCompatibility.IsNaughtDamageClass(item.DamageType))
			{
				ApplyNaughtHitModifiers(target, ref modifiers, item.DamageType, sourceProj: null, baseCritChanceForNative: Player.GetWeaponCrit(item));
			}
			else
			{
				CrimsonKnotGlobalNPC.ClearPendingWitheredDrPeel();
			}
		}

		public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (WeepCompatibility.IsNaughtDamageClass(proj.DamageType))
			{
				ApplyNaughtHitModifiers(target, ref modifiers, proj.DamageType, sourceProj: proj, baseCritChanceForNative: proj.CritChance);
			}
			else
			{
				CrimsonKnotGlobalNPC.ClearPendingWitheredDrPeel();
			}
		}

		public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
		{
			MarkE2CombatActivity(target, damageDone);

			if (item.type == ModContent.ItemType<Naught>())
			{
				TryApplyTideHitDebuffs(target);
			}
		}

		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			MarkE2CombatActivity(target, damageDone);

			if (proj.ModProjectile?.Mod == Mod && WeepCompatibility.IsNaughtDamageClass(proj.DamageType))
			{
				TryApplyTideHitDebuffs(target);
			}
		}

		public void TryApplyTideHitDebuffs(NPC target)
		{
			TryApplyAbyssGripDebuff(target);
			TryApplyWitheredRiverDebuff(target);
		}

		private void TryApplyAnchorDebuff(NPC target, int debuffType, int duration, int refreshThreshold)
		{
			if (target == null || !target.active || target.friendly)
				return;

			if (target.dontTakeDamage && target.type != NPCID.TargetDummy)
				return;

			if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI != Main.myPlayer)
				return;

			NPC anchor = CrimsonKnotGlobalNPC.ResolveKnotAnchor(target) ?? target;
			if (anchor == null || !anchor.active)
				return;

			int buffIdx = anchor.FindBuffIndex(debuffType);
			if (buffIdx < 0 || anchor.buffTime[buffIdx] <= refreshThreshold)
			{
				anchor.AddBuff(debuffType, duration);
			}
		}

		/// <summary>
		/// Applies or refreshes AbyssGripDebuff on the target's resolved boss anchor for Naught hits.
		/// Throttles refreshes until remaining duration drops below AbyssGripRefreshThreshold so each refresh
		/// produces a >60-tick duration jump for CrimsonKnotGlobalNPC.UpdateFromSystem's 9s Slashed Dream cycle.
		/// </summary>
		public void TryApplyAbyssGripDebuff(NPC target)
		{
			if (!HasAbyssGrip)
				return;

			TryApplyAnchorDebuff(target, ModContent.BuffType<AbyssGripDebuff>(), AbyssGripDebuffDuration, AbyssGripRefreshThreshold);
		}

		/// <summary>
		/// Applies or refreshes WitheredRiverDebuff on the target's resolved boss anchor for Naught hits.
		/// Throttles refreshes until remaining duration drops below WitheredRiverRefreshThreshold so each refresh
		/// produces a >60-tick duration jump for CrimsonKnotGlobalNPC.UpdateFromSystem's 9s Slashed Dream cycle.
		/// Falls back to anchor-tracked duration on buff-immune targets (e.g. Destroyer or full buff slots)
		/// so both the 10% DR shred and debuff visuals still apply reliably.
		/// </summary>
		public void TryApplyWitheredRiverDebuff(NPC target)
		{
			if (!HasWitheredRiver || target == null || !target.active || target.friendly)
				return;

			if (target.dontTakeDamage && target.type != NPCID.TargetDummy)
				return;

			if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI != Main.myPlayer)
				return;

			NPC anchor = CrimsonKnotGlobalNPC.ResolveKnotAnchor(target) ?? target;
			if (anchor == null || !anchor.active)
				return;

			int debuffType = ModContent.BuffType<WitheredRiverDebuff>();
			int buffIdx = anchor.FindBuffIndex(debuffType);
			anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var anchorData);

			int currentRemaining = buffIdx >= 0
				? anchor.buffTime[buffIdx]
				: (anchorData?.WitheredRiverFallbackTimer ?? 0);

			if (currentRemaining <= 0 || currentRemaining <= WitheredRiverRefreshThreshold)
			{
				bool isFirstApply = currentRemaining <= 0;
				anchor.AddBuff(debuffType, WitheredRiverDebuffDuration);

				int appliedIdx = anchor.FindBuffIndex(debuffType);
				if (appliedIdx >= 0)
				{
					if (anchorData != null)
					{
						anchorData.WitheredRiverFallbackTimer = 0;
					}
				}
				else if (anchorData != null)
				{
					int fallbackDuration = CrimsonKnotGlobalNPC.IsBossTarget(anchor)
						? (int)MathF.Round(WitheredRiverDebuffDuration * 1.50f)
						: WitheredRiverDebuffDuration;
					anchorData.WitheredRiverFallbackTimer = fallbackDuration;
					TryTriggerDebuff(debuffType, anchor);
				}

				if (anchorData != null)
				{
					anchorData.WitheredRiverApplyFlash = 1f;
				}

				if (!Main.dedServ)
				{
					SpawnWitheredRiverApplyBurst(target, isFirstApply);
				}
			}
		}

		private static void SpawnWitheredRiverApplyBurst(NPC target, bool isFirstApply)
		{
			int sparkCount = isFirstApply ? 9 : 5;
			for (int i = 0; i < sparkCount; i++)
			{
				Vector2 vel = Main.rand.NextVector2Circular(4.2f, 4.2f) + new Vector2(0f, 0.8f);
				Color col = i % 3 == 0 ? new Color(215, 195, 200) : new Color(215, 32, 60);
				PRTLoader.NewParticle<VoidSparkPRT>(target.Center, vel, col, isFirstApply ? 0.85f : 0.70f);
			}

			int petalCount = isFirstApply ? 3 : 1;
			for (int p = 0; p < petalCount; p++)
			{
				Vector2 pVel = Main.rand.NextVector2Circular(2.6f, 2.2f) + Vector2.UnitY * 0.9f;
				PRTLoader.NewParticle<CrimsonPetalPRT>(target.Center, pVel, new Color(185, 32, 54, 215), 0.78f);
			}
		}

		public bool IsUltimateProjectile(Projectile proj)
		{
			if (proj == null)
				return false;

			int type = proj.type;
			if (type == ModContent.ProjectileType<SlashedDreamUltimate>()
				|| type == ModContent.ProjectileType<ThunderCoreBolt>()
				|| type == ModContent.ProjectileType<BloodBlossomNeedle>()
				|| type == ModContent.ProjectileType<VoidFissure>())
			{
				return true;
			}

			if (HasEidolon6 && (type == ModContent.ProjectileType<NaughtHeldSwing>()
				|| type == ModContent.ProjectileType<NaughtSlashVisual>()
				|| type == ModContent.ProjectileType<NaughtCycleSlash>()
				|| type == ModContent.ProjectileType<NaughtExWave>()))
			{
				return true;
			}

			return false;
		}

		public bool IsCurrentStrikeUltimate(Projectile proj = null) =>
			activeStrikeIsUltimate || IsUltimateProjectile(proj);

		/// <summary>
		/// Central hit-time modifier choke point for Naught attacks (StillWater, E1, E4, and KarmaSever DR peel).
		/// Invoked once by standard item/projectile ModifyHit hooks and once by StrikeNPC.
		/// </summary>
		public void ApplyNaughtHitModifiers(
			NPC target,
			ref NPC.HitModifiers modifiers,
			DamageClass damageType,
			Projectile sourceProj = null,
			int baseCritChanceForNative = 0)
		{
			if (suppressNaughtHitModifiers || target == null || !target.active || !WeepCompatibility.IsNaughtDamageClass(damageType))
			{
				CrimsonKnotGlobalNPC.ClearPendingWitheredDrPeel();
				return;
			}

			float pendingWitheredPeel = CrimsonKnotGlobalNPC.ConsumePendingWitheredDrPeel(target.whoAmI);

			if (IsStillWaterActive)
			{
				modifiers.CritDamage += 0.20f;
			}

			bool isUlt = IsCurrentStrikeUltimate(sourceProj);

			// E4 (Shrine in Flame): +8% Ultimate damage inside Nihility Domain (pure damage check, zero debuffs)
			if (HasEidolon4 && isUlt && NihilityDomainSystem.IsPositionInsideDomain(target.Center))
			{
				modifiers.FinalDamage *= 1.08f;
			}

			// Unified Boss DR peel: Karma Sever (10% on Ultimate strikes) + Withered River (10%) evaluated as single pass
			if (HasKarmaSever && isUlt && CrimsonKnotGlobalNPC.IsBossTarget(target))
			{
				if (pendingWitheredPeel > 0f)
				{
					float prevMult = WeepCompatibility.ComputeUnifiedDRPeelMultiplier(target, pendingWitheredPeel);
					float totalMult = WeepCompatibility.ComputeUnifiedDRPeelMultiplier(target, pendingWitheredPeel + 0.10f);
					if (prevMult > 0f && totalMult != prevMult)
					{
						modifiers.FinalDamage *= totalMult / prevMult;
					}
				}
				else
				{
					WeepCompatibility.ApplyUnifiedDRPeel(target, ref modifiers, 0.10f);
				}
			}

			// E1 (Silenced Sky): +8% True Melee Crit Chance against debuffed targets for native colliding hits
			if (!isInsideStrikeNPC
				&& HasEidolon1
				&& WeepCompatibility.IsTrueMeleeDamageClass(damageType)
				&& CrimsonKnotGlobalNPC.CountUniqueDebuffs(target) > 0
				&& damageType.UseStandardCritCalcs
				&& baseCritChanceForNative < 100)
			{
				float baseCritProb = Math.Clamp(baseCritChanceForNative / 100f, 0f, 0.99f);
				float extraRollChance = Math.Min(1f, 0.08f / (1f - baseCritProb));
				if (Main.rand.NextFloat() < extraRollChance)
				{
					modifiers.SetCrit();
				}
			}
		}

		/// <summary>
		/// Unified strike helper replacing raw SimpleStrikeNPC calls for Naught abilities and charms.
		/// Defaults to WeepCompatibility.TrueMelee, runs standard tModLoader hit-modifier hooks and standard crit rolls
		/// (so overcrit/crit-layer mods hook naturally via ModifyHitNPCWithProj / ModifyHitNPC / ToHitInfo),
		/// and prevents double-scaling or damage-share recursion.
		/// </summary>
		public int StrikeNPC(
			NPC target,
			int damage,
			int hitDirection = 0,
			bool? crit = null,
			float knockBack = 0f,
			DamageClass damageType = null,
			bool damageVariation = false,
			bool applyHitModifiers = true,
			bool allowDamageShare = true,
			Projectile sourceProj = null,
			HitModifierCallback modifyHit = null,
			bool noPlayerInteraction = false,
			bool isUltimate = false)
		{
			if (target == null || !target.active || target.friendly || damage <= 0)
				return 0;

			if (target.dontTakeDamage && target.type != NPCID.TargetDummy)
				return 0;

			if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI != Main.myPlayer)
				return 0;

			damageType ??= sourceProj?.DamageType ?? WeepCompatibility.TrueMelee;

			bool prevActiveStrikeIsUlt = activeStrikeIsUltimate;
			bool prevInsideStrikeNPC = isInsideStrikeNPC;
			activeStrikeIsUltimate = isUltimate || IsUltimateProjectile(sourceProj);
			isInsideStrikeNPC = true;

			int damageDone = 0;
			try
			{
				NPC.HitModifiers modifiers = target.GetIncomingStrikeModifiers(damageType, hitDirection);
				modifiers.ArmorPenetration += Player.GetTotalArmorPenetration(damageType);

				bool e1Applies = applyHitModifiers
					&& crit == null
					&& damageType.UseStandardCritCalcs
					&& HasEidolon1
					&& WeepCompatibility.IsTrueMeleeDamageClass(damageType)
					&& CrimsonKnotGlobalNPC.CountUniqueDebuffs(target) > 0;

				int critChance;
				if (sourceProj != null)
				{
					modifiers.ArmorPenetration += sourceProj.ArmorPenetration;
					if (sourceProj.CritChance <= 0)
					{
						sourceProj.CritChance = GetNaughtCritChance();
					}

					if (e1Applies)
					{
						sourceProj.CritChance += 8;
					}

					try
					{
						if (applyHitModifiers)
						{
							// CombinedHooks.ModifyHitNPCWithProj invokes PlayerLoader.ModifyHitNPCWithProj -> NaughtPlayer.ModifyHitNPCWithProj -> ApplyNaughtHitModifiers once.
							CombinedHooks.ModifyHitNPCWithProj(sourceProj, target, ref modifiers);
						}
						else
						{
							suppressNaughtHitModifiers = true;
							try
							{
								CombinedHooks.ModifyHitNPCWithProj(sourceProj, target, ref modifiers);
							}
							finally
							{
								suppressNaughtHitModifiers = false;
							}
						}

						critChance = sourceProj.CritChance;
					}
					finally
					{
						if (e1Applies)
						{
							sourceProj.CritChance -= 8;
						}
						CrimsonKnotGlobalNPC.ClearPendingWitheredDrPeel();
					}
				}
				else
				{
					try
					{
						if (applyHitModifiers)
						{
							PlayerLoader.ModifyHitNPCWithItem(Player, ResolveNaughtItem(), target, ref modifiers);
						}
						else
						{
							suppressNaughtHitModifiers = true;
							try
							{
								PlayerLoader.ModifyHitNPCWithItem(Player, ResolveNaughtItem(), target, ref modifiers);
							}
							finally
							{
								suppressNaughtHitModifiers = false;
							}
						}
					}
					finally
					{
						CrimsonKnotGlobalNPC.ClearPendingWitheredDrPeel();
					}

					critChance = GetNaughtCritChance() + (e1Applies ? 8 : 0);
				}

				bool resolvedCrit = crit ?? (damageType.UseStandardCritCalcs && Main.rand.Next(100) < critChance);
				if (crit == true)
				{
					modifiers.SetCrit();
				}

				modifyHit?.Invoke(ref modifiers);

				NPC.HitInfo hit = modifiers.ToHitInfo(damage, resolvedCrit, knockBack, damageVariation, Player.luck);
				damageDone = target.StrikeNPC(hit, fromNet: false, noPlayerInteraction: noPlayerInteraction);
				if (Main.netMode != NetmodeID.SinglePlayer)
				{
					NetMessage.SendStrikeNPC(target, in hit);
				}

				if (damageDone > 0)
				{
					MarkE2CombatActivity(target, damageDone);

					if (applyHitModifiers && WeepCompatibility.IsNaughtDamageClass(damageType))
					{
						TryApplyTideHitDebuffs(target);
					}

					// Runs ModPlayer.OnHitNPC (e.g. ImprovedCrits tier combat text indicators) without triggering OnHitByProjectile / ShadowMarionette recursion.
					PlayerLoader.OnHitNPC(Player, target, in hit, damageDone);
				}
			}
			finally
			{
				activeStrikeIsUltimate = prevActiveStrikeIsUlt;
				isInsideStrikeNPC = prevInsideStrikeNPC;
			}

			if (allowDamageShare && !isPropagatingMarionette && damageDone > 0 && HasShadowMarionette && WeepCompatibility.IsNaughtDamageClass(damageType))
			{
				RunMarionetteDamageShare(target, damageDone);
			}

			return damageDone;
		}

		public void RunMarionetteDamageShare(NPC sourceNpc, int damageDone)
		{
			if (isPropagatingMarionette || damageDone <= 0 || !HasShadowMarionette || sourceNpc == null || !sourceNpc.active)
				return;

			isPropagatingMarionette = true;
			try
			{
				CrimsonKnotGlobalNPC.PropagateMarionetteDamage(this, sourceNpc, damageDone);
			}
			finally
			{
				isPropagatingMarionette = false;
			}
		}

		public static void GetAbyssGripScaling(out float perDebuff, out int maxDebuffs)
		{
			if (WeepCompatibility.IsCalamityBalanceActive)
			{
				perDebuff = 0.06f; // +6% per unique debuff, up to +24%
				maxDebuffs = 4;
			}
			else
			{
				perDebuff = 0.12f; // +12% per unique debuff, up to +36%
				maxDebuffs = 3;
			}
		}

		public int CountUniqueDebuffsAcrossTarget(NPC target, int excludeBuffType = -1)
			=> CrimsonKnotGlobalNPC.CountUniqueDebuffs(target, excludeBuffType);

		public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
		{
			// When holding Naught and not swinging:
			// Position front hand to rest naturally on the hilt at the hip (samurai ready stance)
			if (Player.active && !Player.dead && !Player.ghost && !drawInfo.headOnlyRender
				&& Player.HeldItem?.type == ModContent.ItemType<Naught>()
				&& Player.itemAnimation == 0
				&& Player.ownedProjectileCounts[ModContent.ProjectileType<NaughtHeldSwing>()] == 0
				&& Player.ownedProjectileCounts[ModContent.ProjectileType<NaughtDash>()] == 0)
			{
				float armAngle = -Player.direction * 0.28f * Player.gravDir;
				Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, armAngle);
			}
		}

		public override void HideDrawLayers(PlayerDrawSet drawInfo)
		{
			if (!drawInfo.headOnlyRender
				&& IsUltVisualActive
				&& drawInfo.drawPlayer.whoAmI == Player.whoAmI
				&& Ult3DRenderHandle.IsUltShaderAndRenderHealthy)
			{
				PlayerDrawLayers.HeldItem.Hide();
			}
		}

		public override void TransformDrawData(ref PlayerDrawSet drawInfo)
		{
			if (!drawInfo.headOnlyRender
				&& IsUltVisualActive
				&& drawInfo.drawPlayer.whoAmI == Player.whoAmI
				&& !PlayerCloneRenderer.TryGetActiveTint(drawInfo.drawPlayer, out _))
			{
				Content.Projectiles.SlashedDreamUltimate ult = FindActiveUltimate();
				if (ult != null)
				{
					Vector2 sourceCenterScreen = Player.MountedCenter - Main.screenPosition;
					Vector2 targetWorld = Ult3DProjection.Project(ult.PlayerPos3D, ult.SafeFrameOrigin, ult.LockedFacingDir, out float perspScale);
					Vector2 targetCenterScreen = targetWorld - Main.screenPosition;

					Ult3DProjection.ComputeAnisotropicScale(
						perspScale,
						ult.PlayerWidthScale,
						ult.PlayerSquashY,
						ult.PlayerStretchDir2D,
						ult.PlayerStretchAmount,
						out float scaleX,
						out float scaleY);

					float farFactor = MathHelper.Clamp(-ult.PlayerPos3D.Z / 340f, 0f, 1f);
					Color depthTint = Color.Lerp(Color.White, new Color(155, 108, 138), farFactor * 0.55f);

					Ult3DProjection.TransformPlayerDrawData(
						ref drawInfo,
						sourceCenterScreen,
						targetCenterScreen,
						scaleX,
						scaleY,
						ult.PlayerLeanShear,
						ult.PlayerBodyTilt,
						depthTint,
						tintStrength: 0f,
						alpha: 1f,
						minOpaqueFloor: 0.72f);
				}
				return;
			}

			if (VoidPhasingTimer <= 0 || drawInfo.headOnlyRender || PlayerCloneRenderer.TryGetActiveTint(drawInfo.drawPlayer, out _))
				return;

			float pulse = 0.38f + MathF.Sin(Main.GlobalTimeWrappedHourly * 14f) * 0.06f;
			float alpha = VoidPhasingTimer < 15
				? MathHelper.Lerp(1f, pulse, VoidPhasingTimer / 15f)
				: pulse;

			Color ghostTint = new Color(175, 115, 255);
			float tintStrength = VoidPhasingTimer < 15
				? MathHelper.Lerp(0f, 0.45f, VoidPhasingTimer / 15f)
				: 0.45f;

			for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
			{
				DrawData data = drawInfo.DrawDataCache[i];
				Color src = data.color;
				Color blended = Color.Lerp(src, ghostTint, tintStrength);
				blended.A = src.A;
				data.color = blended * alpha;
				drawInfo.DrawDataCache[i] = data;
			}
		}
	}

	public sealed class UltCameraSystem : ModSystem
	{
		public override void ModifyTransformMatrix(ref Terraria.Graphics.SpriteViewMatrix Transform)
		{
			if (Main.dedServ || Main.gameMenu)
				return;

			Player localPlayer = Main.LocalPlayer;
			if (localPlayer == null || !localPlayer.active)
				return;

			if (!localPlayer.TryGetModPlayer<NaughtPlayer>(out var np))
				return;

			float zoom = np.UltCameraZoom;
			if (Math.Abs(zoom - 1f) > 0.0005f)
			{
				Transform.Zoom *= zoom;
			}
		}

		public override void OnWorldUnload()
		{
			if (!Main.dedServ && Main.LocalPlayer != null && Main.LocalPlayer.TryGetModPlayer<NaughtPlayer>(out var np))
			{
				np.ResetUltVisualAndCameraState(immediateCamera: true);
			}
		}
	}
}
