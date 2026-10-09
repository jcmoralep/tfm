# Tasks: Assistant conversation (PROPUESTA-MVP step 3)

Conventions: file naming follows the existing module (`XCommand.cs`, `XCommandHandler.cs`, `XCommandValidator.cs`, not the design's `Command.cs`). Tests are xUnit only, no bUnit, `strict_tdd` false (test written with or just before its code). Verbatim Spanish messages come from the SPEC (the contract); see Follow-ups for design drift. Threat matrix: N/A.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~2,850 hand-written (design per-commit table, tests included) plus a generated migration and snapshot (~400-600 not counted) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | Two stacked PRs: PR 1 = commits 1-4 (backend, ~2,150); PR 2 = commits 5-6 (UI + card + docs, ~700). Both still exceed 400; finer alternative: 1+2 (~800) / 3 (~900) / 4 (~450) / 5+6 (~700) |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

Config conflict: `openspec/config.yaml` line 36 says `delivery_strategy single-pr`, which contradicts `ask-on-risk` and the 400-line budget. Reconcile it (not edited here). A single PR would need `size:exception`.

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| C1 | Initiatives `StartPlanning` + `SetInitiativeDepth` | PR 1 | `dotnet test tests/BmadPlatform.Application.Tests --filter "FullyQualifiedName~Initiative"` | N/A, no UI | revert commit; no schema change |
| C2 | Conversation domain + script + journey | PR 1 | `dotnet test tests/BmadPlatform.Application.Tests --filter "FullyQualifiedName~Conversation|FullyQualifiedName~AssistantScript"` | N/A, pure code | revert; nothing references it yet |
| C3 | Use cases + fake service | PR 1 | `dotnet test tests/BmadPlatform.Application.Tests --filter "FullyQualifiedName~Assistant"` and Infrastructure.Tests `~FakeAssistant` | N/A, in-memory doubles | revert; no DB or UI wired |
| C4 | Persistence + migration + boundary test | PR 1 | `dotnet test tests/BmadPlatform.Infrastructure.Tests` and `tests/BmadPlatform.ArchitectureTests` | apply migration on docker MySQL 8.4, check `asi_` tables | `dotnet ef database update AddInitiatives`, revert |
| C5 | Chat UI | PR 2 (base = PR 1 branch if chained) | `dotnet test tests/BmadPlatform.Web.Tests` | Chrome checklist below | revert; route disappears |
| C6 | Detail card + README | PR 2 | `dotnet test` (all) | Chrome checklist items 14-16 | revert; placeholder returns |

## Task 0: Reconcile topic lists (blocks C2)

- [ ] 0.1 Adopt the "Reconciled topic lists" section below as the single source for `AssistantScript` and the tests (`AssistantScriptTests`, `ConversationJourneyTests`). Do not edit spec/design; apply follows this table.

## Reconciled topic lists

Rule of precedence: spec scenarios are the behavior contract; design provides the data structures (`AssistantTopic`, per-level phase membership); counts EXCLUDE confirmation and sizing/depth-proposal (Small 6, Standard 8, Large 10). RF-46 gives the Standard base (idea, users, problem, success, out of scope; then capabilities, constraints, two priorities), Small about 6, Large adds other systems/teams and qualities.

| Level | Aclarar (in order) | Confirm | Planificar (in order) | Count |
|---|---|---|---|---|
| Small | idea, users, problem, capabilities, out-of-scope, success | confirm-planning, then `closing` ("lista para redactar") | none | 6 |
| Standard | idea, users, problem, success, out-of-scope | confirm-planning | capabilities, constraints, priorities | 5 + 3 = 8 |
| Large | idea, users, problem, success, out-of-scope (same as Standard) | confirm-planning | capabilities, constraints, priorities, integrations, qualities | 5 + 5 = 10 |
| Automatic, no level | idea, users, problem, success, out-of-scope, size, depth-proposal | confirm-planning (withheld until depth set) | unknown until a level is set | n/a |

Decisions embedded:
- Small's sixth topic is `capabilities` (spec scenarios), NOT `constraints` (design). `constraints` is not asked in Small.
- Large's `integrations` and `qualities` are Planificar topics after priorities (spec "Large order", "Larger level adds only missing topics"), NOT Aclarar (design). So Standard to Large mid-Clarifying only adds Planificar topics; Small to Standard asks success, constraints later.
- Keys are shared across levels; coverage is by key, so a `capabilities` answer given under Small still covers Standard/Large.
- Counts shown in the progress text: `{covered} de {total} temas cubiertos` where total = the level count above (confirmation never counted; Small reaches "6 de 6" before the confirmation button). Aclarar and Planificar per-phase progress ("3 de 5 preguntas") stays in the panel. Spec example "2 de 9" becomes "2 de 8" (Standard).
- Automatic without level: overall total is not shown; panel shows Aclarar progress only and Planificar as "Se define al elegir el nivel".
- Depth proposal quick replies follow the spec: "Sí, usar el nivel X" (`depth:{level}`) and "Elegir otro nivel" (`depth:other`); after `depth:other` the assistant re-asks the same topic with the three levels (`depth:small|standard|large`, "Pequeña", "Estándar", "Grande"). Mode stays Automatic and depth empty until a level is chosen.

## Commit 1: Initiatives transitions (~250)

- [x] 1.1 `Domain/Initiatives/Initiative.cs`: add `StartPlanning(now)`; no-op if Planning (UpdatedAt untouched); Domain errors "La iniciativa debe estar en Aclarando para pasar a Planificando." and "Elija un nivel de profundidad antes de pasar a Planificando." Test: `Domain/InitiativeTests.cs` (Clarifying+depth, idempotent, Draft/ReadyToBuild, empty depth, locks mode/depth).
- [x] 1.2 Create `Features/Initiatives/StartPlanning/StartPlanningCommand.cs` and `StartPlanningCommandHandler.cs` (id only, owner-resolved, not found for foreign/deleted). Test: `Features/Initiatives/InitiativeTransitionTests.cs` (owner only, deleted, no status member via reflection).
- [x] 1.3 Create `Features/Initiatives/SetInitiativeDepth/{SetInitiativeDepthCommand,CommandHandler,CommandValidator}.cs` (`SetDepth(Manual, depth)`, Draft/Clarifying only, `IsInEnum`). Test: `InitiativeTransitionTests.cs` (Automatic empty to Manual, replace, Planning/ReadyToBuild rejected, undefined level, other user).
- [x] 1.4 `InitiativeTexts.cs`: add level names and deliverables moved from `Web/Components/Initiatives/InitiativeLabels.cs`, which delegates. Test: existing `Web.Tests/Initiatives/InitiativeLabelsTests.cs` stays green.
- [x] 1.5 Test: edit depth Standard to Large in Clarifying touches no conversation data; "only two status transitions" in `InitiativeTransitionTests.cs`.

## Commit 2: Domain and script (~550)

- [ ] 2.1 Create `Domain/Assistant/{MessageRole,AnswerKind,QuickReply}.cs` (`AnswerKind { FreeText, QuickReply, Unknown }`).
- [ ] 2.2 Create `Domain/Assistant/Message.cs` (limits 4000/50, soft `UndoneAt`, `AppliedToInitiative`, `QuickReplyKey`, `QuickReplies` snapshot with label+key).
- [ ] 2.3 Create `Domain/Assistant/Conversation.cs`: `Start`, `AddAssistantMessage`, `AddUserAnswer` (guard: last visible is assistant; trimmed; 1-2000), `UndoLastAnswer` (repeated, stops at applied answer or opening), `Version` bump on every mutator, monotonic `Sequence`. Test: `Domain/ConversationTests.cs` (sequence with undone rows, undo chain A1-A3, undo after first answer, repeated undo then empty, applied/confirmation refused, answer after confirmation undoable once, pending-reply undo, guard on pending reply, trimming 2000 with spaces, version).
- [ ] 2.4 Create `Features/Assistant/Script/{TopicKind,AssistantTopic,AssistantScript}.cs` with the Reconciled topic lists, Spanish "usted", example + reason per topic, "No sé" (`unknown`) on every Question/Choice. Test: `AssistantScriptTests.cs` (counts 6/8/10, unique keys, "No sé" present, example/reason non-empty, forbidden jargon spine/épica/invariante/slug).
- [ ] 2.5 Create `Script/{JourneySnapshot,ConversationJourney}.cs`: required set per mode/depth, next topic, per-phase progress, `PendingTransition`, sizing only Automatic + empty depth, confirmation withheld without depth, coverage by key ignoring undone. Test: `ConversationJourneyTests.cs` (orders per level, thin Small idea not redirected, sizing only in (a), Small accept adds capabilities, Standard to Large adds only missing, Large to Small, back and forth, extra answers excluded, "No sé" counts, progress text "2 de 8 temas cubiertos", quick-reply `confirm:add` does not cover confirmation).

## Commit 3: Use cases and fake service (~900)

- [ ] 3.1 Create `Common/Exceptions/{ConflictException,AssistantUnavailableException}.cs`; `LoggingBehavior.cs` logs both at Warning. Test: `Common/Behaviors/LoggingBehaviorTests.cs`.
- [ ] 3.2 Create `Features/Assistant/{IConversationRepository,IAssistantService,AssistantContracts,AssistantTexts}.cs` (request carries no ids/owner/email/timestamps). Test: request-members reflection in `FakeAssistantServiceTests.cs`.
- [ ] 3.3 Create `ConversationView.cs` and `ConversationViewBuilder.cs` (visible messages, quick replies from last assistant message, Journey, `CanSend`, `CanUndo`, `NeedsResume`, label+key replies, read-only when ReadyToBuild). Test: `ConversationViewBuilderTests.cs`.
- [ ] 3.4 Create `ConversationAdvancer.cs`: retry `StartPlanning` on pending transition, compute journey, append reply only if last visible is not the assistant question for the next topic (also re-asks `depth:other` with three levels), closing once. Register in `Application/DependencyInjection.cs`. Test: `ConversationAdvancerTests.cs` (retry without duplicate confirmation, pending reply resume, concurrent resume yields one reply, closing once).
- [ ] 3.5 Create `GetConversation/{Query,QueryHandler}.cs`. Test: `GetConversationTests.cs` (not found for foreign/deleted/unknown, ReadyToBuild read-only, `Started=false`).
- [ ] 3.6 Create `StartConversation/{Command,CommandHandler}.cs`. Test: `StartConversationTests.cs` (first start, second no-op, two tabs, Draft/ReadyToBuild messages, Planning, no current user).
- [ ] 3.7 Create `SendMessage/{Command,CommandHandler,CommandValidator}.cs`: exactly one of text/key, trimmed, 2000 limit, status guard, key must be offered, `confirm:yes` only when confirmation is next ("Aún faltan preguntas por responder antes de pasar a Planificar." on stale), `ExpectedVersion` stale rejected, typed whole-message "no sé/no se/ni idea/no lo sé" (case/accent/punctuation-insensitive) mapped to `AnswerKind.Unknown` on Question topics only, free text on a choice topic stored and re-asked, depth acceptance writes `SetInitiativeDepthCommand` and marks `AppliedToInitiative`, confirmation saves first then `StartPlanningCommand`, `ToString` redacts `Text`. Test: `SendMessageTests.cs` and `SendMessageValidatorTests.cs` (2000 ok / 2001 fail / whitespace / unknown key / free text beside replies / No sé button vs typed / depth accept gives Manual / stale confirmation / confirmation order and retry / service failure leaves pending then resume / stale version / owner isolation / log has no text).
- [ ] 3.8 Create `UndoLastAnswer/{Command,CommandHandler}.cs` (version check, eligibility, no reply requested). Test: `UndoLastAnswerTests.cs` (other user, Draft/ReadyToBuild, refusal messages, pending-reply undo).
- [ ] 3.9 Create `Infrastructure/Assistant/FakeAssistantService.cs` (pure; acknowledgements per design; `SuggestedDepth` from `size`; no clock/random). Test: `Infrastructure.Tests/Assistant/FakeAssistantServiceTests.cs` (determinism, only next topic asked, no praise).
- [ ] 3.10 Create test doubles `TestDoubles/{InMemoryConversationRepository,ScriptedAssistantService,InitiativesSender}.cs` (deep detached copies + version check; real Initiatives handlers).

## Commit 4: Persistence and boundary (~450 + generated)

- [ ] 4.1 Create `Infrastructure/Assistant/{ConversationConfiguration,MessageConfiguration}.cs` (`asi_` prefix, unique InitiativeId, unique (ConversationId, Sequence), `Version` concurrency token, string enums, JSON `QuickReplies` with converter + `ValueComparer`, no FK to `ini_`/AspNet). Test: `ApplicationDbContextModelTests.cs` (rename table-list test to `Model_contains_identity_user_tables_the_module_tables_and_no_role_tables`).
- [ ] 4.2 Create `Persistence/DbErrors.cs` (`IsUniqueViolation`) and `Assistant/ConversationRepository.cs` (owner-scoped `GetAsync` with `AsNoTracking`, `SaveAsync` maps `DbUpdateConcurrencyException` and duplicate key to `ConflictException`). Test: model test for JSON round trip; repository covered by Chrome run.
- [ ] 4.3 Add migration `*_AddAssistantConversations` and snapshot; register in `Infrastructure/DependencyInjection.cs`. Test: `PendingMigrationsGuardTests.cs` plus migration-present test.
- [ ] 4.4 Create `ArchitectureTests/ModuleBoundaryTests.cs` (data-driven `Module(Name, Folders, PrivateTokens)`, both directions, at least one file per module). Test: itself, plus a negative-case helper test proving a forbidden token fails.

## Commit 5: Chat UI (~600)

- [ ] 5.1 Create `Web/Components/Assistant/AssistantLabels.cs` (step names, progress "{covered} de {total} temas cubiertos", remaining text, card text/action, role prefix). Test: `Web.Tests/Assistant/AssistantLabelsTests.cs`.
- [ ] 5.2 `UserMessages.cs`: map `ConflictException` and `AssistantUnavailableException` (spec wording). Test: `UserMessagesTests.cs`.
- [ ] 5.3 Create `wwwroot/js/chat.js` (`scrollToEnd`, reduced motion).
- [ ] 5.4 Create `Components/Assistant/{ChatMessageList,ChatBubble,QuickReplies,ChatComposer,JourneyPanel,DemoDataNotice}.razor` per design (roles/labels, sr-only prefixes, 2000 counter, Ctrl+Enter, hint about editing level). `DemoDataNotice` shows the full note, non-dismissible. Test: Chrome checklist.
- [ ] 5.5 Create `Pages/Assistant/InitiativeAssistant.razor` (`[Authorize]`, query on parameters set, `StartConversation` on first interactive render when Clarifying/Planning, `busy` flag, UI send queue drained in order, Undo disabled while queue non-empty, "Reintentar" on unavailable, stale-version conflict reloads view but keeps the typed text, ReadyToBuild read-only, no fast-mode control). Test: Chrome checklist.

## Commit 6: Card and docs (~100)

- [ ] 6.1 Create `Components/Assistant/ConversationCard.razor`; replace the placeholder in `Pages/Initiatives/InitiativeDetail.razor` (Draft text, "Abrir asistente", progress + "Continuar conversación", ReadyToBuild "Ver conversación"). Test: `AssistantLabelsTests.cs` (card text/action per status) and Chrome.
- [ ] 6.2 `README.md`: demo-data privacy section (AGENTS 4.8).
- [ ] 6.3 Final gate: `dotnet build`, `dotnet test` (all projects), then the Chrome checklist with docker MySQL 8.4, migrations, seed user, review `Logs/`, tear down.

## Manual Chrome checklist (chat flow)

- [ ] 1 Unauthenticated `/iniciativas/{id}/asistente` redirects to login.
- [ ] 2 Draft initiative: card says to finish creating; no action.
- [ ] 3 Clarifying, "Abrir asistente": opening question (idea) with example, reason, "No sé" reply; privacy note visible and not dismissible.
- [ ] 4 Journey panel: Aclarar / Planificar / Lista para construir, current phase marked, progress text; Small omits Planificar; no fast-mode control.
- [ ] 5 Answer with text and with a quick reply; one question per turn; textarea refocused; 2000 counter; Ctrl+Enter sends.
- [ ] 6 Click "No sé": topic covered, not re-asked. Type "no sé" alone: same behavior.
- [ ] 7 Double click Enviar: one answer. Send twice while "El asistente está escribiendo…": second is queued and sent in order; Undo disabled during the queue.
- [ ] 8 Undo repeatedly until "No hay ninguna respuesta que deshacer."; announced in the status region.
- [ ] 9 Leave midway and return: history, panel and last question intact, no duplicate opening question.
- [ ] 10 Automatic with no level: size question, suggestion, "Elegir otro nivel" shows three levels, choice gives Manual + level (detail shows Manual); accepted level not undoable.
- [ ] 11 Edit level in Clarifying, return: history kept, progress and next question reflect the new level.
- [ ] 12 Confirmation: "Quiero añadir algo" keeps Clarifying and re-offers; "Sí, pasar a Planificar" moves to Planning, level locked, not undoable; Small shows "lista para redactar".
- [ ] 13 Two tabs: answer in tab A, then in stale tab B: conflict message, typed text kept.
- [ ] 14 Detail card per status (Clarifying with/without conversation, Planning, ReadyToBuild read-only).
- [ ] 15 Screen-reader roles: log, status, group "Respuestas rápidas", labelled textarea.
- [ ] 16 `Logs/` contains no message text.

## Follow-ups: spec/design lines to adjust (not edited)

Spec (`specs/assistant-conversation/spec.md`):
- L77-81 Small row/scenarios keep `capabilities` (OK); L78 sizing position matches. L171 and L173-176: total "includes confirmation" and "2 de 9" conflict with confirmed counts; make it "2 de 8".
- L164-167 scenario "Typed text is a normal answer" and L492 assumption contradict the confirmed decision (typed whole-message "no sé" equals the button); rewrite.
- L487 assumption says constraints was left out of Small: consistent with reconciled lists; keep.
- L489 "proposals" for messages: freeze wording (design differs, see below).

Design (`design.md`):
- L70, L68 script table: Small `constraints` becomes `capabilities`; Large `integrations` moves from Clarify to Plan (L70), `qualities` stays Plan.
- L56 depth-proposal replies: "Sí, usar el nivel X" + "Elegir otro nivel", then three levels (not "Prefiero Y/Z").
- L33, L117, L136, L137: message texts differ from spec verbatim: undo ("Esta respuesta ya cambió la iniciativa y no se puede deshacer." vs spec L195; "No hay ninguna respuesta para deshacer." vs "...que deshacer."), conflict (spec L417 "La conversación cambió en otra pestaña. Recargue la página."), unavailable (spec L383 "Su mensaje quedó guardado; inténtelo de nuevo en unos minutos."), Draft "abrir" vs "conversar", validator messages (spec L115 incl. "2000" without separator, vs design "2.000"). Also add missing messages: incomplete confirmation, pending reply, empty answer, invalid quick reply, ReadyToBuild read-only.
- L41, spec/initiatives L9: StartPlanning messages say "Planificando"; design says "Planificar". Keep spec.
- L11 (decision 5) and L117: add typed-"no sé" normalization, stale `ExpectedVersion` message, UI send queue (L108 already) and Undo-disabled rule.
- L75 `JourneySnapshot` progress: specify overall `{covered} de {total}` in addition to per-phase progress.
- L217 "Open Questions": decision 11 (repeated undo) is confirmed; close it.

Config: `openspec/config.yaml` L36 `delivery_strategy single-pr` conflicts with `ask-on-risk`.
