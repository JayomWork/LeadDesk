document.addEventListener("dragstart", (event) => {
    const handle = event.target instanceof Element ? event.target.closest(".notebook-drag") : null;
    if (!handle || !event.dataTransfer) return;

    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", handle.dataset.taskId || "task");
}, true);
