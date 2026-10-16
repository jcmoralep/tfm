# BMAD conformance checks

## Setup (throwaway, outside the repo)

1. Create an empty folder in the scratch area and `git init` it.
2. Install BMAD at the pinned version with modules `core` and `bmm` using the official installer (BMAD docs: https://docs.bmad-method.org/). Confirm `_bmad/_config/manifest.yaml` reports the version.
3. Make `uv` available on PATH (BMAD skills run their Python scripts with `uv run`).
4. Copy the files under test (exported `.md` or goldens). Keep the platform's file names, but for Epics put PRD and Architecture flat in `planning-artifacts/`: the epics discovery patterns (`*prd*.md`, `*prd*/index.md`, `*architecture*.md`, `*architecture*/index.md`) are not recursive.

## Checks

| # | Check | How | Pass when |
|---|---|---|---|
| 1 | `bmad-build` with a Small spec | Copy `spec-small.md` to `specs/spec-demo/SPEC.md`; ask `bmad-build` to build from that folder | Reads it as planning context (Why, `FR-n`, constraints, non-goals, success signal) without demanding `CAP-N` or its own frontmatter |
| 2 | Epics read PRD ids | Copy `prd-large.md` to `planning-artifacts/`; run step 1 of `bmad-create-epics-and-stories` | Inventory keeps `FR-n` / `NFR-n` with the hyphen and the same numbers; none lost |
| 3 | Spanish headings and `Q-n` suffix | Validate the PRD with `bmad-prd` (headless) | File unchanged byte for byte; `Q-n … (Responsable: …; Revisar: …)` read as owner + revisit |
| 4 | Architecture lint | In the throwaway install (not this repo), run `<install>/.claude/skills/bmad-architecture/scripts/lint_spine.py` with `uv run` on the exported spine (check its `--help` for arguments) | `{"ok": true, "total_findings": 0}`. Note: TBD/TODO detection is case-sensitive, uppercase `TODO` in prose is a false positive |
| 5 | AD blocks | Inspect the spine | `### AD-n — title` monotonic; each block contains binds / prevents / rule (the Spanish labels carry the English words) |
| 6 | Epics with PRD + Architecture | Step 1 with both files flat in `planning-artifacts/` | Keeps `FR-n`, `NFR-n`, `AD-n`; accepts Spanish |
| 7 | `epics.md` as build intent | Give the exported `epics.md` to `bmad-build` | Taken as starting intent; `status: approved` is ignored (BMAD knows `draft`, `ready-for-dev`, `in-progress`, `in-review`, `done`) |

## Recording

Add a row per check to `docs/bmad-referencia.md` §7 or a new §7 bis: `| Comprobación | Fecha | Versión | Resultado | Evidencia |`. If a check contradicts §4 or AGENTS.md §6.3, update them in the same change.
