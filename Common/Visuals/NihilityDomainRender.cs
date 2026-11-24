using System;
using InnoVault;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using WeepForTheDeparted.Content.Shaders;

namespace WeepForTheDeparted.Common.Visuals
{
	/// <summary>
	/// InnoVault RenderHandle for the giant Black Hole domain background.
	/// Renders the black hole across both the deep sky pass and before-tiles pass,
	/// ensuring the black hole is visible in both open surface skies and underground/arena environments.
	/// </summary>
	public class NihilityDomainRender : RenderHandle
	{
		public override float Weight => 1.1f;

		public override void DrawSky(SpriteBatch spriteBatch, in SkySlice slice)
		{
			if (!NihilityDomainSystem.IsDomainActive || NihilityDomainSystem.DomainIntensity <= 0.005f)
				return;

			// Draw on the final background slice so it covers mountains, parallax layers, and clouds
			if (!slice.Final)
				return;

			// Safely end whatever batch was currently active
			try { spriteBatch.End(); } catch { }

			Vector2 screenSize = slice.RealScreenSize;

			// Wave 1: Domain Activation Reveal & Reverse Closing Wave
			bool revealActive = NihilityDomainSystem.DomainRevealActive || NihilityDomainSystem.DomainClosingActive;
			Vector2 revealScreenPos = NihilityDomainSystem.DomainRevealOrigin - Main.screenPosition;
			Vector2 revealUV = new Vector2(revealScreenPos.X / screenSize.X, revealScreenPos.Y / screenSize.Y);
			float revealRadiusNorm = NihilityDomainSystem.DomainRevealRadius / screenSize.Y;

			// 1. Draw Black Hole background with custom shader (masked by reveal wave if active)
			spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, ShaderLoader.BlackHoleSkyShader, Matrix.Identity);
			DrawBlackHole(spriteBatch, screenSize, NihilityDomainSystem.DomainIntensity, revealUV, revealRadiusNorm, revealActive);
			spriteBatch.End();

			// 2. Draw persistent celestial stars with Additive blend (masked by reveal wave if active)
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
			DomainStarSystem.Draw(spriteBatch, screenSize, NihilityDomainSystem.DomainIntensity, revealUV, revealRadiusNorm, revealActive);
			spriteBatch.End();

			// 3. Re-open standard background batch for vanilla's subsequent background passes
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, slice.BackgroundMatrix);
		}

		public override void DrawBeforeTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
		{
			if (!NihilityDomainSystem.IsDomainActive || NihilityDomainSystem.DomainIntensity <= 0.005f)
				return;

			SpriteBatchState state = spriteBatch.Capture();
			try { spriteBatch.End(); } catch { }

			Vector2 screenSize = new Vector2(Main.screenWidth, Main.screenHeight);

			// Wave 1: Domain Activation Reveal & Reverse Closing Wave
			bool revealActive = NihilityDomainSystem.DomainRevealActive || NihilityDomainSystem.DomainClosingActive;
			Vector2 revealScreenPos = NihilityDomainSystem.DomainRevealOrigin - Main.screenPosition;
			Vector2 revealUV = new Vector2(revealScreenPos.X / screenSize.X, revealScreenPos.Y / screenSize.Y);
			float revealRadiusNorm = NihilityDomainSystem.DomainRevealRadius / screenSize.Y;

			spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, ShaderLoader.BlackHoleSkyShader, Matrix.Identity);
			DrawBlackHole(spriteBatch, screenSize, NihilityDomainSystem.DomainIntensity, revealUV, revealRadiusNorm, revealActive);
			spriteBatch.End();

			// Draw persistent celestial stars with Additive blend (masked by reveal wave if active)
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
			DomainStarSystem.Draw(spriteBatch, screenSize, NihilityDomainSystem.DomainIntensity, revealUV, revealRadiusNorm, revealActive);
			spriteBatch.End();

			if (state.IsValid)
			{
				spriteBatch.Restart(in state);
			}
			else
			{
				spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
			}
		}

		public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
		{
			// When blade is drawn in the Ultimate, darken and unrender mundane tiles/walls,
			// replacing the terrain with the cosmic domain void and water reflection!
			// This leaves ONLY the player, enemies, and slashes visible on top of the domain.
			if (NihilityDomainSystem.WorldDarkenAmount <= 0.005f)
				return;

			float darkenAlpha = NihilityDomainSystem.WorldDarkenAmount;
			try { spriteBatch.End(); } catch { }

			Vector2 screenSize = new Vector2(Main.screenWidth, Main.screenHeight);

			// Wave 2: Ultimate Terrain Darkening Wave (3.0s, expanding from player to unrender terrain)
			bool ultWaveActive = NihilityDomainSystem.UltWaveActive && NihilityDomainSystem.UltWaveProgress < 0.98f;
			Vector2 ultScreenPos = NihilityDomainSystem.UltWaveOrigin - Main.screenPosition;
			Vector2 ultUV = new Vector2(ultScreenPos.X / screenSize.X, ultScreenPos.Y / screenSize.Y);
			float ultRadiusNorm = NihilityDomainSystem.UltWaveRadius / screenSize.Y;

			// 1. Draw Black Hole Sky directly over tiles, dynamically masked by the expanding ult wave
			spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, ShaderLoader.BlackHoleSkyShader, Matrix.Identity);
			DrawBlackHole(spriteBatch, screenSize, darkenAlpha, ultUV, ultRadiusNorm, ultWaveActive);
			spriteBatch.End();

			// 2. Draw Stars over the void
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
			DomainStarSystem.Draw(spriteBatch, screenSize, darkenAlpha, ultUV, ultRadiusNorm, ultWaveActive);
			spriteBatch.End();

			// Note: The stage contract for DrawNPCsOverTiles requires SpriteBatch to be inactive on exit (activeOnExit = false),
			// as vanilla's orig_DoDraw_DrawNPCsOverTiles begins its own SpriteBatch immediately afterwards.
		}

		private static Texture2D noiseTex = null;
		private static bool attemptedNoiseLoad = false;

		private static void DrawBlackHole(SpriteBatch sb, Vector2 screenSize, float opacity = 1f, Vector2? rippleCenter = null, float rippleRadius = 0f, bool rippleActive = false)
		{
			Effect shader = ShaderLoader.BlackHoleSkyShader;
			if (shader == null)
				return;

			if (!attemptedNoiseLoad && !Main.dedServ)
			{
				attemptedNoiseLoad = true;
				try
				{
					noiseTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/NoiseSoft01", AssetRequestMode.ImmediateLoad).Value;
				}
				catch { noiseTex = null; }
			}

			shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
			shader.Parameters["uResolution"]?.SetValue(screenSize);
			shader.Parameters["uCenter"]?.SetValue(new Vector2(0.65f, 0.45f)); // Offset upper right
			shader.Parameters["uOpacity"]?.SetValue(opacity);
			shader.Parameters["uRippleCenter"]?.SetValue(rippleCenter ?? new Vector2(0.5f, 0.5f));
			shader.Parameters["uRippleRadius"]?.SetValue(rippleRadius);
			shader.Parameters["uRippleActive"]?.SetValue(rippleActive ? 1.0f : 0.0f);

			shader.CurrentTechnique.Passes[0].Apply();
			Texture2D canvas = noiseTex ?? TextureAssets.MagicPixel.Value;
			sb.Draw(canvas, new Rectangle(0, 0, (int)screenSize.X, (int)screenSize.Y), Color.White);
		}
	}
}
