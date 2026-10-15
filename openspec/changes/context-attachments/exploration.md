# Exploration: context-attachments (PROPUESTA-MVP §7 step 7)

## Current state

- The "Contexto" card on the initiative detail is a placeholder (`InitiativeDetail.razor`, `Placeholders` array: "Aquí podrá adjuntar videos, enlaces y notas.", badge "Disponible próximamente").
- Existing modules (Initiatives, Assistant, Artifacts) use `Domain/<M>`, `Application/Features/<M>`, `Infrastructure/<M>`, their own `IEntityTypeConfiguration` and table prefix (`ini_`, `asi_`, `art_`); cross-module access only through MediatR (`ArtifactGuards` sends `GetInitiativeQuery`; ownership = not found for foreign ids). Tables carry `OwnerId varchar(255)` and `InitiativeId` with no foreign key; repositories use `IDbContextFactory` and `AsNoTracking`.
- Tests that a new module must satisfy: `ModuleBoundaryTests` (hardcoded `Modules` array + InlineData per layer) and `ApplicationDbContextModelTests` (hardcoded table list, prefix regex, no pending model changes).
- Download precedent: `ArtifactEndpoints` (minimal API `.RequireAuthorization()`, `IHostEnvironmentAuthenticationStateProvider`, MediatR query for the owner check, `Cache-Control: no-store`, `Results.File`); `DocumentDownloads.razor` uses `<a download data-enhance-nav="false">`.
- No `IFileStorage` and no `Storage:` configuration exist. Railway disk is ephemeral (AGENTS §6.4). SignalR `MaximumReceiveMessageSize` is 128 KB.
- JS modules allowed in `wwwroot/js`: chat.js, password-toggle.js, password-match.js, password-rules.js, confirm-dialog.js, print.js. Views use CSS classes only. `ConfirmPanel` (native dialog, `Destructive`) is ready for delete confirmation.
- Initiative delete is a soft delete; attachments of a deleted initiative simply become unreachable.
- Gemini integration is text only (`GeminiCall.UserContent` is a string; `GeminiDtos.Part` has only `Text`/`Thought`); images/PDF as `inlineData` are unverified with the real key.

## Approaches

- Upload transport: **Blazor `InputFile`** (recommended; `OpenReadStream(maxAllowedSize)` must be explicit, default is 500 KB; stream straight to storage) vs HTTP multipart POST vs custom drop zone/paste (new JS module, YAGNI).
- Storage key: server-generated `{initiativeId:N}/{attachmentId:N}`, never the user's name; original name/content type/size live in MySQL.
- Assistant use: store only (1), text-like content only inside delimited prompt blocks (2, recommended as the last slice), images/PDF as `inlineData` (3, deferred).

## Decisions taken (recommended defaults; the user asked not to be asked)

| Decision | Default |
|---|---|
| Module | New `Context` module, table `ctx_attachments`, `Attachment` + `AttachmentKind` (File, Link, Note) |
| Storage | `IFileStorage` in Application; `LocalFileStorage` only; `Storage:Provider=Local` default; `S3` configured → startup fails with a clear message (same pattern as `Assistant:Provider`) |
| Max size per file | 10 MB (`Storage:MaxFileSizeMb`), zero-byte rejected |
| Per-initiative limits | 20 items, 50 MB of files |
| Allowed types | any content type (RF-22) except an extension blocklist: `.exe .dll .bat .cmd .com .msi .scr .js .vbs .ps1 .sh .jar` |
| Links | `http`/`https` only, ≤ 2,000 chars, `rel="noopener noreferrer"`, `target="_blank"` |
| Notes | title ≤ 120, text ≤ 5,000, stored in MySQL (text, not a file) |
| Edit | none in the MVP: add and delete only |
| Delete | `ConfirmPanel` destructive; file first, then row; orphan tolerated and logged |
| Download | own `GET` endpoint through a MediatR query (owner check); `Content-Disposition: attachment` with sanitised name, `X-Content-Type-Options: nosniff`, `Cache-Control: no-store`, always `application/octet-stream`; same 404 for foreign/deleted/unknown |
| Assistant | S5 optional: text-like content (notes, `text/plain|markdown|csv`) as delimited, neutralised `<context>` data with a cap; images/PDF/video stored as references (RF-24, RF-25) |
| Who/when | owner only, any status, optional (RN-04) |

## Slices (≤ 400 lines each, stacked to main)

- S1 domain, `IFileStorage`, `LocalFileStorage`, EF config, migration, architecture/model tests (~350)
- S2 commands/queries with validators and guards (~350)
- S3 `ContextCard` UI with `InputFile` upload, row list, download endpoint (~350)
- S4 links and notes forms, delete dialog for all kinds (~250)
- S5 text-like content to Gemini, optional (~300)

## Risks

Blazor Server upload limits; path traversal and naming; content-type sniffing/XSS (always forced download); no malware scanner (documented gap); authorization via MediatR with uniform 404; unbounded initiatives because registration is open; Railway ephemeral disk loses attachments on redeploy (S3 later); orphans and soft delete; prompt injection (S5); hardcoded lists in architecture/model tests must be updated in S1; views CSS-only.

## Test plan

Lean xUnit, Theory-grouped: domain factories (limits, scheme allowlist, blocklist), handlers with in-memory fakes (owner isolation, limits, failed row insert deletes the file, delete removes both), validators, `LocalFileStorage` round trip and traversal rejection, model tests for `ctx_attachments`, `Storage:Provider` validation, `ModuleBoundaryTests` entry, pure web logic (file-name sanitising, size labels). Manual Chrome run per AGENTS §5.4 (upload, over-cap file, link, note, download, delete dialog, foreign URL 404, `Logs/` content-free).
