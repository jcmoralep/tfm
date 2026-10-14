# Exploration: architecture-epics (PROPUESTA-MVP §7 step 6)

## Current state

- Architecture and Epics exist only as catalog placeholders: `ArtifactType` has `Spec, Brief, Prd`; `ArtifactCatalog` lists `new("Arquitectura", null)` and `new("Épicas e historias", null)` for Large; `IsSetComplete` is false for null types, so a Large initiative cannot reach ReadyToBuild yet (RF-52). `ArtifactViewBuilder` shows them as `NotYetSupported`.
- No outline, slots, layout, labels, exporter prefix/usage note or golden exists for them. `ArtifactType` is stored as a string (max 20), so no migration is needed ("EpicsAndStories" is 15).
- "Preguntas para desarrollo" (RF-39) was deferred to this step.
- Reusable pipeline: `IArtifactDrafter`, `FakeArtifactDrafter` (`DraftRenderer`), `GeminiArtifactDrafter` (`DraftSlots`, `{text, source}` items), `DraftComposer` and the layouts own numbering, `[ASSUMPTION: A-n]`, `Q-n` lines and caps; the model never writes ids. `DraftRequest.BriefMarkdown` + `BriefFacts` is the precedent for building on an approved upstream document (RF-72).
- Gaps: `Availability` is a pure function of `(type, status, depth, answers)` and cannot see other documents; `ItemPrefix` lacks `AD`; `ArtifactOutline.Sections` throws for unknown types; the exporter throws for unknown types.

## What BMAD 6.12.1 expects

- **Architecture (`bmad-architecture`)**: `planning-artifacts/architecture/architecture-<project>-<date>/ARCHITECTURE-SPINE.md`; frontmatter (name, type, purpose, altitude, paradigm, scope, status, created, updated, binds, sources, companions); sections: Design Paradigm, Inherited Invariants, Invariants & Rules, Consistency Conventions, Stack (pinned versions), Structural Seed, Capability → Architecture Map, Deferred. Each decision is `### AD-n — title` with Binds / Prevents / Rule; ids stable. `lint_spine.py` rejects TBD/TODO, unfilled tokens, non-monotonic AD ids, AD blocks lacking the substrings binds/prevents/rule, and Stack rows without a version.
- **Epics (`bmad-create-epics-and-stories`)**: one flat `planning-artifacts/epics.md` (frontmatter status, stepsCompleted, inputDocuments); Overview, Requirements Inventory, FR Coverage Map, Epic List, `## Epic N` with `### Story N.M` (As a / I want / So that) and Given/When/Then criteria. Epics by user value; no forward dependencies; each FR in at least one story. PRD and Architecture are required inputs.
- Real run of 2026-10-14: hyphenated `FR-n`/`NFR-n` are read without renumbering; `FR3:` is a platform choice, not a BMAD requirement.

## Approaches

- A. Reuse flat slot/item pipeline for both: simple, but epics are nested and need FR/NFR/AD references.
- **B. Hybrid (recommended)**: Architecture = flat slots plus AD blocks (model supplies title/rule/prevents/binds, code validates binds against ids parsed from the approved PRD and assigns `AD-n`); Epics = dedicated nested schema, code writes headings/numbering and computes the coverage map and turns uncovered FRs into development questions. Fake first, Gemini last.
- C. Free-form model Markdown validated afterwards: rejected (breaks RF-71 / RN-03).

## Slices (estimated changed lines)

| Slice | Content | Lines |
|---|---|---|
| S1 | Enum values, catalog Large, Availability with upstream approval, texts, summaries plumbing, labels/routes | ~380 |
| S2 | Outline and format for both types, "Preguntas para desarrollo", `AD` prefix, checker handling of the coverage map | ~300 |
| S3 | Fake Architecture: `DraftRequest.PrdMarkdown`, PRD fact parser, layout, golden | ~380 |
| S4 | Fake Epics: Architecture in request, layout, golden, 20,000-char ladder | ~400 |
| S5 | Gemini Architecture | ~330 |
| S6 | Gemini Epics (nested schema/validator) + export + docs | ~450 (exception-ok) |

## Risks

Lint conflict with Spanish AD labels and unpinned Stack; 20,000-char cap; inventing facts (stack unknown to the platform); checker false positives on repeated FR ids; rubber-stamp approval; staleness when the PRD changes; `Availability` signature change (35 references in 9 files); reversal of the `FR3:` decision (RF-36, RF-44, AGENTS §6.3, bmad-referencia §4); BMAD discovery patterns not matching nested paths; Gemini cost for epics.

## Checks before closing (scratch BMAD 6.12.1)

1. `lint_spine.py` on the Architecture golden. 2. Epics step 1 on exported PRD + Architecture. 3. `bmad-build`/`bmad-spec` accepts the exported epics frontmatter.

## Product decisions (one question each, recommended first)

1. PRD approved before Architecture, Architecture approved before Epics — yes.
2. Keep hyphenated `FR-1` in the epics inventory — yes (amends RF-36, RF-44, AGENTS §6.3, bmad-referencia §4).
3. "Preguntas para desarrollo" pre-assigned to the development team — yes.
4. Hint to regenerate when the PRD is back in Draft — yes.
5. Architecture proposes no stack (pending section + development question) — yes.
6. Gemini drafting included in this change, in the last slices — yes.
7. Approval button stays "Aprobar" with confirmation text about dev review (RN-02) — yes.
8. Keep the 20,000-char limit and cap the number of epics/stories — yes.

Next recommended: sdd-propose (decisions 1–3 before it).

## Decisions taken (2026-10-14)

The user chose the recommended option for all eight questions, asking to keep the closest possible similarity to BMAD. Validated against the installed `bmad-architecture` 6.12.1:

- 1–4, 6–8: as recommended (BMAD requires PRD and Architecture as epics inputs; hyphenated `FR-n` was read without renumbering in the real run).
- 5 (no stack), adjusted to match BMAD: the spine template says "keep only the sections this spine needs and cut the rest (no empty headers)" and keeps "Deferred" for "decisions intentionally pushed down, each with the reason it can wait". So the Architecture draft has NO Stack section; the stack goes in Deferred (reason: the platform does not know the company's stack) plus a development question. BMAD would instead recommend a web-verified starter when the stack is open; the platform cannot verify versions, so that stays in `docs/mejoras-posteriores.md`.
- 3 maps to BMAD's Deferred/open-question handling: open questions are pre-assigned to the development team.
