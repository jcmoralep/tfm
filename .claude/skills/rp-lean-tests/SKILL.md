---
name: rp-lean-tests
description: "Trigger: pruebas, tests, xUnit, Theory, golden, cobertura, escribir tests en Ruta de Producto. Write few, behavior-first unit tests the way this repo expects."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.0"
---

## Activation Contract

Load whenever adding or changing tests under `tests/`.

## Hard Rules

- Unit tests with xUnit only; no integration tests, no database, no network, no Gemini key (AGENTS.md §4.1).
- Test rules, state transitions, formats, security properties and goldens. Do not test reflection/code shape, single label strings, or trivial getters.
- Group cases of the same rule in one `[Theory]` with `InlineData`/`MemberData`.
- A test must be able to fail: assert the behavior, not that a mock was set up. For "nothing happened" assertions, check every downstream double.
- Do not delete existing tests to make room; trim only what you add.
- Test names describe behavior in English sentences with underscores (`A_wrong_code_creates_no_account`).
- Deterministic: no `DateTime.Now`, randomness or sleeps without an injected clock/seed.

## Decision Gates

| Code under test | Test style |
|---|---|
| Handler (Application) | Test doubles from `tests/RutaProducto.Application.Tests/TestDoubles/`; assert result + side effects |
| Options / startup validation | Build `IConfiguration` in memory; assert the exception names the key and does not contain the value |
| Markdown layout / exporter | Golden file in `tests/RutaProducto.Infrastructure.Tests/Artifacts/Goldens/`; update the golden only when the format change is intended and BMAD-checked |
| Identity / tokens | In-memory store helpers in `tests/RutaProducto.Infrastructure.Tests/Identity/` |
| Module boundaries / layers | Extend `tests/RutaProducto.ArchitectureTests/` instead of ad-hoc checks |
| Razor markup | No bUnit in the repo: cover it in the Chrome walkthrough instead |

## Execution Steps

1. List the rules the change introduces; one test (or one Theory) per rule.
2. Reuse existing doubles; add a new double only when none fits.
3. Run `rp-build-gate`; report the new counts.

## Output Contract

Return the rules covered with their test names, any rule left to the walkthrough, and the per-project counts.

## References

- `tests/` — existing patterns; `AGENTS.md` §4.1.
