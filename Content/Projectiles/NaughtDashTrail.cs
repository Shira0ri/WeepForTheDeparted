using System;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Content.Shaders;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles
{
	public class NaughtDashTrail : ModProjectile
	{
		public const int TrailLifetime = 22;

		public override string Texture => "WeepForTheDeparted/Content/Projectiles/NoiseSoft01";

		private Vector2 startPos;
		private Vector2 endPos;
		private bool initialized = false;

		public override void SetDefaults()
		{
			Projectile.width = 2;
			Projectile.height = 2;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = TrailLifetime;
		}

		public void InitTrail(Vector2 start, Vector2 end)
		{
			startPos = start;
			endPos = end;
			Projectile.Center = (start + end) * 0.5f;
			initialized = true;
		}

		public override void AI()
		{
			// Ambient spatial particles along the ribbon via InnoVault PRT
			if (!Main.dedServ && Main.rand.NextBool(2) && initialized)
			{
				float f = Main.rand.NextFloat();
				Vector2 p = Vector2.Lerp(startPos, endPos, f) + Main.rand.NextVector2Circular(8f, 8f);
				Vector2 vel = -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f) + Main.rand.NextVector2Circular(1f, 1f);
				Color col = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
				PRTLoader.NewParticle<VoidSparkPRT>(p, vel, col, 0.75f);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Effect fx = ShaderLoader.FlowShader;
			Texture2D noise = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/NoiseSoft01", AssetRequestMode.ImmediateLoad).Value;

			if (fx == null || noise == null || !initialized)
				return false;

			float progress = 1f - (Projectile.timeLeft / (float)TrailLifetime);
			float opacity = MathHelper.Clamp(Projectile.timeLeft / (float)TrailLifetime, 0f, 1f);

			Vector2 diff = startPos.To(endPos);
			float totalLen = diff.Length();
			if (totalLen < 8f)
				return false;

			Vector2 dir = diff / totalLen;
			Vector2 normal = new Vector2(-dir.Y, dir.X);
			float halfWidth = 32f;

			SpriteBatch sb = Main.spriteBatch;
			GraphicsDevice device = Main.graphics.GraphicsDevice;

			SpriteBatchState state = sb.Capture();
			sb.End();

			BlendState origBlend = device.BlendState;
			RasterizerState origRaster = device.RasterizerState;
			DepthStencilState origDepth = device.DepthStencilState;

			device.BlendState = BlendState.AlphaBlend;
			device.RasterizerState = RasterizerState.CullNone;
			device.DepthStencilState = DepthStencilState.None;

			Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, 0, 1);
			Matrix view = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0)) * Main.GameViewMatrix.TransformationMatrix;
			Matrix transformMatrix = view * projection;

			fx.Parameters["transformMatrix"]?.SetValue(transformMatrix);
			fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			fx.Parameters["uNoiseTex"]?.SetValue(noise);

			// Violet Nihility Palette
			fx.Parameters["uColHot"]?.SetValue(new Vector3(1.0f, 0.95f, 1.0f));
			fx.Parameters["uColBright"]?.SetValue(new Vector3(0.85f, 0.20f, 1.0f));
			fx.Parameters["uColDeep"]?.SetValue(new Vector3(0.42f, 0.02f, 0.65f));
			fx.Parameters["uColDark"]?.SetValue(new Vector3(0.08f, 0.01f, 0.14f));

			fx.Parameters["uOpacity"]?.SetValue(opacity * 0.95f);
			fx.Parameters["uRetract"]?.SetValue(MathHelper.Clamp(progress * 1.25f, 0f, 1f));
			fx.Parameters["uLenScale"]?.SetValue(totalLen / 260f);
			fx.Parameters["uSeed"]?.SetValue((float)(Projectile.identity % 10));
			fx.Parameters["uFlowMul"]?.SetValue(1.5f);
			fx.Parameters["uTearAmp"]?.SetValue(0.45f);
			fx.Parameters["uHeadBoost"]?.SetValue(1.0f);
			fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(1f - progress * 3f, 0f, 1f));

			// Subdivide into quad strip along path
			const int segments = 16;
			VertexPositionColorTexture[] verts = new VertexPositionColorTexture[(segments + 1) * 2];

			for (int i = 0; i <= segments; i++)
			{
				float u = i / (float)segments;
				Vector2 center = Vector2.Lerp(startPos, endPos, u);

				verts[i * 2] = new VertexPositionColorTexture(
					new Vector3(center - normal * halfWidth, 0f),
					Color.White,
					new Vector2(u, 0f)
				);

				verts[i * 2 + 1] = new VertexPositionColorTexture(
					new Vector3(center + normal * halfWidth, 0f),
					Color.White,
					new Vector2(u, 1f)
				);
			}

			foreach (EffectPass pass in fx.CurrentTechnique.Passes)
			{
				pass.Apply();
				device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, segments * 2);
			}

			device.BlendState = origBlend;
			device.RasterizerState = origRaster;
			device.DepthStencilState = origDepth;

			sb.Restart(in state);

			return false;
		}
	}
}
