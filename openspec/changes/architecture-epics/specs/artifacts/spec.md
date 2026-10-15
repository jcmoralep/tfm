# Delta for Artifacts (architecture-epics)

**Baseline.** The capability `artifacts` is NOT in `openspec/specs/` yet. The baseline of this delta is `openspec/changes/artifact-generation/specs/artifacts/spec.md` as amended by `openspec/changes/bmad-alignment/specs/artifacts/spec.md` and `openspec/changes/gemini-assistant/specs/artifacts/spec.md` (all three done). BMAD has no archive or global spec merge, so none of them is archived: every MODIFIED requirement below is written in FULL (complete replacement text, same name as in the latest baseline that defines it) and replaces the baseline requirement of the same name; the scenarios of a MODIFIED requirement that are not repeated here stay as they are in the baseline, and only new or changed scenarios are listed. Every requirement of the baselines that is not named here stays as it is. Source requirements: RF-07, RF-15, RF-16, RF-27, RF-36, RF-39, RF-40, RF-43, RF-44, RF-52 to RF-54, RF-57, RF-59, RF-60 to RF-62, RF-64, RF-70 to RF-72, new RF-73 and RF-74, RN-02, RN-03, RN-05 (REQUISITOS.md; RF-71 is the authority for "never invent", RF-73 and RF-74 are added by this change, see the last section). Companion specs: `gemini-assistant` (`gemini-client`, `assistant-provider-configuration`) and, for completion, `initiatives` (this change).

Scenario tags: **[UNIT]** xUnit at validator, handler or pure-class level (hand-written doubles including a stub model client with canned JSON; no bUnit, no network, no key). **[UI]** manual-only check in Chrome. **[DOC]** documentation deliverable checked by reading the file. **[MANUAL]** needs the live key or a scratch BMAD 6.12.1 install; outside CI. Terminology as in the baselines. Added terms: the **upstream chain** is PRD, then Architecture, then Epics-and-stories; the **upstream** of Architecture is the PRD and the upstreams of Epics-and-stories are the PRD and the Architecture. An **AD block** is one architecture decision, rendered as `### AD-n — <title>` followed by its Binds, Prevents and Rule lines. A **story** is `Story N.M` under `Epic N`. A **citation** is an identifier that refers to an element defined elsewhere (as opposed to a **definition**). **Development question** is an open question (`Q-n`) of the section "Preguntas para desarrollo". **Pre-assigned** is defined in "Preguntas para desarrollo". The **content cap** is the baseline 20,000 characters. Message wording is a proposal unless marked verbatim. Numeric caps are proposals that design MAY lower and MUST NOT raise.

## ADDED Requirements

### Requirement: Stale-upstream hint (RF-54, RF-73, RF-74)

When Architecture or Epics-and-stories exists and one of its upstream documents was edited, regenerated or approved after the dependent was last updated, the document page MUST show a non-blocking amber hint in Spanish naming that upstream, for example "El PRD cambió después de generar este documento. Cuando esté aprobado, use Regenerar para actualizarlo." (the wording is `ArtifactLabels.StaleUpstreamNote`; for the Architecture as upstream it reads "La arquitectura cambió ..."). The rule MUST be derived at read time from the `UpdatedAt` timestamps only (no stored flag, no content comparison): the dependent is stale when the `UpdatedAt` of an upstream is later than its own, checking the upstreams in order (the PRD for the Architecture; the PRD, then the Architecture, for the Epics). The document view MUST expose it as `StaleUpstream`, a single `ArtifactType?` holding the first stale upstream (null when none). Consequences, accepted by the user on 2026-10-14 (design decision 3): any change of an upstream raises the hint, even a minor edit; re-approving the upstream does NOT clear it; the hint clears when the dependent itself is edited, regenerated or approved, because each of those actions updates its timestamp (so approving a stale dependent without regenerating it dismisses the advice; the hint is advisory only). The hint MUST NOT block save, approve (beyond the availability rule of "Approval and completion"), regenerate or export, and MUST NOT cause any automatic regeneration or status change in the dependent document (no cascade; cascade invalidation is listed in `docs/mejoras-posteriores.md`). It MUST NOT appear on a dependent document that does not exist, nor on Brief, PRD or Spec (the Brief-to-PRD relation is unchanged).

#### Scenario: Upstream changed after the dependent marks it [UNIT]
- GIVEN a Large initiative with an Approved PRD and a generated Architecture, and the PRD is then edited
- WHEN the Architecture view is built
- THEN `StaleUpstream` is Prd

#### Scenario: The first stale upstream is reported [UNIT]
- GIVEN generated Epics and both the PRD and the Architecture updated after them
- WHEN the Epics view is built
- THEN `StaleUpstream` is Prd (the first upstream in order), and it is Architecture when only the Architecture is newer

#### Scenario: The dependent's own update clears the hint [UNIT]
- GIVEN an Architecture and a PRD updated at the same instant or earlier than it (as after the Architecture is edited, regenerated or approved)
- WHEN the Architecture view is built
- THEN `StaleUpstream` is null

#### Scenario: No dependent, no hint [UNIT]
- GIVEN a Large initiative with a Draft PRD and no Architecture
- WHEN the artifact list is built
- THEN no entry carries a hint

#### Scenario: The hint does not block or cascade [UNIT]
- GIVEN an Architecture with `StaleUpstream` not null
- WHEN the owner saves an edit, exports it, and the PRD is approved again
- THEN the edit and the export succeed, and the Architecture content, version and status are changed only by the edit

#### Scenario: Hint in the page [UI]
- GIVEN an Architecture whose PRD changed after it was drafted
- WHEN the page renders
- THEN a visible notice names the PRD and offers no automatic action

### Requirement: Architecture document layout (RF-73, RF-36, RF-44, RN-02)

The Architecture MUST be Markdown shaped as the BMAD 6.12.1 architecture spine, titled `# Arquitectura: <initiative name>`, with these seven Spanish `##` headings in this order (the sections that the platform cannot fill honestly are not sections, as the spine template allows: the stack, the structural seed and the consistency conventions are Diferido entries): 1 Paradigma de diseño, 2 Invariantes heredadas, 3 Invariantes y reglas, 4 Mapa de capacidades, 5 Diferido, 6 Preguntas para desarrollo, 7 Índice de suposiciones. The document MUST NOT contain a Stack section, a Structural Seed section, a technology name, a version number or a framework that is not written in the approved PRD.

- **Paradigma de diseño** holds the items of the model or, with none, the assumption "El paradigma de diseño lo propone el equipo de desarrollo al revisar este borrador."
- **Invariantes heredadas** is written by code: the PRD identifiers (`FR-n` and `NFR-n`, in PRD order) that the architecture must satisfy, plus the fixed lines "Lo que el PRD deja fuera sigue fuera." and "Las preguntas abiertas del PRD siguen vigentes."; it does not repeat the NFR text. When the PRD has no identifiers the section carries an assumption.
- **Invariantes y reglas** holds the AD blocks. Each MUST be `### AD-n — <title>` with `n` assigned by the composer from 1, in document order, without gaps within a generation (RF-44; the model never writes the id). Each block MUST contain exactly three labelled lines whose bold labels are `**Vincula (binds):**`, `**Previene (prevents):**` and `**Regla (rule):**` (the lowercase-English lint-safe form fixed by design decision 7), so the substrings `binds`, `prevents` and `rule` appear in lowercase and pass the BMAD spine linter whether it is case-sensitive or not. `Vincula (binds)` lists one or more PRD identifiers (`FR-n`, `NFR-n`) separated by commas, in PRD order, and only identifiers defined in the approved PRD received by the drafter may be kept: any other bind MUST be dropped; a block left with no valid bind MUST be dropped. At most 10 blocks are written. A decision with a valid bind that does not fit under the cap is dropped and MUST yield one development question that says how many were omitted. `Previene` and `Regla` are proposals for the development team to confirm, so they ALWAYS carry `[ASSUMPTION: A-n]`, whether or not the PRD states them. With no usable model decision, the Fake rule writes one block per non-functional requirement of the PRD (the title is the requirement text); with none, the section carries one assumption.
- **Mapa de capacidades** MUST be computed by code, never by the model, as a table with the columns `Requisito`, `Qué pide`, `Decisiones`: one row per functional requirement of the PRD in PRD order, the last column listing the AD ids whose binds include it, or "Ninguna todavía" when no block binds it. A requirement with no decision MUST also yield the development question "¿Qué decisión de arquitectura cubre FR-n?" (at most 8, then one summary question for the rest).
- **Diferido** MUST start with two entries written by code, each with its reason: "Tecnología (pila)" (the platform does not know the company's stack and the development team decides it) and "Esqueleto y convenciones" (they depend on the stack), followed by the model's deferred items. The first development question of the document (Q-1) MUST be "¿Qué tecnología (lenguajes, marcos y versiones) usará la solución?". The drafter MUST NOT propose a stack, starter or version. The guarantee that the model names no technology that the PRD does not name is a prompt instruction plus the assumption marker on `Previene` and `Regla`; the AD title and the Paradigma items are not marked individually (limit recorded in `docs/mejoras-posteriores.md`).
- The text MUST contain no `TBD`, `TODO`, `FIXME`, `XXX`, unfilled template token (`<...>`, `{...}`, `{{...}}`) or placeholder row; an AD block whose text contains one has that text replaced by a fixed sentence and yields a development question; a model question or deferred item that contains one is not written.
- Numbering, `[ASSUMPTION: A-n]`, the Índice de suposiciones, the triage suffix, the `#` escaping and the content cap follow the baseline rules; `AD-n` blocks count as items of their section. The question list may exceed the baseline 20-line limit of a section by a few lines in the worst case (stack question, up to 9 requirement questions, one placeholder question, one omission question and up to 10 model questions); it is a safety margin accepted by design.

#### Scenario: Sections and order [UNIT]
- GIVEN a Large initiative with an Approved PRD that defines FR-1 to FR-3 and NFR-1
- WHEN the Architecture is drafted
- THEN the seven headings appear in that order and no Stack heading exists

#### Scenario: AD blocks are numbered by code and lint-safe [UNIT]
- GIVEN a stub reply with three decisions, one titled "AD-9 Persistencia"
- WHEN the Architecture is drafted
- THEN the headings are `### AD-1 — …`, `### AD-2 — …`, `### AD-3 — …` (the forged "AD-9" is neutralized in the title), and each block contains the labels `**Vincula (binds):**`, `**Previene (prevents):**` and `**Regla (rule):**`

#### Scenario: Binds are filtered against the PRD [UNIT]
- GIVEN a PRD defining FR-1, FR-2 and NFR-1, and a decision binding FR-2, FR-9 and NFR-1
- WHEN the Architecture is drafted
- THEN its Binds line is `FR-2, NFR-1`

#### Scenario: A decision with no valid bind is dropped [UNIT]
- GIVEN a decision that binds only FR-9
- WHEN the Architecture is drafted
- THEN no AD block exists for it

#### Scenario: Capability map is computed [UNIT]
- GIVEN AD-1 binding FR-1 and FR-2, and a PRD that also defines FR-3
- WHEN the Architecture is drafted
- THEN the table has the rows `| FR-1 | … | AD-1 |`, `| FR-2 | … | AD-1 |` and `| FR-3 | … | Ninguna todavía |`, and the question "¿Qué decisión de arquitectura cubre FR-3?" exists

#### Scenario: Decisions beyond the cap are asked about [UNIT]
- GIVEN a stub reply with 12 decisions that each bind a valid requirement
- WHEN the Architecture is drafted
- THEN 10 blocks are written and a development question says that 2 decisions were omitted beyond the maximum of 10

#### Scenario: No stack, stack deferred [UNIT]
- GIVEN any drafted Architecture
- WHEN its text is read
- THEN there is no Stack heading, and Diferido contains "Tecnología (pila)" with its reason and the question asking for the stack

#### Scenario: Stack named by the model is not rendered as a fact [UNIT]
- GIVEN a stub reply whose Regla text says "Usar PostgreSQL 16" and the PRD never mentions it
- WHEN the Architecture is drafted
- THEN the line carries `[ASSUMPTION: A-n]` and the Índice de suposiciones lists it; it is never a plain line

#### Scenario: Placeholders are rejected [UNIT]
- GIVEN an item text "TBD: definir la base de datos"
- WHEN the Architecture is drafted
- THEN the text does not appear and a development question exists instead

#### Scenario: Identifier uniqueness [UNIT]
- GIVEN any drafted Architecture
- WHEN its identifiers are extracted
- THEN each AD, A and Q number is defined once, from 1 without gaps, in document order, and the format checker reports no warning

#### Scenario: Architecture golden passes the BMAD spine linter [MANUAL]
- GIVEN the Architecture golden saved as `ARCHITECTURE-SPINE.md` in a scratch BMAD 6.12.1 install
- WHEN `lint_spine.py` runs on it
- THEN it reports no error (check 1 of "Real-BMAD checks of this change")

### Requirement: Epics and stories document layout (RF-74, RF-36, RF-44, RN-02)

The Epics-and-stories document MUST be Markdown shaped as BMAD 6.12.1 `epics.md`, built from the approved PRD and the approved Architecture, titled `# Épicas e historias: <initiative name>`, with these `##` headings in this order: 1 Resumen, 2 Inventario de requisitos (with the `###` parts Requisitos funcionales, Requisitos no funcionales and Requisitos adicionales), 3 Mapa de cobertura de requisitos, 4 Lista de épicas, then the epics, then Preguntas para desarrollo and Índice de suposiciones. Each epic MUST be `## Epic N: <title>` and each story `### Story N.M: <title>`, where the structural words `Epic`, `Story`, `As a`, `I want`, `So that`, `Acceptance Criteria`, `Given`, `When`, `Then` stay in BMAD's English form and the content is Spanish (assumption A1, checked by check 2).

- **Requirements Inventory.** Code MUST copy every `FR-n` and `NFR-n` of the PRD, with its text, hyphenated and with the SAME number (gaps left by edits are kept; nothing is renumbered), as item lines `- **FR-n**: <text>` and `- **NFR-n**: <text>`, and every `AD-n` of the Architecture with its title under Requisitos adicionales (`- **AD-n**: <title>`; BMAD calls these the additional requirements). The model never writes the inventory. Traceability of the chain (RF-44): the Architecture decisions bind `FR-n`/`NFR-n` (Vincula), the Epics inventory lists the `AD-n` as additional requirements, and each story cites the `FR-n`/`NFR-n` it implements (Cubre). A story does not cite `AD-n` itself (a per-story citation is recorded in `docs/mejoras-posteriores.md`).
- **Epics.** Epics MUST be organized by user value, with no forward dependency (a story may rely only on earlier stories; this is a prompt instruction to the model, and the Fake writes flat, independent stories). At most 6 epics, each with at most 5 stories, at most 20 stories in total, each story with at most 3 acceptance criteria and at most 6 cited requirements. Numbering `Epic N` and `Story N.M` is assigned by the composer, from 1 without gaps.
- **Story.** Each story MUST contain, in this order: its heading; one line `**As a** <role>, **I want** <goal>, **So that** <benefit>`; a citation line `**Cubre:** FR-n, NFR-n` (at least one `FR-n`); and `**Acceptance Criteria:**` followed by one-line `- **Given** … **When** … **Then** …` items. Citations MUST be kept only when the identifier is defined in the approved PRD received by the drafter, de-duplicated and in PRD order; others MUST be dropped, and a story left with no valid `FR-n` MUST be dropped. A story that arrives with no criterion receives one default criterion that restates its first cited requirement. Criteria are proposals for the development team to review; they carry no per-criterion source or assumption marker.
- **Coverage Map.** Code MUST compute it from the final stories: one line `- FR-n: Epic N (N.M, N.K)` per covered requirement, in PRD order (several epics comma-separated), and `- FR-n: sin historia todavía (ver Q-n)` for every uncovered one. Every requirement with no story MUST yield the development question "¿Qué historia cubre FR-n?" (identifier only, no requirement text).
- **Budget.** The whole document MUST NOT exceed the content cap. When it would, code MUST apply the shared answer-budget ladder in order until it fits, never cutting mid-structure: (1) shorten item text with an ellipsis (field clip 400, 250, 160, 100, then 60 characters) and keep at most 3, 3, 2, 2, then 1 criteria per story, per rung; (2) after the last rung, drop the trailing stories (last epic first) and then empty epics. The coverage map and the development questions MUST be computed AFTER the ladder, so a dropped story shows up as an uncovered requirement and a question, never as silent loss. Inventory and map lines are never dropped (more than about 120 requirements still ends in the baseline message that the document cannot exceed the limit).
- If the PRD defines no `FR-n`, the document MUST state in the inventory that there are none, contain no epic, and carry one development question about it.
- Assumptions, questions, caps and escaping follow the baseline rules.

#### Scenario: Sections and order [UNIT]
- GIVEN an Approved PRD with FR-1 to FR-4 and NFR-1 and an Approved Architecture with AD-1 and AD-2
- WHEN the Epics are drafted
- THEN the headings appear in the order listed, with `## Epic 1: …` and `### Story 1.1: …`, and the inventory lists AD-1 and AD-2 under Requisitos adicionales

#### Scenario: Inventory keeps hyphenated numbers [UNIT]
- GIVEN a PRD defining FR-1, FR-3 (FR-2 retired) and NFR-1
- WHEN the Epics are drafted
- THEN the inventory contains `- **FR-1**:`, `- **FR-3**:` and `- **NFR-1**:` with the PRD text, no `FR1:`/`NFR1:` form, no renumbering and no FR-2

#### Scenario: Stories have the BMAD shape [UNIT]
- GIVEN any drafted Epics
- WHEN a story is read
- THEN it has the As a / I want / So that line, a Cubre line with at least one FR-n, and at least one Given/When/Then criterion

#### Scenario: Invalid citations are dropped [UNIT]
- GIVEN a story citing FR-1, FR-9, NFR-7 and AD-5 when the PRD defines FR-1 and the Architecture defines AD-1 only
- WHEN the Epics are drafted
- THEN the Cubre line reads FR-1 only (AD-n is never written in a story)

#### Scenario: A story with no valid FR is dropped [UNIT]
- GIVEN a story that cites only FR-9
- WHEN the Epics are drafted
- THEN it does not appear and the numbering of the remaining stories has no gap

#### Scenario: Coverage map and uncovered requirement [UNIT]
- GIVEN stories covering FR-1 and FR-2 and a PRD that also defines FR-3
- WHEN the Epics are drafted
- THEN the map has `FR-1: Epic 1 (1.1)`, `FR-2: Epic 1 (1.2)` and `FR-3: sin historia todavía (ver Q-n)`, and the question "¿Qué historia cubre FR-3?" exists

#### Scenario: Caps hold [UNIT]
- GIVEN a stub reply with 8 epics, 9 stories in one epic and 6 criteria in one story
- WHEN the Epics are drafted
- THEN at most 6 epics, 5 stories per epic and 3 criteria per story are written and every requirement of the dropped stories is in the map as uncovered

#### Scenario: Budget ladder never cuts mid-structure [UNIT]
- GIVEN a PRD with 40 functional requirements and stub texts that would exceed 20,000 characters
- WHEN the Epics are drafted
- THEN the document is at most 20,000 characters, ends with complete sections, every FR is in the map (covered or uncovered), and every uncovered FR has a development question

#### Scenario: PRD without functional requirements [UNIT]
- GIVEN an Approved PRD that defines no FR-n
- WHEN the Epics are drafted
- THEN there is no epic, the inventory says there are no functional requirements and one development question exists

#### Scenario: No forward dependencies [UNIT]
- GIVEN the Fake Epics
- WHEN the stories are read in order
- THEN each story stands alone (one story per functional requirement), so none refers to a later story or epic (for the model path this is a prompt instruction; see `docs/mejoras-posteriores.md`)

#### Scenario: Epics export is accepted by BMAD [MANUAL]
- GIVEN the exported PRD, Architecture and Epics in a scratch BMAD 6.12.1 install
- WHEN `bmad-create-epics-and-stories` step 1 reads them and `bmad-build` or `bmad-spec` reads the epics frontmatter
- THEN the requirements are read without renumbering and the frontmatter is accepted (checks 2 and 3 of "Real-BMAD checks of this change")

### Requirement: Preguntas para desarrollo (RF-39, RF-61, RF-73, RF-74, RN-02)

Architecture and Epics-and-stories MUST each contain a section "Preguntas para desarrollo" that holds every open question of the document and no other section MUST hold `Q-n` lines. Each is a triage line (see "Open-question triage line") whose suffix is **pre-assigned** to the development team: `(Responsable: Equipo de desarrollo; Revisar: antes de construir)`. Because the owner and the revisit text are neither empty nor the defaults, these questions are NOT unassigned, so they do not block approval (RF-62); the person MAY still edit them with the assign command. The questions come from: `unknown` items; the stack question of Diferido (Architecture, always); every requirement left without a decision (Architecture) or without a story (Epics); any item rewritten because of a placeholder; the decisions omitted beyond the cap (Architecture); and the model's own questions (at most 10). When there are none the section says so, as in the baseline (this cannot happen in Architecture, which always has the stack question). The section is the content that RF-39 promises to development: "lo que la persona de producto no pudo responder". The conversation answers and their "No sé" topics MUST NOT feed these two documents (they do not receive the answers).

#### Scenario: Questions are pre-assigned [UNIT]
- GIVEN a drafted Architecture and a drafted Epics
- WHEN their Q-n lines are read by the triage reader
- THEN every line ends with `(Responsable: Equipo de desarrollo; Revisar: antes de construir)` and none is unassigned

#### Scenario: Pre-assigned questions do not block approval [UNIT]
- GIVEN a Draft Architecture whose only questions are pre-assigned
- WHEN the owner approves with the expected version
- THEN it becomes Approved

#### Scenario: Questions only in this section [UNIT]
- GIVEN any drafted Architecture or Epics
- WHEN the `Q-n` definitions are located
- THEN all of them are inside "Preguntas para desarrollo"

#### Scenario: Assigning still works [UNIT]
- GIVEN a drafted Architecture and an assign entry for Q-1 ("TI", "esta semana")
- WHEN the assign command runs with the expected version
- THEN the Q-1 suffix has those values and the document is Draft v+1

#### Scenario: Conversation answers are not used [UNIT]
- GIVEN a conversation with a "No sé" topic and a Large initiative with Approved PRD
- WHEN the Architecture is drafted
- THEN no question derives from that topic and the request carries no answer

### Requirement: Approval copy for development review (RN-02, RF-19, RF-39)

The button MUST remain "Aprobar" for every type. For Architecture and Epics-and-stories, the approval confirmation MUST state, in Spanish, that the document is a draft for the development team to review and that approving means the person reviewed it, not that it is final (the text of `ArtifactLabels.ApproveConfirmText`: "Aprobar lo marca como el borrador que se entrega al equipo de desarrollo. Al aprobarlo usted confirma que lo revisó; no son decisiones técnicas finales y desarrollo lo revisa antes de construir."). The page MUST show the same reminder near the document of these two types. It MUST NOT add a second button, a role, a review state or a new status. The confirmation of the other types is unchanged.

#### Scenario: Confirmation copy by type [UNIT]
- GIVEN the confirmation text provider
- WHEN it is asked for Architecture, Epics, PRD and Brief
- THEN the first two mention that development reviews the draft and that approving means the person reviewed it, and the other two mention neither

#### Scenario: Button label unchanged [UI]
- GIVEN an Architecture Draft page
- WHEN the user opens the approval confirmation
- THEN the button reads "Aprobar" and the confirmation mentions development review

### Requirement: Document pages and Artefactos card for the new types (RF-08, RF-16, RF-55, RF-41)

For Large initiatives the Artefactos card and the document pages MUST treat Architecture ("Arquitectura") and Epics-and-stories ("Épicas e historias") like the other types: state, Generar/Regenerar, edit, assign questions, approve and export, each with its own route. The "not yet supported" state and its placeholder text MUST disappear. While an upstream is not Approved the card MUST show the type as not available with the upstream message of "Availability and explicit generation" instead of a Generar action. The rendered view MUST show AD blocks, epics and stories as readable headings and lists; the page MUST use CSS classes only (no inline style, no JavaScript in views) and the messages are in Spanish.

#### Scenario: Large card lists the three documents [UI]
- GIVEN a Large Planning initiative with an Approved PRD
- WHEN the Artefactos card renders
- THEN PRD shows Aprobado, Arquitectura offers Generar and Épicas e historias shows the upstream message

#### Scenario: Placeholder gone [UNIT]
- GIVEN the artifact list for a Large initiative
- WHEN it is built
- THEN no entry has the not-yet-supported state

#### Scenario: Walkthrough of a Large initiative with the Fake [UI]
- GIVEN a Large initiative driven to the closing with `Assistant:Provider` = Fake
- WHEN the PRD, the Architecture and the Epics are generated and approved in that order
- THEN the initiative reads "Lista para construir" after the third approval and the rendered pages show no format warnings

### Requirement: Export of Architecture and Epics (RF-39, RF-36, RF-21, RN-02)

The existing Markdown exporter MUST support the two new types (it MUST no longer throw for them) and MUST keep its behavior for the others: the stored body is copied unchanged; the exporter adds frontmatter and a blockquote usage note (the stored text keeps no frontmatter). File names: `architecture-<slug>-<date>.md` and `epics-<slug>-<date>.md` (same slug and date rules as the others). The usage note MUST say it is added by the platform, that the document is a draft for the development team's review (RN-02), that the section "Preguntas para desarrollo" lists what product could not answer, and where to save the file and which skill reads it:
- Architecture: save as `planning-artifacts/architecture/architecture-<project>-<date>/ARCHITECTURE-SPINE.md`; next step `bmad-create-epics-and-stories`.
- Epics: save as `planning-artifacts/epics.md`; next steps `bmad-build` or `bmad-spec`, with the PRD and the Architecture as the inputs already exported.

The frontmatter of every export carries `title`, `status`, `created` and `updated`; the Architecture adds only `type: architecture-spine`, and the Epics add `stepsCompleted: ['step-01-validate-prerequisites']` and `inputDocuments: [prd.md, ARCHITECTURE-SPINE.md]`, each with a non-empty value derived deterministically from the stored data (never `TBD` or `TODO`). The other spine keys (name, purpose, altitude, paradigm, scope, binds, sources, companions) are not written: check 1 showed that `lint_spine.py` requires no frontmatter key, and checks 1 and 3 confirmed the values above. Draft documents carry the existing "borrador" sentence.

#### Scenario: File names and notes [UNIT]
- GIVEN an Architecture and an Epics export source
- WHEN each is exported
- THEN the names start with `architecture-` and `epics-`, and each note names its target path and skill

#### Scenario: Body unchanged [UNIT]
- GIVEN the stored Markdown of each type
- WHEN it is exported
- THEN everything after the usage note equals the stored text (trailing whitespace trimmed)

#### Scenario: Frontmatter keys present [UNIT]
- GIVEN an exported Architecture and an exported Epics
- WHEN the frontmatter is parsed
- THEN every key listed above exists with a non-empty value and no other key is written

#### Scenario: Note mentions development review and questions [UNIT]
- GIVEN the usage note of both types
- WHEN read
- THEN each mentions the development team's review and "Preguntas para desarrollo"

#### Scenario: Existing types unchanged [UNIT]
- GIVEN Spec, Brief and PRD sources
- WHEN exported
- THEN the output equals the output before this change

### Requirement: Gemini drafting of the Architecture (RF-73, RF-71, RF-70)

When `Assistant:Provider` = Gemini, the Architecture MUST be drafted by the model-backed drafter through a JSON schema, and the composer MUST render the Markdown. The schema has the object `{paradigm, decisions, deferred, questions}`, all four keys required: `paradigm` and `deferred` are arrays of `{text, source}` items whose source is `assumption` or `unknown` (there are no conversation answers, so a source `answer` is downgraded to an assumption), `decisions` is an array of `{title, binds[], prevents, rule}`, and `questions` is an array of strings. The model MUST NOT write headings, `AD-n`, `A-n`, `Q-n`, the capability map, the Invariantes list, the stack entry or the triage suffix; code writes them. The rules of "Model-backed drafting through schema items" and "Item sources and never inventing" apply (neutralization of forged ids, one-line plain text, default assumption for an empty section, Malformed on an unknown key, a wrong JSON type or a source outside the allowed ones); an empty `decisions` falls back to the Fake rule. The request MUST carry only text: type, depth, initiative name and description and the approved PRD Markdown, and MUST carry no conversation answer, id, owner, email, timestamp or attachment. It MUST be made only when the PRD is Approved (the Availability rules guarantee it; there is no answers-only fallback). One model call per document. The model MUST be told not to name technologies or versions that the PRD does not name. Failures follow "Drafter failures" and store nothing. With `Assistant:Provider` = Fake, the Fake produces the Architecture from the PRD deterministically.

#### Scenario: Composer renders model decisions [UNIT]
- GIVEN a stub model client returning three decisions and items for every flat section
- WHEN the Architecture is drafted
- THEN the Markdown has the seven headings, AD-1 to AD-3, a computed capability map and sequential A and Q numbers

#### Scenario: Model cannot write identifiers [UNIT]
- GIVEN an item "Q-7 ¿Qué base de datos?" and a decision titled "AD-4 Eventos"
- WHEN the Architecture is drafted
- THEN each AD and Q number is defined once from 1 without gaps and the forged texts are neutralized

#### Scenario: Unknown key, wrong type or unknown source is malformed [UNIT]
- GIVEN a stub reply with an unknown key "stack", another whose root is not an object or with a wrong JSON type, and another with an item source "guess"
- WHEN the Architecture is drafted
- THEN each fails as Malformed and nothing is stored

#### Scenario: Request carries the PRD and nothing else [UNIT]
- GIVEN the drafting request for the Architecture
- WHEN its members are inspected
- THEN it holds the PRD Markdown and no answer, id, owner, email, timestamp or attachment

#### Scenario: Failure stores nothing [UNIT]
- GIVEN no Architecture and a model client failing with reason Quota
- WHEN the owner generates it
- THEN no row exists and the quota message is returned

#### Scenario: Fake is the default and deterministic [UNIT]
- GIVEN `Assistant:Provider` = Fake and the fixed PRD of the golden
- WHEN the Architecture is drafted twice
- THEN both equal the stored golden

#### Scenario: Output passes validation with zero warnings [UNIT]
- GIVEN stub and Fake Architectures
- WHEN each is validated and checked by the format parser
- THEN validation succeeds and no format warning is produced

#### Scenario: Architecture invents no stack [MANUAL]
- GIVEN the persona evaluation with a live key and a PRD that names no technology
- WHEN the Architecture is read
- THEN no technology or version appears except as an assumption or a question

### Requirement: Gemini drafting of Epics and stories (RF-74, RF-71, RF-70)

When `Assistant:Provider` = Gemini, Epics-and-stories MUST be drafted through a nested JSON schema: `epics[]` each with `title`, `goal` and `stories[]`, each story with `title`, `as_a`, `i_want`, `so_that`, `covers[]` (FR/NFR ids) and `criteria[]` of `{given, when, then}`, plus `questions[]` (strings); all keys are required. There is no overview, no per-criterion source and no `decisions[]`. The model MUST NOT write `Epic N`, `Story N.M`, headings, the Requirements Inventory, the coverage map, `A-n`, `Q-n` or the triage suffix; code writes and numbers them from validated data. A response MUST be Malformed when the root is not an object, a top-level key is missing or unknown, a field has the wrong JSON type, or no story survives the filtering. An epic left with no story and a story with a blank title or goal or with no valid `FR-n` are dropped (not Malformed); a story with no criteria receives a default criterion. Citation filtering, caps, the shortening ladder, uncovered-FR questions and the 20,000-character cap are those of "Epics and stories document layout" and are applied by code on the parsed response (never trusting the model's own counts). The request MUST carry only the approved PRD Markdown and the approved Architecture Markdown (with the initiative name and description) and no id, owner, email, timestamp, attachment or conversation answer. One model call per document; the whole request MUST stay within what the model accepts (PRD and Architecture are each at most the content cap). Failures follow "Drafter failures". With `Assistant:Provider` = Fake, the Fake produces the Epics deterministically from the PRD and the Architecture: the same observable guarantees (one story per functional requirement under epics grouped by PRD feature, within the caps, citations kept, criteria that restate the requirement, no invented figure or name).

#### Scenario: Nested reply is rendered by the composer [UNIT]
- GIVEN a stub reply with 2 epics, 3 stories and criteria
- WHEN the Epics are drafted
- THEN Epic 1, Epic 2 and Story 1.1 to 2.x exist with Given/When/Then criteria, the inventory and the map come from the PRD and Architecture, not from the reply

#### Scenario: Model cannot write numbering [UNIT]
- GIVEN an epic titled "Epic 9: Pagos" and a story titled "Story 3.7 Cobro"
- WHEN the Epics are drafted
- THEN the headings are numbered by the composer and the forged numbers are neutralized in the titles

#### Scenario: Structurally invalid reply is malformed [UNIT]
- GIVEN a reply whose root is not an object, one with an unknown or missing top-level key, one with a wrong JSON type, and one whose stories all lack a valid requirement
- WHEN the Epics are drafted
- THEN each fails as Malformed and nothing is stored, while an epic without stories or a story without a valid requirement alone is dropped and the rest is kept

#### Scenario: Coverage is computed from the final document [UNIT]
- GIVEN a reply whose stories cover FR-1 only while the PRD defines FR-1 and FR-2
- WHEN the Epics are drafted
- THEN the map shows FR-2 as uncovered and a development question names FR-2

#### Scenario: Request has no answers or identifiers [UNIT]
- GIVEN the drafting request for the Epics
- WHEN its members are inspected
- THEN it holds the PRD and Architecture Markdown only, with no id, owner, email, timestamp, attachment or answer

#### Scenario: Failed regeneration changes nothing [UNIT]
- GIVEN an Approved Epics v2 and a model client failing with reason Timeout
- WHEN the owner regenerates with confirmation
- THEN the Epics stay v2 Approved with the same content and the timeout message is returned

#### Scenario: Fake Epics equal the golden [UNIT]
- GIVEN `Assistant:Provider` = Fake and the fixed PRD and Architecture of the golden
- WHEN the Epics are drafted
- THEN the Markdown equals the stored golden and has zero format warnings

#### Scenario: Large initiative end to end with a live key [MANUAL]
- GIVEN `Assistant:Provider` = Gemini and a real key
- WHEN a Large initiative is walked in Chrome from the conversation to the three approved documents and their export
- THEN every document renders without format warnings, every FR is in a story or a development question, and `Logs/` holds no content

### Requirement: Real-BMAD checks of this change (docs and manual)

`docs/bmad-referencia.md` MUST record, when the corresponding slice closes and with the result and the date, three checks in a disposable BMAD 6.12.1 install: (1) `lint_spine.py` passes on the Architecture golden saved as `ARCHITECTURE-SPINE.md`; (2) `bmad-create-epics-and-stories` step 1 reads the exported PRD and Architecture, reads `FR-n`/`NFR-n` without renumbering and accepts the hyphenated inventory, the Spanish content and the English structural keywords; (3) `bmad-build` or `bmad-spec` accepts the exported epics frontmatter. A failed check MUST be recorded with the observed behavior and MUST trigger a fix before the change is closed. The document MUST also state that the web-verified stack or starter recommendation of BMAD is not implemented and is listed in `docs/mejoras-posteriores.md`.

#### Scenario: Checks recorded [DOC]
- GIVEN `docs/bmad-referencia.md` after the last slice
- WHEN read
- THEN each of the three checks has the steps, the observed result and the date, and the improvement is listed in `docs/mejoras-posteriores.md`

## MODIFIED Requirements

### Requirement: Required set per level (RF-07, RF-27, RF-40, RF-51)

The system MUST define the required set from the initiative's depth as data: Small requires Spec; Standard requires Brief and PRD; Large requires PRD, Architecture and Epics-and-stories (all three can now be generated). Initiatives with empty depth (Automatic, not yet resolved) MUST have an empty required set and no artifact MUST be available. A type outside the required set of the initiative's current level MUST be rejected for generate, regenerate, save and approve.
(Previously: Large's required set contained types that could not yet be generated.)

#### Scenario: Required set per level [UNIT]
- GIVEN the depth levels
- WHEN the required set of each is requested
- THEN Small yields Spec, Standard yields Brief and PRD, Large yields PRD, Architecture and Epics-and-stories

#### Scenario: Architecture is rejected outside Large [UNIT]
- GIVEN a Standard initiative
- WHEN the owner generates an Architecture
- THEN it is rejected with "Este documento no corresponde al nivel de la iniciativa." and nothing is stored

### Requirement: Availability and explicit generation (RF-53, RF-58, RF-73, RF-74)

Generation MUST be an explicit user action ("Generar"); nothing MUST be generated automatically, on any page load, on any status change or on conversation progress. Availability MUST be evaluated against the initiative's status, the conversation coverage obtained through the Assistant module (never by reading Assistant data directly) and, for the upstream chain, the status of the upstream documents:
- Brief (Standard): available as soon as every required Aclarar topic of the level is covered (the confirmation step is not a topic), while the initiative is Clarifying or Planning.
- Spec (Small) and PRD (Standard, Large): available only at the closing, that is, when the initiative is Planning and every required topic of the level, Aclarar and Planificar, is covered.
- Architecture (Large): available only while the initiative is Planning and the PRD of the initiative exists and is **Approved**.
- Epics-and-stories (Large): available only while the initiative is Planning and both the PRD and the Architecture exist and are **Approved**; the upstreams are checked in order (the PRD first, then the Architecture), and the first one not approved gives the waiting text. A PRD back in Draft after the Architecture was approved therefore blocks generating or regenerating the Epics until it is approved again.
- In Draft nothing is available. In ReadyToBuild generated documents can be read, edited and regenerated where their availability holds (see their requirements), but no new document is generated because the set was already complete.

Availability MUST be a pure function of the initiative's status, depth, the conversation coverage and the statuses of the upstream documents, supplied to it by the caller. Rejections (wording is a proposal): Draft, "Termine de crear la iniciativa para generar documentos."; topics uncovered, "Aún no se puede generar este documento: termine primero las preguntas del asistente."; Architecture without an approved PRD, "Primero apruebe el PRD para generar este documento."; Epics without an approved Architecture, "Primero apruebe la arquitectura para generar las épicas e historias."; document already generated, "El documento ya existe. Use Regenerar para reemplazarlo.". The order of the assistant's script MUST NOT change because of generation. Approving, regenerating and saving a dependent document follow the same availability (see their requirements).
(Previously: Architecture and Epics-and-stories had no availability rule, and availability did not depend on other documents.)

#### Scenario: Architecture needs an approved PRD [UNIT]
- GIVEN a Large Planning initiative with the PRD in Draft
- WHEN the owner generates the Architecture
- THEN it is rejected with "Primero apruebe el PRD para generar este documento." and nothing is stored

#### Scenario: Architecture available after the PRD is approved [UNIT]
- GIVEN the same initiative after the PRD is approved
- WHEN the availability of Architecture and Epics is evaluated
- THEN Architecture is available and Epics is not

#### Scenario: Epics need an approved Architecture [UNIT]
- GIVEN a Large Planning initiative with an Approved PRD and a Draft Architecture
- WHEN the owner generates the Epics
- THEN it is rejected with "Primero apruebe la arquitectura para generar las épicas e historias."

#### Scenario: Chain in order [UNIT]
- GIVEN a Large Planning initiative with no document
- WHEN the PRD, the Architecture and the Epics are generated and approved in that order
- THEN each generation succeeds only after the previous document is approved

#### Scenario: Regenerating the PRD does not touch dependents [UNIT]
- GIVEN an Approved PRD, an Approved Architecture and Draft Epics, and the PRD regenerated with confirmation
- WHEN the stored documents are read
- THEN the Architecture and Epics are unchanged and only the PRD is Draft v+1

#### Scenario: Architecture unavailable once ReadyToBuild [UNIT]
- GIVEN a ReadyToBuild Large initiative
- WHEN a generate for a missing document is sent
- THEN it is rejected as already complete and no document is created

(The scenarios of the baseline for Brief, Spec, PRD, Draft initiative, no automatic generation, generate twice, two tabs and script order stay unchanged.)

### Requirement: Approval and completion (RF-07, RF-19, RF-52, RF-59, RF-62, RN-01, RN-02, RN-06, RN-08)

Approving MUST be a manual action of the owner on a Draft document with the expected version; it MUST set status Approved and the approval time, MUST NOT change the version and MUST NOT depend on the format warnings (a document with warnings can be approved; the warnings are shown in the confirmation). It MUST depend on the open-question gate: a Draft document with any unassigned open question MUST be rejected (see "Approval gate on open questions"), which is not a format warning; the pre-assigned development questions of Architecture and Epics-and-stories are not unassigned. Approving MUST also require that the coverage of the document still holds (it is available per the Availability rules, including the approved upstream), so undoing a Planificar answer after generating, or returning the PRD to Draft, takes the approval of the PRD or of its dependents away until the condition holds again; it is rejected with the existing "topics uncovered" text, or with the upstream text of "Availability and explicit generation" for Architecture and Epics, and the initiative MUST NOT be completed while any document of the level is uncovered. Approving an Approved document at the same version MUST succeed without changes (idempotent) while the coverage holds, without evaluating the open-question gate. When, after an approval, every type of the required set of the initiative's level is Approved, the system MUST mark the initiative ReadyToBuild through the `initiatives` idempotent operation; otherwise the initiative status MUST NOT change. A Large initiative therefore reaches ReadyToBuild only when PRD, Architecture and Epics-and-stories are all Approved, in whatever order their approvals happen to complete after the chain rules. If marking ready fails after the approval was stored, the approval MUST stay and a later approve or completion check MUST retry the transition (the retry is never blocked by the gate). No document MUST be Approved by any path other than this action. The confirmation of the approval of Architecture and Epics-and-stories MUST follow "Approval copy for development review".
(Previously: Large never reached ReadyToBuild because Architecture and Epics could not exist, and approval had no upstream condition or review copy.)

#### Scenario: Large completes with the three documents [UNIT]
- GIVEN a Large Planning initiative with Approved PRD and Approved Architecture and a Draft Epics
- WHEN the Epics are approved
- THEN the initiative becomes ReadyToBuild

#### Scenario: Partial Large set does not complete [UNIT]
- GIVEN a Large Planning initiative with Approved PRD and Architecture and no Epics
- WHEN completion is evaluated
- THEN the initiative stays Planning

#### Scenario: Approving a dependent with a Draft upstream is rejected [UNIT]
- GIVEN an Approved Architecture whose PRD was edited (Draft) and an Architecture edit that is Draft
- WHEN the Architecture is approved
- THEN it is rejected with "Primero apruebe el PRD para generar este documento." and stays Draft

#### Scenario: Editing the PRD reopens the initiative, re-approving completes [UNIT]
- GIVEN a ReadyToBuild Large initiative whose PRD is edited and then approved again
- WHEN each step completes
- THEN the initiative is Planning after the edit and ReadyToBuild after the re-approval, with Architecture and Epics unchanged

#### Scenario: Pre-assigned questions do not reject approval [UNIT]
- GIVEN a Draft Epics whose questions are all pre-assigned
- WHEN the owner approves it
- THEN it becomes Approved

(The scenarios of the baseline for Standard, Small, partial Standard, order, warnings and the question gate stay unchanged; the baseline scenario "Large never completes in this change" is removed.)

### Requirement: Generated content structure per document (RF-16, RF-36, RF-38, RF-40, RF-44, RF-61, RF-64, RF-71, RF-73, RF-74)

`IArtifactDrafter` MUST produce the Markdown for a type from the initiative's name and description, its depth and either the conversation answers (Brief, PRD, Spec) or the approved upstream documents (Architecture, Epics-and-stories). Each generated document MUST contain these sections, in this order, with these Spanish headings:

- **Brief**: Resumen ejecutivo, El problema, La solución, Qué lo hace distinto, A quién sirve, Criterios de éxito, Alcance, Visión, Índice de suposiciones, Preguntas pendientes.
- **PRD**: 0 Propósito del documento, 1 Visión, 2 Usuario objetivo (2.1 Trabajos por hacer, 2.2 No usuarios, 2.3 Recorridos clave), 3 Glosario, 4 Funcionalidades, 5 No objetivos, 6 Alcance del MVP (6.1 Dentro, 6.2 Fuera), 7 Métricas de éxito, 8 Preguntas abiertas, 9 Índice de suposiciones.
- **Spec**: Por qué, Capacidades, Restricciones, No objetivos, Señal de éxito, Índice de suposiciones, Preguntas abiertas.
- **Architecture**: 1 Paradigma de diseño, 2 Invariantes heredadas, 3 Invariantes y reglas, 4 Mapa de capacidades, 5 Diferido, 6 Preguntas para desarrollo, 7 Índice de suposiciones (see "Architecture document layout").
- **Epics-and-stories**: 1 Resumen, 2 Inventario de requisitos (Requisitos funcionales, Requisitos no funcionales, Requisitos adicionales), 3 Mapa de cobertura de requisitos, 4 Lista de épicas, `## Epic N: …` with `### Story N.M: …`, Preguntas para desarrollo, Índice de suposiciones (see "Epics and stories document layout").

Numbering MUST follow RF-44 with a hyphen and a number for every element, numbered in document order from 1 and without gaps within the generation: functional requirements `FR-n` (Capacidades of the Spec and Funcionalidades of the PRD, one per capability item), non-functional `NFR-n`, success metrics `SM-n` (Criterios de éxito of the Brief, Métricas de éxito of the PRD, Señal de éxito of the Spec), architecture decisions `AD-n` (one per AD block, assigned by the composer), assumptions `A-n` (inline as `[ASSUMPTION: A-n]` followed by its text), pending questions `Q-n`; epics and stories use `Epic N` and `Story N.M` (BMAD's format), assigned by the composer. In Architecture and Epics-and-stories the `FR-n`, `NFR-n` and `AD-n` that come from the upstream documents are **citations that keep the upstream number** (never renumbered, gaps included); only the elements they define themselves (`AD-n` in the Architecture, `A-n`, `Q-n`, `Epic N`, `Story N.M`) are numbered from 1. User journeys `UJ-n` are part of the scheme and valid in edited documents, but the generator MUST NOT write any. For Brief, PRD and Spec each answer MUST be split into one item per non-empty line (list markers removed); an answer on a single line yields one item. At most 20 items are written per answer (per section for the model-backed drafter), followed by one plain line "y N más." when there were more, so many tiny lines cannot exceed the content cap. A "nothing" quick reply ("Ninguno", "Ninguno que yo sepa", "Nada por ahora", "Nada especial") MUST become one explanatory sentence without an identifier (for example "No se indicaron integraciones."), never an FR or NFR; the other quick replies (for example "Ambos") keep their label as text. The Fake drafter MUST use the user's own words for the items; the model-backed drafter MAY tidy the wording into plain Spanish but each item MUST stay faithful to the answers (or, for Architecture and Epics, to the approved upstream text) and carry its `source`. Neither MUST present invented facts as certain.

Mapping of answers to sections (the sections not listed take an assumption):
- Brief: idea feeds Resumen ejecutivo and La solución; problem feeds El problema; users feeds A quién sirve; success feeds Criterios de éxito (`SM-n`); out of scope feeds Alcance (outside); Qué lo hace distinto, Visión and the inside of Alcance carry `[ASSUMPTION: A-n]`.
- PRD: idea and initiative name feed Propósito and Visión; users and problem feed Trabajos por hacer; Recorridos clave is not fed by any answer and holds the single assumption of "PRD key journeys section without markers" (no `UJ-n`, whatever the users answer); capabilities feed Funcionalidades (`FR-n`) and Alcance dentro (the line "Todas las funcionalidades de la sección 4.", annotated with the priorities answer when present); constraints and qualities feed `NFR-n` items under a "Requisitos no funcionales" feature of Funcionalidades; integrations and teams (Large) feed `FR-n` items under an "Integraciones" feature, each phrased as a requirement ("Debe integrarse con <sistema o equipo>"); out of scope feeds No objetivos and Alcance fuera; success feeds Métricas (`SM-n`). Glosario and No usuarios carry `[ASSUMPTION: A-n]` when nothing feeds them.
- Spec: idea, users and problem feed Por qué (the users go in one bullet, "Quién lo usará", joined by semicolons); capabilities feed Capacidades (`FR-n`); out of scope feeds No objetivos (at least one item); success feeds Señal de éxito (`SM-n`); Restricciones carries `[ASSUMPTION: A-n]` because Small asks no constraints topic.
- Architecture: the approved PRD feeds Invariantes heredadas (its identifiers) and the capability map (its `FR-n`); its `FR-n` and `NFR-n` are the only identifiers an AD block may bind; Paradigma de diseño (when the model gives no item) and the `Previene`/`Regla` text of each AD block carry `[ASSUMPTION: A-n]`; Diferido always holds the stack entry; Preguntas para desarrollo holds every `Q-n`.
- Epics-and-stories: the approved PRD feeds the inventory (Requisitos funcionales and no funcionales); the approved Architecture feeds Requisitos adicionales; the PRD's `FR-n` group by feature into epics and into stories (Fake) or the model groups them by user value; Resumen and the map are computed.

Every section with no feeding content MUST carry at least one `[ASSUMPTION: A-n]` marker instead of being left empty (the computed sections, the inventory and the map excepted). The Índice de suposiciones MUST list every `A-n` of the document with its text, and Preguntas pendientes / Preguntas abiertas / Preguntas para desarrollo MUST list every `Q-n`, each line ending with the triage suffix of "Open-question triage line" (default `(Responsable: por asignar; Revisar: por definir)`; pre-assigned for Architecture and Epics-and-stories).
(Previously: only Brief, PRD and Spec existed; identifiers were always defined by the document that contains them.)

#### Scenario: Architecture and Epics order [UNIT]
- GIVEN a Large Approved PRD and Approved Architecture
- WHEN the Architecture and the Epics are drafted
- THEN the headings of each appear in the order listed above

#### Scenario: Citations keep upstream numbers [UNIT]
- GIVEN a PRD defining FR-1, FR-3 and NFR-2
- WHEN the Architecture and the Epics are drafted
- THEN every FR and NFR in them uses those numbers and none is renumbered

#### Scenario: Numbers are unique and ordered [UNIT]
- GIVEN any generated document
- WHEN its identifiers are extracted
- THEN each element the document defines (FR, NFR, SM, AD, A, Q, Epic, Story) appears once as a definition, numbered from 1 without gaps in document order for the numbers the document owns, and no UJ is defined

(The other scenarios of the baseline stay.)

### Requirement: Deterministic fake drafter (RF-15)

`IArtifactDrafter` MUST keep a fake implementation that is a pure function of its request: equal requests MUST yield equal Markdown. It is the default (`Assistant:Provider` = Fake). It MUST NOT use the clock, randomness or I/O and MUST NOT send data outside the process. The request MUST carry only text and level data (type, depth, initiative name and description, the answers as topic key, text and don't-know flag, the optional approved Brief text, and, for Architecture and Epics-and-stories, the approved PRD Markdown and, for Epics, the approved Architecture Markdown) and MUST NOT carry ids, owner, email or timestamps; this holds for every implementation. The fake MUST ignore the Brief text; for Architecture and Epics-and-stories it MUST build from the upstream Markdown, reading the PRD identifiers and features with a pure parser. The generated content MUST NOT exceed the content cap; to ensure it, quoted text that would make the document exceed the cap MUST be shortened with an ellipsis (Epics also apply the ladder of their layout). The generated Markdown MUST be accepted by the same validation that applies to saved edits. The goldens of Spec, Brief and PRD stay unchanged; the Architecture and Epics have their own goldens built from a fixed PRD and Architecture.
(Previously: the request had no upstream Markdown other than the Brief and the goldens covered three types.)

#### Scenario: Architecture and Epics goldens [UNIT]
- GIVEN the fixed PRD and Architecture of the goldens
- WHEN each document is drafted
- THEN each equals its stored golden, twice in a row

#### Scenario: Existing goldens unchanged [UNIT]
- GIVEN the fixed conversations of the Spec, Brief and PRD goldens
- WHEN each is drafted
- THEN each equals its stored golden

### Requirement: Model-backed drafting through schema items (RF-71, RF-15, RF-16, RF-73, RF-74)

When `Assistant:Provider` = Gemini, `IArtifactDrafter` MUST be implemented by a drafter in which the model returns JSON per section of the document type, and the composer renders the Markdown. The model MUST NOT write Markdown, headings, identifiers (`FR-n`, `NFR-n`, `SM-n`, `AD-n`, `A-n`, `Q-n`, `Epic N`, `Story N.M`), assumption markers or the triage suffix; the composer MUST produce them. The drafter MUST therefore keep every guarantee of the baseline: the outline and Spanish headings, sequential ids from one numbering pass, `[ASSUMPTION: A-n]` plus the Índice de suposiciones, every `Q-n` line ending with its triage suffix, at most 20 items per section followed by "y N más.", `#` escaping, the 20,000-character cap (item text is shortened with an ellipsis, as the Fake does), no frontmatter, acceptance by the same validation as saved edits, and zero format warnings. Item text MUST be one line of plain Spanish: line breaks are collapsed, list markers and Markdown are removed, and any text that looks like an identifier definition or an `[ASSUMPTION` marker MUST be neutralized so a model cannot forge ids or index entries. Covered types: Spec, Brief and PRD (flat items per section), Architecture (flat items plus decisions, see "Gemini drafting of the Architecture") and Epics-and-stories (nested, see "Gemini drafting of Epics and stories"). The structural content of "Recorridos clave" (RF-64) and the headings stay composer-owned. The request to the model MUST carry only text and level data (type, depth, initiative name and description and, for the Spec, Brief and PRD, the answers with topic key, text and don't-know flag, and, for the PRD, the approved Brief text; for Architecture and Epics-and-stories the approved upstream Markdown instead of answers) and no id, owner, email or timestamp. With `Assistant:Provider` = Fake the Fake drafter and its goldens are unchanged.

A response whose JSON names a section that is not in the outline, has an item with a `source` outside `answer|assumption|unknown`, or lacks the required members MUST be treated as Malformed. A section returned with no items MUST receive the same default assumption the Fake uses for an unfed section.
(Previously: covered types were Spec, Brief and PRD only and the request carried only answers and the Brief.)

Scenarios of the baseline stay unchanged; the new scenarios of the two requirements above cover the new types.

### Requirement: Format warnings from the Markdown (RF-43, RF-44, RF-56, RF-59, RF-73, RF-74)

The system MUST derive format warnings by reading the Markdown on every read and every save, returning at most 50 of them followed by one final entry "y N avisos más." when there are more; a token without a hyphen ("Q4", "A4", "SM2") followed by prose is an ordinary word and MUST NOT be reported, while "FR1", "NFR1", "UJ1", "AD1" and any hyphenated attempt ("FR -1", "FR-", "FR-01", "fr-1", "NFR-x", "AD-") are; warnings MUST never block saving or approving and MUST never alter or lose text. The parser MUST detect, per document type: a required section heading missing; an identifier of the kinds FR, NFR, UJ, SM, AD, A or Q written without hyphen or number (for example "FR1", "FR-", "NFR-x") where a definition is expected; the same identifier defined twice; an `[ASSUMPTION: A-n]` marker whose A-n is absent from the Índice de suposiciones, or an index entry without a marker in the text; a Q-n or A-n numbered with zero; in the Architecture, an `AD-n` heading defined twice, numbered zero or written without a hyphen. The following checks were descoped by design decision 7 and are NOT warnings (recorded in `docs/mejoras-posteriores.md`): an `AD-n` heading out of order, an AD block that lacks one of its three labels, and an Epics document with a functional requirement in the inventory but no `## Epic N` heading. **Where definitions live.** `FR-n`, `NFR-n`, `SM-n` and `UJ-n` are definitions only in the documents that own them (Brief, PRD, Spec) and, for `FR-n`/`NFR-n`, in the Requirements Inventory of the Epics; `AD-n` is a definition only as an `### AD-n —` heading of the Architecture; in every other place (the Architecture's map and binds, the Epics' coverage map, `Cubre`/`Decisiones` lines, inventory 2.3, story text) they are citations and MUST NOT be reported as duplicates or as malformed definitions. `A-n` and `Q-n` are definitions in every type. The rule that numbers are never reused or reordered on edits (RF-44) cannot be verified without history and MUST NOT be reported as a warning. An unassigned open question, a missing or malformed `(Responsable: …; Revisar: …)` suffix and the default suffix are NOT format warnings (they are handled by the approval gate, RF-59/RF-62). Warnings MUST be in Spanish, name the kind of problem and the identifier or heading, quote a short excerpt of the offending line (about 50 characters, without markup characters) instead of a line number, and be returned in document order. The structured index (items table, states) is out of scope.
(Previously: `AD` was not a known kind, and every `FR-n` occurrence in a definition position was a definition.)

#### Scenario: Generated Architecture and Epics have no warnings [UNIT]
- GIVEN a drafted Architecture and Epics (Fake and stub-model)
- WHEN the parser reads each
- THEN each has zero warnings, although `FR-n` appears in the map, in binds and in citations

#### Scenario: Coverage map does not duplicate the inventory [UNIT]
- GIVEN an Epics document whose inventory defines FR-1 and whose map says `- FR-1: Epic 1`
- WHEN the parser reads it
- THEN no duplicate FR-1 is reported

#### Scenario: Malformed AD identifier [UNIT]
- GIVEN an Architecture heading `### AD3 — Eventos`
- WHEN the parser reads it
- THEN a warning names the malformed identifier

#### Scenario: Duplicate AD heading [UNIT]
- GIVEN an Architecture with two headings `### AD-1 — …`
- WHEN the parser reads it
- THEN a warning names AD-1 as defined twice

#### Scenario: Missing Epics section [UNIT]
- GIVEN an Epics document from which "Mapa de cobertura de requisitos" was deleted
- WHEN it is saved
- THEN the save succeeds, the warning names the section and the text is kept

(The other scenarios of the baseline stay.)

### Requirement: Regeneration behind confirmation (RF-17, RF-57, RF-60, RN-07, RF-73, RF-74)

Regenerating MUST replace the whole content, including manual edits, with a new draft from the current conversation (Brief, PRD, Spec) or from the upstream documents as currently stored (Architecture, Epics-and-stories), increase the version by one, set status Draft and clear the approval time. It MUST be possible only for a generated document whose type is available (for Architecture and Epics, with its upstream Approved), and only after the user confirms in a panel that explains that manual changes will be lost; the command MUST carry an explicit confirmation flag and without it MUST be rejected with "Confirme que desea reemplazar el documento." and change nothing. The new document MUST be numbered from scratch (RF-60), and every open question MUST come back with the suffix the generator writes: the default `(Responsable: por asignar; Revisar: por definir)` for Brief, PRD and Spec, and the pre-assigned development suffix for Architecture and Epics-and-stories. Assigned owners are lost together with the manual edits, and the confirmation panel MUST say so. Regenerating a document of a ReadyToBuild initiative MUST reopen it to Planning like an edit does. Regenerating MUST NOT regenerate, edit or change the status of any other document (no cascade).
(Previously: every open question came back with the default suffix; the new types did not exist.)

#### Scenario: Regenerating the Architecture resets its questions to the pre-assigned suffix [UNIT]
- GIVEN an Architecture whose Q-1 was reassigned to "TI"
- WHEN it is regenerated with confirmation
- THEN its Q-n lines end with `(Responsable: Equipo de desarrollo; Revisar: antes de construir)`

#### Scenario: Regenerating needs an approved upstream [UNIT]
- GIVEN an Architecture and a PRD back in Draft
- WHEN the owner regenerates the Architecture with confirmation
- THEN it is rejected with "Primero apruebe el PRD para generar este documento." and nothing changes

#### Scenario: No cascade [UNIT]
- GIVEN an Approved Architecture and Draft Epics
- WHEN the Architecture is regenerated with confirmation
- THEN the Epics' content, version and status are unchanged

(The other scenarios of the baseline stay.)

### Requirement: Open-question triage line (RF-61, RF-43, RF-20, RN-03)

Every open question MUST be one Markdown list line `- **Q-n**: <text> (Responsable: <owner>; Revisar: <revisit>)`, so the Markdown stays the only source of truth (no table, no migration). The suffix is read only at the END of the line (trailing whitespace ignored); everything before it is the question text, which MAY itself contain parentheses. Questions generated by the platform MUST end with the default `(Responsable: por asignar; Revisar: por definir)`, except the development questions of Architecture and Epics-and-stories, which MUST end with the pre-assigned `(Responsable: Equipo de desarrollo; Revisar: antes de construir)` (see "Preguntas para desarrollo").

A pure reader of the Markdown MUST return, in document order, for each open question: id, text, owner, revisit text and whether it is unassigned. It MUST ignore fenced code blocks and the assumption index, and MUST recognise a definition the same way the format checker does (optional list marker, optional bold, valid hyphenated number from 1). A question is **unassigned** when the suffix is missing or malformed, or the owner is empty (after trimming) or equals `por asignar`, or the revisit text is empty or equals `por definir` (comparison ignores case and surrounding spaces). Both data are required: one of them unassigned makes the question unassigned.

Written values (by the assign command) MUST obey: owner at most 60 characters after trimming and revisit text at most 80 characters after trimming, both with inner whitespace collapsed and containing none of `(`, `)`, `;`, `[`, `]`, `*`, `|`, `\` or a line break (one rule for both fields). Suggested owner areas offered to the user: Legal, TI, Finanzas, Quien propone la iniciativa; any other text is accepted. No phase flag, blocking flag or question state exists.
(Previously: every platform-generated question ended with the default suffix.)

#### Scenario: Pre-assigned suffix is read as assigned [UNIT]
- GIVEN the line `- **Q-1**: ¿Qué stack usará el equipo? (Responsable: Equipo de desarrollo; Revisar: antes de construir)`
- WHEN the reader runs
- THEN it returns owner "Equipo de desarrollo", revisit "antes de construir", assigned

(The other scenarios of the baseline stay.)

### Requirement: Identifier scheme and BMAD reconciliation (RF-36, RF-44)

Every identifier in every generated document MUST use the hyphenated scheme (`FR-n`, `NFR-n`, `UJ-n`, `SM-n`, `AD-n`, `A-n`, `Q-n`), as BMAD's own PRD template does. The same number MUST appear everywhere the element is cited. **This includes the requirements inventory of `epics.md`**: it keeps `FR-n` and `NFR-n` hyphenated with the SAME number as the PRD (gaps left by user edits are kept), because in the real BMAD 6.12.1 run of 2026-10-14 `bmad-create-epics-and-stories` read the hyphenated identifiers without renumbering and the unhyphenated `FR3:` form is a platform choice, not a BMAD requirement. The step 6 decision therefore reverses the earlier plan to write `FR1:`/`NFR1:`. `NFR-n` is a platform extension because BMAD's PRD does not number non-functional requirements; `FR-n` replaces BMAD's `CAP-n` in the Spec. The export copies the PRD unchanged. The system MUST add no identifier mapper or converter code. The decision is recorded in REQUISITOS.md (RF-36, RF-44), AGENTS.md section 6 item 3 and `docs/bmad-referencia.md` section 4, which MUST agree.
(Previously: the epics inventory wrote `FR3:`/`NFR3:` with the same number, through a conversion to be built in step 6.)

#### Scenario: Only hyphenated identifiers are generated [UNIT]
- GIVEN drafted Brief, PRD (Standard and Large), Spec, Architecture and Epics
- WHEN their text is scanned for FR, NFR, UJ, SM or AD followed directly by digits
- THEN there is none

#### Scenario: No mapper exists [DOC]
- GIVEN the Artifacts module sources
- WHEN searched for a converter between `FR-n` and `FRn`
- THEN none exists

#### Scenario: Documents agree [DOC]
- GIVEN REQUISITOS.md RF-36 and RF-44, AGENTS.md section 6 item 3 and `docs/bmad-referencia.md` section 4
- WHEN compared
- THEN all state that the epics inventory keeps the hyphenated `FR-n`/`NFR-n` with the same number, and `NFR-n` as a platform extension, and none mentions `FR1:` as a decision to build

### Requirement: Real-BMAD verification checklist (docs only)

`docs/bmad-referencia.md` MUST contain a checklist of the checks that need a real BMAD run, WITHOUT running BMAD in the slice that writes it. Each item MUST state how to run it in a disposable BMAD install, the expected result, and where the result is recorded. Items: (1) `bmad-build` accepts a `SPEC.md` produced for the Small level; (2) the epics step (`bmad-create-epics-and-stories`) reads `FR-n` requirements of an exported PRD and keeps the hyphenated `FR-n`/`NFR-n` with the same numbers in its inventory; (3) BMAD tolerates Spanish headings and the `(Responsable: …; Revisar: …)` suffix in the PRD. The checks specific to the Architecture and Epics documents are those of "Real-BMAD checks of this change".
(Previously: item 2 expected the inventory to be written as `FR1:`/`NFR1:`.)

#### Scenario: Checklist item 2 agrees with the decision [DOC]
- GIVEN `docs/bmad-referencia.md`
- WHEN item 2 is read
- THEN it expects the hyphenated inventory with the same numbers

## Assumptions

- A1. The English structural words of BMAD (`Epic`, `Story`, `As a`, `I want`, `So that`, `Given`, `When`, `Then`) and the AD labels (Spanish word plus the lowercase English word in parentheses: `Vincula (binds)`, `Previene (prevents)`, `Regla (rule)`) are the closest form to BMAD that still reads Spanish; check 2 confirms BMAD reads them. If it does not, the labels are adjusted in design.
- A2. The stale-upstream hint depends only on `UpdatedAt` timestamps (see "Stale-upstream hint"): it cannot tell a typo from a real change, and approving or editing the dependent clears it. Automatic invalidation is out of scope.
- A3. Architecture requirements left without an AD block yield a development question, by analogy with the epics rule (the proposal states the rule only for Epics). It adds at most 9 lines (8 questions and one summary question).
- A4. The numeric caps (10 AD blocks, 6 epics, 5 stories per epic, 20 stories, 3 criteria per story, 6 cited requirements per story, 10 model questions) are the final values fixed by design decision 11, inside the 20,000-character limit.
- A5. The Large required set is three documents (PRD, Architecture, Epics-and-stories). The proposal's wording "five approved documents" counts the Brief and the Spec of the other levels and is not a requirement of this spec.
- A6. The pre-assigned suffix is `Equipo de desarrollo` / `antes de construir`; the wording is a proposal.
- A7. RESOLVED BY DESIGN (decision 7): the labels already use the lowercase English words in parentheses (`Vincula (binds)`, `Previene (prevents)`, `Regla (rule)`), so they pass a case-sensitive or case-insensitive `lint_spine.py`. To be confirmed by the `lint_spine.py` run of check 1 (task F.2). No label switch is expected.

## REQUISITOS.md edits (exact)

Apply these in the slice that closes the spec (S1 for items 4 to 7 and 2; the rest with the slice that implements the behavior; all before verify). Text is Spanish like the file.

1. **RF-36**: replace the sentence "Solo el inventario de requisitos que escribe el paso de épicas (paso 6) dentro de `epics.md` usa `FR1:` y `NFR1:` sin guion y **con el mismo número** (`FR-3` pasa a `FR3:`), porque ese es el formato del paso 1 de `bmad-create-epics-and-stories`; la exportación (paso 8) copia el PRD tal cual." with "El inventario de requisitos que escribe el paso de épicas (paso 6) dentro de `epics.md` **conserva el guion y el mismo número** (`FR-3` sigue siendo `FR-3`): en la ejecución real de BMAD 6.12.1 del 2026-10-14 `bmad-create-epics-and-stories` leyó los identificadores con guion sin renumerar, y `FR3:` era una elección de la plataforma, no una exigencia de BMAD; la exportación copia el PRD tal cual." Keep the tag [VALIDADO EN BMAD 6.12.1].
2. **RF-44**: replace "La forma sin guion (`FR3:`, `NFR3:`) solo aparece en el inventario de `epics.md` y conserva el mismo número (RF-36); `NFR-n` es una extensión..." with "El inventario de `epics.md` conserva la forma con guion y el mismo número (RF-36); las decisiones `AD-n` vinculan `FR-n`/`NFR-n`, el inventario de las épicas las lista como requisitos adicionales y las historias citan los `FR-n` y `NFR-n` que implementan, sin cambiarlos; `NFR-n` es una extensión..." (the rest unchanged).
3. **RF-39**: append "Las preguntas para desarrollo de Arquitectura y Épicas e historias nacen **preasignadas al equipo de desarrollo** (`Responsable: Equipo de desarrollo; Revisar: antes de construir`), por lo que no bloquean la aprobación (RF-62); la persona puede reasignarlas." and replace "cuando se generan borradores de Arquitectura y Épicas, una sección" with "en Arquitectura y en Épicas e historias, la sección" (no longer conditional).
4. **RF-52**: replace "Por eso una iniciativa *Grande* no puede terminar hasta que existan Arquitectura y Épicas e historias (paso 6)." with "Una iniciativa *Grande* termina cuando el PRD, la Arquitectura y las Épicas e historias están aprobados; la Arquitectura exige el PRD aprobado y las Épicas e historias exigen la Arquitectura aprobada (RF-73, RF-74)."
5. **RF-54**: append "Si un documento del que depende otro (el PRD respecto de la Arquitectura; la Arquitectura o el PRD respecto de las Épicas e historias) vuelve a *Borrador* después de haber redactado el dependiente, la página del dependiente muestra un aviso para que se revise si conviene regenerarlo; el aviso no bloquea nada y no regenera nada, y desaparece cuando se edita, regenera o aprueba el dependiente."
6. **RN-02**: replace with "Arquitectura y Épicas/historias son borradores sujetos a revisión del equipo de desarrollo. El botón sigue siendo «Aprobar», pero su confirmación y la página lo dicen: aprobar significa que la persona de producto lo revisó, no que sea definitivo."
7. **RF-73 (new, after RF-72)**: "**RF-73** La Arquitectura se redacta a partir del PRD aprobado con la forma del `ARCHITECTURE-SPINE.md` de BMAD: bloques `AD-n` con *Vincula (binds)*, *Previene (prevents)* y *Regla (rule)*, cuyos enlaces solo pueden citar `FR-n`/`NFR-n` del PRD, y un mapa de capacidades calculado por el código. No propone tecnologías ni versiones: el stack va en **Diferido**, con su motivo, y como pregunta para desarrollo. [CONFIRMADO 2026-10-14]"
8. **RF-74 (new)**: "**RF-74** Las Épicas e historias se redactan a partir del PRD y de la Arquitectura aprobados con la forma de `epics.md` de BMAD: inventario con `FR-n`/`NFR-n` con guion y el mismo número, mapa de cobertura, épicas por valor de usuario y historias con *As a / I want / So that* y criterios *Given / When / Then*. Los números de épica e historia los asigna el código; cada `FR-n` queda cubierto por una historia o aparece como pregunta para desarrollo. Hay topes de épicas e historias y el documento respeta el límite de 20 000 caracteres. [CONFIRMADO 2026-10-14]"

Also (not REQUISITOS.md, same slice): AGENTS.md section 6 item 3 and `docs/bmad-referencia.md` section 4 get the RF-36 wording; `docs/mejoras-posteriores.md` gets the web-verified stack or starter recommendation; the README paragraph on the Gemini switch mentions the two new types.
