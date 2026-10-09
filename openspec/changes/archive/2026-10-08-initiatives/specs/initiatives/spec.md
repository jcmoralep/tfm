# Initiatives Specification

## Purpose

Lets an authenticated product user register an idea as an initiative, build it through a step-by-step wizard with a resumable draft, and list, view, edit and soft-delete it. Every initiative belongs to the user who created it. This is a new capability (no existing specs).

Scenario tags: **[UNIT]** testable with xUnit at domain, validator or handler level. **[UI]** verified manually in the Blazor UI only (no bUnit in this change).

Terminology: Status values are Draft ("Borrador"), Clarifying ("Aclarando"), Planning ("Planificando"), ReadyToBuild ("Lista para construir"). Depth levels are Small ("Pequeña"), Standard ("Estándar"), Large ("Grande"). Depth mode is Manual or Automatic.

## Requirements

### Requirement: Initiative fields and validation (RF-05, RF-33)

The system MUST require a name of 1 to 120 characters after trimming surrounding whitespace. The name MUST NOT be required to be unique, per user or globally. The system MUST accept an optional description of at most 1000 characters. Validation messages MUST be in Spanish.

Messages (verbatim):
- Empty or whitespace-only name: "El nombre es obligatorio."
- Name over 120 characters: "El nombre no puede superar los 120 caracteres."
- Description over 1000 characters: "La descripción no puede superar los 1000 caracteres."

#### Scenario: Name only is valid [UNIT]
- GIVEN a create request with name "App de pagos" and no description
- WHEN it is validated
- THEN validation succeeds

#### Scenario: Empty name rejected [UNIT]
- GIVEN a create request with an empty name
- WHEN it is validated
- THEN validation fails with "El nombre es obligatorio."

#### Scenario: Whitespace-only name rejected [UNIT]
- GIVEN a create request with name "     "
- WHEN it is validated
- THEN validation fails with "El nombre es obligatorio."

#### Scenario: Name of exactly 120 characters accepted [UNIT]
- GIVEN a name of exactly 120 characters
- WHEN it is validated
- THEN validation succeeds

#### Scenario: Name of 121 characters rejected [UNIT]
- GIVEN a name of 121 characters
- WHEN it is validated
- THEN validation fails with "El nombre no puede superar los 120 caracteres."

#### Scenario: Name length is measured after trimming [UNIT]
- GIVEN a name of 120 characters surrounded by spaces
- WHEN it is validated
- THEN validation succeeds

#### Scenario: Description boundaries [UNIT]
- GIVEN a description of exactly 1000 characters, then one of 1001 characters
- WHEN each is validated
- THEN the first succeeds and the second fails with "La descripción no puede superar los 1000 caracteres."

#### Scenario: Duplicate names allowed [UNIT]
- GIVEN user A already owns an initiative named "Portal"
- WHEN user A (or user B) creates another initiative named "Portal"
- THEN the creation succeeds and both initiatives exist with distinct ids

### Requirement: Depth levels and what each yields (RF-27)

The system MUST offer exactly three depth levels with these expected outputs, exposed as data the UI selector can display:
- Small ("Pequeña"): one short specification.
- Standard ("Estándar"): Brief and PRD.
- Large ("Grande"): PRD, Architecture and Epics and stories.

#### Scenario: Level descriptions available [UNIT]
- GIVEN the depth levels
- WHEN their deliverables are requested
- THEN Small yields a short specification, Standard yields Brief and PRD, Large yields PRD, Architecture and Epics and stories

#### Scenario: Selector shows deliverables [UI]
- GIVEN the depth step of the wizard in Manual mode
- WHEN it is displayed
- THEN each level shows its name and what it produces

### Requirement: Depth mode and automatic depth (RF-06, RF-28)

At creation the user MUST be able to choose Manual (the user picks the level) or Automatic (the assistant will suggest it later). In Manual mode a depth level MUST be set when the wizard is finished. In Automatic mode depth MUST remain empty, and the UI MUST display it as "Pendiente de sugerencia". Suggestion by the assistant is out of scope.

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

### Requirement: Wizard with resumable draft (RF-30)

Creation MUST be a step-by-step wizard. A draft MUST be saved with only a name. Each step MUST persist what the user has chosen so far, including the current step. Resuming an initiative that is in Draft MUST return its saved step and all saved selections. Finishing the last step MUST move the initiative from Draft to Clarifying. The exact steps and their storage are a design decision.

#### Scenario: Save draft with name only [UNIT]
- GIVEN an authenticated user
- WHEN they save a new initiative with only a name
- THEN an initiative exists with status Draft, empty description, no depth, owned by that user, with creation and update timestamps in UTC

#### Scenario: Each step persists selections [UNIT]
- GIVEN a Draft initiative
- WHEN the user advances a step with a description, mode and depth
- THEN those values and the new current step are stored

#### Scenario: Resume at saved step [UNIT]
- GIVEN a Draft initiative saved at step N with a description and mode
- WHEN the owner opens it for resuming
- THEN the result contains step N, the description and the mode exactly as saved

#### Scenario: Finishing moves to Clarifying [UNIT]
- GIVEN a Draft initiative with a valid name and, in Manual mode, a depth
- WHEN the wizard is finished
- THEN status is Clarifying

#### Scenario: Finishing a non-draft is rejected [UNIT]
- GIVEN an initiative in Clarifying
- WHEN a finish-wizard request is sent
- THEN it is rejected and status is unchanged

#### Scenario: Wizard navigation and draft banner [UI]
- GIVEN the user leaves the wizard midway
- WHEN they open the initiative from the list
- THEN the wizard reopens at the saved step with prior selections prefilled

### Requirement: Mode and depth editability (RF-29)

The system MUST allow changing depth mode and depth level only while status is Draft or Clarifying. In Planning and ReadyToBuild they MUST be rejected, leaving the initiative unchanged. Name and description MAY be edited in any status (assumption, see Assumptions).

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

### Requirement: Status is never manually editable (RF-34)

No command or edit operation MUST accept a status value. Status MUST change only by wizard completion in this change; other transitions are out of scope.

#### Scenario: Update contract has no status [UNIT]
- GIVEN the update-initiative request type
- WHEN its members are inspected
- THEN it exposes no status member

#### Scenario: Editing fields keeps status [UNIT]
- GIVEN an initiative in Clarifying
- WHEN the owner edits its description
- THEN status remains Clarifying

### Requirement: Owner-only access (RF-26)

An initiative MUST store the id of the user who created it, taken from the current user and never from request input. View, edit, wizard step, finish and delete MUST succeed only for the owner. For any other user the result MUST be indistinguishable from the initiative not existing. Listing MUST return only the caller's initiatives.

#### Scenario: Owner recorded from current user [UNIT]
- GIVEN the current user is A
- WHEN A creates an initiative
- THEN its owner id is A's id

#### Scenario: Other user's initiative reads as not found [UNIT]
- GIVEN an initiative owned by A
- WHEN B requests its detail
- THEN the result is the same not-found result as for a random unknown id

#### Scenario: Other user cannot edit or delete [UNIT]
- GIVEN an initiative owned by A
- WHEN B sends update, wizard-step, finish or delete
- THEN each returns not found and the initiative is unchanged and not deleted

#### Scenario: List excludes others' initiatives [UNIT]
- GIVEN A owns 2 initiatives and B owns 3
- WHEN A lists
- THEN only A's 2 are returned

#### Scenario: Not-found page for foreign id [UI]
- GIVEN B opens /iniciativas/{id} of A's initiative
- WHEN the page loads
- THEN the same not-found message as for a nonexistent id is shown

### Requirement: Listing, search, filters and order (RF-08, RF-31)

The list MUST show the caller's non-deleted initiatives ordered by last modification, most recent first, without pagination. The user MUST be able to search by name (case-insensitive substring) and filter by status and by depth level. Filters and search MUST combine with AND. A depth filter value for "Pendiente de sugerencia" (empty depth) MAY be offered. An empty or whitespace-only search term MUST be ignored. Any update that changes the initiative (including wizard steps and finish) MUST refresh the last-modified time.

#### Scenario: Order by last modification [UNIT]
- GIVEN initiatives updated at T1 < T2 < T3
- WHEN the owner lists
- THEN the order is T3, T2, T1

#### Scenario: Update moves initiative to top [UNIT]
- GIVEN the oldest initiative in the list
- WHEN the owner edits it
- THEN it is returned first on the next list

#### Scenario: Search by name [UNIT]
- GIVEN initiatives named "Portal clientes", "App móvil" and "portal interno"
- WHEN searching "portal"
- THEN "Portal clientes" and "portal interno" are returned

#### Scenario: Empty or whitespace search returns all [UNIT]
- GIVEN 3 initiatives
- WHEN searching with "" or "   "
- THEN all 3 are returned

#### Scenario: Search with no match [UNIT]
- GIVEN 3 initiatives
- WHEN searching "zzz"
- THEN an empty list is returned without error

#### Scenario: Filter by status [UNIT]
- GIVEN initiatives in Draft and Clarifying
- WHEN filtering status Clarifying
- THEN only Clarifying ones are returned

#### Scenario: Filter by depth [UNIT]
- GIVEN initiatives with depth Small, Large and empty
- WHEN filtering depth Large
- THEN only Large ones are returned

#### Scenario: Combined search, status and depth [UNIT]
- GIVEN initiatives matching the search only, the status only, the depth only, and all three
- WHEN search, status and depth are all supplied
- THEN only the one matching all three is returned

#### Scenario: Filters matching nothing [UNIT]
- GIVEN a status and depth combination no initiative has
- WHEN listing with it
- THEN an empty list is returned

#### Scenario: No pagination [UNIT]
- GIVEN 60 initiatives owned by the user
- WHEN listing
- THEN all 60 are returned

#### Scenario: Empty-state message [UI]
- GIVEN a user with no initiatives or filters with no result
- WHEN the list is shown
- THEN a Spanish empty-state message is displayed

### Requirement: Soft delete (RF-32)

Deleting MUST mark the record as deleted, keep it in storage, and stamp it as modified. Deleted initiatives MUST NOT appear in list, search, filters, detail, edit, wizard resume or any other query or command. Deleting an already-deleted or unknown initiative MUST return not found. No trash or restore capability exists.

#### Scenario: Deleted hidden from list [UNIT]
- GIVEN an owned initiative
- WHEN the owner deletes it and then lists
- THEN it is not in the results, with or without search and filters

#### Scenario: Deleted detail is not found [UNIT]
- GIVEN a deleted initiative
- WHEN the owner requests its detail
- THEN the result is not found

#### Scenario: Editing a deleted initiative [UNIT]
- GIVEN a deleted initiative
- WHEN the owner sends update, wizard-step or finish
- THEN each returns not found and nothing changes

#### Scenario: Deleting twice [UNIT]
- GIVEN a deleted initiative
- WHEN the owner deletes it again
- THEN the result is not found

#### Scenario: Record retained [UNIT]
- GIVEN an initiative deleted through the delete handler
- WHEN the underlying store is inspected by the test double
- THEN the record still exists marked as deleted

#### Scenario: Delete confirmation [UI]
- GIVEN the owner clicks delete
- WHEN the confirmation is accepted
- THEN the user returns to the list without the initiative

### Requirement: Detail view with placeholder sections (RF-08, RF-09)

The detail MUST show name, description, status label in Spanish, depth mode, depth (or "Pendiente de sugerencia"), and the sections Conversación, Artefactos and Contexto. Those sections are placeholders with no functional content in this change.

#### Scenario: Detail contents [UNIT]
- GIVEN an owned initiative
- WHEN its detail is requested
- THEN the result carries id, name, description, status, mode, depth, and timestamps

#### Scenario: Status labels in Spanish [UNIT]
- GIVEN each status value
- WHEN its display label is requested
- THEN the labels are "Borrador", "Aclarando", "Planificando" and "Lista para construir"

#### Scenario: Placeholder sections shown [UI]
- GIVEN the detail page
- WHEN it renders
- THEN the sections "Conversación", "Artefactos" and "Contexto" are visible as placeholders

### Requirement: Authentication required

Every initiative page and use case MUST require an authenticated user. Unauthenticated requests to /iniciativas, /iniciativas/nueva and /iniciativas/{id} MUST redirect to login. Handlers MUST NOT run without a current user.

#### Scenario: Unauthenticated redirected [UI]
- GIVEN an unauthenticated visitor
- WHEN they open any initiatives route
- THEN they are redirected to the login page

#### Scenario: Handler without current user [UNIT]
- GIVEN no authenticated current user
- WHEN a handler executes
- THEN it fails and nothing is persisted or returned

### Requirement: Navigation and language (RF-08)

A navigation link to the initiatives list MUST be available to authenticated users. All user-facing text MUST be in Spanish.

#### Scenario: Nav link [UI]
- GIVEN an authenticated user
- WHEN any page renders
- THEN a link to /iniciativas is present

## Assumptions

- Name and description remain editable in every status; only mode and depth are locked (RF-29 restricts only those).
- Manual mode requires a depth to finish the wizard; a Draft may exist without one.
- Search is case-insensitive substring on name only.
- A finished initiative moving back to Draft is not possible; wizard completion is the only status transition in this change.
- Switching to Automatic mode clears depth; switching to Manual leaves it empty until chosen.

## Out of Scope

Assistant depth suggestion, Planning and ReadyToBuild transitions, trash or restore, hard delete, pagination, sharing, roles, content of Conversación, Artefactos and Contexto, integration and bUnit tests.
