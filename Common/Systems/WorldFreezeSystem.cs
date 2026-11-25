using System;
using System.Collections.Generic;
using System.Reflection;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.GlobalNPCs;
using WeepForTheDeparted.Content.Projectiles;
using WeepForTheDeparted.Content.Projectiles.Charms;

namespace WeepForTheDeparted.Common.Systems
{
	/// <summary>
	/// Screen and world time-freeze system modeled after Calamity Overhaul's WorldFreezeSystem.
	/// Freezes liquid physics, hotbar scrolling, NPC physics/AI, and all hostile/environmental projectiles.
	/// </summary>
	public class WorldFreezeSystem : ModSystem
	{
		private static readonly HashSet<string> activeReasons = new HashSet<string>();

		public static bool IsActive => activeReasons.Count > 0;

		private delegate void Hook_UpdateLiquid(Action orig);
		private delegate void Hook_ScrollHotbar(Action<Player, int> orig, Player self, int offset);

		private static readonly List<(MethodBase Method, Delegate Handler)> mountedHooks = new List<(MethodBase, Delegate)>();

		public static void Activate(string reason)
		{
			if (string.IsNullOrEmpty(reason))
				return;

			activeReasons.Add(reason);
		}

		public static void Deactivate(string reason)
		{
			if (string.IsNullOrEmpty(reason))
				return;

			if (activeReasons.Remove(reason) && activeReasons.Count == 0)
			{
				UnfreezeAllNPCs();
			}
		}

		public static void DeactivateAll()
		{
			if (activeReasons.Count > 0)
			{
				activeReasons.Clear();
				UnfreezeAllNPCs();
			}
		}

		private static void UnfreezeAllNPCs()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (npc != null && npc.active && npc.TryGetGlobalNPC<WorldFreezeNPC>(out var freezeData) && freezeData.wasFrozen)
				{
					freezeData.Unfreeze(npc);
				}
			}
		}

		public override void Load()
		{
			if (Main.dedServ)
				return;

			MethodInfo liquidMethod = typeof(Liquid).GetMethod("UpdateLiquid", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
			if (liquidMethod != null)
			{
				Hook_UpdateLiquid handler = OnUpdateLiquidHook;
				VaultHook.Add(liquidMethod, handler, VaultHook.DefaultHookPriority + 50, "WeepForTheDeparted.WorldFreeze.UpdateLiquid");
				mountedHooks.Add((liquidMethod, handler));
			}

			MethodInfo scrollMethod = typeof(Player).GetMethod("ScrollHotbar", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int) }, null);
			if (scrollMethod != null)
			{
				Hook_ScrollHotbar handler = OnScrollHotbarHook;
				VaultHook.Add(scrollMethod, handler, VaultHook.DefaultHookPriority + 50, "WeepForTheDeparted.WorldFreeze.ScrollHotbar");
				mountedHooks.Add((scrollMethod, handler));
			}
		}

		public override void Unload()
		{
			foreach (var (method, handler) in mountedHooks)
			{
				if (!VaultHook.Hooks.TryRemove((method, handler), out var hook))
					continue;

				try
				{
					if (hook.IsApplied)
						hook.Undo();
					hook.Dispose();
				}
				catch
				{
				}
			}
			mountedHooks.Clear();
			DeactivateAll();
		}

		private static void OnUpdateLiquidHook(Action orig)
		{
			if (IsActive)
				return;
			orig();
		}

		private static void OnScrollHotbarHook(Action<Player, int> orig, Player self, int offset)
		{
			if (IsActive)
			{
				self.HotbarOffset = 0;
				self.changeItem = -1;
				return;
			}
			orig(self, offset);
		}

		public override void OnWorldUnload()
		{
			DeactivateAll();
		}

		public static bool IsExempt(Projectile projectile)
		{
			if (projectile == null || !projectile.active)
				return false;

			int type = projectile.type;
			return type == ModContent.ProjectileType<SlashedDreamUltimate>()
				|| type == ModContent.ProjectileType<NaughtHeldSwing>()
				|| type == ModContent.ProjectileType<NaughtSlashVisual>()
				|| type == ModContent.ProjectileType<NaughtDash>()
				|| type == ModContent.ProjectileType<NaughtDashTrail>()
				|| type == ModContent.ProjectileType<NaughtClone>()
				|| type == ModContent.ProjectileType<CrimsonKnotTransferBeam>()
				|| type == ModContent.ProjectileType<MirageWeftThread>()
				|| type == ModContent.ProjectileType<BloodLotusShockwave>();
		}
	}
}
