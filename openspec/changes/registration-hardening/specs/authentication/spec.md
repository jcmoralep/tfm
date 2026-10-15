# Authentication Specification (registration-hardening)

## Purpose

Harden the open self-registration (RF-67) and sign-in with five protections, each a configuration switch that is **off by default**, so behavior is unchanged until the owner provides keys: rate limiting (S1), a captcha (S2), email-sending infrastructure (S3), email confirmation with resend (S4) and password recovery (S5). New capability (full spec; `openspec/specs/` only has `initiatives`, and `authentication` exists only in code and RF-67). Source requirements: RF-67, RF-03, RNF-02, RNF-03, new RF-78 to RF-82 (REQUISITOS.md; the edits are listed in the last section). Context: `openspec/changes/registration-hardening/proposal.md` and `exploration.md`; decisions are final (the user asked not to be asked).

Scenario tags: **[UNIT]** xUnit at validator, handler, options or pure-class level (hand-written in-memory doubles; no bUnit, no network, no SMTP server, no Cloudflare). **[UI]** manual-only check in Chrome with `docker compose` MySQL, the seed user, Turnstile test keys and a local SMTP catcher (e.g. Mailpit), per AGENTS section 5.4. **[DOC]** documentation deliverable checked by reading the file. **[MANUAL]** needs a real Turnstile or SMTP account; outside CI.

Terminology. An **auth POST** is an HTTP `POST` to `/login`, `/registro` or one of the recovery pages (`/recuperar-contrasena`, `/restablecer-contrasena`, `/confirmar-correo/reenviar`); `GET` and every other path are never an auth POST. The **client IP** is `HttpContext.Connection.RemoteIpAddress` as the host sees it. **Confirmation required** means `Email:RequireConfirmedEmail` is `true`. **Uniform response** is one fixed Spanish text and one fixed redirect or page, identical whether or not the address is registered, confirmed or throttled by Identity. A **secret** is `Turnstile__SecretKey`, `Smtp__Password`, any token, any link carrying a token, and the CAPTCHA response token. Page routes `/confirmar-correo`, `/confirmar-correo/reenviar`, `/recuperar-contrasena` and `/restablecer-contrasena` are fixed by design (D17) and exposed as constants in `AuthenticationEndpoints`. This spec was reconciled with `design.md` (design wins); see "Reconciliation notes" in `tasks.md`. Message wording is a proposal unless marked verbatim; Spanish strings are user-facing. Numeric values are the decided defaults; configurable ones are named.

## ADDED Requirements

### Requirement: Rate limiting of auth POSTs (RF-78, RNF-02)

The system MUST throttle auth POSTs with the framework limiter `Microsoft.AspNetCore.RateLimiting` (no new package), as one global limiter that selects requests by method and path, partitioned by client IP, fixed window. It MUST count only auth POSTs: GET requests (so the pages always render), static files, `/logout` and the Blazor circuit MUST NOT be counted or rejected. It MUST be configured by `RateLimiting:Enabled` (default `false`), `RateLimiting:PermitLimit` (default 10) and `RateLimiting:WindowSeconds` (default 60); with `Enabled=false` no limiter middleware MUST be added and behavior is exactly as before. Startup MUST fail, with a message naming the key and never its value, when `Enabled=true` and `PermitLimit` or `WindowSeconds` is not a positive integer (values that are not numbers included). A rejected request MUST answer HTTP 429 with a Spanish page and MUST NOT process the form. The page is the existing not-found page re-executed by the status-code middleware as `/not-found?codigo=429` (`UseStatusCodePagesWithReExecute("/not-found", "?codigo={0}")`), which for code 429 shows the heading `Demasiados intentos`, the text `Hiciste demasiados intentos en poco tiempo. Espera un minuto y vuelve a intentarlo.` (both verbatim) and a link to `/login`; the response includes a `Retry-After` header (the limiter's value, default the window in seconds). If the re-executed POST is not rendered as a GET, the fallback is a `303` to `/not-found?codigo=429` where the page sets status 429 (design D2). The rejection log line (Warning `Auth POST throttled on {Path}`) MUST carry only the path (the status is already in Serilog request logging); it MUST NOT carry the IP address, the form fields, the email or any token. The limiter does not replace Identity lockout (per account); both apply. The limiter depends on forwarded headers for a per-user partition behind Railway's proxy; that is the Railway-prep change (AGENTS section 6.9), so it stays off by default until then and this dependency MUST be documented.

#### Scenario: Default is off [UNIT]
- GIVEN no `RateLimiting` configuration
- WHEN the options are read
- THEN `Enabled` is false, `PermitLimit` 10, `WindowSeconds` 60 (that `AddRateLimiter`/`UseRateLimiter` are not called is checked in the S1 startup smoke, because `Program` has no unit seam)

#### Scenario: Only auth POSTs are selected [UNIT]
- GIVEN the pure path/method selector used by the limiter
- WHEN it is called with POST `/login`, POST `/registro`, POST `/recuperar-contrasena`, POST `/restablecer-contrasena`, POST `/confirmar-correo/reenviar`, then GET `/login`, GET `/registro`, POST `/logout`, POST `/_blazor` and POST `/iniciativas`
- THEN the first five are selected and the rest are not; the path comparison is case-insensitive (`OrdinalIgnoreCase`; a trailing slash is not special-cased, design D1)

#### Scenario: Invalid numbers fail at startup [UNIT]
- GIVEN `Enabled=true` and `PermitLimit` = 0, -1 or "abc", or `WindowSeconds` = 0
- WHEN the options are validated
- THEN each throws a message that names the key and does not contain the configured value (also a non-boolean `Enabled`, which throws whatever the other values are); with `Enabled=false` the same numbers do not throw. `PermitLimit` is valid from 1 to 1000 and `WindowSeconds` from 1 to 3600 (design D3)

#### Scenario: Eleventh POST is rejected [UI]
- GIVEN `RateLimiting__Enabled=true` and the defaults
- WHEN eleven POSTs to `/login` are sent within a minute from one client
- THEN the eleventh answers 429 with the Spanish page, a GET of `/login` still renders, and the log shows the path and 429 but no email or password

#### Scenario: Partition is per IP [UNIT]
- GIVEN a partition-key function and two requests with different client IPs
- WHEN keys are computed
- THEN the keys differ; a request without a client IP falls into one shared fallback partition rather than throwing

### Requirement: Captcha seam and Turnstile verification (RF-79, RNF-03)

The system MUST define `ICaptchaVerifier` in Application (verify one response token, `Task<CaptchaOutcome> VerifyAsync(string? token, CancellationToken ct)` returning `CaptchaOutcome.Passed`, `Rejected` or `Unavailable`) with two implementations in Infrastructure selected by `Captcha:Provider`: `None` (default; always `Passed`, no network, no widget) and `Turnstile`. The setting MUST follow the `AssistantProviderSetting` pattern: empty means `None`, a comparison ignoring case, an unknown value throws at startup with a message that lists the valid values (`None`, `Turnstile`). With `Turnstile`, startup MUST fail, naming the missing variable (`Turnstile:SiteKey`, `Turnstile__SecretKey`) and never its value, when either is empty. The Turnstile verifier MUST call Cloudflare `siteverify` server-side over HTTPS with the secret, and the response token from the form field `cf-turnstile-response` (no `remoteip`: it would be wrong behind the proxy until Railway-prep, design D4), through a named `HttpClient` `"Turnstile"` with a timeout of 10 seconds; it MUST NOT trust any client-side result. A missing, blank or longer than 2,048 characters token MUST be `Rejected` without a network call. The widget settings reach the page as `CaptchaWidgetSettings(string? SiteKey)` (null for `None`). A token is single-use and valid for 300 seconds; the verifier MUST NOT cache or retry it. **Outage behavior (decided): fail closed.** A timeout, network error, non-success HTTP status or unparseable body MUST be `Unavailable`, MUST be treated as not passed (the form is not processed), and MUST show a Spanish message (verbatim: `No pudimos completar la verificación. Inténtalo de nuevo en unos minutos.`); a `Rejected` result shows another (verbatim: `Completa la verificación para continuar.`). Neither message MUST include the Cloudflare error codes, the secret or the token. Logs for a verification MUST carry only the outcome (`Passed`, `Rejected`, `Unavailable`), the duration and Cloudflare's error codes (a closed set of short strings), never the token, the secret, the email or the IP. The switch to `None` is the documented recovery from a Turnstile outage.

The captcha MUST apply to registration (`/registro`) and to the forgot-password request (`/recuperar-contrasena`) only. It MUST NOT apply to login, to reset, to confirmation or to resend (decided: login is protected by the limiter and Identity lockout; resend is uniform and throttled). Validation of the captcha MUST run before any account lookup, creation or email, so a bot gets no signal about accounts. With `None`, neither page renders a widget or the Cloudflare script and the forms behave as today.

The widget MUST be one `CaptchaWidget` Razor component, rendered only when the provider is `Turnstile`, that emits `<div class="cf-turnstile" data-sitekey="..." data-language="es" data-size="flexible">`, a `<noscript>` paragraph (`Activa JavaScript para completar la verificación.`) and one `<script src="https://challenges.cloudflare.com/turnstile/v0/api.js" async defer>` (implicit rendering). It MUST NOT use inline JavaScript, `style` attributes or `IJSRuntime`. The script MUST be loaded directly from Cloudflare (not proxied, not self-hosted), only on the pages that carry the widget and only when the provider is on. The secret key MUST NOT reach the browser; only the site key does. This is the first and only third-party script and is a documented exception to the "views use CSS classes only" rule (AGENTS section 2; see the documentation edits). Because the forms are static SSR with a full reload after each POST, a fresh token is issued after a validation error. The widget sits inside each page's `EditForm` so the implicit render injects `cf-turnstile-response` into that form; the page reads it with `[SupplyParameterFromForm(FormName = "<form name>", Name = "cf-turnstile-response")]`, and if Chrome shows that binding does not receive the value, the fallback is to read `HttpContext.Request.Form["cf-turnstile-response"]` on POST (task 2.5). Without JavaScript the widget does not work, the noscript text is shown and the `Rejected` message appears.

#### Scenario: Default provider is None [UNIT]
- GIVEN no `Captcha` configuration, and then `Captcha:Provider` = "" and "none"
- WHEN the provider is read
- THEN each is `None`, the verifier returns `Passed` for any token including an empty one, and no widget is rendered

#### Scenario: Unknown provider lists valid values [UNIT]
- GIVEN `Captcha:Provider` = "hcaptcha"
- WHEN the provider is read
- THEN it throws a message listing `None` and `Turnstile` and naming `Captcha:Provider`

#### Scenario: Turnstile requires its keys [UNIT]
- GIVEN `Captcha:Provider` = "Turnstile" with an empty `Turnstile:SiteKey`, and then with an empty `Turnstile__SecretKey`
- WHEN the options are validated
- THEN each throws naming the missing variable; a configured secret never appears in the message

#### Scenario: Siteverify outcomes [UNIT]
- GIVEN a fake `HttpMessageHandler` returning `{"success":true}`, then `{"success":false,"error-codes":["invalid-input-response"]}`, then HTTP 500, then a timeout, then garbage
- WHEN the verifier checks a non-empty token
- THEN the results are `Passed`, `Rejected`, `Unavailable`, `Unavailable`, `Unavailable`; the request carries the secret and the token as form fields (`secret`, `response`) and nothing else

#### Scenario: Empty token makes no call [UNIT]
- GIVEN the Turnstile verifier and a handler double that records calls
- WHEN it verifies a null, whitespace or 2,049-character token
- THEN the result is `Rejected` and the double saw no request

#### Scenario: Failure blocks registration before any lookup [UNIT]
- GIVEN a verifier double returning `Rejected`, then `Unavailable`
- WHEN the register command is handled with a new email
- THEN no account is created, no email is queued, no existing-account lookup runs, and the result carries the respective Spanish message

#### Scenario: Messages do not leak [UNIT]
- GIVEN a secret "S3CRET" and a token "TOK" used in a failing verification with a logger double
- WHEN the messages shown and the log lines are collected
- THEN none contains "S3CRET", "TOK", the email or an error body

#### Scenario: Widget and test keys [UI]
- GIVEN Turnstile's official always-pass and always-fail test keys
- WHEN `/registro` and `/recuperar-contrasena` are opened and submitted
- THEN the widget shows in Spanish, always-pass lets the form through, always-fail shows `Completa la verificación para continuar.`, `/login` has no widget, and with `Captcha__Provider=None` no widget and no Cloudflare request appear

### Requirement: Email sending infrastructure (RF-80, RNF-02, RNF-03)

The system MUST define `IEmailSender` in Application (`void Enqueue(EmailMessage)`: non-blocking, never throws; `EmailMessage(To, Kind, Subject, Body)`) as the request-side seam, and `IEmailTransport` in Infrastructure (`SendAsync`, the SMTP delivery, separate from the sender, design D9), and select an implementation by `Email:Provider`: `None` (default; accepts the message and discards it, sending nothing) or `Smtp`. The setting MUST follow the same pattern (empty means `None`, case-insensitive, unknown value lists `None`, `Smtp`). With `Smtp`, startup MUST fail, naming the variable and never the value, when any of `Smtp:Host`, `Smtp:From` (valid address) or `Email:PublicBaseUrl` (absolute URL, `https` unless the host is localhost, trailing `/` trimmed) is missing or invalid, when `Smtp:Port` (default 587, integer 1 to 65535) is out of range or is 465, or when `Smtp:Username` is set without `Smtp__Password` (the password is not required without a username, so local sinks work). Optional: `Smtp:FromName` (default "Ruta de Producto") and `Smtp:TimeoutSeconds` (default 30, 1 to 120). The SMTP transport MUST use `System.Net.Mail.SmtpClient` (no new package), with `Smtp:EnableSsl` (default `true`, STARTTLS on 587; `false` is allowed only when `Smtp:Host` is `localhost` or `127.0.0.1`; implicit TLS on 465 is not supported, throws "implicit TLS is not supported by System.Net.Mail; use 587 (STARTTLS)", and MUST be documented). The password MUST come only from user-secrets or `Smtp__Password`.

Sending MUST NOT happen on the request thread: the application layer enqueues to an in-process queue (`Channel<EmailMessage>` bounded to 100 with `DropWrite`, drained by one `BackgroundService` that calls `IEmailTransport`; registered only with `Smtp`; on shutdown queued messages are lost) and returns, so the response time does not depend on SMTP or on whether an address exists (timing uniformity). If the queue is full the message is dropped and a content-free warning is logged; a lost message on restart or failure is accepted (the person can retry). A send failure MUST be caught, logged and never crash the host or reach the user. Email bodies and subjects are Spanish templates; each rendered message MUST carry the product name "Ruta de Producto" and no "BMAD" name (AGENTS section 4.11). Links in the messages MUST be built from `Email:PublicBaseUrl` only, never from the request `Host` header or any forwarded header (host-header injection would send valid tokens to an attacker domain); the base URL is normalised (no trailing slash) once. Logs of the email subsystem MUST carry only the template kind (`ConfirmEmail`, `AccountExists`, `ResetPassword`), and on failure the exception type and the `SmtpStatusCode`; the token service logs the user id and the outcome. They MUST NOT carry the recipient address, subject, body, token or link (RNF-02).

#### Scenario: Default provider is None [UNIT]
- GIVEN no `Email` configuration
- WHEN the services are resolved
- THEN `IEmailSender` is the discarding sender, `RequireConfirmedEmail` is false and no SMTP setting is read

#### Scenario: Smtp requires its settings [UNIT]
- GIVEN `Email:Provider` = "Smtp" and, one at a time, an empty `Smtp:Host`, `Smtp:Port` = "abc" or 70000, an invalid `Smtp:From`, an empty `Email:PublicBaseUrl`, a relative `Email:PublicBaseUrl`, an `http` `Email:PublicBaseUrl` on a non-local host, `Smtp:Port` = 465, `Smtp:EnableSsl` = false on a non-local host, `Smtp:Username` without `Smtp__Password`
- WHEN the options are validated
- THEN each throws naming the variable and never the configured value; a complete configuration passes, and so does `localhost` with `EnableSsl=false`, no username and no password

#### Scenario: Unknown provider [UNIT]
- GIVEN `Email:Provider` = "Sendgrid"
- WHEN the provider is read
- THEN it throws listing `None` and `Smtp` and naming `Email:Provider`

#### Scenario: Queue returns before sending [UNIT]
- GIVEN an `IEmailTransport` double that blocks for one second, the queued sender and the dispatcher (`EmailDispatcher : BackgroundService`)
- WHEN a message is enqueued
- THEN `Enqueue` returns immediately, and the dispatcher later hands the message to the transport double exactly once; the 101st message while the queue (capacity 100, `DropWrite`) is full is dropped with a Warning that carries the kind only

#### Scenario: Sender failure is contained [UNIT]
- GIVEN a transport double that throws
- WHEN the dispatcher processes two queued messages
- THEN the second is still processed (no retry of the first), the host keeps running, and the log line holds the template kind, the exception type and the `SmtpStatusCode` when present but no address, subject or body

#### Scenario: Links come from configuration [UNIT]
- GIVEN `Email:PublicBaseUrl` = "https://app.example.com/" and a request whose Host is "evil.test"
- WHEN a confirmation link is built
- THEN it starts with "https://app.example.com/" and "evil.test" appears nowhere

#### Scenario: Real delivery [UI]
- GIVEN a local SMTP catcher and `Email__Provider=Smtp`
- WHEN a message is triggered (nothing sends before the S4 registration, so this is checked in the S4 and S5 walkthroughs; S3 only has a startup smoke)
- THEN it arrives in Spanish with a working link, and the app log has no address, token or link

#### Scenario: Provider deliverability [MANUAL]
- GIVEN a real SMTP account (Brevo, Resend or a Gmail app password) and a Railway plan that allows outbound SMTP
- WHEN a confirmation email is sent to Gmail and Outlook
- THEN it arrives (spam folder checked); if Railway blocks SMTP the finding is recorded and `Email:Provider` stays `None`

### Requirement: Email confirmation and resend (RF-81, RF-67, RNF-02)

`Email:RequireConfirmedEmail` (default `false`) MUST control whether an unconfirmed email blocks sign-in. Startup MUST fail, naming `Email:RequireConfirmedEmail`, when it is `true` and `Email:Provider` is `None`. The Identity setup MUST add the default token providers; confirmation tokens MUST have a lifespan of 24 hours (`DataProtectionTokenProviderOptions`), and be Base64Url-encoded in links (`IAccountTokenService` port with the Identity adapter `IdentityAccountTokenService`; `AccountTokenCodec` returns null on malformed input). Links are built by the `IAccountLinks` port (Web adapter) from `Email:PublicBaseUrl` plus the path constants. Seeded users already have `EmailConfirmed = true` and MUST keep it, so enabling confirmation never locks them out.

With confirmation **not** required, behavior is as before (the account is created, the person is signed in, a duplicate address shows the existing "Ya existe una cuenta con ese correo" notice); this is the unchanged default and RF-67's enumeration trade-off stays documented. With confirmation **required**:

1. Registration MUST create the account with `EmailConfirmed = false`, MUST NOT sign the person in, MUST enqueue the confirmation email, and MUST show the uniform result (verbatim, design D13: `Revisa tu correo: te enviamos un enlace para confirmar tu cuenta.`, a green panel that replaces the form).
2. A duplicate address MUST yield the same uniform result and the same redirect as a new one, MUST NOT create an account, and MUST enqueue an "ya tienes una cuenta" email (with a link to sign in and one to recover the password) to the existing owner instead of a confirmation email. Password-policy errors are shown as today (they do not depend on the address).
3. The confirm page MUST accept the user id and token, call the token confirmation, and run on GET (as Identity's default UI) and show either success (verbatim: `Tu correo quedó confirmado. Ya puedes iniciar sesión.`, with a link to sign in) or the uniform failure (verbatim: `El enlace no es válido o venció. Pide uno nuevo.`) for an invalid, expired, reused-after-change or malformed link and for an unknown user id, all alike. Confirming an already confirmed account MAY show success.
4. An unconfirmed account MUST NOT sign in. `NotAllowed` is returned by Identity before the password is checked (it would also appear for a wrong password), and it is also returned before the lockout check, so on `NotAllowed` the auth service MUST, in this order (design D14, pure `SignInOutcome.Resolve(SignInResult, lockedOut, passwordMatches)`): (a) if the account is locked out, report `LockedOut`; (b) if `UserManager.CheckPasswordAsync` matches, report `NotAllowed` (shown as `Confirma tu correo antes de iniciar sesión.` with a link to `/confirmar-correo/reenviar`); (c) otherwise call `UserManager.AccessFailedAsync` (because `CheckPasswordAsync` does not count failures, so unconfirmed accounts cannot be brute-forced without lockout) and report `InvalidCredentials`, so the message never reveals an unconfirmed account to someone without the password. Hardening that is not a spec rule elsewhere (design D15): a sign-in for an unknown email MUST verify the password against a fixed dummy hash (computed once, lazily) before returning `InvalidCredentials`, so the unknown-user path pays the hashing cost.
5. The resend page MUST take an email, validate the captcha-free form, and always show the uniform result (verbatim: `Si hay una cuenta pendiente de confirmar con ese correo, te enviamos un enlace nuevo.`); it MUST send a new confirmation email only when the account exists and is unconfirmed, and nothing otherwise. The resend page is an auth POST (throttled by the limiter). When confirmation is not required but `Email:Provider` is `Smtp`, the confirm and resend pages MUST still work, harmlessly. When `Email:Provider` is `None` the resend page (and the forgot and reset pages) answer as not found (`Navigation.NotFound()`, design D17); the confirm page is not gated.

An old, unconfirmed account created before confirmation was turned on MUST be able to confirm by the resend page. Tokens and links MUST NOT be logged; the logs carry the user id and the outcome only.

#### Scenario: Startup guard [UNIT]
- GIVEN `Email:RequireConfirmedEmail=true` with `Email:Provider` empty or `None`, and then with `Smtp` and a valid configuration
- WHEN the options are validated
- THEN the first throws naming `Email:RequireConfirmedEmail` and `Email:Provider`; the second passes

#### Scenario: Default behavior is unchanged [UNIT]
- GIVEN no `Email` configuration
- WHEN a person registers, and then registers again with the same address
- THEN the first is signed in directly, the second gets `EmailTaken` with the existing notice, and no email is queued

#### Scenario: Registration with confirmation required [UNIT]
- GIVEN `RequireConfirmedEmail=true`, a fake `IAuthService` and a queue double
- WHEN a new address registers
- THEN the account has `EmailConfirmed=false`, no sign-in call is made, one confirmation message is queued and the uniform result is returned

#### Scenario: Duplicate address is indistinguishable [UNIT]
- GIVEN `RequireConfirmedEmail=true` and an existing account
- WHEN the same address registers again
- THEN the result and redirect equal those of a new address, no second account exists, and the queued message is the "ya tienes una cuenta" kind addressed to the existing owner

#### Scenario: Confirmation succeeds and fails uniformly [UNIT]
- GIVEN a valid token, then an expired token (the provider lifespan elapsed: `DataProtectorTokenProvider` reads the system clock, so the test configures a lifespan of a few milliseconds and waits; separately the registered lifespans are asserted to be 24 h and 2 h), a token altered by one character, a non-Base64Url token and an unknown user id, run against `UserManager` over a hand-written in-memory user store (no new package)
- WHEN each is confirmed
- THEN the first confirms the account; the rest all return the same failure outcome and text

#### Scenario: NotAllowed is gated by the password [UNIT]
- GIVEN an unconfirmed account and confirmation required
- WHEN sign-in is attempted with the right password, then with a wrong password, and then (locked-out account) with either
- THEN the first reports `NotAllowed` with the confirm-your-email message and the resend link; the second reports `InvalidCredentials` with the usual message and records one access failure; the locked-out case reports `LockedOut`; and an unknown email reports `InvalidCredentials` (verified through the pure `SignInOutcome.Resolve` Theory; the UserManager calls are checked in the S4 walkthrough: five wrong passwords on an unconfirmed account lock it)

#### Scenario: Unknown email pays the hash cost [UNIT]
- GIVEN the dummy-hash helper
- WHEN the hash is requested twice
- THEN it is computed once (same instance) and a verification of any password against it reports failure

#### Scenario: Resend is uniform [UNIT]
- GIVEN an unconfirmed account, a confirmed account and an unknown address
- WHEN the resend command runs for each
- THEN all three return the same result; exactly one confirmation message is queued, for the first only

#### Scenario: Seeded users keep working [UNIT]
- GIVEN the initial user seeder with confirmation required
- WHEN a seeded user signs in
- THEN the sign-in succeeds because the seeded account is confirmed

#### Scenario: Full flow [UI]
- GIVEN a local SMTP catcher, `Email__Provider=Smtp`, `Email__RequireConfirmedEmail=true`
- WHEN a person registers, tries to sign in before confirming, opens the emailed link, signs in, then re-opens the same link and an altered link
- THEN registration shows the uniform text without signing in, the early sign-in shows the confirm message, the link confirms, sign-in works, and the reused and altered links show `El enlace no es válido o venció. Pide uno nuevo.`; registering the same address again shows the same text and delivers the "ya tienes una cuenta" email

### Requirement: Password recovery (RF-82, RNF-02)

The system MUST offer a forgot-password page and a reset page, both static SSR like the login, linked from the login page ("¿Olvidaste tu contraseña?"). The forgot page MUST take an email, apply the captcha when the provider is `Turnstile` (before any lookup), and ALWAYS show the same result (verbatim: `Si el correo corresponde a una cuenta, te enviamos un enlace para restablecer la contraseña. Vence en 2 horas.`) with the same redirect and a response time that differs only by a data-protection protect and a queue write (microseconds, accepted), whether the address is unknown, unconfirmed, confirmed or locked out. A reset email MUST be queued for **any existing account**, confirmed or not (design D16: the mailbox is the proof), and for no unknown address; no confirmation email is sent from this page. The reset token MUST come from a dedicated token provider with a lifespan of 2 hours (distinct from the 24 h of confirmation), MUST be Base64Url-encoded, and is effectively single-use because changing the password rotates the security stamp. When `Email:Provider` is `None` the forgot and reset pages answer as not found (`Navigation.NotFound()`, design D17) and the login page MUST hide the link ("¿Olvidaste tu contraseña?" shows only when email is enabled); the discarding sender logs `Email {Kind} not sent: Email:Provider is None` and sends nothing.

The reset page MUST take the user id and token from the link plus the new password and its confirmation, validate against the same `PasswordPolicy` as registration (same Spanish messages and the live checklist), and on success MUST set the new password, show success with a link to sign in, and not sign the person in. Resetting also confirms the email address of an unconfirmed account (the link proved ownership). An invalid, expired, already used or malformed token and an unknown user id MUST all show the same failure (`El enlace no es válido o venció. Pide uno nuevo.`) with a link to the forgot page. A successful reset does not change lockout state (the design does not clear it; recorded as a follow-up in `docs/mejoras-posteriores.md`). Both pages are auth POSTs for the limiter. Password, token and link MUST NOT be logged.

#### Scenario: Forgot is uniform [UNIT]
- GIVEN a confirmed account, an unconfirmed account (confirmation required), a locked-out account and an unknown address
- WHEN the forgot command runs for each
- THEN all four return the same result; a reset message is queued for the first three (every existing account), and none for the unknown address; no confirmation message is queued by this command

#### Scenario: Captcha runs before the lookup [UNIT]
- GIVEN a verifier double returning `Rejected` (then `Unavailable`) and a spy `IAccountTokenService`
- WHEN the forgot command runs
- THEN `ForgotPasswordResult.CaptchaRejected` (then `CaptchaUnavailable`) is returned with the Spanish captcha message and the spy was never called

#### Scenario: Reset succeeds [UNIT]
- GIVEN a valid reset token and a policy-compliant password
- WHEN the reset command runs
- THEN the password changes, the old password no longer works, the person is not signed in, and the same token fails on a second use

#### Scenario: Reset rejects bad tokens uniformly [UNIT]
- GIVEN an expired token (the reset provider with a few-millisecond lifespan, as in the confirmation scenario), a confirmation token used as a reset token, an altered token, a non-Base64Url token and an unknown user id
- WHEN each is reset
- THEN all return the same failure outcome and text

#### Scenario: Reset enforces the password policy [UNIT]
- GIVEN a valid token and the password "abc"
- WHEN the reset command is validated
- THEN it fails with the same Spanish rule messages as registration and the token is not consumed

#### Scenario: Reset confirms the email [UNIT]
- GIVEN an unconfirmed account that resets its password with a valid token
- WHEN the reset completes
- THEN `EmailConfirmed` is true

#### Scenario: Link hidden without email [UI]
- GIVEN `Email:Provider=None` (Razor has no unit seam; no bUnit)
- WHEN `/login` is opened, and `/recuperar-contrasena`, `/restablecer-contrasena` and `/confirmar-correo/reenviar` are requested
- THEN the forgot-password link is not offered and the three pages answer as not found; the `AccountEmailSettings(EmailEnabled: false)` value that drives both is asserted in the S3 options tests [UNIT]

#### Scenario: Full flow [UI]
- GIVEN a local SMTP catcher
- WHEN a person asks for a reset for a known and then an unknown address, opens the emailed link, sets a weak and then a valid password, signs in, and re-opens the link
- THEN both asks show the same text, the weak password shows the rule messages, the valid one succeeds and signs in on the next step, and the reused link shows the uniform failure

### Requirement: Data Protection and redeploy caveat (RF-81, RF-82)

Confirmation and reset tokens, and authentication cookies, are ASP.NET Data Protection payloads. Until the Railway-prep change persists the key ring (AGENTS section 6.9), every redeploy or container restart MUST be expected to invalidate outstanding links and sign people out; the product MUST NOT work around this in this change. The failure MUST be the uniform "El enlace no es válido o venció. Pide uno nuevo.", never an unhandled error or a distinct message, and the resend and forgot pages are the way out. The caveat MUST be documented (README and AGENTS section 4.6).

#### Scenario: Unprotectable token is a uniform failure [UNIT]
- GIVEN a confirmation token generated under one key ring and checked under another
- WHEN the confirm and reset commands run
- THEN both return the uniform failure and no exception escapes

#### Scenario: Caveat documented [DOC]
- GIVEN README and AGENTS section 4.6
- WHEN they are read
- THEN both state that links and sessions do not survive a redeploy until keys are persisted

### Requirement: Secrets and logging (RNF-02, RNF-03)

`Turnstile__SecretKey` and `Smtp__Password` MUST be set only through user-secrets or environment variables, never in the repository or in `appsettings*.json` (the committed files hold empty placeholders or nothing). Startup errors MUST name the variable and never its value. No log line, exception message shown to the user, or rendered page MUST contain a secret, the recipient address of an email, a token, a link with a token, a password or the captcha response. `LoggingBehavior` already logs request names and identifiers only (AGENTS section 4.4); new commands (`RegisterCommand`, confirm, resend, forgot, reset) MUST keep that: their log entries carry the command name and, at most, the user id. With every switch at its default, the application MUST behave exactly as before this change and all existing tests MUST pass.

#### Scenario: Placeholders only [UNIT]
- GIVEN the committed `appsettings.json` and `appsettings.Development.json`
- WHEN the secret keys `Turnstile:SecretKey` and `Smtp:Password` are read
- THEN they are absent or empty

#### Scenario: No content in logs [UNIT]
- GIVEN a logger double and each new command run with a recognisable email, token and password
- WHEN the entries are collected
- THEN none contains the email, token, link or password

#### Scenario: Defaults change nothing [UNIT]
- GIVEN no configuration for any of the four switches
- WHEN the existing authentication tests run
- THEN they pass unchanged and no new service starts a background worker that sends anything

## Assumptions

- Production email is SMTP via `System.Net.Mail`; Railway's outbound SMTP policy on the owner's plan is unverified and is checked before turning `Email:Provider` on.
- The limiter stays off until forwarded headers exist (Railway-prep); with it on behind the proxy before then, all users share one bucket.
- Captcha is only on registration and forgot-password; unconfirmed accounts never sign in; confirmation lasts 24 h and reset 2 h.
- The in-process queue loses messages on restart (accepted); sender failure is silent for the user and logged without content.
- Duplicate-address enumeration on `/registro` remains while confirmation is not required (RF-67, decided); it closes when confirmation is required.
- Timing parity is best effort: queued sending removes the SMTP delay, but database work for existing accounts is not equalised.
- `System.Net.Mail.SmtpClient` is marked not recommended (DE0005 style warning) and lacks implicit TLS; the `IEmailSender` seam allows MailKit or an HTTP API later. Any such warning is suppressed narrowly in that one class with a comment, keeping the build at zero warnings.

## Out of Scope

Forwarded headers, Data Protection key persistence, Dockerfile and Railway deployment (Railway-prep change, AGENTS section 6.9); HTTP-API email providers and MailKit; invitation codes; captcha on login; roles or an admin screen (RF-03); changing the email address; account deletion; two-factor authentication; persisting the email queue; the `RutaProducto.*` naming (already done); localization.

## Documentation edits (REQUISITOS.md, AGENTS.md sections 2, 4.4 and 4.6, README)

Exact REQUISITOS.md edits (in apply, per design: S1 for RF-78, S2 for RF-79 and the Turnstile sentence of RNF-03, S3 for RF-80, the Smtp sentence of RNF-03 and RNF-02, S4 for RF-81 and RF-67, S5 for RF-82; the AGENTS.md 4.4 and 4.6 edits and the README final pass are in Group F):

- **RF-67**: in the sentence `Al crearla entra directamente a la plataforma.` append `, salvo que la confirmación del correo esté activa (RF-81): entonces no entra y recibe un aviso para revisar su correo.`; replace `Un correo ya registrado se rechaza con un aviso que invita a iniciar sesión.` with `Un correo ya registrado se rechaza con un aviso que invita a iniciar sesión; con la confirmación del correo activa la respuesta es idéntica a la de un correo nuevo y la persona dueña recibe un correo (RF-81).`; replace `cuando se agregue la verificación` with `y se confirman con el enlace del correo (RF-81)`; and replace the final list `Mejoras posteriores acordadas o evidentes: ... (la capa gratuita de Gemini puede usar los datos).` with `Protecciones disponibles, todas apagadas por defecto: limitación de intentos (RF-78), captcha en el registro (RF-79), correo (RF-80), confirmación del correo (RF-81) y recuperación de contraseña (RF-82). Pendiente: un código de invitación y, antes de abrirlo en un entorno público, activar estas protecciones (la capa gratuita de Gemini puede usar los datos).` Keep `[CONFIRMADO]`.
- **RNF-02**: append `Los registros de correo, captcha y autenticación (RF-78 a RF-82) no contienen direcciones de correo, destinatarios, asuntos, cuerpos, tokens, enlaces, contraseñas ni respuestas del captcha: solo identificadores, el tipo de plantilla, el resultado y la duración.`
- **RNF-03**: append `La clave secreta de Turnstile (Turnstile__SecretKey) y la contraseña SMTP (Smtp__Password) van solo en user-secrets o variables de entorno, nunca en el repositorio ni en los registros; los errores de arranque nombran la variable, no el valor.`
- **RF-78** (new): `Con RateLimiting:Enabled=true (apagado por defecto), las peticiones POST a /login, /registro y a las páginas de recuperación se limitan por IP (10 por 60 s, configurable); al superarlo se responde 429 con un mensaje en español. Las peticiones GET no se limitan. Detrás del proxy de Railway requiere cabeceras reenviadas. [CONFIRMADO]`
- **RF-79** (new): `Con Captcha:Provider=Turnstile (por defecto None) el registro y la solicitud de recuperación de contraseña exigen superar Cloudflare Turnstile, verificado en el servidor; el inicio de sesión no lo usa. Si la verificación falla o no está disponible, el formulario no se procesa y se muestra un mensaje en español (falla cerrado). Faltar la clave impide el arranque. [CONFIRMADO]`
- **RF-80** (new): `El correo se envía mediante IEmailSender con Email:Provider=None (por defecto, no envía nada) o Smtp, en una cola en segundo plano que no retrasa la respuesta; los enlaces se construyen con Email:PublicBaseUrl, nunca con la cabecera Host. [CONFIRMADO]`
- **RF-81** (new): `Con Email:RequireConfirmedEmail=true (por defecto false; exige Email:Provider distinto de None) el registro no inicia sesión y envía un enlace de confirmación válido 24 horas; una cuenta sin confirmar no inicia sesión y el aviso solo se muestra a quien conoce la contraseña; un correo ya registrado recibe la misma respuesta que uno nuevo; existe un reenvío con respuesta uniforme. [CONFIRMADO]`
- **RF-82** (new): `La persona puede pedir restablecer su contraseña en /recuperar-contrasena (con captcha si está activo) y recibe un enlace válido 2 horas, de un solo uso; la respuesta es siempre la misma exista o no la cuenta; la nueva contraseña cumple la misma política que el registro. Los enlaces dejan de valer tras un redespliegue hasta persistir las claves de Data Protection. [CONFIRMADO]`

Other documents:

- AGENTS.md section 2 (Stack, JavaScript row) and section 4.5 (the same exception, written in S2): add a row or note for the new switches, and the documented exception to the JavaScript rule: `Excepción documentada: con Captcha:Provider=Turnstile las páginas /registro y /recuperar-contrasena cargan el script de Cloudflare (challenges.cloudflare.com/turnstile/v0/api.js) y un div con la clase cf-turnstile; es el único script de terceros, solo se carga si el proveedor está activo, y no hay JavaScript propio, atributos style ni IJSRuntime.` The project memory note on the views rule (two files) is outdated and is corrected there to the six existing modules plus this exception.
- AGENTS.md section 4.4: the logging rule extends to emails, tokens and links.
- AGENTS.md section 4.6: the four switches and their defaults, the `NotAllowed` rule, the uniform responses, the `Email:PublicBaseUrl` rule, the startup guards, and the Data Protection caveat.
- README: the configuration table (`RateLimiting:*`, `Captcha:Provider`, `Turnstile:SiteKey`, `Turnstile__SecretKey`, `Email:Provider`, `Email:RequireConfirmedEmail`, `Email:PublicBaseUrl`, `Smtp:*`, `Smtp__Password`), the free options to obtain keys (Turnstile; Brevo, Resend or a Gmail app password), the dependence of the limiter on forwarded headers, the redeploy caveat, and the outage recovery (`Captcha__Provider=None`).
- `docs/mejoras-posteriores.md`: record MailKit or an HTTP-API sender, captcha on login and invitation codes as follow-ups.
