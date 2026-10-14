# Apply progress: gemini-assistant

## S1a: Reason codes and the provider switch (done, 11/11 tasks)

Commits on `feat/gemini-assistant`:
1. `feat(assistant): add reason codes to the unavailable error and reason-specific messages`
2. `feat(infrastructure): add the Assistant:Provider switch, Gemini options and startup validation`

Deviations from design.md / tasks.md:
- `AssistantProvider.Read` is `AssistantProviderSetting.Read` (an enum cannot hold a static method).
- Selecting `Gemini` with valid options registers the options and the named HTTP client, then throws a clear
  startup error ("the Gemini client is not implemented yet") so nothing falls back silently to the Fake. The
  throw is removed when S2/S4a register the Gemini services. Tests cover it.
- `ConversationAdvancer.ReplyAsync` rethrows `AssistantUnavailableException` with `AssistantTexts.UnavailableFor(reason)`
  (inner = original), so the chat text is reason-specific regardless of the service message.
- Added `FailureReason` to `ScriptedAssistantService` for the Quota/Auth/Model Theory.
- Gate run: Release build is blocked by a `dotnet` process (PID 68972, `BmadPlatform.Web.dll --urls http://localhost:5080`)
  that locks `src/BmadPlatform.Web/bin/Release`; the full build and tests were run in Debug instead.

Gate results (Debug): build 0 code warnings (only NU1507); tests: Application 681, Infrastructure 168, Web 196,
Architecture 24, all passing. `rg "ApiKey" src --glob "*.json"` finds nothing. No view touched.

## S1b: The Gemini client (done, 8/8 tasks) on `feat/gemini-s1b-client`

Commits: probe record (docs), client + request/response contract, resilience tests, logging/concurrency tests.

Probe (docs/gemini-sonda.md, 8 requests): the primary structured-output shape (`responseMimeType` +
`responseJsonSchema`) and a single `user` content are accepted; `thinkingLevel` is camelCase, `low` confirmed.
Turn call 38 s under high demand (tokens 125/117, no thoughts count); draft-sized call not measured (two 503, one
90 s timeout). 404 and 400 error shapes recorded; an invalid key returns 400 `API_KEY_INVALID`. No rate-limit
headers. Design Open Question 5 closed.

Deviations:
- All client source landed in the first code commit (the client is one file); the next two commits carry the
  resilience and the concurrency/logging tests that prove behavior already present.
- `GeminiCall` has an optional `InitiativeId` (log tag only, never sent) next to `Purpose`.
- Default timeouts (30/90 s) kept despite the single 38 s sample taken under load; re-measure in task 5.6.
- `GeminiClient` is registered as a singleton in `AddGeminiClient`; the startup stop under Provider=Gemini stays,
  with the message updated ("client exists, services arrive in the next slices").

Gate (Release, no key, no network): build 0 code warnings; tests Application 681, Infrastructure 211, Web 196,
Architecture 24, all passing.

## S2: Model wording behind the assistant seam (7/8 tasks; 2.G waits for the Chrome item) on `feat/gemini-s2-assistant`

Commits: `TurnLayout` extraction (Fake goldens and tests unchanged and green), `ReplyGuard`, then the service, prompt,
script hints and DI.

Done: `TurnLayout`, `TurnReply`, `ReplyGuard`, `AssistantPrompt`, `GeminiAssistantService`,
`AddGeminiAssistant` (registration helper), `ReplyVerdict` + `AssistantReply.Verdict` (default Proceed),
`AssistantTopic.SufficiencyHint` with a hint on every Question topic (priorities included, RF-66 lists none for it).

Deviations from design.md / tasks.md:
- `ReplyVerdict` and `AssistantReply.Verdict` landed in the guard commit (the guard returns the parsed verdict); the
  hints landed with the service commit, as planned.
- `ReplyGuard.Check(reply, answerPrecedes, followUpAllowed, ownedAcknowledgement)` takes the eligibility as a flag
  instead of reading `AssistantRequest.FollowUpFor`, which does not exist until S3. S2 always passes
  `followUpAllowed: false`, so any NeedsMore is rejected and falls back (matches the "ineligible NeedsMore" scenario).
  S3 only changes that argument.
- The turn schema omits `suggestedDepth`: the model is not invited to suggest a level. A `suggestedDepth` that arrives
  anyway is still parsed, checked by the guard (valid or absent) and never applied.
- `GeminiAssistantService(GeminiClient, ILogger)`: no `IOptions<GeminiOptions>` (nothing in the service needs it).
- The system prompt states `verdict` is always `proceed` in this version, and the user content has no
  `<answered_topic>` block; S3 adds both (task 3.6).
- Startup stop under `Assistant:Provider=Gemini` stays until S4a, because the one switch selects both seams and the
  drafter is not there yet (no silent Fake drafter). The Gemini assistant is registered through `AddGeminiAssistant`
  before the stop; the stop message and its test are updated. Consequence: the app cannot run under Gemini until S4a,
  so the optional live smoke turn of 2.G is not possible yet.
- Owned acknowledgement: code keeps the Fake's "No pasa nada..." text on a "No sé" answer and an empty one on the
  opening turn, via `FakeAssistantService.Acknowledge` (now internal).
- Extra user-content hardening: every user string is neutralized AND collapsed to one line, so a user cannot fake a
  `[assistant|...]` transcript line.
- Added one Application test (hostile answer plus a model-suggested Large cannot change topic, level or mode, with the
  scripted double); script tests for hints and absent topics.

Pending for 2.G: Chrome item C-1 (Fake regression), which needs MySQL via docker compose; not run by this executor.

## S3: One pushback per topic (branch feat/gemini-s3-pushback)

Tasks 3.1 to 3.G done. Tests: Application 709, Infrastructure 259, Web 196, Architecture 24 (all green, no key).

- `ConversationJourney.Compute` passes all visible messages (ordered by `Sequence`) to coverage. Question topics are
  covered iff the last visible message with their key is a user answer; Choice keeps "quick reply only"; the
  other kinds keep "any visible user answer". `MaxFollowUpsPerTopic = 1` and `FollowUpCandidate` added (last
  visible message is a FreeText answer on a Question of the current required list, visible assistant messages with
  the key <= 1; "No sé", typed phrase and quick replies are `AnswerKind` Unknown/QuickReply, never eligible).
- Progress dip documented in a comment where the count is computed; the text is unchanged.
- `AssistantRequest.FollowUpFor`; the advancer stores an eligible `NeedsMore` under the answered key with that
  topic's quick replies, anything else under `next.Key`; an ineligible `NeedsMore` is `Proceed` with no second call.
- `GetConversationAnswersQueryHandler.AnswersOf` joins the visible FreeText answers of a topic with `\n`; the last
  answer still decides `IsUnknown`, and a last "No sé" keeps its own text (existing behavior).
- Gemini: `<answered_topic>` block (key, prompt, sufficiency, `follow_up_allowed`), VERDICT section in the system
  prompt, service returns the guard's verdict, `ReplyGuard.Check` gets the real `followUpAllowed`.
- Undo needed no change (it hides the answer and every later message). Existing tests needed no edits.
- Tests: `PushbackTests` (pipeline with the scripted double and its new `Verdicts` queue), journey Theories,
  joined-answers Theory, two Gemini service facts.
- Pending: Chrome item C-6 (live) in the final walkthrough.
