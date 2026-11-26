using System;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.Configs;

namespace WeepForTheDeparted.Common
{
	public static class WeepCompatibility
	{
		private static DamageClass trueMeleeDamageClass;
		private static DamageClass trueMeleeNoSpeedDamageClass;
		private static GlobalNPC calamityGlobalNPCTemplate;
		private static MemberInfo calamityDRMember;
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

				if (calamity.TryFind("CalamityGlobalNPC", out GlobalNPC calGlobalNPC))
				{
					calamityGlobalNPCTemplate = calGlobalNPC;
					Type calNPCType = calGlobalNPC.GetType();
					const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
					calamityDRMember = (MemberInfo)calNPCType.GetField("DR", flags) ?? calNPCType.GetProperty("DR", flags);
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

		/// <summary>
		/// Safely reads CalamityMod's CalamityGlobalNPC.DR value via cached reflection.
		/// Returns false if CalamityMod is not loaded or the member is unavailable.
		/// </summary>
		public static bool TryGetCalamityNPCDR(NPC npc, out float dr)
		{
			dr = 0f;
			if (npc == null || !npc.active)
				return false;

			EnsureInitialized();
			if (calamityGlobalNPCTemplate == null || calamityDRMember == null)
				return false;

			if (!npc.TryGetGlobalNPC(calamityGlobalNPCTemplate, out GlobalNPC calNPC) || calNPC == null)
				return false;

			try
			{
				object raw = calamityDRMember switch
				{
					FieldInfo field => field.GetValue(calNPC),
					PropertyInfo prop => prop.GetValue(calNPC),
					_ => null
				};

				if (raw is float value && float.IsFinite(value))
				{
					dr = value;
					return true;
				}
			}
			catch
			{
				// Ignore reflection failures gracefully and allow Vanilla fallback
			}

			return false;
		}

		public static float GetCalamityNPCDR(NPC npc) => TryGetCalamityNPCDR(npc, out float dr) ? dr : 0f;

		/// <summary>
		/// Computes the final-damage multiplier for a single-pass DR peel of any positive <paramref name="totalPeel"/>
		/// (e.g. 0.10f, 0.12f, 0.14f, 0.15f, 0.16f, 0.22f, 0.29f).
		/// Skips DR peel if Calamity DR &gt; 0.90f (anti-cheese phase shields), peels up to <paramref name="totalPeel"/>
		/// off positive Calamity DR, and converts any unspent remainder (and all 0%-DR or Vanilla targets) cleanly
		/// into multiplicative bonus damage (<c>1f + totalPeel</c> on 0%-DR/Vanilla).
		/// </summary>
		public static float ComputeUnifiedDRPeelMultiplier(NPC target, float totalPeel)
		{
			if (target == null || !target.active || totalPeel <= 0f)
				return 1f;

			if (TryGetCalamityNPCDR(target, out float dr) && dr > 0f)
			{
				if (dr > 0.90f)
					return 1f;

				float peeled = Math.Min(dr, totalPeel);
				float newDr = dr - peeled;
				float remainder = totalPeel - peeled;
				return ((1f - newDr) / (1f - dr)) * (1f + remainder);
			}

			return 1f + totalPeel;
		}

		/// <summary>
		/// Applies unified single-pass DR peel onto <paramref name="modifiers"/>.
		/// </summary>
		public static void ApplyUnifiedDRPeel(NPC target, ref NPC.HitModifiers modifiers, float totalPeel)
		{
			if (totalPeel <= 0f)
				return;

			float mult = ComputeUnifiedDRPeelMultiplier(target, totalPeel);
			if (mult != 1f)
			{
				modifiers.FinalDamage *= mult;
			}
		}

		/// <summary>
		/// Resolves whether Calamity-tuned scaling should be used based on WeepConfig.BalanceMode
		/// (Auto detects CalamityMod; Calamity and Vanilla force their respective branches).
		/// </summary>
		public static bool IsCalamityBalanceActive
		{
			get
			{
				WeepBalanceMode mode = WeepConfig.Instance?.BalanceMode ?? WeepBalanceMode.Auto;
				return mode switch
				{
					WeepBalanceMode.Calamity => true,
					WeepBalanceMode.Vanilla => false,
					_ => HasCalamity
				};
			}
		}

		/// <summary>
		/// Returns true if the given DamageClass belongs to Naught's Melee / True Melee family.
		/// </summary>
		public static bool IsNaughtDamageClass(DamageClass damageType)
		{
			return damageType != null && (
				damageType == TrueMelee ||
				damageType == TrueMeleeNoSpeed ||
				damageType == DamageClass.Melee ||
				damageType == DamageClass.MeleeNoSpeed);
		}

		/// <summary>
		/// Returns true if the given DamageClass counts as True Melee
		/// (Calamity's TrueMeleeDamageClass / TrueMeleeNoSpeedDamageClass when loaded,
		/// or Vanilla Melee / MeleeNoSpeed when Calamity is absent).
		/// </summary>
		public static bool IsTrueMeleeDamageClass(DamageClass damageType)
		{
			if (damageType == null)
				return false;

			EnsureInitialized();
			if (trueMeleeDamageClass != null)
			{
				return damageType == trueMeleeDamageClass
					|| (trueMeleeNoSpeedDamageClass != null && damageType == trueMeleeNoSpeedDamageClass)
					|| damageType.CountsAsClass(trueMeleeDamageClass);
			}

			return damageType == DamageClass.Melee
				|| damageType == DamageClass.MeleeNoSpeed
				|| damageType.CountsAsClass(DamageClass.Melee);
		}

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
