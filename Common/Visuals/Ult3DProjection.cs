using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace WeepForTheDeparted.Common.Visuals
{
	/// <summary>
	/// Combat-local 3D coordinate relative to the Ultimate cast anchor:
	/// +F: Forward toward target (+facingDir), -F: Backward away from target.
	/// +U: Up (-World Y), -U: Down (+World Y).
	/// +Z: Foreground toward viewer/right, -Z: Background deep into screen/left.
	/// </summary>
	public struct UltCoord3D
	{
		public float F;
		public float U;
		public float Z;

		public UltCoord3D(float f, float u, float z)
		{
			F = f;
			U = u;
			Z = z;
		}

		public static UltCoord3D Zero => default;

		public static UltCoord3D operator +(UltCoord3D a, UltCoord3D b) => new(a.F + b.F, a.U + b.U, a.Z + b.Z);
		public static UltCoord3D operator -(UltCoord3D a, UltCoord3D b) => new(a.F - b.F, a.U - b.U, a.Z - b.Z);
		public static UltCoord3D operator *(UltCoord3D a, float s) => new(a.F * s, a.U * s, a.Z * s);

		public float Length() => MathF.Sqrt(F * F + U * U + Z * Z);

		public UltCoord3D SafeNormalize(UltCoord3D fallback)
		{
			float lenSq = F * F + U * U + Z * Z;
			if (lenSq <= 1e-6f)
				return fallback;

			float inv = 1f / MathF.Sqrt(lenSq);
			return new UltCoord3D(F * inv, U * inv, Z * inv);
		}

		public static UltCoord3D Lerp(UltCoord3D a, UltCoord3D b, float t) =>
			new(MathHelper.Lerp(a.F, b.F, t), MathHelper.Lerp(a.U, b.U, t), MathHelper.Lerp(a.Z, b.Z, t));

		public static UltCoord3D Cross(UltCoord3D a, UltCoord3D b) =>
			new(
				a.U * b.Z - a.Z * b.U,
				a.Z * b.F - a.F * b.Z,
				a.F * b.U - a.U * b.F);
	}

	/// <summary>
	/// 2.5D perspective projection and Acheron motion-curve helpers for SlashedDreamUltimate.
	/// </summary>
	public static class Ult3DProjection
	{
		public const float FocalLength = 420f;
		public const float MinZ = -420f;
		public const float MaxZ = 180f;
		public const float ZHorizontalSkew = 0.86f;
		public const float ZVerticalHorizonBias = 0.14f;

		public static float GetPerspectiveScale(float z)
		{
			float clampedZ = MathHelper.Clamp(z, MinZ, MaxZ);
			return FocalLength / (FocalLength - clampedZ);
		}

		public static Vector2 Project(UltCoord3D coord, Vector2 ultOrigin, int facingDir, out float perspScale)
		{
			float clampedZ = MathHelper.Clamp(coord.Z, MinZ, MaxZ);
			perspScale = FocalLength / (FocalLength - clampedZ);

			float dir = facingDir >= 0 ? 1f : -1f;
			float offsetX = coord.F * dir * perspScale + clampedZ * ZHorizontalSkew;
			float offsetY = -coord.U * perspScale + clampedZ * ZVerticalHorizonBias;

			return new Vector2(ultOrigin.X + offsetX, ultOrigin.Y + offsetY);
		}

		/// <summary>
		/// Computes a uniform world-space offset so the choreographed 3D staging stays framed
		/// inside the locked viewport even if the Ultimate begins near a world boundary.
		/// </summary>
		public static Vector2 ComputeSafeFrameOrigin(Vector2 ultOrigin, Vector2 lockedScreenPos)
		{
			float minX = lockedScreenPos.X + 340f;
			float maxX = lockedScreenPos.X + Main.screenWidth - 260f;
			float minY = lockedScreenPos.Y + 210f;
			float maxY = lockedScreenPos.Y + Main.screenHeight - 210f;

			float x = minX <= maxX ? MathHelper.Clamp(ultOrigin.X, minX, maxX) : ultOrigin.X;
			float y = minY <= maxY ? MathHelper.Clamp(ultOrigin.Y, minY, maxY) : ultOrigin.Y;
			return new Vector2(x, y);
		}

		public static float EaseOutCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			float inv = 1f - t;
			return 1f - inv * inv * inv;
		}

		public static float EaseInCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t * t * t;
		}

		public static float EaseOutQuint(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			float inv = 1f - t;
			return 1f - inv * inv * inv * inv * inv;
		}

		public static float EaseInOutQuart(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t < 0.5f
				? 8f * t * t * t * t
				: 1f - MathF.Pow(-2f * t + 2f, 4f) * 0.5f;
		}

		public static void ComputeAnisotropicScale(
			float perspScale,
			float widthScale,
			float squashY,
			Vector2 stretchDir2D,
			float stretchAmount,
			out float scaleX,
			out float scaleY)
		{
			float clampedStretch = Math.Clamp(stretchAmount, 0f, 0.55f);
			float stretchBoost = 1f + clampedStretch * 0.45f;
			float perpCompress = 1f / (1f + clampedStretch * 0.16f);
			bool mostlyHoriz = Math.Abs(stretchDir2D.X) >= Math.Abs(stretchDir2D.Y);
			scaleX = perspScale * widthScale * (mostlyHoriz ? stretchBoost : perpCompress);
			scaleY = perspScale * squashY * (mostlyHoriz ? perpCompress : stretchBoost);
		}

		/// <summary>
		/// Transforms Terraria player DrawData items in-place in screen space for 2.5D staging:
		/// translates from <paramref name="sourceCenterScreen"/> to <paramref name="targetCenterScreen"/>,
		/// applies foot-anchored lean shear, non-uniform perspective scaling, body tilt, and optional tint/fade.
		/// Shared by NaughtPlayer (live Ult player) and PlayerCloneRenderer (3D Ult afterimages).
		/// </summary>
		public static void TransformPlayerDrawData(
			ref Terraria.DataStructures.PlayerDrawSet drawInfo,
			Vector2 sourceCenterScreen,
			Vector2 targetCenterScreen,
			float scaleX,
			float scaleY,
			float leanShear,
			float bodyTilt,
			Color colorTint,
			float tintStrength,
			float alpha,
			float minOpaqueFloor = 0f)
		{
			const float footPivotOffsetY = 21f;
			bool hasTilt = Math.Abs(bodyTilt) > 0.0001f;
			Vector3 tintVec = colorTint.ToVector3();

			for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
			{
				Terraria.DataStructures.DrawData data = drawInfo.DrawDataCache[i];

				if (data.useDestinationRectangle)
				{
					Rectangle rect = data.destinationRectangle;
					Vector2 rel = rect.Center.ToVector2() - sourceCenterScreen;
					rel.X += leanShear * (footPivotOffsetY - rel.Y);
					rel.X *= scaleX;
					rel.Y *= scaleY;
					if (hasTilt)
						rel = rel.RotatedBy(bodyTilt);

					int w = Math.Max(1, (int)MathF.Round(rect.Width * scaleX));
					int h = Math.Max(1, (int)MathF.Round(rect.Height * scaleY));
					Vector2 center = targetCenterScreen + rel;
					data.destinationRectangle = new Rectangle(
						(int)MathF.Round(center.X - w * 0.5f),
						(int)MathF.Round(center.Y - h * 0.5f),
						w,
						h);
					data.rotation += bodyTilt;
				}
				else
				{
					Vector2 rel = data.position - sourceCenterScreen;
					rel.X += leanShear * (footPivotOffsetY - rel.Y);
					rel.X *= scaleX;
					rel.Y *= scaleY;
					if (hasTilt)
						rel = rel.RotatedBy(bodyTilt);

					data.position = targetCenterScreen + rel;
					data.scale = new Vector2(data.scale.X * scaleX, data.scale.Y * scaleY);
					data.rotation += bodyTilt;
				}

				Color src = data.color;
				if (src.A > 0 && minOpaqueFloor > 0f)
				{
					float aNorm = src.A / 255f;
					float floorVal = 255f * aNorm * minOpaqueFloor;
					src = new Color(
						(byte)Math.Clamp(Math.Max(src.R, floorVal), 0, 255),
						(byte)Math.Clamp(Math.Max(src.G, floorVal), 0, 255),
						(byte)Math.Clamp(Math.Max(src.B, floorVal), 0, 255),
						src.A);
				}

				if (tintStrength > 0.001f)
				{
					if (src.A == 0)
					{
						Color addTint = new Color(
							(byte)Math.Clamp(src.R * tintVec.X, 0, 255),
							(byte)Math.Clamp(src.G * tintVec.Y, 0, 255),
							(byte)Math.Clamp(src.B * tintVec.Z, 0, 255),
							(byte)0);
						data.color = addTint * alpha;
					}
					else
					{
						Color sil = colorTint * (src.A / 255f);
						Color mixed = Color.Lerp(src, sil, MathHelper.Clamp(tintStrength, 0f, 1f));
						mixed.A = src.A;
						data.color = mixed * alpha;
					}
				}
				else
				{
					data.color = new Color(
						(byte)Math.Clamp(src.R * tintVec.X, 0, 255),
						(byte)Math.Clamp(src.G * tintVec.Y, 0, 255),
						(byte)Math.Clamp(src.B * tintVec.Z, 0, 255),
						src.A) * alpha;
				}

				drawInfo.DrawDataCache[i] = data;
			}
		}
	}

	public struct UltAfterimage3D
	{
		public UltCoord3D Pos3D;
		public float LeanShear;
		public float BodyTilt;
		public float WidthScale;
		public float SquashY;
		public Vector2 StretchDir2D;
		public float StretchAmount;
		public int DrawDirection;
		public int Age;
		public int MaxAge;
		public float InitialAlpha;
		public bool StarkWhiteLead;
	}

	public struct UltWaterRipple3D
	{
		public UltCoord3D Center3D;
		public float MaxRadius;
		public int Age;
		public int MaxAge;
		public float Opacity;
		public bool DoubleRing;
	}

	public struct UltSlashRibbon3D
	{
		public UltCoord3D Center3D;
		public UltCoord3D AxisU3D;
		public UltCoord3D AxisV3D;
		public float StartAngle;
		public float EndAngle;
		public float RadiusStart;
		public float RadiusEnd;
		public float WidthStart;
		public float WidthEnd;
		public float ZBiasEnd;
		public float Thick;
		public float Seed;
		public int Age;
		public int MaxAge;
		public int SweepFrames;
		public float Opacity;
		public float RazorWiden;
		public bool IsPreemptive;
		public bool IsResurge;
		public float DepthDimBias;
	}

	public struct UltCleaveSeam3D
	{
		public Vector2 WorldCenter;
		public float Angle;
		public float HalfLength;
		public float HalfWidth;
		public float Seed;
		public int Age;
		public int MaxAge;
		public bool PersistUntilSnap;
	}

	public struct UltGleamFlash3D
	{
		public Vector2 WorldPos;
		public float AxisAngle;
		public float Alpha;
		public float Scale;
	}

	public struct UltBladeGhost3D
	{
		public UltCoord3D Hilt3D;
		public UltCoord3D Tip3D;
		public UltCoord3D BroadsideNormal3D;
		public int DrawDirection;
		public int Age;
		public int MaxAge;
		public float Opacity;
	}
}
