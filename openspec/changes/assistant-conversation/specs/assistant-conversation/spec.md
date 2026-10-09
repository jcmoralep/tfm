# Assistant Conversation Specification

## Purpose

A guided, persisted, per-initiative conversation in which a deterministic demo assistant asks one question at a time, shows the journey and progress, and moves the initiative from Clarifying ("Aclarando") to Planning ("Planificando") only after the user explicitly confirms. This is a new capability (full spec). Source requirements: RF-10, RF-13, RF-15, RF-26, RF-34, RF-35, RF-41, RF-45 to RF-50.

Scenario tags: **[UNIT]** xUnit at domain, validator, handler or pure-class level (hand-written doubles, no bUnit). **[UI]** manual-only check in Chrome.

Terminology: initiative statuses and depth levels as in the `initiatives` spec (Small "Pequeña", Standard "Estándar", Large "Grande"; mode Manual or Automatic). A **topic** is one item of the script; a **question topic** takes free text or a quick reply, a **choice topic** (sizing, depth proposal, confirmation) is covered only by a quick reply. The **required set** of a conversation is the ordered topic list for the initiative's current mode and depth (see "Script per level"). **Visible** means not undone. Topic names below are descriptive; the key strings are a design choice.

## Requirements

### Requirement: Access, ownership and eligibility (RF-26, RF-34)

Every conversation use case (start, send, undo, read) MUST resolve the initiative through the current user. For an initiative owned by another user, deleted, or unknown, the result MUST be the same not-found result as the initiatives module gives. Start, send and undo MUST succeed only while the initiative is Clarifying or Planning. For Draft they MUST be rejected with "Termine de crear la iniciativa para abrir el asistente." For ReadyToBuild they MUST be rejected with "La iniciativa ya está lista para construir; la conversación es solo de lectura." Reading an existing conversation of a ReadyToBuild initiative MAY be allowed read-only. A conversation MUST belong to exactly one initiative and its owner MUST be the initiative owner, never taken from request input.

#### Scenario: Owner opens a Clarifying initiative [UNIT]
- GIVEN user A owns a Clarifying initiative
- WHEN A starts the conversation
- THEN the conversation is returned

#### Scenario: Foreign, deleted or unknown initiative reads as not found [UNIT]
- GIVEN an initiative owned by A, a deleted initiative of A, and a random id
- WHEN B starts, sends, undoes and reads on the first, and A does so on the other two
- THEN every call returns the same not-found result and nothing is persisted

#### Scenario: Draft cannot start or continue [UNIT]
- GIVEN an owned Draft initiative
- WHEN the owner starts or sends
- THEN both are rejected with "Termine de crear la iniciativa para abrir el asistente." and no conversation or message is stored

#### Scenario: ReadyToBuild cannot start or continue [UNIT]
- GIVEN an owned ReadyToBuild initiative with or without a conversation
- WHEN the owner starts, sends or undoes
- THEN each is rejected with "La iniciativa ya está lista para construir; la conversación es solo de lectura." and nothing changes

#### Scenario: Planning initiative can continue [UNIT]
- GIVEN an owned Planning initiative with a conversation
- WHEN the owner sends an answer
- THEN the answer is accepted

### Requirement: Start and resume (RF-13, RF-35)

`StartConversation` MUST be idempotent: the first call MUST create the conversation and persist the opening question as an assistant message; any further call MUST return the existing conversation without adding an opening question. Exactly one conversation MUST exist per initiative, even when two tabs or a prerender pass start it at once. Resuming MUST return the full visible history in order, the current progress, and the last question with its quick replies. Every user and assistant message MUST be persisted with its order, role and content. The opening question MUST be the first topic of the required set (the idea) and MUST include an example.

#### Scenario: First start creates opening question [UNIT]
- GIVEN an owned Clarifying initiative without a conversation
- WHEN the owner starts
- THEN one conversation exists with exactly one assistant message, the idea question, with an example and quick replies

#### Scenario: Second start is a no-op [UNIT]
- GIVEN a started conversation
- WHEN start is called again
- THEN the same conversation is returned and the message count is unchanged

#### Scenario: Two tabs start together [UNIT]
- GIVEN no conversation and two start calls issued concurrently
- WHEN both complete
- THEN exactly one conversation and one opening question exist and neither call fails with a raw error

#### Scenario: Resume restores history and position [UNIT]
- GIVEN a conversation with three answered topics
- WHEN the owner reads or starts it again
- THEN all messages are returned in order with the progress and the pending question's quick replies

#### Scenario: Chat page flow [UI]
- GIVEN the owner leaves /iniciativas/{id}/asistente midway
- WHEN they return
- THEN the history, journey panel and last question are shown as left, without a duplicated opening question

### Requirement: Script per level (RF-35, RF-46, RF-42)

The script MUST be data of the platform (BMAD-inspired, not BMAD-verbatim). The required set MUST be computed from the initiative's current mode and depth. The next topic MUST be the first topic, in the order below, that is in the required set and not covered. Order:

| Level | Aclarar | Planificar |
|---|---|---|
| Small | idea, users, problem, capabilities, out of scope, success, confirmation | none |
| Standard | idea, users, problem, success, out of scope, [sizing], confirmation | capabilities, constraints, priorities |
| Large | same Aclarar as Standard | capabilities, constraints, priorities, integrations and teams, non-functional qualities |

The sizing step is in the required set only while mode is Automatic and depth is empty; until then the Aclarar part of the Standard list is used. Counts including the confirmation: Small 7 (about 6), Standard 9 (10 with sizing), Large 11. Each topic MUST be asked on its own turn, with a short plain explanation of why it is asked, an example, and quick replies. Question topics MUST offer "No sé" as a quick reply. No question MUST redirect the user to another level (RF-42): for a thin idea the assistant continues with the next Small topic.

#### Scenario: Small asks six topics, no Planificar [UNIT]
- GIVEN a Small initiative
- WHEN every topic is answered in turn
- THEN the topics asked are idea, users, problem, capabilities, out of scope, success, then the confirmation, and no Planificar topic is ever asked

#### Scenario: Standard order [UNIT]
- GIVEN a Standard (Manual) initiative
- WHEN every topic is answered in turn
- THEN the order is idea, users, problem, success, out of scope, confirmation, then capabilities, constraints, priorities, with no sizing step

#### Scenario: Large order [UNIT]
- GIVEN a Large initiative
- WHEN every topic is answered in turn
- THEN the Planificar part adds integrations and teams, then non-functional qualities after priorities, for 11 topics in total

#### Scenario: One question per turn with example [UNIT]
- GIVEN any topic of any level
- WHEN the assistant turn for it is built
- THEN it asks exactly that topic, contains an example and a short reason, and offers at least one quick reply

#### Scenario: Thin Small idea is not redirected [UNIT]
- GIVEN a Small initiative whose idea answer is "mejorar el reporte de ventas, está feo"
- WHEN the assistant replies
- THEN the next topic is the next Small topic and no text suggests another level

#### Scenario: Sizing only in Automatic without level [UNIT]
- GIVEN initiatives (a) Automatic with empty depth, (b) Manual Standard, (c) Manual Small
- WHEN the required sets are computed
- THEN only (a) contains the sizing step

### Requirement: Answer rules and validation (RF-49, RF-41)

The user MUST always be able to answer with free text, even next to quick replies. A free-text answer MUST be 1 to 2000 characters after trimming surrounding whitespace; the stored text MUST be the trimmed text. A quick-reply answer MUST carry a reply id offered by the current question; the stored content MUST be that reply's label, and free text MAY be absent. All messages MUST be Spanish and plain, without technical jargon. Messages (verbatim):
- Empty or whitespace-only: "La respuesta no puede estar vacía."
- Over 2000 characters: "La respuesta no puede superar los 2000 caracteres."
- Unknown quick reply: "La respuesta rápida no es válida para esta pregunta."

#### Scenario: Exactly 2000 characters accepted [UNIT]
- GIVEN an answer of exactly 2000 characters
- WHEN it is validated
- THEN validation succeeds

#### Scenario: 2001 characters rejected [UNIT]
- GIVEN an answer of 2001 characters
- WHEN it is validated
- THEN validation fails with "La respuesta no puede superar los 2000 caracteres." and nothing is stored

#### Scenario: Length measured after trimming [UNIT]
- GIVEN 2000 characters surrounded by spaces
- WHEN it is validated and sent
- THEN it succeeds and the stored content has no surrounding whitespace

#### Scenario: Empty and whitespace answers [UNIT]
- GIVEN an empty answer, then "     " and no quick reply id
- WHEN each is validated
- THEN each fails with "La respuesta no puede estar vacía."

#### Scenario: Free text beside quick replies [UNIT]
- GIVEN a question that offers quick replies
- WHEN the user sends free text
- THEN it is accepted as the answer to that question

#### Scenario: Free text on a choice topic is re-asked [UNIT]
- GIVEN the current topic is the sizing choice, the depth proposal or the confirmation
- WHEN the user sends free text
- THEN the message is persisted, the topic stays uncovered, the initiative is unchanged, and the assistant re-asks the same topic with its quick replies

#### Scenario: Unknown quick reply id [UNIT]
- GIVEN the current question
- WHEN the user sends a reply id it did not offer
- THEN the request fails with "La respuesta rápida no es válida para esta pregunta." and nothing is stored

### Requirement: "No sé" answers (RF-35)

Choosing the "No sé" quick reply MUST be accepted as a valid answer, MUST cover its topic, and MUST be recorded on the stored user message so it can be told apart from other answers. The assistant MUST NOT re-ask a topic answered with "No sé".

#### Scenario: No sé covers the topic and is marked [UNIT]
- GIVEN a question topic
- WHEN the user chooses "No sé"
- THEN the user message is stored with a don't-know marker, the topic counts as covered and the next topic is asked

#### Scenario: Typed text is a normal answer [UNIT]
- GIVEN a question topic
- WHEN the user types "no sé" as free text
- THEN it is stored as an ordinary answer without the don't-know marker and covers the topic

### Requirement: Derived progress and visible coverage (RF-10, RF-50)

Progress MUST be derived on read from visible messages and MUST NOT depend on stored counters. A topic is covered when a visible user answer exists for it: for question topics any valid answer including "No sé"; for the confirmation only "Sí, pasar a Planificar"; for the sizing step only once the depth is set. "Quiero añadir algo" does not cover the confirmation. Answers to topics outside the current required set MUST be kept but MUST NOT count. The progress value is covered required topics out of required topics, and the visible text MUST be "{covered} de {total} temas cubiertos" (assumption on wording). Total includes the confirmation and, in Planning, the Planificar topics.

#### Scenario: Progress after answers [UNIT]
- GIVEN a Standard conversation with the idea and users answered
- WHEN progress is derived
- THEN it is 2 of 9 and the text is "2 de 9 temas cubiertos"

#### Scenario: No sé counts [UNIT]
- GIVEN a "No sé" answer to the problem topic
- WHEN progress is derived
- THEN the problem topic counts as covered

#### Scenario: Extra answers do not count [UNIT]
- GIVEN a Large conversation with integrations answered, then the level changed to Standard
- WHEN progress is derived
- THEN the integrations answer is retained but excluded from covered and total

#### Scenario: Progress panel [UI]
- GIVEN the chat page
- WHEN it renders
- THEN the journey panel lists Aclarar, Planificar and Lista para construir (Small omits Planificar), marks the current phase and shows the progress text

### Requirement: Undo last answer (RF-49)

The system MUST offer "Deshacer mi última respuesta". Invoking it MUST make the last visible user answer and the assistant reaction that followed it stop being visible, make the question reappear as the current question, and recompute progress. Only the last visible answer MAY be undone. Opening and other assistant messages MUST never be undone alone. An answer that changed the initiative MUST NOT be undone: the confirmation "Sí, pasar a Planificar" and, by assumption, an accepted depth change. Repeated invocation undoes the then-last visible answer, one per call, until none is left or a non-undoable answer is last. Refusals (verbatim): "No se puede deshacer una respuesta que ya cambió el estado de la iniciativa." and, with nothing to undo, "No hay ninguna respuesta que deshacer." Whether undone messages are hidden or deleted is a design decision.

#### Scenario: Undo last answer [UNIT]
- GIVEN answers A1, A2 and A3 with their reactions
- WHEN the user undoes
- THEN A3 and its reaction are not visible, the A3 question is the current one and progress counts A1 and A2 only

#### Scenario: Undo right after the first answer [UNIT]
- GIVEN only the opening question and one answer
- WHEN the user undoes
- THEN the opening question remains as the current question and progress is 0

#### Scenario: Repeated undo walks back one answer each [UNIT]
- GIVEN answers A1 and A2
- WHEN the user undoes twice
- THEN both are hidden in order, and a third call is rejected with "No hay ninguna respuesta que deshacer."

#### Scenario: Undo with nothing to undo [UNIT]
- GIVEN a conversation with only the opening question
- WHEN the user undoes
- THEN it is rejected with "No hay ninguna respuesta que deshacer." and nothing changes

#### Scenario: Confirmation answer cannot be undone [UNIT]
- GIVEN the last answer is "Sí, pasar a Planificar" and the initiative is Planning
- WHEN the user undoes
- THEN it is rejected with "No se puede deshacer una respuesta que ya cambió el estado de la iniciativa." and the status stays Planning

#### Scenario: Answer after the confirmation can be undone, the confirmation cannot [UNIT]
- GIVEN the confirmation then one Planificar answer
- WHEN the user undoes twice
- THEN the first call succeeds and the second is rejected as above

#### Scenario: Accepted depth cannot be undone [UNIT]
- GIVEN the last answer accepted a suggested depth
- WHEN the user undoes
- THEN it is rejected with the same refusal and mode and depth are unchanged

#### Scenario: Undo with a pending reply [UNIT]
- GIVEN the last message is a user answer whose reply failed
- WHEN the user undoes
- THEN that answer is hidden and no reply is requested

#### Scenario: Undo by another user or on a locked state [UNIT]
- GIVEN a conversation of A
- WHEN B undoes, or A undoes on a Draft or ReadyToBuild initiative
- THEN B gets not found and A gets the eligibility rejection

### Requirement: Depth suggestion in Automatic mode (RF-28, RF-47)

When mode is Automatic and depth empty, after the other Aclarar topics (idea, users, problem, success, out of scope) the assistant MUST ask the sizing question with quick replies "Un ajuste pequeño", "Una funcionalidad completa" and "Un producto o varias áreas", then propose a level (Small, Standard, Large respectively) with quick replies to accept it or to choose another level. Accepting MUST switch the initiative to Manual with that level and recompute the required set. Rejecting ("Elegir otro nivel") MUST keep mode Automatic and depth empty, MUST present the three levels as quick replies, and choosing one MUST set Manual with that level. The confirmation MUST NOT be offered while depth is empty.

#### Scenario: Suggestion follows the size answer [UNIT]
- GIVEN Automatic with empty depth and the sizing answer "Una funcionalidad completa"
- WHEN the assistant replies
- THEN the reply suggests Standard (Brief y PRD) with accept and choose-another quick replies

#### Scenario: Accepting switches to Manual [UNIT]
- GIVEN a pending Standard suggestion
- WHEN the user accepts
- THEN the initiative is Manual with depth Standard, the sizing step leaves the required set, and the next topic is the next uncovered one

#### Scenario: Accepting Small adds its missing Aclarar topic [UNIT]
- GIVEN a suggestion of Small accepted before capabilities was answered
- WHEN the next topic is computed
- THEN capabilities is asked before the confirmation

#### Scenario: Rejected suggestion keeps Automatic [UNIT]
- GIVEN a pending suggestion
- WHEN the user chooses "Elegir otro nivel"
- THEN mode stays Automatic, depth stays empty and the three levels are offered

#### Scenario: Choosing another level [UNIT]
- GIVEN the three levels are offered
- WHEN the user chooses Large
- THEN the initiative is Manual with depth Large

#### Scenario: Confirmation withheld without depth [UNIT]
- GIVEN Automatic with empty depth and all other Aclarar topics covered
- WHEN the next topic is computed
- THEN it is the sizing step, not the confirmation

### Requirement: Level change during Aclarando (RF-29, RF-50)

When depth (or mode) is changed on a Clarifying initiative, the conversation MUST NOT restart or lose messages. The required set MUST be recomputed; answered topics MUST stay covered and MUST NOT be asked again; only missing topics of the new level MUST be asked, and topics no longer required MUST stop being required while their answers are retained. The change MUST apply at the next turn without any command on the conversation.

#### Scenario: Larger level adds only missing topics [UNIT]
- GIVEN a Standard conversation with idea, users and problem answered, then depth edited to Large
- WHEN the next topic is computed
- THEN it is success and no answered topic is asked again; Large's extra topics come after priorities

#### Scenario: Smaller level after answering more [UNIT]
- GIVEN a Large conversation with seven topics answered including success, then depth edited to Small
- WHEN progress and next topic are derived
- THEN Planificar-only answers no longer count, any uncovered Small topic (for example capabilities) is asked next, and the history is intact

#### Scenario: Back and forth [UNIT]
- GIVEN depth edited Standard to Small and back to Standard
- WHEN progress is derived
- THEN the covered set equals what it was, with no duplicate or lost answer

#### Scenario: Level change preserves history [UI]
- GIVEN a conversation in progress and the level edited on the edit page
- WHEN the user returns to the chat
- THEN all prior messages are shown, and the progress and next question reflect the new level

### Requirement: Confirmation to plan (RF-34, RF-45)

When all required Aclarar topics are covered, the assistant MUST offer the confirmation with exactly the quick replies "Sí, pasar a Planificar" and "Quiero añadir algo". The initiative MUST move to Planning only after "Sí, pasar a Planificar", never automatically, and the offer MUST NOT expire. "Quiero añadir algo" MUST keep the initiative Clarifying and the conversation open; after the user's next answer the confirmation MUST be offered again. Confirmation MUST be rejected while a required Aclarar topic is uncovered, with "Aún faltan preguntas por responder antes de pasar a Planificar." After confirming, Small MUST show "lista para redactar" and ask nothing more; Standard and Large MUST continue with their Planificar topics. When the final required topic is covered the assistant MUST state that document generation is the next step. The confirmation reply MUST be idempotent: if the confirmation is stored but the initiative is still Clarifying, the next start or resume MUST complete the transition without a second confirmation message.

#### Scenario: Offer once Aclarar is covered [UNIT]
- GIVEN every required Aclarar topic is covered
- WHEN the assistant replies
- THEN it offers exactly "Sí, pasar a Planificar" and "Quiero añadir algo"

#### Scenario: Confirmation moves to Planning and locks [UNIT]
- GIVEN the confirmation is offered on a Standard Clarifying initiative
- WHEN the user chooses "Sí, pasar a Planificar"
- THEN the initiative is Planning, mode and depth are locked, and the next topic is capabilities

#### Scenario: Small ends at ready to draft [UNIT]
- GIVEN a Small initiative confirms
- WHEN the assistant replies
- THEN the initiative is Planning, the reply says "lista para redactar" and no further topic is asked

#### Scenario: Quiero añadir algo keeps conversing [UNIT]
- GIVEN the confirmation is offered
- WHEN the user chooses "Quiero añadir algo" and then sends a free-text addition
- THEN the initiative stays Clarifying, the addition is stored, and the confirmation is offered again

#### Scenario: Never automatic and never expires [UNIT]
- GIVEN the confirmation was offered and the user leaves
- WHEN the conversation is resumed days later (time moves forward in the test)
- THEN the status is still Clarifying and the confirmation is still the current question

#### Scenario: Stale confirmation with new missing topics [UNIT]
- GIVEN the confirmation was offered under Standard and the depth is then edited to Small with capabilities uncovered
- WHEN the user sends "Sí, pasar a Planificar"
- THEN it is rejected with "Aún faltan preguntas por responder antes de pasar a Planificar." and the status stays Clarifying

#### Scenario: Retry when stored but not applied [UNIT]
- GIVEN the confirmation is stored and the initiative is still Clarifying
- WHEN the conversation is started again
- THEN the initiative becomes Planning and the confirmation message is not duplicated

#### Scenario: Planificar complete [UNIT]
- GIVEN a Standard Planning conversation with its last Planificar topic answered
- WHEN the assistant replies
- THEN the reply states that document generation comes next and the status remains Planning

### Requirement: Guided mode only (RF-48)

The assistant MUST offer only the guided, step-by-step mode. The UI MUST NOT show a fast-mode option.

#### Scenario: No fast mode [UI]
- GIVEN the chat page
- WHEN it renders
- THEN no fast-mode control is present

### Requirement: Privacy note

The chat MUST always show the note "Modo demostración: use solo datos de ejemplo". The deterministic fake MUST NOT send data outside the process.

#### Scenario: Note visible [UI]
- GIVEN any state of the chat page, including an empty conversation
- WHEN it renders
- THEN the note is visible

### Requirement: Deterministic fake assistant (RF-15)

`IAssistantService` MUST be implemented for this change by a fake that is a pure function of its request: the same request MUST yield an equal reply (text, quick replies, suggested depth). It MUST NOT use the clock, randomness or I/O. The request MUST carry only text and level data (topic, level, mode, history texts, initiative name and description) and MUST NOT carry ids, owner, email or timestamps. The application, not the service, MUST own progress and the next topic; the service only phrases the turn.

#### Scenario: Same input same output [UNIT]
- GIVEN two equal requests
- WHEN the fake replies to both
- THEN the replies are equal

#### Scenario: Request has no identifiers [UNIT]
- GIVEN the request type
- WHEN its members are inspected
- THEN none is a user id, owner id, email, initiative id or timestamp

#### Scenario: Only the next topic is asked [UNIT]
- GIVEN a request for the users topic
- WHEN the fake replies
- THEN the text asks about users, not another topic

### Requirement: Assistant failure handling (RF-15)

The user message MUST be persisted before the service is called. If the service fails, the user message MUST remain, no assistant message MUST be stored, and the user MUST see "El asistente no está disponible en este momento. Su mensaje quedó guardado; inténtelo de nuevo en unos minutos." A conversation whose last visible message is from the user has a pending reply; the next start or resume MUST produce exactly one reply for it. While a reply is pending, sending another answer MUST be rejected with "El asistente aún debe responder a su mensaje anterior. Vuelva a abrir la conversación para reintentar." (assumption).

#### Scenario: Service fails [UNIT]
- GIVEN a service double that throws the unavailable error
- WHEN the user sends an answer
- THEN the answer is persisted, no reply exists, and the Spanish unavailable message is returned

#### Scenario: Pending reply retried on resume [UNIT]
- GIVEN the last message is a user answer
- WHEN the conversation is started again with a working service
- THEN one assistant reply is added for the next topic

#### Scenario: Concurrent resume produces one reply [UNIT]
- GIVEN a pending reply and two concurrent starts
- WHEN both complete
- THEN exactly one reply is stored

#### Scenario: Send while pending [UNIT]
- GIVEN a pending reply
- WHEN the user sends another answer
- THEN it is rejected with the pending-reply message and nothing is stored

#### Scenario: Confirmation with failing service [UNIT]
- GIVEN the service fails on the confirmation reply
- WHEN the conversation is later resumed with a working service
- THEN the reply is produced and the initiative becomes Planning

### Requirement: Concurrency and double submit

Two answers written for the same turn (two tabs, or a double submit) MUST NOT both be stored. Exactly one MUST succeed; the other MUST fail with "La conversación cambió en otra pestaña. Recargue la página." and persist nothing. The mechanism is a design decision.

#### Scenario: Two answers at once [UNIT]
- GIVEN the same conversation state seen by two senders
- WHEN both send an answer concurrently
- THEN one answer and its reply are stored, the other gets the conflict message, and message order has no gaps or repeats

#### Scenario: Double click on Enviar [UI]
- GIVEN the user clicks send twice quickly
- WHEN the request is in progress
- THEN the control is disabled and one answer appears

### Requirement: Privacy of logs and module boundary

Message content MUST NOT be logged. The Assistant module MUST NOT reference initiative entities, repositories or persistence of the Initiatives module, nor Identity; it MAY use only Initiatives MediatR requests, initiative detail data and shared enums. A test MUST fail when this is violated.

#### Scenario: No content in logs [UNIT]
- GIVEN a send with a distinctive text and a capturing logger double
- WHEN the handler runs
- THEN no log entry contains that text

#### Scenario: Boundary test [UNIT]
- GIVEN the Assistant module sources
- WHEN the boundary check runs
- THEN it passes, and it fails if a forbidden Initiatives or Identity type is referenced

### Requirement: Detail page card

The initiative detail page MUST show a Conversación card in place of the placeholder, with content by status: Draft, "Termine de crear la iniciativa para conversar con el asistente." and no action; Clarifying or Planning without a conversation, a "Abrir asistente" action; with a conversation, the progress text and "Continuar conversación"; ReadyToBuild, read-only history or a no-conversation text with no action to write.

#### Scenario: Card per status [UI]
- GIVEN initiatives in Draft, Clarifying (with and without conversation), Planning and ReadyToBuild
- WHEN each detail page renders
- THEN each card shows the content above

#### Scenario: Action opens the chat [UI]
- GIVEN a Clarifying owned initiative
- WHEN the user clicks "Abrir asistente"
- THEN /iniciativas/{id}/asistente opens with the opening question

### Requirement: Authentication required

Every conversation page and use case MUST require an authenticated user. An unauthenticated request to /iniciativas/{id}/asistente MUST redirect to login. Handlers MUST NOT run without a current user.

#### Scenario: Unauthenticated redirected [UI]
- GIVEN an unauthenticated visitor
- WHEN they open /iniciativas/{id}/asistente
- THEN they are redirected to the login page

#### Scenario: Handler without current user [UNIT]
- GIVEN no current user
- WHEN any conversation handler runs
- THEN it fails and nothing is persisted or returned

### Requirement: Plain Spanish and accessibility (RF-41)

All assistant text, quick replies, labels and errors MUST be Spanish, without technical jargon, and every question MUST include an example. The message list MUST be a live log region, the waiting indicator a status region, quick replies buttons in a labelled group, and the composer textarea labelled.

#### Scenario: Every topic has an example [UNIT]
- GIVEN the full script
- WHEN every topic text is inspected
- THEN each has a non-empty example and reason

#### Scenario: Roles and labels [UI]
- GIVEN the chat page
- WHEN it renders
- THEN the log, status, quick-reply group and composer roles and labels are present

## Assumptions

- Small's six topics are idea, users, problem, capabilities, out of scope, success (the three basic questions of RF-42 are idea/users/problem); the RF-46 list in the exploration included constraints, left out to keep Small at about 6.
- Counts include the confirmation; Small is 7 including it.
- Progress text wording "{covered} de {total} temas cubiertos" and the extra Spanish messages (draft, ready, undo, pending, incomplete confirmation, unavailable, invalid quick reply) are proposals; design may adjust wording but not semantics.
- Repeated undo walks back one answer per call; "single" means one answer per invocation.
- An accepted depth answer is treated as having changed the initiative and is not undoable.
- Only the "No sé" quick reply carries the don't-know marker.
- Sending while a reply is pending is rejected rather than queued.
- Read-only viewing of a ReadyToBuild conversation is optional (MAY).
- When a quick reply is chosen, its label is the stored content.

## Out of Scope

Document generation, artifact index and open-question tracking of "No sé" (step 4); Gemini, fast mode and streaming (step 5); attachments; ReadyToBuild transition; editing or deleting messages beyond the single undo; restarting a conversation; pushing back on weak answers.
