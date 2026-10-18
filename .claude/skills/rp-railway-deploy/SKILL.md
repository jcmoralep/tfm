---
name: rp-railway-deploy
description: "Trigger: Railway, deploy, desplegar, subir a producción, apagar, encender, railway up, variables, dominio, costo, crédito. Deploy and operate Ruta de Producto on Railway with the project-local CLI."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.0"
---

## Activation Contract

Load for any Railway action: first deploy, redeploy, variables, domain, logs, turning services off or on, or a production problem.

## Hard Rules

- Install nothing outside the repo folder: the CLI lives in `.tools/railway` (ignored by Git, own npm cache). Run it as `"$PWD/.tools/railway/node_modules/.bin/railway"`. In Git Bash set `MSYS_NO_PATHCONV=1` or `/var/...` paths become `C:/Program Files/Git/...`.
- The user wants the free tier and no spending: ask before any step that can add cost; one MySQL and one web service only; never upgrade a plan; at the END of every session turn both services off (`references/cookbook.md`).
- Secrets: random values generated inline (`$(openssl rand -hex 16)`), never printed. The database secret reaches the app only as a reference variable. The user's own values go through the Railway dashboard or a value they choose to share (the invite code is meant to be shared).
- Never deploy the app publicly without `Registration__InviteCode` set (registration is open without it) and never load real company data (AGENTS §4.8, 2026-10-16 decision).
- The Dockerfile restore step MUST keep `-p:RequiresAspNetWebAssets=true`: without it the image lacks `_framework/blazor.web.js` and every interactive control stays disabled.
- After every deploy verify from outside: `/health` 200, the `<script src>` of `/login` answers 200, the cookie flags, and the registration page.

## Decision Gates

| Situation | Action |
|---|---|
| Change code | `railway up -s rutaproducto-web --ci` (builds the Dockerfile from the working tree, pre-deploy runs `--migrate`) |
| Change only a variable | `railway variable set -s rutaproducto-web K=V` (restarts the service) |
| A service setting does not apply with `environment edit` | Use `railway api` mutation `serviceInstanceUpdate`, then `serviceInstanceDeployV2` (a plain `redeploy` reuses the old manifest) |
| Page loads but buttons are dead / console 404 on blazor.web | Check the Dockerfile restore flag and the script URL (above) |
| Session ending | Turn both services off, MySQL last |

## Execution Steps

1. Read `references/cookbook.md` for the exact commands and the current project/service names.
2. Apply the gate row; keep the cost rule in mind.
3. Verify from outside (hard rule) and read the service logs.
4. Report what changed, what runs (and therefore consumes), and how to turn it off.

## Output Contract

Return: URL, deployment status, outside checks with results, which services are running, and the exact off/on commands used or pending.

## References

- `references/cookbook.md` — commands, IDs, MySQL settings, on/off cycle, cost notes.
- `README.md` "Despliegue en Railway", `AGENTS.md` §6.8–§6.9.
