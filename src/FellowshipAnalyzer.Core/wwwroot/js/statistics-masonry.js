// Packs statistics cards into a WoWAnalyzer-style masonry: short cards rise to fill
// the space under taller neighbours instead of each holding a full-height column slot.
//
// The panel is a native CSS grid, so column spans (Wide / UltraWide) are pure CSS and
// survive even when this module never attaches. This module only measures each card's
// natural height and sets its row span, so the grid packs vertically. Falls back to a
// plain content-height grid (the `.statistics-panel` rules without `masonry-active`).
//
// ROW_UNIT and ROW_GAP mirror `grid-auto-rows` and the intended vertical gap in
// MasonryPanel.razor.scss; keep them in sync.

const ROW_UNIT = 8;
const ROW_GAP = 12;

export function attach(container) {
    let lastWidth = -1;
    let frame = 0;

    const measureAndPlace = () => {
        const width = container.clientWidth;
        if (!width) {
            return;
        }
        lastWidth = width;

        const cards = Array.from(container.children).filter((node) => node.nodeType === 1);

        // Measure natural heights: clearing the spans and dropping `masonry-active`
        // collapses the rows to min-content so each card reports its real height.
        container.classList.remove('masonry-active');
        for (const card of cards) {
            card.style.gridRowEnd = '';
            card.style.marginBottom = '0';
        }
        const heights = cards.map((card) => card.getBoundingClientRect().height);

        // Switch to fine rows and span each card enough rows for its content plus a gap.
        container.classList.add('masonry-active');
        cards.forEach((card, i) => {
            const span = Math.max(1, Math.ceil((heights[i] + ROW_GAP) / ROW_UNIT));
            card.style.gridRowEnd = `span ${span}`;
        });
    };

    const scheduleLayout = () => {
        if (frame) {
            return;
        }
        frame = requestAnimationFrame(() => {
            frame = 0;
            measureAndPlace();
        });
    };

    // Only a width change alters the column count and thus the heights. The height
    // change we cause by setting spans must not re-trigger, so this observer is
    // width-guarded to avoid a feedback loop.
    const containerObserver = new ResizeObserver(() => {
        if (container.clientWidth !== lastWidth) {
            scheduleLayout();
        }
    });
    containerObserver.observe(container);

    const observeCards = () => {
        for (const card of Array.from(container.children)) {
            if (card.nodeType === 1) {
                cardObserver.observe(card);
            }
        }
    };

    // A card that grows after measurement (late-loading icon, reflowed text) needs a
    // forced re-layout regardless of width.
    const cardObserver = new ResizeObserver(() => scheduleLayout());
    observeCards();

    // Cards added or removed after mount get re-observed and re-packed. childList only,
    // so the inline styles this module writes never re-trigger it.
    const mutationObserver = new MutationObserver(() => {
        observeCards();
        scheduleLayout();
    });
    mutationObserver.observe(container, { childList: true });

    // The heading font reflows text after first paint.
    if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(scheduleLayout);
    }

    measureAndPlace();

    return {
        relayout: scheduleLayout,
        dispose() {
            containerObserver.disconnect();
            cardObserver.disconnect();
            mutationObserver.disconnect();
            if (frame) {
                cancelAnimationFrame(frame);
            }
            container.classList.remove('masonry-active');
            for (const card of Array.from(container.children)) {
                if (card.nodeType === 1) {
                    card.style.gridRowEnd = '';
                    card.style.marginBottom = '';
                }
            }
        },
    };
}
