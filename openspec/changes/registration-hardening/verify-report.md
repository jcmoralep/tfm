```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:5e5f7f4661d35da337ab09dcc171f15701d96a2d946ecac9bde4c1490d09f24b
verdict: fail
blockers: 3
critical_findings: 3
requirements: 0/7
scenarios: 36/44
test_command: dotnet test RutaProducto.slnx -c Release --no-build
test_exit_code: 0
test_output_hash: sha256:f1a62ccf363f31d0f6cd4ca686dc703176cbed9fa384d162078fa18fec874f1c
build_command: dotnet build RutaProducto.slnx -c Release --no-restore
build_exit_code: 0
build_output_hash: sha256:07ccf0deae6cfb35bcd5b8ae5de144df70413832cda62e1ea5888ed74a91a50f
```

## Verification Report

**Change**: registration-hardening
**Version**: N/A (full new capability spec `authentication`)
**Mode**: Standard (strict_tdd false)
**Branch / commit**: fix/hardening-verify at 4182612 (S1-S5 and the validation pass merged as PRs #47 to #51)

### Completeness
| Metric | Value |
|--------|-------|
| Tasks total (leaf and parent boxes in tasks.md) | 74 |
| Tasks marked complete | 55 |
| Tasks incomplete | 19 |

Unchecked: 1.6 sub-bullets (5; the parent 1.6 is ticked and the S1 notes at the end of tasks.md support them), 2.6 (+7 sub-bullets), 2.G, 4.10 (+9), 4.G, 5.7 (+7), 5.G, F.1 to F.5. The three slice-gate boxes (2.G, 4.G, 5.G) are stale: the PRs are merged. The walkthrough boxes 2.6/4.10/5.7 are only partly backed by `docs/evidencias/registration-hardening-recorrido-chrome.md` (see W4, W5). Group F is open by plan order (F.5 says "then run the independent verify"), but F.1 produces a spec-mandated deliverable that is missing (C3).

Spec shape: 7 requirements, 44 scenarios (36 UNIT, 6 UI, 1 DOC, 1 MANUAL).

### Build & Tests Execution
**Build**: PASSED (0 errors; the only warnings are 8 x NU1507 NuGet source-mapping, i.e. zero code warnings)
```text
dotnet restore RutaProducto.slnx --ignore-failed-sources -p:NuGetAudit=false -p:WarningsNotAsErrors=NU1507   -> exit 0
dotnet build RutaProducto.slnx -c Release --no-restore   -> "Compilacion correcta", 8 warnings (NU1507 only), 0 errors, exit 0
```
**Tests**: PASSED, 1,788 passed / 0 failed / 0 skipped
```text
ArchitectureTests 31, Web.Tests 256, Application.Tests 976, Infrastructure.Tests 525  (exit 0)
```
No RutaProducto.Web process was running. **Coverage**: not available (no coverage tool configured).

Extra runtime probes run by the verifier (outside the suite, nothing left behind):
1. A throwaway `mysql:8.4` container (removed afterwards): the server collation is `utf8mb4_0900_ai_ci`; comparing "A@EXAMPLE.COM" with "A@EXÁMPLE.COM" returned 1 row (match); a trailing-space variant returned 0.
2. A throwaway minimal ASP.NET (net10) app mapping `POST /login`: `POST /login/` and `POST /LOGIN/` reached the `/login` endpoint with `Request.Path` = `/login/` (`//login` gave 404). The process was killed afterwards.

### Spec Compliance Matrix
COMPLIANT = covering test passed (or, for [UI]/[DOC]/[MANUAL], the evidence supports it). PARTIAL = covered in part.

| Requirement | Scenario | Test / evidence | Result |
|---|---|---|---|
| Rate limiting | Default is off [UNIT] | `RateLimitOptionsTests > Absent_settings_give_the_disabled_defaults` (Program half: S1 notes) | COMPLIANT |
| Rate limiting | Only auth POSTs are selected [UNIT] | `AuthRateLimitingTests > Only_auth_posts_are_selected` (passes, but see C2) | COMPLIANT (spec-conformant; the spec premise is flawed) |
| Rate limiting | Invalid numbers fail at startup [UNIT] | `RateLimitOptionsTests > Bad_numbers_fail_only_when_enabled_and_never_echo_the_value`, `A_non_boolean_enabled_throws...` | COMPLIANT |
| Rate limiting | Eleventh POST is rejected [UI] | tasks.md "S1 walkthrough notes": 10 x 200, 11th/12th 429, `Retry-After: 60`, page rendered, GET ok, log path-only | COMPLIANT |
| Rate limiting | Partition is per IP [UNIT] | `AuthRateLimitingTests > The_partition_is_the_client_ip_or_unknown`, `The_eleventh_post...` | COMPLIANT |
| Captcha | Default provider is None [UNIT] | `CaptchaOptionsTests > Without_a_provider_the_captcha_is_off_and_passes_anything` | COMPLIANT |
| Captcha | Unknown provider lists valid values [UNIT] | `CaptchaOptionsTests > An_unknown_provider_lists_the_valid_values` | COMPLIANT |
| Captcha | Turnstile requires its keys [UNIT] | `CaptchaOptionsTests > Turnstile_requires_both_keys...`, `A_configured_secret_never_appears...` | COMPLIANT |
| Captcha | Siteverify outcomes [UNIT] | `TurnstileCaptchaVerifierTests > Siteverify_answers_map_to_an_outcome`, `A_timeout_...`, `A_network_error_...` | COMPLIANT |
| Captcha | Empty token makes no call [UNIT] | `TurnstileCaptchaVerifierTests > A_missing_blank_or_oversized_token_is_rejected_without_a_call` | COMPLIANT |
| Captcha | Failure blocks registration before any lookup [UNIT] | `RegisterCommandTests > A_captcha_that_did_not_pass_creates_no_account_signs_no_one_in_and_emails_nobody` (asserts `RegisteredEmail` null; reordering the guard fails it) | COMPLIANT |
| Captcha | Messages do not leak [UNIT] | `TurnstileCaptchaVerifierTests > The_request_carries_only_the_secret_and_the_token_and_nothing_leaks_to_the_log` | COMPLIANT |
| Captcha | Widget and test keys [UI] | evidence doc row "Captcha (S2)": widget on `/registro`, empty token rejected, always-fail secret on `/recuperar-contrasena`, None shows no widget. Not observed: Spanish rendering, `/login` has no widget, JS off, always-block key, blocked Cloudflare, fresh token after a validation error | PARTIAL (W5) |
| Email sending | Default provider is None [UNIT] | `EmailRegistrationTests > Without_configuration_...`, `EmailOptionsTests > Without_a_provider_email_is_off...` | COMPLIANT |
| Email sending | Smtp requires its settings [UNIT] | `EmailOptionsTests > An_invalid_smtp_configuration_stops_the_app...` (15 rows) + `A_local_sink_needs_no_tls...` | COMPLIANT |
| Email sending | Unknown provider [UNIT] | `EmailOptionsTests > An_unknown_provider_lists_the_valid_values` | COMPLIANT |
| Email sending | Queue returns before sending [UNIT] | `EmailDispatcherTests > Enqueue_returns_at_once...`, `The_101st_message_is_dropped...` | COMPLIANT |
| Email sending | Sender failure is contained [UNIT] | `EmailDispatcherTests > A_failing_send_is_logged_without_the_address_and_the_next_message_still_goes` | COMPLIANT |
| Email sending | Links come from configuration [UNIT] | `AccountLinksTests` (4 tests) + evidence doc "Enlaces del correo" (forged Host) | COMPLIANT |
| Email sending | Real delivery [UI] | evidence doc: mails received in Spanish with working links; logs clean | COMPLIANT |
| Email sending | Provider deliverability [MANUAL] | never executed (evidence doc last paragraph; README notes the Railway SMTP caveat) | UNTESTED (W7) |
| Confirmation | Startup guard [UNIT] | `EmailOptionsTests > Requiring_confirmation_without_smtp_stops_the_app`, `..._with_smtp_passes` | COMPLIANT |
| Confirmation | Default behavior is unchanged [UNIT] | `RegisterCommandTests > With_confirmation_off_...` (2 tests) | COMPLIANT |
| Confirmation | Registration with confirmation required [UNIT] | `RegisterCommandTests > With_confirmation_on_a_new_account_gets_a_confirmation_email_and_is_not_signed_in` (asserts no sign-in) | COMPLIANT |
| Confirmation | Duplicate address is indistinguishable [UNIT] | `RegisterCommandTests > With_confirmation_on_a_duplicate_address_...` (passes; the property is broken in production by C1) | COMPLIANT as tested (see C1) |
| Confirmation | Confirmation succeeds and fails uniformly [UNIT] | `ConfirmationTokenTests` (valid + 6 bad-token cases incl. expired and another key ring) | COMPLIANT |
| Confirmation | NotAllowed is gated by the password [UNIT] | `SignInOutcomeTests > NotAllowed_is_gated_by_the_lockout_and_by_the_password` (pure half). The `UserManager` calls (lockout check, `AccessFailedAsync`) have no test and no walkthrough evidence | PARTIAL (W4) |
| Confirmation | Unknown email pays the hash cost [UNIT] | `SignInOutcomeTests > The_dummy_hash_is_computed_once...` (helper only; the call in `IdentityAuthService` is untested) | PARTIAL (W4) |
| Confirmation | Resend is uniform [UNIT] | `AccountConfirmationTests > Resend_gives_the_same_result_and_mails_only_a_pending_account` + evidence doc "Reenvio" | COMPLIANT |
| Confirmation | Seeded users keep working [UNIT] | `InitialUserSeederTests > Seeded_users_are_created_with_a_confirmed_email...` (asserts the flag, not a sign-in) | COMPLIANT |
| Confirmation | Full flow [UI] | evidence doc: uniform panel, no sign-in, early sign-in message, altered link fails, valid link confirms, duplicate registration. Contradicted: re-opened link still succeeds (accepted deviation, W1). Not shown: five wrong passwords lock the account | PARTIAL (W1, W4) |
| Password recovery | Forgot is uniform [UNIT] | `PasswordRecoveryTests > Forgot_gives_the_same_result_and_mails_only_an_existing_account_a_reset_link` (but see C1) | COMPLIANT as tested |
| Password recovery | Captcha runs before the lookup [UNIT] | `PasswordRecoveryTests > A_failed_captcha_stops_before_any_lookup` (spy `Calls` empty; removing the guard fails it) | COMPLIANT |
| Password recovery | Reset succeeds [UNIT] | `ResetTokenTests > A_valid_token_resets_the_password_once` | COMPLIANT |
| Password recovery | Reset rejects bad tokens uniformly [UNIT] | `ResetTokenTests > Every_bad_token_gives_the_same_invalid_link_and_never_throws` | COMPLIANT |
| Password recovery | Reset enforces the password policy [UNIT] | `ResetTokenTests > A_weak_password_is_reported_in_spanish_and_leaves_the_link_usable`; the scenario says the command is validated, the implementation checks strength in Identity after the token (W3) | PARTIAL (W3) |
| Password recovery | Reset confirms the email [UNIT] | `ResetTokenTests > Any_existing_account_gets_a_token_and_a_reset_confirms_the_address` | COMPLIANT |
| Password recovery | Link hidden without email [UI] | evidence doc "Valores por defecto" (link hidden, three pages answer 404 after the fix) | COMPLIANT (with W2) |
| Password recovery | Full flow [UI] | evidence doc "Recuperacion (S5)" | COMPLIANT |
| Data Protection | Unprotectable token is a uniform failure [UNIT] | `ConfirmationTokenTests` and `ResetTokenTests` "another-key-ring" rows | COMPLIANT |
| Data Protection | Caveat documented [DOC] | README lines 192 and 202 state it; `AGENTS.md` section 4.6 does not (C3) | FAILING |
| Secrets and logging | Placeholders only [UNIT] | `AppSettingsSecretsTests` (both files) | COMPLIANT |
| Secrets and logging | No content in logs [UNIT] | only `ToString()` Theories (`AccountConfirmationTests`, `PasswordRecoveryTests`, `RegisterCommandTests`) plus the dispatcher and Turnstile log tests; no capture for the token service, the limiter log or the handlers | PARTIAL (W6) |
| Secrets and logging | Defaults change nothing [UNIT] | `EmailRegistrationTests` (no worker) + the whole existing suite unedited and green | COMPLIANT |

**Compliance summary**: 36 of 44 scenarios compliant, 6 partial, 1 failing, 1 untested (manual).

### Correctness (Static Evidence)
| Requirement | Status | Notes |
|---|---|---|
| Rate limiting | Defective | Implemented as specified, but `IsLimited` does not see `POST /login/` (C2). Off by default, no limiter registered when disabled (`Program.cs:72-99`), `UseRateLimiter` after HTTPS redirection and before authentication, 429 re-execute, `Retry-After`, path-only warning all present |
| Captcha seam and Turnstile | Implemented | Captcha runs first in both handlers (`RegisterCommandHandler.cs:20`, `ForgotPasswordCommand.cs:47`); fail closed; secret only in the form body; log carries outcome, duration, status and a regex-filtered code list; `RemoveAllLoggers` on the named client; no `SecretKey` in committed settings |
| Email infrastructure | Implemented | Bounded queue with explicit fullness check, single dispatcher, exceptions not logged (only type and `SmtpStatusCode`), startup matrix complete. Recipient handling is the weak point (C1) |
| Email confirmation and resend | Implemented with a defect | Uniform panel and sign-in rules as designed; recipient taken from the request (C1); reused link succeeds (accepted, W1) |
| Password recovery | Implemented with a defect | Dedicated 2 h provider, stamp rotation makes the token single-use, a confirmation token is rejected as a reset token (tested); recipient from the request (C1) |
| Data Protection caveat | Partial | README yes, AGENTS 4.6 no (C3) |
| Secrets and logging | Implemented | No new log line carries an address, token, link or password; Serilog request logging records the path without the query, `Microsoft` overridden to Warning |

### Coherence (Design)
| Decision | Followed? | Notes |
|---|---|---|
| D1 limiter shape | No (flawed) | "a trailing slash is not special-cased" is wrong for ASP.NET routing, which serves `/login/` as `/login` (C2) |
| D2 429 re-execute | Yes | verified in Chrome (S1 notes); fallback not needed |
| D3 RateLimitOptions | Yes | |
| D4 to D7 captcha | Yes | binding path (attribute or Form fallback) still not distinguished |
| D8 to D10 email | Yes | `EmailQueue` adds an explicit fullness check (noted in the S3 note) |
| D11 to D12 providers and port | Yes | |
| D13 register | Yes | |
| D14 sign-in | Yes in code; runtime half not evidenced (W4) | |
| D15 dummy hash | Yes in code; wiring untested (W4) | |
| D16 resend, forgot, reset | Partly | recipient taken from the request (C1); reset validator is shape-only (W3) |
| D17 pages and links | Deviates | the three email-dependent pages redirect to `/not-found?codigo=404` instead of `Navigation.NotFound()` (W2); `AccountLinks` never uses the Host header (good) |
| D18 startup matrix | Yes | |

### Issues Found

**CRITICAL (3)**

**C1. Reset, confirmation and "account exists" emails go to the address typed in the request, not the stored address; with MySQL accent-insensitive collation this is account takeover and an enumeration oracle.**
- Where: `src/RutaProducto.Application/Features/Authentication/ForgotPassword/ForgotPasswordCommand.cs:61-68`, `ResendConfirmation/ResendConfirmationCommand.cs:40-47`, `Register/RegisterCommandHandler.cs:29,66-68`; lookup in `src/RutaProducto.Infrastructure/Identity/IdentityAccountTokenService.cs:42-55` (`FindByEmailAsync`); the test that codifies it: `tests/RutaProducto.Application.Tests/Features/Authentication/PasswordRecoveryTests.cs:43` (`Assert.Equal(email, message.To)`).
- Evidence: the Identity columns carry no explicit collation (migration `20261007021707_InitialIdentity.cs`), so they use the server default; the repo itself names it (`InitiativeConfiguration.cs:12`, `utf8mb4_0900_ai_ci`) and `docker-compose.yml` pins `mysql:8.4`. The verifier probe on mysql 8.4.11 showed "A@EXAMPLE.COM" equals "A@EXÁMPLE.COM". So `FindByEmailAsync("a@exámple.com")` finds the account of `a@example.com`, creates a valid reset token for it and the handler mails it to `a@exámple.com`. An attacker who owns an accent-variant domain of the victim domain (an IDN such as `exámple.com`; `MailAddress` converts it to punycode for SMTP) receives the victim reset link, resets the password and takes the account (the unauthenticated forgot form needs only the limiter and, if on, the captcha). Same mechanism through `/registro` with confirmation required: the duplicate check (`DuplicateEmail`) matches the variant, so the attacker mailbox gets "Ya tienes una cuenta" when the target exists and "Confirma tu correo" when it does not, which defeats the uniform-response property (proposal success criterion 3, spec "Duplicate address is indistinguishable"). Not reproduced end to end against the app (static trace plus the collation probe).
- Recommended fix: have `IAccountTokenService` return the stored `user.Email` with the token (or expose a lookup that returns the stored address) and mail only that; for the duplicate path look up the stored address instead of echoing the request value. Add a fake whose stored address differs from the typed one and assert `message.To` is the stored one. Additionally reject non-ASCII addresses in the three validators (and consider an accent-sensitive collation for `NormalizedEmail`/`NormalizedUserName`, which would need a migration and conflicts with "no new migration").

**C2. The rate limiter is bypassed by a trailing slash.**
- Where: `src/RutaProducto.Web/Security/AuthRateLimiting.cs:22-24` (`request.Path.Equals(path, OrdinalIgnoreCase)`); spec line 25 and design D1 ("a trailing slash is not special-cased").
- Evidence: probe app on the net10 SDK: `POST /login/` is routed to the `/login` endpoint while `Request.Path` stays `/login/`, so `IsLimited` returns false and the request gets `GetNoLimiter`. The same applies to `/registro/`, `/recuperar-contrasena/`, `/restablecer-contrasena/`, `/confirmar-correo/reenviar/`. Routing of the Razor component endpoints is expected to behave the same (not re-checked in the browser). Brute force, mass registration and mail bombing are therefore not throttled for anyone who adds a slash; Identity lockout still protects single accounts. `AuthRateLimitingTests` has no trailing-slash row, so the gap is invisible.
- Recommended fix: compare after trimming one trailing slash (or use segment-based matching), add `/login/` and `/registro/` rows to the Theory, and amend spec line 25 and D1.

**C3. Documentation deliverable missing: AGENTS section 4.6 (and 4.4) were never updated, so the "Caveat documented [DOC]" scenario fails.**
- Where: `AGENTS.md` section 4.6 still says "la verificación del correo queda para después" (stale, contradicts RF-81) and has none of: the four switches and defaults, the `NotAllowed` rule, uniform responses, the `Email:PublicBaseUrl` rule, the startup guards, the redeploy caveat. Section 4.4 does not extend the logging rule to emails, tokens and links. Spec line 258 and "Documentation edits" require both. This is task F.1 (open).
- Recommended fix: do F.1 as written; README already carries the caveat (lines 192, 202).

**WARNING (9)**

- **W1. Accepted deviation not reflected in all spec lines.** Re-opening a confirmation link after confirming still succeeds. Lines that still contradict it: `spec.md:198` ("the reused and altered links show El enlace no es válido o venció..."), `tasks.md:118` (walkthrough 4.10), `REQUISITOS.md:113` (RF-81: "Un enlace inválido, vencido o ya usado muestra siempre..."). `spec.md:144` is compatible ("reused-after-change"; "MAY show success"). No Reconciliation note exists for it (only the "Validation pass" paragraph and `docs/mejoras-posteriores.md:61`).
- **W2. Email-off pages: spec and design say `Navigation.NotFound()`, code redirects.** `ForgotPassword.razor:81-84`, `ResetPassword.razor:97-101`, `ResendConfirmation.razor:68-71`, constant `AuthenticationEndpoints.cs:17-18`, status set in `NotFound.razor:36`. Spec lines 146, 202, 239 and design D17 are stale; the change is recorded only in the "Validation pass" paragraph, not as a reconciliation note. The behavior is acceptable (302 then 404).
- **W3. "Reset enforces the password policy" differs from the code.** `ResetPasswordCommand.cs:20-32` validates only required, length and match; strength is enforced by Identity after the token check (`IdentityAccountTokenService.cs:71`), which keeps the link usable after a weak password (better behavior, covered by `ResetTokenTests.cs:100`). Spec lines 226-229 and task 5.3 still say the command validator rejects "abc"; recorded only in the S5 apply note.
- **W4. Sign-in hardening is unproven at runtime.** `IdentityAuthService.cs:46-70` (lockout check, `CheckPasswordAsync`, `AccessFailedAsync`, lock on the limiting failure) and `:35-39` (dummy-hash call) have no unit seam and no walkthrough evidence: the evidence doc does not show five wrong passwords locking an unconfirmed account, nor "locked + right password shows the lock". Deleting either block leaves all 1,788 tests green. Task 4.10 bullet 2 is unchecked.
- **W5. Captcha widget [UI] only partly evidenced.** Not observed: JS-off noscript text and rejection, always-block site key, blocked Cloudflare domain (fail-closed message), `/login` without widget, Spanish widget, fresh token after a validation error, and which binding path (attribute or `Request.Form` fallback) delivers `cf-turnstile-response` (`Register.razor:97-98,160-170`, `ForgotPassword.razor:70-71,116-126`). Tasks 2.6 and 5.7 remain unchecked. The fail-closed logic itself is unit-tested.
- **W6. Logging evidence is thin.** "No content in logs [UNIT]" is tested through `ToString()` only; nothing captures `IdentityAccountTokenService` (`:22,37,53,66,82,88,92`), the limiter warning (`AuthRateLimiting.cs:55-58`) or handler logs. By inspection they carry only user id, outcome, kind or path. `Program.cs` wiring (limiter registered only when enabled, middleware order) has no test, and the S1 step "restart with the limiter unset" was not run (`tasks.md:68,196`). The effect of an app restart on already-sent links was not exercised in the browser (unit tests cover another key ring).
- **W7. [MANUAL] provider deliverability never run;** Railway outbound SMTP policy is unverified (README and mejoras document it). It must be done before turning `Email:Provider` on in production.
- **W8. Stale text in RF-79.** `REQUISITOS.md:111` says "(y, cuando exista, la solicitud de recuperación de contraseña)" but the recovery page exists. The spec RF-79 edit text repeats the idea.
- **W9. Task bookkeeping.** 2.G, 4.G, 5.G are unchecked although the PRs are merged; the 1.6 sub-bullets are unchecked although the S1 notes cover them; F.2 to F.5 are open (the F.3 table "Cobertura de escenarios" does not exist in the evidence doc; the proposal success criteria are still unchecked; criterion 3 is contradicted by C1). Under the skill rule an unchecked core task is CRITICAL; they are reported as WARNING here only because the plan orders Group F before the verify gate and the three gates are demonstrably merged. The Reconciliation notes lack entries for W1, W2, W3 and the `PasswordResetResult` shape.

**SUGGESTION (7)**

- S1. The limiter partition is the textual client IP; one IPv6 user can rotate addresses inside a /64. Consider collapsing IPv6 to /64 when forwarded headers arrive (Railway-prep).
- S2. After a password reset the stamp rotates, but existing cookies stay valid until the next security-stamp revalidation (default 30 minutes). Consider a short `SecurityStampValidatorOptions.ValidationInterval`.
- S3. `DummyPasswordHash` is computed lazily on the first unknown-email sign-in (`DummyPasswordHash.cs:15-16`), so that first request is slower than later ones; warm it at startup.
- S4. Registration with confirmation shows the panel on the POST response (`Register.razor:143-148`); a browser refresh re-POSTs and queues another email. The limiter bounds it; a redirect after POST would remove it.
- S5. Identity `InvalidEmail` error is mapped to "La contraseña no cumple los requisitos." (`IdentityAuthService.cs:93`, `PasswordMessages.cs:18`); pre-existing, but misleading now that more address forms reach Identity.
- S6. `EmailOptions.ReadPublicBaseUrl` (`EmailOptions.cs:85-97`) does not reject a query, fragment or user-info in `Email:PublicBaseUrl`; links would be malformed.
- S7. The module-boundary test does not include the new Authentication area (no entities, so low value) and `AccountLinksTests.The_login_and_forgot_links_..._no_request_host_exists_to_leak` is close to tautological; both are fine to leave.

### Probe results requested by the caller
1. Divergences not in the Reconciliation notes: W1, W2, W3 (plus the C2 premise). Spec text that still contradicts the accepted idempotent-confirmation deviation: `spec.md:198`, `tasks.md:118`, `REQUISITOS.md:113`.
2. Security: enumeration through the recipient (C1); timing parity looks right (password hashing happens before the duplicate check in `UserManager.CreateAsync`, dummy hash for unknown sign-in, queued mail); tokens: 24 h confirmation, 2 h dedicated reset provider, reset single-use through stamp rotation, a confirmation token is rejected as a reset token, a different key ring gives the uniform failure; no open redirect introduced (`LocalRedirect.Sanitize` unchanged, `NotFoundRedirect` is a constant); links built only from `Email:PublicBaseUrl`; antiforgery is on (`UseAntiforgery`, `EditForm` with `FormName`), confirm runs on GET by design; captcha before lookup and fail closed (tested); the Turnstile secret is never in logs, settings or messages; SMTP CRLF/header injection is not reachable (subjects are constants, `To` goes through `MailAddress`, Identity rejects malformed addresses first); limiter bypass by trailing slash (C2) and the shared proxy IP caveat is documented in the README; lockout order implemented per D14 (W4); seeded users are created with `EmailConfirmed = true`; existing open-registered accounts need the resend page when confirmation is turned on (documented in README line 191).
3. Logs content-free: yes by inspection for every new handler, dispatcher, queue, verifier and token service; test depth is limited (W6).
4. Startup validation matrix: all rows of D18 are covered by `EmailOptionsTests`, `CaptchaOptionsTests`, `RateLimitOptionsTests`; messages never echo values.
5. REQUISITOS/AGENTS/README consistency: RF-67, RF-78 to RF-82, RNF-02, RNF-03 are present and the keys, routes, lifespans and defaults agree with the code; the stale items are W1, W8 and C3.
6. Tests are lean and Theory-grouped. Every [UNIT] scenario has a test (partials in the matrix). Wiring tests that fail on guard removal: captcha-before-lookup (register and forgot) and no-sign-in-when-confirmation-required are guarded; the `NotAllowed` lockout/failure counting, the dummy-hash call and the Program-level limiter wiring are not (W4, W6).
7. Group F: F.1 to F.5 unchecked; the evidence file (F.4) exists in a first form without the coverage table.
8. Module boundary, layer and model tests pass (31 architecture tests; `HasPendingModelChanges` false). No migration was added by this change (the latest is `20261015071731_AddContextAttachments`); none is needed unless the collation option of C1 is chosen.

### Spec and doc lines to amend
- `specs/authentication/spec.md:25` and design D1: the path comparison must tolerate one trailing slash (C2); add the scenario row.
- `spec.md` Email confirmation item 5, Password recovery paragraph 1, and design D16: state that every mail goes to the stored account address (C1); add a scenario with a stored address that differs from the typed one.
- `spec.md:146, 202, 239` and design D17: replace `Navigation.NotFound()` with the redirect to `/not-found?codigo=404`, answered as 404 (W2).
- `spec.md:198`, `tasks.md:118`, `REQUISITOS.md:113` (RF-81): a link re-opened after confirming still reports success; only altered, expired, foreign-key-ring and unknown-user links fail (W1).
- `spec.md:226-229` (Reset enforces the password policy) and task 5.3: the validator checks shape; strength is enforced after the token check and leaves the link usable (W3).
- `REQUISITOS.md:111` (RF-79): remove "cuando exista" (W8).
- `AGENTS.md` sections 4.4 and 4.6: the F.1 content, and drop "la verificación del correo queda para después" (C3).
- `tasks.md`: tick 2.G, 4.G, 5.G and the 1.6 sub-bullets once confirmed; add Reconciliation notes R24 to R27 (W1, W2, W3, the `PasswordResetResult` shape); keep 2.6/4.10/5.7 open until the unobserved cases in W4 and W5 are walked.
- `docs/evidencias/registration-hardening-recorrido-chrome.md`: add the "Cobertura de escenarios" table (F.3) after the fixes.

### Verdict
**FAIL**: 3 CRITICAL (the recipient is taken from the request, which with an accent-insensitive collation allows account takeover and defeats uniform responses; the rate limiter is bypassed by a trailing slash; the spec-mandated AGENTS 4.4/4.6 documentation is missing), 9 WARNING, 7 SUGGESTION. Build and all 1,788 tests pass; fix C1 to C3, then re-verify.
