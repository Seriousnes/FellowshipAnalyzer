# Component Patterns Reference

Common, reusable SCSS patterns used across FellowshipAnalyzer.
Each shows the SCSS, the resulting HTML usage, and what component it comes from.

---

## Card / Panel

A bordered inset surface. Use for statistics, data sections, standalone info blocks.

```scss
@use 'mixins' as mx;

.my-card {
    @include mx.card-surface;          // bg-card, steel border, radius-md, shadow-card
    overflow: hidden;
}

.my-card-header {
    padding: 10px 16px;
    border-bottom: var(--fa-border-width) solid var(--fa-border);
    background: var(--fa-raise);
}

.my-card-title {
    margin: 0;
    font-size: var(--fa-fs-value);
    font-weight: 700;
    color: var(--fa-gold);
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.my-card-body {
    padding: 14px 16px;
}
```

Real example: `StatCard.razor.scss`

---

## Guide Section (Two-Column Layout)

Left explanation text + right data panel. Width split is controlled by a CSS variable.

```scss
@use 'mixins' as mx;

.guide-section { margin-bottom: 18px; }

.guide-section-header h3 {
    margin: 0 0 10px;
    font-size: var(--fa-fs-lg);
    font-weight: 700;
    color: var(--fa-gold);
    border-bottom: var(--fa-border-width) solid var(--fa-border);
    padding-bottom: 8px;
}

.guide-section-body {
    display: grid;
    grid-template-columns: var(--explanation-pct) 1fr;
    gap: 16px;

    @include mx.mobile { grid-template-columns: 1fr; }
}

.guide-section-data {
    @include mx.inset-surface;
    padding: 12px;
}
```

In Razor, pass `--explanation-pct` via inline style:
```html
<div class="guide-section-body" style="--explanation-pct: 40%;">
```

Real example: `GuideSection.razor.scss`

---

## Performance Box Row

Small colored squares representing per-cast performance. The tier colour comes from the generated
`*-filled` semantic class, so the SCSS declares no `background` of its own.

```scss
.perf-box-row {
    display: flex;
    flex-wrap: wrap;
    gap: 2px;
    align-items: center;
}

.perf-box {
    width: 16px;
    height: 16px;
    border-radius: var(--fa-radius-xs);
    border: none;
    padding: 0;
    cursor: default;
    transition: opacity 0.12s ease, transform 0.12s ease;

    &.clickable {
        cursor: pointer;

        &:hover {
            opacity: 0.85;
            transform: scale(1.15);
        }
    }
}
```

In Razor, map the tier to its class. An untiered entry gets no class and keeps the base styling:
```razor
<button class="perf-box @FillClass(entry.Performance)" title="@entry.Tooltip"></button>

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

Real example: `PerformanceBoxRow.razor` and `PerformanceBoxRow.razor.scss`

---

## Spell Sequence (Horizontal Filmstrip)

A row of spell icons whose ring carries the cast's performance tier. The badge wears the bare
semantic class, which sets `color`, and `currentColor` carries that colour into the child
component's border. The container holds the neutral ring an untiered cast falls back to.

```scss
.spell-sequence {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
    align-items: center;
    color: var(--fa-border-card);
}

.spell-badge {
    display: inline-flex;

    ::deep .spell-icon-link { color: inherit; }

    ::deep .spell-icon {
        border-width: var(--fa-ring-width);
        border-color: currentColor;
    }
}
```

```razor
<span class="spell-badge @SizeClass @TierClass(cast.Performance)" title="@cast.Tooltip">
    <SpellIcon Spell="cast.Id" Size="@IconSize" />
</span>
```

`::deep` is warranted here because the border belongs to a child component, which is the one case
section 4 of the skill allows it.

Real example: `SpellSequence.razor` and `SpellSequence.razor.scss`

---

## Timeline Row + Sticky Label

Horizontal scrolling layout with a sticky left label column.

```scss
.timeline {
    overflow-x: auto;
    overflow-y: visible;
    background: var(--fa-recess);
    border-radius: var(--fa-radius-sm);
    padding: 8px 0;
}

.timeline-row {
    display: flex;
    align-items: center;
    border-bottom: var(--fa-border-width) solid var(--fa-edge);

    &:last-child { border-bottom: none; }
}

.timeline-label {
    width: 180px;
    flex-shrink: 0;
    position: sticky;
    left: 0;
    background: inherit;   // inherit from parent for sticky bg
    z-index: 1;
    padding: 4px 8px;
}

.timeline-label-name {
    font-size: 13px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.timeline-label-name-section {
    font-size: var(--fa-fs-label);
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--fa-fg-muted);
}
```

Real example: `Timeline.razor.scss`

---

## Eyebrow Label (Global Utility)

Small all-caps section label. Available everywhere as a global class.

```html
<p class="eyebrow">Statistics</p>
```

Defined in `app.scss` — **do not redefine in components**.

---

## Gradient Heading

The gradient text fill used on the home page hero text.

```scss
@use 'mixins' as mx;

.page-title {
    @include mx.gradient-heading;
    font-size: clamp(2rem, 5vw, 2.8rem);
}
```

---

## Stats Grid (3-column → 1-column on mobile)

```scss
@use 'mixins' as mx;

.stats-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 6px;
    margin-bottom: 6px;

    @include mx.mobile-sm { grid-template-columns: 1fr; }
}
```

Real example: `CastOverview.razor.scss`
