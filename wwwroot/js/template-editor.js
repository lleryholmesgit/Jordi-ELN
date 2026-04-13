document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector("[data-template-editor]");
    if (!root) {
        return;
    }

    const editor = root.querySelector("[data-rich-editor]");
    const hidden = root.querySelector("[data-template-rich-text]");
    const highlightButton = root.querySelector("[data-template-highlight]");
    const clearHighlightButton = root.querySelector("[data-template-clear-highlight]");

    if (!editor || !hidden) {
        return;
    }

    const sync = () => {
        hidden.value = editor.innerHTML.trim() || "<p><br></p>";
    };

    const getSelectionRange = () => {
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) {
            return null;
        }

        const range = selection.getRangeAt(0);
        return editor.contains(range.commonAncestorContainer) ? range : null;
    };

    const unwrapHighlight = (element) => {
        const parent = element.parentNode;
        if (!parent) {
            return;
        }

        while (element.firstChild) {
            parent.insertBefore(element.firstChild, element);
        }

        parent.removeChild(element);
    };

    const applyHighlight = () => {
        const range = getSelectionRange();
        if (!range || range.collapsed) {
            return;
        }

        const fragment = range.extractContents();
        const wrapper = document.createElement("mark");
        wrapper.className = "template-highlight";
        wrapper.appendChild(fragment);
        range.insertNode(wrapper);
        sync();
    };

    const clearHighlight = () => {
        const range = getSelectionRange();
        if (!range) {
            return;
        }

        const selectionNode = range.commonAncestorContainer.nodeType === Node.ELEMENT_NODE
            ? range.commonAncestorContainer
            : range.commonAncestorContainer.parentElement;
        const highlighted = selectionNode?.closest?.("mark.template-highlight");
        if (!highlighted || !editor.contains(highlighted)) {
            return;
        }

        unwrapHighlight(highlighted);
        sync();
    };

    highlightButton?.addEventListener("click", () => {
        applyHighlight();
        editor.focus();
    });

    clearHighlightButton?.addEventListener("click", () => {
        clearHighlight();
        editor.focus();
    });

    editor.addEventListener("input", sync);
    root.addEventListener("submit", sync);
    sync();
});
