using System;
using System.Collections.Generic;
using InnoVault;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.Visuals;
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

		public int LastDrawnFrame = -1;
		public int LastUpdatedFrame = -1;

		private readonly HashSet<int> activeDebuffs = new HashSet<int>();
		private readonly Dictionary<int, int> lastBuffTimes = new Dictionary<int, int>();

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

			// Ambient visual: subtle crimson-purple embers hovering around marked enemies
			if (CrimsonKnotStacks > 0 && Main.rand.NextBool(6))
			{
				Vector2 dustPos = npc.position + new Vector2(Main.rand.NextFloat(npc.width), Main.rand.NextFloat(npc.height));
				Dust d = Dust.NewDustPerfect(dustPos, DustID.Shadowflame, -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.0f), 120, default, 0.9f);
				d.noGravity = true;
			}

			if (!hasNaught || naughtPlayer == null || localPlayer == null)
				return;

			// Scan all active buff slots on the NPC
			HashSet<int> currentDebuffs = new HashSet<int>();
			for (int i = 0; i < NPC.maxBuffs; i++)
			{
				int type = npc.buffType[i];
				int time = npc.buffTime[i];

				if (type > 0 && type < Main.debuff.Length && Main.debuff[type])
				{
					currentDebuffs.Add(type);

					bool isNew = !activeDebuffs.Contains(type);
					bool isRefreshed = lastBuffTimes.TryGetValue(type, out int prevTime) && time > prevTime + 60;

					if (isNew || isRefreshed)
					{
						// Scourge of the Withered River: debuffs on bosses linger 40% longer
						if (isNew && naughtPlayer.HasWitheredRiver)
						{
							bool isBoss = npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || (npc.realLife >= 0 && Main.npc[npc.realLife].boss);
							if (isBoss)
							{
								npc.buffTime[i] = (int)(time * 1.40f);
							}
						}

						// Only trigger if enemy is within combat range of player (1800px)
						if (localPlayer.MountedCenter.Distance(npc.Center) < 1800f)
						{
							naughtPlayer.TryTriggerDebuff(type, npc);
						}
					}

					lastBuffTimes[type] = time;
				}
			}

			activeDebuffs.Clear();
			foreach (int d in currentDebuffs)
			{
				activeDebuffs.Add(d);
			}
		}

		public override void OnKill(NPC npc)
		{
			if (npc == null || CrimsonKnotStacks <= 0)
				return;

			var naughtPlayer = Main.LocalPlayer?.GetModPlayer<NaughtPlayer>();
			bool hasBloodLotus = naughtPlayer != null && naughtPlayer.HasBloodLotus;

			if (hasBloodLotus)
			{
				// Blood Lotus Transmission: Jump to 2 highest-HP enemies and spawn Blood Lotus shockwave!
				List<NPC> targets = FindTopHpEnemies(npc.Center, 2, 1800f, npc.whoAmI);
				foreach (var target in targets)
				{
					AddKnot(target, CrimsonKnotStacks, target.Center);
					CrimsonKnotTransferBeam.SpawnBeam(Main.LocalPlayer.GetSource_FromThis(), npc.Center, target.whoAmI);
				}

				// Spawn expanding Blood Lotus Shockwave
				int weaponDmg = Main.LocalPlayer.HeldItem?.damage ?? 100;
				int shockDmg = weaponDmg + CrimsonKnotStacks * 25;
				Projectile.NewProjectile(Main.LocalPlayer.GetSource_FromThis(), npc.Center, Vector2.Zero, ModContent.ProjectileType<BloodLotusShockwave>(), shockDmg, 5f, Main.myPlayer);
			}
			else
			{
				// Default: Find enemy on screen with highest health to inherit all Crimson Knots
				NPC target = NaughtPlayer.FindHighestHpEnemy(npc.Center, 1800f, ignoreMaxedKnots: true);
				if (target != null && target.whoAmI != npc.whoAmI)
				{
					AddKnot(target, CrimsonKnotStacks, target.Center);
					CrimsonKnotTransferBeam.SpawnBeam(Main.LocalPlayer.GetSource_FromThis(), npc.Center, target.whoAmI);
				}
			}

			// Shadow Marionette: Check if any active clone tethered this enemy
			// If tethered, propagate knots to all other tethered enemies!
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.type == ModContent.ProjectileType<NaughtClone>() && p.ModProjectile is NaughtClone clone)
				{
					if (clone.MarionetteTetherNPCs.Contains(npc.whoAmI))
					{
						clone.MarionetteTetherNPCs.Remove(npc.whoAmI);
						foreach (int tetherId in clone.MarionetteTetherNPCs)
						{
							if (tetherId >= 0 && tetherId < Main.maxNPCs && Main.npc[tetherId].active)
							{
								AddKnot(Main.npc[tetherId], CrimsonKnotStacks, Main.npc[tetherId].Center);
								CrimsonKnotTransferBeam.SpawnBeam(Main.LocalPlayer.GetSource_FromThis(), npc.Center, tetherId);
							}
						}
					}
				}
			}

			ClearCrimsonKnot();
		}

		public static List<NPC> FindTopHpEnemies(Vector2 searchOrigin, int count, float maxDistance = 1800f, int excludeWhoAmI = -1)
		{
			List<NPC> list = new List<NPC>();
			var validNPCs = new List<NPC>();

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC other = Main.npc[i];
				if (other.active && other.whoAmI != excludeWhoAmI && !other.friendly && !other.isLikeATownNPC && other.lifeMax > 5)
				{
					bool isDummy = other.type == NPCID.TargetDummy;
					bool isBoss = other.boss || NPCID.Sets.ShouldBeCountedAsBoss[other.type] || (other.realLife >= 0 && Main.npc[other.realLife].boss);

					if (!other.CanBeChasedBy() && !isDummy && !isBoss)
						continue;

					if (Vector2.Distance(searchOrigin, other.Center) < maxDistance)
					{
						validNPCs.Add(other);
					}
				}
			}

			validNPCs.Sort((a, b) => {
				bool aBoss = a.boss || NPCID.Sets.ShouldBeCountedAsBoss[a.type];
				bool bBoss = b.boss || NPCID.Sets.ShouldBeCountedAsBoss[b.type];
				if (aBoss != bBoss) return bBoss.CompareTo(aBoss);
				return b.life.CompareTo(a.life);
			});

			for (int i = 0; i < count && i < validNPCs.Count; i++)
			{
				NPC anchor = ResolveKnotAnchor(validNPCs[i]);
				if (anchor != null && !list.Contains(anchor))
				{
					list.Add(anchor);
				}
			}

			return list;
		}

		public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
		{
			var np = Main.LocalPlayer?.GetModPlayer<NaughtPlayer>();
			if (np != null && np.HasWitheredRiver && CrimsonKnotStacks >= 5)
			{
				modifiers.FinalDamage *= 1.05f; // 5% DR reduction
			}
		}

		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (projectile.DamageType == DamageClass.Melee || projectile.DamageType == WeepCompatibility.TrueMelee)
			{
				PropagateMarionetteDamage(npc, damageDone);
			}
		}

		public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
		{
			PropagateMarionetteDamage(npc, damageDone);
		}

		private static void PropagateMarionetteDamage(NPC sourceNpc, int damageDone)
		{
			int sharedDmg = (int)(damageDone * 0.15f);
			if (sharedDmg <= 0) return;

			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.type == ModContent.ProjectileType<NaughtClone>() && p.ModProjectile is NaughtClone clone)
				{
					if (clone.MarionetteTetherNPCs.Contains(sourceNpc.whoAmI))
					{
						foreach (int id in clone.MarionetteTetherNPCs)
						{
							if (id != sourceNpc.whoAmI && id >= 0 && id < Main.maxNPCs && Main.npc[id].active && !Main.npc[id].friendly && !Main.npc[id].dontTakeDamage)
							{
								Main.npc[id].SimpleStrikeNPC(sharedDmg, 0, false, 0f, DamageClass.Melee);
							}
						}
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

		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			NPC anchor = ResolveKnotAnchor(npc);
			if (anchor == null || anchor.whoAmI != npc.whoAmI)
				return;

			if (!anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData) || knotData.CrimsonKnotStacks <= 0)
				return;

			if (knotData.LastDrawnFrame == (int)Main.GameUpdateCount)
				return;

			knotData.LastDrawnFrame = (int)Main.GameUpdateCount;
			DrawKnotDirect(npc, spriteBatch, screenPos);
		}

		public static void DrawAllKnots(SpriteBatch spriteBatch, Vector2 screenPos)
		{
			if (Main.gameMenu || Main.dedServ)
				return;

			Rectangle screenRect = new Rectangle((int)screenPos.X - 100, (int)screenPos.Y - 100, Main.screenWidth + 200, Main.screenHeight + 200);

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active)
					continue;

				// Only consider primary anchors to prevent duplicate knot draws for multi-segment bosses
				NPC anchor = ResolveKnotAnchor(npc);
				if (anchor == null || anchor.whoAmI != npc.whoAmI)
					continue;

				if (!anchor.TryGetGlobalNPC<CrimsonKnotGlobalNPC>(out var knotData) || knotData.CrimsonKnotStacks <= 0)
					continue;

				if (knotData.LastDrawnFrame == (int)Main.GameUpdateCount)
					continue;

				knotData.LastDrawnFrame = (int)Main.GameUpdateCount;

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
					DrawKnotDirect(drawTarget, spriteBatch, screenPos);
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
