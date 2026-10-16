---
name: rp-artifact-pipeline
description: "Trigger: artefacto, documento, SPEC, Brief, PRD, Arquitectura, Épicas, borrador, exportar .md, layout, golden, prompt de Gemini. Change the artifact pipeline safely."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.0"
---

## Activation Contract

Load before changing how documents are drafted, numbered, checked, rendered, approved or exported, or adding a new document type.

## Hard Rules

- Markdown stored in MySQL is the single source of truth, with a version number; no PDF copy (AGENTS.md §4.9).
- The model never writes ids or structure: code assigns `FR-n`, `NFR-n`, `UJ-n`, `SM-n`, `AD-n`, `A-n`, `Q-n`, `Epic N`, `Story N.M`; ids keep the hyphen (RF-44) and are filtered against the approved upstream document.
- Fake first, Gemini last: every layout works with `FakeArtifactDrafter` and has a golden before the Gemini drafter is wired.
- Chain: Architecture needs the approved PRD; Epics need the approved Architecture; drafts use only the approved upstream text. Upstream changes only show an amber hint (RF-54).
- Caps and the 20,000-character ladder are enforced by code (`LayoutBudget`), not by the prompt.
- Development questions are pre-assigned ("Equipo de desarrollo; antes de construir") and never block approval.
- Any change to the exported shape requires `rp-bmad-conformance`.

## Decision Gates

| Change | Touch |
|---|---|
| Section/outline | `Application/Features/Artifacts/Markdown/ArtifactOutline.cs`, the layout, the golden, `ArtifactFormatChecker` |
| Numbering/ids | `ItemNumberer`, `ItemPrefix`, layout; goldens |
| Model output shape | `Infrastructure/Artifacts/*Prompt.cs`, `DraftSlots`/`EpicsReply`, Gemini schema; keep validators strict |
| Availability/approval | `ArtifactCatalog`, `ArtifactGuards`, `ArtifactViewBuilder` |
| Export | `Infrastructure/Export/MarkdownArtifactExporter.cs` (frontmatter + usage note) |

## Execution Steps

1. Read `references/map.md` and the files for the gate row.
2. Change the fake path and its golden first; run `rp-build-gate`.
3. Wire Gemini; run a moderate live check (`rp-gemini-live-check`).
4. Review outputs with `rp-artifact-reviewer`; validate shape with `rp-bmad-conformance`.

## Output Contract

Return the pipeline stages changed, goldens updated (with reason), BMAD check result, and live-check findings.

## References

- `references/map.md` — pipeline map with file paths.
- `docs/bmad-referencia.md`, `REQUISITOS.md` RF-20, RF-36, RF-39, RF-43, RF-44, RF-54, RF-73, RF-74.
