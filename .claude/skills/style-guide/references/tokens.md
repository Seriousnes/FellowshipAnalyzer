# Design Tokens Reference

Token values live in C#, under `src/FellowshipAnalyzer.Core.Contracts/Design/`:

| Record | Owns |
|---|---|
| `FaPalette` | Every colour token, plus the two section-header gradients |
| `FaTypography` | Font stacks and the type scale |
| `FaMetrics` | Edge widths and corner radii |
| `FaElevation` | Drop shadows |
| `FaTheme` | The three themes (Original, Dark, Light) and the group order below |
| `FaSemantic` | The semantic class names and the token each one wears |
| `FaVar` | The `var(--fa-*)` reference C# hands to markup, one per token named from code |

`src/FellowshipAnalyzer.Tools/emit-palette.cs` renders `FaTheme` into
`src/FellowshipAnalyzer.Core/Styles/_palette.scss`, which declares all 96 tokens under `:root`,
`:root[data-theme='dark']` and `:root[data-theme='light']`, then mints the semantic classes.
A drift test fails the build if the committed partial does not match the C# theme.

Every token resolves in every theme, so stylesheets write `var(--fa-*)` with no fallback.
The live values for all three themes are in `_palette.scss`, and the Design System app's
Tokens page lists them with previews.

---

## Base palette

Twelve raw colours. Identical in every theme, and the base every other colour is chosen against.

| Token | Value | Role |
|---|---|---|
| `--fa-white` | `#ffffff` | Pure white, base for light tints |
| `--fa-black` | `#000000` | Pure black, base for shadow and recess tints |
| `--fa-grey` | `#787878` | Neutral grey |
| `--fa-amber` | `#fab700` | Warning amber |
| `--fa-steel` | `#6b9ed2` | Steel blue |
| `--fa-arcane` | `#9a7bff` | Arcane violet |
| `--fa-nature` | `#5fd0b0` | Nature teal |
| `--fa-link` | `#0087ff` | Hyperlink blue |
| `--fa-frame` | `#63696f` | Desaturated steel frame, base for borders |
| `--fa-blood` | `#661111` | Deep blood red |
| `--fa-rust` | `#dd5533` | Scorched orange-red |
| `--fa-stone` | `#696864` | Warm grey for an inert or available state |

---

## Brand artwork

The brand mark's gradient stops. Artwork, so every theme carries the same value.

| Token | Value |
|---|---|
| `--fa-brand-cream` | `#f4e9d0` |
| `--fa-brand-gold` | `#d4a744` |

---

## Surfaces

Per theme. The Original theme samples the Fellowship in-game UI: one universal ground with panels
stepping up a cool-navy ramp and inset receding.

| Token | Role |
|---|---|
| `--fa-bg-base` | Universal ground under every screen |
| `--fa-bg-raised` | Panel one step above the ground |
| `--fa-bg-surface` | Card sitting on a raised panel |
| `--fa-bg-card` | The card plate itself |
| `--fa-bg-inset` | Recessed data area inside a card |
| `--fa-bg-hover` | Hover wash over any surface |
| `--fa-raise` | Lift applied to a header strip or sticky row |
| `--fa-recess` | Shallow recess behind scrolling content |
| `--fa-recess-deep` | Deep recess, modal scrim and inset wells |
| `--fa-section-header` | Section-header gradient |
| `--fa-section-header-hover` | Section-header gradient on hover |

---

## Foreground

Per theme.

| Token | Role |
|---|---|
| `--fa-text` | Body text and headings |
| `--fa-text-muted` | Secondary text, explanations |
| `--fa-text-dim` | Placeholders, metadata, labels |
| `--fa-fg` | Foreground over a coloured or image surface |
| `--fa-fg-muted` | Secondary foreground over the same |
| `--fa-fg-dim` | Tertiary foreground over the same |
| `--fa-fg-neutral` | Value colour for a stat carrying no performance tier |

---

## Accents and intent

Gold is the emphasis colour: titles, links, eyebrows. Structure is steel, not gold.
Gold varies per theme; `--fa-ice` and `--fa-danger` are the same in all three.

| Token | Role |
|---|---|
| `--fa-gold` | Primary accent |
| `--fa-gold-light` | Hover state, gradient endpoint |
| `--fa-gold-pale` | Panel and section titles |
| `--fa-gold-dim` | Subtle gold fill, hover and active wash |
| `--fa-ice` (`#6ec8e8`) | Rime's frost accent |
| `--fa-danger` (`#ac1f39`) | Destructive or failed intent |

---

## Structure and edges

A quiet tint of the steel frame. Prefer spacing and fill for separation; these are the fallback.
`--fa-border-subtle` is the same in all three themes, the rest vary.

| Token | Role |
|---|---|
| `--fa-border` | Subtle divider and hairline |
| `--fa-border-card` | Component boundary: cards, panels, controls, icons |
| `--fa-border-subtle` | The faintest structural edge |
| `--fa-edge` | Row separator inside a scrolling region |
| `--fa-panel-border` | Panel outline, which some themes leave fully transparent |

---

## Performance tiers

Four tiers, identical in every theme, and separable for the common colour-vision deficiencies.
`PerformanceColors.cs` names them through `FaVar`, so C# holds no value of its own.

| Token | Value | Tier | `QualitativePerformance` |
|---|---|---|---|
| `--fa-perf-perfect` | `#2090c0` | Perfect (blue) | `Perfect` |
| `--fa-perf-good` | `#4ec04e` | Good (green) | `Good` |
| `--fa-perf-ok` | `#ffc84a` | Ok (amber) | `Ok` |
| `--fa-perf-fail` | `#ac1f39` | Fail (red) | `Fail` |

For a generic positive result (kill badge, heal event, full support) reference `--fa-perf-good`.
For a chart accent outside the tier set, reach for the base palette: `--fa-blood` for a severe
loss, `--fa-rust` for a partial one, `--fa-stone` for capacity that was available and unused.

---

## Chart series

Six categorical hues for a chart that plots several series at once. Assign them **in slot order**,
one slot per series, and never cycle or reassign a slot when a series is empty: a colour follows the
thing it names, so a filter that drops a series must not repaint the survivors. The light theme
carries the same six hues stepped for the cream ground rather than flipped.

| Token | Original / Dark | Light | Hue |
|---|---|---|---|
| `--fa-chart1` | `#3987e5` | `#2a78d6` | Blue |
| `--fa-chart2` | `#d95926` | `#eb6834` | Orange |
| `--fa-chart3` | `#199e70` | `#1baf7a` | Aqua |
| `--fa-chart4` | `#c98500` | `#eda100` | Yellow |
| `--fa-chart5` | `#d55181` | `#e87ba4` | Magenta |
| `--fa-chart6` | `#9085e9` | `#4a3aa7` | Violet |

The order is the colour-vision-safety mechanism, not decoration: adjacent slots are the pairs a
stacked or grouped chart puts side by side, and this order was validated for adjacent separation
against the card surface in both the dark and light grounds. Reordering the slots or substituting a
hue means re-validating. Green is deliberately absent so a series never impersonates `--fa-perf-good`.

`FaVar.ChartSeries` lists the six references in slot order. A charting library that writes colours
into SVG presentation attributes cannot read `var(--fa-*)`, so a chart resolves tokens to their CSS
text through `ChartPalette` rather than handing the reference over; that keeps the C# theme the only
place a value is written and keeps runtime token overrides reaching the chart.

---

## Hero roles

`--fa-role-unknown` is a tint of the theme's border colour, so it varies; the other three do not.

| Token | Value | Role |
|---|---|---|
| `--fa-role-tank` | `#336699` | Tank |
| `--fa-role-healer` | `#4ec04e` | Healer |
| `--fa-role-dps` | `#ac1f39` | Dps |
| `--fa-role-unknown` | per theme | Unassigned |

---

## Hero identity

One per shipped hero, identical in every theme. `Hero.Color` maps a `HeroName` to its `FaVar`
member; adding a hero means adding its property to `FaPalette` and every theme in `FaTheme`.

| Token | Value | Token | Value |
|---|---|---|---|
| `--fa-hero-aeona` | `#fc9fec` | `--fa-hero-rime` | `#1ea3ee` |
| `--fa-hero-ardeos` | `#eb6328` | `--fa-hero-sylvie` | `#ea4f84` |
| `--fa-hero-elarion` | `#935dff` | `--fa-hero-tariq` | `#527af5` |
| `--fa-hero-gunde` | `#943738` | `--fa-hero-vigour` | `#dddbc5` |
| `--fa-hero-helena` | `#b46831` | `--fa-hero-xavian` | `#077365` |
| `--fa-hero-mara` | `#965a90` | `--fa-hero-unknown` | `#9a9586` |
| `--fa-hero-meiko` | `#28e05c` | | |

---

## Event types

The colour coding `EventsView` uses. Identical in every theme.

| Token | Value | Event |
|---|---|---|
| `--fa-ev-cast` | `#6ec8e8` | Cast |
| `--fa-ev-damage` | `#ac1f39` | Damage |
| `--fa-ev-heal` | `#4ec04e` | Heal |
| `--fa-ev-buff` | `#9a7bff` | Buff applied or refreshed |
| `--fa-ev-buff-fade` | `#9a7bff` | Buff removed |
| `--fa-ev-debuff` | `#fab700` | Debuff applied, refreshed or removed |
| `--fa-ev-death` | `#661111` | Death |
| `--fa-ev-resource` | `#b8965c` | Resource change |
| `--fa-ev-system` | `#949ba5` | Global cooldown, spell-usable update |
| `--fa-ev-modified` | `#fab700` | Row a normalizer modified |
| `--fa-ev-fabricated` | `#4ec04e` | Row a normalizer fabricated |
| `--fa-ev-reordered` | `#6b9ed2` | Row a normalizer reordered |

Several of these share a value with a base or tier colour. Always reference the one that names
what the rule means: a heal row is `--fa-ev-heal`, not `--fa-ev-fabricated`.

---

## Elevation

Per theme, because a shadow that reads on a dark ground is too heavy on a light one.

| Token | Role |
|---|---|
| `--fa-shadow-card` | Standard card shadow |
| `--fa-shadow-lg` | Large overlapping panel, modal |

---

## Metrics

`FaMetrics.Default`, shared by every theme.

| Token | Value | Usage |
|---|---|---|
| `--fa-border-width` | `1px` | Structural hairline default and dividers |
| `--fa-accent-width` | `3px` | Coloured left-edge accent stripe |
| `--fa-ring-width` | `2px` | Box-shadow rings: focus, selected, identity |
| `--fa-radius-xs` | `3px` | Icons, small chips and badges, timeline bars |
| `--fa-radius-sm` | `5px` | Small cards and tiles, inputs, inset regions |
| `--fa-radius-md` | `8px` | Cards, content panels |
| `--fa-radius-lg` | `12px` | Home card, page-level and modal containers |
| `--fa-radius-xl` | `16px` | Hero-scale elements |
| `--fa-radius-pill` | `999px` | Pills: Badge, SupportBadge, chips |

The game's frames are close to square with only a soft easing of the corner, so the scale stays
tight and panels read as crisp plates. Use `50%` (not a token) for a true circle.

---

## Typography

`FaTypography.Default`, shared by every theme. Headings are an engraved Roman serif echoing the
FELLOWSHIP wordmark; body stays a humanist sans for dense data readability.

| Token | Value |
|---|---|
| `--fa-font-body` | `"Source Sans 3", system-ui, -apple-system, sans-serif` |
| `--fa-font-heading` | `"Cinzel", "Trajan Pro", Georgia, serif` |
| `--fa-font-mono` | `"Cascadia Code", "JetBrains Mono", ui-monospace, "SFMono-Regular", Consolas, monospace` |

### Type scale

A tight, shared scale that keeps guide chrome (section titles, stat tiles, badges, cast inspectors)
compact and consistent. Reach for the nearest role, not a new size.

| Token | Value | Role |
|---|---|---|
| `--fa-fs-label` | `0.66rem` | All-caps micro labels: tile and badge labels, subtitles, eyebrows |
| `--fa-fs-meta` | `0.78rem` | Metadata rows, inline badges, nav labels, pull id |
| `--fa-fs-body` | `0.9rem` | Helper text, secondary body copy |
| `--fa-fs-value` | `1rem` | Distribution counts (filter and perf badges) |
| `--fa-fs-title` | `1.05rem` | Panel and inner section titles |
| `--fa-fs-lg` | `1.2rem` | Headline stat numbers, top-level section headings |

---

## Tint scale

`_tokens.scss` holds the tint percentage scale and the `tint()` function. Nothing else.

| Step | Value |
|---|---|
| `t.$fa-tint-faint` | `6%` |
| `t.$fa-tint-subtle` | `12%` |
| `t.$fa-tint-soft` | `20%` |
| `t.$fa-tint-muted` | `30%` |
| `t.$fa-tint-medium` | `40%` |
| `t.$fa-tint-strong` | `60%` |
| `t.$fa-tint-veil` | `80%` |

`tint()` takes a token reference and a step, and returns a live `color-mix()`:

```scss
t.tint(var(--fa-black), t.$fa-tint-soft)
// color-mix(in srgb, var(--fa-black) 20%, transparent)
```

The step is baked at compile time, the colour is not, so a runtime theme change reaches the tint.

---

## Semantic classes

`_palette.scss` mints three classes for each of the twenty names in `FaSemantic`:

| Form | Declares |
|---|---|
| `.perfect` | `color` |
| `.perfect-bordered` | `border-color` |
| `.perfect-filled` | `background` |

| Group | Names |
|---|---|
| Performance tiers | `perfect`, `good`, `ok`, `fail` |
| Hero roles | `tank`, `healer`, `dps`, `unknown` |
| Event types | `cast`, `damage`, `heal`, `buff`, `buff-fade`, `debuff`, `death`, `resource`, `system`, `modified`, `fabricated`, `reordered` |

A component applies the class rather than restating the token.

---

## Adding a new token

1. Add the property to the relevant design record (`FaPalette`, `FaTypography`, `FaMetrics`, `FaElevation`)
2. Give it a value in every theme in `FaTheme`, and list it in the matching group in `FaTheme.Groups`
3. Add an `FaVar` member if C# needs to name it in markup
4. Regenerate from `src/FellowshipAnalyzer.Tools`:

```powershell
dotnet run --no-cache emit-palette.cs "../FellowshipAnalyzer.Core/Styles/_palette.scss"
```

5. Document it here
