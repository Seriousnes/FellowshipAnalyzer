# Class Naming Reference

## Convention: Atomic Design + Flat Hyphenated

FellowshipAnalyzer uses **Atomic Design** to organize components by complexity.
Class names are **lowercase** and **hyphenated** — always flat, never BEM.

```
{atom}               — smallest self-contained element
{molecule}-{part}    — group of atoms, sub-parts named descriptively
{organism}-{part}    — group of molecules, larger layout sections
```

---

## Atomic Levels

| Level | Description | Examples |
|---|---|---|
| **Atom** | Smallest, indivisible UI unit | `.spell-icon`, `.perf-dot`, `.stat-value`, `.eyebrow` |
| **Molecule** | Composed of atoms, a distinct UI concept | `.stat-card`, `.spell-badge`, `.perf-box`, `.cast-row` |
| **Organism** | Composed of molecules, a major UI region | `.guide-section`, `.cast-overview`, `.timeline` |
| **Global** | Layout / page-level | `app.scss` only — resets, typography, Blazor defaults |

Blazor `.razor.scss` scoping means atoms and molecules can share short names without collision. Use the atomic level to guide *how* you name, not as a CSS class prefix.

---

## Good Examples

```scss
// Atom — single element, no sub-parts needed
.spell-icon { }
.perf-dot { }

// Molecule — root + flat named parts
.stat-card { }
.stat-card-header { }
.stat-card-title { }
.stat-card-body { }

// State modifiers — compound class, always alongside root
.perf-box { }
.perf-box.active { }
.perf-box.clickable { }

// Organism — flat named parts, no nesting in class names
.guide-section { }
.guide-section-header { }
.guide-section-body { }
.guide-section-explanation { }
.guide-section-data { }

// Timeline atom parts — stay flat, no BEM
.timeline-label { }
.timeline-label-name { }      // ✓  (not .timeline-label__name)
.timeline-label-name-section { }  // ✓  (not .timeline-label__name--section)
```

---

## Rules

| Rule | Example |
|---|---|
| Always flat hyphenated | `.guide-section-header` ✓ not `.guide-section__header` ✗ |
| No BEM (`__` or `--`) ever | `.timeline-label-name` ✓ not `.timeline-label__name` ✗ |
| Start with the atom/molecule/organism name | `.guide-section`, `.cast-overview` |
| Append meaningful part names | `.guide-section-header`, `.guide-section-body` |
| State via compound class (no dash) | `.spell-badge.disabled`, `.cast-card.expanded` |
| No camelCase | `.statCard` ✗ → `.stat-card` ✓ |
| No underscores | `.stat_card` ✗ → `.stat-card` ✓ |
| Performance tiers: short names | `.perfect`, `.good`, `.ok`, `.fail` |
| Sizes: sm/md/lg suffix | `.badge-sm`, `.badge-md`, `.badge-lg` |
| Global utilities: keep in app.scss | `.eyebrow` |

---

## Anti-patterns

```scss
// ✗ BEM double-underscore
.guide-section__body { }
.timeline-label__name { }

// ✗ BEM modifier double-dash
.guide-section__header--active { }

// ✗ Abbreviations that aren't obvious
.gs-hdr { }

// ✗ Generic names without component prefix
.header { }
.body { }
.title { }

// ✗ Mixed casing
.guideSection { }
.Guide-Section { }
```

---

## Scoped vs Global

- Component classes (`.stat-card`, `.guide-section`) live in `.razor.scss` and are **automatically scoped** by Blazor, so there is no collision risk between components.
- Semantic classes (`.good`, `.dps-bordered`, `.cast-filled`) are **global**, generated into `_palette.scss` from `FaSemantic`. Apply them from markup; never redeclare one.
- Utility classes (`.eyebrow`) live in `app.scss` and are **global**, so keep them minimal and universal.
- Do not add component-specific classes to `app.scss`.

---

## Size Modifier Naming

When a component has size variants, use:

| Class | Usage |
|---|---|
| `.badge-sm` | Small (e.g. 0.75rem font) |
| `.badge-md` | Default medium (0.85rem) |
| `.badge-lg` | Large (1rem) |

The size class is applied alongside the component root class:
```html
<span class="spell-badge badge-sm">...</span>
```

---

## Semantic Classes

`_palette.scss` mints a global class per semantic name, in three forms: the bare class sets
`color`, `-bordered` sets `border-color`, `-filled` sets `background`.

The four performance tiers:

| Class | Token | C# enum |
|---|---|---|
| `.perfect` | `--fa-perf-perfect` | `QualitativePerformance.Perfect` |
| `.good` | `--fa-perf-good` | `QualitativePerformance.Good` |
| `.ok` | `--fa-perf-ok` | `QualitativePerformance.Ok` |
| `.fail` | `--fa-perf-fail` | `QualitativePerformance.Fail` |

The four hero roles are `.tank`, `.healer`, `.dps` and `.unknown`; the twelve event types are
`.cast`, `.damage`, `.heal`, `.buff`, `.buff-fade`, `.debuff`, `.death`, `.resource`, `.system`,
`.modified`, `.fabricated` and `.reordered`.

These are always applied as modifiers alongside a base class, never standalone:

```html
<div class="bar-segment perfect-filled"></div>
<span class="support-badge support-badge--full good">Full support</span>
```

The component's own SCSS leaves the class's property alone. A scoped rule carries the Blazor scope
attribute and so outranks the global class, which turns the tier into decoration.
