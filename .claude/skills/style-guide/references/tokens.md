# Design Tokens Reference

All tokens are declared in `src/FellowshipAnalyzer.Components/Styles/_tokens.scss`
and emitted as CSS custom properties in `src/FellowshipAnalyzer/wwwroot/app.scss`.

---

## Surface Palette (backgrounds)

| CSS var | SCSS var | Value | Usage |
|---|---|---|---|
| `--fa-bg-base` | `t.$fa-bg-base` | `#0c0e18` | Page/body background |
| `--fa-bg-raised` | `t.$fa-bg-raised` | `#12152a` | Panels, sidebars, raised surfaces |
| `--fa-bg-surface` | `t.$fa-bg-surface` | `#181c35` | Cards sitting on raised bg |
| `--fa-bg-card` | `t.$fa-bg-card` | `rgba(22,26,52,0.82)` | Translucent card bg |
| `--fa-bg-inset` | `t.$fa-bg-inset` | `rgba(10,12,24,0.6)` | Inset / recessed data areas |

---

## Accent Gold

| CSS var | SCSS var | Value | Usage |
|---|---|---|---|
| `--fa-gold` | `t.$fa-gold` | `#d4a744` | Primary accent, headings, borders |
| `--fa-gold-light` | `t.$fa-gold-light` | `#f0d078` | Hover states, gradient endpoints |
| `--fa-gold-dim` | `t.$fa-gold-dim` | `tint($fa-gold, 12%)` | Subtle gold tints, **hover** fills (not selected/active fills) |
| `--fa-bg-selected` | `t.$fa-bg-selected` | `mix($fa-gold, $fa-bg-raised, 20%)` | **Opaque** selected/active control surface (ButtonGroup / Tabs / chips) — deterministic on any parent |

---

## Text

| CSS var | SCSS var | Value | Usage |
|---|---|---|---|
| `--fa-text` | `t.$fa-text` | `#e2dfd8` | Body text, headings |
| `--fa-text-muted` | `t.$fa-text-muted` | `#9a9586` | Secondary text, explanations |
| `--fa-text-dim` | `t.$fa-text-dim` | `#6b6558` | Placeholders, metadata, labels |

---

## Borders & Dividers

| CSS var | SCSS var | Value | Usage |
|---|---|---|---|
| `--fa-border` | `t.$fa-border` | `mix($fa-gold, $fa-bg-surface, 22%)` | Subtle dividers / hairlines. **Opaque** (pre-composited) so it renders the same colour on any surface and stays crisp at 1px |
| `--fa-border-card` | `t.$fa-border-card` | `mix($fa-gold, $fa-bg-surface, 38%)` | Component boundary (cards, panels, controls, icons). **Opaque**, meets ~3:1 |

Both border tokens are opaque. Prefer them for any structural edge or divider; do not
hand-roll a translucent `t.tint($fa-white/…)` border. Reserve translucent tints for hover
hints and elements that genuinely overlay imagery.

---

## Performance Accents

Used for `.perfect`, `.good`, `.ok`, `.fail` modifier classes and `PerformanceColors` C# class.

| CSS var | SCSS var | Value | Tier |
|---|---|---|---|
| `--fa-perf-perfect` | `t.$fa-perf-perfect` | `#2090c0` | Perfect (blue) |
| `--fa-perf-good` | `t.$fa-perf-good` | `#4ec04e` | Good (green) |
| `--fa-perf-ok` | `t.$fa-perf-ok` | `#ffc84a` | Ok (amber) |
| `--fa-perf-fail` | `t.$fa-perf-fail` | `#ac1f39` | Fail (red) |

The four tiers stay distinct (and separable for common colour-vision deficiencies):
blue / green / amber / red. `PerformanceColors.cs` mirrors these exact values — keep the
two in sync. These are performance-tier colours only; for a generic positive/success
green (kill badge, heal event, full-support) reference `--fa-perf-good`, not `--fa-perf-perfect`.
| `--fa-perf-very-bad` | `t.$fa-perf-very-bad` | `#661111` | Severe-loss chart accent |
| `--fa-perf-mediocre` | `t.$fa-perf-mediocre` | `#dd5533` | Partial / mediocre chart accent |
| `--fa-perf-available` | `t.$fa-perf-available` | `#696864` | Cooldown-ready / unused-capacity accent |

**Note:** `PerformanceColors.cs` (C#) uses slightly different hardcoded hex values.
The SCSS/CSS tokens are the authoritative design values going forward; if they need
to stay in sync, update `PerformanceColors.cs` to match.

---

## Hero / Element Accents

| CSS var | SCSS var | Value | Hero |
|---|---|---|---|
| `--fa-ice` | `t.$fa-ice` | `#6ec8e8` | Rime (frost / ice) |

Future heroes add their own accent here. Do not use hero-specific tokens in shared components.

---

## Shadows

| CSS var | SCSS var | Value | Usage |
|---|---|---|---|
| `--fa-shadow-card` | `t.$fa-shadow-card` | `0 8px 32px rgba(0,0,0,0.45)` | Standard card shadow |
| `--fa-shadow-lg` | `t.$fa-shadow-lg` | `0 16px 48px rgba(0,0,0,0.55)` | Large overlapping panels |

---

## Border Radii

| CSS var | SCSS var | Value | Usage |
|---|---|---|---|
| `--fa-radius-xs` | `t.$fa-radius-xs` | `4px` | Icons, small chips/badges, timeline bars |
| `--fa-radius-sm` | `t.$fa-radius-sm` | `8px` | Small cards/tiles, inputs, inset regions |
| `--fa-radius-md` | `t.$fa-radius-md` | `14px` | Cards, content panels |
| `--fa-radius-lg` | `t.$fa-radius-lg` | `20px` | Home card, page-level / modal containers |
| `--fa-radius-xl` | `t.$fa-radius-xl` | `28px` | Hero-scale elements |
| `--fa-radius-pill` | `t.$fa-radius-pill` | `999px` | Pills (Badge, SupportBadge, chips) |

Use `50%` (not a token) for true circles (HeroIcon portrait, InfoTooltip dot).

---

## Border Widths

| CSS var | SCSS var | Value | Usage |
|---|---|---|---|
| `--fa-border-width` | `t.$fa-border-width` | `1px` | Structural hairline default + dividers |
| `--fa-accent-width` | `t.$fa-accent-width` | `3px` | Coloured left-edge accent stripe |
| `--fa-ring-width` | `t.$fa-ring-width` | `2px` | Box-shadow / emphasis rings (focus, selected, identity) |

---

## Typography

| CSS var | SCSS var | Value |
|---|---|---|
| `--fa-font-body` | `t.$fa-font-body` | `'Noto Sans', system-ui, -apple-system, sans-serif` |
| `--fa-font-heading` | `t.$fa-font-heading` | `'Nunito', system-ui, -apple-system, sans-serif` |

### Guide type scale

Compile-time SCSS only (no CSS-var emission). A tight, shared scale that keeps guide
chrome — section titles, stat tiles, badges, cast inspectors — compact and consistent.
Use these in guide components instead of ad-hoc `rem` values; reach for the nearest role,
not a new size.

| SCSS var | Value | Role |
|---|---|---|
| `t.$fa-fs-label` | `0.66rem` | All-caps micro labels: tile/badge labels, subtitles, section eyebrows |
| `t.$fa-fs-meta` | `0.78rem` | Metadata rows, inline badges, nav labels, pull id |
| `t.$fa-fs-body` | `0.9rem` | Helper text, secondary body copy |
| `t.$fa-fs-value` | `1rem` | Distribution counts (filter / perf badges) |
| `t.$fa-fs-title` | `1.05rem` | Panel and inner section titles |
| `t.$fa-fs-lg` | `1.2rem` | Headline stat numbers, top-level section headings |

---

## Adding a New Token

1. Add the SCSS variable to `_tokens.scss` under the appropriate section
2. Add the CSS custom property emission to `app.scss` in the `:root` block
3. Document it here
