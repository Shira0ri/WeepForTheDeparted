using Terraria;
using Terraria.ModLoader;

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
	}
}
