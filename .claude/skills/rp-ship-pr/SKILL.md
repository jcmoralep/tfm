---
name: rp-ship-pr
description: "Trigger: commit, push, PR, pull request, merge, fusionar, subir la rama in Ruta de Producto. Ship a change: gate, conventional commit, English PR, CI, squash merge."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.1"
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

## Output Contract

Return the PR number and URL, CI result, merge commit hash, and the final `git status --short`.

## References

- `assets/pr-body.md` — PR body template.
- `assets/ship.sh` — push, PR, CI watch, squash merge, back to main.
