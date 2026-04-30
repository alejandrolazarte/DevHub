window.devhubTerminal = {
    startResize(dotnetRef, startY, startHeight) {
        const onMove = (e) => {
            const delta = startY - e.clientY;
            const newHeight = Math.max(80, Math.min(900, startHeight + delta));
            dotnetRef.invokeMethodAsync('SetHeight', newHeight);
        };
        const onUp = () => {
            window.removeEventListener('mousemove', onMove);
            window.removeEventListener('mouseup', onUp);
        };
        window.addEventListener('mousemove', onMove);
        window.addEventListener('mouseup', onUp);
    }
};

window.devhubConsole = {
    scrollToBottom(element) {
        if (element) element.scrollTop = element.scrollHeight;
    },
    focusElement(element) {
        if (element) {
            element.focus();
            // move cursor to end
            const len = element.value?.length ?? 0;
            element.setSelectionRange?.(len, len);
        }
    },
    isScrolledToBottom(element) {
        if (!element) return true;
        return element.scrollHeight - element.scrollTop - element.clientHeight < 40;
    }
};
