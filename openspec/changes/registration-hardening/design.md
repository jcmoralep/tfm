# Design: Registration hardening (RF-67 follow-ups)

## Technical Approach

There are four switches, and each one is off by default. With no configuration, the pipeline, the DI graph and the pages render and behave exactly as on `main`. Each switch copies the `AssistantProviderSetting`/`StorageOptions` pattern: `BindAndValidate(IConfiguration)` is called eagerly. An empty value means the safe default. A failure names the env variable and never echoes a value.

- **S1**: the built-in `Microsoft.AspNetCore.RateLimiting` (shared framework, no package) as a global partitioned limiter that selects only the auth POSTs. The 429 is rendered by the existing status-code re-execute.
- **S2**: `ICaptchaVerifier` (Application) has two implementations: `NoneCaptchaVerifier` and `TurnstileCaptchaVerifier` (named `HttpClient`). It fails closed. One `CaptchaWidget.razor` holds the widget.
- **S3**: `IEmailSender` (Application) only enqueues. A bounded `Channel` and an `EmailDispatcher : BackgroundService` hand each message to `IEmailTransport` (`SmtpEmailTransport`, `System.Net.Mail`). The Spanish plain-text templates live in Application.
- **S4/S5**: `AddDefaultTokenProviders` plus a dedicated 2-hour reset provider. A new `IAccountTokenService` port (Identity adapter) and an `IAccountLinks` port (Web adapter, built from `Email:PublicBaseUrl`). One command per use case, one static-SSR page per flow, and uniform results whenever confirmation is on.

### Corrections to the proposal (verified against the code)

| Proposal | Reality | Consequence |
|---|---|---|
| "Spanish 429 page" | `Program.cs` calls `UseStatusCodePagesWithReExecute("/not-found")`, so an empty 429 would render "Página no encontrada" | Change it to `UseStatusCodePagesWithReExecute("/not-found", "?codigo={0}")`. `NotFound.razor` branches on `codigo == 429` (D2) |
| Fix `NotAllowed` with `CheckPasswordAsync` | `CheckPasswordAsync` does not count failures, and `PreSignInCheck` returns `NotAllowed` **before** the lockout check, so unconfirmed accounts could be brute-forced without lockout | On `NotAllowed`: if locked, return `LockedOut`. If the password matches, return `NotAllowed`. Otherwise call `AccessFailedAsync` and return `InvalidCredentials` (D14) |
| "No timing path reveals an email" | Login with an unknown email returns before hashing (`PasswordSignInAsync` → `FindByNameAsync` null), which is a timing leak that already exists | When the user is unknown, verify the password against a fixed dummy hash (D15) |
| "Smtp needs ... `Smtp__Password`" | Local sinks (Mailpit, aiosmtpd) have no authentication | The password is required only when `Smtp:Username` is set (D8) |
| "SMTP sender + background queue" | One class cannot be both the request-side seam and the transport | `IEmailSender` (Application, enqueue) is separate from `IEmailTransport` (Infrastructure, SMTP) (D9) |
| Pages at fixed paths | `@page` needs a literal, while links are built in code | New pages use `@attribute [Route(AuthenticationEndpoints.X)]` with constants, so emailed links cannot drift (D17) |
| `AddDefaultTokenProviders` "just works" | `DataProtectorTokenProvider` needs `IDataProtectionProvider`, which `AddAuthentication` (Web) supplies | Infrastructure tests that build the provider call `services.AddDataProtection()` |

## Architecture Decisions

| # | Topic | Options and tradeoff | Decision |
|---|---|---|---|
| 1 | Limiter shape | `[EnableRateLimiting]` on components (it also throttles GET), or a global limiter | **A global `PartitionedRateLimiter<HttpContext>`** (`Web/Security/AuthRateLimiting.cs`). The pure `IsLimited(HttpRequest)` is true for POSTs only, to `/login`, `/registro`, `/confirmar-correo/reenviar`, `/recuperar-contrasena` and `/restablecer-contrasena` (OrdinalIgnoreCase, from the `AuthenticationEndpoints` constants). Any other request gets `GetNoLimiter`. The partition key is `RemoteIpAddress?.ToString() ?? "unknown"`, **one bucket per IP shared by all auth POSTs**. It is a fixed window with `PermitLimit` and `Window` from options and `QueueLimit = 0`. When the switch is off, neither `AddRateLimiter` nor `UseRateLimiter` is called. `UseRateLimiter()` goes after `UseHttpsRedirection()` and before `UseAuthentication()`, so it sits downstream of the status-code middleware |
| 2 | 429 rendering | Write HTML in `OnRejected` (duplicates App/layout), redirect (loses 429), or re-execute | **Re-execute.** `RejectionStatusCode = 429`. `OnRejected` sets `Retry-After` (from `MetadataName.RetryAfter`, default window seconds) and logs a Warning `"Auth POST throttled on {Path}"` (no IP), with no body. The status-code page re-executes `/not-found?codigo=429`. `NotFound.razor` gains `[SupplyParameterFromQuery(Name = "codigo")] int? Code` and, for 429, renders "Demasiados intentos" / "Hiciste demasiados intentos en poco tiempo. Espera un minuto y vuelve a intentarlo." with a link to `/login`. The re-executed path is not limited. **To verify in S1 (Chrome):** the .NET 10 component endpoint treats a re-executed POST as a GET (no form dispatch or antiforgery). If it does not, `OnRejected` writes `303` to `/not-found?codigo=429` and the page sets `HttpContext.Response.StatusCode = 429` |
| 3 | `RateLimitOptions` (Web/Security) | | Keys `RateLimiting:Enabled` (bool, default false; a non-bool throws), `PermitLimit` (default 10, 1–1000) and `WindowSeconds` (default 60, 1–3600). The numbers are validated **only when Enabled**, as Gemini is. Messages: `"'RateLimiting:PermitLimit' must be an integer from 1 to 1000."` |
| 4 | Captcha port | `bool Verify`, or an outcome with a reason | **`Task<CaptchaOutcome> VerifyAsync(string? token, CancellationToken ct)`**, where `CaptchaOutcome { Passed, Rejected, Unavailable }`. A missing token, a blank one or more than 2,048 characters gives `Rejected` with no call. There is no `remoteip` (it would be wrong behind the proxy until Railway-prep). `CaptchaWidgetSettings(string? SiteKey)` (Application record, registered by Infrastructure like `ContextLimits`) feeds the widget |
| 5 | Turnstile | Typed client, or a named one | **A named client `"Turnstile"`**, `Timeout = 10 s`, `BaseAddress = https://challenges.cloudflare.com/`. It POSTs form-urlencoded `turnstile/v0/siteverify` with `secret` and `response`, and reads `success` and `error-codes`. `success: true` gives Passed. `success: false` gives Rejected. A non-2xx status, a timeout, an `HttpRequestException` or bad JSON gives **Unavailable (fail closed)**. The log shows outcome, error codes, status and duration only, never the token or the secret. `NoneCaptchaVerifier` always returns Passed and has a null SiteKey |
| 6 | `CaptchaOptions` | | `Captcha:Provider` (`None` \| `Turnstile`, unknown values list the valid ones). With Turnstile, `Turnstile:SiteKey` and `Turnstile:SecretKey` are both required: `"Captcha:Provider is Turnstile but the secret key is not configured. Set Turnstile__SecretKey (or the user-secret Turnstile:SecretKey)."` |
| 7 | Widget | Script in `wwwroot/js`, or the official script | **`Web/Components/Shared/CaptchaWidget.razor`**, which renders nothing when `SiteKey` is null. Otherwise it renders `<div class="cf-turnstile" data-sitekey="@key" data-language="es" data-size="flexible"></div>`, `<noscript><p class="text-sm text-danger">Activa JavaScript para completar la verificación.</p></noscript>` and `<script src="https://challenges.cloudflare.com/turnstile/v0/api.js" async defer></script>`. It is placed **inside** the `EditForm` (implicit render injects `cf-turnstile-response` there). The token is read with `[SupplyParameterFromForm(FormName = "register", Name = "cf-turnstile-response")] string? CaptchaToken`. **Exception in AGENTS §2 (JavaScript row) and §4.5:** "the only third-party script, Cloudflare Turnstile `api.js`, on `/registro` and `/recuperar-contrasena`, only when `Captcha:Provider=Turnstile`; it is not self-hosted or proxied (Cloudflare forbids it)." The memory note `views-css-classes-only` gets the same line |
| 8 | Email options | | **`EmailOptions.BindAndValidate`**: `Email:Provider` (`None` \| `Smtp`), `RequireConfirmedEmail` (bool, default false) and `PublicBaseUrl`. **`SmtpOptions`** (only when Smtp): `Host` (required), `Port` (default 587, 1–65535; **465 throws**: "implicit TLS is not supported by System.Net.Mail; use 587 (STARTTLS)"), `EnableSsl` (default true; **false is allowed only for `localhost`/`127.0.0.1`**), `Username` (optional), `Password` (required when Username is set; the message names `Smtp__Password`), `From` (required, a valid `MailAddress`), `FromName` (default "Ruta de Producto") and `TimeoutSeconds` (default 30, 1–120). `PublicBaseUrl` is required with Smtp: an absolute URL, https unless the host is localhost, trailing `/` trimmed. The values are exposed to Application as `AccountEmailSettings(bool EmailEnabled, bool RequireConfirmedEmail, Uri? PublicBaseUrl)` |
| 9 | Sending | Await SMTP in the request (timing leak, slow), or queue | **`IEmailSender.Enqueue(EmailMessage)`** (sync, non-blocking `TryWrite`). The implementations are `QueuedEmailSender` (Smtp) and `DisabledEmailSender` (None: logs `"Email {Kind} not sent: Email:Provider is None"`). The channel is `Channel.CreateBounded<EmailMessage>(100, FullMode = DropWrite)`; when it is full, a Warning is logged with the kind only. **`EmailDispatcher : BackgroundService`**, a single reader: `await foreach (ReadAllAsync(stoppingToken))` → `IEmailTransport.SendAsync(msg, stoppingToken)`. Each failure is caught and logged (`Kind`, exception type, `SmtpStatusCode`), with no retry. **Shutdown:** the stopping token cancels the in-flight `SendMailAsync`, and queued messages are lost (accepted: the person can resend). `SmtpEmailTransport` creates one `SmtpClient` per message (`Timeout` in ms, `NetworkCredential` when a Username is set) and sends plain-text UTF-8 `MailMessage` |
| 10 | Templates | HTML, or plain text | **Plain text, Spanish**, in `Application/Features/Authentication/AccountEmails.cs`, through `EmailMessage(string To, EmailKind Kind, string Subject, string Body)` with `ToString() => Kind`. There are three kinds. `ConfirmEmail`: "Confirma tu correo en Ruta de Producto" (link, valid 24 h). `AccountExists`: "Ya tienes una cuenta en Ruta de Producto" (login link plus forgot link: "si no fuiste tú, ignora este correo"). `ResetPassword`: "Restablece tu contraseña de Ruta de Producto" (link, valid 2 h). Nothing is sent for unknown addresses |
| 11 | Token providers | | `.AddDefaultTokenProviders()` plus `.AddTokenProvider<PasswordResetTokenProvider>("PasswordReset")`, with `options.Tokens.PasswordResetTokenProvider = "PasswordReset"`. `services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = 24 h)` and `Configure<PasswordResetTokenProviderOptions>(o => { o.Name = "PasswordResetDataProtectorTokenProvider"; o.TokenLifespan = 2 h; })`. A reset rotates the security stamp, so the link works once. `options.SignIn.RequireConfirmedEmail = email.RequireConfirmedEmail` |
| 12 | Token port | Grow `IAuthService`, or a new port | **`IAccountTokenService`** (Abstractions/Authentication) and **`IdentityAccountTokenService`**. It encodes and decodes with `WebEncoders.Base64Url` (`AccountTokenCodec`, which returns null on malformed input, so Application never sees ASP.NET). It logs the user id and outcome only |
| 13 | Register | | `RegisterCommand(Email, Password, ConfirmPassword, string? CaptchaToken = null)`. The handler order is: captcha (not Passed → `CaptchaRejected`/`CaptchaUnavailable`, no account), then `RegisterAsync`. **Confirmation off:** today's behavior (auto sign-in; `EmailTaken` is shown). **On:** Succeeded → token → enqueue `ConfirmEmail` → `ConfirmationPending`, with **no sign-in**. EmailTaken → enqueue `AccountExists` → `ConfirmationPending`. InvalidPassword is unchanged (the password is validated before the duplicate check, so it does not leak). `Register.razor` replaces the form with a green panel: "Revisa tu correo: te enviamos un enlace para confirmar tu cuenta." When confirmation is off, nothing is emailed |
| 14 | Sign-in fix | | In `IdentityAuthService.SignInAsync`, when `IsNotAllowed`: if the account is locked, return `LockedOut`. If `CheckPasswordAsync` matches, return `NotAllowed`. Otherwise call `AccessFailedAsync` and return `InvalidCredentials`. This is extracted as the pure `SignInOutcome.Resolve(SignInResult, bool lockedOut, bool passwordMatches)` for tests. `Login.razor` maps `NotAllowed` to "Confirma tu correo antes de iniciar sesión." plus a link to `/confirmar-correo/reenviar`, and shows "¿Olvidaste tu contraseña?" when `EmailEnabled` |
| 15 | Timing | | Unknown user at login: `IPasswordHasher.VerifyHashedPassword(dummy, DummyHash, password)` (the hash is computed once, lazily), then `InvalidCredentials`. Forgot and resend differ only by a DataProtection protect and a `TryWrite` (microseconds), which is accepted. The responses are identical text and status |
| 16 | Confirm, resend, forgot, reset | | `ConfirmEmailCommand(UserId, Code) → bool`, run on **GET** (as in Identity's default UI): ✓ "Tu correo quedó confirmado. Ya puedes iniciar sesión."; ✗ "El enlace no es válido o venció. Pide uno nuevo." plus the resend link. `ResendConfirmationCommand(Email) → Unit` sends only for an existing **unconfirmed** account. `ForgotPasswordCommand(Email, CaptchaToken) → ForgotPasswordResult {Accepted, CaptchaRejected, CaptchaUnavailable}` sends for **any existing account**, because the mailbox is the proof. `ResetPasswordCommand(UserId, Code, Password, ConfirmPassword) → PasswordResetResult {Succeeded, InvalidLink, InvalidPassword(errors)}`; success also sets `EmailConfirmed = true` and does not sign in. The uniform texts are: resend "Si hay una cuenta pendiente de confirmar con ese correo, te enviamos un enlace nuevo."; forgot "Si el correo corresponde a una cuenta, te enviamos un enlace para restablecer la contraseña. Vence en 2 horas." Every command overrides `ToString()`. The validators reuse the register rules and Spanish messages |
| 17 | Pages and links | | Static SSR (`[ExcludeFromInteractiveRouting]`, `AuthLayout`, `EditForm method="post"`): `ConfirmEmail.razor` `/confirmar-correo`, `ResendConfirmation.razor` `/confirmar-correo/reenviar`, `ForgotPassword.razor` `/recuperar-contrasena`, `ResetPassword.razor` `/restablecer-contrasena` (query `usuario` and `codigo`; the SSR form action keeps the query, so there are no hidden fields; it reuses `PasswordField`, the rules and the match scripts). Recovery and resend pages call `Navigation.NotFound()` when `EmailEnabled` is false. **`IAccountLinks`** (Web `AccountLinks`): `EmailConfirmation(userId, code)`, `PasswordReset(userId, code)`, `Login()`, `ForgotPassword()`. Each is `PublicBaseUrl` + path constant + `Uri.EscapeDataString` query, **never** `Request.Host` |
| 18 | Startup matrix | | See the table below. Every check runs in `AddInfrastructure` (Captcha, Email/Smtp) or in `Program` (RateLimiting) before `Build()` |

### Startup validation matrix

| Configuration | Result |
|---|---|
| Nothing set | Starts; all four switches off |
| `Email:RequireConfirmedEmail=true`, Provider None/absent | Throws: "'Email:RequireConfirmedEmail' is true but 'Email:Provider' is None; set Email__Provider=Smtp." |
| `Email:Provider=Smtp` without Host / From / PublicBaseUrl | Throws, naming `Smtp__Host` / `Smtp__From` / `Email__PublicBaseUrl` |
| Smtp with Username and no Password | Throws, naming `Smtp__Password` |
| Port 465, `EnableSsl=false` on a non-local host, PublicBaseUrl http on a non-local host | Throws (rule only) |
| `Captcha:Provider=Turnstile` without SiteKey or SecretKey | Throws, naming `Turnstile__SiteKey` / `Turnstile__SecretKey` |
| Unknown `Email:Provider` / `Captcha:Provider` | Throws, listing the valid values |
| `RateLimiting:Enabled=true` with a bad PermitLimit/WindowSeconds; a non-bool Enabled | Throws (rule only) |

## Interfaces / Contracts

```csharp
// Application/Abstractions/Captcha
public enum CaptchaOutcome { Passed, Rejected, Unavailable }
public interface ICaptchaVerifier { Task<CaptchaOutcome> VerifyAsync(string? token, CancellationToken ct); }
public sealed record CaptchaWidgetSettings(string? SiteKey);
// Application/Abstractions/Email
public enum EmailKind { ConfirmEmail, AccountExists, ResetPassword }
public sealed record EmailMessage(string To, EmailKind Kind, string Subject, string Body) { public override string ToString() => Kind.ToString(); }
public interface IEmailSender { void Enqueue(EmailMessage message); }   // never blocks, never throws
public sealed record AccountEmailSettings(bool EmailEnabled, bool RequireConfirmedEmail, Uri? PublicBaseUrl);
// Application/Abstractions/Authentication
public sealed record AccountToken(string UserId, string Code);           // Code is Base64Url
public interface IAccountTokenService
{
    Task<AccountToken?> CreateEmailConfirmationAsync(string email, CancellationToken ct); // null: unknown or already confirmed
    Task<bool> ConfirmEmailAsync(string userId, string code, CancellationToken ct);
    Task<AccountToken?> CreatePasswordResetAsync(string email, CancellationToken ct);     // null: unknown
    Task<PasswordResetResult> ResetPasswordAsync(string userId, string code, string password, CancellationToken ct);
}
public interface IAccountLinks { Uri EmailConfirmation(AccountToken t); Uri PasswordReset(AccountToken t); Uri Login(); Uri ForgotPassword(); }
// RegistrationStatus gains ConfirmationPending, CaptchaRejected, CaptchaUnavailable
// Infrastructure/Email
internal interface IEmailTransport { Task SendAsync(EmailMessage message, CancellationToken ct); }
```

## Data Flow

```
POST /registro -> [limiter: IP bucket] -> Register.razor -> RegisterCommand
  -> ICaptchaVerifier -> IAuthService.RegisterAsync
  -> (confirm on) IAccountTokenService -> AccountEmails -> IEmailSender.Enqueue -> Channel
                                                   EmailDispatcher -> SmtpEmailTransport -> SMTP
  -> "Revisa tu correo" (new or duplicate)  |  (confirm off) SignInAsync -> redirect
GET  /confirmar-correo?usuario&codigo -> ConfirmEmailCommand -> ok | "pide uno nuevo"
POST /recuperar-contrasena -> captcha -> CreatePasswordResetAsync -> Enqueue? -> uniform text
POST /restablecer-contrasena?usuario&codigo -> ResetPasswordCommand -> /login
429: limiter OnRejected -> StatusCodePages re-execute /not-found?codigo=429
```

## File Changes (authored lines)

| Slice | Files | Lines |
|---|---|---|
| **S1** | `Web/Security/{RateLimitOptions, AuthRateLimiting}.cs`; `Program.cs` (bind, `AddRateLimiter`, `UseRateLimiter`, re-execute query); `AuthenticationEndpoints` (path constants for later slices); `NotFound.razor` (429 branch); `appsettings.json` (`RateLimiting` block, `Enabled: false`); tests `Web.Tests/Security/{RateLimitOptionsTests, AuthRateLimitingTests}`; README; `REQUISITOS.md` RF-78 | ~220 |
| **S2** | `Application/Abstractions/Captcha/*`; `RegisterCommand`/handler/`RegistrationResult` (captcha statuses); `Infrastructure/Captcha/{CaptchaOptions, NoneCaptchaVerifier, TurnstileCaptchaVerifier, CaptchaServiceCollectionExtensions}`; `DependencyInjection`; `Shared/CaptchaWidget.razor`; `Register.razor`; `appsettings.json`; tests `TurnstileCaptchaVerifierTests`, `CaptchaOptionsTests`, `RegisterCommandTests` (captcha cases), `FakeCaptchaVerifier`; `AGENTS.md` §2/§4.5 exception; README; RF-79, RNF-03 | ~350 |
| **S3** | `Application/Abstractions/Email/*`; `Features/Authentication/AccountEmails.cs`; `Infrastructure/Email/{EmailOptions, SmtpOptions, QueuedEmailSender, DisabledEmailSender, EmailDispatcher, IEmailTransport, SmtpEmailTransport, EmailServiceCollectionExtensions}`; `DependencyInjection`; `appsettings.json`; tests `EmailOptionsTests` (the matrix), `EmailDispatcherTests` (fake transport: delivery, failure logged without the address, full queue drops), `AccountEmailsTests`; RF-80, RNF-02 | ~320 |
| **S4** | `IAccountTokenService`, `AccountToken`, `IAccountLinks`; `Infrastructure/Identity/{IdentityAccountTokenService, AccountTokenCodec, PasswordResetTokenProvider, SignInOutcome}`; `IdentityAuthService` (D14, D15); `DependencyInjection` (providers, lifespans, `RequireConfirmedEmail`); `Register/RegisterCommandHandler`; `ConfirmEmail/`, `ResendConfirmation/` (command, validator, handler); `Web/Authentication/AccountLinks.cs`; pages `ConfirmEmail.razor`, `ResendConfirmation.razor`; `Login.razor`, `Register.razor`; tests `RegisterCommandTests`, `AccountConfirmationTests`, `SignInOutcomeTests`, `AccountTokenCodecTests`, `PasswordPolicyTests` (providers, lifespans, the flag), `AccountLinksTests`, TestDoubles `RecordingEmailSender`, `FakeAccountTokenService`, `FakeAccountLinks`; RF-67, RF-81 | ~390 |
| **S5** | `ForgotPassword/`, `ResetPassword/` (command, validator, handler, results); `IdentityAccountTokenService` (reset); pages `ForgotPassword.razor` (with `CaptchaWidget`), `ResetPassword.razor`; `Login.razor` link; tests `PasswordRecoveryTests`, validators; README (Mailpit, Turnstile test keys, Railway SMTP caveat); RF-82; `docs/mejoras-posteriores.md` (HTTP-API sender, MailKit, retries) | ~380 |

## Testing Strategy (xUnit, lean, Theory-grouped, no network)

| Area | Cases |
|---|---|
| Options | One Theory per options class across the matrix rows: valid, missing, a bad number, unknown provider, 465, non-local `EnableSsl=false`, http base URL. It also asserts that the **message never contains the configured secret** |
| Limiter | `IsLimited` Theory (POST `/login` and `/LOGIN` yes, GET `/login` no, POST `/logout` no). One Fact on the real limiter: 10 leases are granted, the 11th is refused, another IP is granted, and GET is always granted |
| Turnstile | Stub `HttpMessageHandler`, as a Theory: success → Passed; `success:false` → Rejected; 500, timeout and bad JSON → Unavailable; blank or 2,049 characters → Rejected with zero calls. One Fact checks that the form carries the secret and the response and that the captured log has neither |
| Email | The dispatcher delivers what was enqueued; a transport exception is logged without the address and the loop continues; the 101st message is dropped. The templates Theory: each kind has its Spanish subject and the link, and `ToString` is the kind only |
| Handlers | Register Theory over {confirm off/on} × {new, duplicate, bad password, captcha rejected/unavailable}: the result, the sign-in call and the email kind queued. Resend and forgot: known/unknown/confirmed give **the same result**, and an email is sent only when expected. Confirm and reset map their outcomes. One Theory checks that no command `ToString` exposes the email, password, code or captcha token |
| Identity | `SignInOutcome.Resolve` Theory (locked, password ok, password wrong → AccessFailed). `AccountTokenCodec` round trip plus malformed input → null. Registration: `RequireConfirmedEmail` follows the configuration, the providers are named, and the lifespans are 24 h / 2 h |
| Web | `AccountLinks` Theory: base with and without a trailing slash, with a path base, the query escaped, and it ignores any `Host` |
| Manual (Chrome) | See below. Plus a `rg 'style=|<script'` audit: the only new `<script>` is in `CaptchaWidget.razor` |

### Manual verification plan

1. **S1:** `RateLimiting__Enabled=true`. Eleven quick POSTs to `/login` (wrong password) → the 11th is "Demasiados intentos" with status 429 and `Retry-After` (DevTools Network). GET `/login` still works. The log has no IP.
2. **S2:** `Captcha__Provider=Turnstile`. Site key `1x00000000000000000000AA` with secret `1x0000000000000000000000000000000AA` (always passes) → the account is created. Secret `2x0000000000000000000000000000000AA` (always fails) → the Spanish rejection. JS disabled → the noscript text plus the rejection. Site key `2x00000000000000000000AB` (always blocks) → no token.
3. **S3–S5 (SMTP sink):** `docker run --rm -d --name mailpit -p 1025:1025 -p 8025:8025 axllent/mailpit`, then read the mail in Chrome at `http://localhost:8025`. As a fallback with no Docker, `pip install aiosmtpd` and `python -m aiosmtpd -n -l 127.0.0.1:1025` prints the messages. Configuration: `Email__Provider=Smtp`, `Email__RequireConfirmedEmail=true`, `Email__PublicBaseUrl=https://localhost:<port>`, `Smtp__Host=localhost`, `Smtp__Port=1025`, `Smtp__EnableSsl=false`, `Smtp__From=no-reply@ruta.local`. The walkthrough: register → "Revisa tu correo" → login refused with "Confirma tu correo" → the link confirms → login works. Re-register the same email → the same panel, and Mailpit shows "Ya tienes una cuenta". Resend for unknown, confirmed and unconfirmed emails → the same text, and only the unconfirmed one gets mail. Forgot → reset → the old password fails and the new one works; reusing the link → "pide uno nuevo". A tampered `codigo` → invalid. Finally check that `Logs/` has no email, token or link, then take everything down.

## Threat Matrix

N/A: there is no routing-to-shell, subprocess, VCS/PR automation, executable classification or process-integration boundary. The auth threats are covered by decisions and tests: brute force and mass registration (D1, D14), bots (D4–D7), enumeration by text and timing (D13–D16), host-header link poisoning (D17), secret leakage (D5, D8, the options tests) and token replay (D11).

## Migration / Rollout

No migration: the Identity columns (`EmailConfirmed`, `SecurityStamp`, lockout) already exist. Each slice ships off by default. Open-registered accounts that already exist are unconfirmed. When `RequireConfirmedEmail` is turned on, they must use the resend page (the seeded users are confirmed); the README states this. The limiter stays off until forwarded headers exist (Railway-prep). Rollback: reset the switches; to revert code, revert the slice PRs in reverse order.

## Open Questions

None that block. Accepted assumptions: the re-executed POST renders as a GET (verified in S1, with the D2 fallback); queued emails are lost on restart; there is one IP bucket for all auth POSTs; confirmation runs on GET, so mail scanners may confirm (this proves only that the mailbox is reachable).
