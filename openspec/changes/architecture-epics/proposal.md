# Proposal: Architecture and Epics drafts (PROPUESTA-MVP step 6)

## Intent

A Large initiative can never reach *Lista para construir*: Architecture and "Épicas e historias" are catalog placeholders with no type, outline or drafter (RF-52). The "Preguntas para desarrollo" section (RF-39) is also still deferred. Step 6 drafts both documents from the approved upstream documents, in the shape BMAD 6.12.1 reads (`ARCHITECTURE-SPINE.md`, `epics.md`), as drafts for the development team to review (RN-02). Success: a Large initiative ends with five approved documents that `bmad-create-epics-and-stories` and `bmad-build` accept without edits, and no invented stack or facts.

## Scope

### In Scope
- `ArtifactType.Architecture` and `EpicsAndStories` in the Large catalog. Availability: Architecture needs an approved PRD, Epics needs an approved Architecture (no migration; the type is a string).
- Architecture: flat slots plus `### AD-n — title` blocks with Binds / Prevents / Rule. The model supplies the text; code assigns `AD-n` and keeps only binds that match ids parsed from the approved PRD. No Stack section: the stack goes to Deferred with its reason, plus a development question.
- Epics: a nested schema. Code writes the headings and the `Epic N` / `Story N.M` numbering, builds the Requirements Inventory with hyphenated `FR-n`/`NFR-n`, and computes the FR Coverage Map. Any FR left uncovered becomes a development question. The 20,000-character limit stays, with caps on the number of epics and stories.
- "Preguntas para desarrollo" in both documents, pre-assigned to the development team.
- A hint to regenerate when the PRD (or Architecture) went back to Draft after the dependent document was drafted.
- Approval stays "Aprobar". Its confirmation text says the draft needs development review.
- Exporter prefix, BMAD paths and usage note. Fake drafter first, Gemini drafter last. Goldens.

### Out of Scope
- A web-verified stack or starter recommendation (goes to `docs/mejoras-posteriores.md`).
- Automatic regeneration and cascade invalidation.
- Renaming `BmadPlatform.*`, attachments sent to the model, sprint planning and story files.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `artifacts`: two new types, upstream-approval availability, AD and epics layouts, coverage-derived questions, the staleness hint, the approval copy and export.
- `initiatives`: a Large set completes with Architecture and Epics (RF-52).

Prerequisite: `artifacts` exists only in unarchived changes (`artifact-generation`, `bmad-alignment`, `gemini-assistant`), so write the delta against those.

## Approach (B: hybrid; budget 400, `exception-ok`)

| PR | Slice | Est. lines |
|---|---|---|
| S1 | Enum, catalog, `Availability` with upstream approval (35 refs, 9 files), texts, labels, routes | ~380 |
| S2 | Outlines and format, "Preguntas para desarrollo", `AD` prefix, checker handles the coverage map | ~300 |
| S3 | Fake Architecture: `DraftRequest.PrdMarkdown`, PRD fact parser, layout, golden | ~380 |
| S4 | Fake Epics: Architecture in the request, layout, golden, 20,000-character ladder | ~400 |
| S5 | Gemini Architecture | ~330 |
| S6 | Gemini Epics (nested schema and validator), export, docs | ~450 (exception) |

Total ~2,240 lines, stacked to main. Approach C (free-form Markdown) was rejected because it breaks RF-71 and RN-03.

## Requirement changes (intentions)

- RF-36, RF-44: the epics inventory keeps hyphenated `FR-n`/`NFR-n`, which reverses the `FR1:` decision. Also update AGENTS §6.3 and `docs/bmad-referencia.md` §4.
- RF-39: development questions are pre-assigned to the development team in Architecture and Epics.
- RF-52: Large completes with all five documents. Architecture needs an approved PRD; Epics needs an approved Architecture.
- RF-54: show a staleness hint on a dependent document when its upstream document went back to Draft.
- RN-02: the approval confirmation copy mentions development review.
- New RF-73 (Architecture from the approved PRD, stack deferred) and RF-74 (Epics from PRD plus Architecture, every FR covered or questioned).

## Affected Areas

| Area | Impact |
|---|---|
| `src/BmadPlatform.Domain/` (`ArtifactType`, catalog) | Modified |
| `src/BmadPlatform.Application/Features/Artifacts/` (`Availability`, `DraftRequest`, outline, composer) | Modified |
| `src/BmadPlatform.Infrastructure/{Artifacts,Gemini,Export}/` | Modified |
| `src/BmadPlatform.Web/` (artifact views, confirmation) | Modified |
| REQUISITOS.md, AGENTS.md, `docs/bmad-referencia.md`, `docs/mejoras-posteriores.md` | Modified |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| `lint_spine.py` rejects Spanish AD labels | Med | Labels contain the substrings binds/prevents/rule; lint the golden |
| Epics exceed 20,000 characters | Med | Caps on epics and stories, plus a shortening ladder |
| Model invents a stack or facts | Med | No Stack section; binds filtered by PRD ids; RF-71 sources |
| Checker false positives on repeated FR ids | Med | The checker skips the coverage map |
| Rubber-stamp approval | Low | RN-02 confirmation copy |
| Gemini cost for epics | Med | One call per document; Fake by default |

## Rollback Plan

Set `Assistant:Provider=Fake`, or revert the slice PRs in reverse order. No migration is involved. Stored Markdown stays valid.

## Success Criteria

- [ ] A Large initiative reaches *Lista para construir* in Chrome (Fake, then Gemini).
- [ ] BMAD 6.12.1 checks in a scratch repo: `lint_spine.py` passes on the Architecture golden; epics step 1 accepts the exported PRD plus Architecture; `bmad-build`/`bmad-spec` accept the epics frontmatter.
- [ ] Every FR appears in a story or in a development question.
- [ ] Lean tests and the build pass without a key. Views use CSS classes only.

## Proposal question round

The user decided all eight questions on 2026-10-14 (recommended options; decision 5 adjusted to BMAD's Deferred). Residual assumptions to review:
- Gemini drafting runs only when the upstream document is approved. There is no answers-only fallback.
- The epics and stories caps are set during design, inside the 20,000-character limit.
