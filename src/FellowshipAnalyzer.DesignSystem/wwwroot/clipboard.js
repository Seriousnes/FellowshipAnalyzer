export function copyText(text) {
    if (!navigator.clipboard) return Promise.resolve(false);
    return navigator.clipboard.writeText(text).then(() => true).catch(() => false);
}
