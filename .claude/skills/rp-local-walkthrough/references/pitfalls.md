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
| Tokenless POSTs to `/login` answer 400, never 429 | The limiter runs before antiforgery and counts them, but the endpoint answers 400; in the 2026-10-15 run 12 tokenless POSTs gave 400 and the next tokened ones 429 at once | Send the antiforgery token to observe the 429 page |
| "Algo salió mal" after approving | Focus restored to a button that was removed | Fixed in PR #55 (`TryFocusAsync` tolerates `JSException`); watch for the pattern |
| Email-off pages answer 500 | `Navigation.NotFound()` in static SSR with status-code re-execute | Redirect to `/not-found?codigo=404` (PR #51) |
| Draft fails with 503 while turns work | Gemini default model overloaded | See `rp-gemini-live-check` (fallback model) |
