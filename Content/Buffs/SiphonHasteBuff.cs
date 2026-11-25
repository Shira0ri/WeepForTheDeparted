using Terraria;
using Terraria.ModLoader;

namespace WeepForTheDeparted.Content.Buffs
{
	public class SiphonHasteBuff : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;
		}
	}
}
