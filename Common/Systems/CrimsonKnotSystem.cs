using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.GlobalNPCs;
using WeepForTheDeparted.Common.Players;

namespace WeepForTheDeparted.Common.Systems
{
	/// <summary>
	/// ModSystem that ensures Crimson Knot debuff tracking, pop animation timers,
	/// and ambient particles update reliably for ALL NPCs every frame.
	/// Running in PostUpdateNPCs guarantees it cannot be skipped or bypassed by any
	/// NPCLoader.NPCAI or NPCOverride hooks (such as Calamity Overhaul's Brutal NPCs).
	/// </summary>
	public class CrimsonKnotSystem : ModSystem
	{
		public override void PostUpdateNPCs()
		{
			if (Main.netMode == NetmodeID.Server)
				return;

			Player localPlayer = Main.LocalPlayer;
			if (localPlayer == null || !localPlayer.active || localPlayer.dead)
				return;

			var naughtPlayer = localPlayer.GetModPlayer<NaughtPlayer>();
			bool hasNaught = naughtPlayer != null && naughtPlayer.IsNaughtEquipped;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active)
					continue;

				if (npc.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData))
				{
					knotData.UpdateFromSystem(npc, naughtPlayer, localPlayer, hasNaught);
				}
			}
		}
	}
}
