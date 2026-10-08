# Design: Initiatives module

## Technical Approach

First data module (PROPUESTA §7 step 2). Rich `Initiative` aggregate in Domain; one MediatR request + handler + Spanish validator per use case in `Application/Features/Initiatives`; a module-local repository port implemented in Infrastructure over `IDbContextFactory`; Blazor pages that only send requests through `IMediator`. Traces RF-05, RF-06, RF-08, RF-09, RF-26..RF-34.

## Architecture Decisions

| # | Topic | Options and tradeoff | Decision |
|---|---|---|---|
| 1 | Wizard step | Derived from filled fields: no column, but cannot tell "went back to step 1" from "never left it" / Column: one extra field | **Column** `CreationStep` (`Details`, `Depth`, `Review`, string). Meaning: the step to reopen. Relevant only while `Draft` |
| 2 | Draft depth fields | Non-null `DepthMode` with a default: lies about a choice the user never made / nullable | **`DepthMode?` and `Depth?`**. `Complete` requires a mode, and a depth when Manual |
| 3 | Soft delete | Global query filter: safe by default, one filter per entity in EF 9 / explicit predicate: easy to forget | **Global filter** `DeletedAt == null`. Single nullable `DeletedAt` column (no separate bool) |
| 4 | Ownership | In a query filter: needs the user inside `DbContext` / in the handler after loading: leaks existence / required repository parameter | **Required `ownerId` parameter on every port method**, predicate inside the repository. Another user's id therefore returns `null`, the same as missing or deleted (RF-26) |
| 5 | Port shape | Specification/generic repository: more abstraction / `IApplicationDbContext`: needs EF in Application (blocked by the architecture test) / per-aggregate methods | **Per-aggregate port** with no-tracking read projections plus load/add/update for commands |
| 6 | Failures | `Result<T>` everywhere: new abstraction, and validation already uses exceptions / typed exceptions | **Exceptions, consistent with `ValidationBehavior`**: `DomainException` (Domain, Spanish message) and `NotFoundException` (Application). Queries return `null` for "not found" |
| 7 | Search | `LOWER()`: defeats indexes / relying on server default collation: implicit | **`EF.Functions.Like` with escaping** and an explicit `utf8mb4_0900_ai_ci` collation on `Name` (case- and accent-insensitive) |
| 8 | Time | `DateTime.UtcNow` in the entity: hard to test | **`TimeProvider`** (BCL) in handlers; domain methods take `DateTimeOffset now` |

Rejected for the concurrency token: YAGNI. Each initiative has one owner, so last write wins.

## Domain model (`Domain/Initiatives`)

- `Initiative.CreateDraft(ownerId, name, description, now)` sets `Status=Draft`, `CreationStep=Details`, and `CreatedAt=UpdatedAt=now`.
- `Rename(name, description, now)` works in any status.
- `SetDepth(mode, depth, now)` is guarded by status `Draft|Clarifying`; otherwise it throws `DomainException("El modo y la profundidad solo se pueden cambiar en Borrador o Aclarando.")`. `Automatic` forces `Depth=null`. Outside `Draft`, `Manual` requires a depth.
- `MoveToStep(step, now)` is allowed only in `Draft`.
- `Complete(now)` requires `Draft`, a mode, and a depth when the mode is Manual, and sets `Clarifying`.
- `Delete(now)` sets `DeletedAt`. Deleting twice is idempotent.
- Every mutator sets `UpdatedAt=now`. Guards: name not blank and at most `NameMaxLength=120`; description at most `DescriptionMaxLength=1000`. These constants are shared by the validators and the EF configuration (DRY).
- `Status` has no setter path from the UI (RF-34). Enums: `InitiativeStatus` (Draft, Clarifying, Planning, ReadyToBuild), `DepthMode`, `InitiativeDepth` (Small, Standard, Large), `CreationStep`.

## Wizard (RF-30)

| Step | Collects | Primary / secondary action |
|---|---|---|
| 1 Details | Name, Description | "Siguiente" (saves, step becomes Depth) / "Guardar borrador" (saves, step stays Details) |
| 2 Depth | DepthMode; Depth when Manual (RF-27 descriptions; Automatic shows "Pendiente de sugerencia") | Same pattern. Step becomes Review or stays Depth |
| 3 Review | Read-only summary | "Finalizar" leads to `Clarifying` and the detail page |

"Atrás" does not persist anything. To resume, open `/iniciativas/{id}/crear`, which loads the details, shows `CreationStep` with the saved values, and redirects to the detail page if the initiative is no longer `Draft`.

## Application contracts (`Application/Features/Initiatives`)

```csharp
public interface IInitiativeRepository   // every method requires ownerId
{
    Task<IReadOnlyList<InitiativeSummary>> ListAsync(string ownerId, InitiativeListFilter filter, CancellationToken ct);
    Task<InitiativeDetails?> GetDetailsAsync(Guid id, string ownerId, CancellationToken ct);
    Task<Initiative?> GetAsync(Guid id, string ownerId, CancellationToken ct);
    Task AddAsync(Initiative initiative, CancellationToken ct);
    Task UpdateAsync(Initiative initiative, CancellationToken ct);
}
public sealed record InitiativeListFilter(string? Search, InitiativeStatus? Status, InitiativeDepth? Depth);
public sealed record InitiativeSummary(Guid Id, string Name, InitiativeStatus Status, DepthMode? DepthMode, InitiativeDepth? Depth, DateTimeOffset UpdatedAt);
public sealed record InitiativeDetails(Guid Id, string Name, string? Description, InitiativeStatus Status, DepthMode? DepthMode, InitiativeDepth? Depth, CreationStep CreationStep, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
```

Use cases:

- `SaveInitiativeDetailsCommand(Guid? Id, Name, Description, bool Advance) -> Guid`: creates the draft when `Id` is null.
- `SaveInitiativeDepthCommand(Id, DepthMode?, Depth?, bool Advance)`
- `CompleteInitiativeCommand(Id)`
- `UpdateInitiativeCommand(Id, Name, Description, DepthMode, Depth?)`: the edit page. Calls `SetDepth` only when mode or depth changed.
- `DeleteInitiativeCommand(Id)`
- `GetInitiativeQuery(Id) -> InitiativeDetails?`
- `ListInitiativesQuery(Search, Status, Depth)`

Validators check shape only: required name and lengths, `IsInEnum`, Automatic with a depth is rejected, and search is at most 120 characters.

The owner comes from `ICurrentUser` through `CurrentUserExtensions.GetRequiredIdAsync`, which throws `InvalidOperationException` (a defect, because pages are `[Authorize]`). Commands throw `NotFoundException` when `GetAsync` returns `null`. `ToString` overrides hide names and descriptions (logging rule). `AddApplication` registers `TryAddSingleton(TimeProvider.System)`.

`LoggingBehavior` change: `ValidationException`, `NotFoundException` and `DomainException` are logged at Warning; anything else at Error.

## Infrastructure

- `Initiatives/InitiativeConfiguration`: table `ini_initiatives`.
  - Enums stored as `varchar(20)` strings.
  - `CreatedByUserId varchar(255)` with no FK to `AspNetUsers`.
  - `Name` uses `UseCollation("utf8mb4_0900_ai_ci")`.
  - Index `(CreatedByUserId, UpdatedAt)`.
  - `HasQueryFilter(i => i.DeletedAt == null)`.
- `InitiativeRepository`: creates one context per call. Reads use `AsNoTracking` plus a projection and `OrderByDescending(UpdatedAt)`. `UpdateAsync` uses `context.Update` followed by `SaveChanges`.
- `LikePattern.Contains(term)` escapes `\`, `%` and `_`, used with `EF.Functions.Like(name, pattern, "\\")`. Search is trimmed, and empty means no filter.
- `ApplicationDbContext.OnModelCreating`: calls `base`, then `ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly)`.
- Migration `AddInitiatives` is generated with `dotnet ef migrations add`.

## Web

Pages:

- `/iniciativas`: list, search, two selects, status badge, "Pendiente de sugerencia".
- `/iniciativas/nueva` and `/iniciativas/{Id:guid}/crear`: one `InitiativeWizard` component.
- `/iniciativas/{Id:guid}`: detail with the Conversación, Artefactos and Contexto placeholders (RF-09), plus delete with an inline confirmation.
- `/iniciativas/{Id:guid}/editar`: edit. Mode and depth are disabled outside Draft and Clarifying; the domain still enforces the rule.

Every page is `[Authorize]` and injects only `IMediator` and `NavigationManager`. A `null` query result renders the shared "La iniciativa no existe" state.

Thin pages without bUnit:

- `UserMessages.TryGet(Exception, out IReadOnlyList<string>)` maps validation, not-found and domain exceptions to Spanish text. Pages use `catch (Exception e) when (UserMessages.TryGet(e, out var m))`.
- `InitiativeLabels` provides the Spanish names for status, mode and depth, plus the RF-27 descriptions.
- Both are plain C# in `Web/Components/Initiatives`, unit-tested in `Web.Tests`.

## Data Flow

```
Create:  Wizard -> SaveInitiativeDetails(null) -> Handler -> CreateDraft(now) -> repo.Add -> Guid
         -> navigate /iniciativas/{id}/crear -> SaveInitiativeDepth -> Complete -> Clarifying
Resume:  /crear -> GetInitiativeQuery(owner) -> null ? not-found : Draft ? show CreationStep : detail
List:    filters -> ListInitiativesQuery -> repo (filter: DeletedAt null) + owner + LIKE -> summaries
Delete:  confirm -> DeleteInitiativeCommand -> repo.Get(owner) ?? NotFound -> Delete(now) -> Update -> list
```

## File Changes and commit slicing

| Commit | Files |
|---|---|
| a) domain + application + tests | Create: `Domain/Common/DomainException.cs`, `Domain/Initiatives/{Initiative,InitiativeStatus,DepthMode,InitiativeDepth,CreationStep}.cs`; `Application/Common/Exceptions/NotFoundException.cs`; `Application/Abstractions/Authentication/CurrentUserExtensions.cs`; `Application/Features/Initiatives/{IInitiativeRepository,InitiativeDtos}.cs` and one folder per use case (command/query, handler, validator). Modify: `Application/DependencyInjection.cs` (TimeProvider), `LoggingBehavior.cs`. Tests: `Application.Tests/Domain/InitiativeTests.cs` (the Application.Tests project references Domain transitively), `Features/Initiatives/*Tests.cs`, `TestDoubles/{InMemoryInitiativeRepository,FakeCurrentUser,FixedTimeProvider}.cs`, plus a LoggingBehavior warning-level test |
| b) infrastructure + migration | Create: `Infrastructure/Initiatives/{InitiativeConfiguration,InitiativeRepository,LikePattern}.cs`, `Persistence/Migrations/*_AddInitiatives*.cs`. Modify: `ApplicationDbContext.cs`, `Infrastructure/DependencyInjection.cs` (`AddScoped<IInitiativeRepository, InitiativeRepository>`), snapshot. Tests: `ApplicationDbContextModelTests` (expected tables include `ini_initiatives`; rename the test; a filter test; an `AddInitiatives` migration test; non-Identity tables must carry a `xxx_` prefix), `Initiatives/LikePatternTests.cs` |
| c) wizard UI | Create: `Web/Components/Initiatives/{UserMessages,InitiativeLabels}.cs`, `{DepthSelector,ErrorAlert}.razor`, `Pages/Initiatives/InitiativeWizard.razor`. Modify: `_Imports.razor`. Tests: `Web.Tests/Initiatives/{UserMessagesTests,InitiativeLabelsTests}.cs` |
| d) list/detail/edit/delete | Create: `Pages/Initiatives/{InitiativeList,InitiativeDetail,InitiativeEdit}.razor`, `Components/Initiatives/StatusBadge.razor`. Modify: `MainLayout.razor` (pill nav "Iniciativas"), `Home.razor` (copy and link) |

## Testing Strategy

| Layer | What | Approach |
|---|---|---|
| Domain | Invariants, status guards, `Complete`, `UpdatedAt`, delete | xUnit, fixed `DateTimeOffset` |
| Application | Validators (Spanish messages), handlers (owner isolation, not-found, advance/step), `ToString` redaction | Hand-written fakes (no NSubstitute) |
| Infrastructure | Model/table/filter/index/collation, migration present, LIKE escaping | Model tests without a DB connection |
| Web | Error mapping, labels | Pure C# tests |

Integration tests (MySQL and bUnit) are out of scope, so the owner and soft-delete predicates in SQL are covered by the model tests plus review only.

## Module boundaries (AGENTS §4.1)

- Module types stay in their `Initiatives` folders.
- The `ini_` prefix is used for every table.
- No navigation or FK to Identity; the owner is a plain string.
- Other modules (steps 3+) must reach initiatives through MediatR queries, never `ini_initiatives` or `IInitiativeRepository`.
- The cross-module architecture test is added when the second data module appears.

## Threat Matrix

N/A: there is no routing, shell, subprocess, VCS/PR automation, executable-file classification or process-integration boundary.

## Migration / Rollout

Additive migration `AddInitiatives`, with no changes to existing tables. Rollback: `dotnet ef database update InitialIdentity`.

## Open Questions

- [ ] None blocking. Concurrency token deferred (YAGNI).
