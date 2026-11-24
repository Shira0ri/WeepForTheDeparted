using System;
using System.Collections.Generic;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common.Players;

namespace WeepForTheDeparted.Common.UI.NihilityLedger
{
	public class NihilityLedgerUI : UIHandle
	{
		private static NihilityLedgerUI instance;
		public static NihilityLedgerUI Instance => instance ??= UIHandleLoader.GetUIHandleOfType<NihilityLedgerUI>();

		public override LayersModeEnum LayersMode => LayersModeEnum.Vanilla_Mouse_Text;
		public override bool CloseOnEscape => true;

		public override SoundStyle? OpenSound => SoundID.Item29 with { Pitch = -0.2f, Volume = 0.85f };
		public override SoundStyle? CloseSound => SoundID.Item37 with { Pitch = 0.35f, Volume = 0.85f };

		// Accurate UI Coordinate Space (taking Main.UIScale into account)
		public static float UIScreenW => (float)Main.screenWidth / Main.UIScale;
		public static float UIScreenH => (float)Main.screenHeight / Main.UIScale;
		public override Vector2 MousePosition => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;

		// Interactive states
		public int SelectedSlot = 0; // Currently selected slot (0: Soul, 1: Tide, 2: Ruin)
		public int HoveredSocket = -1;
		public int HoveredEidolon = -1;
		public int HoveredTraceCard = -1;
		public bool HoveredClose = false;
		private int cardScrollOffset = 0;

		private bool oldMouseLeft = false;
		private bool oldMouseRight = false;
		private uint lastUpdateFrame = 0;

		// Layout rectangles & anchors
		private Rectangle boardRect;
		private Rectangle closeButtonRect;
		private readonly Rectangle[] socketRects = new Rectangle[3];
		private Rectangle dynamicGridPanelRect;
		private readonly List<Rectangle> dynamicCardRects = new List<Rectangle>();
		private readonly List<CharmInfo> currentSlotCharms = new List<CharmInfo>();
		private readonly Vector2[] eidolonPositions = new Vector2[6];
		private Vector2 standCenter;
		private Vector2 bladeDisplayPos;
		private Vector2 sheathDisplayPos;

		// Click ripples
		private class ClickRipple
		{
			public Vector2 Pos;
			public float Radius;
			public float MaxRadius;
			public float Alpha;
		}
		private readonly List<ClickRipple> ripples = new List<ClickRipple>();

		// Ambient ash particles in UI space
		private class UIAshParticle
		{
			public Vector2 Pos;
			public Vector2 Vel;
			public float Alpha;
			public float Scale;
			public Color Color;
		}
		private readonly List<UIAshParticle> ashParticles = new List<UIAshParticle>();

		// Structure for Charms (Traces)
		public struct CharmInfo
		{
			public string Key;
			public int Slot; // 0: Soul, 1: Tide, 2: Ruin
			public string Title;
			public string Subtitle;
			public string Description;
			public string Lore;
		}

		// Structure for Eidolons
		public struct EidolonInfo
		{
			public int Index;
			public string RomanNumeral;
			public string Title;
			public string Subtitle;
			public string Description;
			public string Lore;
		}

		public static readonly CharmInfo[] AvailableCharms = new CharmInfo[]
		{
			// ==============================
			// SLOT 0: SOUL INSCRIPTIONS (6)
			// ==============================
			new CharmInfo {
				Key = "RedOni", Slot = 0,
				Title = "Mark of the Red Oni",
				Subtitle = "Soul Inscription · Starting Dream & Overflow",
				Description = "Begin combat with 5 Slashed Dream charges & 5 Crimson Knots.\nEnables up to 3 overflow charges, refunded after Ultimate concludes.",
				Lore = "\"A crimson shadow lingers by the guard; through endless kalpas, its edge dulls not.\""
			},
			new CharmInfo {
				Key = "NamelessDecoy", Slot = 0,
				Title = "Echo of the Nameless Decoy",
				Subtitle = "Soul Inscription · Clone Cleave Echo",
				Description = "When shadow clones expire or when Ultimate is activated, clones perform\nan Octobolt Echo cleave dealing 160% True Melee damage and 2 Crimson Knots.",
				Lore = "\"Shadows mirror the living; when the body departs, the steel still slashes.\""
			},
			new CharmInfo {
				Key = "FleetingStep", Slot = 0,
				Title = "Fleeting Petal Step",
				Subtitle = "Soul Inscription · Evasion & Projectile Slow",
				Description = "Flash Step cooldown reduced by 25%. Dashing through enemies grants\n1 Slashed Dream (5s cd) and slows hostile projectiles by 50% for 2.5s.",
				Lore = "\"Steps like falling cherry blossoms; between blade and void, neither is touched.\""
			},
			new CharmInfo {
				Key = "MirageWeft", Slot = 0,
				Title = "Mirage Weft",
				Subtitle = "Soul Inscription · Clone Cutting Grid & Dash Reset",
				Description = "Dashing casts dimensional threads connecting you to all clones for 2.0s,\nslicing enemies for 150% damage. Dashing through a clone resets Dash cooldown.",
				Lore = "\"A thousand threads of destiny; sever them all, and only emptiness remains.\""
			},
			new CharmInfo {
				Key = "Rainleaf", Slot = 0,
				Title = "Rainleaf Phasing",
				Subtitle = "Soul Inscription · Fatal Damage Void Phasing",
				Description = "Taking lethal or heavy damage (>35% max HP) triggers 1.2s Void Phasing\n(invulnerable, +50% speed). Grants 2 Slashed Dream charges (90s cooldown).",
				Lore = "\"Like rain to water, leaves tremble in the deluge; life and demise in a breath.\""
			},
			new CharmInfo {
				Key = "StillWater", Slot = 0,
				Title = "Still Water Basin",
				Subtitle = "Soul Inscription · Disciplined Duelist Focus",
				Description = "Remaining near enemies without taking damage for 3s grants [Still Water]:\n+16% True Melee Crit Chance and +20% poise/knockback resist.",
				Lore = "\"Quiet water mirrors the blade; an unrippled mind perceives the void.\""
			},

			// ==============================
			// SLOT 1: TIDE INSCRIPTIONS (6)
			// ==============================
			new CharmInfo {
				Key = "AbyssGrip", Slot = 1,
				Title = "Grip of the Abyss",
				Subtitle = "Tide Inscription · Debuff Amplification",
				Description = "Each unique debuff afflicting the target increases True Melee damage\nby 6% (up to 24% max in Calamity, 36% in Vanilla).",
				Lore = "\"Gaze into the abyss; all mortal suffering rings in the broken blade.\""
			},
			new CharmInfo {
				Key = "BloodLotus", Slot = 1,
				Title = "Blood Lotus Transmission",
				Subtitle = "Tide Inscription · Multi-Target Knot Bloom",
				Description = "Upon target death, Crimson Knots transfer to the 2 highest-HP enemies\nand erupt into an expanding Blood Lotus Shockwave.",
				Lore = "\"Those who perish across the river leave grudges blooming like lotuses.\""
			},
			new CharmInfo {
				Key = "ShadowMarionette", Slot = 1,
				Title = "Shadow Marionette Tethers",
				Subtitle = "Tide Inscription · Clone Damage Sharing",
				Description = "Clones project crimson threads linking to up to 3 nearby enemies,\nsharing 15% of True Melee damage dealt and propagating knots on death.",
				Lore = "\"Ethereal puppets bound by red string; one wound bleeds in three flesh.\""
			},
			new CharmInfo {
				Key = "WitheredRiver", Slot = 1,
				Title = "Scourge of the Withered River",
				Subtitle = "Tide Inscription · Debuff Attrition & Armor Peel",
				Description = "Debuffs inflicted on boss enemies last 40% longer.\nEnemies with 5 or more Crimson Knots have Damage Reduction reduced by 5%.",
				Lore = "\"The waters of Lethe dry to ash; poison seeps into the bones of gods.\""
			},
			new CharmInfo {
				Key = "ShadowCrucible", Slot = 1,
				Title = "Shadow Crucible",
				Subtitle = "Tide Inscription · DoT Acceleration & Armor Strip",
				Description = "Clones project an 18-block aura: enemies suffer -12 Defense,\n-10% Movement Speed, and damage-over-time debuffs tick 35% faster.",
				Lore = "\"Within the shadow crucible, all agony burns swifter into the void.\""
			},
			new CharmInfo {
				Key = "DepartedSiphon", Slot = 1,
				Title = "Siphon of the Departed",
				Subtitle = "Tide Inscription · Knot Detonation Lifesteal",
				Description = "Detonating Crimson Knots heals the player for 1% max HP per knot\n(capped at 9% per Ult) and grants +15% movement speed for 8 seconds.",
				Lore = "\"Life blossoms from demise; drink of the red dew that falls upon the blade.\""
			},

			// ==============================
			// SLOT 2: RUIN INSCRIPTIONS (6)
			// ==============================
			new CharmInfo {
				Key = "ThunderCore", Slot = 2,
				Title = "Thunder Core",
				Subtitle = "Ruin Inscription · Post-Ult Buff & 9 Thunderbolts",
				Description = "After casting Ultimate, deal +15% True Melee damage for 15s.\nStygian Resurge calls down 9 dimensional thunderbolts (30% Resurge DMG each).",
				Lore = "\"Thunder enters the marrow, cleaving the endless night from nothingness.\""
			},
			new CharmInfo {
				Key = "KarmaSever", Slot = 2,
				Title = "Severance of Karma",
				Subtitle = "Ruin Inscription · DR Penetration & Execution",
				Description = "Stygian Resurge ignores 20% enemy defense and penetrates 8% boss DR.\nInstantly executes non-boss enemies below 6% max HP.",
				Lore = "\"One slash severs all karma; on the road to the underworld, there is no return.\""
			},
			new CharmInfo {
				Key = "VoidHorizon", Slot = 2,
				Title = "Rift of the Void Horizon",
				Subtitle = "Ruin Inscription · Lingering Spatial Tears",
				Description = "Rainblades 1–3 leave dimensional tears that linger for 4s after Ult,\ndealing continuous slicing damage and slowing enemies by 30%.",
				Lore = "\"Space cracks like paper; the void bleeds into the waking world.\""
			},
			new CharmInfo {
				Key = "BladesongRain", Slot = 2,
				Title = "Bladesong of the Endless Rain",
				Subtitle = "Ruin Inscription · Rapid Cleave & Infinite Needles",
				Description = "Rainblades execute 35% faster. Detonated knots release Blood Blossom\nNeedles that infinitely pierce and relentlessly home onto live enemies.",
				Lore = "\"Rain falls without end; each needle of blood finds its mark in the dark.\""
			},
			new CharmInfo {
				Key = "SynchronizedExecution", Slot = 2,
				Title = "Synchronized Execution",
				Subtitle = "Ruin Inscription · Clones Awaken in Time-Freeze",
				Description = "Clones awaken during Ult: mirroring Rainblades with 45% DMG slashes,\nand converging in an intersecting 120% cross-slash at the sheathing snap.",
				Lore = "\"A thousand shadows rise together, drowning the sky in a single flash.\""
			},
			new CharmInfo {
				Key = "EmancipatorWill", Slot = 2,
				Title = "Emancipator's Severed Will",
				Subtitle = "Ruin Inscription · Knot Refund & Post-Ult Crit",
				Description = "For every 9 Crimson Knots consumed in Ult, refund 2 Slashed Dream charges.\nGrants +20% True Melee Crit Chance for 10 seconds post-Ult.",
				Lore = "\"The Emancipator walks alone; from endless ruin, power surges anew.\""
			}
		};

		public static readonly EidolonInfo[] Eidolons = new EidolonInfo[]
		{
			new EidolonInfo {
				Index = 0, RomanNumeral = "I",
				Title = "Silenced Sky",
				Subtitle = "Eidolon Rank I",
				Description = "Attacking enemies afflicted by any debuff grants\n+18% True Melee Crit Chance and +25% Crit Damage.",
				Lore = "\"Beneath the quiet canopy, the blade sings at the beginning of truth.\""
			},
			new EidolonInfo {
				Index = 1, RomanNumeral = "II",
				Title = "Mute Thunder",
				Subtitle = "Eidolon Rank II",
				Description = "Passively generates 1 Slashed Dream every 5 seconds in combat;\nmarks the enemy with the highest life with 1 Crimson Knot.",
				Lore = "\"Silent thunder shakes the mountain; the sun sinks into stillness.\""
			},
			new EidolonInfo {
				Index = 2, RomanNumeral = "III",
				Title = "Frosty Strands",
				Subtitle = "Eidolon Rank III",
				Description = "Rainblade strikes deal +20% base damage and their\ndimensional cleave radius is expanded by 1.4x.",
				Lore = "\"Fine rain like strands, frost sealing the edge; each slash unravels space.\""
			},
			new EidolonInfo {
				Index = 3, RomanNumeral = "IV",
				Title = "Shrine in Flame",
				Subtitle = "Eidolon Rank IV",
				Description = "Activating Nihility Domain afflicts all onscreen enemies with\n[Desolation Vulnerability], increasing damage taken by 12%.",
				Lore = "\"Flames spread through the mirror, revealing shadows destined to fade.\""
			},
			new EidolonInfo {
				Index = 4, RomanNumeral = "V",
				Title = "Strewn Petals",
				Subtitle = "Eidolon Rank V",
				Description = "Stygian Resurge damage is increased by +35%;\noverflow refund recovery cooldown is reduced by 50%.",
				Lore = "\"The spider lily withers, petals falling like rain, leaving quietude.\""
			},
			new EidolonInfo {
				Index = 5, RomanNumeral = "VI",
				Title = "Apocalypse, the Emancipator",
				Subtitle = "Eidolon Rank VI",
				Description = "All combo attacks count as Ultimate DMG with full bonuses;\npermanently penetrates 20% of all enemy damage resistance.",
				Lore = "\"From the severance of thought, all is void. I sever all karma; my name is Acheron.\""
			}
		};

		public override void SetStaticDefaults()
		{
			instance = this;
		}

		public void UpdateState()
		{
			LogicUpdate();
		}

		private void RecalculateLayout()
		{
			float screenW = UIScreenW;
			float screenH = UIScreenH;

			int boardMarginX = (int)Math.Max(24f, screenW * 0.04f);
			int boardMarginY = (int)Math.Max(18f, screenH * 0.04f);
			boardRect = new Rectangle(boardMarginX, boardMarginY, (int)screenW - boardMarginX * 2, (int)screenH - boardMarginY * 2);

			// Close button [ × ] at top-right
			closeButtonRect = new Rectangle(boardRect.Right - 54, boardRect.Y + 16, 40, 40);

			// Left Column: 3 Inscription Sockets
			int plaqueX = boardRect.X + 24;
			int plaqueW = (int)Math.Min(400f, boardRect.Width * 0.36f);
			int plaqueH = 70;
			int spacing = 12;
			int startY = boardRect.Y + 84;

			for (int i = 0; i < 3; i++)
			{
				socketRects[i] = new Rectangle(plaqueX, startY + i * (plaqueH + spacing), plaqueW, plaqueH);
			}

			// Two-Tiered Katana Stand in upper-right area
			standCenter = new Vector2(boardRect.X + boardRect.Width * 0.68f, boardRect.Y + 130f);
			bladeDisplayPos = standCenter + new Vector2(0f, -22f);
			sheathDisplayPos = standCenter + new Vector2(0f, 32f);

			// Dynamic Inscription Grid Area (below the 3 sockets)
			int gridY = startY + 3 * (plaqueH + spacing) + 10;
			int gridW = plaqueW;
			int gridH = boardRect.Bottom - gridY - 18;
			dynamicGridPanelRect = new Rectangle(plaqueX, gridY, gridW, gridH);

			// Populate dynamic card rectangles for the currently selected slot
			currentSlotCharms.Clear();
			for (int c = 0; c < AvailableCharms.Length; c++)
			{
				if (AvailableCharms[c].Slot == SelectedSlot)
				{
					currentSlotCharms.Add(AvailableCharms[c]);
				}
			}

			dynamicCardRects.Clear();
			int cardCount = currentSlotCharms.Count;
			if (cardCount > 0)
			{
				int cardSpacing = 8;
				int cardH = 88;
				int contentHeight = cardCount * cardH + (cardCount - 1) * cardSpacing;
				int visibleHeight = gridH - 36;
				int maxScroll = Math.Max(0, contentHeight - visibleHeight);
				cardScrollOffset = Math.Clamp(cardScrollOffset, 0, maxScroll);

				int cardStartY = gridY + 32 - cardScrollOffset;

				for (int k = 0; k < cardCount; k++)
				{
					dynamicCardRects.Add(new Rectangle(plaqueX + 8, cardStartY + k * (cardH + cardSpacing), gridW - 20, cardH));
				}
			}

			// 6 Eidolon node positions along a sweeping arc at bottom-right
			Vector2 eidolonCenter = new Vector2(boardRect.X + boardRect.Width * 0.68f, boardRect.Y + boardRect.Height * 0.74f);
			float arcRadius = MathHelper.Clamp(boardRect.Height * 0.16f, 75f, 100f);
			for (int k = 0; k < 6; k++)
			{
				float ang = MathHelper.ToRadians(165f - k * 35f);
				eidolonPositions[k] = eidolonCenter + new Vector2((float)Math.Cos(ang) * arcRadius * 1.35f, (float)Math.Sin(ang) * arcRadius);
			}
		}

		public override void LogicUpdate()
		{
			// Debounce duplicate update calls within the same frame
			if (lastUpdateFrame == Main.GameUpdateCount)
				return;
			lastUpdateFrame = Main.GameUpdateCount;

			if (OpenProgress.Current <= 0.001f && !IsOpen)
				return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
			{
				if (IsOpen) Close();
				return;
			}

			// Block weapon firing while UI is open
			if (IsOpen && OpenProgress.Current > 0.1f)
			{
				player.mouseInterface = true;
			}

			// Recalculate layout in UI coordinate space
			RecalculateLayout();

			Vector2 mouse = MousePosition;
			Point mousePoint = mouse.ToPoint();
			var naughtPlayer = player.GetModPlayer<NaughtPlayer>();

			// Detect hover states
			HoveredClose = closeButtonRect.Contains(mousePoint);

			HoveredSocket = -1;
			for (int i = 0; i < 3; i++)
			{
				if (socketRects[i].Contains(mousePoint))
					HoveredSocket = i;
			}

			HoveredTraceCard = -1;
			if (dynamicGridPanelRect.Contains(mousePoint))
			{
				int wheel = PlayerInput.ScrollWheelDeltaForUI;
				if (wheel != 0)
				{
					cardScrollOffset -= Math.Sign(wheel) * 36;
					int cardSpacing = 8;
					int cardH = 88;
					int contentHeight = currentSlotCharms.Count * cardH + (currentSlotCharms.Count - 1) * cardSpacing;
					int visibleHeight = dynamicGridPanelRect.Height - 36;
					int maxScroll = Math.Max(0, contentHeight - visibleHeight);
					cardScrollOffset = Math.Clamp(cardScrollOffset, 0, maxScroll);
				}

				for (int k = 0; k < dynamicCardRects.Count; k++)
				{
					Rectangle r = dynamicCardRects[k];
					if (r.Contains(mousePoint) && r.Bottom >= dynamicGridPanelRect.Y + 32 && r.Top <= dynamicGridPanelRect.Bottom - 6)
						HoveredTraceCard = k;
				}
			}

			HoveredEidolon = -1;
			for (int k = 0; k < 6; k++)
			{
				if (Vector2.Distance(mouse, eidolonPositions[k]) <= 22f)
					HoveredEidolon = k;
			}

			// Click detection
			bool leftJustPressed = (Main.mouseLeft && !oldMouseLeft) || (Main.mouseLeft && Main.mouseLeftRelease) || (keyLeftPressState == KeyPressState.Pressed);
			bool rightJustPressed = (Main.mouseRight && !oldMouseRight) || (Main.mouseRight && Main.mouseRightRelease) || (keyRightPressState == KeyPressState.Pressed);

			if (leftJustPressed)
			{
				// Spawn visual ink ripple at click position
				ripples.Add(new ClickRipple {
					Pos = mouse, Radius = 4f, MaxRadius = 65f, Alpha = 0.85f
				});

				// 1. Close button clicked
				if (HoveredClose)
				{
					Close();
					SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.35f, Volume = 0.85f }, player.Center);
					Main.mouseLeftRelease = false;
					oldMouseLeft = Main.mouseLeft;
					oldMouseRight = Main.mouseRight;
					return;
				}

				// 2. Click outside the board to close
				if (!boardRect.Contains(mousePoint))
				{
					Close();
					SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.35f, Volume = 0.85f }, player.Center);
					Main.mouseLeftRelease = false;
					oldMouseLeft = Main.mouseLeft;
					oldMouseRight = Main.mouseRight;
					return;
				}

				// 3. Click an Inscription Socket to select it & open its dynamic trace grid!
				if (HoveredSocket >= 0 && HoveredSocket < 3)
				{
					if (SelectedSlot != HoveredSocket)
					{
						SelectedSlot = HoveredSocket;
						cardScrollOffset = 0;
					}
					SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.2f, Volume = 0.8f }, player.Center);
				}

				// 4. Click a dynamic Trace Card to equip it to the selected socket!
				if (HoveredTraceCard >= 0 && HoveredTraceCard < currentSlotCharms.Count && naughtPlayer != null)
				{
					var charm = currentSlotCharms[HoveredTraceCard];
					naughtPlayer.EquippedCharms[charm.Slot] = charm.Key;
					SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.15f, Volume = 0.95f }, player.Center);
				}

				// 5. Click Eidolon node to toggle unlock
				if (HoveredEidolon >= 0 && HoveredEidolon < 6 && naughtPlayer != null)
				{
					naughtPlayer.UnlockedEidolons[HoveredEidolon] = !naughtPlayer.UnlockedEidolons[HoveredEidolon];
					SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.2f + HoveredEidolon * 0.1f, Volume = 0.9f }, player.Center);
				}
			}

			// Right click on socket or equipped card to unequip
			if (rightJustPressed && naughtPlayer != null)
			{
				if (HoveredSocket >= 0 && HoveredSocket < 3)
				{
					if (!string.IsNullOrEmpty(naughtPlayer.EquippedCharms[HoveredSocket]))
					{
						naughtPlayer.EquippedCharms[HoveredSocket] = "";
						SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.2f, Volume = 0.75f }, player.Center);
					}
				}
				else if (HoveredTraceCard >= 0 && HoveredTraceCard < currentSlotCharms.Count)
				{
					var charm = currentSlotCharms[HoveredTraceCard];
					if (naughtPlayer.EquippedCharms[charm.Slot] == charm.Key)
					{
						naughtPlayer.EquippedCharms[charm.Slot] = "";
						SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.2f, Volume = 0.75f }, player.Center);
					}
				}
			}

			// Update state history
			oldMouseLeft = Main.mouseLeft;
			oldMouseRight = Main.mouseRight;

			// Ambient ash particles in UI space
			if (Main.rand.NextBool(3))
			{
				ashParticles.Add(new UIAshParticle {
					Pos = new Vector2(Main.rand.NextFloat(0, UIScreenW), -10f),
					Vel = new Vector2(Main.rand.NextFloat(-0.8f, 0.4f), Main.rand.NextFloat(1.5f, 3.5f)),
					Alpha = Main.rand.NextFloat(0.4f, 0.85f),
					Scale = Main.rand.NextFloat(1.5f, 3.0f),
					Color = Main.rand.NextBool(4) ? NihilityLedgerTheme.AcheronScarlet : NihilityLedgerTheme.PaperWhite
				});
			}

			for (int i = ashParticles.Count - 1; i >= 0; i--)
			{
				var p = ashParticles[i];
				p.Pos += p.Vel;
				if (p.Pos.Y > UIScreenH + 10 || p.Pos.X < -10)
					ashParticles.RemoveAt(i);
			}

			// Update click ripples
			for (int i = ripples.Count - 1; i >= 0; i--)
			{
				var r = ripples[i];
				r.Radius += (r.MaxRadius - r.Radius) * 0.12f + 1.2f;
				r.Alpha *= 0.91f;
				if (r.Alpha <= 0.01f || r.Radius >= r.MaxRadius * 0.98f)
					ripples.RemoveAt(i);
			}
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			float alpha = MathHelper.Clamp(OpenProgress.Current, 0f, 1f);
			if (alpha <= 0.001f && !IsOpen)
				return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;

			var naughtPlayer = player.GetModPlayer<NaughtPlayer>();
			if (naughtPlayer == null)
				return;

			Vector2 mouse = MousePosition;
			float screenW = UIScreenW;
			float screenH = UIScreenH;

			player.mouseInterface = true;
			RecalculateLayout();

			// 1. Fullscreen Dim Vignette
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, new Rectangle(0, 0, (int)screenW + 10, (int)screenH + 10), Color.Black * (0.86f * alpha));

			// 2. Ambient Ash Particles
			foreach (var p in ashParticles)
			{
				spriteBatch.Draw(pixel, p.Pos, new Rectangle(0, 0, 1, 1), p.Color * (p.Alpha * alpha), 0f, Vector2.Zero, p.Scale, SpriteEffects.None, 0f);
			}

			// 3. Main Outer Lacquer Board
			NihilityLedgerTheme.DrawBeveledBox(spriteBatch, boardRect, NihilityLedgerTheme.VoidBg * alpha, NihilityLedgerTheme.LacquerBorder * alpha, 3f);
			NihilityLedgerTheme.DrawBeveledBox(spriteBatch, new Rectangle(boardRect.X + 4, boardRect.Y + 4, boardRect.Width - 8, boardRect.Height - 8), Color.Transparent, NihilityLedgerTheme.AcheronScarlet * (0.55f * alpha), 1f);

			// 4. Header Bar
			DrawHeader(spriteBatch, mouse, alpha);

			// 5. Blade & Scabbard Presentation (Clean, uncluttered two-tiered horizontal stand)
			DrawBladePresentation(spriteBatch, alpha);

			// 6. Left Column: 3 Inscription Sockets
			DrawSocketPlaques(spriteBatch, naughtPlayer, alpha);

			// 7. Dynamic Traces Grid Panel for Selected Slot
			DrawDynamicTraceGrid(spriteBatch, naughtPlayer, alpha);

			// 8. Bottom-Right: 6-Fold Eidolon Constellation
			DrawEidolonConstellation(spriteBatch, naughtPlayer, alpha);

			// 9. Click Ripples
			foreach (var r in ripples)
			{
				NihilityLedgerTheme.DrawCircle(spriteBatch, r.Pos, r.Radius, NihilityLedgerTheme.AcheronScarlet * (r.Alpha * alpha), 2f, 28);
			}

			// 10. Floating Tooltip Overlay
			DrawTooltip(spriteBatch, mouse, naughtPlayer, alpha);
		}

		private void DrawHeader(SpriteBatch sb, Vector2 mouse, float alpha)
		{
			DynamicSpriteFont fontText = FontAssets.MouseText.Value;

			string titleStr = "NIHILITY LEDGER — Inscriptions & Eidolons";
			Vector2 titlePos = new Vector2(boardRect.X + 24, boardRect.Y + 18);

			sb.DrawString(fontText, titleStr, titlePos + new Vector2(1, 1), Color.Black * alpha, 0f, Vector2.Zero, 1.25f, SpriteEffects.None, 0f);
			sb.DrawString(fontText, titleStr, titlePos, NihilityLedgerTheme.PaperWhite * alpha, 0f, Vector2.Zero, 1.25f, SpriteEffects.None, 0f);

			string subStr = "\"All will return to nothingness, like rain to water.\"  ·  [C] or [ESC] to close";
			Vector2 subPos = new Vector2(boardRect.X + 24, boardRect.Y + 44);
			sb.DrawString(fontText, subStr, subPos, NihilityLedgerTheme.TextDim * alpha, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);

			// Dividing red brush line
			Vector2 lineStart = new Vector2(boardRect.X + 20, boardRect.Y + 70);
			Vector2 lineEnd = new Vector2(boardRect.Right - 20, boardRect.Y + 70);
			NihilityLedgerTheme.DrawGlowLine(sb, lineStart, lineEnd, NihilityLedgerTheme.AcheronScarlet * (0.8f * alpha), NihilityLedgerTheme.DeepBurgundy * (0.4f * alpha), 2f, 5f);

			// Close button [ × ]
			Color closeBg = HoveredClose ? NihilityLedgerTheme.AcheronScarlet * (0.92f * alpha) : NihilityLedgerTheme.LacquerBlack * (0.75f * alpha);
			Color closeBorder = HoveredClose ? NihilityLedgerTheme.HotWhite * alpha : NihilityLedgerTheme.LacquerBorder * alpha;

			NihilityLedgerTheme.DrawBeveledBox(sb, closeButtonRect, closeBg, closeBorder, HoveredClose ? 2.5f : 1.5f);
			Vector2 xPos = new Vector2(closeButtonRect.X + 13, closeButtonRect.Y + 8);
			sb.DrawString(fontText, "X", xPos, (HoveredClose ? Color.White : NihilityLedgerTheme.TextDim) * alpha, 0f, Vector2.Zero, 1.25f, SpriteEffects.None, 0f);
		}

		private void DrawBladePresentation(SpriteBatch sb, float alpha)
		{
			Texture2D bladeTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtUltBlade", AssetRequestMode.ImmediateLoad).Value;
			Texture2D sheathTex = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/Items/Weapons/NaughtSheath", AssetRequestMode.ImmediateLoad).Value;

			Vector2 center = standCenter;

			// Two-Tiered Katana Stand
			float standW = 310f;
			float standBaseY = center.Y + 54f;

			// Base Stand Foot Bar
			Rectangle standBase = new Rectangle((int)(center.X - standW * 0.5f), (int)standBaseY, (int)standW, 10);
			NihilityLedgerTheme.DrawBeveledBox(sb, standBase, NihilityLedgerTheme.LacquerBlack * alpha, NihilityLedgerTheme.LacquerBorder * alpha, 2f);

			// Left & Right Stand Posts
			int leftPostX = (int)(center.X - 90f);
			Rectangle postLeft = new Rectangle(leftPostX, (int)(center.Y - 32f), 14, (int)(standBaseY - (center.Y - 32f)));
			NihilityLedgerTheme.DrawBeveledBox(sb, postLeft, NihilityLedgerTheme.LacquerBlack * alpha, NihilityLedgerTheme.LacquerBorder * alpha, 1.5f);

			Rectangle notchLeftUpper = new Rectangle(leftPostX - 8, (int)(center.Y - 24f), 30, 8);
			Rectangle notchLeftLower = new Rectangle(leftPostX - 8, (int)(center.Y + 30f), 30, 8);
			NihilityLedgerTheme.DrawBeveledBox(sb, notchLeftUpper, NihilityLedgerTheme.LacquerBlack * alpha, NihilityLedgerTheme.LacquerBorder * alpha, 1f);
			NihilityLedgerTheme.DrawBeveledBox(sb, notchLeftLower, NihilityLedgerTheme.LacquerBlack * alpha, NihilityLedgerTheme.LacquerBorder * alpha, 1f);

			int rightPostX = (int)(center.X + 80f);
			Rectangle postRight = new Rectangle(rightPostX, (int)(center.Y - 32f), 14, (int)(standBaseY - (center.Y - 32f)));
			NihilityLedgerTheme.DrawBeveledBox(sb, postRight, NihilityLedgerTheme.LacquerBlack * alpha, NihilityLedgerTheme.LacquerBorder * alpha, 1.5f);

			Rectangle notchRightUpper = new Rectangle(rightPostX - 8, (int)(center.Y - 24f), 30, 8);
			Rectangle notchRightLower = new Rectangle(rightPostX - 8, (int)(center.Y + 30f), 30, 8);
			NihilityLedgerTheme.DrawBeveledBox(sb, notchRightUpper, NihilityLedgerTheme.LacquerBlack * alpha, NihilityLedgerTheme.LacquerBorder * alpha, 1f);
			NihilityLedgerTheme.DrawBeveledBox(sb, notchRightLower, NihilityLedgerTheme.LacquerBlack * alpha, NihilityLedgerTheme.LacquerBorder * alpha, 1f);

			// Lower Tier: Scabbard (flat horizontal)
			if (sheathTex != null)
			{
				Vector2 sheathOrigin = new Vector2(90f, 90f);
				Vector2 sheathPos = sheathDisplayPos;
				float sheathRot = MathHelper.ToRadians(42.8f);
				sb.Draw(sheathTex, sheathPos, null, Color.White * (0.88f * alpha), sheathRot, sheathOrigin, 1.45f, SpriteEffects.None, 0f);
			}

			// Upper Tier: Awakened Nodachi Blade (flat horizontal)
			float floatY = (float)Math.Sin(GlobalTimer * 2f) * 2f;
			Vector2 bladePos = bladeDisplayPos + new Vector2(0f, floatY);

			if (bladeTex != null)
			{
				Vector2 bladeOrigin = new Vector2(90f, 90f);
				float bladeRot = MathHelper.ToRadians(43.8f);

				float auraPulse = (float)Math.Sin(GlobalTimer * 3.5f) * 0.15f + 0.85f;
				Color auraCol = new Color(255, 35, 60, 0) * (0.55f * auraPulse * alpha);
				sb.Draw(bladeTex, bladePos, null, auraCol, bladeRot, bladeOrigin, 1.58f, SpriteEffects.None, 0f);
				sb.Draw(bladeTex, bladePos, null, Color.White * alpha, bladeRot, bladeOrigin, 1.50f, SpriteEffects.None, 0f);
			}
		}

		private void DrawSocketPlaques(SpriteBatch sb, NaughtPlayer player, float alpha)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;

			string[] socketNames = new string[] {
				"Soul Inscription",
				"Tide Inscription",
				"Ruin Inscription"
			};

			string[] slotSubtitles = new string[] {
				"Tang / Tsuka · Starting Dream & Resource Flow",
				"Fuller / Spine · Debuff Amplification & Knot Ripple",
				"Hamon / Edge · Rainblade Lethality & Execution"
			};

			for (int i = 0; i < 3; i++)
			{
				Rectangle plaqueRect = socketRects[i];
				bool hovered = (HoveredSocket == i);
				bool isSelected = (SelectedSlot == i);

				string equippedKey = player.EquippedCharms[i];
				bool hasCharm = !string.IsNullOrEmpty(equippedKey);

				CharmInfo equippedInfo = default;
				if (hasCharm)
				{
					for (int c = 0; c < AvailableCharms.Length; c++)
					{
						if (AvailableCharms[c].Key == equippedKey)
						{
							equippedInfo = AvailableCharms[c];
							break;
						}
					}
				}

				// Plaque background & border
				Color bgCol = isSelected
					? NihilityLedgerTheme.DeepBurgundy * (0.85f * alpha)
					: (hovered ? NihilityLedgerTheme.LacquerBlack * alpha : NihilityLedgerTheme.VoidBg * alpha);

				Color borderCol = isSelected
					? NihilityLedgerTheme.AcheronScarlet * alpha
					: (hovered ? NihilityLedgerTheme.HotWhite * alpha : NihilityLedgerTheme.LacquerBorder * alpha);

				NihilityLedgerTheme.DrawBeveledBox(sb, plaqueRect, bgCol, borderCol, isSelected ? 2.5f : (hovered ? 2f : 1.5f));

				// Left indicator bar for selected socket
				if (isSelected)
				{
					Rectangle indBar = new Rectangle(plaqueRect.X, plaqueRect.Y, 5, plaqueRect.Height);
					sb.Draw(TextureAssets.MagicPixel.Value, indBar, NihilityLedgerTheme.AcheronScarlet * alpha);
				}

				// Socket Index Seal Tag
				Rectangle sealTag = new Rectangle(plaqueRect.X + 10, plaqueRect.Y + 8, 28, 22);
				NihilityLedgerTheme.DrawBeveledBox(sb, sealTag, NihilityLedgerTheme.DeepBurgundy * alpha, NihilityLedgerTheme.AcheronScarlet * alpha, 1f);
				sb.DrawString(font, (i + 1).ToString(), new Vector2(sealTag.X + 9, sealTag.Y + 2), NihilityLedgerTheme.HotWhite * alpha, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);

				// Title
				Vector2 namePos = new Vector2(plaqueRect.X + 44, plaqueRect.Y + 8);
				sb.DrawString(font, socketNames[i], namePos, (isSelected ? NihilityLedgerTheme.HotWhite : NihilityLedgerTheme.PaperWhite) * alpha, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 0f);

				// Subtitle
				Vector2 subPos = new Vector2(plaqueRect.X + 44, plaqueRect.Y + 28);
				sb.DrawString(font, slotSubtitles[i], subPos, NihilityLedgerTheme.TextDim * alpha, 0f, Vector2.Zero, 0.72f, SpriteEffects.None, 0f);

				// Equipped status
				Vector2 statusPos = new Vector2(plaqueRect.X + 16, plaqueRect.Y + 46);
				if (hasCharm)
				{
					sb.DrawString(font, $"[Inscribed] {equippedInfo.Title}", statusPos, NihilityLedgerTheme.AcheronScarlet * alpha, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
				}
				else
				{
					sb.DrawString(font, "— Empty Slot (Click to view traces) —", statusPos, NihilityLedgerTheme.TextDisabled * alpha, 0f, Vector2.Zero, 0.78f, SpriteEffects.None, 0f);
				}
			}
		}

		private void DrawDynamicTraceGrid(SpriteBatch sb, NaughtPlayer player, float alpha)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;

			// Main dynamic panel box
			NihilityLedgerTheme.DrawBeveledBox(sb, dynamicGridPanelRect, NihilityLedgerTheme.VoidBg * alpha, NihilityLedgerTheme.LacquerBorder * alpha, 1.5f);

			string[] slotTitles = new string[] { "Soul Inscriptions", "Tide Inscriptions", "Ruin Inscriptions" };
			string header = $"Available {slotTitles[SelectedSlot]} (Click card to Inscribe)";
			sb.DrawString(font, header, new Vector2(dynamicGridPanelRect.X + 12, dynamicGridPanelRect.Y + 8), NihilityLedgerTheme.PaperWhite * alpha, 0f, Vector2.Zero, 0.82f, SpriteEffects.None, 0f);

			for (int k = 0; k < currentSlotCharms.Count; k++)
			{
				if (k >= dynamicCardRects.Count) break;

				Rectangle cardRect = dynamicCardRects[k];
				if (cardRect.Bottom <= dynamicGridPanelRect.Y + 28 || cardRect.Top >= dynamicGridPanelRect.Bottom - 6)
					continue;

				CharmInfo charm = currentSlotCharms[k];
				bool isEquipped = player.EquippedCharms[charm.Slot] == charm.Key;
				bool hovered = (HoveredTraceCard == k);

				Color cardBg = isEquipped
					? NihilityLedgerTheme.DeepBurgundy * (0.80f * alpha)
					: (hovered ? NihilityLedgerTheme.LacquerBlack * alpha : NihilityLedgerTheme.DarkShadow * (0.85f * alpha));

				Color cardBorder = isEquipped
					? NihilityLedgerTheme.AcheronScarlet * alpha
					: (hovered ? NihilityLedgerTheme.HotWhite * alpha : NihilityLedgerTheme.LacquerBorder * alpha);

				NihilityLedgerTheme.DrawBeveledBox(sb, cardRect, cardBg, cardBorder, hovered ? 2.2f : 1.2f);

				// Title & status badge
				Vector2 titlePos = new Vector2(cardRect.X + 10, cardRect.Y + 8);
				Color titleCol = isEquipped ? NihilityLedgerTheme.AcheronScarlet : (hovered ? NihilityLedgerTheme.HotWhite : NihilityLedgerTheme.PaperWhite);
				sb.DrawString(font, charm.Title, titlePos, titleCol * alpha, 0f, Vector2.Zero, 0.90f, SpriteEffects.None, 0f);

				// Status badge at top-right of card
				string statusBadge = isEquipped ? "[INSCRIBED]" : (hovered ? "[CLICK TO INSCRIBE]" : "[AVAILABLE]");
				Color badgeCol = isEquipped ? NihilityLedgerTheme.AcheronScarlet : (hovered ? NihilityLedgerTheme.HotWhite : NihilityLedgerTheme.TextDim);
				Vector2 badgeSize = font.MeasureString(statusBadge) * 0.72f;
				sb.DrawString(font, statusBadge, new Vector2(cardRect.Right - badgeSize.X - 10, cardRect.Y + 8), badgeCol * alpha, 0f, Vector2.Zero, 0.72f, SpriteEffects.None, 0f);

				// Description
				Vector2 descPos = new Vector2(cardRect.X + 10, cardRect.Y + 28);
				sb.DrawString(font, charm.Description, descPos, NihilityLedgerTheme.PaperWhite * (0.92f * alpha), 0f, Vector2.Zero, 0.72f, SpriteEffects.None, 0f);

				// Lore Quote at bottom
				Vector2 lorePos = new Vector2(cardRect.X + 10, cardRect.Y + cardRect.Height - 18);
				sb.DrawString(font, charm.Lore, lorePos, NihilityLedgerTheme.TextDim * alpha, 0f, Vector2.Zero, 0.68f, SpriteEffects.None, 0f);
			}

			// Draw scrollbar if content exceeds panel height
			int contentHeight = currentSlotCharms.Count * 88 + Math.Max(0, currentSlotCharms.Count - 1) * 8;
			int visibleHeight = dynamicGridPanelRect.Height - 36;
			int maxScroll = Math.Max(0, contentHeight - visibleHeight);
			if (maxScroll > 0)
			{
				float thumbRatio = (float)visibleHeight / contentHeight;
				int thumbH = Math.Max(24, (int)(visibleHeight * thumbRatio));
				int thumbY = dynamicGridPanelRect.Y + 32 + (int)((visibleHeight - thumbH) * ((float)cardScrollOffset / maxScroll));
				Rectangle thumbRect = new Rectangle(dynamicGridPanelRect.Right - 7, thumbY, 3, thumbH);
				sb.Draw(TextureAssets.MagicPixel.Value, thumbRect, NihilityLedgerTheme.AcheronScarlet * (0.85f * alpha));
			}
		}

		private void DrawEidolonConstellation(SpriteBatch sb, NaughtPlayer player, float alpha)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;

			Vector2 center = new Vector2(boardRect.X + boardRect.Width * 0.68f, boardRect.Y + boardRect.Height * 0.74f);
			string title = "Departed Eidolons (E1 – E6)";
			Vector2 titleSize = font.MeasureString(title);
			sb.DrawString(font, title, new Vector2(center.X - titleSize.X * 0.5f * 0.9f, center.Y - 105f), NihilityLedgerTheme.PaperWhite * alpha, 0f, Vector2.Zero, 0.90f, SpriteEffects.None, 0f);

			// Connecting energy strands
			for (int k = 0; k < 5; k++)
			{
				bool bothUnlocked = player.UnlockedEidolons[k] && player.UnlockedEidolons[k + 1];
				Color strandCore = bothUnlocked ? NihilityLedgerTheme.AcheronScarlet : NihilityLedgerTheme.DeepBurgundy * 0.6f;
				Color strandGlow = bothUnlocked ? NihilityLedgerTheme.NihilityViolet : NihilityLedgerTheme.DarkShadow;
				NihilityLedgerTheme.DrawGlowLine(sb, eidolonPositions[k], eidolonPositions[k + 1], strandCore * alpha, strandGlow * alpha, bothUnlocked ? 2.5f : 1.2f, 6f);
			}

			// Eidolon nodes
			float nodeRadius = 18f;
			for (int k = 0; k < 6; k++)
			{
				bool unlocked = player.UnlockedEidolons[k];
				bool hovered = (HoveredEidolon == k);

				if (unlocked)
				{
					float pulse = (float)Math.Sin(GlobalTimer * 4f + k * 0.7f) * 3f + nodeRadius + 3f;
					NihilityLedgerTheme.DrawCircle(sb, eidolonPositions[k], pulse, NihilityLedgerTheme.AcheronScarlet * (0.65f * alpha), 1.5f);
				}

				Color discBg = unlocked ? NihilityLedgerTheme.DeepBurgundy : NihilityLedgerTheme.LacquerBlack;
				Color discBorder = hovered ? NihilityLedgerTheme.HotWhite : (unlocked ? NihilityLedgerTheme.AcheronScarlet : NihilityLedgerTheme.LacquerBorder);

				NihilityLedgerTheme.DrawFilledCircle(sb, eidolonPositions[k], nodeRadius, discBg * alpha);
				NihilityLedgerTheme.DrawCircle(sb, eidolonPositions[k], nodeRadius, discBorder * alpha, hovered ? 2.5f : 1.5f);

				string numStr = Eidolons[k].RomanNumeral;
				Vector2 numSize = font.MeasureString(numStr);
				Color numCol = unlocked ? NihilityLedgerTheme.HotWhite : NihilityLedgerTheme.TextDisabled;
				sb.DrawString(font, numStr, eidolonPositions[k] - numSize * 0.5f * 0.85f, numCol * alpha, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
			}
		}

		private void DrawTooltip(SpriteBatch sb, Vector2 mouse, NaughtPlayer player, float alpha)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;

			string title = null;
			string sub = null;
			string desc = null;
			string lore = null;
			string hint = null;
			Color titleColor = NihilityLedgerTheme.HotWhite;

			if (HoveredEidolon >= 0 && HoveredEidolon < 6)
			{
				var e = Eidolons[HoveredEidolon];
				bool unl = player.UnlockedEidolons[HoveredEidolon];
				title = $"{e.RomanNumeral} · {e.Title}";
				sub = e.Subtitle + (unl ? " [Active]" : " [Locked]");
				desc = e.Description;
				lore = e.Lore;
				hint = unl ? "[Left-Click to toggle off]" : "[Left-Click to unlock & activate]";
				titleColor = unl ? NihilityLedgerTheme.AcheronScarlet : NihilityLedgerTheme.HotWhite;
			}
			else if (HoveredSocket >= 0 && HoveredSocket < 3)
			{
				string key = player.EquippedCharms[HoveredSocket];
				if (!string.IsNullOrEmpty(key))
				{
					for (int c = 0; c < AvailableCharms.Length; c++)
					{
						if (AvailableCharms[c].Key == key)
						{
							title = AvailableCharms[c].Title;
							sub = AvailableCharms[c].Subtitle;
							desc = AvailableCharms[c].Description;
							lore = AvailableCharms[c].Lore;
							hint = "[Right-Click to unequip this Inscription]";
							titleColor = NihilityLedgerTheme.AcheronScarlet;
							break;
						}
					}
				}
				else
				{
					title = "Empty Inscription Slot";
					sub = "Click to view compatible traces in the grid below";
					desc = "Equipping Inscriptions enhances combat pacing, starting Dream,\ndebuff scaling, or Stygian Resurge execution.";
					hint = "[Left-Click to select this slot]";
					titleColor = NihilityLedgerTheme.TextDisabled;
				}
			}

			if (string.IsNullOrEmpty(title))
				return;

			float tipW = 380f;
			float tipH = 145f;
			Vector2 tipPos = mouse + new Vector2(18f, 18f);

			if (tipPos.X + tipW > UIScreenW - 20)
				tipPos.X = mouse.X - tipW - 10;
			if (tipPos.Y + tipH > UIScreenH - 20)
				tipPos.Y = mouse.Y - tipH - 10;

			Rectangle tipRect = new Rectangle((int)tipPos.X, (int)tipPos.Y, (int)tipW, (int)tipH);
			NihilityLedgerTheme.DrawBeveledBox(sb, tipRect, NihilityLedgerTheme.VoidBg * alpha, NihilityLedgerTheme.AcheronScarlet * alpha, 1.5f);

			sb.DrawString(font, title, new Vector2(tipPos.X + 12, tipPos.Y + 8), titleColor * alpha, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 0f);

			if (!string.IsNullOrEmpty(sub))
			{
				sb.DrawString(font, sub, new Vector2(tipPos.X + 12, tipPos.Y + 28), NihilityLedgerTheme.TextDim * alpha, 0f, Vector2.Zero, 0.78f, SpriteEffects.None, 0f);
			}

			if (!string.IsNullOrEmpty(desc))
			{
				sb.DrawString(font, desc, new Vector2(tipPos.X + 12, tipPos.Y + 48), NihilityLedgerTheme.PaperWhite * alpha, 0f, Vector2.Zero, 0.76f, SpriteEffects.None, 0f);
			}

			if (!string.IsNullOrEmpty(lore))
			{
				sb.DrawString(font, lore, new Vector2(tipPos.X + 12, tipPos.Y + 98), NihilityLedgerTheme.TextDim * alpha, 0f, Vector2.Zero, 0.70f, SpriteEffects.None, 0f);
			}

			if (!string.IsNullOrEmpty(hint))
			{
				sb.DrawString(font, hint, new Vector2(tipPos.X + 12, tipPos.Y + 122), NihilityLedgerTheme.LacquerGold * alpha, 0f, Vector2.Zero, 0.72f, SpriteEffects.None, 0f);
			}
		}
	}
}
