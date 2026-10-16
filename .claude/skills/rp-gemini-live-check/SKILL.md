---
name: rp-gemini-live-check
description: "Trigger: Gemini real, probar con Gemini, modelo, 503, cuota, latencia, calidad del asistente o de los documentos. Run a moderate live Gemini check without leaking data."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.0"
---

## Activation Contract

Load when a check needs the real model: prompt or schema changes, model changes, quality reviews of generated documents, context-attachment behavior, or reproducing a Gemini failure.

## Hard Rules

- Free tier: Google may use the data. Only sample data, never real company initiatives (AGENTS.md §4.8, RN-05).
- The key lives in user-secrets `Gemini:ApiKey` or `Gemini__ApiKey`; never read, list, print or copy it. Do not run `dotnet user-secrets list`.
- Use it moderately: plan the calls before starting and stop at the plan. A full Large run is about 10 turns + 3 drafts (~16 calls including retries).
- Tests and CI never call Gemini; this is a manual check only.
- Logs must contain no prompts, answers or document text; only ids, topic/type, model, status, duration, finishReason, tokens.
- Retry a failed call at most once from the UI ("Reintentar" / "Generar"); then switch model or stop.

## Decision Gates

| Symptom | Action |
|---|---|
| 503 on drafts while turns work | Restart with `Gemini__Model=gemini-3.1-flash-lite` (documented fallback) |
| Isolated 429 | Wait about a minute or switch to the fallback model; retry once |
| Daily cap reached | Stop; record it; resume another day |
| Turn > 60 s or draft > 120 s | Record as latency finding; compare with `docs/gemini-sonda.md` |
| Output invents facts or ignores an answer | Record as quality finding (`rp-artifact-reviewer`); do not tune prompts in the same run |

## Execution Steps

1. Write the plan: level (Small/Standard/Large), sample persona answers, documents to generate, call budget.
2. Start the app per `rp-local-walkthrough` with `Assistant__Provider=Gemini`.
3. Run the flow with the turn helper; after each failure read only the `Gemini call` log lines.
4. Count calls (`Gemini call` lines) and scan the log for any sample sentence to prove no content leaked.
5. Review the generated documents with `rp-artifact-reviewer`.
6. Record results in `docs/gemini-evaluacion.md` (date, model, calls, failures, findings).

## Output Contract

Return: model(s) used, calls made vs budget, failures with status and duration, content-leak scan result, and quality findings classified as UI, model quality, or reliability.

## References

- `docs/gemini-evaluacion.md` — personas and past runs.
- `docs/gemini-sonda.md` — latency probe and model decision.
- `src/RutaProducto.Infrastructure/Gemini/` — client, options, prompts.
