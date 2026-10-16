---
name: rp-config-switch
description: "Trigger: new setting, interruptor, feature flag, provider, opción de configuración, secret, off by default. Add a configuration switch the Ruta de Producto way."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.0"
---

## Activation Contract

Load when a change adds a configuration key, a provider choice (None/X), a secret, or an optional protection (captcha, email, rate limit, invite code, storage, assistant).

## Hard Rules

- Off by default: with nothing configured, behavior is exactly as before. Empty or whitespace = off.
- Validate at startup in a `XOptions.BindAndValidate(IConfiguration)`; failures throw `InvalidOperationException` naming the key (and the env var form `Section__Key`) and NEVER echo the value.
- Secrets live only in user-secrets or environment variables; never in `appsettings*.json` (`AppSettingsSecretsTests` guards this).
- Application owns the abstraction (`Abstractions/<Area>/IThing.cs`); Infrastructure owns options, implementation and a `services.AddThing(options)` extension called from `DependencyInjection.cs`. Web reads config only for Web-only middleware (rate limiter in `Program.cs`).
- Unknown provider values fail at startup and list the valid ones.
- Never log the value; log only `on/off` or the provider name when useful.
- Checks that gate an account flow run before any account lookup (no enumeration).

## Decision Gates

| Kind | Shape |
|---|---|
| Boolean protection | `Section:Enabled` + numeric limits validated only when enabled |
| Provider choice | `Section:Provider` enum (`None` default) + provider keys required only when selected |
| Shared secret | single key; minimum length; constant-time compare (SHA-256 + `FixedTimeEquals`) |

## Execution Steps

1. Copy the skeleton in `references/pattern.md`; mirror `CaptchaOptions` / `InviteCodeOptions`.
2. Register in `src/RutaProducto.Infrastructure/DependencyInjection.cs` next to the other `BindAndValidate` calls.
3. Tests (`rp-lean-tests`): off by default, invalid values fail without echoing, behavior on/off, DI toggle.
4. Docs (`rp-docs-sync`): README table row + section, REQUISITOS new RF, AGENTS.md §4.6 table row, `docs/mejoras-posteriores.md` for deferred parts.

## Output Contract

Return the key names, default, startup failures added, files touched, and the doc rows written.

## References

- `references/pattern.md` — code skeleton and test list.
- `src/RutaProducto.Infrastructure/Captcha/CaptchaOptions.cs`, `src/RutaProducto.Infrastructure/Registration/InviteCodeOptions.cs` — canonical examples.
