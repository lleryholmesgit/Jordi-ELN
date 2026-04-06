document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector("[data-column-layout-editor]");
    if (!root) {
        return;
    }

    const table = root.querySelector("[data-inventory-table]");
    const hiddenColumns = root.querySelector("[data-hidden-columns]");
    const form = root.querySelector("[data-layout-form]");
    const saveUrl = root.dataset.saveUrl;
    const token = form?.querySelector('input[name="__RequestVerificationToken"]')?.value;
    let dragKey = null;

    if (!table || !form || !saveUrl || !token) {
        return;
    }

    const getHeaderCells = () => Array.from(table.querySelectorAll("thead th[data-column-key]"));
    const getCellsByKey = (key) => Array.from(table.querySelectorAll(`[data-column-key="${key}"]`));

    const renderHiddenChips = () => {
        if (!hiddenColumns) {
            return;
        }

        hiddenColumns.innerHTML = "";
        getHeaderCells()
            .filter((cell) => cell.classList.contains("is-hidden-column"))
            .forEach((cell) => {
                const chip = document.createElement("button");
                chip.type = "button";
                chip.className = "hidden-column-chip";
                chip.dataset.key = cell.dataset.columnKey || "";
                chip.textContent = cell.dataset.columnLabel || chip.dataset.key;
                hiddenColumns.appendChild(chip);
            });
    };

    const serializeLayout = () => getHeaderCells().map((cell, index) => ({
        key: cell.dataset.columnKey || "",
        position: index,
        isVisible: !cell.classList.contains("is-hidden-column")
    }));

    const persistLayout = async () => {
        const layout = serializeLayout();
        const payload = new URLSearchParams();
        payload.append("__RequestVerificationToken", token);
        layout.forEach((column, index) => {
            payload.append(`columns[${index}].Key`, column.key);
            payload.append(`columns[${index}].Position`, String(column.position));
            payload.append(`columns[${index}].IsVisible`, String(column.isVisible).toLowerCase());
        });

        await fetch(saveUrl, {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                "RequestVerificationToken": token,
                "X-Requested-With": "XMLHttpRequest"
            },
            body: payload.toString(),
            credentials: "same-origin"
        });
    };

    const moveColumn = (draggedKey, targetKey) => {
        if (!draggedKey || !targetKey || draggedKey === targetKey) {
            return;
        }

        const headerRow = table.querySelector("thead tr");
        if (!headerRow) {
            return;
        }

        const draggedHeader = headerRow.querySelector(`th[data-column-key="${draggedKey}"]`);
        const targetHeader = headerRow.querySelector(`th[data-column-key="${targetKey}"]`);
        if (!draggedHeader || !targetHeader) {
            return;
        }

        headerRow.insertBefore(draggedHeader, targetHeader);
        table.querySelectorAll("tbody tr").forEach((row) => {
            const draggedCell = row.querySelector(`td[data-column-key="${draggedKey}"]`);
            const targetCell = row.querySelector(`td[data-column-key="${targetKey}"]`);
            if (draggedCell && targetCell) {
                row.insertBefore(draggedCell, targetCell);
            }
        });
    };

    table.addEventListener("dragstart", (event) => {
        const header = event.target.closest("th[data-column-key]");
        if (!header || header.classList.contains("is-hidden-column")) {
            return;
        }

        dragKey = header.dataset.columnKey || null;
        header.classList.add("is-dragging");
    });

    table.addEventListener("dragend", async (event) => {
        event.target.closest("th[data-column-key]")?.classList.remove("is-dragging");
        dragKey = null;
        renderHiddenChips();
        await persistLayout();
    });

    table.addEventListener("dragover", (event) => {
        const header = event.target.closest("th[data-column-key]");
        if (!dragKey || !header || header.classList.contains("is-hidden-column")) {
            return;
        }

        event.preventDefault();
        moveColumn(dragKey, header.dataset.columnKey || "");
    });

    table.addEventListener("click", async (event) => {
        const hideButton = event.target.closest("[data-hide-column]");
        if (!hideButton) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        const header = hideButton.closest("th[data-column-key]");
        const key = header?.dataset.columnKey;
        if (!key) {
            return;
        }

        getCellsByKey(key).forEach((cell) => cell.classList.add("is-hidden-column"));
        renderHiddenChips();
        await persistLayout();
    });

    hiddenColumns?.addEventListener("click", async (event) => {
        const chip = event.target.closest(".hidden-column-chip");
        const key = chip?.dataset.key;
        if (!key) {
            return;
        }

        getCellsByKey(key).forEach((cell) => cell.classList.remove("is-hidden-column"));
        renderHiddenChips();
        await persistLayout();
    });

    renderHiddenChips();
});
