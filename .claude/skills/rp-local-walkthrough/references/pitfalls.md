# Walkthrough pitfalls already hit (and the fix)

| Symptom | Cause | Fix |
|---|---|---|
| `Access denied for user 'rutaproducto'` on migrate | Named volume `rutaproducto_mysql-data` kept the previous run's password | Only if this run created it: `docker rm -f rutaproducto-mysql && docker volume rm rutaproducto_mysql-data`, then `compose up`. If it predates the run, it may be the developer's data: ask |
| `dotnet ef` targets port 3306 | `rp.env` was sourced in another Bash call; the design-time factory falls back to a placeholder | Source `rp.env` in the same command |
| No `Logs/` folder; console shows `Request starting … ?usuario=…&codigo=…` | App started from the repo root, so `appsettings.json` (file sink, `Microsoft: Warning`) was not loaded | Start from `src/RutaProducto.Web`. The URL lines were an artifact of this, not a product leak |
| Port 3307 busy | Old `bmadplatform-mysql` container (not ours to touch) | Use `MYSQL_PORT=3308` |
| `SmtpException GeneralFailure` | Local sink has no STARTTLS | `Smtp__EnableSsl=false` (only accepted for localhost) |
| Sink writes nothing | Windows Python got a `/c/...` path | Pass `cygpath -m` paths |
| Stop command exits 255 | PowerShell `Stop-Process` quirk in Git Bash | Check the port, continue in a new command |
| Page redirects to Inicio instead of `/registro` | Old auth cookie in the shared Chrome profile | Use an isolated context |
| Tokenless POSTs to `/login` answer 400 and never turn into 429 | Measured 2026-10-16: 11 tokenless POSTs in a row gave 11 × 400 and no 429; with the token the 11th gave 429. The tokenless ones do no account work | Send the antiforgery token (and its cookie) to observe the 429 page |
| "Algo salió mal" after approving | Focus restored to a button that was removed | Fixed in PR #55 (`TryFocusAsync` tolerates `JSException`); watch for the pattern |
| Email-off pages answer 500 | `Navigation.NotFound()` in static SSR with status-code re-execute | Redirect to `/not-found?codigo=404` (PR #51) |
| `/login` answers 500 in a container with `ASPNETCORE_ENVIRONMENT=Production` over plain http | The antiforgery cookie was forced `Secure` (`Always`); the framework refuses to issue it over http | Keep antiforgery on `SameAsRequest` (fixed in PR #58). Production containers also drop the `Secure` session cookie over http: test with `X-Forwarded-Proto: https` + `ForwardedHeaders__Enabled=true`, or use Development |
| Plain `dotnet restore` passes locally but CI fails with NU1902/NU1903 | The local feed is unreachable, so the offline flags skip the vulnerability audit | Reproduce the audit with `dotnet restore RutaProducto.slnx --configfile <nuget.org-only config>` before pushing (see `rp-build-gate`) |
| Draft fails with 503 while turns work | Gemini default model overloaded | See `rp-gemini-live-check` (fallback model) |
