using System;
using System.Collections.Generic;
using InnoVault;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Content.Projectiles;
using WeepForTheDeparted.Content.Shaders;

namespace WeepForTheDeparted.Common.Visuals
{
	/// <summary>
	/// InnoVault RenderHandle rendering 2.5D perspective layers for SlashedDreamUltimate in two passes
	/// bracketing Terraria's native player draw:
	/// - DrawBeforePlayers: water ripples, far (-Z) SlashShader ribbons, Z-sorted PlayerCloneRenderer afterimages, and far 3D blade.
	/// - DrawAfterPlayers: front 3D blade/sheath, near (+Z) SlashShader ribbons, cleaved reality seams, crimson vignette, and gleam flashes.
	/// </summary>
	public sealed class Ult3DRenderHandle : RenderHandle
	{
		private static readonly List<SlashedDreamUltimate> activeUltsScratch = new();
		private static readonly List<UltAfterimage3D> sortedAfterimagesScratch = new();
		private static readonly List<VertexPositionColorTexture> farRibbonVertsScratch = new();
		private static readonly List<VertexPositionColorTexture> nearRibbonVertsScratch = new();
		private static readonly List<VertexPositionColorTexture> rippleVertsScratch = new();

		private static Texture2D cachedUltBladeTex;
		private static Texture2D cachedSheathTex;
		private static Texture2D cachedHiltTex;
		private static Texture2D cachedBrushTex;
		private static Texture2D cachedNoiseTex;

		public static bool IsUltShaderAndRenderHealthy { get; private set; } = true;
		private static int consecutiveRenderFailures = 0;

		public override float Weight => 1.2f;

		public override PlayerDrawPass PlayerDrawPasses => PlayerDrawPass.AfterProjectiles;

		public override bool Active => !Main.gameMenu && !Main.dedServ && HasAnyActiveUltimate();

		public override void OnWorldUnload()
		{
			ResetSessionHealth();
		}

		internal static void ResetSessionHealth()
		{
			IsUltShaderAndRenderHealthy = true;
			consecutiveRenderFailures = 0;
		}

		internal static void DisposeAllResources()
		{
			ResetSessionHealth();
			cachedUltBladeTex = null;
			cachedSheathTex = null;
			cachedHiltTex = null;
			cachedBrushTex = null;
			cachedNoiseTex = null;
		}

		private static bool HasAnyActiveUltimate()
		{
			int ultType = ModContent.ProjectileType<SlashedDreamUltimate>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.type == ultType)
					return true;
			}
			return false;
		}

		private static void CollectActiveUltimates()
		{
			activeUltsScratch.Clear();
			int ultType = ModContent.ProjectileType<SlashedDreamUltimate>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.type == ultType && p.ModProjectile is SlashedDreamUltimate ult
					&& ult.Owner != null && ult.Owner.active && !ult.Owner.dead && !ult.Owner.ghost)
				{
					activeUltsScratch.Add(ult);
				}
			}
		}

		private static void EnsureAssets()
		{
			if (cachedUltBladeTex == null)
			{
				try { cachedUltBladeTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtUltBlade", AssetRequestMode.ImmediateLoad).Value; } catch { }
			}
			if (cachedSheathTex == null)
			{
				try { cachedSheathTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtSheathBody", AssetRequestMode.ImmediateLoad).Value; }
				catch
				{
					try { cachedSheathTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtSheath", AssetRequestMode.ImmediateLoad).Value; } catch { }
				}
			}
			if (cachedHiltTex == null)
			{
				try { cachedHiltTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtUnsheathHilt", AssetRequestMode.ImmediateLoad).Value; } catch { }
			}
			if (cachedBrushTex == null)
			{
				try { cachedBrushTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/SlashBrush01", AssetRequestMode.ImmediateLoad).Value; } catch { }
			}
			if (cachedNoiseTex == null)
			{
				try { cachedNoiseTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/RiftNoise", AssetRequestMode.ImmediateLoad).Value; } catch { }
			}
		}

		private static void ApplyUltQuadPass(
			Effect ultQuad,
			string techniqueName,
			Matrix transformMatrix,
			Texture2D texture,
			Color silhouetteColor,
			float silhouetteStrength)
		{
			EffectTechnique tech = ultQuad.Techniques[techniqueName];
			if (tech != null)
				ultQuad.CurrentTechnique = tech;

			ultQuad.Parameters["transformMatrix"]?.SetValue(transformMatrix);
			ultQuad.Parameters["uQuadTex"]?.SetValue(texture ?? TextureAssets.MagicPixel.Value);
			ultQuad.Parameters["uSilhouetteColor"]?.SetValue(new Vector4(
				silhouetteColor.R / 255f,
				silhouetteColor.G / 255f,
				silhouetteColor.B / 255f,
				silhouetteColor.A / 255f));
			ultQuad.Parameters["uSilhouetteStrength"]?.SetValue(silhouetteStrength);
			ultQuad.CurrentTechnique.Passes[0].Apply();
		}

		private static Matrix BuildWorldProjectionMatrix()
		{
			Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, -4000f, 4000f);
			Matrix view = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0f)) * Main.GameViewMatrix.TransformationMatrix;
			return view * projection;
		}

		public override void DrawBeforePlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
		{
			if (Main.gameMenu || Main.dedServ)
				return;

			CollectActiveUltimates();
			if (activeUltsScratch.Count == 0)
				return;

			Effect ultQuad = ShaderLoader.UltQuadShader;
			if (!IsUltShaderAndRenderHealthy || ultQuad == null || ultQuad.IsDisposed)
				return;

			EnsureAssets();
			Matrix transformMatrix = BuildWorldProjectionMatrix();

			BlendState origBlend = graphicsDevice.BlendState;
			RasterizerState origRaster = graphicsDevice.RasterizerState;
			DepthStencilState origDepth = graphicsDevice.DepthStencilState;
			SamplerState origSampler = graphicsDevice.SamplerStates[0];
			Texture origTexture0 = graphicsDevice.Textures[0];

			try
			{
				for (int i = 0; i < activeUltsScratch.Count; i++)
				{
					RenderUltimateBehindPlayer(spriteBatch, graphicsDevice, ultQuad, transformMatrix, activeUltsScratch[i]);
				}
			}
			catch (Exception ex)
			{
				consecutiveRenderFailures++;
				IsUltShaderAndRenderHealthy = false;
				ModContent.GetInstance<WeepForTheDeparted>()?.Logger.Warn($"[Ult3DRenderHandle] DrawBeforePlayers fallback triggered (#{consecutiveRenderFailures}): {ex}");
			}
			finally
			{
				graphicsDevice.Textures[0] = origTexture0;
				graphicsDevice.BlendState = origBlend;
				graphicsDevice.RasterizerState = origRaster;
				graphicsDevice.DepthStencilState = origDepth;
				graphicsDevice.SamplerStates[0] = origSampler;
			}
		}

		public override void DrawAfterPlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
		{
			if (Main.gameMenu || Main.dedServ)
				return;

			CollectActiveUltimates();
			if (activeUltsScratch.Count == 0)
				return;

			Effect ultQuad = ShaderLoader.UltQuadShader;
			if (!IsUltShaderAndRenderHealthy || ultQuad == null || ultQuad.IsDisposed)
				return;

			EnsureAssets();
			Matrix transformMatrix = BuildWorldProjectionMatrix();

			BlendState origBlend = graphicsDevice.BlendState;
			RasterizerState origRaster = graphicsDevice.RasterizerState;
			DepthStencilState origDepth = graphicsDevice.DepthStencilState;
			SamplerState origSampler = graphicsDevice.SamplerStates[0];
			Texture origTexture0 = graphicsDevice.Textures[0];

			try
			{
				for (int i = 0; i < activeUltsScratch.Count; i++)
				{
					RenderUltimateFrontOfPlayer(spriteBatch, graphicsDevice, ultQuad, transformMatrix, activeUltsScratch[i]);
				}
				consecutiveRenderFailures = 0;
			}
			catch (Exception ex)
			{
				consecutiveRenderFailures++;
				IsUltShaderAndRenderHealthy = false;
				ModContent.GetInstance<WeepForTheDeparted>()?.Logger.Warn($"[Ult3DRenderHandle] DrawAfterPlayers fallback triggered (#{consecutiveRenderFailures}): {ex}");
			}
			finally
			{
				graphicsDevice.Textures[0] = origTexture0;
				graphicsDevice.BlendState = origBlend;
				graphicsDevice.RasterizerState = origRaster;
				graphicsDevice.DepthStencilState = origDepth;
				graphicsDevice.SamplerStates[0] = origSampler;
			}
		}

		private static void RenderUltimateBehindPlayer(
			SpriteBatch sb,
			GraphicsDevice gd,
			Effect ultQuad,
			Matrix transformMatrix,
			SlashedDreamUltimate ult)
		{
			Vector2 origin = ult.SafeFrameOrigin;
			int facing = ult.LockedFacingDir;
			float playerZ = ult.PlayerPos3D.Z;

			Vector2 cameraDrift = ult.Owner.TryGetModPlayer<NaughtPlayer>(out var np) && np.UltCameraLocked
				? (Main.screenPosition - np.UltBaseAnchorScreenPos)
				: Vector2.Zero;

			gd.RasterizerState = RasterizerState.CullNone;
			gd.DepthStencilState = DepthStencilState.None;

			// 1. Reflective Water Surface Ripples (projected on water plane with subtle depth parallax)
			DrawWaterRipples(gd, ultQuad, transformMatrix, ult, origin, facing, cameraDrift);

			// 2. Far Layer of 3D Slash Ribbons (behind player in Z)
			DrawSlashRibbonsSplit(gd, transformMatrix, ult, origin, facing, playerZ, drawNearHalf: false);

			// 3. Z-sorted 3D Afterimage Ghosts via native PlayerCloneRenderer directly onto screenTarget
			DrawAfterimages(sb, ult, origin, facing);

			// 4a. Blade behind player when mid-Z sits further back than player Z
			if (!ult.IsSheathPoseActive && ult.BladeMidZ < playerZ - 6f)
			{
				DrawBladeGhostsAndSword(gd, ultQuad, transformMatrix, ult, origin, facing);
			}
		}

		private static void RenderUltimateFrontOfPlayer(
			SpriteBatch sb,
			GraphicsDevice gd,
			Effect ultQuad,
			Matrix transformMatrix,
			SlashedDreamUltimate ult)
		{
			Vector2 origin = ult.SafeFrameOrigin;
			int facing = ult.LockedFacingDir;
			float playerZ = ult.PlayerPos3D.Z;

			gd.RasterizerState = RasterizerState.CullNone;
			gd.DepthStencilState = DepthStencilState.None;

			// 4b. Blade / Sheath in front of native player
			bool drawBladeBehind = !ult.IsSheathPoseActive && ult.BladeMidZ < playerZ - 6f;
			if (!drawBladeBehind)
			{
				if (ult.IsSheathPoseActive)
				{
					DrawSheathAndHilt3D(sb, ult, origin, facing);
				}
				else
				{
					DrawBladeGhostsAndSword(gd, ultQuad, transformMatrix, ult, origin, facing);
				}
			}

			// 5. Near Layer of 3D Slash Ribbons (in front of player in Z)
			DrawSlashRibbonsSplit(gd, transformMatrix, ult, origin, facing, playerZ, drawNearHalf: true);

			// 6. Monumental Cleaved Reality Seams (Stygian Resurge & dimensional scars)
			DrawCleavedSeams(gd, transformMatrix, ult);

			// 7. Subtle Dark-Crimson Edge Vignette Anchoring Smooth Camera Glides
			if (ult.Owner.TryGetModPlayer<NaughtPlayer>(out var np) && np.UltCameraVignetteIntensity > 0.01f)
			{
				DrawFramingVignette(gd, ultQuad, np.UltCameraVignetteIntensity);
			}

			// 8. Scabbard Mouth / Blade Condensation Anamorphic Gleam Flashes
			DrawGleamFlashes(sb, ult);
		}

		private static void DrawFramingVignette(GraphicsDevice gd, Effect ultQuad, float intensity)
		{
			gd.BlendState = BlendState.AlphaBlend;
			gd.SamplerStates[0] = SamplerState.LinearClamp;
			ApplyUltQuadPass(ultQuad, "VignetteTech", Matrix.Identity, TextureAssets.MagicPixel.Value, Color.Black, silhouetteStrength: 0f);

			Color vigCol = new Color(18, 2, 7, 138) * MathHelper.Clamp(intensity, 0f, 1f);
			VertexPositionColorTexture[] verts =
			{
				new(new Vector3(-1f, 1f, 0f), vigCol, new Vector2(0f, 0f)),
				new(new Vector3(1f, 1f, 0f), vigCol, new Vector2(1f, 0f)),
				new(new Vector3(-1f, -1f, 0f), vigCol, new Vector2(0f, 1f)),
				new(new Vector3(1f, -1f, 0f), vigCol, new Vector2(1f, 1f))
			};

			gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
		}

		private static void DrawWaterRipples(
			GraphicsDevice gd,
			Effect ultQuad,
			Matrix transformMatrix,
			SlashedDreamUltimate ult,
			Vector2 origin,
			int facing,
			Vector2 cameraDrift)
		{
			if (ult.WaterRipples.Count == 0)
				return;

			gd.BlendState = BlendState.Additive;
			gd.SamplerStates[0] = SamplerState.LinearClamp;
			ApplyUltQuadPass(ultQuad, "RippleTech", transformMatrix, TextureAssets.MagicPixel.Value, Color.White, silhouetteStrength: 0f);

			const int Segments = 32;

			for (int r = 0; r < ult.WaterRipples.Count; r++)
			{
				UltWaterRipple3D ripple = ult.WaterRipples[r];
				float progress = MathHelper.Clamp(ripple.Age / (float)Math.Max(1, ripple.MaxAge), 0f, 1f);
				float ease = Ult3DProjection.EaseOutCubic(progress);
				float alpha = (1f - progress * progress) * ripple.Opacity;
				if (alpha <= 0.01f)
					continue;

				float depthParallaxFactor = MathHelper.Clamp(-ripple.Center3D.Z / 420f, -0.35f, 0.35f) * 0.14f;
				Vector2 parallaxOrigin = origin + cameraDrift * depthParallaxFactor;

				float radius = MathHelper.Lerp(10f, ripple.MaxRadius, ease);
				float thickness = MathHelper.Lerp(5.5f, 1.4f, progress);

				// Outer crimson ring
				Color outerCol = new Color(238, 30, 68, 0) * (alpha * 0.65f);
				EmitProjectedWaterRing(gd, ripple.Center3D, parallaxOrigin, facing, radius, thickness * 1.6f, outerCol, Segments);

				// Crisp pale rose-white crest ring
				Color innerCol = new Color(255, 220, 232, 0) * (alpha * 0.85f);
				EmitProjectedWaterRing(gd, ripple.Center3D, parallaxOrigin, facing, radius * 0.96f, thickness * 0.65f, innerCol, Segments);

				if (ripple.DoubleRing && progress > 0.14f)
				{
					float subProg = MathHelper.Clamp((progress - 0.14f) / 0.86f, 0f, 1f);
					float subEase = Ult3DProjection.EaseOutCubic(subProg);
					float subRadius = MathHelper.Lerp(8f, ripple.MaxRadius * 0.72f, subEase);
					float subAlpha = (1f - subProg * subProg) * ripple.Opacity * 0.55f;
					Color subCol = new Color(220, 40, 85, 0) * subAlpha;
					EmitProjectedWaterRing(gd, ripple.Center3D, parallaxOrigin, facing, subRadius, thickness * 0.9f, subCol, Segments);
				}
			}
		}

		private static void EmitProjectedWaterRing(
			GraphicsDevice gd,
			UltCoord3D center3D,
			Vector2 origin,
			int facing,
			float radius,
			float baseWidth,
			Color color,
			int segments)
		{
			if (radius <= 1f || color.R + color.G + color.B == 0)
				return;

			rippleVertsScratch.Clear();
			float halfW = Math.Max(0.8f, baseWidth * 0.5f);

			for (int i = 0; i <= segments; i++)
			{
				float u = i / (float)segments;
				float theta = MathHelper.TwoPi * u;
				float cos = MathF.Cos(theta);
				float sin = MathF.Sin(theta);

				// Flatten vertical radius slightly on the horizontal (F, Z) water plane so rings squash cleanly in perspective
				UltCoord3D outer3D = new(center3D.F + cos * radius, center3D.U, center3D.Z + sin * radius * 0.82f);
				UltCoord3D inner3D = new(center3D.F + cos * Math.Max(1f, radius - halfW * 2f), center3D.U, center3D.Z + sin * Math.Max(1f, radius - halfW * 2f) * 0.82f);

				Vector2 outer2D = Ult3DProjection.Project(outer3D, origin, facing, out float perspOut);
				Vector2 inner2D = Ult3DProjection.Project(inner3D, origin, facing, out _);

				// Ensure minimum visible pixel band even near perspective horizon
				Vector2 radial2D = outer2D - inner2D;
				float minBand = Math.Max(1.0f, baseWidth * perspOut * 0.5f);
				if (radial2D.LengthSquared() < minBand * minBand)
				{
					Vector2 center2D = Ult3DProjection.Project(center3D, origin, facing, out _);
					Vector2 outDir = outer2D - center2D;
					if (outDir.LengthSquared() > 1e-4f)
					{
						outDir.Normalize();
						outer2D = inner2D + outDir * minBand;
					}
				}

				rippleVertsScratch.Add(new VertexPositionColorTexture(new Vector3(outer2D, 0f), color, new Vector2(u, 0f)));
				rippleVertsScratch.Add(new VertexPositionColorTexture(new Vector3(inner2D, 0f), color, new Vector2(u, 1f)));
			}

			VertexPositionColorTexture[] verts = rippleVertsScratch.ToArray();
			gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, segments * 2);
		}

		private static void DrawAfterimages(
			SpriteBatch sb,
			SlashedDreamUltimate ult,
			Vector2 origin,
			int facing)
		{
			if (ult.Afterimages.Count == 0)
				return;

			sortedAfterimagesScratch.Clear();
			sortedAfterimagesScratch.AddRange(ult.Afterimages);
			sortedAfterimagesScratch.Sort(static (a, b) => a.Pos3D.Z.CompareTo(b.Pos3D.Z));

			PlayerCloneRenderer.Prepare(ult.Owner);

			bool batchBegan = false;
			try
			{
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
				batchBegan = true;

				for (int i = 0; i < sortedAfterimagesScratch.Count; i++)
				{
					UltAfterimage3D ghost = sortedAfterimagesScratch[i];
					float life = 1f - MathHelper.Clamp(ghost.Age / (float)Math.Max(1, ghost.MaxAge), 0f, 1f);
					float alpha = life * life * ghost.InitialAlpha;
					if (alpha <= 0.01f)
						continue;

					Vector2 projectedCenter = Ult3DProjection.Project(ghost.Pos3D, origin, facing, out float perspScale);
					float depthRatio = MathHelper.Clamp((ghost.Pos3D.Z - Ult3DProjection.MinZ) / (Ult3DProjection.MaxZ - Ult3DProjection.MinZ), 0f, 1f);
					Color inkTint = ghost.StarkWhiteLead
						? Color.Lerp(new Color(255, 80, 115), new Color(255, 220, 232), 0.55f)
						: Color.Lerp(new Color(145, 16, 42), new Color(238, 30, 62), depthRatio);

					PlayerCloneRenderer.DrawPreparedUltAfterimage(
						ult.Owner,
						projectedCenter,
						perspScale,
						in ghost,
						inkTint,
						tintStrength: ghost.StarkWhiteLead ? 0.85f : 0.72f,
						alpha: Math.Min(1f, ghost.StarkWhiteLead ? alpha * 1.15f : alpha));
				}

				sb.End();
				batchBegan = false;
			}
			catch
			{
				if (batchBegan)
				{
					try { sb.End(); } catch { }
				}
				throw;
			}
		}

		private static void DrawBladeGhostsAndSword(
			GraphicsDevice gd,
			Effect ultQuad,
			Matrix transformMatrix,
			SlashedDreamUltimate ult,
			Vector2 origin,
			int facing)
		{
			if (cachedUltBladeTex == null)
				return;

			gd.SamplerStates[0] = SamplerState.LinearClamp;
			VertexPositionColorTexture[] bladeQuad = new VertexPositionColorTexture[4];

			// 1. Trailing blade motion ghosts
			for (int i = 0; i < ult.BladeGhosts.Count; i++)
			{
				UltBladeGhost3D bg = ult.BladeGhosts[i];
				float life = 1f - MathHelper.Clamp(bg.Age / (float)Math.Max(1, bg.MaxAge), 0f, 1f);
				float alpha = life * life * bg.Opacity;
				if (alpha <= 0.01f)
					continue;

				gd.BlendState = BlendState.Additive;
				BuildBlade3DQuad(
					bg.Hilt3D,
					bg.Tip3D,
					bg.BroadsideNormal3D,
					bg.DrawDirection,
					origin,
					facing,
					new Color(255, 32, 62, 0) * alpha,
					widthBoost: 1.18f,
					bladeQuad);

				ApplyUltQuadPass(ultQuad, "QuadLinearTech", transformMatrix, cachedUltBladeTex, Color.White, silhouetteStrength: 0f);
				gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, bladeQuad, 0, 2);
			}

			// 2. Crimson/White blade aura glow pass
			float midZ = (ult.Hilt3D.Z + ult.Tip3D.Z) * 0.5f;
			float farFactor = MathHelper.Clamp(-midZ / 340f, 0f, 1f);
			float auraAlpha = ult.BladeGlowIntensity * (1f - farFactor * 0.35f);
			if (auraAlpha > 0.02f)
			{
				gd.BlendState = BlendState.Additive;
				Color auraCol = Color.Lerp(new Color(255, 28, 56, 0), new Color(255, 225, 235, 0), MathHelper.Clamp(auraAlpha - 0.5f, 0f, 0.8f)) * auraAlpha;
				BuildBlade3DQuad(
					ult.Hilt3D,
					ult.Tip3D,
					ult.BladeBroadsideNormal3D,
					ult.VisualDrawDirection,
					origin,
					facing,
					auraCol,
					widthBoost: 1.24f,
					bladeQuad);

				ApplyUltQuadPass(ultQuad, "QuadLinearTech", transformMatrix, cachedUltBladeTex, Color.White, silhouetteStrength: 0f);
				gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, bladeQuad, 0, 2);
			}

			// 3. Main perspective-warped 3D Odachi sprite
			gd.BlendState = BlendState.AlphaBlend;
			Color mainTint = Color.Lerp(Color.White, new Color(165, 115, 145), farFactor * 0.45f);
			BuildBlade3DQuad(
				ult.Hilt3D,
				ult.Tip3D,
				ult.BladeBroadsideNormal3D,
				ult.VisualDrawDirection,
				origin,
				facing,
				mainTint,
				widthBoost: 1.0f,
				bladeQuad);

			ApplyUltQuadPass(ultQuad, "QuadLinearTech", transformMatrix, cachedUltBladeTex, Color.White, silhouetteStrength: 0f);
			gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, bladeQuad, 0, 2);
		}

		/// <summary>
		/// Projects NaughtUltBlade.png (180x180, grip at (27,155), tip at (172,8)) into a 3D perspective quad
		/// so the blade naturally swells when whipping toward +Z and foreshortens/tapers into -Z.
		/// </summary>
		private static void BuildBlade3DQuad(
			UltCoord3D hilt3D,
			UltCoord3D tip3D,
			UltCoord3D broadsideNormal3D,
			int drawDirection,
			Vector2 origin,
			int facing,
			Color vertexColor,
			float widthBoost,
			VertexPositionColorTexture[] verts)
		{
			// NaughtUltBlade.png (180x180): grip=(27,155), tip=(172,8)
			const float texSize = 180f;
			Vector2 tGrip = new(27f, 155f);
			Vector2 tTip = new(172f, 8f);
			Vector2 dTex = tTip - tGrip; // (145, -147)
			float dTexLen = dTex.Length(); // ~206.48px
			Vector2 dTexNorm = dTex / dTexLen;
			Vector2 nTex = new(-dTexNorm.Y, dTexNorm.X); // perpendicular in texture pixel space

			// Normalize broadside normal perpendicular to blade axis in 3D
			UltCoord3D bladeDir3D = (tip3D - hilt3D).SafeNormalize(new UltCoord3D(drawDirection, 0.2f, 0f));
			UltCoord3D orthoSide3D = broadsideNormal3D - bladeDir3D * (broadsideNormal3D.F * bladeDir3D.F + broadsideNormal3D.U * bladeDir3D.U + broadsideNormal3D.Z * bladeDir3D.Z);
			orthoSide3D = orthoSide3D.SafeNormalize(new UltCoord3D(0f, 1f, 0f));

			float spriteScaleRatio = (tip3D - hilt3D).Length() / dTexLen;
			float sideFlip = drawDirection < 0 ? -1f : 1f;

			// Map the 4 texture corners (0,0), (180,0), (0,180), (180,180)
			for (int k = 0; k < 4; k++)
			{
				float px = (k % 2 == 0) ? 0f : texSize;
				float py = (k < 2) ? 0f : texSize;

				Vector2 relTex = new Vector2(px, py) - tGrip;
				float alongFrac = Vector2.Dot(relTex, dTexNorm) / dTexLen;
				float acrossPx = -Vector2.Dot(relTex, nTex) * sideFlip * spriteScaleRatio * widthBoost;

				UltCoord3D corner3D = UltCoord3D.Lerp(hilt3D, tip3D, alongFrac) + orthoSide3D * acrossPx;
				Vector2 corner2D = Ult3DProjection.Project(corner3D, origin, facing, out _);

				// Ensure blade never collapses to 0px thickness on pure Z edge-on frames
				Vector2 hilt2D = Ult3DProjection.Project(hilt3D, origin, facing, out float hiltPersp);
				Vector2 tip2D = Ult3DProjection.Project(tip3D, origin, facing, out _);
				Vector2 axis2D = tip2D - hilt2D;
				if (axis2D.LengthSquared() > 4f)
				{
					Vector2 perp2D = Vector2.Normalize(new Vector2(-axis2D.Y, axis2D.X));
					Vector2 spinePoint = Vector2.Lerp(hilt2D, tip2D, alongFrac);
					float signedOffset = Vector2.Dot(corner2D - spinePoint, perp2D);
					float minHalfPx = Math.Abs(acrossPx) * hiltPersp * 0.22f;
					if (Math.Abs(signedOffset) < minHalfPx)
					{
						float sign = signedOffset >= 0f ? 1f : -1f;
						if (Math.Abs(signedOffset) < 0.01f)
							sign = (Vector2.Dot(relTex, nTex) * sideFlip) >= 0f ? 1f : -1f;
						corner2D = spinePoint + perp2D * (sign * minHalfPx);
					}
				}

				verts[k] = new VertexPositionColorTexture(new Vector3(corner2D, 0f), vertexColor, new Vector2(px / texSize, py / texSize));
			}
		}

		private static void DrawSheathAndHilt3D(SpriteBatch sb, SlashedDreamUltimate ult, Vector2 origin, int facing)
		{
			if (cachedSheathTex == null)
				return;

			Texture2D hiltTex = cachedHiltTex ?? cachedUltBladeTex;
			if (hiltTex == null)
				return;

			Vector2 playerWorld = Ult3DProjection.Project(ult.PlayerPos3D, origin, facing, out float perspScale);
			Vector2 drawOrigin = new(90f, 90f);
			int dir = ult.VisualDrawDirection >= 0 ? 1 : -1;
			SpriteEffects effects = dir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

			float baseAngle = MathHelper.ToRadians(205f);
			float sheathRot = dir > 0 ? baseAngle : -baseAngle;
			Vector2 slideDir = new(dir * 0.94f, -0.34f * ult.Owner.gravDir);
			Vector2 hipOffset = new Vector2(dir * -6f, 2f * ult.Owner.gravDir) * perspScale;
			Vector2 sheathWorldPos = playerWorld + hipOffset;
			Vector2 sheathScreenPos = sheathWorldPos - Main.screenPosition;

			float slideDist = ult.CurrentSheathSlideDist * perspScale;
			float slideProgress = MathHelper.Clamp(slideDist / (22f * Math.Max(0.25f, perspScale)), 0f, 1f);
			Vector2 hiltScreenPos = sheathScreenPos + slideDir * slideDist;

			float scale = 0.95f * perspScale;
			Color bladeAura = Color.Lerp(new Color(255, 30, 45, 0), new Color(255, 230, 245, 0), slideProgress);

			sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			// 1. Emerging / retracting hilt underneath scabbard mouth
			sb.Draw(hiltTex, hiltScreenPos, null, Color.White, sheathRot, drawOrigin, scale, effects, 0f);
			if (slideProgress > 0.05f)
			{
				sb.Draw(hiltTex, hiltScreenPos, null, bladeAura * (0.85f * slideProgress), sheathRot, drawOrigin, scale * 1.10f, effects, 0f);
			}

			// 2. Scabbard body on top
			sb.Draw(cachedSheathTex, sheathScreenPos, null, Color.White, sheathRot, drawOrigin, scale, effects, 0f);

			sb.End();
		}

		private static void DrawSlashRibbonsSplit(
			GraphicsDevice gd,
			Matrix transformMatrix,
			SlashedDreamUltimate ult,
			Vector2 origin,
			int facing,
			float playerZ,
			bool drawNearHalf)
		{
			Effect fx = ShaderLoader.SlashShader;
			if (fx == null || cachedBrushTex == null || cachedNoiseTex == null || ult.ActiveRibbons.Count == 0)
				return;

			gd.BlendState = BlendState.AlphaBlend;
			gd.RasterizerState = RasterizerState.CullNone;
			gd.DepthStencilState = DepthStencilState.None;

			fx.Parameters["transformMatrix"]?.SetValue(transformMatrix);
			fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			fx.Parameters["uBrushTex"]?.SetValue(cachedBrushTex);
			fx.Parameters["uNoiseTex"]?.SetValue(cachedNoiseTex);
			fx.Parameters["uFarSel"]?.SetValue(0f);
			fx.Parameters["uFarDim"]?.SetValue(0f);
			fx.Parameters["uFarDirLocal"]?.SetValue(new Vector2(0f, -1f));
			fx.Parameters["uInk"]?.SetValue(0f);
			fx.Parameters["uFrontGlow"]?.SetValue(0.85f);
			fx.Parameters["uFlip"]?.SetValue(1f);

			const int Samples = 34;

			for (int r = 0; r < ult.ActiveRibbons.Count; r++)
			{
				UltSlashRibbon3D rib = ult.ActiveRibbons[r];
				float progress = MathHelper.Clamp(rib.Age / (float)Math.Max(1, rib.MaxAge), 0f, 1f);
				if (progress >= 1f)
					continue;

				float sweepT = MathHelper.Clamp(rib.Age / (float)Math.Max(1, rib.SweepFrames), 0f, 1f);
				float sweep = Ult3DProjection.EaseOutCubic(sweepT);
				float erode = Math.Max(0f, (progress - 0.36f) / 0.64f);
				float tailErode = progress * (rib.IsPreemptive ? 0.90f : 0.68f);
				float flash = rib.IsPreemptive
					? 0f
					: MathHelper.Clamp(MathF.Sin(MathHelper.Clamp(sweepT * 1.15f, 0f, 1f) * MathHelper.Pi) * 0.90f, 0f, 1f);
				float opacity = (1f - progress * progress) * rib.Opacity;
				if (opacity <= 0.01f)
					continue;

				farRibbonVertsScratch.Clear();
				nearRibbonVertsScratch.Clear();

				Vector2 prevOuter = Vector2.Zero;
				Vector2 prevInner = Vector2.Zero;
				float prevU = 0f;
				float prevZ = 0f;

				for (int i = 0; i < Samples; i++)
				{
					float u = i / (float)(Samples - 1);
					float phi = MathHelper.Lerp(rib.StartAngle, rib.EndAngle, u);
					float radius = MathHelper.Lerp(rib.RadiusStart, rib.RadiusEnd, u);
					float width = MathHelper.Lerp(rib.WidthStart, rib.WidthEnd, u);

					float cosP = MathF.Cos(phi);
					float sinP = MathF.Sin(phi);
					UltCoord3D radialDir = (rib.AxisU3D * cosP + rib.AxisV3D * sinP).SafeNormalize(new UltCoord3D(1f, 0f, 0f));

					UltCoord3D outer3D = rib.Center3D + radialDir * radius;
					outer3D.Z += rib.ZBiasEnd * u;

					float innerRadius = Math.Max(10f, radius - width);
					UltCoord3D inner3D = rib.Center3D + radialDir * innerRadius;
					inner3D.Z += rib.ZBiasEnd * u;

					Vector2 outer2D = Ult3DProjection.Project(outer3D, origin, facing, out float perspOut);
					Vector2 inner2D = Ult3DProjection.Project(inner3D, origin, facing, out _);

					// Keep visible ribbon thickness even if a slice turns momentarily edge-on
					Vector2 delta = inner2D - outer2D;
					float minPx = width * perspOut * 0.32f;
					if (delta.LengthSquared() < minPx * minPx)
					{
						Vector2 center2D = Ult3DProjection.Project(rib.Center3D, origin, facing, out _);
						Vector2 fallbackDir = (center2D - outer2D).SafeNormalize(new Vector2(0f, 1f));
						inner2D = outer2D + fallbackDir * minPx;
					}

					float sampleZ = (outer3D.Z + inner3D.Z) * 0.5f;

					if (i > 0)
					{
						float segMidZ = (prevZ + sampleZ) * 0.5f;
						bool isNear = segMidZ >= playerZ;
						var targetList = isNear ? nearRibbonVertsScratch : farRibbonVertsScratch;

						// TexCoords.y = 0.50f on outer razor edge (h=0), 1.00f on inner brush-torn tail (h=1)
						var v0 = new VertexPositionColorTexture(new Vector3(prevOuter, 0f), Color.White, new Vector2(prevU, 0.50f));
						var v1 = new VertexPositionColorTexture(new Vector3(outer2D, 0f), Color.White, new Vector2(u, 0.50f));
						var v2 = new VertexPositionColorTexture(new Vector3(prevInner, 0f), Color.White, new Vector2(prevU, 1.00f));
						var v3 = new VertexPositionColorTexture(new Vector3(inner2D, 0f), Color.White, new Vector2(u, 1.00f));

						targetList.Add(v0);
						targetList.Add(v1);
						targetList.Add(v2);

						targetList.Add(v1);
						targetList.Add(v3);
						targetList.Add(v2);
					}

					prevOuter = outer2D;
					prevInner = inner2D;
					prevU = u;
					prevZ = sampleZ;
				}

				var activeVerts = drawNearHalf ? nearRibbonVertsScratch : farRibbonVertsScratch;
				if (activeVerts.Count < 3)
					continue;

				float farDarken = drawNearHalf ? 0f : MathHelper.Clamp(0.22f + rib.DepthDimBias, 0f, 0.45f);
				float palDim = drawNearHalf ? 1.0f : 0.78f;

				fx.Parameters["uMode"]?.SetValue(1f); // Linear/ribbon UV mode along sampled 3D strip
				fx.Parameters["uSweep"]?.SetValue(sweep);
				fx.Parameters["uErode"]?.SetValue(erode);
				fx.Parameters["uTailErode"]?.SetValue(tailErode);
				fx.Parameters["uFlash"]?.SetValue(flash);
				fx.Parameters["uFlowPhase"]?.SetValue(progress * 0.45f);
				fx.Parameters["uColorShift"]?.SetValue(farDarken);
				fx.Parameters["uOpacity"]?.SetValue(opacity);
				fx.Parameters["uSeed"]?.SetValue(rib.Seed);
				fx.Parameters["uThick"]?.SetValue(rib.Thick);
				fx.Parameters["uRazorTailWiden"]?.SetValue(rib.RazorWiden);

				if (rib.IsPreemptive)
				{
					fx.Parameters["uColHot"]?.SetValue(new Vector3(0.95f, 0.72f, 0.80f) * palDim);
					fx.Parameters["uColBright"]?.SetValue(new Vector3(0.88f, 0.14f, 0.30f) * palDim);
					fx.Parameters["uColDeep"]?.SetValue(new Vector3(0.48f, 0.04f, 0.18f) * palDim);
					fx.Parameters["uColDark"]?.SetValue(new Vector3(0.14f, 0.01f, 0.05f) * palDim);
				}
				else if (rib.IsResurge)
				{
					fx.Parameters["uColHot"]?.SetValue(new Vector3(1.15f, 1.04f, 1.08f) * palDim);
					fx.Parameters["uColBright"]?.SetValue(new Vector3(1.08f, 0.16f, 0.28f) * palDim);
					fx.Parameters["uColDeep"]?.SetValue(new Vector3(0.76f, 0.04f, 0.16f) * palDim);
					fx.Parameters["uColDark"]?.SetValue(new Vector3(0.22f, 0.01f, 0.04f) * palDim);
				}
				else
				{
					fx.Parameters["uColHot"]?.SetValue(new Vector3(1.02f, 0.94f, 0.96f) * palDim);
					fx.Parameters["uColBright"]?.SetValue(new Vector3(1.00f, 0.12f, 0.25f) * palDim);
					fx.Parameters["uColDeep"]?.SetValue(new Vector3(0.70f, 0.04f, 0.14f) * palDim);
					fx.Parameters["uColDark"]?.SetValue(new Vector3(0.20f, 0.01f, 0.04f) * palDim);
				}

				VertexPositionColorTexture[] triArray = activeVerts.ToArray();
				foreach (EffectPass pass in fx.CurrentTechnique.Passes)
				{
					pass.Apply();
					gd.DrawUserPrimitives(PrimitiveType.TriangleList, triArray, 0, triArray.Length / 3);
				}
			}
		}

		private static void DrawCleavedSeams(GraphicsDevice gd, Matrix transformMatrix, SlashedDreamUltimate ult)
		{
			Effect fx = ShaderLoader.SlashShader;
			if (fx == null || cachedBrushTex == null || cachedNoiseTex == null || ult.ActiveSeams.Count == 0)
				return;

			gd.BlendState = BlendState.AlphaBlend;
			gd.RasterizerState = RasterizerState.CullNone;
			gd.DepthStencilState = DepthStencilState.None;

			fx.Parameters["transformMatrix"]?.SetValue(transformMatrix);
			fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			fx.Parameters["uBrushTex"]?.SetValue(cachedBrushTex);
			fx.Parameters["uNoiseTex"]?.SetValue(cachedNoiseTex);
			fx.Parameters["uFarSel"]?.SetValue(0f);
			fx.Parameters["uFarDim"]?.SetValue(0f);
			fx.Parameters["uInk"]?.SetValue(0f);
			fx.Parameters["uFlip"]?.SetValue(1f);

			for (int i = 0; i < ult.ActiveSeams.Count; i++)
			{
				UltCleaveSeam3D seam = ult.ActiveSeams[i];
				float progress = MathHelper.Clamp(seam.Age / (float)Math.Max(1, seam.MaxAge), 0f, 1f);
				if (!seam.PersistUntilSnap && progress >= 1f)
					continue;

				float sweep = Ult3DProjection.EaseOutCubic(MathHelper.Clamp(seam.Age / 3f, 0f, 1f));
				float flash = MathHelper.Clamp(1f - seam.Age / 5f, 0f, 1f);
				float erode = seam.PersistUntilSnap ? MathHelper.Clamp((progress - 0.55f) * 0.45f, 0f, 0.25f) : Math.Max(0f, (progress - 0.35f) * 1.45f);
				float opacity = seam.PersistUntilSnap ? MathHelper.Lerp(1.0f, 0.62f, MathHelper.Clamp(seam.Age / 28f, 0f, 1f)) : (1f - progress * progress);

				Vector2 dir = seam.Angle.ToRotationVector2();
				Vector2 perp = new(-dir.Y, dir.X);
				float widthScale = seam.PersistUntilSnap
					? MathHelper.Lerp(1.15f, 0.38f, Ult3DProjection.EaseOutCubic(MathHelper.Clamp(seam.Age / 22f, 0f, 1f)))
					: MathHelper.Lerp(1.0f, 0.28f, progress);

				Vector2 start = seam.WorldCenter - dir * seam.HalfLength;
				Vector2 end = seam.WorldCenter + dir * seam.HalfLength;
				Vector2 halfPerp = perp * (seam.HalfWidth * widthScale);

				fx.Parameters["uMode"]?.SetValue(1f);
				fx.Parameters["uSweep"]?.SetValue(sweep);
				fx.Parameters["uErode"]?.SetValue(erode);
				fx.Parameters["uTailErode"]?.SetValue(0f);
				fx.Parameters["uFlash"]?.SetValue(flash);
				fx.Parameters["uFlowPhase"]?.SetValue(0f);
				fx.Parameters["uColorShift"]?.SetValue(seam.PersistUntilSnap ? 0.12f : 0.25f * progress);
				fx.Parameters["uOpacity"]?.SetValue(opacity);
				fx.Parameters["uSeed"]?.SetValue(seam.Seed);
				fx.Parameters["uThick"]?.SetValue(seam.PersistUntilSnap ? 0.56f : 0.46f);
				fx.Parameters["uFrontGlow"]?.SetValue(1.1f);
				fx.Parameters["uRazorTailWiden"]?.SetValue(0.35f);
				fx.Parameters["uColHot"]?.SetValue(new Vector3(1.25f, 1.15f, 1.18f));
				fx.Parameters["uColBright"]?.SetValue(new Vector3(1.05f, 0.14f, 0.26f));
				fx.Parameters["uColDeep"]?.SetValue(new Vector3(0.70f, 0.03f, 0.14f));
				fx.Parameters["uColDark"]?.SetValue(new Vector3(0.18f, 0.01f, 0.04f));

				VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4]
				{
					new(new Vector3(start - halfPerp, 0f), Color.White, new Vector2(0f, 0f)),
					new(new Vector3(end - halfPerp, 0f), Color.White, new Vector2(1f, 0f)),
					new(new Vector3(start + halfPerp, 0f), Color.White, new Vector2(0f, 1f)),
					new(new Vector3(end + halfPerp, 0f), Color.White, new Vector2(1f, 1f))
				};

				foreach (EffectPass pass in fx.CurrentTechnique.Passes)
				{
					pass.Apply();
					gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
				}
			}
		}

		private static void DrawGleamFlashes(SpriteBatch sb, SlashedDreamUltimate ult)
		{
			if (ult.ActiveGleams.Count == 0)
				return;

			Texture2D flareTex = TextureAssets.Extra[ExtrasID.SharpTears]?.Value;
			Texture2D glowTex = TextureAssets.Extra[ExtrasID.ThePerfectGlow]?.Value;
			if (flareTex == null && glowTex == null)
				return;

			sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

			for (int i = 0; i < ult.ActiveGleams.Count; i++)
			{
				UltGleamFlash3D g = ult.ActiveGleams[i];
				if (g.Alpha <= 0.002f)
					continue;

				Vector2 screenPos = g.WorldPos - Main.screenPosition;
				float a = g.Alpha;
				float sc = g.Scale;

				if (glowTex != null)
				{
					Vector2 glowOrigin = glowTex.Size() * 0.5f;
					sb.Draw(glowTex, screenPos, null, new Color(255, 30, 60, 0) * (0.85f * a), 0f, glowOrigin, 0.55f * a * sc, SpriteEffects.None, 0f);
					sb.Draw(glowTex, screenPos, null, new Color(255, 240, 245, 0) * a, 0f, glowOrigin, 0.25f * a * sc, SpriteEffects.None, 0f);
				}

				if (flareTex != null)
				{
					Vector2 flareOrigin = flareTex.Size() * 0.5f;
					sb.Draw(flareTex, screenPos, null, Color.White * a, g.AxisAngle, flareOrigin, new Vector2(1.85f, 0.35f) * a * sc, SpriteEffects.None, 0f);
					sb.Draw(flareTex, screenPos, null, new Color(255, 60, 95, 0) * (0.85f * a), g.AxisAngle + MathHelper.PiOver2, flareOrigin, new Vector2(0.95f, 0.25f) * a * sc, SpriteEffects.None, 0f);
					sb.Draw(flareTex, screenPos, null, Color.White * (0.65f * a), g.AxisAngle + MathHelper.PiOver4, flareOrigin, new Vector2(0.65f, 0.20f) * a * sc, SpriteEffects.None, 0f);
				}
			}

			sb.End();
		}
	}

	internal sealed class Ult3DRenderCleanupSystem : ModSystem
	{
		public override void Unload()
		{
			Ult3DRenderHandle.DisposeAllResources();
		}
	}
}
