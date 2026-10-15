# Exploration: registration-hardening

Change: harden the open self-registration (REQUISITOS RF-67) with rate limiting, a captcha, email confirmation and password recovery, all switched off by default.

## Current state (main at dfdd5f1)

| Area | File | What it does today |
|---|---|---|
| Auth seam | `src/RutaProducto.Application/Abstractions/Authentication/IAuthService.cs` | `SignInAsync`, `RegisterAsync`, `SignOutAsync`; enums `AuthenticationResult` (Succeeded, InvalidCredentials, LockedOut, NotAllowed) and `RegistrationStatus` (Succeeded, EmailTaken, InvalidPassword). |
| Register use case | `src/RutaProducto.Application/Features/Authentication/Register/RegisterCommandHandler.cs` | Creates the account, then signs the person in immediately. |
| Identity impl | `src/RutaProducto.Infrastructure/Identity/IdentityAuthService.cs` | `PasswordSignInAsync(..., lockoutOnFailure: true)`; new accounts get `EmailConfirmed = false`; duplicate email maps to `EmailTaken`. |
| Identity setup | `src/RutaProducto.Infrastructure/DependencyInjection.cs` | `AddIdentityCore` + EF stores + `AddSignInManager`; `RequireConfirmedAccount = false`; lockout on for new users (Identity defaults: 5 failures, 5 minutes); **no `AddDefaultTokenProviders`**, so confirmation/reset tokens cannot be generated yet. |
| Seeded users | `Infrastructure/Identity/Seeding/InitialUserSeeder.cs` | `EmailConfirmed = true` (so turning confirmation on never locks them out). |
| Pages | `Web/Components/Pages/Login.razor`, `Register.razor` | Static SSR (`[ExcludeFromInteractiveRouting]`), `EditForm method="post"` without enhanced navigation, ES-module scripts (`password-toggle.js`, `password-match.js`, `password-rules.js`). |
| Endpoints | `Web/Authentication/AuthenticationEndpoints.cs` | Only `POST /logout`; login and registration POST to the Razor component endpoints `/login` and `/registro`. |
| Host | `Web/Program.cs` | No rate limiter, no forwarded headers, no Data Protection key persistence, no CSP header. |
| Switch pattern | `Infrastructure/Gemini/AssistantProvider.cs`, `Storage/StorageOptions.cs` | `Read(configuration)`: empty means the safe default, unknown value throws a message that lists valid values; options `BindAndValidate` throws at startup naming the env variable, never the value. |

Observations that matter for this change:

1. **Enumeration already exists on `/registro`**: "Ya existe una cuenta con ese correo" tells anyone whether an address is registered. RF-67 confirmed this on purpose; it can only be closed when email confirmation is on (otherwise the person needs the message).
2. **Enumeration trap with `RequireConfirmedEmail`**: `SignInManager.CheckPasswordSignInAsync` runs `PreSignInCheck` (confirmed email) *before* checking the password, so `NotAllowed` is returned for a wrong password too. Showing "confirma tu correo" on `NotAllowed` would reveal unconfirmed accounts. Fix: on `NotAllowed`, check the password with `UserManager.CheckPasswordAsync` and report `InvalidCredentials` unless it matches.
3. **Sign-in after registration** must be skipped when confirmation is required (the sign-in would return `NotAllowed`).
4. The project rule "views use CSS classes only" allows ES-module `<script type="module" src>` tags for files in `wwwroot/js` (already six files: chat, confirm-dialog, password-match, password-rules, password-toggle, print). The memory note listing only two files is outdated. No third-party script is loaded today.
5. Behind Railway's proxy `HttpContext.Connection.RemoteIpAddress` is the proxy address until forwarded headers are configured (AGENTS.md §6.9, pending). An IP-partitioned limiter would then put every user in one bucket.
6. Data Protection keys are not persisted (AGENTS.md §6.9). Identity email/reset tokens are Data Protection payloads, so every Railway redeploy invalidates outstanding links (and auth cookies).

## Approaches

### Rate limiting
| Option | Pros | Cons |
|---|---|---|
| **A. Built-in `Microsoft.AspNetCore.RateLimiting` (recommended)** | In the shared framework, no package; partitioned fixed window per IP; `OnRejected` can render a Spanish 429 | Needs a global limiter that selects only auth POSTs (component endpoints are shared by GET and POST); depends on forwarded headers on Railway |
| B. `[EnableRateLimiting]` on the components | Declarative | Applies to GET too; a blocked GET hides the page |
| C. Rely only on Identity lockout | Exists | Per account only; does not stop mass registration or spraying across accounts |

### Captcha
| Option | Pros | Cons |
|---|---|---|
| **A. Cloudflare Turnstile (recommended)** | Free, unlimited, no image puzzles, server-side `siteverify`, official test keys (always-pass / always-fail) | Third-party script from `challenges.cloudflare.com` (must not be proxied or self-hosted); does not work without JavaScript |
| B. hCaptcha free | Similar | More friction, image challenges |
| C. Google reCAPTCHA v3 | Familiar | Google account, score tuning, privacy concerns |
| D. Honeypot field only | No external account | Weak against targeted bots |

Widget approach that respects the views rule: a `CaptchaWidget.razor` renders, only when the provider is Turnstile, `<div class="cf-turnstile" data-sitekey="..." data-language="es">` plus `<script src="https://challenges.cloudflare.com/turnstile/v0/api.js" async defer>`. Implicit rendering means no inline JS, no `style=`, no `IJSRuntime`. The form posts `cf-turnstile-response`; the server verifies it once (tokens are single-use, valid 300 s). The SSR forms reload the full page after each POST, so a fresh token is issued after a validation error. **Explicit exception to document**: the first and only third-party script, loaded only on pages that need it and only when the provider is on.

### Email sending
| Option | Pros | Cons |
|---|---|---|
| **A. `System.Net.Mail.SmtpClient` (recommended)** | In the BCL, no new package (the user's NuGet config has an unreachable feed; restore needs `--ignore-failed-sources`); STARTTLS on 587 with `EnableSsl` | Microsoft marks it "not recommended for new development" (platform-compat DE0005); no implicit TLS on 465; no modern auth |
| B. MailKit (MIT) | Microsoft's recommended replacement; full TLS/auth support | New package plus MimeKit; restore friction |
| C. HTTP API (Resend, Brevo, SendGrid) | Works where SMTP ports are blocked | Vendor-specific code; account required |

Keep everything behind `IEmailSender` so B or C is one more class later.

### Tokens and links
- Add `AddDefaultTokenProviders()` (Data Protection token provider). Confirmation: default 1-day lifespan. Reset: a dedicated provider with a short lifespan (2 h recommended); reset tokens are effectively single-use because a password change rotates the security stamp.
- Base64Url-encode tokens (`WebEncoders`) in links; never log tokens or links.
- Build links from a configured `Email:PublicBaseUrl`, **never from the request `Host` header** (host-header injection would send valid tokens to an attacker domain).

### Uniform responses and timing
- Forgot password: always the same message and the same redirect, whether or not the address exists or is confirmed.
- Resend confirmation: same uniform message.
- Registration with confirmation on: duplicate address shows the same "revisa tu correo" result; the existing owner receives a "ya tienes una cuenta" email instead of a new account.
- Timing: sending only for existing accounts makes the response slower for them. Mitigate with an in-process queue (`Channel<EmailMessage>` + `BackgroundService`) so the request returns before SMTP; a lost email on restart is acceptable (the person can retry).

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Railway restricts outbound SMTP on lower plans (to verify on the owner's plan) | Email flows fail in production | `IEmailSender` seam; HTTP-API sender as a follow-up; keep `Email:Provider=None` until verified |
| Proxy IP shared by all users | Limiter blocks everyone at once | Limiter off by default; enable only after forwarded headers (Railway-prep change) |
| Data Protection keys ephemeral | Links invalid after a redeploy | Uniform "enlace no válido o vencido, pide otro"; persist keys in the Railway-prep change |
| Deliverability (SPF/DKIM/DMARC, spam folder) | People never confirm | Use a provider with domain authentication; resend page; manual check on Gmail/Outlook |
| Enumeration via `NotAllowed`, duplicate email, timing | Account discovery | Observations 1 and 2, queue for timing, uniform texts |
| Turnstile unavailable or blocked by the network | Nobody can register | Provider switch to `None`; clear Spanish message; registration only (not login) |
| Secrets leakage | Credential exposure | Turnstile secret and SMTP password only via user-secrets or env; startup errors name the variable, never the value; logs carry user id and outcome only |

## Free options (owner accounts needed; configuration, not code)

- **Turnstile**: free Cloudflare account, create a widget for the domain to get site key + secret key. Test keys work locally without an account.
- **SMTP**: Brevo free tier (about 300 emails/day), Resend free tier (about 100/day, 3,000/month), or a Gmail account with an app password for demos. Limits to be confirmed by the owner at signup.

## Recommendation

Five slices, each independently shippable and off by default: S1 limiter, S2 Turnstile, S3 email-sending infrastructure, S4 confirmation + resend, S5 password recovery.
