---
name: banned-vocabulary
description: >
  FellowshipAnalyzer's writing rules and settled vocabulary. Use when: writing or editing guide prose,
  statistics descriptions, stat labels, tooltips, changelog entries, XML doc comments, or public
  identifiers; naming an analyzer, module, guide, or property; reviewing a diff that touches any
  user-facing or documented text. Rule 1 governs above all others: write for a reader who already
  knows the mechanics. Every rule is judged by reading a surface in full, never by matching a pattern.
---

# Writing rules

These are owner-stated corrections. They apply to everything: rendered prose, stat labels, tooltip
strings, changelog text, XML doc comments, and public identifiers alike.

## How this check is run

**Judging is reading.** Every rule here turns on what a clause is doing in context, which a pattern
cannot see. Searching has one job: listing the surfaces to read.

1. **Locate.** Use the surface table below to list every file that renders text. This is the only
   place a pattern belongs, and it matches component names, never words.
2. **Read each file end to end.** Not the matching line, not the surrounding ten. The judgement needs
   the stat's label, the value beside it, the sibling stats in the same card, and the prose in the
   panel above, because those are what decide whether a clause is telling the reader anything.
3. **Judge every clause, rule 1 first.** Ask whether the clause should exist before asking which words
   it should use. Most sites resolve there, and a clause deleted under rule 1 never reaches the
   vocabulary question.

**A clean pattern run is evidence of nothing.** The most common breach in this codebase survives every
word list in this file. `DetonateEfficiencyGuide.razor:159` read "Apocalyptic Surge stacks lost to
expiry instead of a free Detonate." A vocabulary pass rewrote it to "stacks that expired without
making a Detonate free" and left the breach in place, because the breach was never the word. It was
the clause explaining what Apocalyptic Surge does, which the reader already knew. The correct text is
**"Apocalyptic Surge stacks that expired unused."** Nine lines above, the same file already said
"expire unused".

### The surfaces

Every one of these renders text a reader sees, or documents code another author will copy. Locate them
by component name, then read the whole file.

| Locator | What to read in it |
|---|---|
| `<GuideSection` | `Title`, and everything inside it |
| `<LeftPanel`, `<RightPanel` | all prose, every `<p>` |
| `<SubSection`, `<Section` | `Title`, and the body |
| `<HelperText`, `<Explanation` | the whole body |
| `<TipBox`, `<PerformanceTipBox` | the body, including `Variant="Info"` callouts |
| `<CastOverview`, `<CastDetail`, `<CastSummary` | `Title` and `Description` |
| `OverviewStat(`, `PerCastStat(` | `Label` and `Tooltip`, and the `Value` where it is a word |
| `StackedBarSegment(`, `CheckItem`, `DotChecklist` | label and tooltip |
| `<StatRow`, `<StatCard` | body text, and `Info` / `<InfoTooltip>` |
| `<PullBanner`, `<WipGuide`, `<FilterBadge` | any literal text |
| `///` | every XML doc comment, on every member |
| identifiers | type, member, local and test method names |

A hero guide lives in `src/Heroes/*/Guides/*.razor`. A module that renders a statistics card is a Razor
component too, `src/Heroes/*/Modules/*.razor`, and it carries the same prose obligations. Core UI under
`src/FellowshipAnalyzer.Core/UI/` renders inside every hero's guide, so a label there has the widest
reach in the repository.

---

## Rule 1. Write for a reader who already knows the mechanics

Assume full knowledge, in every instance, without exception: what an ability does, what it procs, what
it consumes, how its charges behave, what a legendary item grants, what a talent changes. A sentence
describing any of that tells the reader nothing they did not bring with them. Being short does not earn
it a place.

**Write what the reader cannot know**: how best to use the ability, when to press it, what to spend it
on where there is a choice, what to have ready first. That is what prose is for. `FocusedWrathGuide` is
the shape. It never says what Focused Wrath does; it says which spender to put the charges into and how
target count decides that.

This rule outranks every other rule here. Apply it first, to every clause. Where it and a vocabulary
rule disagree, rule 1 wins, and the usual result is that the clause goes rather than changes.

### Judge per clause

A breach does not need its own sentence. It survives as a subordinate clause inside a sentence that is
otherwise reporting a measurement correctly, which is why rewording the sentence leaves it in place.
Two tests, asked of every clause you write or touch.

**The tooltip test.** Could this clause have been written by someone who had read the ability's tooltip
but never opened a log? If yes, it is mechanics. Delete the clause, not the word, and not necessarily
the whole sentence.

**The counting test.** Remove the clause. Does the number change? A clause that selects the same set of
events either way is describing the game, not the measurement, however much it reads like a definition.

The counting test is what catches a qualifier disguised as precision.
`ResurgentWindsTracker.razor` counted "procs spent on an instant Highwind Arrow" and "procs that
expired before an instant Highwind Arrow was cast". A Resurgent Winds proc has exactly one sink, so
every spent proc was spent on a Highwind Arrow and every expired proc was not. Naming the sink counted
nothing. The owner's text is **"procs spent"** and **"procs expired unused"**, with tooltips reading
"{ProcsConsumed} of {ProcsGained} procs spent." and "Procs that expired unused."

Name the object only where it narrows the count. "Fury spent on Hammer Storm" is a real measurement,
because Fury has other spenders. "Procs spent on Highwind Arrow" is not, because there is nothing else
to spend them on.

**Correcting a detail inside a mechanic clause is the signal to delete the clause.** The pass that
produced the wrong text above changed "a free Highwind Arrow" to "an instant Highwind Arrow", refining
the accuracy of a clause that should not have survived. Effort spent getting a mechanic exactly right
is effort that should have gone into deciding whether to state it at all.

### Four shapes that survive editing

In each of these the mechanic is fused to whatever else is being fixed, so the fix lands on the wrong
words.

| Shape | What the wrong fix looks like | The right fix |
|---|---|---|
| The banned word sits inside the mechanic clause, so fixing the word keeps the clause | "where the debuff **is worth** its full 15%" -> "where the debuff **applies** its full 15% reduction" | Delete the clause: "Share of pull time the primary target was at the five-stack cap." |
| The clause names the resource's only sink, so deleting it looks like leaving the stat undefined | "procs spent **on an instant Highwind Arrow**", "stacks lost to expiry **instead of a free Detonate**" | State the event alone: "procs spent", "stacks that expired unused" |
| The clause states the guaranteed consequence of the event being counted | "Hammer Storms cast inside a Focused Wrath window, **so they were empowered**" | Stop at the condition: "Hammer Storms cast inside a Focused Wrath window." |
| The surviving sentence reads unfinished, so the mechanic returns as its justification | "Reported as context rather than scored, **because Blood Arc grants more often than Heart Splitter and Grim Carve come back**" | Stop at the measurement: "Reported as context rather than scored, because it cannot reach 100%." Methodology needs no mechanical justification; the reader supplies it. |

### What a surviving clause is

After the deletions, every clause left standing is one of three things.

- **What was observed** - the quantity, and what was counted.
- **How to read it** - only where it changes the reading: weighted by time, unscored, measured against
  your own build's ceiling.
- **A directive** - when to press it, what to spend it on, what to have ready first.

Anything else is mechanics. A tooltip is not exempt because it "explains the number": a stat
description states what was counted, and the mechanism connecting that count to the game is the
reader's knowledge. Where deleting the mechanic clause leaves a tooltip that only restates its label,
the short tooltip is correct as it stands.

**A stat label is judged differently, because it is not a clause.** Two or three title-case words
cannot be observed-versus-directive. Ask instead whether the label names the quantity the member
computes, and whether it would still be true if the ability changed tomorrow. "Toughness Held" encodes
the mechanic that Iron Wall stops Toughness dropping; the member is what the label should say. A label
that states a mechanic is the same breach in fewer words, and it is worse, because every tooltip
beneath it inherits the framing.

**Worked examples here quote files as they stood when the rule was written.** Read the live file before
assuming an example still matches it, and never treat a file named here as pre-cleared.

## Rule 2. State what was measured

A guide, a stat description and a doc comment say what the number is and what was counted. Leave out
how the resource is displayed, what the stat labels below are called, and how logging works.
Methodology framing survives only where it changes how a number should be read.

## Rule 3. Delete first, reword second

When a clause is in question, decide whether it should exist before choosing words for it. Strip the
questionable phrase and read what is left: where the remainder only restates the ability, delete the
whole clause, sentence or paragraph. Reach for replacement wording once the clause has been shown to
say something drawn from the log.

Owner-decided outcomes on the first five sites reviewed: two were deleted outright, one became a
directive, one was reworded, and one kept its structure with a single verb changed. Deletion is the
common case, not the fallback.

Deleting a whole tooltip is rarely available, because a stat needs a description. Delete the clause
instead. That is the same rule at a smaller scope, and it is the move all four shapes above call for.

## Rule 4. Use the game's verb

Fellowship's own tooltip strings in `external/fs_tc_uploads/s3/*.json` are the vocabulary source of
truth. They say an ability **strikes**, **deals**, **applies**, and that you **gain** something. Use
those. A word the game does not use for an event the game does name is a metaphor someone imported, and
it is a ban candidate on sight. Write no metaphors in prose.

Check before defending a word: ripgrep skips the submodule via its `.gitignore` and will silently
answer from dead s2 data, so use `grep -r` against `external/fs_tc_uploads/s3/` directly.

**Absence from s3 convicts a word only where the game has a word for the same event.** The game writes
ability tooltips; this codebase writes analysis prose, so plenty of legitimate analysis words appear in
s3 zero times. "unused" is one of them, and it is the blessed replacement in the worked example above.
"fell off" is convicted not by its own zero but by the 22 hits for *expire* against it, describing the
same event. No competing game word, no conviction on this rule; judge it under rules 1 and 5 instead.

## Rule 5. Take the replacement from the code you are editing

Read the type, the member and the event you are documenting before inventing wording. The domain term
is nearly always already there:

- `OverhealAnalyzer`'s "Healing lost to full health bars" -> **"Healing lost to overheal"**. The word
  was in the type name.
- "Damage that actually landed on the player" -> **Taken**, the parameter's own name.
- "Healing that landed" -> **Effective**, which is what the sibling field is called.
- "landed on a proc already held" -> **reapplied**, from `ExecutionersGrinTracker.Reapplications`.
- "when the buff landed" -> **applied**, from `ApplyBuffEvent`.

A replacement drawn from the model keeps prose, labels and identifiers saying the same word. A
replacement invented in the prose layer starts a second vocabulary for the same quantity.

**A donor has to pass rule 1 first.** Wording copied from a sibling stat or a neighbouring file imports
that site's breaches along with its vocabulary. `DefensivesGuide.razor:106` was reworded to match line
164 of the same file, and line 164 reads "the gap Iron Wall is meant to cover", which is itself a
mechanic explanation. Read the donor before borrowing from it.

The vocabulary below is a special case of rules 1, 2 and 4, so the escape from a settled word is almost
never a synonym. Delete the clause, or name the quantity.

---

# The settled vocabulary

These words are already ruled on. A word here is decided; a word absent from here is still subject to
rules 1 to 5, which is where most sites resolve.

## Value: name the measurement

Say what was counted and how many: **contributed**, **returns twice the Fury Skull Crusher does**,
**the uptime of each**, **counted twice**, or the multiplier by name.

Recognise the valuation frame by its verb. An ability, cast, proc, window, charge, buff or item **is
worth** some quantity, or the same claim carried by `amounts to`, `is equal to`, `is equivalent to`,
`buys you`, `bought nothing`, `counts for`, `pays for itself`, `is only as good as` applied to an
ability rather than to a model. It asserts a value where a measurement belongs.

Resolve a site in this order.

1. **Delete it.** The valuation usually sits in a clause that exists only to describe the ability, and
   rule 1 removes both together. Owner-decided: Frostweaver's Wrath ("a proc is worth exactly one
   spender cast, so ...") and Sword and Board ("a refund advances the charge count without restarting
   the recharge, so a free cast is worth a whole Shield Slam") were both cut in full.
2. **Turn it into a directive.** Where the valuation was carrying advice, say the advice plainly and
   let the table hold the measurement. Owner-decided on Matriarch Macabre: "the window is worth exactly
   the number of those two you fit inside it. Nothing else is copied." became "Attempt to cast as many
   finishers as possible during the window."
3. **Name the measured quantity.** Only where the sentence genuinely reports a number.
   - "what the item is worth over an encounter" -> "what the item **contributed** over an encounter".
   - "the discount is worth twice as much on Hammer Storm as on Skull Crusher" -> "Hammer Storm returns
     twice the Fury Skull Crusher does."
   - "how much of the pull each was worth" -> "the uptime of each".
   - "each worth double" -> "counted twice", or name the multiplier.

**`bought` splits on its object.** `bought nothing` and `buys you` are the valuation frame. `bought`
with a countable, log-observable object names a measurement and is fine: "the casts it bought above the
threshold", "the free Shield Slams the proc bought".

**Ordinary-English "worth" is a different word and stays.** `worth doing`, `worth showing`, `worth
reporting`, `worth firing`, `worth shading`, `worth publishing`, `worth looking at`, `worth saving it
for` express merit or warrant, not equivalence. Where the sentence answers **"how much?"** the ban
applies; where it answers **"should I?"** the word is fine.

## Cost and return

Say **released**, **returned**, **restored**, **spent**, **costs**, **draws on**, **turns**,
**produced**, **incurs**.

Recognise the imported frame by `pay`, `pays`, `paying`, `paid`, `payout`, `payouts`, `paid off`,
`cash out`.

This holds in internal and technical prose too. "deserialize against the derived type rather than
paying for a base-class allocation" is a violation in an internal API doc comment exactly as it would
be in a guide. Say **rather than incurring**, or name the cost.

Precedent rename: `TotalPaidOut` -> `TotalReleased`, `TotalPayouts` -> `TotalReleases`, `PaidOutInFull`
-> `ReleasedInFull`, `PayoutInstants` -> `ReleaseInstants`.

## Arrival and effect

Six senses take six different words. Recognise all six by `land`, `lands`, `landed`, `landing`, an
imported metaphor for events the game already names.

| Sense | Say | Recognise |
|---|---|---|
| Magnitude after mitigation or overheal | **Taken** / **Effective**, already domain terms here alongside `Overheal` | "Damage that actually landed on the player", `"Landed"` as a stat label |
| Nothing got through | **prevented**, **mitigated**, **absorbed** | "damage that never landed", "share of RawIncoming that never landed" |
| A cast or hit inside a window | **cast**, **pressed**, **taken**, **spent**: "casts made inside the window", "each pair was cast together" | "casts that landed inside the window", "each pair landed together" |
| An aura arriving | **applied**, matching `ApplyBuffEvent` and `ExecutionersGrinTracker.Reapplications` | "when the buff landed", "the debuff never landed", "landed on a proc already held" |
| The state at the moment of a press | **cast** / **pressed**: "Nettlebolts cast at full charges", "this Detonate was cast into nothing" | "Nettlebolts that landed at full charges", "this Detonate landed on nothing" |
| A resource gain arriving | **arrives**, as `WinterOrbGuide` already says in "the gains that arrived on a full pool" | "before the next gain lands" |

Identifiers move too: `LandedResets` -> `RecoveredResets`, `var landed` -> `var taken`. The word hides
inside compound identifiers such as `OnNettleboltLanded` and a long tail of test method names, which is
where the renames are.

## Presence and holding

Nine senses. Recognise them by `carry`, `carries`, `carried`, `carrying`, which Fellowship's own
strings use zero times across all five `s3` files, against 8 uses of `active` and 15 of `strikes`.

| Sense | Say | Recognise |
|---|---|---|
| An aura is on a unit | **active** / **present** | "the Rend the target carried" |
| Share of a pull | **uptime** | "how much of the pull it carried" |
| The thing was there at all | **exists** / **existed** | "no window carried a spender" |
| Already there at pull start | **already active** for an aura; delete for a resource, since the reader knows it persists between pulls | "carrying Toughness in from before" |
| An event or record field holds or lacks a value | **with** / **with no**, then name the field, as in `ShieldMasteryAnalyzer.HitsWithoutToughness` | "casts carrying a Focus reading" |
| A dataset does not record something | name the dataset: **the log records no**, **the data lists no**, the report **shows** | "the log carries no signal" |
| A window, buff or cast holds N | **granted** (`ChargesGranted`), **made inside the window**, **contained**, or name the count | "the charges it carried" |
| The unit a companion or assignment is on | **holder**, from `BlueyAnalyzer.Holders` and `BlueyTracker.TimeByHolderBetween` | "whoever was carrying Bluey" |
| Engineering prose with no game entity in it | **marked with**, **gives**, **with**, introducing no domain word | "a class carrying `[HeroAnalyzer]`" |

The first four senses are owner-stated. The rest are drawn from the model rather than invented, and are
pending confirmation; the evidence for each is in `guide-vocabulary-audit/carry-family.md`.

Identifiers move too: `FreezingTorrent_CarriesGeneratedScalars` -> `..._HasGeneratedScalars`.

## Aura presence

| Quantity | Say |
|---|---|
| Point-in-time presence | **active** ("the Rend active on the target") |
| Past duration of presence | **was active** |
| An aura window | **open** ("while the window was open") |
| Share of a pull | **uptime**, not "covered", not "were active for" |
| A spendable charge or stack | **available**, because a charge is a resource rather than a duration buff |
| A stacked-chart marker position | **stack count**, never "height" |
| Title-case stat label | Active first: "DoTs Standing" -> "Active DoTs" |

Recognise the imported word by `stands`, `was standing`, `stood` applied to a buff, debuff or DoT. The
game's own tooltip strings write "While <Name> is active, ...".

That settles the presence word; it does not ban the noun. **`buff` and `debuff` stay**, because the
model uses them throughout (`ApplyBuffEvent`, `RemoveBuffStackEvent`, `Spells.FocusedWrathSelfBuff`)
and rule 5 keeps prose on the model's word. Prefer the ability's name where the sentence reads better
for it.

Leave `standard`, `StandardGcd`, `netstandard` alone.

## Resources: name the resource, not its display

Say **maximum Toughness** and **at maximum**, and label reduction steps by the reduction itself: `At
40% Reduction`, `40% Reduction`, `No Reduction`. Say **threshold** for a level a cast has to reach, and
for health say "on a target already at full health".

Recognise the display words by `notch`, and by `bar` bound to a quantity: "the combo-point bar", "the
AoE bar", "full health bars", "Toughness bar". The discriminator is whether a resource is attached to
the word.

`bar` standing alone is ordinary English and is correct as written: `QualitativePerformance.Ok` reads
"Meets the minimum acceptable bar but falls short of a good result." No resource, no widget. A UI
element genuinely named a bar (`StackedBar`, `CastBar`, `AuraBar`, `accent-bar`) keeps the word.

The detached form needs an eye rather than a pattern: "on a bar that was already full", "what was lost
to full bars" both name the widget with the resource elsewhere in the sentence.

## Flutterfly placement

A pink Flutterfly is **assigned** to an ally, or **unassigned**. Reserve `bank` and `banked` for
something that genuinely accumulates: Heart Bloom's stored healing, Focus and Fury pools, spell
charges.

## Analyzer surfaces

Razor reads analyzer instances directly. Typed data lives in the analyzer; prose and tiers live only in
the Razor component. `Finding`, `Report` in the verdict sense, and `ScoreCard` name concepts this
codebase does not have, as types, properties or headings.

## Names state what is assessed

An analyzer or guide name states what it **actually assesses**. The **Window** and **Assignment**
qualifier families were removed across every hero (`SupremacyWindowsGuide` -> `SupremacyGuide`,
`PinkFlutterflyAssignmentAnalyzer` -> `PinkFlutterflyAnalyzer`). A wrong name manufactures a false
reason to split an analyzer in two, which collides with the one-analyzer-per-ability rule.

## State the narrowest reading

Give the narrowest reading that explains a correction, say which follow-on edits it forces, and mark
the rest as inferences awaiting confirmation. Batch open questions into one round. **"unambiguous"**,
**"clearly implied"** and **"the only possible reading"** overstate an inference, in prose and in a
message to the owner alike.

## Tier against a fixed reference

Tier on gear-independent setup conditions, on gear-independent units such as a stack count, or against
a fixed game cap. Damage figures take no `QualitativePerformance` and say so: "this is context, not a
verdict." A share of the pull's best cast or window ("of your best window", "compared to your biggest
bank") describes the log against itself and scores nothing.

## Every sentence comes from the log

FSA is a log-analysis tool. Guide prose renders analysis-driven suggestions about this log. "How to
Play", Overview, Core Mechanic, openers-and-priorities, Cooldowns and "Talents and Builds" prose belong
to a guide site, as does an "adapted from method.gg" source link. method.gg is research input for
building analyzers, never UI content.

## Punctuation

Use hyphens, commas, or a restructured sentence. No em dashes or en dashes anywhere: code, prose,
commit messages, UI text.

---

## When a reword is not obvious

Apply the confident cases. For any site where the correct domain term is genuinely uncertain, typically
an adjacent quantity such as a chart axis, a geometric position, a resource or a rating rather than the
main pattern, **collect it and ask in one batch** with the alternatives spelled out. A word that reads
fine in isolation can still be wrong domain vocabulary, and a wrong term propagates silently through
prose, labels and identifiers.

Verify any delegated reword with `git diff --numstat` (insertions equal deletions for a pure reword),
`git diff --check`, and a byte check of the file end. Subagents have reported leaving lines untouched
that they edited, and reported rewording clauses they had deleted.
