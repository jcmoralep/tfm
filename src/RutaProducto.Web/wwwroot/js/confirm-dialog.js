// Opens the confirmation <dialog> as a real modal (imported by ConfirmPanel through IJSRuntime).
// showModal() makes the rest of the page inert, keeps focus inside the dialog and draws the backdrop;
// the browser's own Escape ("cancel") and a click on the backdrop are turned into a call to the component.
export function open(dialog, handler) {
    if (!dialog) {
        return;
    }

    // The component decides whether closing is allowed (it is not while the action runs), so the native close is blocked.
    dialog.addEventListener("cancel", (event) => {
        event.preventDefault();
        handler.invokeMethodAsync("Dismiss");
    });

    // The dialog has no padding of its own, so a click whose target is the dialog itself landed on the backdrop.
    dialog.addEventListener("click", (event) => {
        if (event.target === dialog) {
            handler.invokeMethodAsync("Dismiss");
        }
    });

    // Blazor removes the dialog from the page instead of closing it, so focus goes back to the button that opened it
    // (when that button is still there) as soon as the dialog disappears.
    const opener = document.activeElement;
    const watcher = new MutationObserver(() => {
        if (!dialog.isConnected) {
            watcher.disconnect();
            if (opener?.isConnected) {
                opener.focus();
            }
        }
    });
    watcher.observe(document.body, { childList: true, subtree: true });

    dialog.showModal();
    dialog.querySelector("[data-dialog-title]")?.focus();
}
