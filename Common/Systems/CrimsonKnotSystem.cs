using System;
using System.Collections.Generic;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.GlobalNPCs;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Content.Buffs;
using WeepForTheDeparted.Content.Projectiles;
using WeepForTheDeparted.Content.Projectiles.Charms;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Common.Systems
{
	/// <summary>
	/// ModSystem that ensures Crimson Knot debuff tracking, pop animation timers,
	/// ambient particles, and Red Oni boss openers/milestones update reliably for ALL NPCs every frame.
	/// Running in PostUpdateNPCs guarantees it cannot be skipped or bypassed by any
	/// NPCLoader.NPCAI or NPCOverride hooks (such as Calamity Overhaul's Brutal NPCs).
	/// </summary>
	public class CrimsonKnotSystem : ModSystem
	{
		private int vanillaEoWPeakTotalLife = 0;
		private byte vanillaEoWMilestonesCrossed = 0;

		public override void ClearWorld()
		{
			vanillaEoWPeakTotalLife = 0;
			vanillaEoWMilestonesCrossed = 0;
			MirageWeftThread.ResetPendingDebuffCheck();
			BloodLotusShockwave.ResetGlobalHitCooldowns();
		}

		public override void OnWorldUnload()
		{
			BloodLotusShockwave.ResetGlobalHitCooldowns();
		}

		public override void PostUpdateProjectiles()
		{
			MirageWeftThread.FlushPendingDebuffCheck();
		}

		public override void PostUpdateNPCs()
		{
			if (Main.netMode == NetmodeID.Server)
				return;

			Player localPlayer = Main.LocalPlayer;
			if (localPlayer == null || !localPlayer.active || localPlayer.dead)
				return;

			var naughtPlayer = localPlayer.GetModPlayer<NaughtPlayer>();
			bool hasNaught = naughtPlayer != null && naughtPlayer.IsNaughtEquipped;

			CrimsonKnotGlobalNPC.RefreshCrucibleOverlapsIfNeeded();

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active)
					continue;

				if (npc.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData))
				{
					knotData.MarionetteTetherCount = 0;
					knotData.MarionetteTetherAnchorSum = Vector2.Zero;

					knotData.UpdateFromSystem(npc, naughtPlayer, localPlayer, hasNaught);
				}
			}

			UpdateCrucibleVisualHeatAndParticles(localPlayer, naughtPlayer);
			UpdateMarionetteTetherCrowdControl(localPlayer, naughtPlayer);
			PropagateMarionetteDebuffs(localPlayer, naughtPlayer);
			ProcessRedOniBossPass(localPlayer, naughtPlayer);
		}

		private static readonly HashSet<int> marionetteDebuffRecipients = new HashSet<int>();

		private static bool IsInternalWeepDebuff(int buffType)
		{
			return buffType == ModContent.BuffType<AbyssGripDebuff>()
				|| buffType == ModContent.BuffType<WitheredRiverDebuff>()
				|| buffType == ModContent.BuffType<ShadowCrucibleDebuff>()
				|| buffType == ModContent.BuffType<RainleafCooldownBuff>();
		}

		private static void PropagateMarionetteDebuffs(Player localPlayer, NaughtPlayer naughtPlayer)
		{
			if (WorldFreezeSystem.IsActive || naughtPlayer == null || !naughtPlayer.HasShadowMarionette)
				return;

			marionetteDebuffRecipients.Clear();
			int cloneType = ModContent.ProjectileType<NaughtClone>();

			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.owner != localPlayer.whoAmI || p.type != cloneType || p.ModProjectile is not NaughtClone clone)
					continue;

				if (clone.MarionetteTetherNPCs.Count < 2)
					continue;

				for (int s = 0; s < clone.MarionetteTetherNPCs.Count; s++)
				{
					int sourceId = clone.MarionetteTetherNPCs[s];
					if (sourceId < 0 || sourceId >= Main.maxNPCs)
						continue;

					NPC sourceNpc = Main.npc[sourceId];
					if (!sourceNpc.active || sourceNpc.friendly || sourceNpc.isLikeATownNPC)
						continue;

					if (!sourceNpc.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var sourceData))
						continue;

					sourceData.PruneExpiredMarionetteDebuffs(sourceNpc);

					for (int b = 0; b < NPC.maxBuffs; b++)
					{
						int debuffType = sourceNpc.buffType[b];
						int duration = sourceNpc.buffTime[b];

						if (debuffType <= 0 || debuffType >= Main.debuff.Length || !Main.debuff[debuffType] || duration <= 0)
							continue;

						if (IsInternalWeepDebuff(debuffType) || sourceData.MarionetteSharedDebuffs.Contains(debuffType))
							continue;

						for (int t = 0; t < clone.MarionetteTetherNPCs.Count; t++)
						{
							int targetId = clone.MarionetteTetherNPCs[t];
							if (targetId == sourceId || targetId < 0 || targetId >= Main.maxNPCs)
								continue;

							NPC targetNpc = Main.npc[targetId];
							if (!targetNpc.active || targetNpc.friendly || targetNpc.isLikeATownNPC)
								continue;

							if (targetNpc.dontTakeDamage && targetNpc.type != NPCID.TargetDummy)
								continue;

							if (debuffType < targetNpc.buffImmune.Length && targetNpc.buffImmune[debuffType])
								continue;

							if (!targetNpc.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var targetData))
								continue;

							targetData.PruneExpiredMarionetteDebuffs(targetNpc);

							int existingIdx = targetNpc.FindBuffIndex(debuffType);
							if (existingIdx < 0)
							{
								targetData.MarionetteSharedDebuffs.Add(debuffType);
								targetNpc.AddBuff(debuffType, duration, quiet: true);

								int appliedIdx = targetNpc.FindBuffIndex(debuffType);
								if (appliedIdx >= 0)
								{
									targetData.RecordMarionetteSharedDebuff(debuffType, targetNpc.buffTime[appliedIdx]);
									marionetteDebuffRecipients.Add(targetId);
								}
								else
								{
									targetData.MarionetteSharedDebuffs.Remove(debuffType);
								}
							}
							else if (targetData.MarionetteSharedDebuffs.Contains(debuffType))
							{
								if (targetNpc.buffTime[existingIdx] < duration)
								{
									targetNpc.buffTime[existingIdx] = duration;
								}
								targetData.RecordMarionetteSharedDebuff(debuffType, targetNpc.buffTime[existingIdx]);
								marionetteDebuffRecipients.Add(targetId);
							}
						}
					}
				}
			}

			// Count shared debuffs as strictly 1 Crimson Knot instance per tethered enemy (9s CD), never Slashed Dream
			foreach (int targetId in marionetteDebuffRecipients)
			{
				NPC targetNpc = Main.npc[targetId];
				if (!targetNpc.active || !targetNpc.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var targetData))
					continue;

				if (targetData.MarionetteSharedKnotCooldown <= 0 && targetData.CrimsonKnotStacks < CrimsonKnotGlobalNPC.MaxKnotStacks)
				{
					CrimsonKnotGlobalNPC.AddKnot(targetNpc, 1, targetNpc.Center);
					targetData.MarionetteSharedKnotCooldown = NaughtPlayer.DebuffCooldownTicks;
				}
			}
		}

		private static void UpdateCrucibleVisualHeatAndParticles(Player localPlayer, NaughtPlayer naughtPlayer)
		{
			if (Main.dedServ)
				return;

			int cloneType = ModContent.ProjectileType<NaughtClone>();
			float radiusSq = NaughtClone.CrucibleRadius * NaughtClone.CrucibleRadius;

			// Update per-clone furnace heat telemetry from overlapped NPCs & active DoTs
			for (int pIdx = 0; pIdx < Main.maxProjectiles; pIdx++)
			{
				Projectile p = Main.projectile[pIdx];
				if (!p.active || p.type != cloneType || p.ModProjectile is not NaughtClone clone)
					continue;

				clone.CrucibleActiveOverlapMax = 0;
				clone.CrucibleHasBurningTarget = false;

				var ownerNaught = clone.Owner?.GetModPlayer<NaughtPlayer>();
				if (ownerNaught == null || !ownerNaught.HasShadowCrucible)
					continue;

				Vector2 clonePos = p.Center;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC npc = Main.npc[i];
					if (!npc.active || npc.friendly || npc.isLikeATownNPC || npc.lifeMax <= 5)
						continue;

					if (npc.dontTakeDamage && npc.type != NPCID.TargetDummy)
						continue;

					if (Vector2.DistanceSquared(clonePos, npc.Hitbox.ClosestPointInRect(clonePos)) >= radiusSq)
						continue;

					NPC anchor = CrimsonKnotGlobalNPC.ResolveKnotAnchor(npc);
					if (anchor == null || !anchor.active || !anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var anchorData))
						continue;

					if (anchorData.CrucibleOverlapCount > clone.CrucibleActiveOverlapMax)
					{
						clone.CrucibleActiveOverlapMax = anchorData.CrucibleOverlapCount;
					}

					if (npc.lifeRegen < 0 || anchor.lifeRegen < 0)
					{
						clone.CrucibleHasBurningTarget = true;
					}
				}
			}

			if (WorldFreezeSystem.IsActive || naughtPlayer == null || !naughtPlayer.HasShadowCrucible)
				return;

			// Emit furnace embers on enemies branded with ShadowCrucibleDebuff
			int crucibleDebuffType = ModContent.BuffType<ShadowCrucibleDebuff>();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.isLikeATownNPC || npc.lifeMax <= 5)
					continue;

				if (npc.dontTakeDamage && npc.type != NPCID.TargetDummy)
					continue;

				NPC anchor = CrimsonKnotGlobalNPC.ResolveKnotAnchor(npc);
				if (anchor == null || !anchor.active || !anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var anchorData))
					continue;

				if (anchorData.CrucibleOverlapCount <= 0 && !anchor.HasBuff(crucibleDebuffType))
					continue;

				bool isBurning = npc.lifeRegen < 0 || anchor.lifeRegen < 0;
				if (anchorData.CrucibleOverlapCount > 0 && isBurning)
				{
					int chance = Math.Max(4, 14 - anchorData.CrucibleOverlapCount * 3);
					if (Main.rand.NextBool(chance))
					{
						Vector2 pos = npc.position + new Vector2(Main.rand.NextFloat(npc.width), Main.rand.NextFloat(npc.height));
						Vector2 vel = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.1f, 2.4f));
						Color col = Main.rand.NextBool() ? new Color(195, 55, 255) : new Color(255, 45, 95);
						PRTLoader.NewParticle<VoidSparkPRT>(pos, vel, col, 0.65f + 0.1f * anchorData.CrucibleOverlapCount);
					}
				}
				else if (Main.rand.NextBool(18))
				{
					Vector2 pos = npc.position + new Vector2(Main.rand.NextFloat(npc.width), Main.rand.NextFloat(npc.height));
					Dust d = Dust.NewDustPerfect(pos, DustID.Shadowflame, -Vector2.UnitY * Main.rand.NextFloat(0.35f, 0.85f), 130, default, 0.85f);
					d.noGravity = true;
				}
			}
		}

		private static void UpdateMarionetteTetherCrowdControl(Player localPlayer, NaughtPlayer naughtPlayer)
		{
			if (naughtPlayer == null || !naughtPlayer.HasShadowMarionette)
				return;

			int cloneType = ModContent.ProjectileType<NaughtClone>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.owner != localPlayer.whoAmI || p.type != cloneType || p.ModProjectile is not NaughtClone clone)
					continue;

				for (int t = 0; t < clone.MarionetteTetherNPCs.Count; t++)
				{
					int tetherId = clone.MarionetteTetherNPCs[t];
					if (tetherId >= 0 && tetherId < Main.maxNPCs && Main.npc[tetherId].active && Main.npc[tetherId].TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var kd))
					{
						kd.MarionetteTetherCount++;
						kd.MarionetteTetherAnchorSum += clone.Projectile.Center;
					}
				}
			}

			if (WorldFreezeSystem.IsActive)
				return;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.isLikeATownNPC || npc.type == NPCID.TargetDummy || CrimsonKnotGlobalNPC.IsBossTarget(npc))
					continue;

				if (!npc.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var kd) || kd.MarionetteTetherCount <= 0)
					continue;

				int effectiveClones = Math.Min(kd.MarionetteTetherCount, NaughtClone.MaxMarionetteClones);

				// 1. Puppet-String Snare: 25%..40% movement slow via safe position rollback (preserves gravity)
				float slowRatio = 0.25f + 0.05f * (effectiveClones - 1);
				Vector2 moveDelta = npc.position - npc.oldPosition;
				if (moveDelta.LengthSquared() < 1600f)
				{
					npc.position.X -= moveDelta.X * slowRatio;
					if (npc.noGravity)
					{
						npc.position.Y -= moveDelta.Y * slowRatio;
					}
				}

				// 2. Elastic Puppet Leash: Smoothly drags tethered mobs back toward the clone centroid beyond 175px slack
				if (npc.knockBackResist > 0f)
				{
					Vector2 centroid = kd.MarionetteTetherAnchorSum / kd.MarionetteTetherCount;
					Vector2 toAnchor = centroid - npc.Center;
					float dist = toAnchor.Length();
					if (dist > NaughtClone.MarionetteSlackRadius && dist < NaughtClone.MarionetteTetherRange * 1.25f)
					{
						float stretch = MathHelper.Clamp((dist - NaughtClone.MarionetteSlackRadius) / (NaughtClone.MarionetteTetherRange - NaughtClone.MarionetteSlackRadius), 0f, 1f);
						float pullAccel = (0.18f + 0.08f * effectiveClones) * stretch * MathHelper.Clamp(npc.knockBackResist, 0.25f, 1f);
						Vector2 pullDir = toAnchor / dist;

						npc.velocity.X += pullDir.X * pullAccel;
						if (npc.noGravity)
						{
							npc.velocity.Y += pullDir.Y * pullAccel;
						}
						else if (pullDir.Y < 0f && npc.velocity.Y < 0f)
						{
							npc.velocity.Y += pullDir.Y * pullAccel * 0.45f;
						}
					}
				}
			}
		}

		private void ProcessRedOniBossPass(Player localPlayer, NaughtPlayer naughtPlayer)
		{
			if (naughtPlayer == null)
				return;

			bool hasRedOni = naughtPlayer.HasRedOniTrace;
			bool hasNaughtInInv = naughtPlayer.HasNaughtInInventory();
			bool canAward = hasRedOni && hasNaughtInInv;
			Vector2 playerCenter = localPlayer.MountedCenter;

			bool anyBossInRange = false;
			int vanillaEoWCurrentLife = 0;
			int vanillaEoWCount = 0;
			NPC vanillaEoWNearest = null;
			float vanillaEoWNearestDistSq = float.MaxValue;

			// 1. Scan active NPCs after all AI() ticks so realLife links and CWR intros are finalized
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.lifeMax <= 5 || !CrimsonKnotGlobalNPC.IsBossTarget(npc))
					continue;

				float distSq = Vector2.DistanceSquared(playerCenter, npc.Center);
				if (distSq < 1800f * 1800f)
				{
					anyBossInRange = true;
				}

				if (CrimsonKnotGlobalNPC.IsVanillaEoWSegment(npc))
				{
					vanillaEoWCount++;
					vanillaEoWCurrentLife += Math.Max(0, npc.life);
					if (distSq < vanillaEoWNearestDistSq)
					{
						vanillaEoWNearestDistSq = distSq;
						vanillaEoWNearest = npc;
					}
					continue;
				}

				// Evaluate per-boss milestones strictly on the resolved anchor
				NPC anchor = CrimsonKnotGlobalNPC.ResolveKnotAnchor(npc);
				if (anchor == null || anchor.whoAmI != npc.whoAmI || anchor.lifeMax <= 5)
					continue;

				if (!IsAnchorNearCombat(anchor, playerCenter))
					continue;

				if (anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData))
				{
					float currentRatio = MathHelper.Clamp(anchor.life / (float)anchor.lifeMax, 0f, 1f);
					int crossed = knotData.EvaluateRedOniMilestones(currentRatio, canAward);
					if (crossed > 0)
					{
						int dreamGain = crossed * WeepBalance.Active.RedOniMilestoneDream;
						naughtPlayer.AddSlashedDream(dreamGain);
						CrimsonKnotGlobalNPC.TriggerMilestoneFeedback(localPlayer, anchor, dreamGain);
					}
				}
			}

			// 2. Vanilla Eater of Worlds aggregate HP milestone pool (segments lack realLife)
			if (vanillaEoWCount > 0)
			{
				if (vanillaEoWCurrentLife > vanillaEoWPeakTotalLife)
				{
					vanillaEoWPeakTotalLife = vanillaEoWCurrentLife;
				}

				if (vanillaEoWPeakTotalLife > 0 && vanillaEoWNearestDistSq < 2400f * 2400f)
				{
					float eowRatio = MathHelper.Clamp(vanillaEoWCurrentLife / (float)vanillaEoWPeakTotalLife, 0f, 1f);
					int crossed = 0;
					for (int t = 0; t < CrimsonKnotGlobalNPC.RedOniThresholds.Length; t++)
					{
						byte bit = (byte)(1 << t);
						float threshold = CrimsonKnotGlobalNPC.RedOniThresholds[t];
						if ((vanillaEoWMilestonesCrossed & bit) == 0 && eowRatio <= threshold)
						{
							vanillaEoWMilestonesCrossed |= bit;
							if (canAward)
							{
								crossed++;
							}
						}
					}

					if (crossed > 0)
					{
						int dreamGain = crossed * WeepBalance.Active.RedOniMilestoneDream;
						naughtPlayer.AddSlashedDream(dreamGain);
						CrimsonKnotGlobalNPC.TriggerMilestoneFeedback(localPlayer, vanillaEoWNearest, dreamGain);
					}
				}
			}
			else
			{
				vanillaEoWPeakTotalLife = 0;
				vanillaEoWMilestonesCrossed = 0;
			}

			// 3. Boss Combat Opener: fires once per boss encounter when entering range (1800f px)
			if (anyBossInRange && canAward && naughtPlayer.CanTriggerRedOniBossEncounter())
			{
				naughtPlayer.MarkRedOniBossEncounterTriggered();
				naughtPlayer.TryTriggerRedOniOpener(applyKnots: true);
			}
		}

		private static bool IsAnchorNearCombat(NPC anchor, Vector2 playerCenter)
		{
			if (Vector2.DistanceSquared(playerCenter, anchor.Center) < 2400f * 2400f)
				return true;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC seg = Main.npc[i];
				if (seg.active && seg.realLife == anchor.whoAmI && Vector2.DistanceSquared(playerCenter, seg.Center) < 1800f * 1800f)
					return true;
			}

			return false;
		}
	}
}
