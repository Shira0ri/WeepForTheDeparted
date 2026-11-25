using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace WeepForTheDeparted.Content.Buffs
{
	public class WitheredRiverDebuff : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.pvpBuff[Type] = true;
			Main.buffNoSave[Type] = true;
			BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
			BuffID.Sets.LongerExpertDebuff[Type] = false;
		}
	}
}
