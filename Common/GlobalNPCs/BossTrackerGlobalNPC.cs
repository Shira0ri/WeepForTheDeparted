using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.Systems;

namespace WeepForTheDeparted.Common.GlobalNPCs
{
	public class BossTrackerGlobalNPC : GlobalNPC
	{
		public override void OnKill(NPC npc)
		{
			if (npc == null)
				return;

			if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] ||
			    npc.type == NPCID.EaterofWorldsHead ||
			    npc.type == NPCID.EaterofWorldsBody ||
			    npc.type == NPCID.EaterofWorldsTail)
			{
				BossTrackerSystem.RegisterDefeatedBoss(npc);
			}
		}
	}
}
