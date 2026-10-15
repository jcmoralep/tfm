# Proposal: Registration hardening

## Intent

`/registro` is open to anyone (RF-67) with no throttling, no bot check, no email ownership proof and no way to recover a forgotten password. Before the platform is exposed publicly (Railway), add these protections as configuration switches that are **off by default**, so behavior is unchanged until the owner provides keys and turns them on. Outside the PROPUESTA-MVP §7 step order: it closes the RF-67 follow-ups.

## Scope

### In Scope
- S1 Rate limiter on the POSTs of `/login`, `/registro` (and later recovery pages), per IP, Spanish 429 page.
- S2 `ICaptchaVerifier` + Cloudflare Turnstile on `/registro` (and S5 forgot page), server-side `siteverify`.
- S3 `IEmailSender` + SMTP sender (`System.Net.Mail`, no new package) + background queue + Spanish templates.
- S4 Email confirmation on registration, confirm page, resend page, login message for unconfirmed accounts.
- S5 Forgot/reset password pages with uniform responses.

### Out of Scope
- Forwarded headers, Data Protection key persistence, Dockerfile (Railway-prep change, AGENTS §6.9).
- HTTP-API email providers, MailKit, invitation codes, captcha on login, roles/admin (RF-03).

## Capabilities

### New Capabilities
- `authentication`: registration, sign-in throttling, captcha, email confirmation, password recovery.

### Modified Capabilities
- None (`openspec/specs/` only has `initiatives`).

## Approach

| Switch (default) | Startup validation |
|---|---|
| `RateLimiting:Enabled` (`false`), `PermitLimit` 10, `WindowSeconds` 60 | positive numbers |
| `Captcha:Provider` (`None`\|`Turnstile`) | Turnstile needs `Turnstile:SiteKey`, `Turnstile__SecretKey` |
| `Email:Provider` (`None`\|`Smtp`) | Smtp needs `Smtp:Host`, `Port`, `From`, `Email:PublicBaseUrl`; `Smtp__Password` secret |
| `Email:RequireConfirmedEmail` (`false`) | `true` with `Email:Provider=None` fails |

Copies the `AssistantProviderSetting`/`StorageOptions` pattern: empty = safe default, unknown value lists valid values, errors name the env variable, never the value. Turnstile widget: one `CaptchaWidget.razor` with `div.cf-turnstile` + the official `api.js` script, implicit render; **documented exception** in AGENTS §2: the only third-party script, only when the provider is on. Enumeration: `NotAllowed` reported only after `CheckPasswordAsync` succeeds; duplicate email gets the same result as a new one when confirmation is on; links built from `Email:PublicBaseUrl`, never `Host`; tokens and emails never logged.

| Slice | Content | Est. lines |
|---|---|---|
| S1 | limiter options, global partitioned limiter, 429 page, tests, docs | ~220 |
| S2 | verifier seam, Turnstile + None, widget, register wiring, tests, docs | ~350 |
| S3 | sender seam, SMTP + None, queue, templates, options, tests | ~320 |
| S4 | token providers, confirm/resend commands and pages, register/login changes, tests | ~390 |
| S5 | forgot/reset commands and pages, reset token lifespan 2 h, tests, docs | ~380 |

## Requirements (REQUISITOS.md)
- Amend RF-67 (duplicate email and auto sign-in depend on confirmation; pending list updated); RNF-02 (no emails/tokens/links in logs); RNF-03 (Turnstile and SMTP secrets).
- Add RF-78 throttling, RF-79 captcha, RF-80 email provider switch, RF-81 confirmation + resend, RF-82 password recovery.

## Affected Areas

| Area | Impact |
|---|---|
| `Application/Abstractions/{Authentication,Captcha,Email}` | Modified/New |
| `Application/Features/Authentication/*` | Modified/New |
| `Infrastructure/{Identity,Captcha,Email}`, `DependencyInjection.cs` | Modified/New |
| `Web/Program.cs`, `Web/Components/Pages/*` (login, registro, confirm, recover) | Modified/New |
| `README.md`, `AGENTS.md`, `REQUISITOS.md`, `docs/mejoras-posteriores.md` | Modified |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Railway blocks SMTP on the owner's plan | Med | Verify plan; `IEmailSender` allows an HTTP-API sender later |
| Proxy IP shared → limiter blocks everyone | High if on before Railway-prep | Off by default; document dependency |
| Redeploy invalidates links (ephemeral keys) | High on Railway | Uniform "pide otro enlace"; keys in Railway-prep |
| Turnstile outage blocks registration | Low | Switch to `None` |

## Rollback Plan
Set the switches back to defaults (no redeploy of code). Code rollback: revert the slice PR; no migration is added (Identity columns already exist).

## Dependencies
- Owner accounts (configuration only): Cloudflare Turnstile site/secret keys; SMTP server (Brevo, Resend or Gmail app password); `Email:PublicBaseUrl`.

## Success Criteria
- [x] With no configuration, login/registration behave exactly as today (existing tests pass).
- [x] Each switch fails fast at startup with a message naming the variable, not the value.
- [x] No response, timing path or log reveals whether an email is registered when confirmation is on.
- [x] Build and tests in Release with zero warnings per slice.

## Manual checks (Chrome)
- S1: 11 quick POSTs to `/login` → Spanish 429; GET still works.
- S2: Turnstile test keys (always-pass, always-fail); without JS → Spanish "completa la verificación".
- S3–S5: local SMTP catcher (e.g. a Mailpit container) for confirm, resend, forgot, reset, expired link, unknown email.

## Proposal question round (user asked not to be asked; assumptions to review)
1. Should the limiter be on by default once forwarded headers exist? Assumed: off until Railway-prep.
2. Captcha only on registration and forgot password, not login? Assumed: yes.
3. With confirmation on, may unconfirmed accounts sign in at all? Assumed: no.
4. Reset link lifespan 2 h, confirmation 24 h? Assumed: yes.
5. Production email: SMTP enough, or an HTTP API is needed on Railway? Assumed: SMTP, verify plan.
