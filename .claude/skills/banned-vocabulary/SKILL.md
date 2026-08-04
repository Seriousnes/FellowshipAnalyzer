---
name: banned-vocabulary
description: >
  FellowshipAnalyzer's banned terminology list and the replacement vocabulary. Use when: writing or
  editing guide prose, statistics descriptions, stat labels, tooltips, changelog entries, XML doc
  comments, or public identifiers; naming an analyzer, module, guide, or property; reviewing a diff
  that touches any user-facing or documented text. Every rule here is owner-stated and carries a grep
  pattern so the check is assertable.
---

# Banned vocabulary

Rules are owner-stated corrections. Each one names the banned form, the replacement, and a grep
pattern to assert it. Scope is **everything** unless a rule narrows it: rendered prose, stat labels,
tooltip strings, changelog text, XML doc comments, and public identifiers alike.

Run every grep below after any prose or naming change. Exclude `bin/` and `obj/`; the compiled XML
doc files mirror every doc comment and will double every hit.

## The governing rules

**1. The reader already knows the mechanics.** Assume full knowledge, in every instance, without
exception: what an ability does, what it procs, what it consumes, how its charges behave, what a
legendary item grants, what a talent changes. A sentence describing any of that tells the reader
nothing they did not bring with them. It is not a lead-in, it is not context, and it does not earn its
place by being short.

**What the reader may not know is how best to use it** - when to press it, what to spend it on, what
to have ready first. That is the one thing prose is for. `FocusedWrathGuide` is the shape: it never
says what Focused Wrath does, it says which spender to put the charges into and how target count
decides that.

**2. State what is measured.** A guide, a stat description and a doc comment say what the number is
and what was counted. They do not explain how the resource is displayed, what the stat labels below
are called, or how logging works. Methodology framing survives only where it changes how a number
should be read ("weighted by time", "read against your own build's ceiling").

**3. Delete before rewording.** When a banned phrase turns up, the first question is whether the
sentence should exist at all - not which words replace it. Strip the banned phrase and ask what is
left: if the remainder only restates the ability, delete the whole sentence or paragraph. Reach for
replacement wording only once the sentence has been shown to carry something from the log.

Owner-decided outcomes on the first five sites reviewed: two were deleted outright, one became a
directive, one was reworded, and one kept its structure with a single verb changed. Deletion is the
common case, not the fallback.

**4. Use the game's verb, not an imported metaphor.** Fellowship's own tooltip strings in
`external/fs_tc_uploads/s3/*.json` are the vocabulary source of truth. They say an ability **strikes**,
**deals**, **applies**, and that you **gain** something. They never say an effect *pays*, and never
use *land* as a verb - the only two matches in s3 are the noun ("the land it stands upon"). A word the
game does not use is a metaphor someone imported, and it is a ban candidate on sight.

Check before defending a word: ripgrep skips the submodule via its `.gitignore` and will silently
answer from dead s2 data, so use `grep -r` against `external/fs_tc_uploads/s3/` directly.

**5. The replacement is usually already named in the code you are editing.** Before inventing wording,
read the type, the member and the event you are documenting. The domain term is nearly always sitting
there:

- `OverhealAnalyzer`'s "Healing lost to full health bars" -> **"Healing lost to overheal"**. The word
  was in the type name.
- "Damage that actually landed on the player" -> **Taken**, the parameter's own name.
- "Healing that landed" -> **Effective**, which is what the sibling field is called.
- "landed on a proc already held" -> **reapplied**, from `ExecutionersGrinTracker.Reapplications`.
- "when the buff landed" -> **applied**, from `ApplyBuffEvent`.

A replacement drawn from the model keeps prose, labels and identifiers saying the same word. A
replacement invented in the prose layer starts a second vocabulary for the same quantity.

Most bans below are a special case of rules 1, 2 and 4, so the escape from a banned phrase is almost
never a synonym. Delete the sentence, or name the quantity - rule 5 is how you find it.

---

## 1. The valuation frame: "is worth"

**Banned.** Do not write that an ability, cast, proc, window, charge, buff or item **is worth** some
quantity, and do not write the same claim with a substituted verb.

| Banned | Why |
|---|---|
| "A proc is worth exactly one spender cast" | asserts a value, does not name a measurement |
| "the window is worth exactly the number of those two you fit inside it" | same |
| "the discount is worth twice as much on Hammer Storm" | same |
| "a window is worth whatever you banked for it" | same |
| "what it is worth is decided before you press it" | same |
| "each worth double" | same |
| "worth half the flutterfly bonus" | same |
| "the charge bought nothing" / "the Fury bought nothing" | same frame, different verb |

Substitutes that keep the valuation frame are **equally banned**: `amounts to`, `is equal to`,
`is equivalent to`, `buys you`, `bought nothing`, `counts for`, `pays for itself`, `is only as good as`
applied to an ability rather than to a model.

**Resolving a site, in order of preference.**

1. **Delete it.** The valuation usually sits in a sentence that exists only to describe the ability,
   and rule 1 removes both together. Owner-decided: Frostweaver's Wrath ("a proc is worth exactly one
   spender cast, so ...") and Sword and Board ("a refund advances the charge count without restarting
   the recharge, so a free cast is worth a whole Shield Slam") were both cut in full. Neither was
   replaced with anything, because neither told the reader anything the reader did not know.
2. **Turn it into a directive.** Where the valuation was smuggling in advice, say the advice plainly
   and let the table carry the measurement. Owner-decided on Matriarch Macabre: "the window is worth
   exactly the number of those two you fit inside it. Nothing else is copied." became "Attempt to cast
   as many finishers as possible during the window."
3. **Name the measured quantity.** Only when the sentence genuinely reports a number.
   - "what the item is worth over an encounter" -> "what the item **contributed** over an encounter".
     Owner-approved wording.
   - "the discount is worth twice as much on Hammer Storm as on Skull Crusher" -> "Hammer Storm
     returns twice the Fury Skull Crusher does."
   - "how much of the pull each was worth" -> "the uptime of each".
   - "each worth double" -> "counted twice", or name the multiplier.

**`bought` splits on its object.** `bought nothing` and `buys you` are the banned valuation frame.
`bought` with a countable, log-observable object is naming a measurement and is fine: "the casts it
bought above the threshold", "the free Shield Slams the proc bought".

**Ordinary-English "worth" is a different word and stays.** `worth doing`, `worth showing`,
`worth reporting`, `worth firing`, `worth shading`, `worth publishing`, `worth looking at`,
`worth saving it for` express merit or warrant, not equivalence. Distinguish by test: if the sentence
answers **"how much?"** it is banned; if it answers **"should I?"** it is fine.

```bash
rg -inE "\b(is|are|was|were|be|been)\s+(only\s+|exactly\s+|just\s+|precisely\s+)?worth\b" src/
rg -inE "\bwhat (it|the \w+) is worth\b|\bbought nothing\b|\bbuys you\b|\bamounts to\b" src/
```

## 2. The "pay" family

**Banned outright:** `pay`, `pays`, `paying`, `paid`, `payout`, `payouts`, `paid off`, `cash out`.

**Replacements:** released / returned / restored / spent / costs / draws on / turns / produced /
incurs. (`worth` was previously listed here and is now banned by rule 1; `covered` is banned for the
uptime sense by rule 3.)

There is **no exemption for internal or technical prose.** "deserialize against the derived type
rather than paying for a base-class allocation" is a violation in an internal API doc comment exactly
as it would be in a guide. Say **rather than incurring**, or name the cost.

Precedent rename: `TotalPaidOut` -> `TotalReleased`, `TotalPayouts` -> `TotalReleases`,
`PaidOutInFull` -> `ReleasedInFull`, `PayoutInstants` -> `ReleaseInstants`.

```bash
rg -inE "\bpay(s|ing|out|outs)?\b|\bpaid\b|\bcash(ed)? out\b" src/ tests/
```

## 2a. The "land" family

`land`, `lands`, `landed`, `landing` are banned on the same grounds as the "pay" family: an imported
metaphor for something the game already has a verb for. There is no single replacement - the right
word depends on which of five things the sentence is describing.

| Sense | Banned | Replacement |
|---|---|---|
| Magnitude after mitigation or overheal | "Damage that actually landed on the player", "Healing that landed", `"Landed"` as a stat label | **Taken** / **Effective** - both already domain terms in this codebase (`Taken`, `Effective`, `Overheal`) |
| Nothing got through | "damage that never landed", "share of RawIncoming that never landed" | **prevented**, **mitigated**, **absorbed** |
| A cast or hit inside a window | "casts that landed inside the window", "hits that landed inside a Shields Up window", "each pair landed together" | **cast**, **pressed**, **taken**, **spent** - "casts made inside the window", "hits taken inside the window", "each pair was cast together" |
| An aura arriving | "when the buff landed", "from the buff landing to its removal", "the debuff never landed", "landed on a proc already held" | **applied** - matches `ApplyBuffEvent` and the `Reapplications` member already in `ExecutionersGrinTracker` |
| The state at the moment of a press | "Nettlebolts that landed at full charges", "Culling Strikes that landed above the threshold", "this Detonate landed on nothing" | **cast** / **pressed** - "Nettlebolts cast at full charges", "this Detonate was cast into nothing" |
| A resource gain arriving | "before the next gain lands" | **arrives** - the sibling sentence in `WinterOrbGuide` already says "the gains that arrived on a full pool" |

Identifiers move too: `LandedResets` -> `RecoveredResets`, `var landed` -> `var taken`.

Ordinary English with no ability in it survives ("the casts and the healing fall in separate rows"),
but prefer a rewrite over defending the word.

```bash
rg -inE "\bland(s|ed|ing)?\b" src/
```

## 3. Aura presence: "stands"

A buff, debuff or DoT never **stands**, **was standing** or **stood**.

| Quantity | Term |
|---|---|
| Point-in-time presence | **active** ("the Rend active on the target") |
| Past duration of presence | **was active** |
| An aura window | **open** ("while the window was open") |
| Share of a pull | **uptime** - not "covered", not "were active for" |
| A spendable charge or stack | **available** - a charge is a resource, not a duration buff |
| A stacked-chart marker position | **stack count** - never "height" |
| Title-case stat label | Active first: "DoTs Standing" -> "Active DoTs" |

The game's own tooltip strings write "While <Name> is active, ..." and never use "buff"/"debuff" in
prose. Leave `standard`, `StandardGcd`, `netstandard` alone.

```bash
rg -inE "\bstand(s|ing)\b|\bstood\b" src/ | rg -v "standard"
rg -inE "\bstack height\b" src/
```

## 4. Display words for a resource: notch, bar

`notch` and `bar` are banned for Toughness, because both describe how the resource looks on screen.
Say **maximum Toughness**, **at maximum**, and label reduction steps by the reduction itself:
`At 40% Reduction`, `40% Reduction`, `No Reduction`.

**The discriminator is whether a resource is attached to the word.** `bar` bound to a quantity names
the on-screen widget and is banned; `bar` standing alone is the ordinary standard-to-clear metaphor
and is correct.

- Banned: "the combo-point bar", "the AoE bar", "full health bars", "Toughness bar". Say
  **threshold** for a level a cast has to reach, and for health say "landed on a target already at
  full health".
- Correct, keep as written: `QualitativePerformance.Ok` - "Meets the minimum acceptable bar but falls
  short of a good result." No resource, no widget.

A UI element genuinely named a bar (`StackedBar`, `CastBar`, `AuraBar`, `accent-bar`) keeps the word.

```bash
rg -inE "\bnotch(es)?\b" src/
rg -inE "\b(combo[- ]point|health|toughness|energy|fury|focus|mana|orb|resource|AoE|pull's)\s+bars?\b" src/
rg -inE "\b(a|the|full|their|its) bars?\b" src/ | rg -viE "StackedBar|CastBar|AuraBar|PassFailBar|GradiatedPerformanceBar|accent-bar|cast-efficiency-bar|cooldown-lane-bar|tab bar|GCD bar|recharge bar|aura bar"
```

The second pattern catches the detached form, where the resource is elsewhere in the sentence:
"landed on a bar that was already full", "what was lost to full bars". It needs an eyeball pass
because the UI-element exclusions are not exhaustive.

## 4a. Placement: assigned, not banked

A pink Flutterfly is **assigned** to an ally or **unassigned**. It is not banked, it does not sit in
a bank, and there is no bank to count it in. Reserve `bank` / `banked` for something that genuinely
accumulates: Heart Bloom's stored healing, Focus and Fury pools, spell charges.

```bash
rg -inE "\bbank(s|ed|ing)?\b" src/Heroes/FellowshipAnalyzer.Heroes.Sylvie
```

## 5. Verdict nouns: Findings, Reports, ScoreCards

Those three concepts do not exist in this codebase. Razor reads analyzer instances directly; typed
data lives in the analyzer and prose plus tiers live only in the Razor component. Do not introduce a
`Finding`, a `Report` (in the verdict sense), or a `ScoreCard` type, property or heading.

```bash
rg -nE "\b(Finding|ScoreCard|Scorecard)s?\b" src/
```

## 6. Naming: no qualifier the domain does not need

An analyzer or guide name states what it **actually assesses**. The **Window** and **Assignment**
qualifier families were removed across every hero (`SupremacyWindowsGuide` -> `SupremacyGuide`,
`PinkFlutterflyAssignmentAnalyzer` -> `PinkFlutterflyAnalyzer`). A wrong name manufactures a false
argument for splitting an analyzer in two, which collides with the one-analyzer-per-ability rule.

```bash
rg -l "(Window|Assignment)(Analyzer|Guide)\b" src/
```

## 7. Overconfidence words

Never label an inferred consequence **"unambiguous"**, **"clearly implied"** or **"the only possible
reading"**, in prose or in a message to the owner. State the narrowest reading that explains a
correction, say which follow-on edits it forces, and mark the rest as inferences awaiting
confirmation. Batch open questions into one round.

## 8. Relative-to-best scoring language

Never describe a cast or window as a share of the pull's best one ("of your best window", "compared to
your biggest bank"). Tier on gear-independent setup conditions, on gear-independent units such as a
stack count, or against a fixed game cap. Damage figures carry no `QualitativePerformance` and say so:
"this is context, not a verdict."

## 9. Static rotation prose

FSA is a log-analysis tool, not a guide site. No "How to Play", Overview, Core Mechanic,
openers-and-priorities, Cooldowns, or "Talents and Builds" prose, and no "adapted from method.gg"
source link. method.gg is research input for building analyzers, never UI content.

## 10. Dashes

No em dashes or en dashes anywhere: code, prose, commit messages, UI text. Use hyphens, commas, or
restructure the sentence.

```bash
rg -n "[—–]" src/ tests/
```

---

## When a reword is not obvious

Apply the confident cases. For any site where the correct domain term for the quantity is genuinely
uncertain - typically an adjacent quantity such as a chart axis, a geometric position, a resource or a
rating rather than the main pattern - **collect it and ask in one batch** with the alternatives spelled
out. A word that reads fine in isolation can still be wrong domain vocabulary, and a wrong term
propagates silently through prose, labels and identifiers.

Verify any delegated reword with `git diff --numstat` (insertions must equal deletions for a pure
reword), `git diff --check`, and a byte check of the file end. Subagents have reported leaving lines
untouched that they edited, and reported rewording clauses they had deleted.
