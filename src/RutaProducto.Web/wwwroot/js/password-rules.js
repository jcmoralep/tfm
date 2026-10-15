// Progressive enhancement for the static registration page: marks, while the person types, which password rules
// are already met. The server (Identity) checks again on submit, so the form works without this script and the
// list is still readable as plain text.
// The list carries data-password-rules and data-source (the id of the password input); every item carries
// data-rule ("length", "upper", "lower", "digit" or "symbol"), plus data-min for the length. The script sets
// data-met ("true" or "false", absent while the box is empty) and the mark; the page's classes style them.
const checks = {
    length: (value, item) => [...value].length >= Number(item.dataset.min),
    upper: (value) => /\p{Lu}/u.test(value),
    lower: (value) => /\p{Ll}/u.test(value),
    digit: (value) => /\p{Nd}/u.test(value),
    symbol: (value) => /[^\p{L}\p{Nd}]/u.test(value),
};

for (const list of document.querySelectorAll("[data-password-rules]")) {
    const source = document.getElementById(list.dataset.source);
    if (!source) {
        continue;
    }

    const update = () => {
        const typing = source.value.length > 0;

        for (const item of list.querySelectorAll("[data-rule]")) {
            const check = checks[item.dataset.rule];
            const mark = item.querySelector("[data-mark]");

            // An empty box has nothing to judge yet: the rules stay neutral instead of all showing as failed.
            if (!check || !typing) {
                delete item.dataset.met;
                if (mark) {
                    mark.textContent = "•";
                }
                continue;
            }

            const met = check(source.value, item);
            item.dataset.met = String(met);
            if (mark) {
                mark.textContent = met ? "✓" : "✕";
            }
        }
    };

    source.addEventListener("input", update);
    update();
}
