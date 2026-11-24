using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace WeepForTheDeparted.Common
{
	public static class WeepCompatibility
	{
		private static DamageClass trueMeleeDamageClass;
		private static DamageClass trueMeleeNoSpeedDamageClass;
		private static bool initialized = false;

		private static void EnsureInitialized()
		{
			if (initialized)
				return;

			initialized = true;

			if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
			{
				if (calamity.TryFind("TrueMeleeDamageClass", out DamageClass trueMelee))
				{
					trueMeleeDamageClass = trueMelee;
				}

				if (calamity.TryFind("TrueMeleeNoSpeedDamageClass", out DamageClass trueMeleeNoSpeed))
				{
					trueMeleeNoSpeedDamageClass = trueMeleeNoSpeed;
				}
			}
		}

		/// <summary>
		/// Returns Calamity's TrueMeleeDamageClass if Calamity Mod is installed and active;
		/// otherwise safely falls back to standard DamageClass.Melee (matching Calamity Overhaul's CWRRef.GetTrueMeleeDamageClass standard).
		/// </summary>
		public static DamageClass TrueMelee
		{
			get
			{
				EnsureInitialized();
				return trueMeleeDamageClass ?? DamageClass.Melee;
			}
		}

		public static DamageClass TrueMeleeNoSpeed
		{
			get
			{
				EnsureInitialized();
				return trueMeleeNoSpeedDamageClass ?? DamageClass.MeleeNoSpeed;
			}
		}

		public static bool HasCalamity => ModLoader.HasMod("CalamityMod");

		public static void ApplyDebugDebuffs(NPC target)
		{
			if (target == null || !target.active)
				return;

			// Core vanilla elemental & curse debuffs
			target.AddBuff(BuffID.OnFire3, 360);       // Hellfire
			target.AddBuff(BuffID.Frostburn2, 360);     // Frostbite
			target.AddBuff(BuffID.ShadowFlame, 360);    // Shadowflame
			target.AddBuff(BuffID.Ichor, 360);          // Ichor
			target.AddBuff(BuffID.CursedInferno, 360);  // Cursed Inferno
			target.AddBuff(BuffID.Electrified, 360);    // Electrified
			target.AddBuff(BuffID.Venom, 360);          // Acid Venom
			target.AddBuff(BuffID.Midas, 360);          // Midas

			// Calamity debuffs if loaded
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
			{
				if (cal.TryFind("BrimstoneFlames", out ModBuff brimstone))
					target.AddBuff(brimstone.Type, 360);
				if (cal.TryFind("Plague", out ModBuff plague))
					target.AddBuff(plague.Type, 360);
				if (cal.TryFind("GodSlayerInferno", out ModBuff godslayer))
					target.AddBuff(godslayer.Type, 360);
				if (cal.TryFind("WhisperingDeath", out ModBuff whisper))
					target.AddBuff(whisper.Type, 360);
			}
		}
	}
}
