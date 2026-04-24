# Class Naming Reference

## Convention: Flat Component-Prefixed

Class names are **lowercase**, **hyphenated**, starting with the component name.

```
{component}-{part}[-{modifier}]
```

---

## Good Examples

```scss
// Component root
.stat-card { }

// Parts (append meaningful name)
.stat-card-header { }
.stat-card-title { }
.stat-card-body { }

// State modifiers (single dash)
.perf-box { }
.perf-box.active { }
.perf-box.clickable { }

// Performance tier modifiers (no prefix needed — they're conventional)
.perf-box.perfect { }
.perf-box.good { }
.perf-box.ok { }
.perf-box.fail { }

// BEM-style double-underscore is allowed for sub-elements inside complex components
.timeline-label__name { }
.timeline-label__name--section { }
```

---

## Rules

| Rule | Example |
|---|---|
| Start with component name | `.guide-section`, `.cast-overview` |
| Hyphenate parts | `.guide-section-header`, `.guide-section-body` |
| Single dash for state | `.spell-badge.disabled`, `.cast-card.expanded` |
| No camelCase | `.statCard` ✗ → `.stat-card` ✓ |
| No underscores (except BEM `__`) | `.stat_card` ✗ → `.stat-card` ✓ |
| Performance tiers: short names | `.perfect`, `.good`, `.ok`, `.fail` |
| Sizes: sm/md/lg suffix | `.badge-sm`, `.badge-md`, `.badge-lg` |
| Global utilities: keep in app.scss | `.eyebrow` |

---

## Anti-patterns

```scss
// ✗ Too deeply nested / verbose BEM
.guide-section__body__explanation__list { }

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

- Component classes (`.stat-card`, `.guide-section`) live in `.razor.scss` and are **automatically scoped** by Blazor — no collision risk between components.
- Utility classes (`.eyebrow`) live in `app.scss` and are **global** — keep them minimal and universal.
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

## Performance Tier Classes

The five performance tier modifier classes are a shared convention used across all components:

| Class | Color | C# Enum |
|---|---|---|
| `.perfect` | `--fa-perf-perfect` (#56d67b) | `PerformanceTier.Perfect` |
| `.good` | `--fa-perf-good` (#90cc60) | `PerformanceTier.Good` |
| `.ok` | `--fa-perf-ok` (#d4a744) | `PerformanceTier.Ok` |
| `.fail` | `--fa-perf-fail` (#d4564a) | `PerformanceTier.Fail` |

These are always applied as modifiers alongside a base class, never standalone.
Use `@include mx.perf-tier-colors` inside the base class selector to emit all four at once.
