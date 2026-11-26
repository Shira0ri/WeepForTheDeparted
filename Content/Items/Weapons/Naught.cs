using System;
using System.Collections.Generic;
using InnoVault;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.Configs;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.Systems;
using WeepForTheDeparted.Content.Projectiles;

namespace WeepForTheDeparted.Content.Items.Weapons
{
	public class Naught : ModItem
	{
		private static Item scratchBase;
		private static Item scratchPrefixed;
		private static int cachedPrefix = -1;
		private static int cachedPrefixBase = -1;
		private static float cachedPrefixScale = 1f;

		private static int GetCurrentSetDefaultsBaseDamage()
		{
			WeepConfig cfg = WeepConfig.Instance;
			return cfg?.OverrideWeaponScaling == true
				? cfg.BaseSwordDamage
				: WeepBalance.Active.BaseSwordDamage;
		}

		private float GetPrefixDamageScale()
		{
			if (Item.prefix <= 0)
				return 1f;

			int currentBase = GetCurrentSetDefaultsBaseDamage();
			if (cachedPrefix != Item.prefix || cachedPrefixBase != currentBase || scratchBase == null || scratchPrefixed == null)
			{
				scratchBase ??= new Item();
				scratchPrefixed ??= new Item();
				scratchBase.SetDefaults(Item.type);
				scratchPrefixed.SetDefaults(Item.type);
				scratchPrefixed.Prefix(Item.prefix);

				cachedPrefix = Item.prefix;
				cachedPrefixBase = currentBase;
				cachedPrefixScale = scratchBase.damage > 0
					? scratchPrefixed.damage / (float)scratchBase.damage
					: 1f;
			}

			return cachedPrefixScale;
		}

		public override void Unload()
		{
			scratchBase = null;
			scratchPrefixed = null;
			cachedPrefix = -1;
			cachedPrefixBase = -1;
			cachedPrefixScale = 1f;
		}

		public override void SetDefaults()
		{
			Item.width = 64;
			Item.height = 64;
			Item.damage = GetCurrentSetDefaultsBaseDamage();
			Item.DamageType = WeepCompatibility.TrueMelee;
			Item.useTime = 16;
			Item.useAnimation = 16;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.knockBack = 5.5f;
			Item.value = Item.buyPrice(gold: 5);
			Item.rare = ItemRarityID.Red;
			Item.UseSound = null; // Handled dynamically in projectiles
			Item.autoReuse = true;
			Item.channel = true;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.shoot = ModContent.ProjectileType<NaughtHeldSwing>();
			Item.shootSpeed = 1f;
			Item.scale = 1.0f;
		}

		public override bool AltFunctionUse(Player player) => true;

		public override bool CanUseItem(Player player)
		{
			if (player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] > 0)
				return false;

			if (player.altFunctionUse == 2)
			{
				var naughtPlayer = player.GetModPlayer<NaughtPlayer>();
				if (naughtPlayer.DashCooldown > 0)
					return false;

				return player.ownedProjectileCounts[ModContent.ProjectileType<NaughtDash>()] == 0;
			}

			return player.ownedProjectileCounts[ModContent.ProjectileType<NaughtHeldSwing>()] == 0
				&& player.ownedProjectileCounts[ModContent.ProjectileType<NaughtDash>()] == 0;
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			if (player.altFunctionUse == 2)
			{
				// Right-Click: Flash Step Dash (leaves a 9-second decoy clone at start)
				Projectile.NewProjectile(source, player.MountedCenter, velocity, ModContent.ProjectileType<NaughtDash>(), 0, 0f, player.whoAmI);
				return false;
			}

			// Left-Click: 4-Beat Odachi combo
			return true;
		}

		public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
		{
			Vector2 toMouse = player.MountedCenter.To(Main.MouseWorld);
			if (toMouse == Vector2.Zero)
				toMouse = Vector2.UnitX * player.direction;
			velocity = Vector2.Normalize(toMouse);
		}

		public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
		{
			// On the ground waiting to be picked up, Naught rests inside its elegant purple-embellished scabbard,
			// resting upright on its tip (kojiri planted into the earth) like Onikiri!
			Texture2D sheathTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtSheath", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
			if (sheathTex != null)
			{
				// In NaughtSheath.png (180x180):
				// Scabbard tip (kojiri) is at (172f, 18f)
				// Pommel is at (7f, 164f)
				Vector2 origin = new Vector2(172f, 18f);
				Vector2 groundPos = Item.Bottom - Main.screenPosition;

				// Leaning upright resting on tip: 152 degrees leans gently to the right, 112 degrees leans to the left
				float groundRotation = Item.direction < 0 ? MathHelper.ToRadians(112f) : MathHelper.ToRadians(152f);
				float drawScale = scale * 0.42f;

				// Soft ambient purple glow behind the sheathed blade on the ground
				Color auraColor = new Color(175, 45, 240, 0) * 0.35f;
				spriteBatch.Draw(sheathTex, groundPos, null, auraColor, groundRotation, origin, drawScale * 1.04f, SpriteEffects.None, 0f);

				// Main sheathed sword resting upright on its tip
				spriteBatch.Draw(sheathTex, groundPos, null, lightColor, groundRotation, origin, drawScale, SpriteEffects.None, 0f);
				return false;
			}
			return true;
		}

		public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
		{
			float effectiveBase = BossTrackerSystem.GetEffectiveBaseDamage();
			float prefixScale = GetPrefixDamageScale();

			// Route base damage through BossTrackerSystem while preserving reforge and armor/accessory multiplicative scaling
			damage.Base += effectiveBase - Item.damage;
			if (MathF.Abs(prefixScale - 1f) > 0.0001f)
			{
				damage *= prefixScale;
			}
		}

		public override void ModifyWeaponCrit(Player player, ref float crit)
		{
			crit += BossTrackerSystem.GetEffectiveBonusCrit();
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			WeepConfig cfg = WeepConfig.Instance;
			bool overrideScaling = cfg?.OverrideWeaponScaling == true;
			int bossCount = BossTrackerSystem.GetTotalBossesDefeated();
			float effectiveBase = BossTrackerSystem.GetEffectiveBaseDamage();
			float startingBase = overrideScaling ? cfg.BaseSwordDamage : WeepBalance.Active.BaseSwordDamage;
			float bonusDmg = Math.Max(0f, effectiveBase - startingBase);
			float bonusCrit = BossTrackerSystem.GetEffectiveBonusCrit();
			string stage = BossTrackerSystem.GetProgressionStageName();
			string modeLabel = overrideScaling
				? "Manual Override"
				: (WeepCompatibility.IsCalamityBalanceActive ? "Calamity" : "Vanilla");
			string stageMultText = !overrideScaling && WeepBalance.Active.UseCalamityStageMultiplier
				? $" | Stage Mult: {BossTrackerSystem.GetCalamityPostMLStageMultiplier():0.00}x"
				: string.Empty;

			var loreLine = new TooltipLine(Mod, "AcheronLore",
				"\"I weep for the departed, like rain to water, tears fall to earth...\"\n" +
				"Acheron's blade, cleaving through the boundary between reality and the boundless void.")
			{
				OverrideColor = new Color(200, 180, 210) // Soft nihility lavender
			};
			tooltips.Add(loreLine);

			var comboLine = new TooltipLine(Mod, "ComboDescription",
				"[Left-Click]: 4-Beat Flowing Odachi Sequence\n" +
				"  1. Horizontal Draw Cut\n" +
				"  2. Downward Cross-Slash\n" +
				"  3. Rising Reverse Sweep\n" +
				"  4. Overhead Helm Splitter (Finisher Cleave)\n" +
				"[Right-Click]: Flash Step Dash\n" +
				"  Dashes forward with invulnerability, leaving a 9-second decoy shadow clone at your origin.")
			{
				OverrideColor = new Color(185, 125, 255) // Electric nihility violet
			};
			tooltips.Add(comboLine);

			var scalingLine = new TooltipLine(Mod, "ProgressionScaling",
				$"[Balance Profile: {modeLabel}{stageMultText}]\n" +
				$"[Progression: {stage}]\n" +
				$"[Defeated World Bosses: {bossCount}]\n" +
				$"[Effective Base Damage: {(int)MathF.Round(effectiveBase)} (+{(int)MathF.Round(bonusDmg)}) | Bonus Crit: +{bonusCrit:0.#}%]")
			{
				OverrideColor = new Color(245, 75, 145) // Vivid Acheron magenta
			};
			tooltips.Add(scalingLine);

			var ledgerLine = new TooltipLine(Mod, "NihilityLedgerHint",
				"[Special Controls]:\n" +
				"  [B]: Toggle Nihility Domain\n" +
				"  [V]: Unleash Slashed Dream Cries in Red (When 9/9)\n" +
				"  [C] or Click Slashed Dream HUD: Open Nihility Ledger (Charms & Eidolons)")
			{
				OverrideColor = new Color(255, 140, 160) // Luminous scarlet
			};
			tooltips.Add(ledgerLine);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 10)
				.AddIngredient(ItemID.FallenStar, 5)
				.AddRecipeGroup(RecipeGroupID.Wood, 15)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
