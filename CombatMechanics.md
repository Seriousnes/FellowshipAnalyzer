# Fellowship Combat Mechanics for Log Analysis (Ratings, Haste, and Diminishing Returns)

> **Season scope.** Every stat formula in the body of this document describes **Season 3, "Rise of the Heskyr"**, live since 2026-06-22. [^22]
> Season 3 shipped a full progression reset and a stat squish, and it replaced the Season 2 diminishing-returns curve, so Season 2 rating values and Season 3 rating values are not interchangeable. [^22]
> `StatTracker.RatingToPercentage` is the analyzer's implementation of the Season 3 curve and is the authority for what this codebase actually computes. [^23]
> The superseded Season 2 model is preserved under [Season 2 (historical)](#season-2-historical) for anyone reading a Season 2 log, since the repo still ships `s2` data.

## Executive Overview

Fellowship uses a World of Warcraft–style stat system built around ratings that convert into percentages with tiered diminishing returns and a unified "Haste" stat that speeds up most time-based mechanics (GCD, casts, ticks, and many cooldowns). [^1][^2] These mechanics can be modeled deterministically for a combat-log analysis app by combining published rating→percentage formulas with empirically verified haste scaling on cooldowns and global cooldown. [^2][^3][^4]

A robust analyzer needs to: (1) convert rating to post‑DR secondary percentages, (2) derive effective haste (including buffs) for each time segment of the dungeon, and (3) translate that haste into adjusted GCDs, casts, ticks, and cooldown timers while also applying ability‑specific cooldown modifiers. [^2][^5][^6]

## Secondary Stats and Rating System

Fellowship has four main secondary stats on gear: Critical Strike, Haste, Expertise, and Spirit, all of which are stored as rating values that are converted to percentages. [^2][^1][^7]
These secondary stats modify core combat behavior: Critical Strike increases crit chance (with a special base 5%), Haste speeds up most time-based effects, Expertise increases damage/healing/shields, and Spirit primarily affects ultimate resource generation and hero‑specific proc effects. [^2][^1][^7]

Secondary stats are always sourced as ratings from gear, gems, and some effects, while some talents, set bonuses, and gem effects add flat percentages that stack after the rating-to-percentage and diminishing-returns steps. [^2][^1]

## Season 3 Stat Squish and Base Scaling

Season 3 squished the absolute magnitude of every stat. The `CT_CharacterBaseAttributes` Default row moved base main stat from 1700 to 120 and base health from 28560 to 2856, roughly a 14x squish on main stat and a 10x squish on health. [^22]

Health and damage scaling share the same shape, with those base values substituted in: [^22]

- Health: `ROUND(ROUND(base_health × BaseHealthMultiplier) × DifficultyScaleMultiplier)`
- Damage: `ROUND(ROUND(base_main_stat × Spell_CoEfficient) × DifficultyScaleMultiplier)`

Season 3 uses `base_main_stat = 120`, `base_health = 2856`; Season 2 used `base_main_stat = 1700`, `base_health = 28560`. [^22]
Read those constants from the data file rather than hardcoding them: the current `s3/dungeon_data.json` reads `Default.BasicAttributeSet.Strength / Agility / Intellect: 120.0`, which agrees, but `Default.BasicAttributeSet.BaseHealth: 2999.0`, which is higher than the 2856 in the data dump README. [^22]

The practical consequence for a log analyzer is that **every absolute number changed scale between seasons**: ratings, health pools, and damage amounts from a Season 2 log are roughly an order of magnitude larger than their Season 3 equivalents, so any threshold, breakpoint, or sanity check expressed as an absolute value must be season-aware. [^22]

## Rating to Percentage Conversion

### Base conversion factor

All secondary stats share a single base conversion rate of 0.16 percentage points per 1 rating before diminishing returns. [^22][^23]
This means that 100 rating = 16%, 50 rating = 8%, and 200 rating = 32% as a raw percentage in the absence of any DR, and this base factor is the starting point for the DR band system described below. [^22][^23]

The Season 2 factor was 0.017 per rating, so Season 3 ratings are about 9.4x smaller for the same percentage. Gear item budgets were squished to match, and a real Season 3 secondary rating total is in the low hundreds where a Season 2 total was in the thousands. [^22]

### Critical Strike base value

Critical Strike has an additional 5% base crit chance that is exempt from diminishing returns and is added after DR on rating. [^22][^7]
The Season 3 data dump confirms this directly: each hero's base attribute block carries `CritChance: 0.05` alongside `CritMultiplier: 2.0`. [^22]
In practice, the computation for crit is "post-DR percentage from rating" plus 5% base plus any flat crit percentage bonuses from gems, talents, or set bonuses. [^22][^7]
`StatTracker.BaseCritChance` encodes the 5%, and `CritPercentage(rating, withBase: true)` performs the addition. [^23]

## Diminishing Returns on Secondary Stats

### Conceptual behavior

Fellowship applies soft diminishing returns to all secondary stats: below 10% there is no DR, between 10% and 25% the efficiency of new rating steps down band by band, and beyond 25% efficiency stabilizes at a reduced level. [^22][^1][^7]
The intent is to make low stats feel strong for early characters and to discourage extreme stacking of a single secondary stat in favor of balanced distributions. [^8][^22]

Season 3 DR is mild compared with Season 2: the worst band still returns 92% of base value, where Season 2 fell to about 58%. [^22]

### Banded DR structure

**The DR bands are measured on the raw percentage, `raw = rating × 0.16`, not on the post-DR percentage.** [^23]
Each 5-point band of `raw` is multiplied by a flat per-band factor. The factors do **not** compound: band 3 uses 0.96, not 0.98 × 0.96. [^22][^23]

| Band of `raw` | Band multiplier | Effective % per rating in the band | Post-DR % accrued in the band |
| --- | --- | --- | --- |
| 0% to 10% | 1.00 | 0.16 | 10.0 |
| 10% to 15% | 0.98 | 0.1568 | 4.9 |
| 15% to 20% | 0.96 | 0.1536 | 4.8 |
| 20% to 25% | 0.94 | 0.1504 | 4.7 |
| above 25% | 0.92 | 0.1472 | unbounded |

Because the bands are cut on `raw`, the rating values at which they change are: [^23]

- `raw` = 10% at **62.5** rating
- `raw` = 15% at **93.75** rating
- `raw` = 20% at **125** rating
- `raw` = 25% at **156.25** rating

A consequence worth remembering: at those boundaries the stat **displays** 10%, 14.9%, 19.7%, and 24.4%, not 10/15/20/25%. Only the first boundary is the same number in both raw and post-DR terms. This is a structural change from Season 2, where each band contributed a clean 5 post-DR points and the boundaries were defined on the post-DR percentage.

### Explicit piecewise formula

For any rating R (Crit, Haste, Expertise, or Spirit), the post-DR percentage from rating P(R), excluding flat bonuses and the crit base 5%, is: [^23]

- If R ≤ 62.5:  
  P(R) = R × 0.16
- If 62.5 < R ≤ 93.75:  
  P(R) = 10 + (R − 62.5) × 0.1568
- If 93.75 < R ≤ 125:  
  P(R) = 14.9 + (R − 93.75) × 0.1536
- If 125 < R ≤ 156.25:  
  P(R) = 19.7 + (R − 125) × 0.1504
- If R > 156.25:  
  P(R) = 24.4 + (R − 156.25) × 0.1472

`StatTracker.RatingToPercentage` expresses the same function in clamp form over `raw = rating × 0.16`, which avoids the boundary table entirely and returns a decimal fraction (0.3084 for 30.84%): [^23]

```
pct = min(raw, 10)
    + clamp(raw − 10, 0, 5) × 0.98
    + clamp(raw − 15, 0, 5) × 0.96
    + clamp(raw − 20, 0, 5) × 0.94
    + max(raw − 25, 0)      × 0.92
```

The in-game client displays the result to 2 decimal places. The analyzer does not round: `RatingToPercentage` returns the full-precision fraction, so any rounding is a presentation concern for the UI layer. [^23]

### Worked example

Take **200 Haste rating**, a plausible haste-focused Season 3 total. For scale, a Season 2 log in this repo's test data shows a total Haste rating of 1304, which at the 9.4x rating-scale change maps to roughly 139 in Season 3 terms, so 200 represents a build that is deliberately stacking the stat.

1. Raw percentage: 200 × 0.16 = **32%**.
2. First 10 raw points, no DR: 10 × 1.00 = **10.00**
3. Raw 10 to 15: 5 × 0.98 = **4.90**
4. Raw 15 to 20: 5 × 0.96 = **4.80**
5. Raw 20 to 25: 5 × 0.94 = **4.70**
6. Raw above 25, which is 32 − 25 = 7 points: 7 × 0.92 = **6.44**
7. Sum: 10.00 + 4.90 + 4.80 + 4.70 + 6.44 = **30.84%** post-DR Haste.

`StatTracker.RatingToPercentage(200)` returns `0.3084`. [^23]
The loss relative to the undiminished 32% is 1.16 percentage points, about 3.6% of the raw value.

### Practical gearing implications

Because the worst band still returns 92% of base value, Season 3 DR is a gentle tax rather than a wall. Overall efficiency, meaning post-DR percentage divided by raw percentage, is 96.4% at 32% raw (200 rating), 95.5% at 40% raw (250 rating), and still 94.9% at 48% raw (300 rating). [^23]
Pushing a single secondary well past 25% is a real but small loss, so stat priority in Season 3 is driven mainly by which secondary the hero scales with rather than by DR avoidance. [^22]
Flat percentage bonuses from gems, sets, or talents bypass DR entirely, so they are particularly useful for pushing a priority stat past 25% without suffering any efficiency loss. [^22][^1]

## Haste Mechanics and Cooldown Scaling

### What Haste does

In Fellowship, Haste is described as decreasing cooldowns, increasing attack speed, casting speed, and the tick rate of damage-over-time and healing-over-time effects. [^1][^7]
Community discussion and class guides further emphasize that Haste reduces the global cooldown (GCD) and shortens cast times and certain ability cooldowns, much like in World of Warcraft. [^4][^9][^10]

### Haste percentage from rating

The Haste percentage used for combat calculations is obtained by applying the same rating to percentage with DR formula described above to Haste rating, then adding any flat Haste percentage bonuses (for example from gems, set bonuses, or temporary buffs such as "heroism"-style effects). [^22][^23][^4]
As with other secondaries, DR applies only to rating-based Haste; flat Haste percentage bonuses are added on top after DR and are not diminished. [^22][^1]

### Empirical cooldown scaling formula

Players on the Fellowship subreddit report that, for abilities that scale with Haste, cooldowns are shortened proportionally, with an effective formula equivalent to World of Warcraft or League of Legends–style "ability haste". [^3][^11]
One widely cited explanation is that if a player has H percent Haste, the cooldown is multiplied by 100 ÷ (100 + H), so 100% Haste halves the effective cooldown, and 50% Haste yields about a 33% reduction in cooldown length. [^3]

For example, with 50% Haste, a 30‑second base cooldown would be reduced to roughly 20 seconds, since 30 × 100 ÷ 150 ≈ 20. [^3]
The same player explanation notes that the proportion of time removed relative to the base is H ÷ (H + 100), which for H = 50 gives 50 ÷ 150 ≈ 0.33, i.e., a one‑third reduction. [^3]

### Global Cooldown (GCD)

Community testing indicates that Fellowship's base global cooldown behaves similarly to World of Warcraft, at roughly 1.5 seconds for most abilities, and that Haste reduces this GCD. [^4][^12]
Players report that with sufficiently high Haste (for example from a 30% Haste spirit ability), GCDs clearly become shorter, and 1.5‑second casts can drop below one second during strong Haste windows, confirming that GCD and cast-time spells are affected. [^4]

There is some conflicting discussion about whether certain heroes or situations effectively use a 1.0‑second baseline, but the most consistent reports use 1.5 seconds and show strong Haste scaling. [^13][^4]
For a log analyzer, the base GCD should be treated as a parameter (per hero / spec) with Haste scaling applied as for other cooldowns, and validated against empirical inter‑cast intervals in logs.

### Cast times and periodic ticks

Guides and player explanations state that Haste speeds up cast times and increases the tick rate of damage-over-time and heal-over-time effects, again similar to WoW. [^1][^10][^7]
In practice, the same Haste percentage used to reduce cooldowns is applied to reduce cast durations and tick intervals, such that cast_time_effective = base_cast_time × 100 ÷ (100 + H) and tick_interval_effective = base_tick_interval × 100 ÷ (100 + H) provide a good approximation. [^10][^3]

### Ability-specific cooldown modifiers

Some traits and abilities grant explicit "cooldown recovery" or cooldown-reduction effects, sometimes by a fixed amount (for example, "reduces the cooldown by 1.5 seconds on crit") or by a percentage (for example, a trait that reduces a weapon cooldown by about 30%). [^14][^9][^5]
There are also extreme effects such as channeling Chronoshift, which grants 800% increased cooldown recovery, implying an enormous effective speedup of cooldown timers during its duration. [^5]

Community and guide material suggest that such cooldown recovery modifiers stack multiplicatively with Haste, with Haste affecting the general time flow and recovery modifiers applying to specific abilities or sources, similar to the way ability haste and specific CDR effects interact in other games. [^11][^9]

For an analyzer, the general approach is to compute a generic Haste-adjusted cooldown and then apply ability-specific percentage or flat reductions according to the ability's tooltip logic.

## Implementation Formulas for a Log Analyzer

### Rating to percentage (post-DR)

For each secondary stat S with rating R, the analyzer should:

1. Compute the raw percentage: `raw = R × 0.16`. [^22][^23]
2. Partition `raw` into the bands 0-10, 10-15, 15-20, 20-25, and 25+. The bands are cut on the raw percentage, not on the post-DR percentage, and not on rating. [^23]
3. Multiply each band's portion of `raw` by its flat multiplier (1.00; 0.98; 0.96; 0.94; 0.92). The multipliers do not compound. [^22][^23]
4. Sum the band contributions to obtain P_R, the post-DR percentage from rating. [^23]
5. Add any flat percentage bonuses associated with S (gems, sets, talents), which are not subject to DR. [^22][^1]
6. If S is Critical Strike, add an extra 5% base crit. [^22][^7]

Steps 1 to 4 are exactly `StatTracker.RatingToPercentage`, which returns a decimal fraction and applies no rounding; step 5 is not modelled there and step 6 is `CritPercentage(rating, withBase: true)`. [^23]

### Haste-adjusted time scaling

Given a computed total Haste percentage H_total for a character at a given timestamp (including rating plus flat bonuses and temporary effects), time-based mechanics that scale with Haste can generally be modeled by multiplying their base duration by 100 ÷ (100 + H_total). [^3][^10]

For any base duration T_base (GCD, cast time, tick interval, or haste-scaling cooldown), the effective duration is:

- T_effective = T_base × 100 ÷ (100 + H_total)

This matches community examples: 100% Haste halves the effective cooldown (T_effective = T_base × 100 ÷ 200 = 0.5 T_base), while 50% Haste reduces it to two‑thirds (T_effective ≈ 0.667 T_base). [^3]

### Combining Haste with cooldown recovery modifiers

Some abilities have separate cooldown recovery multipliers or flat reductions, such as "30% reduced cooldown" traits or effects that increase cooldown recovery by 800%. [^9][^5]
A common modeling convention, consistent with other games' handling of CDR and ability haste, is to treat recovery multipliers as additional factors on the denominator.

If an ability has a base cooldown C_base, the character has total Haste H_total, and the ability has a cooldown recovery bonus R_ability (expressed as a percentage, e.g., 30%), then an effective model is:

- First, compute the generic Haste‑adjusted cooldown:  
  C_haste = C_base × 100 ÷ (100 + H_total)
- Then, apply ability‑specific recovery:  
  C_effective = C_haste × 100 ÷ (100 + R_ability)

For extreme effects like "800% increased cooldown recovery", the effective multiplier becomes 100 ÷ (100 + 800) = 100 ÷ 900 ≈ 0.111, so an already Haste‑reduced cooldown becomes about one‑ninth of its Haste‑only value during the effect. [^5]

Flat reductions like "reduce this cooldown by 1.5 seconds on crit" should be implemented as event-driven reductions on the *remaining* cooldown at the time the proc occurs, based on logs that record crits. [^9][^14]

### Global cooldown modeling

Given the lack of exact official documentation, the analyzer should treat the base global cooldown (GCD_base) as configurable (per hero / spec), defaulting to 1.5 seconds based on community consensus and similarity to WoW. [^4][^12]
At any time t, the effective GCD can then be computed using the same Haste formula:

- GCD_effective(t) = GCD_base × 100 ÷ (100 + H_total(t))

Logs can be used to validate and refine this model by measuring minimum spacing between instant-cast ability usages under various Haste states and seeing whether the observed minimum approach matches this prediction. [^4][^13]

### Tick timing for periodic effects

Damage-over-time (DoT) and heal-over-time (HoT) abilities should tick at an interval equal to their base tick interval multiplied by 100 ÷ (100 + H_total), as Haste increases tick frequency. [^1][^10]
The total effect duration may remain fixed in time (more ticks per duration) or effectively shorten (same number of ticks in less time), depending on the specific ability implementation; logs and tooltips can be used to classify each ability. [^14][^15]

For a log analyzer, a practical approach is to treat each periodic effect as a schedule of tick events determined by the first application timestamp and Haste snapshot (or dynamic updates if Haste changes mid‑duration), then align actual tick events in the log against this schedule to detect downtime and clipping.

## Combat Log Implications and State Tracking

### Log contents and third-party tools

Official and community tools confirm that Fellowship can write a combat log file that records detailed events: damage, healing, buffs, debuffs, casts, and movements. [^6]
Archon’s "Fellowship Logs" site parses these logs to display damage breakdowns, buff uptime, death reasons, and more, indicating that the log contains enough structured data for fine-grained analysis. [^6]

This means a custom analyzer can rely on the log as the ground truth for event sequencing while using the above formulas to reconstruct idealized cooldown and resource timelines for comparison.

### Core state to track per player

For accurate cooldown and combat-state reconstruction, the analyzer should maintain, at minimum, the following per-player state over time:

- Current secondary stats in rating (from gear snapshot at pull start or from a separate export).
- Derived post‑DR percentages for Crit, Haste, Expertise, and Spirit.
- Active buffs and debuffs affecting Haste and cooldown recovery (including spirit ultimates, heroism-style effects, and traits that add cooldown recovery).
- Ability-specific cooldown state: remaining cooldowns for all tracked abilities, updated based on casts, procs, and resets.
- Global cooldown state (when the player is GCD‑locked versus free to act).
- Resource state (mana, hero-specific resources, Spirit/ultimate) for advanced analysis of overcapping and drift. [^1][^7][^6]

By updating this state at each log event, the analyzer can maintain a coherent simulation of what the player could have done versus what they actually did.

### Reconstructing ability cooldowns

For each ability, the analyzer should have metadata including base cooldown, whether it scales with Haste, any ability-specific cooldown-reduction interactions, and whether it is on the global cooldown. [^14][^15]
When a cast event is observed in the log, the analyzer can:

1. Compute H_total(t) at the cast timestamp from Haste rating, flat bonuses, and active buffs.
2. Compute C_effective using the formulas above (Haste plus any ability-specific recovery modifiers currently active).
3. Set the ability’s next-available time to t + C_effective.
4. Apply ongoing reductions from procs (e.g., on-crit reductions) as those events appear in the log, shortening the remaining cooldown. [^9][^14]

Comparing these predicted "ready" timestamps with actual subsequent casts provides direct metrics for wasted cooldown time, drift relative to optimal usage, and alignment with encounter mechanics.

### Modeling global cooldown and action windows

To analyze rotational tightness and APM, the analyzer should track GCD windows by:

- At each cast, computing the current GCD_effective(t) and setting a GCD lock until t + GCD_effective.
- Treating any cast that starts within the lock window as delayed relative to the earliest possible input.
- Measuring the gaps between the end of one GCD and the start of the next action to quantify downtime not explained by mechanics.

This model should be refined with empirical data (e.g., network latency, animation lock) by examining high-level logs from top players and aligning predicted earliest-cast times with observed timings. [^4][^6]

### Haste snapshots vs dynamic updates

Some games snapshot Haste at cast start for determining cooldown and tick timing, while others update dynamically. Fellowship community discussions indicate that effects like spirit ultimates and Haste buffs clearly change GCD and cast speed mid-pull, but publicly available documentation does not fully specify snapshot rules. [^4][^3]

A practical approach is to:

- Assume cooldowns and GCD are determined at cast start using the current H_total.
- For periodic effects, treat tick intervals as using the Haste present at application time (snapshot) unless logs show clear evidence of mid-duration changes.
- Provide configuration flags for "snapshot" versus "dynamic" behaviors to allow tuning as more data becomes available.

## Design Suggestions for a Fellowship Log Analysis App

### Data model and configuration

The analyzer should separate static configuration (per-patch definitions of abilities, base cooldowns, and which stats they scale with) from combat-log parsing and simulation logic. [^14][^5]
Static configuration can include per-hero recommended stat caps and breakpoints, leveraging optimization guides and BIS calculators, allowing the analyzer to contextualize a player’s stat choices as well as their rotational execution. [^2][^5][^16]

Supporting multiple patches or server rulesets can be handled by versioned configuration files, as rating conversion formulas and DR parameters may change over time, as seen in other MMOs. [^17][^18]

### Metrics enabled by these mechanics

With correct modeling of ratings, Haste, DR, and cooldown scaling, the app can compute:

- Cooldown alignment: how often major cooldowns could have been used versus when they were actually cast.
- Drift and desync: how far key cooldowns were delayed, especially around encounter mechanics.
- GCD utilization: percentage of time spent GCD-locked versus idle, excluding forced downtime.
- Stat utilization: how much value is obtained from the current secondary distribution, expressed as post-DR percentage against raw percentage so a player can see exactly how many points DR is costing them at their rating. [^22][^1][^19]
- Buff and debuff uptime: adjusted for Haste‑affected durations and tick schedules.

By surfacing these as per-pull and per-ability metrics, the analyzer can give actionable feedback that goes beyond raw DPS/HPS numbers.

### Learning from existing tools

Archon’s Fellowship Logs and Fellows.gg already operate as official or semi-official log and database tools for Fellowship, and their feature sets (damage breakdowns, buff uptime, leaderboards) are good references for baseline functionality and performance expectations. [^6][^5]
External guides from Icy Veins, Method, and other sites provide stat priority and rotation advice; integrating some of their logic (for example recommended Haste and Crit targets) into the analyzer enables role‑aware suggestions such as "you are pushing Haste past the efficient DR band for this hero." [^1][^20][^16]

## Absorb Shields

### Shield Damage Reduction

Several absorb effects, such as Solar Shield and Luminous Barrier, have a built-in 50% damage reduction effect. Damage absorbed by the shield is reduced by 50% before it is applied to the remaining absorbtion effect, effectively doubling the total absorb capacity. Not all absorbs behave this way, the spell definition will define a "Damage Reduction" multiplier on a case by case basis.

A "Critical" absorb behaves in a similar way, and is checked on a per-hit basis. A critical absorb is applied at half the regular rate in the same way the shield damage reduction works. 

If a critical absorb occurs on a shield with 50% DR, it's applied at 1/4th the regular rate to the absorbtion pool.

### Consequences for analyzer code

`AbsorbAnalyzer.AbsorbEfficiency` computes `used / (used + wasted)`, which mixes damage removed with shield strength left over. That is correct only where a hit costs the shield exactly what it absorbed, it does not consider damage reduction or crit absorbs. Read `Face`, `Consumed` and `Absorbed` off `AbsorbUse` instead.

The datamined build is queryable through the `fellowship-codex` MCP server (`find_entity`, `get_entity`, `list_types`) for gem traits, blessings, weapon and neck traits, effects and abilities with their real descriptions. Use it before guessing what an aura does.

## Limitations and Open Questions

Fellowship does not publish official developer documentation for the exact rating, DR, and cooldown formulas. The Season 3 stat numbers here come from the game-table data dump in `external/fs_tc_uploads`, which is the closest thing to a primary source; the surrounding narrative comes from community reverse-engineering and third-party guides. [^22][^1][^7]
The data dump's Season 3 DR note reads "the default mod starts at .16 but each step the new value gets reduced by the next tier", which could be read as compounding the per-band multipliers. The dump's own numeric list (0.98 / 0.96 / 0.94 / 0.92) is flat, and `StatTracker.RatingToPercentage` implements it flat. This document follows the flat reading. [^22][^23]
The dump also writes the bands as "From 10-15% you get 0.98 value" without saying whether that 10-15% is the raw percentage or the displayed post-DR percentage. The code cuts the bands on the raw percentage, and this document follows the code. [^22][^23]
The data dump README states the Season 3 base health as 2856 while the live `s3/dungeon_data.json` reads 2999, so the base-health constant should be read from the data file. [^22]
Some details remain uncertain, including exact snapshot rules for Haste on periodic effects, hero-specific base GCD values, and whether certain cooldown recovery effects stack additively or multiplicatively with Haste in edge cases. [^4][^5]

For a production-grade analyzer, these uncertainties should be handled via configuration, sanity-checked against logs from high-end players, and revisited as new theorycrafting emerges or patches change the underlying math. [^6][^21]

## Season 2 (historical)

This section records the Season 2 stat model. It applies only to logs recorded before Season 3 went live on 2026-06-22, and the analyzer does not implement it. It is kept because the repo still ships `s2` game data and Season 2 logs remain readable. [^22]

**Base values.** Base main stat was 1700 and base health 28560, feeding the same scaling shape used today: `ROUND(ROUND(28560 × BaseHealthMultiplier) × DifficultyScaleMultiplier)` for health and `ROUND(ROUND(1700 × Spell_CoEfficient) × DifficultyScaleMultiplier)` for damage. [^22]

**Base conversion factor.** 0.017 percentage points per 1 rating before DR, so 100 rating = 1.7% and 1000 rating = 17% raw. [^2]

**Compounding tiers.** Unlike Season 3, Season 2 compounded its penalties: the 95%, 90%, 85%, and 80% tier penalties were applied progressively to the running per-rating factor rather than each being a flat multiplier on a band. [^2]
The tiers were also cut on the **post-DR** percentage, so each tier contributed exactly 5 post-DR points and the boundaries landed at clean 10 / 15 / 20 / 25 displayed percentages: [^2]

- 0 ≤ R ≤ 589 (0 to 10%): full efficiency, 0.017% per rating.
- 589 < R ≤ 898 (10 to 15%): 95% efficiency, 0.01615% per rating for this segment.
- 898 < R ≤ 1242 (15 to 20%): 85.5% efficiency, 0.014535% per rating for this segment.
- 1242 < R ≤ 1647 (20 to 25%): 72.675% efficiency, 0.01235475% per rating for this segment.
- R > 1647 (25%+): 58.2% efficiency, 0.009901% per rating for all rating above 1647. [^2]

**Piecewise formula.** [^2]

- If R ≤ 589: P(R) = R × 0.017
- If 589 < R ≤ 898: P(R) = 10 + (R − 589) × 0.01615
- If 898 < R ≤ 1242: P(R) = 15 + (R − 898) × 0.014535
- If 1242 < R ≤ 1647: P(R) = 20 + (R − 1242) × 0.01235475
- If R > 1647: P(R) = 25 + (R − 1647) × 0.009901

**Worked example.** For 2200 Haste rating the raw percentage was 2200 × 0.017 = 37.4%. The first 589 rating gave 10%, each of the next three tiers about 5%, and the final 553 rating (2200 − 1647) about 5.48% at 58.2% efficiency, for roughly 30.48% post-DR Haste: a loss of about 6.9 percentage points. [^2]

**Why the numbers do not transfer.** Season 2 lost 6.9 points at 37.4% raw; Season 3 loses 1.16 points at 32% raw. Season 2 DR punished stacking hard enough that gearing advice was built around it, which is why older guides recommend holding secondaries inside the 10% to 25% band. That advice does not apply to Season 3. [^22]

---

## References

1. [Fellowship Beginner Guide – Everything You Need to Know - Icy Veins](https://www.icy-veins.com/fellowship/news/fellowship-beginner-guide-everything-you-need-to-know/) - This Fellowship Beginner Guide will introduce you to all the unique mechanics and core systems that ...

2. [Fellowship Stats Guide: How Stats Are Calculated - FellowBIS](https://fellowbis.com/stats-guide) - Learn how secondary stats work in Fellowship and how rating values convert to percentages with dimin... **Documents the Season 2 model.** Cited here only for the [Season 2 (historical)](#season-2-historical) section and for stat descriptions that did not change between seasons; see [^22] and [^23] for the live Season 3 numbers.

3. [How much does haste reduce the recharge time of abilities ... - Reddit](https://www.reddit.com/r/fellowshipgame/comments/1rlmuqj/how_much_does_haste_reduce_the_recharge_time_of/) - The formula is x / (x + 100). 50 haste will reduce cds by 50/150, thus 33 ... I don't think haste re...

4. [Melee players are you comfortable with GCD timer? : r/fellowshipgame](https://www.reddit.com/r/fellowshipgame/comments/1odud74/melee_players_are_you_comfortable_with_gcd_timer/) - After WoW, my GCD timer seems too long, and I often miss rotations because of it. Has anyone else en...

5. [Fellowship Heroes Best in Slot Gear Overview](https://fellowbis.com/heroes-bis-overview) - While channeling Chronoshift you gain 800% increased cooldown recovery and are protected by a tempor...

6. [Now Supporting Fellowship: Logging, Builds, and Database](https://www.archon.gg/wow/articles/news/now-supporting-fellowship-logging-builds-and-database) - Archon will be supporting Fellowship with sites for logs, databases, and builds. (WoW - Midnight)

7. [Fellowship Stats Explained: A Deep Dive into Primary, Secondary ...www.neonlightsmedia.com › blog › fellowship-stats-explained-guide](https://www.neonlightsmedia.com/blog/fellowship-stats-explained-guide) - Our deep dive into Fellowship's stat system. Learn about Primary stats, the complexities of diminish...

8. [Rating to percentage formula](https://lotro-wiki.com/wiki/Rating_to_percentage_formula) - This formula can be used to calculate the required rating for a given percentage. The resulting rati...

9. [Rank 1 Sylvie Explains Builds, Rotation & Tips | Fellowship ...](https://www.youtube.com/watch?v=FoU-Sm77sf8) - In this video we're going to go over gear what are the correct stats to go for what weapon to choose...

10. [Haste - Wowpedia - Your wiki guide to the World of Warcraftwowpedia.fandom.com › wiki › Haste](https://wowpedia.fandom.com/wiki/Haste) - Haste is a secondary attribute that increases attack speed, ranged attack speed and casting speed. I...

11. [Haste | League of Legends Wiki - Fandom](https://leagueoflegends.fandom.com/wiki/Haste) - Haste is a category of stats that encompasses ability haste, basic ability haste, ultimate haste, it...

12. [Revert Classes GCD from 1.5 back to 1 second - Blizzard Forums](https://us.forums.blizzard.com/en/wow/t/revert-classes-gcd-from-15-back-to-1-second/1546042) - I remember way back in I believe it was Legion when Hunters and DK's GCD was increased from 1 second...

13. [Please fix GCD feel for high latency :: Fellowship General Discussions](https://steamcommunity.com/app/2352620/discussions/0/624436764983032947/) - It may only be a fraction of a second, but when each GCD is 1 second to begin with, this really adds...

14. [Skills | Fellowship Wiki](https://fellowship.wiki.fextralife.com/Skills) - Measured Strike reduces the cooldown of your Shield Slam and Shield Throw abilities by 1.5 seconds. ...

15. [Fellowship Aeona Hero Guide - Icy Veins](https://www.icy-veins.com/fellowship/news/aeona-hero-healer-guide/) - This page contains a full guide overview of the healer character, Aeona, in Fellowship. This guide c...

16. [Top DPS Builds in Fellowship — Stats, Gear, and Rotation Tips](https://sportsrant.indiatimes.com/gaming/top-dps-builds-in-fellowship-stats-gear-and-rotation-tips-673715.html) - Learn how to maximize your DPS in Fellowship with a complete guide covering core mechanics, best sta...

17. [Combat rating system - Wowpedia - Fandom](https://wowpedia.fandom.com/wiki/Combat_rating_system) - Armor Penetration is now a stat rating. · Hit, Crit, and Haste ratings now affect spells, and there ...

18. [Combat Ratings and Stats in Mists of Pandaria](https://www.tentonhammer.com/guides/combat-ratings-and-stats-in-mists-of-pandaria) - A lot has changed in Mists of Pandaria, do you know what the new combat stats are and how much of th...

19. [Fellowship DPS Guide – Max Uptime, Clean Movement & Perfect Burst](https://boostroom.com/blog/fellowship-dps-guide-uptime-movement-burst-windows) - Master DPS in Fellowship: uptime system, movement tricks, burst alignment, add swaps, and cooldown d...

20. [Stats, Traits & Gems Ardeos Fellowship Hero Guide - Method](https://www.method.gg/fellowship/heroes/ardeos/stats-traits-and-gems) - Method's Fellowship Ardeos Hero Guide includes best Talents, BIS gear, BIS legendary, rotation, inte...

21. [Fellowship - News - Public Playtest Patch Notes - March 1 | eprison.de](https://www.eprison.de/spiele/fellowship/steam-news/1792751526079157/8138/89808.html)

22. [Ângry's Fellowship API-ish Dump - `external/fs_tc_uploads`](https://fs-theorycrafting.com) - Season-first dump of the game's own data tables, vendored into this repo as a submodule. Season 3 "Rise of the Heskyr" DR bands, the 0.16 base conversion factor, the stat squish (base main stat 1700 to 120, base health 28560 to 2856), and the health/damage scaling formulas come from `external/fs_tc_uploads/README.md`; base attribute values are read from `external/fs_tc_uploads/s3/dungeon_data.json` and per-hero base crit from `external/fs_tc_uploads/s3/hero_data.json`.

23. `src/FellowshipAnalyzer.Core/Analysis/StatTracker.cs` - `RatingToPercentage` is this codebase's implementation of the Season 3 rating to percentage conversion and is the authority for what the analyzer computes. `BaseCritChance` holds the 5% base crit and `CritPercentage(rating, withBase: true)` adds it.

