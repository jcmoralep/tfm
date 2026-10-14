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
