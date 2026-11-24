using System;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace WeepForTheDeparted.Content.Visuals.Particles
{
	public class DimensionalExplosionPRT : BasePRT
	{
		public override string Texture => "WeepForTheDeparted/Content/Visuals/DimensionalExplosion";
		public override bool CanPool => true;
		public override int InGame_World_MaxCount => 200;

		private Color tintColor = Color.White;
		private float rotSpeed;

		public DimensionalExplosionPRT Configure(int lifetime, Color tint)
		{
			Lifetime = lifetime;
			tintColor = tint;
			return this;
		}

		public override void Reset()
		{
			base.Reset();
			tintColor = Color.White;
			rotSpeed = 0f;
		}

		public override void SetProperty()
		{
			PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
			RenderLayer = PRTRenderLayer.BeforeInfernoRings;

			if (Lifetime <= 0)
			{
				Lifetime = 28;
			}

			rotSpeed = Main.rand.NextFloat(-0.04f, 0.04f);
		}

		public override void AI()
		{
			Velocity *= 0.88f;
			Rotation += rotSpeed;
		}

		public override bool PreDraw(SpriteBatch spriteBatch)
		{
			Texture2D tex = TexValue;
			if (tex == null)
				return false;

			float t = LifetimeCompletion;
			float progress = VaultUtils.EaseQuadOut(t);
			float fade = 1f - progress;
			Vector2 drawPos = Position - Main.screenPosition;
			Vector2 origin = tex.Size() * 0.5f;

			// Expanding shockwave scaling
			float curScale = Scale * MathHelper.Lerp(0.4f, 1.35f, progress);

			// Outer blooming veil
			Color veilColor = (tintColor == Color.White ? new Color(255, 45, 80) : tintColor) * (fade * fade);
			spriteBatch.Draw(tex, drawPos, null, veilColor * 0.75f, Rotation, origin, curScale * 1.15f, SpriteEffects.None, 0f);

			// Intense white-hot core flash
			Color coreColor = Color.White * (float)Math.Pow(fade, 1.8f);
			spriteBatch.Draw(tex, drawPos, null, coreColor, Rotation, origin, curScale, SpriteEffects.None, 0f);

			return false;
		}
	}
}
