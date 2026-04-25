export function attach(element) {
    let dragging = false;
    let clientX = 0;

    const shouldIgnore = (target) => target.closest('button, a, input, select, textarea, label, [draggable="true"]') !== null;

    const onMouseDown = (event) => {
        if (event.button !== 0 || shouldIgnore(event.target)) {
            return;
        }

        dragging = true;
        clientX = event.clientX;
        element.classList.add('timeline-scroll--dragging');
        event.preventDefault();
    };

    const onMouseUp = () => {
        dragging = false;
        element.classList.remove('timeline-scroll--dragging');
    };

    const onMouseMove = (event) => {
        if (!dragging) {
            return;
        }

        element.scrollLeft -= event.clientX - clientX;
        clientX = event.clientX;
    };

    element.addEventListener('mousedown', onMouseDown);
    window.addEventListener('mouseup', onMouseUp);
    window.addEventListener('mousemove', onMouseMove);

    return {
        dispose() {
            element.removeEventListener('mousedown', onMouseDown);
            window.removeEventListener('mouseup', onMouseUp);
            window.removeEventListener('mousemove', onMouseMove);
        },
    };
}
