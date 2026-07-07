# Guide UI — Design System Report

Framework-agnostic catalog of the components used in the WoWAnalyzer **Guide** UI
(the per-spec performance analysis pages under `src/interface/guide/`).

The report is split into three documents:

| File | What's in it |
|---|---|
| [`01-catalog.md`](./01-catalog.md) | Component catalog — purpose, visual structure, when-to-use, composition |
| [`02-data-contracts.md`](./02-data-contracts.md) | Data-shape appendix — every TypeScript interface a port needs to mirror, with notes |
| [`03-visual-mockups.md`](./03-visual-mockups.md) | Inline-SVG mockups of every major component, using real design-token colors |

Both documents are intentionally framework-neutral. Component descriptions are written
so they could be re-implemented in React / Angular / Vue / Svelte / Blazor / vanilla.

## Source of truth
All page references point into the live source tree:

- `src/interface/guide/` — the catalog itself
- `src/parser/core/MajorCooldowns/` — cooldown analyzer base + UI wrapper
- `src/parser/core/SpellUsage/` — checklist-based cooldown UI
- `src/analysis/retail/**/Guide.tsx` — real spec compositions
