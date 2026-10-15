// Chat helpers for Blazor (imported by ChatMessageList through IJSRuntime). Scrolling only: the page
// keeps focus management in C# with ElementReference.FocusAsync.

// Scrolls the message log to its last message. Smooth scrolling is skipped for people who ask for less motion.
export function scrollToEnd(element) {
    if (!element) {
        return;
    }

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    element.scrollTo({ top: element.scrollHeight, behavior: reduceMotion ? "auto" : "smooth" });
}
