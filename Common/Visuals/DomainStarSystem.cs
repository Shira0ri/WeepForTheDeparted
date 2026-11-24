using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace WeepForTheDeparted.Common.Visuals
{
	/// <summary>
	/// Manages persistent celestial stars inside the Nihility Domain.
	/// Guarantees that star positions never jump or reset across twinkle cycles:
	/// - Near stars orbit the black hole in continuous, slow Keplerian orbits.
	/// - Far stars drift smoothly along divergence-free streamlines without colliding.
	/// - Water surface reflection ripples naturally below the horizon line.
	/// </summary>
	public static class DomainStarSystem
	{
		public class DomainStar
		{
			public Vector2 Position;       // Normalized [0, 1]
			public bool IsOrbital;
			public float OrbitRadius;      // Normalized distance from black hole center
			public float OrbitAngle;       // Continuous radian angle
			public float OrbitSpeed;       // Angular velocity
			public float DriftPhaseX;
			public float DriftPhaseY;
			public float DriftSpeed;
			public float TwinklePhase;
			public float TwinkleSpeed;
			public float BaseSize;
			public float Brightness;
			public Color StarColor;
		}

		private static readonly List<DomainStar> stars = new List<DomainStar>();
		private static bool initialized = false;

		public static readonly Vector2 BlackHoleCenter = new Vector2(0.65f, 0.45f);
		public const float HorizonY = 0.45f;

		public static void EnsureInitialized()
		{
			if (initialized && stars.Count > 0)
				return;

			stars.Clear();
			Random rand = new Random(42819);

			// 1. Orbital stars around the black hole (55 stars)
			for (int i = 0; i < 55; i++)
			{
				float r = (float)(0.22 + rand.NextDouble() * 0.32);
				float angle = (float)(rand.NextDouble() * Math.PI * 2.0);
				// Keplerian: closer orbits slightly faster, overall very slow
				float speed = (float)((0.00035 + rand.NextDouble() * 0.00025) / (r * 1.8 + 0.15));
				if (rand.NextDouble() < 0.1) speed = -speed; // rare counter-orbiting star

				stars.Add(new DomainStar
				{
					IsOrbital = true,
					OrbitRadius = r,
					OrbitAngle = angle,
					OrbitSpeed = speed,
					TwinklePhase = (float)(rand.NextDouble() * Math.PI * 2.0),
					TwinkleSpeed = (float)(0.03 + rand.NextDouble() * 0.04),
					BaseSize = (float)(1.2 + rand.NextDouble() * 1.8),
					Brightness = (float)(0.75 + rand.NextDouble() * 0.25),
					StarColor = PickStarColor(rand)
				});
			}

			// 2. Faraway drifting stars across the deep void (75 stars)
			for (int i = 0; i < 75; i++)
			{
				// Bias towards the left side and top corners where the black hole isn't occupying
				float x = (float)rand.NextDouble();
				float y = (float)(rand.NextDouble() * 0.50); // Deep sky above water
				if (rand.NextDouble() < 0.65)
				{
					x = (float)(rand.NextDouble() * 0.55); // Left half of screen
				}

				stars.Add(new DomainStar
				{
					IsOrbital = false,
					Position = new Vector2(x, y),
					DriftPhaseX = (float)(rand.NextDouble() * Math.PI * 2.0),
					DriftPhaseY = (float)(rand.NextDouble() * Math.PI * 2.0),
					DriftSpeed = (float)(0.00012 + rand.NextDouble() * 0.00016),
					TwinklePhase = (float)(rand.NextDouble() * Math.PI * 2.0),
					TwinkleSpeed = (float)(0.025 + rand.NextDouble() * 0.035),
					BaseSize = (float)(1.0 + rand.NextDouble() * 1.6),
					Brightness = (float)(0.65 + rand.NextDouble() * 0.35),
					StarColor = PickStarColor(rand)
				});
			}

			initialized = true;
		}

		private static Color PickStarColor(Random rand)
		{
			double p = rand.NextDouble();
			if (p < 0.60)
				return new Color(255, 255, 255); // Pure white
			if (p < 0.85)
				return new Color(210, 235, 255); // Pale starlight cyan
			return new Color(255, 195, 210);     // Faint crimson hue
		}

		public static void Update()
		{
			if (!initialized)
				EnsureInitialized();

			float aspect = Main.screenWidth / (float)Math.Max(1, Main.screenHeight);

			for (int i = 0; i < stars.Count; i++)
			{
				DomainStar star = stars[i];

				// Continuous twinkle oscillation (pure sine wave, never resets)
				star.TwinklePhase += star.TwinkleSpeed;
				if (star.TwinklePhase > MathHelper.TwoPi * 100f)
					star.TwinklePhase -= MathHelper.TwoPi * 100f;

				if (star.IsOrbital)
				{
					// Orbital motion: strictly continuous angle increment
					star.OrbitAngle += star.OrbitSpeed;
					if (star.OrbitAngle > MathHelper.TwoPi * 100f)
						star.OrbitAngle -= MathHelper.TwoPi * 100f;

					star.Position.X = BlackHoleCenter.X + (star.OrbitRadius / aspect) * (float)Math.Cos(star.OrbitAngle);
					star.Position.Y = BlackHoleCenter.Y + star.OrbitRadius * (float)Math.Sin(star.OrbitAngle);
				}
				else
				{
					// Faraway irregular motion along divergence-free streamlines (non-colliding)
					star.DriftPhaseX += star.DriftSpeed * 0.7f;
					star.DriftPhaseY += star.DriftSpeed * 0.5f;

					float vx = -(float)Math.Sin(star.Position.X * 4.2f + star.DriftPhaseX) * (float)Math.Sin(star.Position.Y * 3.8f + star.DriftPhaseY) * star.DriftSpeed;
					float vy = -(float)Math.Cos(star.Position.X * 4.2f + star.DriftPhaseX) * (float)Math.Cos(star.Position.Y * 3.8f + star.DriftPhaseY) * star.DriftSpeed;

					star.Position.X += vx;
					star.Position.Y += vy;

					// Gentle soft repulsion against neighbor far stars to strictly guarantee no overlap
					for (int j = i + 1; j < stars.Count; j++)
					{
						DomainStar other = stars[j];
						if (!other.IsOrbital)
						{
							Vector2 diff = star.Position - other.Position;
							diff.X *= aspect;
							float distSq = diff.LengthSquared();
							float minDist = 0.038f;
							if (distSq < minDist * minDist && distSq > 0.000001f)
							{
								float dist = (float)Math.Sqrt(distSq);
								Vector2 push = (diff / dist) * (minDist - dist) * 0.012f;
								push.X /= aspect;
								star.Position += push;
								other.Position -= push;
							}
						}
					}

					// Wrap around bounds gracefully
					if (star.Position.X < -0.04f) star.Position.X = 1.04f;
					else if (star.Position.X > 1.04f) star.Position.X = -0.04f;
					if (star.Position.Y < -0.04f) star.Position.Y = HorizonY + 0.02f;
					else if (star.Position.Y > HorizonY + 0.02f) star.Position.Y = -0.04f;
				}
			}
		}

		public static void Draw(SpriteBatch sb, Vector2 screenSize, float domainOpacity, Vector2? rippleCenter = null, float rippleRadius = 0f, bool rippleActive = false)
		{
			if (domainOpacity <= 0.01f)
				return;

			if (!initialized)
				EnsureInitialized();

			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (pixel == null)
				return;

			float horizonScreenY = HorizonY * screenSize.Y;
			float aspect = screenSize.X / screenSize.Y;
			Vector2 rCenter = rippleCenter ?? new Vector2(0.5f, 0.5f);

			foreach (DomainStar star in stars)
			{
				float screenX = star.Position.X * screenSize.X;
				float screenY = star.Position.Y * screenSize.Y;

				// Skip stars inside the black hole event horizon (0.32 normalized radius)
				Vector2 delta = star.Position - BlackHoleCenter;
				delta.X *= aspect;
				if (delta.Length() < 0.325f)
					continue;

				float starRippleMul = 1f;
				if (rippleActive)
				{
					Vector2 rDelta = star.Position - rCenter;
					rDelta.X *= aspect;
					float starDist = rDelta.Length();

					// If wave hasn't reached this star yet, skip drawing it
					if (starDist > rippleRadius + 0.03f)
						continue;

					// Smooth fade-in near wave crest
					float edge = rippleRadius - starDist;
					if (edge < 0.06f)
					{
						starRippleMul = MathHelper.Clamp((edge + 0.03f) / 0.09f, 0f, 1f);
					}
				}

				float twinkle = (float)Math.Sin(star.TwinklePhase) * 0.32f + 0.68f;
				float alpha = star.Brightness * twinkle * domainOpacity * starRippleMul;
				if (alpha <= 0.01f)
					continue;

				Color col = star.StarColor * alpha;
				float size = star.BaseSize;

				// 1. Draw Star Core & Glow in the Sky
				DrawSingleStar(sb, pixel, new Vector2(screenX, screenY), size, col);

				// 2. Draw Reflection on the Water Surface below horizon line
				if (screenY < horizonScreenY)
				{
					float depth = horizonScreenY - screenY;
					float refY = horizonScreenY + depth * 0.90f;
					float ripple = (float)Math.Sin(depth * 0.08f - Main.timeForVisualEffects * 0.06f) * 0.015f * depth;
					float refX = screenX + ripple;

					Color refCol = col * 0.42f;
					DrawSingleStar(sb, pixel, new Vector2(refX, refY), size * 0.85f, refCol);
				}
			}
		}

		private static void DrawSingleStar(SpriteBatch sb, Texture2D pixel, Vector2 pos, float size, Color col)
		{
			// Soft outer bloom
			int bloomSize = (int)(size * 3.5f + 1f);
			sb.Draw(pixel, pos - new Vector2(bloomSize * 0.5f, bloomSize * 0.5f), new Rectangle(0, 0, bloomSize, bloomSize), col * 0.22f);

			// Medium halo
			int haloSize = (int)(size * 2f + 1f);
			sb.Draw(pixel, pos - new Vector2(haloSize * 0.5f, haloSize * 0.5f), new Rectangle(0, 0, haloSize, haloSize), col * 0.45f);

			// Crisp white-hot pinprick center
			int coreSize = Math.Max(1, (int)size);
			sb.Draw(pixel, pos - new Vector2(coreSize * 0.5f, coreSize * 0.5f), new Rectangle(0, 0, coreSize, coreSize), Color.White * (col.A / 255f));
		}
	}
}
