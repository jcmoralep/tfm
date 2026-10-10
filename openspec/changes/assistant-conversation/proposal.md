# Proposal: Assistant conversation (PROPUESTA-MVP section 7 step 3)

## Intent

An initiative in *Aclarando* has nowhere to go: the detail page shows a "Conversación" placeholder and no path to *Planificando*. Product people, often junior (RF-41), need a guided, persisted conversation that asks one question at a time, shows the journey, and moves the initiative forward only when they confirm. Step 3 delivers that flow end to end with a deterministic fake `IAssistantService`, so the flow, persistence and status rules are proven before Gemini (step 5) and document generation (step 4).

## Scope

### In Scope
- New `Assistant` module: `Conversation` and `Message` per initiative (RF-13), table prefix `asi_`, no FK or navigation into Initiatives or Identity.
- Our BMAD-inspired script as data: topics per level (Small ~6 with no Planificar, Standard ~9-10, Large ~11), Aclarar closes with the confirmation, Planificar topics (RF-35, RF-46); thin Small ideas get the three basic questions, never a redirect (RF-42).
- Guided mode only (RF-48): one question per turn, an example, quick replies including "No sé", a short "why" and progress (RF-41).
- Free text up to 2000 characters next to quick replies; "Deshacer mi última respuesta" for the last answer only, never for one that changed the initiative (RF-49).
- Derived progress that survives a level change during *Aclarando* without restart (RF-50).
- Automatic mode: sizing question, depth suggestion; accepting switches to Manual with that level (RF-47, RF-28).
- Explicit, non-expiring confirmation "Sí, pasar a Planificar" or "Quiero añadir algo" (RF-45).
- Page `/iniciativas/{id}/asistente` (chat plus journey panel), detail "Conversación" card, visible demo-data note.
- Module boundary architecture test; xUnit tests with hand-written doubles.

### Out of Scope
- Brief/PRD/spec generation, including RF-38 Brief auto-generation (step 4).
- Structured artifact index and FR/NFR id scheme (RF-43, RF-44); "No sé" becomes a tracked open question with an owner only in step 4.
- Real Gemini, fast mode RF-12, streaming (step 5); attachments (step 7); export.
- *Listo para construir* transition (RF-07); editing or deleting messages beyond the single undo; restarting a conversation.
- Pushing back on weak answers and contradiction detection (need a real model).

## Capabilities

### New Capabilities
- `assistant-conversation`: conversation lifecycle, script and topic coverage per level, guided turns, quick replies, undo, journey and progress, depth suggestion, confirmation to plan, ownership and privacy note.

### Modified Capabilities
- `initiatives`: new `StartPlanning` transition (*Aclarando* to *Planificando*, requires a depth, only via the assistant's confirmation, RF-34/RF-45); new `SetInitiativeDepthCommand` that sets Manual plus level (RF-47); depth stays editable in *Aclarando* without resetting the conversation and locks from *Planificando* (RF-29, RF-50); detail "Conversación" section is no longer a placeholder.

## Approach

- **Domain** `Assistant/`: `Conversation` (phase), `Message` (sequence, role, content, topic key, quick replies); pure rules for the next topic and coverage. `Initiative.StartPlanning(now)`.
- **Application** `Features/Assistant/`: `AssistantScript` catalog, `ConversationJourney`, `IAssistantService` (`ReplyAsync(request) -> text, quick replies, suggested depth`; Application owns progress and next topic), `IConversationRepository` (owner on every method), commands `StartConversation` (idempotent), `SendMessage`, `UndoLastAnswer`, query `GetConversation`; cross-module calls only through Initiatives MediatR requests. Never log message content.
- **Infrastructure**: configurations, repository over `IDbContextFactory`, unique (ConversationId, Sequence) translated to a Spanish conflict message, `FakeAssistantService` (pure, deterministic), migration `AddAssistantConversations`.
- **Web**: `InitiativeAssistant.razor`, `ChatMessageList`, `ChatBubble`, `QuickReplies`, `ChatComposer`, `JourneyPanel`, `wwwroot/js/chat.js` (scroll); inject only `IMediator`; accessible log/status roles; Spanish copy.

## Decisions for sdd-design
- Undo: hidden (soft flag) versus deleted messages, and how coverage and progress ignore undone answers; which messages count as "changed the initiative".
- Topic catalog under a level change: required set recomputed, answered topics kept, extra answers retained but not required.
- Quick reply persistence (JSON column versus re-derived from the script by topic key).
- `StartConversation` idempotency under prerender (`OnAfterRenderAsync(firstRender)`) and concurrent tabs.
- Non-atomic confirmation: conversation write then `StartPlanningCommand`; idempotent command and retry on resume.
- Architecture test shape (source scan of usings, no new package).
- Location of Spanish script texts (Application data class versus resource file).
- Privacy note wording and placement ("Modo demostración: use solo datos de ejemplo").
- Representation of "No sé" answers so step 4 can turn them into open questions.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/BmadPlatform.Domain/Assistant/` | New | Entities, enums, rules |
| `src/BmadPlatform.Domain/Initiatives/Initiative.cs` | Modified | `StartPlanning` |
| `src/BmadPlatform.Application/Features/Assistant/` | New | Script, journey, ports, use cases, validators |
| `src/BmadPlatform.Application/Features/Initiatives/` | Modified | `StartPlanningCommand`, `SetInitiativeDepthCommand` |
| `src/BmadPlatform.Application/.../UserMessages` | Modified | Conflict and unavailable messages |
| `src/BmadPlatform.Infrastructure/Assistant/`, `Migrations/` | New | Config, repository, fake, migration |
| `src/BmadPlatform.Web/Components/` | New/Modified | Assistant page, chat components, detail card |
| `src/BmadPlatform.Web/wwwroot/js/chat.js` | New | Scroll to end |
| `tests/` | New/Modified | Domain, script, handler, fake, boundary tests |
| README / config | Modified | Free-tier privacy warning (AGENTS 4.8) |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Script is our adaptation, not BMAD verbatim | High | Labelled BMAD-inspired; data-only, easy to tune (RF-46) |
| Confirmation and status change not atomic | Med | Idempotent `StartPlanningCommand`, retry on resume |
| Prerender runs lifecycle twice | Med | Start in `OnAfterRenderAsync(firstRender)`; idempotent command |
| Undo and level change corrupt progress | Med | Progress derived from visible answers only; unit tests per level |
| Cross-module leak | Med | Boundary test in this change |
| Users read "Manual" after accepting a suggestion as surprising | Low | Accepted by RF-47 |
| Real data typed into a demo | Med | Visible chat note; fake sends nothing out |
| Chat UI untested (no bUnit) | Med | Logic kept in handlers and pure classes |

## Review Workload Forecast

| Part | Hand-written lines |
|------|--------------------|
| Domain (Assistant + `StartPlanning`) | ~220 |
| Application (script, journey, 4 use cases, 2 Initiatives commands, ports) | ~700 |
| Infrastructure (configs, repository, fake, DI) | ~330 |
| Web (page, 6 components, card, JS) | ~600 |
| Tests | ~800 |
| **Total** | **~2,500-2,900** |
| Generated migration + snapshot | ~800 (excluded) |

- 400-line budget risk: High (about 6-7x). It does not fit.
- Recommended commit slicing: (1) Initiatives `StartPlanning` + `SetInitiativeDepthCommand` + tests (~250); (2) Assistant domain, script, journey + tests (~550); (3) Application use cases incl. undo + fake + tests (~900); (4) persistence, migration, boundary test (~450 + generated); (5) chat page and components (~600); (6) detail card, README, docs (~100).
- Delivery options for the user (not chosen here): one PR with an explicit size exception using the commits above; or two stacked PRs, backend (1-4, ~2,150) then UI (5-6, ~700). Both PRs still exceed 400.

## Rollback Plan

Revert the PR(s) and run `dotnet ef database update <previous migration>` to drop the `asi_` tables. The only change to existing data is initiatives moved to *Planificando*/Manual by users; reverting code leaves them valid, since those values already exist in the schema.

## Dependencies

- Initiatives module (merged in `main`); updated REQUISITOS RF-35..RF-50 (in `main`).

## Success Criteria

- [ ] Owner opens the assistant on a *Aclarando* initiative; another user gets not found.
- [ ] Each level asks its topic set one at a time with example and quick replies; leaving and returning restores history and progress.
- [ ] Changing the level in *Aclarando* keeps answers and asks only missing topics.
- [ ] Undo removes only the last answer and is refused after the status change.
- [ ] Automatic: accepting the suggestion leaves Manual plus that level.
- [ ] *Planificando* happens only after "Sí, pasar a Planificar"; the level then locks.
- [ ] Boundary test passes; `dotnet build` and `dotnet test` (Release) pass with zero warnings.

## Proposal question round

Completed by the orchestrator before this phase and confirmed by the user; decisions are recorded in REQUISITOS RF-45..RF-50 and Engram `sdd/assistant-conversation/decisions`.
