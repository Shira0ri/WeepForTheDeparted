using System;
using System.Collections.Generic;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.GlobalNPCs;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.UI.NihilityLedger;
using WeepForTheDeparted.Common.Visuals;
using WeepForTheDeparted.Content.Items.Weapons;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles
{
	public class NaughtClone : ModProjectile
	{
		public const int DefaultLifetime = 540; // 9 seconds @ 60 ticks/sec

		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public Player Owner => Main.player[Projectile.owner];

		public int Age = 0;

		// Extensible data dictionary for future charms to modify this clone's behavior
		public readonly Dictionary<string, object> CharmData = new Dictionary<string, object>();

		// Shadow Marionette tethered enemies
		public readonly List<int> MarionetteTetherNPCs = new List<int>();

		private bool poseCaptured = false;
		private Vector2 snapshotDrawOffset;
		private int snapshotDirection = 1;
		private float snapshotGravDir = 1f;
		private Rectangle snapshotBodyFrame;
		private Rectangle snapshotLegFrame;
		private float snapshotFullRotation;
		private Vector2 snapshotFullRotationOrigin;

		public override void SetDefaults()
		{
			Projectile.width = 36;
			Projectile.height = 52;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = DefaultLifetime;
			Projectile.netImportant = true;
		}

		public override bool? CanDamage() => false;

		public override bool ShouldUpdatePosition() => false;

		public void CapturePose(Player player)
		{
			poseCaptured = true;
			snapshotDrawOffset = player.position - Projectile.position;
			snapshotDirection = player.direction >= 0 ? 1 : -1;
			snapshotGravDir = player.gravDir >= 0f ? 1f : -1f;
			snapshotBodyFrame = player.bodyFrame;
			snapshotLegFrame = player.legFrame;
			snapshotFullRotation = player.fullRotation;
			snapshotFullRotationOrigin = player.fullRotationOrigin;
		}

		public override void AI()
		{
			if (!poseCaptured && Owner != null && Owner.active)
			{
				CapturePose(Owner);
			}

			Age++;

			// Subtle ambient ethereal Nihility particles via InnoVault PRT
			if (!Main.dedServ && Age % 18 == 0)
			{
				Vector2 dustPos = Projectile.position + new Vector2(Main.rand.NextFloat(Projectile.width), Main.rand.NextFloat(Projectile.height));
				Vector2 vel = -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.0f);
				Color col = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
				PRTLoader.NewParticle<VoidSparkPRT>(dustPos, vel, col, 0.65f);
			}

			// Soft ethereal purple lighting
			Lighting.AddLight(Projectile.Center, new Vector3(0.25f, 0.08f, 0.35f));

			var naughtPlayer = Owner?.GetModPlayer<NaughtPlayer>();
			if (naughtPlayer != null)
			{
				// Shadow Marionette: Maintain tethers to up to 3 nearby enemies
				if (naughtPlayer.HasShadowMarionette && Age % 12 == 0)
				{
					MarionetteTetherNPCs.Clear();
					for (int i = 0; i < Main.maxNPCs; i++)
					{
						NPC npc = Main.npc[i];
						if (npc.active && !npc.friendly && !npc.dontTakeDamage && Vector2.Distance(Projectile.Center, npc.Center) < 350f)
						{
							MarionetteTetherNPCs.Add(npc.whoAmI);
							if (MarionetteTetherNPCs.Count >= 3) break;
						}
					}
				}

				// Shadow Crucible: 18-block aura (-12 defense, -10% speed, and accelerated DoTs)
				if (naughtPlayer.HasShadowCrucible)
				{
					float crucibleRadius = 288f; // 18 blocks
					for (int i = 0; i < Main.maxNPCs; i++)
					{
						NPC npc = Main.npc[i];
						if (npc.active && !npc.friendly && !npc.dontTakeDamage && Vector2.Distance(Projectile.Center, npc.Center) < crucibleRadius)
						{
							npc.defense = Math.Max(0, npc.defense - 12);
							npc.velocity *= 0.98f;

							// Accelerate DoT damage pressure
							if (Age % 3 == 0)
							{
								for (int b = 0; b < npc.buffType.Length; b++)
								{
									int buff = npc.buffType[b];
									if (buff > 0 && Main.debuff[buff])
									{
										npc.lifeRegen -= 4;
										break;
									}
								}
							}
						}
					}
				}
			}
		}

		public override void OnKill(int timeLeft)
		{
			if (Owner != null && Owner.active && Main.myPlayer == Owner.whoAmI)
			{
				var np = Owner.GetModPlayer<NaughtPlayer>();
				if (np != null)
				{
					np.OnCloneCycleComplete(Projectile.Center);

					// Echo of the Nameless Decoy: perform 160% True Melee Octobolt Echo cleave + 2 knots
					if (np.HasNamelessDecoy)
					{
						TriggerOctoboltCleave();
					}
				}
			}

			// Ethereal purple dispersion sparks and red petals via InnoVault PRT
			for (int i = 0; i < 8; i++)
			{
				Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
				Color col = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
				PRTLoader.NewParticle<VoidSparkPRT>(Projectile.Center, vel, col, 0.85f);
			}

			for (int p = 0; p < 3; p++)
			{
				Vector2 pVel = Main.rand.NextVector2Circular(2f, 2f) - Vector2.UnitY * 1.0f;
				PRTLoader.NewParticle<CrimsonPetalPRT>(Projectile.Center, pVel, new Color(245, 30, 55, 200), 0.75f);
			}
		}

		public void TriggerOctoboltCleave()
		{
			if (Owner == null || !Owner.active) return;

			int weaponDmg = Owner.HeldItem?.damage ?? 100;
			int damage = (int)(weaponDmg * 1.60f);

			SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f, Volume = 0.9f }, Projectile.Center);

			if (Main.myPlayer == Owner.whoAmI)
			{
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(snapshotDirection, 0f), ModContent.ProjectileType<NaughtSlashVisual>(), 0, 0f, Owner.whoAmI, 1f, 0f);
			}

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (npc.active && !npc.friendly && !npc.dontTakeDamage && Vector2.Distance(Projectile.Center, npc.Center) < 180f)
				{
					npc.SimpleStrikeNPC(damage, snapshotDirection, false, 6f, DamageClass.Melee);
					CrimsonKnotGlobalNPC.AddKnot(npc, 2, npc.Center);
				}
			}
		}

		public void PerformMirrorRainblade(int slashStage, Vector2 dir)
		{
			if (Owner == null || !Owner.active || !Owner.GetModPlayer<NaughtPlayer>().HasSynchronizedExecution)
				return;

			int weaponDmg = Owner.HeldItem?.damage ?? 100;
			int damage = (int)(weaponDmg * 0.45f);

			SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.35f, Volume = 0.75f }, Projectile.Center);

			if (Main.myPlayer == Owner.whoAmI)
			{
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir, ModContent.ProjectileType<NaughtSlashVisual>(), 0, 0f, Owner.whoAmI, 0.7f, slashStage);
			}

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (npc.active && !npc.friendly && !npc.dontTakeDamage && Vector2.Distance(Projectile.Center, npc.Center) < 220f)
				{
					npc.SimpleStrikeNPC(damage, (int)Math.Sign(dir.X), false, 4f, DamageClass.Melee);
				}
			}
		}

		public void PerformSheathingCrossSlash(NPC target)
		{
			if (Owner == null || !Owner.active || !Owner.GetModPlayer<NaughtPlayer>().HasSynchronizedExecution || target == null || !target.active)
				return;

			int weaponDmg = Owner.HeldItem?.damage ?? 100;
			int damage = (int)(weaponDmg * 1.20f);

			Vector2 dashDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
			Projectile.Center = target.Center + dashDir * 40f;

			SoundEngine.PlaySound(WeepSound.KatanaHitB with { Pitch = 0.1f, Volume = 0.95f }, target.Center);

			target.SimpleStrikeNPC(damage, (int)Math.Sign(dashDir.X), true, 8f, DamageClass.Melee);

			for (int p = 0; p < 6; p++)
			{
				PRTLoader.NewParticle<CrimsonPetalPRT>(target.Center, Main.rand.NextVector2Circular(4f, 4f), new Color(238, 26, 56), 0.85f);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ || !poseCaptured || Owner == null || !Owner.active)
				return false;

			float fadeIn = VaultUtils.EaseQuadOut(MathHelper.Clamp(Age / 12f, 0f, 1f));
			float fadeOut = VaultUtils.EaseQuadOut(MathHelper.Clamp(Projectile.timeLeft / 35f, 0f, 1f));

			// Translucent ethereal phantom opacity (approx 40% base)
			float opacity = fadeIn * fadeOut * 0.42f;

			if (opacity <= 0.01f)
				return false;

			var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
			if (naughtPlayer != null)
			{
				// Shadow Marionette: Draw red threads connecting clone to up to 3 tethered enemies
				if (naughtPlayer.HasShadowMarionette && MarionetteTetherNPCs.Count > 0)
				{
					Vector2 cloneCenter = Projectile.Center - Main.screenPosition;
					Color stringCol = new Color(238, 26, 56) * (0.85f * opacity);
					for (int i = 0; i < MarionetteTetherNPCs.Count; i++)
					{
						int id = MarionetteTetherNPCs[i];
						if (id >= 0 && id < Main.maxNPCs && Main.npc[id].active)
						{
							Vector2 npcCenter = Main.npc[id].Center - Main.screenPosition;
							NihilityLedgerTheme.DrawGlowLine(Main.spriteBatch, cloneCenter, npcCenter, stringCol, stringCol * 0.5f, 1.5f, 6f);
						}
					}
				}

				// Shadow Crucible: Draw 18-block aura ring on ground
				if (naughtPlayer.HasShadowCrucible)
				{
					Vector2 cloneCenter = Projectile.Center - Main.screenPosition;
					Color auraCol = new Color(165, 35, 235) * (0.35f * opacity);
					NihilityLedgerTheme.DrawCircle(Main.spriteBatch, cloneCenter, 288f, auraCol, 2f);
				}
			}

			Vector2 drawPos = Projectile.position + snapshotDrawOffset;

			PlayerCloneRenderer.Prepare(Owner);

			// Single-pass translucent phantom render preserving armor and vanity
			Color phantomTint = new Color(190, 140, 255, (byte)(255 * opacity));
			PlayerCloneRenderer.DrawPrepared(drawPos, phantomTint, snapshotDirection,
				snapshotBodyFrame, snapshotLegFrame, snapshotFullRotation,
				snapshotFullRotationOrigin, snapshotGravDir);

			// Draw sheathed scabbard properly resting at the player's waist/hip
			DrawSheathAtHip(opacity);

			return false;
		}

		private void DrawSheathAtHip(float opacity)
		{
			Texture2D sheathTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtSheath", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
			if (sheathTex == null)
				return;

			// In NaughtSheath.png (180x180):
			// Tsuba / guard center is at (45f, 138f)
			// Scabbard tip is at (172f, 18f)
			// Pommel is at (7f, 164f)
			Vector2 origin = new Vector2(45f, 138f);

			// Position anchored at the clone's hip/beltline
			Vector2 drawPos = Projectile.position + snapshotDrawOffset;
			Vector2 hipPos = drawPos + new Vector2(snapshotDirection > 0 ? 12f : 20f, 26f) - Main.screenPosition;

			// When facing right: 191.5 degrees rotates the scabbard to point down-backwards (+150 deg)
			// and the hilt forward towards the hand (-30 deg).
			// When facing left: flip horizontally and negate rotation for exact symmetry.
			float rotation = MathHelper.ToRadians(191.5f);
			SpriteEffects effects = SpriteEffects.None;

			if (snapshotDirection < 0)
			{
				effects = SpriteEffects.FlipHorizontally;
				origin.X = sheathTex.Width - origin.X;
				rotation = -rotation;
			}

			Color sheathGhostColor = new Color(195, 140, 255, (byte)(255 * opacity)) * 0.85f;
			Main.EntitySpriteDraw(sheathTex, hipPos, null, sheathGhostColor, rotation, origin, 0.38f, effects, 0);
		}
	}
}
