```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:b2c0b51bd55318431344c54e3c177d5fc6ea4a96d1782e1bed8b7bad5d0f751b
verdict: fail
blockers: 0
critical_findings: 0
requirements: 4/7
scenarios: 42/45
test_command: dotnet test RutaProducto.slnx -c Release --no-build
test_exit_code: 0
test_output_hash: sha256:2dc1581ec353f28d44d18174ceb66152f76768f40c47a90a35531a5ae77b31da
build_command: dotnet build RutaProducto.slnx -c Release --no-restore
build_exit_code: 0
build_output_hash: sha256:7bc42a17fc63a8ec2584c6ddfa7469091db0355974c3ee919536924f2d40246f
```

## Verification Report (second run, after the closure pass)

**Change**: registration-hardening. **Mode**: Standard. **Branch**: fix/hardening-verify at f4428bb (2 local commits over main 4182612).

**Verdict: FAIL on evidence completeness only (the envelope verdict is fail because 3 of 45 scenarios and 3 of 7 requirements are not fully evidenced; the validator rejects a pass in that state). There is no code or spec defect: 0 CRITICAL, 7 WARNING, 6 SUGGESTION, and a recommendation to archive once the owner accepts the evidence gaps.** The first run was FAIL (3 CRITICAL, 9 WARNING, 7 SUGGESTION).

Note: the skill treats any unchecked task as CRITICAL. About 15 walkthrough boxes in tasks.md (parents 2.6, 4.10, 5.7, their sub-bullets, and one line in 1.6) remain unchecked, each annotated "NOT run/observed". They are evidence-gathering steps, not code, and are the same class the first run reported as WARNING (W5, W9). If the skill rule is applied literally, this run is FAIL on those boxes alone.

### Execution
- Restore and Release build: exit 0, 0 errors, 8 warnings (all NU1507 source mapping, zero code warnings).
- Tests: 1,809 passed, 0 failed, 0 skipped (Architecture 31, Web 263, Application 984, Infrastructure 531). It was 1,788. No RutaProducto.Web process was running.
- Mutations run by the verifier, each reverted with git checkout (git status is clean apart from the untracked .claude/, and the tree was rebuilt afterwards):
  - IdentityAuthService: remove AccessFailedAsync -> SignInWiringTests.Wrong_passwords_on_an_unconfirmed_account_count_and_the_fifth_locks_it fails.
  - Force lockedOut = false -> A_locked_unconfirmed_account_shows_the_lock_even_with_the_right_password fails.
  - Remove the DummyPasswordHash.Verify call -> An_unknown_email_pays_one_dummy_hash_verification_... fails.
  - Revert the four mail call sites to the typed address -> 4 tests fail (PasswordRecoveryTests.The_reset_mail_goes_to_the_stored_address_..., two in RegisterCommandTests, AccountConfirmationTests.Resend_mails_the_stored_address_...).

### Per-finding status (first run)
| Id | Status | Evidence |
|---|---|---|
| C1 stored address | CLOSED | AccountToken.Email (IAccountTokenService.cs), set from user.Email in IdentityAccountTokenService.cs:24,58. Used at ForgotPasswordCommand.cs:70, RegisterCommandHandler.cs:61, ResendConfirmationCommand.cs:48. The duplicate path mails FindStoredEmailAsync (RegisterCommandHandler.cs:67-73) and sends nothing if it is null, with the same response. MustBeAsciiAddress is in the Register, Forgot and Resend validators (EmailAddressRules.cs), before EmailAddress, with one message whether or not the account exists. Only four Enqueue(AccountEmails...) sites exist and none uses the typed value. Other paths: login and seeding use the typed value only to look up the user and need the password (no mail, no oracle); registration sets UserName = Email, which the default Identity AllowedUserNameCharacters restricts to ASCII, so no non-ASCII legacy account can exist and the new validators lock nobody out. |
| C2 trailing slash | CLOSED | AuthRateLimiting.cs:23-34 trims trailing slashes, ordinal ignore-case. AuthRateLimitingTests has rows /login/, /LOGIN/ (limited) and /logout/ (not). Other normalisations: Request.Path is already percent-decoded by Kestrel; there is no UsePathBase in src; //login is not matched here but routing answers 404 (first-run probe, not re-run); extra trailing slashes are over-limited, which is harmless. |
| C3 AGENTS 4.4/4.6 | CLOSED | 4.4 logging rule extended; 4.6 has the switch table with defaults, uniform responses, the stored-address rule, NotAllowed ordering, startup guards and the redeploy/Data Protection caveat; the stale text saying email verification is left for later is gone. |
| W1 idempotent link | CLOSED | Spec item 3, the Full flow scenario, tasks.md 4.10, RF-81 and note R24 now state it. |
| W2 redirect to /not-found | CLOSED | Spec items, scenario, design D17 and R25 updated; code unchanged and walked in Chrome (302 then 404). |
| W3 shape-only reset validator | CLOSED | Spec scenario, design D16 and R26; ResetTokenTests plus PasswordRecoveryTests.The_reset_validator_uses_the_registration_messages. |
| W4 sign-in wiring | CLOSED | SignInWiringTests (4 tests) fail on each mutation above. The five-wrong-passwords lock was still never shown in Chrome (task 4.10 stays open, honestly noted). |
| W5 captcha widget [UI] | OPEN | JS off, always-block key, blocked Cloudflare, /login without widget, Spanish rendering and fresh token after a validation error are still unobserved. The fail-closed logic is unit-tested. |
| W6 logging evidence | PARTLY CLOSED | AccountTokenLogTests captures the token service log. No test for the limiter warning or the Program.cs wiring (limiter registered only when enabled, middleware order); the S1 step "restart with the limiter unset" (tasks.md:68) was never run. |
| W7 [MANUAL] deliverability | OPEN | Never executed; recorded in docs/mejoras-posteriores.md. Must precede enabling Email:Provider in production. |
| W8 RF-79 wording | CLOSED | The "cuando exista" clause was removed; it points to RF-82. |
| W9 task bookkeeping | CLOSED except the open walkthrough boxes | 2.G, 4.G, 5.G, 1.6 sub-bullets and F.1 to F.5 ticked; R24 to R28 added; proposal criteria ticked; the evidence doc has the coverage table (37 UNIT rows checked against test names, plus the UI/DOC/MANUAL table marked "pendiente" where unobserved). |
| S1 to S7 | unchanged | Not part of the closure; still valid. |

### Scenario count (45 in the spec: 37 UNIT, 6 UI, 1 DOC, 1 MANUAL)
- 37 UNIT: COMPLIANT (named test passes, full suite green; the new "Mail goes to the stored address" scenario is covered by 5 tests).
- UI compliant, supported by docs/evidencias/registration-hardening-recorrido-chrome.md: Eleventh POST (S1 notes in tasks.md), Real delivery, Link hidden without email, Confirmation Full flow. That is 4.
- UI PARTIAL: Widget and test keys (W5); Recovery Full flow (the evidence records that the old password stops working and the success message, but not a sign-in with the new password).
- DOC compliant: Caveat documented (README lines 192 and 202, AGENTS 4.6).
- MANUAL UNTESTED: Provider deliverability.
- Total 42 compliant, 2 partial, 1 untested. Requirements fully compliant: Rate limiting, Confirmation, Data Protection, Secrets and logging = 4/7 (Captcha, Email sending and Password recovery each carry a partial or untested scenario).

### Issues
**CRITICAL (0).**

**WARNING (7)**
1. Open walkthrough boxes (see caveat): tasks.md:68, 82, 84-86, 88, 115, 117, 121, 124, 137, 141, 143, 144 and parents 2.6, 4.10, 5.7. F.5 is ticked although it says "then run the independent verify"; this run is that verify.
2. W5 captcha UI gaps (above).
3. W6: no test or walkthrough for the Program.cs limiter wiring or the limiter warning log; the "limiter unset" restart step was not run.
4. W7 [MANUAL] never run; Railway outbound SMTP unverified.
5. Recovery Full flow [UI]: sign-in with the new password is not recorded (the scenario says "signs in on the next step").
6. Walkthrough 5.7: tampered codigo, a confirmation link on the reset page, the unconfirmed-account reset and the redeploy (new key ring) case are unit-tested only.
7. The routing behaviour behind C2 (/login/ served as /login) rests on the first-run probe and unit rows; no browser or curl check of /login/ with the limiter on was recorded after the fix.

**SUGGESTION (6)**
- IdentityAccountTokenService.cs:24,58 use user.Email with a null-forgiving operator; a null would throw when the mail is built. Unreachable today (Identity requires a unique email), but an explicit guard is cheap.
- Style: missing space after the comma in ForgotPasswordCommand.cs:70, RegisterCommandHandler.cs:61, ResendConfirmationCommand.cs:48.
- The ASCII-only rule also rejects legitimate IDN addresses typed in Unicode (punycode xn-- still works). Accepted trade-off; mention it in the README if users complain.
- Earlier S1 (IPv6 /64 partition), S2 (security-stamp validation interval), S3 (warm the dummy hash), S4 (redirect after the register POST), S5 (InvalidEmail mapped to the password message) and S6 (PublicBaseUrl query/fragment) remain open and low priority.

### New defects introduced by the closure pass
None found. Checked: the AccountToken third parameter (all construction sites updated, ToString() still prints only the type name, log tests green); the validators do not leak existence (same message for any non-ASCII input); the duplicate path adds one lookup but the HTTP result is identical; no regression for existing non-ASCII users (none can exist); the 31 architecture tests pass and no migration was added.

### Next
The evidence gaps need an owner decision. sdd-archive is reasonable if the owner accepts WARNINGs 1 to 7 (all are evidence gaps, none is a code defect). Before enabling email or captcha in production, run the [MANUAL] deliverability check and the captcha UI gaps.

## Orchestrator note (2026-10-15)

The owner accepted, by the standing instruction to decide with the recommended option, the remaining evidence gaps as documented gaps: the captcha widget cases (JavaScript off, always-block site key, blocked Cloudflare, `/login` without widget), signing in with the new password after a reset, the post-fix `/login/` check with the limiter on, the limiter warning log and restart check, and the provider deliverability [MANUAL] check. They are listed in `docs/mejoras-posteriores.md` and must be walked before enabling email or captcha in production. C1 was additionally exercised end to end against MySQL on 2026-10-15 (typed upper-case address mailed the stored lower-case one; an accent variant was rejected with the neutral message; an unknown address got the uniform text and no mail).
