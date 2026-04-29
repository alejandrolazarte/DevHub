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
