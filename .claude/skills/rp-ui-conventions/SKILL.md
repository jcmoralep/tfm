---
name: rp-ui-conventions
description: "Trigger: .razor, Blazor, vista, pantalla, componente, CSS, Tailwind, JavaScript, copy en español, formulario. Build Ruta de Producto views with its UI rules and known Blazor pitfalls."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.0"
---

## Activation Contract

Load before editing any `.razor`, `wwwroot/js`, `Styles/*.css`, or user-facing Spanish text.

## Hard Rules

- Views use CSS classes only: no `style=`, no `<script>`, no `onclick=` strings. Blazor `@onclick`/`@bind` to C# are fine.
- JavaScript only as ES modules in `wwwroot/js` through `IJSRuntime`. Allowed today: `chat.js`, `password-toggle.js`, `password-match.js`, `password-rules.js`, `confirm-dialog.js`, `print.js`. The single third-party exception is Cloudflare Turnstile `api.js`, only inside `Components/Shared/CaptchaWidget.razor`. A new module is a decision: add it to AGENTS.md §2/§4.5 in the same change.
- Visual tokens live only in `src/RutaProducto.Web/Styles/theme.css`; component styles in `Styles/app.css`. No hex colors in views. Style reference: `DESIGN.md` (Intercom) with the approved mockup winning on palette.
- Copy in Spanish, neutral, addressing the user as "usted" in the app and "tú" on auth pages (match the page). No jargon for junior users (RF-41). No BMAD in product names.
- Feedback: success/saved messages in green, required fields with a red asterisk, a visible loading state for slow actions, confirmation panels that name the action and its consequence.
- Components inject `IMediator` and send commands/queries; no business logic in views. Use `IDbContextFactory`, never a `DbContext` in components.
- Accessibility: alerts in `role=alert`, statuses in `role=status`, labelled fields, focus moved deliberately.

## Decision Gates

| Situation | Rule |
|---|---|
| Static SSR page must answer 404 | Redirect to `/not-found?codigo=404`; never `Navigation.NotFound()` there |
| Restoring focus to an element that may disappear | Catch `JSException` too (element gone) |
| Interactive vs static page | Auth pages are static SSR forms; app pages are Interactive Server |
| Need a dynamic visual (progress) | Native element (`<progress>`) or a finite set of literal Tailwind classes |

## Execution Steps

1. Read the closest existing page/component and copy its structure and classes.
2. Write the view; keep logic in handlers.
3. Audit: grep the changed views for ` style=`, `<script`, `onclick=`, `#[0-9a-fA-F]{3,6}` and new `JSRuntime` uses.
4. Walk it in Chrome (`rp-local-walkthrough`) on desktop and a narrow width.

## Output Contract

Return the files touched, the audit grep result (must be empty or justified), and the walkthrough result.

## References

- `references/pitfalls.md` — Blazor issues already met in this repo.
- `DESIGN.md`, `AGENTS.md` §2 and §4.5, `docs/design/`.
