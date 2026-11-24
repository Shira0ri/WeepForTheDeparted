using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;
using WeepForTheDeparted.Content.Shaders;

namespace WeepForTheDeparted.Common.Visuals
{
	public class BlackHoleSky : CustomSky
	{
		private bool _isActive;
		private float _opacity;

		public const string Name = "WeepForTheDeparted:BlackHoleSky";

		public override void Update(GameTime gameTime)
		{
			_opacity = _isActive
				? MathHelper.Clamp(_opacity + 0.06f, 0f, 1f)
				: MathHelper.Clamp(_opacity - 0.06f, 0f, 1f);
		}

		public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
		{
			// NihilityDomainRender handles the sky and before-tiles passes via InnoVault with precise layer slicing and wave masking.
			// CustomSky remains active in SkyManager for vanilla lighting calculations and cloud suppression (GetCloudAlpha).
			return;
		}

		public override void Activate(Vector2 position, params object[] args) => _isActive = true;
		public override void Deactivate(params object[] args) => _isActive = false;
		public override void Reset() { _isActive = false; _opacity = 0f; }
		public override bool IsActive() => _isActive || _opacity > 0.001f;
		public override float GetCloudAlpha() => 1f - _opacity;
	}
}
