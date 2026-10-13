# Exploration: bmad-alignment

Origin: BMAD alignment assessment after step 4 (docs/valoracion-alineacion-bmad-paso-4.md). The user chose a dedicated change for what does not need Gemini: (1) owners on open questions with an approval gate, (2) drop the placeholder user journeys, (3) decide the identifier reconciliation with BMAD. Source: sdd-explore subagent report, persisted by the orchestrator.

## Current state

- Open questions: `DraftComposer.Question`/`OpenQuestions` write `- **Q-n**: Falta saber …: «pregunta»` with no owner, state or phase. `ArtifactFormatChecker` is a pure function that only validates the Q-n format. `ApproveArtifactCommandHandler` and `ArtifactViewBuilder` never look at Q-n. RF-43 promises an items table with owner and state; RF-56 defers it; RF-59 lets warnings through; RF-20/RN-03 make the Markdown the only source of truth. RF-35/41/42 promise "pregunta pendiente con responsable", which nothing delivers.
- Journeys: `PrdLayout.WriteJourneys` writes one placeholder `UJ-n` per line of the users answer plus one shared assumption (decision D1). BMAD's PRD template lets 2.3 be a single sentence in the "lighter" dial; its checklist never mentions UJ.
- Other fixed filler (tagged and indexed, honest but noisy): Brief "Qué lo hace distinto", "Alcance/Dentro", "Visión"; PRD "No usuarios", "Glosario"; Spec "Restricciones". Replacing them needs new questions, which belong to step 5.
- Identifiers: the platform uses FR/NFR/UJ/SM/A/Q with a hyphen (RF-44). New finding from the BMAD source: the PRD template itself uses `FR-1`, `UJ-1`, `SM-1`. Only `bmad-create-epics-and-stories` step 01 writes `FR1:`/`NFR1:`, in the inventory it builds inside `epics.md`, and its extraction is fuzzy (language-model driven, "FR1:, Functional Requirement 1:, or similar"). Caveat: this is a source reading, not a run in a real BMAD install.
- Process: `openspec/specs/` only has `initiatives`; the `artifacts` spec still lives in the unarchived artifact-generation change, so a delta of this change cannot modify a main spec yet.

## Item 1: owners on open questions

Options for where the owner lives: (A) inline in the Markdown line `- **Q-1**: … (Responsable: Legal)` parsed by a pure function: keeps RF-20/RN-03, no migration, visible in exports, medium effort; (B) table `art_artifact_items` rebuilt on save (RF-43): matches RF-43 literally but needs a migration and reconciliation on edit/regenerate/renumber, and exceeds the review budget; (C) ask in the chat: best UX but adds script questions (step 5).

Recommended A. Generated suffix `(Responsable: por asignar)`; unassigned means missing, empty or `por asignar`; new pure `OpenQuestions(markdown)` returning (Id, Text, Owner?) reused by the view builder, the approve guard and a rewriter. Owner is free text (max about 60 characters, no parentheses, `*`, `|` or newlines) with suggestions through a `datalist` (Legal, TI, Finanzas, "Quien propone la iniciativa"); a fixed list forces wrong answers and a user directory is out of scope. No phase or blocking flag now (YAGNI); every open question needs an owner.

UI: a "Preguntas pendientes" panel above the read view, shown only when there are open questions, one row per Q-n with an owner input and one "Guardar responsables" button; new `AssignQuestionOwnersCommand(InitiativeId, Type, ExpectedVersion, Owners)` that rewrites the lines with a pure `QuestionOwners.Apply` and follows the `SaveArtifactContentCommandHandler` path (version +1, Draft, ReopenPlanning if ReadyToBuild, optimistic concurrency). The raw editor keeps working.

Approval: `UnassignedQuestions` in `ArtifactView`; `CanApprove=false` while non-empty; a guard in `ApproveArtifactCommandHandler` next to `RequireAvailable`, placed INSIDE the Draft-to-Approved branch so the idempotent CompletionPending retry still works; message like "Asigne un responsable a las preguntas pendientes antes de aprobar: Q-1, Q-3." Format warnings stay non-blocking (RF-59 gets a one-line clarification). Documents already Approved are grandfathered. Regenerate renumbers Q-n and resets owners (RF-57/RF-60).

## Item 2: placeholder journeys

Options: (a) keep heading 2.3 with one honest assumption and no `UJ-n`, e.g. `[ASSUMPTION: A-n] Aún no se describió un recorrido concreto de uso; se completa con un caso real (una persona con nombre) al revisar el documento.`; (b) omit the section (changes outline, checker, numbering, goldens, diverges from BMAD); (c) a Q-n "falta un recorrido real" (every PRD would then need an owner for a question the user never saw). Recommended (a): keep `UJ` in the prefix list and checker for edits and step 5; both PRD goldens lose two lines; update the artifact-generation spec text and mark D1 as superseded. Other filler stays until step 5.

## Item 3: identifier reconciliation (decision only)

Recommended A: keep the hyphen in the platform and the PRD (BMAD-native for PRD/SM/UJ); the step 6 epics generator writes the epics inventory as `FR1:`/`NFR1:` with the same number (`FR-3` becomes `FR3`); the step 8 exporter copies the PRD as is. Rejected: generating `FR1:` everywhere (breaks RF-44 and the BMAD PRD convention), emitting both forms (noise), rewriting the PRD at export (diverges for no reason). Record the decision in docs/bmad-referencia.md, AGENTS.md §6 item 3, RF-36/RF-44 and the artifacts spec assumptions ("numbers are identical; only epics.md uses the unhyphenated form; NFR-n is a platform extension"). Pass explicit numbers to the epics step because user edits can leave gaps. No mapper now; verification with a real `bmad-create-epics-and-stories` run stays for step 6. `AD-n` belongs to step 6.

## Open product decisions (ask one at a time)

1. Journeys: replace the placeholder lines with one honest note (recommended a).
2. Who counts as an owner: free text with suggested areas (recommended) or a closed list.
3. Which questions need an owner: all of them (recommended) or only those blocking the next phase.
4. Identifiers and RF-43: keep the hyphen, convert only inside epics.md, owners live in the document text and the RF-43 table stays deferred (recommended; confirm both).

## Risks

REQUISITOS.md is modified on branch fix/ui-evidence-findings (RF-47) and this change edits RF-36/43/44/56/59 (rebase or wait); the artifacts baseline spec is missing from openspec/specs (archive or sync artifact-generation first, or write full-requirement deltas); assigning owners bumps the version and reopens a ReadyToBuild initiative (batch all owners in one save and say so in the UI); a hand edit can break the suffix (missing counts as unassigned); the approve guard must stay inside the Draft-to-Approved branch; the suffix changes every golden with questions; a user who truly does not know needs a neutral suggestion; the epics-step reading is source-only.

## Out of scope (steps 5-9)

BMAD conduct rules, coaching, per-section sufficiency criteria and new script questions (real journey, differentiator, non-users, glossary, constraints) in step 5; independent validation before approval, open/answered state, blocks-phase flag, user directory and the items table; epics/architecture generation and AD-n (step 6); attachments (7); exporter, frontmatter, folder naming, usage note, "Preguntas para desarrollo", the integrations section (8); party mode; tests with real people (9); Pequeña 3-vs-6 questions and a runtime check of the epics step.

## Size forecast and slices

About 315 production lines, about 350 test lines, about 150 doc lines plus regenerated goldens: roughly 700, above the 400 budget (exception-ok). Slices: (1) decision and journeys (remove UJ placeholders, goldens, spec text, identifier decision recorded); (2) owner model and gate (parser, composer suffix and goldens, UnassignedQuestions in the view, approve guard with grandfathering, RF-43/56/59 amendments); (3) owner UI (assign command and rewriter, "Preguntas pendientes" panel, Chrome walk-through).
