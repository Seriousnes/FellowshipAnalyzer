# Guide skill training implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Train `.claude/skills/create-guide/SKILL.md` against a corpus of owner-blessed guide sections, one gated edit per round, so the writing procedure improves from measured evidence rather than hand maintenance.

**Architecture:** A fixture is a pair of git refs around an owner prose correction, so `git show` materialises both the draft and the approved text with no copying. `guide-judge` reads a candidate section and returns five integer counts. `score.cs` reduces those counts to a run total and a gate verdict, deterministically and outside the model. `guide-optimizer` proposes edits to `create-guide`; one survives ranking, and it is kept only on a strictly greater gate score.

**Tech Stack:** Markdown skills and agents under `.claude/`, one C# file-based script (`dotnet run score.cs`) for the arithmetic, git for fixture materialisation, PowerShell for the test runner.

**Design source:** `.claude/guide-training/README.md`. Read it before Task 1.

## Global Constraints

- Edit budget is **one** accepted edit per round. Not the paper's default of four.
- The gate compares with **strict inequality**. A tie is a rejection.
- Scores are **integer counts**, never a quality rating. The arithmetic runs in `score.cs`, not in a model.
- **No comments in any file**, including `<!-- -->` in markdown, `#` in YAML frontmatter, and `//` in C#.
- **No em dashes or en dashes** anywhere, including the `&mdash;` and `&ndash;` entities. Use a hyphen, a comma, or a restructured sentence.
- **Python is not installed.** Use PowerShell or `dotnet run <script>.cs`.
- **Never run `git stash`** or any stash-mutating command. Read-only `git stash list` and `git stash show` are fine.
- After Task 6, `.claude/agents/guide-writer.md` is frozen. No later task edits it.
- `.claude/skills/create-guide/SKILL.md` is the trained artifact. Only Task 8 and Task 9 edit it, and only through the gate.
- Every task commits on completion.

---

### Task 1: Corpus fixtures as git ref pairs

**Files:**
- Create: `.claude/guide-training/corpus/FORMAT.md`
- Create: `.claude/guide-training/corpus/sylvie-safehaven/fixture.md`
- Create: `.claude/guide-training/corpus/sylvie-mana/fixture.md`
- Create: `.claude/guide-training/tools/materialise.ps1`
- Test: `.claude/guide-training/tools/test-materialise.ps1`

**Interfaces:**
- Produces: a fixture directory name, and `materialise.ps1 -Fixture <name> -Side before|after -OutDir <path>` writing `guide.razor` and `analyzer.cs` for that side.

**Owner prerequisite, blocking:** every `fixture.md` needs its directive written in the owner's own words. `guide-writer` fails closed without one, so a fixture missing it produces no rollout. The two fixtures below are seeded from `06055b4`. The owner adds seven more to reach nine, three per split, each naming a commit pair where a prose correction landed. `git log --format='%h %s' -- 'src/Heroes/**/Guides/*.razor'` lists the candidate commits. Tasks 3 onward run on the two seeded fixtures; Task 5 needs a third before it can apply its majority threshold.

- [ ] **Step 1: Write the fixture format**

Create `.claude/guide-training/corpus/FORMAT.md`:

```markdown
# Fixture format

One directory per blessed section, named `<hero>-<section>` in lower kebab case.

Each directory holds exactly one file, `fixture.md`, with this frontmatter and body.

    ---
    hero: Sylvie
    section: Safe Haven
    guide: src/Heroes/FellowshipAnalyzer.Heroes.Sylvie/Guides/SafeHavenGuide.razor
    analyzer: src/Heroes/FellowshipAnalyzer.Heroes.Sylvie/Modules/SafeHavenAnalyzer.cs
    before: 81184bb
    after: 06055b4
    blessed: 2026-08-11
    split: propose | gate | held
    ---

    ## Directive

    The owner's own words. When to press the ability, what to spend it on, what to have
    ready first.

`before` is the ref where the section reads as a draft. `after` is the ref where the owner's
correction landed. Both sides are materialised with `git show <ref>:<path>`, so nothing is copied
and a fixture cannot drift from what was actually reviewed.

`blessed` is the date the owner cleared the `after` side. It is an explicit override: both
`guide-writer.md` and `guide-review` instruct that no file is pre-cleared.

`split` assigns the fixture. `propose` fixtures supply rollout evidence, `gate` fixtures decide
acceptance, `held` fixtures are used for neither and are scored once at the end of a run.
```

- [ ] **Step 2: Write the two seeded fixtures**

Create `.claude/guide-training/corpus/sylvie-safehaven/fixture.md`:

```markdown
---
hero: Sylvie
section: Safe Haven
guide: src/Heroes/FellowshipAnalyzer.Heroes.Sylvie/Guides/SafeHavenGuide.razor
analyzer: src/Heroes/FellowshipAnalyzer.Heroes.Sylvie/Modules/SafeHavenAnalyzer.cs
before: 81184bb
after: 06055b4
blessed: 2026-08-11
split: propose
---

## Directive

OWNER TO WRITE. This fixture cannot produce a rollout until this section states, in the owner's
own words, when to place Safe Haven and what to have ready first.
```

Create `.claude/guide-training/corpus/sylvie-mana/fixture.md`:

```markdown
---
hero: Sylvie
section: Mana
guide: src/Heroes/FellowshipAnalyzer.Heroes.Sylvie/Modules/SylvieManaTracker.razor
analyzer: src/Heroes/FellowshipAnalyzer.Heroes.Sylvie/Modules/SylvieManaTracker.cs
before: 81184bb
after: 06055b4
blessed: 2026-08-11
split: gate
---

## Directive

OWNER TO WRITE. This fixture cannot produce a rollout until this section states, in the owner's
own words, how mana should be managed across a pull.
```

Both analyzer paths are verified to exist: `SafeHavenAnalyzer.cs` and `SylvieManaTracker.cs` under
`src/Heroes/FellowshipAnalyzer.Heroes.Sylvie/Modules/`.

- [ ] **Step 3: Write the failing test**

Create `.claude/guide-training/tools/test-materialise.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $env:TEMP "fixture-test-$(Get-Random)"
$failures = @()

& "$PSScriptRoot/materialise.ps1" -Fixture sylvie-safehaven -Side before -OutDir $out
if (-not (Test-Path "$out/guide.razor")) { $failures += 'before side wrote no guide.razor' }
$before = Get-Content "$out/guide.razor" -Raw

& "$PSScriptRoot/materialise.ps1" -Fixture sylvie-safehaven -Side after -OutDir $out
$after = Get-Content "$out/guide.razor" -Raw

if ($before -eq $after) { $failures += 'before and after sides are identical, so the fixture holds no correction' }
if ($before -notmatch 'Reported as context rather than scored') { $failures += 'before side is missing the clause the owner deleted' }
if ($after -match 'Reported as context rather than scored') { $failures += 'after side still holds the deleted clause' }

Remove-Item $out -Recurse -Force
if ($failures) { $failures | ForEach-Object { Write-Host "FAIL: $_" }; exit 1 }
Write-Host 'PASS'
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `pwsh .claude/guide-training/tools/test-materialise.ps1`
Expected: FAIL, because `materialise.ps1` does not exist yet.

- [ ] **Step 5: Write materialise.ps1**

```powershell
param(
    [Parameter(Mandatory)][string]$Fixture,
    [Parameter(Mandatory)][ValidateSet('before', 'after')][string]$Side,
    [Parameter(Mandatory)][string]$OutDir
)

$ErrorActionPreference = 'Stop'
$repo = & git rev-parse --show-toplevel
$path = Join-Path $repo ".claude/guide-training/corpus/$Fixture/fixture.md"
if (-not (Test-Path $path)) { throw "No fixture named $Fixture" }

$text = Get-Content $path -Raw
$field = { param($name) if ($text -match "(?m)^$name`:\s*(.+)$") { $Matches[1].Trim() } else { throw "Fixture $Fixture has no $name" } }

$ref = & $field $Side
$guide = & $field 'guide'
$analyzer = & $field 'analyzer'

if ($text -match '(?s)##\s*Directive\s*(.+?)$') {
    if ($Matches[1] -match 'OWNER TO WRITE') { throw "Fixture $Fixture has no directive yet" }
} else { throw "Fixture $Fixture has no Directive section" }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
& git show "${ref}:$guide" | Set-Content (Join-Path $OutDir 'guide.razor')
& git show "${ref}:$analyzer" | Set-Content (Join-Path $OutDir 'analyzer.cs')
```

- [ ] **Step 6: Unblock the test on the directive check**

The test dispatches nothing, so replace the `OWNER TO WRITE` body in `sylvie-safehaven/fixture.md`
with a single line reading `Placeholder pending owner directive, materialise only.` and add
`-SkipDirectiveCheck` as a switch on `materialise.ps1` that bypasses the throw. The test passes
`-SkipDirectiveCheck`. Rollouts never pass it, so a fixture without a real directive still cannot
produce one.

- [ ] **Step 7: Run the test to verify it passes**

Run: `pwsh .claude/guide-training/tools/test-materialise.ps1`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add .claude/guide-training/corpus .claude/guide-training/tools
git commit -m "Add guide training fixtures as git ref pairs"
```

---

### Task 2: Reconcile guide-review against the memory record

**Superseded 2026-08-23.** `banned-vocabulary` was replaced by `.claude/skills/house-style/SKILL.md`,
which states the style positively and carries no `the log records no` entry, so there is nothing to
reconcile. The steps below are the earlier plan.

**Files:**
- Modify: `.claude/skills/banned-vocabulary/SKILL.md:292`

**Interfaces:**
- Produces: a rulebook whose presence table no longer contradicts `feedback_never_mention_the_log`.

This precedes the judge because the judge reads the rulebook as its rubric. An unreconciled entry
produces a wrong count, not a stale note.

- [ ] **Step 1: Read both sides**

Read `.claude/skills/banned-vocabulary/SKILL.md` lines 280 to 300, and
`C:\Users\Sean\.claude\projects\G--source-FellowshipAnalyzer\memory\feedback_never_mention_the_log.md`
in full.

Line 292 blesses `the log records no` as the replacement when a dataset does not record something.
The memory states that phrasing was never blessed and that the presence table misled a session into
using it.

- [ ] **Step 2: Confirm the resolution with the owner**

Ask which reading holds, quoting both. Do not decide it. The memory is newer, so the expected
outcome is that line 292's `the log records no` is removed and the row's remaining replacements
stand, but that is the owner's call and a wrong resolution poisons every count the judge produces.

- [ ] **Step 3: Apply the owner's resolution**

Edit the single table row on line 292 to match. Change nothing else in the file.

- [ ] **Step 4: Verify no other entry contradicts a memory**

Run: `grep -rn "the log" .claude/skills/banned-vocabulary/SKILL.md`
Read each hit against `feedback_never_mention_the_log`. Report any further collision as a question
rather than editing it.

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/banned-vocabulary/SKILL.md
git commit -m "Reconcile the presence table against feedback_never_mention_the_log"
```

---

### Task 3: guide-judge and the five counts

**Files:**
- Create: `.claude/agents/guide-judge.md`
- Create: `.claude/guide-training/schema/scores.md`

**Interfaces:**
- Consumes: `materialise.ps1` output from Task 1.
- Produces: a JSON object per section with keys `fixture`, `split`, `measurementStated` (bool), `directiveStated` (bool), `mechanicClauses` (int), `keptDeletedClauses` (int), `vocabularyBreaches` (int), and `evidence` (array of strings). Task 4's `score.cs` reads exactly these keys.

- [ ] **Step 1: Write the score schema**

Create `.claude/guide-training/schema/scores.md`:

```markdown
# scores.json

    {
      "runId": "2026-08-12-01",
      "skillVersion": "baseline",
      "sections": [
        {
          "fixture": "sylvie-safehaven",
          "split": "gate",
          "measurementStated": true,
          "directiveStated": true,
          "mechanicClauses": 1,
          "keptDeletedClauses": 2,
          "vocabularyBreaches": 0,
          "evidence": [
            "mechanic: 'because an ally leaves the radius for reasons the log does not record'",
            "kept: 'The log lists no party roster, so this is not scored against a group size.'"
          ]
        }
      ]
    }

`skillVersion` is `baseline`, `candidate`, or `control`. A `control` document scores the approved
prose and detects judge drift between rounds.

Every count carries at least one `evidence` entry naming the clause it counted. A count with no
evidence is not reproducible and the round is discarded.
```

- [ ] **Step 2: Write the judge agent**

Create `.claude/agents/guide-judge.md`:

```markdown
---
name: guide-judge
description: Counts writing-rule breaches in one FellowshipAnalyzer guide section against the approved version. Returns five integer counts as JSON. Dispatch it with the candidate section, the approved section, and the dispatched directive.
model: inherit
color: yellow
tools: Read, Glob, Grep
---

You count. You do not rate, rank, or rewrite.

## Your dispatch carries

1. The candidate section, by path.
2. The approved section, by path.
3. The directive that was dispatched to the writer, verbatim.
4. The fixture name and its split.

## Read before you count

1. `.claude/skills/house-style/SKILL.md`, end to end. It is your rubric.
2. Both sections in full, not the differing lines. A clause is judged by what sits beside it.

## The five counts

For the candidate section only. The approved section is evidence of what the owner chose, never a
target to match wording against.

**mechanicClauses.** Clauses that fail the tooltip test or the counting test. The tooltip test: could
this clause have been written by someone who read the ability's tooltip but never opened a log. The
counting test: remove the clause, and ask whether the number changes.

**keptDeletedClauses.** Clauses the approved section does not contain and the candidate does. Count
the clause, not the sentence, and not a rewording of a clause the approved section keeps.

**vocabularyBreaches.** Breaches of the settled vocabulary. One per site.

**measurementStated.** True when the section says what the number is and what was counted.

**directiveStated.** True when the section states the dispatched directive. Compare against the
directive text you were given, not against the approved section's wording.

## Report

Respond with one JSON object and nothing else. No markdown fence, no preamble.

    {
      "fixture": "<name>",
      "split": "<split>",
      "measurementStated": true,
      "directiveStated": false,
      "mechanicClauses": 0,
      "keptDeletedClauses": 0,
      "vocabularyBreaches": 0,
      "evidence": ["<kind>: '<the clause you counted>'"]
    }

Every non-zero count needs at least one `evidence` entry quoting the clause. A false boolean needs an
evidence entry naming what is absent.

Do not total the counts. The arithmetic runs outside you.

## Constraints you do not inherit

Your caller runs an output style you do not have. Write no em dashes or en dashes, and add no
comments to any file.
```

- [ ] **Step 3: Run the judge against the approved side**

Materialise `sylvie-safehaven` on the `after` side, then dispatch `guide-judge` with that file as
both candidate and approved.

Expected: `mechanicClauses`, `keptDeletedClauses` and `vocabularyBreaches` all `0`, because the
approved side is the owner's own text scored against itself. A non-zero count here means either the
rubric and the owner disagree or the judge is inventing breaches, and both block Task 5.

- [ ] **Step 4: Run the judge against the draft side**

Materialise the `before` side and dispatch the judge with `before` as candidate and `after` as
approved.

Expected: `keptDeletedClauses` at least 2, because `06055b4` deleted
`Reported as context rather than scored, because an ally leaves the radius for reasons the log does
not record.` and `The log lists no party roster, so this is not scored against a group size.`

- [ ] **Step 5: Commit**

```bash
git add .claude/agents/guide-judge.md .claude/guide-training/schema
git commit -m "Add guide-judge and the five-count score schema"
```

---

### Task 4: score.cs and the gate arithmetic

**Files:**
- Create: `.claude/guide-training/tools/score.cs`
- Create: `.claude/guide-training/tools/testdata/baseline.json`
- Create: `.claude/guide-training/tools/testdata/candidate.json`
- Create: `.claude/guide-training/tools/testdata/expected-gate.txt`
- Create: `.claude/guide-training/tools/test-score.ps1`

**Interfaces:**
- Consumes: the `scores.json` shape from Task 3.
- Produces: `dotnet run score.cs -- <baseline.json> <candidate.json> <split>` printing a per-section table, both run totals, and a final line of exactly `ACCEPT` or `REJECT`. Task 8 reads that final line.

- [ ] **Step 1: Write the test data**

Create `.claude/guide-training/tools/testdata/baseline.json`:

```json
{
  "runId": "test",
  "skillVersion": "baseline",
  "sections": [
    { "fixture": "a", "split": "gate", "measurementStated": true, "directiveStated": false, "mechanicClauses": 2, "keptDeletedClauses": 1, "vocabularyBreaches": 0, "evidence": ["x"] },
    { "fixture": "b", "split": "gate", "measurementStated": true, "directiveStated": true, "mechanicClauses": 0, "keptDeletedClauses": 0, "vocabularyBreaches": 1, "evidence": ["x"] },
    { "fixture": "c", "split": "propose", "measurementStated": false, "directiveStated": false, "mechanicClauses": 9, "keptDeletedClauses": 9, "vocabularyBreaches": 9, "evidence": ["x"] }
  ]
}
```

Section `a` scores `1 + 0 - 2 - 1 - 0 = -2`. Section `b` scores `1 + 1 - 0 - 0 - 1 = 1`. The gate
total is `-1`. Section `c` is in another split and must be excluded.

Create `.claude/guide-training/tools/testdata/candidate.json` identical except section `a` has
`"mechanicClauses": 1`, giving `a = -1` and a gate total of `0`.

Create `.claude/guide-training/tools/testdata/expected-gate.txt`:

```
fixture              baseline  candidate  delta
a                          -2         -1     +1
b                           1          1      0
total                      -1          0     +1
ACCEPT
```

- [ ] **Step 2: Write the failing test**

Create `.claude/guide-training/tools/test-score.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
$data = Join-Path $PSScriptRoot 'testdata'
$actual = & dotnet run (Join-Path $PSScriptRoot 'score.cs') -- (Join-Path $data 'baseline.json') (Join-Path $data 'candidate.json') gate
$expected = Get-Content (Join-Path $data 'expected-gate.txt')

$diff = Compare-Object $expected ($actual -split "`r?`n" | Where-Object { $_ -ne '' })
if ($diff) { $diff | Format-Table | Out-String | Write-Host; Write-Host 'FAIL'; exit 1 }

$tie = & dotnet run (Join-Path $PSScriptRoot 'score.cs') -- (Join-Path $data 'baseline.json') (Join-Path $data 'baseline.json') gate
if (($tie | Select-Object -Last 1) -ne 'REJECT') { Write-Host 'FAIL: a tie was not rejected'; exit 1 }

Write-Host 'PASS'
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `pwsh .claude/guide-training/tools/test-score.ps1`
Expected: FAIL, because `score.cs` does not exist yet.

- [ ] **Step 4: Write score.cs**

```csharp
using System.Text.Json;

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: score.cs <baseline.json> <candidate.json> <split>");
    return 2;
}

var split = args[2];
var baseline = Load(args[0], split);
var candidate = Load(args[1], split);

Console.WriteLine($"{"fixture",-20}{"baseline",10}{"candidate",11}{"delta",7}");

var baseTotal = 0;
var candTotal = 0;

foreach (var fixture in baseline.Keys.OrderBy(key => key, StringComparer.Ordinal))
{
    if (!candidate.TryGetValue(fixture, out var candScore))
    {
        Console.Error.WriteLine($"candidate is missing fixture {fixture}");
        return 2;
    }

    var baseScore = baseline[fixture];
    baseTotal += baseScore;
    candTotal += candScore;
    Console.WriteLine($"{fixture,-20}{baseScore,10}{candScore,11}{Delta(candScore - baseScore),7}");
}

Console.WriteLine($"{"total",-20}{baseTotal,10}{candTotal,11}{Delta(candTotal - baseTotal),7}");
Console.WriteLine(candTotal > baseTotal ? "ACCEPT" : "REJECT");
return 0;

static string Delta(int value) => value > 0 ? $"+{value}" : value.ToString();

static Dictionary<string, int> Load(string path, string split)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    var scores = new Dictionary<string, int>(StringComparer.Ordinal);

    foreach (var section in doc.RootElement.GetProperty("sections").EnumerateArray())
    {
        if (section.GetProperty("split").GetString() != split)
            continue;

        var score = Bit(section, "measurementStated")
            + Bit(section, "directiveStated")
            - section.GetProperty("mechanicClauses").GetInt32()
            - section.GetProperty("keptDeletedClauses").GetInt32()
            - section.GetProperty("vocabularyBreaches").GetInt32();

        scores[section.GetProperty("fixture").GetString()!] = score;
    }

    return scores;
}

static int Bit(JsonElement section, string name) => section.GetProperty(name).GetBoolean() ? 1 : 0;
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `pwsh .claude/guide-training/tools/test-score.ps1`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add .claude/guide-training/tools
git commit -m "Add score.cs and the strict-inequality gate"
```

---

### Task 5: The falsification test, a stop-or-continue gate

**Files:**
- Create: `.claude/guide-training/runs/falsification/scores-approved.json`
- Create: `.claude/guide-training/runs/falsification/scores-draft.json`
- Create: `.claude/guide-training/runs/falsification/decision.md`

**Interfaces:**
- Consumes: Task 1 fixtures, Task 3 judge, Task 4 scorer.
- Produces: a written verdict. Tasks 6 to 9 do not start until it reads CONTINUE.

This is the load-bearing assumption of the whole method. If the scorer cannot separate the owner's
prose from a draft, the gate is inert and no edit budget rescues it.

- [ ] **Step 1: Score the approved side of three fixtures**

Materialise the `after` side of three fixtures. Dispatch `guide-judge` on each with the approved
section as candidate and the same file as approved. Collect the objects into
`scores-approved.json` with `"skillVersion": "control"`.

- [ ] **Step 2: Score the draft side of the same three fixtures**

Materialise the `before` side. Dispatch `guide-judge` on each with `before` as candidate and `after`
as approved. Collect into `scores-draft.json` with `"skillVersion": "baseline"`.

- [ ] **Step 3: Measure the control band**

Repeat step 1 twice more, into `scores-approved-2.json` and `scores-approved-3.json`. The control
band is the largest per-section spread across the three runs.

Run: `dotnet run .claude/guide-training/tools/score.cs -- .claude/guide-training/runs/falsification/scores-approved.json .claude/guide-training/runs/falsification/scores-approved-2.json gate`

Expected: a `total` delta of `0`. A non-zero delta is the band, and it goes in the decision.

- [ ] **Step 4: Apply the threshold**

Run: `dotnet run .claude/guide-training/tools/score.cs -- .claude/guide-training/runs/falsification/scores-draft.json .claude/guide-training/runs/falsification/scores-approved.json gate`

CONTINUE requires the approved side to lead by **more than the control band, so by two counts or
more**, on at least two of the three sections, and to lead on the total. A one-count lead sits inside
judge variance and is not a pass.

- [ ] **Step 5: Write the decision**

Create `decision.md` stating the per-section scores, the measured control band, the threshold, and
CONTINUE or STOP. On STOP, state which of the five counts failed to discriminate and hand back to the
owner. Do not proceed to Task 6 on a STOP.

- [ ] **Step 6: Commit**

```bash
git add .claude/guide-training/runs/falsification
git commit -m "Record the falsification verdict for the guide scorer"
```

---

### Task 6: Rename to guide-review and unhook the writer

**Superseded 2026-08-23.** `banned-vocabulary` was replaced by `.claude/skills/house-style/SKILL.md`;
`guide-writer` and `create-guide` read it at write time, and the judge and the optimizer read it as
the rubric. The steps below are the earlier plan.

**Files:**
- Modify: `.claude/skills/banned-vocabulary/SKILL.md` renamed to `.claude/skills/guide-review/SKILL.md`
- Modify: `.claude/agents/guide-writer.md:32`, `:53`
- Modify: `.claude/agents/guide-judge.md`
- Modify: `.claude/skills/create-guide/SKILL.md:70`
- Modify: six memory files under `C:\Users\Sean\.claude\projects\G--source-FellowshipAnalyzer\memory\`

**Interfaces:**
- Produces: a rulebook the writer does not read, and a frozen `guide-writer.md`. No later task edits that file.

- [ ] **Step 1: Get the owner's confirmation**

`guide-writer.md:32` instructs reading the record end to end, and `create-guide/SKILL.md:70` says to
read it alongside the Left panel voice section. The owner committed the second by hand in `81184bb`.
Removing both is the design's intent and it reverses a fresh authorial decision, so quote both lines
and ask before editing. Do not proceed without an answer.

- [ ] **Step 2: Rename the skill directory**

```bash
git mv .claude/skills/banned-vocabulary .claude/skills/guide-review
```

Then change the `name:` field in the frontmatter from `banned-vocabulary` to `guide-review`, and
rewrite the `description:` so it says the record is read by the judge and the optimizer rather than
when writing prose.

- [ ] **Step 3: Update every referrer**

Run: `grep -rn "banned-vocabulary" .claude CLAUDE.md "C:/Users/Sean/.claude/projects/G--source-FellowshipAnalyzer/memory/"`

Expected before editing: ten lines across eight files. `guide-writer.md` 32 and 53,
`create-guide/SKILL.md` 70, and `feedback_guide_prose_banned_vocabulary.md`,
`feedback_hero_projects_zero_comments.md`, `feedback_never_mention_the_log.md`,
`feedback_optimality_claims_are_not_mechanics.md`, `MEMORY.md`, `project_guide_prose_style_source.md`.

`guide-writer.md:32` and `create-guide/SKILL.md:70` are removals, per step 1. The rest are renames.
Update the memory files in place; do not create new ones beside them.

- [ ] **Step 4: Point the judge at the new name**

`guide-judge.md` says `.claude/skills/banned-vocabulary/SKILL.md` twice. Change both.

- [ ] **Step 5: Verify nothing still refers to the old name**

Run: `grep -rn "banned-vocabulary" .claude CLAUDE.md "C:/Users/Sean/.claude/projects/G--source-FellowshipAnalyzer/memory/"`
Expected: only `.claude/guide-training/README.md` and `PLAN.md`, which record the rename as history.

- [ ] **Step 6: Commit**

```bash
git add -A .claude
git commit -m "Move the writing rulebook optimizer-side as guide-review"
```

---

### Task 7: guide-optimizer and the rejected-edit buffer

**Files:**
- Create: `.claude/agents/guide-optimizer.md`
- Create: `.claude/guide-training/rejected.md`

**Interfaces:**
- Consumes: rollout reports from `guide-writer`, section scores from Task 3, `rejected.md`.
- Produces: `proposals.json` with keys `reasoning` and `edits`, each edit having `op` (`append`, `insert_after`, `replace`, `delete`), `target`, `content`, `supportCount`, `sourceType` (`failure` or `success`), and `selectedIndex` naming the one edit to apply.

- [ ] **Step 1: Seed the buffer**

Create `.claude/guide-training/rejected.md`:

```markdown
# Rejected edits

One entry per edit the gate rejected, newest last. The optimizer reads this file before proposing
and does not repropose an entry here.

Format per entry: the run id, the op and its target, the content, and the gate delta it produced.

No entries yet.
```

- [ ] **Step 2: Write the optimizer agent**

Create `.claude/agents/guide-optimizer.md`:

```markdown
---
name: guide-optimizer
description: Proposes bounded edits to create-guide from scored guide-writer rollouts. Returns a ranked edit pool as JSON with one edit selected. Dispatch it with the rollout reports, their scores, and the rejected-edit buffer.
model: inherit
color: purple
tools: Read, Glob, Grep
---

You edit the writing procedure. You never edit a guide, and you never write guide prose.

## Read before you propose

1. `.claude/skills/create-guide/SKILL.md`. This is the document you are editing.
2. `.claude/skills/guide-review/SKILL.md`. This is the precedent record behind every count.
3. `.claude/guide-training/rejected.md`. Every entry here was tried and lowered the score.
4. Each rollout report and its section scores.

## Process

Split the rollouts into failures and successes by section score.

Read the failure rollouts as a group, not one at a time. A single rollout produces an anecdotal fix;
a group exposes the recurring procedural error. Propose corrective edits for what recurs.

Read the success rollouts as a group. Propose edits that preserve what worked, only for patterns
`create-guide` does not already state.

Where a failure edit and a success edit cover the same point, keep the failure edit.

Rank the pool by how many rollouts an edit addresses, then by generality, then by how concrete its
guidance is. An edit phrased as a general principle outranks one naming a specific ability, hero or
stat.

## Bounds

- Propose no edit whose content names a hero, an ability, a stat, or a file. The procedure has to
  generalise to the next hero.
- Propose no edit that targets the `## Longitudinal guidance` section. It is written only at a run
  boundary, by a separate process.
- Repropose nothing in `rejected.md`.
- Select exactly one edit.

## Report

Respond with one JSON object and nothing else.

    {
      "reasoning": "<why these edits address what recurred>",
      "edits": [
        {
          "op": "append",
          "target": "",
          "content": "<markdown>",
          "supportCount": 3,
          "sourceType": "failure"
        }
      ],
      "selectedIndex": 0
    }

`op` is one of `append`, `insert_after`, `replace`, `delete`. `target` is the exact existing text for
`insert_after`, `replace` and `delete`, and empty for `append`.

## Constraints you do not inherit

Your caller runs an output style you do not have. Write no em dashes or en dashes, and no comments in
any file, including `<!-- -->` in markdown.
```

- [ ] **Step 3: Add the protected heading to create-guide**

Append to `.claude/skills/create-guide/SKILL.md`:

```markdown
## Longitudinal guidance

Written only at a run boundary. Round edits never target this section.

No guidance yet.
```

- [ ] **Step 4: Verify the optimizer respects its bounds**

Dispatch `guide-optimizer` with the two draft-side rollouts from Task 5 and their scores. Check the
returned JSON: exactly one `selectedIndex`, no edit targeting `## Longitudinal guidance`, and no edit
whose `content` names Sylvie, Safe Haven, or a stat label. Reject and redispatch on a violation.

- [ ] **Step 5: Commit**

```bash
git add .claude/agents/guide-optimizer.md .claude/guide-training/rejected.md .claude/skills/create-guide/SKILL.md
git commit -m "Add guide-optimizer and the rejected-edit buffer"
```

---

### Task 8: The round runner

**Files:**
- Create: `.claude/skills/train-guide-skill/SKILL.md`

**Interfaces:**
- Consumes: every artifact from Tasks 1, 3, 4, 6 and 7.
- Produces: `.claude/guide-training/runs/<run-id>/` holding `rollouts/`, `scores.json`, `proposals.json` and `decision.md`, and at most one committed edit to `create-guide`.

- [ ] **Step 1: Write the skill**

Create `.claude/skills/train-guide-skill/SKILL.md`:

```markdown
---
name: train-guide-skill
description: Runs one training round over the blessed guide corpus. Use when the owner invokes /train-guide-skill. Dispatches guide-writer per propose fixture, scores with guide-judge, proposes one edit to create-guide with guide-optimizer, and keeps it only on a strictly greater gate score.
---

# One training round

Read `.claude/guide-training/README.md` first. Run id is the current date plus a two-digit sequence,
for example `2026-08-12-01`.

## 1. Rollout

For each fixture with `split: propose`:

    pwsh .claude/guide-training/tools/materialise.ps1 -Fixture <name> -Side before -OutDir .claude/guide-training/runs/<run-id>/rollouts/<name>

Dispatch `guide-writer` at the copy, never at the corpus and never at a live guide under `src/`.
Its dispatch needs the copied guide path, the copied analyzer path, and the fixture's directive
verbatim. Save its report beside the copy as `report.md`.

## 2. Score

Materialise the `after` side of every propose and gate fixture. Dispatch `guide-judge` once per
section. Write the objects to `scores.json` with `"skillVersion": "baseline"`.

Dispatch the judge once more over the approved prose of the gate fixtures, into `control.json` with
`"skillVersion": "control"`. Compare it to the previous round's control with `score.cs`. A total
delta beyond one count means the rounds are not comparable: stop, write `decision.md` saying so, and
apply no edit.

## 3. Propose

Dispatch `guide-optimizer` with the rollout reports, `scores.json`, and
`.claude/guide-training/rejected.md`. Save the returned JSON as `proposals.json`.

## 4. Gate

Apply the edit at `selectedIndex` to a copy of `create-guide/SKILL.md`. Rescore the **gate** fixtures
against that copy and write `candidate.json`.

    dotnet run .claude/guide-training/tools/score.cs -- .claude/guide-training/runs/<run-id>/scores.json .claude/guide-training/runs/<run-id>/candidate.json gate

On `ACCEPT`, write the edit to the real `create-guide/SKILL.md`.
On `REJECT`, discard the copy and append the edit to `rejected.md` with its gate delta.

`score.cs` compares with strict inequality, so a tie rejects. Do not override it.

## 5. Verify the protected section

    git diff .claude/skills/create-guide/SKILL.md

The `## Longitudinal guidance` section must be unchanged. Revert the edit and record it as rejected
if it is not.

## 6. Record

Write `decision.md` with the run id, the gate scores before and after, the accepted or rejected edit,
and the held-fixture scores. Commit the run directory and any accepted edit together.

## Bounds

- One accepted edit per round.
- Never dispatch `guide-writer` at a file under `src/`.
- Never edit `.claude/agents/guide-writer.md`. It is the harness.
```

- [ ] **Step 2: Run one round end to end**

Invoke the skill. Expected: a populated `runs/<run-id>/` directory, and either one edit in
`create-guide` or a new entry in `rejected.md`. Both outcomes are a pass. An empty `rejected.md`
alongside an unchanged `create-guide` is a failure, because the round decided nothing.

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/train-guide-skill .claude/guide-training/runs
git commit -m "Add the train-guide-skill round runner"
```

---

### Task 9: Longitudinal guidance at the run boundary

**Files:**
- Modify: `.claude/skills/train-guide-skill/SKILL.md`

**Interfaces:**
- Consumes: two or more completed rounds under `runs/`.
- Produces: a rewritten `## Longitudinal guidance` section in `create-guide`, accepted through the same gate.

- [ ] **Step 1: Add the run-boundary section to the skill**

Append to `.claude/skills/train-guide-skill/SKILL.md`:

```markdown
## Run boundary

After two or more rounds, rescore the propose fixtures under the run's first and last version of
`create-guide` and sort each section into improvements, regressions, and persistent failures.

Dispatch `guide-optimizer` with those three groups and the current `## Longitudinal guidance` text.
Ask it for a replacement section that prevents the regressions first, addresses the persistent
failures second, and reinforces what improved third, and that states nothing already in the body
above it.

Apply the replacement to a copy, rescore the gate fixtures, and keep it only on `ACCEPT`. The section
is rewritten whole, never patched, and only here.
```

- [ ] **Step 2: Run a second round and the boundary**

Invoke the skill twice more. Expected: `## Longitudinal guidance` either holds text accepted through
the gate, or still reads `No guidance yet.` with a rejection recorded. Both are a pass.

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/train-guide-skill .claude/guide-training/runs .claude/skills/create-guide/SKILL.md
git commit -m "Add longitudinal guidance at the run boundary"
```
