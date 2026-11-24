using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.Systems;

namespace WeepForTheDeparted.Common.GlobalNPCs
{
	public class WorldFreezeNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		public bool wasFrozen = false;
		public Vector2 frozenPosition = Vector2.Zero;
		public Vector2 resumeVelocity = Vector2.Zero;
		public Rectangle frozenFrame;
		public int frozenDirection;
		public int frozenSpriteDirection;
		public float frozenRotation;

		public void Freeze(NPC npc)
		{
			if (!wasFrozen)
			{
				wasFrozen = true;
				frozenPosition = npc.position;
				resumeVelocity = npc.velocity;
				frozenFrame = npc.frame;
				frozenDirection = npc.direction;
				frozenSpriteDirection = npc.spriteDirection;
				frozenRotation = npc.rotation;
			}

			npc.position = frozenPosition;
			npc.velocity = Vector2.Zero;
			npc.frame = frozenFrame;
			npc.direction = frozenDirection;
			npc.spriteDirection = frozenSpriteDirection;
			npc.rotation = frozenRotation;

			// Prevent debuffs and lifespan from prematurely expiring during the time stop
			if (npc.timeLeft < int.MaxValue)
				npc.timeLeft++;

			for (int i = 0; i < NPC.maxBuffs; i++)
			{
				if (npc.buffType[i] > 0 && npc.buffTime[i] > 0 && npc.buffTime[i] < int.MaxValue)
				{
					npc.buffTime[i]++;
				}
			}
		}

		public void Unfreeze(NPC npc)
		{
			if (wasFrozen)
			{
				wasFrozen = false;
				npc.velocity = resumeVelocity;
			}
		}

		public override bool PreAI(NPC npc)
		{
			if (WorldFreezeSystem.IsActive)
			{
				Freeze(npc);
				return false; // Suppress NPC AI and movement
			}
			else if (wasFrozen)
			{
				Unfreeze(npc);
			}

			return true;
		}

		public override void PostAI(NPC npc)
		{
			if (WorldFreezeSystem.IsActive && wasFrozen)
			{
				npc.position = frozenPosition;
				npc.velocity = Vector2.Zero;
				npc.frame = frozenFrame;
				npc.direction = frozenDirection;
				npc.spriteDirection = frozenSpriteDirection;
				npc.rotation = frozenRotation;
			}
		}

		public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
		{
			// Frozen enemies cannot deal contact damage
			if (WorldFreezeSystem.IsActive)
				return false;
			return base.CanHitPlayer(npc, target, ref cooldownSlot);
		}

		public override void UpdateLifeRegen(NPC npc, ref int damage)
		{
			// Frozen enemies do not tick DoT damage during time stop
			if (WorldFreezeSystem.IsActive)
			{
				npc.lifeRegen = 0;
				damage = 0;
			}
		}
	}
}
