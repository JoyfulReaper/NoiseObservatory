document.addEventListener("DOMContentLoaded", () => {
    const rows = document.querySelectorAll(".event-row[data-detail-id]");

    const toggleRow = row => {
        const detailId = row.dataset.detailId;
        const detailRow = document.getElementById(detailId);

        if (!detailRow) {
            return;
        }

        const isExpanded = row.getAttribute("aria-expanded") === "true";
        row.setAttribute("aria-expanded", (!isExpanded).toString());
        detailRow.hidden = isExpanded;
    };

    rows.forEach(row => {
        row.addEventListener("click", () => toggleRow(row));

        row.addEventListener("keydown", event => {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                toggleRow(row);
            }
        });
    });
});
