// Progressive enhancement for the static login page: shows or hides the password.
// The toggle buttons ship hidden and only appear once this module runs, so the form works without it.
const showLabel = "Mostrar contraseña";
const hideLabel = "Ocultar contraseña";

for (const button of document.querySelectorAll("[data-password-toggle]")) {
    const input = document.getElementById(button.getAttribute("aria-controls"));
    if (!input) {
        continue;
    }

    button.dataset.ready = "";

    button.addEventListener("click", () => {
        const reveal = input.type === "password";
        input.type = reveal ? "text" : "password";
        button.setAttribute("aria-pressed", String(reveal));
        button.setAttribute("aria-label", reveal ? hideLabel : showLabel);

        for (const icon of button.querySelectorAll("svg")) {
            icon.classList.toggle("hidden");
        }
    });
}
