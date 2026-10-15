// Progressive enhancement for the static registration page: tells the person, while they type, whether the two
// passwords match. The server checks again on submit, so the form works without this script.
// The message element carries data-password-match, data-source and data-target (ids of the two inputs) and the
// messages; the script only sets data-state ("empty", "ok" or "bad") and the text, and the page's classes style it.
for (const message of document.querySelectorAll("[data-password-match]")) {
    const source = document.getElementById(message.dataset.source);
    const target = document.getElementById(message.dataset.target);
    if (!source || !target) {
        continue;
    }

    // The element is a live region: the text is only replaced when it really changes, so a screen reader does not
    // repeat the same sentence on every keystroke.
    const show = (state, text) => {
        message.dataset.state = state;
        if (message.textContent !== text) {
            message.textContent = text;
        }
    };

    const update = () => {
        // Nothing is said until the second box has something in it: an empty box is not a mismatch yet.
        if (target.value.length === 0) {
            show("empty", "");
        } else if (source.value === target.value) {
            show("ok", message.dataset.okText);
        } else {
            show("bad", message.dataset.badText);
        }
    };

    source.addEventListener("input", update);
    target.addEventListener("input", update);
    update();
}
