#!/usr/bin/env bash
# Push the current branch, open (or reuse) its PR against main, wait for CI, squash-merge, return to main.
# Usage: bash ship.sh "<title>" <body-file>
# Run only after the build gate is green on HEAD and the user agreed to push and merge.
set -uo pipefail
title="${1:?title required}"
body="${2:?body file required}"
branch=$(git branch --show-current)
[ "$branch" = "main" ] && { echo "refusing to ship from main"; exit 1; }
git push -q -u origin "$branch" || { echo "push failed"; exit 1; }
pr=$(gh pr list --head "$branch" --json number -q '.[0].number')
if [ -z "$pr" ]; then
  url=$(gh pr create --base main --head "$branch" --title "$title" --body-file "$body" | tail -1)
  pr=${url##*/}
else
  echo "PR #$pr already exists: title and body were not changed (use gh pr edit)"
fi
[ -n "$pr" ] && [ "$pr" -eq "$pr" ] 2>/dev/null || { echo "could not create or find the PR"; exit 1; }
echo "PR #$pr"
# Wait until CI registers its checks (up to ~2 minutes) before watching them.
for _ in $(seq 1 24); do
  gh pr checks "$pr" >/dev/null 2>&1 && break
  sleep 5
done
if ! gh pr checks "$pr" --watch; then
  echo "CI failed: not merging (gh run view --log-failed)"
  exit 1
fi
gh pr merge "$pr" --squash --delete-branch || { echo "merge failed"; exit 1; }
git checkout -q main && git pull -q --ff-only && git log --oneline -1
git status --short
