using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using WeepForTheDeparted.Common.GlobalNPCs;
using WeepForTheDeparted.Common.Systems;
using WeepForTheDeparted.Common.UI.NihilityLedger;
using WeepForTheDeparted.Common.Visuals;
using WeepForTheDeparted.Content.Items.Weapons;
using WeepForTheDeparted.Content.Projectiles;

namespace WeepForTheDeparted.Common.Players
{
	public class NaughtPlayer : ModPlayer
	{
		public int DashCooldown = 0;

		// Slashed Dream Resource (0 to 9, max 9)
		public int SlashedDream = 0;
		public const int MaxSlashedDream = 9;

		// Overflow Dream points (up to 3, unlocked by Trace 1 Red Oni)
		public int OverflowDream = 0;
		public const int MaxOverflowDream = 3;

		// Nihility Ledger: 3 Charm Sockets (0: Soul, 1: Tide, 2: Ruin)
		public string[] EquippedCharms = new string[3] { "RedOni", "AbyssGrip", "ThunderCore" };

		// Nihility Ledger: 6 Eidolon Remnants (E1 to E6)
		public bool[] UnlockedEidolons = new bool[6] { false, false, false, false, false, false };

		// Charm helper accessors
		public bool HasCharm(string key)
		{
			if (EquippedCharms == null) return false;
			for (int i = 0; i < EquippedCharms.Length; i++)
			{
				if (EquippedCharms[i] == key) return true;
			}
			return false;
		}

		public bool HasSoulCharm(string key) => EquippedCharms != null && EquippedCharms.Length > 0 && EquippedCharms[0] == key;
		public bool HasTideCharm(string key) => EquippedCharms != null && EquippedCharms.Length > 1 && EquippedCharms[1] == key;
		public bool HasRuinCharm(string key) => EquippedCharms != null && EquippedCharms.Length > 2 && EquippedCharms[2] == key;

		// Slot 0 (Soul) Charms
		public bool HasRedOniTrace => HasSoulCharm("RedOni");
		public bool HasNamelessDecoy => HasSoulCharm("NamelessDecoy");
		public bool HasFleetingStep => HasSoulCharm("FleetingStep");
		public bool HasMirageWeft => HasSoulCharm("MirageWeft");
		public bool HasRainleafPhasing => HasSoulCharm("Rainleaf");
		public bool HasStillWater => HasSoulCharm("StillWater");

		// Slot 1 (Tide) Charms
		public bool HasAbyssGrip => HasTideCharm("AbyssGrip");
		public bool HasBloodLotus => HasTideCharm("BloodLotus");
		public bool HasShadowMarionette => HasTideCharm("ShadowMarionette");
		public bool HasWitheredRiver => HasTideCharm("WitheredRiver");
		public bool HasShadowCrucible => HasTideCharm("ShadowCrucible");
		public bool HasDepartedSiphon => HasTideCharm("DepartedSiphon");

		// Slot 2 (Ruin) Charms
		public bool HasThunderCore => HasRuinCharm("ThunderCore");
		public bool HasKarmaSever => HasRuinCharm("KarmaSever");
		public bool HasVoidHorizon => HasRuinCharm("VoidHorizon");
		public bool HasBladesongRain => HasRuinCharm("BladesongRain");
		public bool HasSynchronizedExecution => HasRuinCharm("SynchronizedExecution");
		public bool HasEmancipatorWill => HasRuinCharm("EmancipatorWill");

		// Charm Dynamic Buff Timers & States
		public int VoidPhasingTimer = 0;
		public int VoidPhasingCooldown = 0;
		public int StillWaterFocusTimer = 0;
		public bool IsStillWaterActive => StillWaterFocusTimer >= 180;
		public int ThunderCoreBuffTimer = 0;
		public bool IsThunderCoreActive => ThunderCoreBuffTimer > 0;
		public int EmancipatorCritBuffTimer = 0;
		public bool IsEmancipatorCritActive => EmancipatorCritBuffTimer > 0;
		public int SiphonMoveSpeedTimer = 0;
		public bool IsSiphonMoveSpeedActive => SiphonMoveSpeedTimer > 0;
		public int MirageWeftResetCooldown = 0;
		public int FleetingStepDreamCooldown = 0;

		public bool IsNaughtEquipped => Player.HeldItem?.type == ModContent.ItemType<Naught>();

		// Cooldown tracking per debuff type: debuffId -> remaining cooldown ticks
		private readonly Dictionary<int, int> debuffCooldowns = new Dictionary<int, int>();
		public const int DebuffCooldownTicks = 540; // 9 seconds per specific debuff type

		public int LastCloneCycleFrame = -1;

		public override void OnEnterWorld()
		{
			// Trace 1 (Red Oni): start battle / world with 5 Slashed Dream
			if (HasRedOniTrace && SlashedDream < 5)
			{
				SlashedDream = 5;
			}
		}

		public override void OnRespawn()
		{
			if (HasRedOniTrace && SlashedDream < 5)
			{
				SlashedDream = 5;
			}
		}

		public override void SetControls()
		{
			// Completely freeze the player in place during the Ultimate!
			if (Player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] > 0)
			{
				Player.controlLeft = false;
				Player.controlRight = false;
				Player.controlUp = false;
				Player.controlDown = false;
				Player.controlJump = false;
				Player.controlHook = false;
				Player.controlMount = false;
				Player.controlUseItem = false;
				Player.controlUseTile = false;
			}
		}

		public bool IsInUltimate => Player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] > 0;

		public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable)
		{
			// True invincibility during the ultimate or during Void Phasing without i-frame transparency blinking
			if (IsInUltimate || VoidPhasingTimer > 0)
				return true;
			return base.ImmuneTo(damageSource, cooldownCounter, dodgeable);
		}

		public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
		{
			if (IsInUltimate || VoidPhasingTimer > 0)
				return false;
			return base.CanBeHitByNPC(npc, ref cooldownSlot);
		}

		public override bool CanBeHitByProjectile(Projectile proj)
		{
			if (IsInUltimate || VoidPhasingTimer > 0)
				return false;
			return base.CanBeHitByProjectile(proj);
		}

		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			if (IsInUltimate || VoidPhasingTimer > 0)
			{
				modifiers.FinalDamage *= 0f;
				modifiers.Cancel();
				return;
			}

			// Rainleaf Phasing: Save from heavy damage (>35% max HP)
			if (HasRainleafPhasing && VoidPhasingCooldown <= 0)
			{
				modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
				{
					if (info.Damage >= Player.statLifeMax2 * 0.35f || info.Damage >= Player.statLife)
					{
						VoidPhasingTimer = 72; // 1.2s invulnerability
						VoidPhasingCooldown = 5400; // 90s cooldown
						AddSlashedDream(2);
						info.Damage = 0;
						SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.95f }, Player.Center);
					}
				};
			}

			// Still Water Basin resets upon taking damage
			StillWaterFocusTimer = 0;

			base.ModifyHurt(ref modifiers);
		}

		public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
		{
			if (IsInUltimate || VoidPhasingTimer > 0)
				return false;

			// Rainleaf Phasing: Prevent death
			if (HasRainleafPhasing && VoidPhasingCooldown <= 0)
			{
				Player.statLife = Math.Max(1, (int)(Player.statLifeMax2 * 0.25f));
				VoidPhasingTimer = 72; // 1.2s
				VoidPhasingCooldown = 5400; // 90s
				AddSlashedDream(2);
				SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.95f }, Player.Center);
				return false;
			}

			return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genDust, ref damageSource);
		}

		public override void PreUpdateMovement()
		{
			// Eliminate all velocity and gravity during the Ultimate
			if (IsInUltimate)
			{
				Player.velocity = Vector2.Zero;
				Player.gravity = 0f;
			}
		}

		public override void PostUpdate()
		{
			if (DashCooldown > 0)
			{
				DashCooldown--;
			}

			if (VoidPhasingTimer > 0) VoidPhasingTimer--;
			if (VoidPhasingCooldown > 0) VoidPhasingCooldown--;
			if (ThunderCoreBuffTimer > 0) ThunderCoreBuffTimer--;
			if (EmancipatorCritBuffTimer > 0) EmancipatorCritBuffTimer--;
			if (SiphonMoveSpeedTimer > 0) SiphonMoveSpeedTimer--;
			if (MirageWeftResetCooldown > 0) MirageWeftResetCooldown--;
			if (FleetingStepDreamCooldown > 0) FleetingStepDreamCooldown--;

			// Still Water Basin focus increment when near live enemies
			if (HasStillWater && !Player.dead && Player.active)
			{
				bool enemyNearby = false;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC npc = Main.npc[i];
					if (npc.active && !npc.friendly && !npc.isLikeATownNPC && Vector2.Distance(Player.Center, npc.Center) < 280f)
					{
						enemyNearby = true;
						break;
					}
				}

				if (enemyNearby)
				{
					if (StillWaterFocusTimer < 180)
						StillWaterFocusTimer++;
				}
				else
				{
					if (StillWaterFocusTimer > 0)
						StillWaterFocusTimer--;
				}
			}
			else
			{
				StillWaterFocusTimer = 0;
			}

			// Decrement per-debuff cooldown timers
			if (debuffCooldowns.Count > 0)
			{
				var keys = new List<int>(debuffCooldowns.Keys);
				foreach (int key in keys)
				{
					if (debuffCooldowns[key] > 0)
					{
						debuffCooldowns[key]--;
					}
					else
					{
						debuffCooldowns.Remove(key);
					}
				}
			}
		}

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (WeepKeybinds.DomainKeybind != null && WeepKeybinds.DomainKeybind.JustPressed)
			{
				ToggleDomain();
			}

			if (WeepKeybinds.UltimateKeybind != null && WeepKeybinds.UltimateKeybind.JustPressed)
			{
				// If ultimate is not running, attempt to start it
				if (Player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] == 0)
				{
					TryActivateUltimate();
				}
			}

			if (WeepKeybinds.LedgerKeybind != null && WeepKeybinds.LedgerKeybind.JustPressed)
			{
				ToggleLedgerUI();
			}
		}

		public void ToggleLedgerUI()
		{
			if (NihilityLedgerUI.Instance == null)
				return;

			if (NihilityLedgerUI.Instance.IsOpen)
				NihilityLedgerUI.Instance.Close();
			else
				NihilityLedgerUI.Instance.Open();
		}

		public override void SaveData(TagCompound tag)
		{
			if (EquippedCharms != null)
			{
				tag["EquippedCharms"] = new List<string>(EquippedCharms);
			}

			if (UnlockedEidolons != null)
			{
				tag["UnlockedEidolons"] = new List<bool>(UnlockedEidolons);
			}
		}

		public override void LoadData(TagCompound tag)
		{
			if (tag.ContainsKey("EquippedCharms"))
			{
				var list = tag.Get<List<string>>("EquippedCharms");
				for (int i = 0; i < 3 && i < list.Count; i++)
				{
					EquippedCharms[i] = list[i];
				}
			}

			if (tag.ContainsKey("UnlockedEidolons"))
			{
				var list = tag.Get<List<bool>>("UnlockedEidolons");
				for (int i = 0; i < 6 && i < list.Count; i++)
				{
					UnlockedEidolons[i] = list[i];
				}
			}
		}

		public void ToggleDomain()
		{
			if (Player.whoAmI != Main.myPlayer || !Player.active || Player.dead || Player.ghost)
				return;

			if (!IsNaughtEquipped)
				return;

			if (NihilityDomainSystem.IsDomainActive)
			{
				// Do not close domain during active ultimate execution
				if (Player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] > 0)
					return;

				// If already in closing reverse wave, re-triggering re-opens the domain
				if (NihilityDomainSystem.DomainClosingActive)
				{
					NihilityDomainSystem.ActivateDomain(Player.Center);
					SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 0.95f }, Player.Center);
					SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f, Volume = 0.9f }, Player.Center);
				}
				else
				{
					// Start reverse wave that collapses back into the player and restores the normal world!
					NihilityDomainSystem.StartDomainClosing(Player.Center);
				}
			}
			else
			{
				NihilityDomainSystem.ActivateDomain(Player.Center);
				SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 0.95f }, Player.Center);
				SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f, Volume = 0.9f }, Player.Center);
			}
		}

		public bool TryActivateUltimate()
		{
			if (Player.whoAmI != Main.myPlayer)
				return false;

			if (!Player.active || Player.dead || Player.ghost || Player.CCed)
				return false;

			if (!IsNaughtEquipped)
				return false;

			// Ultimate is strictly restricted to being cast inside the Nihility Domain!
			if (!NihilityDomainSystem.IsDomainActive)
			{
				return false;
			}

			if (SlashedDream < MaxSlashedDream)
			{
				return false;
			}

			if (Player.ownedProjectileCounts[ModContent.ProjectileType<SlashedDreamUltimate>()] > 0)
				return false;

			// Spawn Ultimate execution projectile
			int damage = Player.HeldItem?.damage ?? 100;
			float knockback = 8f;
			Projectile.NewProjectile(Player.GetSource_FromThis(), Player.MountedCenter, Vector2.Zero, ModContent.ProjectileType<SlashedDreamUltimate>(), damage, knockback, Player.whoAmI);
			return true;
		}

		public void AddSlashedDream(int amount = 1)
		{
			if (amount <= 0)
				return;

			int prevDream = SlashedDream;
			int maxOverflow = HasRedOniTrace ? MaxOverflowDream : 0;

			for (int i = 0; i < amount; i++)
			{
				if (SlashedDream < MaxSlashedDream)
				{
					SlashedDream++;
				}
				else if (OverflowDream < maxOverflow)
				{
					OverflowDream++;
				}
			}

			// Chime sound when reaching full 9 points
			if (prevDream < MaxSlashedDream && SlashedDream >= MaxSlashedDream)
			{
				SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.85f, Pitch = 0.15f }, Player.MountedCenter);
			}
		}

		public bool CanDebuffTrigger(int debuffType)
		{
			if (debuffCooldowns.TryGetValue(debuffType, out int cd) && cd > 0)
				return false;

			return true;
		}

		public void TryTriggerDebuff(int debuffType, NPC target)
		{
			if (!CanDebuffTrigger(debuffType))
				return;

			// Put this specific debuff on its own cooldown
			debuffCooldowns[debuffType] = DebuffCooldownTicks;

			// Gain 1 Slashed Dream
			AddSlashedDream(1);

			// Apply 1 Crimson Knot to target's resolved anchor
			if (target != null && target.active)
			{
				CrimsonKnotGlobalNPC.AddKnot(target, 1, target.Center);
			}
		}

		public void OnCloneCycleComplete(Vector2 clonePosition)
		{
			// Prevent multiple clones expiring in the same frame from rewarding duplicate points
			if (Main.GameUpdateCount == LastCloneCycleFrame)
				return;

			LastCloneCycleFrame = (int)Main.GameUpdateCount;

			// 1. Gain 1 Slashed Dream
			AddSlashedDream(1);

			// 2. Apply 1 Crimson Knot to highest-health enemy on screen (ignoring enemies that are already at max stacks)
			NPC target = FindHighestHpEnemy(clonePosition, 1800f, ignoreMaxedKnots: true);
			if (target != null)
			{
				CrimsonKnotGlobalNPC.AddKnot(target, 1, target.Center);

				// Spawn tether beam from clone to enemy
				CrimsonKnotTransferBeam.SpawnBeam(Player.GetSource_FromThis(), clonePosition, target.whoAmI);
			}

			// Ethereal clone expiration audio chime
			SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.65f, Pitch = 0.35f }, clonePosition);
		}

		public static NPC FindHighestHpEnemy(Vector2 searchOrigin, float maxDistance = 1800f, bool ignoreMaxedKnots = false)
		{
			NPC target = null;
			int highestPriority = -1;
			int highestHp = 0;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC other = Main.npc[i];
				if (other.active && !other.friendly && !other.isLikeATownNPC && other.lifeMax > 5)
				{
					bool isDummy = other.type == NPCID.TargetDummy;
					bool isBoss = other.boss || NPCID.Sets.ShouldBeCountedAsBoss[other.type] || (other.realLife >= 0 && Main.npc[other.realLife].boss);

					if (!other.CanBeChasedBy() && !isDummy && !isBoss)
						continue;

					if (ignoreMaxedKnots)
					{
						NPC knotAnchor = CrimsonKnotGlobalNPC.ResolveKnotAnchor(other);
						var knotNPC = knotAnchor.GetGlobalNPC<CrimsonKnotGlobalNPC>();
						if (knotNPC != null && knotNPC.CrimsonKnotStacks >= CrimsonKnotGlobalNPC.MaxKnotStacks)
							continue;
					}

					float dist = Vector2.Distance(searchOrigin, other.Center);
					if (dist < maxDistance)
					{
						int priority = isBoss ? 2 : (!isDummy ? 1 : 0);
						if (priority > highestPriority || (priority == highestPriority && other.life > highestHp))
						{
							highestPriority = priority;
							highestHp = other.life;
							target = other;
						}
					}
				}
			}

			return target != null ? CrimsonKnotGlobalNPC.ResolveKnotAnchor(target) : null;
		}

		public override void PostUpdateRunSpeeds()
		{
			if (VoidPhasingTimer > 0)
			{
				Player.runAcceleration *= 1.5f;
				Player.maxRunSpeed *= 1.5f;
			}
			else if (IsSiphonMoveSpeedActive)
			{
				Player.runAcceleration *= 1.15f;
				Player.maxRunSpeed *= 1.15f;
			}
		}

		public override void ModifyWeaponDamage(Item item, ref StatModifier damage)
		{
			if (IsNaughtEquipped && item.type == ModContent.ItemType<Naught>())
			{
				if (IsThunderCoreActive)
				{
					damage += 0.15f; // +15% True Melee damage for 15s post-ult
				}
			}
		}

		public override void ModifyWeaponCrit(Item item, ref float crit)
		{
			if (IsNaughtEquipped && item.type == ModContent.ItemType<Naught>())
			{
				if (IsStillWaterActive)
				{
					crit += 16f; // +16% True Melee Crit from Still Water
				}
				if (IsEmancipatorCritActive)
				{
					crit += 20f; // +20% True Melee Crit post-ult
				}
			}
		}

		public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
		{
			ApplyAbyssGrip(target, ref modifiers);
		}

		public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (proj.DamageType == DamageClass.Melee || proj.DamageType == WeepCompatibility.TrueMelee)
			{
				ApplyAbyssGrip(target, ref modifiers);
			}
		}

		private void ApplyAbyssGrip(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (HasAbyssGrip && target != null && target.active)
			{
				int debuffCount = 0;
				for (int b = 0; b < target.buffType.Length; b++)
				{
					int buff = target.buffType[b];
					if (buff > 0 && Main.debuff[buff])
					{
						debuffCount++;
					}
				}

				if (debuffCount > 0)
				{
					int capped = Math.Min(debuffCount, 4);
					modifiers.SourceDamage += capped * 0.06f; // +6% per debuff up to +24%
				}
			}
		}

		public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
		{
			// When holding Naught and not swinging:
			// Position front hand to rest naturally on the hilt at the hip (samurai ready stance)
			if (Player.active && !Player.dead && !Player.ghost && !drawInfo.headOnlyRender
				&& Player.HeldItem?.type == ModContent.ItemType<Naught>()
				&& Player.itemAnimation == 0
				&& Player.ownedProjectileCounts[ModContent.ProjectileType<NaughtHeldSwing>()] == 0
				&& Player.ownedProjectileCounts[ModContent.ProjectileType<NaughtDash>()] == 0)
			{
				float armAngle = -Player.direction * 0.28f * Player.gravDir;
				Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, armAngle);
			}
		}
	}
}
