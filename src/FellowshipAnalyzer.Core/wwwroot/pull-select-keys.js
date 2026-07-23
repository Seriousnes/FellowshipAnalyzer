const PREVENT_KEYS = new Set(['ArrowDown', 'ArrowUp', 'Home', 'End', ' ', 'Spacebar']);

export function attach(trigger, root, dotNetRef) {
    const onKeyDown = (event) => {
        if (PREVENT_KEYS.has(event.key)) {
            event.preventDefault();
        }
    };

    const onPointerDown = (event) => {
        if (!root.contains(event.target)) {
            dotNetRef.invokeMethodAsync('CloseFromOutsideAsync');
        }
    };

    trigger.addEventListener('keydown', onKeyDown);
    let listening = false;

    return {
        setOpen(open) {
            if (open && !listening) {
                document.addEventListener('pointerdown', onPointerDown);
                listening = true;
            } else if (!open && listening) {
                document.removeEventListener('pointerdown', onPointerDown);
                listening = false;
            }
        },
        dispose() {
            trigger.removeEventListener('keydown', onKeyDown);
            if (listening) {
                document.removeEventListener('pointerdown', onPointerDown);
            }
        },
    };
}
