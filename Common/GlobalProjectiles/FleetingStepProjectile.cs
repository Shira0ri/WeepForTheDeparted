using System;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.Systems;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Common.GlobalProjectiles
{
	/// <summary>
	/// Global hostile projectile time-dilation for Fleeting Petal Step (FleetingStep).
	/// Runs AI at 50% cadence (every other sub-update) while moving smoothly at 0.5x velocity
	/// every tick so homing, gravity, extraUpdates, and directional PreDraw remain accurate
	/// and snap cleanly back to 100% speed when the 2.5s (150-tick) window expires.
	/// </summary>
	public class FleetingStepProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		private bool isSlowed;
		private bool hasHalfVelocityApplied;
		private bool skipAiNextStep;
		private Vector2 realVelocity;
		private Vector2 appliedHalfVelocity;

		private void RestoreUnscaledVelocity(Projectile projectile)
		{
			if (!hasHalfVelocityApplied)
				return;

			// Preserve per-axis modifications (e.g. OnTileCollide bounce/zeroing) that occurred since PostAI.
			float vx = MathF.Abs(projectile.velocity.X - appliedHalfVelocity.X) < 0.0001f
				? realVelocity.X
				: projectile.velocity.X * 2f;
			float vy = MathF.Abs(projectile.velocity.Y - appliedHalfVelocity.Y) < 0.0001f
				? realVelocity.Y
				: projectile.velocity.Y * 2f;

			projectile.velocity = new Vector2(vx, vy);
			hasHalfVelocityApplied = false;
		}

		private void ResetSlowState()
		{
			isSlowed = false;
			hasHalfVelocityApplied = false;
			skipAiNextStep = false;
			realVelocity = Vector2.Zero;
			appliedHalfVelocity = Vector2.Zero;
		}

		public override bool PreAI(Projectile projectile)
		{
			// Coordinate cleanly with WorldFreezeProjectile (which runs alphabetically after FleetingStepProjectile).
			bool worldFrozen = WorldFreezeSystem.IsActive && !WorldFreezeSystem.IsExempt(projectile);
			bool freezePendingRestore = projectile.TryGetGlobalProjectile<WorldFreezeProjectile>(out var freezeProj) && freezeProj.IsFrozen;

			if (worldFrozen)
			{
				// Restore unscaled velocity before WorldFreezeProjectile snapshots resumeVelocity.
				if (hasHalfVelocityApplied)
				{
					RestoreUnscaledVelocity(projectile);
				}
				skipAiNextStep = false;
				return true;
			}

			if (freezePendingRestore)
			{
				// Yield on the unfreeze frame so WorldFreezeProjectile restores resumeVelocity first.
				skipAiNextStep = false;
				hasHalfVelocityApplied = false;
				return true;
			}

			bool shouldSlow = projectile.active && projectile.hostile && NaughtPlayer.IsAnyFleetingStepSlowActive();
			if (!shouldSlow)
			{
				if (hasHalfVelocityApplied)
				{
					RestoreUnscaledVelocity(projectile);
				}
				if (isSlowed)
				{
					ResetSlowState();
				}
				return true;
			}

			isSlowed = true;

			if (skipAiNextStep)
			{
				// Half-tick: keep non-zero 0.5x velocity for smooth 60fps glide & directional drawing,
				// skip AI() so homing/gravity/timers advance at 50%, and preserve timeLeft.
				skipAiNextStep = false;
				if (projectile.timeLeft < int.MaxValue)
				{
					projectile.timeLeft++;
				}
				return false;
			}

			// Active AI tick: restore unscaled velocity so AI() computes physics at normal speed.
			if (hasHalfVelocityApplied)
			{
				RestoreUnscaledVelocity(projectile);
			}
			return true;
		}

		public override void PostAI(Projectile projectile)
		{
			if (WorldFreezeSystem.IsActive && !WorldFreezeSystem.IsExempt(projectile))
				return;

			if (!projectile.active || !projectile.hostile || !NaughtPlayer.IsAnyFleetingStepSlowActive())
			{
				if (isSlowed)
				{
					ResetSlowState();
				}
				return;
			}

			isSlowed = true;
			realVelocity = projectile.velocity;
			appliedHalfVelocity = realVelocity * 0.5f;
			projectile.velocity = appliedHalfVelocity;
			hasHalfVelocityApplied = true;
			skipAiNextStep = true;

			if (!Main.dedServ && Main.rand.NextBool(18))
			{
				Vector2 vel = -appliedHalfVelocity * 0.2f + Main.rand.NextVector2Circular(0.8f, 0.8f);
				Color col = Main.rand.NextBool() ? new Color(238, 26, 56, 210) : new Color(185, 45, 255, 200);
				PRTLoader.NewParticle<CrimsonPetalPRT>(projectile.Center + Main.rand.NextVector2Circular(6f, 6f), vel, col, 0.48f);
			}
		}

		public override bool PreDraw(Projectile projectile, ref Color lightColor)
		{
			if (isSlowed && projectile.hostile && !(WorldFreezeSystem.IsActive && !WorldFreezeSystem.IsExempt(projectile)))
			{
				lightColor = Color.Lerp(lightColor, new Color(255, 115, 165), 0.45f);
			}
			return true;
		}
	}
}
