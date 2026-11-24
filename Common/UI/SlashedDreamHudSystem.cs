using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace WeepForTheDeparted.Common.UI
{
	/// <summary>
	/// SlashedDreamHud is an InnoVault UIHandle. SlashedDreamHudSystem ensures logic ticks run reliably
	/// and acts as a safe fallback layer if InnoVault's UIHandleLoader has not already inserted the mouse text layer.
	/// </summary>
	public class SlashedDreamHudSystem : ModSystem
	{
		public override void PostUpdateEverything()
		{
			SlashedDreamHud.Instance?.UpdateState();
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			// Check if InnoVault's UIHandleLoader is already rendering UIHandle layers
			bool innoVaultHandling = layers.Exists(layer => layer != null && layer.Name != null && layer.Name.StartsWith("UIHander: Vanilla: Mouse Text"));
			if (innoVaultHandling)
				return;

			// Fallback: if InnoVault didn't insert the layer, ensure the HUD is rendered cleanly
			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex != -1)
			{
				layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
					"WeepForTheDeparted: Slashed Dream HUD Fallback",
					delegate
					{
						SlashedDreamHud.Instance?.Draw(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI)
				);
			}
		}
	}
}
