using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using WeepForTheDeparted.Common.Systems;

namespace WeepForTheDeparted.Common.UI.NihilityLedger
{
	public class NihilityLedgerSystem : ModSystem
	{
		private static bool IsLedgerBlockingInput()
		{
			var ui = NihilityLedgerUI.Instance;
			return ui != null && (ui.IsOpen || ui.OpenProgress.Current > 0.01f);
		}

		public override void PostUpdateInput()
		{
			if (Main.dedServ || Main.gameMenu)
				return;

			var ui = NihilityLedgerUI.Instance;
			if (ui == null)
				return;

			if (ui.IsOpen)
			{
				if (Main.netMode == NetmodeID.SinglePlayer)
					Main.gamePaused = true;

				Main.playerInventory = false;
				Main.ingameOptionsWindow = false;

				bool escPressed = Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape);
				bool invPressed = PlayerInput.Triggers.JustPressed.Inventory;
				bool ledgerPressed = WeepKeybinds.LedgerKeybind != null
					&& WeepKeybinds.LedgerKeybind.JustPressed
					&& Main.GameUpdateCount != ui.OpenedUpdateCount;

				if (escPressed || invPressed || ledgerPressed)
				{
					ui.Close();
					PlayerInput.Triggers.JustPressed.Inventory = false;
					PlayerInput.Triggers.Current.Inventory = false;
				}
			}
			else if (ui.JustClosedGuardTicks > 0)
			{
				Main.playerInventory = false;
				Main.ingameOptionsWindow = false;
				PlayerInput.Triggers.JustPressed.Inventory = false;
				PlayerInput.Triggers.Current.Inventory = false;
				ui.JustClosedGuardTicks--;
			}
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ || Main.gameMenu)
				return;

			var ui = NihilityLedgerUI.Instance;
			if (ui == null)
				return;

			if (ui.IsOpen)
			{
				if (Main.netMode == NetmodeID.SinglePlayer)
					Main.gamePaused = true;

				Main.playerInventory = false;
				Main.ingameOptionsWindow = false;
				Main.LocalPlayer.mouseInterface = true;
				Main.blockMouse = true;
			}
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			// Block vanilla inventory, hotbar, and world clicks before any vanilla interface layer runs
			int logicIndex = layers.FindIndex(layer => layer != null && layer.Name == "Vanilla: Interface Logic 1");
			int insertIndex = logicIndex >= 0 ? logicIndex : 0;
			layers.Insert(insertIndex, new LegacyGameInterfaceLayer(
				"WeepForTheDeparted: Nihility Ledger Input Blocker",
				delegate
				{
					if (IsLedgerBlockingInput())
					{
						Main.playerInventory = false;
						Main.LocalPlayer.mouseInterface = true;
						Main.blockMouse = true;
					}
					return true;
				},
				InterfaceScaleType.UI)
			);

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
						NihilityLedgerUI.Instance?.UpdateState();
						NihilityLedgerUI.Instance?.Draw(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI)
				);
			}
		}
	}
}
