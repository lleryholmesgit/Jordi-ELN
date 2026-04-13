document.addEventListener("DOMContentLoaded", () => {
    const parseJson = (text, fallback) => {
        try {
            return text ? JSON.parse(text) : fallback;
        } catch {
            return fallback;
        }
    };

    const createId = () => {
        if (window.crypto && typeof window.crypto.randomUUID === "function") {
            return window.crypto.randomUUID();
        }

        return `id-${Date.now()}-${Math.random().toString(16).slice(2)}`;
    };

    const flowRoot = document.querySelector("[data-flowchart-editor]");
    if (flowRoot) {
        const hidden = document.querySelector("[data-flowchart-json]");
        const canvas = flowRoot.querySelector("[data-flowchart-canvas]");
        const state = parseJson(flowRoot.dataset.flowchartSource, { nodes: [], edges: [] });
        if (!Array.isArray(state.nodes)) state.nodes = [];
        if (!Array.isArray(state.edges)) state.edges = [];
        let drag = null;

        const sync = () => {
            hidden.value = JSON.stringify(state);
        };

        const drawEdges = () => {
            canvas.querySelectorAll(".flow-edge").forEach((node) => node.remove());
            state.edges.forEach((edge) => {
                const from = state.nodes.find((node) => node.id === edge.from);
                const to = state.nodes.find((node) => node.id === edge.to);
                if (!from || !to) return;
                const dx = to.x - from.x;
                const dy = to.y - from.y;
                const length = Math.sqrt(dx * dx + dy * dy);
                const angle = Math.atan2(dy, dx) * 180 / Math.PI;
                const line = document.createElement("div");
                line.className = "flow-edge";
                line.style.left = `${from.x + 60}px`;
                line.style.top = `${from.y + 22}px`;
                line.style.width = `${length}px`;
                line.style.transform = `rotate(${angle}deg)`;
                canvas.appendChild(line);
            });
        };

        const renderNodes = () => {
            canvas.querySelectorAll(".flow-node").forEach((node) => node.remove());
            state.nodes.forEach((node) => {
                const element = document.createElement("div");
                element.className = "flow-node";
                element.textContent = node.label;
                element.style.left = `${node.x}px`;
                element.style.top = `${node.y}px`;
                element.addEventListener("pointerdown", (event) => {
                    drag = { id: node.id, offsetX: event.offsetX, offsetY: event.offsetY };
                    element.setPointerCapture(event.pointerId);
                });
                element.addEventListener("dblclick", () => {
                    const next = window.prompt("Node label", node.label);
                    if (next) {
                        node.label = next;
                        renderNodes();
                    }
                });
                canvas.appendChild(element);
            });
            drawEdges();
            sync();
        };

        canvas.addEventListener("pointermove", (event) => {
            if (!drag) return;
            const node = state.nodes.find((item) => item.id === drag.id);
            if (!node) return;
            const rect = canvas.getBoundingClientRect();
            node.x = Math.max(0, event.clientX - rect.left - drag.offsetX);
            node.y = Math.max(0, event.clientY - rect.top - drag.offsetY);
            renderNodes();
        });

        canvas.addEventListener("pointerup", () => {
            drag = null;
        });

        flowRoot.querySelector("[data-add-node]")?.addEventListener("click", () => {
            state.nodes.push({ id: createId(), label: `Step ${state.nodes.length + 1}`, x: 24 + state.nodes.length * 26, y: 24 + state.nodes.length * 18 });
            renderNodes();
        });

        flowRoot.querySelector("[data-add-edge]")?.addEventListener("click", () => {
            if (state.nodes.length >= 2) {
                const lastTwo = state.nodes.slice(-2);
                state.edges.push({ from: lastTwo[0].id, to: lastTwo[1].id });
                renderNodes();
            }
        });

        if (!state.nodes.length) {
            state.nodes.push({ id: createId(), label: "Start", x: 28, y: 28 });
        }
        renderNodes();
    }

    document.querySelectorAll("[data-flowchart-viewer]").forEach((flowViewer) => {
        const canvas = flowViewer.querySelector("[data-flowchart-canvas]");
        const emptyState = flowViewer.querySelector("[data-flowchart-empty]");
        const state = parseJson(flowViewer.dataset.flowchartSource, { nodes: [], edges: [] });
        if (!canvas) {
            return;
        }

        if (!Array.isArray(state.nodes)) state.nodes = [];
        if (!Array.isArray(state.edges)) state.edges = [];

        if (!state.nodes.length) {
            canvas.hidden = true;
            if (emptyState) {
                emptyState.hidden = false;
            }
            return;
        }

        const drawEdges = () => {
            state.edges.forEach((edge) => {
                const from = state.nodes.find((node) => node.id === edge.from);
                const to = state.nodes.find((node) => node.id === edge.to);
                if (!from || !to) return;
                const dx = to.x - from.x;
                const dy = to.y - from.y;
                const length = Math.sqrt(dx * dx + dy * dy);
                const angle = Math.atan2(dy, dx) * 180 / Math.PI;
                const line = document.createElement("div");
                line.className = "flow-edge";
                line.style.left = `${from.x + 60}px`;
                line.style.top = `${from.y + 22}px`;
                line.style.width = `${length}px`;
                line.style.transform = `rotate(${angle}deg)`;
                canvas.appendChild(line);
            });
        };

        state.nodes.forEach((node) => {
            const element = document.createElement("div");
            element.className = "flow-node";
            element.textContent = node.label;
            element.style.left = `${node.x}px`;
            element.style.top = `${node.y}px`;
            canvas.appendChild(element);
        });

        drawEdges();
        if (emptyState) {
            emptyState.hidden = true;
        }
    });

    const recordRoot = document.querySelector("[data-record-editor]");
    if (!recordRoot) {
        return;
    }

    const inventoryOptionsNode = recordRoot.querySelector("[data-inventory-options]");
    const inventoryOptions = parseJson(inventoryOptionsNode?.textContent, [])
        .map((item) => ({
            id: item?.id ?? item?.Id,
            code: item?.code ?? item?.Code,
            label: item?.label ?? item?.Label,
            detailPath: item?.detailPath ?? item?.DetailPath
        }))
        .filter((item) => item && item.id)
        .map((item) => ({
            value: String(item.id),
            label: String(item.label || ""),
            code: String(item.code || "").trim().toUpperCase(),
            detailPath: String(item.detailPath || `/Inventory/Details/${item.id}`)
        }));
    const templateOptionsNode = recordRoot.querySelector("[data-template-options]");
    const templateOptions = parseJson(templateOptionsNode?.textContent, [])
        .map((item) => ({
            id: item?.id ?? item?.Id,
            name: item?.name ?? item?.Name,
            defaultRichText: item?.defaultRichText ?? item?.DefaultRichText
        }))
        .filter((item) => item && item.id)
        .map((item) => ({
            value: String(item.id),
            name: String(item.name || ""),
            defaultRichText: String(item.defaultRichText || "<p><br></p>")
        }));
    const notebookOptionsNode = recordRoot.querySelector("[data-notebook-options]");
    const notebookOptions = parseJson(notebookOptionsNode?.textContent, [])
        .map((item) => ({
            id: item?.id ?? item?.Id,
            code: item?.code ?? item?.Code,
            title: item?.title ?? item?.Title,
            label: item?.label ?? item?.Label,
            detailPath: item?.detailPath ?? item?.DetailPath
        }))
        .filter((item) => item && item.id)
        .map((item) => ({
            value: String(item.id),
            label: String(item.label || ""),
            title: String(item.title || ""),
            code: String(item.code || "").trim().toUpperCase(),
            detailPath: String(item.detailPath || `/Records/Details/${item.id}`)
        }));

    const notebookBlocksHidden = recordRoot.querySelector("[data-notebook-blocks]");
    const richTextHidden = recordRoot.querySelector("[data-rich-text-content]");
    const instrumentLinksHidden = recordRoot.querySelector("[data-instrument-links]");
    const clientInput = recordRoot.querySelector("input[name='Title']");
    const projectNameInput = recordRoot.querySelector("input[name='ProjectName']");
    const experimentCodeInput = recordRoot.querySelector("input[name='ExperimentCode']");
    const editorShell = recordRoot.querySelector(".notebook-editor-shell");
    const editor = recordRoot.querySelector("[data-rich-editor]");
    const templateSelect = recordRoot.querySelector("select[name='TemplateId']");
    const photoInput = recordRoot.querySelector("[data-photo-input]");
    const insertInventoryButton = recordRoot.querySelector("[data-insert-inventory]");
    const insertRecordLinkButton = recordRoot.querySelector("[data-insert-record-link]");
    const insertTableButton = recordRoot.querySelector("[data-insert-table]");
    const insertEquationButton = recordRoot.querySelector("[data-insert-equation]");
    const insertPhotoButton = recordRoot.querySelector("[data-insert-photo]");
    const tableContext = recordRoot.querySelector("[data-table-context]");
    const inventorySearch = recordRoot.querySelector("[data-inventory-search]");
    const inventorySearchInput = recordRoot.querySelector("[data-inventory-search-input]");
    const inventorySearchResults = recordRoot.querySelector("[data-inventory-search-results]");
    const inventoryHoursInput = recordRoot.querySelector("[data-inventory-hours]");
    const closeInventorySearchButton = recordRoot.querySelector("[data-close-inventory-search]");
    const recordSearch = recordRoot.querySelector("[data-record-search]");
    const recordSearchInput = recordRoot.querySelector("[data-record-search-input]");
    const recordSearchResults = recordRoot.querySelector("[data-record-search-results]");
    const closeRecordSearchButton = recordRoot.querySelector("[data-close-record-search]");
    const equationEditor = recordRoot.querySelector("[data-equation-editor]");
    const equationInput = recordRoot.querySelector("[data-equation-input]");
    const insertEquationConfirmButton = recordRoot.querySelector("[data-insert-equation-confirm]");
    const closeEquationEditorButton = recordRoot.querySelector("[data-close-equation-editor]");

    if (!editor || !editorShell || !richTextHidden || !instrumentLinksHidden || !notebookBlocksHidden || !tableContext) {
        return;
    }

    let activeCell = null;
    let selectedCells = [];
    let resizeState = null;
    let savedEditorRange = null;
    let draggedNoteBlock = null;
    let lastTemplateId = templateSelect?.value || "";
    let experimentCodeRequestTimer = null;
    const draggableBlockSelector = "[data-draggable-note-block]";
    const dragHandleSelector = "[data-note-block-handle]";

    const isTableContextVisible = () => !tableContext.hidden;

    const setTableContextVisible = (visible) => {
        tableContext.hidden = !visible;
        insertTableButton?.classList.toggle("is-active", visible);
    };

    const saveEditorRangeFromSelection = () => {
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) {
            return;
        }

        const range = selection.getRangeAt(0);
        if (editor.contains(range.commonAncestorContainer)) {
            savedEditorRange = range.cloneRange();
        }
    };

    const clearSelectedState = () => {
        selectedCells.forEach((cell) => cell.classList.remove("selected-table-cell"));
        selectedCells = [];
    };

    const normalizeEditorMarkup = (markup) => {
        const text = String(markup || "").trim();
        return text ? text : "<p><br></p>";
    };

    const isEditorEffectivelyEmpty = () => {
        const clone = editor.cloneNode(true);
        clone.querySelectorAll(".inline-widget-remove, .note-block-handle").forEach((element) => element.remove());
        const text = clone.textContent?.replace(/\u00a0/g, " ").trim() || "";
        const hasEmbeddedBlocks = Boolean(clone.querySelector("[data-inventory-id], [data-record-link-id], [data-equation-source], table, figure"));
        if (hasEmbeddedBlocks) {
            return false;
        }

        const markup = normalizeEditorMarkup(clone.innerHTML)
            .replace(/<p><br><\/p>/gi, "")
            .replace(/<div><br><\/div>/gi, "")
            .replace(/\s+/g, "");
        return !text && markup.length === 0;
    };

    const applyTemplateContent = (templateId, requireConfirmation) => {
        const option = templateOptions.find((item) => item.value === String(templateId || ""));
        if (!option) {
            return true;
        }

        const nextMarkup = normalizeEditorMarkup(option.defaultRichText);
        const currentMarkup = normalizeEditorMarkup(editor.innerHTML);
        const hasMeaningfulContent = !isEditorEffectivelyEmpty() && currentMarkup !== nextMarkup;
        if (requireConfirmation && hasMeaningfulContent && !window.confirm(`Replace the current notebook content with the "${option.name}" template?`)) {
            return false;
        }

        editor.innerHTML = nextMarkup;
        enhanceEditorContent();
        syncHiddenFields();
        editor.focus();
        return true;
    };

    const setSelectedCells = (cells) => {
        clearSelectedState();
        selectedCells = cells.filter(Boolean);
        selectedCells.forEach((cell) => cell.classList.add("selected-table-cell"));
        activeCell = selectedCells[0] ?? null;
    };

    const applyResponsiveImageSize = (image) => {
        const maxWidth = Math.max(280, Math.min(editor.clientWidth - 48, window.innerWidth - 160, 980));
        const naturalWidth = image.naturalWidth || maxWidth;
        image.style.width = `${Math.min(naturalWidth, maxWidth)}px`;
        image.style.maxWidth = "100%";
        image.style.height = "auto";
        image.style.maxHeight = `${Math.min(window.innerHeight * 0.7, 840)}px`;
    };

    const ensureColGroup = (table) => {
        let colgroup = table.querySelector("colgroup");
        const columnCount = Math.max(...Array.from(table.rows).map((row) => row.cells.length), 1);
        if (!colgroup) {
            colgroup = document.createElement("colgroup");
            table.prepend(colgroup);
        }

        while (colgroup.children.length < columnCount) {
            const col = document.createElement("col");
            col.style.width = `${Math.max(140, Math.floor(table.clientWidth / columnCount) || 160)}px`;
            colgroup.appendChild(col);
        }

        while (colgroup.children.length > columnCount) {
            colgroup.lastElementChild?.remove();
        }

        return Array.from(colgroup.children);
    };

    const normalizeTable = (table) => {
        if (!table.tBodies.length) {
            const body = document.createElement("tbody");
            Array.from(table.rows).forEach((row) => body.appendChild(row));
            table.appendChild(body);
        }

        if (table.tHead) {
            const body = table.tBodies[0];
            Array.from(table.tHead.rows).reverse().forEach((row) => {
                Array.from(row.cells).forEach((cell) => {
                    if (cell.tagName === "TH") {
                        const replacement = document.createElement("td");
                        replacement.innerHTML = cell.innerHTML;
                        row.replaceChild(replacement, cell);
                    }
                });
                body.insertBefore(row, body.firstChild);
            });
            table.tHead.remove();
        }

        table.querySelectorAll("th").forEach((cell) => {
            const replacement = document.createElement("td");
            replacement.innerHTML = cell.innerHTML;
            cell.parentElement?.replaceChild(replacement, cell);
        });

        table.querySelectorAll("td").forEach((cell) => {
            cell.contentEditable = "true";
        });

        ensureColGroup(table);
    };

    const isEmptyEditorParagraph = (element) => {
        return Boolean(element)
            && element.tagName === "P"
            && !element.textContent.trim()
            && (element.innerHTML || "").replace(/\s+/g, "").toLowerCase() === "<br>";
    };

    const hasOnlyOneMeaningfulChild = (container, child) => {
        const meaningfulNodes = Array.from(container.childNodes)
            .filter((node) => !(node.nodeType === Node.TEXT_NODE && !node.textContent.trim()));
        return meaningfulNodes.length === 1 && meaningfulNodes[0] === child;
    };

    const promoteStandaloneBlock = (element) => {
        if (!element || element.parentElement === editor) {
            return;
        }

        let topLevel = element.parentElement;
        while (topLevel && topLevel.parentElement !== editor) {
            topLevel = topLevel.parentElement;
        }

        if (!topLevel || topLevel.parentElement !== editor || topLevel === element) {
            return;
        }

        if (topLevel.tagName === "P" && hasOnlyOneMeaningfulChild(topLevel, element)) {
            editor.insertBefore(element, topLevel.nextSibling);
            topLevel.remove();
        }
    };

    const isDraggableBlock = (element) => {
        if (!element || element.parentElement !== editor) {
            return false;
        }

        if (element.matches(".notebook-inline-table, figure.notebook-inline-photo, [data-inventory-id], [data-record-link-id], [data-equation-source]")) {
            return true;
        }

        return element.tagName === "P" && !isEmptyEditorParagraph(element);
    };

    const ensureBlockHandle = (element) => {
        if (element.querySelector(":scope > .note-block-handle")) {
            return;
        }

        const handle = document.createElement("button");
        handle.type = "button";
        handle.className = "note-block-handle";
        handle.textContent = "Drag";
        handle.contentEditable = "false";
        handle.setAttribute("draggable", "true");
        handle.setAttribute("data-note-block-handle", "true");
        element.insertBefore(handle, element.firstChild);
    };

    const decorateDraggableBlocks = () => {
        editor.querySelectorAll("[data-inventory-id], [data-record-link-id]").forEach((element) => {
            promoteStandaloneBlock(element);
        });

        Array.from(editor.children).forEach((element) => {
            element.classList.remove("notebook-draggable-block", "is-dragging-note-block", "drag-before", "drag-after");
            element.removeAttribute("data-draggable-note-block");
            element.removeAttribute("draggable");
            element.querySelector(":scope > .note-block-handle")?.remove();

            if (!isDraggableBlock(element)) {
                return;
            }

            element.classList.add("notebook-draggable-block");
            element.setAttribute("data-draggable-note-block", "true");
            element.setAttribute("draggable", "true");
            if (!element.dataset.noteBlockId) {
                element.dataset.noteBlockId = createId();
            }
            ensureBlockHandle(element);
        });
    };

    const enhanceEditorContent = () => {
        editor.querySelectorAll("[data-inventory-id]").forEach((widget) => {
            widget.classList.add("inventory-inline-card");
            widget.contentEditable = "false";
            if (!widget.querySelector(".inline-widget-remove")) {
                const remove = document.createElement("button");
                remove.type = "button";
                remove.className = "inline-widget-remove";
                remove.setAttribute("data-remove-inline-widget", "true");
                remove.textContent = "-";
                widget.appendChild(remove);
            }
        });

        editor.querySelectorAll("[data-record-link-id]").forEach((widget) => {
            widget.classList.add("record-inline-card");
            widget.contentEditable = "false";
            if (!widget.querySelector(".inline-widget-remove")) {
                const remove = document.createElement("button");
                remove.type = "button";
                remove.className = "inline-widget-remove";
                remove.setAttribute("data-remove-inline-widget", "true");
                remove.textContent = "-";
                widget.appendChild(remove);
            }
        });

        editor.querySelectorAll("[data-equation-source]").forEach((widget) => {
            widget.classList.add("notebook-equation-block");
            widget.contentEditable = "false";
            const source = widget.getAttribute("data-equation-source") || widget.textContent || "";
            widget.setAttribute("data-equation-source", source);
            if (!widget.querySelector(".equation-badge")) {
                const badge = document.createElement("span");
                badge.className = "equation-badge";
                badge.textContent = "Equation";
                widget.insertBefore(badge, widget.firstChild);
            }

            let render = widget.querySelector(".equation-render");
            if (!render) {
                render = document.createElement("div");
                render.className = "equation-render";
                widget.appendChild(render);
            }

            render.innerHTML = renderEquationMarkup(source);
            if (!widget.querySelector(".inline-widget-remove")) {
                const remove = document.createElement("button");
                remove.type = "button";
                remove.className = "inline-widget-remove";
                remove.setAttribute("data-remove-inline-widget", "true");
                remove.textContent = "-";
                widget.appendChild(remove);
            }
        });

        editor.querySelectorAll("figure figcaption").forEach((caption) => {
            caption.contentEditable = "true";
        });

        editor.querySelectorAll(".notebook-inline-photo img, .photo-embed img, figure img").forEach((image) => {
            image.addEventListener("load", () => applyResponsiveImageSize(image));
            if (image.complete) {
                applyResponsiveImageSize(image);
            }
        });

        editor.querySelectorAll("table").forEach((table) => {
            normalizeTable(table);
            if (!table.closest(".notebook-inline-table")) {
                const wrapper = document.createElement("div");
                wrapper.className = "notebook-inline-table";
                table.parentElement?.insertBefore(wrapper, table);
                wrapper.appendChild(table);
            }
        });

        decorateDraggableBlocks();
    };

    const syncHiddenFields = () => {
        const clone = editor.cloneNode(true);
        clone.querySelectorAll(".inline-widget-remove").forEach((element) => element.remove());
        clone.querySelectorAll(".note-block-handle").forEach((element) => element.remove());
        clone.querySelectorAll("[contenteditable]").forEach((element) => element.removeAttribute("contenteditable"));
        clone.querySelectorAll(".selected-table-cell").forEach((element) => element.classList.remove("selected-table-cell"));
        clone.querySelectorAll("[data-draggable-note-block]").forEach((element) => {
            element.removeAttribute("data-draggable-note-block");
            element.removeAttribute("draggable");
            element.removeAttribute("data-note-block-id");
            element.classList.remove("notebook-draggable-block", "is-dragging-note-block", "drag-before", "drag-after");
        });
        richTextHidden.value = clone.innerHTML;
        notebookBlocksHidden.value = "[]";

        const inventoryLinks = Array.from(editor.querySelectorAll("[data-inventory-id]"))
            .map((element) => ({
                instrumentId: Number(element.getAttribute("data-inventory-id")),
                usageHours: element.getAttribute("data-usage-hours") ? Number(element.getAttribute("data-usage-hours")) : null
            }))
            .filter((item) => item.instrumentId > 0);

        instrumentLinksHidden.value = JSON.stringify(inventoryLinks);
    };

    const getSelectionRange = () => {
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0) {
            if (savedEditorRange && editor.contains(savedEditorRange.commonAncestorContainer)) {
                return savedEditorRange.cloneRange();
            }

            const range = document.createRange();
            range.selectNodeContents(editor);
            range.collapse(false);
            return range;
        }

        const range = selection.getRangeAt(0);
        if (!editor.contains(range.commonAncestorContainer)) {
            if (savedEditorRange && editor.contains(savedEditorRange.commonAncestorContainer)) {
                return savedEditorRange.cloneRange();
            }

            const fallback = document.createRange();
            fallback.selectNodeContents(editor);
            fallback.collapse(false);
            return fallback;
        }

        return range;
    };

    const placeCaretInCell = (cell, collapseToEnd = false) => {
        const selection = window.getSelection();
        const range = document.createRange();
        range.selectNodeContents(cell);
        range.collapse(!collapseToEnd ? true : false);
        selection?.removeAllRanges();
        selection?.addRange(range);
        cell.focus();
    };

    const insertNodeAtCursor = (node) => {
        editor.focus();
        const selection = window.getSelection();
        const range = getSelectionRange();
        range.deleteContents();
        range.insertNode(node);
        range.setStartAfter(node);
        range.collapse(true);
        selection?.removeAllRanges();
        selection?.addRange(range);
        savedEditorRange = range.cloneRange();
        syncHiddenFields();
    };

    const appendParagraphSpacer = () => {
        const paragraph = document.createElement("p");
        paragraph.innerHTML = "<br>";
        insertNodeAtCursor(paragraph);
    };

    const getCurrentEditorBlock = () => {
        const range = getSelectionRange();
        let node = range.startContainer;

        if (node === editor) {
            const candidate = editor.childNodes[range.startOffset] || editor.lastChild;
            return candidate?.nodeType === Node.ELEMENT_NODE ? candidate : null;
        }

        if (node.nodeType === Node.TEXT_NODE) {
            node = node.parentElement;
        }

        while (node && node.parentElement !== editor) {
            node = node.parentElement;
        }

        return node && node.parentElement === editor ? node : null;
    };

    const focusEditorBlock = (element) => {
        const selection = window.getSelection();
        const range = document.createRange();
        range.selectNodeContents(element);
        range.collapse(false);
        selection?.removeAllRanges();
        selection?.addRange(range);
        savedEditorRange = range.cloneRange();
    };

    const ensureTrailingParagraph = (element) => {
        let paragraph = element.nextElementSibling;
        if (!isEmptyEditorParagraph(paragraph)) {
            paragraph = document.createElement("p");
            paragraph.innerHTML = "<br>";
            editor.insertBefore(paragraph, element.nextSibling);
        }

        return paragraph;
    };

    const insertBlockAtCursor = (element) => {
        editor.focus();
        const anchor = getCurrentEditorBlock();
        if (isEmptyEditorParagraph(anchor)) {
            editor.insertBefore(element, anchor);
            enhanceEditorContent();
            focusEditorBlock(anchor);
        } else if (anchor && anchor.parentElement === editor) {
            editor.insertBefore(element, anchor.nextSibling);
            enhanceEditorContent();
            const paragraph = ensureTrailingParagraph(element);
            focusEditorBlock(paragraph);
        } else {
            editor.appendChild(element);
            enhanceEditorContent();
            const paragraph = ensureTrailingParagraph(element);
            focusEditorBlock(paragraph);
        }

        syncHiddenFields();
    };

    const createInventoryWidget = (option, usageHours) => {
        const widget = document.createElement("div");
        widget.className = "inventory-inline-card";
        widget.contentEditable = "false";
        widget.setAttribute("data-inventory-id", option.value);
        if (usageHours !== null && usageHours !== undefined && usageHours !== "") {
            widget.setAttribute("data-usage-hours", String(usageHours));
        }

        const link = document.createElement("a");
        link.href = option.detailPath || `/Inventory/Details/${option.value}`;
        link.target = "_blank";
        link.rel = "noreferrer";
        link.textContent = option.label;

        const hours = document.createElement("span");
        hours.className = "inventory-pill-hours";
        hours.textContent = usageHours !== null && usageHours !== undefined && usageHours !== "" ? `${usageHours} h` : "Linked";

        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "inline-widget-remove";
        remove.setAttribute("data-remove-inline-widget", "true");
        remove.textContent = "-";

        widget.append(link, hours, remove);
        return widget;
    };

    const createRecordLinkWidget = (option) => {
        const widget = document.createElement("div");
        widget.className = "record-inline-card";
        widget.contentEditable = "false";
        widget.setAttribute("data-record-link-id", option.value);

        const link = document.createElement("a");
        link.href = option.detailPath || `/Records/Details/${option.value}`;
        link.target = "_blank";
        link.rel = "noreferrer";
        link.textContent = option.label;

        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "inline-widget-remove";
        remove.setAttribute("data-remove-inline-widget", "true");
        remove.textContent = "-";

        widget.append(link, remove);
        return widget;
    };

    const createTableBlock = () => {
        const wrapper = document.createElement("div");
        wrapper.className = "notebook-inline-table";

        const table = document.createElement("table");
        table.className = "notebook-rich-table";
        const body = document.createElement("tbody");
        for (let rowIndex = 0; rowIndex < 2; rowIndex += 1) {
            const row = document.createElement("tr");
            for (let cellIndex = 0; cellIndex < 2; cellIndex += 1) {
                const cell = document.createElement("td");
                cell.contentEditable = "true";
                row.appendChild(cell);
            }
            body.appendChild(row);
        }

        table.appendChild(body);
        wrapper.appendChild(table);
        normalizeTable(table);
        return wrapper;
    };

    const createPhotoFigure = (dataUrl) => {
        const figure = document.createElement("figure");
        figure.className = "notebook-inline-photo";

        const image = document.createElement("img");
        image.src = dataUrl;
        image.alt = "Notebook photo";
        image.addEventListener("load", () => applyResponsiveImageSize(image));

        const caption = document.createElement("figcaption");
        caption.contentEditable = "true";
        caption.textContent = "Photo caption";

        figure.append(image, caption);
        return figure;
    };

    const escapeHtml = (value) => String(value || "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");

    const normalizeEquationText = (value) => String(value || "")
        .replaceAll("<=", "≤")
        .replaceAll(">=", "≥")
        .replaceAll("!=", "≠")
        .replaceAll("->", "→")
        .replaceAll("<-", "←");

    const isWrappedGroup = (value, openChar, closeChar) => {
        if (!value.startsWith(openChar) || !value.endsWith(closeChar)) {
            return false;
        }

        let depth = 0;
        for (let index = 0; index < value.length; index += 1) {
            const character = value[index];
            if (character === openChar) {
                depth += 1;
            } else if (character === closeChar) {
                depth -= 1;
                if (depth === 0 && index < value.length - 1) {
                    return false;
                }
            }
        }

        return depth === 0;
    };

    const unwrapOuterParentheses = (value) => {
        let current = String(value || "").trim();
        let wrapped = false;

        while (isWrappedGroup(current, "(", ")")) {
            current = current.slice(1, -1).trim();
            wrapped = true;
        }

        return { value: current, wrapped };
    };

    const wrapParenthesized = (markup) => `<span class="equation-parenthesized"><span class="equation-paren">(</span>${markup}<span class="equation-paren">)</span></span>`;

    const readBalancedSegment = (value, startIndex, openChar, closeChar) => {
        let depth = 0;
        for (let index = startIndex; index < value.length; index += 1) {
            const character = value[index];
            if (character === openChar) {
                depth += 1;
            } else if (character === closeChar) {
                depth -= 1;
                if (depth === 0) {
                    return {
                        content: value.slice(startIndex + 1, index),
                        nextIndex: index + 1
                    };
                }
            }
        }

        return {
            content: value.slice(startIndex + 1),
            nextIndex: value.length
        };
    };

    const tokenizeTopLevelOperators = (value, operators) => {
        const tokens = [];
        const operatorSet = new Set(Array.isArray(operators) ? operators : [operators]);
        let current = "";
        let parenthesisDepth = 0;
        let braceDepth = 0;

        const previousNonWhitespaceCharacter = (index) => {
            for (let cursor = index - 1; cursor >= 0; cursor -= 1) {
                if (!/\s/.test(value[cursor])) {
                    return value[cursor];
                }
            }

            return "";
        };

        for (let index = 0; index < value.length; index += 1) {
            const character = value[index];
            if (character === "(") {
                parenthesisDepth += 1;
                current += character;
                continue;
            }

            if (character === ")") {
                parenthesisDepth = Math.max(0, parenthesisDepth - 1);
                current += character;
                continue;
            }

            if (character === "{") {
                braceDepth += 1;
                current += character;
                continue;
            }

            if (character === "}") {
                braceDepth = Math.max(0, braceDepth - 1);
                current += character;
                continue;
            }

            const isUnaryPlusOrMinus = (character === "+" || character === "-")
                && "+-*/=_(,".includes(previousNonWhitespaceCharacter(index) || "");

            if (parenthesisDepth === 0 && braceDepth === 0 && operatorSet.has(character) && !(index === 0 && (character === "+" || character === "-")) && !isUnaryPlusOrMinus) {
                tokens.push({ type: "value", value: current });
                tokens.push({ type: "operator", value: character });
                current = "";
                continue;
            }

            current += character;
        }

        tokens.push({ type: "value", value: current });
        return tokens;
    };

    const findTopLevelSlash = (value) => {
        let parenthesisDepth = 0;
        let braceDepth = 0;
        for (let index = 0; index < value.length; index += 1) {
            const character = value[index];
            if (character === "(") {
                parenthesisDepth += 1;
                continue;
            }

            if (character === ")") {
                parenthesisDepth = Math.max(0, parenthesisDepth - 1);
                continue;
            }

            if (character === "{") {
                braceDepth += 1;
                continue;
            }

            if (character === "}") {
                braceDepth = Math.max(0, braceDepth - 1);
                continue;
            }

            if (character === "/" && parenthesisDepth === 0 && braceDepth === 0) {
                return index;
            }
        }

        return -1;
    };

    const readScriptOperand = (value, startIndex) => {
        let index = startIndex;
        while (index < value.length && /\s/.test(value[index])) {
            index += 1;
        }

        if (index >= value.length) {
            return { expression: "", nextIndex: value.length, wrapped: false };
        }

        if (value[index] === "{") {
            const grouped = readBalancedSegment(value, index, "{", "}");
            return { expression: grouped.content, nextIndex: grouped.nextIndex, wrapped: false };
        }

        if (value[index] === "(") {
            const grouped = readBalancedSegment(value, index, "(", ")");
            return { expression: grouped.content, nextIndex: grouped.nextIndex, wrapped: true };
        }

        if (/sqrt\s*\(/i.test(value.slice(index))) {
            const match = /^sqrt\s*\(/i.exec(value.slice(index));
            if (match) {
                const openingIndex = index + match[0].length - 1;
                const grouped = readBalancedSegment(value, openingIndex, "(", ")");
                return { expression: `sqrt(${grouped.content})`, nextIndex: grouped.nextIndex, wrapped: false };
            }
        }

        let endIndex = index;
        if ((value[endIndex] === "+" || value[endIndex] === "-") && endIndex + 1 < value.length) {
            endIndex += 1;
        }

        while (endIndex < value.length && /[A-Za-z0-9.]/.test(value[endIndex])) {
            endIndex += 1;
        }

        if (endIndex === index) {
            endIndex += 1;
        }

        return {
            expression: value.slice(index, endIndex),
            nextIndex: endIndex,
            wrapped: false
        };
    };

    const renderEquationMarkup = (expression) => {
        const renderSegment = (segment) => {
            const trimmed = String(segment || "").trim();
            if (!trimmed) {
                return "";
            }

            const outerParentheses = unwrapOuterParentheses(trimmed);
            const working = outerParentheses.value;

            const relationTokens = tokenizeTopLevelOperators(working, "=");
            if (relationTokens.some((token) => token.type === "operator")) {
                const relationMarkup = relationTokens
                    .map((token) => token.type === "operator"
                        ? `<span class="equation-operator">${escapeHtml(token.value)}</span>`
                        : renderSegment(token.value))
                    .join(" ");
                return outerParentheses.wrapped ? wrapParenthesized(relationMarkup) : relationMarkup;
            }

            const additiveTokens = tokenizeTopLevelOperators(working, ["+", "-"]);
            if (additiveTokens.some((token) => token.type === "operator")) {
                const additiveMarkup = additiveTokens
                    .map((token) => token.type === "operator"
                        ? `<span class="equation-operator">${escapeHtml(token.value)}</span>`
                        : renderSegment(token.value))
                    .join(" ");
                return outerParentheses.wrapped ? wrapParenthesized(additiveMarkup) : additiveMarkup;
            }

            const slashIndex = findTopLevelSlash(working);
            if (slashIndex > -1) {
                const numerator = renderSegment(working.slice(0, slashIndex));
                const denominator = renderSegment(working.slice(slashIndex + 1));
                const fractionMarkup = `<span class="equation-fraction"><span class="equation-fraction-numerator">${numerator || "&nbsp;"}</span><span class="equation-fraction-denominator">${denominator || "&nbsp;"}</span></span>`;
                return outerParentheses.wrapped ? wrapParenthesized(fractionMarkup) : fractionMarkup;
            }

            const multiplicativeTokens = tokenizeTopLevelOperators(working, ["*"]);
            if (multiplicativeTokens.some((token) => token.type === "operator")) {
                const multiplicativeMarkup = multiplicativeTokens
                    .map((token) => token.type === "operator"
                        ? '<span class="equation-operator">×</span>'
                        : renderSegment(token.value))
                    .join(" ");
                return outerParentheses.wrapped ? wrapParenthesized(multiplicativeMarkup) : multiplicativeMarkup;
            }

            if (/^sqrt\s*\(/i.test(working)) {
                const sqrtMatch = /^sqrt\s*\(/i.exec(working);
                const openingIndex = sqrtMatch ? sqrtMatch[0].length - 1 : -1;
                if (openingIndex >= 0 && working[working.length - 1] === ")" && isWrappedGroup(working.slice(openingIndex), "(", ")")) {
                    const rootContent = working.slice(openingIndex + 1, -1);
                    const sqrtMarkup = `<span class="equation-sqrt">√</span><span class="equation-overline">${renderSegment(rootContent)}</span>`;
                    return outerParentheses.wrapped ? wrapParenthesized(sqrtMarkup) : sqrtMarkup;
                }
            }

            let scriptIndex = -1;
            let parenthesisDepth = 0;
            let braceDepth = 0;
            for (let index = 0; index < working.length; index += 1) {
                const character = working[index];
                if (character === "(") parenthesisDepth += 1;
                else if (character === ")") parenthesisDepth = Math.max(0, parenthesisDepth - 1);
                else if (character === "{") braceDepth += 1;
                else if (character === "}") braceDepth = Math.max(0, braceDepth - 1);
                else if ((character === "^" || character === "_") && parenthesisDepth === 0 && braceDepth === 0) {
                    scriptIndex = index;
                    break;
                }
            }

            if (scriptIndex > 0) {
                const baseMarkup = renderSegment(working.slice(0, scriptIndex));
                let decoratedMarkup = baseMarkup;
                let cursor = scriptIndex;

                while (cursor < working.length) {
                    const operator = working[cursor];
                    if (operator !== "^" && operator !== "_") {
                        break;
                    }

                    const operand = readScriptOperand(working, cursor + 1);
                    const scriptMarkup = renderSegment(operand.expression);
                    decoratedMarkup += operator === "^"
                        ? `<sup>${operand.wrapped ? wrapParenthesized(scriptMarkup) : scriptMarkup}</sup>`
                        : `<sub>${operand.wrapped ? wrapParenthesized(scriptMarkup) : scriptMarkup}</sub>`;
                    cursor = operand.nextIndex;
                }

                if (cursor < working.length) {
                    decoratedMarkup += renderSegment(working.slice(cursor));
                }

                return outerParentheses.wrapped ? wrapParenthesized(decoratedMarkup) : decoratedMarkup;
            }

            const plainMarkup = escapeHtml(normalizeEquationText(working));
            return outerParentheses.wrapped ? wrapParenthesized(plainMarkup) : plainMarkup;
        };

        return renderSegment(expression);
    };

    const createEquationBlock = (expression) => {
        const wrapper = document.createElement("div");
        wrapper.className = "notebook-equation-block";
        wrapper.contentEditable = "false";
        wrapper.setAttribute("data-equation-source", String(expression || "").trim());

        const badge = document.createElement("span");
        badge.className = "equation-badge";
        badge.textContent = "Equation";

        const equation = document.createElement("div");
        equation.className = "equation-render";
        equation.innerHTML = renderEquationMarkup(expression);

        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "inline-widget-remove";
        remove.setAttribute("data-remove-inline-widget", "true");
        remove.textContent = "-";

        wrapper.append(badge, equation, remove);
        return wrapper;
    };

    const createTableBlockFromMatrix = (matrix) => {
        const rows = matrix.filter((row) => row.some((cell) => String(cell || "").trim() !== ""));
        if (!rows.length) {
            return createTableBlock();
        }

        const wrapper = document.createElement("div");
        wrapper.className = "notebook-inline-table";

        const table = document.createElement("table");
        table.className = "notebook-rich-table";
        const body = document.createElement("tbody");
        rows.forEach((rowValues) => {
            const row = document.createElement("tr");
            rowValues.forEach((value) => {
                const cell = document.createElement("td");
                cell.contentEditable = "true";
                cell.textContent = String(value || "");
                row.appendChild(cell);
            });
            body.appendChild(row);
        });

        table.appendChild(body);
        wrapper.appendChild(table);
        normalizeTable(table);
        return wrapper;
    };

    const extractTableMatrixFromHtml = (html) => {
        const parser = new DOMParser();
        const documentNode = parser.parseFromString(html, "text/html");
        const table = documentNode.querySelector("table");
        if (!table) {
            return [];
        }

        return Array.from(table.rows).map((row) =>
            Array.from(row.cells).map((cell) => cell.textContent?.replace(/\u00a0/g, " ").trim() || "")
        );
    };

    const parseDelimitedTable = (text) => {
        const lines = String(text || "")
            .replace(/\r/g, "")
            .split("\n")
            .filter((line) => line.trim() !== "");

        if (!lines.length || !lines.some((line) => line.includes("\t"))) {
            return [];
        }

        return lines.map((line) => line.split("\t"));
    };

    const normalizeInventoryQuery = (value) => value.toUpperCase().replace(/\s+/g, "");

    const updateExperimentCodeSuggestion = async () => {
        if (!clientInput || !projectNameInput || !experimentCodeInput || !recordRoot.dataset.experimentCodeUrl) {
            return;
        }

        const client = clientInput.value.trim();
        const projectName = projectNameInput.value.trim();
        if (!client || !projectName) {
            experimentCodeInput.value = "";
            return;
        }

        const recordId = recordRoot.querySelector("input[name='Id']")?.value || "";
        const url = new URL(recordRoot.dataset.experimentCodeUrl, window.location.origin);
        url.searchParams.set("client", client);
        url.searchParams.set("projectName", projectName);
        if (recordId) {
            url.searchParams.set("recordId", recordId);
        }

        try {
            const response = await fetch(url.toString(), {
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });
            if (!response.ok) {
                return;
            }

            const payload = await response.json();
            experimentCodeInput.value = payload?.experimentCode || "";
        } catch {
        }
    };

    const scheduleExperimentCodeSuggestion = () => {
        if (experimentCodeRequestTimer) {
            window.clearTimeout(experimentCodeRequestTimer);
        }

        experimentCodeRequestTimer = window.setTimeout(() => {
            updateExperimentCodeSuggestion();
        }, 180);
    };

    const getInventoryMatches = (query) => {
        const normalizedQuery = normalizeInventoryQuery(query || "");
        const matches = inventoryOptions.filter((item) => {
            if (!normalizedQuery) {
                return true;
            }

            return item.code.replace(/\s+/g, "").includes(normalizedQuery)
                || item.label.toUpperCase().replace(/\s+/g, "").includes(normalizedQuery)
                || item.detailPath.toUpperCase().includes((query || "").trim().toUpperCase());
        });

        return matches
            .sort((left, right) => {
                const leftExact = left.code === normalizedQuery ? 0 : 1;
                const rightExact = right.code === normalizedQuery ? 0 : 1;
                if (leftExact !== rightExact) {
                    return leftExact - rightExact;
                }

                return left.label.localeCompare(right.label);
            })
            .slice(0, 8);
    };

    const getNotebookMatches = (query) => {
        const normalizedQuery = normalizeInventoryQuery(query || "");
        const matches = notebookOptions.filter((item) => {
            if (!normalizedQuery) {
                return true;
            }

            return item.code.replace(/\s+/g, "").includes(normalizedQuery)
                || item.label.toUpperCase().replace(/\s+/g, "").includes(normalizedQuery)
                || item.title.toUpperCase().replace(/\s+/g, "").includes(normalizedQuery)
                || item.detailPath.toUpperCase().includes((query || "").trim().toUpperCase());
        });

        return matches
            .sort((left, right) => {
                const leftExact = left.code === normalizedQuery ? 0 : 1;
                const rightExact = right.code === normalizedQuery ? 0 : 1;
                if (leftExact !== rightExact) {
                    return leftExact - rightExact;
                }

                return left.label.localeCompare(right.label);
            })
            .slice(0, 8);
    };

    const renderInventoryResults = (query) => {
        if (!inventorySearchResults) {
            return [];
        }

        const matches = getInventoryMatches(query);
        inventorySearchResults.innerHTML = "";

        if (!matches.length) {
            const emptyState = document.createElement("div");
            emptyState.className = "inventory-search-empty";
            emptyState.textContent = "No inventory items match that text yet.";
            inventorySearchResults.appendChild(emptyState);
            return matches;
        }

        matches.forEach((option) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "inventory-search-option";
            button.dataset.inventoryId = option.value;

            const code = document.createElement("strong");
            code.textContent = option.code;

            const label = document.createElement("span");
            label.textContent = option.label;

            button.append(code, label);
            inventorySearchResults.appendChild(button);
        });

        return matches;
    };

    const setInventorySearchVisible = (visible) => {
        if (!inventorySearch) {
            return;
        }

        inventorySearch.hidden = !visible;
        insertInventoryButton?.classList.toggle("is-active", visible);
        if (visible) {
            setTableContextVisible(false);
        }

        if (!visible) {
            if (inventorySearchInput) {
                inventorySearchInput.value = "";
            }

            if (inventoryHoursInput) {
                inventoryHoursInput.value = "";
            }

            inventorySearchResults?.replaceChildren();
            editor.focus();
            return;
        }

        renderInventoryResults(inventorySearchInput?.value || "");
        inventorySearchInput?.focus();
        inventorySearchInput?.select();
    };

    const renderNotebookResults = (query) => {
        if (!recordSearchResults) {
            return [];
        }

        const matches = getNotebookMatches(query);
        recordSearchResults.innerHTML = "";

        if (!matches.length) {
            const emptyState = document.createElement("div");
            emptyState.className = "inventory-search-empty";
            emptyState.textContent = "No notebook entries match that text yet.";
            recordSearchResults.appendChild(emptyState);
            return matches;
        }

        matches.forEach((option) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "inventory-search-option";
            button.dataset.recordId = option.value;

            const code = document.createElement("strong");
            code.textContent = option.code;

            const label = document.createElement("span");
            label.textContent = option.label;

            button.append(code, label);
            recordSearchResults.appendChild(button);
        });

        return matches;
    };

    const setRecordSearchVisible = (visible) => {
        if (!recordSearch) {
            return;
        }

        recordSearch.hidden = !visible;
        insertRecordLinkButton?.classList.toggle("is-active", visible);
        if (visible) {
            setTableContextVisible(false);
        }

        if (!visible) {
            if (recordSearchInput) {
                recordSearchInput.value = "";
            }

        recordSearchResults?.replaceChildren();
        editor.focus();
        return;
    }

        renderNotebookResults(recordSearchInput?.value || "");
        recordSearchInput?.focus();
        recordSearchInput?.select();
    };

    const setEquationEditorVisible = (visible) => {
        if (!equationEditor) {
            return;
        }

        equationEditor.hidden = !visible;
        insertEquationButton?.classList.toggle("is-active", visible);
        if (visible) {
            setTableContextVisible(false);
        }

        if (!visible) {
            if (equationInput) {
                equationInput.value = "";
            }

            editor.focus();
            return;
        }

        equationInput?.focus();
        equationInput?.select();
    };

    const insertInventoryOption = (option) => {
        const usageValue = inventoryHoursInput?.value?.trim() || "";
        const usageHours = usageValue === "" ? null : Number(usageValue);
        insertBlockAtCursor(createInventoryWidget(option, Number.isFinite(usageHours) ? usageHours : null));
        setInventorySearchVisible(false);
    };

    const insertNotebookOption = (option) => {
        insertBlockAtCursor(createRecordLinkWidget(option));
        setRecordSearchVisible(false);
    };

    const insertEquation = () => {
        const source = equationInput?.value?.trim() || "";
        if (!source) {
            return;
        }

        insertBlockAtCursor(createEquationBlock(source));
        setEquationEditorVisible(false);
    };

    const getCellContext = (cell = activeCell) => {
        if (!cell) {
            return null;
        }

        const table = cell.closest("table");
        const body = table?.tBodies[0];
        const row = cell.parentElement;
        if (!table || !body || !row) {
            return null;
        }

        const rows = Array.from(body.rows);
        const rowIndex = rows.indexOf(row);
        const cellIndex = Array.from(row.cells).indexOf(cell);
        return rowIndex >= 0 && cellIndex >= 0 ? { table, body, row, rowIndex, cellIndex, rows } : null;
    };

    const getSelectedContext = () => {
        const cells = selectedCells.length ? selectedCells : (activeCell ? [activeCell] : []);
        if (!cells.length) {
            return null;
        }

        const table = cells[0].closest("table");
        const contexts = cells
            .filter((cell) => cell.closest("table") === table)
            .map((cell) => getCellContext(cell))
            .filter(Boolean);

        if (!contexts.length || !table) {
            return null;
        }

        return { table, contexts };
    };

    const selectRect = (startCell, endCell) => {
        const start = getCellContext(startCell);
        const end = getCellContext(endCell);
        if (!start || !end || start.table !== end.table) {
            return;
        }

        const minRow = Math.min(start.rowIndex, end.rowIndex);
        const maxRow = Math.max(start.rowIndex, end.rowIndex);
        const minCol = Math.min(start.cellIndex, end.cellIndex);
        const maxCol = Math.max(start.cellIndex, end.cellIndex);
        const cells = [];
        for (let rowIndex = minRow; rowIndex <= maxRow; rowIndex += 1) {
            const row = start.rows[rowIndex];
            for (let cellIndex = minCol; cellIndex <= maxCol; cellIndex += 1) {
                if (row?.cells[cellIndex]) {
                    cells.push(row.cells[cellIndex]);
                }
            }
        }
        setSelectedCells(cells);
    };

    const hideTableContext = () => {
        setTableContextVisible(false);
    };

    const ensureTableSelection = () => {
        if (activeCell && activeCell.closest("table")) {
            return activeCell;
        }

        const firstExistingCell = editor.querySelector(".notebook-rich-table td");
        if (firstExistingCell) {
            setSelectedCells([firstExistingCell]);
            placeCaretInCell(firstExistingCell);
            return firstExistingCell;
        }

        const block = createTableBlock();
        insertBlockAtCursor(block);
        const firstCell = block.querySelector("td");
        if (firstCell) {
            setSelectedCells([firstCell]);
            placeCaretInCell(firstCell);
        }
        syncHiddenFields();
        return firstCell;
    };

    const addRow = (insertBelow) => {
        const context = getCellContext();
        if (!context) return;
        const columnCount = Math.max(1, context.row.cells.length);
        const row = document.createElement("tr");
        for (let index = 0; index < columnCount; index += 1) {
            const cell = document.createElement("td");
            cell.contentEditable = "true";
            row.appendChild(cell);
        }
        const reference = context.body.rows[insertBelow ? context.rowIndex + 1 : context.rowIndex] || null;
        context.body.insertBefore(row, reference);
        ensureColGroup(context.table);
        setSelectedCells([row.cells[Math.min(context.cellIndex, row.cells.length - 1)]]);
        placeCaretInCell(row.cells[Math.min(context.cellIndex, row.cells.length - 1)]);
        syncHiddenFields();
    };

    const addColumn = (insertRight) => {
        const context = getCellContext();
        if (!context) return;
        Array.from(context.body.rows).forEach((row) => {
            const cell = document.createElement("td");
            cell.contentEditable = "true";
            const referenceIndex = insertRight ? context.cellIndex + 1 : context.cellIndex;
            row.insertBefore(cell, row.cells[referenceIndex] || null);
        });
        ensureColGroup(context.table);
        const targetCell = context.body.rows[context.rowIndex].cells[insertRight ? context.cellIndex + 1 : context.cellIndex];
        if (targetCell) {
            setSelectedCells([targetCell]);
            placeCaretInCell(targetCell);
        }
        syncHiddenFields();
    };

    const clearSelectionCells = () => {
        const cells = selectedCells.length ? selectedCells : (activeCell ? [activeCell] : []);
        cells.forEach((cell) => {
            cell.innerHTML = "";
        });
        if (cells[0]) {
            placeCaretInCell(cells[0]);
        }
        syncHiddenFields();
    };

    const deleteSelectedRows = () => {
        const context = getSelectedContext();
        if (!context) return;
        const rowIndexes = [...new Set(context.contexts.map((item) => item.rowIndex))].sort((a, b) => b - a);
        const body = context.contexts[0].body;
        if (rowIndexes.length >= body.rows.length) {
            Array.from(body.rows).forEach((row) => Array.from(row.cells).forEach((cell) => { cell.innerHTML = ""; }));
            const firstCell = body.rows[0]?.cells[0];
            if (firstCell) {
                setSelectedCells([firstCell]);
                placeCaretInCell(firstCell);
            }
        } else {
            rowIndexes.forEach((rowIndex) => body.rows[rowIndex]?.remove());
            const nextRow = body.rows[Math.min(rowIndexes[rowIndexes.length - 1], body.rows.length - 1)] || body.rows[0];
            const nextCell = nextRow?.cells[0];
            if (nextCell) {
                setSelectedCells([nextCell]);
                placeCaretInCell(nextCell);
            }
        }
        ensureColGroup(context.table);
        syncHiddenFields();
    };

    const deleteSelectedColumns = () => {
        const context = getSelectedContext();
        if (!context) return;
        const columnIndexes = [...new Set(context.contexts.map((item) => item.cellIndex))].sort((a, b) => b - a);
        const body = context.contexts[0].body;
        const columnCount = body.rows[0]?.cells.length ?? 0;
        if (!columnCount) return;

        if (columnIndexes.length >= columnCount) {
            Array.from(body.rows).forEach((row) => Array.from(row.cells).forEach((cell) => { cell.innerHTML = ""; }));
            const firstCell = body.rows[0]?.cells[0];
            if (firstCell) {
                setSelectedCells([firstCell]);
                placeCaretInCell(firstCell);
            }
        } else {
            Array.from(body.rows).forEach((row) => {
                columnIndexes.forEach((columnIndex) => row.cells[columnIndex]?.remove());
            });
            const firstCell = body.rows[0]?.cells[0];
            if (firstCell) {
                setSelectedCells([firstCell]);
                placeCaretInCell(firstCell);
            }
        }
        ensureColGroup(context.table);
        syncHiddenFields();
    };

    const deleteTable = () => {
        const context = getCellContext();
        if (!context) return;
        const wrapper = context.table.closest(".notebook-inline-table") || context.table;
        const paragraph = document.createElement("p");
        paragraph.innerHTML = "<br>";
        wrapper.parentElement?.insertBefore(paragraph, wrapper.nextSibling);
        wrapper.remove();
        setSelectedCells([]);
        hideTableContext();
        syncHiddenFields();
    };

    const moveToAdjacentCell = (direction) => {
        const context = getCellContext();
        if (!context) {
            return false;
        }

        let nextRowIndex = context.rowIndex;
        let nextCellIndex = context.cellIndex + direction;
        const currentRowLength = context.row.cells.length;

        if (direction > 0 && nextCellIndex >= currentRowLength) {
            nextRowIndex += 1;
            nextCellIndex = 0;
        }

        if (direction < 0 && nextCellIndex < 0) {
            nextRowIndex -= 1;
            const prevRow = context.rows[nextRowIndex];
            nextCellIndex = prevRow ? prevRow.cells.length - 1 : 0;
        }

        if (nextRowIndex >= context.rows.length) {
            addRow(true);
            return true;
        }

        if (nextRowIndex < 0) {
            return false;
        }

        const nextCell = context.rows[nextRowIndex]?.cells[nextCellIndex];
        if (!nextCell) {
            return false;
        }

        setSelectedCells([nextCell]);
        placeCaretInCell(nextCell);
        return true;
    };

    const isNearRightEdge = (cell, event) => {
        const rect = cell.getBoundingClientRect();
        return Math.abs(rect.right - event.clientX) <= 8 && event.clientX > rect.left + 24;
    };

    const beginResize = (cell, event) => {
        const context = getCellContext(cell);
        if (!context) return;
        const cols = ensureColGroup(context.table);
        const width = cols[context.cellIndex]?.getBoundingClientRect().width || cell.getBoundingClientRect().width;
        resizeState = {
            table: context.table,
            cols,
            columnIndex: context.cellIndex,
            startX: event.clientX,
            startWidth: width
        };
        document.body.style.cursor = "col-resize";
        event.preventDefault();
    };

    const applyResize = (event) => {
        if (!resizeState) return;
        const nextWidth = Math.max(90, resizeState.startWidth + (event.clientX - resizeState.startX));
        resizeState.cols[resizeState.columnIndex].style.width = `${nextWidth}px`;
    };

    const endResize = () => {
        if (!resizeState) return;
        resizeState = null;
        document.body.style.cursor = "";
        syncHiddenFields();
    };

    insertInventoryButton?.addEventListener("click", () => {
        if (!inventorySearch) {
            return;
        }

        saveEditorRangeFromSelection();
        setRecordSearchVisible(false);
        setEquationEditorVisible(false);
        setInventorySearchVisible(inventorySearch.hidden);
    });

    insertRecordLinkButton?.addEventListener("click", () => {
        if (!recordSearch) {
            return;
        }

        saveEditorRangeFromSelection();
        setInventorySearchVisible(false);
        setEquationEditorVisible(false);
        setRecordSearchVisible(recordSearch.hidden);
    });

    insertTableButton?.addEventListener("click", () => {
        setInventorySearchVisible(false);
        setRecordSearchVisible(false);
        setEquationEditorVisible(false);
        if (isTableContextVisible()) {
            hideTableContext();
            return;
        }

        ensureTableSelection();
        setTableContextVisible(true);
    });

    insertEquationButton?.addEventListener("click", () => {
        if (!equationEditor) {
            return;
        }

        saveEditorRangeFromSelection();
        setInventorySearchVisible(false);
        setRecordSearchVisible(false);
        setEquationEditorVisible(equationEditor.hidden);
    });

    insertPhotoButton?.addEventListener("click", () => {
        setInventorySearchVisible(false);
        setRecordSearchVisible(false);
        setEquationEditorVisible(false);
        photoInput?.click();
    });

    templateSelect?.addEventListener("change", () => {
        const selectedTemplateId = templateSelect.value || "";
        if (!selectedTemplateId) {
            lastTemplateId = "";
            return;
        }

        const applied = applyTemplateContent(selectedTemplateId, true);
        if (!applied) {
            templateSelect.value = lastTemplateId;
            return;
        }

        lastTemplateId = selectedTemplateId;
    });

    closeInventorySearchButton?.addEventListener("click", () => {
        setInventorySearchVisible(false);
    });

    closeRecordSearchButton?.addEventListener("click", () => {
        setRecordSearchVisible(false);
    });

    closeEquationEditorButton?.addEventListener("click", () => {
        setEquationEditorVisible(false);
    });

    insertEquationConfirmButton?.addEventListener("click", () => {
        insertEquation();
    });

    inventorySearchInput?.addEventListener("input", () => {
        renderInventoryResults(inventorySearchInput.value);
    });

    inventorySearchInput?.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            event.preventDefault();
            setInventorySearchVisible(false);
            return;
        }

        if (event.key !== "Enter") {
            return;
        }

        event.preventDefault();
        const [firstMatch] = getInventoryMatches(inventorySearchInput.value);
        if (firstMatch) {
            insertInventoryOption(firstMatch);
        }
    });

    inventorySearchResults?.addEventListener("click", (event) => {
        const optionButton = event.target.closest(".inventory-search-option");
        const option = inventoryOptions.find((item) => item.value === optionButton?.dataset.inventoryId);
        if (option) {
            insertInventoryOption(option);
        }
    });

    recordSearchInput?.addEventListener("input", () => {
        renderNotebookResults(recordSearchInput.value);
    });

    recordSearchInput?.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            event.preventDefault();
            setRecordSearchVisible(false);
            return;
        }

        if (event.key !== "Enter") {
            return;
        }

        event.preventDefault();
        const [firstMatch] = getNotebookMatches(recordSearchInput.value);
        if (firstMatch) {
            insertNotebookOption(firstMatch);
        }
    });

    recordSearchResults?.addEventListener("click", (event) => {
        const optionButton = event.target.closest(".inventory-search-option");
        const option = notebookOptions.find((item) => item.value === optionButton?.dataset.recordId);
        if (option) {
            insertNotebookOption(option);
        }
    });

    equationInput?.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            event.preventDefault();
            setEquationEditorVisible(false);
            return;
        }

        if (event.key === "Enter") {
            event.preventDefault();
            insertEquation();
        }
    });

    photoInput?.addEventListener("change", () => {
        const [file] = photoInput.files || [];
        if (!file) {
            return;
        }

        const reader = new FileReader();
        reader.onload = () => {
            insertBlockAtCursor(createPhotoFigure(reader.result));
        };
        reader.readAsDataURL(file);
        photoInput.value = "";
    });

    editor.addEventListener("paste", (event) => {
        const targetCell = event.target.closest?.("td");
        if (targetCell) {
            return;
        }

        const html = event.clipboardData?.getData("text/html") || "";
        const text = event.clipboardData?.getData("text/plain") || "";
        const htmlMatrix = html ? extractTableMatrixFromHtml(html) : [];
        const textMatrix = parseDelimitedTable(text);

        if (!htmlMatrix.length && !textMatrix.length) {
            return;
        }

        event.preventDefault();
        setInventorySearchVisible(false);
        setRecordSearchVisible(false);
        setEquationEditorVisible(false);
        insertBlockAtCursor(createTableBlockFromMatrix(htmlMatrix.length ? htmlMatrix : textMatrix));
    });

    editor.addEventListener("mousedown", (event) => {
        const removeButton = event.target.closest("[data-remove-inline-widget]");
        if (removeButton) {
            event.preventDefault();
            removeButton.closest("[data-inventory-id], [data-record-link-id], [data-equation-source]")?.remove();
            syncHiddenFields();
            return;
        }

        const cell = event.target.closest("td");
        if (!cell || !editor.contains(cell)) {
            return;
        }

        if (isNearRightEdge(cell, event)) {
            beginResize(cell, event);
            return;
        }

        if (event.shiftKey && activeCell && activeCell.closest("table") === cell.closest("table")) {
            selectRect(activeCell, cell);
            event.preventDefault();
        } else {
            setSelectedCells([cell]);
        }
    });

    editor.addEventListener("mousemove", (event) => {
        if (resizeState) {
            applyResize(event);
            return;
        }

        const cell = event.target.closest("td");
        editor.style.cursor = cell && isNearRightEdge(cell, event) ? "col-resize" : "text";
    });

    document.addEventListener("mouseup", () => {
        endResize();
    });

    document.addEventListener("selectionchange", saveEditorRangeFromSelection);

    const clearDragIndicators = () => {
        editor.querySelectorAll(".drag-before, .drag-after, .is-dragging-note-block").forEach((element) => {
            element.classList.remove("drag-before", "drag-after", "is-dragging-note-block");
        });
    };

    editor.addEventListener("dragstart", (event) => {
        const handle = event.target.closest(dragHandleSelector);
        const block = handle?.closest(draggableBlockSelector);
        if (!handle || !block) {
            event.preventDefault();
            return;
        }

        draggedNoteBlock = block;
        clearDragIndicators();
        draggedNoteBlock.classList.add("is-dragging-note-block");
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", draggedNoteBlock.dataset.noteBlockId || "note-block");
    });

    editor.addEventListener("dragover", (event) => {
        if (!draggedNoteBlock) {
            return;
        }

        event.preventDefault();
        clearDragIndicators();
        const targetBlock = event.target.closest(draggableBlockSelector);
        if (targetBlock && targetBlock !== draggedNoteBlock) {
            const targetRect = targetBlock.getBoundingClientRect();
            const insertBefore = event.clientY < targetRect.top + (targetRect.height / 2);
            targetBlock.classList.add(insertBefore ? "drag-before" : "drag-after");
            editor.insertBefore(draggedNoteBlock, insertBefore ? targetBlock : targetBlock.nextSibling);
            return;
        }

        const lastBlock = Array.from(editor.children).filter((child) => child.matches?.(draggableBlockSelector)).at(-1);
        if (lastBlock && lastBlock !== draggedNoteBlock) {
            editor.appendChild(draggedNoteBlock);
        }
    });

    editor.addEventListener("drop", (event) => {
        if (!draggedNoteBlock) {
            return;
        }

        event.preventDefault();
        clearDragIndicators();
        enhanceEditorContent();
        syncHiddenFields();
        draggedNoteBlock = null;
    });

    editor.addEventListener("dragend", () => {
        if (!draggedNoteBlock) {
            return;
        }

        clearDragIndicators();
        enhanceEditorContent();
        syncHiddenFields();
        draggedNoteBlock = null;
    });

    editor.addEventListener("contextmenu", (event) => {
        const cell = event.target.closest("td");
        if (!cell || !editor.contains(cell)) {
            hideTableContext();
            return;
        }

        event.preventDefault();
        if (!selectedCells.includes(cell)) {
            setSelectedCells([cell]);
        }
        setTableContextVisible(true);
    });

    editor.addEventListener("keydown", (event) => {
        const selection = window.getSelection();
        const node = selection?.anchorNode;
        const element = node?.nodeType === Node.ELEMENT_NODE ? node : node?.parentElement;
        const cell = element?.closest?.("td");
        if (!cell || !editor.contains(cell)) {
            return;
        }

        activeCell = cell;

        if (event.key === "Tab") {
            event.preventDefault();
            moveToAdjacentCell(event.shiftKey ? -1 : 1);
            syncHiddenFields();
        }

        if ((event.key === "Delete" || event.key === "Backspace") && selectedCells.length > 1) {
            event.preventDefault();
            clearSelectionCells();
        }
    });

    tableContext.addEventListener("click", (event) => {
        const button = event.target.closest("button[data-table-action]");
        if (!button) {
            return;
        }

        const action = button.dataset.tableAction;
        if (action === "row-above") addRow(false);
        if (action === "row-below") addRow(true);
        if (action === "column-left") addColumn(false);
        if (action === "column-right") addColumn(true);
        if (action === "clear-cells") clearSelectionCells();
        if (action === "delete-rows") deleteSelectedRows();
        if (action === "delete-columns") deleteSelectedColumns();
        if (action === "delete-table") deleteTable();
    });

    window.addEventListener("resize", () => {
        editor.querySelectorAll("img").forEach((image) => applyResponsiveImageSize(image));
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            hideTableContext();
            setInventorySearchVisible(false);
            setRecordSearchVisible(false);
            setEquationEditorVisible(false);
        }
    });

    editor.addEventListener("input", () => {
        saveEditorRangeFromSelection();
        syncHiddenFields();
    });
    recordRoot.addEventListener("submit", syncHiddenFields);
    enhanceEditorContent();
    hideTableContext();
    setInventorySearchVisible(false);
    setRecordSearchVisible(false);
    setEquationEditorVisible(false);
    if ((templateSelect?.value || "") && isEditorEffectivelyEmpty()) {
        applyTemplateContent(templateSelect.value, false);
    }
    if (recordRoot.dataset.initialInventoryCode && isEditorEffectivelyEmpty()) {
        const initialOption = inventoryOptions.find((option) => option.code === recordRoot.dataset.initialInventoryCode.trim().toUpperCase());
        if (initialOption) {
            insertInventoryOption(initialOption);
        }
    }
    clientInput?.addEventListener("input", scheduleExperimentCodeSuggestion);
    projectNameInput?.addEventListener("input", scheduleExperimentCodeSuggestion);
    updateExperimentCodeSuggestion();
    syncHiddenFields();
});
