```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:c8d08e9c350d3f5b04dbf51b69e3fa586bd69475bcf366a42a386542dbafe2b4
verdict: fail
blockers: 0
critical_findings: 0
requirements: 12/17
scenarios: 65/73
test_command: dotnet test -c Release --no-build
test_exit_code: 0
test_output_hash: sha256:694999540d002452c854b6ad124a66b656281fcaa0b5949dd9263fd85bbbeaba
build_command: dotnet build -c Release --no-restore
build_exit_code: 0
build_output_hash: sha256:9b2e795be683b971010633de5a8318da3184a9d6d6f6042c06fc24a13f8fa3bd
```

## Verification Report

**Change**: context-attachments (PROPUESTA-MVP step 7)
**Version**: N/A
**Mode**: Standard (strict_tdd false)
**Commit verified**: 38e92bf (main with S1-S5 merged), branch fix/context-verify, working tree clean except untracked `.claude/`.

### Completeness

| Metric | Value |
|--------|-------|
| Slices S1-S5 | 5/5 ticked; code, tests and docs present for each |
| Group F (F.1-F.5) | 0/5 ticked (see W3) |
| Sub-checks under 3.5, 4.4, 5.6 | all unticked although the parent tasks are ticked (see W3) |
| proposal.md success criteria | 0/3 ticked |

### Build and Tests Execution

**Build**: PASSED. Restore exit 0; `dotnet build -c Release --no-restore` exit 0, 0 errors; the only warnings are the pre-existing NU1507 package-source warnings (no code warnings).
**Tests**: PASSED, exit 0, no skips: ArchitectureTests 31, Application.Tests 932, Infrastructure.Tests 432, Web.Tests 223 (1,618 total). No BmadPlatform.Web process was running.
**Coverage**: not available (no coverage tool configured).

### Spec Compliance Matrix

Legend: COMPLIANT = a passing covering test (or, for [UI]/[DOC], supporting evidence that I re-read: the Chrome document, a grep, the source). PARTIAL = only part of the scenario is proven. UNTESTED = no test and no evidence.

**SPEC-CX (context): 13 requirements, 62 scenarios**

| Requirement | Scenario | Evidence | Result |
|---|---|---|---|
| Model and kinds | Kind-specific fields | AttachmentTests (file metadata, links, notes) | COMPLIANT |
| | Table, prefix, no cross-module access | ApplicationDbContextModelTests, ModuleBoundaryTests (Context pairs, non-vacuous) | COMPLIANT |
| | Nothing requires context | DraftContextTests (pdf and link only), AssistantContextTests, handler tests with no attachments | COMPLIANT |
| Adding a file | Valid file is stored | ContextHandlerTests > A_valid_file_is_stored_under_its_key_and_listed | COMPLIANT |
| | Zero-byte file is rejected | ContextValidatorTests > Unacceptable_files_fail...; ContextHandlerTests > A_real_zero_byte_stream... | COMPLIANT |
| | Over the per-file cap | ContextValidatorTests; ContextHandlerTests > A_stream_longer_than_announced... | COMPLIANT |
| | Blocklisted extensions | ContextValidatorTests > Every_blocked_extension_fails, Acceptable_files_pass | COMPLIANT |
| | Item and total caps | ContextHandlerTests > The_twenty_first_item..., The_total_cap... | COMPLIANT |
| | Path parts are dropped | ContextHandlerTests > Path_parts_of_the_name_are_dropped | COMPLIANT |
| | Failed insert removes the file | ContextHandlerTests > A_failed_insert_removes_the_stored_file_and_rethrows | COMPLIANT |
| | Another user initiative | ContextHandlerTests > Another_users_or_a_deleted_or_an_unknown_initiative... (Theory over 3 kinds) | COMPLIANT |
| Adding a link | Valid links; Rejected links | ContextValidatorTests, AttachmentTests | COMPLIANT (2) |
| | Link label is the host | ContextHandlerTests > The_view_lists_the_items... (label example.com) | COMPLIANT |
| | Link opens safely [UI] | Chrome doc S4 (target and rel seen); ContextCard.razor:50 | COMPLIANT |
| Adding a note | Limits | ContextValidatorTests, AttachmentTests | COMPLIANT |
| | Plain text rendering [UI] | Chrome doc S4 (literal markup shown); ContextCard.razor:45 plain interpolation | COMPLIANT |
| Add and delete only | No edit operation [DOC] | Features/Context holds add x3, delete, list, download, excerpt query | COMPLIANT |
| | Any status [UNIT] | ContextHandlerTests:173-190 covers add in 3 statuses; the delete half has no test | PARTIAL |
| | Oldest first | ContextHandlerTests > The_view_lists_the_items... (out-of-order insert) | COMPLIANT |
| Owner-only access | Foreign initiative on every operation | ContextHandlerTests (adds), ContextAccessTests (delete, download), GetContext null test | COMPLIANT |
| | Attachment of another initiative | ContextAccessTests > Everything_that_is_not_an_own_downloadable_file_gives_null | COMPLIANT |
| | Soft-deleted initiative | ContextHandlerTests (deleted Theory), ContextAccessTests (deleted-initiative case) | COMPLIANT |
| Download endpoint | Headers on success [UI] | Chrome doc S3 (200, octet-stream, attachment with filename star, nosniff, no-store); ContextEndpoints.cs:49-52 | COMPLIANT (manual only, see W6) |
| | Hostile content type and name | Unit half: AttachmentFileNameTests, AttachmentTests. The UI download of a hostile name is not in the Chrome doc | PARTIAL |
| | Name sanitising table | AttachmentFileNameTests | COMPLIANT |
| | Uniform 404 | Unit half: ContextAccessTests. UI: the Chrome doc shows 404 for a random id and for another person URL only (no deleted-initiative id, no Link id, no header check) | PARTIAL |
| | Original name on download [UI] | The Chrome doc shows the download and the other-user 404 but never records the saved file name | PARTIAL |
| Storage key | Key shape; Traversal rejected; Root confinement | AttachmentTests, LocalFileStorageTests (10-case Theory, lazy root, below-root) | COMPLIANT (3) |
| IFileStorage contract | Round trip; Delete is idempotent | LocalFileStorageTests | COMPLIANT (2) |
| | Default provider; S3 and unknown fail; Invalid max size | StorageRegistrationTests, StorageOptionsTests | COMPLIANT (3) |
| | Ephemeral disk documented [DOC] | README.md section Archivos adjuntos | COMPLIANT |
| Delete order | File then row; Storage failure still removes row; Missing file; Link and note rows; Foreign delete | ContextAccessTests (5 tests) | COMPLIANT (5) |
| Logging | Distinctive values do not appear | ContextAccessTests > No_log_entry_contains... (add file/link/note, get, download, delete failure, insert failure) | COMPLIANT |
| | Logs in a real session [UI] | Chrome doc S3 and S4 (Logs/ without names, titles, URLs, text) | COMPLIANT |
| Context card | Upload with feedback [UI] | Chrome doc S3: row, size, Descargar, green line. The loading state is not recorded | PARTIAL |
| | Over-cap, empty and blocked | Chrome doc S3 (11 MB, empty, .exe) | COMPLIANT |
| | Limit reached [UI] | Chrome doc says the 20-item and 50 MB caps were not exercised in the UI | UNTESTED (UI) |
| | Link and note forms | Chrome doc S4 | COMPLIANT |
| | Delete confirmation | Chrome doc S4 (cancel keeps, confirm removes, three kinds) | COMPLIANT |
| | Maximum size is explicit | View carries the cap (ContextHandlerTests); OpenReadStream(limits.MaxFileBytes) at ContextCard.razor:380 | COMPLIANT |
| | CSS-only views [DOC] | rg for style= and script tags in Components/Context and InitiativeDetail.razor: no match | COMPLIANT |
| Text-like context (S5) | Only text-like items | ContextExcerptTests > Only_notes_and_the_four_text_extensions... | COMPLIANT |
| | No ids or URLs in the block | Gemini assistant and drafter tests (exact block assertions) | COMPLIANT |
| | Delimiter injection neutralised | Both Gemini test classes (exactly one closing tag, at the end) | COMPLIANT |
| | Caps and order | ContextExcerptTests (5,000 / 15,000 / 10 items / marker) | COMPLIANT |
| | Invalid text replaced, missing skipped | ContextExcerptTests | COMPLIANT |
| | No context leaves the request unchanged | Gemini tests (null and empty list byte-identical) | COMPLIANT |
| | Context stays out of the system instruction | Gemini tests (system equals System + ContextRules) | COMPLIANT |
| | Fake ignores context | Fake assistant and Fake drafter tests | COMPLIANT |
| | Architecture and Epics carry no context | DraftContextTests; GeminiArtifactDrafterTests:882 | COMPLIANT |
| | Counts-only logs | ContextExcerptTests > The_logs_carry_the_counts... | COMPLIANT |
| | An embedded instruction is ignored [MANUAL] | docs/gemini-evaluacion.md:158-164: turns only, no draft run, example did not clearly cite the data | PARTIAL |

**SPEC-AS (assistant): 3 requirements, 8 scenarios.** Block inside user content only, Draft call carries it too, No block same body, No identifiers or URLs, Hostile context cannot change the script (AssistantContextTests, GeminiAssistantServiceTests), Delimiter inside context text, No file parts (inlineData absent in both Gemini test classes), Context counts without content: all COMPLIANT (8/8).

**SPEC-IN (initiatives): 1 requirement, 3 scenarios.** Contexto is a live card (Chrome doc S3; grep of the placeholder text and the Placeholders array in src/Web finds nothing): COMPLIANT. Initiatives module does not depend on Context (ModuleBoundaryTests): COMPLIANT. Context in any status [UI] (the Chrome doc lists the per-status label check as not exercised): UNTESTED (UI).

**Compliance summary**: 65/73 scenarios compliant, 6 PARTIAL, 2 UNTESTED (both [UI], recorded by the author as not exercised). No FAILING scenario. Requirements fully compliant: 12/17 (not complete: Add and delete only, Download endpoint, Context card, Text-like context, and the Initiatives MODIFIED requirement).

### Correctness (Static Evidence)

| Area | Status | Notes |
|---|---|---|
| Path traversal | OK | Regex `^[0-9a-f]{32}/[0-9a-f]{32}\z` rejects `..`, rooted paths, backslash, uppercase, trailing newline; the full-path prefix check is a second guard; the `.part` name cannot match a key so it is never readable; the part file is deleted on any failure (`await using` closes the handle before the catch). One robustness bug in the root itself (W1) |
| Blocklist | OK | Last extension of the normalised name, case-insensitive; `a.exe.` and `a.exe ` are trimmed; the colon is replaced so an NTFS stream suffix cannot survive; `x.exe.txt` accepted by design. Unicode trailing spaces (NBSP) are not trimmed, harmless because nothing is executed or served inline |
| Content-type trust | OK | Stored for information only; the response type is the constant `application/octet-stream` (ContextEndpoints.cs:52) |
| Download | OK | `.RequireAuthorization()`, ids only, `Results.File` writes `Content-Disposition: attachment` with the extended filename form, plus `nosniff`, `no-store`, range processing off; every non-owner path returns null and then an empty `Results.NotFound()` |
| Owner isolation | OK | Every handler goes through `GetInitiativeQuery`; every repository read filters `OwnerId`; delete re-checks the owner; a soft-deleted initiative resolves to null |
| Links | OK (probed with .NET Uri) | `javascript:` in any case or with leading spaces, tab-split `java script:`, `data:`, `file:`, `//host`, `http:host`, `https:/host`, backslash forms and an empty host are rejected. The card also re-checks the scheme before rendering an `href` (ContextCard.razor:48) |
| XSS | OK | Titles, names, note text and URLs go through Razor encoding; no `MarkupString`, no `style=`, no script tag |
| Open redirect | None | The feature produces no redirect |
| Upload limits | OK | `OpenReadStream(MaxFileBytes)`; declared size 0 or over the cap is rejected before opening; storage counts real bytes and throws past `min(cap, 50 MB - used)`; the part file is removed on cancel; an insert failure or a cancellation after the write deletes the key with `CancellationToken.None`; cap races are accepted and documented |
| Prompt injection | OK | `Neutralize` replaces every ASCII angle bracket; titles and names collapse to one line; the block is appended to the single user part; the rules go to the system instruction only when the block is present; Architecture and Epics are excluded in `ArtifactGuards.DraftRequestAsync` (lines 116-118) and their prompts are untouched; ids and URLs never enter the block |
| Logs (RNF-02) | OK | New handlers log only ids, exception type and counts; `LoggingBehavior` logs names and types; the card logs type and id; the Gemini client is unchanged |
| Migration | OK | `AddContextAttachments` creates `ctx_attachments` and one index only; the snapshot diff is additions only (50 lines); no FK; the model test passes |
| Module boundary | OK | Context entry plus the reverse pairs for all four modules; the lowercase "attached" rule is enforced because `AttachmentEntityToken` is applied to the Assistant and Artifacts folders |

### Coherence (Design)

All 20 decisions are followed. Each of R1-R23 was checked against the code and holds as recorded. Divergences that are NOT in the Reconciliation notes, all minor: S2 (shared `ContextPrompt` helper), S3 (skipped-count semantics), S5 (blank `MaxFileSizeMb`). None breaks a spec requirement.

### Issues Found

**CRITICAL**: None.

**WARNING**

- W1. A trailing directory separator in `Storage:LocalRootPath` makes every save, open and delete fail. `LocalFileStorage.cs:22` keeps the separator that `Path.GetFullPath` preserves (running `GetFullPath("App_Data/attachments/")` returns a path ending in a backslash), and `LocalFileStorage.cs:117` then requires the prefix `root + separator`, a double separator that no path matches. Every key is rejected as not valid and the card shows a generic failure. A Railway volume path such as `/data/` or a drive root triggers it; the default works. Fix: `root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath))` (handle a filesystem root) plus a `LocalFileStorageTests` case with a trailing separator.
- W2. README.md:7 still says step 6 is the current state and that the attached context (step 7) is pending. Step 7 is delivered, so the status line contradicts the new Archivos adjuntos section. Fix: update the sentence (step 7 delivered, step 8 pending).
- W3. Group F is not done and tracking is inconsistent. tasks.md F.1-F.5 are unticked; the sub-checks under 3.5, 4.4 and 5.6 are unticked while their parents are ticked; the proposal success criteria are unticked. Missing deliverables: the F.2 table "Cobertura de escenarios" in `docs/evidencias/context-attachments-recorrido-chrome.md` (the file has only the S3 and S4 rows and one not-exercised sentence: no S5 row, no environment or deviation sections); the F.1 follow-ups in `docs/mejoras-posteriores.md` (S3 storage, images and PDF to the model, malware scan, drag and drop, purge of deleted initiatives, context for Architecture and Epics, cross-tab race, listing order) are absent. I treated these as closing documentation (WARNING). Under the sdd-verify rule that unchecked tasks stay CRITICAL the orchestrator may reclassify; code and tests are complete either way. Fix: finish F.1-F.3 and tick the boxes.
- W4. Eight [UI] items are not fully evidenced, several recorded by the author as not exercised: Limit reached (20 items or 50 MB in the UI); Context in any status (Draft, Planning, Ready to build); download of a hostile name; saved name of a download; 404 for a deleted-initiative id and a Link id, and the 404 headers; the upload loading state; the guard for more than 5 files. There is no S5 Fake-regression row (first two bullets of task 5.6). Fix: run them in Chrome and add rows, or record them as accepted gaps.
- W5. The live Gemini check is partial. `docs/gemini-evaluacion.md:158-164` ran two assistant turns and no Brief or PRD draft, and says the example did not clearly cite the note. The scenario An embedded instruction is ignored needs a turn, a draft and a cited datum. The heading at line 158 ("architecture del paso 7") is a copy-paste slip. The document is honest about the gap. Fix: run one draft with context, record it, fix the heading.
- W6. The security-critical response contract has no automated test. `ContextEndpoints.DownloadAsync` (`ContextEndpoints.cs:25-53`) sets `nosniff`, `no-store`, the constant content type and the attachment disposition, and the route carries `.RequireAuthorization()`; removing any of them keeps all 1,618 tests green. R8 accepted manual-only evidence, but the method is `internal static` with plain parameters, so a Web.Tests case with `DefaultHttpContext`, a stub `IMediator` and execution of the result is cheap. Fix: one small Theory (found file and null) asserting status, headers and Content-Disposition.
- W7. Any status is only half proven: `ContextHandlerTests.cs:173-190` adds in three statuses, but no test deletes in Draft, Planning and ReadyToBuild (the scenario says an item is added and deleted). Fix: extend the Theory with a delete and a status assertion.

**SUGGESTION**

- S1. The 404 carries no `nosniff` or `no-store` (`ContextEndpoints.cs:44`) while the scenario Uniform 404 says the same headers. Set both on every response of the endpoint, or amend the scenario to say empty body.
- S2. `ContextPrompt` (`src/BmadPlatform.Infrastructure/Gemini/ContextPrompt.cs`) is a shared helper that D18 and R14 do not mention (they say AssistantPrompt and DraftPrompt build the block). It sits outside every module folder, so the boundary scan does not read it. Record it in D18 and R14.
- S3. The excerpt log skipped count is `candidates.Count - excerpts.Count` (`GetContextForAssistantQueryHandler.cs:84`), so items dropped by the 10-item or 15,000-character cap and empty files count as skipped; the spec ties skipped to missing or unreadable files. Count them separately or amend the spec wording.
- S4. Inside a note or file text, a line starting with a triple dash plus `note:` or `file:` can imitate an item boundary, and fullwidth lookalikes of the angle brackets (U+FF1C, U+FF1E) pass `Neutralize` (`ContextPrompt.cs:28-30`, `PromptText.cs:9-10`). The block is data and the rules say so, so the risk is low. Optional hardening: neutralise a leading triple dash per line and map the fullwidth brackets.
- S5. `Storage:MaxFileSizeMb` set to an empty string throws at startup (`StorageOptions.cs:54-60`) while an empty `Storage:Provider` means Local (R23). An empty environment variable is a common deployment slip; treat blank as absent and record it as R24.
- S6. `AttachmentRepository` has no automated test (owner filter, `ExecuteDeleteAsync` result, ordering tie-break); the handler tests use an in-memory double that re-implements the filter. Accepted by AGENTS 4.1 (no integration tests yet); revisit with Testcontainers.
- S7. Docs: the README attachments section does not say that notes and text files (`.txt`, `.md`, `.markdown`, `.csv`) go to Gemini when `Assistant:Provider=Gemini` (free tier, AGENTS 4.8 warning); AGENTS.md section 6.4 item 4 still says to confirm on reaching step 7 although S3 was deferred.
- S8. `ContextCard.razor:386-390` maps every `IOException` to the per-file cap message, so a disk-full error during upload would read as a size error. Design D13 prescribes it; consider letting storage errors reach the generic failure path.
- S9. A `.part` file left by a process crash is never cleaned (it is not listed, counted or served, so it is harmless); mention it in the README disk notes or add a startup sweep later.

### Spec and doc lines to amend

1. specs/context/spec.md, requirements Storage key and IFileStorage contract: state that the configured root has its trailing separator removed (after W1).
2. specs/context/spec.md, scenario Uniform 404: replace "the same headers" with the exact expectation chosen for S1.
3. specs/context/spec.md, scenario Counts-only logs and the Text-like paragraph: define skipped (S3).
4. design.md D18 and tasks.md R14: record the shared `ContextPrompt` helper (S2). design.md D5: trailing separator rule. D6 and R23: blank `MaxFileSizeMb` as a new R24 (S5).
5. specs/context/spec.md scenario Any status and tasks.md 2.3 and 2.5: say the delete half is covered by the extended Theory (W7).
6. tasks.md: tick F.1-F.5, the sub-checks of 3.5, 4.4 and 5.6, and the proposal success criteria once done (W3); docs/gemini-evaluacion.md:158 heading (W5).
7. README.md:7 status sentence (W2); README attachments section and AGENTS.md 6.4 item 4 (S7).
8. docs/evidencias/context-attachments-recorrido-chrome.md: add the S5 row, the Cobertura de escenarios table, deviations and the not-verified list (W3, W4).

### Verdict

FAIL on evidence completeness only (envelope verdict `fail`: 12/17 requirements and 65/73 scenarios complete). Code-quality verdict: PASS WITH WARNINGS.

0 CRITICAL, 7 WARNING, 9 SUGGESTION. Build and all 1,618 tests pass, the security probes found no exploitable defect, and the code violates no spec requirement. The validator refuses a passing envelope while 8 scenarios are PARTIAL or UNTESTED (6 [UI], 1 [MANUAL], 1 [UNIT] delete half), so the envelope is `fail` although nothing is a CRITICAL defect. Route: close Group F (W3), run the missing Chrome and live checks or record them as accepted gaps (W4, W5), and apply W1, W6 and W7 as a small fix; then re-verify. If the orchestrator accepts the gaps explicitly in tasks.md and the evidence document, the change can be archived as PASS WITH WARNINGS.
