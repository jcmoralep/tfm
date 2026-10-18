---
name: rp-security-privacy
description: "Trigger: seguridad, autenticación, registro, correo, tokens, logs, secretos, adjuntos, descargas, privacidad. Apply Ruta de Producto security and logging rules."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.0"
---

## Activation Contract

Load for any change in authentication, account flows, email, captcha, rate limiting, attachments/downloads, logging, configuration of secrets, or anything that handles initiative content.

## Hard Rules

- Logs (RNF-02, AGENTS.md §4.4): never emails/recipients, subjects, bodies, tokens, links (with or without token), passwords, captcha responses, secrets, prompts, model answers or document text. Log ids, template/type, outcome, duration, and on failure the exception type and status code. Commands carrying such data override `ToString()`.
- Uniform responses: registration with confirmation, resend and recovery show the same text and redirect whether the account exists or not; similar timing (dummy hash for unknown emails).
- Mail goes to the STORED address, never the typed one (MySQL `utf8mb4_0900_ai_ci` ignores accents); validators reject non-ASCII addresses.
- Links are built only from `Email:PublicBaseUrl`, never from the `Host` header.
- Tokens are single-use with expiry (24 h confirm, 2 h reset); compare secrets in constant time.
- Gating checks (captcha, invite code) run before any account lookup.
- Owner-only resources answer a uniform 404 with no body for every other case; downloads are `application/octet-stream`, `attachment`, `nosniff`, `no-store`.
- Secrets only in user-secrets/env; startup failures name the key, never the value.
- Treat uploaded files as hostile: blocklist executables, never execute, never render inline; text sent to the model is delimited and neutralized as data.
- Parsing untrusted documents (.docx and .pdf): inspect before parsing (zip: at most 2,000 entries and 100 MB really decompressed, counted while decompressing because headers can lie; XML: no DTD, bounded depth; PDF: at most 300 pages, checked before reading any text, pages read one at a time), never write the file to disk, cap the extracted text (200,000 characters), run under a timeout (15 s) that also bounds an extractor ignoring its token, treat any failure as `Unreadable` without failing the upload, and never log file names, text or exception messages (exception type only).

## Decision Gates

| Change | Must also check |
|---|---|
| New account flow | enumeration (text, redirect, timing), stored address, token lifetime, rate limiter coverage of its POST path (case and trailing slash) |
| New log line | the forbidden list above; add a log test with a capturing logger when the data is sensitive |
| New download/upload | headers, owner check, size/count limits, blocklist |
| Content to Gemini | only text-like context, caps, counts-only logs |

## Execution Steps

1. List the trust boundaries the change touches.
2. Apply the rules and gates above; add tests for each rule that the code enforces.
3. In the walkthrough, scan the app output for the sample emails, passwords and sentences used.

## Output Contract

Return each boundary touched, the rule applied, the test that proves it, and the log-scan result.

## References

- `AGENTS.md` §4.4, §4.6, §4.7, §4.8; `REQUISITOS.md` RNF-02, RF-78–RF-83.
- `openspec/changes/registration-hardening/verify-report.md` — past findings (account takeover via collation, trailing-slash limiter bypass).
