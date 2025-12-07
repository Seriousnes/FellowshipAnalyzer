# Fellowship Combat Mechanics for Log Analysis (Ratings, Haste, and Diminishing Returns)

## Executive Overview

Fellowship uses a World of Warcraft–style stat system built around ratings that convert into percentages with tiered diminishing returns and a unified "Haste" stat that speeds up most time-based mechanics (GCD, casts, ticks, and many cooldowns). [^1][^2] These mechanics can be modeled deterministically for a combat-log analysis app by combining published rating→percentage formulas with empirically verified haste scaling on cooldowns and global cooldown. [^2][^3][^4]

A robust analyzer needs to: (1) convert rating to post‑DR secondary percentages, (2) derive effective haste (including buffs) for each time segment of the fight, and (3) translate that haste into adjusted GCDs, casts, ticks, and cooldown timers while also applying ability‑specific cooldown modifiers. [^2][^5][^6]

## Secondary Stats and Rating System

Fellowship has four main secondary stats on gear: Critical Strike, Haste, Expertise, and Spirit, all of which are stored as rating values that are converted to percentages. [^2][^1][^7]
These secondary stats modify core combat behavior: Critical Strike increases crit chance (with a special base 5%), Haste speeds up most time-based effects, Expertise increases damage/healing/shields, and Spirit primarily affects ultimate resource generation and hero‑specific proc effects. [^2][^1][^7]

Secondary stats are always sourced as ratings from gear, gems, and some effects, while some talents, set bonuses, and gem effects add flat percentages that stack after the rating-to-percentage and diminishing-returns steps. [^2][^1]

## Rating to Percentage Conversion

### Base conversion factor

All secondary stats share a single base conversion rate of 0.017 percentage points per 1 rating before diminishing returns. [^2][^1]
This means that 100 rating = 1.7%, 500 rating = 8.5%, and 1000 rating = 17% in the absence of any DR, and this base factor is the starting point for the DR tier system described below. [^2]

### Critical Strike base value

Critical Strike has an additional 5% base crit chance that is explicitly exempt from diminishing returns and is added after DR on rating. [^2][^7]
In practice, the computation for crit is "post‑DR percentage from rating" plus 5% base plus any flat crit percentage bonuses from gems, talents, or set bonuses. [^2][^7]

## Diminishing Returns on Secondary Stats

### Conceptual behavior

Fellowship applies soft diminishing returns to all secondary stats: below 10% there is no DR, between 10–25% the efficiency of new rating gradually falls, and beyond 25% efficiency stabilizes at a reduced level. [^2][^1][^7]
The intent is to make low stats feel strong for early characters and to discourage extreme stacking of a single secondary stat in favor of balanced distributions. [^8][^2]

### Tiered DR structure

The best-documented formula from community reverse‑engineering splits rating into tiers with different effective percentages per rating, derived from multiplicative penalties applied to the base 0.017% per rating. [^2]
For any given rating value R (for Crit, Haste, Expertise, or Spirit), the post‑DR percentage from rating (excluding flat bonuses and crit base 5%) can be computed piecewise as follows: [^2]

- 0 ≤ R ≤ 589 (0–10%): full efficiency, 0.017% per rating.
- 589 < R ≤ 898 (≈10–15%): 95% efficiency, 0.01615% per rating for this segment.
- 898 < R ≤ 1242 (≈15–20%): 85.5% efficiency, 0.014535% per rating for this segment.
- 1242 < R ≤ 1647 (≈20–25%): 72.675% efficiency, 0.01235475% per rating for this segment.
- R > 1647 (25%+): 58.2% efficiency, 0.009901% per rating for all rating above 1647. [^2]

These efficiency factors correspond to the multiplicative penalties of 95%, 90%, 85%, and 80% applied progressively to the base 0.017% per rating, with compounding up to 25% and then a flat efficiency afterwards. [^2]

### Explicit piecewise formula

The FellowBIS stats guide gives a concrete closed-form piecewise function for the post‑DR percentage from rating, P(R), excluding flat bonuses and crit base 5%: [^2]

- If R ≤ 589:  
  P(R) = R × 0.017
- If 589 < R ≤ 898:  
  P(R) = 10 + (R − 589) × 0.01615
- If 898 < R ≤ 1242:  
  P(R) = 15 + (R − 898) × 0.014535
- If 1242 < R ≤ 1647:  
  P(R) = 20 + (R − 1242) × 0.01235475
- If R > 1647:  
  P(R) = 25 + (R − 1647) × 0.009901

The final percentage is then rounded up to 2 decimal places to match the in‑game display. [^2]

### Worked example

For 2200 Haste rating, the theoretical base percentage without DR would be 2200 × 0.017 = 37.4%. [^2]
Applying the tiered DR, the guide shows the first 589 rating giving 10%, the next tiers each contributing about 5%, and the final 553 rating (2200 − 1647) yielding about 5.48% at the reduced efficiency, for a final post‑DR Haste of roughly 30.48%. [^2]

This example shows a loss of about 6.9 percentage points relative to the non‑DR 37.4%, highlighting how punishing stacking beyond 25% can be. [^2]

### Practical gearing implications

Because efficiency falls from 100% at 0–10% to about 58% at and beyond 25%, most optimization guides recommend keeping individual secondary stats around 15–25% and then investing rating into other secondaries rather than pushing well past 25%. [^2][^1][^7]
Flat percentage bonuses from gems, sets, or talents bypass DR, so they are particularly powerful for pushing a priority stat above 25% without suffering further efficiency loss. [^2][^1]

## Haste Mechanics and Cooldown Scaling

### What Haste does

In Fellowship, Haste is described as decreasing cooldowns, increasing attack speed, casting speed, and the tick rate of damage-over-time and healing-over-time effects. [^1][^7]
Community discussion and class guides further emphasize that Haste reduces the global cooldown (GCD) and shortens cast times and certain ability cooldowns, much like in World of Warcraft. [^4][^9][^10]

### Haste percentage from rating

The Haste percentage used for combat calculations is obtained by applying the same rating→percentage with DR formula described above to Haste rating, then adding any flat Haste percentage bonuses (for example from gems, set bonuses, or temporary buffs such as "heroism"-style effects). [^2][^1][^4]
As with other secondaries, DR applies only to rating-based Haste; flat Haste percentage bonuses are added on top after DR and are not diminished. [^2][^1]

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

1. Partition R into the DR tiers of 0–589, 589–898, 898–1242, 1242–1647, and 1647+. [^2]
2. For each tier segment, multiply the tier's rating portion by the tier's effective percent-per-rating factor (0.017; 0.01615; 0.014535; 0.01235475; 0.009901). [^2]
3. Sum the tier contributions to obtain P_R, the post-DR percentage from rating. [^2]
4. Add any flat percentage bonuses associated with S (gems, sets, talents), which are not subject to DR. [^2][^1]
5. If S is Critical Strike, add an extra 5% base crit. [^2][^7]
6. Round to 2 decimal places to approximate the client display. [^2]

These steps replicate the FellowBIS formula and the behavior described in multiple community resources. [^2][^1][^7]

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

Comparing these predicted "ready" timestamps with actual subsequent casts provides direct metrics for wasted cooldown time, drift relative to optimal usage, and alignment with fight mechanics.

### Modeling global cooldown and action windows

To analyze rotational tightness and APM, the analyzer should track GCD windows by:

- At each cast, computing the current GCD_effective(t) and setting a GCD lock until t + GCD_effective.
- Treating any cast that starts within the lock window as delayed relative to the earliest possible input.
- Measuring the gaps between the end of one GCD and the start of the next action to quantify downtime not explained by mechanics.

This model should be refined with empirical data (e.g., network latency, animation lock) by examining high-level logs from top players and aligning predicted earliest-cast times with observed timings. [^4][^6]

### Haste snapshots vs dynamic updates

Some games snapshot Haste at cast start for determining cooldown and tick timing, while others update dynamically. Fellowship community discussions indicate that effects like spirit ultimates and Haste buffs clearly change GCD and cast speed mid-fight, but publicly available documentation does not fully specify snapshot rules. [^4][^3]

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
- Stat utilization: how much value is obtained from current secondary distribution relative to recommended bands (e.g., staying inside 10–25% before DR becomes severe). [^2][^1][^19]
- Buff and debuff uptime: adjusted for Haste‑affected durations and tick schedules.

By surfacing these as per-pull and per-ability metrics, the analyzer can give actionable feedback that goes beyond raw DPS/HPS numbers.

### Learning from existing tools

Archon’s Fellowship Logs and Fellows.gg already operate as official or semi-official log and database tools for Fellowship, and their feature sets (damage breakdowns, buff uptime, leaderboards) are good references for baseline functionality and performance expectations. [^6][^5]
External guides from Icy Veins, Method, and other sites provide stat priority and rotation advice; integrating some of their logic (for example recommended Haste and Crit targets) into the analyzer enables role‑aware suggestions such as "you are pushing Haste past the efficient DR band for this hero." [^1][^20][^16]

## Limitations and Open Questions

Fellowship does not publish official developer documentation for the exact rating, DR, and cooldown formulas; current understanding relies on community reverse-engineering and tools like FellowBIS and third-party guides. [^2][^1][^7]
Some details remain uncertain, including exact snapshot rules for Haste on periodic effects, hero‑specific base GCD values, and whether certain cooldown recovery effects stack additively or multiplicatively with Haste in edge cases. [^4][^5]

For a production-grade analyzer, these uncertainties should be handled via configuration, sanity-checked against logs from high-end players, and revisited as new theorycrafting emerges or patches change the underlying math. [^6][^21]

---

## References

1. [Fellowship Beginner Guide – Everything You Need to Know - Icy Veins](https://www.icy-veins.com/fellowship/news/fellowship-beginner-guide-everything-you-need-to-know/) - This Fellowship Beginner Guide will introduce you to all the unique mechanics and core systems that ...

2. [Fellowship Stats Guide: How Stats Are Calculated - FellowBIS](https://fellowbis.com/stats-guide) - Learn how secondary stats work in Fellowship and how rating values convert to percentages with dimin...

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

