using System;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.UI.NihilityLedger;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles.Charms
{
	public class ThunderCoreBolt : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public Vector2 TargetPos;
		public Vector2 StrikeStart;
		private bool struck = false;
		private int lingerTimer = 16;

		public override void SetDefaults()
		{
			Projectile.width = 32;
			Projectile.height = 32;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 25;
			Projectile.DamageType = WeepCompatibility.TrueMelee;
		}

		public override bool? CanDamage() => false;
		public override bool ShouldUpdatePosition() => false;

		public void Init(Vector2 targetPos)
		{
			TargetPos = targetPos;
			StrikeStart = targetPos - new Vector2(Main.rand.NextFloat(-60f, 60f), 700f);
			Projectile.Center = targetPos;
		}

		public override void AI()
		{
			if (!struck)
			{
				struck = true;
				SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 0.85f }, TargetPos);

				// Deal damage around TargetPos (100px radius)
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC npc = Main.npc[i];
					if (npc.active && !npc.friendly && !npc.dontTakeDamage && Vector2.Distance(TargetPos, npc.Center) <= 110f)
					{
						npc.SimpleStrikeNPC(Projectile.damage, 0, true, 6f, DamageClass.Melee);
					}
				}

				// Thunder impact particles
				for (int i = 0; i < 10; i++)
				{
					Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
					Color col = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 40, 70);
					PRTLoader.NewParticle<VoidSparkPRT>(TargetPos, vel, col, 0.95f);
				}
			}

			lingerTimer--;
			Lighting.AddLight(TargetPos, new Vector3(0.5f, 0.2f, 0.7f));
		}

		public override bool PreDraw(ref Color lightColor)
		{
			float alpha = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
			Color core = new Color(255, 245, 255) * alpha;
			Color glow = new Color(185, 45, 255) * (0.9f * alpha);

			Vector2 p1 = StrikeStart - Main.screenPosition;
			Vector2 p2 = TargetPos - Main.screenPosition;

			NihilityLedgerTheme.DrawGlowLine(Main.spriteBatch, p1, p2, core, glow, 3.5f, 16f);
			return false;
		}
	}
}
