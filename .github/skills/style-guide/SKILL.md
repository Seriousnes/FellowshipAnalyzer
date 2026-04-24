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
| Global tokens → CSS vars | `app.scss` | `FellowshipAnalyzer.Client/wwwroot/` |
| SCSS token variables | `_tokens.scss` | `FellowshipAnalyzer.Components/Styles/` |
| SCSS mixins & helpers | `_mixins.scss` | `FellowshipAnalyzer.Components/Styles/` |
| Component styles | `ComponentName.razor.scss` | Beside its `.razor` file |
| Hero component styles | `ComponentName.razor.scss` | Beside its `.razor` in the hero project |

**Partials** (`_tokens.scss`, `_mixins.scss`) are never compiled directly — they are only `@use`d.  
**Component SCSS** (`.razor.scss`) is compiled by SassCompiler to a `.razor.css` that Blazor scopes automatically.

---

## 2. Importing Tokens and Mixins

All projects have `includePaths` configured in `sasscompiler.json` pointing to
`FellowshipAnalyzer.Components/Styles/`, so you can import without a path prefix:

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

Use **flat component-prefixed names** — readable, non-verbose.

```scss
// Good ✓
.guide-section { }
.guide-section-header { }
.guide-section-body { }
.guide-section-data { }

// Avoid: strict BEM ✗ (too noisy for small components)
.guide-section__header--active { }

// BEM-style double-underscore is acceptable for sub-elements with no simpler alternative
.timeline-label__name { }
.timeline-label__name--section { }
```

Rules:
- Start with the component name: `.stat-card`, `.cast-overview`, `.spell-badge`
- Append a meaningful part name: `-header`, `-body`, `-title`, `-row`, `-icon`
- Append state/modifier with single dash: `-active`, `-disabled`, `-clickable`
- Use performance tier names as modifiers: `.perfect`, `.good`, `.ok`, `.fail`
- Keep names lowercase and hyphenated — no camelCase, no underscores

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

// Good ✓ — & for modifiers and pseudo-classes
.brand {
    color: var(--fa-text);

    &:hover { color: var(--fa-gold); }

    &-icon { color: var(--fa-gold); }  // Produces .brand-icon
}

// Avoid ✗ — too many levels, hard to read the resulting selector
.timeline {
    .timeline-row {
        .timeline-label {
            .timeline-label__name { }  // 4 levels deep
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

- **Hardcoded colors** — always use `var(--fa-*)` or `t.$fa-*`. Never `#d4a744` inline.
- **Hardcoded `rgba(255,255,255,x)` in component files** — use `rgba(t.$fa-text, x)` or a token.
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
