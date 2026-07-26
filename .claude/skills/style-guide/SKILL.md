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
The build tool is `AspNetCore.SassCompiler`, so there are no manual compilation steps.

Design token *values* live in C#, under `src/FellowshipAnalyzer.Core.Contracts/Design/`.
`FaTheme` owns three themes (Original, Dark, Light) and every token resolves in all three.
An emitter renders them into the generated partial `FellowshipAnalyzer.Core/Styles/_palette.scss`,
which declares every token as a CSS custom property and mints the semantic classes.
Stylesheets consume tokens as `var(--fa-*)`.

---

## 1. File Conventions

| Concern | File | Location |
|---|---|---|
| Token values (source of truth) | `FaPalette` / `FaTypography` / `FaMetrics` / `FaElevation` | `FellowshipAnalyzer.Core.Contracts/Design/` |
| Generated CSS custom properties + semantic classes | `_palette.scss` | `FellowshipAnalyzer.Core/Styles/` |
| Tint scale and the `tint()` function | `_tokens.scss` | `FellowshipAnalyzer.Core/Styles/` |
| SCSS mixins, helpers and breakpoints | `_mixins.scss` | `FellowshipAnalyzer.Core/Styles/` |
| App global styles | `app.scss` | each app's `wwwroot/` |
| Component styles | `ComponentName.razor.scss` | Beside its `.razor` file |
| Hero component styles | `ComponentName.razor.scss` | Beside its `.razor` in the hero project |

**Partials** (`_palette.scss`, `_tokens.scss`, `_mixins.scss`) are never compiled directly, only `@use`d.
**Component SCSS** (`.razor.scss`) is compiled by SassCompiler to a `.razor.css` that Blazor scopes automatically.

Each app's `app.scss` starts with `@use 'palette';`. That single line is what puts the custom
properties and the semantic classes on the page, so an app that renders shared components must have it.

> **Rule: `_palette.scss` is generated. Never hand-edit it.**
> Change the value in the C# theme and rerun the emitter (see section 5).

> **Rule: Always use `.razor.scss`, never `.razor.css`.**
> If both exist for a component, the `.razor.scss` is the source of truth. Delete the `.razor.css` and keep only the `.razor.scss`.
> When creating styles for a new component, always create a `.razor.scss`.

> **Rule: Never edit a compiled `.razor.css` file when a `.razor.scss` exists.**
> The `.razor.css` is generated output and edits will be overwritten on the next build. Always edit the `.razor.scss` source.

---

## 2. Importing Tokens and Mixins

All projects have `IncludePaths` configured in `sasscompiler.json` pointing to
`FellowshipAnalyzer.Core/Styles/`, so you can import without a path prefix:

```scss
// In any .razor.scss or app.scss
@use 'tokens' as t;
@use 'mixins' as mx;
```

Import `tokens` only when you need `t.tint()` or a `t.$fa-tint-*` step; import `mixins` only when
you include one. A component that just reads tokens needs neither import, because `var(--fa-*)`
resolves without one.

Every token is a `var(--fa-*)` reference in a property value:

```scss
.thing {
    color: var(--fa-text);
    background: var(--fa-bg-card);
    border: var(--fa-border-width) solid var(--fa-border-card);
    border-radius: var(--fa-radius-md);
    box-shadow: var(--fa-shadow-card);
    font-family: var(--fa-font-body);
    font-size: var(--fa-fs-body);
}
```

> **Rule: never write a fallback.**
> Every token is declared for every theme, so `var(--fa-gold, #b8965c)` can only go stale.
> Write `var(--fa-gold)`.

---

## 3. Class Naming

Use **Atomic Design** to organize components, with **flat hyphenated** class names, no BEM ever.

| Level | Examples |
|---|---|
| Atom | `.spell-icon`, `.perf-dot`, `.stat-value` |
| Molecule | `.stat-card`, `.stat-card-header`, `.spell-badge` |
| Organism | `.guide-section`, `.guide-section-header`, `.timeline` |

```scss
// Good ✓ - flat hyphenated at every level
.guide-section { }
.guide-section-header { }
.guide-section-body { }
.guide-section-data { }
.timeline-label { }
.timeline-label-name { }          // ✓ flat, not .timeline-label__name
.timeline-label-name-section { }  // ✓ flat, not .timeline-label__name--section

// Never ✗ - BEM
.guide-section__header--active { }
.timeline-label__name--section { }
```

Rules:
- Identify the atomic level (atom/molecule/organism) before naming
- Start with the component name: `.stat-card`, `.cast-overview`, `.spell-badge`
- Append meaningful part names with hyphens: `-header`, `-body`, `-title`, `-row`, `-icon`
- State via compound class alongside root: `.perf-box.active`, `.spell-badge.disabled`
- Use the semantic class names as tier modifiers: `.perfect`, `.good`, `.ok`, `.fail`
- Keep names lowercase and hyphenated, no camelCase, no underscores, no BEM `__` element syntax
- A single `--variant` suffix survives on the existing `Badge`, `Avatar`, and `SupportBadge` primitives (`.support-badge--full`, `.badge--solid`); tolerate it there, but name new components and their parts with flat hyphenated classes

---

## 4. Scoping Rules

| Scope | Rule |
|---|---|
| **Scoped** (`.razor.scss`) | Default for all component-specific styles. Blazor auto-adds a scope attribute. |
| **Global** (`app.scss`) | Layout primitives, resets, typography, utility classes (`.eyebrow`), Blazor defaults. |
| **Hero projects** | Each hero's `.razor.scss` files are scoped to that hero's components. Reuse the shared mixins and tokens. |

Do **not** put component-specific styles in `app.scss`.
Do **not** use `:global()` or `::deep` unless the rule genuinely has to reach inside a child or
third-party component, as `SpellSequence` does to recolour the `SpellIcon` ring it wraps.

Scoped selectors carry the Blazor scope attribute, so `.support-badge--full[b-abc123]` is (0,2,0)
and beats a global `.good` at (0,1,0). If an element is meant to take its colour from a semantic
class, nothing scoped may declare that property on it.

---

## 5. Using Tokens

See [./references/tokens.md](./references/tokens.md) for the full token reference.

> **Rule: the C# theme is the only place a colour value is written.**
> No `rgb()`, no `rgba()`, no `hsl()`, no bare hex and no named colour in any `.scss`, `.cs` or
> `.razor` file. Every colour is a token reference.

> **Rule: one token per colour. A component never invents a variant.**
> `FaPalette` declares the variants the design system has (`--fa-gold`, `--fa-gold-light`,
> `--fa-gold-pale`, `--fa-gold-dim`, and so on). A lighter, darker or faded version of a colour
> that the palette does not declare is *that same token at a tint step*, never a new token.

> **Rule: a component must not declare its own colour custom property.**
> `--badge-accent: var(--fa-gold);` inside a component is a private token, and a private token
> drifts. Apply a semantic class, or reference the token directly.
> The one caller-facing channel is `--fa-supplied-accent`, which `Badge` uses when the caller
> (not a tier) chooses the colour; it always carries a `var(--fa-*)` reference.

Transparency comes only from the **tint scale**, via `tint()`:

```scss
$fa-tint-faint:  6%;    $fa-tint-muted:  30%;
$fa-tint-subtle: 12%;   $fa-tint-medium: 40%;
$fa-tint-soft:   20%;   $fa-tint-strong: 60%;
                        $fa-tint-veil:   80%;

.thing {
    border: var(--fa-border-width) solid t.tint(var(--fa-gold), t.$fa-tint-subtle);   // ✓
    background: t.tint(var(--fa-white), t.$fa-tint-faint);                            // ✓

    background: rgba(255, 255, 255, 0.08);                    // ✗ banned
    background: t.tint(var(--fa-white), 8%);                  // ✗ off-scale, use the nearest step
    background: var(--fa-white-a08);                          // ✗ no such token, and never add one
}
```

`tint()` takes the token reference and returns a live `color-mix()`:

```scss
t.tint(var(--fa-gold), t.$fa-tint-faint)
// color-mix(in srgb, var(--fa-gold) 6%, transparent)
```

The step is baked at compile time, the colour is not, so a runtime theme change reaches the tint.
Pass the `var()` reference, never a literal.

Need a tint that is not on the scale? Use the nearest step. The scale is the design, so an off-scale
value is drift. Need a colour that has no token? Add **one** base colour to `FaPalette` and every
theme in `FaTheme` (never a variant of an existing one), rerun the emitter, then reference it.

### Adding or changing a token

1. Add or edit the property on the relevant design record (`FaPalette`, `FaTypography`, `FaMetrics`, `FaElevation`)
2. Give it a value in every theme in `FaTheme`, and list it in the matching group in `FaTheme.Groups`
3. Add an `FaVar` member if C# needs to name it in markup
4. Regenerate the stylesheet from `src/FellowshipAnalyzer.Tools`:

```powershell
dotnet run --no-cache emit-palette.cs "../FellowshipAnalyzer.Core/Styles/_palette.scss"
```

5. Document it in [./references/tokens.md](./references/tokens.md)

A drift test fails the build if the committed `_palette.scss` does not match the C# theme, so
forgetting step 4 is caught, not shipped.

### Naming a token from C#

`FaVar` holds one member per token that C# hands to markup, each derived from the palette property
that defines it, so a rename breaks the build instead of emitting a dead custom property:

```csharp
private static readonly string SelectedRing = FaVar.White;   // "var(--fa-white)"
```

Use it for inline `style` attributes that carry a dynamic colour (`PerformanceColors`,
`HeroRoleStyles`). Static colours belong in SCSS.

---

## 6. Semantic Classes

`_palette.scss` mints three global classes for each of the nineteen semantic names: the four
performance tiers (`perfect`, `good`, `ok`, `fail`), the three hero roles (`tank`, `healer`, `dps`)
and the twelve event types (`cast`, `damage`, `heal`, `buff`, `buff-fade`, `debuff`,
`death`, `resource`, `system`, `modified`, `fabricated`, `reordered`).

`--fa-role-unknown` is a token but has no semantic class: it is a low-alpha structural grey that
would render text near-invisible, so use it as an accent edge via `var(--fa-role-unknown)` rather
than expecting an `.unknown` class.

| Form | Declares | Use for |
|---|---|---|
| `.good` | `color` | Text, icons and anything reading `currentColor` |
| `.good-bordered` | `border-color` | An edge or accent stripe in the tier colour |
| `.good-filled` | `background` | A solid swatch, bar segment or box |

Apply the class alongside the component's own class:

```razor
<span class="support-badge support-badge--full good">Full support</span>
<div class="bar-segment perfect-filled" style="width: @pct%"></div>
<button class="perf-box @FillClass(entry.Performance)"></button>

@code {
    private static string FillClass(QualitativePerformance tier) => tier switch
    {
        QualitativePerformance.Perfect => "perfect-filled",
        QualitativePerformance.Good => "good-filled",
        QualitativePerformance.Ok => "ok-filled",
        QualitativePerformance.Fail => "fail-filled",
        _ => "",
    };
}
```

Real examples: `PerformanceBoxRow.razor`, `GradiatedPerformanceBar.razor`, `SupportBadge.razor`.

This is how a component takes a performance tier, hero role or event-type colour. Do not restate
the token in the component's own SCSS: the class already sets it, and a scoped rule for the same
property wins on specificity and silently overrides the tier.

Because `.good` sets `color`, `currentColor` carries the tier into fills and edges derived from it:

```scss
.support-badge {
    background: color-mix(in srgb, currentColor 12%, transparent);
}
```

---

## 7. Using Mixins

See [./references/patterns.md](./references/patterns.md) for usage examples.

```scss
@use 'mixins' as mx;

.my-card {
    @include mx.card-surface;          // bg, border, radius, shadow
}

.my-panel {
    @include mx.panel-surface($radius: var(--fa-radius-sm));
}

.my-inset {
    @include mx.inset-surface;
}

.my-label {
    @include mx.eyebrow;               // all-caps, gold, letter-spaced
}

.page-title {
    @include mx.gradient-heading;      // gold gradient text fill
}

// Breakpoints
.my-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);

    @include mx.mobile {               // max-width: 768px
        grid-template-columns: 1fr;
    }

    @include mx.mobile-sm {            // max-width: 600px
        gap: 0.25rem;
    }
}
```

---

## 8. SCSS Nesting

Use nesting to group related selectors, but keep it **shallow** (max 3 levels).

```scss
// Good ✓ - nesting reduces repetition, stays readable
.guide-section-explanation {
    color: var(--fa-text-muted);

    ol, ul {
        margin: 0.5em 0;
        padding-left: 1.25em;
    }

    li { margin-bottom: 0.35em; }
}

// Good ✓ - & for pseudo-classes and state modifiers
.brand {
    color: var(--fa-text);

    &:hover { color: var(--fa-gold); }
}

// Good ✓ - & for element modifier classes (produces .brand-icon as a sibling class, not nested)
// Note: only use &- suffix when it produces a flat, readable class name
.brand-icon { color: var(--fa-gold); }  // Prefer this explicit flat form

// Avoid ✗ - too many levels, hard to read the resulting selector
.timeline {
    .timeline-row {
        .timeline-label {
            .timeline-label-name { }  // 4 levels deep - write as flat classes instead
        }
    }
}
```

---

## 9. Hero Accents

Every hero has an identity colour in the palette: `--fa-hero-rime`, `--fa-hero-ardeos`,
`--fa-hero-aeona`, and so on, plus `--fa-hero-unknown`. Rime's frost accent is `--fa-ice`.

```scss
.rime-orb-bar { background: var(--fa-ice); }
```

A hero project references those tokens directly; it never declares a colour of its own.
Adding a hero means adding its `--fa-hero-*` property to `FaPalette`, giving it a value in every
theme, and regenerating (section 5).

Do not use a hero-specific token in shared `FellowshipAnalyzer.Core` styles.

---

## 10. Common Pitfalls

- **Editing generated output**: `_palette.scss` is written by the emitter, and `Component.razor.css` is written by SassCompiler. Edit the C# theme or the `.razor.scss` instead.
- **Creating `.razor.css` files**: always create `.razor.scss`. Never create a new `.razor.css`, the compiler generates that file.
- **A colour literal anywhere**: hex, `rgb()`, `rgba()`, `hsl()` or a named colour in `.scss`, `.cs` or `.razor`. The values live in `FaPalette`; everywhere else references `var(--fa-*)`.
- **A `var()` fallback**: `var(--fa-gold, #b8965c)`. Every token is always declared, so the fallback is dead code that can only go stale. Write `var(--fa-gold)`.
- **Passing a literal to `tint()`**: `t.tint(#b8965c, t.$fa-tint-soft)` bakes a colour that no longer tracks the theme. Pass `var(--fa-gold)`.
- **An off-scale tint**: `t.tint(var(--fa-white), 7%)`. Snap to the nearest scale step.
- **Inventing a colour variant**: a private `--badge-accent`, or a `$fa-gold-a12`. One token per colour, and the palette owns the variants that exist.
- **Restating a semantic class in a component**: `color: var(--fa-perf-good)` on an element that already carries `.good`. The scoped rule wins and the class becomes decoration.
- **Inline `style` for presentation**: only use inline `style` for dynamic values (performance bar widths, a colour chosen at runtime via `FaVar`). Static colours and sizes belong in SCSS.
- **Adding to app.scss for one component**: keep it in the component's `.razor.scss`.
- **Duplicating token values**: never copy a value out of `_palette.scss`; reference the token.

---

## 11. Adding Styles to a New Component

1. Create `MyComponent.razor.scss` beside `MyComponent.razor`
2. Add `@use 'tokens' as t;` if you need `tint()`, and `@use 'mixins' as mx;` if you include a mixin
3. Use flat component-prefixed class names
4. Use `var(--fa-*)` for every token, with no fallback
5. Apply a semantic class for a performance tier, hero role or event-type colour
6. Use `@include mx.mobile` / `@include mx.mobile-sm` for breakpoints
7. Never add a global class to `app.scss`, use scoped selectors

---

## References

- [Design Tokens](./references/tokens.md): every `--fa-*` token, its group and its role
- [Class Naming](./references/naming.md): naming rules, examples, and anti-patterns
- [Patterns](./references/patterns.md): reusable component patterns (card, guide section, perf badge, timeline)
