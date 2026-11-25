using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace WeepForTheDeparted.Common.Visuals
{
	public class PlayerCloneRenderer : ModSystem
	{
		private static Player dummy;
		private static bool tintActive;
		private static Color activeTint;
		private static bool ultGhostActive;
		private static Vector2 ultGhostTargetScreen;
		private static float ultGhostScaleX;
		private static float ultGhostScaleY;
		private static float ultGhostLeanShear;
		private static float ultGhostBodyTilt;
		private static Color ultGhostInkColor;
		private static float ultGhostTintStrength;
		private static float ultGhostAlpha;

		public override void OnModUnload()
		{
			dummy = null;
			tintActive = false;
			activeTint = default;
			ultGhostActive = false;
		}

		public static void Prepare(Player owner)
		{
			if (owner == null || Main.dedServ)
				return;

			dummy ??= new Player();
			dummy.isDisplayDollOrInanimate = true;
			dummy.CopyVisuals(owner);

			// Copy full armor, vanity, and dye inventories
			if (owner.armor != null)
			{
				for (int i = 0; i < owner.armor.Length; i++)
				{
					dummy.armor[i] = owner.armor[i].Clone();
				}
			}

			if (owner.dye != null)
			{
				for (int i = 0; i < owner.dye.Length; i++)
				{
					dummy.dye[i] = owner.dye[i].Clone();
				}
			}

			if (owner.miscEquips != null)
			{
				for (int i = 0; i < owner.miscEquips.Length; i++)
					dummy.miscEquips[i] = owner.miscEquips[i].Clone();
			}

			if (owner.miscDyes != null)
			{
				for (int i = 0; i < owner.miscDyes.Length; i++)
					dummy.miscDyes[i] = owner.miscDyes[i].Clone();
			}

			dummy.head = owner.head;
			dummy.body = owner.body;
			dummy.legs = owner.legs;
			dummy.handon = owner.handon;
			dummy.handoff = owner.handoff;
			dummy.back = owner.back;
			dummy.front = owner.front;
			dummy.shoe = owner.shoe;
			dummy.waist = owner.waist;
			dummy.wings = owner.wings;
			dummy.neck = owner.neck;
			dummy.face = owner.face;
			dummy.shield = owner.shield;
			dummy.skinVariant = owner.skinVariant;

			dummy.ResetEffects();
			dummy.UpdateDyes();
		}

		public static void DrawPrepared(Vector2 position, Color tint, int direction,
			Rectangle bodyFrame, Rectangle legFrame,
			float fullRotation = 0f, Vector2 fullRotationOrigin = default, float gravDir = 1f)
		{
			if (dummy == null || Main.dedServ)
				return;

			ApplyPose(position, direction, gravDir, bodyFrame, legFrame, fullRotation, fullRotationOrigin);
			tintActive = true;
			activeTint = tint;
			try
			{
				Main.PlayerRenderer.DrawPlayer(Main.Camera, dummy, dummy.position, dummy.fullRotation, dummy.fullRotationOrigin);
			}
			finally
			{
				tintActive = false;
			}
		}

		public static void Draw(Player owner, Vector2 position, Color tint, int direction,
			Rectangle bodyFrame, Rectangle legFrame,
			float fullRotation = 0f, Vector2 fullRotationOrigin = default, float gravDir = 1f)
		{
			if (owner == null || Main.dedServ)
				return;

			Prepare(owner);
			DrawPrepared(position, tint, direction, bodyFrame, legFrame, fullRotation, fullRotationOrigin, gravDir);
		}

		private static void ApplyPose(Vector2 position, int direction, float gravDir,
			Rectangle bodyFrame, Rectangle legFrame,
			float fullRotation, Vector2 fullRotationOrigin)
		{
			dummy.position = position;
			dummy.velocity = Vector2.Zero;
			dummy.direction = direction >= 0 ? 1 : -1;
			dummy.gravDir = gravDir >= 0f ? 1f : -1f;
			dummy.bodyFrame = bodyFrame;
			dummy.legFrame = legFrame;
			dummy.fullRotation = fullRotation;
			dummy.fullRotationOrigin = fullRotationOrigin;
			dummy.heldProj = -1;
			dummy.itemAnimation = 0;
			dummy.itemTime = 0;
		}

		internal static bool TryGetActiveTint(Player player, out Color tint)
		{
			tint = activeTint;
			return (tintActive || ultGhostActive) && ReferenceEquals(player, dummy);
		}

		internal static bool TryGetActiveUltGhostTransform(
			Player player,
			out Vector2 targetScreen,
			out float scaleX,
			out float scaleY,
			out float leanShear,
			out float bodyTilt,
			out Color inkColor,
			out float tintStrength,
			out float alpha)
		{
			if (ultGhostActive && ReferenceEquals(player, dummy))
			{
				targetScreen = ultGhostTargetScreen;
				scaleX = ultGhostScaleX;
				scaleY = ultGhostScaleY;
				leanShear = ultGhostLeanShear;
				bodyTilt = ultGhostBodyTilt;
				inkColor = ultGhostInkColor;
				tintStrength = ultGhostTintStrength;
				alpha = ultGhostAlpha;
				return true;
			}

			targetScreen = default;
			scaleX = 1f;
			scaleY = 1f;
			leanShear = 0f;
			bodyTilt = 0f;
			inkColor = Color.White;
			tintStrength = 0f;
			alpha = 1f;
			return false;
		}

		public static void DrawPreparedUltAfterimage(
			Player owner,
			Vector2 projectedCenterWorld,
			float perspScale,
			in UltAfterimage3D ghost,
			Color inkColor,
			float tintStrength,
			float alpha)
		{
			if (dummy == null || owner == null || Main.dedServ)
				return;

			// Anchor dummy at owner.position for bright world lighting lookup, then transform into 2.5D projected screen coords
			ApplyPose(owner.position, ghost.DrawDirection, owner.gravDir, owner.bodyFrame, owner.legFrame, 0f, Vector2.Zero);

			Ult3DProjection.ComputeAnisotropicScale(perspScale, ghost.WidthScale, ghost.SquashY, ghost.StretchDir2D, ghost.StretchAmount, out float sx, out float sy);

			ultGhostTargetScreen = projectedCenterWorld - Main.screenPosition;
			ultGhostScaleX = sx;
			ultGhostScaleY = sy;
			ultGhostLeanShear = ghost.LeanShear;
			ultGhostBodyTilt = ghost.BodyTilt;
			ultGhostInkColor = inkColor;
			ultGhostTintStrength = tintStrength;
			ultGhostAlpha = alpha;
			ultGhostActive = true;
			try
			{
				Main.PlayerRenderer.DrawPlayer(Main.Camera, dummy, dummy.position, 0f, Vector2.Zero);
			}
			finally
			{
				ultGhostActive = false;
			}
		}
	}

	internal sealed class PlayerCloneTintPlayer : ModPlayer
	{
		public override void TransformDrawData(ref PlayerDrawSet drawInfo)
		{
			if (PlayerCloneRenderer.TryGetActiveUltGhostTransform(
				drawInfo.drawPlayer,
				out Vector2 targetScreen,
				out float scaleX,
				out float scaleY,
				out float leanShear,
				out float bodyTilt,
				out Color inkColor,
				out float tintStrength,
				out float ghostAlpha))
			{
				Vector2 sourceCenterScreen = drawInfo.drawPlayer.MountedCenter - Main.screenPosition;
				Ult3DProjection.TransformPlayerDrawData(
					ref drawInfo,
					sourceCenterScreen,
					targetScreen,
					scaleX,
					scaleY,
					leanShear,
					bodyTilt,
					inkColor,
					tintStrength,
					ghostAlpha,
					minOpaqueFloor: 0.75f);
				return;
			}

			if (!PlayerCloneRenderer.TryGetActiveTint(drawInfo.drawPlayer, out Color tint))
				return;

			float alpha = tint.A / 255f;
			for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
			{
				DrawData data = drawInfo.DrawDataCache[i];
				Color src = data.color;

				// Preserve player's full armor/clothing colors and dyes while tinting with translucent ethereal violet
				byte r = (byte)MathHelper.Clamp(src.R * 0.70f + tint.R * 0.30f, 0, 255);
				byte g = (byte)MathHelper.Clamp(src.G * 0.65f + tint.G * 0.35f, 0, 255);
				byte b = (byte)MathHelper.Clamp(src.B * 0.80f + tint.B * 0.20f, 0, 255);
				byte a = (byte)MathHelper.Clamp(src.A * alpha, 0, 255);

				data.color = new Color(r, g, b, a) * alpha;
				drawInfo.DrawDataCache[i] = data;
			}
		}
	}
}
