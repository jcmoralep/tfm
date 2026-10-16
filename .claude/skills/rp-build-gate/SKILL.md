---
name: rp-build-gate
description: "Trigger: build, test, gate, compilar, correr pruebas, before commit or PR in Ruta de Producto. Run the restore/build/test gate the way CI does and report counts."
license: Apache-2.0
metadata:
  author: "jcmoralep"
  version: "1.1"
---

## Activation Contract

Load before any commit, PR, merge or "done" claim in this repo, and whenever a change touches `src/` or `tests/`.

## Hard Rules

- Stop any running app first: a live `RutaProducto.Web.dll` locks `bin/` and the build fails or tests run stale code.
- Warnings are errors (`Directory.Build.props`); never silence one with `NoWarn` to pass the gate.
- Use Release, as CI does (`.github/workflows/ci.yml`).
- Report the real per-project counts; never claim green without the output.
- Tests need no database, network or Gemini key; a test that needs them is wrong.

## Decision Gates

| Situation | Action |
|---|---|
| Plain restore fails on an unreachable feed | Retry with `--ignore-failed-sources -p:NuGetAudit=false -p:WarningsNotAsErrors=NU1507`, and say in the report that the vulnerability audit was skipped (CI will still run it) |
| `Stop-Process` command exits 255 | Expected: verify the port is free (`netstat -ano`) and continue in a new command |
| Only a `.razor`/Web file changed and the app must restart quickly | `dotnet build src/RutaProducto.Web -c Release --no-restore`, then still run the full gate before commit |
| A migration was added | Also run `dotnet ef database update` in a walkthrough (see `rp-local-walkthrough`) |

## Execution Steps

1. Stop the app (PowerShell): `Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*RutaProducto.Web.dll*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }`.
2. `dotnet restore RutaProducto.slnx` (as CI); only on a feed error use the fallback flags from the gate table.
3. `dotnet build RutaProducto.slnx -c Release --no-restore`
4. `dotnet test RutaProducto.slnx -c Release --no-build`
5. On failure, read the first error, fix the cause, rerun from step 3.

## Output Contract

Return: build result (errors/warnings), and one line per test project with passed/failed/total (Architecture, Web, Application, Infrastructure). Say explicitly if any step was skipped.

## References

- `.github/workflows/ci.yml` — the CI gate this mirrors.
- `Directory.Build.props` — warnings-as-errors and shared settings.
