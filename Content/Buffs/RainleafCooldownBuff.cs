using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace WeepForTheDeparted.Content.Buffs
{
	public class RainleafCooldownBuff : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;
			BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
		}
	}
}
