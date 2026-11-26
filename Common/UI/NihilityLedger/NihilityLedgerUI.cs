using System;
using System.Collections.Generic;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using WeepForTheDeparted.Common;
using WeepForTheDeparted.Common.Configs;
using WeepForTheDeparted.Common.Players;
using WeepForTheDeparted.Common.Systems;

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
		private float cardScrollOffset = 0f;
		private int outerMaxScroll = 0;
		private bool hoveredOuterScroll = false;
		private bool draggingOuterScroll = false;
		private float outerDragGrabY = 0f;
		private int outerWheelLatchTimer = 0;
		private int hoveredDescScrollCard = -1;
		private string draggingDescKey = null;
		private float descDragGrabY = 0f;

		private bool oldMouseLeft = false;
		private bool oldMouseRight = false;
		private int oldScrollWheelValue = 0;
		private float lastAnimGlobalTimer = 0f;
		public uint OpenedUpdateCount { get; private set; }
		public int JustClosedGuardTicks { get; set; }

		private const float DescTextScale = 0.72f;
		private const float LoreTextScale = 0.68f;
		private const int CardHeight = 92;
		private const int CardSpacing = 8;
		private const int GridHeaderHeight = 30;
		private static readonly RasterizerState ScissorRaster = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };

		// Layout rectangles & anchors
		private Rectangle boardRect;
		private Rectangle closeButtonRect;
		private readonly Rectangle[] socketRects = new Rectangle[3];
		private Rectangle dynamicGridPanelRect;
		private Rectangle gridViewportRect;
		private Rectangle outerScrollTrackRect;
		private Rectangle outerScrollThumbRect;
		private readonly List<CharmInfo> currentSlotCharms = new List<CharmInfo>();
		private readonly List<CardLayoutData> cardLayouts = new List<CardLayoutData>();
		private readonly Dictionary<string, float> descScrollOffsets = new Dictionary<string, float>();
		private readonly Dictionary<string, WrappedCardCacheEntry> wrappedCardCache = new Dictionary<string, WrappedCardCacheEntry>();
		private readonly Vector2[] eidolonPositions = new Vector2[NaughtPlayer.EidolonCount];
		private readonly float[] eidolonHoverEase = new float[NaughtPlayer.EidolonCount];
		private readonly float[] eidolonStampFlash = new float[NaughtPlayer.EidolonCount];
		private readonly float[] eidolonStrandFlow = new float[NaughtPlayer.EidolonCount];
		private Vector2 eidolonCenter;
		private float eidolonArcRadius;
		private int lastHoveredEidolon = -1;
		private int lastHoveredSocket = -1;
		private Vector2 standCenter;
		private Vector2 bladeDisplayPos;
		private Vector2 sheathDisplayPos;

		private sealed class CardLayoutData
		{
			public CharmInfo Charm;
			public Rectangle CardRect;
			public Rectangle DescBoxRect;
			public Rectangle VisibleDescRect;
			public Rectangle ScrollTrackRect;
			public Rectangle ScrollThumbRect;
			public List<string> DescLines;
			public List<string> LoreLines;
			public float DescContentHeight;
			public float DescMaxScroll;
		}

		private struct WrappedCardCacheEntry
		{
			public string DescText;
			public string LoreText;
			public float WrapWidth;
			public List<string> DescLines;
			public List<string> LoreLines;
		}

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
				Subtitle = "Soul Inscription · Opening Surge & Boss Milestones",
				Description = "Begin combat with at least 5 Slashed Dream & 5 Crimson Knots on the strongest foe.\nGain +2 Slashed Dream each time a boss crosses 75%, 50%, and 25% HP.",
				Lore = "\"A crimson shadow lingers by the guard; through endless kalpas, its edge dulls not.\""
			},
			new CharmInfo {
				Key = "NamelessDecoy", Slot = 0,
				Title = "Echo of the Nameless Decoy",
				Subtitle = "Soul Inscription · Clone Volley Echo",
				Description = "Each expiring shadow clone adds +1 extra 50% True Melee cycle slash and grants +1 bonus Crimson Knot per cycle (basekit: 4 x 50% slashes).",
				Lore = "\"Shadows mirror the living; when the body departs, the steel still slashes.\""
			},
			new CharmInfo {
				Key = "FleetingStep", Slot = 0,
				Title = "Fleeting Petal Step",
				Subtitle = "Soul Inscription · Evasion & Projectile Stasis",
				Description = "Flash Step cooldown reduced by 25%. Dashing through enemies restores 1 Slashed Dream and freezes hostile projectiles in place for 3s (30s cd).",
				Lore = "\"Steps like falling cherry blossoms; between blade and void, neither is touched.\""
			},
			new CharmInfo {
				Key = "MirageWeft", Slot = 0,
				Title = "Mirage Weft",
				Subtitle = "Soul Inscription · Rapid Step & Cutting Threads",
				Description = "Flash Step cooldown reduced by 50%. Active clones link in a chain to you via cutting threads dealing 25% True Melee damage every 1s.",
				Lore = "\"A thousand threads of destiny; sever them all, and only emptiness remains.\""
			},
			new CharmInfo {
				Key = "Rainleaf", Slot = 0,
				Title = "Rainleaf Phasing",
				Subtitle = "Soul Inscription · Low-HP & Fatal Void Phasing",
				Description = "Dropping to or below 25% max HP triggers 3s Void Phasing (invulnerable, +50% speed). Grants 2 Slashed Dream charges (90s cooldown).",
				Lore = "\"Like rain to water, leaves tremble in the deluge; life and demise in a breath.\""
			},
			new CharmInfo {
				Key = "StillWater", Slot = 0,
				Title = "Still Water Basin",
				Subtitle = "Soul Inscription · Disciplined Duelist Focus",
				Description = "Holding Naught without taking damage for 15s grants [Still Water]:\n+20% True Melee Crit Chance and +20% Crit Damage.",
				Lore = "\"Quiet water mirrors the blade; an unrippled mind perceives the void.\""
			},

			// ==============================
			// SLOT 1: TIDE INSCRIPTIONS (6)
			// ==============================
			new CharmInfo {
				Key = "AbyssGrip", Slot = 1,
				Title = "Grip of the Abyss",
				Subtitle = "Tide Inscription · Debuff Amplification",
				Description = "Naught hits inflict [Grip of the Abyss]: each unique debuff increases True Melee DMG received by +6% up to +24% (Calamity) or +12% up to +36% (Vanilla).",
				Lore = "\"Gaze into the abyss; all mortal suffering rings in the broken blade.\""
			},
			new CharmInfo {
				Key = "BloodLotus", Slot = 1,
				Title = "Blood Lotus Transmission",
				Subtitle = "Tide Inscription · Multi-Target Knot Bloom",
				Description = "Upon target death, Crimson Knots transfer to 2 enemies (preferring <9 stacks) and erupt in a Blood Lotus Shockwave (100% + 15%/knot True Melee DMG).",
				Lore = "\"Those who perish across the river leave grudges blooming like lotuses.\""
			},
			new CharmInfo {
				Key = "ShadowMarionette", Slot = 1,
				Title = "Shadow Marionette Tethers",
				Subtitle = "Tide Inscription · Crowd Control & Tether Share",
				Description = "Clones last indefinitely (max 4, 15s pulse) and tether up to 3 enemies, slowing/leashing non-bosses and sharing debuffs & 15%/clone True Melee DMG.",
				Lore = "\"Ethereal puppets bound by red string; one wound bleeds in three flesh.\""
			},
			new CharmInfo {
				Key = "WitheredRiver", Slot = 1,
				Title = "Scourge of the Withered River",
				Subtitle = "Tide Inscription · Debuff Attrition & Armor Peel",
				Description = "Debuffs inflicted on boss enemies last 50% longer.\nNaught hits inflict [Withered River], shredding enemy Damage Reduction by 10%.",
				Lore = "\"The waters of Lethe dry to ash; poison seeps into the bones of gods.\""
			},
			new CharmInfo {
				Key = "ShadowCrucible", Slot = 1,
				Title = "Shadow Crucible",
				Subtitle = "Tide Inscription · DoT Furnace & Cycle Detonation",
				Description = "Clones project an 18-block aura: DoTs deal 2x DMG (+35%/overlap, max 3) and brand foes with [Shadow Crucible], detonating 50% stored DoT on cycle expiry.",
				Lore = "\"Within the shadow crucible, all agony burns swifter into the void.\""
			},
			new CharmInfo {
				Key = "DepartedSiphon", Slot = 1,
				Title = "Siphon of the Departed",
				Subtitle = "Tide Inscription · Knot Detonation Lifesteal",
				Description = "Detonating Crimson Knots during Ultimate heals 2% max HP per knot (up to 18% per Ult) and grants +15% movement speed for 8s after Ult.",
				Lore = "\"Life blossoms from demise; drink of the red dew that falls upon the blade.\""
			},

			// ==============================
			// SLOT 2: RUIN INSCRIPTIONS (6)
			// ==============================
			new CharmInfo {
				Key = "ThunderCore", Slot = 2,
				Title = "Thunder Core",
				Subtitle = "Ruin Inscription · Post-Ult Buff & 9 Thunderbolts",
				Description = "After Ultimate ends, gain +15% True Melee damage for 15s and call down 9 staggered thunderbolts every 0.3s (144% DMG each, max +1 Dream/Knot).",
				Lore = "\"Thunder enters the marrow, cleaving the endless night from nothingness.\""
			},
			new CharmInfo {
				Key = "KarmaSever", Slot = 2,
				Title = "Severance of Karma",
				Subtitle = "Ruin Inscription · Defense & Boss DR Penetration",
				Description = "Stygian Resurge ignores 20% enemy defense.\nAll Ultimate strikes penetrate 10% boss DR (converted to bonus damage if unspent).",
				Lore = "\"One slash severs all karma; on the road to the underworld, there is no return.\""
			},
			new CharmInfo {
				Key = "VoidHorizon", Slot = 2,
				Title = "Rift of the Void Horizon",
				Subtitle = "Ruin Inscription · Dimensional Void Portals",
				Description = "Stygian Resurge opens 9s Void Fissures on active clones (or target), pulling non-bosses inward while dealing continuous True Melee damage.",
				Lore = "\"Space cracks like paper; the void bleeds into the waking world.\""
			},
			new CharmInfo {
				Key = "BladesongRain", Slot = 2,
				Title = "Bladesong of the Endless Rain",
				Subtitle = "Ruin Inscription · Rapid Cleave & Blossom Needles",
				Description = "Rainblades execute 35% faster. Detonated knots fan out Blood Blossom Needles (70% True Melee DMG) that pierce infinitely, seeking other foes before converging.",
				Lore = "\"Rain falls without end; each needle of blood finds its mark in the dark.\""
			},
			new CharmInfo {
				Key = "SynchronizedExecution", Slot = 2,
				Title = "Synchronized Execution",
				Subtitle = "Ruin Inscription · Clones Awaken in Time-Freeze",
				Description = "Clones pause expiry during Ult, mirroring Rainblades (45% DMG) and converging in a 115% crit cross-slash before fading into crimson petals (full DMG up to 3 clones; soft-capped at 5).",
				Lore = "\"A thousand shadows rise together, drowning the sky in a single flash.\""
			},
			new CharmInfo {
				Key = "EmancipatorWill", Slot = 2,
				Title = "Emancipator's Severed Will",
				Subtitle = "Ruin Inscription · Knot Refund & Post-Ult Crit",
				Description = "Refunds 1 Slashed Dream for every 3 Crimson Knots detonated in Ult (overflows into Overflow Dream).\nGrants +20% True Melee Crit Chance for 10 seconds once you regain control.",
				Lore = "\"The Emancipator walks alone; from endless ruin, power surges anew.\""
			}
		};

		public static readonly EidolonInfo[] Eidolons = new EidolonInfo[]
		{
			new EidolonInfo {
				Index = 0, RomanNumeral = "I",
				Title = "Silenced Sky",
				Subtitle = "Eidolon Rank I",
				Description = "True Melee hits against debuffed enemies gain +8% Crit Chance.",
				Lore = "\"Beneath the quiet canopy, the blade sings at the beginning of truth.\""
			},
			new EidolonInfo {
				Index = 1, RomanNumeral = "II",
				Title = "Mute Thunder",
				Subtitle = "Eidolon Rank II",
				Description = "While wielding Naught in combat, passively generates +1 Slashed Dream every 9 seconds.",
				Lore = "\"Silent thunder shakes the mountain; the sun sinks into stillness.\""
			},
			new EidolonInfo {
				Index = 2, RomanNumeral = "III",
				Title = "Frosty Strands",
				Subtitle = "Eidolon Rank III",
				Description = "Rainblade base damage increased to 1.70x (+0.60x per knot). Odachi combo reach increased by +15%.",
				Lore = "\"Fine rain like strands, frost sealing the edge; each slash unravels space.\""
			},
			new EidolonInfo {
				Index = 3, RomanNumeral = "IV",
				Title = "Shrine in Flame",
				Subtitle = "Eidolon Rank IV",
				Description = "Enemies within Nihility Domain take +8% increased Ultimate damage.",
				Lore = "\"Flames spread through the mirror, revealing shadows destined to fade.\""
			},
			new EidolonInfo {
				Index = 4, RomanNumeral = "V",
				Title = "Strewn Petals",
				Subtitle = "Eidolon Rank V",
				Description = "Stygian Resurge damage increased to 6.30x (+0.40x per knot). Overflow Dream cap raised to 5; Clone Cycle slashes deal 55% damage.",
				Lore = "\"The spider lily withers, petals falling like rain, leaving quietude.\""
			},
			new EidolonInfo {
				Index = 5, RomanNumeral = "VI",
				Title = "Apocalypse, the Emancipator",
				Subtitle = "Eidolon Rank VI",
				Description = "Odachi combos, Clone Cycle slashes, and EX waves count as Ultimate damage. Base Clone Cycle volley gains +1 slash.",
				Lore = "\"From the severance of thought, all is void. I sever all karma; my name is Acheron.\""
			},
			new EidolonInfo {
				Index = 6, RomanNumeral = "EX",
				Title = "Horizon's Severance",
				Subtitle = "Eidolon Rank EX",
				Description = "Odachi combo reach increased by an additional +15%. Every combo slash launches a 45% crimson spatial wave that cannot double-hit enemies struck by the blade.",
				Lore = "\"Beyond the horizon's scar, every swing parts reality itself.\""
			}
		};

		private static int ToPercent(float ratio) => (int)MathF.Round(ratio * 100f);

		private static string FormatSeconds(int ticks)
		{
			float seconds = ticks / 60f;
			if (ticks % 60 == 0)
				return ((int)seconds).ToString();
			if (ticks % 6 == 0)
				return seconds.ToString("0.#");
			return seconds.ToString("0.##");
		}

		private static string FormatUpdatesSeconds(int updates) => (updates / 120f).ToString("0.##");

		private static string FormatTiles(float pixels) => (pixels / 16f).ToString("0.##");

		private static string GetCharmDescription(in CharmInfo charm)
		{
			NaughtBalanceProfile p = WeepBalance.Active;
			bool calamityActive = WeepCompatibility.IsCalamityBalanceActive;

			switch (charm.Key)
			{
				// Soul Inscriptions (Slot 0)
				case "RedOni":
					return $"Begin each boss encounter with at least {p.RedOniOpenerFloor} Slashed Dream & {p.RedOniOpenerFloor} Crimson Knots on the strongest foe (once per encounter).\n" +
						$"Gain +{p.RedOniMilestoneDream} Slashed Dream each time a boss crosses 75%, 50%, and 25% HP.";

				case "NamelessDecoy":
					return $"Each expiring shadow clone adds +1 extra cycle slash (up to +{p.NamelessDecoyBonusSlashCap} bonus, 50% True Melee DMG; basekit: 4) and grants +1 bonus Crimson Knot per cycle.";

				case "FleetingStep":
				{
					int cdReducePct = ToPercent(1f - p.FleetingStepDashCdMult);
					float cdSec = p.DashCooldownTicks * p.FleetingStepDashCdMult / 60f;
					string slowSec = FormatSeconds(p.FleetingStepSlowTicks);
					string icdSec = FormatSeconds(p.FleetingStepCooldownTicks);
					return $"Flash Step cooldown reduced by {cdReducePct}% ({cdSec:0.##}s). Dashing through enemies restores 1 Slashed Dream and slows hostile projectiles by 50% for {slowSec}s ({icdSec}s cd).";
				}

				case "MirageWeft":
				{
					int cdReducePct = ToPercent(1f - p.MirageWeftDashCdMult);
					float cdSec = p.DashCooldownTicks * p.MirageWeftDashCdMult / 60f;
					int threadDmgPct = ToPercent(p.MirageWeftDamageMult);
					string hitCdSec = FormatSeconds(p.MirageWeftHitCooldownTicks);
					return $"Flash Step cooldown reduced by {cdReducePct}% ({cdSec:0.##}s). Active clones link in a chain to you via cutting threads dealing {threadDmgPct}% True Melee damage every {hitCdSec}s.";
				}

				case "Rainleaf":
				{
					string lethalClause = p.RainleafLethalHpRatio > 0f
						? $"surviving lethal hits at {ToPercent(p.RainleafLethalHpRatio)}% max HP"
						: "surviving lethal hits at 1 HP";
					string invulnSec = FormatSeconds(p.RainleafInvulnTicks);
					int speedPct = ToPercent(p.RainleafSpeedMult - 1f);
					string cdSec = FormatSeconds(p.RainleafCooldownTicks);
					return $"Dropping below 25% HP or {lethalClause} triggers {invulnSec}s Void Phasing (invulnerable, +{speedPct}% speed) and grants +{p.RainleafDreamGrant} Slashed Dream ({cdSec}s cooldown).";
				}

				case "StillWater":
					return $"Holding Naught without taking damage for 15s grants [Still Water]:\n" +
						$"+{(int)MathF.Round(p.StillWaterCritChance)}% True Melee Crit Chance and +{ToPercent(p.StillWaterCritDamage)}% Crit Damage.";

				// Tide Inscriptions (Slot 1)
				case "AbyssGrip":
				{
					int perPct = ToPercent(p.AbyssGripPerDebuff);
					int maxPct = perPct * p.AbyssGripMaxDebuffs;
					string activeLabel = calamityActive ? "Calamity" : "Vanilla";
					return $"Naught hits inflict [Grip of the Abyss]: each unique debuff increases True Melee DMG received by +{perPct}% (up to +{maxPct}%, {p.AbyssGripMaxDebuffs} max).\n" +
						$"[Active: {activeLabel} · Counts across all active boss segments]";
				}

				case "BloodLotus":
					return $"Upon target death, Crimson Knots transfer to 2 enemies (<9 stacks preferred) and erupt in a {FormatTiles(p.BloodLotusMaxRadiusPx)}-tile Blood Lotus Shockwave ({ToPercent(p.BloodLotusBaseMult)}% + {ToPercent(p.BloodLotusKnotMult)}%/knot True Melee DMG, {FormatSeconds(p.BloodLotusGlobalHitCooldownTicks)}s global hit CD).";

				case "ShadowMarionette":
					return $"Clones last indefinitely (max 4, 15s pulse) and tether up to 3 enemies, slowing non-bosses and sharing debuffs & {ToPercent(p.MarionetteSharePerClone)}%/clone True Melee DMG (up to {ToPercent(p.MarionetteShareCap)}%; {ToPercent(p.MarionetteUltShareScale)}% on Ultimate strikes).";

				case "WitheredRiver":
				{
					int durPct = ToPercent(p.WitheredRiverBossDurationMult - 1f);
					if (calamityActive)
					{
						int peelPct = ToPercent(p.WitheredRiverDrPeel);
						return $"Debuffs inflicted on boss enemies last {durPct}% longer.\n" +
							$"Naught hits inflict [Withered River], peeling enemy Damage Reduction by {peelPct}% (converted to +{peelPct}% DMG on 0% DR targets).";
					}
					int vanillaPct = ToPercent(p.WitheredRiverVanillaDamageMult - 1f);
					return $"Debuffs inflicted on boss enemies last {durPct}% longer.\n" +
						$"Naught hits inflict [Withered River], increasing all damage taken by +{vanillaPct}% (also peels {ToPercent(p.WitheredRiverDrPeel)}% DR).";
				}

				case "ShadowCrucible":
					return $"Clones project an 18-tile aura: DoTs deal {p.CrucibleDotAmps[0]:0.##}x/{p.CrucibleDotAmps[1]:0.##}x/{p.CrucibleDotAmps[2]:0.##}x DMG (1-3 overlaps) and brand foes with [Shadow Crucible], detonating {ToPercent(p.CruciblePopRatio)}% stored DoT on cycle expiry (min {ToPercent(p.CrucibleFloorPerDebuff)}%/debuff, cap {ToPercent(p.CrucibleWeaponCapMult)}% weapon / {ToPercent(p.CrucibleBossHpCapRatio)}% boss HP).";

				case "DepartedSiphon":
				{
					float healPerKnotPct = p.SiphonHealPerKnot * 100f;
					float maxHealPct = healPerKnotPct * 9f;
					return $"Detonating Crimson Knots during Ultimate heals {healPerKnotPct:0.#}% max HP per knot (up to {maxHealPct:0.#}% per Ult) and grants +15% movement speed for 8s after Ult.";
				}

				// Ruin Inscriptions (Slot 2)
				case "ThunderCore":
					return $"After Ultimate ends, gain +{ToPercent(p.ThunderCoreBuffDamage)}% True Melee damage for {FormatSeconds(p.ThunderCoreBuffTicks)}s and call down 9 staggered guaranteed-crit thunderbolts every 0.3s ({ToPercent(p.ThunderCoreBoltMult)}% DMG each, max +1 Dream/Knot).";

				case "KarmaSever":
				{
					int defPenPct = ToPercent(p.KarmaSeverUltDefPen);
					int resurgePct = ToPercent(p.KarmaSeverResurgeBonusMult - 1f);
					int e6ScalePct = ToPercent(p.KarmaSeverE6Scale);
					if (calamityActive)
					{
						int peelPct = ToPercent(p.KarmaSeverBossDrPeel);
						float e6PeelPct = p.KarmaSeverBossDrPeel * p.KarmaSeverE6Scale * 100f;
						return $"All Ultimate strikes ignore {defPenPct}% enemy defense and peel {peelPct}% boss DR (E6 swings: {e6ScalePct}% / {e6PeelPct:0.#}% peel).\n" +
							$"Stygian Resurge gains +{resurgePct}% bonus DMG.";
					}
					int bossPct = ToPercent(p.KarmaSeverVanillaBossMult - 1f);
					float e6BossPct = (p.KarmaSeverVanillaBossMult - 1f) * p.KarmaSeverE6Scale * 100f;
					return $"All Ultimate strikes ignore {defPenPct}% enemy defense and deal +{bossPct}% boss DMG (E6 swings: +{e6BossPct:0.#}%).\n" +
						$"Stygian Resurge gains +{resurgePct}% bonus DMG.";
				}

				case "VoidHorizon":
				{
					string driftNote = p.VoidHorizonFallbackDriftSpeed > 0f
						? $" Fallback portal drifts toward bosses ({(p.VoidHorizonFallbackDriftSpeed * 60f / 16f):0.#} tiles/s)."
						: string.Empty;
					return $"Stygian Resurge opens 9s Void Fissures ({FormatTiles(p.VoidHorizonRadiusPx)}-tile radius) on active clones (or target), pulling non-bosses and dealing {ToPercent(p.VoidHorizonDamageMult)}% True Melee DMG every 0.4s.{driftNote}";
				}

				case "BladesongRain":
					return $"Rainblades execute 35% faster. Detonated knots fan out Blood Blossom Needles ({ToPercent(p.BladesongNeedleMult)}% True Melee DMG, {FormatUpdatesSeconds(p.BladesongFlightUpdates)}s flight, max {p.BladesongMaxHitsPerNpc} hits/enemy at {FormatUpdatesSeconds(p.BladesongHitCooldownUpdates)}s CD) piercing infinitely through crowds.";

				case "SynchronizedExecution":
					return $"Clones pause expiry during Ult, mirroring Rainblades ({ToPercent(p.SyncMirrorRainbladeMult)}% DMG) and converging in a {ToPercent(p.SyncCrossSlashMult)}% crit cross-slash before fading (full DMG up to 3 clones; soft-capped at {p.SyncSoftCapClones:0.#}).";

				case "EmancipatorWill":
					return $"Refunds 1 Slashed Dream per 3 Crimson Knots detonated in Ult (up to +{p.EmancipatorMaxDreamRefund} Dream, overflows into Overflow Dream).\n" +
						$"Grants +{(int)MathF.Round(p.EmancipatorCritBonus)}% True Melee Crit Chance for 10s after regaining control.";

				default:
					return charm.Description;
			}
		}

		private static string GetEidolonDescription(in EidolonInfo eidolon)
		{
			NaughtBalanceProfile p = WeepBalance.Active;
			bool calamityActive = WeepCompatibility.IsCalamityBalanceActive;

			switch (eidolon.Index)
			{
				case 0:
					return $"True Melee hits against debuffed enemies gain +{(int)MathF.Round(p.E1CritBonus)}% Crit Chance.";
				case 1:
					return $"While wielding Naught in combat, passively generates +1 Slashed Dream every {FormatSeconds(p.E2PassiveDreamIntervalTicks)}s.";
				case 2:
					return $"Rainblade base damage increased to {p.E3RainbladeBaseMult:0.00}x (+{p.E3RainbladeKnotMult:0.00}x per knot). Odachi combo reach increased by +15%.";
				case 3:
					return "Enemies within Nihility Domain take +8% increased Ultimate damage.";
				case 4:
					return $"Stygian Resurge damage increased to {p.E5ResurgeBaseMult:0.00}x (+{p.E5ResurgeKnotMult:0.00}x per knot). Overflow Dream cap raised to 5; Clone Cycle slashes deal 55% damage.";
				case 5:
				{
					string sevNote = calamityActive
						? $"{(p.KarmaSeverBossDrPeel * p.KarmaSeverE6Scale * 100f):0.#}% Severance boss DR peel"
						: $"+{((p.KarmaSeverVanillaBossMult - 1f) * p.KarmaSeverE6Scale * 100f):0.#}% Severance boss DMG";
					return $"Odachi combos, Clone Cycle slashes, and EX waves inherit Ultimate status (full E4 +8%, {ToPercent(p.KarmaSeverE6Scale)}% Severance: {sevNote}). Base Clone Cycle volley gains +1 slash.";
				}
				case 6:
					return $"Odachi combo reach increased by an additional +15%. Every combo slash launches a {ToPercent(p.ExWaveDamageMult)}% crimson spatial wave that cannot double-hit enemies struck by the blade.";
				default:
					return eidolon.Description;
			}
		}

		private static List<string> WrapText(DynamicSpriteFont font, string text, float maxWidth, float scale)
		{
			var lines = new List<string>();
			if (font == null || string.IsNullOrEmpty(text))
				return lines;

			float safeMaxW = Math.Max(24f, maxWidth);
			string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');

			for (int p = 0; p < paragraphs.Length; p++)
			{
				string para = paragraphs[p];
				if (para.Length == 0)
				{
					lines.Add(string.Empty);
					continue;
				}

				string[] words = para.Split(' ');
				string current = string.Empty;

				for (int w = 0; w < words.Length; w++)
				{
					string word = words[w];
					string candidate = current.Length == 0 ? word : current + " " + word;

					if (font.MeasureString(candidate).X * scale <= safeMaxW)
					{
						current = candidate;
					}
					else
					{
						if (current.Length > 0)
						{
							lines.Add(current);
							current = string.Empty;
						}

						// Break single oversized token if needed so it never overflows horizontally
						if (font.MeasureString(word).X * scale > safeMaxW)
						{
							string chunk = string.Empty;
							for (int c = 0; c < word.Length; c++)
							{
								string nextChunk = chunk + word[c];
								if (chunk.Length > 0 && font.MeasureString(nextChunk).X * scale > safeMaxW)
								{
									lines.Add(chunk);
									chunk = word[c].ToString();
								}
								else
								{
									chunk = nextChunk;
								}
							}
							current = chunk;
						}
						else
						{
							current = word;
						}
					}
				}

				if (current.Length > 0)
					lines.Add(current);
			}

			return lines;
		}

		private void GetWrappedCardLines(DynamicSpriteFont font, in CharmInfo charm, float wrapWidth, out List<string> descLines, out List<string> loreLines)
		{
			string descText = GetCharmDescription(in charm);
			string loreText = charm.Lore ?? string.Empty;

			if (wrappedCardCache.TryGetValue(charm.Key, out var cached)
				&& Math.Abs(cached.WrapWidth - wrapWidth) < 0.5f
				&& cached.DescText == descText
				&& cached.LoreText == loreText)
			{
				descLines = cached.DescLines;
				loreLines = cached.LoreLines;
				return;
			}

			descLines = WrapText(font, descText, wrapWidth, DescTextScale);
			loreLines = WrapText(font, loreText, wrapWidth, LoreTextScale);
			wrappedCardCache[charm.Key] = new WrappedCardCacheEntry
			{
				DescText = descText,
				LoreText = loreText,
				WrapWidth = wrapWidth,
				DescLines = descLines,
				LoreLines = loreLines
			};
		}

		public override void SetStaticDefaults()
		{
			instance = this;
		}

		public override void OnEnterWorld()
		{
			if (IsOpen)
				Close();
			SnapOpenProgress();
			JustClosedGuardTicks = 0;
		}

		protected override void OnOpen()
		{
			Main.playerInventory = false;
			Main.ingameOptionsWindow = false;
			OpenedUpdateCount = Main.GameUpdateCount;
			JustClosedGuardTicks = 0;
			oldScrollWheelValue = Mouse.GetState().ScrollWheelValue;
			lastAnimGlobalTimer = GlobalTimer;
			outerWheelLatchTimer = 0;
			draggingOuterScroll = false;
			draggingDescKey = null;
			lastHoveredEidolon = -1;
			lastHoveredSocket = -1;
			Array.Clear(eidolonHoverEase, 0, eidolonHoverEase.Length);
			Array.Clear(eidolonStampFlash, 0, eidolonStampFlash.Length);

			var naughtPlayer = Main.LocalPlayer?.GetModPlayer<NaughtPlayer>();
			for (int k = 0; k < 5; k++)
			{
				bool both = naughtPlayer != null && naughtPlayer.IsEidolonActive(k) && naughtPlayer.IsEidolonActive(k + 1);
				eidolonStrandFlow[k] = both ? 1f : 0f;
			}
			eidolonStrandFlow[5] = (naughtPlayer != null && naughtPlayer.IsEidolonActive(2) && naughtPlayer.IsEidolonActive(6)) ? 1f : 0f;
			eidolonStrandFlow[6] = (naughtPlayer != null && naughtPlayer.IsEidolonActive(3) && naughtPlayer.IsEidolonActive(6)) ? 1f : 0f;
		}

		protected override void OnClose()
		{
			JustClosedGuardTicks = 2;
			draggingOuterScroll = false;
			draggingDescKey = null;
			outerWheelLatchTimer = 0;
			hoveredOuterScroll = false;
			hoveredDescScrollCard = -1;
			lastHoveredEidolon = -1;
			lastHoveredSocket = -1;
		}

		public void UpdateState()
		{
			Update();
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

			// Right Column anchor & Two-Tiered Katana Stand
			int rightAreaLeft = plaqueX + plaqueW + 24;
			int rightAreaRight = boardRect.Right - 24;
			float rightCenterX = (rightAreaLeft + rightAreaRight) * 0.5f;

			standCenter = new Vector2(rightCenterX, boardRect.Y + 130f);
			bladeDisplayPos = standCenter + new Vector2(0f, -22f);
			sheathDisplayPos = standCenter + new Vector2(0f, 32f);

			// Dynamic Inscription Grid Area (below the 3 sockets)
			int gridY = startY + 3 * (plaqueH + spacing) + 10;
			int gridW = plaqueW;
			int gridH = boardRect.Bottom - gridY - 18;
			dynamicGridPanelRect = new Rectangle(plaqueX, gridY, gridW, gridH);
			gridViewportRect = new Rectangle(plaqueX + 4, gridY + GridHeaderHeight, Math.Max(0, gridW - 8), Math.Max(0, gridH - GridHeaderHeight - 6));

			// Populate dynamic card rectangles for the currently selected slot
			currentSlotCharms.Clear();
			for (int c = 0; c < AvailableCharms.Length; c++)
			{
				if (AvailableCharms[c].Slot == SelectedSlot)
				{
					currentSlotCharms.Add(AvailableCharms[c]);
				}
			}

			cardLayouts.Clear();
			int cardCount = currentSlotCharms.Count;
			outerMaxScroll = 0;
			outerScrollTrackRect = Rectangle.Empty;
			outerScrollThumbRect = Rectangle.Empty;

			if (cardCount > 0)
			{
				int contentHeight = cardCount * CardHeight + (cardCount - 1) * CardSpacing;
				int visibleHeight = Math.Max(1, gridViewportRect.Height);
				outerMaxScroll = Math.Max(0, contentHeight - visibleHeight);
				cardScrollOffset = Math.Clamp(cardScrollOffset, 0f, outerMaxScroll);

				if (outerMaxScroll > 0)
				{
					outerScrollTrackRect = new Rectangle(dynamicGridPanelRect.Right - 10, gridViewportRect.Y + 2, 5, Math.Max(1, gridViewportRect.Height - 4));
					float thumbRatio = (float)visibleHeight / contentHeight;
					int thumbH = Math.Max(24, (int)(outerScrollTrackRect.Height * thumbRatio));
					int travel = Math.Max(0, outerScrollTrackRect.Height - thumbH);
					int thumbY = outerScrollTrackRect.Y + (travel > 0 ? (int)MathF.Round(travel * (cardScrollOffset / outerMaxScroll)) : 0);
					outerScrollThumbRect = new Rectangle(outerScrollTrackRect.X, thumbY, outerScrollTrackRect.Width, thumbH);
				}

				int cardW = outerMaxScroll > 0 ? gridW - 24 : gridW - 16;
				int cardStartY = gridViewportRect.Y + 2 - (int)MathF.Round(cardScrollOffset);
				DynamicSpriteFont font = FontAssets.MouseText?.Value;

				for (int k = 0; k < cardCount; k++)
				{
					CharmInfo charm = currentSlotCharms[k];
					Rectangle cardRect = new Rectangle(plaqueX + 8, cardStartY + k * (CardHeight + CardSpacing), cardW, CardHeight);
					Rectangle descBoxRect = new Rectangle(cardRect.X + 8, cardRect.Y + 30, Math.Max(20, cardRect.Width - 16), Math.Max(12, cardRect.Height - 36));
					Rectangle visibleDescRect = Rectangle.Intersect(descBoxRect, gridViewportRect);

					float wrapWidth = Math.Max(24f, descBoxRect.Width - 14f);
					GetWrappedCardLines(font, in charm, wrapWidth, out var descLines, out var loreLines);

					float lineSpacing = font != null ? font.LineSpacing : 24f;
					float descBlockH = descLines.Count * (lineSpacing * DescTextScale);
					float loreGap = (descLines.Count > 0 && loreLines.Count > 0) ? 4f : 0f;
					float loreBlockH = loreLines.Count * (lineSpacing * LoreTextScale);
					float descContentH = descBlockH + loreGap + loreBlockH + 4f;
					float descMaxScroll = Math.Max(0f, descContentH - descBoxRect.Height);

					float descScroll = descScrollOffsets.TryGetValue(charm.Key, out float savedScroll) ? savedScroll : 0f;
					descScroll = Math.Clamp(descScroll, 0f, descMaxScroll);
					descScrollOffsets[charm.Key] = descScroll;

					Rectangle scrollTrackRect = Rectangle.Empty;
					Rectangle scrollThumbRect = Rectangle.Empty;
					if (descMaxScroll > 0.5f)
					{
						scrollTrackRect = new Rectangle(descBoxRect.Right - 6, descBoxRect.Y + 2, 4, Math.Max(1, descBoxRect.Height - 4));
						float innerRatio = descBoxRect.Height / descContentH;
						int thumbH = Math.Max(14, (int)(scrollTrackRect.Height * innerRatio));
						int travel = Math.Max(0, scrollTrackRect.Height - thumbH);
						int thumbY = scrollTrackRect.Y + (travel > 0 ? (int)MathF.Round(travel * (descScroll / descMaxScroll)) : 0);
						scrollThumbRect = new Rectangle(scrollTrackRect.X, thumbY, scrollTrackRect.Width, thumbH);
					}

					cardLayouts.Add(new CardLayoutData
					{
						Charm = charm,
						CardRect = cardRect,
						DescBoxRect = descBoxRect,
						VisibleDescRect = visibleDescRect,
						ScrollTrackRect = scrollTrackRect,
						ScrollThumbRect = scrollThumbRect,
						DescLines = descLines,
						LoreLines = loreLines,
						DescContentHeight = descContentH,
						DescMaxScroll = descMaxScroll
					});
				}
			}

			// 7 Eidolon node positions: I-VI along the crown arch, EX centered at the horizon apex
			int rightAreaTop = boardRect.Y + 236;
			int rightAreaBottom = boardRect.Bottom - 20;
			eidolonCenter = new Vector2(rightCenterX, (rightAreaTop + rightAreaBottom) * 0.5f + 12f);
			float maxRadiusH = Math.Max(70f, (rightAreaBottom - rightAreaTop) * 0.38f);
			float maxRadiusW = Math.Max(90f, (rightAreaRight - rightAreaLeft) * 0.34f);
			eidolonArcRadius = MathHelper.Clamp(Math.Min(maxRadiusH, maxRadiusW), 88f, 148f);

			for (int k = 0; k < 6; k++)
			{
				float ang = MathHelper.ToRadians(-165f + k * 30f);
				eidolonPositions[k] = eidolonCenter + new Vector2((float)Math.Cos(ang) * eidolonArcRadius * 1.32f, (float)Math.Sin(ang) * eidolonArcRadius);
			}
			eidolonPositions[6] = eidolonCenter + new Vector2(0f, -eidolonArcRadius * 0.50f);
		}

		private CardLayoutData FindCardLayout(string charmKey)
		{
			if (string.IsNullOrEmpty(charmKey))
				return null;

			for (int i = 0; i < cardLayouts.Count; i++)
			{
				if (cardLayouts[i].Charm.Key == charmKey)
					return cardLayouts[i];
			}

			return null;
		}

		private static Rectangle ExpandScrollbarHitRect(Rectangle rect, Rectangle clipBounds)
		{
			if (rect == Rectangle.Empty)
				return Rectangle.Empty;

			Rectangle expanded = new Rectangle(rect.X - 4, rect.Y, rect.Width + 8, rect.Height);
			return Rectangle.Intersect(expanded, clipBounds);
		}

		public override void Update()
		{
			// Always consume scroll delta so opening the UI never applies accumulated pre-open wheel movement
			int rawWheel = Mouse.GetState().ScrollWheelValue;
			int wheel = rawWheel - oldScrollWheelValue;
			oldScrollWheelValue = rawWheel;

			float dt = Math.Clamp(GlobalTimer - lastAnimGlobalTimer, 0f, 0.05f);
			lastAnimGlobalTimer = GlobalTimer;
			float frames = dt > 0.0001f ? dt * 60f : 1f;

			if (outerWheelLatchTimer > 0)
				outerWheelLatchTimer--;

			if (OpenProgress.Current <= 0.001f && !IsOpen)
				return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
			{
				if (IsOpen) Close();
				return;
			}

			// Keep inventory closed, block world/hotbar/inventory click-through, and suppress weapon switching
			if (IsOpen)
			{
				Main.playerInventory = false;
				Main.ingameOptionsWindow = false;
				player.mouseInterface = true;
				Main.blockMouse = true;
				UIInputGuard.SuppressWeaponSwitch();
				PlayerInput.LockVanillaMouseScroll("WeepForTheDeparted/NihilityLedger");
			}
			else if (OpenProgress.Current > 0.01f)
			{
				player.mouseInterface = true;
				Main.blockMouse = true;
			}

			// Recalculate layout in UI coordinate space
			RecalculateLayout();

			Vector2 mouse = MousePosition;
			Point mousePoint = mouse.ToPoint();
			var naughtPlayer = player.GetModPlayer<NaughtPlayer>();

			// Maintain active scrollbar dragging
			if (draggingOuterScroll)
			{
				if (!Main.mouseLeft || outerMaxScroll <= 0)
				{
					draggingOuterScroll = false;
				}
				else
				{
					float travel = outerScrollTrackRect.Height - outerScrollThumbRect.Height;
					if (travel > 0f)
					{
						float ratio = MathHelper.Clamp((mouse.Y - outerDragGrabY - outerScrollTrackRect.Y) / travel, 0f, 1f);
						cardScrollOffset = ratio * outerMaxScroll;
						RecalculateLayout();
					}
				}
			}
			else if (draggingDescKey != null)
			{
				CardLayoutData draggedCard = FindCardLayout(draggingDescKey);
				if (!Main.mouseLeft || draggedCard == null || draggedCard.DescMaxScroll <= 0.5f)
				{
					draggingDescKey = null;
				}
				else
				{
					float travel = draggedCard.ScrollTrackRect.Height - draggedCard.ScrollThumbRect.Height;
					if (travel > 0f)
					{
						float ratio = MathHelper.Clamp((mouse.Y - descDragGrabY - draggedCard.ScrollTrackRect.Y) / travel, 0f, 1f);
						descScrollOffsets[draggingDescKey] = ratio * draggedCard.DescMaxScroll;
						RecalculateLayout();
					}
				}
			}

			// Detect hover states (only while open and mostly revealed)
			bool inputReady = IsOpen && OpenProgress.Current > 0.25f;
			HoveredClose = inputReady && closeButtonRect.Contains(mousePoint);

			HoveredSocket = -1;
			if (inputReady)
			{
				for (int i = 0; i < 3; i++)
				{
					if (socketRects[i].Contains(mousePoint))
						HoveredSocket = i;
				}
			}
			if (HoveredSocket != lastHoveredSocket)
			{
				if (HoveredSocket >= 0)
					SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.1f, Volume = 0.28f });
				lastHoveredSocket = HoveredSocket;
			}

			hoveredOuterScroll = inputReady && outerMaxScroll > 0 && ExpandScrollbarHitRect(outerScrollTrackRect, dynamicGridPanelRect).Contains(mousePoint);
			HoveredTraceCard = -1;
			hoveredDescScrollCard = -1;

			if (inputReady && dynamicGridPanelRect.Contains(mousePoint))
			{
				CardLayoutData hoveredDescBoxCard = null;
				if (gridViewportRect.Contains(mousePoint) && !hoveredOuterScroll)
				{
					for (int k = 0; k < cardLayouts.Count; k++)
					{
						CardLayoutData card = cardLayouts[k];
						if (card.VisibleDescRect.Contains(mousePoint))
							hoveredDescBoxCard = card;

						if (card.CardRect.Contains(mousePoint))
						{
							if (card.DescMaxScroll > 0.5f && ExpandScrollbarHitRect(card.ScrollTrackRect, gridViewportRect).Contains(mousePoint))
								hoveredDescScrollCard = k;
							else
								HoveredTraceCard = k;
						}
					}
				}

				if (wheel != 0 && !draggingOuterScroll && draggingDescKey == null)
				{
					int dir = -Math.Sign(wheel);
					if (outerWheelLatchTimer > 0 && outerMaxScroll > 0)
					{
						cardScrollOffset = Math.Clamp(cardScrollOffset + dir * 36f, 0f, outerMaxScroll);
						outerWheelLatchTimer = 20;
					}
					else if (hoveredDescBoxCard != null && hoveredDescBoxCard.DescMaxScroll > 0.5f)
					{
						string key = hoveredDescBoxCard.Charm.Key;
						float cur = descScrollOffsets.TryGetValue(key, out float v) ? v : 0f;
						bool canScrollCard = (dir > 0 && cur < hoveredDescBoxCard.DescMaxScroll - 0.5f) || (dir < 0 && cur > 0.5f);
						if (canScrollCard)
						{
							descScrollOffsets[key] = Math.Clamp(cur + dir * 24f, 0f, hoveredDescBoxCard.DescMaxScroll);
						}
						else if (outerMaxScroll > 0)
						{
							cardScrollOffset = Math.Clamp(cardScrollOffset + dir * 36f, 0f, outerMaxScroll);
							outerWheelLatchTimer = 20;
						}
					}
					else if (outerMaxScroll > 0)
					{
						cardScrollOffset = Math.Clamp(cardScrollOffset + dir * 36f, 0f, outerMaxScroll);
						outerWheelLatchTimer = 20;
					}
					RecalculateLayout();
				}
			}

			HoveredEidolon = -1;
			if (inputReady)
			{
				for (int k = 0; k < NaughtPlayer.EidolonCount; k++)
				{
					if (Vector2.Distance(mouse, eidolonPositions[k]) <= 26f)
					{
						HoveredEidolon = k;
						break;
					}
				}
			}
			if (HoveredEidolon != lastHoveredEidolon)
			{
				if (HoveredEidolon >= 0)
					SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.25f, Volume = 0.30f });
				lastHoveredEidolon = HoveredEidolon;
			}

			// Smooth Eidolon hover, stamp ripple, and strand flow animations
			for (int k = 0; k < NaughtPlayer.EidolonCount; k++)
			{
				float target = (HoveredEidolon == k) ? 1f : 0f;
				float lerpRate = 1f - MathF.Pow(1f - (target > eidolonHoverEase[k] ? 0.24f : 0.14f), frames);
				eidolonHoverEase[k] += (target - eidolonHoverEase[k]) * lerpRate;

				if (eidolonStampFlash[k] > 0f)
					eidolonStampFlash[k] = Math.Max(0f, eidolonStampFlash[k] - 0.035f * frames);
			}

			if (naughtPlayer != null)
			{
				for (int k = 0; k < 5; k++)
				{
					bool both = naughtPlayer.IsEidolonActive(k) && naughtPlayer.IsEidolonActive(k + 1);
					if (both)
						eidolonStrandFlow[k] = Math.Min(1f, eidolonStrandFlow[k] + 0.05f * frames);
					else
						eidolonStrandFlow[k] = Math.Max(0f, eidolonStrandFlow[k] - 0.08f * frames);
				}

				bool exLeft = naughtPlayer.IsEidolonActive(2) && naughtPlayer.IsEidolonActive(6);
				eidolonStrandFlow[5] = exLeft
					? Math.Min(1f, eidolonStrandFlow[5] + 0.05f * frames)
					: Math.Max(0f, eidolonStrandFlow[5] - 0.08f * frames);

				bool exRight = naughtPlayer.IsEidolonActive(3) && naughtPlayer.IsEidolonActive(6);
				eidolonStrandFlow[6] = exRight
					? Math.Min(1f, eidolonStrandFlow[6] + 0.05f * frames)
					: Math.Max(0f, eidolonStrandFlow[6] - 0.08f * frames);
			}

			// Click detection (edge-triggered once per press)
			bool leftJustPressed = inputReady && ((keyLeftPressState == KeyPressState.Pressed) || (Main.mouseLeft && !oldMouseLeft));
			bool rightJustPressed = inputReady && ((keyRightPressState == KeyPressState.Pressed) || (Main.mouseRight && !oldMouseRight));

			if (leftJustPressed && !draggingOuterScroll && draggingDescKey == null)
			{
				Main.mouseLeftRelease = false;

				// Spawn visual ink ripple at click position
				ripples.Add(new ClickRipple {
					Pos = mouse, Radius = 4f, MaxRadius = 65f, Alpha = 0.85f
				});

				// 1. Close button clicked
				if (HoveredClose)
				{
					Close();
					SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.35f, Volume = 0.85f });
					oldMouseLeft = Main.mouseLeft;
					oldMouseRight = Main.mouseRight;
					return;
				}

				// 2. Click outside the board to close
				if (!boardRect.Contains(mousePoint))
				{
					Close();
					SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.35f, Volume = 0.85f });
					oldMouseLeft = Main.mouseLeft;
					oldMouseRight = Main.mouseRight;
					return;
				}

				// 2b. Outer scrollbar drag/jump
				if (hoveredOuterScroll && outerMaxScroll > 0)
				{
					draggingOuterScroll = true;
					Rectangle thumbHit = ExpandScrollbarHitRect(outerScrollThumbRect, dynamicGridPanelRect);
					if (thumbHit.Contains(mousePoint))
					{
						outerDragGrabY = mouse.Y - outerScrollThumbRect.Y;
					}
					else
					{
						outerDragGrabY = outerScrollThumbRect.Height * 0.5f;
						float travel = outerScrollTrackRect.Height - outerScrollThumbRect.Height;
						if (travel > 0f)
						{
							float ratio = MathHelper.Clamp((mouse.Y - outerDragGrabY - outerScrollTrackRect.Y) / travel, 0f, 1f);
							cardScrollOffset = ratio * outerMaxScroll;
							RecalculateLayout();
						}
					}
					oldMouseLeft = Main.mouseLeft;
					oldMouseRight = Main.mouseRight;
					return;
				}

				// 2c. Card description scrollbar drag/jump
				if (hoveredDescScrollCard >= 0 && hoveredDescScrollCard < cardLayouts.Count)
				{
					CardLayoutData scrollCard = cardLayouts[hoveredDescScrollCard];
					if (scrollCard.DescMaxScroll > 0.5f)
					{
						draggingDescKey = scrollCard.Charm.Key;
						Rectangle thumbHit = ExpandScrollbarHitRect(scrollCard.ScrollThumbRect, gridViewportRect);
						if (thumbHit.Contains(mousePoint))
						{
							descDragGrabY = mouse.Y - scrollCard.ScrollThumbRect.Y;
						}
						else
						{
							descDragGrabY = scrollCard.ScrollThumbRect.Height * 0.5f;
							float travel = scrollCard.ScrollTrackRect.Height - scrollCard.ScrollThumbRect.Height;
							if (travel > 0f)
							{
								float ratio = MathHelper.Clamp((mouse.Y - descDragGrabY - scrollCard.ScrollTrackRect.Y) / travel, 0f, 1f);
								descScrollOffsets[draggingDescKey] = ratio * scrollCard.DescMaxScroll;
								RecalculateLayout();
							}
						}
						oldMouseLeft = Main.mouseLeft;
						oldMouseRight = Main.mouseRight;
						return;
					}
				}

				// 3. Click an Inscription Socket to select it & open its dynamic trace grid!
				if (HoveredSocket >= 0 && HoveredSocket < 3)
				{
					if (SelectedSlot != HoveredSocket)
					{
						SelectedSlot = HoveredSocket;
						cardScrollOffset = 0f;
						outerWheelLatchTimer = 0;
						draggingOuterScroll = false;
						draggingDescKey = null;
					}
					SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.2f, Volume = 0.8f });
				}

				// 4. Click a dynamic Trace Card to equip it to the selected socket (only if unsealed)
				if (HoveredTraceCard >= 0 && HoveredTraceCard < currentSlotCharms.Count && naughtPlayer != null)
				{
					var charm = currentSlotCharms[HoveredTraceCard];
					if (BossTrackerSystem.IsCharmUnlocked(charm.Key))
					{
						naughtPlayer.EquippedCharms[charm.Slot] = charm.Key;
						SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.15f, Volume = 0.95f });
					}
					else
					{
						SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.25f, Volume = 0.55f });
					}
				}

				// 5. Click unlocked Eidolon node to toggle active state
				if (HoveredEidolon >= 0 && HoveredEidolon < NaughtPlayer.EidolonCount && naughtPlayer != null)
				{
					if (naughtPlayer.IsEidolonUnlocked(HoveredEidolon))
					{
						bool nowEnabled = !naughtPlayer.EnabledEidolons[HoveredEidolon];
						naughtPlayer.EnabledEidolons[HoveredEidolon] = nowEnabled;
						if (nowEnabled)
						{
							eidolonStampFlash[HoveredEidolon] = 1f;
							SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.15f + HoveredEidolon * 0.08f, Volume = 0.9f });
						}
						else
						{
							SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.2f, Volume = 0.7f });
						}
					}
					else
					{
						SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.25f, Volume = 0.55f });
					}
				}
			}

			// Right click on socket or equipped card to unequip
			if (rightJustPressed && naughtPlayer != null)
			{
				Main.mouseRightRelease = false;

				if (HoveredSocket >= 0 && HoveredSocket < 3)
				{
					if (!string.IsNullOrEmpty(naughtPlayer.EquippedCharms[HoveredSocket]))
					{
						naughtPlayer.EquippedCharms[HoveredSocket] = "";
						SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.2f, Volume = 0.75f });
					}
				}
				else if (HoveredTraceCard >= 0 && HoveredTraceCard < currentSlotCharms.Count)
				{
					var charm = currentSlotCharms[HoveredTraceCard];
					if (naughtPlayer.EquippedCharms[charm.Slot] == charm.Key)
					{
						naughtPlayer.EquippedCharms[charm.Slot] = "";
						SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.2f, Volume = 0.75f });
					}
				}
			}

			// Update state history
			oldMouseLeft = Main.mouseLeft;
			oldMouseRight = Main.mouseRight;

			// Ambient ash particles in UI space
			if (Main.rand.NextFloat() < 0.33f * frames)
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
				p.Pos += p.Vel * frames;
				if (p.Pos.Y > UIScreenH + 10 || p.Pos.X < -10)
					ashParticles.RemoveAt(i);
			}

			// Update click ripples
			float rippleDecay = MathF.Pow(0.91f, frames);
			for (int i = ripples.Count - 1; i >= 0; i--)
			{
				var r = ripples[i];
				r.Radius += ((r.MaxRadius - r.Radius) * 0.12f + 1.2f) * frames;
				r.Alpha *= rippleDecay;
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

			bool sandboxUnsealed = ModContent.GetInstance<WeepConfig>()?.UnlockAllInscriptionsAndEidolons == true;
			bool calamityActive = WeepCompatibility.IsCalamityBalanceActive;
			string profileTag = calamityActive ? "[CALAMITY PROFILE]" : "[VANILLA PROFILE]";
			Color profileColor = calamityActive ? NihilityLedgerTheme.AcheronScarlet : NihilityLedgerTheme.VioletGlow;

			int rightTagX = closeButtonRect.X - 12;

			if (sandboxUnsealed)
			{
				const string sandboxTag = "[SANDBOX UNSEALED]";
				Vector2 tagSize = fontText.MeasureString(sandboxTag) * 0.78f;
				int tagBoxW = (int)tagSize.X + 16;
				Rectangle tagRect = new Rectangle(rightTagX - tagBoxW, boardRect.Y + 20, tagBoxW, 26);
				NihilityLedgerTheme.DrawBeveledBox(sb, tagRect, NihilityLedgerTheme.LacquerBlack * (0.9f * alpha), NihilityLedgerTheme.LacquerGold * alpha, 1.5f);
				sb.DrawString(fontText, sandboxTag, new Vector2(tagRect.X + 8, tagRect.Y + 4), NihilityLedgerTheme.LacquerGold * alpha, 0f, Vector2.Zero, 0.78f, SpriteEffects.None, 0f);
				rightTagX = tagRect.X - 8;
			}

			Vector2 profileSize = fontText.MeasureString(profileTag) * 0.78f;
			int profileBoxW = (int)profileSize.X + 16;
			Rectangle profileRect = new Rectangle(rightTagX - profileBoxW, boardRect.Y + 20, profileBoxW, 26);
			NihilityLedgerTheme.DrawBeveledBox(sb, profileRect, NihilityLedgerTheme.LacquerBlack * (0.9f * alpha), profileColor * alpha, 1.5f);
			sb.DrawString(fontText, profileTag, new Vector2(profileRect.X + 8, profileRect.Y + 4), profileColor * alpha, 0f, Vector2.Zero, 0.78f, SpriteEffects.None, 0f);

			// Dividing crimson tapered slash line
			Vector2 lineStart = new Vector2(boardRect.X + 20, boardRect.Y + 70);
			Vector2 lineEnd = new Vector2(boardRect.Right - 20, boardRect.Y + 70);
			NihilityLedgerTheme.DrawTaperedSlash(sb, lineStart, lineEnd, 3.0f, -1.2f, 0.9f * alpha, alpha);

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
				bool charmWorldUnlocked = !hasCharm || BossTrackerSystem.IsCharmUnlocked(equippedKey);

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

				Color borderCol = !charmWorldUnlocked
					? Color.Lerp(NihilityLedgerTheme.LacquerGold, NihilityLedgerTheme.TextDisabled, 0.35f) * alpha
					: (isSelected
						? NihilityLedgerTheme.AcheronScarlet * alpha
						: (hovered ? NihilityLedgerTheme.HotWhite * alpha : NihilityLedgerTheme.LacquerBorder * alpha));

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
					if (charmWorldUnlocked)
					{
						sb.DrawString(font, $"[Inscribed] {equippedInfo.Title}", statusPos, NihilityLedgerTheme.AcheronScarlet * alpha, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
					}
					else
					{
						sb.DrawString(font, $"[Sealed in current world] {equippedInfo.Title}", statusPos, NihilityLedgerTheme.LacquerGold * (0.85f * alpha), 0f, Vector2.Zero, 0.80f, SpriteEffects.None, 0f);
					}
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
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			// Main dynamic panel box
			NihilityLedgerTheme.DrawBeveledBox(sb, dynamicGridPanelRect, NihilityLedgerTheme.VoidBg * alpha, NihilityLedgerTheme.LacquerBorder * alpha, 1.5f);

			string[] slotTitles = new string[] { "Soul Inscriptions", "Tide Inscriptions", "Ruin Inscriptions" };
			string header = $"Available {slotTitles[SelectedSlot]} (Click card to Inscribe)";
			sb.DrawString(font, header, new Vector2(dynamicGridPanelRect.X + 12, dynamicGridPanelRect.Y + 8), NihilityLedgerTheme.PaperWhite * alpha, 0f, Vector2.Zero, 0.82f, SpriteEffects.None, 0f);

			if (gridViewportRect.Width > 0 && gridViewportRect.Height > 0)
			{
				Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;
				Rectangle panelClip = InnoVault.VaultUtils.GetClippingRectangle(sb, gridViewportRect);
				sb.End();
				sb.GraphicsDevice.ScissorRectangle = panelClip;
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.SamplerStateForCursor, DepthStencilState.None, ScissorRaster, null, Main.UIScaleMatrix);

				for (int k = 0; k < cardLayouts.Count; k++)
				{
					CardLayoutData card = cardLayouts[k];
					Rectangle cardRect = card.CardRect;
					if (cardRect.Bottom <= gridViewportRect.Y || cardRect.Top >= gridViewportRect.Bottom)
						continue;

					CharmInfo charm = card.Charm;
					bool isUnlocked = BossTrackerSystem.IsCharmUnlocked(charm.Key);
					bool isEquipped = player.EquippedCharms[charm.Slot] == charm.Key;
					bool hovered = (HoveredTraceCard == k);

					Color cardBg = !isUnlocked
						? NihilityLedgerTheme.VoidBg * (0.78f * alpha)
						: (isEquipped
							? NihilityLedgerTheme.DeepBurgundy * (0.80f * alpha)
							: (hovered ? NihilityLedgerTheme.LacquerBlack * alpha : NihilityLedgerTheme.DarkShadow * (0.85f * alpha)));

					Color cardBorder = !isUnlocked
						? (hovered ? NihilityLedgerTheme.LacquerGold * (0.75f * alpha) : NihilityLedgerTheme.TextDisabled * (0.55f * alpha))
						: (isEquipped
							? NihilityLedgerTheme.AcheronScarlet * alpha
							: (hovered ? NihilityLedgerTheme.HotWhite * alpha : NihilityLedgerTheme.LacquerBorder * alpha));

					NihilityLedgerTheme.DrawBeveledBox(sb, cardRect, cardBg, cardBorder, hovered ? 2.2f : 1.2f);

					// Title & status badge
					float titleOffsetX = 10f;
					if (!isUnlocked)
					{
						Vector2 sealPos = new Vector2(cardRect.X + 14, cardRect.Y + 14);
						NihilityLedgerTheme.DrawDiamond(sb, sealPos, 5.2f, NihilityLedgerTheme.LacquerBlack * alpha, NihilityLedgerTheme.TextDisabled * alpha, 0f, 1.2f);
						titleOffsetX = 24f;
					}

					Vector2 titlePos = new Vector2(cardRect.X + titleOffsetX, cardRect.Y + 6);
					Color titleCol = !isUnlocked
						? (hovered ? NihilityLedgerTheme.TextDim : NihilityLedgerTheme.TextDisabled)
						: (isEquipped ? NihilityLedgerTheme.AcheronScarlet : (hovered ? NihilityLedgerTheme.HotWhite : NihilityLedgerTheme.PaperWhite));
					sb.DrawString(font, charm.Title, titlePos, titleCol * alpha, 0f, Vector2.Zero, 0.86f, SpriteEffects.None, 0f);

					string statusBadge = !isUnlocked
						? (isEquipped ? "[SEALED · EQUIPPED]" : "[SEALED]")
						: (isEquipped ? "[INSCRIBED]" : (hovered ? "[CLICK TO INSCRIBE]" : "[AVAILABLE]"));
					Color badgeCol = !isUnlocked
						? (hovered ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.TextDisabled)
						: (isEquipped ? NihilityLedgerTheme.AcheronScarlet : (hovered ? NihilityLedgerTheme.HotWhite : NihilityLedgerTheme.TextDim));
					Vector2 badgeSize = font.MeasureString(statusBadge) * 0.70f;
					sb.DrawString(font, statusBadge, new Vector2(cardRect.Right - badgeSize.X - 10, cardRect.Y + 7), badgeCol * alpha, 0f, Vector2.Zero, 0.70f, SpriteEffects.None, 0f);

					// Subtle divider and recessed description box backdrop
					sb.Draw(pixel, new Rectangle(cardRect.X + 8, cardRect.Y + 27, cardRect.Width - 16, 1), NihilityLedgerTheme.LacquerBorder * ((isUnlocked ? 0.55f : 0.30f) * alpha));
					sb.Draw(pixel, card.DescBoxRect, NihilityLedgerTheme.VoidBg * (0.42f * alpha));

					// Scissor-clipped scrollable description & lore text
					if (card.VisibleDescRect.Width > 0 && card.VisibleDescRect.Height > 0)
					{
						sb.End();
						sb.GraphicsDevice.ScissorRectangle = InnoVault.VaultUtils.GetClippingRectangle(sb, card.VisibleDescRect);
						sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.SamplerStateForCursor, DepthStencilState.None, ScissorRaster, null, Main.UIScaleMatrix);

						float descScroll = descScrollOffsets.TryGetValue(charm.Key, out float s) ? s : 0f;
						float textX = card.DescBoxRect.X + 4;
						float textY = card.DescBoxRect.Y + 2 - descScroll;
						float textFade = isUnlocked ? 1f : 0.50f;
						DrawWrappedLines(sb, font, card.DescLines, textX, ref textY, DescTextScale, NihilityLedgerTheme.PaperWhite * (0.92f * textFade * alpha), card.VisibleDescRect.Y, card.VisibleDescRect.Bottom);
						if (card.DescLines.Count > 0 && card.LoreLines.Count > 0)
							textY += 4f;
						DrawWrappedLines(sb, font, card.LoreLines, textX, ref textY, LoreTextScale, NihilityLedgerTheme.TextDim * (textFade * alpha), card.VisibleDescRect.Y, card.VisibleDescRect.Bottom);

						sb.End();
						sb.GraphicsDevice.ScissorRectangle = panelClip;
						sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.SamplerStateForCursor, DepthStencilState.None, ScissorRaster, null, Main.UIScaleMatrix);
					}

					// Per-card description scrollbar when description overflows its box
					if (card.DescMaxScroll > 0.5f && card.ScrollTrackRect != Rectangle.Empty)
					{
						bool activeInnerScroll = (hoveredDescScrollCard == k) || (draggingDescKey == charm.Key);
						sb.Draw(pixel, card.ScrollTrackRect, NihilityLedgerTheme.LacquerBlack * (0.9f * alpha));
						Color thumbCol = activeInnerScroll ? NihilityLedgerTheme.HotWhite : NihilityLedgerTheme.AcheronScarlet;
						sb.Draw(pixel, card.ScrollThumbRect, thumbCol * (0.9f * alpha));
					}
				}

				sb.End();
				sb.GraphicsDevice.ScissorRectangle = prevScissor;
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.SamplerStateForCursor, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
			}

			// Draw outer panel scrollbar if cards exceed panel height
			if (outerMaxScroll > 0 && outerScrollTrackRect != Rectangle.Empty)
			{
				bool activeOuter = hoveredOuterScroll || draggingOuterScroll;
				sb.Draw(pixel, outerScrollTrackRect, NihilityLedgerTheme.LacquerBlack * (0.85f * alpha));
				Color thumbCol = activeOuter ? NihilityLedgerTheme.HotWhite : NihilityLedgerTheme.AcheronScarlet;
				sb.Draw(pixel, outerScrollThumbRect, thumbCol * (0.9f * alpha));
			}
		}

		private static float HeartBeat(float time)
		{
			float f = time * 0.55f;
			f -= MathF.Floor(f);
			float b1 = MathF.Exp(-(f - 0.10f) * (f - 0.10f) * 340f);
			float b2 = MathF.Exp(-(f - 0.26f) * (f - 0.26f) * 300f) * 0.62f;
			return MathHelper.Clamp(b1 + b2, 0f, 1f);
		}

		private void DrawEidolonConstellation(SpriteBatch sb, NaughtPlayer player, float alpha)
		{
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			float time = GlobalTimer;

			int unlockedCount = 0;
			int activeCount = 0;
			for (int k = 0; k < NaughtPlayer.EidolonCount; k++)
			{
				if (player.IsEidolonUnlocked(k))
					unlockedCount++;
				if (player.IsEidolonActive(k))
					activeCount++;
			}
			bool allActive = activeCount >= NaughtPlayer.EidolonCount;
			bool exActive = player.IsEidolonActive(6);
			float beat = allActive ? HeartBeat(time) : 0f;

			Vector2 discCenter = eidolonCenter;
			Vector2 coreCenter = eidolonCenter + new Vector2(0f, 18f);
			float discRadius = eidolonArcRadius * 1.16f;

			// 1. Lacquer Astrolabe Backdrop Disc & Sacred Geometry Rings
			NihilityLedgerTheme.DrawSoftGlow(sb, coreCenter, discRadius * (1.15f + beat * 0.08f),
				allActive ? NihilityLedgerTheme.AcheronScarlet : NihilityLedgerTheme.NihilityViolet,
				alpha * (0.28f + activeCount * 0.045f + beat * 0.14f));

			NihilityLedgerTheme.DrawFilledCircle(sb, discCenter + new Vector2(3f, 5f), discRadius, Color.Black * (0.50f * alpha), 48);
			NihilityLedgerTheme.DrawFilledCircle(sb, discCenter, discRadius, NihilityLedgerTheme.LacquerBlack * (0.82f * alpha), 48);
			NihilityLedgerTheme.DrawCircle(sb, discCenter, discRadius, NihilityLedgerTheme.LacquerBorder * (0.95f * alpha), 2.5f, 56);
			NihilityLedgerTheme.DrawCircle(sb, discCenter, discRadius * 0.965f, NihilityLedgerTheme.LacquerGold * (0.42f * alpha), 1.1f, 56);
			NihilityLedgerTheme.DrawCircle(sb, discCenter, discRadius * 0.72f, NihilityLedgerTheme.DeepBurgundy * (0.55f * alpha), 1.2f, 44);

			// Rotating inner astrolabe segments & outer horological tick marks
			for (int seg = 0; seg < 6; seg++)
			{
				float a0 = time * 0.16f + seg * (MathHelper.TwoPi / 6f);
				NihilityLedgerTheme.DrawArc(sb, discCenter, discRadius * 0.84f, 1.3f,
					NihilityLedgerTheme.AcheronScarlet * ((0.22f + activeCount * 0.035f) * alpha), a0, a0 + 0.55f, 12);
			}

			for (int tick = 0; tick < 24; tick++)
			{
				float a = tick * (MathHelper.TwoPi / 24f);
				Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
				bool major = (tick % 4 == 0);
				float rIn = discRadius * (major ? 0.91f : 0.94f);
				float rOut = discRadius * 0.985f;
				Color tickCol = major ? NihilityLedgerTheme.LacquerGold * (0.52f * alpha) : NihilityLedgerTheme.TextDisabled * (0.35f * alpha);
				NihilityLedgerTheme.DrawLine(sb, discCenter + dir * rIn, discCenter + dir * rOut, tickCol, major ? 1.5f : 1f);
			}

			// 2. Section Header with Tapered Slash Underline
			float headerY = eidolonCenter.Y - eidolonArcRadius - 44f;
			string title = "DEPARTED EIDOLONS · E1 – E6 + EX";
			Vector2 titleSize = font.MeasureString(title) * 0.92f;
			Vector2 titlePos = new Vector2(eidolonCenter.X - titleSize.X * 0.5f, headerY);
			sb.DrawString(font, title, titlePos + new Vector2(1f, 1.5f), Color.Black * (0.85f * alpha), 0f, Vector2.Zero, 0.92f, SpriteEffects.None, 0f);
			sb.DrawString(font, title, titlePos, (allActive ? NihilityLedgerTheme.HotWhite : NihilityLedgerTheme.PaperWhite) * alpha, 0f, Vector2.Zero, 0.92f, SpriteEffects.None, 0f);
			NihilityLedgerTheme.DrawTaperedSlash(sb,
				new Vector2(eidolonCenter.X - titleSize.X * 0.58f, headerY + titleSize.Y + 1f),
				new Vector2(eidolonCenter.X + titleSize.X * 0.58f, headerY + titleSize.Y + 1f),
				2.4f, -0.8f, 0.85f * alpha, alpha);

			// 3. Radial Resonance Tethers from Central Seal to Each Eidolon Node
			for (int k = 0; k < NaughtPlayer.EidolonCount; k++)
			{
				bool active = player.IsEidolonActive(k);
				bool unlocked = player.IsEidolonUnlocked(k);
				float hover = eidolonHoverEase[k];
				if (active)
				{
					Color startCol = (k == 6 ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.DeepBurgundy) * (0.55f * alpha);
					Color endCol = Color.Lerp(k == 6 ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.AcheronScarlet, NihilityLedgerTheme.HotWhite, hover * 0.45f) * ((0.72f + beat * 0.25f) * alpha);
					NihilityLedgerTheme.DrawGradientLine(sb, coreCenter, eidolonPositions[k], startCol, endCol, 1.4f + hover * 0.7f, 10);
				}
				else
				{
					Color dormantCol = (unlocked ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.LacquerBorder) * ((0.24f + hover * 0.32f) * alpha);
					NihilityLedgerTheme.DrawLine(sb, coreCenter, eidolonPositions[k], dormantCol, 1f);
				}
			}

			// 4. Central Nihility Resonance Core (6-Petal Slashed Dream Emblem)
			Texture2D texLit = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/UI/SlashedDreamPetalLit", AssetRequestMode.ImmediateLoad).Value;
			Texture2D texUnlit = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/UI/SlashedDreamPetalUnlit", AssetRequestMode.ImmediateLoad).Value;
			Texture2D texCenter = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/UI/SlashedDreamFlowerCenter", AssetRequestMode.ImmediateLoad).Value;
			Texture2D texAwakened = ModContent.Request<Texture2D>("WeepForTheDeparted/Content/UI/SlashedDreamFlowerAwakened", AssetRequestMode.ImmediateLoad).Value;

			float coreBaseScale = MathHelper.Clamp(eidolonArcRadius / 138f, 0.68f, 1.0f) * 0.30f * (1f + beat * 0.08f);
			Vector2 texOrigin = new Vector2(128f, 128f);

			if (texLit != null && texUnlit != null)
			{
				for (int k = 0; k < 6; k++)
				{
					float rot = MathHelper.ToRadians(k * 60f) + time * 0.06f;
					bool lit = player.IsEidolonActive(k);
					float hover = eidolonHoverEase[k];
					Vector2 dir = new Vector2((float)Math.Sin(rot), -(float)Math.Cos(rot));

					if (lit)
					{
						float wave = (float)Math.Sin(time * 3.2f - k * 0.85f);
						Color petalCol = Color.Lerp(NihilityLedgerTheme.NihilityViolet, NihilityLedgerTheme.AcheronScarlet, k / 5f);
						petalCol = Color.Lerp(petalCol, NihilityLedgerTheme.HotWhite, Math.Max(0f, wave) * 0.35f + hover * 0.3f + beat * 0.25f);
						Vector2 drawPos = coreCenter + dir * (3f + wave * 2.2f + hover * 4f);
						sb.Draw(texLit, drawPos, null, petalCol * alpha, rot, texOrigin, coreBaseScale * (1.02f + hover * 0.1f), SpriteEffects.None, 0f);
					}
					else
					{
						Color unlitCol = Color.Lerp(NihilityLedgerTheme.TextDisabled, NihilityLedgerTheme.PaperWhite, hover * 0.55f);
						sb.Draw(texUnlit, coreCenter + dir * (hover * 3f), null, unlitCol * (0.48f * alpha), rot, texOrigin, coreBaseScale * 0.96f, SpriteEffects.None, 0f);
					}
				}
			}

			if (texCenter != null)
			{
				Color centerCol = allActive
					? Color.Lerp(NihilityLedgerTheme.HotWhite, NihilityLedgerTheme.AcheronScarlet, beat * 0.5f)
					: NihilityLedgerTheme.PaperWhite;
				sb.Draw(texCenter, coreCenter, null, centerCol * alpha, -time * 0.08f, texOrigin, coreBaseScale * 1.04f, SpriteEffects.None, 0f);
			}

			if ((allActive || exActive) && texAwakened != null)
			{
				Color awkCol = exActive && !allActive ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.AcheronScarlet;
				sb.Draw(texAwakened, coreCenter, null, awkCol * ((0.55f + beat * 0.4f) * alpha),
					time * 0.18f, texOrigin, coreBaseScale * (1.14f + beat * 0.08f), SpriteEffects.None, 0f);
			}

			// Orbiting resonance motes around core when all 7 Eidolons are active
			if (allActive)
			{
				for (int i = 0; i < 3; i++)
				{
					float orbA = time * (0.85f + beat * 0.25f) + i * (MathHelper.TwoPi / 3f);
					Vector2 orbPos = coreCenter + new Vector2((float)Math.Cos(orbA), (float)Math.Sin(orbA)) * (32f + beat * 4f);
					NihilityLedgerTheme.DrawDiamond(sb, orbPos, 3.6f, NihilityLedgerTheme.HotWhite * alpha, NihilityLedgerTheme.AcheronScarlet * alpha, orbA, 1f);
				}
			}

			// 5. Constellation Strands Connecting Adjacent Eidolons (E1-E6 arc + EX crown links)
			for (int k = 0; k < 5; k++)
			{
				DrawConstellationLink(sb, eidolonPositions[k], eidolonPositions[k + 1],
					player.IsEidolonActive(k), player.IsEidolonActive(k + 1),
					HoveredEidolon == k, HoveredEidolon == k + 1,
					eidolonStrandFlow[k], isCrownLink: false, alpha, time, k);
			}
			DrawConstellationLink(sb, eidolonPositions[2], eidolonPositions[6],
				player.IsEidolonActive(2), exActive,
				HoveredEidolon == 2, HoveredEidolon == 6,
				eidolonStrandFlow[5], isCrownLink: true, alpha, time, 5);
			DrawConstellationLink(sb, eidolonPositions[3], eidolonPositions[6],
				player.IsEidolonActive(3), exActive,
				HoveredEidolon == 3, HoveredEidolon == 6,
				eidolonStrandFlow[6], isCrownLink: true, alpha, time, 6);

			// 6. Eidolon Medallion Nodes (I - VI + EX)
			for (int k = 0; k < NaughtPlayer.EidolonCount; k++)
			{
				bool isEx = k == 6;
				Vector2 pos = eidolonPositions[k];
				bool unlocked = player.IsEidolonUnlocked(k);
				bool active = player.IsEidolonActive(k);
				bool unlockedDisabled = unlocked && !active;
				float hover = eidolonHoverEase[k];
				float stamp = eidolonStampFlash[k];
				float nodeRadius = (isEx ? 22.5f : 20.5f) * (1f + hover * 0.14f);

				// Soft radial backlight
				if (active || hover > 0.05f)
				{
					Color glowCol = active ? (isEx ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.AcheronScarlet) : NihilityLedgerTheme.NihilityViolet;
					float glowAlpha = (active ? 0.42f : 0f) + hover * 0.35f + beat * 0.18f;
					NihilityLedgerTheme.DrawSoftGlow(sb, pos, nodeRadius * 1.95f, glowCol, glowAlpha * alpha);
				}

				// Pulse ring for active Eidolons
				if (active)
				{
					float pulse = (float)Math.Sin(time * 3.6f + k * 0.75f) * 2.5f + nodeRadius + 5.5f;
					Color pulseCol = isEx ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.AcheronScarlet;
					NihilityLedgerTheme.DrawCircle(sb, pos, pulse, pulseCol * ((0.50f + hover * 0.3f) * alpha), 1.4f, 32);
				}

				// Outer rotating diamond lacquer frame
				float diamondRot = active ? (MathHelper.PiOver4 * 0.15f * MathF.Sin(time * 1.2f + k) + hover * 0.35f) : hover * 0.25f;
				Color diamondFill = active ? NihilityLedgerTheme.VoidBg * (0.9f * alpha) : NihilityLedgerTheme.LacquerBlack * (0.7f * alpha);
				Color diamondBorder = hover > 0.2f
					? Color.Lerp(isEx ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.AcheronScarlet, NihilityLedgerTheme.HotWhite, hover) * alpha
					: (active
						? NihilityLedgerTheme.LacquerGold * (0.88f * alpha)
						: (unlockedDisabled ? NihilityLedgerTheme.LacquerGold * (0.42f * alpha) : NihilityLedgerTheme.LacquerBorder * (0.55f * alpha)));
				NihilityLedgerTheme.DrawDiamond(sb, pos, nodeRadius * 1.24f, diamondFill, diamondBorder, diamondRot, active || hover > 0.2f ? 1.8f : 1.2f);

				// Circular medallion disc & double bezel
				Color discBg = active
					? Color.Lerp(NihilityLedgerTheme.DeepBurgundy, isEx ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.AcheronScarlet, isEx ? 0.22f + hover * 0.25f : hover * 0.32f)
					: (unlockedDisabled
						? Color.Lerp(NihilityLedgerTheme.LacquerBlack, NihilityLedgerTheme.DeepBurgundy, 0.28f + hover * 0.25f)
						: Color.Lerp(NihilityLedgerTheme.LacquerBlack, NihilityLedgerTheme.DeepBurgundy, hover * 0.3f));
				Color discBorder = hover > 0.1f
					? Color.Lerp(active ? NihilityLedgerTheme.AcheronScarlet : NihilityLedgerTheme.LacquerBorder, NihilityLedgerTheme.HotWhite, hover)
					: (active ? (isEx ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.AcheronScarlet) : (unlockedDisabled ? NihilityLedgerTheme.LacquerGold * 0.55f : NihilityLedgerTheme.TextDisabled * 0.65f));

				NihilityLedgerTheme.DrawFilledCircle(sb, pos, nodeRadius, discBg * alpha, 28);
				NihilityLedgerTheme.DrawCircle(sb, pos, nodeRadius, discBorder * alpha, hover > 0.2f ? 2.4f : 1.6f, 32);
				if (active || unlockedDisabled)
					NihilityLedgerTheme.DrawCircle(sb, pos, nodeRadius * 0.78f, NihilityLedgerTheme.LacquerGold * ((active ? 0.55f : 0.28f) * alpha), 1f, 28);

				// 4 orbiting cardinal diamond pips on active medallions
				if (active)
				{
					float pipOrbit = time * 0.55f + k * 0.5f;
					for (int p = 0; p < 4; p++)
					{
						float pa = pipOrbit + p * MathHelper.PiOver2;
						Vector2 pipPos = pos + new Vector2((float)Math.Cos(pa), (float)Math.Sin(pa)) * (nodeRadius + 4.5f);
						sb.Draw(pixel, pipPos, new Rectangle(0, 0, 1, 1), NihilityLedgerTheme.LacquerGold * (0.85f * alpha),
							pa + MathHelper.PiOver4, new Vector2(0.5f), new Vector2(3.2f, 3.2f), SpriteEffects.None, 0f);
					}
				}

				// Stamp ripple when newly awakened/enabled
				if (stamp > 0.01f)
				{
					float spread = 1f - stamp;
					NihilityLedgerTheme.DrawCircle(sb, pos, nodeRadius * (1.05f + spread * 1.45f),
						NihilityLedgerTheme.AcheronScarlet * (stamp * 0.9f * alpha), 1.2f + stamp * 2.0f, 36);
					NihilityLedgerTheme.DrawCircle(sb, pos, nodeRadius * (1.0f + spread * 0.95f),
						NihilityLedgerTheme.LacquerGold * (stamp * 0.75f * alpha), 1.0f + stamp * 1.2f, 32);
					NihilityLedgerTheme.DrawSoftGlow(sb, pos, nodeRadius * 1.5f, NihilityLedgerTheme.HotWhite, stamp * stamp * 0.65f * alpha);
				}

				// Roman numeral / EX label with drop shadow
				string numStr = Eidolons[k].RomanNumeral;
				float numScale = (isEx ? 0.84f : 0.92f) + hover * 0.10f;
				Vector2 numSize = font.MeasureString(numStr) * numScale;
				Vector2 numPos = pos - numSize * 0.5f + new Vector2(0f, 1f);
				Color numCol = active
					? Color.Lerp(NihilityLedgerTheme.HotWhite, NihilityLedgerTheme.LacquerGold, isEx ? 0.45f : hover * 0.35f)
					: (unlockedDisabled
						? Color.Lerp(NihilityLedgerTheme.TextDim, NihilityLedgerTheme.LacquerGold, hover * 0.6f)
						: Color.Lerp(NihilityLedgerTheme.TextDisabled, NihilityLedgerTheme.TextDim, hover * 0.65f));
				sb.DrawString(font, numStr, numPos + new Vector2(1f, 1f), Color.Black * (0.9f * alpha), 0f, Vector2.Zero, numScale, SpriteEffects.None, 0f);
				sb.DrawString(font, numStr, numPos, numCol * alpha, 0f, Vector2.Zero, numScale, SpriteEffects.None, 0f);
			}

			// 7. Resonance Tally Pips & Dynamic Rank Banner Below Constellation Core (7 pips: I-VI + EX)
			float footerY = eidolonCenter.Y + Math.Max(74f, eidolonArcRadius * 0.58f);
			float pipSpacing = 22f;
			float pipsStartX = eidolonCenter.X - ((NaughtPlayer.EidolonCount - 1) * pipSpacing) * 0.5f;
			for (int k = 0; k < NaughtPlayer.EidolonCount; k++)
			{
				bool unlocked = player.IsEidolonUnlocked(k);
				bool active = player.IsEidolonActive(k);
				bool hovered = (HoveredEidolon == k);
				Vector2 pipPos = new Vector2(pipsStartX + k * pipSpacing, footerY);
				Color pipFill = active
					? (k == 6 ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.AcheronScarlet) * alpha
					: (unlocked ? NihilityLedgerTheme.DeepBurgundy * (0.7f * alpha) : NihilityLedgerTheme.LacquerBlack * alpha);
				Color pipEdge = hovered
					? NihilityLedgerTheme.HotWhite * alpha
					: (unlocked ? NihilityLedgerTheme.LacquerGold * alpha : NihilityLedgerTheme.LacquerBorder * alpha);
				NihilityLedgerTheme.DrawDiamond(sb, pipPos, hovered ? 6.5f : 5.2f, pipFill, pipEdge, 0f, 1.3f);
			}

			string statusText;
			Color statusColor;
			if (HoveredEidolon >= 0 && HoveredEidolon < NaughtPlayer.EidolonCount)
			{
				var e = Eidolons[HoveredEidolon];
				bool unl = player.IsEidolonUnlocked(HoveredEidolon);
				bool act = player.IsEidolonActive(HoveredEidolon);
				string stateTag = !unl ? "SEALED" : (act ? "ACTIVE" : "DISABLED");
				statusText = $"{e.RomanNumeral} · {e.Title}  [{stateTag}]";
				statusColor = act ? NihilityLedgerTheme.AcheronScarlet : (unl ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.TextDim);
			}
			else
			{
				statusText = $"Resonance: {activeCount} / {NaughtPlayer.EidolonCount} Active ({unlockedCount} Unsealed)  ·  Click unsealed sigil to toggle";
				statusColor = allActive ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.TextDim;
			}

			Vector2 statusSize = font.MeasureString(statusText) * 0.76f;
			sb.DrawString(font, statusText, new Vector2(eidolonCenter.X - statusSize.X * 0.5f, footerY + 14f),
				statusColor * alpha, 0f, Vector2.Zero, 0.76f, SpriteEffects.None, 0f);
		}

		private static void DrawConstellationLink(
			SpriteBatch sb,
			Vector2 p0,
			Vector2 p1,
			bool a0,
			bool a1,
			bool h0,
			bool h1,
			float flow,
			bool isCrownLink,
			float alpha,
			float time,
			int phaseIndex)
		{
			bool bothActive = a0 && a1;
			bool previewLink = !bothActive && ((a0 && h1) || (a1 && h0));

			if (bothActive && flow > 0.01f)
			{
				float easedFlow = 1f - (1f - flow) * (1f - flow);
				Vector2 tip = Vector2.Lerp(p0, p1, easedFlow);
				Color core = (isCrownLink ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.AcheronScarlet) * (0.9f * alpha);
				Color glow = (isCrownLink ? NihilityLedgerTheme.AcheronScarlet : NihilityLedgerTheme.NihilityViolet) * (0.45f * alpha);
				NihilityLedgerTheme.DrawGlowLine(sb, p0, tip, core, glow, 2.4f, 7f);

				float moteT = (time * 0.35f + phaseIndex * 0.21f) % 1f;
				Vector2 motePos = Vector2.Lerp(p0, p1, moteT);
				NihilityLedgerTheme.DrawSoftGlow(sb, motePos, 8f, NihilityLedgerTheme.HotWhite, 0.7f * alpha);
				NihilityLedgerTheme.DrawDiamond(sb, motePos, 3.2f, NihilityLedgerTheme.HotWhite * alpha, core, MathHelper.PiOver4, 1f);
			}
			else if (previewLink)
			{
				NihilityLedgerTheme.DrawDashedLine(sb, p0, p1, NihilityLedgerTheme.NihilityViolet, NihilityLedgerTheme.VioletGlow, 0.85f * alpha, time, phaseIndex * 1.3f);
			}
			else
			{
				NihilityLedgerTheme.DrawDashedLine(sb, p0, p1, NihilityLedgerTheme.LacquerBorder, NihilityLedgerTheme.DeepBurgundy, 0.38f * alpha, time * 0.25f, phaseIndex);
			}
		}

		private static void DrawWrappedLines(SpriteBatch sb, DynamicSpriteFont font, List<string> lines, float x, ref float y, float scale, Color color, float? clipTop = null, float? clipBottom = null)
		{
			if (lines == null || lines.Count == 0)
				return;

			float lineH = font.LineSpacing * scale;
			for (int i = 0; i < lines.Count; i++)
			{
				if ((!clipTop.HasValue || y + lineH >= clipTop.Value) && (!clipBottom.HasValue || y <= clipBottom.Value))
					sb.DrawString(font, lines[i], new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				y += lineH;
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

			if (HoveredEidolon >= 0 && HoveredEidolon < NaughtPlayer.EidolonCount)
			{
				var e = Eidolons[HoveredEidolon];
				bool unl = player.IsEidolonUnlocked(HoveredEidolon);
				bool act = player.IsEidolonActive(HoveredEidolon);
				title = $"{e.RomanNumeral} · {e.Title}";
				sub = e.Subtitle + (!unl ? " [Sealed]" : (act ? " [Active]" : " [Disabled]"));
				desc = GetEidolonDescription(in e);
				lore = e.Lore;
				hint = !unl
					? BossTrackerSystem.GetEidolonUnlockHint(HoveredEidolon)
					: (act ? "[Left-Click to toggle off]" : "[Left-Click to toggle on]");
				titleColor = act ? NihilityLedgerTheme.AcheronScarlet : (unl ? NihilityLedgerTheme.LacquerGold : NihilityLedgerTheme.TextDim);
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
							bool worldUnlocked = BossTrackerSystem.IsCharmUnlocked(key);
							title = AvailableCharms[c].Title;
							sub = AvailableCharms[c].Subtitle + (worldUnlocked ? "" : " [Sealed in current world]");
							desc = GetCharmDescription(in AvailableCharms[c]);
							lore = AvailableCharms[c].Lore;
							hint = worldUnlocked
								? "[Right-Click to unequip this Inscription]"
								: $"{BossTrackerSystem.GetCharmUnlockHint(key)}\n[Saved loadout preserved · Right-Click to unequip]";
							titleColor = worldUnlocked ? NihilityLedgerTheme.AcheronScarlet : NihilityLedgerTheme.LacquerGold;
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
			else if (HoveredTraceCard >= 0 && HoveredTraceCard < cardLayouts.Count)
			{
				var charm = cardLayouts[HoveredTraceCard].Charm;
				if (!BossTrackerSystem.IsCharmUnlocked(charm.Key))
				{
					title = charm.Title;
					sub = charm.Subtitle + " [Sealed]";
					desc = GetCharmDescription(in charm);
					lore = charm.Lore;
					hint = BossTrackerSystem.GetCharmUnlockHint(charm.Key);
					titleColor = NihilityLedgerTheme.LacquerGold;
				}
			}

			if (string.IsNullOrEmpty(title))
				return;

			float tipW = 390f;
			float innerW = tipW - 24f;
			var subLines = WrapText(font, sub, innerW, 0.78f);
			var descLines = WrapText(font, desc, innerW, 0.76f);
			var loreLines = WrapText(font, lore, innerW, 0.70f);
			var hintLines = WrapText(font, hint, innerW, 0.72f);

			float titleH = font.LineSpacing * 0.95f;
			float subH = subLines.Count * (font.LineSpacing * 0.78f);
			float descH = descLines.Count * (font.LineSpacing * 0.76f);
			float loreH = loreLines.Count * (font.LineSpacing * 0.70f);
			float hintH = hintLines.Count * (font.LineSpacing * 0.72f);

			float tipH = 12f + titleH
				+ (subLines.Count > 0 ? 2f + subH : 0f)
				+ (descLines.Count > 0 ? 4f + descH : 0f)
				+ (loreLines.Count > 0 ? 4f + loreH : 0f)
				+ (hintLines.Count > 0 ? 4f + hintH : 0f)
				+ 8f;

			Vector2 tipPos = mouse + new Vector2(18f, 18f);

			if (tipPos.X + tipW > UIScreenW - 20)
				tipPos.X = mouse.X - tipW - 10;
			if (tipPos.Y + tipH > UIScreenH - 20)
				tipPos.Y = mouse.Y - tipH - 10;
			tipPos.X = Math.Max(12f, tipPos.X);
			tipPos.Y = Math.Max(12f, tipPos.Y);

			Rectangle tipRect = new Rectangle((int)tipPos.X, (int)tipPos.Y, (int)tipW, (int)MathF.Ceiling(tipH));
			NihilityLedgerTheme.DrawBeveledBox(sb, tipRect, NihilityLedgerTheme.VoidBg * alpha, NihilityLedgerTheme.AcheronScarlet * alpha, 1.5f);

			float drawX = tipPos.X + 12f;
			float drawY = tipPos.Y + 8f;
			sb.DrawString(font, title, new Vector2(drawX, drawY), titleColor * alpha, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 0f);
			drawY += titleH;

			if (subLines.Count > 0)
			{
				drawY += 2f;
				DrawWrappedLines(sb, font, subLines, drawX, ref drawY, 0.78f, NihilityLedgerTheme.TextDim * alpha);
			}

			if (descLines.Count > 0)
			{
				drawY += 4f;
				DrawWrappedLines(sb, font, descLines, drawX, ref drawY, 0.76f, NihilityLedgerTheme.PaperWhite * alpha);
			}

			if (loreLines.Count > 0)
			{
				drawY += 4f;
				DrawWrappedLines(sb, font, loreLines, drawX, ref drawY, 0.70f, NihilityLedgerTheme.TextDim * alpha);
			}

			if (hintLines.Count > 0)
			{
				drawY += 4f;
				DrawWrappedLines(sb, font, hintLines, drawX, ref drawY, 0.72f, NihilityLedgerTheme.LacquerGold * alpha);
			}
		}
	}
}
