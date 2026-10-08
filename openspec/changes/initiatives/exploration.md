# Exploration: initiatives (PROPUESTA-MVP §7 step 2)

## Source requirements
- PROPUESTA §3.1: "Crea una **iniciativa** (nombre y descripción corta) y **elige cómo se define la profundidad**: la define el propio usuario, o la sugiere el asistente."
- PROPUESTA §6: "**Iniciativa:** id, nombre, descripción, modo de profundidad (automático o manual), profundidad, estado, creada por, fechas."
- PROPUESTA §4.1: "Crear, listar, ver y editar. Estados: *Aclarando*, *Planificando*, *Lista para construir*. Detalle con tres secciones: Conversación, Artefactos y Contexto."
- REQUISITOS RF-05, RF-06, RF-08, RF-09 are confirmed. RF-07 (automatic move to "Lista para construir" when artifacts are approved) is an assumption and belongs to step 4.
- Depth values are not enumerated anywhere. Hints in PROPUESTA §3.3: vague idea -> Brief; clear and large idea -> PRD + Architecture + Epics; small change -> short spec (assumption, RF-11).
- Not specified: length limits, uniqueness, delete/archive, default state.

## Proposed model
`Id` (Guid), `Name` (required), `Description`, `DepthMode` (Manual | Automatic), `Depth` (nullable; Small | Standard | Large candidates), `Status` (Clarifying | Planning | ReadyToBuild; initial Clarifying), `CreatedByUserId` (string, plain column, no navigation to Identity), `CreatedAt`, `UpdatedAt` (UTC).

## Layout (modular monolith, one DbContext)
- Constraint: the architecture test forbids EF Core in Application, so handlers need a persistence port (`IInitiativeRepository`) implemented in Infrastructure with `IDbContextFactory`.
- Domain `Initiatives/`: entity with factory method and `Update`, three enums.
- Application `Features/Initiatives/`: Create, List, Get, Update (command/query + handler + Spanish validator), read DTOs, port. User from `ICurrentUser`.
- Infrastructure `Initiatives/`: `IEntityTypeConfiguration<Initiative>`, table `ini_initiatives`, enums as strings, repository, `ApplyConfigurationsFromAssembly`, migration `AddInitiatives`.
- Web: `/iniciativas` (list), `/iniciativas/nueva` (create), `/iniciativas/{id:guid}` (detail + edit, with placeholder sections Conversación, Artefactos, Contexto). Nav link in `MainLayout`; update `Home.razor` copy. Components inject only `IMediator`.
- Existing test `Model_contains_identity_user_tables_and_no_role_tables` must be updated for the new table.

## Open product questions (defaults in parentheses)
1. Visibility: does everyone see all initiatives, or only their own? (all visible, creator shown, anyone can edit)
2. Depth values in manual mode. (Small = spec only, Standard = Brief + PRD, Large = PRD + Architecture + Epics)
3. Automatic mode: depth empty until the assistant suggests one in step 3; editable while status is Clarifying. (yes)
4. Delete/archive. (non-goal, no delete)
5. Field rules. (name required max 120, description optional max 1000, names not unique)
6. Manual status editing in step 2. (no; status starts Clarifying and changes in later steps)
7. List behavior. (ordered by UpdatedAt desc, no search, filter or pagination)

## Approaches
- Depth: two enums plus nullable `Depth` (recommended, matches §6) vs a single nullable depth (loses the explicit user choice).
- Persistence: repository port in Application (recommended) vs `IApplicationDbContext` (needs EF in Application, rejected).
- Detail page: build with section placeholders now so step 3 only fills Conversación.
- No pagination now (YAGNI).

## Risks and size
- First use of the persistence-port pattern and `IEntityTypeConfiguration`; it sets the template for later modules.
- Depth values leak into steps 3, 4 and 6; changing them later means a migration.
- No MySQL integration tests and no bUnit: logic must stay in handlers and validators.
- Estimate: 450-700 lines including the generated migration. Hand-written code is near the 400-line budget; the migration pushes it over. Delivery is single-PR by user choice, so the migration would need to be treated as generated, or the PR split by commit (backend, then UI).
