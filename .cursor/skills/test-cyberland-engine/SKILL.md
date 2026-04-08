---
name: test-cyberland-engine
description: >-
  Runs Cyberland.Engine unit tests with coverlet (100% line coverage gate on
  Cyberland.Engine). Use when the user wants to run engine tests, verify CI
  coverage, or validate changes to src/Cyberland.Engine after edits.
---

# Test Cyberland Engine

The test project is **`tests/Cyberland.Engine.Tests`**. It references **`Cyberland.Engine`**, **`Cyberland.TestMod`** (minimal `IMod` for `ModLoader` tests), and enforces **100% line coverage** on the engine assembly via **coverlet.msbuild** (`/p:CollectCoverage=true`).

## Quick command (repository root)

```powershell
dotnet test tests/Cyberland.Engine.Tests/Cyberland.Engine.Tests.csproj -c Debug /p:CollectCoverage=true
```

- **Release:** use `-c Release` instead of Debug if matching CI/release builds.
- **Faster iteration (no coverage):** omit `/p:CollectCoverage=true` (build still runs tests; threshold is only enforced when coverage collection runs).

## VS Code / Cursor

- **Terminal → Run Task… → `test-engine`** — same as the `dotnet test` command above with coverage.

## Agent workflow

1. Run tests from the **repository root** (workspace root for this project).
2. After changing **`src/Cyberland.Engine`**, run **`dotnet test`** with **`/p:CollectCoverage=true`** so coverage failures surface before handoff.
3. If tests fail, read the xUnit output; if coverage fails, add or adjust tests in **`tests/Cyberland.Engine.Tests`** (see **`.cursor/rules/cyberland-engine-tests.mdc`**).
4. Types that need a GPU, window, OpenAL, or Win32 dialog are marked **`[ExcludeFromCodeCoverage]`**; do not remove the gate to “fix” coverage—extend tests for testable code instead.

## Output

Coverlet writes **`coverage.cobertura.xml`** under the test project’s output folder; it is gitignored.
