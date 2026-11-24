using System;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.UI.NihilityLedger;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles.Charms
{
	public class VoidFissure : ModProjectile
	{
		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public Vector2 StartPos;
		public Vector2 EndPos;
		private bool initialized = false;

		public override void SetDefaults()
		{
			Projectile.width = 32;
			Projectile.height = 32;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 240; // 4.0 seconds
			Projectile.DamageType = WeepCompatibility.TrueMelee;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 25;
		}

		public override bool? CanDamage() => false;
		public override bool ShouldUpdatePosition() => false;

		public void Init(Vector2 start, Vector2 end)
		{
			StartPos = start;
			EndPos = end;
			initialized = true;
			Projectile.Center = (start + end) * 0.5f;
		}

		public override void AI()
		{
			if (!initialized)
			{
				StartPos = Projectile.Center - Vector2.UnitX * 100f;
				EndPos = Projectile.Center + Vector2.UnitX * 100f;
				initialized = true;
			}

			// Strike enemies intersecting the fissure line
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (npc.active && !npc.friendly && !npc.dontTakeDamage)
				{
					float point = 0f;
					if (Collision.CheckAABBvLineCollision(npc.Hitbox.TopLeft(), npc.Hitbox.Size(), StartPos, EndPos, 24f, ref point))
					{
						if (Projectile.timeLeft % 20 == 0)
						{
							npc.SimpleStrikeNPC(Projectile.damage, 0, false, 2f, DamageClass.Melee);
							npc.AddBuff(BuffID.Slow, 120);

							// PRT sparks
							if (!Main.dedServ)
							{
								Vector2 hitPos = Vector2.Lerp(StartPos, EndPos, point);
								PRTLoader.NewParticle<VoidSparkPRT>(hitPos, Main.rand.NextVector2Circular(3f, 3f), new Color(238, 26, 56), 0.7f);
							}
						}
					}
				}
			}

			// Ambient fissure energy
			if (!Main.dedServ && Main.rand.NextBool(4))
			{
				float t = Main.rand.NextFloat();
				Vector2 pos = Vector2.Lerp(StartPos, EndPos, t);
				Vector2 vel = Main.rand.NextVector2Circular(2f, 2f);
				Color col = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(238, 26, 56);
				PRTLoader.NewParticle<VoidSparkPRT>(pos, vel, col, 0.65f);
			}

			Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.05f, 0.25f));
		}

		public override bool PreDraw(ref Color lightColor)
		{
			float alpha = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
			Color core = new Color(255, 235, 245) * alpha;
			Color glow = new Color(238, 26, 56) * (0.85f * alpha);

			Vector2 p1 = StartPos - Main.screenPosition;
			Vector2 p2 = EndPos - Main.screenPosition;

			NihilityLedgerTheme.DrawGlowLine(Main.spriteBatch, p1, p2, core, glow, 2.5f, 12f);
			return false;
		}
	}
}
