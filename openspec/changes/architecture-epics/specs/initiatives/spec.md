# Delta for Initiatives (architecture-epics)

Changes to the `initiatives` capability caused by step 6 (RF-07, RF-27, RF-34, RF-52, RF-54). This delta stacks on the not-yet-archived deltas of `assistant-conversation` and `artifact-generation` (`openspec/changes/artifact-generation/specs/initiatives/spec.md`), whose "Mark ready to build transition" and "Detail view with Artefactos card" it builds on; the requirement it modifies below is the version defined there. Tags: **[UNIT]** xUnit, **[UI]** manual in Chrome. No requirement is removed. The completeness check stays in the Artifacts module (see `openspec/changes/architecture-epics/specs/artifacts/spec.md`, "Approval and completion"); the Initiatives module still verifies only its own status and MUST NOT reference the Artifacts module.

## ADDED Requirements

### Requirement: Large reaches ReadyToBuild only with the full set (RF-52, RF-27, RF-34)

A Large initiative MUST become ReadyToBuild only when its PRD, its Architecture and its Epics-and-stories are all Approved, through the existing `mark ready` transition invoked by the Artifacts module (Planning to ReadyToBuild, idempotent, no status value accepted). Approving fewer than the three documents MUST leave the initiative in Planning. An edit, regeneration or return to Draft of any of the three documents while the initiative is ReadyToBuild MUST reopen it to Planning, and it becomes ReadyToBuild again only when the three are Approved again. The status remains non-editable by hand (RF-34). The deliverables of the level shown to the user (Large: PRD, Architecture and Epics and stories) do not change.

#### Scenario: Two of three do not complete [UNIT]
- GIVEN a Large Planning initiative with Approved PRD and Approved Architecture and no Epics
- WHEN the completion check runs
- THEN the initiative stays Planning

#### Scenario: Third approval completes [UNIT]
- GIVEN the same initiative and a Draft Epics
- WHEN the Epics are approved
- THEN the initiative becomes ReadyToBuild

#### Scenario: Editing any of the three reopens [UNIT]
- GIVEN a ReadyToBuild Large initiative
- WHEN its Architecture is edited with a text change
- THEN the initiative is Planning and the Architecture is Draft v+1; approving the Architecture again makes it ReadyToBuild

#### Scenario: Re-approving the PRD after an edit completes again [UNIT]
- GIVEN a ReadyToBuild Large initiative whose PRD is edited, then approved
- WHEN each step completes
- THEN the initiative is Planning after the edit and ReadyToBuild after the approval, with Architecture and Epics still Approved

#### Scenario: Status follows the three documents [UI]
- GIVEN a Large initiative walked in Chrome through the three approvals and then an edit of the Epics
- WHEN the detail is shown after each step
- THEN the status label reads "Planificando" until the third approval, "Lista para construir" after it and "Planificando" after the edit

## MODIFIED Requirements

### Requirement: Detail view with Artefactos card (RF-08, RF-09, RF-10, RF-16)

The detail MUST show name, description, status label in Spanish, depth mode, depth (or "Pendiente de sugerencia"), and the sections Conversación, Artefactos and Contexto. Conversación is a functional card (see `assistant-conversation`). Artefactos MUST be a functional card that lists the documents required by the level with their states and actions (see the `artifacts` capability); for a Large initiative these are the PRD, the Arquitectura and the Épicas e historias, each with its own state and, when an upstream document is not yet approved, the reason it cannot be generated yet. Contexto remains a placeholder with no functional content. The detail MUST show the status "Lista para construir" when the initiative reaches it and "Planificando" after a reopen.
(Previously: the card could not offer actions for the Architecture and Epics because they could not be generated.)

#### Scenario: Large card lists the three documents [UI]
- GIVEN a Large Planning initiative
- WHEN the detail renders
- THEN the Artefactos card lists PRD, Arquitectura and Épicas e historias, with Arquitectura and Épicas explaining which document must be approved first until it is

(The other scenarios of the baseline stay.)

## REMOVED Requirements

None.

## Non-requirement sections to update when archiving

- Out of Scope: the sentence that Large cannot finish because Architecture and Epics do not exist (if present) is removed.
- Assumptions: the transition to ReadyToBuild for Large happens at the approval of the last of the three documents of the chain.

## Assumptions

- The three-document set of Large is the catalog set (PRD, Architecture, Epics-and-stories); the Brief belongs to Standard only.
- Repeated mark ready does not refresh the last-modified time (unchanged).
