using System;
using System.Collections.Generic;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.Systems;
using WeepForTheDeparted.Common.UI.NihilityLedger;
using WeepForTheDeparted.Common.Visuals;
using WeepForTheDeparted.Content.Buffs;
using WeepForTheDeparted.Content.Projectiles;
using WeepForTheDeparted.Content.Projectiles.Charms;
using WeepForTheDeparted.Content.Shaders;
using WeepForTheDeparted.Content.Visuals.Particles;

namespace WeepForTheDeparted.Common.GlobalNPCs
{
	public class CrimsonKnotGlobalNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		public int CrimsonKnotStacks = 0;
		public const int MaxKnotStacks = 9;

		public int PopTimer = 0;
		public const int MaxPopTimer = 16;

		public int LastUpdatedFrame = -1;

		// Shadow Marionette 1-hop debuff sharing, tether count, and leash centroid tracking
		public readonly HashSet<int> MarionetteSharedDebuffs = new HashSet<int>();
		public int MarionetteSharedKnotCooldown = 0;
		public int MarionetteTetherCount = 0;
		public Vector2 MarionetteTetherAnchorSum = Vector2.Zero;

		// Shadow Crucible DoT furnace & cycle detonation tracking (persisted on resolved anchor)
		public int CrucibleOverlapCount = 0;
		public int CrucibleOwnerWhoAmI = -1;
		public float CrucibleAccumulatedDot = 0f;
		public bool CrucibleExposedThisCycle = false;
		public float CrucibleDisplayedChargeRatio = 0f;

		private static uint lastCrucibleOverlapFrame = uint.MaxValue;
		private static readonly HashSet<int> crucibleTouchedAnchors = new HashSet<int>();
		private static readonly List<NaughtClone> crucibleActiveClones = new List<NaughtClone>();
		private static readonly Dictionary<int, int> marionetteShareCounts = new Dictionary<int, int>();
		private static readonly HashSet<int> uniqueDebuffScratch = new HashSet<int>();

		// Mark of the Red Oni boss HP milestone tracking (per resolved boss anchor)
		public float HighestObservedHpRatio = 0f;
		public byte RedOniMilestonesCrossed = 0;
		public static readonly float[] RedOniThresholds = { 0.75f, 0.50f, 0.25f };

		// Scourge of the Withered River fallback tracking (for buff-immune targets) & visual animation state
		public int WitheredRiverFallbackTimer = 0;
		public float WitheredRiverVisualFade = 0f;
		public float WitheredRiverApplyFlash = 0f;

		private static bool witherShaderActive = false;
		private static int witherShaderNpcIndex = -1;
		private static int pendingWitheredDrPeelNpc = -1;
		private static float pendingWitheredDrPeel = 0f;

		public static float ConsumePendingWitheredDrPeel(int npcWhoAmI)
		{
			if (pendingWitheredDrPeelNpc != npcWhoAmI)
				return 0f;

			float peel = pendingWitheredDrPeel;
			pendingWitheredDrPeelNpc = -1;
			pendingWitheredDrPeel = 0f;
			return peel;
		}

		public static void ClearPendingWitheredDrPeel()
		{
			pendingWitheredDrPeelNpc = -1;
			pendingWitheredDrPeel = 0f;
		}

		private readonly HashSet<int> activeDebuffs = new HashSet<int>();
		private readonly Dictionary<int, int> lastBuffTimes = new Dictionary<int, int>();
		private readonly HashSet<int> thunderBarrageSuppressedDebuffs = new HashSet<int>();
		private readonly HashSet<int> mirageWeftSuppressedDebuffs = new HashSet<int>();

		public static void AddKnot(NPC target, int count = 1, Vector2? visualPos = null)
		{
			if (target == null || !target.active || count <= 0)
				return;

			NPC anchor = ResolveKnotAnchor(target) ?? target;
			if (anchor == null || !anchor.active)
				return;

			if (!anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData))
				return;

			knotData.CrimsonKnotStacks = Math.Clamp(knotData.CrimsonKnotStacks + count, 0, MaxKnotStacks);
			knotData.PopTimer = MaxPopTimer;

			Vector2 center = visualPos ?? target.Center;
			if (center != Vector2.Zero)
			{
				int sparkCount = 6 + knotData.CrimsonKnotStacks * 2;
				for (int i = 0; i < sparkCount; i++)
				{
					Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
					Color col = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
					PRTLoader.NewParticle<VoidSparkPRT>(center, vel, col, 0.85f);
				}

				if (knotData.CrimsonKnotStacks >= 6)
				{
					for (int p = 0; p < 3; p++)
					{
						Vector2 pVel = Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY * 1.5f;
						PRTLoader.NewParticle<CrimsonPetalPRT>(center, pVel, new Color(245, 30, 55, 220), 0.75f);
					}
				}
			}
		}

		public void AddCrimsonKnot(int count = 1, Vector2? visualPos = null)
		{
			if (count <= 0)
				return;

			CrimsonKnotStacks = Math.Clamp(CrimsonKnotStacks + count, 0, MaxKnotStacks);
			PopTimer = MaxPopTimer;

			Vector2 center = visualPos ?? Vector2.Zero;
			if (center != Vector2.Zero)
			{
				// Visual feedback: InnoVault void sparks and petals burst from the target
				int sparkCount = 6 + CrimsonKnotStacks * 2;
				for (int i = 0; i < sparkCount; i++)
				{
					Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
					Color col = Main.rand.NextBool() ? new Color(185, 45, 255) : new Color(255, 30, 60);
					PRTLoader.NewParticle<VoidSparkPRT>(center, vel, col, 0.85f);
				}

				if (CrimsonKnotStacks >= 6)
				{
					for (int p = 0; p < 3; p++)
					{
						Vector2 pVel = Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY * 1.5f;
						PRTLoader.NewParticle<CrimsonPetalPRT>(center, pVel, new Color(245, 30, 55, 220), 0.75f);
					}
				}
			}
		}

		public void ClearCrimsonKnot()
		{
			CrimsonKnotStacks = 0;
		}

		public int DetonateKnots(int maxToDetonate)
		{
			if (CrimsonKnotStacks <= 0)
				return 0;

			int count = Math.Min(CrimsonKnotStacks, maxToDetonate);
			CrimsonKnotStacks -= count;
			PopTimer = MaxPopTimer;
			return count;
		}

		public override bool PreAI(NPC npc)
		{
			if (NihilityDomainSystem.IsDomainActive && NihilityDomainSystem.TimeStopFactor < 0.9f)
			{
				// Heavy hitstop slowdown during Slashed Dream domain: slow motion
				if (Main.GameUpdateCount % 6 != 0)
				{
					npc.velocity *= 0.8f;
					return false;
				}
			}
			return base.PreAI(npc);
		}

		public override void PostAI(NPC npc)
		{
			Player localPlayer = Main.LocalPlayer;
			var naughtPlayer = (localPlayer != null && localPlayer.active && !localPlayer.dead) ? localPlayer.GetModPlayer<NaughtPlayer>() : null;
			bool hasNaught = naughtPlayer != null && naughtPlayer.IsNaughtEquipped;

			UpdateFromSystem(npc, naughtPlayer, localPlayer, hasNaught);
		}

		public void UpdateFromSystem(NPC npc, NaughtPlayer naughtPlayer, Player localPlayer, bool hasNaught)
		{
			if (npc == null || !npc.active)
				return;

			if (LastUpdatedFrame == (int)Main.GameUpdateCount)
				return;
			LastUpdatedFrame = (int)Main.GameUpdateCount;

			if (PopTimer > 0)
				PopTimer--;

			if (MarionetteSharedKnotCooldown > 0 && !WorldFreezeSystem.IsActive)
				MarionetteSharedKnotCooldown--;

			if (WitheredRiverFallbackTimer > 0 && !WorldFreezeSystem.IsActive)
				WitheredRiverFallbackTimer--;

			bool hasWitheredRiver = HasWitheredRiverDebuff(npc);
			WitheredRiverVisualFade = hasWitheredRiver
				? MathF.Min(WitheredRiverVisualFade + 1f / 14f, 1f)
				: MathF.Max(WitheredRiverVisualFade - 1f / 20f, 0f);
			if (WitheredRiverApplyFlash > 0f)
				WitheredRiverApplyFlash = MathF.Max(WitheredRiverApplyFlash - 1f / 18f, 0f);

			PruneExpiredMarionetteDebuffs(npc);

			// Ambient visual: subtle crimson-purple embers hovering around marked enemies
			if (CrimsonKnotStacks > 0 && Main.rand.NextBool(6))
			{
				Vector2 dustPos = npc.position + new Vector2(Main.rand.NextFloat(npc.width), Main.rand.NextFloat(npc.height));
				Dust d = Dust.NewDustPerfect(dustPos, DustID.Shadowflame, -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.0f), 120, default, 0.9f);
				d.noGravity = true;
			}

			// Visual feedback for Withered River debuff: dried Lethe ash & downward-drifting crimson embers
			if (!Main.dedServ && hasWitheredRiver)
			{
				if (Main.rand.NextBool(4))
				{
					Vector2 dustPos = npc.position + new Vector2(Main.rand.NextFloat(npc.width), Main.rand.NextFloat(npc.height));
					bool crimsonMote = Main.rand.NextBool(3);
					int dustType = crimsonMote ? DustID.CrimsonTorch : DustID.Ash;
					Vector2 dustVel = new Vector2(Main.rand.NextFloat(-0.55f, 0.55f), Main.rand.NextFloat(0.35f, 1.15f));
					Color dustColor = crimsonMote ? new Color(195, 38, 62) : new Color(185, 168, 174);
					Dust d = Dust.NewDustPerfect(dustPos, dustType, dustVel, 120, dustColor, crimsonMote ? 0.95f : 1.05f);
					d.noGravity = true;
				}

				if (Main.rand.NextBool(12))
				{
					Vector2 sparkPos = npc.position + new Vector2(Main.rand.NextFloat(npc.width), Main.rand.NextFloat(npc.height));
					Vector2 sparkVel = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.5f, 1.4f));
					Color sparkCol = Main.rand.NextBool(3) ? new Color(215, 195, 200) : new Color(210, 32, 60);
					PRTLoader.NewParticle<VoidSparkPRT>(sparkPos, sparkVel, sparkCol, 0.55f);
				}
			}

			// Visual feedback for Grip of the Abyss debuff: void violet & crimson abyssal motes
			if (!Main.dedServ && HasAbyssGripDebuff(npc) && Main.rand.NextBool(8))
			{
				Vector2 dustPos = npc.position + new Vector2(Main.rand.NextFloat(npc.width), Main.rand.NextFloat(npc.height));
				bool violetMote = Main.rand.NextBool(3);
				int dustType = violetMote ? DustID.Shadowflame : DustID.CrimsonTorch;
				Vector2 dustVel = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.55f, 0.25f));
				Color dustColor = violetMote ? new Color(185, 75, 255) : new Color(235, 35, 75);
				Dust d = Dust.NewDustPerfect(dustPos, dustType, dustVel, 130, dustColor, violetMote ? 0.90f : 0.80f);
				d.noGravity = true;
			}

			SyncCrucibleState(npc, naughtPlayer);

			if (!hasNaught || naughtPlayer == null || localPlayer == null)
			{
				thunderBarrageSuppressedDebuffs.Clear();
				mirageWeftSuppressedDebuffs.Clear();
				return;
			}

			// Scan all active buff slots on the NPC
			HashSet<int> currentDebuffs = new HashSet<int>();
			for (int i = 0; i < NPC.maxBuffs; i++)
			{
				int type = npc.buffType[i];
				int time = npc.buffTime[i];

				if (type > 0 && type < Main.debuff.Length && Main.debuff[type] && time > 0)
				{
					currentDebuffs.Add(type);

					bool isNew = !activeDebuffs.Contains(type);
					bool isRefreshed = lastBuffTimes.TryGetValue(type, out int prevTime) && time > prevTime + 60;

					if (isNew || isRefreshed)
					{
						// Scourge of the Withered River: debuffs on bosses linger longer (strictly on initial application)
						if (isNew && naughtPlayer.HasWitheredRiver && IsBossTarget(npc))
						{
							int extendedTime = (int)MathF.Round(time * WeepBalance.Active.WitheredRiverBossDurationMult);
							if (extendedTime > time)
							{
								npc.buffTime[i] = extendedTime;
								time = extendedTime;
							}
						}

						// Suppress TryTriggerDebuff (Slashed Dream & per-debuff Knots) for debuffs shared via Shadow Marionette or capped ThunderCore/MirageWeft hits
						if (!MarionetteSharedDebuffs.Contains(type)
							&& !thunderBarrageSuppressedDebuffs.Contains(type)
							&& !mirageWeftSuppressedDebuffs.Contains(type)
							&& localPlayer.MountedCenter.Distance(npc.Center) < 1800f)
						{
							naughtPlayer.TryTriggerDebuff(type, npc);
						}
					}

					lastBuffTimes[type] = time;
				}
			}

			if (lastBuffTimes.Count > currentDebuffs.Count)
			{
				var staleKeys = new List<int>();
				foreach (int key in lastBuffTimes.Keys)
				{
					if (!currentDebuffs.Contains(key))
						staleKeys.Add(key);
				}

				for (int i = 0; i < staleKeys.Count; i++)
				{
					lastBuffTimes.Remove(staleKeys[i]);
				}
			}

			activeDebuffs.Clear();
			foreach (int d in currentDebuffs)
			{
				activeDebuffs.Add(d);
			}
			thunderBarrageSuppressedDebuffs.Clear();
			mirageWeftSuppressedDebuffs.Clear();
		}

		public void PruneExpiredMarionetteDebuffs(NPC npc)
		{
			if (MarionetteSharedDebuffs.Count == 0 || npc == null || !npc.active)
				return;

			MarionetteSharedDebuffs.RemoveWhere(type => !npc.HasBuff(type));
		}

		public void RecordMarionetteSharedDebuff(int debuffType, int duration)
		{
			activeDebuffs.Add(debuffType);
			lastBuffTimes[debuffType] = duration;
		}

		public void RecordThunderBarrageDebuff(int debuffType, int duration)
		{
			thunderBarrageSuppressedDebuffs.Add(debuffType);
			activeDebuffs.Add(debuffType);
			lastBuffTimes[debuffType] = duration;
		}

		public void RecordMirageWeftDebuff(int debuffType)
		{
			mirageWeftSuppressedDebuffs.Add(debuffType);
		}

		public override void OnKill(NPC npc)
		{
			if (npc == null)
				return;

			CrucibleAccumulatedDot = 0f;
			CrucibleOverlapCount = 0;
			CrucibleExposedThisCycle = false;
			CrucibleDisplayedChargeRatio = 0f;
			CrucibleOwnerWhoAmI = -1;
			WitheredRiverFallbackTimer = 0;
			WitheredRiverVisualFade = 0f;
			WitheredRiverApplyFlash = 0f;

			// Mark of the Red Oni: award any remaining crossed 25% HP milestones on a lethal burst kill
			if (Main.netMode != NetmodeID.Server && npc.lifeMax > 5 && IsBossTarget(npc) && !IsVanillaEoWSegment(npc))
			{
				NPC anchor = ResolveKnotAnchor(npc);
				if (anchor != null && anchor.whoAmI == npc.whoAmI)
				{
					Player localPlayer = Main.LocalPlayer;
					var np = (localPlayer != null && localPlayer.active && !localPlayer.dead) ? localPlayer.GetModPlayer<NaughtPlayer>() : null;
					bool canAward = np != null && np.HasRedOniTrace && np.HasNaughtInInventory();
					int crossed = EvaluateRedOniMilestones(0f, canAward);
					if (crossed > 0)
					{
						int dreamGain = crossed * WeepBalance.Active.RedOniMilestoneDream;
						np.AddSlashedDream(dreamGain);
						TriggerMilestoneFeedback(localPlayer, anchor, dreamGain);
					}
				}
			}

			NPC deadAnchor = ResolveKnotAnchor(npc) ?? npc;
			int deadAnchorId = deadAnchor.whoAmI;
			var marionetteSurvivors = new HashSet<int>();
			int cloneProjType = ModContent.ProjectileType<NaughtClone>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.type == cloneProjType && p.ModProjectile is NaughtClone clone)
				{
					if (clone.MarionetteTetherNPCs.Remove(deadAnchorId) || (npc.whoAmI != deadAnchorId && clone.MarionetteTetherNPCs.Remove(npc.whoAmI)))
					{
						foreach (int tetherId in clone.MarionetteTetherNPCs)
						{
							if (tetherId >= 0 && tetherId < Main.maxNPCs && tetherId != deadAnchorId && tetherId != npc.whoAmI && Main.npc[tetherId].active)
							{
								marionetteSurvivors.Add(tetherId);
							}
						}
					}
				}
			}

			if (CrimsonKnotStacks <= 0)
				return;

			var naughtPlayer = Main.LocalPlayer?.GetModPlayer<NaughtPlayer>();
			bool hasBloodLotus = naughtPlayer != null && naughtPlayer.HasBloodLotus;
			int basekitTargetWhoAmI = -1;

			if (hasBloodLotus)
			{
				// Blood Lotus Transmission: Jump to 2 highest-HP enemies and spawn Blood Lotus shockwave!
				List<NPC> targets = FindTopHpEnemies(npc.Center, 2, 1800f, npc.whoAmI);
				foreach (var target in targets)
				{
					AddKnot(target, CrimsonKnotStacks, target.Center);
					CrimsonKnotTransferBeam.SpawnBeam(naughtPlayer.Player.GetSource_FromThis(), npc.Center, target.whoAmI);
				}

				// Spawn expanding Blood Lotus Shockwave scaled via NaughtPlayer True Melee damage
				var profile = WeepBalance.Active;
				float dmgMult = profile.BloodLotusBaseMult + profile.BloodLotusKnotMult * CrimsonKnotStacks;
				int shockDmg = naughtPlayer.GetNaughtDamage(dmgMult);
				int projIdx = Projectile.NewProjectile(
					naughtPlayer.Player.GetSource_FromThis(),
					npc.Center,
					Vector2.Zero,
					ModContent.ProjectileType<BloodLotusShockwave>(),
					shockDmg,
					5f,
					naughtPlayer.Player.whoAmI,
					ai0: CrimsonKnotStacks);

				if (projIdx >= 0 && projIdx < Main.maxProjectiles)
				{
					Main.projectile[projIdx].CritChance = naughtPlayer.GetNaughtCritChance();
				}
			}
			else
			{
				// Default: Find enemy on screen with highest health to inherit all Crimson Knots
				NPC target = NaughtPlayer.FindHighestHpEnemy(npc.Center, 1800f, ignoreMaxedKnots: true);
				if (target != null && target.whoAmI != npc.whoAmI)
				{
					basekitTargetWhoAmI = target.whoAmI;
					AddKnot(target, CrimsonKnotStacks, target.Center);
					CrimsonKnotTransferBeam.SpawnBeam(Main.LocalPlayer.GetSource_FromThis(), npc.Center, target.whoAmI);
				}
			}

			// Shadow Marionette: Propagate knots once per unique surviving tethered anchor (no duplicate beams/stacks)
			if (naughtPlayer != null && naughtPlayer.HasShadowMarionette && marionetteSurvivors.Count > 0)
			{
				foreach (int survivorId in marionetteSurvivors)
				{
					if (survivorId == basekitTargetWhoAmI)
						continue;

					NPC survivor = Main.npc[survivorId];
					if (survivor.active)
					{
						AddKnot(survivor, CrimsonKnotStacks, survivor.Center);
						CrimsonKnotTransferBeam.SpawnBeam(Main.LocalPlayer.GetSource_FromThis(), npc.Center, survivorId);
					}
				}
			}

			ClearCrimsonKnot();
		}

		public static List<NPC> FindTopHpEnemies(Vector2 searchOrigin, int count, float maxDistance = 1800f, int excludeWhoAmI = -1)
		{
			List<NPC> list = new List<NPC>();
			if (count <= 0)
				return list;

			int excludeAnchorWhoAmI = excludeWhoAmI;
			if (excludeWhoAmI >= 0 && excludeWhoAmI < Main.maxNPCs)
			{
				NPC excluded = Main.npc[excludeWhoAmI];
				if (excluded.realLife >= 0 && excluded.realLife < Main.maxNPCs)
				{
					excludeAnchorWhoAmI = excluded.realLife;
				}
			}

			var candidates = new List<(NPC Anchor, bool IsMaxed, int Priority, int Life)>();

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC other = Main.npc[i];
				if (other.active && other.whoAmI != excludeWhoAmI && other.life > 0 && !other.friendly && !other.isLikeATownNPC && other.lifeMax > 5)
				{
					bool isDummy = other.type == NPCID.TargetDummy;
					bool isBoss = IsBossTarget(other);

					if (!other.CanBeChasedBy() && !isDummy && !isBoss)
						continue;

					if (Vector2.Distance(searchOrigin, other.Center) >= maxDistance)
						continue;

					NPC anchor = ResolveKnotAnchor(other);
					if (anchor == null || !anchor.active || anchor.life <= 0 || anchor.whoAmI == excludeAnchorWhoAmI)
						continue;

					bool isMaxed = anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData) && knotData.CrimsonKnotStacks >= MaxKnotStacks;
					int priority = isBoss ? 2 : (!isDummy ? 1 : 0);
					int life = Math.Max(other.life, anchor.life);
					candidates.Add((anchor, isMaxed, priority, life));
				}
			}

			// Prioritize targets below 9 Crimson Knot stacks first, falling back to maxed targets only if needed
			candidates.Sort((a, b) =>
			{
				if (a.IsMaxed != b.IsMaxed) return a.IsMaxed.CompareTo(b.IsMaxed);
				if (a.Priority != b.Priority) return b.Priority.CompareTo(a.Priority);
				return b.Life.CompareTo(a.Life);
			});

			for (int i = 0; i < candidates.Count && list.Count < count; i++)
			{
				NPC anchor = candidates[i].Anchor;
				if (!list.Contains(anchor))
				{
					list.Add(anchor);
				}
			}

			return list;
		}

		public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
		{
			// Record pre-hit peak HP ratio on the resolved boss anchor so tick-0 one-shots still capture 100% HP
			NPC anchor = ResolveKnotAnchor(npc);
			if (anchor != null && anchor.active && anchor.lifeMax > 5 && IsBossTarget(anchor) && !IsVanillaEoWSegment(anchor))
			{
				if (anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var anchorData))
				{
					float preHitRatio = MathHelper.Clamp(anchor.life / (float)anchor.lifeMax, 0f, 1f);
					if (preHitRatio > anchorData.HighestObservedHpRatio)
					{
						anchorData.HighestObservedHpRatio = preHitRatio;
					}
				}
			}

			ClearPendingWitheredDrPeel();

			if (HasWitheredRiverDebuff(npc))
			{
				float witheredPeel = WeepBalance.Active.WitheredRiverDrPeel;
				WeepCompatibility.ApplyUnifiedDRPeel(npc, ref modifiers, witheredPeel);
				pendingWitheredDrPeelNpc = npc.whoAmI;
				pendingWitheredDrPeel = witheredPeel;
			}

			if (HasAbyssGripDebuff(npc) && WeepCompatibility.IsTrueMeleeDamageClass(modifiers.DamageType))
			{
				int debuffCount = CountUniqueDebuffs(npc);
				if (debuffCount > 0)
				{
					NaughtPlayer.GetAbyssGripScaling(out float perDebuff, out int maxDebuffs);
					int capped = Math.Min(debuffCount, maxDebuffs);
					modifiers.FinalDamage *= 1f + capped * perDebuff;
				}
			}
		}

		public static float GetCrucibleDotMultiplier(int overlapCount)
		{
			if (overlapCount <= 0)
				return 1f;

			var amps = WeepBalance.Active.CrucibleDotAmps;
			int idx = Math.Clamp(overlapCount, 1, amps.Length) - 1;
			return amps[idx];
		}

		public static void RefreshCrucibleOverlapsIfNeeded()
		{
			if (lastCrucibleOverlapFrame == Main.GameUpdateCount)
				return;

			lastCrucibleOverlapFrame = Main.GameUpdateCount;
			crucibleActiveClones.Clear();

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var kd))
				{
					kd.CrucibleOverlapCount = 0;
				}
			}

			int cloneProjType = ModContent.ProjectileType<NaughtClone>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.type != cloneProjType || p.ModProjectile is not NaughtClone clone)
					continue;

				Player owner = clone.Owner;
				if (owner == null || !owner.active || owner.dead)
					continue;

				var ownerNaught = owner.GetModPlayer<NaughtPlayer>();
				if (ownerNaught == null || !ownerNaught.HasShadowCrucible)
					continue;

				clone.CrucibleActiveOverlapMax = 0;
				clone.CrucibleHasBurningTarget = false;
				crucibleActiveClones.Add(clone);
			}

			if (crucibleActiveClones.Count == 0)
				return;

			float radiusSq = NaughtClone.CrucibleRadius * NaughtClone.CrucibleRadius;
			int crucibleDebuffType = ModContent.BuffType<ShadowCrucibleDebuff>();

			for (int c = 0; c < crucibleActiveClones.Count; c++)
			{
				NaughtClone clone = crucibleActiveClones[c];
				Vector2 clonePos = clone.Projectile.Center;
				int ownerWhoAmI = clone.Owner.whoAmI;
				int desiredBuffTicks = Math.Max(15, clone.Projectile.timeLeft + 15);
				bool canApplyDebuff = Main.netMode != NetmodeID.MultiplayerClient || ownerWhoAmI == Main.myPlayer;
				crucibleTouchedAnchors.Clear();

				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC npc = Main.npc[i];
					if (!npc.active || npc.friendly || npc.isLikeATownNPC || npc.lifeMax <= 5)
						continue;

					if (npc.dontTakeDamage && npc.type != NPCID.TargetDummy)
						continue;

					Vector2 closestPt = npc.Hitbox.ClosestPointInRect(clonePos);
					if (Vector2.DistanceSquared(clonePos, closestPt) >= radiusSq)
						continue;

					NPC anchor = ResolveKnotAnchor(npc);
					if (anchor == null || !anchor.active)
						continue;

					if (anchor.dontTakeDamage && anchor.type != NPCID.TargetDummy)
						continue;

					if (!crucibleTouchedAnchors.Add(anchor.whoAmI))
						continue;

					if (!anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var anchorData))
						continue;

					anchorData.CrucibleOverlapCount = Math.Min(NaughtClone.MaxCrucibleClones, anchorData.CrucibleOverlapCount + 1);
					anchorData.CrucibleOwnerWhoAmI = ownerWhoAmI;
					anchorData.CrucibleExposedThisCycle = true;

					if (canApplyDebuff)
					{
						int buffIdx = anchor.FindBuffIndex(crucibleDebuffType);
						if (buffIdx < 0)
						{
							anchor.AddBuff(crucibleDebuffType, desiredBuffTicks);
						}
						else if (anchor.buffTime[buffIdx] < clone.Projectile.timeLeft + 2)
						{
							// Keep debuff synced to the clone cycle without causing >60t duration jumps
							anchor.buffTime[buffIdx] = Math.Max(anchor.buffTime[buffIdx], desiredBuffTicks);
						}
					}
				}
			}
		}

		private void SyncCrucibleState(NPC npc, NaughtPlayer naughtPlayer)
		{
			NPC anchor = ResolveKnotAnchor(npc);
			if (anchor == null || anchor.whoAmI != npc.whoAmI)
				return;

			int crucibleDebuffType = ModContent.BuffType<ShadowCrucibleDebuff>();
			bool hasDebuff = npc.HasBuff(crucibleDebuffType);

			if (naughtPlayer != null && !naughtPlayer.HasShadowCrucible && CrucibleOwnerWhoAmI == naughtPlayer.Player.whoAmI)
			{
				ClearCrucibleOnAnchorAndSegments(npc, crucibleDebuffType);
				return;
			}

			if (!hasDebuff && CrucibleOverlapCount == 0)
			{
				CrucibleAccumulatedDot = 0f;
				CrucibleExposedThisCycle = false;
				CrucibleDisplayedChargeRatio = 0f;
				CrucibleOwnerWhoAmI = -1;
				return;
			}

			if (hasDebuff && naughtPlayer != null && naughtPlayer.HasShadowCrucible)
			{
				var profile = WeepBalance.Active;
				int maxCap = Math.Max(1, naughtPlayer.GetNaughtDamage(profile.CrucibleWeaponCapMult));
				int otherDebuffs = naughtPlayer.CountUniqueDebuffsAcrossTarget(npc, crucibleDebuffType);
				int floorBurst = Math.Min(otherDebuffs, 4) * naughtPlayer.GetNaughtDamage(profile.CrucibleFloorPerDebuff);
				float projected = Math.Max(CrucibleAccumulatedDot * profile.CruciblePopRatio, floorBurst);
				CrucibleDisplayedChargeRatio = MathHelper.Clamp(projected / maxCap, 0f, 1f);
			}
		}

		public static void ClearCrucibleOnAnchorAndSegments(NPC anchor, int crucibleDebuffType)
		{
			if (anchor == null)
				return;

			if (anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var anchorData))
			{
				anchorData.CrucibleAccumulatedDot = 0f;
				anchorData.CrucibleOverlapCount = 0;
				anchorData.CrucibleExposedThisCycle = false;
				anchorData.CrucibleDisplayedChargeRatio = 0f;
				anchorData.CrucibleOwnerWhoAmI = -1;
			}

			int anchorIdx = anchor.FindBuffIndex(crucibleDebuffType);
			if (anchorIdx >= 0)
			{
				anchor.DelBuff(anchorIdx);
			}

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC seg = Main.npc[i];
				if (seg.active && seg.realLife == anchor.whoAmI)
				{
					int segIdx = seg.FindBuffIndex(crucibleDebuffType);
					if (segIdx >= 0)
					{
						seg.DelBuff(segIdx);
					}
				}
			}
		}

		public static void DetonateShadowCrucibles(NaughtPlayer naughtPlayer)
		{
			if (naughtPlayer == null || !naughtPlayer.HasShadowCrucible)
				return;

			int ownerWhoAmI = naughtPlayer.Player.whoAmI;
			if (Main.netMode == NetmodeID.MultiplayerClient && ownerWhoAmI != Main.myPlayer)
				return;

			var profile = WeepBalance.Active;
			int crucibleDebuffType = ModContent.BuffType<ShadowCrucibleDebuff>();
			int maxCap = Math.Max(1, naughtPlayer.GetNaughtDamage(profile.CrucibleWeaponCapMult));
			int floorPerDebuff = naughtPlayer.GetNaughtDamage(profile.CrucibleFloorPerDebuff);
			bool playedAudio = false;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.isLikeATownNPC || npc.lifeMax <= 5)
					continue;

				NPC anchor = ResolveKnotAnchor(npc);
				if (anchor == null || anchor.whoAmI != npc.whoAmI)
					continue;

				if (!anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var anchorData))
					continue;

				bool hasDebuff = anchor.HasBuff(crucibleDebuffType);
				if (!hasDebuff && anchorData.CrucibleAccumulatedDot <= 0f && !anchorData.CrucibleExposedThisCycle)
					continue;

				if (anchorData.CrucibleOwnerWhoAmI >= 0 && anchorData.CrucibleOwnerWhoAmI != ownerWhoAmI)
					continue;

				float accumulatedDot = anchorData.CrucibleAccumulatedDot;
				bool wasExposed = hasDebuff || anchorData.CrucibleExposedThisCycle;

				int otherDebuffs = naughtPlayer.CountUniqueDebuffsAcrossTarget(anchor, crucibleDebuffType);
				ClearCrucibleOnAnchorAndSegments(anchor, crucibleDebuffType);

				if (!wasExposed && accumulatedDot <= 0f)
					continue;

				int dotBurst = (int)MathF.Round(accumulatedDot * profile.CruciblePopRatio);
				int floorBurst = Math.Min(otherDebuffs, 4) * floorPerDebuff;
				int rawBurst = Math.Max(dotBurst, floorBurst);
				if (rawBurst <= 0)
					continue;

				int finalBurst = Math.Min(rawBurst, maxCap);
				if (IsBossTarget(anchor) && anchor.type != NPCID.TargetDummy)
				{
					int bossHpCap = Math.Max(1, (int)(anchor.lifeMax * profile.CrucibleBossHpCapRatio));
					finalBurst = Math.Min(finalBurst, bossHpCap);
				}

				NPC hitTarget = FindBestDamageableSegment(anchor, Main.screenPosition);
				if (hitTarget == null)
					continue;

				int dealt = naughtPlayer.StrikeNPC(
					hitTarget,
					finalBurst,
					hitDirection: 0,
					crit: false,
					knockBack: 0f,
					damageType: WeepCompatibility.TrueMelee,
					damageVariation: false,
					applyHitModifiers: false,
					allowDamageShare: false,
					modifyHit: static (ref NPC.HitModifiers modifiers) =>
					{
						modifiers.DefenseEffectiveness *= 0f;
					});

				if (dealt > 0 && !Main.dedServ)
				{
					float intensity = MathHelper.Clamp(finalBurst / (float)maxCap, 0.25f, 1f);
					Vector2 fxPos = hitTarget.Center;

					Color explosionTint = Color.Lerp(new Color(195, 55, 255), new Color(255, 45, 95), intensity);
					PRTLoader.NewParticle<DimensionalExplosionPRT>(fxPos, Vector2.Zero, Color.White, 0.85f + 0.45f * intensity)
						.Configure(22, explosionTint);

					int sparkCount = 8 + (int)MathF.Round(8f * intensity);
					for (int s = 0; s < sparkCount; s++)
					{
						float ang = MathHelper.TwoPi * s / sparkCount + Main.rand.NextFloat(-0.2f, 0.2f);
						Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(3.5f, 7.5f);
						Color col = s % 2 == 0 ? new Color(205, 60, 255) : new Color(255, 45, 90);
						PRTLoader.NewParticle<VoidSparkPRT>(fxPos, vel, col, 0.85f + 0.25f * intensity);
					}

					int petalCount = 3 + (int)MathF.Round(3f * intensity);
					for (int p = 0; p < petalCount; p++)
					{
						Vector2 pVel = Main.rand.NextVector2Circular(4.2f, 4.2f) - Vector2.UnitY * 1.2f;
						PRTLoader.NewParticle<CrimsonPetalPRT>(fxPos, pVel, new Color(245, 30, 55, 225), 0.85f);
					}

					if (!playedAudio)
					{
						playedAudio = true;
						SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.22f, Volume = 0.72f }, fxPos);
					}
				}
			}
		}

		private static NPC FindBestDamageableSegment(NPC anchor, Vector2 screenPos)
		{
			NPC visible = FindBestVisibleSegment(anchor, screenPos);
			if (visible != null && visible.active && (!visible.dontTakeDamage || visible.type == NPCID.TargetDummy))
				return visible;

			if (anchor != null && anchor.active && (!anchor.dontTakeDamage || anchor.type == NPCID.TargetDummy))
				return anchor;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC seg = Main.npc[i];
				if (seg.active && seg.realLife == anchor.whoAmI && (!seg.dontTakeDamage || seg.type == NPCID.TargetDummy))
					return seg;
			}

			return null;
		}

		public override void UpdateLifeRegen(NPC npc, ref int damage)
		{
			if (WorldFreezeSystem.IsActive || npc == null || !npc.active || npc.friendly || npc.isLikeATownNPC)
				return;

			bool isDummy = npc.type == NPCID.TargetDummy;
			if (npc.dontTakeDamage && !isDummy)
				return;

			RefreshCrucibleOverlapsIfNeeded();

			NPC targetAnchor = ResolveKnotAnchor(npc);

			// Shadow Crucible: Amplify all active DoTs (Vanilla, Calamity, etc.) inside clone zones
			// and accumulate amplified DoT damage on the resolved anchor for cycle-expiry detonation.
			if (targetAnchor != null && targetAnchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var anchorData)
				&& anchorData.CrucibleOverlapCount > 0 && npc.lifeRegen < 0)
			{
				float dotMult = GetCrucibleDotMultiplier(anchorData.CrucibleOverlapCount);
				int baseNegRegen = -npc.lifeRegen;
				int amplifiedNegRegen = Math.Max(baseNegRegen, (int)MathF.Round(baseNegRegen * dotMult));
				npc.lifeRegen = -amplifiedNegRegen;

				if (damage > 0)
				{
					damage = Math.Max(1, (int)MathF.Round(damage * dotMult));
				}

				anchorData.CrucibleAccumulatedDot += amplifiedNegRegen / 120f;
			}
		}

		public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
		{
			ClearPendingWitheredDrPeel();

			if (projectile != null && projectile.type != ModContent.ProjectileType<MirageWeftThread>())
			{
				MirageWeftThread.FlushPendingDebuffCheck();
			}
		}

		public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
		{
			ClearPendingWitheredDrPeel();
			MirageWeftThread.FlushPendingDebuffCheck();
		}

		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (projectile != null && projectile.type == ModContent.ProjectileType<MirageWeftThread>())
			{
				MirageWeftThread.ProcessPendingDebuffCheck(projectile, npc);
			}

			if (damageDone <= 0 || projectile == null || !WeepCompatibility.IsNaughtDamageClass(projectile.DamageType))
				return;

			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;

			Player owner = Main.player[projectile.owner];
			var naughtPlayer = (owner != null && owner.active) ? owner.GetModPlayer<NaughtPlayer>() : null;
			if (naughtPlayer != null && naughtPlayer.HasShadowMarionette && !naughtPlayer.IsPropagatingMarionette)
			{
				naughtPlayer.RunMarionetteDamageShare(npc, damageDone, naughtPlayer.IsTrueUltimateStrike(projectile));
			}
		}

		public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
		{
			if (damageDone <= 0 || item == null || !WeepCompatibility.IsNaughtDamageClass(item.DamageType))
				return;

			var naughtPlayer = (player != null && player.active) ? player.GetModPlayer<NaughtPlayer>() : null;
			if (naughtPlayer != null && naughtPlayer.HasShadowMarionette && !naughtPlayer.IsPropagatingMarionette)
			{
				naughtPlayer.RunMarionetteDamageShare(npc, damageDone, isTrueUltimateStrike: false);
			}
		}

		public static void PropagateMarionetteDamage(NaughtPlayer naughtPlayer, NPC sourceNpc, int damageDone, bool isTrueUltimateStrike = false)
		{
			if (naughtPlayer == null || !naughtPlayer.HasShadowMarionette || sourceNpc == null || !sourceNpc.active || damageDone <= 0)
				return;

			NPC sourceAnchor = ResolveKnotAnchor(sourceNpc) ?? sourceNpc;
			int sourceAnchorId = sourceAnchor.whoAmI;
			int ownerWhoAmI = naughtPlayer.Player.whoAmI;
			int cloneProjType = ModContent.ProjectileType<NaughtClone>();

			marionetteShareCounts.Clear();

			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.owner != ownerWhoAmI || p.type != cloneProjType || p.ModProjectile is not NaughtClone clone)
					continue;

				if (!clone.MarionetteTetherNPCs.Contains(sourceAnchorId))
					continue;

				for (int t = 0; t < clone.MarionetteTetherNPCs.Count; t++)
				{
					int targetId = clone.MarionetteTetherNPCs[t];
					if (targetId == sourceAnchorId || targetId < 0 || targetId >= Main.maxNPCs)
						continue;

					NPC target = Main.npc[targetId];
					if (!target.active || target.friendly || target.isLikeATownNPC)
						continue;

					if (target.dontTakeDamage && target.type != NPCID.TargetDummy)
						continue;

					marionetteShareCounts.TryGetValue(targetId, out int count);
					marionetteShareCounts[targetId] = count + 1;
				}
			}

			var profile = WeepBalance.Active;
			foreach (var kvp in marionetteShareCounts)
			{
				NPC target = Main.npc[kvp.Key];
				if (!target.active)
					continue;

				int effectiveClones = Math.Min(kvp.Value, NaughtClone.MaxMarionetteClones);
				float shareRatio = Math.Min(profile.MarionetteShareCap, profile.MarionetteSharePerClone * effectiveClones);
				if (isTrueUltimateStrike)
				{
					shareRatio *= profile.MarionetteUltShareScale;
				}

				int sharedDmg = (int)MathF.Round(damageDone * shareRatio);
				if (sharedDmg <= 0)
					continue;

				naughtPlayer.StrikeNPC(
					target,
					sharedDmg,
					hitDirection: 0,
					crit: false,
					knockBack: 0f,
					damageType: WeepCompatibility.TrueMelee,
					damageVariation: false,
					applyHitModifiers: false,
					allowDamageShare: false);

				// Subtle puppet-string micro-yank on non-boss targets when shared damage lands
				if (!IsBossTarget(target) && target.type != NPCID.TargetDummy && target.knockBackResist > 0f)
				{
					target.velocity.X *= 0.65f;
				}

				if (!Main.dedServ)
				{
					for (int s = 0; s < 3; s++)
					{
						Vector2 vel = Main.rand.NextVector2Circular(3.2f, 3.2f);
						PRTLoader.NewParticle<VoidSparkPRT>(target.Center, vel, new Color(245, 30, 65), 0.72f);
					}
				}
			}
		}

		public static NPC ResolveKnotAnchor(NPC npc)
		{
			if (npc == null || !npc.active)
				return null;

			if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].active)
				return Main.npc[npc.realLife];

			return npc;
		}

		public static bool IsVanillaEoWSegment(NPC npc)
		{
			return npc != null
				&& npc.realLife < 0
				&& (npc.type == NPCID.EaterofWorldsHead || npc.type == NPCID.EaterofWorldsBody || npc.type == NPCID.EaterofWorldsTail);
		}

		private static bool HasAnchorOrSelfDebuff(NPC npc, int debuffType)
		{
			if (npc == null || !npc.active)
				return false;

			if (npc.HasBuff(debuffType))
				return true;

			NPC anchor = ResolveKnotAnchor(npc);
			return anchor != null && anchor.active && anchor.whoAmI != npc.whoAmI && anchor.HasBuff(debuffType);
		}

		public static bool HasWitheredRiverDebuff(NPC npc)
		{
			if (npc == null || !npc.active)
				return false;

			if (npc.HasBuff(ModContent.BuffType<WitheredRiverDebuff>()))
				return true;

			if (npc.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var selfData) && selfData.WitheredRiverFallbackTimer > 0)
				return true;

			NPC anchor = ResolveKnotAnchor(npc);
			if (anchor != null && anchor.active && anchor.whoAmI != npc.whoAmI)
			{
				if (anchor.HasBuff(ModContent.BuffType<WitheredRiverDebuff>()))
					return true;

				if (anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var anchorData) && anchorData.WitheredRiverFallbackTimer > 0)
					return true;
			}

			return false;
		}

		public static bool TryGetWitheredRiverVisualState(NPC npc, out float fade, out float flash, out float seed)
		{
			fade = 0f;
			flash = 0f;
			seed = 0f;
			if (npc == null || !npc.active)
				return false;

			if (npc.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var selfData) && selfData.WitheredRiverVisualFade > 0.02f)
			{
				fade = selfData.WitheredRiverVisualFade;
				flash = selfData.WitheredRiverApplyFlash;
				seed = (npc.whoAmI * 0.618f) % 1f;
				return true;
			}

			NPC anchor = ResolveKnotAnchor(npc);
			if (anchor != null && anchor.active && anchor.whoAmI != npc.whoAmI
				&& anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var anchorData) && anchorData.WitheredRiverVisualFade > 0.02f)
			{
				fade = anchorData.WitheredRiverVisualFade;
				flash = anchorData.WitheredRiverApplyFlash;
				seed = (anchor.whoAmI * 0.618f) % 1f;
				return true;
			}

			return false;
		}

		public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			RestoreLeakedWitherBatch(spriteBatch);

			if (npc == null || !npc.active || npc.IsABestiaryIconDummy)
				return true;

			if (!TryGetWitheredRiverVisualState(npc, out float witherFade, out float witherFlash, out float witherSeed))
				return true;

			Effect shader = ShaderLoader.WitheredRiverShader;
			if (shader == null || npc.type <= NPCID.None || npc.type >= TextureAssets.Npc.Length)
				return true;

			Texture2D tex = TextureAssets.Npc[npc.type].Value;
			if (tex == null || tex.Width <= 0 || tex.Height <= 0)
				return true;

			Rectangle frame = npc.frame;
			if (frame.Width <= 0 || frame.Height <= 0)
				frame = tex.Bounds;

			shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
			shader.Parameters["uTexelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
			shader.Parameters["uUvRect"]?.SetValue(new Vector4(
				(frame.X + 0.5f) / tex.Width,
				(frame.Y + 0.5f) / tex.Height,
				(frame.X + frame.Width - 0.5f) / tex.Width,
				(frame.Y + frame.Height - 0.5f) / tex.Height));
			shader.Parameters["uFade"]?.SetValue(witherFade);
			shader.Parameters["uFlash"]?.SetValue(witherFlash);
			shader.Parameters["uSeed"]?.SetValue(witherSeed);

			spriteBatch.End();
			spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
				Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
				null, Main.GameViewMatrix.TransformationMatrix);

			EffectTechnique bodyTech = shader.Techniques["TechWitherBody"];
			if (bodyTech != null)
			{
				shader.CurrentTechnique = bodyTech;
			}
			shader.CurrentTechnique.Passes[0].Apply();

			witherShaderActive = true;
			witherShaderNpcIndex = npc.whoAmI;
			return true;
		}

		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (witherShaderActive && (npc == null || witherShaderNpcIndex == npc.whoAmI))
			{
				EndWitherShaderBatch(spriteBatch);
			}
		}

		private static void RestoreLeakedWitherBatch(SpriteBatch spriteBatch)
		{
			if (witherShaderActive)
			{
				EndWitherShaderBatch(spriteBatch);
			}
		}

		private static void EndWitherShaderBatch(SpriteBatch spriteBatch)
		{
			if (!witherShaderActive)
				return;

			witherShaderActive = false;
			witherShaderNpcIndex = -1;

			spriteBatch.End();
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
				Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
				null, Main.GameViewMatrix.TransformationMatrix);
		}

		public static bool HasAbyssGripDebuff(NPC npc) => HasAnchorOrSelfDebuff(npc, ModContent.BuffType<AbyssGripDebuff>());

		private static void CollectUniqueDebuffsOnNpc(NPC npc, HashSet<int> output, int excludeBuffType = -1)
		{
			if (npc == null || !npc.active)
				return;

			int buffSlots = Math.Min(npc.buffType.Length, npc.buffTime.Length);
			for (int b = 0; b < buffSlots; b++)
			{
				int buff = npc.buffType[b];
				if (buff > 0 && buff != excludeBuffType && buff < Main.debuff.Length && Main.debuff[buff] && npc.buffTime[b] > 0)
				{
					output.Add(buff);
				}
			}
		}

		public static int CountUniqueDebuffs(NPC target, int excludeBuffType = -1)
		{
			if (target == null || !target.active)
				return 0;

			uniqueDebuffScratch.Clear();

			NPC anchor = ResolveKnotAnchor(target);
			int anchorWhoAmI = anchor != null && anchor.active ? anchor.whoAmI : target.whoAmI;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active)
					continue;

				if (n.whoAmI == target.whoAmI || n.whoAmI == anchorWhoAmI || (n.realLife >= 0 && n.realLife == anchorWhoAmI))
				{
					CollectUniqueDebuffsOnNpc(n, uniqueDebuffScratch, excludeBuffType);
				}
			}

			return uniqueDebuffScratch.Count;
		}

		public static bool IsBossTarget(NPC npc)
		{
			if (npc == null || !npc.active)
				return false;

			if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || IsVanillaEoWSegment(npc))
				return true;

			if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs)
			{
				NPC anchor = Main.npc[npc.realLife];
				if (anchor.active && (anchor.boss || NPCID.Sets.ShouldBeCountedAsBoss[anchor.type]))
					return true;
			}

			return false;
		}

		public int EvaluateRedOniMilestones(float currentRatio, bool canAward)
		{
			if (currentRatio > HighestObservedHpRatio)
			{
				HighestObservedHpRatio = currentRatio;
			}

			int newlyCrossed = 0;
			for (int i = 0; i < RedOniThresholds.Length; i++)
			{
				byte bit = (byte)(1 << i);
				float threshold = RedOniThresholds[i];
				if ((RedOniMilestonesCrossed & bit) == 0 && HighestObservedHpRatio > threshold && currentRatio <= threshold)
				{
					RedOniMilestonesCrossed |= bit;
					if (canAward)
					{
						newlyCrossed++;
					}
				}
			}

			return newlyCrossed;
		}

		public static void TriggerMilestoneFeedback(Player player, NPC bossAnchor, int dreamGain)
		{
			if (Main.netMode == NetmodeID.Server || player == null || !player.active)
				return;

			// Crisp two-layer chime + blade resonance at player
			SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.45f, Volume = 0.85f }, player.MountedCenter);
			SoundEngine.PlaySound(WeepSound.KatanaHitB with { Pitch = 0.25f, Volume = 0.65f }, player.MountedCenter);

			// Player surge particles & floating dream gain text
			PRTLoader.NewParticle<CrimsonBloomPRT>(player.MountedCenter, Vector2.Zero, Color.White, 0.55f)
				.Configure(26, new Color(255, 70, 95));
			for (int i = 0; i < 10; i++)
			{
				Vector2 vel = Main.rand.NextVector2Circular(4.5f, 4.5f);
				Color col = i % 2 == 0 ? new Color(255, 195, 70) : new Color(255, 45, 80);
				PRTLoader.NewParticle<VoidSparkPRT>(player.MountedCenter, vel, col, 0.9f);
			}

			CombatText.NewText(player.Hitbox, new Color(255, 195, 75), $"+{dreamGain} Slashed Dream", dramatic: true);

			// Boss-side visual burst on the nearest visible segment (handles worms cleanly)
			if (bossAnchor != null && bossAnchor.active)
			{
				NPC visualTarget = FindBestVisibleSegment(bossAnchor, Main.screenPosition) ?? bossAnchor;
				Vector2 bossPos = visualTarget.Center;

				PRTLoader.NewParticle<DimensionalExplosionPRT>(bossPos, Vector2.Zero, Color.White, 1.1f)
					.Configure(22, new Color(255, 65, 95));
				PRTLoader.NewParticle<CrimsonBloomPRT>(bossPos, Vector2.Zero, Color.White, 0.85f)
					.Configure(28, new Color(255, 45, 75));

				for (int i = 0; i < 8; i++)
				{
					Vector2 pVel = Main.rand.NextVector2Circular(5f, 5f);
					PRTLoader.NewParticle<CrimsonPetalPRT>(bossPos, pVel, new Color(245, 30, 55, 230), 0.9f);
				}
			}
		}

		private static NPC FindBestVisibleSegment(NPC anchor, Vector2 screenPos)
		{
			Rectangle screenRect = new Rectangle((int)screenPos.X, (int)screenPos.Y, Main.screenWidth, Main.screenHeight);
			Vector2 screenCenter = screenPos + new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
			NPC best = null;
			float bestDistSq = float.MaxValue;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && (n.whoAmI == anchor.whoAmI || n.realLife == anchor.whoAmI))
				{
					if (n.Hitbox.Intersects(screenRect))
					{
						float dSq = Vector2.DistanceSquared(n.Center, screenCenter);
						if (dSq < bestDistSq)
						{
							bestDistSq = dSq;
							best = n;
						}
					}
				}
			}

			return best ?? anchor;
		}

		private static void DrawMarionetteBindIndicator(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, int tetherCount)
		{
			int stacks = Math.Clamp(tetherCount, 1, NaughtClone.MaxMarionetteClones);
			float stackProgress = (stacks - 1f) / 3f;
			Vector2 center = npc.Center - screenPos;
			float radius = MathHelper.Clamp(Math.Max(npc.width, npc.height) * 0.42f, 16f, 34f);

			Color ringColor = Color.Lerp(new Color(220, 24, 54), new Color(255, 130, 160), stackProgress) * (0.45f + 0.35f * stackProgress);
			NihilityLedgerTheme.DrawCircle(spriteBatch, center, radius, ringColor, 1.3f + 0.4f * stackProgress, 20);

			float baseRot = Main.GlobalTimeWrappedHourly * (1.2f + 0.6f * stackProgress);
			Color pipGlow = new Color(238, 26, 56) * (0.55f + 0.35f * stackProgress);
			Color pipCore = new Color(255, 205, 220) * (0.75f + 0.25f * stackProgress);

			for (int i = 0; i < stacks; i++)
			{
				float ang = baseRot + MathHelper.TwoPi * i / stacks;
				Vector2 nodePos = center + ang.ToRotationVector2() * radius;
				NihilityLedgerTheme.DrawFilledCircle(spriteBatch, nodePos, 3.6f + stackProgress * 1.4f, pipGlow, 8);
				NihilityLedgerTheme.DrawFilledCircle(spriteBatch, nodePos, 1.8f, pipCore, 6);
			}
		}

		private static void DrawCrucibleBrandIndicator(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, CrimsonKnotGlobalNPC knotData)
		{
			int overlap = Math.Clamp(knotData.CrucibleOverlapCount, 0, NaughtClone.MaxCrucibleClones);
			float chargeRatio = MathHelper.Clamp(knotData.CrucibleDisplayedChargeRatio, 0f, 1f);
			bool isHeating = overlap > 0;
			float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * (isHeating ? 7.5f : 3.2f));

			Vector2 center = npc.Center - screenPos;
			float radius = MathHelper.Clamp(Math.Max(npc.width, npc.height) * 0.48f, 20f, 44f);

			Color coolViolet = NihilityLedgerTheme.NihilityViolet;
			Color hotCrimson = NihilityLedgerTheme.AcheronScarlet;
			Color ringColor = Color.Lerp(coolViolet, hotCrimson, Math.Max(chargeRatio, isHeating ? 0.25f + 0.2f * overlap : 0f));
			float visibility = isHeating ? (0.62f + 0.24f * pulse) : (0.40f + 0.14f * pulse);

			// Base furnace perimeter ring + 4 rotating diamond rune ticks
			NihilityLedgerTheme.DrawCircle(spriteBatch, center, radius, ringColor * (visibility * 0.45f), 3.6f, 28);
			NihilityLedgerTheme.DrawCircle(spriteBatch, center, radius, ringColor * visibility, 1.4f, 28);

			// Accumulated DoT charge arc around the seal
			if (chargeRatio > 0.02f)
			{
				int totalSegs = 24;
				int filledSegs = Math.Max(1, (int)MathF.Ceiling(totalSegs * chargeRatio));
				float startAngle = -MathHelper.PiOver2;
				float step = MathHelper.TwoPi / totalSegs;
				Color chargeCore = Color.Lerp(new Color(225, 110, 255), NihilityLedgerTheme.HotWhite, chargeRatio * 0.65f) * (0.72f + 0.25f * pulse);
				Color chargeGlow = hotCrimson * (0.35f + 0.35f * chargeRatio);

				Vector2 prev = center + startAngle.ToRotationVector2() * (radius + 4f);
				for (int s = 1; s <= filledSegs; s++)
				{
					float a = startAngle + s * step;
					Vector2 curr = center + a.ToRotationVector2() * (radius + 4f);
					NihilityLedgerTheme.DrawGlowLine(spriteBatch, prev, curr, chargeCore, chargeGlow, 1.8f, 4.2f);
					prev = curr;
				}
			}

			// Active overlap heat pips (1..3) orbiting while inside crucible aura(s)
			float rot = Main.GlobalTimeWrappedHourly * (isHeating ? 2.1f : 0.7f);
			if (overlap > 0)
			{
				Color pipGlow = hotCrimson * 0.78f;
				Color pipCore = NihilityLedgerTheme.HotWhite * 0.92f;
				for (int i = 0; i < overlap; i++)
				{
					float ang = rot + MathHelper.TwoPi * i / overlap;
					Vector2 pipPos = center + ang.ToRotationVector2() * (radius + 4f);
					NihilityLedgerTheme.DrawFilledCircle(spriteBatch, pipPos, 4.0f, pipGlow, 8);
					NihilityLedgerTheme.DrawFilledCircle(spriteBatch, pipPos, 2.0f, pipCore, 6);
				}
			}
			else
			{
				// Smoldering cardinal ticks while branded outside clone aura awaiting expiry detonation
				for (int i = 0; i < 4; i++)
				{
					float ang = rot + MathHelper.PiOver2 * i;
					Vector2 dir = ang.ToRotationVector2();
					NihilityLedgerTheme.DrawLine(spriteBatch, center + dir * (radius - 3f), center + dir * (radius + 3f), ringColor * visibility, 1.6f);
				}
			}
		}

		private static void DrawWitheredRiverIndicator(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, CrimsonKnotGlobalNPC knotData)
		{
			float fade = MathHelper.Clamp(knotData.WitheredRiverVisualFade, 0f, 1f);
			if (fade <= 0.02f)
				return;

			float flash = MathHelper.Clamp(knotData.WitheredRiverApplyFlash, 0f, 1f);
			float seed = (npc.whoAmI * 0.618f) % 1f;
			Vector2 center = npc.Center - screenPos;
			float radius = MathHelper.Clamp(Math.Max(npc.width, npc.height) * 0.56f, 24f, 62f);

			Effect witherShader = ShaderLoader.WitheredRiverShader;
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (witherShader != null && pixel != null)
			{
				EffectTechnique sealTech = witherShader.Techniques["TechWitherSeal"];
				if (sealTech != null)
				{
					var sbState = spriteBatch.Capture();
					spriteBatch.End();

					witherShader.CurrentTechnique = sealTech;
					witherShader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
					witherShader.Parameters["uFade"]?.SetValue(fade);
					witherShader.Parameters["uFlash"]?.SetValue(flash);
					witherShader.Parameters["uSeed"]?.SetValue(seed);

					spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, witherShader, sbState.Matrix);
					float quadSize = radius * 2.55f;
					Vector2 pixelScale = new Vector2(quadSize / pixel.Width, quadSize / pixel.Height);
					spriteBatch.Draw(pixel, center, null, Color.White, 0f, pixel.Size() * 0.5f, pixelScale, SpriteEffects.None, 0f);
					spriteBatch.End();
					spriteBatch.Restart(in sbState);
					return;
				}
			}

			// CPU fallback if shader failed to load
			float rot = Main.GlobalTimeWrappedHourly * 0.85f + seed * MathHelper.TwoPi;
			Color red = new Color(215, 32, 60) * (0.68f * fade);
			Color ash = new Color(210, 192, 198) * (0.55f * fade);
			for (int arc = 0; arc < 3; arc++)
			{
				float start = rot + MathHelper.TwoPi * arc / 3f;
				const int segs = 6;
				float arcSpan = 1.05f;
				Vector2 prev = center + start.ToRotationVector2() * radius;
				for (int s = 1; s <= segs; s++)
				{
					float ang = start + arcSpan * (s / (float)segs);
					Vector2 curr = center + ang.ToRotationVector2() * radius;
					NihilityLedgerTheme.DrawGlowLine(spriteBatch, prev, curr, ash, red, 1.4f, 3.6f);
					prev = curr;
				}
			}
		}

		public static void DrawAllKnots(SpriteBatch spriteBatch, Vector2 screenPos)
		{
			if (Main.gameMenu || Main.dedServ)
				return;

			RestoreLeakedWitherBatch(spriteBatch);

			Rectangle screenRect = new Rectangle((int)screenPos.X - 100, (int)screenPos.Y - 100, Main.screenWidth + 200, Main.screenHeight + 200);
			int crucibleDebuffType = ModContent.BuffType<ShadowCrucibleDebuff>();

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active)
					continue;

				// Only consider primary anchors to prevent duplicate knot/crucible draws for multi-segment bosses
				NPC anchor = ResolveKnotAnchor(npc);
				if (anchor == null || anchor.whoAmI != npc.whoAmI)
					continue;

				if (!anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData))
					continue;

				bool hasMarionetteVisual = knotData.MarionetteTetherCount > 0;
				bool hasCrucibleVisual = knotData.CrucibleOverlapCount > 0 || anchor.HasBuff(crucibleDebuffType);
				bool hasWitheredRiverVisual = knotData.WitheredRiverVisualFade > 0.02f || HasWitheredRiverDebuff(anchor);
				bool hasKnotVisual = knotData.CrimsonKnotStacks > 0;

				if (!hasMarionetteVisual && !hasCrucibleVisual && !hasWitheredRiverVisual && !hasKnotVisual)
					continue;

				NPC drawTarget = anchor;
				if (anchor.realLife >= 0 || anchor.aiStyle == NPCAIStyleID.Worm)
				{
					if (!anchor.Hitbox.Intersects(screenRect))
					{
						NPC bestSegment = FindBestVisibleSegment(anchor, screenPos);
						if (bestSegment != null)
							drawTarget = bestSegment;
					}
				}

				bool isBoss = drawTarget.boss || NPCID.Sets.ShouldBeCountedAsBoss[drawTarget.type] || drawTarget.realLife >= 0;
				if (isBoss || drawTarget.Hitbox.Intersects(screenRect))
				{
					if (hasWitheredRiverVisual)
					{
						DrawWitheredRiverIndicator(drawTarget, spriteBatch, screenPos, knotData);
					}

					if (hasMarionetteVisual)
					{
						DrawMarionetteBindIndicator(drawTarget, spriteBatch, screenPos, knotData.MarionetteTetherCount);
					}

					if (hasCrucibleVisual)
					{
						DrawCrucibleBrandIndicator(drawTarget, spriteBatch, screenPos, knotData);
					}

					if (hasKnotVisual)
					{
						DrawKnotDirect(drawTarget, spriteBatch, screenPos);
					}
				}
			}
		}

		public static void DrawKnotDirect(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
		{
			if (npc == null || !npc.active)
				return;

			NPC anchor = ResolveKnotAnchor(npc);
			if (anchor == null || !anchor.active)
				return;

			var knotData = anchor.GetGlobalNPC<CrimsonKnotGlobalNPC>();
			if (knotData == null || knotData.CrimsonKnotStacks <= 0)
				return;

			int stacks = knotData.CrimsonKnotStacks;

			// Purple flower for < 6 stacks, Red flower for 6+ stacks
			string texturePath = stacks >= 6
				? "WeepForTheDeparted/Content/Projectiles/CrimsonKnotRed"
				: "WeepForTheDeparted/Content/Projectiles/CrimsonKnotPurple";

			Texture2D knotTex = ModContent.Request<Texture2D>(texturePath, AssetRequestMode.ImmediateLoad).Value;
			if (knotTex == null)
				return;

			// Smooth floating oscillation above the enemy's head
			float floatOffset = (float)Math.Sin((Main.GameUpdateCount + npc.whoAmI * 17) * 0.06f) * 4f;

			// Position calculation:
			// For standard enemies: float 32px above top.
			// For large bosses: position above visual top or clamp so it stays comfortably on-screen near the boss.
			float halfHeight = Math.Min(npc.height * 0.5f, 90f);
			Vector2 knotWorldPos = npc.Center - new Vector2(0f, halfHeight + 36f + floatOffset);
			Vector2 drawPos = knotWorldPos - screenPos;

			// On-screen clamping for bosses so the knot indicator never flies off the top/bottom edge of the screen
			bool isBoss = npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || npc.realLife >= 0;
			if (isBoss)
			{
				drawPos.X = MathHelper.Clamp(drawPos.X, 40f, Main.screenWidth - 40f);
				drawPos.Y = MathHelper.Clamp(drawPos.Y, 48f, Main.screenHeight - 48f);
			}

			Vector2 origin = knotTex.GetOrig();

			// Scale-pop punch animation when stacks are gained using InnoVault easing
			float popScale = 1f;
			if (knotData.PopTimer > 0)
			{
				float popProgress = knotData.PopTimer / (float)MaxPopTimer;
				popScale += (float)Math.Sin(popProgress * MathHelper.Pi) * 0.35f;
			}
			float scale = (62f / knotTex.Width) * popScale;
			if (isBoss)
			{
				scale *= 1.2f;
			}

			// Switch to LinearClamp sampler & high-clarity shader to completely eliminate in-game pixelation
			var sbState = spriteBatch.Capture();
			spriteBatch.End();

			Effect knotShader = ShaderLoader.CrimsonKnotShader;
			if (knotShader != null)
			{
				knotShader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
				knotShader.Parameters["uTexSize"]?.SetValue(new Vector2(knotTex.Width, knotTex.Height));
				knotShader.Parameters["uEdgeSharpness"]?.SetValue(0.40f);
				knotShader.Parameters["uSheenIntensity"]?.SetValue(0.20f);

				spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, knotShader, sbState.Matrix);
			}
			else
			{
				spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, sbState.Matrix);
			}

			// Main Crimson Knot flower (drawn smooth, crisp, and high-definition)
			spriteBatch.Draw(knotTex, drawPos, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);

			spriteBatch.End();
			spriteBatch.Restart(in sbState);

			// Stack number placed non-overlapping at the bottom-right of the flower
			string stackStr = stacks.ToString();
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			if (font != null)
			{
				Vector2 textSize = font.MeasureString(stackStr);
				Vector2 numOffset = new Vector2(25f, 22f);
				Vector2 textDrawPos = drawPos + numOffset - textSize * 0.5f;

				// Gradual gradient from purple (1 stack) to crimson red (9 stacks)
				float progress = MathHelper.Clamp((stacks - 1f) / 8f, 0f, 1f);
				Color numberColor = Color.Lerp(new Color(200, 115, 255), new Color(255, 30, 45), progress);

				// Crisp black 4-way outline for instant readability against any backdrop
				Utils.DrawBorderStringFourWay(spriteBatch, font, stackStr, textDrawPos.X, textDrawPos.Y, numberColor, Color.Black, Vector2.Zero, 1.0f);
			}
		}
	}
}
