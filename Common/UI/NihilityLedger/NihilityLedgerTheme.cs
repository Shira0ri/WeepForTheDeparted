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
				sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), color, a, origin, new Vector2(radius, 3f), SpriteEffects.None, 0f);
			}
		}

		public static void DrawSoftGlow(SpriteBatch sb, Vector2 center, float radius, Color color, float alpha)
		{
			if (radius <= 1f || alpha <= 0.01f)
				return;

			Color add = new Color(color.R, color.G, color.B, 0);
			for (int i = 0; i < 4; i++)
			{
				float t = i / 4f;
				float r = radius * (0.35f + t * 0.65f);
				DrawFilledCircle(sb, center, r, add * (alpha * 0.16f * (1f - t * 0.7f)), 24);
			}
		}

		public static void DrawArc(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color, float startAngle, float endAngle, int segments = 24)
		{
			if (segments < 2 || radius <= 0.5f)
				return;

			float span = endAngle - startAngle;
			float step = span / segments;
			Vector2 prev = center + new Vector2((float)Math.Cos(startAngle) * radius, (float)Math.Sin(startAngle) * radius);

			for (int i = 1; i <= segments; i++)
			{
				float a = startAngle + i * step;
				Vector2 curr = center + new Vector2((float)Math.Cos(a) * radius, (float)Math.Sin(a) * radius);
				DrawLine(sb, prev, curr, color, thickness);
				prev = curr;
			}
		}

		public static void DrawGradientLine(SpriteBatch sb, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness = 2f, int segments = 10)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (pixel == null) return;

			Vector2 edge = end - start;
			float len = edge.Length();
			if (len < 1f) return;

			float rot = (float)Math.Atan2(edge.Y, edge.X);
			float segLen = len / segments;
			for (int i = 0; i < segments; i++)
			{
				float t = (i + 0.5f) / segments;
				Color col = Color.Lerp(startColor, endColor, t);
				Vector2 pos = start + edge * (i / (float)segments);
				sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), col, rot, new Vector2(0f, 0.5f), new Vector2(segLen + 0.6f, thickness), SpriteEffects.None, 0f);
			}
		}

		public static void DrawTaperedSlash(SpriteBatch sb, Vector2 start, Vector2 end, float maxThick, float bow, float alpha, float sweep = 1f)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (pixel == null) return;

			Vector2 edge = end - start;
			float fullLen = edge.Length();
			if (fullLen < 2f || alpha <= 0.01f || sweep <= 0.02f)
				return;

			Vector2 dir = edge / fullLen;
			Vector2 perp = new Vector2(dir.Y, -dir.X);
			float len = fullLen * MathHelper.Clamp(sweep, 0f, 1f);
			const int Seg = 16;
			float segLen = len / Seg;
			float rot = (float)Math.Atan2(dir.Y, dir.X);

			for (int i = 0; i < Seg; i++)
			{
				float tm = (i + 0.5f) / Seg;
				float tShape = tm * MathHelper.Clamp(sweep, 0f, 1f);
				float profile = (float)Math.Pow(Math.Sin(tShape * Math.PI), 0.62);
				float thick = maxThick * Math.Max(profile, 0.12f);
				Vector2 pos = start + dir * (segLen * i) + perp * ((float)Math.Sin(tShape * Math.PI) * bow);
				Color col = Color.Lerp(DeepBurgundy, AcheronScarlet, profile) * alpha;
				sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), col, rot, new Vector2(0f, 0.5f), new Vector2(segLen + 0.7f, thick), SpriteEffects.None, 0f);

				if (tShape > 0.08f && tShape < 0.52f)
				{
					float core = (float)Math.Sin((tShape - 0.08f) / 0.44f * Math.PI);
					sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), HotWhite * (alpha * 0.72f * core), rot, new Vector2(0f, 0.5f), new Vector2(segLen + 0.7f, thick * 0.38f), SpriteEffects.None, 0f);
				}
			}
		}

		public static void DrawDashedLine(SpriteBatch sb, Vector2 start, Vector2 end, Color dimCol, Color glowCol, float alpha, float time, float phase = 0f)
		{
			Vector2 diff = end - start;
			float len = diff.Length();
			if (len < 2f || alpha <= 0.01f)
				return;

			Vector2 dir = diff / len;
			float dashLen = 8f;
			float gapLen = 6f;
			float step = dashLen + gapLen;
			float scroll = (time * 24f + phase * 12f) % step;

			for (float d = -scroll; d < len; d += step)
			{
				float s = Math.Max(0f, d);
				float e = Math.Min(len, d + dashLen);
				if (e - s <= 1f)
					continue;

				float midT = (s + e) * 0.5f / len;
				float wave = 0.5f + 0.5f * (float)Math.Sin(time * 3f + midT * 6f + phase);
				Color col = Color.Lerp(dimCol, glowCol, wave) * alpha;
				DrawLine(sb, start + dir * s, start + dir * e, col, 1.5f);
			}
		}

		public static void DrawDiamond(SpriteBatch sb, Vector2 center, float radius, Color fillColor, Color borderColor, float rotation = 0f, float borderWidth = 1.6f)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (pixel == null || radius <= 0.5f)
				return;

			float side = radius * 1.4142135f;
			float angle = MathHelper.PiOver4 + rotation;
			if (fillColor.A > 0)
			{
				sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), fillColor, angle, new Vector2(0.5f), new Vector2(side, side), SpriteEffects.None, 0f);
			}

			if (borderColor.A > 0)
			{
				Vector2[] corners = new Vector2[4];
				for (int i = 0; i < 4; i++)
				{
					float a = rotation + i * MathHelper.PiOver2;
					corners[i] = center + new Vector2((float)Math.Cos(a) * radius, (float)Math.Sin(a) * radius);
				}
				for (int i = 0; i < 4; i++)
				{
					DrawLine(sb, corners[i], corners[(i + 1) % 4], borderColor, borderWidth);
				}
			}
		}
	}
}
