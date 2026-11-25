using System;
using InnoVault;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.Systems;
using WeepForTheDeparted.Common.Visuals.Dismember;
using WeepForTheDeparted.Content.Shaders;

namespace WeepForTheDeparted.Common.Visuals
{
#pragma warning disable CS0618
	public class NihilityDomainShaderData : ScreenShaderData
	{
		public NihilityDomainShaderData(Ref<Effect> shader, string passName) : base(shader, passName)
		{
		}
#pragma warning restore CS0618

		public override void Apply()
		{
			Effect effect = Shader;
			if (effect != null)
			{
				Vector2 screenSize = new Vector2(Main.screenWidth, Main.screenHeight);
				effect.Parameters["uScreenSize"]?.SetValue(screenSize);
				effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
				float currentOpacity = (NihilityDomainSystem.IsDomainActive || NihilityDomainSystem.DomainClosingActive) ? NihilityDomainSystem.DomainIntensity : 0f;
				effect.Parameters["uOpacity"]?.SetValue(currentOpacity);
				effect.Parameters["uNegativeFlash"]?.SetValue(NihilityDomainSystem.NegativeFlash);
				effect.Parameters["uAnomalyPulse"]?.SetValue(NihilityDomainSystem.AnomalyPulse);

				bool waveActive = NihilityDomainSystem.DomainRevealActive || NihilityDomainSystem.DomainClosingActive;

				Vector2 rippleScreenPos = NihilityDomainSystem.DomainRevealOrigin - Main.screenPosition;
				Vector2 rippleUV = new Vector2(rippleScreenPos.X / screenSize.X, rippleScreenPos.Y / screenSize.Y);
				float rippleRadiusNorm = NihilityDomainSystem.DomainRevealRadius / screenSize.Y;

				effect.Parameters["uRippleCenter"]?.SetValue(rippleUV);
				effect.Parameters["uRippleRadius"]?.SetValue(rippleRadiusNorm);
				effect.Parameters["uRippleActive"]?.SetValue(waveActive ? 1.0f : 0.0f);
			}

			base.Apply();
		}
	}

	public class NihilityDomainSystem : ModSystem
	{
		public const string FilterName = "WeepForTheDeparted:NihilityDomain";

		public static bool IsDomainActive { get; private set; }
		public static float DomainIntensity { get; set; } = 0f;
		public static float TargetIntensity { get; set; } = 0f;
		public static float NegativeFlash { get; set; } = 0f;
		public static float AnomalyPulse { get; set; } = 0f;
		public static float TimeStopFactor { get; set; } = 1f;
		public static float MusicSilenceFactor { get; set; } = 0f;
		public static float TargetMusicSilence { get; set; } = 0f;

		// Wave 1: Domain Activation Reveal & Reverse Closing Wave
		public static bool DomainRevealActive { get; set; } = false;
		public static bool DomainClosingActive { get; set; } = false;
		public static Vector2 DomainRevealOrigin { get; set; }
		public static float DomainRevealRadius { get; set; } = 0f;
		public static float DomainRevealMaxRadius { get; set; } = 2800f;
		public static float DomainRevealProgress { get; set; } = 0f;
		public static float DomainRevealStep { get; set; } = 1f / 300f; // 5.0s for opening
		public static float DomainCloseStep { get; set; } = 1f / 150f;  // 2.5s for reverse wave

		// Wave 2: Ultimate Terrain Darkening (3.0s expand on unsheath, reverse contraction during sheathing to restore terrain)
		public static bool UltWaveActive { get; set; } = false;
		public static bool UltWaveClosingActive { get; set; } = false;
		public static Vector2 UltWaveOrigin { get; set; }
		public static float UltWaveRadius { get; set; } = 0f;
		public static float UltWaveMaxRadius { get; set; } = 2800f;
		public static float UltWaveProgress { get; set; } = 0f;
		public static float UltWaveStep { get; set; } = 1f / 180f;
		public static float UltWaveCloseStep { get; set; } = 1f / 154f;
		public static float WorldDarkenAmount { get; set; } = 0f;
		public static float TargetWorldDarken { get; set; } = 0f;

		public static void StartDomainReveal(Vector2 origin, float durationTicks = 300f)
		{
			DomainRevealActive = true;
			DomainClosingActive = false;
			DomainRevealOrigin = origin;
			DomainRevealRadius = 0f;
			DomainRevealProgress = 0f;
			DomainRevealStep = 1f / Math.Max(1f, durationTicks);
		}

		public static void StartDomainClosing(Vector2? origin = null)
		{
			if (!IsDomainActive || DomainClosingActive)
				return;

			DomainClosingActive = true;
			DomainRevealActive = false;

			// If opening wave was still running, contract smoothly from its current radius!
			// Otherwise start contracting from max radius!
			if (DomainRevealProgress >= 1f || DomainRevealRadius <= 0f)
			{
				DomainRevealProgress = 1f;
				DomainRevealRadius = DomainRevealMaxRadius;
			}

			DomainRevealOrigin = origin ?? (Main.LocalPlayer?.Center ?? Vector2.Zero);
			DomainCloseStep = 1f / 150f; // 2.5s closing reverse wave

			// Play domain collapse audio cue: reversing gong / resonant chime
			SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.40f, Volume = 0.85f }, DomainRevealOrigin);
		}

		public static void StartReflectionRipple(Vector2 origin)
		{
			// Ultimate unsheath wave: 3.0 seconds (180 ticks), darkens mundane terrain into domain reflection
			UltWaveActive = true;
			UltWaveClosingActive = false;
			UltWaveOrigin = origin;
			UltWaveRadius = 0f;
			UltWaveProgress = 0f;
			UltWaveStep = 1f / 180f;
			TargetWorldDarken = 1f;
			WorldDarkenAmount = 1f;

			// Ensure domain background reveal is complete so the background sky is 100% visible
			DomainRevealActive = false;
			DomainClosingActive = false;
			DomainRevealProgress = 1f;
			DomainRevealRadius = DomainRevealMaxRadius;
		}

		public static bool IsPositionInsideDomain(Vector2 worldPos)
		{
			if (!IsDomainActive || DomainRevealRadius <= 16f)
				return false;

			return Vector2.DistanceSquared(worldPos, DomainRevealOrigin) <= DomainRevealRadius * DomainRevealRadius;
		}

		public static void StartReflectionRippleReverse(Vector2 origin, float durationTicks = 154f)
		{
			if (UltWaveClosingActive || (!UltWaveActive && WorldDarkenAmount <= 0.005f))
				return;

			// If the opening wave was still expanding, start seamlessly from its current radius on the quad ease-in curve
			if (!UltWaveActive || UltWaveProgress >= 1f || UltWaveRadius <= 0f)
			{
				UltWaveProgress = 1f;
				UltWaveRadius = UltWaveMaxRadius;
			}
			else
			{
				float currentNorm = MathHelper.Clamp(UltWaveRadius / UltWaveMaxRadius, 0f, 1f);
				UltWaveProgress = (float)Math.Sqrt(currentNorm);
			}

			UltWaveActive = false;
			UltWaveClosingActive = true;
			UltWaveOrigin = origin;
			UltWaveCloseStep = UltWaveProgress / Math.Max(1f, durationTicks);
			TargetWorldDarken = 1f;
			WorldDarkenAmount = 1f;
		}

		public static void ResetReflectionRipple()
		{
			UltWaveActive = false;
			UltWaveClosingActive = false;
			UltWaveProgress = 0f;
			UltWaveRadius = 0f;
			TargetWorldDarken = 0f;
			WorldDarkenAmount = 0f;
		}

		public static void ActivateDomain(Vector2? origin = null)
		{
			// If already active and not closing, do not re-trigger domain activation
			if (IsDomainActive && !DomainClosingActive)
				return;

			IsDomainActive = true;
			DomainClosingActive = false;
			TargetIntensity = 1f;
			DomainIntensity = 1f; // All domain gameplay effects active immediately on activation!
			NegativeFlash = 0f;   // No jarring screen inversion; world remains normal until wave travels
			TimeStopFactor = 1f;  // Normal speed during domain; time dilation is reserved for the ultimate!

			Vector2 ripplePos = origin ?? (Main.LocalPlayer?.Center ?? Vector2.Zero);
			// Domain activation ripple: 5.0 seconds (300 ticks), gradually reveals background domain without unrendering terrain
			StartDomainReveal(ripplePos, durationTicks: 300f);

			if (!Main.dedServ)
			{
				SkyManager.Instance.Activate(BlackHoleSky.Name);
			}
		}

		public static void DeactivateDomain()
		{
			IsDomainActive = false;
			DomainClosingActive = false;
			DomainRevealActive = false;
			DomainRevealProgress = 0f;
			DomainRevealRadius = 0f;
			TargetIntensity = 0f;
			DomainIntensity = 0f;
			TimeStopFactor = 1f;
			TargetMusicSilence = 0f;
			ResetReflectionRipple();

			if (!Main.dedServ)
			{
				SkyManager.Instance.Deactivate(BlackHoleSky.Name);
			}
		}

		public override void Load()
		{
			if (Main.dedServ)
				return;

			try
			{
				On_FilterManager.EndCapture += Hook_FilterManager_EndCapture;

				SkyManager.Instance[BlackHoleSky.Name] = new BlackHoleSky();
				Filters.Scene[BlackHoleSky.Name] = new Filter(new ScreenShaderData("FilterMiniTower").UseColor(0.03f, 0.03f, 0.06f).UseOpacity(0f), EffectPriority.High);

				Effect domainShader = ShaderLoader.NihilityDomainShader;
				if (domainShader != null)
				{
					Filters.Scene[FilterName] = new Filter(new NihilityDomainShaderData(new Ref<Effect>(domainShader), "P0"), EffectPriority.VeryHigh);
				}
				else
				{
					// Fallback to FilterMiniTower with stark deep tone if custom fxc fails
					Filters.Scene[FilterName] = new Filter(new ScreenShaderData("FilterMiniTower")
						.UseColor(0.08f, 0.02f, 0.05f)
						.UseOpacity(0f), EffectPriority.VeryHigh);
				}
			}
			catch (Exception ex)
			{
				ModContent.GetInstance<WeepForTheDeparted>()?.Logger.Warn("Failed to register NihilityDomain filter: " + ex.Message);
			}
		}

		public override void Unload()
		{
			TargetMusicSilence = 0f;
			MusicSilenceFactor = 0f;

			if (!Main.dedServ)
			{
				On_FilterManager.EndCapture -= Hook_FilterManager_EndCapture;
			}
		}

		private static void Hook_FilterManager_EndCapture(On_FilterManager.orig_EndCapture orig, FilterManager self, RenderTarget2D finalTexture, RenderTarget2D screenTarget1, RenderTarget2D screenTarget2, Color clearColor)
		{
			orig(self, finalTexture, screenTarget1, screenTarget2, clearColor);

			// Redraw active enemies in 100% full original color on top of the black/white/red domain!
			if (IsDomainActive && DomainIntensity > 0.05f && !Main.gameMenu)
			{
				SpriteBatch sb = Main.spriteBatch;
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

				Rectangle screenRect = new Rectangle((int)Main.screenPosition.X - 100, (int)Main.screenPosition.Y - 100, Main.screenWidth + 200, Main.screenHeight + 200);

				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC npc = Main.npc[i];
					if (npc.active && !npc.friendly && !npc.isLikeATownNPC && npc.Hitbox.Intersects(screenRect))
					{
						// Do not redraw whole intact NPC over active dismemberment slices!
						if (NihilityDismember.IsDismembered(npc.whoAmI))
							continue;

						Main.instance.DrawNPCDirect(sb, npc, false, Main.screenPosition);
					}
				}

				sb.End();
			}
		}

		public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
		{
			if ((!IsDomainActive && !DomainClosingActive) || DomainIntensity <= 0.005f)
				return;

			// Wave-tied sunlight removal:
			// As the wave expands across the screen (0 to 1200px), sunlight smoothly dims to black.
			// When the reverse wave contracts into the player (1200px to 0), sunlight smoothly returns!
			float waveDim;
			if (DomainRevealActive || DomainClosingActive)
			{
				waveDim = MathHelper.Clamp(DomainRevealRadius / 1200f, 0f, 1f);
			}
			else
			{
				waveDim = 1f;
			}

			float effectIntensity = DomainIntensity * waveDim;
			if (effectIntensity <= 0.005f)
				return;

			// Extinguishes sunlight/daylight completely while preserving ALL light sources
			// (torches, shine potion, player glow, campfires, scabbard aura, projectiles)
			tileColor = Color.Lerp(tileColor, Color.Black, effectIntensity * 0.92f);
			backgroundColor = Color.Lerp(backgroundColor, Color.Black, effectIntensity * 0.98f);
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ)
				return;

			// Smooth transition for domain intensity
			DomainIntensity = MathHelper.Lerp(DomainIntensity, TargetIntensity, 0.12f);
			if (Math.Abs(DomainIntensity - TargetIntensity) < 0.005f)
			{
				DomainIntensity = TargetIntensity;
			}

			// Advance domain reveal wave expansion (Wave 1 Opening: 5.0s)
			if (DomainRevealActive)
			{
				DomainRevealProgress = Math.Min(DomainRevealProgress + DomainRevealStep, 1f);
				float t = 1f - DomainRevealProgress;
				float easeOut = 1f - t * t;
				DomainRevealRadius = easeOut * DomainRevealMaxRadius;

				if (DomainRevealProgress >= 1f)
				{
					DomainRevealActive = false;
				}
			}

			// Advance domain reverse closing wave contraction (Wave 1 Reverse: 2.5s)
			if (DomainClosingActive)
			{
				DomainRevealProgress = Math.Max(0f, DomainRevealProgress - DomainCloseStep);

				// Reverse wave contracts directly into the player!
				if (Main.LocalPlayer != null && Main.LocalPlayer.active)
				{
					DomainRevealOrigin = Main.LocalPlayer.Center;
				}

				// Quad ease-in contraction from max radius down to 0
				float t = DomainRevealProgress;
				DomainRevealRadius = t * t * DomainRevealMaxRadius;

				if (DomainRevealProgress <= 0f)
				{
					// Reverse wave has contracted to 0 and vanished into the player!
					DomainClosingActive = false;
					DomainRevealActive = false;
					DomainRevealRadius = 0f;
					IsDomainActive = false;
					TargetIntensity = 0f;
					DomainIntensity = 0f;
					TargetMusicSilence = 0f;
					ResetReflectionRipple();

					if (!Main.dedServ)
					{
						SkyManager.Instance.Deactivate(BlackHoleSky.Name);
					}

					Filters.Scene[FilterName]?.GetShader()?.UseOpacity(0f);
					if (Filters.Scene[FilterName].IsActive())
					{
						Filters.Scene.Deactivate(FilterName);
					}

					// Final crisp sheathing lock and bell sound
					SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.1f, Volume = 0.7f }, DomainRevealOrigin);
					SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.35f, Volume = 0.65f }, DomainRevealOrigin);
				}
			}

			// Advance ultimate terrain darkening wave expansion (Wave 2: 3.0s)
			if (UltWaveActive)
			{
				UltWaveProgress = Math.Min(UltWaveProgress + UltWaveStep, 1f);
				float t = 1f - UltWaveProgress;
				float easeOut = 1f - t * t;
				UltWaveRadius = easeOut * UltWaveMaxRadius;

				if (UltWaveProgress >= 1f)
				{
					UltWaveActive = false;
				}
			}

			// Advance ultimate reverse wave contraction during sheathing to restore terrain rendering
			if (UltWaveClosingActive)
			{
				UltWaveProgress = Math.Max(0f, UltWaveProgress - UltWaveCloseStep);

				float t = UltWaveProgress;
				UltWaveRadius = t * t * UltWaveMaxRadius;

				if (UltWaveProgress <= 0.0001f)
				{
					ResetReflectionRipple();
				}
			}

			WorldDarkenAmount = MathHelper.Lerp(WorldDarkenAmount, TargetWorldDarken, 0.14f);
			if (Math.Abs(WorldDarkenAmount - TargetWorldDarken) < 0.005f)
			{
				WorldDarkenAmount = TargetWorldDarken;
			}

			// Smooth transition for music silencing during ultimate execution
			MusicSilenceFactor = MathHelper.Lerp(MusicSilenceFactor, TargetMusicSilence, 0.10f);
			if (Math.Abs(MusicSilenceFactor - TargetMusicSilence) < 0.005f)
			{
				MusicSilenceFactor = TargetMusicSilence;
			}

			if (MusicSilenceFactor > 0.001f && Main.musicFade != null)
			{
				float maxMusicVol = 1f - MusicSilenceFactor;
				for (int i = 0; i < Main.musicFade.Length; i++)
				{
					if (Main.musicFade[i] > maxMusicVol)
					{
						Main.musicFade[i] = maxMusicVol;
					}
				}
			}

			// Update persistent celestial stars in the domain
			if (DomainIntensity > 0.01f)
			{
				DomainStarSystem.Update();
			}

			// Fade negative flash quickly
			if (NegativeFlash > 0.01f)
			{
				NegativeFlash = MathHelper.Lerp(NegativeFlash, 0f, 0.22f);
			}
			else
			{
				NegativeFlash = 0f;
			}

			// Fade anomaly pulse
			if (AnomalyPulse > 0.01f)
			{
				AnomalyPulse = MathHelper.Lerp(AnomalyPulse, 0f, 0.15f);
			}
			else
			{
				AnomalyPulse = 0f;
			}

			// Handle Filter activation
			float filterOpacity = (IsDomainActive || DomainClosingActive) ? DomainIntensity : 0f;
			if (filterOpacity > 0.01f)
			{
				if (!Filters.Scene[FilterName].IsActive())
				{
					Filters.Scene.Activate(FilterName, Main.LocalPlayer.Center);
				}
				Filters.Scene[FilterName]?.GetShader()?.UseOpacity(filterOpacity);

				// Spawn atmospheric falling red/silver rain and ash particles when fully open or opening
				if (!DomainClosingActive && (!DomainRevealActive || DomainRevealProgress > 0.35f) && Main.rand.NextBool(2))
				{
					Vector2 spawnPos = Main.screenPosition + new Vector2(Main.rand.NextFloat(Main.screenWidth), -20f);
					Dust d = Dust.NewDustPerfect(spawnPos, DustID.CrimsonTorch, new Vector2(Main.rand.NextFloat(-2f, -1f), Main.rand.NextFloat(8f, 16f)), 0, Color.Lerp(Color.Crimson, Color.White, Main.rand.NextFloat(0.3f)), Main.rand.NextFloat(0.8f, 1.4f));
					d.noGravity = true;
				}

				// Spawn suction convergence particles collapsing into the player during the reverse closing wave!
				if (DomainClosingActive && Main.rand.NextBool(2) && DomainRevealRadius > 20f)
				{
					Vector2 dustOffset = Main.rand.NextVector2CircularEdge(Math.Min(DomainRevealRadius * 0.85f, 900f), Math.Min(DomainRevealRadius * 0.85f, 900f));
					Vector2 dustPos = DomainRevealOrigin + dustOffset;
					Vector2 dustVel = (DomainRevealOrigin - dustPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(8f, 16f);
					Dust d = Dust.NewDustPerfect(dustPos, DustID.CrimsonTorch, dustVel, 0, Color.Lerp(Color.Crimson, Color.White, 0.35f), Main.rand.NextFloat(0.9f, 1.4f));
					d.noGravity = true;
				}
			}
			else
			{
				if (Filters.Scene[FilterName].IsActive())
				{
					Filters.Scene.Deactivate(FilterName);
				}
			}
		}
	}
}
