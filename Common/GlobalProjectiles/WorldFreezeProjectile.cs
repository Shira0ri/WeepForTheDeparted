using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.Systems;

namespace WeepForTheDeparted.Common.GlobalProjectiles
{
	public class WorldFreezeProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		private bool wasFrozen = false;
		private Vector2 frozenPosition = Vector2.Zero;
		private Vector2 resumeVelocity = Vector2.Zero;
		private float frozenRotation;
		private int frozenFrame;
		private int frozenSpriteDirection;

		public override bool PreAI(Projectile projectile)
		{
			if (WorldFreezeSystem.IsActive && !WorldFreezeSystem.IsExempt(projectile))
			{
				if (!wasFrozen)
				{
					wasFrozen = true;
					frozenPosition = projectile.position;
					resumeVelocity = projectile.velocity;
					frozenRotation = projectile.rotation;
					frozenFrame = projectile.frame;
					frozenSpriteDirection = projectile.spriteDirection;
				}

				projectile.position = frozenPosition;
				projectile.velocity = Vector2.Zero;
				projectile.rotation = frozenRotation;
				projectile.frame = frozenFrame;
				projectile.spriteDirection = frozenSpriteDirection;

				// Prevent projectile from expiring while suspended in time stop
				if (projectile.timeLeft < int.MaxValue)
					projectile.timeLeft++;

				return false;
			}
			else if (wasFrozen)
			{
				wasFrozen = false;
				projectile.velocity = resumeVelocity;
			}

			return true;
		}

		public override bool ShouldUpdatePosition(Projectile projectile)
		{
			if (WorldFreezeSystem.IsActive && !WorldFreezeSystem.IsExempt(projectile))
				return false;
			return true;
		}
	}
}
