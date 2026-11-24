using System;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using WeepForTheDeparted.Content.Shaders;

namespace WeepForTheDeparted.Content.Visuals.Particles
{
	public class CrimsonBloomPRT : BasePRT
	{
		public override string Texture => "WeepForTheDeparted/Content/Visuals/CrimsonFlowerBloom";
		public override bool CanPool => true;
		public override int InGame_World_MaxCount => 200;

		private Color bloomColor = new Color(255, 30, 50);

		public CrimsonBloomPRT Configure(int lifetime, Color color)
		{
			Lifetime = lifetime;
			bloomColor = color;
			return this;
		}

		public override void Reset()
		{
			base.Reset();
			bloomColor = new Color(255, 30, 50);
		}

		public override void SetProperty()
		{
			PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
			RenderLayer = PRTRenderLayer.BeforeInfernoRings;

			if (Lifetime <= 0)
			{
				Lifetime = 36;
			}
		}

		public override void AI()
		{
			Velocity *= 0.90f;
		}

		public override bool PreDraw(SpriteBatch spriteBatch)
		{
			Texture2D tex = TexValue;
			if (tex == null)
				return false;

			float t = LifetimeCompletion;
			float progress = VaultUtils.EaseQuadOut(t);
			float curScale = Scale * MathHelper.Lerp(0.35f, 1.25f, progress);
			Vector2 origin = tex.Size() * 0.5f;
			Vector2 drawPos = Position - Main.screenPosition;

			Effect shader = ShaderLoader.FlowerBloomShader;
			if (shader != null)
			{
				shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
				shader.Parameters["uProgress"]?.SetValue(t);
				shader.Parameters["uBloomColor"]?.SetValue(new Vector3(bloomColor.R / 255f, bloomColor.G / 255f, bloomColor.B / 255f));
				shader.CurrentTechnique.Passes[0].Apply();
			}

			float alpha = 1f - MathHelper.Clamp((t - 0.45f) / 0.55f, 0f, 1f);
			Color drawCol = (shader != null ? Color.White : bloomColor) * alpha;

			spriteBatch.Draw(tex, drawPos, null, drawCol, Rotation + progress * 0.25f, origin, curScale, SpriteEffects.None, 0f);

			return false;
		}
	}
}
