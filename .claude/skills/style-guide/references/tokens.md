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
| `--fa-gold-dim` | `t.$fa-gold-dim` | `rgba(212,167,68,0.14)` | Subtle gold tints, hover fills |

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
| `--fa-border` | `t.$fa-border` | `rgba(212,167,68,0.12)` | Subtle dividers, low-contrast borders |
| `--fa-border-card` | `t.$fa-border-card` | `rgba(212,167,68,0.18)` | Card borders (more visible) |

---

## Performance Accents

Used for `.perfect`, `.good`, `.ok`, `.fail` modifier classes and `PerformanceColors` C# class.

| CSS var | SCSS var | Value | Tier |
|---|---|---|---|
| `--fa-perf-perfect` | `t.$fa-perf-perfect` | `#56d67b` | Perfect |
| `--fa-perf-good` | `t.$fa-perf-good` | `#90cc60` | Good |
| `--fa-perf-ok` | `t.$fa-perf-ok` | `#d4a744` | Ok (same as gold) |
| `--fa-perf-fail` | `t.$fa-perf-fail` | `#d4564a` | Fail |
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
| `--fa-radius-sm` | `t.$fa-radius-sm` | `8px` | Small elements, inset regions |
| `--fa-radius-md` | `t.$fa-radius-md` | `14px` | Cards, panels |
| `--fa-radius-lg` | `t.$fa-radius-lg` | `20px` | Home card, large panels |
| `--fa-radius-xl` | `t.$fa-radius-xl` | `28px` | Hero-scale elements |

---

## Typography

| CSS var | SCSS var | Value |
|---|---|---|
| `--fa-font-body` | `t.$fa-font-body` | `'Noto Sans', system-ui, -apple-system, sans-serif` |
| `--fa-font-heading` | `t.$fa-font-heading` | `'Nunito', system-ui, -apple-system, sans-serif` |

---

## Adding a New Token

1. Add the SCSS variable to `_tokens.scss` under the appropriate section
2. Add the CSS custom property emission to `app.scss` in the `:root` block
3. Document it here
