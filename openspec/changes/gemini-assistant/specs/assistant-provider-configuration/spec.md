# Assistant Provider Configuration Specification

## Purpose

One configuration switch decides whether the assistant and the drafter are the deterministic fakes or Gemini, and the options that govern the Gemini client. New capability (full spec). Source requirements: RF-15, RF-68 (new), RN-05, RNF-02, RNF-04, AGENTS.md section 4.7 pattern (`Storage:Provider`) and section 4.8.

Scenario tags: **[UNIT]** xUnit (options binding, validation and DI resolution with hand-written doubles; no network, no key). **[DOC]** documentation deliverable checked by reading the file. **[MANUAL]** needs the live key; outside CI. Message wording is a proposal unless marked verbatim.

## ADDED Requirements

### Requirement: Provider switch for assistant and drafter (RF-68, RF-15)

`Assistant:Provider` MUST accept `Fake` or `Gemini` (case-insensitive) and MUST select BOTH `IAssistantService` and `IArtifactDrafter`; there is no separate switch for the drafter. When the section or the value is absent or empty, the provider MUST be `Fake`. Any other value MUST fail at startup with a message that names `Assistant:Provider` and lists the valid values. With `Fake` the application MUST behave exactly as before this change (same replies, same goldens, no network access, no Gemini options validated or read).

#### Scenario: Default is Fake for both [UNIT]
- GIVEN a configuration with only a connection string
- WHEN the services are resolved
- THEN `IAssistantService` is the fake assistant and `IArtifactDrafter` is the fake drafter

#### Scenario: Gemini selects both [UNIT]
- GIVEN `Assistant:Provider` = "Gemini" and a non-empty `Gemini:ApiKey`
- WHEN the services are resolved
- THEN `IAssistantService` and `IArtifactDrafter` are the Gemini implementations

#### Scenario: Value is case-insensitive [UNIT]
- GIVEN `Assistant:Provider` = "gemini"
- WHEN the options are bound
- THEN the provider is Gemini

#### Scenario: Unknown value fails at startup [UNIT]
- GIVEN `Assistant:Provider` = "OpenAI"
- WHEN the application is built
- THEN startup fails with a message naming `Assistant:Provider` and the values Fake and Gemini

#### Scenario: Fake needs nothing from Gemini [UNIT]
- GIVEN `Assistant:Provider` = "Fake" and an invalid `Gemini:ThinkingLevel`
- WHEN the application is built
- THEN startup succeeds

#### Scenario: Switching back restores the fakes [UNIT]
- GIVEN a database with conversations and documents created under Gemini
- WHEN `Assistant:Provider` is set to Fake and a conversation is resumed and a document read
- THEN both work with the stored messages and Markdown, and no migration is needed

### Requirement: Gemini options and defaults (RF-68, RF-70)

The options under `Gemini:` MUST be: `ApiKey`, `Model`, `BaseUrl`, `ThinkingLevel`, `TurnTimeoutSeconds`, `DraftTimeoutSeconds` and `MaxConcurrentCalls`. Defaults when absent: `Model` = `gemini-3.8-flash`; `BaseUrl` = `https://generativelanguage.googleapis.com/v1beta`; `ThinkingLevel` = `low`; `TurnTimeoutSeconds` = 60; `DraftTimeoutSeconds` = 120; `MaxConcurrentCalls` = 2; `ApiKey` has no default. When the provider is Gemini, startup validation MUST reject: a `ThinkingLevel` other than `low`, `medium` or `high` (including `minimal`); a timeout or `MaxConcurrentCalls` that is not a positive integer; a `BaseUrl` that is not an absolute http(s) URL; an empty `Model`. Each message MUST name the offending option and MUST NOT echo the key. The model name is configuration only: changing `Gemini:Model` MUST change the request path with no code change.

#### Scenario: Defaults [UNIT]
- GIVEN `Assistant:Provider` = "Gemini", a key, and no other Gemini option
- WHEN the options are bound
- THEN the model is `gemini-3.8-flash`, the level `low`, the timeouts 60 and 120 seconds, the limit 2 and the base URL the Google endpoint

#### Scenario: Overrides are honored [UNIT]
- GIVEN `Gemini:Model` = "other-model", `Gemini:TurnTimeoutSeconds` = 10 and `Gemini:MaxConcurrentCalls` = 1
- WHEN a call is made through the client with a stub
- THEN the request path contains "other-model", the turn deadline is 10 seconds and one call at a time is allowed

#### Scenario: Invalid thinking level [UNIT]
- GIVEN `Gemini:ThinkingLevel` = "minimal"
- WHEN the application is built with the Gemini provider
- THEN startup fails naming `Gemini:ThinkingLevel`

#### Scenario: Non-positive numbers [UNIT]
- GIVEN `Gemini:TurnTimeoutSeconds` = 0, then `Gemini:DraftTimeoutSeconds` = -5, then `Gemini:MaxConcurrentCalls` = 0
- WHEN the application is built with the Gemini provider
- THEN each fails at startup naming the option

#### Scenario: Bad base URL [UNIT]
- GIVEN `Gemini:BaseUrl` = "not a url"
- WHEN the application is built with the Gemini provider
- THEN startup fails naming `Gemini:BaseUrl`

### Requirement: Startup validation of the API key (RF-68, RNF-04)

With `Assistant:Provider` = Gemini and `Gemini:ApiKey` missing, empty or whitespace, the application MUST fail at startup (not at the first request) with a message that names the environment variable `Gemini__ApiKey` and says the assistant cannot start without it. No startup message, log entry or exception MUST contain the value of the key, whether valid or not. The key MUST come from an environment variable or user-secrets and MUST NOT be present in any committed file.

#### Scenario: Missing key fails and names the variable [UNIT]
- GIVEN `Assistant:Provider` = "Gemini" and no key
- WHEN the application is built
- THEN startup fails with a message containing "Gemini__ApiKey"

#### Scenario: Whitespace key fails [UNIT]
- GIVEN `Assistant:Provider` = "Gemini" and `Gemini:ApiKey` = "   "
- WHEN the application is built
- THEN startup fails with the same message

#### Scenario: A configured key passes and is never echoed [UNIT]
- GIVEN `Assistant:Provider` = "Gemini" and `Gemini:ApiKey` = "K-12345"
- WHEN the application is built and an invalid option makes validation fail
- THEN the failure message does not contain "K-12345"

#### Scenario: Fake does not require a key [UNIT]
- GIVEN `Assistant:Provider` = "Fake" and no key
- WHEN the application is built
- THEN startup succeeds

#### Scenario: No key in the repository [DOC]
- GIVEN the committed `appsettings*.json` files
- WHEN read
- THEN `Gemini:ApiKey` is absent or empty and `Assistant:Provider` is `Fake`

### Requirement: Tests and CI never need a key or the network (RF-68)

The build and the whole automated test suite MUST pass with no `Gemini:ApiKey` and no network access. Every test of the Gemini code MUST use a stub `HttpMessageHandler` or a scripted double. The live-key checks (probe call, persona evaluation, Chrome walkthrough) are manual and are never part of CI. The existing registration test that builds `AddInfrastructure` with only a connection string MUST still resolve the fake drafter and the fake assistant.

#### Scenario: Suite passes without a key [UNIT]
- GIVEN an environment with no `Gemini__ApiKey`
- WHEN the full suite runs
- THEN it passes and no test opens a network connection

#### Scenario: Registration test still resolves the fakes [UNIT]
- GIVEN `AddInfrastructure` built with only a connection string
- WHEN `IArtifactDrafter` and `IAssistantService` are resolved
- THEN both are the fakes

#### Scenario: Live evaluation with a real key [MANUAL]
- GIVEN `Assistant:Provider` = "Gemini" and a real key in user-secrets
- WHEN the persona evaluation script and the Chrome walkthrough are run (Small and Standard end to end, "Reintentar" after a forced failure)
- THEN the results are recorded in the change notes and `Logs/` is checked for content

### Requirement: Configuration documentation (RF-68, RN-05)

README and AGENTS.md section 4.8 MUST document: the `Assistant:Provider` switch and its default, each `Gemini:*` option with its default, how to supply the key (user-secrets and `Gemini__ApiKey`), the default model name, that the free tier may use submitted data to improve Google products (so only sample data is used), and how to go back to the fake. REQUISITOS.md MUST add RF-68 and cross-reference the switch and the logging rule from RN-05, RNF-04 and RNF-02.

#### Scenario: README states the switch and the warning [DOC]
- GIVEN README.md
- WHEN read
- THEN it lists the switch, the options with defaults, the key instructions and the free-tier warning

#### Scenario: AGENTS and REQUISITOS agree [DOC]
- GIVEN AGENTS.md section 4.8 and REQUISITOS.md RF-68, RN-05, RNF-02 and RNF-04
- WHEN compared
- THEN they state the same switch, the same key variable and the same logging rule
