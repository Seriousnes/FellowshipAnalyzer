# Fellowship Combat Mechanics for Log Analysis (Ratings, Haste, and Diminishing Returns)

> **Season scope.** Every stat formula in the body of this document describes **Season 3, "Rise of the Heskyr"**, live since 2026-06-22. [^22]
> Season 3 shipped a full progression reset and a stat squish, and it replaced the Season 2 diminishing-returns curve, so Season 2 rating values and Season 3 rating values are not interchangeable. [^22]
> `StatTracker.RatingToPercentage` is the analyzer's implementation of the Season 3 curve and is the authority for what this codebase actually computes. [^23]
> The superseded Season 2 model is preserved under [Season 2 (historical)](#season-2-historical) for anyone reading a Season 2 log.

## Executive Overview

Fellowship uses a World of Warcraft-style stat system built around ratings that convert into percentages with tiered diminishing returns and a unified "Haste" stat that speeds up most time-based mechanics (GCD, casts, ticks, and many cooldowns). [^1][^2] These mechanics can be modeled deterministically for a combat-log analysis app by combining published rating→percentage formulas with empirically verified haste scaling on cooldowns and global cooldown. [^2][^3][^4]

A robust analyzer needs to: (1) convert rating to post‑DR secondary percentages, (2) derive effective haste (including buffs) for each time segment of the dungeon, and (3) translate that haste into adjusted GCDs, casts, ticks, and cooldown timers while also applying ability‑specific cooldown modifiers. [^2][^5][^6]

## Secondary Stats and Rating System

Fellowship has four main secondary stats on gear: Critical Strike, Haste, Expertise, and Spirit, all of which are stored as rating values that are converted to percentages. [^2][^1][^7]
These secondary stats modify core combat behavior: Critical Strike increases crit chance (with a special base 5%), Haste speeds up most time-based effects, Expertise increases damage/healing/shields, and Spirit primarily affects ultimate resource generation and hero‑specific proc effects. [^2][^1][^7]

Secondary stats are always sourced as ratings from gear, gems, and some effects, while some talents, set bonuses, and gem effects add flat percentages that stack after the rating-to-percentage and diminishing-returns steps. [^2][^1]

## Season 3 Stat Squish and Base Scaling

Season 3 squished the absolute magnitude of every stat. Base main stat moved from 1700 to 120 and base health from 28560 to 2856, roughly a 14x squish on main stat and a 10x squish on health.

Health and damage scaling share the same shape, with those base values substituted in:

- Health: `ROUND(ROUND(base_health × BaseHealthMultiplier) × DifficultyScaleMultiplier)`
- Damage: `ROUND(ROUND(base_main_stat × Spell_CoEfficient) × DifficultyScaleMultiplier)`

Season 3 uses `base_main_stat = 120`, `base_health = 2856`; Season 2 used `base_main_stat = 1700`, `base_health = 28560`.

The two `DifficultyScaleMultiplier` series are not one series: `difficultyScaling.health` and `difficultyScaling.damage` are identical from difficulty 1 through 37 and diverge from 38 upward, where health reads 82.30 against damage 81.50, and the gap widens from there. [^22]
The export corroborates the 120 without naming it as a constant: 179 of the 213 creature records that carry an attribute block read `strength`/`agility`/`intellect` of 120, and the next largest group is 25 records at 100. Base health has no counterpart in the export at all, so the 2856 figure is inherited from the retired data dump and is unverified here.

The practical consequence for a log analyzer is that **every absolute number changed scale between seasons**: ratings, health pools, and damage amounts from a Season 2 log are roughly an order of magnitude larger than their Season 3 equivalents, so any threshold, breakpoint, or sanity check expressed as an absolute value must be season-aware. [^22]

## Rating to Percentage Conversion

### Base conversion factor

All secondary stats share a single base conversion rate of 0.16 percentage points per 1 rating before diminishing returns. [^22][^23]
This means that 100 rating = 16%, 50 rating = 8%, and 200 rating = 32% as a raw percentage in the absence of any DR, and this base factor is the starting point for the DR band system described below. [^22][^23]

The Season 2 factor was 0.017 per rating, so Season 3 ratings are about 9.4x smaller for the same percentage. Gear item budgets were squished to match, and a real Season 3 secondary rating total is in the low hundreds where a Season 2 total was in the thousands. [^22]

### Critical Strike base value

Critical Strike has an additional 5% base crit chance that is exempt from diminishing returns and is added after DR on rating. [^22][^7]
In practice, the computation for crit is "post-DR percentage from rating" plus 5% base plus any flat crit percentage bonuses from gems, talents, or set bonuses. [^22][^7]
`StatTracker.BaseCritChance` encodes the 5%, and `CritPercentage(rating, withBase: true)` performs the addition. [^23]

## Diminishing Returns on Secondary Stats

### Conceptual behavior

Fellowship applies soft diminishing returns to all secondary stats: below 10% there is no DR, between 10% and 25% the efficiency of new rating steps down band by band, and beyond 25% efficiency stabilizes at a reduced level. [^22][^1][^7]
The intent is to make low stats feel strong for early characters and to discourage extreme stacking of a single secondary stat in favor of balanced distributions. [^8][^22]

Season 3 DR is mild compared with Season 2: rating past 25% still converts at 81.4% of base value, where Season 2 fell to about 58%. [^22][^24]

### Bracketed DR structure

The bracket edges 10, 15, 20 and 25 are points of the **converted** percentage, and each bracket's penalty compounds on every penalty before it. [^22][^24]
Rating converts to a raw percentage, `raw = rating × 0.16`, and each slice of `raw` converts at the efficiency of the bracket the converted percentage is in at that point.

| Converted bracket | Bracket penalty | Efficiency (converted per raw point) | % per rating in the bracket | `raw` width of the bracket |
| --- | --- | --- | --- | --- |
| 0% to 10% | 1.00 | 1.00 | 0.16 | 10.0000 |
| 10% to 15% | 0.98 | 0.98 | 0.1568 | 5.1020 |
| 15% to 20% | 0.96 | 0.9408 | 0.150528 | 5.3146 |
| 20% to 25% | 0.94 | 0.884352 | 0.14149632 | 5.6539 |
| above 25% | 0.92 | 0.81360384 | 0.13017661 | unbounded |

Each bracket below 25% adds exactly 5 converted points (10 for the first). The rating values at the bracket edges are: [^23][^24]

- 10% at **62.5** rating
- 15% at **94.3878** rating
- 20% at **127.6042** rating
- 25% at **162.9408** rating

### Explicit piecewise formula

For any rating R (Crit, Haste, Expertise, or Spirit), the post-DR percentage from rating P(R), excluding flat bonuses and the crit base 5%, is: [^23][^24]

- If R ≤ 62.5:  
  P(R) = R × 0.16
- If 62.5 < R ≤ 94.3878:  
  P(R) = 10 + (R − 62.5) × 0.1568
- If 94.3878 < R ≤ 127.6042:  
  P(R) = 15 + (R − 94.3878) × 0.150528
- If 127.6042 < R ≤ 162.9408:  
  P(R) = 20 + (R − 127.6042) × 0.14149632
- If R > 162.9408:  
  P(R) = 25 + (R − 162.9408) × 0.13017661

`StatTracker.RatingToPercentage` walks the brackets over `raw = rating × 0.16`: each bracket multiplies the running efficiency by its penalty and consumes `(edge − converted) ÷ efficiency` raw points. It returns a decimal fraction (0.298242 for 29.8242%). [^23]

The in-game client displays the result to 2 decimal places. The analyzer does not round: `RatingToPercentage` returns the full-precision fraction, so any rounding is a presentation concern for the UI layer. [^23]

### Worked example

Take **200 Haste rating**, a plausible haste-focused Season 3 total. For scale, a Season 2 log in this repo's test data shows a total Haste rating of 1304, which at the 9.4x rating-scale change maps to roughly 139 in Season 3 terms, so 200 represents a build that is deliberately stacking the stat.

1. Raw percentage: 200 × 0.16 = **32%**.
2. First bracket: 10 raw points at efficiency 1.00 reach **10%**.
3. Second bracket: 5.1020 raw points at 0.98 reach **15%** (15.1020 raw consumed).
4. Third bracket: 5.3146 raw points at 0.9408 reach **20%** (20.4167 raw consumed).
5. Fourth bracket: 5.6539 raw points at 0.884352 reach **25%** (26.0705 raw consumed).
6. Above 25%: the 5.9295 raw points left convert at 0.81360384, adding **4.8242**.
7. Sum: 25 + 4.8242 = **29.8242%** post-DR Haste.

`StatTracker.RatingToPercentage(200)` returns `0.298242`. [^23]
The loss relative to the undiminished 32% is 2.18 percentage points, about 6.8% of the raw value.

### Practical gearing implications

Rating past 25% converts at 81.4% of base value. Overall efficiency, meaning post-DR percentage divided by raw percentage, is 93.2% at 32% raw (200 rating), 90.8% at 40% raw (250 rating), and 89.3% at 48% raw (300 rating). [^23]
Pushing a single secondary well past 25% is a real loss, so stat priority in Season 3 weighs which secondary the hero scales with against how deep into the brackets that secondary already is. [^22]
Flat percentage bonuses from gems, sets, or talents bypass DR entirely, so they are particularly useful for pushing a priority stat past 25% without suffering any efficiency loss. [^22][^1]

## Haste Mechanics and Cooldown Scaling

### What Haste does

In Fellowship, Haste is described as decreasing cooldowns, increasing attack speed, casting speed, and the tick rate of damage-over-time and healing-over-time effects. [^1][^7]
Community discussion and class guides further emphasize that Haste reduces the global cooldown (GCD) and shortens cast times and certain ability cooldowns, much like in World of Warcraft. [^4][^9][^10]

### Haste percentage from rating

The Haste percentage used for combat calculations is obtained by applying the same rating to percentage with DR formula described above to Haste rating, then adding any flat Haste percentage bonuses (for example from gems, set bonuses, or temporary buffs such as "heroism"-style effects). [^22][^23][^4]
As with other secondaries, DR applies only to rating-based Haste; flat Haste percentage bonuses are added on top after DR and are not diminished. [^22][^1]

### Haste time scaling

With total Haste `h` as a fraction (0.30 for 30%), a hasted duration is `T_base ÷ (1 + h)`. [^24]

- **Cast time**: `cast_base ÷ (1 + h)`, with `h` at the cast's start. `Haste.ScaleDuration` computes it.
- **Global cooldown**: `1.5 s ÷ (1 + h)`, with `h` at the cast's start. `GlobalCooldown` computes it from the ability's `GcdInfo.Base` (1500 ms by default), and a `GcdInfo.Static` GCD takes no Haste.
- **Channel ticks**: the tick period is `tick_base ÷ (1 + h)`, with `h` at the channel's start. The channel's duration is fixed and takes no Haste.
- **Hasted periodic effects**: each tick comes after `tick_base` of hasted progress, where progress runs at `1 + h` per second. A Haste change re-times the tick in progress from that moment; the effect's duration is fixed. [^24]

For example, 50% Haste takes a 2 s cast to 1.333 s and the 1.5 s global cooldown to 1 s.

### Cooldown model

A cooldown holds an amount of **cooldown progress** that drains at a **recovery rate**. [^24]

**Duration at the start.** When a charge starts recharging, its duration in cooldown progress is

- `D = C_base × (1 − ACR)`, then divided by `1 + h` when the export lists Haste in `cooldownDurationScalesOff` (`CooldownHasteScaling.Duration`).

Ability Cooldown Reduction (`CooldownPool.AbilityCooldownReduction`, capped at 1) and Duration-scaled Haste are read once, at the charge's start. Every later charge of a multi-charge ability takes `D` from the values current when that charge starts.

**Recovery rate.** The cooldown drains at

- `rate = 1 + h_rate + Σ acceleration`

where `h_rate` is the current Haste when the export lists Haste in `cooldownTimeRateScalesOff` (`CooldownHasteScaling.TimeRate`), and 0 otherwise. Every Cooldown Acceleration source is one additive term in the same pool:

| Source | Export modifier | Term |
| --- | --- | --- |
| Strand of Eternity (equipped legendary) | `CooldownRecovery` × 1.1 | +0.10 |
| Chronoshift channel (Asha's Arcane Embrace) | `CooldownRecovery` × 8 | +7.0 |
| Glorious Purpose (Fated Strike) | `CooldownRecovery` × 3 | +2.0 |
| The Monarch (MAJOR abilities) | 3% / 5% / 8% / 12% by tier | +tier + min(0.1 × h, 0.05) |

A `CooldownRecovery` multiplier of `m` joins the pool as `m − 1`. Haste on a time-rate cooldown joins the same sum, so Chronoshift takes a cooldown with 20% time-rate Haste to `1 + 0.2 + 7 = 8.2×`.

The real time left on a charge is `progress_left ÷ rate`. A change to Haste or to any acceleration term changes the rate from that moment: the progress already made is kept, and the time left becomes `time_left × rate_old ÷ rate_new`. `SpellUsable.EffectiveRate` computes the rate.

**Weapon abilities.** A Weapon ability's cooldown takes no Cooldown Acceleration from Strand of Eternity, The Monarch or another ability's effect. It takes only the terms its own effects grant (`CooldownModifier.GrantedBy`): Chronoshift recovers at 8× while Chronoshift is channelled, and Fated Strike takes Glorious Purpose's +2.0. Ability Cooldown Reduction and the export's own Haste scaling apply to a Weapon ability.

**Flat reductions.** A flat reduction ("reduces the cooldown by 1.5 seconds") removes that much cooldown progress. Ability Cooldown Reduction does not scale it. While the cooldown recovers at `rate`, a reduction of `X` shortens the time left by `X ÷ rate`: a 1 s reduction is worth 0.5 s at 2×. A reduction larger than the progress left completes that charge and carries the remainder to the next one; a reduction past the last charge is wasted. `SpellUsable.ReduceCooldown` reports the requested and the applied progress.

**Charges.** Charges recharge one at a time from the first spend, unless the ability declares `IndependentCharges`. Each charge's recharge starts from the state current at its start.

**Worked example.** A 30 s, single-charge, Duration-scaled cooldown at 20% Haste with 10% ACR and Strand of Eternity: `D = 30 × 0.9 ÷ 1.2 = 22.5 s` of progress, recovering at 1.1×, so the cooldown is ready after 20.45 s. A Chronoshift channel 10 s in, with 11.5 s of progress left, takes the rate to 8.1× for 3 s, which drains 24.3 s of progress and finishes the cooldown 1.42 s into the channel.

### Legendary items

A player wears one legendary item at a time, whatever its slot. The export marks no legendary unique-equipped, and each hero has a legendary for the chest, head and legs slots, but only one is equipped. The equipped legendary grants Strand of Eternity's +10% Cooldown Acceleration.

## Implementation Formulas for a Log Analyzer

### Rating to percentage (post-DR)

For each secondary stat S with rating R, the analyzer should:

1. Compute the raw percentage: `raw = R × 0.16`. [^22][^23]
2. Walk the converted brackets 0-10, 10-15, 15-20 and 20-25, multiplying the running efficiency by each bracket's penalty (1.00; 0.98; 0.96; 0.94) and spending `(edge − converted) ÷ efficiency` raw points on each. [^22][^24]
3. Convert the raw points left past 25% at the running efficiency × 0.92. [^22][^24]
4. Sum the bracket contributions to obtain P_R, the post-DR percentage from rating. [^23]
5. Add any flat percentage bonuses associated with S (gems, sets, talents), which are not subject to DR. [^22][^1]
6. If S is Critical Strike, add an extra 5% base crit. [^22][^7]

Steps 1 to 4 are exactly `StatTracker.RatingToPercentage`, which returns a decimal fraction and applies no rounding; step 5 is not modelled there and step 6 is `CritPercentage(rating, withBase: true)`. [^23]

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

1. Compute the total Haste `h` at the cast timestamp from Haste rating, flat bonuses, and active buffs.
2. Compute the charge's duration `D` and the recovery rate as the cooldown model above states.
3. Set the ability’s next-available time to t + D ÷ rate, and move it whenever the rate changes.
4. Apply flat reductions from procs (e.g., on-crit reductions) as those events appear in the log, taking that much cooldown progress off the charge recharging. [^9][^14][^24]

Comparing these predicted "ready" timestamps with actual subsequent casts provides direct metrics for wasted cooldown time, drift relative to optimal usage, and alignment with encounter mechanics.

### Modeling global cooldown and action windows

To analyze rotational tightness and APM, the analyzer should track GCD windows by:

- At each cast, computing the hasted global cooldown `1.5 s ÷ (1 + h)` and setting a GCD lock until it ends.
- Treating any cast that starts within the lock window as delayed relative to the earliest possible input.
- Measuring the gaps between the end of one GCD and the start of the next action to quantify downtime not explained by mechanics.

This model should be refined with empirical data (e.g., network latency, animation lock) by examining high-level logs from top players and aligning predicted earliest-cast times with observed timings. [^4][^6]

### Haste snapshots and dynamic updates

Haste is read at a fixed moment for these values: [^24]

- Cast time, global cooldown and channel tick period: at the cast's or channel's start.
- Duration-scaled cooldown and Ability Cooldown Reduction: at each charge's recharge start.

Haste is read continuously for these values, and a change takes effect from that moment: [^24]

- A time-rate cooldown's recovery rate and every Cooldown Acceleration term.
- A hasted periodic effect's progress towards its next tick.

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

Several absorb effects, such as Solar Shield and Luminous Barrier, have a built-in 50% damage reduction effect. Damage absorbed by the shield is reduced by 50% before it is applied to the remaining absorption effect, effectively doubling the total absorb capacity. Not all absorbs behave this way, the spell definition will define a "Damage Reduction" multiplier on a case by case basis.

A "Critical" absorb behaves in a similar way, and is checked on a per-hit basis. A critical absorb is applied at half the regular rate in the same way the shield damage reduction works. 

If a critical absorb occurs on a shield with 50% DR, it's applied at 1/4th the regular rate to the absorption pool.

### Consequences for analyzer code

`AbsorbAnalyzer.AbsorbEfficiency` computes `used / (used + wasted)`, which mixes damage removed with shield strength left over. That is correct only where a hit costs the shield exactly what it absorbed, it does not consider damage reduction or crit absorbs. Read `Total`, `Effective` and `Absorbed` off `AbsorbUse` instead.

The datamined build is queryable through the `fellowship-codex` MCP server (`find_entity`, `get_entity`, `list_types`) for gem traits, blessings, weapon and neck traits, effects and abilities with their real descriptions. Use it before guessing what an aura does.

## Limitations and Open Questions

Fellowship does not publish official developer documentation for the exact rating, DR, and cooldown formulas. The Season 3 stat numbers here come from the offline game-data export vendored at `data/v24736527`, which is the closest thing to a primary source; the surrounding narrative comes from community reverse-engineering and third-party guides. [^22][^1][^7]
The export writes the bracket edges as `statCalculation.attributes.<Stat>.brackets[].maxBracketValue` and the penalties as `penaltyPercentage` and `finalPenaltyPercentage`. The edges are points of the converted percentage and the penalties compound, as SimFell's engine applies them. Combat logs record ratings and no percentages, so they do not decide between bracket readings. [^22][^24]
The base-health constant has no primary source in the vendored export: it carries no named base-health value, and creature `health` spans 149.95 to 10000000. The 2856 and 2999 figures are inherited from the retired data dump and should be treated as unverified.
Hero-specific base GCD values remain uncertain. [^4][^5]

For a production-grade analyzer, these uncertainties should be handled via configuration, sanity-checked against logs from high-end players, and revisited as new theorycrafting emerges or patches change the underlying math. [^6][^21]

## Season 2 (historical)

This section records the Season 2 stat model. It applies only to logs recorded before Season 3 went live on 2026-06-22 [^22], and the analyzer does not implement it. It is kept because Season 2 logs remain readable.

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

22. Offline game-data export, vendored at `data/v24736527` (`entities.jsonl`, `settings.json`). The 0.16 base conversion factor and the diminishing-returns bands come from `settings.json` `statCalculation.attributes`, where CritChance, Haste, Expertise and Spirit each carry `baseStatMultiplier: 0.16`, brackets at 10 / 15 / 20 / 25 with penalties 1 / 0.98 / 0.96 / 0.94, and `finalPenaltyPercentage: 0.92`. The per-difficulty health and damage multipliers come from `difficultyScaling`, keyed 1 to 151.

23. `src/FellowshipAnalyzer.Core/Analysis/StatTracker.cs` - `RatingToPercentage` is this codebase's implementation of the Season 3 rating to percentage conversion and is the authority for what the analyzer computes. `BaseCritChance` holds the 5% base crit and `CritPercentage(rating, withBase: true)` adds it.

24. SimFell, the Fellowship combat simulator replayed against FellowshipLogs combat logs (`G:/source/SimFell`). `Engine/StatBlock.cs` `StatDiminishingReturns` holds the rating conversion; `Domain/Ability.cs` holds cast time, the cooldown duration at the start and the channel tick period; `Domain/Hero.cs` `CooldownRate` and `CooldownState` hold the recovery rate, weapon abilities, flat reductions and charges; `Engine/HastedTimer.cs` holds hasted periodic progress; `Engine/SimulationContext.cs` holds the global cooldown.

