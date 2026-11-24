using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.Players;

namespace WeepForTheDeparted.Content.Items.Debug
{
	public class NihilityDebugItem : ModItem
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.damage = 15;
			Item.DamageType = WeepCompatibility.TrueMelee;
			Item.useTime = 10;
			Item.useAnimation = 10;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.knockBack = 3f;
			Item.value = 0;
			Item.rare = ItemRarityID.Cyan;
			Item.autoReuse = true;
			Item.accessory = true; // Can be equipped as accessory OR swung as weapon!
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetModPlayer<NihilityDebugPlayer>().DebuffInflictionActive = true;
		}

		public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
		{
			WeepCompatibility.ApplyDebugDebuffs(target);
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "DebugTag", "[DEV / QA TESTING TOOL]") { OverrideColor = Color.Yellow });
			tooltips.Add(new TooltipLine(Mod, "DebugDesc",
				"Equip as an accessory OR swing directly as a blade.\n" +
				"While equipped, ALL True Melee (or Melee) strikes inflict 8+ unique debuffs on enemies:\n" +
				"  - Hellfire, Frostbite, Shadowflame, Ichor, Cursed Inferno, Electrified, Venom, Midas\n" +
				"  - Plus Calamity debuffs (Brimstone Flames, Plague, God Slayer Inferno, Whispering Death) if Calamity is loaded.\n" +
				"Crafted freely with 1 Dirt Block for testing Slashed Dream & Crimson Knot mechanics!")
			{
				OverrideColor = new Color(245, 120, 180)
			});
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock, 1)
				.Register();
		}
	}
}
