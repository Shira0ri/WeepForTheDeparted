using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace WeepForTheDeparted.Content.Buffs
{
	/// <summary>
	/// Applied to enemies inside a Shadow Crucible clone's 288f px aura.
	/// Marks targets storing amplified DoT pressure until the synchronized clone cycle detonates.
	/// </summary>
	public class ShadowCrucibleDebuff : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.pvpBuff[Type] = true;
			Main.buffNoSave[Type] = true;
			BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
		}
	}
}
