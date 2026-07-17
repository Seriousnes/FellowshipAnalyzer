---
name: style-guide
description: >
  FellowshipAnalyzer styling standard. Use when: adding CSS or SCSS to any component,
  creating a new Razor component with styles, migrating an existing .razor.css to SCSS,
  styling a new hero project, or reviewing existing component styles for consistency.
  Covers SCSS setup, design tokens, class naming, scoped vs global, and component patterns.
---

# FellowshipAnalyzer Style Guide

## Overview

All component styles use **SCSS** (`.razor.scss` for scoped, `app.scss` for global).
The build tool is `AspNetCore.SassCompiler` — no manual compilation steps needed.
Design tokens live in `_tokens.scss` and are mirrored to CSS custom properties at runtime.

---

## 1. File Conventions

| Concern | File | Location |
|---|---|---|
| Global tokens → CSS vars | `app.scss` | `FellowshipAnalyzer/wwwroot/` |
| SCSS token variables | `_tokens.scss` | `FellowshipAnalyzer.Core/Styles/` |
| SCSS mixins & helpers | `_mixins.scss` | `FellowshipAnalyzer.Core/Styles/` |
| Component styles | `ComponentName.razor.scss` | Beside its `.razor` file |
| Hero component styles | `ComponentName.razor.scss` | Beside its `.razor` in the hero project |

**Partials** (`_tokens.scss`, `_mixins.scss`) are never compiled directly — they are only `@use`d.  
**Component SCSS** (`.razor.scss`) is compiled by SassCompiler to a `.razor.css` that Blazor scopes automatically.

> **Rule: Always use `.razor.scss`, never `.razor.css`.**  
> If both exist for a component, the `.razor.scss` is the source of truth. Delete the `.razor.css` and keep only the `.razor.scss`.  
> When creating styles for a new component, always create a `.razor.scss` — never `.razor.css`.

> **Rule: Never edit a compiled `.razor.css` file when a `.razor.scss` exists.**  
> The `.razor.css` is generated output — edits will be overwritten on the next build. Always edit the `.razor.scss` source.

---

## 2. Importing Tokens and Mixins

All projects have `includePaths` configured in `sasscompiler.json` pointing to
`FellowshipAnalyzer.Core/Styles/`, so you can import without a path prefix:

```scss
// In any .razor.scss or app.scss
@use 'tokens' as t;
@use 'mixins' as mx;
```

Use `t.$fa-*` for token values in SCSS expressions.  
Use `var(--fa-*)` for runtime CSS custom properties in property values.

**When to use which:**
- SCSS function arguments: use `t.$fa-gold` (e.g. `color.adjust(t.$fa-gold, $lightness: -10%)`)
- Standard property values: use `var(--fa-gold)` (survives runtime overrides / DevTools)
- Fallback in scoped CSS: `var(--fa-gold, #{t.$fa-gold})`

---

## 3. Class Naming

Use **Atomic Design** to organize components, with **flat hyphenated** class names — no BEM ever.

| Level | Examples |
|---|---|
| Atom | `.spell-icon`, `.perf-dot`, `.stat-value` |
| Molecule | `.stat-card`, `.stat-card-header`, `.spell-badge` |
| Organism | `.guide-section`, `.guide-section-header`, `.timeline` |

```scss
// Good ✓ — flat hyphenated at every level
.guide-section { }
.guide-section-header { }
.guide-section-body { }
.guide-section-data { }
.timeline-label { }
.timeline-label-name { }          // ✓ flat, not .timeline-label__name
.timeline-label-name-section { }  // ✓ flat, not .timeline-label__name--section

// Never ✗ — BEM
.guide-section__header--active { }
.timeline-label__name--section { }
```

Rules:
- Identify the atomic level (atom/molecule/organism) before naming
- Start with the component name: `.stat-card`, `.cast-overview`, `.spell-badge`
- Append meaningful part names with hyphens: `-header`, `-body`, `-title`, `-row`, `-icon`
- State via compound class alongside root: `.perf-box.active`, `.spell-badge.disabled`
- Use performance tier names as modifiers: `.perfect`, `.good`, `.ok`, `.fail`
- Keep names lowercase and hyphenated — no camelCase, no underscores, no BEM `__` or `--`

---

## 4. Scoping Rules

| Scope | Rule |
|---|---|
| **Scoped** (`.razor.scss`) | Default for all component-specific styles. Blazor auto-adds a scope attribute. |
| **Global** (`app.scss`) | Layout primitives, resets, typography, utility classes (`.eyebrow`), Blazor defaults. |
| **Hero projects** | Each hero's `.razor.scss` files are scoped to that hero's components. Reuse shared mixins/tokens but define hero-specific accent overrides locally. |

Do **not** put component-specific styles in `app.scss`.  
Do **not** use `:global()` or `::deep` unless absolutely necessary for third-party component overrides.

---

## 5. Using Tokens

See [./references/tokens.md](./references/tokens.md) for the full token reference.

> **Rule: `_tokens.scss` is the only file that may contain a colour literal.**
> No `rgb()`, no `rgba()`, no bare hex in any other `.scss`. Every colour is a token reference.

Alpha shades are tokens too, named `$fa-{base}-a{alpha×100}` and derived from their base tint
with `color.change` **in `_tokens.scss`**:

```scss
// _tokens.scss — the only place literals and color.change appear
$fa-white:     #ffffff;
$fa-gold:      #d4a744;
$fa-white-a08: color.change($fa-white, $alpha: 0.08);
$fa-gold-a12:  color.change($fa-gold,  $alpha: 0.12);

// Any component — token references only
.thing {
    background: t.$fa-white-a08;   // ✓
    border: 1px solid t.$fa-gold-a12;

    background: rgba(255, 255, 255, 0.08);            // ✗ banned
    border: 1px solid color.change(t.$fa-gold, $alpha: 0.12);  // ✗ belongs in _tokens.scss
}
```

Need a shade with no token? Add it to `_tokens.scss`, then reference it. Never inline it "just once".

**Custom properties need interpolation** — Sass does not evaluate `--*` values, so a bare token is
emitted as literal text and yields invalid CSS:

```scss
--badge-border: t.$fa-gold-a12;      // ✗ emits the string "t.$fa-gold-a12"
--badge-border: #{t.$fa-gold-a12};   // ✓
```

Quick cheatsheet:

```scss
@use 'tokens' as t;

.my-card {
    background: var(--fa-bg-card);           // runtime CSS var
    border: 1px solid var(--fa-border-card);
    border-radius: t.$fa-radius-md;          // SCSS compile-time (border-radius: 14px)
    box-shadow: t.$fa-shadow-card;
    color: var(--fa-text);
}

.my-heading {
    color: var(--fa-gold);
    font-family: var(--fa-font-heading);
}

.my-perf-perfect { background: var(--fa-perf-perfect); }
```

---

## 6. Using Mixins

See [./references/patterns.md](./references/patterns.md) for usage examples.

```scss
@use 'mixins' as mx;

.my-card {
    @include mx.card-surface;          // bg, border, radius, shadow
}

.my-panel {
    @include mx.panel-surface($radius: t.$fa-radius-sm);
}

.my-label {
    @include mx.eyebrow;               // all-caps, gold, letter-spaced
}

// Performance tier colors
.perf-box {
    @include mx.perf-tier-colors;     // emits .perfect, .good, .ok, .fail sub-classes
}

// Breakpoints
.my-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);

    @include mx.mobile {
        grid-template-columns: 1fr;
    }
}
```

---

## 7. SCSS Nesting

Use nesting to group related selectors — but keep it **shallow** (max 3 levels).

```scss
// Good ✓ — nesting reduces repetition, stays readable
.guide-section-explanation {
    color: var(--fa-text-muted);

    ol, ul {
        margin: 0.5em 0;
        padding-left: 1.25em;
    }

    li { margin-bottom: 0.35em; }
}

// Good ✓ — & for pseudo-classes and state modifiers
.brand {
    color: var(--fa-text);

    &:hover { color: var(--fa-gold); }
}

// Good ✓ — & for element modifier classes (produces .brand-icon as a sibling class, not nested)
// Note: only use &- suffix when it produces a flat, readable class name
.brand-icon { color: var(--fa-gold); }  // Prefer this explicit flat form

// Avoid ✗ — too many levels, hard to read the resulting selector
.timeline {
    .timeline-row {
        .timeline-label {
            .timeline-label-name { }  // 4 levels deep — write as flat classes instead
        }
    }
}
```

---

## 8. Hero-Specific Accents

Each hero defines its own accent token. For Rime, this is `--fa-ice` (`#6ec8e8`).  
Future heroes should declare their accent in their project's `.razor.scss` or a hero-scoped `_hero-tokens.scss`:

```scss
// _rime-tokens.scss (if needed for SCSS function use)
$rime-ice: #6ec8e8;
$rime-deep: #1a3a52;

// In component:
.rime-orb-bar { background: var(--fa-ice); }
```

Do not use hero-specific tokens in shared `FellowshipAnalyzer.Components` styles.

---

## 9. Common Pitfalls

- **Editing compiled output** — never edit `Component.razor.css` when `Component.razor.scss` exists. The `.razor.css` is generated on build and will overwrite your changes. Edit the `.razor.scss` instead.
- **Creating `.razor.css` files** — always create `.razor.scss`. Never create a new `.razor.css` — the compiler generates that file.
- **Hardcoded colors** — always use `var(--fa-*)` or `t.$fa-*`. Never `#d4a744` inline.
- **`rgb()` / `rgba()` anywhere outside `_tokens.scss`** — banned. Use the matching alpha token (`t.$fa-white-a08`, `t.$fa-gold-a12`, `t.$fa-black-a45`). If the shade you need has no token, add one to `_tokens.scss` — do not inline it.
- **`color.change()` at a call site** — that belongs on the token definition in `_tokens.scss`, not in a component. A component references a token by name and nothing else.
- **A token in a CSS custom property without interpolation** — `--x: t.$fa-gold;` emits the literal text `t.$fa-gold` and produces invalid CSS, because Sass does not evaluate custom-property values. Write `--x: #{t.$fa-gold};`.
- **Inline `style` for presentation** — only use inline `style` for dynamic values (e.g. performance bar widths from C# variables). Static colors/sizes belong in SCSS.
- **Adding to app.scss for one component** — keep it in the component's `.razor.scss`.
- **Duplicating token values** — never copy-paste hex colors; import the token.

---

## 10. Adding Styles to a New Component

1. Create `MyComponent.razor.scss` beside `MyComponent.razor`
2. Add `@use 'tokens' as t;` (and `@use 'mixins' as mx;` if needed) at the top
3. Use flat component-prefixed class names
4. Use `var(--fa-*)` for property values; `t.$fa-*` in SCSS expressions
5. Use `@include mx.mobile` / `@include mx.mobile-sm` for breakpoints
6. Never add a global class to `app.scss` — use scoped selectors

---

## References

- [Design Tokens](./references/tokens.md) — All `--fa-*` / `$fa-*` values with descriptions
- [Class Naming](./references/naming.md) — Naming rules, examples, and anti-patterns
- [Patterns](./references/patterns.md) — Reusable component patterns (card, guide section, perf badge, timeline)
