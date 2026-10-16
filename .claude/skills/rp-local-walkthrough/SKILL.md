---
name: rp-local-walkthrough
description: "Trigger: recorrido, walkthrough, validar en Chrome, verify UI, end-to-end, probar flujo. Run Ruta de Producto locally on throwaway MySQL and walk the real flow in Chrome DevTools MCP."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.1"
---

## Activation Contract

Load after any change with UI or a migration (AGENTS.md §5.4), when asked to validate a flow, or to reproduce a bug end to end.

## Hard Rules

- Never use the Chrome DevTools MCP while the user says they are using DevTools themselves; ask first.
- Credentials are random, live only in a scratch env file outside the repo, and are never printed (except the throwaway seed password needed to log in).
- `docker-compose.yml` fixes the names `rutaproducto-mysql` and `rutaproducto_mysql-data`, the same ones a developer uses daily. If either exists before the run, STOP and ask; remove only what this run created. Never touch other containers (`bmadplatform-mysql`, `hcm_*`, …).
- Every Bash call is a fresh shell: source `rp.env` in the same command as `docker compose`, `dotnet ef` and the app start (otherwise `dotnet ef` falls back to the design-time placeholder on port 3306).
- Start the app with the Web project as content root, or `appsettings*.json` (Serilog file sink, log overrides, defaults) is not loaded and the run is not representative.
- `ASPNETCORE_ENVIRONMENT=Development` also loads the developer's user-secrets; a leftover secret (`Registration:InviteCode`, `Email:*`) changes the run. Mention it in the report.
- Use an isolated Chrome context per run. Use the Fake assistant unless the check is about Gemini (`rp-gemini-live-check`).
- Tear everything down and delete the scratch files, even on failure.

## Decision Gates

| Need | Setting |
|---|---|
| Email flows | `Email__Provider=Smtp Smtp__Host=127.0.0.1 Smtp__Port=1025 Smtp__EnableSsl=false Smtp__From=noreply@example.com Email__PublicBaseUrl=http://localhost:5080` + start `assets/smtp_sink.py` |
| Captcha | `Captcha__Provider=Turnstile` with Cloudflare test keys (`references/settings.md`) |
| Rate limit | `RateLimiting__Enabled=true`; send the antiforgery token to see the 429 page (see `references/pitfalls.md`) |
| Invite code | `Registration__InviteCode=<12+ chars>` |

## Execution Steps

1. Run `rp-build-gate`. Check `docker ps -a --filter name=rutaproducto-mysql` and `docker volume ls --filter name=rutaproducto_mysql-data`: both must be empty.
2. `bash .claude/skills/rp-local-walkthrough/assets/walkthrough-env.sh <scratch-dir>`; then `. <scratch-dir>/rp.env && docker compose up -d`; wait for `healthy`.
3. `. <scratch-dir>/rp.env && dotnet ef database update --project src/RutaProducto.Infrastructure --startup-project src/RutaProducto.Infrastructure --configuration Release --no-build`
4. In the background: `. <scratch-dir>/rp.env && cd src/RutaProducto.Web && dotnet bin/Release/net10.0/RutaProducto.Web.dll > <scratch-dir>/app.out 2>&1`; wait for `Now listening`. If email is on, start the sink with Windows paths (`cygpath -m`).
5. Walk the flow in Chrome; drive Blazor inputs with `assets/chrome-helpers.md`.
6. Scan `app.out` and `src/RutaProducto.Web/Logs/` for leaks (sample emails, passwords, tokens, user content) and `[ERR]` / `Unhandled exception`.
7. Tear down: stop the app and the sink, `docker rm -f -v rutaproducto-mysql`, `docker volume rm rutaproducto_mysql-data`, delete scratch files and the run's `Logs/` files; confirm with `docker ps -a` and `netstat -ano`.

## Output Contract

Return a table Area | Observed (pass/fail with the exact UI text), bugs found with log evidence, what was NOT exercised, user-secrets caveat, and teardown confirmation. Record lasting results in `docs/evidencias/<change>-recorrido-chrome.md` (no new screenshots: user decision).

## References

- `references/pitfalls.md` — failures already hit and their fixes.
- `references/settings.md` — test keys and switches.
- `assets/walkthrough-env.sh`, `assets/smtp_sink.py`, `assets/chrome-helpers.md`.
