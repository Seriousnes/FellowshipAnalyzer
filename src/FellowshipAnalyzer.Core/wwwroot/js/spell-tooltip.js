/**
 * Spell tooltip module.
 * Lazily fetches tooltip HTML from the Fellowship Codex on hover and displays it
 * in a floating popover using Shadow DOM for style isolation.
 */

const SHOW_DELAY_MS = 200;
const HIDE_DELAY_MS = 150;
const GAP_PX = 8;

const BASE_CSS = `
    :host { pointer-events: auto; }
    .tooltip-body {
        background: #1a1d2e;
        border: 1px solid #3f3f46;
        border-radius: 6px;
        padding: 12px;
        box-shadow: 0 4px 24px rgba(0, 0, 0, 0.6);
        max-width: 420px;
    }
    .tooltip-loading {
        color: #909BA6;
        font-family: 'Noto Sans', sans-serif;
        font-size: 0.875rem;
        padding: 4px 8px;
    }
`;

/** @type {Map<string, string>} */
const cache = new Map();

/** @type {HTMLDivElement | null} */
let container = null;

/** @type {ShadowRoot | null} */
let shadow = null;

/** @type {Element | null} */
let currentTarget = null;

let origin = '';
let showTimer = 0;
let hideTimer = 0;

export function init(codexOrigin) {
    if (container) return;

    origin = codexOrigin;

    container = document.createElement('div');
    container.id = 'spell-tooltip';
    Object.assign(container.style, {
        position: 'fixed',
        zIndex: '99999',
        display: 'none',
        pointerEvents: 'none',
    });
    document.body.appendChild(container);

    shadow = container.attachShadow({ mode: 'open' });

    document.addEventListener('pointerenter', onEnter, true);
    document.addEventListener('pointerleave', onLeave, true);
    container.addEventListener('pointerenter', cancelHide);
    container.addEventListener('pointerleave', scheduleHide);
}

export function dispose() {
    if (!container) return;
    document.removeEventListener('pointerenter', onEnter, true);
    document.removeEventListener('pointerleave', onLeave, true);
    container.remove();
    container = null;
    shadow = null;
}

function onEnter(e) {
    const el = e.target.closest?.('[data-tooltip-spell-id]');
    if (!el) return;
    cancelHide();
    clearTimeout(showTimer);
    showTimer = setTimeout(() => show(el), SHOW_DELAY_MS);
}

function onLeave(e) {
    const el = e.target.closest?.('[data-tooltip-spell-id]');
    if (!el) return;
    clearTimeout(showTimer);
    scheduleHide();
}

function scheduleHide() {
    clearTimeout(hideTimer);
    hideTimer = setTimeout(hide, HIDE_DELAY_MS);
}

function cancelHide() {
    clearTimeout(hideTimer);
}

function hide() {
    if (container) container.style.display = 'none';
    currentTarget = null;
}

async function show(anchor) {
    currentTarget = anchor;
    const path = anchor.dataset.tooltipSpellId;
    if (!path) return;

    render('<div class="tooltip-loading">Loading…</div>');
    container.style.display = 'block';
    position(anchor);

    let html = cache.get(path);
    if (!html) {
        try {
            const res = await fetch(`${origin}/${path}`);
            if (!res.ok) throw new Error(String(res.status));
            html = await res.text();
            cache.set(path, html);
        } catch {
            html = '<div class="tooltip-loading">Tooltip unavailable</div>';
        }
    }

    if (currentTarget !== anchor) return;

    render(html);
    position(anchor);
}

function render(html) {
    shadow.innerHTML = '';
    const style = document.createElement('style');
    style.textContent = BASE_CSS;
    shadow.appendChild(style);
    const wrapper = document.createElement('div');
    wrapper.className = 'tooltip-body';
    wrapper.innerHTML = html;
    shadow.appendChild(wrapper);
}

function position(anchor) {
    container.style.left = '0px';
    container.style.top = '0px';

    const r = anchor.getBoundingClientRect();
    const tr = container.getBoundingClientRect();

    let top = r.top - tr.height - GAP_PX;
    if (top < GAP_PX) {
        top = r.bottom + GAP_PX;
    }

    let left = r.left + r.width / 2 - tr.width / 2;
    left = Math.max(GAP_PX, Math.min(left, window.innerWidth - tr.width - GAP_PX));
    top = Math.max(GAP_PX, Math.min(top, window.innerHeight - tr.height - GAP_PX));

    container.style.left = `${left}px`;
    container.style.top = `${top}px`;
}
