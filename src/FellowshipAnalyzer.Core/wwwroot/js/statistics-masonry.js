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

        container.classList.remove('masonry-active');
        for (const card of cards) {
            card.style.gridRowEnd = '';
            card.style.marginBottom = '0';
        }
        const heights = cards.map((card) => card.getBoundingClientRect().height);

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

    const cardObserver = new ResizeObserver(() => scheduleLayout());
    observeCards();

    const mutationObserver = new MutationObserver(() => {
        observeCards();
        scheduleLayout();
    });
    mutationObserver.observe(container, { childList: true });

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
