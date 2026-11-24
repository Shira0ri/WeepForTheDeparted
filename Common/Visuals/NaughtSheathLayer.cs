using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using WeepForTheDeparted.Content.Items.Weapons;
using WeepForTheDeparted.Content.Projectiles;

namespace WeepForTheDeparted.Common.Visuals
{
	public class NaughtSheathLayer : PlayerDrawLayer
	{
		public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.ArmOverItem);

		public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
		{
			Player player = drawInfo.drawPlayer;
			if (player == null || !player.active || player.dead || player.ghost || drawInfo.headOnlyRender)
				return false;

			// Only visible when holding Naught
			if (player.HeldItem?.type != ModContent.ItemType<Naught>())
				return false;

			// Hide while actively swinging the blade, dashing, or executing ultimate
			if (player.itemAnimation > 0
				|| player.ownedProjectileCounts[ModContent.ProjectileType<NaughtHeldSwing>()] > 0
				|| player.ownedProjectileCounts[ModContent.ProjectileType<NaughtDash>()] > 0
				|| player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] > 0)
			{
				return false;
			}

			return true;
		}

		protected override void Draw(ref PlayerDrawSet drawInfo)
		{
			Player player = drawInfo.drawPlayer;
			Texture2D sheathTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtSheath", AssetRequestMode.ImmediateLoad).Value;
			if (sheathTex == null)
				return;

			// In NaughtSheath.png (180x180):
			// Tsuba / guard center is at (45f, 138f)
			// Scabbard tip is at (172f, 18f)
			// Pommel is at (7f, 164f)
			Vector2 origin = new Vector2(45f, 138f);

			int facing = player.direction < 0 ? -1 : 1;
			int grav = player.gravDir < 0f ? -1 : 1;

			// Position anchored at the player's hip / beltline (matching clone)
			Vector2 hipOffset = new Vector2(facing > 0 ? 8f : 12f, 25f * grav);
			Vector2 drawPos = drawInfo.Position - Main.screenPosition + hipOffset + drawInfo.drawPlayer.bodyPosition + new Vector2(0f, player.gfxOffY);

			// Rotation: 191.5 degrees rotates the scabbard down-backward (+150 deg)
			// and hilt forward towards the hand (-30 deg), exactly like the clone
			float rotation = MathHelper.ToRadians(191.5f) * facing * grav;

			SpriteEffects effects = SpriteEffects.None;
			if (facing < 0)
			{
				effects |= SpriteEffects.FlipHorizontally;
				origin.X = sheathTex.Width - origin.X;
			}
			if (grav < 0)
			{
				effects |= SpriteEffects.FlipVertically;
				origin.Y = sheathTex.Height - origin.Y;
			}

			float drawScale = 0.42f;

			// Soft ambient Nihility purple aura behind the sheath
			Color auraColor = new Color(175, 45, 240, 0) * 0.30f;
			drawInfo.DrawDataCache.Add(new DrawData(sheathTex, drawPos, null, auraColor, rotation, origin, drawScale * 1.05f, effects, 0));

			// Main sheathed odachi matching torso lighting
			Color bodyLight = drawInfo.colorArmorBody;
			drawInfo.DrawDataCache.Add(new DrawData(sheathTex, drawPos, null, bodyLight, rotation, origin, drawScale, effects, 0));
		}
	}
}
