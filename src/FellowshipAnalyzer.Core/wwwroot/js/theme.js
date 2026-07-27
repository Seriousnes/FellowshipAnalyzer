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
