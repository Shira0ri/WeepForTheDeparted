using Terraria;
using Terraria.ModLoader;

namespace WeepForTheDeparted.Content.Buffs
{
	/// <summary>
	/// Applied by Naught hits while Grip of the Abyss is equipped.
	/// Counts as a debuff for Slashed Dream / Crimson Knot triggers and increases True Melee damage received per unique debuff.
	/// </summary>
	public class AbyssGripDebuff : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.pvpBuff[Type] = true;
			Main.buffNoSave[Type] = true;
		}
	}
}
