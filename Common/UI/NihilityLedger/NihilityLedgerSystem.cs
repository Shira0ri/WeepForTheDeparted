using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace WeepForTheDeparted.Common.UI.NihilityLedger
{
	public class NihilityLedgerSystem : ModSystem
	{
		// InnoVault's UIHandleLoader already automatically calls LogicUpdate() on registered UIHandles
		// every tick during the game logic update loop.

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			bool innoVaultHandling = layers.Exists(layer => layer != null && layer.Name != null && layer.Name.StartsWith("UIHander: Vanilla: Mouse Text"));
			if (innoVaultHandling)
				return;

			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex != -1)
			{
				layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
					"WeepForTheDeparted: Nihility Ledger UI Fallback",
					delegate
					{
						NihilityLedgerUI.Instance?.Draw(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI)
				);
			}
		}
	}
}
