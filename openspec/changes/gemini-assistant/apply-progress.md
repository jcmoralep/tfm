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

## S4a: Drafter for Spec and Brief (branch feat/gemini-s4a-drafter, size:exception)

Tasks 4a.1 to 4a.G done. Tests: Application 717, Infrastructure 286, Web 196, Architecture 24 (all green, no key,
no network). Release build: 0 code warnings (NU1507 only).

- 4a.1 proof: before and after the refactor `ArtifactGoldenTests` + `FakeArtifactDrafterTests` ran green with no edit
  (61 tests; the full Infrastructure suite 259 before the new tests).
- `DraftRenderer` (budget loop moved out of the Fake), `DraftItem`/`DraftItems`, composer `items`, `Open`,
  `OpenQuestion`, `ModelText`. Spec/Brief layouts call `Open` for constraints, differentiator, scope-inside, vision.
- `DraftSlots` (Spec 7 slots, Brief 8; differentiator AllowsAnswer=false), `DraftPrompt` (English system text,
  Spanish "usted" output, answers inside one delimited block, neutralized and collapsed), `GeminiArtifactDrafter`
  (`GeminiClient` Draft kind, Malformed on unknown slot/source, no Fake fallback), `ArtifactTexts.Draft*`.
- PRD delegates to the `FakeArtifactDrafter` with the comment `S4b replaces this` and a test pinning it.
- DI: the temporary startup stop is gone; Gemini registers the client, assistant and drafter (`AddGeminiDrafter`).
- Safeguards in code: "No sé"/"nothing" topics ignore model items (Q-n by code); an `answer` item with no answer
  from the person or in an AllowsAnswer=false slot becomes an assumption; empty slot with an answer quotes it, with
  none takes the default assumption (equal to the Fake's text); forged ids/markers stripped; 20 items then "y N más.";
  per-slot budget loop keeps the 20,000 cap.
- Tests: `GeminiArtifactDrafterTests` (27 cases), registration (Gemini resolves both seams, Fake resolves the golden
  Spec), failure Theories through `GenerateArtifact`/`RegenerateArtifact` with reasoned drafter failures.
- Rollback boundary: revert the three commits; the Fake drafter stays the default.
- Pending: Chrome item C-5 for a document failure (live), the PRD drafter (S4b).

## S4b: PRD drafter and Brief inheritance (branch feat/gemini-s4b-prd, size:exception)

Tasks 4b.1 to 4b.G done. Tests: Application 723, Infrastructure 300, Web 196, Architecture 24 (all green, no key,
no network). Release build: 0 code warnings (NU1507 only).

- `DraftRequest.BriefMarkdown?`; `ArtifactGuards.ApprovedBriefAsync` (PRD + Standard + owner's Brief `WasApproved`,
  else null; read through the owner-scoped repository, so another user's Brief reads as missing). Generate and
  regenerate pass it; regenerate reads the Brief as approved at that moment.
- `DraftSlots.For(type, depth)`: PRD slots (idea, users, problem, non-users, glossary, capabilities, constraints,
  [Large: qualities, integrations], out-of-scope, priorities, success). `non-users` and `glossary` AllowsAnswer=false.
  A slot the level lacks (qualities in a Standard PRD) is an unknown key, so Malformed.
- `GeminiArtifactDrafter` drafts the PRD; the Fake delegation and its pinning test are gone (replaced by PRD tests).
- `DraftPrompt`: `<brief>` block (neutralized, clipped to the document cap) and a BRIEF rules block added to the system
  text only when the request carries a Brief; Spec and Brief prompts are unchanged.
- `BriefFacts` (heuristic, documented): a PRD `answer` item whose normalized text equals (or, with at least four words,
  contains / is contained in) a Brief `A-n` text is downgraded to an assumption; Brief `Q-n` texts the model did not
  restate are carried (max 10) through `DraftItems.CarriedQuestions` and `DraftComposer.CarryBriefQuestions`, which
  skips a question already registered (for example the one a "No sé" forces) and writes new `Q-n` with the default suffix.
- With a Brief in the request an `answer` item for a topic the person did not answer is kept (the Brief states it).
- `PrdLayout`: non-users and glossary go through `Open`; the Fake output is byte-identical (goldens untouched).
- Tests: handler Theory + foreign Brief + regenerate fact; Fake ignores the Brief; PRD Standard/Large zero-warning and
  empty-slot-equals-Fake cases added to the existing Theories; prompt with/without Brief, repeat downgrade, carried
  question (omitted/restated), "No sé" on constraints, Recorridos (one assumption, no UJ-n), failures through a PRD
  request, no Brief text in logs.
- Rollback boundary: revert the two code commits; the Fake drafter stays the default.
- Pending: Chrome item C-4 (live) in the final walkthrough.

## S5-A: documentation and requirement amendments (2026-10-13)

Branch `feat/gemini-s5-docs-walkthrough`. Documents only; no source or test file touched, no build, no network, no key.

- 5.1 `README.md`: status line (step 5 delivered once S5 merges), the Fake/Gemini section, the key via user-secrets or `Gemini__ApiKey` (no real key), the options table with defaults (model `gemini-3.6-flash`, 60 and 120 s), behavior on errors, the privacy statement; the artifacts bullet no longer says Gemini "will come".
- 5.2 `AGENTS.md` 4.8: the switch, the model and why (latency table, owner decision 2026-10-13), the key rules, the free-tier warning kept as documentation, model is configuration, content-free logs.
- 5.3 `REQUISITOS.md`: RF-68..RF-72 added as `[CONFIRMADO 2026-10-13]`; RF-42, RF-46, RF-48 amended; RN-05, RNF-02, RNF-04 cross-referenced.
- 5.4 `docs/mejoras-posteriores.md`: fast mode plus stakes calibration (mixed-stakes rule), new script topics (RF-64), model-suggested level, separate models for turns and drafts, Interactions API. The judge-only last-question row already existed and was not duplicated.
- 5.5 committed (955046f). Also `docs/gemini-sonda.md` got the 2026-10-13 model comparison (3.8 Flash 58 s, 3.5 Flash-Lite 72 s, 3.6 Flash 2.0 s, 3.1 Flash-Lite 1.3 s).
- 5.6 (partial): `docs/gemini-evaluacion.md` created with three personas, per-persona checks and an empty results table (a841713). Filename follows the caller, not the older `evaluacion-gemini.md` in the task text; the task path was corrected. The LIVE run stays open.
- Left open for the orchestrator: 5.6 run, 5.7, 5.8, 5.G, C-1..C-8, F.*.
