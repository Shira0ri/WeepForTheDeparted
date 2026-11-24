using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using WeepForTheDeparted.Common.Configs;

namespace WeepForTheDeparted.Common.Systems
{
	public class BossTrackerSystem : ModSystem
	{
		public static HashSet<string> DefeatedBosses { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public override void ClearWorld()
		{
			DefeatedBosses.Clear();
		}

		public static void RegisterDefeatedBoss(NPC npc)
		{
			if (npc == null)
				return;

			bool universal = WeepConfig.Instance?.UniversalBossScaling ?? false;
			bool includeCalamity = WeepConfig.Instance?.IncludeCalamityBosses ?? true;

			if (npc.ModNPC != null)
			{
				bool isCalamity = npc.ModNPC.Mod?.Name == "CalamityMod";
				if (!isCalamity && !universal)
					return;

				if (isCalamity && !includeCalamity)
					return;
			}

			string key;
			if (npc.ModNPC != null)
			{
				key = "Mod:" + npc.ModNPC.FullName;
			}
			else
			{
				key = "Vanilla:" + npc.TypeName;
			}

			DefeatedBosses.Add(key);
		}

		public static int GetTotalBossesDefeated()
		{
			HashSet<string> uniqueBosses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			// Add vanilla bosses from standard world progress flags
			AddVanillaBossFlags(uniqueBosses);

			// Add Calamity bosses if enabled and Calamity Mod is present
			bool includeCalamity = WeepConfig.Instance?.IncludeCalamityBosses ?? true;
			if (includeCalamity)
			{
				AddCalamityBossFlags(uniqueBosses);
			}

			// Add other modded bosses only if universal boss scaling is explicitly enabled
			bool universal = WeepConfig.Instance?.UniversalBossScaling ?? false;
			if (universal)
			{
				foreach (string key in DefeatedBosses)
				{
					uniqueBosses.Add(key);
				}
			}

			return uniqueBosses.Count;
		}

		public static float GetSwordBonusDamage()
		{
			int bossCount = GetTotalBossesDefeated();
			float perBoss = WeepConfig.Instance?.DamagePerBoss ?? 5.0f;
			return Math.Max(0f, bossCount * perBoss);
		}

		public static float GetSwordBonusCrit()
		{
			int bossCount = GetTotalBossesDefeated();
			float perBoss = WeepConfig.Instance?.CritPerBoss ?? 0.4f;
			return Math.Max(0f, bossCount * perBoss);
		}

		public static string GetProgressionStageName()
		{
			int bossCount = GetTotalBossesDefeated();

			if (NPC.downedMoonlord)
			{
				if (bossCount >= 25)
					return "Calamitous Void / Cosmic Nihility";
				return "Post-Moon Lord";
			}

			if (NPC.downedAncientCultist)
				return "Lunar Celestial Event";

			if (NPC.downedPlantBoss)
				return "Jungle & Temple Depths";

			if (NPC.downedMechBossAny)
				return "Mechanical Era";

			if (Main.hardMode)
				return "Early Hardmode";

			if (NPC.downedBoss3 || NPC.downedBoss2 || NPC.downedQueenBee || NPC.downedDeerclops)
				return "Late Pre-Hardmode";

			if (NPC.downedBoss1 || NPC.downedSlimeKing)
				return "Early Pre-Hardmode";

			return "Pre-Boss Journey";
		}

		private static void AddVanillaBossFlags(HashSet<string> set)
		{
			if (NPC.downedSlimeKing) set.Add("Vanilla:KingSlime");
			if (NPC.downedBoss1) set.Add("Vanilla:EyeOfCthulhu");
			if (NPC.downedBoss2) set.Add("Vanilla:EvilBoss");
			if (NPC.downedQueenBee) set.Add("Vanilla:QueenBee");
			if (NPC.downedBoss3) set.Add("Vanilla:Skeletron");
			if (NPC.downedDeerclops) set.Add("Vanilla:Deerclops");
			if (Main.hardMode) set.Add("Vanilla:WallOfFlesh");
			if (NPC.downedQueenSlime) set.Add("Vanilla:QueenSlime");
			if (NPC.downedMechBoss1) set.Add("Vanilla:TheDestroyer");
			if (NPC.downedMechBoss2) set.Add("Vanilla:TheTwins");
			if (NPC.downedMechBoss3) set.Add("Vanilla:SkeletronPrime");
			if (NPC.downedPlantBoss) set.Add("Vanilla:Plantera");
			if (NPC.downedGolemBoss) set.Add("Vanilla:Golem");
			if (NPC.downedFishron) set.Add("Vanilla:DukeFishron");
			if (NPC.downedEmpressOfLight) set.Add("Vanilla:EmpressOfLight");
			if (NPC.downedAncientCultist) set.Add("Vanilla:LunaticCultist");
			if (NPC.downedMoonlord) set.Add("Vanilla:MoonLord");

			// Invasions and Event bosses
			if (NPC.downedHalloweenTree) set.Add("Vanilla:MourningWood");
			if (NPC.downedHalloweenKing) set.Add("Vanilla:Pumpking");
			if (NPC.downedChristmasTree) set.Add("Vanilla:Everscream");
			if (NPC.downedChristmasSantank) set.Add("Vanilla:SantaNK1");
			if (NPC.downedChristmasIceQueen) set.Add("Vanilla:IceQueen");
			if (NPC.downedPirates) set.Add("Vanilla:FlyingDutchman");
			if (NPC.downedMartians) set.Add("Vanilla:MartianSaucer");
			if (NPC.downedTowerSolar) set.Add("Vanilla:SolarPillar");
			if (NPC.downedTowerVortex) set.Add("Vanilla:VortexPillar");
			if (NPC.downedTowerNebula) set.Add("Vanilla:NebulaPillar");
			if (NPC.downedTowerStardust) set.Add("Vanilla:StardustPillar");

			// Old One's Army
			if (DD2Event.DownedInvasionT1) set.Add("Vanilla:DarkMage");
			if (DD2Event.DownedInvasionT2) set.Add("Vanilla:Ogre");
			if (DD2Event.DownedInvasionT3) set.Add("Vanilla:Betsy");
		}

		private static void AddCalamityBossFlags(HashSet<string> set)
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity))
				return;

			try
			{
				Type downedSystem = calamity.Code?.GetType("CalamityMod.DownedBossSystem");
				if (downedSystem != null)
				{
					FieldInfo[] fields = downedSystem.GetFields(BindingFlags.Public | BindingFlags.Static);
					foreach (FieldInfo field in fields)
					{
						if (field.FieldType == typeof(bool) && field.Name.StartsWith("downed", StringComparison.OrdinalIgnoreCase))
						{
							object val = field.GetValue(null);
							if (val is bool isDowned && isDowned)
							{
								set.Add("Calamity:" + field.Name);
							}
						}
					}
				}
			}
			catch
			{
				// Fail silently if Calamity internal structure differs
			}
		}

		public override void SaveWorldData(TagCompound tag)
		{
			tag["DefeatedBosses"] = DefeatedBosses.ToList();
		}

		public override void LoadWorldData(TagCompound tag)
		{
			if (tag.ContainsKey("DefeatedBosses"))
			{
				DefeatedBosses = new HashSet<string>(tag.GetList<string>("DefeatedBosses"), StringComparer.OrdinalIgnoreCase);
			}
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(DefeatedBosses.Count);
			foreach (string boss in DefeatedBosses)
			{
				writer.Write(boss);
			}
		}

		public override void NetReceive(BinaryReader reader)
		{
			DefeatedBosses.Clear();
			int count = reader.ReadInt32();
			for (int i = 0; i < count; i++)
			{
				DefeatedBosses.Add(reader.ReadString());
			}
		}
	}
}
