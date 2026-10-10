# Delta for Initiatives

Changes to the `initiatives` capability caused by artifact generation (RF-07, RF-29, RF-34, RF-52, RF-54, RN-06). This delta stacks on the one of the not-yet-archived change `assistant-conversation` (`openspec/changes/assistant-conversation/specs/initiatives/spec.md`): it builds on its Planning transition, so the requirements it modifies below are the versions defined there. Tags: **[UNIT]** xUnit, **[UI]** manual in Chrome. No requirement is removed.

## ADDED Requirements

### Requirement: Mark ready to build transition (RF-07, RF-34, RF-52)

The initiative MUST provide a transition from Planning to ReadyToBuild, invoked only by the Artifacts module when the last document of the level's required set is approved, through a narrow command that accepts the initiative id and no status value. It MUST require status Planning. On success it MUST set status ReadyToBuild and refresh the last-modified time. Calling it when the initiative is already ReadyToBuild MUST succeed without changing state (idempotent; the last-modified time is not refreshed again). From Draft or Clarifying it MUST be rejected with the Spanish message "La iniciativa debe estar en Planificando para pasar a Lista para construir." and leave the initiative unchanged. The Initiatives module MUST NOT verify the documents itself and MUST NOT reference the Artifacts module; the caller is responsible for the completeness check.

#### Scenario: Planning moves to ReadyToBuild [UNIT]
- GIVEN an owned Planning initiative at time T
- WHEN mark ready is executed
- THEN status is ReadyToBuild, name, description, mode and depth are unchanged, and the last-modified time is T

#### Scenario: Idempotent when already ready [UNIT]
- GIVEN an owned ReadyToBuild initiative
- WHEN mark ready is executed again
- THEN it succeeds and status and last-modified time are unchanged

#### Scenario: Wrong status rejected [UNIT]
- GIVEN owned initiatives in Draft and Clarifying
- WHEN mark ready is executed on each
- THEN each fails with "La iniciativa debe estar en Planificando para pasar a Lista para construir." and is unchanged

#### Scenario: Owner only and deleted [UNIT]
- GIVEN an initiative of A and a deleted initiative of A
- WHEN B executes mark ready on the first and A on the second
- THEN both return not found and nothing changes

### Requirement: Reopen to Planning transition (RF-54, RN-06)

The initiative MUST provide a transition from ReadyToBuild back to Planning, invoked only by the Artifacts module when a document of a ready initiative changes (edit or regeneration), through a narrow command that accepts the initiative id and no status value. On success it MUST set status Planning and refresh the last-modified time. Calling it when the initiative is already Planning MUST succeed without changing state (idempotent). From Draft or Clarifying it MUST be rejected with "La iniciativa debe estar en Lista para construir para volver a Planificando." and leave the initiative unchanged. Reopening MUST NOT unlock mode or depth: they stay locked in Planning, and the initiative MUST NOT return to Clarifying or Draft by any path.

#### Scenario: ReadyToBuild returns to Planning [UNIT]
- GIVEN an owned ReadyToBuild initiative
- WHEN reopen is executed
- THEN status is Planning, depth and mode are unchanged and the last-modified time is refreshed

#### Scenario: Idempotent when already Planning [UNIT]
- GIVEN an owned Planning initiative
- WHEN reopen is executed
- THEN it succeeds and nothing changes

#### Scenario: Wrong status rejected [UNIT]
- GIVEN owned initiatives in Draft and Clarifying
- WHEN reopen is executed on each
- THEN each fails with "La iniciativa debe estar en Lista para construir para volver a Planificando." and is unchanged

#### Scenario: Reopened initiative keeps mode and depth locked [UNIT]
- GIVEN an initiative reopened to Planning
- WHEN the owner changes mode or depth
- THEN the change is rejected (see "Mode and depth editability")

#### Scenario: Owner only and deleted [UNIT]
- GIVEN an initiative of A and a deleted initiative of A
- WHEN B executes reopen on the first and A on the second
- THEN both return not found and nothing changes

#### Scenario: Round trip [UNIT]
- GIVEN a Planning initiative marked ready and then reopened
- WHEN mark ready is executed again
- THEN the status is ReadyToBuild again

## MODIFIED Requirements

### Requirement: Status is never manually editable (RF-34)

No command or edit operation MUST accept a status value. Status MUST change only by wizard completion (Draft to Clarifying), by the start-planning transition triggered by the user's confirmation in the assistant conversation (Clarifying to Planning), by the mark-ready transition triggered by the approval of the last required document (Planning to ReadyToBuild) and by the reopen transition triggered by a change to a document of a ready initiative (ReadyToBuild to Planning). Other transitions are out of scope; in particular there is no way back to Clarifying or Draft.
(Previously: only wizard completion and start planning were listed; the ready-to-build and reopen transitions are added.)

#### Scenario: Update contract has no status [UNIT]
- GIVEN the update-initiative request type
- WHEN its members are inspected
- THEN it exposes no status member

#### Scenario: Editing fields keeps status [UNIT]
- GIVEN an initiative in Clarifying
- WHEN the owner edits its description
- THEN status remains Clarifying

#### Scenario: Transition contracts have no status [UNIT]
- GIVEN the start-planning, set-depth, mark-ready and reopen request types
- WHEN their members are inspected
- THEN none exposes a status member

#### Scenario: Only four transitions exist [UNIT]
- GIVEN the available operations
- WHEN every status change path is listed
- THEN only wizard completion, start planning, mark ready and reopen change status, each in the direction stated above

### Requirement: Mode and depth editability (RF-29, RF-50)

The system MUST allow changing depth mode and depth level only while status is Draft or Clarifying, from the edit page and from the accept-suggestion operation. In Planning and ReadyToBuild they MUST be rejected, leaving the initiative unchanged, including after a ReadyToBuild initiative is reopened to Planning. Changing them in Clarifying MUST NOT depend on any conversation. Name and description MAY be edited in any status. Because generated documents depend on the level, a change of depth in Clarifying MUST NOT delete generated documents.
(Previously: the reopen case and the retention of documents are added.)

#### Scenario: Change allowed in Draft and Clarifying [UNIT]
- GIVEN initiatives in Draft and in Clarifying
- WHEN the owner changes depth to Large
- THEN depth is Large in both

#### Scenario: Change rejected in Planning and ReadyToBuild [UNIT]
- GIVEN initiatives in Planning and in ReadyToBuild
- WHEN the owner changes mode or depth
- THEN the change is rejected and mode and depth are unchanged

#### Scenario: Change rejected after reopening [UNIT]
- GIVEN an initiative that was ReadyToBuild and was reopened to Planning
- WHEN the owner changes depth
- THEN the change is rejected

#### Scenario: Name edit allowed outside Draft [UNIT]
- GIVEN an initiative in ReadyToBuild
- WHEN the owner changes its name to a valid value
- THEN the name is updated, mode, depth and status are unchanged

#### Scenario: Locked controls in UI [UI]
- GIVEN an initiative in Planning or ReadyToBuild
- WHEN the detail is shown
- THEN mode and depth are displayed read-only

### Requirement: Detail view with Artefactos card (RF-08, RF-09, RF-10, RF-16)

The detail MUST show name, description, status label in Spanish, depth mode, depth (or "Pendiente de sugerencia"), and the sections Conversación, Artefactos and Contexto. Conversación is a functional card (see `assistant-conversation`). Artefactos MUST be a functional card that lists the documents required by the level with their states and actions (see the `artifacts` capability). Contexto remains a placeholder with no functional content. The detail MUST show the status "Lista para construir" when the initiative reaches it and "Planificando" after a reopen.
(Previously: Artefactos was a placeholder together with Contexto.)

#### Scenario: Detail contents [UNIT]
- GIVEN an owned initiative
- WHEN its detail is requested
- THEN the result carries id, name, description, status, mode, depth, and timestamps

#### Scenario: Status labels in Spanish [UNIT]
- GIVEN each status value
- WHEN its display label is requested
- THEN the labels are "Borrador", "Aclarando", "Planificando" and "Lista para construir"

#### Scenario: Sections shown [UI]
- GIVEN the detail page
- WHEN it renders
- THEN "Conversación" and "Artefactos" show their cards and "Contexto" is visible as a placeholder

#### Scenario: Status follows the documents [UI]
- GIVEN a Small initiative whose Spec is approved, and then edited
- WHEN the detail is shown after each step
- THEN the status label reads "Lista para construir" after the approval and "Planificando" after the edit

## REMOVED Requirements

None.

## Non-requirement sections to update when archiving

- Purpose: the capability no longer excludes the ReadyToBuild and reopen transitions.
- Assumptions: the status transitions are wizard completion (Draft to Clarifying), the assistant's confirmation (Clarifying to Planning), the approval of the last required document (Planning to ReadyToBuild) and the reopen after a document change (ReadyToBuild to Planning); keep the other assumptions.
- Out of Scope: remove the ReadyToBuild transition and "content of Artefactos"; "content of Contexto", trash or restore, hard delete, pagination, sharing, roles, integration and bUnit tests stay out.

## Assumptions

- Repeated mark ready and reopen do not refresh the last-modified time.
- Mark ready checks only the initiative status; the completeness of the set is the Artifacts module's responsibility through its own query of documents.
- The two Spanish rejection messages are proposals.
- Owner-only and soft-delete rules of the existing spec apply to the new operations by the same not-found behavior.
