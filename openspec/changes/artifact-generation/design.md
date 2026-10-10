# Design: Artifact generation (PROPUESTA-MVP section 7 step 4)

## Technical Approach

A third data module, `Artifacts` (table prefix `art_`), built like `Initiatives` and `Assistant`. One aggregate, `Artifact`, holds one Markdown document per initiative and type, with a `Version` concurrency token and a Draft/Approved status. A pure `ArtifactCatalog` says which documents each level requires and when each one can be generated. A deterministic `FakeArtifactDrafter` behind `IArtifactDrafter` turns the conversation answers into Markdown. The Markdown helpers (`ItemNumberer`, `ArtifactFormatChecker`) live in Application and are shared by the fake now and by Gemini in step 5. Coupling to other modules goes through MediatR only: `GetInitiativeQuery`, the new `GetConversationAnswersQuery` (Assistant), and the new `MarkReadyToBuildCommand` and `ReopenPlanningCommand` (Initiatives). The design traces RF-07, RF-16 to RF-20, RF-43, RF-44, RF-51 to RF-60, RN-06 and RN-07.

The guiding invariant is RN-06: **an initiative is Ready to build only while every required artifact is approved.** Every write is ordered so that a half-completed operation can leave the initiative in Planning with all artifacts approved (recoverable by a retry), but never Ready to build with a Draft artifact.

## Architecture Decisions

| # | Topic | Options and tradeoff | Decision |
|---|---|---|---|
| 1 | Storage | Row per version: history comes free, but RF-18 keeps history out of the MVP / one row per type: simple, overwrite in place | **One row per (InitiativeId, Type)**, unique index. `Version` is a counter, not a history |
| 2 | Concurrency | Last write wins: a stale tab silently erases edits / version token | **`Version` is both the user-facing number and the EF concurrency token.** Commands carry `ExpectedVersion`, checked before mutating and as the EF original value on save. Approval does not bump it (RF-18), so an approval races an edit only at the DB `WHERE Version = n` |
| 3 | Required set for Large | Add `Architecture` and `Epics` to the enum now: no drafter, dead branches / catalog entries without a type | **Catalog entries `ArtifactSlot(Label, ArtifactType? Type)`**. Large lists PRD plus two slots with `Type = null` ("Disponible próximamente"). `IsSetComplete` is false while any slot has no type, so Large can never reach Ready to build (RF-52) |
| 4 | Set completion order (approve) | Mark the initiative first: a failed approval save leaves Ready to build with a Draft (breaks RN-06) / approve first | **Approve first, then `MarkReadyToBuildCommand`.** A failure leaves Planning with everything approved. `ApproveArtifactCommand` is idempotent at the same version (no save, the completion check runs again), so the page offers "Reintentar" when the view reports `CompletionPending` |
| 5 | Reopen order (edit or regenerate an approved artifact while Ready to build) | Save first: a failed reopen leaves Ready to build with a Draft / reopen first | **`ReopenPlanningCommand` first, then save.** A failed save leaves Planning with everything approved, which is the same recoverable `CompletionPending` state as decision 4 |
| 6 | Format index (RF-43, RF-56) | Persist an items table now / derive warnings on read | **Derived on read, nothing stored.** `ArtifactFormatChecker` is a pure function of the Markdown; the items table, owners and statuses are a later change |
| 7 | Generated length | Truncate the finished document / bound each inserted answer | **The drafter contract requires output of at most `Artifact.ContentMaxLength` (20,000).** The fake clips each inserted answer to 1,500 characters with "…"; a test with every answer at 2,000 characters proves the bound. The domain rejects anything longer anyway |
| 8 | Identical save | Bump the version anyway / no-op | **No-op**: content equal after line-ending normalization changes nothing (no version, no reopen) |
| 9 | Rendering | Markdig in Application / in Web | **Web only** (`MarkdownRenderer`), raw HTML disabled, link and image URLs restricted to `http`, `https`, `mailto` and relative; others are emptied |
| 10 | Document styles | Tailwind typography plugin: availability in the standalone CLI not verified / scoped CSS | **`.doc-prose` rules in the Tailwind input CSS** with `@apply` and the theme tokens |
| 11 | Spec item ids | BMAD `CAP-n` / RF-44 `FR-n` | **`FR-n`**: RF-44 has no `CAP` prefix and one checker serves every type |
| 12 | Generic text helpers | Artifacts reads `AssistantScript` topics / the answers DTO carries what is needed | **The DTO carries `TopicKey`, `Question` and `Text`.** Artifacts references only the public `AssistantScript.Keys` constants |

## Domain (`Domain/Artifacts`)

- `ArtifactType { Spec, Brief, Prd }`, `ArtifactStatus { Draft, Approved }`.
- `Artifact`: `Id`, `InitiativeId`, `OwnerId`, `Type`, `Version`, `Status`, `Content`, `CreatedAt`, `UpdatedAt`, `ApprovedAt?`. `ContentMaxLength = 20_000`.
  - `Create(initiativeId, ownerId, type, content, now)`: Draft, version 1.
  - `Replace(content, now)` (regenerate): version + 1, Draft, `ApprovedAt = null`.
  - `Edit(content, now)`: returns `false` and changes nothing when the content is identical; otherwise version + 1, Draft, `ApprovedAt = null`.
  - `Approve(now)`: Draft becomes Approved and sets `ApprovedAt`; version unchanged; already Approved is a no-op.
  - Content is normalized (`\r\n` to `\n`), must be non-blank ("El documento no puede estar vacío.") and at most 20,000 ("El documento no puede superar los 20000 caracteres."), otherwise `DomainException`.
  - `WasApproved` helper for handlers: the status before a mutation.
- Initiatives: `Initiative.MarkReadyToBuild(now)`: Planning becomes ReadyToBuild; ReadyToBuild is a no-op; anything else throws "La iniciativa debe estar en Planificando para pasar a Lista para construir." `Initiative.ReopenPlanning(now)`: ReadyToBuild becomes Planning; Planning is a no-op; anything else throws. Neither touches mode or depth, and `SetDepth` still rejects Planning and ReadyToBuild (RF-29).

## Application

### Catalog and availability (`Features/Artifacts/ArtifactCatalog.cs`)

| Level | Slots (order = `InitiativeTexts.Deliverables`) |
|---|---|
| Small | Especificación (`Spec`) |
| Standard | Brief (`Brief`), PRD (`Prd`) |
| Large | PRD (`Prd`), Arquitectura (null), Épicas e historias (null) |

`Availability(type, status, depth, answers)` returns `ArtifactAvailability { Available, InitiativeDraft, LevelNotSet, NotRequired, WaitingForClarify, WaitingForClosing, NotYetSupported }`:

- Brief: Standard, status Clarifying, Planning or ReadyToBuild, and `answers.ClarifyComplete` (RF-53).
- Spec and PRD: `answers.PlanComplete` (RF-58).
- `IsSetComplete(depth, summaries)`: every slot has a type and its artifact is Approved.

Generation needs `Available`. Edit, regenerate and approve need an existing row whose type is still required and a non-Draft initiative; regenerate also needs `Available` (it redrafts from the answers).

### Markdown helpers (`Features/Artifacts/Markdown/`)

- `ItemPrefix` constants `FR`, `NFR`, `UJ`, `SM`, `A`, `Q`; `ItemNumberer` hands out `FR-1`, `FR-2`… per prefix, starting at 1 for every draft (RF-60).
- `ArtifactOutline.Sections(type)`: the required headings (Spanish), from `docs/bmad-referencia.md` section 2. Canonical item line: `- **FR-1**: texto`; inline assumption: `[ASSUMPTION: A-1]`.
- `ArtifactFormatChecker.Check(type, markdown)` returns `FormatWarning(int? Line, string Message)`:
  - a missing required heading: "Falta la sección «Alcance del MVP»."
  - a malformed id (`FR1`, `FR -1`, `fr-1`, `FR-`, `FR-01`): "Línea 12: «FR1» no sigue el formato FR-n."
  - the same id defined twice: "Línea 30: FR-2 ya está definido en la línea 14."
  - an `[ASSUMPTION` marker without a valid `A-n`.
  - It never changes the text.

### Drafter

```csharp
public interface IArtifactDrafter { Task<string> DraftAsync(DraftRequest request, CancellationToken ct); }
public sealed record DraftRequest(ArtifactType Type, string InitiativeName, string? Description,
    InitiativeDepth Depth, IReadOnlyList<TopicAnswer> Answers);
```

No ids, owner or e-mail. `FakeArtifactDrafter` (`Infrastructure/Artifacts`) is pure: no clock, no randomness, every answer used at most once per document.

| Answer | Brief | PRD | Spec |
|---|---|---|---|
| idea (+ description) | Resumen ejecutivo, La solución | 0 Propósito, 1 Visión | Por qué |
| problem | El problema | 0 Propósito, 2.1 Trabajos por hacer | Por qué |
| users | A quién sirve | 2 Usuario objetivo, `UJ-n` (always tagged `[ASSUMPTION: A-n]` "recorrido por confirmar"; a real journey question is for step 5) | Por qué |
| capabilities | — | 4 `FR-n`, 6.1 Dentro | Capacidades `FR-n` |
| constraints, qualities | — | 4 `NFR-n` (feature "Requisitos no funcionales") | — (Restricciones takes an assumption) |
| success | Criterios de éxito `SM-n` | 7 `SM-n` | Señal de éxito `SM-n` |
| out-of-scope | Alcance (fuera) | 5 No objetivos, 6.2 Fuera | No objetivos |
| priorities | — | 6.1 Dentro (annotates the capabilities) | — |
| integrations, teams (Large) | — | 4 `FR-n` (feature "Integraciones") | — |
| "No sé" (any topic) | Preguntas pendientes `Q-n` with the question | 8 Preguntas abiertas `Q-n` | Preguntas abiertas `Q-n` |
| empty section | `[ASSUMPTION: A-n]` plus the Índice de suposiciones | same | same |

Multi-line answers split into one item per non-empty line, with leading bullets and numbers stripped. User lines that start with `#` are escaped as `\#` so they cannot create headings. Every section nothing feeds gets an assumption (Brief: Qué lo hace distinto, Visión, inside of Alcance; PRD: Glosario, 2.2 No usuarios; Spec: Restricciones).

### Cross-module contracts

- Assistant: `GetConversationAnswersQuery(InitiativeId)` returns `ConversationAnswers?` (null when the initiative is not found): `ClarifyComplete`, `PlanComplete`, `Answers: IReadOnlyList<TopicAnswer(TopicKey, Question, Text, IsUnknown)>`. Visible user answers in order, content topics only (no size, depth-proposal or confirm-planning). Computed with `ConversationJourney`: `ClarifyComplete = Clarify.IsComplete && Depth is not null`; `PlanComplete = Status is Planning or ReadyToBuild && (Plan is null || Plan.IsComplete)`. A missing conversation gives empty answers and both flags false.
- Initiatives: `MarkReadyToBuildCommand(Id)` and `ReopenPlanningCommand(Id)`, idempotent, owner-scoped, sent only by Artifacts (RF-34).
- Boundary tokens: Artifacts adds `(?<!\.)\bArtifact\b(?!\s*[,;)])`, `\bIArtifactRepository\b`, `Infrastructure\.Artifacts`, `\bart_`. The names `GetConversationAnswersQuery`, `ConversationAnswers` and `InitiativeDetails` contain no bare `Conversation` or `Initiative` token, so they are allowed. Initiatives code must not mention `Artifact` outside `//` comments.

### Use cases (`Features/Artifacts/`)

| Request | Result | Rules |
|---|---|---|
| `GetArtifactsOverviewQuery(InitiativeId)` | `ArtifactsOverview?` | Slots with state (not available plus reason, ready to generate, Draft vN, Approved vN) and `CompletionPending` |
| `GetArtifactQuery(InitiativeId, Type)` | `ArtifactView?` | Content, warnings, availability, and the `Can*` flags, `ApprovalCompletesSet`, `CompletionPending` |
| `GenerateArtifactCommand(InitiativeId, Type)` | `ArtifactView` | Requires `Available` and no row ("El documento ya existe. Use Regenerar para reemplazarlo."). Drafter, `Create`, `AddAsync`; a unique violation becomes `ConflictException` |
| `SaveArtifactContentCommand(InitiativeId, Type, ExpectedVersion, Content)` | `ArtifactView` | Version check; if unchanged, no-op; if Approved and the initiative is ReadyToBuild, `ReopenPlanningCommand` first; then `Edit` and `SaveAsync`. `ToString` redacts `Content` |
| `RegenerateArtifactCommand(InitiativeId, Type, ExpectedVersion)` | `ArtifactView` | Requires `Available`; version check; drafter; reopen first as above; `Replace`, `SaveAsync` |
| `ApproveArtifactCommand(InitiativeId, Type, ExpectedVersion)` | `ArtifactView` | Version check; `Approve` (no save when already approved); if the status is Planning and `IsSetComplete`, `MarkReadyToBuildCommand` |

- Validators: `InitiativeId` not empty, `Type` `IsInEnum`, `ExpectedVersion > 0`, content not empty and at most 20,000, with the Spanish messages above.
- Port: `IArtifactRepository { GetAsync(initiativeId, type, ownerId); ListAsync(initiativeId, ownerId) -> ArtifactSummary(Type, Status, Version); AddAsync; SaveAsync(artifact, expectedVersion) }`. Every read takes the owner; summaries never load content.
- Errors: `ConflictException(ArtifactTexts.Conflict)`: "El documento cambió en otra pestaña. Recargue la página." Other texts go in `ArtifactTexts`. `UserMessages` already maps `ConflictException` and `DomainException`; no new exception type is needed.

## Data Flow

```
Approve: Page -(type, v)-> ApproveArtifactHandler -> GetInitiativeQuery(owner) -> repo.Get -> version check
   -> Approve -> repo.Save(v)                                [write 1]
   -> repo.List -> Catalog.IsSetComplete -> MarkReadyToBuildCommand  [write 2, Initiatives]
Edit approved while ReadyToBuild: ReopenPlanningCommand [write 1] -> Edit -> repo.Save(v) [write 2]
Generate: GetConversationAnswersQuery -> Catalog.Availability -> IArtifactDrafter -> Create -> repo.Add
```

## Persistence (`Infrastructure/Artifacts`)

- `art_artifacts`: `Id`, `InitiativeId`, `OwnerId` `varchar(255)`, `Type` and `Status` `varchar(20)` strings, `Version` (`IsConcurrencyToken`), `Content` `longtext` (utf8mb4 `varchar(20000)` would exceed the row limit), `CreatedAt`, `UpdatedAt`, `ApprovedAt` `datetime(6)`. Unique index (`InitiativeId`, `Type`). No foreign keys.
- `ArtifactRepository` over `IDbContextFactory`, one context per call, the same `SaveAsync` pattern as `ConversationRepository` (`OriginalValue = expectedVersion`; `DbUpdateConcurrencyException` and `DbErrors.IsUniqueViolation` become `ConflictException`).
- DI: the repository is scoped; `IArtifactDrafter` is a singleton `FakeArtifactDrafter` with a short delay (about 900 ms) so the loading state can be seen.
- Migration `AddArtifacts`. Model tests: the table list gains `art_artifacts`; unique index; token; string enums; `longtext`; no FKs; migration present; repository and drafter registration.

## Web

- `Components/Artifacts/ArtifactsCard.razor` replaces the "Artefactos" placeholder. It shows one row per slot (label, state badge, link or reason) from `GetArtifactsOverviewQuery`; a failure gives a neutral card, like `ConversationCard`.
- Page `Pages/Artifacts/ArtifactDocument.razor`, route `/iniciativas/{Id:guid}/artefactos/{Slug}`, slugs `especificacion`, `brief`, `prd` (`ArtifactLabels.TryParseSlug`; unknown gives `InitiativeNotFound`), `[Authorize]`.
  - Read view: title, "Versión N", Borrador/Aprobado badge, the rendered document in `.doc-prose`, and the warnings panel (`role="status"`).
  - Actions: Generar, Editar, Regenerar, Aprobar, Volver.
  - Edit mode: labelled textarea "Contenido en Markdown" with `maxlength=20000` and a "0 / 20.000" counter, Guardar and Cancelar. When approved, a hint says "Al guardar, el documento vuelve a Borrador" (plus "y la iniciativa vuelve a Planificando" when it is Ready to build).
  - Confirmation panels in the delete-panel pattern:
    - Regenerar (RN-07): "Se reemplazará todo el contenido, incluidos sus cambios manuales."
    - Aprobar: lists the warnings (RF-59) and says when the approval will make the initiative Ready to build.
  - Feedback: green success notices ("Documento generado.", "Cambios guardados. Versión N.", "Documento aprobado. La iniciativa está Lista para construir."), the typing-dot loading indicator "Generando el documento…", a `busy` flag, controls disabled until interactive, and "Reintentar" when `CompletionPending`.
  - Errors: `catch … when (UserMessages.TryGet)`; a conflict reloads through the query.
- `MarkdownRenderer` (singleton): Markdig pipeline with `DisableHtml()`, pipe tables and URL filtering, returned as a `MarkupString`.
- `ArtifactLabels` (pure, tested): slugs, names, state texts, reasons per availability, success texts.
- `ChatPanel`: when `Journey.NextTopic` is Closing or the view is read-only, it shows a call to action "Ir a los documentos" linking to `/iniciativas/{id}#section-artifacts` (`AssistantLabels.DocumentsCallToAction`).
- Packages: `Markdig` (BSD-2-Clause) in central package management, referenced by Web only.

## File Changes (by commit)

| # | Create / Modify |
|---|---|
| 1 Initiatives transitions (~200) | M `Domain/Initiatives/Initiative.cs`; C `Features/Initiatives/{MarkReadyToBuild,ReopenPlanning}/{Command,Handler}.cs`; tests `Domain/InitiativeTests.cs`, `InitiativeTransitionTests.cs` |
| 2 Domain, catalog, Markdown (~600) | C `Domain/Artifacts/{Artifact,ArtifactType,ArtifactStatus}.cs`; `Features/Artifacts/{ArtifactCatalog,ArtifactTexts}.cs`, `Markdown/{ItemPrefix,ItemNumberer,ArtifactOutline,ArtifactFormatChecker,FormatWarning}.cs`; tests |
| 3 Answers query and drafter (~650, goldens excluded) | C `Features/Assistant/GetConversationAnswers/*`; `Features/Artifacts/{IArtifactDrafter,DraftRequest}.cs`; `Infrastructure/Artifacts/FakeArtifactDrafter.cs`; tests plus `Infrastructure.Tests/Artifacts/Goldens/*.md` |
| 4 Use cases (~900) | C `Features/Artifacts/{IArtifactRepository,ArtifactView,ArtifactsOverview,ArtifactViewBuilder}.cs`, folders `GetArtifact`, `GetArtifactsOverview`, `GenerateArtifact`, `SaveArtifactContent`, `RegenerateArtifact`, `ApproveArtifact`; tests, `TestDoubles/{InMemoryArtifactRepository,ModulesSender,ScriptedArtifactDrafter}` |
| 5 Persistence and boundary (~300 plus generated) | C `Infrastructure/Artifacts/{ArtifactConfiguration,ArtifactRepository}.cs`, migration `*_AddArtifacts`; M `Infrastructure/DependencyInjection.cs`, snapshot, `ApplicationDbContextModelTests.cs`, `ModuleBoundaryTests.cs` |
| 6 UI (~850) | C `Web/Components/Artifacts/{ArtifactsCard.razor,ArtifactLabels.cs,MarkdownRenderer.cs,ConfirmPanel.razor}`, `Pages/Artifacts/ArtifactDocument.razor`; M `InitiativeDetail.razor`, `ChatPanel.razor`, `AssistantLabels.cs`, `Program.cs` (renderer), `Web.csproj`, `Directory.Packages.props`, Tailwind input CSS; tests `Web.Tests/Artifacts/{ArtifactLabelsTests,MarkdownRendererTests}.cs` |

`ModulesSender` replaces `InitiativesSender` as the double for cross-module tests. It routes the Initiatives and Assistant requests to their real handlers over in-memory repositories.

## Testing Strategy (xUnit only)

| Layer | What | Approach |
|---|---|---|
| Domain | `Artifact` versioning (edit +1, identical no-op, approve keeps the version, edit or replace after approval returns to Draft), length and blank rules; `MarkReadyToBuild` and `ReopenPlanning` idempotency and the RF-29 lock | Fixed time |
| Application | Catalog per level, availability matrix; the numberer; checker cases (each warning, a clean document gives none, text never changes); answers query (flags per level, undone answers ignored, "No sé" flagged); handlers (owner isolation, stale version gives a conflict, Generate twice, set completion for Small and Standard, never for Large, reopen-first order, failure injection gives `CompletionPending` and retry, regenerate renumbers) | Hand-written doubles with detached copies |
| Infrastructure | Fake drafter: goldens for Spec, Brief, PRD Standard and PRD Large (byte-exact, `\n`; `UPDATE_GOLDENS=1` rewrites them), zero checker warnings on every output, at most 20,000 with maximum answers, determinism, `#` escaping; model tests | Goldens in the test project |
| Architecture | Three-module boundary, including the token sample tests for `Artifact` | Source scan |
| Web | `ArtifactLabels`; `MarkdownRenderer` (`<script>` and `<img onerror>` stay text, `javascript:` links emptied, headings and lists render) | Pure C#; Chrome walkthrough per AGENTS 5.4 |

## Threat Matrix

N/A: there is no routing, shell, subprocess, VCS/PR automation, executable-file classification or process-integration boundary. The content-injection risk (Markdown to HTML) is covered by decision 9 and its tests.

## Migration / Rollout

The migration `AddArtifacts` is additive. Rollback: `dotnet ef database update AddAssistantConversations`. `ReadyToBuild` is an existing enum value.

## Review Forecast

About 3,500 hand-written lines in six commits on one PR (exception-ok, as in the proposal). 400-line budget risk: High. Decision needed before apply: No. Chained PRs recommended: No.

## Risks

- An approve racing an edit in another tab can still leave Ready to build with a Draft artifact. The window is small; the next edit or approval self-corrects it through the same checks.
- Fake output is crude until Gemini arrives (step 5). The goldens will change then.
- Line-based checker heuristics may give false warnings on unusual but valid Markdown. Warnings never block (RF-59).
- Draft rows left behind after a level change in Clarifying stay hidden. They come back if the level is restored.

## Open Questions / Assumptions for the orchestrator

- [ ] A1: Spec capabilities use `FR-n`, not BMAD `CAP-n` (decision 11).
- [ ] A2: Saving identical content is a silent no-op with no version change (decision 8).
- [ ] A3: Recovery from a failed completion is "Reintentar", which resends the idempotent Approve; there is no separate command.
- [ ] A4: An artifact of a type no longer required (level changed in Clarifying) is hidden from the card; commands on it are rejected with "Este documento no corresponde al nivel de la iniciativa."
- [ ] A5: The closing call to action links to the detail page's Artefactos card, not straight to a document.
- [ ] A6: Format warnings are computed on read and on save, and are never stored.
- [ ] A7: Generated answers are clipped at 1,500 characters each to respect the 20,000 cap.
- [ ] A8: Route slugs are `especificacion`, `brief` and `prd`.
