// Runtime half of ThemeService.
//
// A design token override is written as a declaration in the root element's style attribute. That
// beats every author-origin rule regardless of selector, so one property overrides the generated
// :root block without touching any other declaration, and var() substitution happens at
// computed-value time, so a color-mix() that reads the token re-resolves on the spot.

const root = document.documentElement;

/// Overrides one custom property. `name` is the full property, e.g. '--fa-gold'.
export function setToken(name, value) {
    root.style.setProperty(name, value);
}

/// Drops one override, so the property falls back to the stylesheet block for the current theme.
export function clearToken(name) {
    root.style.removeProperty(name);
}

/// Selects a theme by its data-theme value, e.g. 'dark'.
export function setTheme(selector) {
    root.dataset.theme = selector;
}

/// The data-theme value in force, or null before one has been selected.
export function currentTheme() {
    return root.dataset.theme || null;
}
