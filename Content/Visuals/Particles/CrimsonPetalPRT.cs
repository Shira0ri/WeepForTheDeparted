using System;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace WeepForTheDeparted.Content.Visuals.Particles
{
	public class CrimsonPetalPRT : BasePRT
	{
		public override string Texture => "WeepForTheDeparted/Content/Visuals/DriftingPetal";
		public override bool CanPool => true;
		public override int InGame_World_MaxCount => 300;

		private float flipPhase;
		private float flipRate;
		private float spin;
		private float driftDir;
		private float fallSpeed;
		private Color initialColor;

		public CrimsonPetalPRT Configure(int lifetime, Color color, float fall = 0.55f)
		{
			Lifetime = lifetime;
			initialColor = color;
			Color = color;
			fallSpeed = fall;
			return this;
		}

		public override void Reset()
		{
			base.Reset();
			flipPhase = 0f;
			flipRate = 0f;
			spin = 0f;
			driftDir = 0f;
			fallSpeed = 0.55f;
			initialColor = default;
		}

		public override void SetProperty()
		{
			PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
			RenderLayer = PRTRenderLayer.BeforeInfernoRings;
			Opacity = 1f;

			flipPhase = Main.rand.NextFloat(MathHelper.TwoPi);
			flipRate = Main.rand.NextFloat(0.08f, 0.16f);
			spin = Main.rand.NextFloat(0.02f, 0.06f) * (Main.rand.NextBool() ? 1f : -1f);
			driftDir = Main.rand.NextBool() ? 1f : -1f;

			if (Lifetime <= 0)
			{
				Lifetime = Main.rand.Next(90, 160);
			}

			if (fallSpeed <= 0f)
			{
				fallSpeed = Main.rand.NextFloat(0.4f, 0.8f);
			}

			if (initialColor == default)
			{
				initialColor = new Color(245, 30, 55, 230);
			}

			Color = initialColor;
		}

		public override void AI()
		{
			flipPhase += flipRate;

			// Air resistance damping initial burst, horizontal breeze drifting
			Velocity *= 0.94f;
			Velocity.X += driftDir * 0.045f + (float)Math.Sin(flipPhase * 0.7f) * 0.035f;
			Velocity.Y = Math.Min(Velocity.Y + 0.015f, fallSpeed);
			Rotation += spin;

			float t = LifetimeCompletion;
			float fade = MathHelper.Clamp((t - 0.65f) / 0.35f, 0f, 1f);
			Color = Color.Lerp(initialColor, Color.Transparent, VaultUtils.EaseQuadIn(fade));
		}

		public override bool PreDraw(SpriteBatch spriteBatch)
		{
			Texture2D tex = TexValue;
			if (tex == null)
				return false;

			Vector2 origin = tex.Size() * 0.5f;
			Vector2 pos = Position - Main.screenPosition;

			float flip = Math.Abs((float)Math.Cos(flipPhase));
			float speedStretch = 1f + MathHelper.Clamp(Velocity.Length() / 6f, 0f, 0.45f);
			Vector2 drawScale = new Vector2(0.40f * flip, 0.52f * speedStretch) * Scale;

			spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, drawScale, SpriteEffects.None, 0f);

			// Glistening translucent highlight when petal faces camera
			float facing = MathHelper.Clamp(flip * 1.3f - 0.3f, 0f, 1f);
			if (facing > 0.05f)
			{
				Color highlight = new Color(255, 220, 230) * (Color.A / 255f * facing * 0.55f);
				spriteBatch.Draw(tex, pos, null, highlight, Rotation, origin, drawScale * new Vector2(0.6f, 0.75f), SpriteEffects.None, 0f);
			}

			return false;
		}
	}
}
