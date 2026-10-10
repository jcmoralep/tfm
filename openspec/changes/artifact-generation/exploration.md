# Exploration: artifact-generation (PROPUESTA-MVP §7 step 4)

Source: sdd-explore subagent report, persisted by the orchestrator. Product decisions are still open (see the end).

## Current state

- The Assistant module (step 3) is built. `ConversationJourney.NextTopic` returns the `Closing` topic when the initiative is in Planning and every plan topic is covered. The closing text says the next step is generating the documents; no UI action exists yet.
- Answers are stored per topic key (idea, users, problem, success, out-of-scope, capabilities, constraints, priorities, integrations, qualities). `AnswerKind.Unknown` marks "No sé" and is reserved for step 4 open questions.
- `IAssistantService` is the wrong abstraction for documents: it takes a topic and returns chat text.
- `ReadyToBuild` is unreachable today: `Initiative.cs` has `Complete` (Draft to Clarifying) and `StartPlanning` (Clarifying to Planning) only. Step 4 makes it reachable, so the card and read-only chat paths marked "NOT OBSERVED" in assistant-conversation become verifiable.
- The initiative detail has an "Artefactos" placeholder (`InitiativeDetail.razor`, the `Placeholders` array).
- `ModuleBoundaryTests` is data-driven (Initiatives, Assistant); Artifacts is a third entry. `ApplicationDbContextModelTests` hard-lists tables and enforces a prefix on each.
- No Markdown library is referenced. `Program.cs` keeps the default 32 KB SignalR receive limit, so edited documents need a length cap (about 20,000 characters).

## Approach (recommended)

New `Artifacts` module (table prefix `art_`), one row per initiative and type in `art_artifacts` (Id, InitiativeId, OwnerId, Type, Version as concurrency token and user-facing version, Status Draft/Approved, Content longtext Markdown, ApprovedAt, CreatedAt, UpdatedAt; unique index on InitiativeId + Type; no foreign keys). The stored text carries no status/version frontmatter (single source of truth, RF-20); the step 8 exporter composes it. Version increments on every content change; approval does not bump it.

A new `IArtifactDrafter` (Application) with `FakeArtifactDrafter` (Infrastructure) mirrors the `IAssistantService` pattern; Gemini replaces it at step 5. Shared structure (headings, FR/NFR/UJ/SM/A/Q numbering per RF-44) lives in an Application helper. The fake is a pure function: capabilities become `FR-n`, constraints and qualities `NFR-n`, success `SM-n`, "No sé" answers become `Q-n`, empty sections carry `[ASSUMPTION: A-n]`. Input comes through a new MediatR query in Assistant (`GetConversationAnswersQuery`) that returns a DTO, never the entity.

Coupling stays MediatR-only: Artifacts to Initiatives through `GetInitiativeQuery` plus new idempotent `MarkReadyToBuildCommand` (and a reopen command for edits after approval); Artifacts to Assistant through the answers query. `ArtifactCatalog.RequiredFor(depth)`: Small = Spec, Standard = Brief + PRD, Large = PRD (+ Architecture and Epics in step 6), checked against `InitiativeTexts.Deliverables`.

Flow: no row, Draft vN, edit gives Draft vN+1, Approve, edit after approval returns to Draft vN+1. Generate and Regenerate are explicit (Regenerate overwrites edits behind a confirmation panel). Commands carry `ExpectedVersion` (`ConflictException` on a stale tab) and redact content in `ToString`.

UI: `ArtifactsCard` replaces the placeholder; page `/iniciativas/{Id:guid}/artefactos/{Type}` with Guardar, Regenerar, Aprobar, Volver; `ArtifactLabels` (pure, tested); green success notice, red required asterisk, loading indicator; a call to action at the assistant's closing.

## Markdown structure (BMAD 6.12.1, docs/bmad-referencia.md §2)

- Brief: Resumen ejecutivo, El problema, La solución, Qué lo hace distinto, A quién sirve, Criterios de éxito (`SM-n`), Alcance, Visión, then Índice de suposiciones and Preguntas pendientes.
- PRD sections 0 to 9: propósito, visión, usuario objetivo (trabajos por hacer, no usuarios, recorridos `UJ-n`), glosario, funcionalidades (`FR-n` plus `NFR-n`), no objetivos, alcance del MVP, métricas (`SM-n`), preguntas abiertas (`Q-n`), índice de suposiciones.
- Small spec (`SPEC.md`): Por qué, Capacidades (`CAP-n` or `FR-n`), Restricciones, No objetivos (at least one), Señal de éxito.
- Not step 4: usage note and "Preguntas para desarrollo" (RF-39), Architecture/Epics, stakes calibration.

## Later steps

Gemini drafts: 5. Architecture and Epics: 6. Attachments feeding generation: 7. Download, copy, PDF, frontmatter, usage note: 8. Version history and diff: out of MVP (RF-18).

## Risks and size

About 3,000 to 3,500 hand-written lines including tests (step 3 was about 2,850). Delivery is exception-ok; the explorer suggests three stacked slices (domain and generator and use cases; cross-module commands, persistence and boundary; UI). Fake output quality is crude and needs golden tests. New Initiatives transitions must respect the locks (RF-29, RF-34). If Markdig is added, render with HTML disabled. Chrome walkthrough required (AGENTS §5.4).

## Open product decisions (asked one at a time)

1. Include the Small spec in step 4? Recommended: yes.
2. Large level: gate ReadyToBuild on the full required set? Recommended: yes.
3. When does the Brief appear (RF-38)? Recommended: right after Aclarar, with an explicit "Generar" button.
4. Editing after approval: allowed, back to Draft and Planning until re-approved? Recommended: yes.
5. Editor: raw textarea only, or rendered view plus edit mode (adds Markdig)? Recommended: rendered view plus textarea.
6. Structured index (RF-43): numbering and parser now, items table and owners later? Recommended: yes.
7. Regenerate overwrites edits behind a confirmation? Recommended: yes.
