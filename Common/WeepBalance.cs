using System.Collections.Immutable;

namespace WeepForTheDeparted.Common
{
	/// <summary>
	/// Immutable balance profile holding all mode-dependent ratios for Naught,
	/// basekit combat, all 18 Nihility Charms, and Eidolons (E1-E6 + EX).
	/// </summary>
	public sealed class NaughtBalanceProfile
	{
		// Weapon Scaling
		public int BaseSwordDamage { get; init; }
		public float DamagePerBoss { get; init; }
		public float CritPerBoss { get; init; }
		public bool UseCalamityStageMultiplier { get; init; }

		// Basekit Combat
		public ImmutableArray<float> ComboBaseReachPx { get; init; }
		public int DashCooldownTicks { get; init; }
		public int DashLingerIFrames { get; init; }
		public float DashMinDistPx { get; init; }
		public float DashMaxDistPx { get; init; }
		public int BasekitMaxClones { get; init; }
		public int GlobalDebuffDreamCooldownTicks { get; init; }
		public int UltimateRecastCooldownTicks { get; init; }
		public float RainbladeBaseMult { get; init; }
		public float RainbladeKnotMult { get; init; }
		public float ResurgeBaseMult { get; init; }
		public float ResurgeKnotMult { get; init; }

		// Soul Charms (Slot 0)
		public int RedOniOpenerFloor { get; init; }
		public int RedOniMilestoneDream { get; init; }
		public int NamelessDecoyBonusSlashCap { get; init; }
		public float FleetingStepDashCdMult { get; init; }
		public int FleetingStepSlowTicks { get; init; }
		public int FleetingStepCooldownTicks { get; init; }
		public float MirageWeftDashCdMult { get; init; }
		public float MirageWeftDamageMult { get; init; }
		public int MirageWeftHitCooldownTicks { get; init; }
		public float RainleafLethalHpRatio { get; init; }
		public int RainleafInvulnTicks { get; init; }
		public float RainleafSpeedMult { get; init; }
		public int RainleafDreamGrant { get; init; }
		public int RainleafCooldownTicks { get; init; }
		public float StillWaterCritChance { get; init; }
		public float StillWaterCritDamage { get; init; }

		// Tide Charms (Slot 1)
		public float AbyssGripPerDebuff { get; init; }
		public int AbyssGripMaxDebuffs { get; init; }
		public float BloodLotusBaseMult { get; init; }
		public float BloodLotusKnotMult { get; init; }
		public float BloodLotusMaxRadiusPx { get; init; }
		public int BloodLotusGlobalHitCooldownTicks { get; init; }
		public float SiphonHealPerKnot { get; init; }
		public float WitheredRiverBossDurationMult { get; init; }
		public float WitheredRiverDrPeel { get; init; }
		public float WitheredRiverVanillaDamageMult { get; init; }
		public float MarionetteSharePerClone { get; init; }
		public float MarionetteShareCap { get; init; }
		public float MarionetteUltShareScale { get; init; }
		public ImmutableArray<float> CrucibleDotAmps { get; init; }
		public float CruciblePopRatio { get; init; }
		public float CrucibleWeaponCapMult { get; init; }
		public float CrucibleBossHpCapRatio { get; init; }
		public float CrucibleFloorPerDebuff { get; init; }

		// Ruin Charms (Slot 2)
		public float ThunderCoreBoltMult { get; init; }
		public float ThunderCoreBuffDamage { get; init; }
		public int ThunderCoreBuffTicks { get; init; }
		public float BladesongNeedleMult { get; init; }
		public int BladesongMaxHitsPerNpc { get; init; }
		public int BladesongHitCooldownUpdates { get; init; }
		public int BladesongFlightUpdates { get; init; }
		public float VoidHorizonDamageMult { get; init; }
		public float VoidHorizonRadiusPx { get; init; }
		public float VoidHorizonFallbackDriftSpeed { get; init; }
		public float KarmaSeverUltDefPen { get; init; }
		public float KarmaSeverBossDrPeel { get; init; }
		public float KarmaSeverVanillaBossMult { get; init; }
		public float KarmaSeverResurgeBonusMult { get; init; }
		public float KarmaSeverE6Scale { get; init; }
		public int EmancipatorMaxDreamRefund { get; init; }
		public float EmancipatorCritBonus { get; init; }
		public float SyncMirrorRainbladeMult { get; init; }
		public float SyncCrossSlashMult { get; init; }
		public float SyncSoftCapClones { get; init; }

		// Eidolons (E1-E6 + EX)
		public float E1CritBonus { get; init; }
		public int E2PassiveDreamIntervalTicks { get; init; }
		public float E3RainbladeBaseMult { get; init; }
		public float E3RainbladeKnotMult { get; init; }
		public float E5ResurgeBaseMult { get; init; }
		public float E5ResurgeKnotMult { get; init; }
		public float ExWaveDamageMult { get; init; }

		public static NaughtBalanceProfile Vanilla => WeepBalance.Vanilla;
		public static NaughtBalanceProfile Calamity => WeepBalance.Calamity;
		public static NaughtBalanceProfile Active => WeepBalance.Active;
	}

	/// <summary>
	/// Single source of truth for Vanilla and Calamity balance profiles.
	/// </summary>
	public static class WeepBalance
	{
		public static readonly NaughtBalanceProfile Vanilla = new()
		{
			// Weapon Scaling
			BaseSwordDamage = 14,
			DamagePerBoss = 4.0f,
			CritPerBoss = 0.3f,
			UseCalamityStageMultiplier = false,

			// Basekit Combat
			ComboBaseReachPx = ImmutableArray.Create(102f, 109f, 113f, 129f),
			DashCooldownTicks = 120,
			DashLingerIFrames = 2,
			DashMinDistPx = 96f,
			DashMaxDistPx = 208f,
			BasekitMaxClones = 6,
			GlobalDebuffDreamCooldownTicks = 90,
			UltimateRecastCooldownTicks = 720,
			RainbladeBaseMult = 1.40f,
			RainbladeKnotMult = 0.45f,
			ResurgeBaseMult = 5.00f,
			ResurgeKnotMult = 0.30f,

			// Soul Charms
			RedOniOpenerFloor = 4,
			RedOniMilestoneDream = 1,
			NamelessDecoyBonusSlashCap = 3,
			FleetingStepDashCdMult = 0.85f,
			FleetingStepSlowTicks = 150,
			FleetingStepCooldownTicks = 1800,
			MirageWeftDashCdMult = 0.80f,
			MirageWeftDamageMult = 0.25f,
			MirageWeftHitCooldownTicks = 60,
			RainleafLethalHpRatio = 0f,
			RainleafInvulnTicks = 105,
			RainleafSpeedMult = 1.30f,
			RainleafDreamGrant = 1,
			RainleafCooldownTicks = 7200,
			StillWaterCritChance = 15f,
			StillWaterCritDamage = 0.15f,

			// Tide Charms
			AbyssGripPerDebuff = 0.06f,
			AbyssGripMaxDebuffs = 3,
			BloodLotusBaseMult = 0.85f,
			BloodLotusKnotMult = 0.10f,
			BloodLotusMaxRadiusPx = 184f,
			BloodLotusGlobalHitCooldownTicks = 24,
			SiphonHealPerKnot = 0.015f,
			WitheredRiverBossDurationMult = 1.50f,
			WitheredRiverDrPeel = 0.14f,
			WitheredRiverVanillaDamageMult = 1.14f,
			MarionetteSharePerClone = 0.10f,
			MarionetteShareCap = 0.35f,
			MarionetteUltShareScale = 0.50f,
			CrucibleDotAmps = ImmutableArray.Create(1.75f, 2.00f, 2.25f),
			CruciblePopRatio = 0.50f,
			CrucibleWeaponCapMult = 5.0f,
			CrucibleBossHpCapRatio = 0.05f,
			CrucibleFloorPerDebuff = 0.30f,

			// Ruin Charms
			ThunderCoreBoltMult = 0.45f,
			ThunderCoreBuffDamage = 0.10f,
			ThunderCoreBuffTicks = 600,
			BladesongNeedleMult = 0.45f,
			BladesongMaxHitsPerNpc = 2,
			BladesongHitCooldownUpdates = 42,
			BladesongFlightUpdates = 180,
			VoidHorizonDamageMult = 0.45f,
			VoidHorizonRadiusPx = 120f,
			VoidHorizonFallbackDriftSpeed = 0f,
			KarmaSeverUltDefPen = 0.35f,
			KarmaSeverBossDrPeel = 0.15f,
			KarmaSeverVanillaBossMult = 1.15f,
			KarmaSeverResurgeBonusMult = 1.15f,
			KarmaSeverE6Scale = 0.50f,
			EmancipatorMaxDreamRefund = 3,
			EmancipatorCritBonus = 15f,
			SyncMirrorRainbladeMult = 0.35f,
			SyncCrossSlashMult = 0.95f,
			SyncSoftCapClones = 4.5f,

			// Eidolons
			E1CritBonus = 6f,
			E2PassiveDreamIntervalTicks = 720,
			E3RainbladeBaseMult = 1.50f,
			E3RainbladeKnotMult = 0.50f,
			E5ResurgeBaseMult = 5.35f,
			E5ResurgeKnotMult = 0.35f,
			ExWaveDamageMult = 0.40f
		};

		public static readonly NaughtBalanceProfile Calamity = new()
		{
			// Weapon Scaling
			BaseSwordDamage = 16,
			DamagePerBoss = 3.0f,
			CritPerBoss = 0.2f,
			UseCalamityStageMultiplier = true,

			// Basekit Combat
			ComboBaseReachPx = ImmutableArray.Create(113f, 120f, 124f, 142f),
			DashCooldownTicks = 90,
			DashLingerIFrames = 5,
			DashMinDistPx = 120f,
			DashMaxDistPx = 256f,
			BasekitMaxClones = 6,
			GlobalDebuffDreamCooldownTicks = 150,
			UltimateRecastCooldownTicks = 840,
			RainbladeBaseMult = 1.60f,
			RainbladeKnotMult = 0.55f,
			ResurgeBaseMult = 5.80f,
			ResurgeKnotMult = 0.35f,

			// Soul Charms
			RedOniOpenerFloor = 5,
			RedOniMilestoneDream = 2,
			NamelessDecoyBonusSlashCap = 4,
			FleetingStepDashCdMult = 0.80f,
			FleetingStepSlowTicks = 150,
			FleetingStepCooldownTicks = 2100,
			MirageWeftDashCdMult = 0.75f,
			MirageWeftDamageMult = 0.35f,
			MirageWeftHitCooldownTicks = 51,
			RainleafLethalHpRatio = 0.10f,
			RainleafInvulnTicks = 120,
			RainleafSpeedMult = 1.35f,
			RainleafDreamGrant = 2,
			RainleafCooldownTicks = 6300,
			StillWaterCritChance = 15f,
			StillWaterCritDamage = 0.18f,

			// Tide Charms
			AbyssGripPerDebuff = 0.04f,
			AbyssGripMaxDebuffs = 4,
			BloodLotusBaseMult = 1.00f,
			BloodLotusKnotMult = 0.12f,
			BloodLotusMaxRadiusPx = 208f,
			BloodLotusGlobalHitCooldownTicks = 24,
			SiphonHealPerKnot = 0.015f,
			WitheredRiverBossDurationMult = 1.40f,
			WitheredRiverDrPeel = 0.10f,
			WitheredRiverVanillaDamageMult = 1.10f,
			MarionetteSharePerClone = 0.10f,
			MarionetteShareCap = 0.40f,
			MarionetteUltShareScale = 0.50f,
			CrucibleDotAmps = ImmutableArray.Create(1.25f, 1.40f, 1.55f),
			CruciblePopRatio = 0.40f,
			CrucibleWeaponCapMult = 6.0f,
			CrucibleBossHpCapRatio = 0.04f,
			CrucibleFloorPerDebuff = 0.35f,

			// Ruin Charms
			ThunderCoreBoltMult = 0.60f,
			ThunderCoreBuffDamage = 0.12f,
			ThunderCoreBuffTicks = 720,
			BladesongNeedleMult = 0.55f,
			BladesongMaxHitsPerNpc = 2,
			BladesongHitCooldownUpdates = 42,
			BladesongFlightUpdates = 180,
			VoidHorizonDamageMult = 0.60f,
			VoidHorizonRadiusPx = 136f,
			VoidHorizonFallbackDriftSpeed = 0.80f,
			KarmaSeverUltDefPen = 0.40f,
			KarmaSeverBossDrPeel = 0.12f,
			KarmaSeverVanillaBossMult = 1.12f,
			KarmaSeverResurgeBonusMult = 1.15f,
			KarmaSeverE6Scale = 0.50f,
			EmancipatorMaxDreamRefund = 4,
			EmancipatorCritBonus = 20f,
			SyncMirrorRainbladeMult = 0.45f,
			SyncCrossSlashMult = 1.15f,
			SyncSoftCapClones = 5.0f,

			// Eidolons
			E1CritBonus = 8f,
			E2PassiveDreamIntervalTicks = 600,
			E3RainbladeBaseMult = 1.70f,
			E3RainbladeKnotMult = 0.60f,
			E5ResurgeBaseMult = 6.20f,
			E5ResurgeKnotMult = 0.40f,
			ExWaveDamageMult = 0.50f
		};

		public static NaughtBalanceProfile Active => WeepCompatibility.IsCalamityBalanceActive ? Calamity : Vanilla;
	}
}
