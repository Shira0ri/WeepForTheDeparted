using System;
using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace WeepForTheDeparted.Common.Configs
{
	public enum WeepBalanceMode
	{
		Auto,
		Calamity,
		Vanilla
	}

	public class WeepConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		public static WeepConfig Instance => ModContent.GetInstance<WeepConfig>();

		[Header("WeaponScaling")]
		/// <summary>
		/// When false (default), WeepBalance.Active controls base sword damage, per-boss damage,
		/// per-boss crit, and Calamity stage multipliers. Enable to use the manual slider overrides below.
		/// </summary>
		[DefaultValue(false)]
		public bool OverrideWeaponScaling { get; set; } = false;

		[DefaultValue(25)]
		[Range(1, 1000)]
		public int BaseSwordDamage { get; set; } = 25;

		[DefaultValue(5.0f)]
		[Range(0f, 100f)]
		[Increment(0.5f)]
		public float DamagePerBoss { get; set; } = 5.0f;

		[DefaultValue(0.4f)]
		[Range(0f, 10f)]
		[Increment(0.1f)]
		public float CritPerBoss { get; set; } = 0.4f;

		[Header("BossTracking")]
		[DefaultValue(false)]
		public bool UniversalBossScaling { get; set; } = false;

		[DefaultValue(true)]
		public bool IncludeCalamityBosses { get; set; } = true;

		[Header("GameplayBalance")]
		/// <summary>
		/// Governs the unified Vanilla/Calamity Balance Profile (weapon scaling, basekit combat,
		/// all 18 Nihility Charms, and Eidolons E1-EX). Auto detects Calamity Mod.
		/// </summary>
		[DefaultValue(WeepBalanceMode.Auto)]
		public WeepBalanceMode BalanceMode { get; set; } = WeepBalanceMode.Auto;

		[DefaultValue(false)]
		public bool UnlockAllInscriptionsAndEidolons { get; set; } = false;
	}
}
