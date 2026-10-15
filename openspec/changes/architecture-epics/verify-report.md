```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:f5e8ffd058c2f9cf8b5d068e4622ded648fe5886d7cff10d06e68978fde4e0d4
verdict: fail
blockers: 3
critical_findings: 3
requirements: 6/23
scenarios: 69/98
test_command: dotnet test -c Release --no-build
test_exit_code: 0
test_output_hash: sha256:6d9d78352ae0b7394535055e7d710a79a47224569119c4d3e8adda45cfbd2ae8
build_command: dotnet build -c Release --no-restore
build_exit_code: 0
build_output_hash: sha256:44e9747c829557fb6e704e96c7e8320cb3674493a5bffe92cdc4ff1c0003b351
```

## Verification Report

**Change**: architecture-epics (PROPUESTA-MVP step 6)
**Version**: N/A (delta specs `artifacts` and `initiatives`)
**Mode**: Standard (strict_tdd false)
**Commit verified**: `206cb37` on branch `fix/arch-epics-verify` (main, slices S1-S6 and F merged)
**Persistence**: openspec file written. Engram: not saved, this executor has no `mem_*` tool (see the end).

**Verdict: FAIL** (3 CRITICAL, 9 WARNING, 7 SUGGESTION).

Reading guide: the build is clean and all 1,375 tests pass. No shipped behavior was found to be wrong or unsafe. The FAIL comes from the skill rule that every spec scenario needs a passing covering test and a spec that matches the code: (1) three spec MUSTs of the format checker were silently descoped, (2) the spec text was never aligned with the design it lost to, (3) five [UNIT] scenarios on the dependent-document handlers have no test. All three are closeable without redesign (see "Path to PASS").

### Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 59 top-level items (S1 11, S2 7, S3 11, S4 8, S5 6, S6 12, F 4) |
| Tasks complete | 58 |
| Tasks incomplete | 1 (`F.4`, `tasks.md:165`) |

- `F.4` ("Tick the success criteria of `proposal.md` ... Then run the independent verify") is open, and `proposal.md:83-86` still has four unticked success criteria. It is the closing task that this verify feeds, so it is a WARNING (cleanup), not a reason to skip the suite.
- The Chrome-walkthrough sub-bullets under tasks 1.10, 3.10, 4.7 and 5.5 (`tasks.md:69-73, 101-108, 122-127, 138`) are all `[ ]` while their parents are `[x]`. The repo holds no artifact proving the walks. The only recorded UI evidence is the live Gemini note in `docs/gemini-evaluacion.md:126-135`. See W1.
- There is no `apply-progress` file in `openspec/changes/architecture-epics/`, and this executor has no Engram tool, so apply-progress could not be read. Evidence below comes from code, tests and docs only.

### Build and Tests Execution

Commands run from `C:\repos\tfm`, no `BmadPlatform.Web` process running.

**Restore**: passed (`dotnet restore --ignore-failed-sources -p:NuGetAudit=false -p:WarningsNotAsErrors=NU1507`).

**Build**: passed. `dotnet build -c Release --no-restore` gives 0 errors and 8 warnings, all `NU1507` (two package sources, central package management). Zero code warnings.

**Tests**: 1,375 passed, 0 failed, 0 skipped (`dotnet test -c Release --no-build`, exit 0).

| Project | Passed |
|---|---|
| BmadPlatform.ArchitectureTests (includes ModuleBoundaryTests) | 24 |
| BmadPlatform.Web.Tests | 205 |
| BmadPlatform.Application.Tests | 769 |
| BmadPlatform.Infrastructure.Tests | 377 |

`test_output_hash` and `build_output_hash` in the envelope are the SHA-256 of the captured console logs of these two runs. `evidence_revision` is SHA-256 of `HEAD commit hash + build hash + test hash`.

**Coverage**: not available (no coverage tool configured).

**Extra probes run by the verifier (not part of the suite)**:
- ReDoS: the regex shapes used on user and model text (`LinkRegex`, `AssumptionMarkerRegex`, `ForgedIdRegex`, `PlaceholderRegex`, `RequirementItems.ItemRegex`) were run on 20,000-character adversarial lines (`[` x20000, `{` x20000, `<` x20000, `[a` x10000, 20,000 spaces). Worst case 17 ms. No timeout risk.
- Secrets: `git grep` for `AIza...` keys and for `ApiKey` in `*.json`: nothing. Only `GeminiOptions.cs` and `GeminiClient.cs` name `ApiKey`.
- Views: `style=` and `<script` in `src/BmadPlatform.Web/**/*.razor`: only the pre-existing module scripts in `App.razor`, `Login.razor`, `Register.razor`. No hit in the files this change touched.

### Spec Compliance Matrix

Legend: ✅ COMPLIANT (a passing test covers it, or recorded manual evidence exists for a [MANUAL]/[DOC] scenario). ✅* COMPLIANT against the design (the observable outcome holds, the spec wording differs and is on the amend list). ❌ DIVERGES (the spec literally contradicts the shipped design; amend the spec). ⚠️ PARTIAL. ❌ UNTESTED. 🖐 UI scenario with no recorded evidence.

**artifacts, R1 Stale-upstream hint (RF-54)**

| Scenario | Evidence | Result |
|---|---|---|
| PRD back in Draft marks the Architecture | `ArtifactViewBuilderTests > The_stale_upstream_hint_follows_the_timestamps` | ✅* (design: `ArtifactType?` from `UpdatedAt`) |
| Re-approving the PRD clears the hint | code keeps the hint until the dependent is edited, regenerated or approved (`ArtifactCatalog.cs:113`) | ❌ DIVERGES |
| Epics list both upstreams in Draft | `StaleUpstream` returns the first one only (`ArtifactCatalog.cs:111-117`) | ❌ DIVERGES |
| No dependent, no hint | `ArtifactViewBuilderTests > There_is_no_hint_when_the_dependent_does_not_exist`; `ArtifactCatalogTests > Stale_upstream_is_absent_...` | ✅ |
| The hint does not block or cascade | `ApproveArtifactTests > Approving_the_prd_again_after_an_edit_completes_with_the_dependents_untouched_even_if_stale` covers no cascade; no test saves or exports with a hint | ⚠️ PARTIAL |
| Hint in the page [UI] | markup at `ArtifactDocument.razor:134-137`; no walkthrough evidence | 🖐 |

**R2 Architecture document layout (RF-73)**

| Scenario | Evidence | Result |
|---|---|---|
| Sections and order | golden; `ArtifactOutline.cs:70-79` has 7 sections, spec says 9 | ❌ DIVERGES |
| AD blocks numbered by code and lint-safe | `ArchitectureLayoutTests > Decisions_are_numbered_by_code_...`; `GeminiArtifactDrafterTests > Architecture_decisions_are_numbered_...` | ✅ |
| Binds are filtered against the PRD | same tests | ✅ |
| A decision with no valid bind is dropped | same tests (Solo inventada) | ✅ |
| Capability map is computed | `ArchitectureLayoutTests > The_capability_map_and_the_questions_...` (table form, Ninguna todavía) | ✅* |
| No stack, stack deferred | `ArchitectureLayoutTests > The_stack_is_deferred_...` (Tecnología (pila), not Tecnologías y versiones) | ✅* |
| Stack named by the model is not a fact | `GeminiArtifactDrafterTests > A_stack_named_by_the_model_is_written_as_an_assumption_...` (Previene and Regla only; the title is plain, see W7) | ✅ |
| Placeholders are rejected | `ArchitectureLayoutTests > A_placeholder_never_reaches_the_document_...` (Theory x3) | ✅ |
| Identifier uniqueness | `ArtifactGoldenTests > The_architecture_equals_its_golden_...` (monotonic AD) plus zero checker warnings | ✅ |
| Golden passes the BMAD spine linter [MANUAL] | `docs/bmad-referencia.md` section 7 bis: ok true, 0 findings, 2026-10-14 | ✅ |

**R3 Epics and stories document layout (RF-74)**

| Scenario | Evidence | Result |
|---|---|---|
| Sections and order | golden; headings are Resumen, Requisitos adicionales, Mapa de cobertura de requisitos (`ArtifactOutline.cs:83-91`, `EpicsLayout.cs:118`), not the spec names | ❌ DIVERGES |
| Inventory keeps hyphenated numbers | `EpicsLayoutTests > The_inventory_copies_the_prd_numbers_with_their_gaps_...` | ✅ |
| Stories have the BMAD shape | `EpicsLayoutTests > The_fake_writes_one_story_per_functional_requirement_in_the_bmad_shape_...` | ✅ |
| Invalid citations are dropped | `EpicsLayoutTests > Citations_are_filtered_...` (Theory x3); no Decisiones line exists by design | ✅* |
| A story with no valid FR is dropped | `EpicsLayoutTests > A_story_without_a_valid_functional_requirement_is_dropped_...` | ✅ |
| Coverage map and uncovered requirement | `EpicsLayoutTests > Coverage_and_the_questions_come_from_the_final_stories` (`sin historia todavía (ver Q-n)`, not `sin cobertura`) | ✅* |
| Caps hold | `EpicsLayoutTests > Caps_hold_...` (6 epics, 5 stories, 3 criteria; spec says 5 epics) | ✅* |
| Budget ladder never cuts mid-structure | `EpicsLayoutTests > Forty_long_requirements_fit_the_cap_...` | ✅ |
| PRD without functional requirements | `EpicsLayoutTests > A_prd_without_functional_requirements_...` | ✅ |
| No forward dependencies | the Fake output is flat and nothing asserts the absence of later-story references; the model path relies on the prompt only | ⚠️ PARTIAL |
| Epics export accepted by BMAD [MANUAL] | `docs/bmad-referencia.md` section 7 bis, 2026-10-14 (content accepted; the discovery limit was recorded and fixed in the Architecture note) | ✅ |

**R4 Preguntas para desarrollo (RF-39)**

| Scenario | Evidence | Result |
|---|---|---|
| Questions are pre-assigned | `ArtifactGoldenTests` (owner Equipo de desarrollo, `Unassigned` empty) and `EpicsLayoutTests` | ✅ |
| Pre-assigned questions do not block approval | only the reader is tested (`OpenQuestionsTests > The_development_suffix_reads_as_assigned`); no Approve handler test with a new type | ⚠️ PARTIAL |
| Questions only in this section | not asserted anywhere (no section-scoped check) | ⚠️ PARTIAL |
| Assigning still works | `AssignQuestionOwners*` tests use the older types only | ⚠️ PARTIAL |
| Conversation answers are not used | `GenerateArtifactTests > Upstream_text_travels_only_while_that_document_is_approved_and_the_chain_carries_no_answers` | ✅ |

**R5 Approval copy (RN-02)**

| Scenario | Evidence | Result |
|---|---|---|
| Confirmation copy by type | `ArtifactLabelsTests > Approval_copy_mentions_development_review_only_for_the_draft_documents` (see W2 for the missing sentence) | ✅ |
| Button label unchanged [UI] | `ArtifactDocument.razor:164` | 🖐 |

**R6 Document pages and Artefactos card**

| Scenario | Evidence | Result |
|---|---|---|
| Large card lists the three documents [UI] | `ArtifactReadTests` (slots) but no browser evidence | 🖐 |
| Placeholder gone | `ArtifactReadTests` lines 57-60 (no `NotYetSupported` for Large) | ✅ |
| Walkthrough of a Large initiative with the Fake [UI] | `tasks.md` 4.7 sub-bullets unchecked | 🖐 |

**R7 Export**

| Scenario | Evidence | Result |
|---|---|---|
| File names and notes | `MarkdownArtifactExporterTests > Each_type_gets_its_file_name_...` | ✅ |
| Body unchanged | `MarkdownArtifactExporterTests > The_file_has_the_bmad_frontmatter_then_...` | ✅ |
| Frontmatter keys present | Architecture exports title, type, status, created, updated only (`MarkdownArtifactExporter.cs:28-33, 53`); the spec lists 12 spine keys | ❌ DIVERGES |
| Note mentions development review and questions | `...The_architecture_declares_its_kind_...` and `...The_epics_name_their_inputs_...` | ✅ |
| Existing types unchanged | same file, Brief/Prd/Spec rows | ✅ |

**R8 Gemini Architecture**

| Scenario | Evidence | Result |
|---|---|---|
| Composer renders model decisions | `GeminiArtifactDrafterTests:504` | ✅* |
| Model cannot write identifiers | `:504` and `The_model_cannot_forge_identifiers_or_markers` | ✅ |
| Unknown section or source is malformed | `:555-561` (Theory x6) | ✅ |
| Request carries the PRD and nothing else | `:593` | ✅ |
| Failure stores nothing | `:618` | ✅ |
| Fake is the default and deterministic | `ArtifactGoldenTests` (twice equal) | ✅ |
| Output passes validation with zero warnings | `:504`, `:527` | ✅ |
| Architecture invents no stack [MANUAL] | `docs/gemini-evaluacion.md:126-131` (live run 2026-10-14) | ✅ |

**R9 Gemini Epics**

| Scenario | Evidence | Result |
|---|---|---|
| Nested reply is rendered by the composer | `GeminiArtifactDrafterTests:677` | ✅ |
| Model cannot write numbering | `:677` and `DraftComposerTests` forged-id rows | ✅ |
| Structurally invalid reply is malformed | `:718-726`; a story with no criteria is NOT malformed, it receives a default criterion (`EpicsLayout.cs:369-372`), and an epic without stories is dropped | ❌ DIVERGES |
| Unknown criterion becomes a question | the schema has no per-criterion source (`EpicsPrompt.cs:57-103`) | ❌ DIVERGES |
| Coverage is computed from the final document | `:677` | ✅ |
| Request has no answers or identifiers | `:764` | ✅ |
| Failed regeneration changes nothing | `RegenerateArtifactTests > A_failed_regeneration_with_a_reason_changes_nothing_...` and `:789` | ✅ |
| Fake Epics equal the golden | `ArtifactGoldenTests > The_epics_equal_their_golden_...` | ✅ |
| Large initiative end to end with a live key [MANUAL] | `docs/gemini-evaluacion.md:132-135` | ✅ |

**R10 Real-BMAD checks (docs)**: Checks recorded [DOC] ✅ (`docs/bmad-referencia.md` section 7 bis, three checks, 2026-10-14; improvement listed in `docs/mejoras-posteriores.md:28`).

**R11 Required set per level**

| Scenario | Evidence | Result |
|---|---|---|
| Required set per level | `ArtifactCatalogTests > Required_set_per_level` | ✅ |
| Architecture is rejected outside Large | no Standard + Architecture row in `GenerateArtifactTests:50-52` or `ArtifactCatalogTests:162-166` (the generic NotRequired path is covered) | ⚠️ PARTIAL |

**R12 Availability and explicit generation**

| Scenario | Evidence | Result |
|---|---|---|
| Architecture needs an approved PRD | `ArtifactCatalogTests > Dependent_documents_wait_...`; `GenerateArtifactTests:219-226` | ✅ |
| Architecture available after the PRD is approved | same | ✅ |
| Epics need an approved Architecture | `ArtifactCatalogTests:209-210` | ✅ |
| Chain in order | `ApproveArtifactTests > Large_is_ready_to_build_only_when_...` | ✅ |
| Regenerating the PRD does not touch dependents | `ApproveArtifactTests > Regenerating_the_prd_leaves_the_approved_dependents_alone` | ✅ |
| Architecture unavailable once ReadyToBuild | only the older types are tested (`ArtifactCatalogTests > Ready_to_build_keeps_documents_available_...`) | ⚠️ PARTIAL |

**R13 Approval and completion**

| Scenario | Evidence | Result |
|---|---|---|
| Large completes with the three documents | `ApproveArtifactTests:96` Theory | ✅ |
| Partial Large set does not complete | `ApproveArtifactTests:96` | ✅ |
| Approving a dependent with a Draft upstream is rejected | the rule is proven in `ArtifactCatalogTests:211`; no handler test (`tasks.md` 1.5 admits it; its stated reason, the catalog lists no type, no longer holds) | ❌ UNTESTED at handler level |
| Editing the PRD reopens, re-approving completes | `ApproveArtifactTests:107, 118` | ✅ |
| Pre-assigned questions do not reject approval | no Approve test on a new type with its own questions | ⚠️ PARTIAL |

**R14 Generated content structure**

| Scenario | Evidence | Result |
|---|---|---|
| Architecture and Epics order | headings differ from the spec lists (`spec.md:468-469`) | ❌ DIVERGES |
| Citations keep upstream numbers | `EpicsLayoutTests` (FR-1, FR-3 gap), `ArchitectureLayoutTests` | ✅ |
| Numbers are unique and ordered | goldens plus `Assert.Empty(ArtifactFormatChecker.Check(...))` | ✅ |

**R15 Deterministic fake**: Architecture and Epics goldens ✅; Existing goldens unchanged ✅ (`ArtifactGoldenTests` rows spec, brief, prd).
**R16 Model-backed drafting**: no own scenarios; the guarantees are verified through R8 and R9. ✅

**R17 Format warnings**

| Scenario | Evidence | Result |
|---|---|---|
| Generated Architecture and Epics have no warnings | goldens, `ArchitectureLayoutTests`, `EpicsLayoutTests` | ✅ |
| Coverage map does not duplicate the inventory | `ArtifactFormatCheckerTests > A_requirement_repeated_is_silent_only_inside_the_coverage_map` | ✅ |
| Malformed AD identifier | `ArtifactFormatCheckerTests:329` (AD3) | ✅ |
| Non-increasing AD numbers | not implemented (`ArtifactFormatChecker.cs:54-63`), no test | ❌ UNTESTED |
| AD block without a label | not implemented, no test | ❌ UNTESTED |
| Missing Epics section | `ArtifactFormatCheckerTests > Missing_section_is_named_...` plus the Epics outline | ✅ |

**R18 Regeneration behind confirmation**: Regenerating the Architecture resets its questions ❌ UNTESTED; Regenerating needs an approved upstream ❌ UNTESTED; No cascade (Architecture regenerated, Epics untouched) ❌ UNTESTED. `RegenerateArtifactTests` has no Architecture or Epics case.

**R19 Open-question triage line**: Pre-assigned suffix is read as assigned ✅ (`OpenQuestionsTests > The_development_suffix_reads_as_assigned`).

**R20 Identifier scheme and BMAD reconciliation**: Only hyphenated identifiers are generated ✅ (`EpicsLayoutTests:55`, goldens); No mapper exists [DOC] ✅ (no converter in `src`); Documents agree [DOC] ⚠️ PARTIAL (stale `FR1:` statements remain, see W6).

**R21 Real-BMAD verification checklist**: Checklist item 2 agrees with the decision [DOC] ✅ (`docs/bmad-referencia.md:71`).

**initiatives spec**

| Scenario | Evidence | Result |
|---|---|---|
| Two of three do not complete | `ApproveArtifactTests:96` (Architecture row) | ✅ |
| Third approval completes | `ApproveArtifactTests:96` (Epics row) | ✅ |
| Editing any of the three reopens | `ApproveArtifactTests:107` | ✅ |
| Re-approving the PRD after an edit completes again | `ApproveArtifactTests:118` | ✅ |
| Status follows the three documents [UI] | no evidence | 🖐 |
| Large card lists the three documents [UI] | no evidence | 🖐 |

**Compliance summary**: 69 of 98 scenarios compliant (artifacts 65/92, initiatives 4/6). Of the 29 not counted: 8 DIVERGES (the spec text loses to the design), 21 PARTIAL, UNTESTED or UI without evidence. Requirements fully compliant: 6/23 (Gemini Architecture, Real-BMAD checks, Deterministic fake, Model-backed drafting, Open-question triage, Real-BMAD checklist).

### Correctness (Static Evidence)

| Area | Status | Notes |
|---|---|---|
| Availability chain | ✅ Implemented | `ArtifactCatalog.cs:70-96`: coverage first, then upstream in order (PRD, then Architecture). Epics with a Draft PRD and an Approved Architecture gives `WaitingForPrd` (spec line 391 says only the Architecture is checked; design D1 wins) |
| `IsSetAvailable` coverage-only | ✅ Implemented | `ArtifactCatalog.cs:163-169`; `Approving_the_prd_again_after_an_edit_...` proves a Large set with the PRD back in Draft completes again when the PRD is approved, with the three documents involved |
| Stale hint | ⚠️ Implemented as D3 | timestamp compare; see W4 for the cases where it is cleared by an unrelated action |
| Upstream text in the request | ✅ Implemented | `ArtifactGuards.DraftRequestAsync` (`ArtifactGuards.cs:94-123`): PRD and Architecture text only while Approved, owner-scoped, no answers for the chain |
| AD / Epic / Story id forging | ✅ Implemented | `DraftComposer.ModelText` strips leading FR/NFR/SM/UJ/A/Q/AD-n, Epic N, Story N.M, Épica, Historia, assumption markers and Responsable/Revisar labels (`DraftComposer.cs:391-410, 536`); numbers come from `Next()` and loop indexes |
| Bind and cover filtering | ✅ Implemented | `PrdFacts.CanonicalId` plus `Knows`, de-duplicated, PRD order (`ArchitectureLayout.cs:148-159`, `EpicsReply.ValidCovers`); a bind-less decision and an FR-less story are dropped |
| Placeholders | ✅ Implemented | `ArchitectureLayout.HasPlaceholder` (TBD/TODO/FIXME/XXX, braces and angle tokens); a regex timeout counts as a placeholder. Gap: see S1 |
| Caps and the 20,000 ladder | ✅ Implemented | `DraftRenderer.cs:16-38` and `LayoutBudget`; test with 40 long FRs. More than about 120 requirements still ends in the existing no-puede-superar message (design D12 accepts it) |
| Empty or null PRD | ✅ Implemented | `MarkdownLines.Split(null)` is safe; Architecture writes assumption lines, Epics writes the no-FR question; the no-FR case is tested |
| Regex hazards | ✅ OK | every new `GeneratedRegex` carries a timeout except `ArtifactFormatChecker.AdHeadingRegex` (S2); the probes show no blow-up |
| Module boundary | ✅ OK | `ModuleBoundaryTests` pass; prompts use the lowercase `<initiative>` |
| Logs content-free (RNF-02) | ✅ OK | `GeminiClient` logs label, status, duration, tokens; the drafter logs the type and the reason; `GeminiArtifactDrafterTests:618, 789` assert no prompt, draft or key in the log entries |
| Secrets | ✅ OK | no key in the repository |
| Export | ✅ Implemented | prefixes, `type: architecture-spine`, Epics `stepsCompleted` and `inputDocuments`, notes with target path and skill, mention of Preguntas para desarrollo |
| REQUISITOS RF-36/39/52/54/73/74 | ✅ Match the code | |
| REQUISITOS RF-44, RN-02 | ⚠️ Do not match | RF-44: stories cite AD-n (W3). RN-02: the confirmation must say the person reviewed it (W2) |

### Coherence (Design)

| Decision | Followed? | Notes |
|---|---|---|
| D1 Availability with summaries, private `Coverage`, `Upstream` order | ✅ Yes | |
| D2 `DraftRequest` upstream strings, `DraftRequestAsync` | ✅ Yes | |
| D3 `UpdatedAt` stale hint, single `ArtifactType?` | ✅ Yes | see W4 |
| D4 `RequirementItems`, `ArchitectureDecisionItems`, `PrdFacts` | ✅ Yes | |
| D5 7 sections | ✅ Yes | the spec still says 9 |
| D6 AD numbering, drop bind-less, max 10, Fake rule | ✅ Yes | no closing note for dropped decisions (W5) |
| D7 lint-safe labels, checker heading branch | ✅ Yes | the branch covers duplicates, AD-0 and malformed only (CR1) |
| D8 coverage map skipped by the checker | ✅ Yes | |
| D9 pre-assigned suffix | ✅ Yes | |
| D10 nested schema and validator | ✅ Yes | |
| D11 caps 6/5/20/3/6/10/10 | ✅ Yes | |
| D12 shared ladder | ✅ Yes | |
| D13 uncovered FR question | ✅ Yes | |
| D14 prompts without answers | ✅ Yes | |
| D15 forged-id regex | ✅ Yes | |
| D16 approval copy | ✅ Yes | the text lacks the you-reviewed-it sentence of RN-02 (W2) |
| D17 labels | ✅ Yes | |
| D18 export | ✅ Yes | |
| Design Open Questions 1 and 2 | ⚠️ Resolved in practice | boxes still unticked (`design.md:137-138`, S5) |

### Issues Found

**CRITICAL (3)**

- **CR1. Three MUSTs of the Format warnings requirement are not implemented and have no test.** The spec (`spec.md:526, 544-552`) requires warnings for an AD heading not strictly increasing, an AD block missing any of binds, prevents or rule, and an Epics document with FRs in the inventory but no `## Epic N` heading. `ArtifactFormatChecker.Check` (`ArtifactFormatChecker.cs:54-63`) only runs `CheckDefinition` on AD headings (duplicates, AD-0, malformed). Design D7 and task 2.2 scoped the checker to duplicates and AD-0 (gaps stay silent), but the descoped scenarios (Non-increasing AD numbers, AD block without a label) were never moved to the Reconciliation notes (`tasks.md:167-176`). Impact is low because warnings never block, but a user edit that removes a label or reorders the ADs gets no warning. Fix: add the two AD rules and the Epic-heading rule to the checker with Theory rows in `ArtifactFormatCheckerTests`, or delete the three clauses and the two scenarios from the spec.
- **CR2. The delta spec contradicts the shipped design in many places and was never aligned.** `tasks.md:5` says design wins and `tasks.md:167` says the spec text is to be aligned at verify or archive. Eight scenarios would fail if run literally (R1 x2, R2 sections, R3 sections, R7 frontmatter keys, R9 x2, R14 order) and about a dozen requirement sentences are wrong (see the amendment list). Archiving this spec would record behavior that does not exist. Fix: apply the amendment list below to `specs/artifacts/spec.md` (documentation only).
- **CR3. Five [UNIT] scenarios on the dependent-document handlers have no test, and the wiring they prove is not exercised.** Missing: (a) Approving a dependent with a Draft upstream is rejected, (b) Regenerating needs an approved upstream, (c) Regenerating the Architecture resets its questions to the pre-assigned suffix, (d) No cascade for an Architecture regeneration, (e) Assigning still works on an Architecture. `ApproveArtifactTests`, `RegenerateArtifactTests` and `AssignQuestionOwnersTests` have no Architecture or Epics case besides the Large chain tests. The `ListAsync` plus `Availability(..., summaries)` calls in `ApproveArtifactCommandHandler.cs:36-37` and `RegenerateArtifactCommandHandler.cs:38-39` could be removed without any test failing. Task 1.5 deferred this because the catalog listed no type for the new documents; the catalog has listed them since S3 and S4, so the tests can be written now. Fix: add the five cases using `ArtifactTestContext` (the `LargeApprovedThrough` helper already exists at `ApproveArtifactTests.cs:79`).

**WARNING (9)**

- **W1. No evidence for the six [UI] scenarios and the Chrome walkthroughs.** Tasks 1.10, 3.10, 4.7 and 5.5 are ticked with unticked sub-bullets (`tasks.md:69-73, 101-108, 122-127, 138`); AGENTS.md section 5.4 requires the walk and nothing in the repo records it except the live Gemini notes. Also `F.4` and `proposal.md:83-86` are still open. Fix: run the Large walk once (Fake), record the result, then tick F.4 and the proposal criteria.
- **W2. The approval confirmation omits that approving means the person reviewed the draft (RN-02).** `ArtifactLabels.cs:134-136` says the approval marks the draft as the one handed to the development team and that development reviews it. `REQUISITOS.md:96` (RN-02) and `spec.md:202` require the copy to say approving means the product person reviewed it, not that it is final. Fix: add that sentence and a row in `ArtifactLabelsTests`.
- **W3. Stories do not cite AD-n, but RF-44 says they do.** `REQUISITOS.md:43` (each story cites the FR-n, NFR-n and AD-n it implements) and `spec.md:110, 312` (the Decisiones line and `decisions[]`) versus `EpicsLayout.cs:209-211` (only the Cubre line) and the `EpicsPrompt` schema (no `decisions`). The S4 note at `tasks.md:175` records the descope but RF-44 and the spec still promise it. The Architecture decisions reach the Epics only as inventory lines. Fix: reword RF-44 and the spec, or add the citation.
- **W4. The stale hint is cleared by actions that do not refresh the dependent.** `Artifact.Approve` sets `UpdatedAt` (`Artifact.cs:99-100`) and so does any edit, including a question assignment, so approving or assigning a question on a stale Epics or Architecture makes the hint disappear without regenerating. In the other direction any PRD edit, even a typo, raises it, and the note text in `ArtifactLabels.cs:140` (Cuando esté aprobado, use Regenerar) reads oddly when the upstream is already approved. Design D3 accepted the trade-off and `docs/mejoras-posteriores.md:29` records cascade invalidation, but not this specific loss. Fix: add a line to `mejoras-posteriores.md`, or later compare against a stored version link.
- **W5. Decisions beyond the cap vanish silently.** The spec (`spec.md:47`) asks for a closing line after the maximum; `ArchitectureLayout.cs:143-146` just stops at 10 and says nothing. For Epics the dropped stories surface as uncovered-FR questions, but dropped stories that cover only NFRs leave no trace. Fix: amend the spec or write the note.
- **W6. Documentation inconsistencies.**
  - `README.md:7` still reads paso 5 delivered with Arquitectura y Épicas pending, and `README.md:55` says Large shows Arquitectura and Épicas as available in a later step; both contradict `README.md:66`.
  - `docs/bmad-referencia.md:47` still states as the current rule that the epics step expects `FR1:` and `NFR1:`; `:50` says the BMAD checks are still to be confirmed although section 7 bis records them; `:64` says the three checks were not run above a table that records them; `:79` keeps the row title with `FR1:`.
  - `REQUISITOS.md:83` (RF-62) says regenerating leaves all questions unassigned, which contradicts RF-39 (`REQUISITOS.md:38`) for Architecture and Epics; `REQUISITOS.md:80` (RF-56) lists the identifier kinds without AD.
  - The PRD export note (`MarkdownArtifactExporter.cs:70`) still tells the user to save the PRD under `prds/prd-<proyecto>-<fecha>/prd.md`, a path the epics step cannot discover (finding of check 2, `bmad-referencia.md` section 7 bis). Only the Architecture note was adjusted.
- **W7. Two no-invented-fact guards are prompt-only.** RF-73 (no technology or version the PRD does not name) and no forward dependencies are not enforced in code on the Gemini path. The AD title is written plain (`ArchitectureLayout.cs:112`), so a title such as a named database with a version would appear as a fact, while the same text in Previene or Regla is marked an assumption (`GeminiArtifactDrafterTests:527` covers only those two). Mitigated by the system prompt and the live run. Fix: mark the title as an assumption when the decision came from the model, or record the limit in the spec.
- **W8. Epics `stepsCompleted` is misleading for a finished document.** `MarkdownArtifactExporter.cs:57-59` writes `stepsCompleted` with only step-01-validate-prerequisites and `status: approved` on a complete epics file; check 3 says `bmad-build` ignores both keys, so no harm today, but a BMAD workflow that resumes from `stepsCompleted` would restart at step 2. Fix: confirm with a BMAD epics-step run, or drop the key.
- **W9. Task bookkeeping.** `tasks.md:165` (F.4) is unchecked. Listed for the completeness table; the action is in W1.

**SUGGESTION (7)**

- S1. `EpicsLayout.Quote` (`EpicsLayout.cs:150`) copies PRD requirement text with `ModelText` but without the placeholder test, so a PRD line containing a TODO marker reaches the Epics inventory; `ArchitectureLayout.Fit` does test it. Use the same helper.
- S2. `ArtifactFormatChecker.AdHeadingRegex` (`ArtifactFormatChecker.cs:236`) has no match timeout, unlike the new readers (250 ms). The probe shows no hazard; add the timeout for uniformity.
- S3. The question lists can pass the 20-line section limit: Architecture reaches 21 (stack question, 8 requirement questions, 1 summary, the rejected-decision question and 10 model questions, `ArchitectureLayout.cs:16-20, 61-64`) and Epics has no cap on uncovered-FR questions (`EpicsLayout.cs:58-61`) plus 10 model questions. The per-FR question rule makes the Epics case intended; amend the baseline wording or add a closing line.
- S4. Dead and stale code: the `ArtifactSlot` doc comment still says Epics is listed but can never be generated (`ArtifactCatalog.cs:7-11`); `ArtifactAvailability.NotYetSupported` (`:23`) and its text and tests are unreachable for the catalog now; the `MarkdownArtifactExporter` class summary lists only four frontmatter keys (`:9-14`); the test `Large_never_completes_in_this_change` (`ArtifactCatalogTests.cs:199-203`) keeps a stale name (it only asserts that a lone PRD is incomplete).
- S5. `design.md:137-138` open questions are still unticked although tasks and docs resolve them.
- S6. `RequirementItems` takes the first `- **FR-n**:` line anywhere in the PRD, so an edited PRD that cites a requirement in a line in an earlier section (Glosario, Trabajos por hacer) wins over the real definition. Consider reading only the Funcionalidades section.
- S7. `EpicsLayout.Normalize` applies `Take(MaxEpics)` to the raw epics (`EpicsLayout.cs:310`) before story-level filtering; `EpicsReply` already removes epics without a valid story, so a valid seventh epic cannot be lost today, but the two places must stay in sync.

### Spec and doc lines to amend

`specs/artifacts/spec.md` (design wins, per `tasks.md:5`):

| Lines | Amend to |
|---|---|
| 9-41 (Stale-upstream hint and its 6 scenarios), 632 (A2) | rule: a dependent is stale when an upstream `UpdatedAt` is later than its own (edited, regenerated or approved after the dependent); `StaleUpstream` is one `ArtifactType?` (the first stale upstream); wording from `ArtifactLabels.StaleUpstreamNote`; drop the scenarios Re-approving the PRD clears the hint and Epics list both upstreams; add that the hint also clears when the dependent itself is edited or approved |
| 45, 55-57, 273, 468 | 7 headings: Paradigma de diseño, Invariantes heredadas, Invariantes y reglas, Mapa de capacidades, Diferido, Preguntas para desarrollo, Índice de suposiciones (no Propósito y alcance, no Convenciones de consistencia) |
| 47, 633 (A3), 634 (A4) | at most 10 decisions (not 12); no closing line for dropped decisions (or add it, W5); Previene and Regla always carry the assumption marker |
| 48 | Invariantes heredadas lists the PRD ids (FR and NFR) plus two fixed lines, not the NFR text |
| 49, 74-77 | the capability map is the table with columns Requisito, Qué pide, Decisiones; an unbound FR shows Ninguna todavía; the question is ¿Qué decisión de arquitectura cubre FR-n? |
| 50, 79-82 | Diferido entries Tecnología (pila) and Esqueleto y convenciones; the stack question is ¿Qué tecnología (lenguajes, marcos y versiones) usará la solución? |
| 106, 117-119, 469 | Epics headings: Resumen, Inventario de requisitos (Requisitos funcionales, no funcionales, adicionales with AD-n), Mapa de cobertura de requisitos, Lista de épicas, `## Epic N`, Preguntas para desarrollo, Índice de suposiciones |
| 109, 146-149, 634 (A4) | caps: 6 epics, 5 stories per epic, 20 stories, 3 criteria, 6 covers, 10 decisions, 10 model questions |
| 110, 131-135, 312, 478 | remove the Decisiones line and `decisions[]`, or restore them in code (W3) |
| 111, 141-144 | map line `- FR-n: Epic N (N.M)`; uncovered `- FR-n: sin historia todavía (ver Q-n)`; question ¿Qué historia cubre FR-n? |
| 112 | the ladder is the shared answer-budget rungs (field clip 400/250/160/100/60, criteria 3/3/2/2/1), then the trailing stories are dropped |
| 202 | the confirmation example text of `ArtifactLabels.ApproveConfirmText` (after fixing W2) |
| 239, 251-254 | the Architecture frontmatter adds only `type: architecture-spine`; the Epics add `stepsCompleted` and `inputDocuments` |
| 268, 273 | Architecture schema is paradigm, decisions (title, binds, prevents, rule), deferred, questions; no answer source (it is downgraded) |
| 312, 324-332 | Epics schema is epics (title, goal, stories (title, as_a, i_want, so_that, covers, criteria (given, when, then))) plus questions; no overview, no criterion source; a story without criteria gets a default criterion; Malformed when the root, a key or a type is wrong or no story survives; remove the scenario Unknown criterion becomes a question |
| 391 | Epics availability checks the PRD first, then the Architecture |
| 526, 539-552 | after CR1: either delete the AD-order, AD-label and Epic-heading clauses and the two scenarios, or keep them and implement |

Other documents:

| File and line | Amend |
|---|---|
| `REQUISITOS.md:43` (RF-44) | stories cite FR-n and NFR-n (AD-n only if W3 is implemented) |
| `REQUISITOS.md:83` (RF-62) | regeneration leaves questions unassigned except Architecture and Epics, which return pre-assigned |
| `REQUISITOS.md:80` (RF-56) | add AD to the kinds |
| `REQUISITOS.md:96` (RN-02) | keep; make the code match (W2) |
| `README.md:7, 55` | step 6 delivered; Large lists Architecture and Epics as generable draft documents |
| `docs/bmad-referencia.md:47, 50, 64, 79` | remove or date the expects-FR1 statement, the still-to-confirm sentence and the not-run sentence; retitle row 2 |
| `openspec/changes/architecture-epics/design.md:137-138` | tick open questions 1 and 2 |
| `openspec/changes/architecture-epics/proposal.md:83-86`, `tasks.md:165` | tick after W1 |
| `src/BmadPlatform.Infrastructure/Export/MarkdownArtifactExporter.cs:70` (PRD note) | same discovery advice as the Architecture note |

### Path to PASS

1. Apply the spec amendments above (CR2).
2. Add the five handler tests (CR3) and either implement or delete the three checker clauses (CR1).
3. Fix W2 (one sentence and one test row) and the README, bmad-referencia and REQUISITOS lines of W6.
4. Run one Chrome walk of the Large flow with the Fake, record it (W1), tick F.4.
5. Re-run verify. No redesign and no change to the drafters is needed.

### Verdict

**FAIL**

Build and all 1,375 tests are green and the shipped behavior matches the design, but archive is blocked by three closeable items: an unreconciled spec (CR2), three descoped checker MUSTs (CR1) and five untested handler scenarios (CR3).

### Engram

Not persisted to Engram: this executor has no `mem_save` tool. The orchestrator should save this file under topic `sdd/architecture-epics/verify-report` (type `architecture`, `capture_prompt: false`).
