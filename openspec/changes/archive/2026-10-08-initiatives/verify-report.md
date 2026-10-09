# Verification Report: initiatives

Change: initiatives | Branch: feat/initiatives (PR #2) | Mode: Standard (strict_tdd false) | Store: hybrid
Date: 2026-10-08

## Runtime evidence (executed by the verifier)

| Command | Exit | Result |
|---|---|---|
| `dotnet restore BmadPlatform.slnx --configfile <temp nuget.org-only config>` | 0 | up to date |
| `dotnet build BmadPlatform.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test BmadPlatform.slnx --configuration Release --no-restore` | 0 | 212 passed, 0 failed, 0 skipped |

Per project: Application.Tests 103, ArchitectureTests 12, Web.Tests 65, Infrastructure.Tests 32 (total 212). Matches the orchestrator numbers. Coverage tooling is not configured (not run).

Process note: the sdd-verify skill asks for `gentle-ai sdd-verify-validate` before persisting. The orchestrator forbade calling gentle-ai from this phase, so the validator was NOT run and the report is persisted on the orchestrator explicit instruction.

## Completeness

- Tasks: 1.1-4.5 (all implementation tasks) are `[x]` and every referenced file exists. The 8 manual Chrome items (tasks.md lines 80-87) were `[ ]`; they were checked off with a note each, based on the orchestrator walkthrough evidence. One gap is noted on item 82 (newest-first order has no explicit Chrome evidence).
- Spec: 12 requirements, 54 scenarios (45 [UNIT], 9 [UI]).

## Spec compliance matrix

Status key: COMPLIANT = implemented and a passing test (or, for [UI], Chrome evidence) covers it. "double" = the test runs against InMemoryInitiativeRepository, not SQL.

### R1 Initiative fields and validation (RF-05, RF-33)
Impl: InitiativeValidationRules.cs:9-18 (messages verbatim), Initiative.cs:60-82 (domain guard, same messages), validators under Features/Initiatives/*/.

| Scenario | Status | Test(s) |
|---|---|---|
| Name only is valid | COMPLIANT | InitiativeValidatorTests.Name_only_is_valid |
| Empty name rejected | COMPLIANT | Empty_or_whitespace_name_is_rejected (empty); InitiativeTests.Blank_name_is_rejected |
| Whitespace-only rejected | COMPLIANT | Empty_or_whitespace_name_is_rejected (spaces) |
| 120 chars accepted | COMPLIANT | Name_of_120_characters_is_accepted_and_121_is_rejected |
| 121 chars rejected | COMPLIANT | same test (exact message asserted) |
| Length after trimming | COMPLIANT | Name_length_is_measured_after_trimming; InitiativeTests.Name_over_limit_is_rejected_but_trimmed_limit_is_accepted |
| Description boundaries | COMPLIANT | Description_of_1000_characters_is_accepted_and_1001_is_rejected; InitiativeTests.Description_over_limit_is_rejected |
| Duplicate names allowed | COMPLIANT | InitiativeWizardHandlerTests.Duplicate_names_are_allowed_for_the_same_and_for_other_users; the only index is non-unique (model test Initiative_owner_is_a_plain_string_without_foreign_key_and_the_list_index_exists) |

### R2 Depth levels (RF-27)
| Scenario | Status | Test(s) |
|---|---|---|
| Level descriptions available | COMPLIANT | InitiativeLabelsTests.Small_yields_a_short_specification, Standard_yields_brief_and_prd, Large_yields_prd_architecture_and_epics_and_stories (InitiativeLabels.cs:58-64) |
| Selector shows deliverables [UI] | COMPLIANT (Chrome) | DepthSelector.razor:29-47; Chrome: selector shows deliverables |

### R3 Depth mode and automatic depth (RF-06, RF-28)
| Scenario | Status | Test(s) |
|---|---|---|
| Manual stores chosen level | COMPLIANT | InitiativeTests.Manual_mode_stores_the_chosen_level |
| Automatic leaves depth empty + label | COMPLIANT | InitiativeWizardHandlerTests.Automatic_mode_leaves_depth_empty; InitiativeDisplayTests.Depth_summary_covers_level_pending_and_undefined (InitiativeLabels.cs:45-48) |
| Switching to Automatic clears depth | COMPLIANT | InitiativeTests.Switching_to_automatic_clears_the_depth (Initiative.cs:114); InitiativeUpdateTests.Switching_to_automatic_while_clarifying_clears_the_depth |
| Manual requires level to finish | COMPLIANT | InitiativeTests.Complete_in_manual_mode_without_a_level_is_rejected_and_status_stays_draft; InitiativeWizardHandlerTests.Manual_mode_without_a_level_cannot_finish_and_stays_draft (Initiative.cs:143-146) |

### R4 Wizard with resumable draft (RF-30)
| Scenario | Status | Test(s) |
|---|---|---|
| Save draft with name only | COMPLIANT | InitiativeWizardHandlerTests.Saving_only_a_name_creates_a_draft_owned_by_the_current_user (UTC offset, owner, defaults) |
| Each step persists selections | COMPLIANT | Each_step_persists_selections_and_the_step_to_reopen; InitiativePersistenceTests.Saving_the_depth_persists_mode_level_and_step |
| Resume at saved step | COMPLIANT | Resume_returns_the_saved_step_description_and_mode |
| Finishing moves to Clarifying | COMPLIANT | Finishing_a_valid_draft_moves_it_to_clarifying; InitiativePersistenceTests.Completing_persists_the_new_status |
| Finishing a non-draft rejected | COMPLIANT | Finishing_a_non_draft_is_rejected_and_status_is_unchanged |
| Wizard navigation and draft banner [UI] | COMPLIANT (Chrome) | InitiativeWizard.razor:202-244; Chrome: resume at saved step with selections. The Draft banner on the detail page (InitiativeDetail.razor:51-56) has no explicit evidence; the list Continuar link does |

### R5 Mode and depth editability (RF-29)
| Scenario | Status | Test(s) |
|---|---|---|
| Change allowed Draft/Clarifying | COMPLIANT | InitiativeTests.Depth_can_change_in_draft_and_clarifying; InitiativeUpdateTests.Depth_can_change_while_clarifying; InitiativePersistenceTests.A_draft_can_change_its_depth_through_update |
| Change rejected Planning/ReadyToBuild | COMPLIANT | InitiativeTests.Depth_change_is_rejected_in_planning_and_ready_to_build (both statuses); InitiativePersistenceTests.Changing_the_level_is_rejected_once_planning_or_ready (both); Changing_the_mode_is_rejected_in_planning_and_nothing_changes (Planning only at handler level) |
| Name edit allowed outside Draft | COMPLIANT | InitiativeUpdateTests.Name_edit_is_allowed_in_planning_while_unchanged_mode_and_depth_pass; InitiativeTests.Name_can_be_edited_outside_draft_without_touching_mode_or_depth |
| Locked controls in UI [UI] | COMPLIANT (Chrome) | InitiativeEdit.razor:57-75; Chrome: edit in Planning shows mode and depth read-only with explanation |

### R6 Status never editable (RF-34)
| Scenario | Status | Test(s) |
|---|---|---|
| Update contract has no status | COMPLIANT | InitiativeValidatorTests.Update_contract_has_no_status_member (reflection on type and name; fails if a status member is added) |
| Editing fields keeps status | COMPLIANT | InitiativeUpdateTests.Editing_the_description_keeps_the_status |

### R7 Owner-only access (RF-26)
| Scenario | Status | Test(s) |
|---|---|---|
| Owner from current user | COMPLIANT | Saving_only_a_name_creates_a_draft_owned_by_the_current_user (commands carry no owner field; SaveInitiativeDetailsCommandHandler.cs:15) |
| Other user detail = not found | COMPLIANT (double) | InitiativeAccessTests.Other_users_detail_is_the_same_null_as_an_unknown_id |
| Other user cannot edit/delete | COMPLIANT (double) | Other_user_cannot_update_save_a_step_finish_or_delete (update, details step, depth step, finish, delete; zero UpdateAsync calls) |
| List excludes others | COMPLIANT (double) | InitiativeListTests.List_contains_only_the_callers_initiatives (2 vs 3) |
| Not-found page for foreign id [UI] | COMPLIANT (Chrome) | Chrome: second user gets the same not-found message for detail and edit of the first user id |

### R8 Listing, search, filters, order (RF-08, RF-31)
| Scenario | Status | Test(s) |
|---|---|---|
| Order by last modification | COMPLIANT (double) | List_is_ordered_by_last_modification_newest_first; real SQL OrderByDescending(UpdatedAt) at InitiativeRepository.cs:43 |
| Update moves to top | COMPLIANT | InitiativeUpdateTests.Update_moves_the_initiative_to_the_top_of_the_list; Wizard_steps_and_finish_refresh_the_last_modified_time |
| Search by name | COMPLIANT (double + Chrome) | Search_matches_names_ignoring_case_and_surrounding_whitespace. Real LIKE and collation behavior only via the SQL-translation model test and Chrome (accent-insensitive search) |
| Empty/whitespace search returns all | COMPLIANT | Empty_or_whitespace_search_returns_everything (empty, spaces, null) |
| Search with no match | COMPLIANT | Search_without_a_match_returns_an_empty_list |
| Filter by status | COMPLIANT | Filters_by_status_and_by_depth |
| Filter by depth | COMPLIANT | Filters_by_status_and_by_depth (Large only) |
| Combined search/status/depth | COMPLIANT | Search_status_and_depth_combine_with_and (all four subsets) |
| Filters matching nothing | COMPLIANT | Filters_matching_nothing_return_an_empty_list |
| No pagination | COMPLIANT | There_is_no_pagination (60) |
| Empty-state message [UI] | COMPLIANT (Chrome) | InitiativeList.razor:66-87; Chrome: empty state and no-match state with clear link |

### R9 Soft delete (RF-32)
| Scenario | Status | Test(s) |
|---|---|---|
| Deleted hidden from list | COMPLIANT (double) | InitiativeAccessTests.Deleting_hides_the_initiative_but_retains_the_record (with and without filters); model tests Initiative_has_a_single_soft_delete_query_filter and Initiative_search_translates_to_an_escaped_like_with_the_soft_delete_predicate cover the real filter |
| Deleted detail not found | COMPLIANT | same test (Get returns null) |
| Editing a deleted initiative | COMPLIANT | Deleted_initiative_cannot_be_updated_stepped_or_finished; InitiativePersistenceTests.A_stale_update_of_a_deleted_initiative_is_not_found_and_keeps_it_deleted |
| Deleting twice | COMPLIANT | Deleting_twice_is_not_found |
| Record retained | COMPLIANT (double + MySQL check) | Deleting_hides_the_initiative_but_retains_the_record; Chrome/MySQL: row kept with DeletedAt |
| Delete confirmation [UI] | COMPLIANT (Chrome) | InitiativeDetail.razor:65-82; Chrome: Cancelar and confirm buttons |

### R10 Detail view (RF-08, RF-09)
| Scenario | Status | Test(s) |
|---|---|---|
| Detail contents | COMPLIANT (weak assertion, see W3) | InitiativeListTests.Detail_carries_every_field |
| Status labels in Spanish | COMPLIANT | InitiativeLabelsTests.Status_labels_are_in_Spanish (4 values) |
| Placeholder sections [UI] | COMPLIANT (Chrome) | InitiativeDetail.razor:114-123,129-134; Chrome: three placeholders |

### R11 Authentication required
| Scenario | Status | Test(s) |
|---|---|---|
| Unauthenticated redirected [UI] | COMPLIANT (Chrome) | [Authorize] on all four pages; Routes.razor AuthorizeRouteView + RedirectToLogin; Chrome: redirect with ReturnUrl and return after login |
| Handler without current user | COMPLIANT | Handlers_fail_without_a_current_user_and_persist_nothing; Update_save_depth_and_complete_also_fail_without_a_current_user_and_persist_nothing (all 7 handlers; CurrentUserExtensions.cs:9-14) |

### R12 Navigation and language (RF-08)
| Scenario | Status | Test(s) |
|---|---|---|
| Nav link [UI] | COMPLIANT (Chrome) | MainNav.razor, MainLayout.razor:9-12; NavSectionTests for the active-state logic; Chrome: pill with aria-current |

Totals: 54 scenarios. COMPLIANT 54, PARTIAL 0, MISSING 0. The 9 [UI] scenarios are covered by Chrome evidence (see W2 for sub-gaps). No UNIT scenario lacks a passing test.

## Correctness (static)

| Check | Result |
|---|---|
| Domain has no external dependencies | OK: no project or package refs, enforced by LayerDependencyTests (passing) |
| Application has no EF Core | OK: csproj references only MediatR, FluentValidation, Logging.Abstractions; architecture theory passes for EF, Pomelo, AspNetCore, Serilog |
| One handler per use case | OK: 7 use cases, 7 handlers |
| Pages inject only IMediator (plus NavigationManager) | OK for the four initiative pages. MainNav and AppErrorBoundary inject NavigationManager or ILogger (UI plumbing). Home.razor injects IMediator (pre-existing) |
| No business logic in pages | OK: pages map exceptions via UserMessages and navigate. InitiativeEdit.depthEditable mirrors the domain rule for display only; the domain stays authoritative |
| UI text in Spanish | OK (all literals reviewed) |
| Validation messages verbatim | OK: the three spec messages match character for character in InitiativeValidationRules.cs and Initiative.cs |
| IDbContextFactory, one context per call | OK (InitiativeRepository.cs) |
| Table prefix, no FK, single filter, collation | OK: ini_initiatives, no FK, one HasQueryFilter, utf8mb4_0900_ai_ci; migration AddInitiatives creates only that table and its index |
| Log redaction and levels | OK: ToString overrides hide name, description and search term (test ToString_hides_names_descriptions_and_search_terms); Warning level for Validation, NotFound and Domain covered by LoggingBehaviorTests |
| Leftovers | none: no TODO or FIXME in src; working tree clean except untracked .claude/ (not part of the change) |

## RF-26..RF-34 coverage

| RF | Implemented | Where |
|---|---|---|
| RF-26 owner-only, not-found, owner id stored | Yes | ownerId on every repository method (InitiativeRepository.cs); tests in R7 |
| RF-27 three levels and deliverables in selector | Yes | InitiativeLabels.Deliverables, DepthSelector.razor |
| RF-28 Automatic leaves depth empty, pending text | Yes | Initiative.cs:114, InitiativeTexts.DepthPending |
| RF-29 lock in Planning and ReadyToBuild | Yes | Initiative.SetDepth guard; edit page read-only |
| RF-30 wizard with resumable draft, Clarifying at the end | Yes | CreationStep, InitiativeWizard.razor, Initiative.Complete |
| RF-31 search, status and depth filters, order, no pagination | Yes | InitiativeRepository.ListAsync, InitiativeList.razor |
| RF-32 soft delete | Yes | DeletedAt plus global query filter |
| RF-33 name 1-120 trimmed, duplicates allowed, description 1000 | Yes | validators and domain |
| RF-34 status not manually editable | Yes | no status member on any command; only Complete changes it |

RF-05, RF-06, RF-08 and RF-09 (also in scope) are implemented. RF-07 (ReadyToBuild on approval) is a SUPUESTO and out of scope.

## Design coherence

Followed: column-based CreationStep; nullable mode and depth; global soft-delete filter; required ownerId parameter; exception-based failures; LIKE with escaping and explicit collation; TimeProvider; one handler and validator per use case; thin pages with UserMessages and InitiativeLabels; the /editar page; redirect of non-Draft /crear to the detail page (replace navigation).

Deviations (all documented or benign):

| # | Deviation | Severity |
|---|---|---|
| D1 | UpdateInitiativeCommand.DepthMode is nullable (design: non-null); Initiative.SetDepth takes a nullable mode so a draft can keep an unfinished choice | Known, benign |
| D2 | InitiativeDetails.DepthPendingText was dropped; label logic lives in InitiativeLabels.DepthSummary (spec aligned in 6b1fe58) | Known |
| D3 | Edit page /iniciativas/{id}/editar is in the design but not in the spec list of routes; it is [Authorize] and works | Known |
| D4 | InitiativeRepository.UpdateAsync loads a tracked row through the global filter and uses SetValues, instead of context.Update on a detached entity. Safer (a stale edit cannot resurrect a deleted row); recorded only in a code comment, not in design.md | Benign |
| D5 | Extra ordering tie-break ThenByDescending(Id) in ListAsync | Benign |
| D6 | Files not in the design table: InitiativeTexts.cs, InitiativeValidationRules.cs, InitiativeNotFound.razor, InitiativeListCriteria.cs, MainNav.razor, NavSection.cs, InitiativePersistenceTests.cs | Benign |
| D7 | Changes outside the initiatives module from the review-fix and UI commits: MySqlDatabase command timeout of 30 s (plus test), AppErrorBoundary recovers on navigation and its message text changed, theme.css warning and success tokens | Benign, not mentioned in proposal or design |
| D8 | Complete also sets CreationStep to Review; wizard step semantics as in apply-progress | Known |
| D9 | Behaviors live in Application/Common/Behaviors (tasks.md says Application/Behaviors) | Known |
| D10 | List state in the query string (q, estado, nivel) and responsive card and table layout | Extra, benign |

## Issues

### CRITICAL
None.

### WARNING
- W1. Repository SQL behavior has no automated test: owner predicate, ORDER BY, status and depth filters, LIKE escaping, collation-based case and accent insensitivity, and the DateTimeOffset round trip are covered only by model and SQL-translation tests, the in-memory double (which uses OrdinalIgnoreCase, so it does not exercise the collation) and the orchestrator manual MySQL and Chrome check. Removing the owner predicate from InitiativeRepository.GetAsync or ListAsync would not fail any automated test. This is a documented limitation (integration tests are out of scope) but it is the riskiest area for RF-26.
- W2. Evidence gaps for [UI] and Chrome: no Lighthouse or accessibility audit, no HTTPS profile run; newest-first order, the Draft banner on the detail page, and the unauthenticated redirect for each of the three listed routes individually have no explicit Chrome evidence (same mechanism, low risk).
- W3. InitiativeListTests.Detail_carries_every_field does not assert Description or exact CreatedAt, so dropping Description from the detail projection (double or real) would not fail it. The Detail contents scenario is weakly covered.
- W4. apply-progress.md stops at batch 4 (192 tests). The review-fix work (commit 89f8932, 212 tests, InitiativePersistenceTests, repository SetValues, DB command timeout, error-boundary recovery, theme tokens) is not recorded, and design.md was not updated for D4 and D7.

### SUGGESTION
- S1. Add MySQL-backed (Testcontainers) repository tests later for owner isolation, soft delete and search, as AGENTS.md already plans.
- S2. Add a handler-level test for a mode change in ReadyToBuild (today only at domain level) for symmetry with Planning.
- S3. Append a Batch 5 section to apply-progress.md and note D4 and D7 in design.md before archiving.
- S4. Run the sdd-verify validator (gentle-ai sdd-verify-validate) from the orchestrator, since it was not run here.

## Verdict: PASS WITH WARNINGS

0 CRITICAL, 4 WARNING, 4 SUGGESTION. Build is clean and all 212 tests pass; all 54 scenarios and RF-26..RF-34 are implemented and covered (45 by passing unit tests, 9 by the Chrome walkthrough). Nothing blocks archive. Before archiving, ideally update apply-progress.md and design.md (W4) and run the native validator (S4); W1-W3 can be accepted as known limits.
