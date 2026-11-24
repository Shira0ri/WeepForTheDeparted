using System;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace WeepForTheDeparted.Content.Visuals.Particles
{
	public class VoidSparkPRT : BasePRT
	{
		public override string Texture => "InnoVault/Assets/Light";
		public override bool CanPool => true;
		public override int InGame_World_MaxCount => 800;

		private Color sparkColor;
		private float lengthMult = 1f;

		public VoidSparkPRT Configure(int lifetime, Color color, float length = 1f)
		{
			Lifetime = lifetime;
			sparkColor = color;
			Color = color;
			lengthMult = length;
			return this;
		}

		public override void Reset()
		{
			base.Reset();
			sparkColor = default;
			lengthMult = 1f;
		}

		public override void SetProperty()
		{
			PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
			RenderLayer = PRTRenderLayer.BeforeInfernoRings;

			if (Lifetime <= 0)
			{
				Lifetime = Main.rand.Next(14, 26);
			}

			if (sparkColor == default)
			{
				sparkColor = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
			}

			Color = sparkColor;
		}

		public override void AI()
		{
			Velocity *= 0.92f;
			Rotation = Velocity.ToRotation();

			float t = LifetimeCompletion;
			Color = Color.Lerp(sparkColor, Color.Transparent, VaultUtils.EaseQuadIn(t));
		}

		public override bool PreDraw(SpriteBatch spriteBatch)
		{
			Texture2D tex = TexValue;
			if (tex == null)
				return false;

			Vector2 origin = tex.Size() * 0.5f;
			Vector2 drawPos = Position - Main.screenPosition;

			float speed = Velocity.Length();
			float stretch = 1f + speed * 0.35f * lengthMult;
			Vector2 scale = new Vector2(stretch, 0.35f) * (Scale * 0.08f);

			// Outer luminous spark trail
			spriteBatch.Draw(tex, drawPos, null, Color * 0.75f, Rotation, origin, scale * 1.4f, SpriteEffects.None, 0f);

			// Inner bright core
			spriteBatch.Draw(tex, drawPos, null, Color.White * (Color.A / 255f), Rotation, origin, scale * 0.7f, SpriteEffects.None, 0f);

			return false;
		}
	}
}
