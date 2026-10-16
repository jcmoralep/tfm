# Artifact pipeline map

| Stage | Responsibility | Files |
|---|---|---|
| Catalog | Which documents a depth level delivers and when each is available | `src/RutaProducto.Application/Features/Artifacts/ArtifactCatalog.cs`, `ArtifactViewBuilder.cs`, `ArtifactsOverview.cs` |
| Guards | Ownership, status, upstream approval; builds the `DraftRequest` with approved upstream text | `ArtifactGuards.cs`, `DraftRequest.cs` |
| Use cases | Generate, regenerate, save, approve, assign owners, export | `GenerateArtifact/`, `RegenerateArtifact/`, `SaveArtifactContent/`, `ApproveArtifact/`, `AssignQuestionOwners/`, `ExportArtifact/` |
| Drafting seam | `IArtifactDrafter`: fake or Gemini, chosen by `Assistant:Provider` | `IArtifactDrafter.cs`; `src/RutaProducto.Infrastructure/Artifacts/FakeArtifactDrafter.cs`, `GeminiArtifactDrafter.cs` |
| Prompts and replies | What the model is asked and the strict reply shape | `DraftPrompt.cs`, `ArchitecturePrompt.cs`, `EpicsPrompt.cs`, `DraftSlots.cs`, `DraftItem.cs`, `EpicsReply.cs`; `Infrastructure/Gemini/GeminiSchema.cs` |
| Facts from upstream | Ids and text parsed from approved documents | `BriefFacts.cs`, `PrdFacts.cs` |
| Composition | Numbering, assumptions, questions, caps, budget | `DraftComposer.cs`, `DraftRenderer.cs`, `LayoutBudget.cs`, `SpecLayout.cs`, `BriefLayout.cs`, `PrdLayout.cs`, `ArchitectureLayout.cs`, `EpicsLayout.cs` |
| Markdown rules | Outline, ids, questions, format warnings | `Application/Features/Artifacts/Markdown/` (`ArtifactOutline`, `ItemNumberer`, `ItemPrefix`, `OpenQuestions`, `QuestionOwners`, `ArtifactFormatChecker`, `ArchitectureDecisionItems`, `RequirementItems`) |
| Persistence | Markdown + version per artifact | `Infrastructure/Artifacts/ArtifactRepository.cs`, `ArtifactConfiguration.cs` |
| Export | BMAD frontmatter, usage note, file name | `Infrastructure/Export/MarkdownArtifactExporter.cs`; Web endpoint `src/RutaProducto.Web/Artifacts/ArtifactEndpoints.cs` |
| UI | Document page, questions panel, downloads, print | `Web/Components/Pages/Artifacts/ArtifactDocument.razor`, `Web/Components/Artifacts/` |
| Goldens | Expected Markdown per type | `tests/RutaProducto.Infrastructure.Tests/Artifacts/Goldens/` (`spec-small`, `brief-standard`, `prd-standard`, `prd-large`, `architecture-large`, `epics-large`) |

Known model-quality gaps (recorded in `docs/mejoras-posteriores.md`, 2026-10-15): features the person said could wait still appear; invented assumptions (99% availability); awkward integration lines; technical epics; "Suposición A-n" prefixes inside decisions.
