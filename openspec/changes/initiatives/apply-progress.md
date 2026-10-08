# Apply progress: initiatives

## Batch 1 (commit a: Domain + Application + unit tests) - tasks 1.1 to 1.11 DONE

Mode: Standard (strict TDD off). Tests: 55 before, 127 after (Application 14 -> 86; Architecture 12, Web 13, Infrastructure 16 unchanged). Build: 0 warnings, 0 errors.

### Created
- Domain: `Common/DomainException.cs`; `Initiatives/{Initiative,InitiativeStatus,DepthMode,InitiativeDepth,CreationStep}.cs`.
- Application: `Common/Exceptions/NotFoundException.cs`; `Abstractions/Authentication/CurrentUserExtensions.cs`; `Features/Initiatives/{IInitiativeRepository,InitiativeDtos,InitiativeValidationRules}.cs`; one folder per use case (SaveInitiativeDetails, SaveInitiativeDepth, CompleteInitiative, UpdateInitiative, DeleteInitiative, GetInitiative, ListInitiatives) with command/query, handler and validator where applicable.
- Tests: `Domain/InitiativeTests.cs`; `Features/Initiatives/{InitiativeTestContext,InitiativeValidatorTests,InitiativeWizardHandlerTests,InitiativeAccessTests,InitiativeUpdateTests,InitiativeListTests}.cs`; `TestDoubles/{InMemoryInitiativeRepository,FakeCurrentUser,FixedTimeProvider}.cs`.

### Modified
- `Application/DependencyInjection.cs` (`TryAddSingleton(TimeProvider.System)`).
- `Application/Common/Behaviors/LoggingBehavior.cs` (Validation, NotFound and Domain exceptions log at Warning) plus a theory in `LoggingBehaviorTests`.
- `openspec/changes/initiatives/tasks.md` (1.1-1.11 marked).

### Decisions and deviations (none contradict spec or design)
- Path: the existing behaviors live in `Application/Common/Behaviors` (tasks.md says `Application/Behaviors`); the existing location was kept.
- `Initiative.SetDepth(DepthMode? mode, InitiativeDepth? depth, now)`: nullable mode so a draft can persist an unfinished depth step. Depth without mode is rejected. Outside Draft (Clarifying) mode is required and Manual requires a level. Automatic always clears depth.
- `Complete` also sets `CreationStep = Review`.
- Wizard step semantics (design says "the step to reopen"): SaveInitiativeDetails sets step to Depth when `Advance`, else Details; SaveInitiativeDepth sets Review when `Advance`, else Depth. Both go through `MoveToStep`, so saving wizard steps of a non-Draft initiative throws `DomainException`.
- `Description` that is blank is stored as `null`. Name is trimmed in the domain; the validator measures the trimmed length.
- Detail model: `InitiativeDetails.DepthPendingText` returns "Pendiente de sugerencia" when mode is Automatic and depth is empty (null otherwise). The constant and the not-found text live in `InitiativeTexts` (`Features/Initiatives/InitiativeDtos.cs`); `NotFoundException` carries "La iniciativa no existe.".
- `ListInitiativesQuery` validator also checks `IsInEnum` for status and depth (messages "El estado no es válido." / "El nivel de profundidad no es válido.").
- `Initiative` has a private parameterless constructor and private setters for EF (batch 2 can map it without changes).

### Notes for the next batch
- Environment: the machine-level NuGet config includes an unreachable private feed (`redarbor...`), so plain `dotnet build/test` fails restore with NU1900/NU1507 (warnings as errors). Restore was done once with a temporary nuget.config containing only nuget.org (`dotnet restore BmadPlatform.slnx --configfile <temp>`); afterwards `dotnet build ... --no-restore` and `dotnet test ... --no-build` work. The repo was not modified for this. Adding new packages in later batches needs the same `--configfile` restore.
- `AGENTS.md` shows as modified in the working tree; it was not changed by this batch and was not committed.
- Repository contract expectations for Infrastructure (batch b): owner predicate and `DeletedAt == null` in every method, `GetAsync` must not return deleted or foreign records, trimmed search with empty meaning no filter, order by `UpdatedAt` descending, `Depth` filter compares exactly.
- Tests reach Planning/ReadyToBuild status by reflection on the private `Status` setter (those transitions are out of scope).
- Depth deliverables test (RF-27) is still pending in task 3.2 as planned.

## Batch 2 (commit b: Infrastructure + migration + model tests) - tasks 2.1 to 2.7 DONE

Mode: Standard (strict TDD off). Tests: 127 before, 142 after (Infrastructure 16 -> 31; others unchanged). Build: 0 warnings, 0 errors (Release).

### Created
- `Infrastructure/Initiatives/{InitiativeConfiguration,InitiativeRepository,LikePattern}.cs`.
- Migration `Persistence/Migrations/20261008233559_AddInitiatives` (+ Designer), generated with `dotnet ef migrations add`; only creates `ini_initiatives` and the `(CreatedByUserId, UpdatedAt)` index. Snapshot regenerated.
- Tests: `Infrastructure.Tests/Initiatives/LikePatternTests.cs`.

### Modified
- `Persistence/ApplicationDbContext.cs` (`OnModelCreating`: base + `ApplyConfigurationsFromAssembly`), `Infrastructure/DependencyInjection.cs` (`AddScoped<IInitiativeRepository, InitiativeRepository>`).
- `ApplicationDbContextModelTests`: table test renamed (`Model_contains_identity_user_tables_the_initiatives_table_and_no_role_tables`); new tests for module prefix rule, query filter (evaluated on active and deleted instances), string enums, collation and lengths, owner (no FK) and index, `datetime(6)` mapping, LIKE/ESCAPE SQL translation with `DeletedAt IS NULL`, and `AddInitiatives` migration present.

### Verifications against real packages
1. Single global filter per entity (EF Core 9.0.20): one `HasQueryFilter(DeletedAt == null)`; test evaluates it and checks the generated SQL.
2. Pomelo 9.0.0 `DateTimeOffset`: maps to `datetime(6)` (`MySqlDateTimeOffsetTypeMapping`); SQL literal and the MySqlConnector parameter text both convert to UTC (offset +02:00 written as 08:00). The read path was not exercised (no DB).
3. Collation `utf8mb4_0900_ai_ci` on `Name`: emitted in the migration; asserted from the design-time model.
4. `EF.Functions.Like` with `"\\"` escape translates (`LIKE ... ESCAPE`) in Pomelo; `LikePattern` escapes `\`, `%`, `_` (unit tested).
5. Migration generated by the tool, contains only `ini_initiatives`.

### Coverage limits
Repository SQL behavior (owner predicate, ordering, filters, soft delete on real rows, DateTimeOffset round trip, collation matching) is covered only by model-level and query-translation tests plus review; there is no MySQL integration test.

### Notes
- `UpdateAsync` uses `Update` on a detached entity read with `AsNoTracking` (all columns written, last write wins).
