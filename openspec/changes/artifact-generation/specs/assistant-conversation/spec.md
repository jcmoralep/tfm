# Delta for Assistant Conversation

Changes to the `assistant-conversation` capability caused by artifact generation (RF-45, RF-50, RF-53, RF-58). The capability's spec is still in the not-yet-archived change `assistant-conversation` (`openspec/changes/assistant-conversation/specs/assistant-conversation/spec.md`); this delta applies on top of it. Tags: **[UNIT]** xUnit, **[UI]** manual in Chrome. The order of the script, the topics and the confirmation rules do not change.

## ADDED Requirements

### Requirement: Conversation answers query for drafting (RF-35, RF-53, RF-58)

The Assistant module MUST expose a MediatR query for the Artifacts module, restricted to the initiative's owner, that returns a plain DTO (never an entity) with: for each topic of the current required set that has a visible user answer, the topic key, the stored answer text and whether it was recorded with the don't-know marker; whether every required Aclarar topic is covered (the confirmation is not a topic); and whether every required topic of the level, Aclarar and Planificar, is covered (the closing). The DTO MUST carry no message ids, conversation ids, owner id or timestamps. Only visible (not undone) answers MUST be returned, in script order, using the latest visible answer per topic; answers to topics outside the current required set MUST NOT be returned. An initiative without a conversation MUST return no answers and both flags false. For another user's, deleted or unknown initiative the result MUST be the same not-found result as the rest of the module. The query MUST NOT create or change any data, MUST work in every status (including ReadyToBuild and Draft) and MUST NOT log message content.

#### Scenario: Answers in script order [UNIT]
- GIVEN a Standard conversation with users answered before idea was edited by undo and re-answer
- WHEN the query runs
- THEN the answers are returned in script order, one per topic, with the latest visible text

#### Scenario: Undone answers are excluded [UNIT]
- GIVEN the success answer was undone
- WHEN the query runs
- THEN no success answer is returned and the Aclarar flag is false

#### Scenario: Don't-know marker is exposed [UNIT]
- GIVEN the problem topic answered with "No sé" and the constraints topic answered with "ni idea"
- WHEN the query runs
- THEN both topics are returned with the don't-know flag set and no invented text

#### Scenario: Aclarar covered flag [UNIT]
- GIVEN a Standard Clarifying conversation with all Aclarar topics covered and none of Planificar
- WHEN the query runs
- THEN the Aclarar flag is true and the closing flag is false

#### Scenario: Closing flag [UNIT]
- GIVEN a Standard Planning conversation with all eight topics covered
- WHEN the query runs
- THEN both flags are true

#### Scenario: Out-of-set answers are not returned [UNIT]
- GIVEN a Large conversation with integrations answered, then the level changed to Standard
- WHEN the query runs
- THEN the integrations answer is not returned and the stored message is untouched

#### Scenario: No conversation [UNIT]
- GIVEN an owned initiative without a conversation
- WHEN the query runs
- THEN it returns no answers, both flags false and no error

#### Scenario: Other user, deleted or unknown [UNIT]
- GIVEN an initiative of A, a deleted initiative of A, and a random id
- WHEN B queries the first and A queries the other two
- THEN every call returns the same not-found result

#### Scenario: Read does not change data [UNIT]
- GIVEN a conversation with a pending reply
- WHEN the query runs repeatedly
- THEN no message is added and no reply is produced

#### Scenario: No identifiers and no logged content [UNIT]
- GIVEN the DTO type and a capturing logger double
- WHEN its members are inspected and the query runs with a distinctive answer
- THEN none is an id, owner or timestamp and no log entry contains the text

### Requirement: Call to action to the documents (RF-45, RF-53, RF-58)

When documents become available, the chat page MUST offer a way to reach them without changing the conversation: from the point where the required Aclarar topics are covered on a Standard initiative, a link to the Artefactos of the initiative that points at the Brief; at the closing of any level, a link to the document set ("Ir a los documentos") and, when the closing message says document generation comes next, the link next to that message. The call to action MUST NOT generate anything, MUST NOT change the status and MUST NOT alter the order of the script, the progress or the quick replies. In ReadyToBuild, where the conversation is read-only, the link MUST read "Ver documentos". Wording is a proposal.

#### Scenario: Link after Aclarar on Standard [UI]
- GIVEN a Standard Clarifying conversation with all Aclarar topics covered
- WHEN the chat renders
- THEN a link to the Artefactos is shown, the confirmation question and its quick replies are unchanged and no document exists yet

#### Scenario: Link at the closing [UI]
- GIVEN a Small Planning conversation that says "lista para redactar", or a Standard conversation whose last Planificar topic was answered
- WHEN the chat renders
- THEN "Ir a los documentos" is shown next to the closing message and opens the Artefactos of the initiative

#### Scenario: No link before the documents are available [UI]
- GIVEN a Standard conversation with an Aclarar topic uncovered
- WHEN the chat renders
- THEN no link to the documents is shown

#### Scenario: Read-only conversation links to the documents [UI]
- GIVEN a ReadyToBuild initiative with a conversation
- WHEN the chat renders
- THEN the history is read-only, no composer is active and "Ver documentos" is shown

#### Scenario: Link does not alter the script [UNIT]
- GIVEN the same conversation with and without documents generated
- WHEN progress and next topic are derived
- THEN they are equal

## MODIFIED Requirements

### Requirement: Confirmation to plan (RF-34, RF-45)

When all required Aclarar topics are covered, the assistant MUST offer the confirmation with exactly the quick replies "Sí, pasar a Planificar" and "Quiero añadir algo". The initiative MUST move to Planning only after "Sí, pasar a Planificar", never automatically, and the offer MUST NOT expire. "Quiero añadir algo" MUST keep the initiative Clarifying and the conversation open; after the user's next answer the confirmation MUST be offered again. Confirmation MUST be rejected while a required Aclarar topic is uncovered, with "Aún faltan preguntas por responder antes de pasar a Planificar." After confirming, Small MUST show "lista para redactar" and ask nothing more; Standard and Large MUST continue with their Planificar topics. When the final required topic is covered the assistant MUST state that the documents are the next step and point to them (see "Call to action to the documents"); the assistant MUST NOT generate any document itself. The confirmation reply MUST be idempotent: if the confirmation is stored but the initiative is still Clarifying, the next start or resume MUST complete the transition without a second confirmation message. Answers added through "Quiero añadir algo" after a document was generated MUST NOT regenerate or change that document.
(Previously: "the assistant MUST state that document generation is the next step"; the pointer to the documents and the independence from already generated documents are added.)

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

#### Scenario: Brief generated before confirming does not block confirmation [UNIT]
- GIVEN a Standard Clarifying initiative whose Brief was generated
- WHEN the user chooses "Sí, pasar a Planificar"
- THEN the initiative becomes Planning and the Brief is unchanged

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

#### Scenario: Planificar complete points to the documents [UNIT]
- GIVEN a Standard Planning conversation with its last Planificar topic answered
- WHEN the assistant replies
- THEN the reply states that the documents come next, the status remains Planning and no document is generated

### Requirement: Access, ownership and eligibility (RF-26, RF-34)

Every conversation use case (start, send, undo, read) MUST resolve the initiative through the current user. For an initiative owned by another user, deleted, or unknown, the result MUST be the same not-found result as the initiatives module gives. Start, send and undo MUST succeed only while the initiative is Clarifying or Planning. For Draft they MUST be rejected with "Termine de crear la iniciativa para abrir el asistente." For ReadyToBuild they MUST be rejected with "La iniciativa ya está lista para construir; la conversación es solo de lectura." Reading an existing conversation of a ReadyToBuild initiative MAY be allowed read-only. When a ReadyToBuild initiative is reopened to Planning because a document changed, the conversation MUST accept start, send and undo again under the same rules as any Planning initiative. A conversation MUST belong to exactly one initiative and its owner MUST be the initiative owner, never taken from request input.
(Previously: ReadyToBuild was unreachable; the behavior after a reopen to Planning is added.)

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

#### Scenario: Reopened initiative can continue [UNIT]
- GIVEN an initiative that was ReadyToBuild and was reopened to Planning, with a conversation whose topics are all covered
- WHEN the owner reads or starts the conversation
- THEN it is accepted, no new question is asked beyond the closing message, and the status stays Planning

#### Scenario: Planning initiative can continue [UNIT]
- GIVEN an owned Planning initiative with a conversation
- WHEN the owner sends an answer
- THEN the answer is accepted

### Requirement: Detail page card

The initiative detail page MUST show a Conversación card in place of the placeholder, with content by status: Draft, "Termine de crear la iniciativa para conversar con el asistente." and no action; Clarifying or Planning without a conversation, a "Abrir asistente" action; with a conversation, the progress text and "Continuar conversación"; ReadyToBuild, read-only history or a no-conversation text with no action to write. Its content MUST NOT depend on whether documents exist.
(Previously: the independence from documents is added; the card content is unchanged.)

#### Scenario: Card per status [UI]
- GIVEN initiatives in Draft, Clarifying (with and without conversation), Planning and ReadyToBuild
- WHEN each detail page renders
- THEN each card shows the content above

#### Scenario: Action opens the chat [UI]
- GIVEN a Clarifying owned initiative
- WHEN the user clicks "Abrir asistente"
- THEN /iniciativas/{id}/asistente opens with the opening question

## REMOVED Requirements

None.

## Non-requirement sections to update when archiving

- Out of Scope: remove "Document generation, artifact index and open-question tracking of 'No sé' (step 4)" and "ReadyToBuild transition"; keep "Gemini, fast mode and streaming (step 5)", attachments, editing or deleting messages beyond the single undo, restarting a conversation, pushing back on weak answers. The owner and status tracking of open questions remain a follow-up.
- Purpose: the closing now leads to document generation in the `artifacts` capability.
- Privacy of logs and module boundary: the Assistant module still MUST NOT reference the Artifacts module; only Artifacts calls the answers query.

## Assumptions

- The answers query returns the latest visible answer per topic; the don't-know flag follows the stored marker of that answer.
- Both flags are computed by the Assistant module, which owns progress and the required set; the Artifacts module never recomputes them.
- The Brief link appears as soon as the Aclarar flag is true, including while the confirmation is the current question.
- Link wording ("Ir a los documentos", "Ver documentos") is a proposal.
- After a reopen to Planning with all topics already covered, no new question appears; the conversation shows the closing message again.
