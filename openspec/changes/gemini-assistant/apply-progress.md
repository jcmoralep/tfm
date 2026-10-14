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
