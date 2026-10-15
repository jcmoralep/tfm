# Delta for Initiatives (context-attachments)

Changes to the `initiatives` capability caused by step 7 (RF-08, RF-09, RF-22, RF-75). This delta stacks on the not-yet-archived deltas of `assistant-conversation`, `artifact-generation` and `architecture-epics`; the requirement it modifies is the latest version, "Detail view with Artefactos card" in `openspec/changes/architecture-epics/specs/initiatives/spec.md` (itself replacing "Detail view with placeholder sections" of `openspec/specs/initiatives/spec.md`). The replaced requirement keeps the name given by the latest baseline. Tags: **[UNIT]** xUnit, **[UI]** manual in Chrome. The Initiatives module MUST NOT reference the Context module: the card lives in the Web layer and talks to Context through MediatR (see `openspec/changes/context-attachments/specs/context/spec.md`).

## MODIFIED Requirements

### Requirement: Detail view with Artefactos card (RF-08, RF-09, RF-10, RF-16, RF-22, RF-75)

The detail MUST show name, description, status label in Spanish, depth mode, depth (or "Pendiente de sugerencia"), and the sections Conversación, Artefactos and Contexto. Conversación is a functional card (see `assistant-conversation`). Artefactos MUST be a functional card that lists the documents required by the level with their states and actions (see the `artifacts` capability); for a Large initiative these are the PRD, the Arquitectura and the Épicas e historias, each with its own state and, when an upstream document is not yet approved, the reason it cannot be generated yet. Contexto MUST be a functional card (see the `context` capability) where the owner adds, downloads and deletes files, links and notes, in every status (items listed oldest first); it MUST NOT show "Disponible próximamente" nor any placeholder text. The detail MUST show the status "Lista para construir" when the initiative reaches it and "Planificando" after a reopen. Adding or deleting context MUST NOT change the status.
(Previously: Contexto remained a placeholder with the text "Aquí podrá adjuntar videos, enlaces y notas." and the badge "Disponible próximamente".)

#### Scenario: Contexto is a live card [UI]
- GIVEN the detail of an owned initiative
- WHEN it renders
- THEN the Contexto card shows its empty state, the three ways to add and the counter, and no "Disponible próximamente" badge exists anywhere in the page

#### Scenario: Context in any status [UI]
- GIVEN initiatives in Draft, Planning and Ready to build
- WHEN the Contexto card is used on each
- THEN adding and deleting work and the status label does not change

#### Scenario: Initiatives module does not depend on Context [UNIT]
- GIVEN `ModuleBoundaryTests`
- WHEN they run
- THEN the Initiatives layers reference no Context type

(The other scenarios of the baseline stay; the scenario "Placeholder sections shown" no longer applies to Contexto and, where it still lists "Contexto" as a placeholder, it is replaced by "Contexto is a live card".)

## REMOVED Requirements

None.

## Non-requirement sections to update when archiving

- Out of Scope: any sentence that says the Contexto card is a placeholder or that attachments are a later step is removed.
- Assumptions: attachments of a soft-deleted initiative are unreachable but not purged.
