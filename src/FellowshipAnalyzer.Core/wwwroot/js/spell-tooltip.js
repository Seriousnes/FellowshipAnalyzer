/**
 * Spell tooltip module.
 * Fetches tooltip fragments from the Fellowship Codex on hover and paints them into a Shadow DOM
 * layer. A fragment arrives fully styled, so nothing is drawn around it. On a coarse pointer a tap
 * asks for the mobile fragment instead, which comes wrapped in its own full-screen chrome.
 */

const SHOW_DELAY_MS = 200;
const HIDE_DELAY_MS = 150;
const HOVERS = '(hover: hover)';

/** @type {Map<string, string>} Fragments the codex has answered. A refusal is not kept, so a
 * request that failed once is asked again on the next hover. */
const cache = new Map();

/** @type {HTMLDivElement | null} */
let container = null;

/** @type {ShadowRoot | null} */
let shadow = null;

/** @type {Element | null} */
let currentTarget = null;

let origin = '';
let modal = false;
let sequence = 0;
let showTimer = 0;
let hideTimer = 0;

export function init(codexOrigin) {
    if (container) return;

    origin = codexOrigin;

    container = document.createElement('div');
    container.id = 'spell-tooltip';
    Object.assign(container.style, {
        position: 'fixed',
        left: '0',
        top: '0',
        zIndex: '99999',
        display: 'none',
        pointerEvents: 'none',
    });
    document.body.appendChild(container);

    shadow = container.attachShadow({ mode: 'open' });

    document.addEventListener('pointerenter', onEnter, true);
    document.addEventListener('pointerleave', onLeave, true);
    document.addEventListener('click', onClick, true);
    document.addEventListener('keydown', onKey);
}

export function dispose() {
    if (!container) return;
    document.removeEventListener('pointerenter', onEnter, true);
    document.removeEventListener('pointerleave', onLeave, true);
    document.removeEventListener('click', onClick, true);
    document.removeEventListener('keydown', onKey);
    hide();
    container.remove();
    container = null;
    shadow = null;
}

const hovers = () => window.matchMedia(HOVERS).matches;

/** Whether the event came from a pointer that cannot hover, so a tap is all the reader has. */
const coarse = event => (event.pointerType ? event.pointerType !== 'mouse' : !hovers());

function onEnter(e) {
    if (modal || (e.pointerType && e.pointerType !== 'mouse')) return;

    const el = e.target.closest?.('[data-tooltip-spell-id]');
    if (!el) return;

    cancelHide();
    clearTimeout(showTimer);
    showTimer = setTimeout(() => show(el), SHOW_DELAY_MS);
}

function onLeave(e) {
    if (modal || (e.pointerType && e.pointerType !== 'mouse')) return;
    if (!e.target.closest?.('[data-tooltip-spell-id]')) return;

    clearTimeout(showTimer);
    scheduleHide();
}

function onClick(e) {
    const path = e.composedPath();

    if (modal && path[0] instanceof Element && path[0].hasAttribute('data-dismiss')) {
        e.preventDefault();
        e.stopPropagation();
        hide();
        return;
    }

    if (modal && path.some(node => node instanceof Element && node.matches?.('.fx-modal .open'))) {
        hide();
        return;
    }

    const link = e.target.closest?.('[data-tooltip-spell-id]');
    if (!link || !coarse(e)) return;

    e.preventDefault();
    e.stopPropagation();
    present(link);
}

function onKey(e) {
    if (e.key === 'Escape') hide();
}

function scheduleHide() {
    if (modal) return;
    clearTimeout(hideTimer);
    hideTimer = setTimeout(hide, HIDE_DELAY_MS);
}

function cancelHide() {
    clearTimeout(hideTimer);
}

function hide() {
    clearTimeout(showTimer);
    clearTimeout(hideTimer);
    sequence++;

    if (container) {
        container.style.display = 'none';
        container.style.inset = '';
        container.style.pointerEvents = 'none';
    }

    if (shadow) shadow.replaceChildren();

    currentTarget = null;

    if (modal) {
        modal = false;
        document.documentElement.style.overflow = '';
    }
}

async function fragment(path) {
    const cached = cache.get(path);
    if (cached) return cached;

    try {
        const response = await fetch(`${origin}/${path}`, { headers: { accept: 'text/html' } });
        if (!response.ok) return null;

        const body = await response.text();

        cache.set(path, body);
        return body;
    } catch {
        return null;
    }
}

async function show(anchor) {
    const path = anchor.dataset.tooltipSpellId;
    if (!path) return;

    const mine = ++sequence;
    const body = await fragment(path);

    if (mine !== sequence || !body || modal || !anchor.isConnected) return;

    currentTarget = anchor;
    paint(body);
    container.style.display = 'block';
    position(anchor);
}

async function present(anchor) {
    const path = anchor.dataset.tooltipSpellId;
    if (!path) return;

    hide();

    const mine = ++sequence;
    const body = await fragment(mobile(path));

    if (mine !== sequence || !body) return;

    paint(body);

    const open = shadow.querySelector('.fx-modal .open');
    if (open && anchor.href) open.href = anchor.href;

    modal = true;
    container.style.display = 'block';
    container.style.inset = '0';
    container.style.pointerEvents = 'auto';
    document.documentElement.style.overflow = 'hidden';

    shadow.querySelector('.fx-modal .shut')?.focus();
}

function mobile(path) {
    return path + (path.includes('?') ? '&' : '?') + 'mobile';
}

function paint(html) {
    shadow.innerHTML = html;
}

function position(anchor) {
    container.style.left = '0px';
    container.style.top = '0px';

    const r = anchor.getBoundingClientRect();
    const width = container.offsetWidth;
    const height = container.offsetHeight;

    let top = r.top - height - 8;
    if (top < 8) {
        top = r.bottom + 8;
    }

    let left = r.left + r.width / 2 - width / 2;
    left = Math.max(8, Math.min(left, window.innerWidth - width - 8));
    top = Math.max(8, Math.min(top, window.innerHeight - height - 8));

    container.style.left = `${left}px`;
    container.style.top = `${top}px`;
}
