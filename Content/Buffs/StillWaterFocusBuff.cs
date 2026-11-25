using Terraria;
using Terraria.ModLoader;

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
	}
}
