using InnoVault.GameSystem;
using Terraria;
using WeepForTheDeparted.Common.Systems;

namespace WeepForTheDeparted.Common.GlobalNPCs
{
	/// <summary>
	/// Universal InnoVault NPCOverride to freeze NPCs and bosses that override vanilla AI
	/// via InnoVault (such as Calamity Overhaul's Brutal NPCs).
	/// UniversalInstances run BEFORE any specific boss aiOverrides in InnoVault's OnNPCAIHook.
	/// Returning false here completely blocks the reworked boss's state machine during time stop.
	/// </summary>
	public class NihilityWorldFreezeOverNPC : NPCOverride
	{
		public override int TargetID => -1;

		public override bool AI()
		{
			if (WorldFreezeSystem.IsActive)
			{
				if (npc != null && npc.active)
				{
					if (npc.TryGetGlobalNPC<WorldFreezeNPC>(out var freezeData))
					{
						freezeData.Freeze(npc);
					}
					else
					{
						npc.velocity = Microsoft.Xna.Framework.Vector2.Zero;
					}
				}
				return false; // Blocks all subsequent Brutal boss AI and vanilla AI
			}

			return true;
		}
	}
}
