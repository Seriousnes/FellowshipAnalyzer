---
name: house-style
description: >
  FellowshipAnalyzer's writing style: the voice, clause types, grammar, and vocabulary for every
  rendered and documented text surface. Use when: writing or editing guide prose, stat labels,
  tooltips, statistics descriptions, changelog entries, XML doc comments, or public identifiers;
  naming an analyzer, module, guide, or property; reviewing a diff that touches any of those.
---

# House style

The style governs everything a reader sees or another author copies: rendered prose, stat labels,
tooltip strings, changelog text, XML doc comments, and public identifiers. Judging text means
reading its whole file: the stat's label, the value beside it, the sibling stats in the same card,
and the prose in the panel above all bear on what a clause is saying.

## The reader

The reader knows every mechanic: what an ability does, what it procs, what it consumes, how its
charges behave, what a legendary item grants, what a talent changes. Prose delivers what only this
report can say: what happened, and what to do about it. `FocusedWrathGuide` is the shape: it says
which spender to put the charges into and how target count decides that.

## Three clause types

Every clause is one of these three.

- **A measurement**: the quantity, and what was counted.
- **A reading note**, only where it changes how the number reads: weighted by time, unscored,
  measured against the build's own ceiling.
- **A directive**: when to cast it, what to spend it on where there is a choice, what to have
  ready first.

Writing means choosing one. Reviewing means confirming each clause is one, judged before any word
is weighed; a clause that is none of the three is removed, and most review sites resolve there.

**A condition clause earns its place by narrowing the count.** "Fury spent on Hammer Storm" narrows
the count, because Fury has other spenders; where a resource has a single sink, "procs spent" is
the complete measurement. "Damage converted to Rend while Bloodbound Spirit was active" keeps its
clause for the same reason.

**A mechanic clause earns its place by stating what no single ability tooltip says**: an
interaction across two abilities ("Each hit from Primordial Storm is considered a Main-Hand attack,
and can trigger Windfury Weapon separately") or a ranking among abilities ("the most efficient way
to reduce the cooldown of Sanctify"). A tooltip cannot rank and cannot see a second ability.

**The surface sets the depth.** A left panel keeps the clause that motivates a directive ("Try to
use Executioner's Grin as soon as possible, to avoid overwriting any procs.") and may open on a
ranking ("key to maximising single target damage"). A stat description or tooltip states the
measurement alone, and where that leaves it restating its label, the short tooltip is finished.

## Literal words only

Metaphors of all kinds are banned, for any reason, on every surface this style governs: prose,
labels, tooltips, doc comments, identifiers, commit messages. Every event, quantity, and mechanism
takes its literal name, the model's word first, then the game's. A word that pictures the domain as
something else is wrong even where it reads well, because it starts a second vocabulary for a
quantity that already has a name.

## Grammar of a summary

A stat description, tooltip, or doc summary is a single line naming the quantity.

- The participle attaches straight to the noun: "Procs expired unspent.", "Sunders cast during
  Avatar of Stone." The ability name pluralises and takes the participle directly.
- The minimal phrase naming the quantity is the whole line: "Time at {max} stacks.", with no
  subject-verb frame around it.
- A second quantity takes its own label.
- A label names the quantity the member computes in the fewest words that do ("Extra Casts",
  `Holders`), and stays true if the ability changes tomorrow.
- An unmeasurable case folds into the nearest category: a stealth Backstab with no poison debuff
  reads "no poison was applied".
- An absence is stated plainly: "No resource snapshot was observed."

Shapes to copy:

- "Per-spell healing to mana efficiency."
- "Number of times the DoT was refreshed before expiring."
- "The spell's mana efficiency."
- "Casts with no Shields Up charge left."
- "Culling Strikes cast inside an execute phase."
- "Time at {max} stacks."

## Left panel voice

The shapes below are derived from the WoWAnalyzer retail corpus, which the owner contributes to.
The authoritative samples are the two the owner wrote by hand, `shaman/enhancement` and
`shaman/elemental` in that repository.

### The three moves

A left panel is a role sentence, then directives, then a reading note. Only the directives are
mandatory, the panel's sentences are these three moves, and the panel ends after its reading note.

```razor
<LeftPanel>
    <p>
        <strong><SpellLink Spell="Spells.{Ability}" /></strong> is your {ranking}.
        {Directive}, and {precondition to have ready}.
    </p>
</LeftPanel>
```

Fill the placeholders from the hero's own registry and the directive you were given. Each hero's
resource and ability names come from its own kit.

**1. Role sentence.** The ability name in bold, then where it ranks in the hero's kit. A
comparative or superlative is the point of this sentence: "your highest damage-per-Focus spender",
"your primary filler while the cooldown is unavailable", "the strongest Blood spender". A tooltip
cannot rank, so a ranking is decision content and belongs here.

**2. Directives.** One to three sentences: when to cast it, what to spend it on where there is a
choice, what to have ready first. Live example, `SerratedEdgeGuide.razor`: "Avoid consuming
Serrated Edge on filler abilities, try to consume it with Grim Carve for AoE or Heart Splitter for
single target or priority targets."

**3. Reading note.** Only where it changes how the number should be read. A scoring scope ("the
only casts flagged here are those cast with the buff already available") and an exclusion ("Blood
spent during Slaughter is not evaluated") both qualify. Where nothing changes the reading, the
panel ends after the directives.

A panel may be a `<ul>` instead of paragraphs where the directives partition cleanly and each
bullet opens on a bolded verb, as `FuryEconomyGuide.razor` does with **Build** and **Spend**. Use
it for a resource economy panel; use paragraphs everywhere else.

### Register

Mixed, set by how much the pull can prevent compliance.

| Strength | Words | Use for |
|---|---|---|
| Absolute | `Never`, `Always` | A state that is wrong regardless of build: a generator cast at cap, a spender below its floor |
| Target | `Try to`, `Aim to`, `Attempt to` | A percentage or count the pull can deny: uptime, window fill, cast efficiency |
| Plain | bare imperative | Everything else: "Spend Blood at five stacks", "Open the window with Owed in Blood ready" |

Second person earns its place where the sentence reports the reader's own result ("You overcapped
47 Fury during this pull").

### Sentence templates

Each of these recurs across the corpus. Fill and use them rather than inventing a shape.

| Shape | Template |
|---|---|
| Resource opener | "{Hero}'s primary resource is {resource}. Avoid capping {resource}, lost {resource} generation is lost damage." |
| Builder and spender pair | "Never use a builder at maximum {resource}, and always wait until {N} to use a spender." |
| Role plus directive | "**{Ability}** is your {ranking}. {Directive}." |
| Window fill | "During {window}, cast as many {ability} as possible. Enter it with {precondition} so you can begin immediately." |
| Uptime target | "Keep {aura} active on the target at all times. Try to maintain {N}% uptime." |
| Cooldown holding | "{Hero}'s cooldowns should not be held for long. Cast each as soon as it becomes available, as long as a target is within range." |
| Chart pointer | "The chart below shows your {quantity} through {pull}." |
| Graph legend | "{Graph name} - this graph shows {what it plots}. Grey segments show {neutral state}, yellow segments show {busy state}. Red segments highlight {the missed opportunity}." |
| Tier legend | "Perfect - {condition}. Good - {condition}. Ok - {condition}. Fail - {condition}." |
| Concession | "{Absolute directive}. It will occasionally be impossible to {comply}, while handling mechanics or during {phase type}." |
| Permission | "{Doing X} briefly is fine, but {the condition that makes it a failure}." |
| Named non-goal | "This section is about {what it measures}, not {the adjacent quantity it does not}." |
| Measurement boundary | "This section flags only {the one condition measured}. {The other case a reader would expect to be judged} is not flagged, and is treated as acceptable." |
| Window ceiling | "You {cast} {n} of a maximum of {max} this window, from {entry state}, {gains during it}, and {gains that arrived too late to convert}." |
| Defensive tolerance | "{Defensive} usage varies from pull to pull, and may need to be delayed for specific mechanics. Any amount of usage is good, and anywhere you could fit another usage is a theoretical loss." |
| Context, not a verdict | "This section is informative only and is not suggestive of poor performance." |

Absolute directives take the concession in the same paragraph, never in one of their own.

A proc or window analyzer that scores timing takes four in order: the interaction, the directive,
the named non-goal, then the permission clause. Naming the non-goal is what stops a reader reading
a timing score as a throughput score. `SerratedEdgeGuide.razor` shows the permission form: "A
sub-optimal consumer is better than a missed cast opportunity, so avoid holding Blood Arc for too
long."

The window ceiling template keeps a per-window maximum honest: derive it from that window's own
entry state and length.

### Numbers in prose

The left panel may state a measured value inline: "You overcapped 47 Fury during this pull." Where
the value is the panel's whole point, this is preferred to a bare directive beside a table that
repeats it. The tier is shown by the stat in the right panel.

### Nothing to report

Three states, three forms. An individual passing cast gets its tier colour and no praise.

| State | Form |
|---|---|
| Scored, nothing failed | The only place praise belongs: "All of your casts of this ability were good!" |
| Nothing recorded | A `TipBox` with `Variant="TipBoxVariant.Info"`, stating it plainly: "No Serrated Edge buff was recorded on any pull." |
| Not built yet | A plain statement ending in a period: "Per-cast breakdown for this ability is not built yet." |

### Panel width

`GuideSection` splits at `LeftPanelPercent`, default 40, and collapses to one column under 768px,
so write "the table", locating nothing on the screen.

## The lexicon

The word for each sense. A sense outside these tables takes its word from the model or from the
game's tooltip strings, in that order.

### Auras and presence

| Sense | Word |
|---|---|
| An aura on a unit now | active, present |
| Past presence | was active |
| An aura window | open |
| Share of a pull | uptime |
| Summed window duration | active time |
| An aura arriving | applied |
| Arriving on one already active | reapplied, overwritten |
| A DoT lapsing on a boss | drops off; an event doc comment says expires |
| A break between aura windows | gap |
| A DoT's spread across enemies | spread to, spread across |
| A stacked-chart marker position | stack count |

`buff` and `debuff` are the model's nouns and stay; prefer the ability's name where the sentence
reads better for it.

### Damage and healing

| Sense | Word |
|---|---|
| Damage after mitigation | taken |
| Healing that raised health | effective; the remainder is overheal |
| Damage stopped | prevented, mitigated, absorbed |
| The enemies a cast damaged | hit |

### Casting

| Sense | Word |
|---|---|
| An ability activated | cast |
| A surface named for uses | used |
| A proc spent by casting another ability | used, spent |
| A cooldown ready | became available |
| Ready and cast at once | on cooldown, "cast the moment it becomes available" |
| An ability available at pull end | still available, not cast |
| The halves of a cast | activation, completion |
| A low-priority cast occupying a global | filler |
| A bounded counting period | window |
| Time outside every pull | time between pulls |

### Resources and procs

| Sense | Word |
|---|---|
| A resource gain arriving | arrives |
| A resource leaving | spent |
| A spendable charge or stack | available |
| A proc that lapsed | expired unspent, or expired unused, matching the sibling stat's verb |
| A resource lost at the ceiling | wasted, overcapped |
| Generating above the ceiling | overcap, capping |
| Accumulating ahead of a window | pool, pooling |
| Genuine accumulation | bank, banked: stored healing, resource pools, spell charges |
| A resource ceiling | maximum: "maximum Toughness", "at maximum" |
| A level a cast must reach | threshold |
| Full health | "on a target already at full health" |
| A reduction step | the reduction itself: "At 40% Reduction", "No Reduction" |

### Cost and contribution

| Sense | Word |
|---|---|
| A refund or grant | released, returned, restored, granted |
| A cost | costs, draws on, incurs |
| An item's effect over an encounter | contributed |
| A double-counted quantity | counted twice, or the multiplier by name |
| A comparison between spenders | the measured ratio: "Hammer Storm returns twice the Fury Skull Crusher does" |

### Companions and fields

| Sense | Word |
|---|---|
| The unit a companion is on | holder |
| A pink Flutterfly on an ally | assigned; otherwise unassigned |
| An event field's value | with, with no, then the field's name |
| A class or member marked by an attribute | marked with, gives, with |

## Where words come from

1. **The model being documented.** Read the type, the member and the event first; the domain term
   is nearly always already there: `Taken`, `Effective`, `Overheal`, `applied` from
   `ApplyBuffEvent`, `reapplied` from `Reapplications`, `holder` from `TimeByHolderBetween`. One
   word serves prose, label and identifier alike. Read a donor sentence in full before copying it,
   so its wording passes the clause types first.
2. **The game's tooltip strings**, in the `description` field of the ability, effect, talent and
   trait records in `data/v*/entities.jsonl`. The game says an ability **strikes**, **deals**,
   **applies**, and that you **gain**; an aura **is active** and **expires**. Write the game's word
   for an event the game names. One line of the export is one whole JSON record, so counting a
   word's tooltip use takes every stage of this pipeline:

```
grep -E '^\{"\$type":"(ability|effect|talent|trait)"' data/v*/entities.jsonl \
  | grep -oE '"description":"[^"]*"' \
  | sed -E 's/\\u[0-9A-Fa-f]{4}/ /g' \
  | grep -oiE '\b<word>\b' | sort | uniq -c
```

Analysis words the game has no event for ("unused", "uptime", "overcap") are this codebase's own,
and the lexicon above is their register.

## Names

- An analyzer or guide is named for what it assesses: `SupremacyGuide`, `PinkFlutterflyAnalyzer`.
- A member takes its label's word, and a rename propagates to every reader: other guides, statistics
  components, tests.
- A test method names the observed behaviour: `FreezingTorrent_HasGeneratedScalars`.

## Tiers

A tier compares against a fixed reference: a gear-independent setup condition, a stack count, or a
game cap. A damage figure renders untiered.

## This report

Rendered prose is analysis of this report. It names the game's entities and the report's own
quantities; a stat says what was counted; an absence is stated plainly ("No resource snapshot was
observed."). `Fellowship Logs` is the product the report came from and is named normally.

## Punctuation

A hyphen, a comma, or a restructured sentence joins clauses, everywhere: prose, code, string
literals, commit messages, UI text.

## The surfaces

Every one of these renders text a reader sees, or documents code another author will copy. Locate
them by component name, then read the whole file.

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

A hero guide lives in `src/Heroes/*/Guides/*.razor`. A module that renders a statistics card is a
Razor component too, `src/Heroes/*/Modules/*.razor`, with the same prose obligations. Core
UI under `src/FellowshipAnalyzer.Core/UI/` renders inside every hero's guide, so a label there is the
most widely rendered text in the repository.

## An uncertain word

Where the right word for a sense is genuinely uncertain, typically an adjacent quantity such as a
chart axis, a geometric position, a resource or a rating, collect the site and ask in one batch
with the alternatives spelled out. A word propagates through prose, labels and identifiers, so it
is settled once, as a question to the owner.
