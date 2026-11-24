using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria.ModLoader;

namespace WeepForTheDeparted.Common.UI
{
	public static class CalamityOverhaulHudIntegration
	{
		private static bool initialized = false;
		private static Hook resolveAnchorHook = null;

		public delegate Vector2 Orig_ResolveAnchor(object hud);

		public static void EnsureRegistered()
		{
			if (initialized)
				return;

			initialized = true;

			if (!ModLoader.TryGetMod("CalamityOverhaul", out Mod covMod))
				return;

			try
			{
				Assembly covAssembly = covMod.Code ?? covMod.GetType().Assembly;
				Type bottomLeftHudStackType = covAssembly.GetType("CalamityOverhaul.Content.UIs.HudStack.BottomLeftHudStack");
				if (bottomLeftHudStackType == null)
					return;

				MethodInfo resolveAnchorMethod = bottomLeftHudStackType.GetMethod("ResolveAnchor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				if (resolveAnchorMethod == null)
					return;

				MethodInfo hookMethod = typeof(CalamityOverhaulHudIntegration).GetMethod(nameof(Hook_ResolveAnchor), BindingFlags.NonPublic | BindingFlags.Static);
				resolveAnchorHook = new Hook(resolveAnchorMethod, hookMethod);
			}
			catch (Exception ex)
			{
				ModContent.GetInstance<WeepForTheDeparted>()?.Logger.Warn("Calamity Overhaul BottomLeftHudStack hook failed: " + ex.Message);
			}
		}

		private static Vector2 Hook_ResolveAnchor(Orig_ResolveAnchor orig, object hud)
		{
			Vector2 anchor = orig(hud);

			// When Slashed Dream HUD is visible, push Calamity Overhaul's stacked HUDs (Soul Guide Lantern, etc.) up!
			if (hud != null && SlashedDreamHud.Instance != null && SlashedDreamHud.Instance.Appear > 0.001f)
			{
				anchor.Y -= 145f * SlashedDreamHud.Instance.Appear;
			}

			return anchor;
		}

		public static void Unload()
		{
			resolveAnchorHook?.Dispose();
			resolveAnchorHook = null;
			initialized = false;
		}
	}
}
