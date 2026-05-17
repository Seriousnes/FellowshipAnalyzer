# Component Patterns Reference

Common, reusable SCSS patterns used across FellowshipAnalyzer.
Each shows the SCSS, the resulting HTML usage, and what component it comes from.

---

## Card / Panel

A bordered inset surface. Use for statistics, data sections, standalone info blocks.

```scss
@use 'tokens' as t;
@use 'mixins' as mx;

.my-card {
    @include mx.card-surface;          // bg-card, gold border, radius-md, shadow-card
    overflow: hidden;
}

.my-card-header {
    padding: 10px 16px;
    border-bottom: 1px solid var(--fa-border);
    background: rgba(255, 255, 255, 0.03);
}

.my-card-title {
    margin: 0;
    font-size: 1rem;
    font-weight: 700;
    color: var(--fa-gold);
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.my-card-body {
    padding: 14px 16px;
}
```

Real example: `StatCard.razor.css`

---

## Guide Section (Two-Column Layout)

Left explanation text + right data panel. Width split is controlled by a CSS variable.

```scss
@use 'tokens' as t;

.guide-section { margin-bottom: 18px; }

.guide-section-header h3 {
    margin: 0 0 10px;
    font-size: 1.3rem;
    font-weight: 700;
    color: var(--fa-gold);
    border-bottom: 1px solid var(--fa-border);
    padding-bottom: 8px;
}

.guide-section-body {
    display: grid;
    grid-template-columns: var(--explanation-pct) 1fr;
    gap: 16px;

    @include mx.mobile { grid-template-columns: 1fr; }
}

.guide-section-data {
    background: var(--fa-bg-inset);
    border: 1px solid var(--fa-border);
    border-radius: t.$fa-radius-sm;
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

Small colored squares representing per-cast performance.

```scss
@use 'mixins' as mx;

.perf-box-row {
    display: flex;
    flex-wrap: wrap;
    gap: 2px;
    align-items: center;
}

.perf-box {
    width: 16px;
    height: 16px;
    border-radius: 2px;
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

    @include mx.perf-tier-colors;
}
```

Real example: `PerformanceBoxRow.razor.scss`

---

## Spell Badge (Horizontal Filmstrip)

Inline badges with colored borders indicating performance.

```scss
.spell-sequence {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
    align-items: center;
}

.spell-badge {
    display: inline-flex;
    align-items: center;
    border: 1px solid;      // color set via inline style from C#
    border-radius: 4px;
    background: var(--fa-bg-inset);
    font-weight: 600;
    white-space: nowrap;

    &.badge-sm { padding: 2px 6px;  font-size: 0.75rem; }
    &.badge-md { padding: 4px 8px;  font-size: 0.85rem; }
    &.badge-lg { padding: 6px 12px; font-size: 1rem; }
}
```

Real example: `SpellSequence.razor.scss`

---

## Timeline Row + Sticky Label

Horizontal scrolling layout with a sticky left label column.

```scss
.timeline {
    overflow-x: auto;
    overflow-y: visible;
    background: rgba(0, 0, 0, 0.15);
    border-radius: 6px;
    padding: 8px 0;
}

.timeline-row {
    display: flex;
    align-items: center;
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);

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
    font-size: 11px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: rgba(255, 255, 255, 0.45);
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
