---
name: rp-docs-sync
description: "Trigger: documentación, docs, REQUISITOS, AGENTS.md, README, mejoras posteriores, RF, after any behavior change. Keep Ruta de Producto docs in sync with the code."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.0"
---

## Activation Contract

Load at the end of every change that alters behavior, configuration, scope or a decision, and when deferring work.

## Hard Rules

- Project docs are in Spanish (neutral register); code identifiers and PR text stay in English.
- REQUISITOS: append new `RF-n`/`RNF-n` after the highest existing number; never renumber; mark `[CONFIRMADO]` only for user decisions; edit the old RF when its text becomes false.
- Every deferred idea goes to `docs/mejoras-posteriores.md` as a table row `| Mejora | Por qué no ahora | Origen |` with the change name and absolute date (YYYY-MM-DD).
- Decisions by the user are recorded with their date ("Decisión del usuario (2026-10-12)").
- Nothing named BMAD in product names (AGENTS.md §4.11); descriptive mentions only.
- No new screenshots in the repo (user decision recorded in `docs/evidencias/registration-hardening-recorrido-chrome.md`); the approved mockup and older evidence images stay.

## Decision Gates

| Change | Update |
|---|---|
| New config key / switch | README variables table + feature section; AGENTS.md §4.6 (auth) or the module section; REQUISITOS RF |
| New module rule (logging, storage, export) | AGENTS.md §4.x for that module |
| BMAD format checked or changed | `docs/bmad-referencia.md` §7 table (date, version, result, evidence) and AGENTS.md §6.3 |
| Gemini model/latency/quality run | `docs/gemini-evaluacion.md` or `docs/gemini-sonda.md` |
| Manual walkthrough | `docs/evidencias/<change>-recorrido-chrome.md` |
| Pending decision resolved | AGENTS.md §6 item struck through with the resolution |
| Build order step closed | PROPUESTA-MVP.md only if scope changed |

## Execution Steps

1. List what changed for a user, an operator and a developer.
2. Apply the table above; read each target section before editing.
3. Check consistency: README, REQUISITOS and AGENTS must state the same defaults and limits.
4. Grep for stale statements ("pendiente", old names, old numbers) about the same feature.

## Output Contract

Return the list of doc files and sections edited, the RF numbers added or changed, and any stale statement fixed.

## References

- `AGENTS.md`, `REQUISITOS.md`, `README.md`, `PROPUESTA-MVP.md`, `docs/mejoras-posteriores.md`, `docs/bmad-referencia.md`.
