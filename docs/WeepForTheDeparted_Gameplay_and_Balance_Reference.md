# Weep for the Departed (`Naught`) — Gameplay & Balance Reference

> **Scope**: Complete, ground-truth gameplay, combat math, timing, and balance reference for **Weep for the Departed** (`Naught`), covering basekit mechanics, all 18 Nihility Charms, all 7 Eidolons (`E1–E6 + EX`), boss-gated progression, and cross-mechanic rules and caps.
> **Units**: `1.00s = 60 frames` (ticks); `1 tile = 16 px`; damage percentages scale from Naught's current scaled weapon damage.

---

## Table of Contents

1. [Weapon Overview, Scaling & Config](#1-weapon-overview-scaling--config)
2. [Basekit Combat Mechanics](#2-basekit-combat-mechanics)
   - [4-Beat Flowing Odachi Combo](#4-beat-flowing-odachi-combo)
   - [Flash Step Dash](#flash-step-dash)
   - [Shadow Clones & Cycle Volley](#shadow-clones--cycle-volley)
   - [Slashed Dream, Debuff Detection & Crimson Knots](#slashed-dream-debuff-detection--crimson-knots)
   - [Nihility Domain & World Time-Stop Rules](#nihility-domain--world-time-stop-rules)
   - [Ultimate: Slashed Dream Cries in Red](#ultimate-slashed-dream-cries-in-red)
3. [Nihility Charms (18 Inscriptions)](#3-nihility-charms-18-inscriptions)
   - [3.1 Soul Inscriptions (Slot 0)](#31-soul-inscriptions-slot-0)
   - [3.2 Tide Inscriptions (Slot 1)](#32-tide-inscriptions-slot-1)
   - [3.3 Ruin Inscriptions (Slot 2)](#33-ruin-inscriptions-slot-2)
4. [Eidolons (`E1–E6 + EX`)](#4-eidolons-e1e6--ex)
5. [Boss Unlock Progression Chart](#5-boss-unlock-progression-chart)
6. [Master Balance Rules, Caps & Interactions Quick-Reference](#6-master-balance-rules-caps--interactions-quick-reference)

---

## 1. Weapon Overview, Scaling & Config

### Crafting Recipe & Base Stats

- **Crafting Recipe**: `10` Iron or Lead Bars + `5` Fallen Stars + `15` Wood at an Anvil.
- **Base Damage**: **`14`** in Vanilla mode; **`16`** in Calamity mode *(or **`25`** default when **Override Weapon Scaling** is enabled, configurable `1`–`1000`)*.
- **Base Knockback**: `5.5`.
- **Base Crit Chance**: `4%` (standard player base crit; `0%` innate weapon crit bonus before boss-kill scaling).
- **Use Time / Animation**: `0.27s (16 frames)` base item use time (continuous auto-swing; actual combo cadence is governed by the 4-beat combo controller).
- **Rarity & Value**: Red rarity; `5` gold buy value (`1` gold sell value).
- **Damage Class**: **True Melee** when Calamity Mod is loaded; **Melee** when Calamity Mod is absent.
  - All Naught hits and lingering abilities scale with True Melee / Melee damage, crit, and armor penetration stats and snapshot Naught's active reforge prefix and boss-kill scaling even when swapped away from Naught while clones or lingering projectiles remain active.

### Controls & Keybinds

| Input / Keybind | Action & Rules |
| :--- | :--- |
| **Left-Click** *(Tap or Hold)* | Executes the **4-Beat Flowing Odachi Combo**; holding Left-Click loops Beats 1–4 continuously. |
| **Right-Click** | Triggers **Flash Step Dash** toward the cursor; cancels any combo beat on any frame if off cooldown. |
| **`[B]`** *(Domain: Nihility)* | Toggles **Nihility Domain** open or reverse-closed while holding Naught. Cannot close during an active Ultimate; pressing while closing re-opens it. |
| **`[V]`** *(Ultimate: Slashed Dream Cries in Red)* | Casts the Ultimate when prerequisites are met (including off post-Ultimate recast cooldown); tap or hold **`[V]`** during the slashing window to advance strikes. |
| **`[C]`** *(Nihility Ledger)* / **Click Flower HUD** | Opens or closes the **Nihility Ledger** interface to socket Charms and toggle Eidolons. |

- All weapon attacks, dashes, movement, item/tile usage, hotbar scrolling, and Domain closing are locked out for the entire duration of an active Ultimate.

### Boss-Kill Scaling Formulas & Calamity Stage Multipliers

Naught's base weapon stats grow permanently with each unique boss and major event boss defeated in the current world, governed by the active **Balance Mode** profile (unless **Override Weapon Scaling** is enabled):

- **Vanilla Mode**:
  $$\text{Scaled Base Damage} = 14 + (4.0 \times \text{Unique Bosses Defeated})$$
  $$\text{Bonus Crit Chance (\%)} = 0.3\% \times \text{Unique Bosses Defeated}$$
- **Calamity Mode**:
  $$\text{Scaled Base Damage} = \bigl(16 + (3.0 \times \text{Unique Bosses Defeated})\bigr) \times \text{Stage Multiplier}$$
  $$\text{Bonus Crit Chance (\%)} = 0.2\% \times \text{Unique Bosses Defeated}$$
- **Manual Override** *(when **Override Weapon Scaling** is enabled)*:
  $$\text{Scaled Base Damage} = \text{Base Sword Damage} + (\text{Unique Bosses Defeated} \times \text{Bonus Damage Per Boss})$$
  $$\text{Bonus Crit Chance (\%)} = \text{Unique Bosses Defeated} \times \text{Bonus Crit Per Boss}$$

| Highest Progression Milestone Cleared *(Calamity Mode)* | Stage Multiplier |
| :--- | :---: |
| **Pre-Hardmode** | `1.00×` |
| **Hardmode (Wall of Flesh defeated)** | `1.05×` |
| **Post-Golem** | `1.15×` |
| **Post-Moon Lord** | `1.35×` |
| **Post-Providence** | `2.00×` |
| **Post-Polterghast** | `3.20×` |
| **Post-Devourer of Gods** | `4.50×` |
| **Post-Yharon** | `6.20×` |
| **Post-Exo Mechs or Supreme Witch, Calamitas** | `9.50×` |

- Because the scaled base damage replaces Naught's base damage before prefixes and stat modifiers, it scales multiplicatively with weapon prefixes/reforges, armor, accessories, potions, and damage buffs.
- **Eligible Unique Bosses** (each species/event tracked at most once per world):
  - **17 Vanilla Bosses**: King Slime, Eye of Cthulhu, Evil Boss (Eater of Worlds / Brain of Cthulhu counted once), Queen Bee, Skeletron, Deerclops, Wall of Flesh, Queen Slime, The Destroyer, The Twins, Skeletron Prime, Plantera, Golem, Duke Fishron, Empress of Light, Lunatic Cultist, and Moon Lord.
  - **14 Vanilla Event / Invasion Bosses**: Mourning Wood, Pumpking, Everscream, Santa-NK1, Ice Queen, Flying Dutchman, Martian Saucer, all 4 Celestial Pillars (Solar, Vortex, Nebula, Stardust), and all 3 Old One's Army bosses (Dark Mage, Ogre, Betsy).
  - **Calamity Mod Bosses** *(when Include Calamity Bosses is enabled)*: All unique defeated Calamity bosses and event bosses.
  - **Other Modded Bosses** *(only when Universal Boss Scaling is enabled)*: Each unique non-Vanilla, non-Calamity modded boss defeated in the world.

### Balance Mode & Server Configuration

| Setting | Default | Range / Options | Gameplay Impact |
| :--- | :---: | :---: | :--- |
| **Override Weapon Scaling** | `Disabled` (`false`) | `true` / `false` | When disabled (default), **Balance Mode** controls Naught's starting damage, per-boss damage/crit scaling, and Calamity stage multipliers. Enable to use the manual weapon-scaling sliders below. |
| **Base Sword Damage** | `25` | `1` – `1000` | Manual starting base damage before boss-kill scaling (active only when **Override Weapon Scaling** is enabled). |
| **Bonus Damage Per Boss** | `5.0` | `0.0` – `100.0` (step `0.5`) | Manual flat base damage added per unique defeated boss (active only when **Override Weapon Scaling** is enabled). |
| **Bonus Crit Per Boss (%)** | `0.4%` | `0.0%` – `10.0%` (step `0.1%`) | Manual flat crit chance added per unique defeated boss (active only when **Override Weapon Scaling** is enabled). |
| **Include Calamity Bosses** | `Enabled` (`true`) | `true` / `false` | Counts defeated Calamity Mod bosses toward unique boss scaling. |
| **Universal Boss Scaling** | `Disabled` (`false`) | `true` / `false` | Counts non-Vanilla, non-Calamity modded bosses toward unique boss scaling. |
| **Balance Mode** | `Auto` | `Auto`, `Calamity`, `Vanilla` | Selects the unified **Vanilla** or **Calamity** balance profile governing weapon scaling, basekit combat, all `18` Nihility Charms, and all `7` Eidolons (`Auto` uses Calamity tuning whenever Calamity Mod is loaded, otherwise Vanilla tuning). Does not change Naught's Damage Class (which always uses True Melee when Calamity Mod is loaded). |
| **Unlock All Inscriptions & Eidolons** | `Disabled` (`false`) | `true` / `false` | Sandbox toggle that bypasses all world boss-progression locks on all 18 Charms and 7 Eidolons. |

- **New-Character Defaults**: New characters start with **Mark of the Red Oni** (Soul), **Grip of the Abyss** (Tide), and **Thunder Core** (Ruin) socketed and all 7 Eidolon toggles enabled (world boss locks still seal locked items until defeated or bypassed).

---

## 2. Basekit Combat Mechanics

This section describes Naught's baseline behavior with **no Charms or Eidolons active**.

### 4-Beat Flowing Odachi Combo

- **Attack Speed Rule**: Swing speed equals $\max(0.70\times,\ \text{Total Melee Attack Speed})$. Attack speed bonuses shorten Windup, Active, and Recovery proportionally; melee speed penalties cannot slow swings below `0.70x`.
- **Hitbox & Anti-Double-Hit**: Each beat sweeps both the blade line (including an anti-tunneling sweep from the previous frame's tip) and a trailing crescent arc at `85%` of effective reach, with `1.5 tiles` of target hitbox padding and `2.06 tiles` (`2.44 tiles` on Beat 4) sweep thickness. Both hitboxes share a per-beat target list so each enemy is struck **at most once per beat**.
- **Crit, Knockback & Resource Rule**: Uses standard crit rolls and `5.5` knockback. Basekit combo swings generate **`0` Slashed Dream** and **`0` Crimson Knots**.

| Beat | Move Name | Windup / Active / Recovery | Total Base Time | Arc | Effective Reach *(Vanilla / Calamity)* | Damage % | Cancel & Loop Rules |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **1** | Horizontal Draw Cut | `0.05s / 0.13s / 0.08s` (`3 / 8 / 5` frames) | `0.27s (16 frames)` | `195°` | **`7.01 tiles`** (`6.38 × 1.10x`) / **`7.77 tiles`** (`7.06 × 1.10x`) | `100%` | Right-Click dash-cancels on any frame; holding Left-Click chains into Beat 2 at end of Recovery. |
| **2** | Downward Cross-Slash | `0.05s / 0.13s / 0.08s` (`3 / 8 / 5` frames) | `0.27s (16 frames)` | `205°` | **`7.49 tiles`** (`6.81 × 1.10x`) / **`8.25 tiles`** (`7.50 × 1.10x`) | `100%` | Right-Click dash-cancels on any frame; holding Left-Click chains into Beat 3 at end of Recovery. |
| **3** | Rising Reverse Sweep | `0.05s / 0.13s / 0.08s` (`3 / 8 / 5` frames) | `0.27s (16 frames)` | `205°` | **`7.77 tiles`** (`7.06 × 1.10x`) / **`8.53 tiles`** (`7.75 × 1.10x`) | `100%` | Reverse swing direction. Right-Click dash-cancels on any frame; holding Left-Click chains into Beat 4. |
| **4** | Overhead Helm Splitter *(Finisher)* | `0.08s / 0.18s / 0.13s` (`5 / 11 / 8` frames) | `0.40s (24 frames)` | `270°` | **`10.48 tiles`** (`8.06 × 1.30x`) / **`11.54 tiles`** (`8.88 × 1.30x`) | `140%` | Right-Click dash-cancels on any frame; holding Left-Click loops back to Beat 1 at end of Recovery. |

### Flash Step Dash

- **Distance Range**: Clamped toward the cursor between **`6.00 tiles`** minimum and **`13.00 tiles`** maximum in **Vanilla** (`7.50 tiles` minimum and `16.00 tiles` maximum in **Calamity**). Ray-marches in `0.75-tile` steps and stops `0.75 tiles` short of solid blocks (passes through platforms).
- **Duration & Exit Velocity**: **`0.12s (7 frames)`** dash travel, exiting with `11.25 tiles/s` residual velocity in the dash direction.
- **Invulnerability (i-frames)**: Refreshes non-blinking invulnerability on every frame of the `7`-frame dash to **`0.03s (2 frames)`** linger in **Vanilla** (`0.15s / 9 frames` total from dash start) or **`0.08s (5 frames)`** linger in **Calamity** (`0.20s / 12 frames` total from dash start).
- **Cooldown**: **`2.00s (120 frames)`** base cooldown in **Vanilla**; **`1.50s (90 frames)`** base cooldown in **Calamity**. Cannot be cast while already dashing.
- **Clone Spawning**: Spawns `1` stationary **Shadow Clone** at the player's pre-dash position when the dash begins.

### Shadow Clones & Cycle Volley

- **Lifetime**: **`9.00s (540 frames)`** base lifetime for the first active clone in a cycle.
- **Concurrency & Synchronization Rule**: Capped at **`6` concurrent basekit clones**. Whenever a new clone spawns while earlier clones are active, the new clone copies the active cycle's remaining lifetime so **all clones in the cycle expire on the exact same frame**. Spawning a 7th clone silently evicts the oldest clone without triggering cycle completion.
- **Cycle Expiry Payout** *(deduplicated to **1 payout per cycle** even when multiple clones expire simultaneously)*:
  - **Slashed Dream**: Grants **`+1` Slashed Dream** (spilling into Overflow Dream if at `9/9`).
  - **Crimson Knot**: Applies **`+1` Crimson Knot** to the highest-priority valid enemy within **`112.5 tiles`** of the expiring clone that has fewer than `9` Crimson Knots.
  - **Target Selection Priority**: `Boss > Normal Enemy > Target Dummy`, tie-broken by highest current HP (resolved to the primary body on parent-linked multi-segment bosses).
- **Localized Cycle Volley**:
  - **Target Acquisition**: Searches within **`112.5 tiles`** of the expiring clone (falling back to `112.5 tiles` around the player if none are in clone range) using the same `Boss > Normal > Dummy` and highest-HP priority. Unlike the Knot grant, the volley **can target enemies already at `9/9` Crimson Knots** and skips invulnerable targets (except Target Dummies).
  - **Slash Count & Interval**: Fires **`4` localized slashes** spaced **`0.07s (4 frames)` apart** (first slash fires immediately on expiry; total volley span `0.20s` from first to fourth slash).
  - **Damage, Crit & Knockback**: **`50%` weapon damage** per slash (**`200%` total** across `4` slashes), True Melee (or Melee in Vanilla), standard crit roll, and `3.5` knockback.
  - **Dynamic Re-Targeting & Time-Stop**: If the initial target dies or turns invulnerable mid-volley, remaining slashes re-acquire the highest-priority valid target within `112.5 tiles` of the player. Pending volley dispatches pause during World Time-Stop and resume afterward.

### Slashed Dream, Debuff Detection & Crimson Knots

- **Slashed Dream Capacity**: **`0–9`** active stacks (`9` required to cast the Ultimate).
- **Base Overflow Capacity**: **`0–3`** stacks (`12` total stored stacks max before Eidolons). Any Slashed Dream gained at `9/9` spills automatically into Overflow Dream up to the cap.
- **Debuff Detection Rules**:
  - Active **only while holding Naught** and only for enemies within **`112.5 tiles`** of the player.
  - Scans all active debuffs on every active enemy every frame (including modded/reworked bosses).
  - Triggers when an enemy debuff is either:
    1. **Newly applied** (absent on that enemy on the previous frame), **or**
    2. **Refreshed by strictly more than `1.00s (60 frames)`** (remaining duration jumps by `> 1.00s` over the previous frame).
  - **Per-Debuff & Global Debuff-to-Dream ICDs**: Each debuff type carries an independent **`9.00s (540 frames)`** ICD on the player, paired with a **`1.50s (90 frames)`** (**Vanilla**) / **`2.50s (150 frames)`** (**Calamity**) **Global Debuff-to-Dream ICD**. Triggering a debuff off its per-debuff ICD always puts that debuff on its `9.00s` ICD and applies **`+1` Crimson Knot** to the target's anchor, while granting **`+1` Slashed Dream** only when the Global Debuff-to-Dream ICD is also ready (which resets the global ICD).
- **Crimson Knot Mark Rules**:
  - **Max Stacks**: **`9`** stacks per enemy (`0–9`). *(Visual flower shifts from purple at `1–5` stacks to red at `6–9` stacks.)*
  - **Multi-Segment Boss Pooling**: Enemies linked to a shared parent body (worms and multi-part bosses) pool all Crimson Knots onto the primary head/root entity (`0–9` total). *(Segments without a shared parent entity—such as Vanilla Eater of Worlds segments—track Crimson Knot stacks individually.)*
  - **Death Transfer**: When a marked enemy dies in basekit, **all** of its Crimson Knot stacks transfer to the highest-priority valid enemy within **`112.5 tiles`** of the dying enemy that is below `9` Crimson Knots (`Boss > Normal > Dummy`, tie-broken by highest current HP), and stacks on the dead enemy clear.

### Nihility Domain & World Time-Stop Rules

- **Nihility Domain Rules**:
  - **Activation**: Toggleable at any time via **`[B]`** while alive and holding Naught. Expands from `0` to **`175 tiles`** over **`5.00s (300 frames)`** and contracts back to `0` over **`2.50s (150 frames)`** when toggled off.
  - **Combat Role**: Required active field to cast *Slashed Dream Cries in Red* (and required by Domain-conditional Eidolons). The Domain alone does **not** slow or freeze enemies, projectiles, or timers (`1.00x` world speed).
  - **Persistence**: Cannot be closed during an active Ultimate; remains open after the Ultimate ends until manually toggled off with `[B]`.
- **World Time-Stop Rules (Active During *Slashed Dream Cries in Red*)**:
  - Engages on Frame 1 of the Ultimate and remains active until the Ultimate ends.
  - **Frozen Entities & World**:
    - **Enemies & Bosses**: All enemies and bosses (including modded/reworked bosses) are completely frozen (`0` velocity, AI halted), deal `0` contact damage, take `0` damage-over-time (life regeneration drain suspended), and have their despawn timers and active buff/debuff durations paused (`+1` frame restored per frame so debuffs never expire during the Ultimate).
    - **Projectiles**: Non-exempt projectiles freeze position, rotation, animation, and lifetime. Only Ultimate slashes, held combo/dash/clone/transfer visuals, Mirage Weft thread geometry (with damage disabled), and Blood Lotus shockwaves continue updating; Cycle Slashes, Blood Blossom Needles, Void Fissures, and Thunder Core Bolts remain paused until time-stop ends.
    - **World & Timers**: Liquid flow physics freeze. **All Naught cooldowns and ICDs pause during World Time-Stop**: Flash Step Dash cooldown, per-debuff `9.00s` ICDs, Global Debuff-to-Dream ICD, post-Ultimate Recast Cooldown, Mirage Weft's `9.00s` debuff-resource ICD, Fleeting Petal Step's trigger ICD and `2.50s` projectile slowdown, Rainleaf Phasing's cooldown, pending Cycle Volley dispatches, clone cycle timers, DoT accumulation, E2's combat/passive timers, Red Oni's `5.00s` out-of-combat encounter reset timer, and post-Ultimate buff timers all pause cleanly while Time-Stop is active.
  - **Player Restrictions & Protection**:
    - Player is locked to their cast position (`0` velocity, `0` gravity) and cannot move, jump, grapple, mount, use items/tiles, swing, dash, or scroll the hotbar.
    - Player has **true non-blinking invincibility**: immune to all hits, projectiles, lethal bypasses, and negative life-regeneration damage-over-time.

### Ultimate: *Slashed Dream Cries in Red*

- **Activation Conditions**: Alive, not crowd-controlled, holding Naught, **Nihility Domain active**, **Slashed Dream at `9/9`**, **off Post-Ultimate Recast Cooldown** (`12.00s` Vanilla / `14.00s` Calamity starting when the previous Ultimate ends), and no Ultimate already active. Press **`[V]`** to cast.
- **Phase Timeline**:
  1. **Phase 0 — Unsheathing (`0.00s–4.00s` / Frames `1–240`)**:
     - `0.02s` (Frame `1`): Locks player in place and activates **World Time-Stop**.
     - Frame `60` (`1.00s`): Terrain-darkening wave expands (`0` -> `175 tiles` over `3.00s / 180 frames`) and normal world music fades to silence.
     - Frame `240` (`4.00s`): Blade finishes drawing; enters Ready stance.
  2. **Phase 1 — Slashing Window (`Max 9.00s` / `540 frames` starting after Frame `240`)**:
     - Executes **Rainblade 1 -> Rainblade 2 -> Rainblade 3 -> Stygian Resurge**.
     - **Tap vs. Hold**: Tapping `[V]` after each strike's cooldown triggers the next strike at Tap speed; holding `[V]` triggers faster Hold animations and auto-advances at each stage's Hold threshold; letting the `9.00s` window expire auto-advances remaining strikes. Ultimate animation timings are fixed and do **not** scale with Melee Attack Speed.
     - On **Stygian Resurge** cleave impact: clears all remaining Crimson Knots within **`137.5 tiles`**, queues a delayed Resurge hit per target, and immediately refunds **`Overflow Dream` (`0–3`, or `0–5` with E5) into `Slashed Dream`** (resetting Overflow Dream to `0`).
  3. **Phase 2 — Reverse Sheathing & Snap (`4.12s` / `247 frames` total real time after Resurge follow-through ends)**:
     - **`0.00s–2.57s` (Sheath frames `1–154`)**: Blade slides back into scabbard (`2.57s / 154 frames`), terrain-darkening wave reverses (`2.57s`), and scabbard locks at `2.57s` (frame `154`).
     - **`2.57s–3.57s` (Sheath frames `154–214`)**: `1.00s (60 frames)` tension hold.
     - **`3.57s` (Sheath frame `214`)**: **Sheathing Snap** — delivers all queued **Stygian Resurge** hits (`0.13s / 8 frames` hitstop pause) and reassembles sliced targets.
     - **`3.57s–4.12s` (Sheath frames `215–239` + `8` hitstop frames)**: `0.42s (25 frames)` post-snap linger (`0.55s / 33 frames` real time including hitstop), then Ultimate ends, World Time-Stop deactivates, world music resumes, and the **Post-Ultimate Recast Cooldown** (`12.00s` Vanilla / `14.00s` Calamity) begins.

#### Ultimate Strike-by-Strike Table (Basekit)

*`Weapon Damage` = Naught's scaled weapon damage snapshot at Ultimate cast (`True Melee` in Calamity, `Melee` in Vanilla). Individual multipliers truncate down to integer damage.*

| Strike | Timing *(Tap / Hold / Hold Auto-Advance)* | Impact Delay *(Tap / Hold)* | Range | Knots Consumed | Damage Formula *(Vanilla / Calamity)* | Crit, Pen & Hitstop |
| :--- | :--- | :--- | :---: | :---: | :--- | :--- |
| **Rainblade 1 — Preemptive** | Shares Rainblade 1 window (`0.57s / 0.37s / 0.37s` = `34 / 22 / 22` frames) | `0.12s` (`7`f) / `0.08s` (`5`f) | `< 112.5 tiles` | `0` | **`35%`** *(Vanilla)* / **`40%`** *(Calamity)* weapon damage (`25%` split of `140%` / `160%` base; avoids main-hit Knot procs) | Normal crit roll; `2` KB; `0.02s` (`1`f) hitstop |
| **Rainblade 1 — Main** | `0.57s` (`34`f) Tap / `0.37s` (`22`f) Hold / `0.37s` (`22`f) Auto-Advance *(CD: `0.50s` / `0.30s`)* | `0.32s` (`19`f) / `0.20s` (`12`f) | `< 112.5 tiles` | Up to **`3`** per enemy | **Vanilla**: **`105%` + `45%`/knot** (`240%` max main; `275%` combined)<br>**Calamity**: **`120%` + `55%`/knot** (`285%` max main; `325%` combined) | Normal crit roll; `4` KB; `0.05s` (`3`f) hitstop |
| **Rainblade 2 — Preemptive** | Shares Rainblade 2 window (`0.57s / 0.37s / 0.37s` = `34 / 22 / 22` frames) | `0.12s` (`7`f) / `0.08s` (`5`f) | `< 112.5 tiles` | `0` | **`35%`** *(Vanilla)* / **`40%`** *(Calamity)* weapon damage (`25%` split of `140%` / `160%` base; avoids main-hit Knot procs) | Normal crit roll; `2` KB; `0.02s` (`1`f) hitstop |
| **Rainblade 2 — Main** | `0.57s` (`34`f) Tap / `0.37s` (`22`f) Hold / `0.37s` (`22`f) Auto-Advance *(CD: `0.50s` / `0.30s`)* | `0.32s` (`19`f) / `0.20s` (`12`f) | `< 112.5 tiles` | Up to **`3`** per enemy | **Vanilla**: **`105%` + `45%`/knot** (`240%` max main; `275%` combined)<br>**Calamity**: **`120%` + `55%`/knot** (`285%` max main; `325%` combined) | Normal crit roll; `4` KB; `0.05s` (`3`f) hitstop |
| **Rainblade 3** *(No Preemptive)* | `0.63s` (`38`f) Tap / `0.40s` (`24`f) Hold / `0.40s` (`24`f) Auto-Advance *(CD: `0.57s` / `0.33s`)* | `0.20s` (`12`f) / `0.12s` (`7`f) | `< 112.5 tiles` | Up to **`3`** per enemy | **Vanilla**: **`140%` + `45%`/knot** (`275%` max)<br>**Calamity**: **`160%` + `55%`/knot** (`325%` max) | Normal crit roll; `4` KB; `0.08s` (`5`f) hitstop |
| **Stygian Resurge** *(Queued -> Snap)* | `0.77s` (`46`f) Tap / `0.47s` (`28`f) Hold / `0.50s` (`30`f) Auto-Advance *(Unscaled)* | Cleave: `0.33s` (`20`f) / `0.20s` (`12`f); Damage lands at `3.57s` (`214`f into sheathing) | `< 137.5 tiles` | **All remaining** (`0–9`) per enemy | **Vanilla**: **`500%` + `30%`/knot** (`770%` at `9` knots)<br>**Calamity**: **`580%` + `35%`/knot** (`895%` at `9` knots); refunds `Overflow Dream` (`0–3`) on cleave impact | **Guaranteed Crit** (`100%`); normal defense (`0%` extra base pen); `10` KB; `0.08s` (`5`f) cleave + `0.13s` (`8`f) snap hitstop |

---

## 3. Nihility Charms (18 Inscriptions)

Naught has three mutually exclusive Charm sockets in the **Nihility Ledger**: **Soul** (Slot 0), **Tide** (Slot 1), and **Ruin** (Slot 2). Only `1` Charm per slot can be active at once. Unlocks are checked dynamically against the current world's defeated bosses (locked Charms remain socketed in your character loadout as `[Sealed in current world]` without losing your setup).

### 3.1 Soul Inscriptions (Slot 0)

#### 1. Mark of the Red Oni
- **Unlock Requirement**: Unlocked at journey's start (Pre-Boss).
- **Summary**: Frontloads Ultimate and Crimson Knot buildup once per boss encounter and awards Slashed Dream bursts whenever bosses cross 25% HP thresholds.
- **Effects & Numbers**:
  - **Opener Floor**: Triggers on world entry, respawn, and **once per boss encounter** upon entering boss combat within `112.5 tiles`:
    - Raises Slashed Dream to at least **`4`** (**Vanilla**) / **`5`** (**Calamity**) (no effect if already at or above the floor).
    - While carrying Naught in hand, on the cursor, or in the inventory, adds enough Crimson Knots to reach at least **`4`** (**Vanilla**) / **`5`** (**Calamity**) stacks on the highest-priority valid enemy below `9` stacks within `112.5 tiles` (`Boss > Normal Enemy > Target Dummy`, tie-broken by highest current HP).
  - **Boss HP Milestones**: Crossing `75%`, `50%`, and `25%` max HP on any boss grants **`+1` Slashed Dream** (**Vanilla**, up to `+3` total per boss) or **`+2` Slashed Dream** (**Calamity**, up to `+6` total per boss) per threshold crossed, spilling naturally into Overflow Dream.
  - **Lethal & Multi-Threshold Bursts**: Skipping multiple thresholds in a single hit or killing a boss in a burst awards `+1` (**Vanilla**) / `+2` (**Calamity**) Slashed Dream for every newly crossed milestone.
  - **Feedback**: Displays `+N Slashed Dream` combat text on the player when a milestone triggers.
- **Restrictions & Synergies**:
  - **Once-Per-Boss-Encounter Opener Guard**: The boss combat opener floor triggers **at most once per boss encounter**, resetting only upon world entry, respawn, or after **no boss has been alive anywhere in the world for at least `5.00s (300 frames)`** (paused during World Time-Stop). Leaving `112.5 tiles` and re-entering mid-fight cannot re-trigger the opener.
  - Boss combat openers and HP milestones require Naught to be present in hand, on the cursor, or in the inventory (it does not need to be actively held); world-entry and respawn openers still grant the Slashed Dream floor even without Naught in inventory, but only apply Crimson Knots if Naught is carried.
  - Boss HP milestones are evaluated within `150 tiles` of the boss's primary entity (or `112.5 tiles` of any active boss segment; `150 tiles` for pooled Eater of Worlds segments) and track each boss's peak observed HP ratio so heals, phase resets, or 1-HP intro animations cannot re-trigger claimed milestones.
  - Multi-segment bosses share a single milestone pool (including Eater of Worlds, whose active segments are summed into one combined HP pool).
  - Stacks cleanly with Overflow Dream (`0–3` base, or `0–5` with **E5**).

#### 2. Echo of the Nameless Decoy
- **Unlock Requirement**: Defeat **King Slime**.
- **Summary**: Expands each Shadow Clone cycle completion with extra localized orbital slashes (capped per mode) and an additional Crimson Knot.
- **Effects & Numbers**:
  - **Extra Cycle Slashes**: Adds `+1` localized orbital slash per expiring clone on cycle completion, capped at **`+3` bonus slashes** in **Vanilla** (`7` basekit max; `8` with **E6**) or **`+4` bonus slashes** in **Calamity** (`8` basekit max; `9` with **E6**).
  - **Slash Stats**: Each orbital slash deals `50% weapon damage` (`55%` with **E5**) as True Melee damage (or Melee in Vanilla) with normal crit rolls and `3.5` knockback, dispatching `0.07s (4 frames)` apart against the highest-priority valid enemy within `112.5 tiles` (`Boss > Normal Enemy > Target Dummy`, tie-broken by highest current HP; re-acquires around the player if out of clone range or if the target dies mid-volley).
  - **Bonus Crimson Knot**: Adds a flat `+1` Crimson Knot to the basekit cycle payout on the highest-priority valid enemy below `9` stacks within `112.5 tiles` (`+2` Crimson Knots total per cycle, regardless of expiring clone count).
- **Restrictions & Synergies**:
  - Synchronized clones expiring on the same frame still grant only `1` cycle payout (`+1` Slashed Dream and `+2` Crimson Knots total), while contributing up to the mode's bonus slash cap (`+3` Vanilla / `+4` Calamity).
  - Does not shorten the `9.00s` clone lifetime and has no direct Ultimate-cast effect.
  - **Shadow Marionette Tethers**: Each `15.00s` periodic pulse uses the current active clone count (up to `4`, capped at `+3` bonus slashes in Vanilla / `+4` in Calamity); clones silently evicted by the `4`-clone cap do not trigger cycle completion.
  - **Synchronized Execution**: Clones dissipated by Sheathing Cross-Slash suppress cycle completion on death and do not trigger this charm's slashes or bonus knot.
  - **E5 / E6 & Time-Stop**: Scales additively with **E6** (`+1` base slash) and **E5** (`55% weapon damage` per slash); pending cycle slashes pause during Ultimate time-stop and resume afterward.

#### 3. Fleeting Petal Step
- **Unlock Requirement**: Defeat **Eye of Cthulhu**.
- **Summary**: Reduces Flash Step cooldown and rewards dashing through an enemy with +1 Slashed Dream and a 2.50s global hostile projectile slowdown.
- **Effects & Numbers**:
  - **Dash Cooldown Reduction**:
    - **Vanilla Mode**: Reduces Flash Step cooldown by **`15%`**, from `2.00s (120 frames)` to **`1.70s (102 frames)`**.
    - **Calamity Mode**: Reduces Flash Step cooldown by **`20%`**, from `1.50s (90 frames)` to **`1.20s (72 frames)`**.
  - **Pass-Through Trigger**: Passing through any valid enemy along the dash path (start hitbox, end hitbox, swept line, or mid-dash contact; includes armored worm segments, Target Dummies, and invulnerable boss phases; excludes friendly/town NPCs and `<= 5` max HP critters) triggers once per **`30.00s` (Vanilla)** / **`35.00s` (Calamity)** shared cooldown:
    - Grants `+1` Slashed Dream (overflows into Overflow Dream if at `9/9`).
    - Slows all active and newly spawned hostile projectiles worldwide for **`2.50s (150 frames)`**.
  - **Hostile Projectile Slowdown**: Slowed hostile projectiles advance AI at `50%` cadence (every other sub-update), glide at `50%` velocity every frame, preserve remaining lifetime on skipped AI steps, and immediately restore `100%` velocity when the `2.50s` window ends.
  - **Visual Cue**: Slowed hostile projectiles tint rose-crimson while active.
- **Restrictions & Synergies**:
  - The `+1` Slashed Dream and `2.50s` slowdown share a single `30.00s` (**Vanilla**) / `35.00s` (**Calamity**) cooldown and can trigger at most once per dash.
  - During Ultimate time-stop, **both** the `2.50s` slowdown timer and the `30.00s` / `35.00s` trigger cooldown **pause** (restoring unscaled projectile velocities before time-stop freezes them, then resuming afterward).
  - The active `2.50s` slowdown clears immediately upon death, world entry, respawn, or unequipping the charm.

#### 4. Mirage Weft
- **Unlock Requirement**: Defeat **The Destroyer**.
- **Summary**: Reduces Flash Step cooldown and links all active Shadow Clones to the player in a chronological chain of cutting threads that deal True Melee damage and channel on-hit debuffs.
- **Effects & Numbers**:
  - **Dash Cooldown Reduction**:
    - **Vanilla Mode**: Reduces Flash Step cooldown by **`20%`**, from `2.00s (120 frames)` to **`1.60s (96 frames)`**.
    - **Calamity Mode**: Reduces Flash Step cooldown by **`25%`**, from `1.50s (90 frames)` to **`1.13s (68 frames)`**.
  - **Thread Chain Geometry**: Maintains a single persistent thread chain linking all active clones chronologically (`Oldest Clone -> ... -> Latest Clone -> Player`) with a `1.0 tile` hit width along each segment; despawns when no clones remain or when unequipped.
  - **Damage & Hit Rate**:
    - **Vanilla Mode**: Deals **`25%` weapon damage** as Melee/True Melee damage with `0` knockback, normal crit rolls, infinite pierce, and a **`1.00s (60 frames)`** per-enemy hit cooldown.
    - **Calamity Mode**: Deals **`35%` weapon damage** as True Melee damage with `0` knockback, normal crit rolls, infinite pierce, and a **`0.85s (51 frames)`** per-enemy hit cooldown.
  - **On-Hit Procs & Dedicated `9.00s` Debuff Cap**: Thread hits count as standard True Melee projectile hits, triggering weapon flasks/imbues, melee-debuff gear, and Tide on-hit debuffs. Any new or refreshed (`> 1.00s` duration jump) debuff applied by the threads grants at most `+1` Slashed Dream and `+1` Crimson Knot (on the struck enemy or debuffed anchor) on a dedicated `9.00s` shared cooldown—separate from normal per-debuff `9.00s` cooldowns and the Global Debuff-to-Dream ICD—and suppresses normal debuff-scan triggers for that application frame.
- **Restrictions & Synergies**:
  - **World Time-Freeze**: Thread geometry stays active during Ultimate time-stop so links remain aligned with clones, while threads deal `0` damage and the `9.00s` debuff-resource cooldown **pauses** during time-stop.
  - **Tide & Soul Interactions**: Thread hits benefit from **Still Water Basin**, **E1**, **Grip of the Abyss**, and **Scourge of the Withered River**, apply **Grip of the Abyss** / **Withered River**, and trigger **Shadow Marionette Tethers**'s `10%` per-clone damage sharing.
  - **Shadow Marionette Tethers**: Indefinite clones keep the cutting thread chain active until death or unequipping.

#### 5. Rainleaf Phasing
- **Unlock Requirement**: Defeat **Plantera**.
- **Summary**: Triggers invulnerable void phasing, movement haste, and Slashed Dream when dropping to the lethal/low-HP threshold or surviving a fatal hit.
- **Effects & Numbers**:
  - **Trigger Conditions** *(when off cooldown and not already phasing; threshold = **`1` HP** in **Vanilla** or **`10%` max HP** [min `1` HP] in **Calamity**)*:
    - An incoming hit would leave current HP at or below the threshold.
    - Damage-over-time or life-drain drops HP from above the threshold to at or below the threshold, or actively drains HP while already at or below it (simply equipping the charm out of combat while low HP does not trigger it).
    - Any lethal hit or bypass effect reducing HP to `<= 0` is intercepted, clamping/restoring HP to **`1` HP** (**Vanilla**) or **`10%` max HP** (**Calamity**, min `1` HP) and activating phasing.
  - **Phasing Effects**:
    - **Duration**: **`1.75s (105 frames)`** in **Vanilla**; **`2.00s (120 frames)`** in **Calamity**.
    - Grants full invulnerability to contact hits, projectiles, and lethal effects, and clamps negative life regeneration (DoTs deal `0` damage while phasing).
    - Multiplies movement speed and run acceleration by **`+30%` (`1.30×`)** in **Vanilla** or **`+35%` (`1.35×`)** in **Calamity**.
    - Grants **`+1` Slashed Dream** (**Vanilla**) or **`+2` Slashed Dream** (**Calamity**) immediately on trigger.
    - Does not restrict attacking, dashing, toggling Nihility Domain, or casting Ultimate.
  - **Cooldown**: **`120.0s (7200 frames)`** in **Vanilla**; **`105.0s (6300 frames)`** in **Calamity**. Displays a non-removable **Rainleaf Cooldown** debuff icon (cannot be removed by the Nurse; **pauses** during World Time-Stop; plays a ready chime on expiry).
  - **Visual Cue**: Player turns translucent void-violet while phasing.
- **Restrictions & Synergies**:
  - Non-lethal hits that cross the threshold still deal their normal damage; only lethal intercepts clamp/restore HP to `1` HP (Vanilla) / `10%` max HP (Calamity).
  - Both the phasing timer and cooldown reset on world entry or respawn.
  - **Siphon of the Departed**: Movement speed bonuses do not stack; while Rainleaf Phasing is active, its `+30%` / `+35%` speed overrides Siphon's `+15%` speed.
  - **Shadow Marionette Tethers**: **Rainleaf Cooldown** is excluded from debuff sharing.

#### 6. Still Water Basin
- **Unlock Requirement**: Defeat **Empress of Light**.
- **Summary**: Rewards holding Naught without taking damage for 15.00s with +15% True Melee crit chance and +15% (Vanilla) / +18% (Calamity) crit damage.
- **Effects & Numbers**:
  - **Focus Buildup**: Charges while alive, equipped with the charm, and actively holding Naught, activating **Still Water** after `15.00s` without taking damage.
  - **Active Bonuses**: While **Still Water** is active and Naught is held:
    - **`+15%`** True Melee Crit Chance on Naught (both modes).
    - **`+15%` Crit Damage** (`+0.15` crit damage multiplier) in **Vanilla**, or **`+18%` Crit Damage** (`+0.18` crit damage multiplier) in **Calamity**, on all Naught Melee and True Melee hits.
  - **Buff Cue**: Displays a **Still Water** buff icon while active.
- **Restrictions & Synergies**:
  - **Damage Reset**: Taking any non-negated hit dealing `> 0` damage resets focus progress to `0` (`15.00s` to rebuild). Attacks avoided via Flash Step i-frames, standard i-frames, Rainleaf Phasing, or Ultimate invulnerability do not reset focus.
  - **Weapon Swap Behavior**: Switching away from Naught pauses accumulation and temporarily suspends the bonuses without resetting stored progress; unequipping the charm, dying, entering a world, or respawning resets progress to `0`.
  - **Crit Damage Scope**: Applies to all Naught Melee/True Melee attacks (4-beat combo, cycle slashes, Mirage Weft threads, Blood Lotus shockwaves, Blood Blossom Needles, Void Fissures, Thunder Core bolts, Synchronized Execution slashes, Rainblades, and guaranteed-crit Stygian Resurge), but does not apply to **Shadow Marionette Tethers**'s echoed damage-share strikes.
  - Stacks additively with **Emancipator's Severed Will** (`+15%` Vanilla / `+20%` Calamity crit chance) and **E1** (`+6%` Vanilla / `+8%` Calamity crit chance against debuffed enemies).

---

### 3.2 Tide Inscriptions (Slot 1)

#### 1. Grip of the Abyss
- **Unlock Requirement**: Unlocked at journey's start (Pre-Boss).
- **Summary**: Inflicts Grip of the Abyss on Naught hits and multiplies True Melee damage dealt to afflicted targets based on their unique active debuffs.
- **Effects & Numbers**:
  - **On-Hit Debuff**: All Naught weapon and projectile hits inflict **Grip of the Abyss** on the target's primary entity for `6.00s`. Refreshes are throttled until remaining duration drops to `<= 4.50s`, creating a `>= 1.50s` duration jump that reliably triggers Naught's `9.00s` debuff-refresh Slashed Dream / Crimson Knot cycle on its own.
  - **True Melee Damage Amplification** *(multiplicative final damage increase per unique active debuff across the target and all segments of a multi-segment boss)*:
    - **Vanilla Mode**: **`+6%`** per unique debuff, capped at `3` debuffs (**`+18%` max**).
    - **Calamity Mode**: **`+4%`** per unique debuff, capped at `4` debuffs (**`+16%` max**).
  - **Visual Cue**: Emits void-violet and crimson motes on afflicted enemies.
- **Restrictions & Synergies**:
  - Only boosts True Melee damage class hits (covers Naught's basekit and all charm projectiles, but not non-True-Melee weapons).
  - **Grip of the Abyss** itself counts as `1` of the unique active debuffs toward the cap.
  - On multi-segment bosses (worms), unique debuffs are unioned across the struck segment, the primary entity, and all active segments so debuffs on any segment count once toward the cap.
  - Excluded from **Shadow Marionette Tethers** debuff sharing and not applied by Shadow Marionette's echoed damage-share hits.

#### 2. Blood Lotus Transmission
- **Unlock Requirement**: Defeat **Eater of Worlds** or **Brain of Cthulhu**.
- **Summary**: Upgrades Crimson Knot death inheritance to transfer full stacks to up to 2 enemies while erupting in an expanding True Melee Blood Lotus shockwave with a global hit cooldown.
- **Effects & Numbers**:
  - **Dual Knot Inheritance**: When an enemy carrying `>= 1` Crimson Knot dies, its full Crimson Knot stacks (`1–9`) transfer to up to `2` unique enemy anchors within `112.5 tiles`, prioritizing targets below `9` Crimson Knots first (falling back to maxed targets only if needed), then ranking `Boss > Normal Enemy > Target Dummy`, then descending current HP.
  - **Shockwave Explosion**: Simultaneously spawns an expanding shockwave at the dying enemy's center:
    - **Vanilla Mode**: Deals **`85% + 10% per Crimson Knot`** (`95%` at `1` knot up to `175%` at `9` knots) as Melee/True Melee damage, expanding on a square-root curve from `0.63 tiles` to **`11.50 tiles`** over `0.58s (35 frames)`.
    - **Calamity Mode**: Deals **`100% + 12% per Crimson Knot`** (`112%` at `1` knot up to `208%` at `9` knots) as True Melee damage, expanding on a square-root curve from `0.63 tiles` to **`13.00 tiles`** over `0.58s (35 frames)`.
    - Normal crit rolls, `5.0` knockback, infinite pierce, and a **`0.40s (24 frames)` global per-enemy hit cooldown**.
- **Restrictions & Synergies**:
  - **Global Overlap Cooldown**: The `0.40s (24 frames)` per-enemy hit cooldown is **shared globally** per player across all active Blood Lotus shockwaves, preventing simultaneous add kills from stacking shockwave hits onto the same boss.
  - Does not trigger if the dying enemy has `0` Crimson Knots.
  - Multi-segment bosses are deduplicated by primary entity so `2` segments of the same worm cannot take both transfer slots.
  - **World Time-Freeze**: The shockwave is exempt from time-stop, allowing kills scored during Ultimate Rainblades or Stygian Resurge to expand and deal damage immediately.
  - **Not an Ultimate Strike**: Does not receive **E4** (`+8%` in Domain) or **Severance of Karma** (`15%` / `12%` Boss bonus/DR peel), though it benefits from **Still Water Basin**, **E1**, **Grip of the Abyss**, and **Scourge of the Withered River**.

#### 3. Shadow Marionette Tethers
- **Unlock Requirement**: Defeat **The Twins**.
- **Summary**: Converts Shadow Clones into indefinite puppet anchors (capped at 4 with a 15.00s cycle pulse) that tether up to 3 enemies each to slow/leash non-bosses, share debuffs, and echo 10% True Melee damage per clone.
- **Effects & Numbers**:
  - **Clone Cap & Lifetime Override**: Clones persist indefinitely until the player dies, unequips the charm (resuming normal `9.00s` decay), or exceeds the cap of `4` concurrent clones; spawning a 5th clone silently evicts the oldest clone without triggering cycle completion.
  - **Periodic `15.00s` Cycle Pulse (Tradeoff)**: Replaces the `9.00s` expiry cycle with a periodic pulse every `15.00s` while `>= 1` clone is active (centered on the newest clone), granting `+1` Slashed Dream, `+1` Crimson Knot, and `4` localized orbital slashes (`50% weapon damage` True Melee each; `5` base slashes with **E6**; `55%` with **E5**).
  - **Tether Acquisition**: Refreshes on spawn and every `0.20s (12 frames)`. Each clone links up to `3` nearest unique enemy anchors within `21.88 tiles` (includes bosses and Target Dummies; excludes friendly/town NPCs, invulnerable non-dummy targets, and `<= 5` max HP critters), deduplicating worms to `1` slot per clone.
  - **Damage Sharing**: When any Naught Melee or True Melee hit deals `> 0` damage to a tethered enemy, all other enemies tethered by the same clone(s) take **1 consolidated True Melee strike** for **`10% × (overlapping clones tethering both source and target, max 4)`** of the damage dealt, capped at **`35%` max** in **Vanilla** or **`40%` max** in **Calamity**:
    - **True Ultimate Share Scaling**: When triggered by a **true Ultimate strike** (Rainblades, Stygian Resurge, Thunder Core bolts, Blood Blossom Needles, Void Fissures, or Synchronized Execution slashes), shared damage is multiplied by **`0.50×`** (`5%`–`17.5%` Vanilla; `5%`–`20%` Calamity). E6-inherited combo/cycle/EX hits retain the full `10%`–`35%` / `40%` share rate.
    - Shared strikes cannot crit, deal `0` knockback, ignore hit modifiers (**Still Water Basin**, **E4**, **Severance of Karma**, etc.) to prevent double-scaling, and never recursively trigger further sharing.
    - Non-boss, non-dummy targets with knockback susceptibility also have horizontal velocity multiplied by `0.65` when shared damage lands.
  - **1-Hop Debuff Sharing & Knot Cap**:
    - Copies active non-internal debuffs from tethered enemies onto co-tethered targets (1-hop only; shared debuffs cannot propagate further, and excludes **Grip of the Abyss**, **Withered River**, **Shadow Crucible**, and **Rainleaf Cooldown**).
    - Shared debuffs grant **`0` Slashed Dream** and never trigger normal per-debuff Crimson Knots; instead, receiving shared debuffs grants at most **`+1` Crimson Knot total** per tethered enemy on a `9.00s` per-enemy cooldown (skipped at `9/9` stacks).
  - **Crowd Control (Non-Boss, Non-Dummy Only)**:
    - **Puppet-String Snare**: Slows tethered non-bosses by `25% / 30% / 35% / 40%` for `1 / 2 / 3 / 4` tethering clones (caps at `4`; skips teleports/fast dashes `>= 150.0 tiles/s` (`2.5 tiles/frame`)).
    - **Elastic Puppet Leash**: Pulls tethered non-bosses with knockback susceptibility toward their tethering clones' centroid when stretched between `10.94 tiles` slack radius and `27.34 tiles` max leash.
  - **Tethered Knot Cascade**: On death of a knotted enemy, Crimson Knots also propagate once to each surviving tethered anchor (excluding the basekit inheritance target).
  - **Visual Cue**: Draws crimson puppet strings and a `1–4` pip bind ring on tethered enemies.
- **Restrictions & Synergies**:
  - **World Time-Freeze**: During Ultimate time-stop, the `15.00s` cycle timer, `9.00s` shared-knot cooldown, debuff sharing, and crowd-control slow/leash pause cleanly.
  - **Bosses & Target Dummies**: Exempt from snare, elastic leash, and horizontal micro-yank, but fully participate in tether slots, damage sharing, debuff sharing, and death cascades.
  - **Soul & Ruin Synergies**: Stacks with **Echo of the Nameless Decoy** (`+1` extra slash per active clone on each `15.00s` pulse up to `+3`/`+4` + `+1` flat Crimson Knot), **Mirage Weft** (permanent thread chain across up to `4` clones + damage sharing on thread hits), **Rift of the Void Horizon** (fissures open on up to `4` clones), and **Synchronized Execution** (clones stay indefinite until Sheathing Cross-Slash dissipates them without triggering cycle completion).
  - **No Ultimate Inheritance**: Shared damage strikes never count as Ultimate strikes even when triggered by an Ultimate attack or **E6**.

#### 4. Scourge of the Withered River
- **Unlock Requirement**: Defeat **Wall of Flesh** (Hardmode).
- **Summary**: Extends newly applied boss debuffs and inflicts Withered River on Naught hits to peel enemy Damage Reduction / amplify all incoming damage and sustain 9.00s debuff-refresh cycles.
- **Effects & Numbers**:
  - **Boss Debuff Duration Extension**: Any debuff newly applied to a boss (including worm anchors and Eater of Worlds segments) has its duration multiplied by **`1.50×` (`+50%`)** in **Vanilla** or **`1.40×` (`+40%`)** in **Calamity** once on initial application (does not compound on refreshes).
  - **On-Hit Withered River Debuff**: All Naught weapon and projectile hits inflict **Withered River** on the target's anchor for `6.00s` base (`9.00s` on bosses in Vanilla via `+50%`; `8.40s` on bosses in Calamity via `+40%`).
    - Refreshes are throttled until remaining duration drops to `<= 4.50s`, creating a `> 1.00s` duration jump to reliably trigger `+1` Crimson Knot (and `+1` Slashed Dream when the Global Debuff-to-Dream ICD is ready) every `9.00s`.
    - **Buff-Immune Fallback**: If a target is immune to the debuff or has full buff slots, a hidden fallback timer (`9.00s` Vanilla / `8.40s` Calamity on bosses, `6.00s` on non-bosses, paused during time-stop) maintains the effect and triggers the `9.00s` cycle directly.
  - **Damage Reduction Shred / Damage Amplification** *(all incoming hits against an afflicted target or any segment of its worm)*:
    - **Vanilla Mode (or `0%`-DR targets)**: Applies a flat **`+14%` (`1.14×`)** final damage multiplier in **Vanilla** (`+10%` / `1.10×` on `0%`-DR targets in **Calamity**).
    - **Calamity DR (`0% < DR <= 90%`)**: Peels up to **`10%` DR** (**Calamity**, or **`14%`** in **Vanilla** mode) ($\text{newDR} = \max(0,\ \text{DR} - \text{Peel})$), scaling final damage by $\frac{1 - \text{newDR}}{1 - \text{DR}}$. Any unspent peel converts to multiplicative bonus damage ($\times (1 + \text{remainder})$).
    - **Calamity DR (`> 90%`)**: DR peel is skipped (anti-cheese guard on phase-shielded bosses).
  - **Visual Cue**: Displays an ashen-crimson seal on afflicted enemies.
- **Restrictions & Synergies**:
  - The `+50%` (**Vanilla**) / `+40%` (**Calamity**) debuff duration bonus applies only to bosses (not regular enemies or Target Dummies).
  - The damage amplification / DR shred applies to **all** incoming hits against the afflicted enemy, not only Naught attacks.
  - **Severance of Karma**: When combined with Severance of Karma on Ultimate strikes against bosses, both peels are evaluated in a single pass (`29%` Vanilla / `22%` Calamity on true Ultimate strikes; `21.5%` Vanilla / `16%` Calamity on E6-inherited strikes; still skipped if Calamity DR `> 90%`) rather than double-multiplying.
  - Excluded from Nurse removal, Expert debuff extension, and **Shadow Marionette Tethers** debuff sharing.

#### 5. Shadow Crucible
- **Unlock Requirement**: Defeat **Golem**.
- **Summary**: Surrounds every Shadow Clone with an 18.0-tile furnace aura that amplifies enemy DoTs and detonates stored DoT damage on cycle expiry while ignoring 100% defense.
- **Effects & Numbers**:
  - **Furnace Aura (`18.0 tiles` radius)**: Every active Shadow Clone projects an `18.0-tile` aura (checked against enemy hitbox bounds, deduplicated per primary entity).
  - **DoT Amplification & Storage**: Enemies inside overlapping clone auras have all active DoT rates and DoT damage popups multiplied:
    - **Vanilla Mode**: `1 / 2 / 3+` clones = **`1.75× / 2.00× / 2.25×`**; stores **`50%`** of the amplified DoT HP accumulated while inside the aura(s).
    - **Calamity Mode**: `1 / 2 / 3+` clones = **`1.25× / 1.40× / 1.55×`**; stores **`40%`** of the amplified DoT HP accumulated while inside the aura(s).
    - Brands affected anchors with **Shadow Crucible** (cannot be removed by the Nurse). Leaving the aura pauses amplification/accumulation while keeping the stored charge until cycle expiry.
  - **Cycle-Expiry Detonation**: Upon Shadow Clone cycle completion, every branded enemy anywhere in the world detonates once (clearing the brand):
    - **Vanilla Mode Formula**: $\max(50\%\text{ of stored amplified DoT},\ \min(\text{other unique debuffs},\ 4) \times 30\%\text{ weapon damage})$ (`30%`–`120%` non-DoT floor); capped at **`500% weapon damage`**, and further capped at **`5%` of boss max HP** on non-dummy bosses.
    - **Calamity Mode Formula**: $\max(40\%\text{ of stored amplified DoT},\ \min(\text{other unique debuffs},\ 4) \times 35\%\text{ weapon damage})$ (`35%`–`140%` non-DoT floor); capped at **`600% weapon damage`**, and further capped at **`4%` of boss max HP** on non-dummy bosses.
    - **Hit Properties**: Deals True Melee damage (or Melee in Vanilla), ignores `100%` enemy defense, cannot crit, deals `0` knockback, and ignores external hit modifiers.
  - **Visual Cue**: Clones display an `18.0-tile` furnace ring and branded enemies show a charge-arc seal.
- **Restrictions & Synergies**:
  - Maximum `3` overlapping clones contribute to DoT amplification (`2.25×` Vanilla / `1.55×` Calamity cap), and worms count each clone at most once per primary entity.
  - **Shadow Crucible** itself does not count toward the `other unique debuffs` floor.
  - **World Time-Freeze**: DoT ticking, amplification, and accumulation pause cleanly during Ultimate time-stop.
  - **Synchronized Execution**: Clones dissipated by Sheathing Cross-Slash suppress cycle completion and do not trigger Crucible detonation.
  - Detonation strikes the nearest visible damageable segment on multi-segment bosses.

#### 6. Siphon of the Departed
- **Unlock Requirement**: Defeat **Queen Bee**.
- **Summary**: Heals 1.5% max HP per Crimson Knot detonated during Ultimate (up to 13.5% per Ultimate) and grants +15% movement speed and acceleration for 8.00s afterward.
- **Effects & Numbers**:
  - **Cumulative Knot Lifesteal**: Every Crimson Knot detonated during Rainblades 1–3 or Stygian Resurge heals for **`1.5%` of max HP**, capped at **`9` knots (`13.5%` max HP total per Ultimate)** in both modes. Computed cumulatively across the Ultimate so fractional rounding never loses HP.
  - **Post-Ultimate Haste**: When the Ultimate ends (if `>= 1` Crimson Knot was detonated during that Ultimate), multiplies movement speed and run acceleration by `+15%` (`1.15×`) for **`8.00s`**, displaying a **Siphon Haste** buff icon.
- **Restrictions & Synergies**:
  - Healing is hard-capped at `13.5%` max HP per Ultimate cast even when detonating more than `9` total knots across multiple enemies.
  - Grants neither healing nor the `8.00s` haste buff if `0` Crimson Knots were detonated during the Ultimate.
  - The `8.00s` haste timer pauses during Ultimate / world time-stop and clears on death or unequipping the charm.
  - **Rainleaf Phasing**: Does not stack with Rainleaf Phasing's speed boost (`+30%` Vanilla / `+35%` Calamity); while Rainleaf Phasing is active, its speed takes priority over Siphon's `+15%` speed.

---

### 3.3 Ruin Inscriptions (Slot 2)

#### 1. Thunder Core
- **Unlock Requirement**: Unlocked at journey's start (Pre-Boss).
- **Summary**: Calls down a staggered 9-bolt sky barrage after Stygian Resurge while granting a sustained post-Ultimate weapon damage buff.
- **Effects & Numbers**:
  - **9-Bolt Sky Barrage**: Queued at the Ultimate's delayed sheathing snap (`3.57s (214 frames)` after sheathing begins) and begins dispatching once the Ultimate time-stop ends.
  - **Dispatch Cadence**: Fires `9` bolts sequentially, `1` bolt every `0.30s (18 frames)` (`2.40s` span from first to ninth bolt).
  - **Bolt Damage & Crit**: Each bolt deals **`45%` weapon damage** (**Vanilla**) or **`60%` weapon damage** (**Calamity**) as **True Melee** (or Melee in Vanilla) with a **guaranteed critical hit**, `6.0` knockback, and counts as a **true Ultimate strike**.
  - **Bolt Strike Radius & Scatter**: Strikes all valid enemies within **`6.88 tiles`** (`6.875 tiles`) of each bolt's impact point; each bolt lands with random scatter of **`±4.25 tiles`** horizontally and **`±1.5 tiles`** vertically around the target's center.
  - **Target Acquisition**: Tracks the primary target from the Ultimate snap; if that target dies or becomes untargetable, re-acquires the nearest valid enemy within **`100 tiles`** of the player, or falls back to the last snap target position (`12.5 tiles` ahead of the player if none existed).
  - **Debuff & Resource Cap**: Each damaging bolt inflicts **Electrified** for **`3.00s`**. Across all `9` bolts, targets, and debuff types triggered or refreshed by the barrage, grants at most **`+1` Slashed Dream** and **`+1` Crimson Knot** total per Ultimate barrage (still subject to the `9.00s` per-debuff ICD and Global Debuff-to-Dream ICD).
  - **Post-Ultimate Damage Buff**: When the Ultimate ends after reaching the sheathing snap, grants **`+10%` additive weapon damage for `10.00s`** (**Vanilla**) or **`+12%` additive weapon damage for `12.00s`** (**Calamity**) to Naught (scaling both direct swings and all Naught abilities/charm effects).
- **Restrictions & Synergies**:
  - The post-Ultimate damage buff pauses its timer if another time-stop becomes active and begins ticking once the player regains control.
  - Barrage bolts remain queued and do not fire while a time-stop is active.
  - Because each bolt counts as a **true Ultimate strike**, it benefits from **E4 (Shrine in Flame)**'s `+8%` Domain Ultimate multiplier and **Severance of Karma**'s full defense penetration and Boss DR penetration / Vanilla Boss bonus.

#### 2. Severance of Karma
- **Unlock Requirement**: Defeat **Skeletron Prime**.
- **Summary**: Pierces enemy defense across all true Ultimate strikes, penetrates Boss Damage Reduction / amplifies Boss Ultimate damage, and boosts Stygian Resurge damage by +15%.
- **Effects & Numbers**:
  - **True Ultimate Defense Penetration**: All **true Ultimate strikes** (Rainblades 1–3, Stygian Resurge, Thunder Core bolts, Blood Blossom Needles, Void Fissures, and Synchronized Execution clones) ignore **`35%` enemy defense** (**Vanilla**) or **`40%` enemy defense** (**Calamity**).
  - **Stygian Resurge Bonus**: Multiplies Stygian Resurge's queued damage by **`+15%` (`1.15×`)** in both modes.
  - **Boss DR Penetration / Boss Damage Amplification** *(on Ultimate strikes against Boss targets, including boss segments and Eater of Worlds)*:
    - **Vanilla Mode** *(or `0%`-DR targets)*: Applies **`+15%` (`1.15×`)** final damage on **true Ultimate strikes**, and **`50%` effectiveness (`+7.5%` / `1.075×`)** on **E6-inherited strikes**.
    - **Calamity Mode** *(`0% < DR <= 90%`)*: Peels **`12%` Boss DR** on **true Ultimate strikes**, and **`50%` effectiveness (`6%` Boss DR peel)** on **E6-inherited strikes**; any unspent peel converts to multiplicative bonus damage ($\times (1 + \text{remainder})$).
    - **Calamity `DR > 90%`** *(phase-shield protection)*: DR penetration and bonus damage conversion are **skipped (`1.00×`)**.
- **Restrictions & Synergies**:
  - Stacks with **Scourge of the Withered River** in a **single unified pass** for up to **`29%`** (**Vanilla**) / **`22%`** (**Calamity**) combined peel/bonus on true Ultimate strikes (`21.5%` Vanilla / `16%` Calamity on E6-inherited strikes; still skipped above `90%` DR).
  - **E6 (Apocalypse, the Emancipator)**: E6-inherited 4-beat combo swings, Shadow Clone Cycle Slashes, and EX Spatial Waves receive **`50%` effectiveness** of Severance's Boss DR peel / Vanilla Boss bonus (`6%` Calamity DR peel / `+7.5%` Vanilla Boss dmg) and do **not** receive the `35%` / `40%` true-Ultimate defense penetration.

#### 3. Rift of the Void Horizon
- **Unlock Requirement**: Defeat **Queen Slime**.
- **Summary**: Manifests gravitational void portals on active Shadow Clones after Stygian Resurge to group non-boss enemies and deal sustained True Melee damage.
- **Effects & Numbers**:
  - **Portal Manifestation**: At Stygian Resurge's delayed sheathing snap (`3.57s (214 frames)` after sheathing begins), spawns a Void Fissure portal at the center of **every active Shadow Clone**. If **`0` clones** are active, spawns **`1` fallback portal** on the primary target's center (or **`10.0 tiles`** in front of the player if no target exists).
  - **Calamity Fallback Drift**: In **Calamity** mode, when spawned as a `0`-clone fallback portal, the portal smoothly drifts toward the nearest alive boss within `112.5 tiles` at **`3.0 tiles/s`** (paused during World Time-Stop); in **Vanilla** mode, fallback portals remain stationary.
  - **Duration**: **`9.00s` (`540 frames`)**; pauses during time-stop so portals operate for their full `9.00s` after the Ultimate concludes.
  - **Damage, Radius & Hit Rate**:
    - **Vanilla Mode**: Strikes enemies overlapping within **`7.50 tiles`** for **`45%` weapon damage** every `0.40s (24 frames)`.
    - **Calamity Mode**: Strikes enemies overlapping within **`8.50 tiles`** for **`60%` weapon damage** every `0.40s (24 frames)`.
    - Deals **True Melee** (or Melee in Vanilla) damage with normal crit roll, `0` knockback, and **true Ultimate strike** status (`2.5 hits/s`, up to `23` hits over `9.00s`).
  - **On-Hit Debuff**: Inflicts **Slow** for **`2.00s`** on hit (triggering debuff-count synergies such as **Grip of the Abyss** and **E1**).
  - **Vortex Pull & Movement Slow**: Pulls eligible **non-boss** enemies whose center is within **`13.75 tiles`** inward toward the portal with a swirl (holding them inside a **`1.63-tile`** core), and slows their movement speed by **`35%`** while their hitbox overlaps the portal damage radius (`7.50 tiles` Vanilla / `8.50 tiles` Calamity) and center is within `13.75 tiles`.
- **Restrictions & Synergies**:
  - **Shared Overlap Guard**: Hit cooldown (`0.40s`) and vortex pull (`1` per frame) are tracked globally per enemy across all active Void Fissures, preventing stacked clones from multiplying portal DPS or pull velocity.
  - **Crowd-Control Exclusions**: Pull and movement slow do **not** affect bosses, boss-flagged enemies, town NPCs, immortal enemies, Target Dummies, enemies with `<= 5` max HP, knockback-immune enemies, non-head worm segments, or any segment of a boss worm.
  - **Synchronized Execution Order**: Portals spawn at the Shadow Clones' positions **before** **Synchronized Execution**'s cross-slash moves and dissipates those clones.
  - Counts as a **true Ultimate strike**, inheriting **E4** (`+8%` Domain damage) and **Severance of Karma** (`35%/40%` defense pen & `15%/12%` Boss bonus/DR peel).

#### 4. Bladesong of the Endless Rain
- **Unlock Requirement**: Defeat **Skeletron**.
- **Summary**: Accelerates Rainblade execution speed by 35% and converts every detonated Crimson Knot into a homing blossom needle capped at 2 hits per enemy.
- **Effects & Numbers**:
  - **35% Faster Rainblades**: Multiplies total duration, impact delays, sub-phase timings, and hold-to-auto-advance thresholds by **`0.65×`** for **Rainblades 1–3** (Stygian Resurge is unscaled):
    - **Rainblades 1 & 2 (Tap)**: **`0.37s (22 frames)`** duration (`0.08s (5 frames)` preemptive delay + `0.20s (12 frames)` main delay).
    - **Rainblades 1 & 2 (Hold)**: **`0.23s (14 frames)`** duration (`0.05s (3 frames)` preemptive delay + `0.13s (8 frames)` main delay); auto-advance threshold **`0.23s (14 frames)`**.
    - **Rainblade 3 (Tap)**: **`0.40s (24 frames)`** duration (`0.12s (7 frames)` impact delay).
    - **Rainblade 3 (Hold)**: **`0.25s (15 frames)`** duration (`0.08s (5 frames)` impact delay); auto-advance threshold **`0.25s (15 frames)`**.
  - **Blood Blossom Needles**: Spawns **`1` needle per Crimson Knot detonated** during **Rainblades 1–3** (up to `3` needles per Rainblade per enemy).
  - **Needle Damage & Hit Cap**: Deals **`45%` weapon damage** (**Vanilla**) or **`55%` weapon damage** (**Calamity**) as **True Melee** (or Melee in Vanilla) with normal crit roll, `2.0` knockback, **true Ultimate strike** status, **infinite crowd pierce**, a **`0.35s`** per-target hit cooldown, and a hard cap of **maximum `2` hits per enemy per needle**.
  - **Flight & Homing**: Fans outward for **`0.25s–0.34s`** (cannot hit its originating enemy/worm root during this burst), then homes on enemies with remaining hit budget within **`87.5 tiles`** for up to **`1.50s`** total flight time, slowing from its `135.0 tiles/s` cruise speed to `75%` speed (`101.25 tiles/s`) and increasing turn rate from `16.8–28.8 rad/s` to `36.0 rad/s` when passing within `11.25 tiles` while moving away from the target.
  - **Target Priority**: Prioritizes enemies **other than** its originating enemy/multi-segment root, falling back to the origin enemy only in `1v1` fights.
  - **Active Needle Cap**: Maximum **`36` active needles** per player; spawning additional needles immediately retires the oldest needle.
- **Restrictions & Synergies**:
  - Needles spawn **only** on Rainblade 1–3 detonations — Stygian Resurge's knot purge does **not** spawn needles.
  - Needles are **not** exempt from the Ultimate time-stop; they remain suspended in mid-air during the Ultimate and begin flying/homing once reality resumes.
  - Counts as a **true Ultimate strike**, inheriting **E4** (`+8%` Domain damage) and **Severance of Karma** (`35%/40%` defense pen & `15%/12%` Boss bonus/DR peel).

#### 5. Synchronized Execution
- **Unlock Requirement**: Defeat **Lunatic Cultist**.
- **Summary**: Freezes Shadow Clone expiration during the Ultimate so clones mirror all three Rainblades and converge in a guaranteed-crit sheathing cross-slash.
- **Effects & Numbers**:
  - **Ultimate Lifetime Freeze**: Non-indefinite Shadow Clones pause their `9.00s` expiry timer while the Ultimate is active, ensuring every clone present at cast participates in all `3` Mirror Rainblades and the final cross-slash.
  - **Diminishing-Returns Clone Scaling**:
    - **`1–3` clones**: **`100%` (`1.00×`)** per-clone damage (`1.00×` / `2.00×` / `3.00×` total output).
    - **`4+` clones**: Total effective clones follow $3 + (\text{SoftCap} - 3) \times \left(1 - e^{-(n - 3)/4}\right)$, asymptotically soft-capping total output at **`4.5` effective clones** (**Vanilla**) or **`5.0` effective clones** (**Calamity**). Per-clone multiplier equals $\frac{\text{Effective Clones}}{n}$.
  - **Mirror Rainblades (Stages 1–3)**: Whenever the player initiates Rainblade 1, 2, or 3, each active clone slashes all valid enemies within **`13.75 tiles`** of itself for **`35% × per-clone scale`** (**Vanilla**) or **`45% × per-clone scale`** (**Calamity**) weapon damage (`True Melee` or `Melee` in Vanilla, normal crit roll, `4.0` knockback, counts as a **true Ultimate strike**).
  - **Sheathing Cross-Slash**: At Stygian Resurge's delayed snap (`3.57s (214 frames)` into sheathing), each active clone dashes through the primary target (re-acquiring a live target or `12.5 tiles` ahead of the player if Resurge killed it), exiting **`4.0` to `15.0 tiles`** past the target's center and dealing **`95% × per-clone scale`** (**Vanilla**) or **`115% × per-clone scale`** (**Calamity**) weapon damage as a **guaranteed critical hit** (`True Melee` or `Melee` in Vanilla, `8.0` knockback, counts as a **true Ultimate strike**).
  - **Petal Dissipation**: Participating clones dissipate over **`0.30s (18 frames)`** after the cross-slash with their normal expiry payout suppressed.
- **Restrictions & Synergies**:
  - Mirror Rainblades do **not** detonate Crimson Knots.
  - **No Cycle Double-Dipping**: Participating clones do **not** grant the normal clone-cycle payout (`+1` Slashed Dream, `+1` Crimson Knot, or Cycle Slashes) when they dissipate after the cross-slash.
  - **Shadow Marionette Tethers**: Works with Shadow Marionette's `4` indefinite clones (`3.33×` Vanilla / `3.44×` Calamity total scaling), consuming and dissipating those clones at the sheathing snap.
  - **Rift of the Void Horizon**: Spawns its portals at the clones' original positions just before Synchronized Execution dashes them across the target.

#### 6. Emancipator's Severed Will
- **Unlock Requirement**: Defeat **Duke Fishron**.
- **Summary**: Refunds Slashed Dream based on total Crimson Knots detonated during the Ultimate (capped per mode) and grants a full-duration post-Ultimate crit chance surge.
- **Effects & Numbers**:
  - **Crimson Knot-to-Dream Refund**: Immediately after Stygian Resurge purges remaining knots, refunds **`1` Slashed Dream per `3` Crimson Knots detonated** ($\lfloor \text{Total Knots Detonated} / 3 \rfloor$) across all Rainblades and Stygian Resurge combined across all targets in range, capped at **`3` Dream per Ultimate** (**Vanilla**) or **`4` Dream per Ultimate** (**Calamity**).
  - **Post-Ultimate Crit Surge**: At the delayed sheathing snap (`3.57s (214 frames)` into sheathing), grants **`+15%`** (**Vanilla**) or **`+20%`** (**Calamity**) True Melee Crit Chance on Naught for **`10.00s`** (displays a positive buff icon and HUD countdown).
  - **Full-Duration Timing**: The `10.00s` crit buff timer pauses while the Ultimate or World Time-Stop is active so none of the window is lost during the remaining `0.42s (25 frames)` (`0.55s` real time) sheathing linger.
- **Restrictions & Synergies**:
  - Refunded Slashed Dream is added after basekit **Overflow Dream** restores into the main gauge and spills excess cleanly back into **Overflow Dream** (up to `3` base, or `5` with **E5**).
  - Because the crit bonus (`+15%` Vanilla / `+20%` Calamity) applies directly to Naught's weapon crit chance, it scales normal combo swings, Shadow Clone Cycle Slashes, Mirage Weft threads, and post-Ultimate charm projectiles (and raises effective crit chance for overcrit-compatible systems even on guaranteed-crit strikes).
  - Buff timer is cleared immediately upon death, unequipping the charm, or entering/respawning in a world.

---

## 4. Eidolons (`E1–E6 + EX`)

All `7` Eidolons (`E1–E6` plus `EX`) unlock dynamically per world upon defeating their milestone boss (or unconditionally when the sandbox unseal config option is enabled). Unlocked Eidolons default to active and can be toggled on or off individually in the **Nihility Ledger**.

| Rank | Numeral & Title | World Unlock Milestone | Summary of Mechanical Changes *(Vanilla / Calamity)* |
| :---: | :--- | :--- | :--- |
| **E1** | `I` · **Silenced Sky** | **Eater of Worlds** or **Brain of Cthulhu** | **`+6%`** *(Vanilla)* / **`+8%`** *(Calamity)* **Crit Chance** on True Melee hits against debuffed enemies. |
| **E2** | `II` · **Mute Thunder** | **Wall of Flesh** | Passively grants **`+1` Slashed Dream every `12.0s` (Vanilla) / `10.0s` (Calamity)** while wielding Naught in combat. |
| **E3** | `III` · **Frosty Strands** | **Any 1 Mechanical Boss** *(Destroyer, Twins, or Skeletron Prime)* | Rainblades 1–3 scale to **`150%` + `50%`/knot** *(Vanilla)* / **`170%` + `60%`/knot** *(Calamity)*; Odachi combo reach & scale **`+15%`**. |
| **E4** | `IV` · **Shrine in Flame** | **Plantera** | Enemies inside **Nihility Domain** take **`+8%` Ultimate damage**. |
| **E5** | `V` · **Strewn Petals** | **Golem** | Stygian Resurge scales to **`535%` + `35%`/knot** *(Vanilla)* / **`620%` + `40%`/knot** *(Calamity)*; **Overflow Dream cap `3 -> 5`**; Clone Cycle Slash **`50% -> 55%`**. |
| **E6** | `VI` · **Apocalypse, the Emancipator** | **Moon Lord** | Combo swings, Cycle Slashes, and EX Waves inherit Ultimate status (**full E4 `+8%`**, **`50%` Severance of Karma**); base Cycle Volley **`+1` slash (`4 -> 5`)**. |
| **EX** | `EX` · **Horizon's Severance** | **First Post-Moon Lord Boss** *(Modded)*, or **Moon Lord** *(Vanilla)* | Odachi combo reach & scale **`+15%`** (`+30%` with E3); every beat fires a **`40%`** *(Vanilla)* / **`50%`** *(Calamity)* **spatial wave** (zero double-hit). |

### Detailed Eidolon Mechanics

- **E1 — I · Silenced Sky**:
  - True Melee hits against any target carrying **at least `1` active debuff** (pooled across all active segments of multi-segment bosses/worms) gain **`+6%`** (**Vanilla**) or **`+8%`** (**Calamity**) **Crit Chance**.
  - On ability strikes (Cycle Slashes, Ultimate, Void Fissures, Synchronized Execution clones), adds `+6%` / `+8%` directly to the strike's crit chance when crit is not already guaranteed. On native colliding hits (combo swings, Mirage Weft, Blood Lotus, Bladesong needles, EX waves), applies an exact conditional `+6%` / `+8%` crit roll when below `100%` crit.
- **E2 — II · Mute Thunder**:
  - While **actively holding Naught** in combat, generates **`+1` Slashed Dream every `12.0s (720 frames)`** (**Vanilla**) or **`10.0s (600 frames)`** (**Calamity**) (spills into Overflow Dream if at `9/9`).
  - **Active Combat Condition**: Triggered by dealing or taking non-zero damage (excluding Target Dummies and friendly NPCs) within the last **`6.00s (360 frames)`**, **or** having any Boss target alive within **`225 tiles`**.
  - Pauses during time-stop; resets its progress timer if combat ends or Naught is unequipped.
- **E3 — III · Frosty Strands**:
  - **Rainblade Damage Scaling**:
    - **Vanilla Mode**: Rainblades 1 & 2 (`25%` preemptive / `75%` main split) increase to **`37.5%` preemptive + `112.5%` main (`150%` combined base)**; Rainblade 3 increases from `140%` to **`150%` base**; Crimson Knot bonus increases from `+45%` to **`+50%` per detonated knot** (up to `+150%` at `3` knots; `300%` max per Rainblade).
    - **Calamity Mode**: Rainblades 1 & 2 (`25%` preemptive / `75%` main split) increase to **`42.5%` preemptive + `127.5%` main (`170%` combined base)**; Rainblade 3 increases from `160%` to **`170%` base**; Crimson Knot bonus increases from `+55%` to **`+60%` per detonated knot** (up to `+180%` at `3` knots; `350%` max per Rainblade).
  - **Odachi Reach & Arc Scale**: Increases 4-Beat Odachi combo blade reach and crescent slash scale by **`+15%`** uniformly across all `4` beats. Does not modify combo damage multipliers (`100% / 100% / 100% / 140%`).
- **E4 — IV · Shrine in Flame**:
  - Enemies positioned within the active **Nihility Domain** (`Domain radius > 1 tile` from its activation origin while open) take **`+8%` multiplicative Ultimate damage (`1.08×`)**.
  - Applies at full `1.08×` strength to both **true Ultimate strikes** and **E6-inherited strikes**. Pure damage multiplier — applies **zero** debuffs.
- **E5 — V · Strewn Petals**:
  - **Stygian Resurge Scaling**:
    - **Vanilla Mode**: Base damage increases from `500%` to **`535%` weapon damage**, and bonus per remaining Crimson Knot purged increases from `+30%` to **`+35%` per knot** (up to `850%` at `9` knots).
    - **Calamity Mode**: Base damage increases from `580%` to **`620%` weapon damage**, and bonus per remaining Crimson Knot purged increases from `+35%` to **`+40%` per knot** (up to `980%` at `9` knots).
  - **Overflow Dream Cap**: Increases maximum **Overflow Dream** from `3` to **`5`** (raising total storable Slashed Dream to **`14`** = `9` base + `5` overflow). Toggling E5 off immediately clamps stored Overflow Dream down to `3`.
  - **Cycle Volley Scaling**: Increases each **Shadow Clone Cycle Slash** from `50%` to **`55%` weapon damage**.
- **E6 — VI · Apocalypse, the Emancipator**:
  - **Ultimate Strike Inheritance**: Grants **E6-inherited Ultimate strike** classification to:
    1. **4-Beat Flowing Odachi Combo** *(both blade swings and crescent slash arcs)*
    2. **Shadow Clone Cycle Slashes** *(basekit, E6, and Echo of the Nameless Decoy slashes)*
    3. **EX Spatial Waves** *(from Horizon's Severance)*
    - Inherited strikes gain **full E4 (`+8%` Domain Ultimate damage)** and **`50%` Severance of Karma effectiveness** (`+7.5%` Vanilla Boss damage / `6%` Calamity Boss DR peel).
    - Inherited strikes do **not** receive Severance of Karma's `35%`/`40%` true-Ultimate defense penetration and do **not** trigger Shadow Marionette's `0.50×` true-Ultimate share reduction.
    - *(Does **not** extend Ultimate status to Mirage Weft threads, Blood Lotus shockwaves, Shadow Crucible detonations, or Shadow Marionette shared damage.)*
  - **Cycle Volley Count**: Adds **`+1` slash** to the base Shadow Clone cycle expiry volley (`4 -> 5` base slashes, stacking additively with **Echo of the Nameless Decoy**'s `+1` slash per expiring clone up to the mode's cap).
- **EX — EX · Horizon's Severance**:
  - **Unlock Rule**: Unlocks after defeating the **first Post-Moon Lord boss** (any modded boss killed after Moon Lord, or any Calamity Post-Moon Lord boss) whenever a supported Post-Moon Lord content mod (Calamity, Fargo's Souls, Thorium, Stars Above, Catalyst, Wrath of the Gods, or Mod of Redemption) is loaded; otherwise unlocks upon defeating **Moon Lord** in Vanilla.
  - **Additional Reach & Scale**: Increases 4-Beat Odachi combo blade reach and crescent slash scale by an additional **`+15%`** (stacking additively with **E3** for **`+30%` total**). Does not scale EX spatial wave size or damage.
  - **Projected Crimson Spatial Wave**: Every beat (`1–4` uniformly) launches a piercing spatial wave at `45%` of the slash swing in the beat's aim direction:
    - Deals **`40%` weapon damage** (**Vanilla**) or **`50%` weapon damage** (**Calamity**) as **True Melee** (or Melee in Vanilla) with normal crit roll, `3.58` knockback (`65%` of beat knockback), and **E6-inherited Ultimate strike** status when **E6** is active.
    - Spawns `2.0 tiles` ahead of the player with an initial speed of `1.63 tiles/frame` (`97.5 tiles/s`), decelerating by `5%` per frame over **`0.33s (20 frames)`**, with a **`6.88-tile`** wide × **`1.5-tile`** thick tilted hitbox that passes through tiles and pierces infinitely. Not exempt from time-stop.
    - **Zero Double-Hit Deduplication**: Shares a single per-beat hit list with the originating combo beat's blade and crescent arc—enemies already hit by the blade/arc on that beat cannot be struck by the wave, and enemies hit by the wave cannot be struck by the trailing blade/arc on that same beat.

---

## 5. Boss Unlock Progression Chart

Equipping charms and toggling Eidolons persist per character, while unlocks are checked dynamically against the current world's defeated bosses (so bringing a late-game character into an early-game world temporarily seals locked charms/Eidolons without erasing your loadout). Enabling **Unlock All Inscriptions & Eidolons** bypasses all boss gates.

| Progression Stage | Boss / Milestone | Unlocked Charm(s) | Charm Slot | Unlocked Eidolon |
| :--- | :--- | :--- | :---: | :--- |
| **Pre-Boss** | Journey's Start *(Craft Naught)* | **Mark of the Red Oni**<br>**Grip of the Abyss**<br>**Thunder Core** | Soul<br>Tide<br>Ruin | — |
| **Pre-Hardmode** | **King Slime** | **Echo of the Nameless Decoy** | Soul | — |
| | **Eye of Cthulhu** | **Fleeting Petal Step** | Soul | — |
| | **Eater of Worlds / Brain of Cthulhu** | **Blood Lotus Transmission** | Tide | **E1: Silenced Sky** |
| | **Queen Bee** | **Siphon of the Departed** | Tide | — |
| | **Skeletron** | **Bladesong of the Endless Rain** | Ruin | — |
| | **Wall of Flesh** | **Scourge of the Withered River** | Tide | **E2: Mute Thunder** |
| **Early–Mid Hardmode** | **Queen Slime** | **Rift of the Void Horizon** | Ruin | — |
| | **The Destroyer** | **Mirage Weft** | Soul | **E3: Frosty Strands** *(1st Mech)* |
| | **The Twins** | **Shadow Marionette Tethers** | Tide | *(E3 if 1st Mech)* |
| | **Skeletron Prime** | **Severance of Karma** | Ruin | *(E3 if 1st Mech)* |
| **Late Hardmode** | **Plantera** | **Rainleaf Phasing** | Soul | **E4: Shrine in Flame** |
| | **Golem** | **Shadow Crucible** | Tide | **E5: Strewn Petals** |
| | **Duke Fishron** | **Emancipator's Severed Will** | Ruin | — |
| | **Empress of Light** | **Still Water Basin** | Soul | — |
| | **Lunatic Cultist** | **Synchronized Execution** | Ruin | — |
| **Endgame** | **Moon Lord** | *(All 18 charms unsealed)* | — | **E6: Apocalypse, the Emancipator** *(+ **EX** in Vanilla)* |
| **Post-Moon Lord** | **First Post-Moon Lord Boss** *(Modded)* | *(All 18 charms unsealed)* | — | **EX: Horizon's Severance** |

---

## 6. Master Balance Rules, Caps & Interactions Quick-Reference

### 6.1 Resource, Entity & Damage Caps

| System | Cap / Limit *(Vanilla / Calamity)* | Notes & Overflow Behavior |
| :--- | :--- | :--- |
| **Slashed Dream** | `9` active stacks | Required to cast Ultimate (`9/9`). Excess spills into Overflow Dream. |
| **Overflow Dream** | `3` base (`5` with **E5**) | `12` (`14` with **E5**) total stored Dream. Refunded into Slashed Dream on Stygian Resurge cleave impact (`0` Overflow afterward). Toggling **E5** off immediately clamps Overflow down to `3`. |
| **Crimson Knots** | `9` stacks per enemy anchor | Parent-linked multi-segment bosses/worms pool onto 1 anchor (`0–9`); Vanilla Eater of Worlds segments store knots individually. Rainblades 1–3 detonate up to `3` per enemy; Stygian Resurge purges all remaining (`0–9`). |
| **Shadow Clones** | **`6` concurrent** basekit (`4` cap with **Shadow Marionette Tethers**) | Basekit clones sync lifetime to the active `9.00s` cycle and grant **1 cycle payout per frame**. Spawning past the cap (`7th` basekit / `5th` Marionette) silently evicts the oldest clone without cycle payout. |
| **Blood Blossom Needles** | `36` active needles per player; **`2` hits/enemy/needle** | Spawns on Rainblades 1–3 only (not Stygian Resurge); exceeding `36` retires the oldest needle immediately. Preserves infinite pierce against crowds. |
| **Shadow Crucible** | `3` overlapping clones (**`2.25×` / `1.55×`** DoT amp); **`500%` / `600%`** weapon dmg & **`5%` / `4%`** boss max HP detonation cap | Ignores `100%` enemy defense, cannot crit, skips hit modifiers; **Shadow Crucible** excluded from its own non-DoT floor (`4 × 30% = 120%` Vanilla / `4 × 35% = 140%` Calamity). |
| **Siphon of the Departed** | `9` detonated knots (**`13.5%` max HP heal**) per Ultimate | Evaluated cumulatively across all enemies hit during the Ultimate; `8.00s` `+15%` haste requires `>= 1` knot detonated. |
| **Synchronized Execution** | **`4.5`** *(Vanilla)* / **`5.0`** *(Calamity)* effective clones (asymptotic soft cap) | `1–3` clones = `100%` each; `4+` clones follow diminishing returns (see curve below). Consumed clones suppress cycle payout on dissipation. |
| **Emancipator's Severed Will** | **`3` Dream** *(Vanilla)* / **`4` Dream** *(Calamity)* per Ultimate | Refunds `1` Dream per `3` Crimson Knots detonated across the Ultimate up to the mode cap. |
| **Grip of the Abyss** | **`+18%` max** *(Vanilla: `3` × `+6%`)* / **`+16%` max** *(Calamity: `4` × `+4%`)* | Counts **Grip of the Abyss** itself toward the cap; unions unique debuffs across all active segments of multi-segment worms/bosses. |
| **4-Beat Combo Speed Floor** | `0.70x` minimum swing speed | Attack speed penalties cannot reduce combo progression below `70%` base speed. |

### 6.2 Cooldowns, Internal Cooldowns (ICDs) & Shared Cooldowns

| Mechanic | Duration / Rate *(Vanilla / Calamity)* | Scope & Time-Stop Behavior |
| :--- | :--- | :--- |
| **Flash Step Dash** | **`2.00s`** base (`1.70s` Petal; `1.60s` Weft) / **`1.50s`** base (`1.20s` Petal; `1.13s` Weft) | Player dash cooldown; **pauses** during World Time-Stop. |
| **Per-Debuff Dream/Knot ICD** | `9.00s` per debuff type | Independent per debuff type; triggers on new debuff or `> 1.00s` refresh while holding Naught within `112.5 tiles`. **Pauses** during Time-Stop. |
| **Global Debuff-to-Dream ICD** | **`1.50s`** *(Vanilla)* / **`2.50s`** *(Calamity)* | Gates `+1` Slashed Dream gain from debuff triggers (Crimson Knot still applies). **Pauses** during Time-Stop. |
| **Post-Ultimate Recast Cooldown** | **`12.00s`** *(Vanilla)* / **`14.00s`** *(Calamity)* | Begins when World Time-Stop ends after the Ultimate; blocks recasting `[V]`. **Pauses** during any active Time-Stop. |
| **Mark of the Red Oni Boss Opener** | Once per boss encounter (`5.00s` no-boss-alive reset) | Resets on world entry, respawn, or `>= 5.00s` with no boss alive anywhere in the world (`5.00s` timer **pauses** during Time-Stop). |
| **Fleeting Petal Step Trigger** | **`30.00s`** *(Vanilla)* / **`35.00s`** *(Calamity)* shared CD (`+1` Dream + **`2.50s`** slow) | Max `1` trigger per dash; both trigger CD and `2.50s` slow **pause** during Time-Stop. |
| **Mirage Weft Threads** | **`1.00s`** / **`0.85s`** per-target hit CD; `9.00s` shared debuff-resource CD | Thread-applied debuffs grant at most `+1` Dream and `+1` Knot per `9.00s` across all thread hits, separate from normal debuff & global ICDs; CD **pauses** during Time-Stop while thread damage is `0`. |
| **Rainleaf Phasing** | **`1.75s` / `2.00s`** phasing; **`120.0s` / `105.0s`** non-removable CD | Resets on world entry or respawn; cooldown **pauses** during Time-Stop. |
| **Shadow Marionette Tethers** | `15.00s` cycle pulse; `0.20s` tether refresh; `9.00s` shared-debuff Knot CD | Shared debuffs grant `0` Dream and at most `+1` Knot per enemy per `9.00s`. All Marionette timers **pause** during Time-Stop. |
| **Thunder Core Barrage** | `0.30s` bolt interval (`9` bolts); **`10.00s` `+10%`** / **`12.00s` `+12%`** dmg buff | Capped at **`+1` Dream and `+1` Knot total** across all `9` bolts/targets/debuffs per Ultimate (still gated by `9.00s` per-debuff & global ICDs). Bolts & buff **pause** during Time-Stop. |
| **Rift of the Void Horizon** | `9.00s` duration; `0.40s` shared hit CD; `1` pull/frame | Hit CD & pull guard are shared globally across all active portals per enemy. Portals, timer, and Calamity fallback drift **pause** during Time-Stop. |
| **Bladesong Needles** | `0.35s` per-target hit CD (max `2` hits/enemy); `0.25s–0.34s` origin burst immunity; `1.50s` flight | Needles are suspended (**pause**) during Time-Stop and resume flying afterward. |
| **Blood Lotus Shockwave** | `0.40s` **global** per-enemy hit CD (`0.58s` total expansion) | Global CD shared across all active shockwaves per player; **exempt** from Time-Stop (expands and damages immediately during Ultimate). |
| **Emancipator's Severed Will** | `10.00s` **`+15%`** *(Vanilla)* / **`+20%`** *(Calamity)* crit buff | **Pauses** while Ultimate or Time-Stop is active; starts full `10.00s` countdown when Ultimate ends. |
| **Siphon of the Departed** | `8.00s` `+15%` speed buff | **Pauses** during Time-Stop. |
| **E2: Mute Thunder** | `+1` Dream every **`12.00s`** *(Vanilla)* / **`10.00s`** *(Calamity)* (`6.00s` combat timer / `225 tiles` boss range) | **Pauses** during Time-Stop; resets if combat ends or Naught is unequipped. |

### 6.3 Unified Boss DR Peel Formula (`Scourge of the Withered River` + `Severance of Karma`)

Both DR-peel sources evaluate in a **single pass** (Severance of Karma adjusts Withered River's pre-applied multiplier rather than double-multiplying):

- **Vanilla Mode**:
  $$\text{Total Peel} = \underbrace{0.14}_{\text{if Withered River active}} + \underbrace{0.15 \times s}_{\text{if Severance of Karma + Boss target}} \quad (\max 0.29)$$
- **Calamity Mode**:
  $$\text{Total Peel} = \underbrace{0.10}_{\text{if Withered River active}} + \underbrace{0.12 \times s}_{\text{if Severance of Karma + Boss target}} \quad (\max 0.22)$$

where $s = 1.0$ for **true Ultimate strikes**, $s = 0.5$ for **E6-inherited strikes** (`0.075` Vanilla / `0.06` Calamity), and $s = 0$ otherwise.

- **Calamity `DR > 90%`**: Multiplier = **`1.00×`** (DR peel skipped entirely as an anti-cheese phase-shield guard).
- **Calamity `0% < DR <= 90%`**:
  - $\text{Peeled} = \min(\text{DR},\ \text{Total Peel})$
  - $\text{New DR} = \text{DR} - \text{Peeled}$
  - $\text{Remainder} = \text{Total Peel} - \text{Peeled}$
  - $\text{Final Damage Multiplier} = \dfrac{1 - \text{New DR}}{1 - \text{DR}} \times (1 + \text{Remainder})$
- **Vanilla (or Calamity `DR == 0%`)**:
  - $\text{Final Damage Multiplier} = 1 + \text{Total Peel}$ (**Vanilla**: `1.14×` Withered River alone, `1.15×` Severance true Ult alone, `1.215×` combined E6-inherited, `1.29×` combined true Ult; **Calamity `0%` DR**: `1.10×`, `1.12×`, `1.16×`, `1.22×`).

### 6.4 `Synchronized Execution` Clone Diminishing-Returns Curve

For $n$ active clones participating in the Ultimate:
- For $n \le 3$: $\text{Effective Clones} = n$, $\text{Per-Clone Scale} = 1.00$ (`100%`).
- For $n \ge 4$: $\text{Effective Clones} = 3 + (\text{SoftCap} - 3) \times \left(1 - e^{-(n - 3)/4}\right)$ (where $\text{SoftCap} = 4.5$ in **Vanilla** and $5.0$ in **Calamity**), $\text{Per-Clone Scale} = \dfrac{\text{Effective Clones}}{n}$.

| Active Clones ($n$) | Vanilla Effective Clones *(Scale)* | Vanilla Mirror / Cross-Slash *(per clone)* | Calamity Effective Clones *(Scale)* | Calamity Mirror / Cross-Slash *(per clone)* |
| :---: | :---: | :---: | :---: | :---: |
| `1` | `1.000×` (`100.0%`) | `35.0%` / `95.0%` | `1.000×` (`100.0%`) | `45.0%` / `115.0%` |
| `2` | `2.000×` (`100.0%`) | `35.0%` / `95.0%` | `2.000×` (`100.0%`) | `45.0%` / `115.0%` |
| `3` | `3.000×` (`100.0%`) | `35.0%` / `95.0%` | `3.000×` (`100.0%`) | `45.0%` / `115.0%` |
| `4` | `3.332×` (`83.3%`) | `29.2%` / `79.1%` | `3.442×` (`86.1%`) | `38.7%` / `99.0%` |
| `5` | `3.590×` (`71.8%`) | `25.1%` / `68.2%` | `3.787×` (`75.7%`) | `34.1%` / `87.1%` |
| `6` | `3.791×` (`63.2%`) | `22.1%` / `60.0%` | `4.055×` (`67.6%`) | `30.4%` / `77.7%` |
| `8` | `4.070×` (`50.9%`) | `17.8%` / `48.3%` | `4.427×` (`55.3%`) | `24.9%` / `63.6%` |
| `∞` | `4.500×` *(cap)* | `-> 0%` | `5.000×` *(cap)* | `-> 0%` |

### 6.5 `Ultimate Strike` Classification Table

*Strikes classified as **True Ultimate Strikes** receive **E4 (Shrine in Flame)**'s `+8%` multiplicative Domain damage, **Severance of Karma**'s full defense penetration (`35%` Vanilla / `40%` Calamity) and full Boss DR peel/bonus (`15%` Vanilla / `12%` Calamity), and trigger **Shadow Marionette Tethers**'s `0.50×` Ultimate damage-share scaling. **E6-Inherited Ultimate Strikes** receive full **E4 (`+8%`)** and **`50%` Severance of Karma Boss effectiveness** (`+7.5%` Vanilla / `6%` Calamity) without true-Ultimate defense penetration or Shadow Marionette share reduction.*

| Classification | Abilities Included |
| :--- | :--- |
| **True Ultimate Strikes** | 1. **Slashed Dream Cries in Red** (Rainblades 1–2 Preemptive & Main, Rainblade 3, Stygian Resurge)<br>2. **Thunder Core** (`9` sky barrage bolts)<br>3. **Bladesong of the Endless Rain** (`Blood Blossom Needles`)<br>4. **Rift of the Void Horizon** (`Void Fissure` portal hits)<br>5. **Synchronized Execution** (`Mirror Rainblades` & `Sheathing Cross-Slash`) |
| **E6-Inherited Ultimate Strikes** *(With Apocalypse, the Emancipator)* | 1. **4-Beat Flowing Odachi Combo** (blade sweeps & crescent arcs)<br>2. **Shadow Clone Cycle Slashes** (basekit, E6, & Echo of the Nameless Decoy cuts)<br>3. **EX Spatial Waves** (Horizon's Severance projected waves) |
| **Never Ultimate Strikes** | **Mirage Weft** cutting threads, **Blood Lotus Transmission** shockwaves, **Shadow Crucible** cycle-expiry detonations, and **Shadow Marionette Tethers** echoed damage shares *(echoed damage also suppresses all hit modifiers to prevent double-scaling)*. |

### 6.6 Mutual Exclusions, Overrides & Anti-Double-Hit Rules

- **Combo Anti-Double-Hit**: Each beat's blade sweep, trailing crescent arc, and **EX** spatial wave share a single per-beat hit list—an enemy can be hit **at most once per beat** across all three hitboxes.
- **Movement Speed Override**: **Rainleaf Phasing** (`+30%` Vanilla / `+35%` Calamity speed) overrides **Siphon of the Departed** (`+15%` speed) while phasing is active; they do not stack.
- **Crit Stacking**: **Still Water Basin** (`+15%`), **Emancipator's Severed Will** (`+15%` Vanilla / `+20%` Calamity), **E1** (`+6%` Vanilla / `+8%` Calamity vs. debuffed targets), and boss-kill scaling (`0.3%` Vanilla / `0.2%` Calamity × bosses; `0.4%` default when manually overridden) stack additively on Naught's crit chance. **Still Water Basin**'s crit damage (`+15%` Vanilla / `+18%` Calamity) applies to all Naught Melee/True Melee attacks except **Shadow Marionette Tethers** echoed shares (which cannot crit).
- **Reach Stacking**: **E3** (`+15%`) and **EX** (`+15%`) stack additively (`+30%` total) on the 4-beat combo's blade reach and crescent scale; neither scales **EX** spatial wave size/damage or Ultimate slash scale (locked at `1.00×`).
- **Crowd-Control Exclusions**: **Shadow Marionette Tethers** snare/leash/micro-yank and **Rift of the Void Horizon** vortex pull/`35%` slow exclude bosses, boss-flagged enemies, Target Dummies, town NPCs, immortal targets, `<= 5` max HP critters, and knockback-immune enemies (Void Fissures also exclude non-head worm segments and all boss-worm segments).
- **Shadow Marionette Debuff Exclusions**: **Grip of the Abyss**, **Withered River**, **Shadow Crucible**, and **Rainleaf Cooldown** never propagate via debuff sharing; shared debuffs are strictly 1-hop and grant `0` Slashed Dream.
- **Cycle Payout Suppression**: Clones consumed by **Synchronized Execution**'s Sheathing Cross-Slash or silently evicted by **Shadow Marionette Tethers**'s `4`-clone cap or basekit's `6`-clone cap do **not** trigger cycle completion payouts (`+1` Dream, `+1/+2` Knots, Cycle Slashes, or **Shadow Crucible** detonation).
