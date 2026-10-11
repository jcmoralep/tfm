# Design: BMAD alignment after step 4

## Technical Approach

The change stays inside the `Artifacts` module and adds no table, column, flag or state (RF-20, RN-03, RF-56). The owner and the revisit condition of an open question live in its Markdown line, `- **Q-n**: texto (Responsable: X; Revisar: Y)` (RF-61). Two pure functions in `Application/Features/Artifacts/Markdown` carry all the logic. `OpenQuestions.Parse` reads the lines and is reused by the view builder, the approve guard and the panel. `QuestionOwners.Apply` rewrites only the suffix of the targeted lines. The approval gate (RF-62, RN-08) runs only on the Draft-to-Approved transition. A new `AssignQuestionOwnersCommand` follows the edit path of `SaveArtifactContentCommandHandler` (RF-63, RF-54, RN-06). PRD 2.3 loses its `UJ-n` placeholders (RF-64). The identifier decision (RF-36/RF-44) is documentation only. The real-BMAD checklist is a new section in `docs/bmad-referencia.md`. Format warnings stay non-blocking (RF-59), and regeneration resets owners to the placeholders (RF-57, RF-60).

## Architecture Decisions

| # | Topic | Options and tradeoff | Decision |
|---|---|---|---|
| 1 | Which lines are questions | Only lines inside the "Preguntas abiertas/pendientes" section: a moved line would escape the gate / every line the checker treats as a valid `Q-n` definition | **The same recognition as `ArtifactFormatChecker`**: line start, optional list marker and bold, outside fences and headings, anywhere in the document (the assumption index included, as in the checker). A line the checker counts as `Q-n` can never escape the gate |
| 2 | Malformed ids (`Q1`, `q-1`, `Q-01`, `Q-0`) | Gate them too: guessing / ignore them | **Ignored by the parser.** `q-1`, `Q-01` and `Q-0` already raise format warnings. `Q1` is prose to the checker (decision of artifact-generation), so a hand edit can bypass the gate (see Risks) |
| 3 | Suffix grammar | Anywhere in the line: text with parentheses breaks it / **anchored at the end** | `\s*\(\s*Responsable\s*:\s*(?<owner>[^();]*?)\s*(?:;\s*Revisar\s*:\s*(?<revisit>[^();]*?)\s*)?\)\s*$` (labels case-insensitive). It matches only the last group of the line. Text such as `«¿Qué pasa (si aplica)?»` keeps its parentheses. A trailing `(ver anexo)` is not a suffix, so `Apply` appends one after it |
| 4 | Unassigned | Placeholder per field / any placeholder in either field | **A missing suffix, a missing `Revisar`, an empty field, or either placeholder (`por asignar`, `por definir`, trimmed, case-insensitive) in either field.** "Revisar: por asignar" is as empty as "por definir" |
| 5 | Duplicate ids | Key by line: fragile / reject the save: the gate could never be met from the panel / **key by id, apply to every occurrence** | The panel shows one row per distinct id (first text). The save writes every line with that id. The checker already warns about the duplicate |
| 6 | Allowed values | Free text with no rules: `;`, `(` or `[ASSUMPTION` would break the suffix or the checker / a closed list (rejected, RF-61) | **Free text, trimmed, inner whitespace collapsed.** Owner up to 60 characters, revisit up to 80 (aligned with the spec). Forbidden in both fields: `( ) ; [ ] * | \` and line breaks. Empty writes the placeholder. `QuestionOwners.IsAllowed` is the single rule, used by the validator |
| 7 | Gate placement | Next to `RequireAvailable`: breaks the CompletionPending retry and reopens grandfathered documents / **inside `if (artifact.Status != Approved)`** | The idempotent retry and documents approved before this change keep working (RF-62) |
| 8 | Partial save | Require every field: a person cannot save progress / **allow partial** | Saving is never blocked by empty fields; approval is. The red asterisk means "needed to approve", and the legend says so |
| 9 | Edit path | Copy the save handler / **extract `ArtifactGuards.SaveEditAsync`** | Shared by save and assign (DRY): unchanged content does nothing; otherwise reopen first if ReadyToBuild, then `Edit`, then `SaveAsync(expectedVersion)` |
| 10 | Journeys | Omit 2.3 / a `Q-n` / **one honest assumption** | Exact RF-64 text. If the users answer is "No sé", ` Ver Q-n.` is appended (that question already exists from section 2) |

## Interfaces / Contracts

```csharp
// Application/Features/Artifacts/Markdown
public sealed record OpenQuestion(string Id, int Line, string Text, string? Owner, string? Revisit)
{ public bool IsAssigned { get; } }                       // decision 4
public static partial class OpenQuestions
{
    public const string OwnerPlaceholder = "por asignar", RevisitPlaceholder = "por definir";
    public static string DefaultSuffix { get; }            // " (Responsable: por asignar; Revisar: por definir)"
    public static string Suffix(string owner, string revisit);
    public static IReadOnlyList<OpenQuestion> Parse(string markdown);   // document order, duplicates kept
    public static IReadOnlyList<string> Unassigned(string markdown);   // distinct ids, document order
}
public sealed record QuestionAssignment(string Id, string? Owner, string? Revisit);
public static partial class QuestionOwners
{
    public const int OwnerMaxLength = 60, RevisitMaxLength = 80;
    public static bool IsAllowed(string? value);
    // Rewrites only head + text + canonical suffix of the targeted lines; every other byte is unchanged.
    // Unknown id -> DomainException(ArtifactTexts.QuestionNotFound(id)).
    public static string Apply(string markdown, IReadOnlyList<QuestionAssignment> assignments);
}
// Features/Artifacts/AssignQuestionOwners
public sealed record AssignQuestionOwnersCommand(Guid InitiativeId, ArtifactType Type, int ExpectedVersion,
    IReadOnlyList<QuestionAssignment> Assignments) : IRequest<ArtifactView>;  // ToString: ids and count only
```

- Line split: `head` is the definition prefix as written (`- **Q-2**:`, `Q-2.`, `- **Q-2:**`), `text` is the body without the suffix and with its edges trimmed. A rewritten line is `head + " " + text + Suffix`, or `head + Suffix` when there is no text. Idempotence: `Apply(Apply(m,a),a) == Apply(m,a)`, and applying the current values returns the input unchanged, so the handler does nothing.
- Fences and line endings: the `FencedLines`/split logic moves from the checker into an internal `MarkdownLines` helper shared by both readers. Its behaviour does not change.
- `ArtifactView` gains `IReadOnlyList<OpenQuestion> Questions` and `IReadOnlyList<string> UnassignedQuestions` (appended). `CanApprove &= UnassignedQuestions.Count == 0`, and `ApprovalCompletesSet` follows. `CompletionPending` is unchanged. Its `ToString` stays redacted.
- Texts (`ArtifactTexts`): `UnassignedQuestions(ids)` = "Asigne un responsable y una condición de revisión a las preguntas pendientes antes de aprobar: Q-1, Q-3." `QuestionNotFound(id)` = "La pregunta Q-9 ya no está en el documento." Per-row validator messages are prefixed with the id, for example "Q-2: el responsable no puede superar los 60 caracteres." and "Q-2: la condición de revisión no puede contener paréntesis, punto y coma, corchetes, asteriscos, barras ni saltos de línea." Tamper-only rules stay in `ArtifactValidationTexts`: `Assignments` must not be empty, at most 200 entries, ids must match `^Q-[1-9][0-9]{0,3}$` and be distinct.

## Data Flow

```
Approve (Draft):  version check -> RequireAvailable -> [Draft only] OpenQuestions.Unassigned(content) non-empty
                  -> DomainException(UnassignedQuestions) | Approve -> Save -> completion check (unchanged)
Assign:           validator -> owner/initiative/RequireManageable/RequireGenerated/RequireVersion
                  -> QuestionOwners.Apply -> Artifact.NormalizeContent (20,000 cap)      [no write yet]
                  -> SaveEditAsync: unchanged? no-op : ReadyToBuild? ReopenPlanning [w1] -> Edit -> Save(v) [w2]
                  -> ReloadViewAsync
Generate/Regenerate: DraftComposer.OpenQuestions writes Item(id, text + DefaultSuffix)
```

Every rule that can reject (validator, unknown id, length) runs before `ReopenPlanningCommand`. A failed save after reopening leaves Planning with approved documents, which is the CompletionPending state the next approval completes (RN-06). The accepted two-tab race from artifact-generation stays as it was. An approval in tab A against an owner save in tab B is caught by `WHERE Version = n`. Any content change returns the document to Draft, so the gate applies again. The narrow approve-versus-edit window can still leave ReadyToBuild with a Draft until the next write.

## Web

- `Components/Artifacts/OpenQuestionsPanel.razor` (presentational) owns only the field state. Its parameters are `Questions`, `Version`, `Busy`, `Enabled`, `Hint`, `OnSave(QuestionOwnersSubmission(BaseVersion, Assignments))` and `OnCancel`. It copies `Questions` and `Version` once in `OnInitialized` (the editBaseVersion lesson). The page renders it with `@key="questionsRevision"` and increments that only after a successful write or a Cancelar. After a conflict or a failure, the typed values and the stale base version stay, so a retry conflicts again until the person clicks Cancelar, matching `ArtifactTexts.Conflict`.
- Markup: a `<section aria-labelledby>` with an h2 "Preguntas pendientes" and an intro saying that saving counts as an edit (`ArtifactLabels.EditHint` when Approved or ReadyToBuild). Each question is a `<fieldset>` whose `<legend>` holds the id and text, with a text badge "Sin asignar" (not colour alone). It has two labelled inputs, "Responsable" (`list` → `<datalist>` Legal, TI, Finanzas, Quien propone la iniciativa) and "Revisar cuando" (placeholder "antes de la arquitectura"), with `maxlength` 60/120, a red `RequiredMark` and the legend "* necesario para aprobar". Inputs are `readonly` while busy, buttons are disabled until interactive, and `aria-describedby` points to the hint and to `doc-errors`. There is a single primary **Guardar**, which sends every row in one batch, and a secondary Cancelar.
- `ArtifactDocument.razor` (container): the panel shows in the read view when `Questions.Count > 0`, above the warnings. The page's only addition is `SaveOwnersAsync`, which goes through `RunAsync`. It shows the typing-dot "Guardando las preguntas pendientes…" and then a green notice: "Preguntas guardadas. Versión N." or "No había cambios que guardar." when the version did not move. Validation errors appear in red without a reload; a conflict or domain error reloads. When `UnassignedQuestions` is not empty, an amber hint with id `approve-blocked` shows the same `UnassignedQuestions` text and the Aprobar button gets `aria-describedby`. The Regenerar panel adds "Los responsables de las preguntas vuelven a «por asignar»."

## File Changes

| File | Action |
|---|---|
| `Application/Features/Artifacts/Markdown/{MarkdownLines,OpenQuestion,OpenQuestions,QuestionOwners,QuestionAssignment}.cs` | Create (`MarkdownLines` extracted from the checker) |
| `Application/Features/Artifacts/Markdown/ArtifactFormatChecker.cs` | Modify (uses `MarkdownLines`) |
| `Application/Features/Artifacts/{ArtifactView,ArtifactViewBuilder,ArtifactTexts,ArtifactValidationTexts,ArtifactGuards}.cs` | Modify |
| `Application/Features/Artifacts/ApproveArtifact/ApproveArtifactCommandHandler.cs`, `SaveArtifactContent/…Handler.cs` | Modify (gate; `SaveEditAsync`) |
| `Application/Features/Artifacts/AssignQuestionOwners/{Command,Handler,Validator}.cs` | Create |
| `Infrastructure/Artifacts/{DraftComposer,PrdLayout}.cs` | Modify |
| `Web/Components/Artifacts/{OpenQuestionsPanel.razor,ArtifactLabels.cs}`, `Pages/Artifacts/ArtifactDocument.razor` | Create / Modify |
| `tests/.../Goldens/*.md` (all four) | Regenerate (`UPDATE_GOLDENS=1`) |
| `docs/bmad-referencia.md` | Modify (new §7) |

## Testing Strategy (xUnit only)

| Layer | What | Approach |
|---|---|---|
| Markdown | Parse: canonical line, bold-colon and plain variants, fences skipped, headings skipped, line in the index, text with inner parentheses, trailing `(ver anexo)`, missing suffix, missing `Revisar`, empty fields, placeholders in either field, trailing spaces, duplicates, malformed ids ignored, `Pendiente: ver Q-1.` not a definition. Apply: only targeted lines change, other bytes identical, idempotent, current values give the same string, empty value gives the placeholder, duplicates all rewritten, unknown id throws, round trip `Parse(Apply(m,a))` | Pure theories |
| Round trip with the checker | For every golden and every regenerated draft: `Check` gives zero warnings before and after `Apply` with sample values; `Unassigned` lists every generated `Q-n` | Infrastructure tests |
| Handlers | Approve: Draft with unassigned is refused with the ids and nothing is saved; assigned is approved; Approved with unassigned, retried at the same version, still completes (CompletionPending); grandfathered Approved untouched. Assign: version +1 and Draft; unchanged is a no-op; ReadyToBuild reopens before the save; stale version conflicts; unknown id and over-length content are refused with no reopen; owner isolation. Regenerate resets owners. `ToString` is redacted | `ModulesSender`, `InMemoryArtifactRepository`, failure injection |
| Validators / view | Per-row messages, forbidden characters, limits, distinct ids; `CanApprove`, `UnassignedQuestions` | Existing validator and builder tests |
| Drafter | PRD 2.3 has one assumption and no `UJ-n` (answered, "No sé", missing); goldens | Replace the journeys test |
| Web | `ArtifactLabels` texts; Chrome walk-through: generate, approve blocked, partial save, full save, approve, conflict in two tabs keeps the typed text, ReadyToBuild reopen | Pure C# and Chrome DevTools MCP (AGENTS §5.4) |

Existing approve tests whose drafts contain "No sé" questions must assign the owners first or use answers with no unknowns.

## Docs deliverables

The identifier wording is already in `docs/bmad-referencia.md` §4 and `AGENTS.md` §6.3; only check that it stays consistent. The new `docs/bmad-referencia.md` §7 is "Verificación pendiente en una instalación real (antes del paso 6)". It covers three checks, each with how to run it (a disposable BMAD 6.12.1 install outside the repo, as in the header, with a golden copied in), the expected result, and the result:

1. `bmad-build` with `spec-small.md` as `SPEC.md` (`FR-n` instead of `CAP-N`).
2. `bmad-create-epics-and-stories` step 1 on `prd-large.md`: an inventory with `FR1:`/`NFR1:` and the same numbers.
3. Spanish headings, plus a `Q-n` line with its suffix, read without loss.

Results go in a table (Comprobación, Fecha, Versión, Resultado, Evidencia). A contradiction updates §4 and `AGENTS.md` §6.3. BMAD is not run in this change.

## Threat Matrix

N/A: there is no routing, shell, subprocess, VCS/PR automation, executable-file classification or process-integration boundary. Owner text reaches HTML only through Markdig with `DisableHtml`, and the forbidden characters keep it out of markers and the suffix grammar.

## Migration / Rollout

No migration. Existing Drafts without a suffix count as unassigned. Approved documents are untouched until their next edit.

## Commit slices (authored lines; goldens excluded)

| # | Slice | Size |
|---|---|---|
| 1 | Journeys (`PrdLayout`, drafter tests, PRD goldens) and the §7 checklist | ~120 |
| 2 | `MarkdownLines`, `OpenQuestions`, composer suffix (four goldens), view fields, approve gate, tests | ~380 |
| 3 | `QuestionOwners.Apply`, command, handler, validator, `SaveEditAsync`, tests | ~420 |
| 4 | Panel, page, labels, label tests, Chrome walk-through | ~260 |

The total is about 1,180 authored lines, more than the proposal's ~700 estimate, so `sdd-tasks` must re-forecast against the 400-line budget.

## Risks

- `Q1:` (no hyphen) bypasses the gate and the checker does not warn. This is accepted because it needs a deliberate hand edit; it is noted for step 5's validation.
- Many existing approve tests use drafts with questions and will fail until they are adjusted.
- `(Revisar: X)` written alone is not recognised, so the next save appends a full suffix after it. The text is still readable.

## Assumptions

- A1: Either placeholder in either field counts as unassigned.
- A2: The value limits are 60 (owner) and 80 (revisit) characters, and the forbidden set (same for both fields) is `( ) ; [ ] * | \` plus line breaks.
- A3: Duplicate ids are assigned together.
- A4: Partial saves are allowed; the asterisk means "needed to approve".
- A5: The panel is also shown for Approved documents, and saving it returns them to Draft.
- Assumptions confirmed by the user (2026-10-11): A1-A5 are accepted as written, and the panel is KEPT in this change.
