# Delta for Initiatives

Changes to `openspec/specs/initiatives/spec.md` caused by the assistant conversation (RF-29, RF-34, RF-45, RF-47, RF-50). Tags: **[UNIT]** xUnit, **[UI]** manual in Chrome. No requirement is removed.

## ADDED Requirements

### Requirement: Start planning transition (RF-34, RF-45)

The initiative MUST provide a transition from Clarifying to Planning, invoked only by the assistant's confirmation through a narrow command that accepts the initiative id and no status value. It MUST require status Clarifying and a non-empty depth. On success it MUST set status Planning and refresh the last-modified time. Calling it when the initiative is already Planning MUST succeed without changing state (idempotent; the last-modified time is not refreshed again, assumption). From Draft or ReadyToBuild, or with empty depth, it MUST be rejected with a Spanish message and leave the initiative unchanged. Messages: "La iniciativa debe estar en Aclarando para pasar a Planificando." and "Elija un nivel de profundidad antes de pasar a Planificando."

#### Scenario: Clarifying with depth moves to Planning [UNIT]
- GIVEN an owned Clarifying initiative with depth Standard at time T
- WHEN start planning is executed
- THEN status is Planning, depth and mode are unchanged, and the last-modified time is T

#### Scenario: Idempotent when already Planning [UNIT]
- GIVEN an owned Planning initiative
- WHEN start planning is executed again
- THEN it succeeds and status, mode, depth and last-modified time are unchanged

#### Scenario: Wrong status rejected [UNIT]
- GIVEN owned initiatives in Draft and ReadyToBuild
- WHEN start planning is executed on each
- THEN each fails with "La iniciativa debe estar en Aclarando para pasar a Planificando." and is unchanged

#### Scenario: Empty depth rejected [UNIT]
- GIVEN an owned Clarifying Automatic initiative with empty depth
- WHEN start planning is executed
- THEN it fails with "Elija un nivel de profundidad antes de pasar a Planificando." and status stays Clarifying

#### Scenario: Planning locks mode and depth [UNIT]
- GIVEN an initiative that has just moved to Planning
- WHEN the owner changes mode or depth
- THEN the change is rejected (see "Mode and depth editability")

#### Scenario: Owner only and deleted [UNIT]
- GIVEN an initiative owned by A, and a deleted initiative of A
- WHEN B executes start planning on the first and A on the second
- THEN both return not found and nothing changes

### Requirement: Set initiative depth (RF-47)

The system MUST provide an operation that sets depth mode to Manual and the depth to a given valid level in one step, used when the user accepts a level suggested by the assistant. It MUST succeed only while status is Draft or Clarifying, MUST refresh the last-modified time, and MUST NOT change status, name or description. In Planning or ReadyToBuild it MUST be rejected with a Spanish message and leave mode and depth unchanged. An undefined level MUST be rejected. Owner-only, deleted and unknown behave as not found.

#### Scenario: Automatic with empty depth becomes Manual with level [UNIT]
- GIVEN an owned Clarifying Automatic initiative with empty depth
- WHEN the operation sets Standard
- THEN mode is Manual, depth is Standard, status is Clarifying and the label shown is "Manual" with the level

#### Scenario: Replaces an existing level [UNIT]
- GIVEN an owned Clarifying Manual initiative with depth Small
- WHEN the operation sets Large
- THEN depth is Large and mode stays Manual

#### Scenario: Locked statuses reject [UNIT]
- GIVEN owned initiatives in Planning and in ReadyToBuild
- WHEN the operation is executed
- THEN each is rejected and mode and depth are unchanged

#### Scenario: Undefined level rejected [UNIT]
- GIVEN an owned Clarifying initiative
- WHEN the operation receives a level outside Small, Standard and Large
- THEN validation fails and nothing changes

#### Scenario: Other user or deleted [UNIT]
- GIVEN an initiative of A and a deleted initiative of A
- WHEN B executes the operation on the first and A on the second
- THEN both return not found and nothing changes

### Requirement: Depth edits do not depend on the conversation (RF-50)

Editing mode or depth of a Clarifying initiative through the existing edit operation MUST behave exactly as before and MUST NOT require, create, read or change any conversation data. The Initiatives module MUST NOT reference the Assistant module.

#### Scenario: Level change in Clarifying with a conversation present [UNIT]
- GIVEN an owned Clarifying initiative that has a conversation in another module
- WHEN the owner changes depth from Standard to Large through the edit operation
- THEN depth is Large, status stays Clarifying and no conversation data was touched

## MODIFIED Requirements

### Requirement: Depth mode and automatic depth (RF-06, RF-28, RF-47)

At creation the user MUST be able to choose Manual (the user picks the level) or Automatic (the assistant suggests it during the conversation). In Manual mode a depth level MUST be set when the wizard is finished. In Automatic mode depth MUST remain empty until the user accepts a level suggested by the assistant, and the UI MUST display it as "Pendiente de sugerencia". Accepting a suggested level MUST change the initiative to Manual mode with that level (see "Set initiative depth"); the origin "suggested by the assistant" is not kept.
(Previously: "Suggestion by the assistant is out of scope"; the suggestion is now delivered by the assistant conversation and its acceptance switches to Manual.)

#### Scenario: Manual mode stores chosen level [UNIT]
- GIVEN an initiative in Manual mode
- WHEN the user selects Standard
- THEN the depth is Standard

#### Scenario: Automatic mode leaves depth empty [UNIT]
- GIVEN an initiative with Automatic mode
- WHEN the wizard is completed
- THEN depth is empty and the depth label shown for the initiative is "Pendiente de sugerencia"

#### Scenario: Switching to Automatic clears depth [UNIT]
- GIVEN a Draft initiative in Manual mode with depth Large
- WHEN the mode is changed to Automatic
- THEN depth becomes empty

#### Scenario: Manual mode requires a level to finish [UNIT]
- GIVEN a Draft initiative in Manual mode with no depth
- WHEN the wizard is finished
- THEN the operation is rejected and status remains Draft

#### Scenario: Accepting a suggestion shows Manual [UNIT]
- GIVEN a Clarifying Automatic initiative with empty depth
- WHEN the suggested level Large is accepted
- THEN the detail shows mode Manual and depth Large, not "Pendiente de sugerencia"

### Requirement: Mode and depth editability (RF-29, RF-50)

The system MUST allow changing depth mode and depth level only while status is Draft or Clarifying, from the edit page and from the accept-suggestion operation. In Planning and ReadyToBuild they MUST be rejected, leaving the initiative unchanged. Changing them in Clarifying MUST NOT depend on any conversation. Name and description MAY be edited in any status (assumption, see Assumptions). The transition to Planning locks mode and depth.
(Previously: only the edit path was mentioned; the accept-suggestion operation, the conversation independence and the lock by transition are added.)

#### Scenario: Change allowed in Draft and Clarifying [UNIT]
- GIVEN initiatives in Draft and in Clarifying
- WHEN the owner changes depth to Large
- THEN depth is Large in both

#### Scenario: Change rejected in Planning and ReadyToBuild [UNIT]
- GIVEN initiatives in Planning and in ReadyToBuild
- WHEN the owner changes mode or depth
- THEN the change is rejected and mode and depth are unchanged

#### Scenario: Name edit allowed outside Draft [UNIT]
- GIVEN an initiative in Planning
- WHEN the owner changes its name to a valid value
- THEN the name is updated and mode and depth are unchanged

#### Scenario: Locked controls in UI [UI]
- GIVEN an initiative in Planning
- WHEN the detail is shown
- THEN mode and depth are displayed read-only

#### Scenario: Locked after confirmation [UNIT]
- GIVEN a Clarifying initiative that has just moved to Planning
- WHEN the owner changes its depth
- THEN the change is rejected

### Requirement: Status is never manually editable (RF-34)

No command or edit operation MUST accept a status value. Status MUST change only by wizard completion (Draft to Clarifying) and by the start-planning transition triggered by the user's confirmation in the assistant conversation (Clarifying to Planning). Other transitions are out of scope.
(Previously: "Status MUST change only by wizard completion in this change".)

#### Scenario: Update contract has no status [UNIT]
- GIVEN the update-initiative request type
- WHEN its members are inspected
- THEN it exposes no status member

#### Scenario: Editing fields keeps status [UNIT]
- GIVEN an initiative in Clarifying
- WHEN the owner edits its description
- THEN status remains Clarifying

#### Scenario: Start-planning and set-depth contracts have no status [UNIT]
- GIVEN the start-planning and set-depth request types
- WHEN their members are inspected
- THEN neither exposes a status member

#### Scenario: Only two transitions exist [UNIT]
- GIVEN the available operations
- WHEN every status change path is listed
- THEN only wizard completion (Draft to Clarifying) and start planning (Clarifying to Planning) change status

### Requirement: Detail view with Conversación card (RF-08, RF-09, RF-10)

The detail MUST show name, description, status label in Spanish, depth mode, depth (or "Pendiente de sugerencia"), and the sections Conversación, Artefactos and Contexto. Conversación MUST be a functional card that reflects the conversation (see the `assistant-conversation` capability: per-status content, "Abrir asistente", "Continuar conversación"). Artefactos and Contexto remain placeholders with no functional content.
(Previously: Conversación, Artefactos and Contexto were all placeholders.)

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
- THEN "Conversación" shows its card and "Artefactos" and "Contexto" are visible as placeholders

## REMOVED Requirements

None.

## Non-requirement sections to update when archiving

- Purpose: the capability no longer excludes the Planning transition.
- Assumptions: replace "wizard completion is the only status transition in this change" with "wizard completion (Draft to Clarifying) and the assistant's confirmation (Clarifying to Planning) are the only status transitions"; keep the other assumptions.
- Out of Scope: remove "Assistant depth suggestion, Planning" and "content of Conversación"; ReadyToBuild transition, trash or restore, hard delete, pagination, sharing, roles, content of Artefactos and Contexto, integration and bUnit tests stay out.
- Unchanged requirements (fields and validation, depth levels, wizard, owner-only access, listing, soft delete, authentication, navigation) stay as in the main spec.

## Assumptions

- The idempotent repeat of start planning does not refresh the last-modified time.
- The two Spanish rejection messages for start planning and the wording of the set-depth rejection are proposals.
- Owner-only and soft-delete rules of the existing spec apply to the new operations by the same not-found behavior.
