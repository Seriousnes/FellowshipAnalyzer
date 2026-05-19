# Elarion Analysis Modules — Implementation Plan

Based on analysis of [Ângry's S2 Elarion Guide](https://www.fellowsguide.com/guides/heroes/angrys-s2-elarion-guide-1774959696/) (Rotation + Advanced Topics sections) and the current state of the Elarion hero project.

## Current State

The Elarion hero has only three modules — `Abilities`, `ElarionAuras`, and `FocusTracker` — with a minimal guide showing only a Focus resource graph. No event-driven analyzers, statistics components, or normalizers exist yet.

---

## Proposed Modules

### 1. Lunarlight Mark Eruption Analyzer

**Why:** The guide's top AoE priority is erupting Lunarlight Marks with Heartseeker Barrage or Impending Heartseeker. A common mistake is casting Barrage/Impending when no marks are on targets, wasting the eruption mechanic entirely.

**What to track:**
- Heartseeker Barrage / Impending Heartseeker casts that erupted marks vs. those that didn't
- Wasted eruptions: marks consumed by non-Barrage abilities (e.g. crits from other abilities consuming marks prematurely — guide notes "higher crit can cause you to burn more marks with other abilities")
- Mark application → eruption time gap (marks that expired without being erupted)

**Module type:** Analyzer  
**Guide component:** Performance breakdown showing eruption efficiency percentage and a timeline of missed eruptions.

---

### 2. Empowered Multishot Waste Analyzer

**Why:** AoE rotation priority #5 is explicitly "don't waste empowered Multishot charges." Empowered Multishot should always be cast before using a regular Multishot. Using regular Multishot while empowered charges are available is a rotation mistake.

**What to track:**
- Normal Multishot casts while empowered Multishot was available (wasted empowerment)
- Total empowered Multishot casts vs. regular Multishot casts

**Module type:** Analyzer  
**Guide component:** List of wasted empowerment instances with timestamps.

---

### 3. Highwind Arrow Cap Analyzer

**Why:** AoE rotation priority #6 is "don't overcap Highwind Arrow charges." Highwind Arrow has 3 charges; sitting at max charges means wasted recharge time.

**What to track:**
- Time spent at max Highwind Arrow charges (3/3)
- Casts that happened while already at max charges (indicating the player let it cap before using)
- Charge-seconds wasted (time at cap × 1 charge wasting recharge)

**Module type:** Analyzer  
**Guide component:** Overcapped time percentage with suggestions.

---

### 4. Cooldown Efficiency Analyzer (Skystrider's Grace + Event Horizon)

**Why:** The guide says to use Skystrider's Grace and Event Horizon "on CD as long as you'll get the full durational value or close to it." Holding these unnecessarily is a DPS loss, but the guide also warns not to waste them on short windows.

**What to track:**
- Skystrider's Grace: total casts, time held past cooldown coming up, effective buff duration vs. maximum duration
- Event Horizon: total casts, time held past cooldown, effective buff duration vs. maximum duration
- Wasted cooldown time: how long each ability sat off-cooldown before being used

**Module type:** Analyzer  
**Guide component:** Per-cooldown efficiency breakdown with held-time and uptime stats.

---

### 5. Pre-Ultimate Checklist Analyzer

**Why:** The guide defines a specific pre-ult sequence (Skystrider's Supremacy → Voidbringer's Touch → Skystrider's Grace + Event Horizon) and conditions to check before ulting. Sending ultimate without proper setup is a major DPS loss.

**What to track:**
- For each Spirit of Heroism (ultimate) window:
  - Was Skystrider's Supremacy cast before entering ult?
  - Was Voidbringer's Touch cast before entering ult?
  - Was Skystrider's Grace active during the ult window?
  - Was Event Horizon active during the ult window?
  - Did the player have Heartseeker Barrage or Impending Heartseeker available?
- Score each ult window on how many pre-conditions were met

**Module type:** Analyzer  
**Guide component:** Per-ult-window breakdown table showing which pre-conditions were met/missed.

---

### 6. Starfall Volley Desync Analyzer

**Why:** AoE rotation priority #3 is to "desync Lunarlight Mark and Starfall Volley by 10-15s." If both are cast together, the player wastes the window where marks could be re-applied and erupted between Volley casts. Keeping them staggered maximizes mark eruptions per Volley.

**What to track:**
- Time gap between Lunarlight Mark cast and the next Starfall Volley cast
- Instances where Volley was cast within 5s of a Lunarlight Mark cast (too close together = bad desync)
- Average desync gap over the fight

**Module type:** Analyzer  
**Guide component:** Timeline visualization showing Mark and Volley cast alignment.

---

### 7. Impending Heartseeker Proc Tracking

**Why:** The guide emphasizes using Focused Shot to trigger Impending Heartseeker procs (bad luck protection / locking in charges). Procs stack twice, last 15s, and the second proc resets the first timer. Letting procs expire without use is waste.

**What to track:**
- Impending Heartseeker proc gains and consumptions
- Expired procs (proc gained but never consumed within 15s)
- Time between proc and consumption
- Procs that refreshed an existing stack (good — means both charges active)

**Module type:** Analyzer  
**Guide component:** Proc efficiency rate and timeline of gains/consumptions.

---

### 8. Voidbringer's Touch Usage Analyzer

**Why:** The guide has specific Voidbringer's Touch rules: "send 1st cast ASAP, then save so you can void → ults → void." Also warns "don't double mark the same target until the first mark is consumed." Misuse wastes spirit generation from Visions of Grandeur.

**What to track:**
- Voidbringer's Touch casts and their timing relative to ultimate windows
- Double-marking the same target before the first mark was consumed
- Spirit overcap situations where Voidbringer's Touch was held too long

**Module type:** Analyzer  
**Guide component:** Per-cast breakdown showing optimal vs. suboptimal usage.

---

## Implementation Order

Recommended priority based on impact and complexity:

| Priority | Module | Rationale |
|----------|--------|-----------|
| 1 | Empowered Multishot Waste | Simple to detect, clear-cut mistake, high impact |
| 2 | Highwind Arrow Cap | Simple charge tracking, well-defined bad behavior |
| 3 | Lunarlight Mark Eruption | Core mechanic of the S2 barrage rotation |
| 4 | Cooldown Efficiency (Grace/EH) | Reusable pattern, applies to multiple cooldowns |
| 5 | Pre-Ultimate Checklist | High value but needs correlating multiple buff/cast events |
| 6 | Impending Heartseeker Procs | Proc tracking requires buff event correlation |
| 7 | Starfall Volley Desync | More nuanced timing analysis |
| 8 | Voidbringer's Touch Usage | Requires target tracking + spirit resource correlation |

## Dependencies

- All modules need proper Elarion spell IDs already defined in `Spells.cs` (14 spells + 4 effects exist)
- Some modules (Lunarlight Mark Eruption, Voidbringer double-mark) may need an **event normalizer** to link mark application events to their targets
- The Pre-Ultimate Checklist module depends on identifying Spirit of Heroism windows, which requires buff tracking from `ElarionAuras`
- Empowered Multishot tracking may need a normalizer or buff event linking to distinguish empowered vs. regular casts
