# Terraria / tModLoader Reference Paths & Rules

Only inspect a repository below when it is **explicitly mentioned** or **inferred to be relevant** to the current task (do not scan all paths needlessly on unrelated work).

## Mandatory Reference Docs Rule (`WeepForTheDeparted`)

- **Path**: `G:\GitRepos\WeepForTheDeparted\docs` (architecture & technical reference: [WeepForTheDeparted_Feature_and_Architecture_Reference.md](file:///g:/GitRepos/WeepForTheDeparted/docs/WeepForTheDeparted_Feature_and_Architecture_Reference.md); gameplay & balance reference: [WeepForTheDeparted_Gameplay_and_Balance_Reference.md](file:///g:/GitRepos/WeepForTheDeparted/docs/WeepForTheDeparted_Gameplay_and_Balance_Reference.md))
- **Rule**: Whenever working on `WeepForTheDeparted` (design, debugging, balance, charms, Eidolons, shaders, UI, or compatibility hooks):
  1. **Consult first**: Read the relevant sections of the reference doc(s) in `G:\GitRepos\WeepForTheDeparted\docs` before making assumptions or edits.
  2. **Update after**: Keep [WeepForTheDeparted_Feature_and_Architecture_Reference.md](file:///g:/GitRepos/WeepForTheDeparted/docs/WeepForTheDeparted_Feature_and_Architecture_Reference.md) up to date whenever features, numbers, hooks, files, shaders, or implementation status change (and follow **Gameplay & Balance Documentation Sync** below for [WeepForTheDeparted_Gameplay_and_Balance_Reference.md](file:///g:/GitRepos/WeepForTheDeparted/docs/WeepForTheDeparted_Gameplay_and_Balance_Reference.md)).

## Commonly Accessed Mod Paths

| Path | Scope / What Lives Here | Consult When Mentioned or Inferred |
| :--- | :--- | :--- |
| `G:\GitRepos\WeepForTheDeparted\docs` | Architecture/feature reference and gameplay/balance reference docs for `WeepForTheDeparted`. | **Always** when working on `WeepForTheDeparted` (read & keep updated). |
| `G:\GitRepos\WeepForTheDeparted` | **Weep for the Departed** mod (`Naught`, Nihility Charms, Eidolons, Domain, WorldFreeze, Dismemberment). | Working on `WeepForTheDeparted`, or cross-referencing shared systems (boss scaling, True Melee `StrikeNPC`, shaders, Calamity/CWR hooks). |
| `G:\GitRepos\InnoVault` | **InnoVault** API library (`VaultHook`, `NPCOverride`, `RenderHandle`, `UIHandle`, `PRTLoader`/`BasePRT`, `TileProcessor`, `VaultUtils`, networking). | Any `InnoVault` API usage, hook priority/ordering, `NPCOverride` behavior, particle/UI/render pipeline questions, or `WeepForTheDeparted`/`CalamityOverhaul` engine hooks. |
| `G:\GitRepos\CalamityOverhaul` | **Calamity Overhaul (CWR)** source (`NPCOverride` / Brutal boss AI, `BottomLeftHudStack`, Onikiri/katana/dash systems, `OtherMods` compat). | `CalamityOverhaul` / CWR is mentioned, or when handling CWR `NPCOverride` bosses, CWR HUD stacking, Calamity compatibility, or Onikiri-style dash/slash references. |
| `G:\GitRepos\Mirror Wakizashi` | **The Five Divine Instruments** (`MirrorWakizashi`, `Talisman`, `Saya`, `Braid`, `Prayer Beads` accessories, `BossTrackerSystem`, `SayaPlayer` flash dash). | `Mirror Wakizashi` / Divine Instruments are mentioned, or when referencing True Melee accessories, echo strikes, Battoujutsu dashes, crit buffs, or boss-scaling patterns. |
| `G:\GitRepos\WeaponOutLite` | **WeaponOut Lite** held-item pose/draw-layer vanity mod (`WeaponOutLayerRenderer`, `PoseStyleID`, `ModCalls`, `Compatibility`). | `WeaponOutLite` is mentioned, or when touching held-weapon/scabbard `PlayerDrawLayer` visibility, `noUseGraphic`, pose overrides, or held-item mod compatibility. |
| `C:\Users\Shiraori\Documents\My Games\Terraria\tModLoader\ModReader\ImprovedCrits` | **ImprovedCrits** extracted source (`CustomCritPlayer`, `CritGlobalItem`, `CritServerConfig`, >100% overcrit tiers, summon crits). | `ImprovedCrits` is mentioned **or inferred** — any work involving **crit chance buffs, guaranteed crits, >100% overcrit tiers, crit damage modifiers, custom strike helpers (`StrikeNPC`), `Projectile.CritChance` syncing, `ModifyHitNPCWithItem`/`ModifyHitNPCWithProj`, crit `CombatText` (`!`), or summon crit classes**. |

## Quick Inference Checklist

- **Crit chance / crit buff / custom strike logic**: Think `ImprovedCrits` (`CustomCritPlayer` hooks `ModifyHitNPCWithItem(Player.GetWeaponCrit(item))`, `ModifyHitNPCWithProj(proj.CritChance)`, and `OnHitNPC` for tier multipliers and `!` combat text — manual strikes must preserve those hooks and `CritChance`).
- **Boss AI / HUD overlap / Calamity compatibility**: Think `CalamityOverhaul` (`NPCOverride`, `BottomLeftHudStack`) and `InnoVault` (`VaultHook`, `NPCOverride`).
- **Custom rendering, particles, UI, or detours**: Think `InnoVault` (`RenderHandle`, `PRTLoader`, `UIHandle`, `VaultHook`).
- **Idle weapon/sheath drawing or pose conflicts**: Think `WeaponOutLite` (`ModCalls`, `noUseGraphic`, draw layers).
- **Divine Instrument mechanics or shared katana/dash patterns**: Think `Mirror Wakizashi` and `WeepForTheDeparted`.

# Gameplay & Balance Documentation Sync

- **Trigger condition**: Whenever a code change modifies any gameplay or balance behavior—including damage ratios/formulas, scaling, crit rules, defense/DR penetration, cooldowns, durations, ICDs, ranges/radii, stack/projectile caps, diminishing returns, debuff/resource/clone/Ult triggers, charm or eidolon effects, boss unlock milestones, Balance Mode branches, crafting recipes, keybinds, or config defaults—update `docs/WeepForTheDeparted_Gameplay_and_Balance_Reference.md` in the same task before finishing.
- **Skip condition**: Do NOT touch `docs/WeepForTheDeparted_Gameplay_and_Balance_Reference.md` for purely technical, visual, shader, particle, sound, UI-layout, or refactor changes that have zero effect on combat math, timings, or player-facing mechanics.
- **Sync scope**: Update every affected location in the doc (both the feature's main section AND any summary tables such as Boss Progression or Master Balance Rules, Caps & Interactions).
- **Strict style guardrails**: Keep all edits strictly gameplay-only—never introduce C# class/method/field names, file paths, hooks, shaders, or rendering internals. Keep all units normalized to seconds, tiles (16 px = 1 tile), and percentages of scaled weapon damage, and verify numbers directly against the edited C# code.
- **Reporting**: Mention any updates made to `docs/WeepForTheDeparted_Gameplay_and_Balance_Reference.md` in the final task summary.
