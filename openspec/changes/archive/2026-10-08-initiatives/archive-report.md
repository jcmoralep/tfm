# Archive Report: initiatives

**Change**: initiatives (PROPUESTA-MVP §7 step 2)  
**Archived**: 2026-10-08  
**Branch**: feat/initiatives (PR #2)  
**Mode**: hybrid (openspec + engram)  
**Status**: PASS WITH WARNINGS (0 CRITICAL)

## Artifacts

All artifacts persisted to `openspec/changes/initiatives/` and merged into the source of truth:

| Artifact | Path | Status |
|----------|------|--------|
| Proposal | `openspec/changes/initiatives/proposal.md` | Archived |
| Exploration | `openspec/changes/initiatives/exploration.md` | Archived |
| Specification | `openspec/changes/initiatives/specs/initiatives/spec.md` | Merged to `openspec/specs/initiatives/spec.md` |
| Design | `openspec/changes/initiatives/design.md` | Archived |
| Tasks | `openspec/changes/initiatives/tasks.md` | Archived (all tasks checked) |
| Apply Progress | `openspec/changes/initiatives/apply-progress.md` | Archived |
| Verify Report | `openspec/changes/initiatives/verify-report.md` | Archived (PASS WITH WARNINGS) |
| Archive Report | `openspec/changes/initiatives/archive-report.md` | Written |

## Final State (Authority: Launch Prompt)

### Implementation

The feature is complete and merged into feat/initiatives (PR #2).

**Commits**: 8 implementation commits plus docs
- `5825a26`: Domain + Application + unit tests (Batch 1)
- `335eacf`: Infrastructure + migration + model tests (Batch 2)
- `a026d7e`: Wizard UI + Web helpers (Batch 3)
- `aabb1ab`: List, detail, edit, delete UI (Batch 4)
- `03a658d`: Responsive list layout
- `791e202`: Traffic-light status colors
- `4209f18`: Status column reordering
- `89f8932`: Code review fixes

**Build**: Release configuration, 0 warnings, 0 errors (verified 2026-10-08)

**Tests**: 212 passed, 0 failed
- Application.Tests: 103
- Architecture.Tests: 12
- Web.Tests: 65
- Infrastructure.Tests: 32

### Review

**Native review**: Not run (orchestrator forbade `gentle-ai` calls from verify phase; native integration required native binding which was unavailable).

**Code review** (post-verification): 4-lens review (risk, readability, reliability, resilience)
- Security findings: none
- Test verification: 1 blind spot found and fixed (in-memory repository stored live instances; double now stores detached copies; mutation check added per handler)
- Quality findings: double-submit guard added, UpdateAsync loads tracked row through filter (prevents resurrection of deleted rows), AppErrorBoundary recovery improved, MySqlDatabase command timeout set to 30 s
- No structural defects

### Verification

**Verdict** (per `verify-report`, observation recorded at verification time):
- 54 scenarios: 54 COMPLIANT, 0 PARTIAL, 0 MISSING
- 12 requirements (RF-05..RF-34): 100% implemented
- Spec-to-code matrix: all paths covered by unit tests (45 [UNIT]) or Chrome evidence (9 [UI])
- 0 CRITICAL issues
- 4 WARNING issues (known limits):
  - **W1**: Repository SQL (owner predicate, filters, soft delete, collation) covered by model tests and manual MySQL + Chrome check, not by automated integration tests (Testcontainers planned for future)
  - **W2**: No Lighthouse audit, accessibility check, or HTTPS profile; [UI] gaps (newest-first order, draft banner, per-route unauthenticated redirect) covered by same mechanism
  - **W3**: Detail assertion weak (does not check Description or exact CreatedAt); risk low
  - **W4** (resolved post-verification): apply-progress.md stale (stopped at Batch 4, 192 tests); design.md did not record UpdateAsync refactor or theme tokens. **Fix applied**: Batch 5 section added to apply-progress.md (commit 89f8932, 212 tests); design.md revision note added
- 4 SUGGESTION items (all recorded for future reference)

**PR #2 CI status**: Passed

### Known Limitations (By Design)

Deliberately deferred to future work (not blockers):
- Refactoring duplicated page plumbing (wizard/list/detail share error handling and shared states)
- Hard cap on initiatives list
- Cancellation tokens tied to circuit lifetime
- Route constants (routes scattered in component code)

### Depth Label Scenario — Correction Applied

**Original stale claim** (exploration.md): "Depth values are not enumerated anywhere."

**Corrected state** (per proposal and spec): Three depth levels are defined (Small, Standard, Large) with deliverables (RF-27). Selector displays each level and its output. Automatic mode shows "Pendiente de sugerencia".

**Evidence**: InitiativeLabels.cs (lines 58–64), DepthSelector.razor (lines 29–47), 54 scenarios all COMPLIANT including level descriptions and selector display.

**Documentation**: spec.md requirement R2 (RF-27) is authoritative.

---

## Lessons Learned

1. **Persistence double must store detached copies**: The in-memory `IInitiativeRepository` test double must return copies of persisted entities, not live references, so that removing a persistence call in a handler leaves only one test failing (the mutation check), not all dependent tests passing silently.

2. **Review-lens agents require native review binding**: Multi-lens review agents (risk, readability, reliability, resilience) refuse to operate without the `GENTLE_AI_REVIEW_BINDING` prefix in the task prompt and the exact frozen candidate tree context (opaque handle, not inline bytes). A review attempt without this machinery produces no output.

3. **Attempt ledger settlement needs reset per phase**: The Engram attempt ledger (scheduling/execution records for multi-turn work) must be explicitly reset after each SDD phase completes, else subsequent phases inherit stale attempt records and conflict resolution becomes opaque.

4. **Soft-delete SQL translation via model tests**: Repository predicates for soft delete and ownership are untested by unit tests. Model-level and EF query-translation tests (SQL shape and filter application) plus manual integration checks (MySQL and UI) are the coverage strategy when full integration tests are out of scope.

5. **Wizard step as stored column beats derived state**: A stored `CreationStep` column is simpler than deriving step from filled fields. It enables "went back to step 1" vs "never left" disambiguation and deterministic resumption, at the cost of one extra column that must be migrated if wizard structure changes.

---

## Source of Truth

The merged main spec is now the authoritative definition of the initiatives capability:
- **Location**: `openspec/specs/initiatives/spec.md`
- **Completeness**: 12 requirements, 54 scenarios, all implemented
- **Status**: Ready for step 3 (assistant and context)
- **Backward compatibility**: Additive migration; no changes to Identity or other tables

The change folder (`openspec/changes/initiatives/`) is preserved for audit trail and future reference. Per user instruction, the orchestrator will archive it with git history intact.

---

## Next Steps

Step 3 (assistant and context) may begin. The initiatives capability is stable and complete for its scope.

All warnings are documented and known. No blockers to deployment.
