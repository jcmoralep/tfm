# Tasks: Initiatives module

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines (hand-written, generated migration excluded) | ~1,200 (a ~620, b ~160, c ~210, d ~250) |
| 400-line budget risk | High (budget knowingly exceeded) |
| Chained PRs recommended | No (user decision) |
| Suggested split | Single PR, four reviewable commits (a to d) |
| Delivery strategy | single-pr |
| Chain strategy | size-exception |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High

The budget is exceeded. Delivery is single-pr with `size:exception`, accepted by the user, so no further decision is needed before apply. Generated migration and snapshot (~150-200 lines) are excluded.

### Suggested Work Units (commits in one PR)

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| a | Domain + application + unit tests | Commit 1 | `dotnet test tests/BmadPlatform.Application.Tests --configuration Release` | N/A (no UI/DB) | Revert commit; nothing depends on it yet |
| b | Infrastructure + migration + model tests | Commit 2 | `dotnet test BmadPlatform.slnx --configuration Release` | `dotnet ef migrations list` shows `AddInitiatives` | Revert commit; `dotnet ef database update InitialIdentity` |
| c | Wizard UI + Web helpers | Commit 3 | `dotnet test BmadPlatform.slnx --configuration Release` | Chrome: `/iniciativas/nueva` | Revert commit |
| d | List/detail/edit/delete UI | Commit 4 | `dotnet test BmadPlatform.slnx --configuration Release` | Chrome: manual checklist below | Revert commit |

Every commit MUST build with 0 warnings (warnings are errors) and pass `dotnet test BmadPlatform.slnx --configuration Release`, including the existing architecture tests. Conventional commits, no AI attribution.

## Rules for the apply agent

- Follow design.md; do not invent behavior that contradicts spec.md (all 7 assumptions are confirmed). If spec and design conflict, stop and report.
- xUnit only; hand-written doubles (no FluentAssertions, NSubstitute, bUnit). Spanish validation messages verbatim from the spec.
- Verify against the real packages before relying on them: EF Core 9 allows a single global query filter per entity; Pomelo `DateTimeOffset` storage and its round-trip; collation `utf8mb4_0900_ai_ci` is valid on MySQL 8.4 and `UseCollation` emits it in the migration; `EF.Functions.Like` with escape char translates in Pomelo.

## Commit (a): Domain + Application + tests

- [x] 1.1 Create `src/BmadPlatform.Domain/Common/DomainException.cs` and enums `Initiatives/{InitiativeStatus,DepthMode,InitiativeDepth,CreationStep}.cs`. Covers: Status never manually editable.
- [x] 1.2 Create `Domain/Initiatives/Initiative.cs`: `CreateDraft`, `Rename`, `SetDepth`, `MoveToStep`, `Complete`, `Delete`, `NameMaxLength=120`, `DescriptionMaxLength=1000`; name trimmed. Covers: Initiative fields; Depth mode (all 4); Mode and depth editability; Soft delete (retained). Dep: 1.1.
- [x] 1.3 Create `tests/BmadPlatform.Application.Tests/Domain/InitiativeTests.cs`: Draft defaults, Manual/Automatic, switch to Automatic clears depth, Complete guards, SetDepth in Planning/ReadyToBuild rejected, rename outside Draft, UpdatedAt, idempotent delete. Dep: 1.2.
- [x] 1.4 Create `Application/Common/Exceptions/NotFoundException.cs` and `Application/Abstractions/Authentication/CurrentUserExtensions.cs` (`GetRequiredIdAsync`). Covers: Handler without current user.
- [x] 1.5 Create `Application/Features/Initiatives/{IInitiativeRepository,InitiativeDtos}.cs` (design contracts, `ownerId` on every method). Add the "Pendiente de sugerencia" depth text to the detail model. Covers: Detail contents; Automatic mode leaves depth empty. Dep: 1.1.
- [x] 1.6 Create command, handler and validator per use case under `Application/Features/Initiatives/<UseCase>/`: SaveInitiativeDetails, SaveInitiativeDepth, CompleteInitiative, UpdateInitiative (no status member), DeleteInitiative, GetInitiative, ListInitiatives. `ToString` overrides hide name/description. Use `TimeProvider`. Covers: Owner-only access; Soft delete; Wizard; Listing validators (search <=120). Dep: 1.4, 1.5.
- [x] 1.7 Modify `Application/DependencyInjection.cs`: `TryAddSingleton(TimeProvider.System)`.
- [x] 1.8 Modify `Application/Behaviors/LoggingBehavior.cs`: `ValidationException`, `NotFoundException`, `DomainException` log at Warning; others at Error. Add a test in the existing LoggingBehavior tests asserting Warning for NotFound and Domain exceptions. Dep: 1.1, 1.4.
- [x] 1.9 Create `TestDoubles/{InMemoryInitiativeRepository,FakeCurrentUser,FixedTimeProvider}.cs`. In-memory repo mimics owner and `DeletedAt` filtering and retains deleted records (inspectable). Dep: 1.5.
- [x] 1.10 Create `Features/Initiatives/*Tests.cs`: validators (exact messages, 120/121, trimmed 120, description 1000/1001), duplicate names, resume at saved step, finish non-draft, owner isolation (detail/update/step/finish/delete), deleted not found, delete twice, record retained, list order/search/whitespace/filters/combined/no pagination (60), update moves to top, update contract has no status (reflection), status labels. Depth deliverables test lives in 3.2. Dep: 1.6, 1.9.
- [x] 1.11 Verify: build 0 warnings, `dotnet test BmadPlatform.slnx --configuration Release` green, architecture tests pass. Commit.

## Commit (b): Infrastructure + migration

- [x] 2.1 Create `Infrastructure/Initiatives/InitiativeConfiguration.cs`: table `ini_initiatives`, enums as `varchar(20)`, `CreatedByUserId varchar(255)` no FK, `Name` collation `utf8mb4_0900_ai_ci`, index `(CreatedByUserId, UpdatedAt)`, `HasQueryFilter(DeletedAt == null)`, lengths from domain constants. Dep: 1.2.
- [x] 2.2 Create `Infrastructure/Initiatives/LikePattern.cs` (escape `\`, `%`, `_`) and `LikePatternTests.cs`. Covers: Search by name.
- [x] 2.3 Create `Infrastructure/Initiatives/InitiativeRepository.cs` over `IDbContextFactory`: one context per call, owner predicate, `AsNoTracking` projections, `OrderByDescending(UpdatedAt)`, trimmed search, `Update` + `SaveChanges`. Dep: 1.5, 2.1, 2.2.
- [x] 2.4 Modify `Persistence/ApplicationDbContext.cs` (`base.OnModelCreating` then `ApplyConfigurationsFromAssembly`) and `Infrastructure/DependencyInjection.cs` (`AddScoped<IInitiativeRepository, InitiativeRepository>`).
- [x] 2.5 Generate migration `AddInitiatives` with `dotnet ef migrations add`; inspect it adds only `ini_initiatives` and the collation; no changes to Identity tables.
- [x] 2.6 Update `ApplicationDbContextModelTests`: rename the table-list test (Identity tables plus `ini_initiatives`, no role tables); add tests for query filter, collation, index, string enums, and `AddInitiatives` migration present; every non-Identity table carries an `xxx_` prefix. Dep: 2.4, 2.5.
- [x] 2.7 Verify: build 0 warnings, full test run, architecture rules pass. Commit.

## Commit (c): Wizard UI

- [x] 3.1 Create `Web/Components/Initiatives/UserMessages.cs` (`TryGet`: validation, not-found, domain to Spanish) and `InitiativeLabels.cs` (status, mode, depth names; RF-27 deliverables). Covers: Status labels; Level descriptions.
- [x] 3.2 Create `Web.Tests/Initiatives/{UserMessagesTests,InitiativeLabelsTests}.cs`. Dep: 3.1.
- [x] 3.3 Create `Components/Initiatives/{DepthSelector,ErrorAlert}.razor` and modify `_Imports.razor`. Covers: Selector shows deliverables [UI].
- [x] 3.4 Create `Pages/Initiatives/InitiativeWizard.razor` at `/iniciativas/nueva` and `/iniciativas/{Id:guid}/crear`, `[Authorize]`, only `IMediator` and `NavigationManager`; Siguiente / Guardar borrador / Atrás (no persist) / Finalizar; redirect to detail when not Draft; not-found state. Dep: 3.1, 3.3.
- [x] 3.5 Verify: build, tests, then Chrome: create, leave, resume. Commit.

## Commit (d): List/detail/edit/delete UI

- [ ] 4.1 Create `Components/Initiatives/StatusBadge.razor`.
- [ ] 4.2 Create `Pages/Initiatives/InitiativeList.razor` at `/iniciativas`: search, status and depth selects, "Pendiente de sugerencia", Spanish empty state, drafts link to `/crear`.
- [ ] 4.3 Create `InitiativeDetail.razor` (placeholders Conversación, Artefactos, Contexto; inline delete confirmation; shared not-found state) and `InitiativeEdit.razor` (mode/depth disabled outside Draft/Clarifying).
- [ ] 4.4 Modify `Layout/MainLayout.razor` (pill nav "Iniciativas") and `Pages/Home.razor` (copy and link).
- [ ] 4.5 Verify: build 0 warnings, `dotnet test BmadPlatform.slnx --configuration Release`, then the manual checklist. Commit.

## Final manual verification (Chrome, [UI] scenarios)

- [ ] Unauthenticated visit to `/iniciativas`, `/iniciativas/nueva`, `/iniciativas/{id}` redirects to login.
- [ ] Nav link to `/iniciativas` shows on every page.
- [ ] List: empty state, search, filters, newest-first order, "Pendiente de sugerencia".
- [ ] Wizard: save draft with name only, leave, reopen from list at the saved step with prefilled values; selector shows deliverables; Finalizar leads to Clarifying and the detail page.
- [ ] Owner-only: user B opens A's `/iniciativas/{id}` and sees the same not-found message as for a random id.
- [ ] Soft delete: confirm returns to the list without it; its URL shows not-found; row still in `ini_initiatives` with `DeletedAt` set.
- [ ] Locked fields: in Planning, mode and depth are read-only (set status in the DB for testing); name and description still editable.
- [ ] Detail shows the three placeholder sections.
