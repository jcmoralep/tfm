# Exploration: gemini-assistant (PROPUESTA-MVP step 5)

Date: 2026-10-13. Produced by an `sdd-explore` run (read-only; no source change, no API call, no secret read) and persisted by the orchestrator, because that run had no write tool. Spot-checked against the code: the `IAssistantService.ReplyAsync` and `AssistantReply` signatures, the advancer's `InnerException` log, `SmallClarify` and the DI registration lines match.

## Summary

Keep the script as owner of topics, coverage, quick replies and level (assistant-conversation design decision). Add a plain-`HttpClient` Gemini client on `generateContent`. Gemini phrases each question and judges the last free-text answer, returning structured JSON with a verdict that allows at most one pushback per topic. The drafter should use "the model fills a JSON schema, existing code renders the Markdown". About 6 PRs of 300 to 500 lines each.

## Current state

**Assistant seam**
- `IAssistantService.ReplyAsync(AssistantRequest, ct)`: the service only words the turn. `AssistantRequest = (InitiativeSnapshot, NextTopic, History, OffersLevelChoice)`; `AssistantReply(Text, SuggestedDepth)`. No ids, owner or timestamps travel to the model. A real implementation throws `AssistantUnavailableException`.
- `ConversationAdvancer.AdvanceAsync`: retries a pending `StartPlanning` first; calls the service only when the last visible message is not already the expected question (`IsInSync`); re-reads the conversation before the model call, so a second tab spends nothing; stores the reply under `next.Key` with quick replies composed by code; keeps the stored reply on a version race; logs only topic and cause type.
- The user's answer is stored before the reply. If the model fails, the conversation keeps a pending reply and the UI offers "Reintentar" (resumes through `StartConversation` and the advancer). The `SendQueue` pauses and resumes.
- Quick replies and "No sé" are script data. Each Question topic ends with `Unknown`; `unknown` or a typed "no sé" maps to `AnswerKind.Unknown` (typed form only on Question topics).
- The journey is a pure function of status, mode, level and visible user answers. A topic is covered by any visible answer with its key; a Choice topic only by a quick reply; the depth proposal never by history; `confirm-planning` needs the explicit "Sí, pasar a Planificar" (RF-45).
- `FakeAssistantService` is pure and deterministic (acknowledgement, prompt, `Por ejemplo:`, `Por qué lo pregunto:`), never praises, and maps the size answer to a level.
- Limits: user answer 2,000 characters; assistant message 4,000; topic and quick-reply keys 50; document 20,000. `ChatBubble` renders plain text with `whitespace-pre-line`, so model output must be plain text.
- DI registers both fakes as singletons. Infrastructure already references `Microsoft.AspNetCore.App`, so `AddHttpClient` needs no new package. `ArtifactDrafterRegistrationTests` builds `AddInfrastructure` with only a connection string and expects the fake: a missing key must default to the fake.
- Gaps: `AssistantUnavailableException` has no inner-exception constructor although the advancer logs `InnerException`; its default text says "Su mensaje quedó guardado", wrong for the drafter; the circuit passes no cancellation token, so only the HTTP timeout stops a hung call.

**Drafter seam**
- `IArtifactDrafter.DraftAsync(DraftRequest, ct)`; `DraftRequest(Type, InitiativeName, Description, Depth, Answers)`. The result must be non-blank, at most 20,000 characters, use the `ArtifactOutline` ids and have no frontmatter. On failure it throws `AssistantUnavailableException` and nothing is stored.
- The catalog relies on `ConversationAnswers(ClarifyComplete, PlanComplete, Answers)`: the Brief needs `ClarifyComplete`; Spec and PRD need `PlanComplete`. `TopicAnswer.Question` is the script prompt, not the model's wording. `IsUnknown` and `IsNone` are set in code.
- `DraftComposer` guarantees what a model should not reimplement: headings from `ArtifactOutline`, sequential ids from `ItemNumberer`, `[ASSUMPTION: A-n]` plus the index, `Q-n` with the `(Responsable: por asignar; Revisar: por definir)` suffix, a maximum of 20 items per answer, `#` escaping. Goldens are byte-exact.

**What a real model must preserve:** topic keys (the app stores every reply under `next.Key`; the model never emits keys; a reply asking the wrong thing is not detectable by code), the script prompt as the document question, code-composed quick replies with "No sé" on every Question topic, a valid `SuggestedDepth` only on the depth proposal, text within 4,000 characters, no jargon (RF-41) and no praise (RF-35) (only deny-list checkable), `unknown`/`none` semantics in code, and the explicit confirmation as a button. **Free to change:** wording, examples, reasons, acknowledgement and (if the journey allows) pushback follow-ups.

## BMAD behaviour to copy (BMAD 6.12.1 source) mapped to RF-65/RF-66

| BMAD rule (source) | RF | Where to enforce |
|---|---|---|
| One question at a time, in dependency order (`bmad-forge-idea`) | 65a | The script owns order. Separate schema fields (`acknowledgement`, `question`, `example`, `why`) assembled by code in the Fake's layout. |
| Include your current best answer or hypothesis (forge-idea) | 65b | Prompt; `example` required and tailored. |
| Praise is noise (forge-idea) | 65c | Prompt plus a live evaluation; code can only deny-list. |
| Coach, do not quiz; push back when an answer is thin (`bmad-product-brief`) | 65d | `verdict` enum; code decides eligibility and the cap. |
| If the material contradicts the claim, stop and resolve (forge-idea) | 65e | Verdict `needs_more` with a contradiction reason; shares the follow-up cap. |
| Do not invent user detail, metrics or scope (`bmad-prd` headless; `bmad-spec`) | 65f | Drafter items carry `source = answer / assumption / unknown`; code tags assumptions and builds `Q-n`. |
| Gaps become open questions (spec) | 65g | Already in code. |
| Brain dump then "anything else?" (product-brief; prd) | RF-12 | `idea` is the dump; "¿algo más?" is a sufficiency follow-up on `idea`. |
| Stakes calibration (prd) | RF-12, RF-41 | Not in the script (deliberate deviation); later change. |
| Fast vs coaching (product-brief; prd) | RF-12, RF-48 | Needs journey changes; deferred. |
| Triage: defer with owner and revisit condition (prd) | RF-61 | Already built (`bmad-alignment`). |
| Sufficiency minima (`prd-validation-checklist`) | RF-66 | Per-topic `SufficiencyHint` as script data; the model applies it; minima for topics the script never asks become an assumption or `Q-n`. |

## Gemini integration (verified against ai.google.dev on 2026-10-13)

- `POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`, key in the `x-goog-api-key` header. Google labels `generateContent` "legacy" but "fully supported", no sunset date; the Interactions API is recommended for new projects but is stateful by default (`store=true`), has no custom safety settings and had breaking schema changes in May 2026. Recommendation: `generateContent` (the platform already owns history, RF-13).
- System prompt in top-level `systemInstruction`. Multi-turn via `contents` with alternating `user`/`model` roles.
- Structured output: JSON in schema key order; semantics not guaranteed; large schemas may be rejected; unsupported keywords ignored.
- Thinking: `generationConfig.thinkingConfig.thinkingLevel`; `gemini-3.8-flash` supports `low`, `medium` (default), `high`; `minimal` errors; thinking cannot be disabled; `thinkingBudget` is for the 2.5 series. `maxOutputTokens` includes thinking tokens (`MAX_TOKENS` with empty output is possible); lower the level instead of capping tokens. The roughly 100 thinking tokens on a one-word answer are the default `medium`. Make it configuration (`Gemini:ThinkingLevel`, default `low`).
- Gemini 3.6+ ignores custom `temperature`, `top_p`, `top_k`; do not send them. Do not set `safetySettings`.
- Blocked prompt: `promptFeedback.blockReason`; blocked response: `finishReason` SAFETY; only `STOP` is success.
- Errors: 429 and 503 transient; 400, 401, 403, 404 not retryable (404 = model not found). Retry only 429/408/5xx with backoff and jitter. Free-tier limits are per project, visible only in AI Studio, daily reset at Pacific midnight.
- Models: `gemini-3.8-flash` stable (1,048,576 input / 65,536 output tokens, structured output). `gemini-2.5-flash` only for users who already used it. Free-tier status of 3.8 is not documented; it works per the owner's test.
- **HttpClient versus SDK.** `Google.GenAI` 1.26.0: Apache-2.0, net8.0 and netstandard2.0, with Google.Apis.Auth and Microsoft.Extensions.AI.Abstractions dependencies; thinking-config support, timeout/retry hooks and DLL size not confirmed. Recommended: plain `HttpClient` with typed DTOs (no new package, full control of timeout, redaction and schema shape, stub `HttpMessageHandler` tests, about 150 lines of DTOs).
- **Configuration:** `Gemini:ApiKey` (user-secrets / env `Gemini__ApiKey`), `Gemini:Model` (default `gemini-3.8-flash`), `Gemini:BaseUrl`, `Gemini:ThinkingLevel`, `Gemini:TurnTimeoutSeconds` (~30), `Gemini:DraftTimeoutSeconds` (~90), `Gemini:MaxConcurrentCalls` (~2). Plus `Assistant:Provider=Fake|Gemini`, default `Fake` (mirrors `Storage:Provider`); Provider=Gemini with an empty key fails at startup naming `Gemini__ApiKey`, never its value. Shared client in `Infrastructure/Gemini/` (the module-boundary scan blocks Assistant code from mentioning `Artifact` and the reverse).
- **Not verified (no live call allowed):** whether `generateContent` expects `responseFormat` or `responseMimeType` + `responseJsonSchema`; whether the first `contents` turn must be `user` (avoid it with one `user` content holding a delimited transcript); the 2026 error JSON (parse on HTTP status); real latency, tokens per turn and free-tier numbers (a forum report mentions about 20 requests per day: unconfirmed); whether `IHttpClientFactory` redacts header values (header auth keeps the key out of the URL anyway). One probe call early in apply settles the first two.

## Design hinge

| Option | Description | Testability | Injection | Failure modes |
|---|---|---|---|---|
| A. Model drives | JSON gives next message, covered topics, quick replies | Poor (goldens, `ClarifyComplete`, level changes non-deterministic) | User text can skip topics | Drift, loops; already rejected by assistant-conversation |
| **B. Script owns topics; model phrases and judges sufficiency (recommended)** | One call per turn returns `{verdict, acknowledgement, question, example, why, suggestedDepth?}`; code assembles the text in the Fake's layout | Good: Fake unchanged; stub handler and `ScriptedAssistantService` cover it | Bounded: the model cannot change topics, level or confirmation | Transport failure keeps today's contract; malformed or blocked output falls back to script wording for that turn |
| C. Model only phrases | B without the verdict | Best | Minimal | Fails RF-65d, e and RF-66; valid as a first slice of B |

**How B stays pure.** Coverage becomes: a Question topic is covered iff the last visible message with that key is a user answer (only `ConversationJourney.IsCovered` changes). A follow-up is an assistant message with the same topic key: the topic becomes uncovered, `NextTopic` is the same key, `IsInSync` stays true. Undo already hides the answer and everything after it. Cap: one follow-up per topic, derived by counting visible assistant messages with that key; Unknown and quick-reply answers are never eligible. `AssistantReply` gains `Verdict` (`Proceed` / `NeedsMore`, default `Proceed`); the advancer stores the text under the answered key on `NeedsMore`, otherwise under `next.Key`; the Fake always returns `Proceed`. Side effect: progress ("3 de 5") dips by one while a follow-up is pending.

**Drafter.** D1 model writes all Markdown (ids, index and outline unreliable; cap and goldens hard). **D2 (recommended): the model returns JSON items per section with `source = answer|assumption|unknown`; `DraftComposer` and the layouts render it** (outline, ids, `A-n` index, `Q-n` suffix, cap and zero-warning guarantees retained; no Fake fallback for documents: it would be silent low-quality output). D3 keep the fake drafter in this change.

## Resilience

- Transport failures (429, 5xx, timeout, 400/401/403/404): throw `AssistantUnavailableException` with a Spanish message; the answer is already stored, "Reintentar" resumes, no data loss. Add a reason enum (Quota, Auth, Model, Timeout, Blocked, Malformed). 429: no automatic retry on the free tier, with a quota-specific message. 500/503/504: one retry after 1 to 2 seconds with jitter. Caller cancellation propagates.
- Blocked content: the assistant falls back to script wording for that turn and says the person can undo the last answer and rephrase; the drafter throws with that guidance. Malformed output: one parse failure falls back to script wording for assistant turns; drafter throws with a document-specific message.
- `ReplyGuard` (deterministic): non-empty fields, total at most 4,000 characters, no forbidden jargon (spine, épica, invariante, slug), `SuggestedDepth` in the enum, Markdown stripped.
- Missing key: Provider=Fake works with no key; Provider=Gemini with no key fails at startup; tests never need a key.
- Concurrency: singleton `SemaphoreSlim(MaxConcurrentCalls)`; the version re-check already avoids duplicate calls from two tabs.
- Logging: only initiative id, topic key, model name, HTTP status, duration, `finishReason` and token counts; never message or document text, the response body or the key.
- Prompt injection: user text only inside the `user` content as a delimited transcript; system prompt in `systemInstruction`; no tools, grounding or URL context; schema-constrained output rendered as plain text. Worst case: a wasted or skipped pushback (capped).

## Open decisions for the product owner (one at a time, each with a recommendation)

1. **Pequeña question count.** The script has 6 (idea, users, problem, capabilities, out-of-scope, success); RF-42 says 3; RF-46 says "unas 6". Recommend keeping 6 (BMAD's guided walk of the five spec fields) and rewording RF-42 to "at least those three", with pushback on a thin `idea`.
2. **PRD inherits the approved Brief.** Recommend yes (`DraftRequest.BriefMarkdown?`, Standard only; slice S4b).
3. **Fast mode now.** Recommend no; put it with stakes calibration in a later change.
4. **Mixed stakes.** Moot unless stakes calibration is introduced; if it is, take the higher level.
5. **Drafter in this change.** Recommend yes, in later slices.
6. **Quota or timeout.** Block with "Reintentar" (recommended) or silently degrade to script wording?
7. **New script topics** (real journey, non-users, glossary, differentiator; RF-64). Recommend a separate change (moves counts, RF-46 numbers, topic names and goldens).
8. **Provider switch.** One `Assistant:Provider` for both (recommended) or two.
9. **Prompt language.** English instructions with Spanish output rules and "usted" (recommended) or a Spanish prompt.

## Slices and size (authored lines, tests included)

| PR | Slice | Est. |
|---|---|---|
| S1 | `Infrastructure/Gemini`: options, validation, typed client, DTOs, error taxonomy, concurrency guard, DI switch, exception reason code, stub-handler tests | 450 to 500 |
| S2 | `GeminiAssistantService`: prompt, schema, assembly in the Fake's layout, `ReplyGuard`, `SufficiencyHint`, model skipped for non-Question turns | 350 to 400 |
| S3 | Pushback: derived `IsCovered`, `Verdict`, cap, advancer branch, scripted double and tests | 300 to 400 |
| S4a | `GeminiArtifactDrafter` for Spec and Brief (JSON items, layout rendering) | 400 to 500 |
| S4b | PRD drafter plus Brief inheritance | 350 to 450 |
| S5 | README and AGENTS 4.8, docs, manual evaluation script with persona answers, Chrome walkthrough with a real key (outside CI) | 100 |

Total about 2,000 to 2,300 lines. S1 to S3 sequential; S4 depends only on S1. Under the accepted `exception-ok` if combined.

## Risks

- The free-tier daily quota may cover only a few full sessions per day (a Standard initiative is roughly 10 to 19 assistant calls plus 2 documents). Mitigation: one call per turn, no model on Choice, Confirmation and Closing turns, no retry on 429.
- `generateContent` is labelled legacy and the schema is moving. Mitigation: one probe call in apply, keep the HTTP shape in one class.
- A reply can pass the guard yet ask the wrong question; only prompt quality and the manual evaluation can catch it.
- Progress dips during a pushback; undo of the original answer interacts with the follow-up (hidden together; tested in S3).
- `AssistantUnavailableException` needs a reason code and the drafter its own default message: a small Application change precedes S1.
- REQUISITOS RF-42, RF-46, RF-48 and AGENTS 4.8 need small amendments when the decisions land.

## Ready for proposal

Yes. Ask decisions 1 to 5 first, one at a time. Run the probe call early in apply to settle the structured-output shape and the first-turn role question.
