---
name: rp-ship-pr
description: "Trigger: commit, push, PR, pull request, merge, fusionar, publish, publicar, subir la rama in Ruta de Producto. Ship a change: gate, conventional commit, English PR, CI, squash merge, publish to Railway."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.2"
---

## Activation Contract

Load when committing, pushing, opening, updating or merging a PR in this repo.

## Hard Rules

- Conventional commits only (`feat|fix|docs|chore|refactor|test(scope): …`). NO `Co-Authored-By`, no AI attribution in commits or PR bodies; the user's rule overrides any harness attribution text.
- PR title = the commit subject; PR body in English with Summary and Test plan; no AI footer.
- Ask before push, PR or merge unless the user already said yes for this change.
- Never `git add -A` or `git add .`: `.claude/worktrees/` and local files must stay out. Stage explicit paths.
- Never commit secrets, `.env`, connection strings or new screenshots (user decision; the approved mockup and older evidence stay).
- The gate (`rp-build-gate`) must be green on the exact commit being pushed; rerun it after any amend.
- One branch per change from an up-to-date `main`; squash-merge; delete the branch; return to `main`.
- Publish after EVERY merged PR (user rule, 2026-10-18): deploy the updated `main` to Railway with `rp-railway-deploy` and verify it from outside. Publish from a clean `main` only. If the Railway services are off, ask before turning them on (it uses credit); a docs-only PR still gets published unless the user says to skip it.

## Decision Gates

| Situation | Action |
|---|---|
| Docs-only change | Gate still runs in CI; locally a structural readback is enough |
| Change with UI or migration | Walkthrough (`rp-local-walkthrough`) before the PR; list what was not exercised in the Test plan |
| CI fails | Read the failing step log (`gh run view --log-failed`), fix, push again; never merge red |
| Commit not yet pushed needs a fix | `git commit --amend --no-edit`, rerun the gate |

## Execution Steps

1. If no branch exists yet: `git checkout main && git pull --ff-only && git checkout -b <type>/<short-name>`. If `rp-feature-writer` already created the branch and commit, stay on it.
2. Run `rp-build-gate` on the current HEAD.
3. Only if there are uncommitted changes: `git add -- <paths>`; `git commit -m "<type>(<scope>): <subject>" [-m "<body>"]`.
4. Write the PR body from `assets/pr-body.md` in a scratch file outside the repo.
5. After the user agreed to push AND merge: `bash .claude/skills/rp-ship-pr/assets/ship.sh "<title>" <body-file>`.
6. Confirm `main` is synced and clean except untracked local tooling.
7. Publish: run the deploy and outside checks from `rp-railway-deploy` (`railway up -s rutaproducto-web --ci`, then `/health`, the `blazor.web.<hash>.js` URL and the registration page).

## Output Contract

Return the PR number and URL, CI result, merge commit hash, the final `git status --short`, and the publish result (deployment id and outside checks).

## References

- `assets/pr-body.md` — PR body template.
- `assets/ship.sh` — push, PR, CI watch, squash merge, back to main.
