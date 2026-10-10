# Proposal: Artifact generation (PROPUESTA-MVP section 7 step 4)

## Intent

The assistant's closing says "next, the documents", but nothing exists: the detail shows an "Artefactos" placeholder and *Lista para construir* is unreachable. Product people need to generate, read, edit, regenerate and approve their level's documents so the initiative can finish (RF-07, RF-16..RF-19, RF-51..RF-57).

## Scope

### In Scope
- `Artifacts` module (`art_` prefix): one Markdown document per initiative and type, version, Draft/Approved.
- Spec (Small), Brief and PRD (Standard), PRD (Large) from a deterministic `FakeArtifactDrafter` behind `IArtifactDrafter`.
- Explicit Generar; Brief available as soon as the Aclarar topics are covered, still in *Aclarando* (RF-53).
- Rendered read view plus Markdown textarea edit (Markdig, raw HTML disabled) (RF-55).
- RF-44 numbering; parser warns on broken format (RF-56).
- Regenerate overwrites behind a confirmation panel (RF-57).
- Approve; auto *Lista para construir* when the level's full set is approved; edit after approval returns to Draft vN+1 and *Planificando* (RF-52, RF-54).
- Detail card, document page, call to action at the assistant's closing.

### Out of Scope
- Gemini (step 5); Architecture and Epics (step 6), so Large cannot finish yet; attachments (7); export, frontmatter, usage note, "Preguntas para desarrollo" (8).
- Items table, owners and status of questions and assumptions (follow-up change).
- Version history and diff (RF-18); PRD questions derived from the Brief.

## Capabilities

### New Capabilities
- `artifacts`: required set per level, generate, view, edit, regenerate, approve, versioning, format warnings, ownership.

### Modified Capabilities
- `initiatives`: `MarkReadyToBuild` and reopen-to-Planning transitions; "Artefactos" card replaces the placeholder.
- `assistant-conversation`: closing offers the documents; answers query for drafting.

## Approach

Exploration's recommendation: `art_artifacts` (unique InitiativeId + Type, no FKs, Version as concurrency token); `ArtifactCatalog.RequiredFor(depth)`; shared numbering helper; MediatR-only coupling (`GetInitiativeQuery`, `GetConversationAnswersQuery`, idempotent `MarkReadyToBuildCommand`, reopen command); commands carry `ExpectedVersion` and redact content in logs.

## Affected Areas

| Area | Impact |
|------|--------|
| `src/*/Artifacts/`, `Features/Artifacts/` (all layers) | New |
| `Domain/Initiatives/Initiative.cs`, `Features/Initiatives/` | Modified |
| `Features/Assistant/` (answers query, closing) | Modified |
| `Web/Components/` (card, page, labels), Markdig package | New/Modified |
| `Infrastructure/Migrations/`, `tests/` | New/Modified |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Crude fake output | High | Golden tests; Gemini in step 5 |
| Status transitions break locks (RF-29, RF-34) | Med | Idempotent domain transitions, unit tests |
| Stale tab overwrites | Med | `ExpectedVersion`, conflict message |
| SignalR 32 KB limit | Med | Content cap (~20,000 chars) |
| HTML injection | Low | Markdig HTML disabled |

## Open Questions (resolved by the user, recommended options)
1. Spec and PRD are available only at the closing, when every topic of the level is covered (RF-58).
2. A document with format warnings can be approved; the warnings show in the confirmation (RF-59).
3. Regenerate renumbers from scratch; the RF-44 never-reuse rule applies to edits (RF-60).
4. Approving does not bump the version (RF-18).

## Review Workload Forecast

- ~3,000-3,500 hand-written lines; migration excluded.
- 400-line budget risk: High. Decision needed before apply: No (exception-ok). Chained PRs recommended: No.
- Commit slices: domain, drafter, use cases; cross-module commands, persistence, boundary; UI.

## Rollback Plan

Revert the PR and `dotnet ef database update <previous migration>` to drop `art_`. *Lista para construir* is an existing enum value, so initiatives stay valid.

## Success Criteria

- [ ] Each level generates its documents; other users get not found.
- [ ] Edit raises the version; approved edits reopen to Draft and *Planificando*.
- [ ] Regenerate needs confirmation; broken format warns without losing text.
- [ ] Small and Standard reach *Lista para construir*; Large does not.
- [ ] Build, tests (zero warnings), and Chrome walkthrough pass.

## Proposal question round

Seven decisions taken by the user (recommended options); recorded in REQUISITOS RF-51..RF-57, RN-06, RN-07.
