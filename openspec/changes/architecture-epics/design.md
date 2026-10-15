# Design: Architecture and Epics drafts (PROPUESTA-MVP step 6)

## Technical Approach

This is approach B from `proposal.md`. Both documents go through the existing pipeline: `DraftRequest`, then `DraftRenderer`, then `DraftComposer` and a layout. Code owns the headings, ids, markers, the question suffix and the caps.

- **Architecture**: flat slots plus decision objects. The model supplies the title, binds, prevents and rule of each decision. Code numbers the decisions `AD-n` and keeps only the binds whose ids exist in the approved PRD.
- **Epics**: a nested model shape (epics, stories, criteria). Code writes `Epic N` and `Story N.M`, the inventory, the coverage map, and the questions for uncovered FRs.
- **Upstream text**: both documents read the upstream text from `DraftRequest`. Neither one reads the conversation answers.
- **Order**: the Fake drafter ships first (S3, S4) and Gemini last (S5, S6).

### Corrections to the proposal (verified against the code)

| Proposal | Reality | Consequence |
|---|---|---|
| S1 adds the catalog types | `DraftRenderer.Compose`, `MarkdownArtifactExporter.FilePrefix` and `DraftSlots.For` throw for unknown types. A catalog entry with no layout gives a broken Generar or Descargar on `main` | S1 keeps `new("Arquitectura", null)`. **S3 switches on Architecture and S4 switches on Epics**, each together with its layout and export. S1 delivers the enum, labels, the signature and the staleness check |
| Export goes in S6 | The download throws as soon as a type is switched on | Export prefixes and usage notes move to S3 and S4. S6 keeps only the docs |
| `IsSetAvailable` reuses `Availability` | With upstream gates, approving a PRD again while Arch and Epics are Approved would compute `covered=false` | `IsSetAvailable` stays **coverage-only** (see D1) |
| AD headings are checked | `ArtifactFormatChecker` skips every heading line | The checker gains a heading branch for `AD-n` only (D7) |

## Architecture Decisions

| # | Topic | Options and tradeoff | Decision |
|---|---|---|---|
| 1 | How availability sees upstream approval | A new `ArtifactContext` record means many call-site edits for little gain. A predicate `Func<ArtifactType,bool>` hides the data. A summaries parameter: the callers already hold the summaries, or can get them with one extra `ListAsync` | **`Availability(type, status, depth, answers, IReadOnlyList<ArtifactSummary> summaries)`**. A private `Coverage(...)` keeps today's logic (Draft, level, required, Clarify or Closing). Only after it returns Available is `Upstream(type)` checked in order: the first one not Approved gives `WaitingForPrd` or `WaitingForArchitecture`. `Upstream`: Architecture needs [Prd]; EpicsAndStories needs [Prd, Architecture]; the rest need []. `IsSetAvailable` keeps its signature and uses `Coverage`. Generate, Regenerate and Approve call `ListAsync` before the check |
| 2 | Upstream text in the request | Separate optional records; or optional strings like `BriefMarkdown` | **`DraftRequest(..., string? BriefMarkdown = null, string? PrdMarkdown = null, string? ArchitectureMarkdown = null)`**. A new `ArtifactGuards.DraftRequestAsync(repository, initiative, ownerId, type, answers, ct)` builds the whole request, replacing the copies in Generate and Regenerate. It sets `PrdMarkdown` for Architecture and Epics, and `ArchitectureMarkdown` for Epics, only while the upstream is Approved. It absorbs `ApprovedBriefAsync` |
| 3 | Staleness hint (RF-54) with no stored version link | Store the upstream version, which needs a migration (rejected). Or compare existing timestamps | **`ArtifactSummary` gains `UpdatedAt`** (two constructors: the repository and `InMemoryArtifactRepository`). `ArtifactCatalog.StaleUpstream(type, summaries)` returns the first upstream whose `UpdatedAt > dependent.UpdatedAt`, or null. An upstream must be approved before the dependent can exist, and approving an approved document is a no-op, so only an edit, a regeneration or a re-approval moves it later. `ArtifactView.StaleUpstream` feeds an amber note on the document page: "El PRD cambió después de generar este documento. Cuando esté aprobado, use Regenerar para actualizarlo." The note only advises; nothing is blocked |
| 4 | PRD facts | Reuse `BriefFacts` (text matching), or an id reader | **A public reader in `Application/.../Markdown`**: `RequirementItems.Parse(md)` returns `RequirementItem(Id, Text)` for `- **FR-n**: text` and `- **NFR-n**: text` lines. Bold is optional. Fenced code, headings and the assumption index are skipped, through `MarkdownLines`. The first occurrence wins. `ArchitectureDecisionItems.Parse(md)` returns `(Id, Title)` from `### AD-n — title`. `Infrastructure/Artifacts/PrdFacts` wraps these readers: `Functional`, `NonFunctional`, `Knows(id)` and `static string? CanonicalId(raw)` (`fr3`, `FR 3` and `FR-3` all give `FR-3`). There is no answer-to-assumption downgrade, because no Architecture slot allows `answer` |
| 5 | Architecture shape | Follow every BMAD spine section; or apply "keep only the sections this spine needs" | **7 required sections**: Paradigma de diseño, Invariantes heredadas, Invariantes y reglas, Mapa de capacidades, Diferido, Preguntas para desarrollo, Índice de suposiciones. There are no Stack, Structural Seed or Consistency Conventions sections. They become code-owned Diferido entries, each with its reason |
| 6 | AD numbering and binds | The model writes the ids (breaks RN-03), or code assigns them | **Code numbers AD-1..n in output order**, which keeps the ids monotonic for lint. Binds are canonicalised, filtered with `Knows`, de-duplicated and sorted in PRD order. **A decision with no valid bind is dropped**, so every AD is anchored to a PRD fact. At most **10** decisions. If no decisions survive, the Fake rule applies: one AD per PRD NFR (title = the NFR text). If the PRD has no NFRs, the section carries one assumption line |
| 7 | Lint-safe AD labels | English labels (the rest of the document is Spanish), or Spanish labels that contain the English words | **`- **Vincula (binds):** FR-1, NFR-2`**, **`- **Previene (prevents):** …`** and **`- **Regla (rule):** …`**. They contain the lowercase `binds`, `prevents` and `rule`, so they pass whether the lint is case-sensitive or not. They also do not match the checker's definition regex (`Vin`, `Pre` and `Reg` are not prefixes). The checker gains a heading branch: `^#{2,6}\s+AD…` runs `CheckDefinition`, which reports duplicates and `AD-0`. Gaps stay silent |
| 8 | Coverage map versus duplicate ids | Relax the duplicate rule, or skip one range | **The checker skips definitions inside `## Mapa de cobertura de requisitos`.** `MarkdownLines.FindSectionRange(headings, title, count)` generalises `FindIndexRange`. Map lines (`- FR-1: Epic 1 (1.1, 1.2)`) never count as definitions. The inventory is the only place where FR-n and NFR-n are defined |
| 9 | Questions (RF-39) | Default suffix, or pre-assigned | **Pre-assigned**: `OpenQuestions.DevelopmentSuffix = Suffix("Equipo de desarrollo", "antes de construir")`. The composer call becomes `OpenQuestions(title, none, suffix = DefaultSuffix)`. Both new documents use `## Preguntas para desarrollo` with that suffix, so they never block Aprobar. The Architecture always has the stack question first: "¿Qué tecnología (lenguajes, marcos y versiones) usará la solución?" |
| 10 | Epics model shape and validation | Flat slots (cannot nest), or a nested schema | **The nested schema below.** `EpicsReply.TryParse` returns Malformed in these cases: the root is not an object, there is an unknown top-level key, a field has the wrong JSON type, or no story is left after filtering. A story with a blank `title` or `i_want` is dropped. A story with no valid `covers` is dropped. An epic with no stories left is dropped. Covers are canonicalised and filtered to PRD FR and NFR ids |
| 11 | Caps (fixed) | Unbounded, or fixed caps | **`MaxEpics=6`, `MaxStoriesPerEpic=5`, `MaxStories=20`, `MaxCriteriaPerStory=3`, `MaxCoversPerStory=6`, `MaxDecisions=10`, `MaxModelQuestions=10`.** Items beyond a cap are dropped; an FR that only a dropped story covered becomes uncovered |
| 12 | 20,000-character ladder | A separate loop, or the shared `DraftRenderer` rungs | **Shared rungs.** `LayoutBudget.For(answerBudget)` maps 1500/1000/600/300/100 to a field clip of 400/250/160/100/60 and to 3/3/2/2/1 criteria per story. FR texts in the inventory and the map are clipped the same way. After the last rung, `EpicsLayout` drops stories from the end until the draft fits. Each story removed saves about 560 characters and adds an id-only question of about 105, so the loop converges. Pathological case: an edited PRD with more than about 120 requirements can still exceed 20,000; then `Artifact.NormalizeContent` raises the existing "no puede superar" message |
| 13 | Uncovered FR | Silent, or a question | Coverage line `- FR-3: sin historia todavía (ver Q-n)` plus the question "¿Qué historia cubre FR-3?" (id only, to keep it short). Only FRs are mapped, as in BMAD's FR Coverage Map |
| 14 | Prompts | Extend `DraftPrompt`, or one prompt class per shape | **`ArchitecturePrompt` and `EpicsPrompt`** (internal static, C# raw strings), each with `System`, `Schema`, `UserContent` and `Parse`. The input is `<initiative>`, `<prd>` and, for Epics, `<architecture>`. Each block is `PromptText.Neutralize`d and clipped to 20,000 characters. There is **no `<answers>` block**: the approved PRD is the source. `GeminiArtifactDrafter` dispatches on type. Flat slots still go through `DraftComposer.ModelText` |
| 15 | Forged ids in model text | Keep the current regex, or extend it | `ForgedIdRegex` also strips a leading `Epic N`, `Story N.M`, `Épica N` or `Historia N.M`. The existing goldens do not change |
| 16 | Approval copy (RN-02) | A new button label, or confirmation text | The button stays "Aprobar". `ArtifactLabels.ApproveConfirmText(type)` keeps today's text for Spec, Brief and PRD. For the two new types: "Aprobar lo marca como el borrador que se entrega al equipo de desarrollo. No son decisiones técnicas finales: desarrollo lo revisa antes de construir." |
| 17 | Labels | | Slugs `arquitectura` and `epicas-e-historias`. Names "Arquitectura" and "Épicas e historias". Help: "Borrador de las decisiones técnicas principales, para que desarrollo lo revise." and "El trabajo dividido en épicas e historias, para que desarrollo lo revise." Reasons `WaitingForPrd` ("…apruebe primero el PRD.") and `WaitingForArchitecture` ("…apruebe primero la Arquitectura.") |
| 18 | Export | | Prefixes `architecture` and `epics`. Notes: save as `planning-artifacts/architecture/architecture-<proyecto>-<fecha>/ARCHITECTURE-SPINE.md` (input of `bmad-create-epics-and-stories` with the PRD), and as `planning-artifacts/epics.md` (input of `bmad-build` story by story). Both notes add "Es un borrador para revisión del equipo de desarrollo." Frontmatter additions: Architecture adds `type: architecture-spine`. Epics adds `inputDocuments: [prd.md, ARCHITECTURE-SPINE.md]` and `stepsCompleted` (exact values settled by BMAD check 3) |

## Interfaces / Contracts

```csharp
// Domain
public enum ArtifactType { Spec, Brief, Prd, Architecture, EpicsAndStories }   // string column, length 15 <= 20
// Application
public sealed record ArtifactSummary(ArtifactType Type, ArtifactStatus Status, int Version, DateTimeOffset UpdatedAt);
ArtifactAvailability += WaitingForPrd, WaitingForArchitecture;
ArtifactCatalog.Upstream(ArtifactType) : IReadOnlyList<ArtifactType>;
ArtifactCatalog.StaleUpstream(ArtifactType, IReadOnlyList<ArtifactSummary>) : ArtifactType?;
ArtifactView += ArtifactType? StaleUpstream;
ArtifactOutline: DevelopmentQuestionsTitle, CoverageMapTitle; ItemPrefix.ArchitectureDecision = "AD" (All: NFR, FR, UJ, SM, AD, A, Q)
// Infrastructure/Artifacts
internal sealed record DecisionItem(string Title, IReadOnlyList<string> Binds, string Prevents, string Rule);
internal sealed record CriterionItem(string Given, string When, string Then);
internal sealed record StoryItem(string Title, string AsA, string IWant, string SoThat, IReadOnlyList<string> Covers, IReadOnlyList<CriterionItem> Criteria);
internal sealed record EpicItem(string Title, string Goal, IReadOnlyList<StoryItem> Stories);
DraftItems += IReadOnlyList<DecisionItem>? Decisions, IReadOnlyList<EpicItem>? Epics, IReadOnlyList<string>? DevelopmentQuestions;
```

Architecture schema: `{paradigm: Items, decisions: [{title, binds: [string], prevents, rule}], deferred: Items, questions: [string]}`. All keys are required. The `paradigm` and `deferred` slots are "assumption or unknown only".

Epics schema: `{epics: [{title, goal, stories: [{title, as_a, i_want, so_that, covers: [string], criteria: [{given, when, then}]}]}], questions: [string]}`. Everything is required. There is no `maxItems`; code applies the caps.

Layouts:
- **Architecture** (`# Arquitectura: {name}`):
  - Paradigma: model items, or the assumption "lo propone el equipo de desarrollo al revisar este borrador".
  - Invariantes heredadas: code-owned lines. The list of PRD ids, "lo que el PRD deja fuera sigue fuera" and "las preguntas abiertas del PRD siguen vigentes".
  - AD blocks.
  - Mapa: the table `| Requisito | Qué pide | Decisiones |`, where the last column is the ADs whose binds include the FR, or "Ninguna todavía". `|` in a cell is escaped.
  - Diferido: the code-owned Tecnología (pila) and Esqueleto y convenciones entries come first, then the model items.
  - Then the questions and the index.
- **Epics** (`# Épicas e historias: {name}`), in this order:
  1. Resumen, with one assumption: one story per FR in the Fake.
  2. Inventario: `### Requisitos funcionales`, `### Requisitos no funcionales` and `### Requisitos adicionales` (`- **AD-n**: title` from the approved Architecture).
  3. Mapa de cobertura.
  4. Lista de épicas (`- Epic N: title. goal`).
  5. `## Epic N: title`, then its goal, then `### Story N.M: title`.
  6. Each story has `**As a** …, **I want** …, **So that** …`, then `**Cubre:** FR-1, NFR-2`, then `**Acceptance Criteria:**` with `- **Given** … **When** … **Then** …`.
  7. Questions and the index.
- **Fake Epics**: chunks the PRD FRs into epics of 5 ("Funcionalidades del PRD (parte k)"), one generic story and one criterion per FR.

## Data Flow

```
Generate(Architecture) -> initiative + answers + ListAsync -> Availability(...summaries) [WaitingForPrd?]
  -> DraftRequestAsync: PrdMarkdown = approved PRD content -> drafter
     Fake:   DraftRenderer -> ArchitectureLayout(PrdFacts)                      -> Markdown
     Gemini: ArchitecturePrompt -> GeminiClient -> Parse (binds filter, caps) -> DraftRenderer -> Markdown
  -> Artifact.Create -> ReloadView (StaleUpstream computed from summaries)
Generate(Epics): same, + ArchitectureMarkdown; EpicsLayout computes coverage, uncovered FR -> pre-assigned Q
```

## File Changes (authored lines; goldens excluded)

All paths are under `src/BmadPlatform.*` or `tests/`.

| Slice | Files | Lines |
|---|---|---|
| **S1** enum, signature, staleness, labels | `Domain/Artifacts/ArtifactType`; `Application/.../{ArtifactCatalog, ArtifactSummary, ArtifactView, ArtifactViewBuilder, ArtifactTexts}`, `{Generate,Regenerate,Approve}…Handler`; `Infrastructure/Artifacts/ArtifactRepository`; `Web/.../{ArtifactLabels, ArtifactDocument.razor}` (stale note, approval copy); tests `ArtifactCatalogTests` (15 refs), `InMemoryArtifactRepository`, `ArtifactViewBuilderTests`, `ArtifactLabelsTests` | ~340 |
| **S2** outline and checker | `Application/.../Markdown/{ArtifactOutline, ItemPrefix, ArtifactFormatChecker, MarkdownLines, OpenQuestions, RequirementItems, ArchitectureDecisionItems}`; `Infrastructure/Artifacts/DraftComposer` (suffix, forged ids); tests `ArtifactFormatCheckerTests`, `OpenQuestionsTests`, reader Theory | ~290 |
| **S3** Fake Architecture | `DraftRequest`, `ArtifactGuards.DraftRequestAsync`, handlers; `ArtifactCatalog` (switch on Architecture); `Infrastructure/Artifacts/{PrdFacts, ArchitectureLayout, LayoutBudget, DraftItem, DraftRenderer}`; `Export/MarkdownArtifactExporter`; tests: golden row `architecture-large.md` (input `prd-large.md`), upstream Theory, Generate passes the PRD, exporter row | ~400 |
| **S4** Fake Epics | `ArtifactCatalog` (switch on Epics); `Infrastructure/Artifacts/{EpicsLayout, DraftItem, DraftRenderer}`; exporter; tests: golden `epics-large.md` (inputs from the two goldens), coverage Theory, budget test (40 long FRs, 20 full stories, at most 20,000) | ~420 |
| **S5** Gemini Architecture | `Infrastructure/Artifacts/{ArchitecturePrompt, GeminiArtifactDrafter}`; `GeminiArtifactDrafterTests` (binds filter, numbering, drop, fallback, Malformed, prompt without answers) | ~330 |
| **S6** Gemini Epics and docs | `Infrastructure/Artifacts/{EpicsPrompt, EpicsReply, GeminiArtifactDrafter}`; tests (validator Theory, caps); `REQUISITOS.md` (RF-36, 39, 44, 52, 54, 73, 74; RN-02), `AGENTS.md` §6.3, `docs/bmad-referencia.md` §4, `docs/mejoras-posteriores.md` (web-verified stack, cascade invalidation) | ~450 (exception) |

The total is about 2,230 lines.

Between S3 and S5, the provider `Gemini` throws `NotSupportedException` for the new types, which the page shows as `ActionFailed`. This is accepted because the default provider is Fake.

## Testing Strategy (xUnit, lean, Theory-grouped)

| Area | Cases |
|---|---|
| Catalog | One Theory of (type, PRD status, Arch status) to availability. Covers `IsSetAvailable` ignoring upstream, and a `StaleUpstream` Theory (before, after, missing) |
| Checker and readers | Theory rows: duplicate `AD-n` heading warns; `AD-0` warns; a repeated FR inside the coverage map is silent; a repeated FR outside it warns; reader rows for bold and plain lines, the index skip and first-wins |
| Goldens | Two new rows, both with zero warnings, `Unassigned` empty, no `\b(TBD\|TODO)\b` and monotonic ADs. `lint_spine.py` is run manually on the exported golden |
| Layouts | Uncovered FR becomes a question with the dev suffix; invalid covers and binds are dropped; caps are enforced; the 20,000-character limit holds |
| Gemini | Stub HTTP: numbering, filtering, zero-AD fallback, the Malformed Theory, `<prd>`/`<architecture>` present, `<answers>` absent |
| Unchanged | Existing goldens, `ModuleBoundaryTests` |

**Module boundary.** Artifacts sources forbid `\bConversation\b` and the Initiatives entity token. Prompt literals must therefore never contain a capitalised "Conversation", or "Initiative" not followed by `, ; )`. Use the lowercase `<initiative>`. No Assistant-module file changes.

## Threat Matrix

N/A: there is no routing, shell, subprocess, VCS/PR automation, executable classification or process-integration boundary. The model output is schema-checked, sanitized with `ModelText` and rendered by code.

## Migration / Rollout

No migration: the type is stored as a string. Rollback: `Assistant:Provider=Fake`, or revert the slices in reverse order.

## Open Questions

- [x] 1. (Accepted by the user, 2026-10-14; cascade invalidation recorded in `docs/mejoras-posteriores.md`.) Re-approving the PRD can complete a Large set (Ready to build) while the Architecture or Epics is stale. The staleness note only advises. Recommendation: accept, and record cascade invalidation in `docs/mejoras-posteriores.md`.
- [x] 2. (Resolved by checks 1 and 3 on 2026-10-14, `docs/bmad-referencia.md` section 7 bis.) Three formats to confirm with BMAD 6.12.1 in the S3 and S6 checks. Only the exporter and the fixed texts would change:
  - whether the `lint_spine.py` substring and TODO checks are case-sensitive (the Spanish "todo" would match a case-insensitive check);
  - the spine frontmatter keys;
  - the exact `stepsCompleted` values for epics.
  - **S3 observation (task 3.9, 2026-10-14, scratch BMAD 6.12.1):** `lint_spine.py` on the exported Architecture golden (frontmatter plus note plus body) reports `ok: true`, 0 findings. The placeholder check is case-sensitive (`\b(TBD|TODO|FIXME|XXX)\b`, no ignore-case): a probe with "Todo", "todo" and "TODO" flagged only the uppercase one, so Spanish "todo" is safe. The AD field check lowercases the block and looks for the substrings `binds`, `prevents`, `rule`, so `Vincula (binds)` etc. pass; the heading regex is `^#{2,4}\s*AD-(\d+)\b`. The linter requires no frontmatter key (it only scans the frontmatter for placeholders and `{token}`); the spine template lists `name, type, purpose, altitude, paradigm, scope, status, created, updated, binds, sources, companions`, and the exporter writes `title, type, status, created, updated`. No label or exporter change was needed. Still open: the `stepsCompleted` values (S4/F.3) and whether the spec's full list of spine keys should be exported (decision: design wins, only `type` is added). Also noted for F.3: `bmad-create-epics-and-stories` step 1 searches `{planning_artifacts}/*architecture*.md` and `{planning_artifacts}/*architecture*/index.md`; the export note's target path `architecture/architecture-<proyecto>-<fecha>/ARCHITECTURE-SPINE.md` matches neither pattern, so check 2 must confirm how the skill finds the spine.
