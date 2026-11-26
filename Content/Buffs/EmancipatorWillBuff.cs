using System;
using Terraria;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;

namespace WeepForTheDeparted.Content.Buffs
{
	public class EmancipatorWillBuff : ModBuff
	{
		public override string Texture => "WeepForTheDeparted/Content/Buffs/StillWaterFocusBuff";

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			int critBonus = (int)MathF.Round(WeepBalance.Active.EmancipatorCritBonus);
			tip = $"Post-Ultimate surge grants +{critBonus}% True Melee Crit Chance to Naught and its echoes.";
		}
	}
}
