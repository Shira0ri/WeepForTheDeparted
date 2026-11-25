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
		public static bool DefeatedPostMoonLordBoss { get; private set; } = false;

		private static readonly string[] CalamityPostMoonLordFlags =
		{
			"downedGuardians",
			"downedDragonfolly",
			"downedProvidence",
			"downedCeaselessVoid",
			"downedStormWeaver",
			"downedSignus",
			"downedPolterghast",
			"downedBoomerDuke",
			"downedDoG",
			"downedYharon",
			"downedExoMechs",
			"downedCalamitas",
			"downedPrimordialWyrm",
			"downedBossRush"
		};

		public override void ClearWorld()
		{
			DefeatedBosses.Clear();
			DefeatedPostMoonLordBoss = false;
		}

		public static bool HasPostMoonLordContentMod =>
			WeepCompatibility.HasCalamity
			|| ModLoader.HasMod("CalamityMod")
			|| ModLoader.HasMod("FargowiltasSouls")
			|| ModLoader.HasMod("ThoriumMod")
			|| ModLoader.HasMod("StarsAbove")
			|| ModLoader.HasMod("CatalystMod")
			|| ModLoader.HasMod("NoxusBoss")
			|| ModLoader.HasMod("Redemption");

		public static bool HasDefeatedPostMoonLordBoss
		{
			get
			{
				if (DefeatedPostMoonLordBoss)
					return true;

				if (WeepCompatibility.HasCalamity && HasAnyCalamityPostMoonLordBossDowned())
					return true;

				return false;
			}
		}

		public static void MarkPostMoonLordBossDefeated()
		{
			if (DefeatedPostMoonLordBoss)
				return;

			DefeatedPostMoonLordBoss = true;
			if (Main.netMode == NetmodeID.Server)
			{
				NetMessage.SendData(MessageID.WorldData);
			}
		}

		private static bool HasAnyCalamityPostMoonLordBossDowned()
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity))
				return false;

			try
			{
				Type downedSystem = calamity.Code?.GetType("CalamityMod.DownedBossSystem");
				if (downedSystem == null)
					return false;

				for (int i = 0; i < CalamityPostMoonLordFlags.Length; i++)
				{
					FieldInfo field = downedSystem.GetField(CalamityPostMoonLordFlags[i], BindingFlags.Public | BindingFlags.Static);
					if (field != null && field.FieldType == typeof(bool) && field.GetValue(null) is bool isDowned && isDowned)
					{
						return true;
					}
				}
			}
			catch
			{
				// Fail silently if Calamity internal structure differs
			}

			return false;
		}

		public static bool IsCharmUnlocked(string charmId)
		{
			if (string.IsNullOrEmpty(charmId))
				return true;

			if (ModContent.GetInstance<WeepConfig>()?.UnlockAllInscriptionsAndEidolons == true)
				return true;

			return charmId switch
			{
				// Starters
				"RedOni" or "AbyssGrip" or "ThunderCore" => true,

				// Pre-Hardmode
				"NamelessDecoy" => NPC.downedSlimeKing,
				"FleetingStep" => NPC.downedBoss1,
				"BloodLotus" => NPC.downedBoss2,
				"DepartedSiphon" => NPC.downedQueenBee,
				"BladesongRain" => NPC.downedBoss3,
				"WitheredRiver" => Main.hardMode,

				// Early-Mid Hardmode
				"VoidHorizon" => NPC.downedQueenSlime,
				"MirageWeft" => NPC.downedMechBoss1,
				"ShadowMarionette" => NPC.downedMechBoss2,
				"KarmaSever" => NPC.downedMechBoss3,

				// Late Hardmode
				"Rainleaf" => NPC.downedPlantBoss,
				"ShadowCrucible" => NPC.downedGolemBoss,
				"EmancipatorWill" => NPC.downedFishron,
				"StillWater" => NPC.downedEmpressOfLight,
				"SynchronizedExecution" => NPC.downedAncientCultist,

				_ => false
			};
		}

		public static bool IsEidolonUnlocked(int index)
		{
			if (ModContent.GetInstance<WeepConfig>()?.UnlockAllInscriptionsAndEidolons == true)
				return true;

			return index switch
			{
				0 => NPC.downedBoss2,        // E1: EoW / BoC
				1 => Main.hardMode,          // E2: Wall of Flesh
				2 => NPC.downedMechBossAny,  // E3: Any Mechanical Boss
				3 => NPC.downedPlantBoss,    // E4: Plantera
				4 => NPC.downedGolemBoss,    // E5: Golem
				5 => NPC.downedMoonlord,     // E6: Moon Lord
				6 => HasPostMoonLordContentMod
					? HasDefeatedPostMoonLordBoss
					: NPC.downedMoonlord,    // EX: First Post-ML Boss (or Moon Lord in Vanilla)
				_ => false
			};
		}

		public static string GetCharmUnlockHint(string charmId)
		{
			return charmId switch
			{
				"RedOni" or "AbyssGrip" or "ThunderCore" => "Unsealed at journey's start",
				"NamelessDecoy" => "Defeat King Slime to unseal",
				"FleetingStep" => "Defeat Eye of Cthulhu to unseal",
				"BloodLotus" => "Defeat Eater of Worlds or Brain of Cthulhu to unseal",
				"DepartedSiphon" => "Defeat Queen Bee to unseal",
				"BladesongRain" => "Defeat Skeletron to unseal",
				"WitheredRiver" => "Defeat Wall of Flesh to unseal",
				"VoidHorizon" => "Defeat Queen Slime to unseal",
				"MirageWeft" => "Defeat The Destroyer to unseal",
				"ShadowMarionette" => "Defeat The Twins to unseal",
				"KarmaSever" => "Defeat Skeletron Prime to unseal",
				"Rainleaf" => "Defeat Plantera to unseal",
				"ShadowCrucible" => "Defeat Golem to unseal",
				"EmancipatorWill" => "Defeat Duke Fishron to unseal",
				"StillWater" => "Defeat Empress of Light to unseal",
				"SynchronizedExecution" => "Defeat Lunatic Cultist to unseal",
				_ => "Sealed by world progression"
			};
		}

		public static string GetEidolonUnlockHint(int index)
		{
			return index switch
			{
				0 => "Defeat Eater of Worlds or Brain of Cthulhu to unseal",
				1 => "Defeat Wall of Flesh to unseal",
				2 => "Defeat any Mechanical Boss to unseal",
				3 => "Defeat Plantera to unseal",
				4 => "Defeat Golem to unseal",
				5 => "Defeat Moon Lord to unseal",
				6 => HasPostMoonLordContentMod
					? "Defeat a Post-Moon Lord Boss to unseal"
					: "Defeat Moon Lord to unseal",
				_ => "Sealed by world progression"
			};
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
			tag["DefeatedPostMoonLordBoss"] = DefeatedPostMoonLordBoss;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			if (tag.ContainsKey("DefeatedBosses"))
			{
				DefeatedBosses = new HashSet<string>(tag.GetList<string>("DefeatedBosses"), StringComparer.OrdinalIgnoreCase);
			}

			DefeatedPostMoonLordBoss = tag.ContainsKey("DefeatedPostMoonLordBoss") && tag.GetBool("DefeatedPostMoonLordBoss");
		}

		public override void NetSend(BinaryWriter writer)
		{
			BitsByte flags = new BitsByte(DefeatedPostMoonLordBoss);
			writer.Write(flags);

			writer.Write(DefeatedBosses.Count);
			foreach (string boss in DefeatedBosses)
			{
				writer.Write(boss);
			}
		}

		public override void NetReceive(BinaryReader reader)
		{
			BitsByte flags = reader.ReadByte();
			DefeatedPostMoonLordBoss = flags[0];

			DefeatedBosses.Clear();
			int count = reader.ReadInt32();
			for (int i = 0; i < count; i++)
			{
				DefeatedBosses.Add(reader.ReadString());
			}
		}
	}
}
