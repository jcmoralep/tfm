# Design: Assistant conversation (PROPUESTA-MVP section 7 step 3)

## Technical Approach

A second data module, `Assistant`, built the same way as `Initiatives`. There is one aggregate, `Conversation`, which holds its `Message`s. The script is pure C# data in Application. A pure `ConversationJourney` derives progress and the next topic from the initiative's state plus the visible answers. A shared `ConversationAdvancer` runs the "apply effect, then produce the assistant turn" step for both `StartConversation` and `SendMessage`. Status and depth changes stay in Initiatives and are reached only through MediatR (`StartPlanningCommand`, `SetInitiativeDepthCommand`, `GetInitiativeQuery`). The design traces RF-10, RF-13, RF-28, RF-29, RF-34, RF-35, RF-41, RF-42 and RF-45 to RF-50.

Deviation from the proposal: `Conversation` has **no stored Phase**. The phase is derived from the initiative status and coverage, so it can never disagree with `ini_initiatives` after a write that only half completed.

## Architecture Decisions

| # | Topic | Options and tradeoff | Decision |
|---|---|---|---|
| 1 | Aggregate | Messages as a separate aggregate: concurrency is harder / inside Conversation: the whole history is loaded each time (small) | **`Conversation` root owns `Message`s**. Loaded whole, saved through one port |
| 2 | Undo storage | Delete rows: removes the text, but `Sequence` is reused and there is no audit / soft flag: undone text stays in MySQL | **Soft `UndoneAt`** on the message. `Sequence` stays monotonic (next = count of all messages + 1). There is **no** global query filter, because the aggregate needs every row to compute the next sequence. Read paths drop rows where `UndoneAt` is set |
| 3 | Concurrency (two tabs, undo vs send) | Unique (ConversationId, Sequence) only: an undo is an UPDATE and is not caught / plus a version | **`Conversation.Version` concurrency token**, incremented by every mutator. Commands carry `ExpectedVersion`, checked before mutating (stale tab) and as the EF original value on save (race). The unique sequence index remains as a second guard. Both map to `ConflictException` |
| 4 | Decision topics | Covered by a message in history: after the user edits the level back to Automatic, an old acceptance would skip the proposal / derived from state | **Derived from state.** `depth-proposal` is required only while Automatic with no level, and history never covers it. `confirm-planning` is required while the status is Clarifying, and the status (not history) closes it |
| 5 | Non-atomic confirmation | StartPlanning first: the level locks with no recorded consent / conversation first | **Conversation first**: the "Sí" message is saved with `AppliedToInitiative = true`, then `StartPlanningCommand` runs, which is idempotent. On resume, a last visible answer `confirm:yes` while the status is still Clarifying triggers a retry. A failed `SetInitiativeDepth` is not retried: the proposal is required again, so it is asked again |
| 6 | Quick replies | Re-derived from the script: labels can drift, and step 5 cannot add model suggestions / JSON snapshot | **JSON column** on assistant messages (snapshot of what the user saw). The client sends only the **key**. The server reads the label from that snapshot |
| 7 | Script location | resx/JSON resource: no compile-time keys and needs a loader / C# static data | **`AssistantScript` static C# data** in Application, Spanish, "usted" register (same as the existing UI) |
| 8 | Command results | Command returns void and the page runs a second query / command returns the view | **Commands return `ConversationView`**, like `SaveInitiativeDetails` returns a Guid. This saves a round trip and gives one projection (`ConversationViewBuilder`) |
| 9 | "No sé" | Bool flag / reserved key / enum | **`AnswerKind { FreeText, QuickReply, Unknown }`** stored as a string on user messages. Step 4 filters `Unknown` to create open questions. The topic counts as covered (RF-41) |
| 10 | Boundary test | Reflection on signatures: misses method bodies / NetArchTest: new package / source scan | **Source scan**, data-driven per module (see below) |
| 11 | Undo depth | Single level / repeated | **Repeated**: each undo hides the then-last visible answer. It stops at an applied answer or at the opening question. No extra state is needed. *Must match the spec* |

Rejected: storing `Phase`; using a Gemini-style "model decides the next topic" (Application owns the flow, as recommended in the exploration).

## Domain (`Domain/Assistant`)

- `Conversation`: `Id`, `InitiativeId`, `OwnerId`, `Version`, `CreatedAt`, `UpdatedAt`, `Messages` (backing field `_messages`, ordered by `Sequence` in code).
  - `Start(initiativeId, ownerId, now)`
  - `AddAssistantMessage(topicKey, content, quickReplies, now)`
  - `AddUserAnswer(topicKey, content, kind, quickReplyKey, appliesToInitiative, now)`. Guard: the last visible message must be from the assistant. Content must be non-blank and at most `UserAnswerMaxLength = 2000`.
  - `UndoLastAnswer(now)` hides the last visible user message and every visible message after it. It throws `DomainException` "No hay ninguna respuesta para deshacer." or "Esta respuesta ya cambió la iniciativa y no se puede deshacer."
  - Read members: `VisibleMessages`, `LastVisible`, `CanUndo`.
  - Every mutator sets `UpdatedAt` and increments `Version`.
- `Message`: `Id`, `ConversationId`, `Sequence`, `Role` (User or Assistant), `Content` (at most `ContentMaxLength = 4000`), `TopicKey` (at most 50), `QuickReplies` (`IReadOnlyList<QuickReply>`, assistant messages only), `QuickReplyKey?`, `AnswerKind?`, `AppliedToInitiative`, `UndoneAt?`, `CreatedAt`.
- `QuickReply(string Key, string Label)` is a record.
- Initiatives: `Initiative.StartPlanning(now)`.
  - Already in Planning: no-op (idempotent).
  - Not Clarifying: `DomainException` "Solo se puede pasar a Planificar desde Aclarando."
  - No level: `DomainException` "Elija un nivel de profundidad antes de pasar a Planificar."

## Script and journey (`Application/Features/Assistant/Script`)

`AssistantTopic(Key, Kind, Prompt, Example?, Why, QuickReplies)` with `TopicKind { Question, Choice, DepthProposal, Confirmation, Closing }`. Every Question and Choice topic ends with `unknown` / "No sé". Phase membership is defined **per level**, so one key can be a Clarify topic in Small and a Plan topic in Standard.

| Key | Kind | Prompt (abridged) | Quick replies (besides "No sé") |
|---|---|---|---|
| idea | Question | "Cuénteme la idea con sus palabras: ¿qué quiere lograr o cambiar?" | none |
| users | Question | "¿Quién va a usar esto?" | Clientes · Personal interno · Ambos |
| problem | Question | "¿Qué problema resuelve y por qué hace falta ahora?" | none |
| success | Question | "¿Cómo sabremos que funcionó?" | none |
| out-of-scope | Question | "¿Qué queda fuera, al menos por ahora?" | Nada por ahora |
| integrations | Question | "¿Qué otros sistemas o equipos participan?" | Ninguno |
| size | Choice | "¿Qué tan grande le parece?" | Un ajuste pequeño · Una funcionalidad completa · Un producto o varias áreas |
| depth-proposal | DepthProposal | phrased by the service with the suggestion | Sí, usar el nivel X · Prefiero Y · Prefiero Z (keys `depth:{level}`) |
| confirm-planning | Confirmation | "Ya tenemos lo necesario… Al pasar a Planificar el nivel queda fijo." | Sí, pasar a Planificar (`confirm:yes`) · Quiero añadir algo (`confirm:add`) |
| capabilities | Question | "¿Qué debe poder hacer una persona con esto?" | none |
| constraints | Question | "¿Hay límites o reglas que debamos respetar?" | Ninguno que yo sepa |
| priorities | Question | "Si solo pudiéramos hacer dos cosas, ¿cuáles serían?" | none |
| qualities | Question | "¿Necesita algo especial de velocidad o seguridad?" | Nada especial |
| closing | Closing | "Listo: tenemos lo necesario para redactar {entregables}…" | none |

Every topic also has an example ("Por ejemplo: …") and a reason ("Por qué lo pregunto: …"), with no jargon (RF-41).

| Level | Clarify (in order) | Plan |
|---|---|---|
| Small (6 questions) | idea, users, problem, constraints, out-of-scope, success, confirm | none, so it goes straight to closing |
| Standard (8) | idea, users, problem, success, out-of-scope, confirm | capabilities, constraints, priorities |
| Large (10) | Standard plus integrations, confirm | capabilities, constraints, qualities, priorities |
| Automatic, no level | Standard clarify, size, depth-proposal, confirm | unknown ("Se define al elegir el nivel") |

Small covers the three basic questions of RF-42 and never redirects to another level.

`ConversationJourney.Compute(JourneyInput(status, mode, depth), visibleAnswers)` returns `JourneySnapshot(Step, NextTopic?, Clarify: PhaseProgress, Plan: PhaseProgress?, PendingTransition)`.

- Coverage: a Question or Choice topic is covered by any visible answer with its key.
- Progress counts Question and Choice topics only.
- Changing the level (RF-50) only changes the required list. Answers are kept, and an extra answer stays in history without being required.
- Tests assert:
  - the counts per level
  - unique keys
  - "No sé" present
  - non-empty example and reason
  - a forbidden-jargon list (spine, épica, invariante, slug)
  - level changes in both directions
  - that undone answers are ignored

## Assistant service

```csharp
public interface IAssistantService { Task<AssistantReply> ReplyAsync(AssistantRequest request, CancellationToken ct); }
public sealed record AssistantRequest(InitiativeSnapshot Initiative, AssistantTopic NextTopic, IReadOnlyList<ConversationTurn> History);
public sealed record InitiativeSnapshot(string Name, string? Description, InitiativeDepth? Depth);
public sealed record ConversationTurn(MessageRole Role, string Text, string TopicKey, string? QuickReplyKey, AnswerKind? Kind);
public sealed record AssistantReply(string Text, InitiativeDepth? SuggestedDepth);
```

- The request contains no ids, owner, e-mail or timestamps, and only visible turns.
- Quick replies are composed by Application from the script. For a depth proposal, the suggested level comes first. If `SuggestedDepth` is null, Application uses Standard.
- `FakeAssistantService` (Infrastructure) is pure: no clock, no randomness. Its text is an acknowledgement, then the prompt, then "Por ejemplo: …", then "Por qué lo pregunto: …". The acknowledgement depends on the last user turn:
  - "Anotado."
  - after "No sé": "No pasa nada: lo dejo anotado como pregunta pendiente y seguimos."
  - after a re-ask on a decision topic: "Para seguir, elija una de las opciones."
  - after `confirm:add`: "Claro, escriba lo que quiera añadir."
  - No praise (RF-35).
- `SuggestedDepth` is filled only for `depth-proposal`. It comes from the `size` key: small → Small, feature → Standard, product → Large. Otherwise it is Standard. Level names and deliverables come from `InitiativeTexts`, which this change moves out of `InitiativeLabels` (DRY).
- Queue (confirmed): a send while a reply is in flight is queued in `ConversationView` and drained in order after the previous reply is stored; the domain guard (last visible message must be from the assistant) is unchanged, and Undo is disabled while the queue is not empty.
- Failure: the real implementation throws `AssistantUnavailableException`. The user message is already saved, so the conversation is left with a "reply pending". The UI shows "Reintentar", which sends `StartConversationCommand`.

## Use cases (`Application/Features/Assistant`)

| Request | Result | Rules |
|---|---|---|
| `GetConversationQuery(InitiativeId)` | `ConversationView?` (null when the initiative is not found) | Read only. `Started = false` before the first visit |
| `StartConversationCommand(InitiativeId)` | `ConversationView` | Draft: `DomainException`. ReadyToBuild: view only. Get or create; a duplicate on Add (`ConflictException`) means reload. Then `ConversationAdvancer`. A conflict while advancing means reload and return the view (never surfaced) |
| `SendMessageCommand(InitiativeId, ExpectedVersion, Text?, QuickReplyKey?)` | `ConversationView` | Validator: exactly one of the two ("Escriba una respuesta o elija una opción."), at most 2000 ("La respuesta no puede superar los 2.000 caracteres."), key at most 50. Status must be Clarifying or Planning. The key must be among the last assistant message's quick replies. `confirm:yes` only when confirmation is next. Save, then the effect, then the advancer. `ToString` redacts `Text` |
| `UndoLastAnswerCommand(InitiativeId, ExpectedVersion)` | `ConversationView` | Version check, `UndoLastAnswer`, save |
| `StartPlanningCommand(Id)` (Initiatives) | none | Sent only by the assistant (RF-34). Idempotent |
| `SetInitiativeDepthCommand(Id, Depth)` (Initiatives) | none | `SetDepth(Manual, depth)`, which gives Manual plus the level (RF-47). Validator: `IsInEnum` |

`ConversationAdvancer.AdvanceAsync(conversation, details)` does three things:

1. On a pending transition, it retries `StartPlanningCommand` and reloads the details.
2. It computes the journey.
3. If the last visible message is not an assistant question for `NextTopic`, it calls the service, appends the reply and saves. This also re-syncs after a level change. Closing is appended once.

Notes on the use cases:

- **Port:** `IConversationRepository { GetAsync(initiativeId, ownerId); AddAsync(conversation); SaveAsync(conversation, expectedVersion) }`.
  - Every read takes the owner.
  - `GetAsync` returns all messages, detached and with `AsNoTracking`.
  - `SaveAsync` inserts new messages, copies `UndoneAt`, and throws `NotFoundException` or `ConflictException`.
- **Exceptions:**
  - New `Common/Exceptions/ConflictException`: "La conversación cambió en otra pestaña o ventana. Recargue la página para ver lo último."
  - New `AssistantUnavailableException`: "El asistente no está disponible en este momento. Su respuesta quedó guardada; pulse Reintentar en unos minutos."
  - Both are logged at Warning by `LoggingBehavior` and mapped by `UserMessages`.
  - Status texts live in `AssistantTexts`, for example the Draft message "Termine de crear la iniciativa para conversar con el asistente."
- **`ConversationView`** carries:
  - `InitiativeId`, `InitiativeName`, `Status`, `DepthMode`, `Depth`
  - `Started`, `Version`
  - `Messages` (`Sequence`, `Role`, `Content`, `AnswerKind`)
  - `QuickReplies` (from the last assistant message only)
  - `Journey`
  - `CanSend`, `CanUndo`, `NeedsResume`

## Data Flow

```
Send: Page -(key|text, version)-> SendMessageHandler
   -> GetInitiativeQuery (owner) -> repo.Get(owner) -> version check
   -> AddUserAnswer -> repo.Save(v)                       [write 1: consent recorded]
   -> effect: SetInitiativeDepthCommand | StartPlanningCommand  [write 2, Initiatives]
   -> ConversationAdvancer: Journey -> IAssistantService -> AddAssistantMessage -> repo.Save(v+1)
   -> ConversationView
Resume (first interactive render): StartConversation -> get/create -> Advancer (retry transition, pending reply, re-sync)
```

## Persistence (`Infrastructure/Assistant`)

- **`asi_conversations`**: `Id`, `InitiativeId` (**unique**), `OwnerId` (`varchar(255)`, no FK), `Version` (`IsConcurrencyToken`), `CreatedAt`, `UpdatedAt` (`datetime(6)`).
- **`asi_messages`**: `Id`, `ConversationId` (FK to `asi_conversations` within the module, cascade), `Sequence`, **unique** (`ConversationId`, `Sequence`). Columns:
  - `Role` and `AnswerKind` as `varchar(20)` strings
  - `Content` as `varchar(4000)`
  - `TopicKey` and `QuickReplyKey` as `varchar(50)`
  - `QuickReplies` as `json`, through a `System.Text.Json` converter plus a `ValueComparer`
  - `AppliedToInitiative`, `UndoneAt`, `CreatedAt`
- **Repository:** `ConversationRepository` over `IDbContextFactory`, one context per call.
  - `GetAsync` loads the messages with an `Include` ordered by `Sequence`.
  - `SaveAsync` loads the tracked row by `Id` and `OwnerId`, sets `Version.OriginalValue = expectedVersion`, applies the changes and translates two errors to `ConflictException`: `DbUpdateConcurrencyException`, and `MySqlException` `DuplicateKeyEntry` (through the helper `Persistence/DbErrors.IsUniqueViolation`).
- **Migration:** `AddAssistantConversations`.
- **Model tests:**
  - The table list test is renamed to `Model_contains_identity_user_tables_the_module_tables_and_no_role_tables` and includes the `asi_` tables.
  - The unique indexes.
  - The concurrency token.
  - String enums.
  - The `json` column plus a converter round trip.
  - No FK to `ini_` or `AspNet`.
  - The migration is present.
  - The existing prefix rule already covers `asi_`.

## Module boundary test (`ArchitectureTests/ModuleBoundaryTests.cs`)

It scans `.cs` files under `Domain/{Module}`, `Application/Features/{Module}` and `Infrastructure/{Module}`, with `//` comments stripped. The table is data-driven, `Module(Name, Folders, PrivateTokens)`:

- Initiatives private tokens: `\bInitiative\b`, `\bIInitiativeRepository\b`, `Infrastructure\.Initiatives`, `\bini_`.
- Assistant private tokens: `\bConversation\b`, `\bIConversationRepository\b`, `Infrastructure\.Assistant`, `\basi_`.

For every pair of modules A and B, A's files must not match B's tokens. Allowed public surface: MediatR requests, DTOs, `InitiativeTexts` and the enums. Each module must yield at least one file, so the test cannot pass vacuously after a rename. Web is excluded, because it composes the modules.

## Web

- **Page** `Pages/Assistant/InitiativeAssistant.razor`, route `/iniciativas/{Id:guid}/asistente`, `[Authorize]`, injects `IMediator` and `NavigationManager`.
  - `OnParametersSetAsync` runs `GetConversationQuery`. It also runs during prerender, so the history shows immediately.
  - `OnAfterRenderAsync(firstRender)` runs `StartConversationCommand`, but only when the status is Clarifying or Planning.
  - Controls stay disabled until `RendererInfo.IsInteractive`.
  - A `busy` flag blocks double submits.
  - Errors use `catch … when (UserMessages.TryGet)`. A conflict or an unavailable assistant reloads the view through the query.
- **Components** in `Components/Assistant/`:
  - `ChatMessageList`: `role="log"` and `aria-label="Conversación con el asistente"`. It imports `wwwroot/js/chat.js` (`scrollToEnd`, honours reduced motion) and disposes it, catching `JSDisconnectedException`.
  - `ChatBubble`: an sr-only prefix "Asistente:" or "Usted:", `whitespace-pre-line`, tokens `bg-bubble` and `bg-primary`.
  - `QuickReplies`: `role="group"`, `aria-label="Respuestas rápidas"`, outline `border-primary` pills.
  - `ChatComposer`: a labelled textarea "Su respuesta" with `maxlength=2000`, `@bind:event="oninput"`, a "0 / 2.000" counter, "Enviar", and Ctrl+Enter to send through `@onkeydown`. `aria-describedby` points to the note.
  - `JourneyPanel`: an `<ol>` with `aria-current="step"`, "3 de 5 preguntas · Faltan 2", the deliverables, and in Clarifying the hint "Puede cambiar el nivel desde Editar sin perder lo respondido".
  - `DemoDataNotice`.
  - `ConversationCard`.
- **Page markup:**
  - An undo button "Deshacer mi última respuesta" with the result announced in a `role="status"` region ("Se deshizo su última respuesta.").
  - "El asistente está escribiendo…" as `role="status"`.
  - Focus returns to the textarea after a send, a quick reply or an undo.
- **Privacy note:** persistent and not dismissible, at the top of the chat card: "Modo de demostración: escriba solo datos de ejemplo, no información real de la empresa ni datos personales." A matching section goes in the README (AGENTS 4.8). The configuration key arrives with Gemini in step 5.
- **Detail card** (it replaces the placeholder and reuses `GetConversationQuery`):
  - Draft: "Termine de crear la iniciativa para conversar con el asistente."
  - Clarifying or Planning: progress, plus "Abrir asistente" or "Continuar conversación".
  - ReadyToBuild: "Ver conversación".
- **Thin pages:** `AssistantLabels` (pure C#: step names, progress and remaining text, card text and action, role prefix) is tested in `Web.Tests`. `UserMessages` gains `ConflictException` and `AssistantUnavailableException`.

## File Changes (by commit)

| # | Create / Modify |
|---|---|
| 1 Initiatives (~250) | M `Domain/Initiatives/Initiative.cs` (`StartPlanning`); C `Application/Features/Initiatives/StartPlanning/{Command,Handler}.cs`, `SetInitiativeDepth/{Command,Handler,Validator}.cs`; M `InitiativeTexts.cs` (level names, deliverables), `Web/Components/Initiatives/InitiativeLabels.cs` (delegates); tests `Domain/InitiativeTests.cs`, `Features/Initiatives/InitiativeTransitionTests.cs` |
| 2 Domain + script (~550) | C `Domain/Assistant/{Conversation,Message,QuickReply,MessageRole,AnswerKind}.cs`; `Application/Features/Assistant/Script/{AssistantTopic,TopicKind,AssistantScript,ConversationJourney,JourneySnapshot}.cs`; tests `Domain/ConversationTests.cs`, `Features/Assistant/{AssistantScriptTests,ConversationJourneyTests}.cs` |
| 3 Use cases + fake (~900) | C `Application/Common/Exceptions/{ConflictException,AssistantUnavailableException}.cs`; `Features/Assistant/{IConversationRepository,IAssistantService,AssistantContracts,AssistantTexts,ConversationView,ConversationViewBuilder,ConversationAdvancer}.cs`; folders `GetConversation`, `StartConversation`, `SendMessage`, `UndoLastAnswer`; `Infrastructure/Assistant/FakeAssistantService.cs`; M `Application/DependencyInjection.cs` (advancer), `LoggingBehavior.cs`; tests: handler tests and `TestDoubles/{InMemoryConversationRepository (deep detached copies plus a version check), ScriptedAssistantService, InitiativesSender}`, `Infrastructure.Tests/Assistant/FakeAssistantServiceTests.cs` |
| 4 Persistence (~450 plus generated) | C `Infrastructure/Assistant/{ConversationConfiguration,MessageConfiguration,ConversationRepository}.cs`, `Persistence/DbErrors.cs`, migration `*_AddAssistantConversations`; M `Infrastructure/DependencyInjection.cs`, the snapshot; tests `ApplicationDbContextModelTests.cs`, `ArchitectureTests/ModuleBoundaryTests.cs` |
| 5 Chat UI (~600) | C `Web/Components/Pages/Assistant/InitiativeAssistant.razor`, `Web/Components/Assistant/*.razor` (above), `AssistantLabels.cs`, `wwwroot/js/chat.js`; M `UserMessages.cs`, `_Imports.razor`; tests `Web.Tests/Assistant/AssistantLabelsTests.cs`, `UserMessagesTests.cs` |
| 6 Card + docs (~100) | C `Components/Assistant/ConversationCard.razor`; M `InitiativeDetail.razor`, `README.md` (privacy note) |

Note on commit 3: handlers call Initiatives through `ISender`. The tests use a hand-written `ISender` double (`InitiativesSender`) that delegates to the real Initiatives handlers over `InMemoryInitiativeRepository`, so the cross-module flow is real.

## Testing Strategy

| Layer | What | Approach |
|---|---|---|
| Domain | Sequence, undo (chain, applied, empty), the pending guard, versioning, `StartPlanning` | xUnit, fixed time |
| Application | Script data, journey per level and level change, the four handlers (owner isolation, stale version leads to `ConflictException`, confirmation order and retry, depth acceptance gives Manual, service failure leaves a pending reply then resume, ReadyToBuild read-only) | Hand-written doubles with detached copies. A mutation check per persistence call |
| Infrastructure | Model, indexes, token, json, migration; fake determinism | Model tests without a database |
| Architecture | Module boundary | Source scan |
| Web | `AssistantLabels`, `UserMessages` | Pure C#. No bUnit |

## Threat Matrix

N/A: there is no routing, shell, subprocess, VCS/PR automation, executable-file classification or process-integration boundary.

## Migration / Rollout

The migration `AddAssistantConversations` is additive. Rollback: `dotnet ef database update AddInitiatives`.

## Open Questions

- [ ] Repeated undo (decision 11) must match the spec; the alternative is a single undo until the next answer.
- [ ] Undone text stays in MySQL (soft hide). This is acceptable with demo data; revisit before real data (step 5).
