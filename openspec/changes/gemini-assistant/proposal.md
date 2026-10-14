# Proposal: Gemini behind IAssistantService and IArtifactDrafter (PROPUESTA-MVP step 5)

## Intent

The assistant and the drafter are deterministic fakes. They word every turn the same way, never push back on a thin answer and fill documents with fixed wording. Because of that, RF-65 (conduct rules) and RF-66 (sufficiency) cannot be met, and the Brief, Spec and PRD only repeat what the person typed. Step 5 connects Gemini (free tier, RF-15) behind the existing seams. The script keeps ownership of topics, order, quick replies, level and confirmation (architecture B in `exploration.md`). The model words each question, judges the last answer and fills a document schema that the existing code renders. Success: a non-technical person gets tailored examples, at most one well-founded pushback per topic, and documents built from their answers that never invent anything.

## Scope

### In Scope
- `Infrastructure/Gemini/`: options, startup validation, a typed plain-`HttpClient` client on `generateContent`, DTOs, an error taxonomy, a concurrency guard and stub-handler tests. No new package.
- `Assistant:Provider=Fake|Gemini` (default `Fake`) selects both the assistant and the drafter. `Gemini` with an empty key fails at startup, naming `Gemini__ApiKey` and never its value.
- `GeminiAssistantService`: English prompt with Spanish output in the "usted" register. The JSON reply is `{verdict, acknowledgement, question, example, why, suggestedDepth?}` and code assembles it in the Fake's layout. Also `ReplyGuard`, per-topic `SufficiencyHint`, and no model call on Choice, Confirmation or Closing turns.
- Pushback: coverage comes from the last visible message per topic key. `Verdict` is `Proceed|NeedsMore`, with at most one follow-up per topic. "No sé" and quick-reply answers are never eligible.
- `GeminiArtifactDrafter` (D2): JSON items per section with `source = answer|assumption|unknown`. `DraftComposer` renders them, which keeps the outline, ids, the `A-n` index, the `Q-n` suffix and the goldens. Covers Spec, Brief and PRD. The PRD receives the approved Brief text (Standard only, RF-38).
- Resilience: 429, timeouts and other transport failures throw `AssistantUnavailableException` with a reason code. The UI then shows "Reintentar" and the typed answer stays saved. Blocked or malformed output falls back to the script wording for that turn (assistant), or throws with guidance (drafter).
- Logs record only ids, topic keys, model, status, duration, `finishReason` and token counts.
- Docs: README, AGENTS §4.8, REQUISITOS amendments.

### Out of Scope (later changes)
- Fast mode and stakes calibration (RF-12, RF-48, mixed-stakes rule). These form one later change, recorded in `docs/mejoras-posteriores.md`.
- New script topics: the named-person journey, non-users, glossary, differentiator and RF-64.
- Attachments sent to the model (RF-14, RF-24), the Architecture and Epics drafters (step 6), the Interactions API, an SDK, a paid plan, and a privacy notice in the UI (RN-05).

## Capabilities

### New Capabilities
- `gemini-client`: the HTTP contract, timeouts, the error taxonomy, the retry policy (one retry on 5xx, none on 429), concurrency, and logging redaction.
- `assistant-provider-configuration`: the provider switch, the `Gemini:*` options and startup validation.

### Modified Capabilities
- `assistant-conversation`: model wording, verdict and pushback cap, derived coverage, and fallback on blocked or malformed output.
- `artifacts`: model-backed drafting via schema items, `source` semantics, Brief inheritance for the PRD, and the drafter's failure messages.

Prerequisite: these two specs exist only in the unarchived `assistant-conversation`, `artifact-generation` and `bmad-alignment` changes. Write the deltas against those specs.

## Approach (slices; budget 400, `exception-ok` accepted)

| PR | Slice | Est. lines |
|---|---|---|
| S1 | Exception reason code, Gemini client, options, DI switch, tests | ~450 |
| S2 | `GeminiAssistantService`, prompt, schema, guard, hints | ~380 |
| S3 | Pushback: `IsCovered`, `Verdict`, cap, advancer branch, tests | ~350 |
| S4a | Drafter for Spec and Brief | ~450 |
| S4b | PRD drafter and Brief inheritance | ~350 |
| S5 | Docs, evaluation script, Chrome walkthrough | ~120 |

Total ~2,100 lines. S1 to S3 run in sequence; S4 depends only on S1.

## Requirement changes (intentions)

- New RF-68: the provider switch and startup validation.
- New RF-69: one pushback per topic; "No sé" and quick-reply answers are exempt.
- New RF-70: quota or timeout blocks with "Reintentar"; blocked or malformed output falls back.
- New RF-71: drafter items carry a source and never invent (RF-65f).
- New RF-72: the PRD builds on the approved Brief.
- RF-42: "at least those three" within Pequeña's 6 questions, with pushback on a thin `idea`.
- RF-46: confirm 6 questions for Pequeña.
- RF-48: guided only until the fast-mode change, no longer tied to Gemini.
- RN-05, RNF-04 and RNF-02: cross-reference the switch and the logging rule.
- AGENTS §4.8: the switch, the model, the key and the free-tier warning.

## Affected Areas

| Area | Impact |
|---|---|
| `src/BmadPlatform.Infrastructure/Gemini/` | New |
| `src/BmadPlatform.Infrastructure/{Assistant,Artifacts}/`, `DependencyInjection` | Modified |
| `src/BmadPlatform.Application/Features/Assistant/` (`AssistantReply`, `ConversationJourney`, `ConversationAdvancer`, exception) | Modified |
| `src/BmadPlatform.Application/Features/Artifacts/` (`DraftRequest`) | Modified |
| `appsettings*.json`, README, AGENTS.md, REQUISITOS.md, `docs/mejoras-posteriores.md` | Modified |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Free-tier daily quota covers few sessions | High | One call per turn, no model on non-Question turns, no 429 retry, clear message |
| `generateContent` is labelled legacy and its schema is moving | Med | One HTTP class; a probe call in apply |
| A reply passes the guard but asks the wrong question | Med | Prompt quality plus a manual persona evaluation |
| Progress ("3 de 5") dips during a pushback; undo interacts with the follow-up | Med | Tested in S3; one follow-up cap |

## Rollback Plan

Set `Assistant:Provider=Fake`. This needs no migration, and the stored Markdown and messages stay valid. Revert the slice PRs if needed.

## Success Criteria

- [ ] Stub-handler tests cover success, 429, 5xx retry, timeout, blocked, malformed and the missing key. The build and lean tests pass without a key.
- [ ] One live probe call in apply settles the structured-output fields and the first-turn role.
- [ ] Manual evaluation with persona answers: tailored examples, no praise, no jargon, pushback only on thin answers, documents with no invented data.
- [ ] Chrome walkthrough with the real key (outside CI): Pequeña and Standard end to end, "Reintentar" after a forced failure, and logs free of content.
- [ ] Views use CSS classes only; no AI attribution.

## Proposal question round

Product-owner decisions 1 to 9 are final. Residual assumptions to review:
- If the Brief is not approved, the PRD drafts from the answers alone.
- The follow-up cap is one per topic, including contradictions (RF-65e).
- `Gemini:ThinkingLevel=low` is enough for verdict quality.
