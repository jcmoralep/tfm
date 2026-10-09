# Proposal: Initiatives module (PROPUESTA-MVP §7 step 2)

## Intent

Product people need a place to register an idea before the assistant (step 3) can work on it. Today the app has only login. This change delivers the first business module: create (step-by-step wizard with resumable draft), list, view, edit and soft-delete initiatives, scoped to the owner. It also sets the template (persistence port, `IEntityTypeConfiguration`, table prefix) for the later modules.

## Scope

### In Scope
- `Initiative` entity: name (required, <=120, not unique), description (optional, <=1000), `DepthMode` (Manual/Automatic), nullable `Depth` (Small/Standard/Large), `Status` (Draft/Clarifying/Planning/ReadyToBuild), wizard step, owner id, soft-delete flag, UTC timestamps (RF-05, RF-06, RF-08, RF-27, RF-33).
- Wizard with draft: name is enough to save; each step persists; resume opens the saved step; finishing sets Clarifying (RF-30).
- Automatic mode keeps depth empty, shown as "Pendiente de sugerencia" (RF-28).
- Mode/depth editable only in Draft and Clarifying (RF-29); status never manually editable (RF-34).
- List: search by name, filter by status and depth, order by `UpdatedAt` desc, no pagination (RF-31).
- Detail with placeholder sections Conversación, Artefactos, Contexto (RF-09).
- Owner-only access; another user's id behaves as not found (RF-26).
- Soft delete, hidden everywhere (RF-32).
- Unit tests (xUnit) for domain rules, validators and handlers.

### Out of Scope
- Assistant suggesting depth, Planning/ReadyToBuild transitions (steps 3-4, RF-07).
- Trash/restore UI, hard delete, pagination, sharing, roles.
- Conversation, artifacts and context content.
- Integration tests (MySQL/bUnit).

## Capabilities

### New Capabilities
- `initiatives`: lifecycle, wizard draft, ownership, listing/search/filter, soft delete, depth rules.

### Modified Capabilities
- None

## Approach

- Domain `Initiatives/`: rich entity (factory, step methods, `Complete`, `ChangeDepth` guarded by status, `Delete`), enums.
- Application `Features/Initiatives/`: one command/query + handler + Spanish validator per use case; `IInitiativeRepository` port (EF banned in Application); owner from `ICurrentUser`.
- Infrastructure `Initiatives/`: configuration, table `ini_initiatives`, enums as strings, repository over `IDbContextFactory`, migration `AddInitiatives`.
- Web: `/iniciativas`, `/iniciativas/nueva` (wizard), `/iniciativas/{id:guid}`; nav link; components inject only `IMediator`.

## Decisions for sdd-design
- Wizard step modelling (int/enum column vs derived from filled fields) and exact step list.
- Soft delete: global query filter vs explicit repository predicate.
- Repository port shape (per-aggregate methods vs specification).
- Ownership check location (repository predicate vs handler) and not-found result type.
- Search: case-insensitive `LIKE` relying on MySQL collation.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/BmadPlatform.Domain/Initiatives/` | New | Entity, enums |
| `src/BmadPlatform.Application/Features/Initiatives/` | New | Use cases, validators, DTOs, port |
| `src/BmadPlatform.Infrastructure/Initiatives/`, `Migrations/` | New | Config, repository, migration |
| `ApplicationDbContext`, DI registration | Modified | `ApplyConfigurationsFromAssembly`, repository |
| `src/BmadPlatform.Web/Components/` | New/Modified | Pages, `MainLayout` nav, `Home.razor` copy |
| `tests/` | New/Modified | Unit tests; update `Model_contains_identity_user_tables_and_no_role_tables` |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Depth enum leaks into steps 3, 4, 6 | Med | Stored as strings; changes need migration |
| Wizard/draft UI logic untested (no bUnit) | Med | Rules in domain and handlers |
| Pattern sets template for all modules | Med | Settle port shape in design |
| Size exceeds budget | High | See forecast |

## Review Workload Forecast

| Part | Estimate |
|------|----------|
| Domain | ~100 |
| Application (7 use cases, port, DTOs) | ~320 |
| Infrastructure (config, repository, DI) | ~100 |
| Web (list+filters, wizard, detail/edit/delete) | ~400 |
| Tests | ~280 |
| **Hand-written total** | **~1,100-1,200** |
| Generated migration + snapshot | ~150-200 (excluded) |

- 400-line budget risk: High. Hand-written code alone is roughly 3x the budget.
- Delivery is `single-pr` by user choice. Options for the user (not chosen here):
  1. Single PR split into reviewable commits: (a) domain + application + tests, (b) infrastructure + migration, (c) wizard UI, (d) list/detail/edit/delete UI.
  2. Chained PRs with the same four slices, each building and passing tests.
  3. Single PR with explicit `size:exception`.

## Rollback Plan

Revert the PR(s); run `dotnet ef database update <previous migration>` to drop `ini_initiatives`. No existing table changes.

## Dependencies

- Step 1 foundation (Identity, `ICurrentUser`, MediatR behaviors), currently on `feat/project-foundation`; must be merged or used as the base branch.

## Success Criteria

- [ ] Draft saved with name only and resumed at the same step; finishing yields Clarifying.
- [ ] Another user's initiative returns not found; deleted ones never appear.
- [ ] Mode/depth edits rejected outside Draft/Clarifying.
- [ ] Search, filters and ordering work.
- [ ] `dotnet build` and `dotnet test` (Release) pass with zero warnings.

## Proposal question round

Completed by the orchestrator before this phase; decisions recorded in Engram `sdd/initiatives/decisions` and REQUISITOS RF-26..RF-34 are final.
