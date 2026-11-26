using System;
using Terraria;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;

namespace WeepForTheDeparted.Content.Buffs
{
	public class StillWaterFocusBuff : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			NaughtBalanceProfile profile = WeepBalance.Active;
			int critChance = (int)MathF.Round(profile.StillWaterCritChance);
			int critDmg = (int)MathF.Round(profile.StillWaterCritDamage * 100f);
			tip = $"Unbroken poise sharpens Naught's edge (+{critChance}% True Melee Crit Chance and +{critDmg}% Crit Damage).";
		}
	}
}
