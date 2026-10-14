# Delta for Artifacts (gemini-assistant)

**Baseline.** The capability `artifacts` is NOT in `openspec/specs/` yet. The baseline of this delta is `openspec/changes/artifact-generation/specs/artifacts/spec.md` as amended by `openspec/changes/bmad-alignment/specs/artifacts/spec.md` (both done). BMAD has no archive or global spec merge, so neither change is archived: every MODIFIED requirement below is written in FULL (complete replacement text, same name as in the latest baseline) and replaces the baseline requirement of the same name; every requirement of the baseline that is not named here stays as it is. Source requirements: RF-15, RF-16, RF-36, RF-38, RF-41 to RF-44, RF-61, RF-64 to RF-66, RF-70 to RF-72 (REQUISITOS.md; RF-65, RF-66 and RF-71 are the authority for "never invent"). Companion specs: `gemini-client` (HTTP contract, reason codes) and `assistant-provider-configuration` (switch).

Scenario tags: **[UNIT]** xUnit at validator, handler or pure-class level (hand-written doubles including a stub model client with canned JSON; no bUnit, no network, no key). **[UI]** manual-only check in Chrome. **[DOC]** documentation deliverable checked by reading the file. **[MANUAL]** needs the live key; outside CI. Terminology as in the baselines. Added terms: an **item** is one entry of a section in the model's JSON (`text` and `source`); **source** is `answer` (stated by the person), `assumption` (a reasonable guess to confirm) or `unknown` (not known; becomes an open question). The **composer** is the existing `DraftComposer` and its layouts. Message wording is a proposal unless marked verbatim.

## ADDED Requirements

### Requirement: Model-backed drafting through schema items (RF-71, RF-15, RF-16)

When `Assistant:Provider` = Gemini, `IArtifactDrafter` MUST be implemented by a drafter in which the model returns JSON items per section of the document type, and the composer renders the Markdown. The model MUST NOT write Markdown, headings, identifiers (`FR-n`, `NFR-n`, `SM-n`, `A-n`, `Q-n`), assumption markers or the triage suffix; the composer MUST produce them. The drafter MUST therefore keep every guarantee of the baseline: the outline and Spanish headings, sequential ids from one numbering pass, `[ASSUMPTION: A-n]` plus the Índice de suposiciones, every `Q-n` line ending with `(Responsable: por asignar; Revisar: por definir)`, at most 20 items per section followed by "y N más.", `#` escaping, the 20,000-character cap (item text is shortened with an ellipsis, as the Fake does), no frontmatter, acceptance by the same validation as saved edits, and zero format warnings. Item text MUST be one line of plain Spanish: line breaks are collapsed, list markers and Markdown are removed, and any text that looks like an identifier definition or an `[ASSUMPTION` marker MUST be neutralized so a model cannot forge ids or index entries. Covered types in this change: Spec, Brief and PRD. The structural content of "Recorridos clave" (RF-64) and the headings stay composer-owned. The request to the model MUST carry only text and level data (type, depth, initiative name and description, the answers with topic key, text and don't-know flag, and, for the PRD, the approved Brief text) and no id, owner, email or timestamp. With `Assistant:Provider` = Fake the Fake drafter and its goldens are unchanged.

A response whose JSON names a section that is not in the outline, has an item with a `source` outside `answer|assumption|unknown`, or lacks the required members MUST be treated as Malformed. A section returned with no items MUST receive the same default assumption the Fake uses for an unfed section.

#### Scenario: Composer renders model items [UNIT]
- GIVEN a stub model client returning items for every Brief section
- WHEN the Brief is drafted
- THEN the Markdown has the Brief headings in order, sequential ids from the composer and an Índice de suposiciones listing every `A-n`

#### Scenario: Model cannot write identifiers [UNIT]
- GIVEN an item text "FR-9 Debe exportar" and another "[ASSUMPTION: A-7] algo"
- WHEN the document is drafted
- THEN the Markdown defines each FR, A and Q number once, from 1 without gaps, and the forged text is neutralized

#### Scenario: Multi-line item text is one line [UNIT]
- GIVEN an item text with a line break, a heading marker and bold markup
- WHEN the document is drafted
- THEN the item is one line of plain text and `#` at line start is escaped

#### Scenario: Cap and 20-item limit hold [UNIT]
- GIVEN a stub reply with 30 items in a section and long texts that would exceed 20,000 characters
- WHEN the PRD is drafted
- THEN each section has at most 20 items followed by "y 10 más.", and the total is at most 20,000 characters ending with complete sections

#### Scenario: Output passes validation with zero warnings [UNIT]
- GIVEN stub replies for Spec, Brief and PRD
- WHEN each is drafted, validated and checked by the format parser
- THEN validation succeeds and no format warning is produced

#### Scenario: A section with no answer gets the default assumption [UNIT]
- GIVEN a conversation with no answer feeding Visión and a stub reply with no items for it
- WHEN the Brief is drafted
- THEN Visión carries `[ASSUMPTION: A-n]` and the index lists it

#### Scenario: Unknown section or source is malformed [UNIT]
- GIVEN a stub reply naming a section "Extra", then another with source "guess"
- WHEN the document is drafted
- THEN each fails as Malformed (see "Drafter failures")

#### Scenario: Fake drafter and goldens unchanged [UNIT]
- GIVEN `Assistant:Provider` = Fake and the fixed conversations of the goldens
- WHEN each document is drafted
- THEN each equals its stored golden

#### Scenario: Request has no identifiers and no attachments [UNIT]
- GIVEN the model drafting request
- WHEN its members are inspected
- THEN none is a user id, owner id, email, initiative id, timestamp or attachment

### Requirement: Item sources and never inventing (RF-71, RF-65 f-g, RF-66)

The model MUST tag every item with its `source`, and the composer MUST render by source: `answer` as a plain item in the person's meaning; `assumption` as an item with `[ASSUMPTION: A-n]` and an index entry; `unknown` as a reference to a new `Q-n` in Preguntas pendientes / Preguntas abiertas, written as a triage line with the default suffix. The drafter MUST NOT present as certain anything the person did not say (RF-65 f). A topic answered with "No sé" (decided in code from the don't-know flag, not by the model) MUST always yield a `Q-n` and the section it feeds MUST refer to it; any `answer` item the model offers for a section that is fed only by that topic MUST be dropped. Metrics, quality bounds, glossary terms, non-users, a differentiator and any detail the person never gave MUST be an `assumption` or an `unknown`, never an `answer` (RF-66). A section may keep an `answer` item only for what is in the answers (or, for the PRD, in the approved Brief). The drafter MUST also keep the "No sé" scheme of the baseline: topics outside the required set produce no item, and a document with no unknown topics says there are no open questions.

#### Scenario: Answer items render plainly [UNIT]
- GIVEN an item with source `answer` in Alcance
- WHEN the document is drafted
- THEN it is a plain line without a marker and without a Q-n

#### Scenario: Assumption items are tagged [UNIT]
- GIVEN an item with source `assumption` in "Qué lo hace distinto"
- WHEN the document is drafted
- THEN the line carries `[ASSUMPTION: A-n]` and the Índice de suposiciones lists that A-n with the text

#### Scenario: Unknown items become questions [UNIT]
- GIVEN an item with source `unknown` in Restricciones
- WHEN the document is drafted
- THEN a new Q-n exists with the default suffix, the section refers to it, and the item is not shown as a fact

#### Scenario: "No sé" always yields a question, whatever the model says [UNIT]
- GIVEN the constraints topic answered "No sé" and a stub reply that fills Requisitos no funcionales with `answer` items
- WHEN the PRD is drafted
- THEN those items are dropped, Preguntas abiertas contains a Q-n about constraints and the feature refers to it

#### Scenario: A section outside the document's outline is malformed [UNIT]
- GIVEN a Small conversation and a stub reply with an item under a section name that is not in the Spec outline (for example one fed by a Planificar topic)
- WHEN the Spec is drafted
- THEN the reply is Malformed, the drafter throws with guidance and nothing is stored

#### Scenario: Several unknowns are numbered in order [UNIT]
- GIVEN "No sé" for users and for success
- WHEN the Brief is drafted
- THEN Q-1 refers to users and Q-2 to success, in document order

#### Scenario: No unknowns, no questions [UNIT]
- GIVEN every topic answered with content and no `unknown` items
- WHEN the document is drafted
- THEN Preguntas pendientes (or abiertas) says there are none and contains no Q-n

#### Scenario: Metrics without target are not facts [UNIT]
- GIVEN a success answer "mejorar la satisfacción" and a stub reply that adds an `assumption` item for the target and counter-metric
- WHEN the document is drafted
- THEN the target appears as `[ASSUMPTION: A-n]`, not as an `answer`

#### Scenario: Documents invent nothing [MANUAL]
- GIVEN the persona evaluation with a live key and answers that omit metrics, users and constraints
- WHEN the Spec, Brief and PRD are read
- THEN every figure, name or decision not in the answers is tagged as an assumption or appears as a Q-n

### Requirement: The PRD builds on the approved Brief (RF-72, RF-38)

For a Standard initiative, when the Brief exists and is Approved, the drafter MUST receive the Brief's Markdown (the request gains an optional `BriefMarkdown`) and the PRD MUST build on it: it MUST stay consistent with the Brief and MUST NOT ask again or contradict what the Brief says, and content found only in the approved Brief MAY be `answer` items, because the person approved it. Content that rests only on an assumption or an open question of the Brief MUST stay an `assumption` or an `unknown` in the PRD, never an `answer`. When the Brief does not exist, is a Draft, or the initiative is not Standard, `BriefMarkdown` MUST be absent and the PRD MUST be drafted from the answers alone. Regenerating the PRD MUST use the Brief as approved at that moment. The Fake drafter ignores `BriefMarkdown`, so its goldens are unchanged. A PRD drafted without the Brief MUST NOT carry any note about it. The Brief text is sent to the model within the 20,000-character cap of a document.

#### Scenario: Approved Brief reaches the drafter [UNIT]
- GIVEN a Standard initiative with an Approved Brief and a drafter double
- WHEN the owner generates the PRD
- THEN the drafting request carries the Brief's Markdown

#### Scenario: Draft Brief is not used [UNIT]
- GIVEN a Standard initiative whose Brief is Draft
- WHEN the owner generates the PRD
- THEN the request has no Brief text and the PRD is drafted from the answers alone

#### Scenario: No Brief, no Brief text [UNIT]
- GIVEN a Standard initiative with no Brief generated
- WHEN the owner generates the PRD
- THEN the request has no Brief text and generation succeeds

#### Scenario: Large and Small never receive a Brief [UNIT]
- GIVEN a Large initiative with a PRD and a Small initiative with a Spec
- WHEN each is generated
- THEN the request has no Brief text

#### Scenario: Regeneration uses the current approved Brief [UNIT]
- GIVEN a PRD generated before the Brief was edited and re-approved
- WHEN the owner regenerates the PRD with confirmation
- THEN the request carries the re-approved Brief text

#### Scenario: Brief assumptions stay assumptions [UNIT]
- GIVEN an approved Brief with `[ASSUMPTION: A-1] el equipo es de 5 personas` and a stub reply that returns it as `answer` in the PRD
- WHEN the PRD is drafted
- THEN the content is rendered as an assumption with a new `[ASSUMPTION: A-n]`, not as a fact

#### Scenario: Brief open questions stay questions [UNIT]
- GIVEN an approved Brief with an open question about budget and a stub reply that omits it
- WHEN the PRD is drafted
- THEN the budget gap appears in the PRD as a Q-n or an assumption, not as a fact

#### Scenario: Fake ignores the Brief [UNIT]
- GIVEN the fake drafter and a request with Brief text
- WHEN the PRD is drafted
- THEN the Markdown equals the same request without Brief text

#### Scenario: Consistency between Brief and PRD [MANUAL]
- GIVEN a live key and an approved Brief for a Standard initiative
- WHEN the PRD is drafted
- THEN problem, users and success agree with the Brief and nothing contradicts it

### Requirement: Drafter failures (RF-70, RF-15)

Any failure of the model-backed drafter MUST throw `AssistantUnavailableException` with a reason code, MUST store nothing and MUST leave an existing document (version, status, content, owners) untouched. There MUST be no fallback to the Fake for documents, because it would be silent low-quality output. The messages MUST be about the document and MUST NOT say that a message was saved (proposals, in Spanish):
- Quota: "El asistente alcanzó su límite de uso por ahora. No se generó el documento; inténtelo de nuevo más tarde."
- Timeout: "El asistente tardó demasiado en responder. No se generó el documento; inténtelo de nuevo."
- Auth and Model: "El asistente no está configurado correctamente. No se generó el documento; avise a quien mantiene la plataforma."
- Blocked: "El asistente no pudo redactar el documento con las respuestas actuales. Revise sus respuestas (puede deshacer la última y escribirla de otra forma) e inténtelo de nuevo."
- Malformed: "El asistente devolvió un borrador que no se pudo usar. Inténtelo de nuevo."
The user retries with the same action (Generar or Regenerar); there is no automatic retry, and a 429 is never retried. The page MUST show the message near the action and keep the page usable.

#### Scenario: Failure stores nothing [UNIT]
- GIVEN no Brief and a model client that fails with reason Quota
- WHEN the owner generates the Brief
- THEN no document row exists and the quota message is returned

#### Scenario: Failed regeneration changes nothing [UNIT]
- GIVEN a PRD v2 Approved and a model client that fails with reason Timeout
- WHEN the owner regenerates with confirmation
- THEN the PRD stays v2 Approved with the same content and the timeout message is returned

#### Scenario: Blocked throws with guidance [UNIT]
- GIVEN a model client that fails with reason Blocked
- WHEN the document is generated
- THEN the exception reason is Blocked, the message mentions reviewing the answers and no document is stored

#### Scenario: Malformed output throws [UNIT]
- GIVEN a stub reply that is not valid JSON
- WHEN the document is generated
- THEN the exception reason is Malformed, the Malformed message is returned and nothing is stored

#### Scenario: Messages do not claim a saved message [UNIT]
- GIVEN every reason code
- WHEN the drafter messages are listed
- THEN none contains "quedó guardado"

#### Scenario: No silent fake fallback [UNIT]
- GIVEN `Assistant:Provider` = Gemini and a model client that always fails
- WHEN the document is generated
- THEN the error is returned and the stored content never comes from the fake drafter

#### Scenario: Failure leaves the page usable [UI]
- GIVEN a forced failure with the real key (for example a wrong model name)
- WHEN the owner presses Generar
- THEN the message appears next to the action, the button is enabled again, and a later retry works once the service works

#### Scenario: Full walkthrough with the real key [MANUAL]
- GIVEN `Assistant:Provider` = Gemini and a real key
- WHEN Small and Standard are walked end to end in Chrome (conversation, Spec, Brief and PRD generation, approval, export)
- THEN the documents render without format warnings and `Logs/` holds no content

## MODIFIED Requirements

### Requirement: Generated content structure per document (RF-16, RF-36, RF-38, RF-40, RF-44, RF-61, RF-64, RF-71)

`IArtifactDrafter` MUST produce the Markdown for a type from the initiative's name and description, its depth and the conversation answers. Each generated document MUST contain these sections, in this order, with these Spanish headings:

- **Brief**: Resumen ejecutivo, El problema, La solución, Qué lo hace distinto, A quién sirve, Criterios de éxito, Alcance, Visión, Índice de suposiciones, Preguntas pendientes.
- **PRD**: 0 Propósito del documento, 1 Visión, 2 Usuario objetivo (2.1 Trabajos por hacer, 2.2 No usuarios, 2.3 Recorridos clave), 3 Glosario, 4 Funcionalidades, 5 No objetivos, 6 Alcance del MVP (6.1 Dentro, 6.2 Fuera), 7 Métricas de éxito, 8 Preguntas abiertas, 9 Índice de suposiciones.
- **Spec**: Por qué, Capacidades, Restricciones, No objetivos, Señal de éxito, Índice de suposiciones, Preguntas abiertas.

Numbering MUST follow RF-44 with a hyphen and a number for every element, numbered in document order from 1 and without gaps within the generation: functional requirements `FR-n` (Capacidades of the Spec and Funcionalidades of the PRD, one per capability item), non-functional `NFR-n`, success metrics `SM-n` (Criterios de éxito of the Brief, Métricas de éxito of the PRD, Señal de éxito of the Spec), assumptions `A-n` (inline as `[ASSUMPTION: A-n]` followed by its text), pending questions `Q-n`. User journeys `UJ-n` are part of the scheme and valid in edited documents, but the generator MUST NOT write any. Each answer MUST be split into one item per non-empty line (list markers removed); an answer on a single line yields one item. At most 20 items are written per answer (per section for the model-backed drafter), followed by one plain line "y N más." when there were more, so many tiny lines cannot exceed the content cap. A "nothing" quick reply ("Ninguno", "Ninguno que yo sepa", "Nada por ahora", "Nada especial") MUST become one explanatory sentence without an identifier (for example "No se indicaron integraciones."), never an FR or NFR; the other quick replies (for example "Ambos") keep their label as text. The Fake drafter MUST use the user's own words for the items; the model-backed drafter MAY tidy the wording into plain Spanish but each item MUST stay faithful to the answers and carry its `source`. Neither MUST present invented facts as certain.
(Previously: the document "MUST use the user's own words for the items" for any drafter; the model-backed drafter did not exist.)

Mapping of answers to sections (the sections not listed take an assumption):
- Brief: idea feeds Resumen ejecutivo and La solución; problem feeds El problema; users feeds A quién sirve; success feeds Criterios de éxito (`SM-n`); out of scope feeds Alcance (outside); Qué lo hace distinto, Visión and the inside of Alcance carry `[ASSUMPTION: A-n]`.
- PRD: idea and initiative name feed Propósito and Visión; users and problem feed Trabajos por hacer; Recorridos clave is not fed by any answer and holds the single assumption of "PRD key journeys section without markers" (no `UJ-n`, whatever the users answer); capabilities feed Funcionalidades (`FR-n`) and Alcance dentro (the line "Todas las funcionalidades de la sección 4.", annotated with the priorities answer when present); constraints and qualities feed `NFR-n` items under a "Requisitos no funcionales" feature of Funcionalidades; integrations and teams (Large) feed `FR-n` items under an "Integraciones" feature, each phrased as a requirement ("Debe integrarse con <sistema o equipo>"); out of scope feeds No objetivos and Alcance fuera; success feeds Métricas (`SM-n`). Glosario and No usuarios carry `[ASSUMPTION: A-n]` when nothing feeds them.
- Spec: idea, users and problem feed Por qué (the users go in one bullet, "Quién lo usará", joined by semicolons); capabilities feed Capacidades (`FR-n`); out of scope feeds No objetivos (at least one item); success feeds Señal de éxito (`SM-n`); Restricciones carries `[ASSUMPTION: A-n]` because Small asks no constraints topic.

Every section with no feeding answer MUST carry at least one `[ASSUMPTION: A-n]` marker instead of being left empty. The Índice de suposiciones MUST list every `A-n` of the document with its text, and Preguntas pendientes / Preguntas abiertas MUST list every `Q-n`, each line ending with the triage suffix of "Open-question triage line" (default `(Responsable: por asignar; Revisar: por definir)`).

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
- THEN each FR, NFR, SM, A and Q number appears once as a definition, numbered from 1 without gaps in document order, and no UJ is defined

#### Scenario: Question lines carry the suffix [UNIT]
- GIVEN a document with two "No sé" topics
- WHEN it is drafted
- THEN both Q-n lines end with `(Responsable: por asignar; Revisar: por definir)`

#### Scenario: Spec always has a non-goal [UNIT]
- GIVEN a Small conversation whose out-of-scope answer is "No sé"
- WHEN the Spec is drafted
- THEN No objetivos contains at least one item, marked with an assumption, and a Q-n exists for it

#### Scenario: Model-backed items stay faithful and sourced [UNIT]
- GIVEN a stub reply whose items rephrase the answers and carry sources
- WHEN the document is drafted
- THEN every item in a section fed by an answer has source `answer`, `assumption` or `unknown`, and none is rendered as certain without a source

### Requirement: Deterministic fake drafter (RF-15)

`IArtifactDrafter` MUST keep a fake implementation that is a pure function of its request: equal requests MUST yield equal Markdown. It is the default (`Assistant:Provider` = Fake). It MUST NOT use the clock, randomness or I/O and MUST NOT send data outside the process. The request MUST carry only text and level data (type, depth, initiative name and description, the answers as topic key, text and don't-know flag, and the optional approved Brief text) and MUST NOT carry ids, owner, email or timestamps; this holds for every implementation. The fake MUST ignore the Brief text. The generated content MUST NOT exceed the content cap; to ensure it, quoted answer text that would make the document exceed the cap MUST be shortened with an ellipsis. The generated Markdown MUST be accepted by the same validation that applies to saved edits.
(Previously: the fake was the only implementation and the request had no Brief text.)

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
- THEN each output equals its stored golden Markdown (the goldens of bmad-alignment, unchanged)

#### Scenario: Fake ignores the Brief text [UNIT]
- GIVEN a PRD request with and without Brief text
- WHEN the fake drafts both
- THEN the Markdown is identical

### Requirement: No demo notice on the document pages (RN-05)

The document page and the Artefactos card MUST NOT show a demo-mode notice (the product owner accepted the free-tier risk on 2026-10-12 and the notice was removed). This supersedes the baseline scenario "Demo notice" of `artifact-generation`. The privacy risk of the free Gemini tier stays documented in the README and `AGENTS.md` §4.8.

#### Scenario: No demo notice on the document page and the card [UI]
- GIVEN the document page and the Artefactos card
- WHEN they render
- THEN no "Modo de demostración" note is present

## Assumptions

- When a section has an answer but the model returns no items for it, the drafter quotes the answer (clipped as the Fake does); the default assumption is used only when the section has no answer at all. This follows BMAD's "never invent" rule: a quote is faithful and a model silence is not an excuse to add text.
- The model-backed drafter is covered by stub-client tests with canned JSON; its text is not byte-compared to goldens.
- Dropping `answer` items for a section fed only by a "No sé" topic and the default assumption for an empty section are deterministic safeguards chosen here; the model's quality is still checked in the manual evaluation.
- A Brief assumption or open question carried into the PRD gets a new `A-n` / `Q-n`; owners assigned in the Brief are not copied (they are lost on regeneration, as the baseline says).
- Large initiatives have no Brief in their required set, so the PRD of Large never receives one.

## Out of Scope

Architecture and Epics-and-stories drafters (step 6); attachments sent to the model; fast mode; any change to export, approval, the assign-owners command or the Q-n gate.
