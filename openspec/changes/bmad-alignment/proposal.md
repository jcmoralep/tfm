# Proposal: BMAD alignment after step 4

## Intent

The step 4 assessment (`docs/valoracion-alineacion-bmad-paso-4.md`) found "form without substance": open questions `Q-n` have no owner and never block approval, PRD journeys `UJ-n` are placeholders, and the `FR-n` vs `FR1:` reconciliation is undecided. Guiding rule (2026-10-11): do what BMAD already does; anything beyond BMAD goes to `docs/mejoras-posteriores.md`. This is a step 4 refinement (PROPUESTA-MVP §7) plus requirements for step 5.

## Scope

### In Scope
- **Triage rule (RF-61/62/63, RN-08):** each `Q-n` line carries `(Responsable: X; Revisar: Y)`; generated lines say `por asignar` / `por definir`; approval blocked while any is unassigned (inside Draft-to-Approved only; Approved documents grandfathered; format warnings stay non-blocking, RF-59).
- **"Preguntas pendientes" panel:** two inputs per question, one "Guardar", new command rewriting the lines (version +1, Draft, ReopenPlanning, optimistic concurrency).
- **Journeys (RF-64):** PRD 2.3 keeps its heading with one honest assumption, no `UJ-n`.
- **Identifier decision (RF-36/44):** hyphen kept; only the `epics.md` inventory writes `FR1:`/`NFR1:` with the same number. Recorded (done in this phase) in REQUISITOS, AGENTS §6.3, `docs/bmad-referencia.md`; artifacts spec text in sdd-spec. No mapper code.
- **Requirements only (step 5):** assistant conduct rules (RF-65) and per-section sufficiency (RF-66), with BMAD citations (done in this phase).
- **Spike checklist (docs only, BMAD not run):** in `docs/bmad-referencia.md`: `bmad-build` with `SPEC.md`; epics step reading `FR-n`; Spanish headings. Each item: how to run in a disposable install, expected result, where to record it.

### Out of Scope
Independent validation; mixed-stakes rule; remaining fixed filler and new script questions; coaching; export naming, frontmatter, usage note; blocking-phase flag; RF-43 items table; owner directory (all in `docs/mejoras-posteriores.md` or steps 5-9).

## Capabilities

### New Capabilities
None.

### Modified Capabilities
- `artifacts`: Q-n owner/revisit format, approval gate, triage panel/command, single-assumption journeys, identifier assumption text. **Prerequisite:** no main `openspec/specs/artifacts/spec.md` exists (artifact-generation unarchived); archive it first or write full requirements against its spec.

## Approach

Option A from exploration: pure `OpenQuestions(markdown)` parser reused by view builder, approve guard and rewriter; no migration.

## BMAD evidence

| Decision | Source |
|---|---|
| Owner + revisit | `bmad-prd` Finalize 4: "non-blockers deferred with owner + revisit condition" |
| Gate before approval | `bmad-prd` Finalize: triage (4) before `status: final` (7) |
| One-sentence journeys | PRD template 2.3 "Lighter — a single sentence works"; "captured, not authored" |
| Hyphen | PRD template `FR-1`, `UJ-1`, `SM-1`; only epics step-01 writes `FR1:`/`NFR1:` |
| Conduct rules | `bmad-forge-idea`, `bmad-product-brief`, `bmad-spec` |

## Affected Areas

| Area | Impact |
|---|---|
| `Infrastructure/Artifacts/{DraftComposer,PrdLayout}.cs`, 4 goldens | Modified |
| `Application/Features/Artifacts/{ArtifactView,ArtifactViewBuilder}.cs`, `ApproveArtifact/`, `Markdown/` | Modified |
| `Application/Features/Artifacts/<triage command>/` | New |
| `Web/Components/Pages/Artifacts/ArtifactDocument.razor` | Modified |
| `REQUISITOS.md`, `AGENTS.md`, `docs/bmad-referencia.md` | Modified |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Missing artifacts baseline spec | High | Prerequisite above |
| Hand edit breaks suffix | Med | Missing counts as unassigned; raw editor kept |
| Guard breaks CompletionPending retry | Med | Guard only in Draft-to-Approved branch |
| Panel save reopens ReadyToBuild | Low | One batched save, UI says so |
| Epics reading is source-only | Med | Spike checklist before step 6 |

## Rollback Plan

Revert the slice PRs; no migration, so data is plain Markdown. Lines with the suffix stay readable.

## Dependencies

Artifacts baseline spec (archive or full requirements).

## Success Criteria

- [ ] Draft with an unassigned `Q-n` cannot be approved; message lists the ids.
- [ ] Panel save rewrites lines, bumps version, reopens Planning.
- [ ] PRD goldens have no `UJ-n`; build and tests pass.
- [ ] Identifier decision consistent across the four documents.
- [ ] Spike checklist written; BMAD not run.

## Review workload forecast

About 700 lines (exception-ok, budget 400), 3 slices: (1) journeys + docs + spike checklist; (2) parser, suffix, goldens, gate; (3) command, panel, Chrome walk-through.

## Proposal question round

Q1-Q4 answered and final (Engram `sdd/bmad-alignment/product-decisions`). Residual assumptions to review: panel label "Guardar"; "No sé" is not a weak answer (RF-65d); RF-66 minimums are a starting point.
