# Proposal: Context attachments (PROPUESTA-MVP step 7)

## Intent

Product people often start from a video, an AI-made prototype or a few notes, but the "Contexto" card on the initiative detail is still a placeholder ("Disponible próximamente"). RF-22 and RF-23 promise optional context of any kind stored outside MySQL, and RF-14 says the assistant uses it when it can. Step 7 lets the owner attach files, links and notes to an initiative, download or delete them, and gives the assistant the text-like ones. Success: an owner can add, download and delete the three kinds in Chrome. No file bytes are stored in MySQL. Other users get a 404. The assistant cites a note or a `.md` file without following instructions written inside it.

## Scope

### In Scope
- New `Context` module: `Attachment` + `AttachmentKind` (File, Link, Note), table `ctx_attachments`, additive migration, `ModuleBoundaryTests` and model-test entries.
- `IFileStorage` in Application. `LocalFileStorage` only, behind `Storage:Provider=Local` (the default). If `S3` is configured, startup fails with a clear message, as `Assistant:Provider` does. The storage key is server-generated (`{initiativeId:N}/{attachmentId:N}`) and never the user's file name.
- Limits: 10 MB per file (`Storage:MaxFileSizeMb`), zero-byte files rejected, 20 items and 50 MB of files per initiative. Any content type is accepted except an extension blocklist (`.exe .dll .bat .cmd .com .msi .scr .js .vbs .ps1 .sh .jar`).
- Links: `http`/`https` only, up to 2,000 characters, open in a new tab with `noopener noreferrer`. Notes: a title up to 120 characters and text up to 5,000, stored in MySQL.
- Add and delete only (no edit). Only the owner can do this, in any status, and attachments stay optional (RN-04). Delete uses the destructive `ConfirmPanel`, removes the file first and then the row, and logs an orphaned file.
- A download endpoint behind a MediatR owner check. It always sends `application/octet-stream` with `Content-Disposition: attachment` and a sanitised name, plus `nosniff` and `no-store`. Foreign, deleted and unknown ids get the same 404.
- `ContextCard` with `InputFile` (explicit `maxAllowedSize`, streamed to storage). Views use CSS classes only.
- S5 (the last slice): notes and `text/plain|markdown|csv` files go to Gemini as delimited, neutralised `<context>` data with a size cap. Every other type stays a reference (RF-24, RF-25).

### Out of Scope
- `S3FileStorage` and the Railway bucket (AGENTS §6.4), images, PDF or video sent to the model as `inlineData`, malware scanning, editing, drag-and-drop or paste (needs a new JS module), and the `BmadPlatform.*` rename.

## Capabilities

### New Capabilities
- `context`: attachments (file, link, note), the storage abstraction, limits, download and delete.

### Modified Capabilities
- `initiatives`: the "Contexto" section is no longer a placeholder (the requirement "Detail view with placeholder sections").
- `assistant` (S5): the prompt includes text-like context. This capability exists only in the unarchived `gemini-assistant` change, so write the delta against it.

## Approach (hybrid; budget 400, stacked to main)

| PR | Slice | Est. lines |
|---|---|---|
| S1 | Domain factories and limits, `IFileStorage`, `LocalFileStorage` (traversal-safe), `Storage:` options and startup validation, EF config, migration, architecture and model tests | ~350 |
| S2 | Add file/link/note commands, list and download queries, delete command, validators, ownership guard through `GetInitiativeQuery` | ~350 |
| S3 | `ContextCard`, `InputFile` upload, item list, download endpoint | ~350 |
| S4 | Link and note forms, delete dialog for all kinds | ~250 |
| S5 | Text-like context in the Gemini prompt (delimited, capped, neutralised) | ~300 |

Total ~1,600 lines.

## Requirement changes (intentions)

- RF-14: amend. The assistant receives only text-like context (notes, plain text, Markdown, CSV) as delimited data, never as instructions.
- RF-22: amend. Any type except the executable blocklist, with the per-file and per-initiative limits.
- RF-23: amend. Local storage only for now. Selecting `S3` fails at startup until the S3 slice exists. Railway's disk is ephemeral.
- RF-24: amend and confirm. Define the interpretable types (as in RF-14). Change `[SUPUESTO]` to `[CONFIRMADO]`.
- RF-25: amend. Images and PDF join video and prototypes as stored references.
- RN-04: unchanged. Reference it from the new requirements.
- New RF-75: the owner adds and deletes files, links and notes in any status. There is no edit, and deletion is confirmed.
- New RF-76: limits and validation (10 MB, 20 items, 50 MB, zero-byte, blocklist, link scheme and length, note lengths).
- New RF-77: downloads are always forced, with a sanitised name, and other users get a uniform 404.

## Affected Areas

| Area | Impact |
|---|---|
| `src/BmadPlatform.Domain/Context/` | New |
| `src/BmadPlatform.Application/Features/Context/`, `Abstractions` (`IFileStorage`) | New |
| `src/BmadPlatform.Infrastructure/Context/`, `Storage/`, `Migrations/` | New |
| `src/BmadPlatform.Infrastructure/Gemini/` (S5) | Modified |
| `src/BmadPlatform.Web/` (`InitiativeDetail.razor`, `ContextCard`, endpoint, `appsettings.json`) | New/Modified |
| `tests/` (module boundary, model, domain, handlers, storage) | New/Modified |
| REQUISITOS.md, AGENTS.md §4.7, README | Modified |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Blazor upload limits (500 KB default, 128 KB SignalR) | Med | Explicit `maxAllowedSize`, streamed reads, a clear error over the cap |
| Path traversal or a hostile file name | Low | Server keys, a root-confined path check, a test |
| Stored XSS or content sniffing | Low | Always octet-stream, attachment and `nosniff` |
| No malware scan | Med | Blocklist and forced download. The gap is documented |
| Disk growth from open registration | Med | Per-initiative caps |
| Railway redeploy loses files | High (prod) | Documented. The S3 slice comes later |
| Prompt injection through notes (S5) | Med | Delimited data block, neutralised tags, a cap, system instruction |
| Hardcoded lists in the architecture and model tests | High | Updated in S1 |

## Rollback Plan

Revert the slice PRs in reverse order (S5 first, so the prompt goes back to text only). The migration is additive: dropping `ctx_attachments` affects no other module. `Storage:Provider=Local` stays the only working provider. The stored files sit under the storage root and can be deleted.

## Success Criteria

- [ ] In Chrome (`docker compose` MySQL, seed user): upload a file and download it with its original name. A file over 10 MB, a zero-byte file and a `.exe` are rejected with Spanish messages. A link and a note are added. Deleting each kind shows the dialog and removes the item. A foreign or unknown download URL returns 404. `Logs/` has ids only, with no names or content. _Partly evidenced: rejections, link, note and delete were seen in Chrome; the saved file name was not recorded (pendiente)._
- [ ] The Fake assistant runs unchanged. In the live Gemini check (manual, `docs/gemini-evaluacion.md`), a note and a `.md` file are used, and an embedded instruction is ignored. _Not yet: the Fake regression with context was not walked in Chrome, and the live check ran two turns and no draft (see the Chrome evidence doc)._
- [x] Lean xUnit tests and the build pass with no key and no network. The new module passes the boundary and model tests. A grep for `style=` and `<script` in the touched views is clean.

## Proposal question round

Skipped by the user's instruction: every product decision in `exploration.md` is final (the recommended defaults). Residual assumptions to review:
- Attachments of a soft-deleted initiative become unreachable but are not purged.
- The exact S5 context cap (characters per item and in total) is set during design.
