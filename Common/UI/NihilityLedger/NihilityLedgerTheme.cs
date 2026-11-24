using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace WeepForTheDeparted.Common.UI.NihilityLedger
{
	public static class NihilityLedgerTheme
	{
		// ==== Nihility & Acheron Palette ====
		public static readonly Color VoidBg = new Color(13, 11, 14, 240);
		public static readonly Color LacquerBlack = new Color(22, 14, 18);
		public static readonly Color LacquerBorder = new Color(55, 18, 28);
		public static readonly Color LacquerGold = new Color(218, 172, 82);
		
		public static readonly Color PaperWhite = new Color(242, 236, 228);
		public static readonly Color TextDim = new Color(158, 134, 124);
		public static readonly Color TextDisabled = new Color(96, 78, 74);
		
		public static readonly Color AcheronScarlet = new Color(238, 26, 56);
		public static readonly Color DeepBurgundy = new Color(108, 10, 26);
		public static readonly Color DarkShadow = new Color(38, 4, 10);
		
		public static readonly Color NihilityViolet = new Color(165, 35, 235);
		public static readonly Color VioletGlow = new Color(205, 75, 255);
		public static readonly Color HotWhite = new Color(255, 246, 242);

		// ==== Drawing Helpers ====

		public static void DrawBeveledBox(SpriteBatch sb, Rectangle rect, Color bg, Color border, float borderWidth = 2f)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (pixel == null) return;

			// Background
			sb.Draw(pixel, rect, bg);

			int bw = (int)Math.Max(1, borderWidth);
			// Top
			sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, bw), border);
			// Bottom
			sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - bw, rect.Width, bw), border);
			// Left
			sb.Draw(pixel, new Rectangle(rect.X, rect.Y, bw, rect.Height), border);
			// Right
			sb.Draw(pixel, new Rectangle(rect.Right - bw, rect.Y, bw, rect.Height), border);
		}

		public static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float width = 2f)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (pixel == null) return;

			Vector2 diff = end - start;
			float length = diff.Length();
			if (length < 0.001f) return;

			float rot = (float)Math.Atan2(diff.Y, diff.X);
			Vector2 origin = new Vector2(0f, 0.5f);
			Vector2 scale = new Vector2(length, width);

			sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, rot, origin, scale, SpriteEffects.None, 0f);
		}

		public static void DrawGlowLine(SpriteBatch sb, Vector2 start, Vector2 end, Color coreColor, Color glowColor, float width = 2f, float glowWidth = 6f)
		{
			// Outer soft glow
			DrawLine(sb, start, end, glowColor, glowWidth);
			// Inner sharp core
			DrawLine(sb, start, end, coreColor, width);
		}

		public static void DrawCircle(SpriteBatch sb, Vector2 center, float radius, Color color, float thickness = 2f, int segments = 36)
		{
			float angleStep = MathHelper.TwoPi / segments;
			Vector2 prev = center + new Vector2(radius, 0f);

			for (int i = 1; i <= segments; i++)
			{
				float a = i * angleStep;
				Vector2 curr = center + new Vector2((float)Math.Cos(a) * radius, (float)Math.Sin(a) * radius);
				DrawLine(sb, prev, curr, color, thickness);
				prev = curr;
			}
		}

		public static void DrawFilledCircle(SpriteBatch sb, Vector2 center, float radius, Color color, int segments = 24)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (pixel == null) return;

			float angleStep = MathHelper.TwoPi / segments;
			Vector2 origin = new Vector2(0f, 0.5f);

			for (int i = 0; i < segments; i++)
			{
				float a = i * angleStep;
				Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
				sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), color, a, origin, new Vector2(radius, 3f), SpriteEffects.None, 0f);
			}
		}
	}
}
