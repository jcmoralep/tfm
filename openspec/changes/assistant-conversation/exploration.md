# Exploration: assistant-conversation (PROPUESTA-MVP section 7 step 3)

Condensed from the exploration report (2026-10-08). Source URLs are listed at the end.

## 1. Current state (relevant)

- Initiatives module is in `main` and archived (`openspec/specs/initiatives/spec.md`). `Initiative` has statuses Draft, Clarifying, Planning, ReadyToBuild; `Complete` (Draft to Clarifying) is the only transition; `SetDepth` is guarded to Draft or Clarifying; Automatic forces `Depth = null` (enforced in the domain, `InitiativeValidationRules`, `DepthSelector.razor`, the wizard and the edit page).
- Failure policy: `DomainException`, `NotFoundException`, `ValidationException` are expected failures (logged at Warning, mapped to Spanish by `UserMessages.TryGet`); anything else reaches `AppErrorBoundary`.
- `InitiativeDetail.razor` has a dashed placeholder "Conversación" ("Disponible próximamente").
- `App.razor` uses `InteractiveServer` with prerender, so lifecycle methods run twice.
- No cross-module architecture test exists yet; the initiatives design promised it with the second data module (this one).
- Mockup `docs/design/opcion-c-intercom-azul.png`: left card with the initiative and a numbered step list, "Contexto adjunto" card; right chat card "Asistente BMAD" with caption "Paso 1 de 5 · Brief", gray-blue assistant bubbles, solid blue user bubbles, blue-outline quick-reply pills, composer "Escribe tu respuesta..." and "Enviar".

## 2. BMAD grounding (official docs, checked 2026-10-08)

Confirmed:
- Phases: "Clarify -> Plan -> Build and verify -> Learn and adjust" (matches PROPUESTA section 2).
- Planning paths: small work is Build territory; epic-sized work uses `bmad-spec` (spec file, `tickets.toml`); project-sized uses the full flow (PRD, UX, architecture, one spec per epic).
- Guided elicitation: `bmad-forge-idea` "works one question at a time and includes its own best answer when that helps you respond" (maps to one question per turn plus quick replies).
- Fast vs coaching paths in `bmad-prd` (brain dump first, then either gap-batched draft with `[ASSUMPTION]` tags, or section-by-section coaching): RF-12 is largely confirmed; the docs say Fast and Coaching.
- Five-part spec: Why, Capabilities (intent and success condition), Constraints, Non-goals, Success signal.
- Brief is "one- to two-page"; PRD describes capabilities with stable functional requirement IDs.
- Small change in BMAD (`bmad-build`) is build-first with a written plan, not a formal spec: RF-11 stays an assumption and is partly contradicted.

Not confirmed (the docs do not list them): the actual questions of `bmad-product-brief` and `bmad-prd`; official brief section names; whether "Quick Flow" still exists; that "Épicas e historias" equals `bmad-ticket`/`tickets.toml`. The topic script below is therefore "BMAD-informed", not BMAD-verbatim.

A sandbox validation would still need to confirm: real discovery questions and section names, the `[ASSUMPTION]` format, output paths and `<type>-<slug>/` folders, the `tickets.toml` shape, whether Small maps to `bmad-build` or `bmad-spec`, and whether the installed version matches the website.

## 3. What step 3 delivers and does not

Delivers: from the detail of an owned initiative (Clarifying or Planning) open the assistant at `/iniciativas/{id}/asistente`; the first visit creates the conversation and the opening question; free text or quick replies, each persisted; the fake replies with the next question; a side panel shows the journey (Aclarar, Planificar, Lista para construir) and progress; leaving and returning restores everything; when Aclarar completes the user confirms and the initiative moves to Planning; at the end the UI says document generation comes next; in Automatic mode the assistant suggests a depth and the user accepts it.

Does not deliver: Brief/PRD generation (step 4), Gemini (step 5), attachments (step 7), export, the fast path (RF-12), message edit/delete, the ReadyToBuild transition (RF-07).

## 4. Domain model

- New module `Assistant`, table prefix `asi_`, one `IEntityTypeConfiguration` per entity, no FK or navigation to `ini_initiatives` or Identity (plain `InitiativeId` Guid and `OwnerId` string).
- `Conversation`: Id, InitiativeId (unique), OwnerId, Phase (Clarify, Plan, ReadyForArtifacts), CreatedAt, UpdatedAt.
- `Message`: Id, ConversationId, Sequence (unique with ConversationId), Role (User, Assistant), Content (max 2000 for users), TopicKey (nullable), QuickRepliesJson (nullable, assistant only), CreatedAt. Deviation from PROPUESTA section 6, which attaches Mensaje directly to the initiative: going through Conversation gives phase state a home.
- Progress is derived, not stored: a user message carries the TopicKey it answers; covered topics are the distinct TopicKeys; the phase is complete when the required topic set for the current depth is a subset of the covered set. This stays correct if depth changes during Aclarar.
- The script is pure data in Application (`AssistantScript`: `AssistantTopic(Key, Kind, Phase, Prompt, QuickReplies)`; kinds Question, Choice, DepthProposal, Confirmation, Closing), a topic catalog with per-level subsets. Proposed sets (ours, to validate): Small about 6 (idea, why, capabilities, constraints, out of scope, success signal); Standard about 9 (idea, users, problem, success, out of scope, plus size and depth proposal when depth is empty; then capabilities, constraints, priorities); Large about 11 (adds integrations and teams, non-functional needs). A Confirmation topic closes Aclarar.
- The fake is a pure function of (next topic, history): no clock, no randomness; free text answers a Question; Choice, DepthProposal and Confirmation are covered only by a quick-reply id.
- Status changes: the rule stays in Initiatives: `Initiative.StartPlanning(now)` (requires Clarifying and a non-null depth) and a narrow `StartPlanningCommand`; Assistant calls it through MediatR after the user's explicit confirmation. Two DbContext calls are not atomic: persist the conversation first, make the command idempotent, retry on resume if the confirmation is covered but the initiative is still Clarifying.
- Module boundary: Assistant never uses `Domain.Initiatives.Initiative`, `IInitiativeRepository` or `Infrastructure.Initiatives`; only Initiatives MediatR requests, `InitiativeDetails` and shared enums. Add the architecture test now (source scan of usings in `*/Assistant/*`, about 60 lines, no new package).

## 5. IAssistantService contract (Application)

```csharp
Task<AssistantReply> ReplyAsync(AssistantRequest request, CancellationToken ct);
record AssistantRequest(InitiativeSnapshot Initiative, ConversationPhase Phase, AssistantTopic NextTopic, IReadOnlyList<ConversationTurn> History);
record AssistantReply(string Text, IReadOnlyList<QuickReply> QuickReplies, InitiativeDepth? SuggestedDepth);
```
- Application owns progress, completion and the next topic; the service only phrases the turn (recommended over letting the model decide). A later additive `AlsoCoveredTopicKeys` lets Gemini skip topics already covered (step 5).
- No streaming in step 3 (additive later). Only text is sent to the model (no ids, owner, email, timestamps). A visible chat note: "Modo demostración: use solo datos de ejemplo". Never log message content. The user message is persisted before calling the service; `AssistantUnavailableException` mapped in `UserMessages`; a conversation whose last message is from the user means a reply is pending (the idempotent start command retries).

## 6. Use cases

- `GetConversationQuery(InitiativeId)`; `StartConversationCommand(InitiativeId)` (idempotent get-or-create plus opening question or pending reply); `SendMessageCommand(InitiativeId, Text, QuickReplyId?)` (resolve owner and initiative, require Clarifying or Planning, validate, persist the user message with TopicKey, compute the next topic, call the service, persist the reply, then send `StartPlanningCommand` on the confirmation or `SetInitiativeDepthCommand` on a depth quick reply).
- New Initiatives-side commands: `StartPlanningCommand`, `SetInitiativeDepthCommand(Id, Depth)`.
- `IConversationRepository` takes `ownerId` on every method; inserts only new messages in one transaction; the unique (ConversationId, Sequence) index rejects a concurrent second write and is translated to a Spanish conflict message ("La conversación cambió en otra pestaña. Recargue la página.").
- Journey is a pure `ConversationJourney` (Aclarar, Planificar, Lista para construir plus the deliverables of the depth as captions); labels live in Web. This deviates from the mockup, which lists documents as steps; RF-10 asks for phases and step 3 produces no documents.

## 7. Automatic depth (RF-06, RF-28)

The fake runs a sizing Choice only when Automatic and `Depth` is null ("Un ajuste pequeño", "Una funcionalidad completa", "Un producto o varias áreas"), then a DepthProposal ("Te sugiero el nivel Estándar: Brief y PRD") with quick replies to accept or pick another level. Accepting is a fork: (a) switch the mode to Manual with the chosen level (reuses `SetDepth`, no change to the archived invariant, label reads Manual afterwards) or (b) keep Automatic and allow a depth (touches the domain, validator, `DepthSelector`, wizard and edit page, +60 lines, risk that the edit handler clears the accepted depth). Recommendation: (a).

## 8. UI plan

Dedicated page `/iniciativas/{Id:guid}/asistente`; the detail's Conversación placeholder becomes a card (status, progress, "Abrir asistente" or "Continuar conversación"; a Draft is told to finish the creation wizard; ReadyToBuild shows read-only history). Components: `InitiativeAssistant.razor`, `ChatMessageList`, `ChatBubble`, `QuickReplies`, `ChatComposer`, `JourneyPanel`; reuse tokens (`bg-bubble`, `bg-primary`, `border-primary`). Accessibility: `role="log"` with `aria-live="polite"`, "escribiendo..." as `role="status"`, quick replies as buttons in a named group, a labelled textarea (Enter inserts a newline; Enter-to-send would need JS, later). Blazor Server: inject only `IMediator`, busy flag, prerender runs lifecycle twice so reads go in `OnParametersSetAsync` and `StartConversationCommand` in `OnAfterRenderAsync(firstRender)`; scroll-to-end through a small ES module `wwwroot/js/chat.js`.

## 9. Open product questions (defaults exist for all)

1. Status flow: Clarifying becomes Planning only after an explicit "Sí, pasar a Planificar"; Small also passes through Planning (no Planificar questions) and then shows "listo para redactar"; ReadyToBuild stays with artifact approval (step 4 and later).
2. Script: is the proposed topic set acceptable as a start (Small 6, Standard about 9, Large about 11), labelled "BMAD-informed".
3. Depth suggestion acceptance in Automatic mode: option (a) or (b) above.
4. Quick mode (RF-12): guided path only in step 3; the fast path waits for Gemini.
5. Input rules: free text always allowed next to quick replies; append-only history; 2000 characters; no restart in step 3.
6. Depth changed during Aclarar: allowed through the edit page; progress recalculates; locked from Planning (RF-29).

## 10. Risks and size

Risks: script is our adaptation of BMAD; RF-11 partly contradicted by the docs; non-atomic Clarifying to Planning; prerender double execution; cross-module leak (mitigated by the boundary test); privacy with the free tier in step 5; the Manual-after-suggestion label may surprise users.

Size: about 2,300 to 2,600 hand-written lines (Domain about 190, Application about 600, Infrastructure about 320, Web about 550, tests about 700) plus a generated migration of about 800 lines. Suggested commits: (a) Initiatives `StartPlanning` and `SetInitiativeDepthCommand` (about 200); (b) Assistant domain and application (about 900); (c) Infrastructure, fake service, generated migration `AddAssistantConversations`, model tests, boundary test (about 450 plus generated); (d) Web chat components and page (about 550); (e) detail card, docs and spec. One PR with a size exception or two stacked PRs (backend, then UI).

## Sources

- https://docs.bmad-method.org/
- https://docs.bmad-method.org/plan/choose-a-planning-path/
- https://docs.bmad-method.org/plan/explore-and-validate-an-idea/
- https://docs.bmad-method.org/plan/define-requirements-and-a-specification/
- https://docs.bmad-method.org/plan/design-ux-and-architecture/
- https://docs.bmad-method.org/build/build-a-change/
- https://docs.bmad-method.org/reference/skills-and-agents/
