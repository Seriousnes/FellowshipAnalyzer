# Guide skill training

`create-guide/SKILL.md` is trained rather than hand-maintained. A round dispatches the frozen
`guide-writer` agent across a corpus of sections the owner has blessed, counts what each draft got
wrong, proposes one edit to the skill, and keeps that edit only when it strictly improves the count on
a held-out split.

The method is SkillOpt (arXiv 2605.23904v2), reduced to the scale this repository runs at. Its
transferable result: a skill reaches its full gain on one to four accepted edits, and the deployed
document stays under 2,000 tokens. The gate is what produces that, by rejecting almost everything the
optimizer proposes.

## Roles

| Role | File | Changes how |
|---|---|---|
| Harness | `.claude/agents/guide-writer.md` | Frozen. A round never edits it |
| Trained artifact | `.claude/skills/create-guide/SKILL.md` | One gated edit per round |
| Scorer | `.claude/skills/guide-review/SKILL.md` | By hand, by the owner |
| Judge | `.claude/agents/guide-judge.md` | By hand, by the owner |
| Optimizer | `.claude/agents/guide-optimizer.md` | By hand, by the owner |
| Corpus | `.claude/guide-training/corpus/` | By hand, when the owner blesses a section |

`guide-review` is read by the judge and the optimizer. `guide-writer` does not read it. A vocabulary
rule reaches the writer by passing the gate into `create-guide`, one rule at a time, which is the test
of whether that rule changes what the writer produces.

## The corpus

Nine sections, split into three equal parts: three supply proposals, three gate them, and three are
held back for neither, scored once at the end of a run. Six sections in a three-way split of two is
the smallest corpus a round can use. At this size the gate is the point and the totals are noise.

One fixture per section:

```
.claude/guide-training/corpus/<hero>-<section>/
  fixture.md        directive, blessing date, source paths, split assignment
  analyzer.cs       the analyzer as of the blessing date
  guide.razor       the guide with every writer-owned region emptied
  blessed.razor     the owner's approved prose
```

`fixture.md` states the directive in the owner's own words: when to press the ability, what to spend
it on, what to have ready first. `guide-writer` fails closed without one, so a fixture missing it
produces no rollout.

Blessing is a dated override. `guide-writer.md` and `guide-review` both instruct that no file is
pre-cleared, so a section enters the corpus because the owner cleared it on a stated date, and
`fixture.md` records that date.

The writer-owned regions emptied in `guide.razor` are the ones `guide-writer.md` lists: `LeftPanel`
prose, `GuideSection` / `SubSection` / `CastOverview` / `CastDetail` titles and descriptions,
`HelperText` and `TipBox` bodies, and every `OverviewStat` and `PerCastStat` label and tooltip.

## The scorer

Five counts per section, produced by `guide-judge` reading the candidate prose, the blessed prose,
and `guide-review`.

| Count | Symbol | Direction |
|---|---|---|
| Mechanic clauses surviving the tooltip test and the counting test | `m` | lower |
| Clauses the blessed version deletes that the candidate keeps | `k` | lower |
| Settled-vocabulary breaches against `guide-review` | `v` | lower |
| The section states what was measured | `s` | boolean, higher |
| The section states the dispatched directive | `d` | boolean, higher |

Section score is `s + d - m - k - v`. Run score is the sum across the split. `s` and `d` are booleans
so the score cannot be inflated by adding clauses, which caps a run at twice the section count.

`m` and `k` are counted against each other by design. `k` alone is maximised by an empty section, and
deletion is the common outcome on this corpus, so `s` and `d` are what stop the optimizer training the
writer to delete everything.

Counts reproduce across judge calls. A quality rating does not, and the gate compares with strict
inequality.

### Judge stability

Every round rescores the blessed prose as a control. A control that moves by more than one count means
the round's numbers are not comparable to the previous round's, and the round is discarded without
applying an edit.

### The falsification test

Before any optimizer exists, score the blessed prose and a fresh `guide-writer` draft on three
fixtures. The blessed prose must lead by more than the control band on at least two of the three
sections, so by two counts or more, and lead on the total. A one-count lead sits inside judge
variance and is not a pass.

A scorer that does not separate them makes the gate inert, and the loop cannot work at any budget.
This test is the first deliverable and everything after it depends on the result.

## A round

`/train-guide-skill` runs one round.

1. **Rollout.** Copy each training fixture into `runs/<run-id>/rollouts/` and dispatch `guide-writer`
   at the copy, never at the corpus. Its report contract already returns `git diff --numstat`,
   `git diff --check`, every clause deleted with its reason, and every member renamed.
2. **Score.** `guide-judge` produces the five counts per rollout, plus the blessed control.
3. **Reflect.** `guide-optimizer` reads the rollouts split into failures and successes by score, plus
   `rejected.md`. Failure rollouts propose corrective edits; success rollouts propose edits that
   preserve what worked. Failures win a conflict.
4. **Bound.** Rank the pool by how many rollouts an edit addresses, then by generality, and keep one.
5. **Gate.** Apply the edit to a candidate copy of `create-guide`, rerun the selection split, and
   accept only on a strictly greater run score. A tie is a rejection, so the skill never drifts on
   noise.
6. **Buffer.** A rejected edit appends to `rejected.md` with the score drop it produced. The next
   round reads it and does not repropose it.

Edit budget is one per round.

Edits are `append`, `insert_after`, `replace`, and `delete` against `create-guide`. `delete` is
available on every round, so a rule that stops earning its place can leave.

### Longitudinal guidance

`create-guide` holds a `## Longitudinal guidance` section. Round edits never target it. It is
rewritten only at the end of a run, from a comparison of the same fixtures under the run's first and
last skill version, sorted into improvements, regressions, and persistent failures. That rewrite
passes the same gate.

## Layout

```
.claude/guide-training/
  README.md
  corpus/<hero>-<section>/
  runs/<run-id>/
    rollouts/       one directory per fixture, holding the draft and the agent report
    scores.json     five counts per section, plus the control
    proposals.json  the full pool, with the ranking and the selected index
    decision.md     accepted or rejected, the score before and after
  rejected.md
```

`runs/` is the recoverable record of why `create-guide` says what it says.

## Build order

1. The owner blesses six to nine sections and writes each `fixture.md` directive.
2. `create-guide/SKILL.md` is committed, which `81184bb` did. It is the trained artifact, so a round
   has no baseline until it settles.
3. Reconcile `guide-review` against the memory record. `feedback_never_mention_the_log` postdates the
   rulebook and reverses it: the presence table blesses "the log records no", the memory states that
   phrasing was never blessed. The judge reads `guide-review` as its rubric, so an unreconciled entry
   produces a wrong count rather than a stale note.
4. `guide-judge` and the five counts. Run the falsification test.
5. Rename `banned-vocabulary` to `guide-review` and update the ten referring lines across eight files:
   `guide-writer.md` lines 32 and 53, `create-guide/SKILL.md` line 70, and six memory files including
   `MEMORY.md`. Both `guide-writer.md:32` and `create-guide/SKILL.md:70` instruct that the record be
   read at write time, and `81184bb` committed the second of those by hand today, so removing them is
   the owner's call to confirm rather than a mechanical rename. This is setup, and it is the last edit
   made to the harness before it is frozen.
6. `guide-optimizer`, the budget, the gate, and `rejected.md`.
7. Longitudinal guidance.
