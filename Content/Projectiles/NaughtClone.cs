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
using ReLogic.Content;
using WeepForTheDeparted.Common.Visuals;
using WeepForTheDeparted.Content.Items.Weapons;
using WeepForTheDeparted.Content.Shaders;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Content.Projectiles
{
	public class NaughtClone : ModProjectile
	{
		public const int DefaultLifetime = 540; // 9 seconds @ 60 ticks/sec
		public const int MaxMarionetteClones = 4;
		public const float MarionetteTetherRange = 350f;
		public const float MarionetteSlackRadius = 175f;
		public const int MaxMarionetteTethersPerClone = 3;
		public const float CrucibleRadius = 288f; // 18 blocks
		public const int MaxCrucibleClones = 3;

		public override string Texture => "WeepForTheDeparted/Content/Items/Weapons/Naught";

		public Player Owner => Main.player[Projectile.owner];

		public int Age = 0;
		public uint SpawnSequence = 0;
		public bool IsMarionetteIndefinite = false;
		public bool SuppressCycleOnKill = false;
		public bool SyncDissipating = false;

		private int syncDissipateTimer = 0;
		private const int SyncDissipateDuration = 18;
		private int syncCrossSlashTimer = 0;
		private const int SyncCrossSlashDuration = 22;
		private Vector2 syncCrossSlashStart;
		private Vector2 syncCrossSlashEnd;
		private Vector2 syncCrossSlashTargetCenter;
		private bool syncEmitRuptureFlash = false;
		private float syncShaderSeed = 0f;

		// Extensible data dictionary for future charms to modify this clone's behavior
		public readonly Dictionary<string, object> CharmData = new Dictionary<string, object>();

		// Shadow Marionette tethered enemy anchor IDs (deduplicated via ResolveKnotAnchor)
		public readonly List<int> MarionetteTetherNPCs = new List<int>();
		private readonly Dictionary<int, float> marionetteCandidateDistSq = new Dictionary<int, float>();
		private readonly List<(int AnchorId, float DistSq)> marionetteSortedCandidates = new List<(int, float)>();

		// Shadow Crucible per-clone furnace heat telemetry (updated by CrimsonKnotSystem)
		public int CrucibleActiveOverlapMax = 0;
		public bool CrucibleHasBurningTarget = false;

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
			Projectile.DamageType = WeepCompatibility.TrueMelee;
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
			if (Owner == null || !Owner.active || Owner.dead || Owner.ghost)
			{
				if (IsMarionetteIndefinite)
				{
					SuppressCycleOnKill = true;
					Projectile.Kill();
					return;
				}
			}
			else if (!poseCaptured)
			{
				CapturePose(Owner);
			}

			Age++;

			if (syncCrossSlashTimer > 0)
				syncCrossSlashTimer--;

			if (SyncDissipating)
			{
				syncDissipateTimer++;
				if (!Main.dedServ && syncDissipateTimer % 3 == 0)
				{
					Vector2 pVel = Main.rand.NextVector2Circular(2.4f, 2.4f) - Vector2.UnitY * 0.8f;
					PRTLoader.NewParticle<CrimsonPetalPRT>(Projectile.Center, pVel, new Color(245, 30, 55, 220), 0.85f);
				}

				if (syncDissipateTimer >= SyncDissipateDuration)
				{
					SuppressCycleOnKill = true;
					Projectile.Kill();
					return;
				}
			}

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
				// Synchronized Execution: suspend clone lifetime decay while SlashedDreamUltimate is active
				if (naughtPlayer.IsInUltimate && !IsMarionetteIndefinite && !SyncDissipating)
				{
					Projectile.timeLeft++;
					if (Projectile.timeLeft < 2)
						Projectile.timeLeft = 2;
				}

				// Shadow Marionette: Indefinite lifetime while equipped + deduplicated tethers to up to 3 nearest enemy anchors
				if (naughtPlayer.HasShadowMarionette)
				{
					IsMarionetteIndefinite = true;
					Projectile.timeLeft = DefaultLifetime;

					if (Age == 1 || Age % 12 == 0)
					{
						RefreshMarionetteTethers();
					}
					else
					{
						PruneInactiveMarionetteTethers();
					}
				}
				else
				{
					IsMarionetteIndefinite = false;
					if (MarionetteTetherNPCs.Count > 0)
					{
						MarionetteTetherNPCs.Clear();
					}
				}
			}
			else
			{
				IsMarionetteIndefinite = false;
				if (MarionetteTetherNPCs.Count > 0)
				{
					MarionetteTetherNPCs.Clear();
				}
			}
		}

		private void PruneInactiveMarionetteTethers()
		{
			for (int i = MarionetteTetherNPCs.Count - 1; i >= 0; i--)
			{
				int id = MarionetteTetherNPCs[i];
				if (id < 0 || id >= Main.maxNPCs || !Main.npc[id].active)
				{
					MarionetteTetherNPCs.RemoveAt(i);
				}
			}
		}

		private void RefreshMarionetteTethers()
		{
			MarionetteTetherNPCs.Clear();
			marionetteCandidateDistSq.Clear();
			marionetteSortedCandidates.Clear();

			float maxDistSq = MarionetteTetherRange * MarionetteTetherRange;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.isLikeATownNPC || npc.lifeMax <= 5)
					continue;

				bool isDummy = npc.type == NPCID.TargetDummy;
				if (npc.dontTakeDamage && !isDummy)
					continue;

				float distSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
				if (distSq >= maxDistSq)
					continue;

				NPC anchor = CrimsonKnotGlobalNPC.ResolveKnotAnchor(npc);
				if (anchor == null || !anchor.active)
					continue;

				if (anchor.dontTakeDamage && anchor.type != NPCID.TargetDummy)
					continue;

				if (!marionetteCandidateDistSq.TryGetValue(anchor.whoAmI, out float existingDistSq) || distSq < existingDistSq)
				{
					marionetteCandidateDistSq[anchor.whoAmI] = distSq;
				}
			}

			foreach (var kvp in marionetteCandidateDistSq)
			{
				marionetteSortedCandidates.Add((kvp.Key, kvp.Value));
			}

			marionetteSortedCandidates.Sort(static (a, b) => a.DistSq.CompareTo(b.DistSq));

			int limit = Math.Min(MaxMarionetteTethersPerClone, marionetteSortedCandidates.Count);
			for (int i = 0; i < limit; i++)
			{
				MarionetteTetherNPCs.Add(marionetteSortedCandidates[i].AnchorId);
			}
		}

		public override void OnKill(int timeLeft)
		{
			bool hasCrucible = false;
			if (!SuppressCycleOnKill && Owner != null && Owner.active)
			{
				var np = Owner.GetModPlayer<NaughtPlayer>();
				if (np != null)
				{
					hasCrucible = np.HasShadowCrucible;
					if (Main.myPlayer == Owner.whoAmI)
					{
						int expiringCount = CountExpiringCycleClones();
						np.OnCloneCycleComplete(Projectile.Center, expiringCount);
					}
				}
			}

			if (Main.dedServ)
				return;

			if (hasCrucible)
			{
				Color burstTint = CrucibleHasBurningTarget ? new Color(255, 55, 115) : new Color(195, 55, 255);
				PRTLoader.NewParticle<DimensionalExplosionPRT>(Projectile.Center, Vector2.Zero, Color.White, 0.72f)
					.Configure(20, burstTint);
			}

			// Ethereal purple dispersion sparks and red petals via InnoVault PRT
			int sparkCount = hasCrucible ? 12 : 8;
			for (int i = 0; i < sparkCount; i++)
			{
				Vector2 vel = Main.rand.NextVector2Circular(hasCrucible ? 4.8f : 3.5f, hasCrucible ? 4.8f : 3.5f);
				Color col = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
				PRTLoader.NewParticle<VoidSparkPRT>(Projectile.Center, vel, col, hasCrucible ? 0.95f : 0.85f);
			}

			int petalCount = SyncDissipating ? 9 : 3;
			for (int p = 0; p < petalCount; p++)
			{
				Vector2 pVel = Main.rand.NextVector2Circular(SyncDissipating ? 3.6f : 2f, SyncDissipating ? 3.6f : 2f) - Vector2.UnitY * 1.0f;
				PRTLoader.NewParticle<CrimsonPetalPRT>(Projectile.Center, pVel, new Color(245, 30, 55, 220), SyncDissipating ? 0.90f : 0.75f);
			}
		}

		private int CountExpiringCycleClones()
		{
			int count = 1; // Count this clone even if active is clearing during OnKill
			int cloneType = Projectile.type;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				if (i == Projectile.whoAmI)
					continue;

				Projectile p = Main.projectile[i];
				if (p.active && p.owner == Projectile.owner && p.type == cloneType && p.timeLeft <= 2)
				{
					count++;
				}
			}
			return count;
		}

		public void PerformMirrorRainblade(int slashStage, Vector2 dir, float damageScale = 1f)
		{
			var naughtPlayer = Owner?.GetModPlayer<NaughtPlayer>();
			if (Owner == null || !Owner.active || naughtPlayer == null || !naughtPlayer.HasSynchronizedExecution)
				return;

			int damage = Math.Max(1, naughtPlayer.GetNaughtDamage(WeepBalance.Active.SyncMirrorRainbladeMult * damageScale));
			Projectile.CritChance = naughtPlayer.GetNaughtCritChance();

			SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.35f, Volume = 0.75f }, Projectile.Center);

			if (Main.myPlayer == Owner.whoAmI)
			{
				bool facingRight = dir.X >= 0f;
				float slashAngle = slashStage switch
				{
					0 => facingRight ? -0.35f : MathHelper.Pi + 0.35f,
					1 => facingRight ? 0.35f : MathHelper.Pi - 0.35f,
					_ => facingRight ? 0.05f : MathHelper.Pi - 0.05f
				};
				int flipDir = (slashStage == 1) ? (facingRight ? -1 : 1) : (facingRight ? 1 : -1);

				Vector3 colHot = new Vector3(1.0f, 0.94f, 0.98f);
				Vector3 colBright = slashStage == 2 ? new Vector3(0.95f, 0.14f, 0.32f) : new Vector3(0.84f, 0.18f, 0.98f);
				Vector3 colDeep = new Vector3(0.46f, 0.04f, 0.58f);
				Vector3 colDark = new Vector3(0.10f, 0.01f, 0.14f);

				var slash = NaughtSlashVisual.SpawnSlashArc(
					Projectile.GetSource_FromThis(),
					Projectile.Center + slashAngle.ToRotationVector2() * 32f,
					slashAngle,
					175f,
					MathHelper.ToRadians(155f),
					flipDir,
					18,
					colHot,
					colBright,
					colDeep,
					colDark,
					damage: 0,
					knockback: 0f,
					parentWhoAmI: -1,
					owner: Owner.whoAmI);

				if (slash != null)
				{
					slash.AnchorToOwner = false;
					slash.VisualOnly = true;
					slash.Projectile.friendly = false;
				}
			}

			int hitDir = Math.Sign(dir.X);
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (npc.active && !npc.friendly && (!npc.dontTakeDamage || npc.type == NPCID.TargetDummy) && Vector2.Distance(Projectile.Center, npc.Center) < 220f)
				{
					naughtPlayer.StrikeNPC(npc, damage, hitDir, crit: null, knockBack: 4f, sourceProj: Projectile, isUltimate: true);
				}
			}
		}

		public void PerformSheathingCrossSlash(NPC target, Vector2? fallbackCenter = null, float damageScale = 1f, bool emitRuptureFlash = false)
		{
			var naughtPlayer = Owner?.GetModPlayer<NaughtPlayer>();
			if (Owner == null || !Owner.active || naughtPlayer == null || !naughtPlayer.HasSynchronizedExecution)
				return;

			bool hasLiveTarget = target != null && target.active;
			if (!hasLiveTarget && !fallbackCenter.HasValue)
				return;

			Vector2 centerPos = hasLiveTarget ? target.Center : fallbackCenter.Value;
			Vector2 dashDir = (centerPos - Projectile.Center).SafeNormalize(new Vector2(Owner.direction, 0f));

			float halfSize = hasLiveTarget ? MathF.Max(target.width, target.height) * 0.5f : 24f;
			float exitOffset = MathHelper.Clamp(halfSize + 56f, 64f, 240f);

			syncCrossSlashStart = Projectile.Center;
			syncCrossSlashTargetCenter = centerPos;
			syncCrossSlashEnd = centerPos + dashDir * exitOffset;
			Projectile.Center = syncCrossSlashEnd;

			syncCrossSlashTimer = SyncCrossSlashDuration;
			syncEmitRuptureFlash = emitRuptureFlash;
			syncShaderSeed = (Projectile.whoAmI % 17) * 0.19f;

			SoundEngine.PlaySound(WeepSound.KatanaHitB with { Pitch = 0.1f, Volume = 0.95f }, centerPos);

			if (hasLiveTarget && (!target.dontTakeDamage || target.type == NPCID.TargetDummy))
			{
				int damage = Math.Max(1, naughtPlayer.GetNaughtDamage(WeepBalance.Active.SyncCrossSlashMult * damageScale));
				Projectile.CritChance = naughtPlayer.GetNaughtCritChance();
				naughtPlayer.StrikeNPC(target, damage, Math.Sign(dashDir.X), crit: true, knockBack: 8f, sourceProj: Projectile, isUltimate: true);
			}

			for (int p = 0; p < 6; p++)
			{
				PRTLoader.NewParticle<CrimsonPetalPRT>(centerPos, Main.rand.NextVector2Circular(4f, 4f), new Color(238, 26, 56), 0.90f);
			}

			// Cleanly dissipate participating clone into crimson petals without double-dipping OnCloneCycleComplete
			SuppressCycleOnKill = true;
			SyncDissipating = true;
			syncDissipateTimer = 0;
			Projectile.timeLeft = SyncDissipateDuration;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ || !poseCaptured || Owner == null || !Owner.active)
				return false;

			if (syncCrossSlashTimer > 0)
			{
				DrawSyncCrossSlashShaderEffects();
			}

			var naughtPlayer = Owner.GetModPlayer<NaughtPlayer>();
			float fadeIn = VaultUtils.EaseQuadOut(MathHelper.Clamp(Age / 12f, 0f, 1f));
			float fadeOut = SyncDissipating
				? 1f - VaultUtils.EaseQuadIn(MathHelper.Clamp(syncDissipateTimer / (float)SyncDissipateDuration, 0f, 1f))
				: (naughtPlayer != null && naughtPlayer.IsInUltimate ? 1f : VaultUtils.EaseQuadOut(MathHelper.Clamp(Projectile.timeLeft / 35f, 0f, 1f)));

			// Translucent ethereal phantom opacity (approx 40% base)
			float opacity = fadeIn * fadeOut * 0.42f;

			if (opacity <= 0.01f)
				return false;

			if (naughtPlayer != null)
			{
				// Shadow Marionette: Draw distinct pure-crimson puppet strings with multi-clone tension escalation
				if (naughtPlayer.HasShadowMarionette && MarionetteTetherNPCs.Count > 0)
				{
					float stringVisibility = fadeIn * fadeOut;
					Vector2 cloneCenter = Projectile.Center - Main.screenPosition;
					for (int i = 0; i < MarionetteTetherNPCs.Count; i++)
					{
						int id = MarionetteTetherNPCs[i];
						if (id >= 0 && id < Main.maxNPCs && Main.npc[id].active)
						{
							NPC targetNpc = Main.npc[id];
							int tetherStack = targetNpc.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData)
								? Math.Clamp(knotData.MarionetteTetherCount, 1, MaxMarionetteClones)
								: 1;
							DrawPuppetString(cloneCenter, targetNpc.Center - Main.screenPosition, Vector2.Distance(Projectile.Center, targetNpc.Center), tetherStack, stringVisibility, i);
						}
					}
				}

				// Shadow Crucible: Draw upgraded 18-block furnace aura ring
				if (naughtPlayer.HasShadowCrucible)
				{
					Vector2 cloneCenter = Projectile.Center - Main.screenPosition;
					DrawCrucibleAuraRing(Main.spriteBatch, cloneCenter, fadeIn * fadeOut, CrucibleActiveOverlapMax, CrucibleHasBurningTarget);
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

		private static void DrawPuppetString(Vector2 startScreen, Vector2 endScreen, float worldDist, int tetherStack, float visibility, int stringIndex)
		{
			if (visibility <= 0.01f || worldDist < 2f)
				return;

			float stackProgress = MathHelper.Clamp((tetherStack - 1f) / 3f, 0f, 1f);
			float tension = MathHelper.Clamp((worldDist - MarionetteSlackRadius) / (MarionetteTetherRange - MarionetteSlackRadius), 0f, 1f);

			Color glowColor = Color.Lerp(new Color(180, 12, 42), new Color(245, 28, 65), stackProgress) * (visibility * (0.34f + 0.36f * stackProgress + 0.15f * tension));
			Color coreColor = Color.Lerp(new Color(238, 26, 56), new Color(255, 175, 195), stackProgress * 0.65f + tension * 0.35f) * (visibility * (0.68f + 0.27f * stackProgress));

			float coreWidth = 1.15f + 0.35f * (tetherStack - 1);
			float glowWidth = 3.5f + 1.4f * (tetherStack - 1) + 1.2f * tension;

			Vector2 diff = endScreen - startScreen;
			Vector2 dir = diff / worldDist;
			Vector2 perp = new Vector2(-dir.Y, dir.X);

			// Puppet-string catenary droop when slack; pulls taut and vibrates when stretched past 175px
			float sag = 10f * (1f - tension);
			float pulse = tension > 0.05f
				? MathF.Sin(Main.GlobalTimeWrappedHourly * 34f + stringIndex * 2.1f) * (1.5f * tension * (0.5f + 0.5f * stackProgress))
				: 0f;

			const int segments = 8;
			Vector2 prev = startScreen;
			for (int s = 1; s <= segments; s++)
			{
				float u = s / (float)segments;
				float arc = MathF.Sin(u * MathHelper.Pi);
				Vector2 pt = Vector2.Lerp(startScreen, endScreen, u);
				pt.Y += sag * arc;
				pt += perp * (pulse * MathF.Sin(u * MathHelper.TwoPi));

				NihilityLedgerTheme.DrawGlowLine(Main.spriteBatch, prev, pt, coreColor, glowColor, coreWidth, glowWidth);
				prev = pt;
			}
		}

		private static void DrawCrucibleAuraRing(SpriteBatch sb, Vector2 center, float visibility, int overlapMax, bool hasBurningTarget)
		{
			if (visibility <= 0.01f)
				return;

			int clampedOverlap = Math.Clamp(overlapMax, 0, MaxCrucibleClones);
			float heatRatio = MathHelper.Clamp(clampedOverlap / (float)MaxCrucibleClones, 0f, 1f);
			float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * (hasBurningTarget ? 6.5f : 3.2f));

			Color coolViolet = NihilityLedgerTheme.NihilityViolet;
			Color moltenMagenta = new Color(225, 45, 175);
			Color searingScarlet = NihilityLedgerTheme.AcheronScarlet;

			Color baseCol = heatRatio <= 0.4f
				? Color.Lerp(coolViolet, moltenMagenta, heatRatio / 0.4f)
				: Color.Lerp(moltenMagenta, searingScarlet, (heatRatio - 0.4f) / 0.6f);

			if (hasBurningTarget)
			{
				baseCol = Color.Lerp(baseCol, searingScarlet, 0.25f + 0.15f * pulse);
			}

			float glowAlpha = visibility * (0.14f + 0.14f * heatRatio + (hasBurningTarget ? 0.06f * pulse : 0f));
			float coreAlpha = visibility * (0.42f + 0.32f * heatRatio + (hasBurningTarget ? 0.12f * pulse : 0f));
			float coreWidth = 1.6f + 0.85f * heatRatio;
			float glowWidth = 5.0f + 2.8f * heatRatio;

			NihilityLedgerTheme.DrawCircle(sb, center, CrucibleRadius, baseCol * glowAlpha, glowWidth, 64);
			NihilityLedgerTheme.DrawCircle(sb, center, CrucibleRadius, Color.Lerp(baseCol, Color.White, 0.22f) * coreAlpha, coreWidth, 64);

			if (clampedOverlap > 0)
			{
				float innerRadius = CrucibleRadius - 10f + 3.5f * pulse;
				NihilityLedgerTheme.DrawCircle(sb, center, innerRadius, baseCol * (visibility * (0.18f + 0.18f * heatRatio)), 1.25f, 56);
			}

			// Rotating perimeter furnace rune ticks indicating zone charge / overlap tier
			int tickCount = 4 + clampedOverlap * 2;
			float rotSpeed = 0.45f + 0.35f * heatRatio;
			float baseRot = Main.GlobalTimeWrappedHourly * rotSpeed;
			Color tickGlow = baseCol * (visibility * (0.28f + 0.32f * heatRatio));
			Color tickCore = Color.Lerp(baseCol, NihilityLedgerTheme.HotWhite, 0.45f) * (visibility * (0.55f + 0.35f * heatRatio));
			for (int i = 0; i < tickCount; i++)
			{
				float ang = baseRot + MathHelper.TwoPi * i / tickCount;
				Vector2 dir = ang.ToRotationVector2();
				Vector2 p0 = center + dir * (CrucibleRadius - 7f);
				Vector2 p1 = center + dir * (CrucibleRadius + 7f);
				NihilityLedgerTheme.DrawGlowLine(sb, p0, p1, tickCore, tickGlow, 1.5f, 4.0f);
			}
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

		private void DrawSyncCrossSlashShaderEffects()
		{
			Effect fx = ShaderLoader.SyncCrossSlashShader;
			Texture2D brush = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/SlashBrush01", AssetRequestMode.ImmediateLoad).Value;
			Texture2D noise = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Projectiles/RiftNoise", AssetRequestMode.ImmediateLoad).Value;
			if (fx == null || brush == null || noise == null)
				return;

			float progress = 1f - MathHelper.Clamp(syncCrossSlashTimer / (float)SyncCrossSlashDuration, 0f, 1f);
			SpriteBatch sb = Main.spriteBatch;
			GraphicsDevice device = Main.graphics.GraphicsDevice;
			SpriteBatchState state = sb.Capture();
			sb.End();

			BlendState origBlend = device.BlendState;
			RasterizerState origRaster = device.RasterizerState;
			device.BlendState = BlendState.AlphaBlend;
			device.RasterizerState = RasterizerState.CullNone;

			Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, 0, 1);
			Matrix view = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0)) * Main.GameViewMatrix.TransformationMatrix;

			fx.Parameters["transformMatrix"]?.SetValue(view * projection);
			fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			fx.Parameters["uBrushTex"]?.SetValue(brush);
			fx.Parameters["uNoiseTex"]?.SetValue(noise);
			fx.Parameters["uSeed"]?.SetValue(syncShaderSeed);
			fx.Parameters["uColHot"]?.SetValue(new Vector3(1.0f, 0.94f, 0.98f));
			fx.Parameters["uColBright"]?.SetValue(new Vector3(0.96f, 0.14f, 0.34f));
			fx.Parameters["uColDeep"]?.SetValue(new Vector3(0.54f, 0.05f, 0.68f));
			fx.Parameters["uColDark"]?.SetValue(new Vector3(0.10f, 0.01f, 0.14f));
			fx.Parameters["uProgress"]?.SetValue(progress);
			fx.Parameters["uFlash"]?.SetValue(MathF.Max(0f, 1f - progress * 3.5f));

			Vector2 span = syncCrossSlashEnd - syncCrossSlashStart;
			float len = span.Length();
			if (len > 4f)
			{
				Vector2 dir = span / len;
				Vector2 perp = new Vector2(-dir.Y, dir.X);

				// 1. Afterimage Trail (uMode = 1)
				fx.Parameters["uMode"]?.SetValue(1f);
				fx.Parameters["uOpacity"]?.SetValue(0.75f);
				DrawQuadStrip(device, fx, syncCrossSlashStart, syncCrossSlashEnd, perp * 26f);

				// 2. Convergent Cross-Slash Beam (uMode = 0)
				fx.Parameters["uMode"]?.SetValue(0f);
				fx.Parameters["uOpacity"]?.SetValue(1.0f);
				DrawQuadStrip(device, fx, syncCrossSlashStart - dir * 30f, syncCrossSlashEnd + dir * 30f, perp * 34f);
			}

			// 3. Crimson Rupture Flash at target center (uMode = 2)
			if (syncEmitRuptureFlash)
			{
				float r = 175f;
				fx.Parameters["uMode"]?.SetValue(2f);
				fx.Parameters["uOpacity"]?.SetValue(0.95f);
				Vector2 c = syncCrossSlashTargetCenter;
				VertexPositionColorTexture[] flashVerts = new VertexPositionColorTexture[4]
				{
					new VertexPositionColorTexture(new Vector3(c.X - r, c.Y - r, 0f), Color.White, new Vector2(0f, 0f)),
					new VertexPositionColorTexture(new Vector3(c.X + r, c.Y - r, 0f), Color.White, new Vector2(1f, 0f)),
					new VertexPositionColorTexture(new Vector3(c.X - r, c.Y + r, 0f), Color.White, new Vector2(0f, 1f)),
					new VertexPositionColorTexture(new Vector3(c.X + r, c.Y + r, 0f), Color.White, new Vector2(1f, 1f))
				};
				foreach (EffectPass pass in fx.CurrentTechnique.Passes)
				{
					pass.Apply();
					device.DrawUserPrimitives(PrimitiveType.TriangleStrip, flashVerts, 0, 2);
				}
			}

			device.BlendState = origBlend;
			device.RasterizerState = origRaster;
			sb.Restart(in state);
		}

		private static void DrawQuadStrip(GraphicsDevice device, Effect fx, Vector2 start, Vector2 end, Vector2 halfNormal)
		{
			VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4]
			{
				new VertexPositionColorTexture(new Vector3(start - halfNormal, 0f), Color.White, new Vector2(0f, 0f)),
				new VertexPositionColorTexture(new Vector3(end - halfNormal, 0f), Color.White, new Vector2(1f, 0f)),
				new VertexPositionColorTexture(new Vector3(start + halfNormal, 0f), Color.White, new Vector2(0f, 1f)),
				new VertexPositionColorTexture(new Vector3(end + halfNormal, 0f), Color.White, new Vector2(1f, 1f))
			};
			foreach (EffectPass pass in fx.CurrentTechnique.Passes)
			{
				pass.Apply();
				device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
			}
		}
	}
}
