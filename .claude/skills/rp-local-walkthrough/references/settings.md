# Walkthrough settings

All switches are off by default (AGENTS.md §4.6). Export only what the flow needs, after `. rp.env`.

| Feature | Variables |
|---|---|
| Gemini | `Assistant__Provider=Gemini` (key from user-secrets `Gemini:ApiKey`; never read or print it). Fallback model: `Gemini__Model=gemini-3.1-flash-lite` |
| Email | `Email__Provider=Smtp Smtp__Host=127.0.0.1 Smtp__Port=1025 Smtp__EnableSsl=false Smtp__From=noreply@example.com Email__PublicBaseUrl=http://localhost:5080`; add `Email__RequireConfirmedEmail=true` for confirmation |
| Captcha (always passes) | `Captcha__Provider=Turnstile Turnstile__SiteKey=1x00000000000000000000AA Turnstile__SecretKey=1x0000000000000000000000000000000AA` |
| Captcha (always fails) | secret `2x0000000000000000000000000000000AA` |
| Rate limiter | `RateLimiting__Enabled=true` (10 POSTs / 60 s per IP) |
| Invite code | `Registration__InviteCode=<at least 12 chars>` |
| Storage | `Storage__Provider=Local` (only provider; `S3` fails at startup) |

Cloudflare test keys are public documentation values, safe to commit here.
