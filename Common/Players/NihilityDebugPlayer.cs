using Terraria;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;

namespace WeepForTheDeparted.Common.Players
{
	public class NihilityDebugPlayer : ModPlayer
	{
		public bool DebuffInflictionActive = false;

		public override void ResetEffects()
		{
			DebuffInflictionActive = false;
		}

		public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (DebuffInflictionActive && (item.DamageType == WeepCompatibility.TrueMelee || item.DamageType == DamageClass.Melee))
			{
				WeepCompatibility.ApplyDebugDebuffs(target);
			}
		}

		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (DebuffInflictionActive && (proj.DamageType == WeepCompatibility.TrueMelee || proj.DamageType == DamageClass.Melee))
			{
				WeepCompatibility.ApplyDebugDebuffs(target);
			}
		}
	}
}
