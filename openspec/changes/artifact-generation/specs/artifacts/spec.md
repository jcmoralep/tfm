# Artifacts Specification

## Purpose

Lets the owner of an initiative generate, read, edit, regenerate and approve the Markdown documents that its depth level requires (Spec, Brief, PRD), with one document per initiative and type, a version number, a Draft/Approved status and format warnings. When the full set required by the level is approved, the initiative becomes ready to build. This is a new capability (full spec). Source requirements: RF-07, RF-16 to RF-20, RF-27, RF-36, RF-38, RF-40, RF-43, RF-44, RF-51 to RF-60, RN-01, RN-03, RN-06, RN-07.

Scenario tags: **[UNIT]** xUnit at domain, validator, handler or pure-class level (hand-written doubles, no bUnit). **[UI]** manual-only check in Chrome.

Terminology: initiative statuses and depth levels as in the `initiatives` spec (Draft "Borrador", Clarifying "Aclarando", Planning "Planificando", ReadyToBuild "Lista para construir"; Small "Pequeña", Standard "Estándar", Large "Grande"). Artifact types in this change: **Spec** ("Especificación"), **Brief**, **PRD**. Architecture and Epics-and-stories exist only in step 6. Artifact status: **Draft** ("Borrador") or **Approved** ("Aprobado"). The **required set** of an initiative is given by its current depth. An artifact is **available** when the conditions of "Availability" hold; **generated** when a row exists. **Closing** means the assistant's required topics of the level are all covered (the conversation reached its "ready to draft" point). Section names below are the Spanish headings of the generated Markdown. Message wording is a proposal unless marked verbatim.

## Requirements

### Requirement: Required set per level (RF-07, RF-27, RF-40, RF-51)

The system MUST define the required set from the initiative's depth as data: Small requires Spec; Standard requires Brief and PRD; Large requires PRD and, once step 6 exists, Architecture and Epics-and-stories. In this change Large's required set is the full level set, so it contains types that cannot yet be generated. Initiatives with empty depth (Automatic, not yet resolved) MUST have an empty required set and no artifact MUST be available. A type outside the required set of the initiative's current level MUST be rejected for generate, regenerate, save and approve.

#### Scenario: Required set per level [UNIT]
- GIVEN the depth levels
- WHEN the required set of each is requested
- THEN Small yields Spec, Standard yields Brief and PRD, Large yields PRD, Architecture and Epics-and-stories

#### Scenario: Catalog agrees with the level deliverables [UNIT]
- GIVEN the required set and the level deliverables of the `initiatives` capability
- WHEN both are compared per level
- THEN they describe the same documents

#### Scenario: Empty depth has no set [UNIT]
- GIVEN an Automatic initiative with empty depth
- WHEN its required set is requested
- THEN it is empty and generating any type is rejected

#### Scenario: Type outside the level is rejected [UNIT]
- GIVEN a Small initiative
- WHEN the owner generates a Brief
- THEN it is rejected with "Este documento no corresponde al nivel de la iniciativa." and nothing is stored

### Requirement: Availability and explicit generation (RF-53, RF-58)

Generation MUST be an explicit user action ("Generar"); nothing MUST be generated automatically, on any page load, on any status change or on conversation progress. Availability MUST be evaluated against the initiative's status and the conversation coverage obtained through the Assistant module, never by reading Assistant data directly:
- Brief (Standard): available as soon as every required Aclarar topic of the level is covered (the confirmation step is not a topic), while the initiative is Clarifying or Planning.
- Spec (Small) and PRD (Standard, Large): available only at the closing, that is, when the initiative is Planning and every required topic of the level, Aclarar and Planificar, is covered.
- In Draft nothing is available. In ReadyToBuild generated documents can be read, edited and regenerated (see their requirements), but no new document is generated because the set was already complete.

Rejections (wording is a proposal): Draft, "Termine de crear la iniciativa para generar documentos."; topics uncovered, "Aún no se puede generar este documento: termine primero las preguntas del asistente."; document already generated, "El documento ya existe. Use Regenerar para reemplazarlo.". The order of the assistant's script MUST NOT change because of generation.

#### Scenario: Brief available after Aclarar while Clarifying [UNIT]
- GIVEN a Standard Clarifying initiative whose Aclarar topics are all covered and whose Planificar topics are not
- WHEN the availability of Brief and PRD is evaluated
- THEN Brief is available and PRD is not

#### Scenario: Brief not available with a missing Aclarar topic [UNIT]
- GIVEN a Standard Clarifying initiative with one Aclarar topic uncovered
- WHEN the owner generates the Brief
- THEN it is rejected with "Aún no se puede generar este documento: termine primero las preguntas del asistente." and nothing is stored

#### Scenario: Spec only at the closing [UNIT]
- GIVEN a Small initiative that is Clarifying with every topic answered, and the same initiative after confirming (Planning)
- WHEN the availability of Spec is evaluated in each state
- THEN it is unavailable while Clarifying and available once Planning

#### Scenario: PRD only after Planificar topics [UNIT]
- GIVEN a Standard Planning initiative with capabilities answered but constraints and priorities uncovered
- WHEN the owner generates the PRD
- THEN it is rejected as not yet available, and becomes available after the last Planificar topic is covered

#### Scenario: Large PRD at the closing [UNIT]
- GIVEN a Large Planning initiative with all ten topics covered
- WHEN the availability of PRD is evaluated
- THEN it is available

#### Scenario: Draft initiative [UNIT]
- GIVEN a Draft initiative
- WHEN the owner generates any document
- THEN it is rejected with "Termine de crear la iniciativa para generar documentos."

#### Scenario: Nothing is generated automatically [UNIT]
- GIVEN every topic covered and the closing reached
- WHEN the conversation and artifact read queries run any number of times
- THEN no artifact row is created

#### Scenario: Generate twice is rejected [UNIT]
- GIVEN a generated Brief
- WHEN the owner generates the Brief again
- THEN it is rejected with "El documento ya existe. Use Regenerar para reemplazarlo." and the stored document is unchanged

#### Scenario: Two tabs generate together [UNIT]
- GIVEN no Brief and two concurrent generate requests
- WHEN both complete
- THEN exactly one document exists (version 1) and the other request fails with the "already exists" message or the conflict message, never a raw error

#### Scenario: Script order unchanged [UNIT]
- GIVEN a Standard conversation with the Brief generated after Aclarar
- WHEN the next topic is computed
- THEN it is the first Planificar topic (capabilities), as without the Brief

### Requirement: Document identity and storage (RF-18, RF-20, RN-03)

There MUST be at most one document per initiative and type. The system MUST store each document as Markdown only, with owner id (taken from the current user), type, version, status (Draft or Approved), content, approval time, and creation and update times in UTC. The stored text MUST carry no status or version frontmatter (single source of truth); no copy in another format MUST be stored. A first generation MUST create version 1 in Draft. The Artifacts module MUST be self-contained: its tables use their own prefix and carry no foreign keys to other modules.

#### Scenario: First generation creates version 1 Draft [UNIT]
- GIVEN an available Brief
- WHEN the owner generates it
- THEN one document exists with version 1, status Draft, non-empty Markdown, the owner as owner and creation and update times in UTC

#### Scenario: Uniqueness per initiative and type [UNIT]
- GIVEN a stored document of type Brief for an initiative
- WHEN a second Brief row for the same initiative is stored by the test double
- THEN the store rejects it, while a PRD for the same initiative is accepted

#### Scenario: No frontmatter stored [UNIT]
- GIVEN any generated document
- WHEN its content is inspected
- THEN it does not start with a frontmatter block and contains no status or version field

### Requirement: Generated content structure per document (RF-16, RF-36, RF-38, RF-40, RF-44)

`IArtifactDrafter` MUST produce the Markdown for a type from the initiative's name and description, its depth and the conversation answers. Each generated document MUST contain these sections, in this order, with these Spanish headings:

- **Brief**: Resumen ejecutivo, El problema, La solución, Qué lo hace distinto, A quién sirve, Criterios de éxito, Alcance, Visión, Índice de suposiciones, Preguntas pendientes.
- **PRD**: 0 Propósito del documento, 1 Visión, 2 Usuario objetivo (2.1 Trabajos por hacer, 2.2 No usuarios, 2.3 Recorridos clave), 3 Glosario, 4 Funcionalidades, 5 No objetivos, 6 Alcance del MVP (6.1 Dentro, 6.2 Fuera), 7 Métricas de éxito, 8 Preguntas abiertas, 9 Índice de suposiciones.
- **Spec**: Por qué, Capacidades, Restricciones, No objetivos, Señal de éxito, Índice de suposiciones, Preguntas abiertas.

Numbering MUST follow RF-44 with a hyphen and a number for every element, numbered in document order from 1 and without gaps within the generation: functional requirements `FR-n` (Capacidades of the Spec and Funcionalidades of the PRD, one per capability item), non-functional `NFR-n`, user journeys `UJ-n`, success metrics `SM-n` (Criterios de éxito of the Brief, Métricas de éxito of the PRD, Señal de éxito of the Spec), assumptions `A-n` (inline as `[ASSUMPTION: A-n]` followed by its text), pending questions `Q-n`. Each answer MUST be split into one item per non-empty line (list markers removed); an answer on a single line yields one item. At most 20 items (or journeys) are written per answer, followed by one plain line "y N más." when the answer had more, so many tiny lines cannot exceed the content cap. A "nothing" quick reply ("Ninguno", "Ninguno que yo sepa", "Nada por ahora", "Nada especial") MUST become one explanatory sentence without an identifier (for example "No se indicaron integraciones."), never an FR or NFR; the other quick replies (for example "Ambos") keep their label as text. The document MUST use the user's own words for the items, and MUST NOT present invented facts as certain.

Mapping of answers to sections (the sections not listed take an assumption):
- Brief: idea feeds Resumen ejecutivo and La solución; problem feeds El problema; users feeds A quién sirve; success feeds Criterios de éxito (`SM-n`); out of scope feeds Alcance (outside); Qué lo hace distinto, Visión and the inside of Alcance carry `[ASSUMPTION: A-n]`.
- PRD: idea and initiative name feed Propósito and Visión; users and problem feed Trabajos por hacer; the users answer also yields one journey `UJ-n` per person or group named, the whole Recorridos clave section carries one shared `[ASSUMPTION: A-n]` (journeys to confirm) because the script does not ask for a real journey (decision D1; a journey question is for step 5); capabilities feed Funcionalidades (`FR-n`) and Alcance dentro (the line "Todas las funcionalidades de la sección 4.", annotated with the priorities answer when present); constraints and qualities feed `NFR-n` items under a "Requisitos no funcionales" feature of Funcionalidades; integrations and teams (Large) feed `FR-n` items under an "Integraciones" feature, each phrased as a requirement ("Debe integrarse con <sistema o equipo>"); out of scope feeds No objetivos and Alcance fuera; success feeds Métricas (`SM-n`). Glosario and No usuarios carry `[ASSUMPTION: A-n]` when nothing feeds them.
- Spec: idea, users and problem feed Por qué (the users go in one bullet, "Quién lo usará", joined by semicolons); capabilities feed Capacidades (`FR-n`); out of scope feeds No objetivos (at least one item); success feeds Señal de éxito (`SM-n`); Restricciones carries `[ASSUMPTION: A-n]` because Small asks no constraints topic.

Every section with no feeding answer MUST carry at least one `[ASSUMPTION: A-n]` marker instead of being left empty. The Índice de suposiciones MUST list every `A-n` of the document with its text, and Preguntas pendientes / Preguntas abiertas MUST list every `Q-n`.

#### Scenario: Brief sections and order [UNIT]
- GIVEN a Standard conversation with the five Aclarar topics answered
- WHEN the Brief is drafted
- THEN the headings appear in the order Resumen ejecutivo, El problema, La solución, Qué lo hace distinto, A quién sirve, Criterios de éxito, Alcance, Visión, Índice de suposiciones, Preguntas pendientes

#### Scenario: PRD sections and order [UNIT]
- GIVEN a Standard conversation with all eight topics answered
- WHEN the PRD is drafted
- THEN sections 0 to 9 appear in order, with 2.1 to 2.3 and 6.1 and 6.2 under their parents

#### Scenario: Spec sections and order [UNIT]
- GIVEN a Small conversation with six topics answered
- WHEN the Spec is drafted
- THEN the headings Por qué, Capacidades, Restricciones, No objetivos, Señal de éxito, Índice de suposiciones and Preguntas abiertas appear in order

#### Scenario: Capabilities become functional requirements [UNIT]
- GIVEN a capabilities answer with three lines
- WHEN the PRD is drafted
- THEN Funcionalidades contains FR-1, FR-2 and FR-3 with those lines

#### Scenario: Constraints and qualities become non-functional requirements [UNIT]
- GIVEN a Large conversation with two constraint lines and one quality line
- WHEN the PRD is drafted
- THEN NFR-1, NFR-2 and NFR-3 exist, numbered in document order

#### Scenario: Success answers become metrics [UNIT]
- GIVEN a success answer with two lines
- WHEN any document with success content is drafted
- THEN SM-1 and SM-2 exist in the success section

#### Scenario: Unfed sections carry assumptions [UNIT]
- GIVEN a Brief drafted from five answers
- WHEN the sections Qué lo hace distinto and Visión are read
- THEN each contains an `[ASSUMPTION: A-n]` marker and the Índice de suposiciones lists those same A-n with their text

#### Scenario: Numbers are unique and ordered [UNIT]
- GIVEN any generated document
- WHEN its identifiers are extracted
- THEN each FR, NFR, UJ, SM, A and Q number appears once as a definition, numbered from 1 without gaps in document order

#### Scenario: Spec always has a non-goal [UNIT]
- GIVEN a Small conversation whose out-of-scope answer is "No sé"
- WHEN the Spec is drafted
- THEN No objetivos contains at least one item, marked with an assumption, and a Q-n exists for it

### Requirement: "No sé" answers become open questions (RF-35, RF-41, RF-43)

Every topic answered with "No sé" MUST appear in the document as a pending question `Q-n` in Preguntas pendientes (Brief), Preguntas abiertas (PRD, Spec) naming the topic in plain Spanish. The section that the topic feeds MUST refer to that `Q-n` and, where needed, carry an `[ASSUMPTION: A-n]`; the document MUST NOT invent content for that topic. A topic that is not in the current required set MUST NOT produce an item. Owner and status of the questions are out of scope.

#### Scenario: Unknown answer yields a question [UNIT]
- GIVEN the constraints topic answered "No sé"
- WHEN the PRD is drafted
- THEN Preguntas abiertas contains a Q-n about constraints, and the "Requisitos no funcionales" feature of Funcionalidades refers to that Q-n instead of an invented NFR

#### Scenario: Several unknowns are numbered in order [UNIT]
- GIVEN "No sé" for users and for success
- WHEN the Brief is drafted
- THEN Q-1 refers to users and Q-2 to success, in document order

#### Scenario: Typed don't-know phrase counts [UNIT]
- GIVEN the answer "ni idea" stored with the don't-know marker
- WHEN the document is drafted
- THEN it is treated exactly like the "No sé" quick reply

#### Scenario: No unknowns, no questions [UNIT]
- GIVEN every topic answered with content
- WHEN the document is drafted
- THEN Preguntas pendientes (or abiertas) is present with a text saying there are none and contains no Q-n

### Requirement: Deterministic fake drafter (RF-15)

`IArtifactDrafter` MUST be implemented in this change by a fake that is a pure function of its request: equal requests MUST yield equal Markdown. It MUST NOT use the clock, randomness or I/O and MUST NOT send data outside the process. The request MUST carry only text and level data (type, depth, initiative name and description, and the answers as topic key, text and don't-know flag) and MUST NOT carry ids, owner, email or timestamps. The generated content MUST NOT exceed the content cap; to ensure it, quoted answer text that would make the document exceed the cap MUST be shortened with an ellipsis. The generated Markdown MUST be accepted by the same validation that applies to saved edits.

#### Scenario: Same input same output [UNIT]
- GIVEN two equal drafting requests
- WHEN the fake drafts both
- THEN the Markdown is identical

#### Scenario: Request has no identifiers [UNIT]
- GIVEN the drafting request type
- WHEN its members are inspected
- THEN none is a user id, owner id, email, initiative id or timestamp

#### Scenario: Maximum answers stay under the cap [UNIT]
- GIVEN the largest case: a Large conversation with every topic answered with 2000 characters
- WHEN the PRD is drafted
- THEN the Markdown length is at most the content cap and ends with complete sections

#### Scenario: Output passes validation and has no warnings [UNIT]
- GIVEN any generated document
- WHEN it is validated and checked by the format parser
- THEN validation succeeds and no format warning is produced

#### Scenario: Golden outputs [UNIT]
- GIVEN fixed conversations for Small, Standard (Brief and PRD) and Large (PRD)
- WHEN each is drafted
- THEN each output equals its stored golden Markdown

### Requirement: Reading documents and the artifact list (RF-55)

The system MUST provide a query that returns, for an initiative, the list of its required types with, for each: whether it is generated, available or not yet available (with a plain reason), version, status and last update; and a query that returns one document (type, version, status, Markdown, format warnings, approval time). Both MUST be restricted to the owner and MUST use no tracking. They MUST work in every initiative status except Draft, where the list is empty with the reason "Termine de crear la iniciativa para generar documentos.". Reading a type that has not been generated MUST return a not-generated result, not an error. Documents of types no longer in the required set (for example a Brief after a Standard to Small change in Clarifying) MUST NOT appear in the list and MUST NOT count for completion, but MUST be retained.

#### Scenario: List shows state per required type [UNIT]
- GIVEN a Standard Planning initiative with a Draft Brief v2 and no PRD
- WHEN the list is requested
- THEN Brief shows generated, Draft, v2 and PRD shows its availability, with no Architecture or Epics

#### Scenario: Large lists its full level set [UNIT]
- GIVEN a Large initiative
- WHEN the list is requested
- THEN PRD, Architecture and Epics-and-stories are listed, and the last two are shown as coming in a later step and not available

#### Scenario: Not-generated read [UNIT]
- GIVEN an owned initiative without a Brief
- WHEN the Brief is read
- THEN the result is "not generated" and carries no content

#### Scenario: Orphan document is hidden but kept [UNIT]
- GIVEN a Standard Clarifying initiative with a generated Brief, then depth changed to Small
- WHEN the list is requested
- THEN only Spec is listed and the Brief row still exists

#### Scenario: Read does not change state [UNIT]
- GIVEN a document
- WHEN it is read repeatedly
- THEN version, status and update time are unchanged

### Requirement: Content validation and cap (RF-55)

Saved content MUST be non-empty after trimming surrounding whitespace and at most 20000 characters, measured on the stored text. Line endings MUST be normalized to a single form before measuring and storing, and surrounding whitespace trimmed. Messages (verbatim): empty, "El documento no puede estar vacío."; over the cap, "El documento no puede superar los 20000 caracteres.". A rejected save MUST persist nothing and MUST NOT lose the text in the editor.

#### Scenario: Empty and whitespace-only content [UNIT]
- GIVEN an empty content, then "     " and a newline-only content
- WHEN each is validated
- THEN each fails with "El documento no puede estar vacío."

#### Scenario: Exactly 20000 characters accepted [UNIT]
- GIVEN content of exactly 20000 characters
- WHEN it is validated
- THEN validation succeeds

#### Scenario: 20001 characters rejected [UNIT]
- GIVEN content of 20001 characters
- WHEN it is validated
- THEN validation fails with "El documento no puede superar los 20000 caracteres." and the stored document is unchanged

#### Scenario: Length measured after normalization [UNIT]
- GIVEN 20000 characters surrounded by blank lines, with Windows line endings
- WHEN it is validated and saved
- THEN it succeeds and the stored text has no surrounding whitespace and uniform line endings

#### Scenario: Rejected save keeps the typed text [UI]
- GIVEN the editor holds text over the cap
- WHEN Guardar is pressed
- THEN the error is shown in red next to the editor and the typed text remains

### Requirement: Editing and versioning (RF-17, RF-18, RF-54)

Saving an edit MUST require the expected version of the document. When the content differs from the stored content, it MUST replace it, increase the version by exactly one, set status Draft, clear the approval time and refresh the update time. When the content is identical after normalization, the save MUST succeed without changing version, status or times (no-op). Editing an Approved document MUST therefore return it to Draft with version N+1. If the initiative is ReadyToBuild, the edit MUST also reopen it to Planning through the `initiatives` reopen operation, and the initiative MUST end in Planning only when the document change was stored. Editing a Draft document in Clarifying or Planning MUST NOT change the initiative status. Version MUST rise only on content changes (edit or regeneration), never on approval.

#### Scenario: Edit raises the version [UNIT]
- GIVEN a Draft PRD v1
- WHEN the owner saves different content with expected version 1
- THEN the document is Draft v2 with the new content

#### Scenario: Unchanged content is a no-op [UNIT]
- GIVEN a Draft PRD v3
- WHEN the owner saves the same content
- THEN version stays 3 and the update time is unchanged

#### Scenario: Edit after approval returns to Draft [UNIT]
- GIVEN an Approved Brief v2 on a Planning initiative
- WHEN the owner saves a change with expected version 2
- THEN the Brief is Draft v3 with no approval time and the initiative stays Planning

#### Scenario: Edit after the initiative is ready reopens Planning [UNIT]
- GIVEN a ReadyToBuild Standard initiative with Brief and PRD Approved
- WHEN the owner edits the PRD
- THEN the PRD is Draft v+1 and the initiative is Planning (RN-06)

#### Scenario: Brief edit in Clarifying leaves the status [UNIT]
- GIVEN a Clarifying Standard initiative with a Draft Brief
- WHEN the owner edits the Brief
- THEN the Brief version rises and the initiative stays Clarifying

#### Scenario: Version never falls or repeats [UNIT]
- GIVEN a sequence of edits, a regeneration and an approval
- WHEN the version is read after each step
- THEN it increases by one at each content change and never changes at approval

#### Scenario: Editing a type not generated [UNIT]
- GIVEN no PRD
- WHEN the owner saves content for the PRD
- THEN it is rejected with "Todavía no se ha generado este documento." and nothing is stored

### Requirement: Optimistic concurrency (RF-17)

Every command that changes a document (save, regenerate, approve) MUST carry `ExpectedVersion`. If the stored version differs, the command MUST fail with the conflict message "El documento cambió en otra pestaña. Si estaba editando, su texto sigue en el cuadro: cópielo, cancele y edite de nuevo la versión actual." and persist nothing, in the initiative as well. The version MUST act as the concurrency token of the row so two simultaneous writers cannot both succeed. A conflict MUST NOT discard the text typed in the editor, and the editor MUST keep sending the version its text was loaded from, so saving again conflicts again until the person cancels and edits the current version (the typed text is never silently saved over the other tab's change).

#### Scenario: Stale save is rejected [UNIT]
- GIVEN two tabs that loaded PRD v2, and the first saves (now v3)
- WHEN the second saves with expected version 2
- THEN it fails with "El documento cambió en otra pestaña. Si estaba editando, su texto sigue en el cuadro: cópielo, cancele y edite de nuevo la versión actual." and the stored content is the first save

#### Scenario: Stale approve and regenerate are rejected [UNIT]
- GIVEN a document at v3 and commands with expected version 2
- WHEN approve and regenerate are executed
- THEN both fail with the conflict message and nothing changes

#### Scenario: Concurrent writers [UNIT]
- GIVEN two simultaneous saves with the same expected version
- WHEN both complete
- THEN exactly one succeeds and the other gets the conflict message

#### Scenario: Conflict keeps typed text [UI]
- GIVEN a stale tab with unsaved text in the editor
- WHEN Guardar produces the conflict
- THEN the message is shown and the editor still holds the typed text

### Requirement: Regeneration behind confirmation (RF-17, RF-57, RF-60, RN-07)

Regenerating MUST replace the whole content, including manual edits, with a new draft from the current conversation, increase the version by one, set status Draft and clear the approval time. It MUST be possible only for a generated document whose type is available, and only after the user confirms in a panel that explains that manual changes will be lost; the command MUST carry an explicit confirmation flag and without it MUST be rejected with "Confirme que desea reemplazar el documento." and change nothing. The new document MUST be numbered from scratch (RF-60). Regenerating a document of a ReadyToBuild initiative MUST reopen it to Planning like an edit does.

#### Scenario: Regenerate replaces edits [UNIT]
- GIVEN a Brief v2 with manual edits
- WHEN the owner regenerates with confirmation and expected version 2
- THEN the content equals a fresh draft, the version is 3 and the status is Draft

#### Scenario: Without confirmation nothing happens [UNIT]
- GIVEN a generated PRD
- WHEN regenerate is sent without confirmation
- THEN it is rejected with "Confirme que desea reemplazar el documento." and the document is unchanged

#### Scenario: Regeneration renumbers from scratch [UNIT]
- GIVEN a PRD edited so that FR-4 was removed and FR-7 added
- WHEN it is regenerated
- THEN the identifiers restart at FR-1 without gaps according to the current answers

#### Scenario: Regenerate an approved document [UNIT]
- GIVEN an Approved Spec on a ReadyToBuild initiative
- WHEN it is regenerated with confirmation
- THEN the Spec is Draft v+1 and the initiative is Planning

#### Scenario: Regenerate a document not generated [UNIT]
- GIVEN no Brief
- WHEN regenerate is sent
- THEN it is rejected with "Todavía no se ha generado este documento."

#### Scenario: Confirmation panel [UI]
- GIVEN a document page
- WHEN the user presses Regenerar
- THEN a panel states that manual changes will be lost and offers a confirm and a cancel action, and cancel changes nothing

### Requirement: Approval and completion (RF-07, RF-19, RF-52, RF-59, RN-01, RN-06)

Approving MUST be a manual action of the owner on a Draft document with the expected version; it MUST set status Approved and the approval time, MUST NOT change the version and MUST NOT depend on the format warnings (a document with warnings can be approved; the warnings are shown in the confirmation). Approving MUST also require that the coverage of the document still holds (it is available per the Availability rules), so undoing a Planificar answer after generating takes the approval away until the topic is answered again; it is rejected with the existing "topics uncovered" text, and the initiative MUST NOT be completed while any document of the level is uncovered. Approving an Approved document at the same version MUST succeed without changes (idempotent) while the coverage holds. When, after an approval, every type of the required set of the initiative's level is Approved, the system MUST mark the initiative ReadyToBuild through the `initiatives` idempotent operation; otherwise the initiative status MUST NOT change. Large MUST NOT reach ReadyToBuild in this change because Architecture and Epics-and-stories cannot exist. If marking ready fails after the approval was stored, the approval MUST stay and a later approve or completion check MUST retry the transition. No document MUST be Approved by any path other than this action.

#### Scenario: Approve keeps the version [UNIT]
- GIVEN a Draft Brief v2
- WHEN the owner approves with expected version 2
- THEN it is Approved v2 with an approval time

#### Scenario: Approve with warnings is allowed [UNIT]
- GIVEN a Draft PRD whose Markdown has format warnings
- WHEN the owner approves it
- THEN it succeeds and the result carries the warnings that were shown

#### Scenario: Standard needs both documents [UNIT]
- GIVEN a Standard Planning initiative with the Brief Approved and the PRD Draft
- WHEN the PRD is approved
- THEN the initiative becomes ReadyToBuild

#### Scenario: Partial set does not complete [UNIT]
- GIVEN a Standard Clarifying initiative
- WHEN the Brief is approved and the PRD does not exist
- THEN the initiative stays Clarifying

#### Scenario: Small completes with the Spec [UNIT]
- GIVEN a Small Planning initiative with a Draft Spec
- WHEN the Spec is approved
- THEN the initiative becomes ReadyToBuild

#### Scenario: Large never completes in this change [UNIT]
- GIVEN a Large Planning initiative with an Approved PRD
- WHEN completion is evaluated
- THEN the initiative stays Planning because the full level set is not approved

#### Scenario: Approval order does not matter [UNIT]
- GIVEN a Standard Planning initiative
- WHEN the PRD is approved first and the Brief second
- THEN the initiative becomes ReadyToBuild only after the second approval

#### Scenario: Idempotent approve [UNIT]
- GIVEN an Approved Spec at v2
- WHEN approve is sent again with expected version 2
- THEN it succeeds and nothing changes

#### Scenario: Ready transition fails after approval [UNIT]
- GIVEN the ready operation fails once after the last approval was stored
- WHEN approve is retried
- THEN the document stays Approved and the initiative becomes ReadyToBuild on the retry

#### Scenario: Approving needs ownership and availability [UNIT]
- GIVEN a document of another user's initiative, and a non-generated type
- WHEN approve is sent
- THEN both return not found or "Todavía no se ha generado este documento." and nothing changes

#### Scenario: Approval confirmation lists warnings [UI]
- GIVEN a document with format warnings
- WHEN the user presses Aprobar
- THEN the confirmation lists the warnings, confirming approves and a success notice appears in green

### Requirement: Format warnings from the Markdown (RF-43, RF-44, RF-56)

The system MUST derive format warnings by reading the Markdown on every read and every save, returning at most 50 of them followed by one final entry "y N avisos más." when there are more; a token without a hyphen ("Q4", "A4", "SM2") followed by prose is an ordinary word and MUST NOT be reported, while "FR1", "NFR1" and "UJ1" and any hyphenated attempt ("FR -1", "FR-", "FR-01", "fr-1", "NFR-x") are; warnings MUST never block saving or approving and MUST never alter or lose text. The parser MUST detect, per document type: a required section heading missing; an identifier of the kinds FR, NFR, UJ, SM, A or Q written without hyphen or number (for example "FR1", "FR-", "NFR-x") where a definition is expected; the same identifier defined twice; an `[ASSUMPTION: A-n]` marker whose A-n is absent from the Índice de suposiciones, or an index entry without a marker in the text; a Q-n or A-n numbered with zero. The rule that numbers are never reused or reordered on edits (RF-44) cannot be verified without history and MUST NOT be reported as a warning. Warnings MUST be in Spanish, name the kind of problem and the identifier or heading, quote a short excerpt of the offending line (about 50 characters, without markup characters) instead of a line number, and be returned in document order. The structured index (items table, owners, states) is out of scope.

#### Scenario: Generated documents have no warnings [UNIT]
- GIVEN a drafted Brief, PRD and Spec
- WHEN the parser reads each
- THEN each has zero warnings

#### Scenario: Missing section [UNIT]
- GIVEN a PRD from which "Glosario" was deleted by an edit
- WHEN it is saved
- THEN the save succeeds, the warning names the missing section and the text is kept

#### Scenario: Malformed identifier [UNIT]
- GIVEN a definition written "FR3 Exportar" instead of "FR-3"
- WHEN the parser reads it
- THEN a warning names the malformed identifier

#### Scenario: Duplicate identifier [UNIT]
- GIVEN two definitions of FR-2
- WHEN the parser reads the document
- THEN one warning names the duplicate FR-2

#### Scenario: Assumption marker without index entry [UNIT]
- GIVEN an inline `[ASSUMPTION: A-5]` and an index without A-5
- WHEN the parser reads the document
- THEN a warning names A-5

#### Scenario: Gaps and retired numbers are not warnings [UNIT]
- GIVEN a document with FR-1 and FR-3, FR-2 marked as retired
- WHEN the parser reads it
- THEN no warning is produced for the gap

#### Scenario: Warnings do not block [UNIT]
- GIVEN content with three warnings
- WHEN it is saved and then approved
- THEN both succeed

#### Scenario: Warnings in the page [UI]
- GIVEN a document with warnings
- WHEN the page renders
- THEN the warnings are listed near the document as a non-blocking notice

### Requirement: Rendered view and edit mode (RF-55)

The document page MUST show a read view in which the Markdown is presented as a document, and an edit mode with a text area holding the Markdown. The Markdown to HTML conversion MUST use a Markdown library with raw HTML disabled so that embedded HTML tags (script, iframe, on-event attributes, raw tags) are not interpreted; dangerous link schemes (such as `javascript:`) MUST NOT become active links: a link with a disallowed address MUST render as plain text (the link text), never as an anchor. Images MUST NOT be rendered as images at all (the viewer's browser must not fetch an external address): an image shows its alt text. Headings in the document MUST be demoted one level (`#` renders as h2, capped at h6) because the page already has an h1. Switching between view and edit MUST NOT lose unsaved text on a failed save. Both modes MUST show type, version, status and approval state.

#### Scenario: HTML is not interpreted [UNIT]
- GIVEN Markdown containing `<script>alert(1)</script>`, `<img src=x onerror=alert(1)>` and `[x](javascript:alert(1))`
- WHEN it is rendered
- THEN the output contains no script element, no img element with an event attribute, no anchor and no javascript link (the link text stays as plain text)

#### Scenario: Markdown is presented [UNIT]
- GIVEN Markdown with headings, lists and a table
- WHEN it is rendered
- THEN the output contains the corresponding heading (one level lower, so no h1), list and table elements

#### Scenario: Read view and edit mode [UI]
- GIVEN a document
- WHEN the user opens it and presses Editar
- THEN the read view is replaced by a text area with the Markdown, and Guardar and Cancelar are offered

#### Scenario: Cancel discards unsaved edits [UI]
- GIVEN edited but unsaved text
- WHEN the user presses Cancelar
- THEN the read view shows the stored document unchanged

### Requirement: Owner-only access and eligibility (RF-26)

Every artifact use case MUST resolve the initiative through the current user. For an initiative owned by another user, deleted, or unknown, the result MUST be the same not-found result as the initiatives module gives, and nothing MUST be persisted. The owner of a document MUST be the initiative owner, never taken from request input. A deleted initiative's documents MUST NOT be readable or changeable.

#### Scenario: Foreign, deleted or unknown initiative reads as not found [UNIT]
- GIVEN an initiative of A, a deleted initiative of A and a random id
- WHEN B lists, reads, generates, saves, regenerates and approves on the first, and A does so on the other two
- THEN every call returns the same not-found result and nothing is persisted

#### Scenario: Owner recorded from the current user [UNIT]
- GIVEN the current user is A
- WHEN A generates a document
- THEN the document owner is A's id

#### Scenario: Not-found page for a foreign id [UI]
- GIVEN B opens /iniciativas/{id}/artefactos/{tipo} of A's initiative
- WHEN the page loads
- THEN the same not-found message as for a nonexistent initiative is shown

### Requirement: Authentication required

Every artifact page and use case MUST require an authenticated user. An unauthenticated request to /iniciativas/{id}/artefactos/{tipo} MUST redirect to login. Handlers MUST NOT run without a current user.

#### Scenario: Unauthenticated redirected [UI]
- GIVEN an unauthenticated visitor
- WHEN they open an artifact route
- THEN they are redirected to the login page

#### Scenario: Handler without current user [UNIT]
- GIVEN no current user
- WHEN any artifact handler runs
- THEN it fails and nothing is persisted or returned

### Requirement: Privacy of logs and module boundary

Document content MUST NOT be logged: commands and results that carry Markdown MUST redact it in their string representation, and the logging behavior MUST record only the request name and identifiers. The Artifacts module MUST NOT reference initiative or conversation entities, repositories or persistence, nor Identity; it MAY use only MediatR requests of Initiatives and Assistant, their DTOs and shared enums. The chat and detail screens MUST keep the demo-data notice visible next to the documents. A test MUST fail when the boundary is violated.

#### Scenario: No content in logs [UNIT]
- GIVEN a save with a distinctive text and a capturing logger double
- WHEN the handler runs, succeeds or fails
- THEN no log entry contains that text

#### Scenario: Commands redact content [UNIT]
- GIVEN a save command holding distinctive Markdown
- WHEN its ToString is called
- THEN the Markdown does not appear

#### Scenario: Boundary test [UNIT]
- GIVEN the Artifacts module sources
- WHEN the boundary check runs
- THEN it passes, and it fails if a forbidden Initiatives, Assistant or Identity type is referenced

#### Scenario: Table prefix [UNIT]
- GIVEN the model of the database context
- WHEN the Artifacts tables are listed
- THEN each name starts with the Artifacts prefix and none has a foreign key to another module

#### Scenario: Demo notice [UI]
- GIVEN the document page and the Artefactos card
- WHEN they render
- THEN the note "Modo de demostración: escriba solo datos de ejemplo, no información real de la empresa ni datos personales." is visible

### Requirement: Artefactos card on the detail (RF-08, RF-16)

The initiative detail MUST show an Artefactos card in place of the placeholder, listing the required types of the level, each with its state ("Sin generar", "Disponible", "Aún no disponible" with the reason, "Borrador vN", "Aprobado vN"), and an action: "Generar" when available and not generated, "Abrir" when generated. Types of Large that belong to step 6 MUST be shown as pending a later step with no action. In Draft the card MUST show "Termine de crear la iniciativa para generar documentos." and no action.

#### Scenario: Card per state [UI]
- GIVEN initiatives in Draft, Clarifying with Aclarar covered (Standard), Planning with documents Draft and Approved, and ReadyToBuild
- WHEN each detail renders
- THEN each card shows the states and actions above

#### Scenario: Generar then open [UI]
- GIVEN an available Brief
- WHEN the user presses Generar
- THEN a loading indicator is shown, then the Brief opens at its page in Borrador v1 with a green success notice

### Requirement: Document page, labels and navigation

The page `/iniciativas/{id}/artefactos/{tipo}` MUST show the document with the actions Editar, Guardar, Cancelar, Regenerar, Aprobar and Volver (to the detail), enabled according to the document state: Aprobar only for Draft, Guardar only in edit mode. An unknown `tipo` MUST show the not-found message. Labels for types, statuses and states MUST come from a pure, tested helper in Spanish.

#### Scenario: Labels [UNIT]
- GIVEN each artifact type, status and state
- WHEN its label is requested
- THEN the labels are the Spanish names ("Especificación", "Brief", "PRD", "Borrador", "Aprobado", "Sin generar", "Disponible", "Aún no disponible")

#### Scenario: Unknown type [UI]
- GIVEN the route with an unknown tipo
- WHEN the page loads
- THEN the not-found message is shown

#### Scenario: Action states [UI]
- GIVEN an Approved document
- WHEN the page renders
- THEN Aprobar is disabled or absent, Editar and Regenerar are available, and the approved state is shown

### Requirement: UI feedback, language and accessibility (RF-41)

All text MUST be Spanish and plain. Success notices (generated, saved, approved, regenerated) MUST use the green success style; errors and warnings MUST be visually distinct from success; the required-field asterisk MUST be red; a loading indicator MUST be shown during generate, regenerate, save and approve, and the controls MUST be disabled while an operation is in flight so that a double click stores one change. The rendered document MUST use semantic headings; the editor textarea MUST have an associated label; notices, warnings and the loading indicator MUST be exposed to assistive technology as status or alert regions; the confirmation panels MUST be reachable and dismissible by keyboard.

#### Scenario: Success is green [UI]
- GIVEN the user saves or approves a document
- WHEN the operation succeeds
- THEN the notice is shown in the green success style

#### Scenario: Required marker [UI]
- GIVEN the editor shows a required content field
- WHEN it renders
- THEN its label carries a red asterisk

#### Scenario: Loading and double submit [UI]
- GIVEN the user presses Generar or Guardar
- WHEN the request is in progress
- THEN a loading indicator is visible, the controls are disabled and one change is stored

#### Scenario: Roles and labels [UI]
- GIVEN the document page
- WHEN it renders
- THEN the editor label, status and alert regions and the confirmation dialogs are present and keyboard operable

## Assumptions

- Brief is a Standard-only document; Large's PRD flow has no Brief (the required sets of RF-27/RF-51).
- Brief availability counts the required Aclarar topics only; it is available at the same time the assistant offers the confirmation, and in Automatic mode only after the level is accepted.
- Documents are available in ReadyToBuild only for read, edit and regenerate; the content of an orphan document (a type removed from the required set by a depth change in Clarifying) is retained but hidden.
- A save with identical content after normalization is a no-op (no version bump).
- Regeneration is a command with an explicit confirmation flag; the confirmation panel is UI plus that flag.
- The content cap (20000 characters) is measured after trimming and line-ending normalization; generated content is shortened (answers quoted with an ellipsis) when needed to stay under it.
- Answers are split into one item per non-empty line; list markers are removed; items keep the user's words.
- The Spec uses `FR-n` for Capacidades (platform scheme RF-44) instead of BMAD's `CAP-n`. BMAD's PRD defines no identifier for non-functional requirements and its epics step reads `FR1:` and `NFR1:` without a hyphen; the platform keeps the hyphenated RF-44 scheme for all of them, and the step 8 exporter (or the step 6 epics generator) maps `FR-n`, `NFR-n` and `CAP-n` to what BMAD expects.
- Integrations and teams (Large) become `FR-n` items under an "Integraciones" feature in the PRD, each phrased as "Debe integrarse con ..." so BMAD's epics step reads a requirement and not a bare system name.
- Decision D1 refinement: the Recorridos clave section carries one shared assumption for all its `UJ-n` journeys (they are placeholders to confirm), so the assumption index is not inflated with one entry per journey.
- A document edited in Clarifying or Planning that is made stale by later conversation answers is not regenerated automatically; the user may regenerate.
- Format-warning wording, detection list and the Spanish rejection messages are proposals; design may adjust wording but not semantics.
- The `tipo` route segment slugs are a design choice.
- A malformed identifier without a hyphen is reported only for the long prefixes (`FR1`, `NFR1`, `UJ1`); `Q4`, `A4` and `SM2` followed by prose read as ordinary words and are not reported. En dash and comma-terminated identifiers (`FR-1,`) are not detected.
- A quick reply that states there is nothing to list is carried to the drafter as such and is written as a sentence, so it never becomes a requirement.

## Out of Scope

Gemini drafting (step 5); Architecture and Epics-and-stories generation (step 6), so Large cannot finish; attachments feeding generation (step 7); export, copy, PDF, frontmatter, usage note and "Preguntas para desarrollo" (step 8); the structured index table, owners and states of questions and assumptions; version history and diff (RF-18); PRD questions derived from the approved Brief (RF-38 part); automatic regeneration when answers change; deleting a document.
