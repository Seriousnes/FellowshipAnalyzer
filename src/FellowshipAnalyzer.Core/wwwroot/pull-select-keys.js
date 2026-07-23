const PREVENT_KEYS = new Set(['ArrowDown', 'ArrowUp', 'Home', 'End', ' ', 'Spacebar']);

export function attach(element) {
    const onKeyDown = (event) => {
        if (PREVENT_KEYS.has(event.key)) {
            event.preventDefault();
        }
    };

    element.addEventListener('keydown', onKeyDown);

    return {
        dispose() {
            element.removeEventListener('keydown', onKeyDown);
        },
    };
}
