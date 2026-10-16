---
name: rp-bmad-conformance
description: "Trigger: BMAD, comprobar con BMAD, conformidad, formato de artefactos, bmad-build, epics, lint_spine, nueva versión de BMAD. Check exported artifacts against a real BMAD install."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.0"
---

## Activation Contract

Load when a change alters the shape of an exported artifact (SPEC, Brief, PRD, Architecture, Epics), its frontmatter, ids or usage note, when BMAD releases a new version, or when the user asks to "validar contra BMAD".

## Hard Rules

- BMAD first: adopt BMAD's own behavior and cite the source file; ideas beyond BMAD go to `docs/mejoras-posteriores.md`.
- Never invent BMAD structure; verify in a real install (AGENTS.md §5.5). The pinned reference is BMAD 6.12.1 (`docs/bmad-referencia.md`).
- Install only in a throwaway folder OUTSIDE the repo, with its own empty `git init`; never install BMAD into this repository.
- `bmad-build` needs `uv` on PATH; in headless runs allow `Bash(uv run:*)`.
- Read-only toward the product code: the check produces evidence, not fixes. Fixes go through `rp-feature-writer`.
- Respect the trademark rule: no product names containing BMAD (AGENTS.md §4.11).

## Decision Gates

| Changed artifact | Checks (see `references/checks.md`) |
|---|---|
| SPEC (Small) | 1 `bmad-build` accepts `SPEC.md` |
| PRD | 2 epics step 1 reads `FR-n`/`NFR-n` without renumbering; 3 Spanish headings and `Q-n` suffix survive `bmad-prd` validate |
| Architecture | 4 `lint_spine.py` returns `ok: true`; 5 AD ids monotonic and `binds/prevents/rule` present |
| Epics | 6 epics step 1 with PRD + Architecture copied flat into `planning-artifacts/`; 7 `bmad-build` takes `epics.md` as starting intent |
| New BMAD version | All checks; diff the skill folders against 6.12.1 before trusting old notes |

## Execution Steps

1. Export the artifacts from the app or take the goldens in `tests/RutaProducto.Infrastructure.Tests/Artifacts/Goldens/`.
2. Prepare the throwaway install (`references/checks.md` → Setup) and confirm `_bmad/_config/manifest.yaml` shows the expected version.
3. Run the checks that the gate table selects; capture the exact output.
4. Compare with `docs/bmad-referencia.md` §2–§4; any contradiction is a finding.
5. Record results in `docs/bmad-referencia.md` §7 / §7 bis (date, version, result, evidence) via `rp-docs-sync`.

## Output Contract

Return a table Check | Version | Result (cumple / no cumple / parcial) | Evidence (quoted output), plus required doc or code changes. Never mark "cumple" without output.

## References

- `references/checks.md` — setup and the exact checks.
- `docs/bmad-referencia.md` — pinned facts and past results.
- `docs/hallazgos-simulaciones-bmad.md` — earlier simulation findings.
