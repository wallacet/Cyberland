---
name: run-cyberland
description: >-
  Build and run the Cyberland game (Cyberland.Host) for manual testing. Use when
  the user wants to run the game, verify a feature, or debug the host process.
---

# Run Cyberland

The executable entry point is **`src/Cyberland.Host`**. It references **Cyberland.Engine** and stages **mods/Cyberland.Game** into the output `Mods` folder on build.

## Quick commands (repository root)

- **Build solution:** `dotnet build Cyberland.sln -c Debug`
- **Run (builds if needed):** `dotnet run --project src/Cyberland.Host/Cyberland.Host.csproj -c Debug`
- **Run with hot reload:** `dotnet watch run --project src/Cyberland.Host/Cyberland.Host.csproj -c Debug`

## Windows PowerShell

From repo root:

```powershell
.\scripts\Run-Cyberland.ps1
.\scripts\Run-Cyberland.ps1 -Watch
```

## VS Code / Cursor

- **Terminal → Run Task… → `build`** — compile the solution (default build task: **Ctrl+Shift+B**).
- **Run Task… → `run`** — `dotnet run` the host (game window).
- **Run Task… → `watch`** — `dotnet watch run` for iterative testing.
- **Run and Debug → `Cyberland.Host`** — launch under the debugger (**F5**), after `build`.

## Agent workflow

When the user asks to run or test the game, prefer **`dotnet run --project src/Cyberland.Host/Cyberland.Host.csproj`** from the workspace root so the working directory and mod staging match local development. If the build fails, run **`dotnet build Cyberland.sln`** and fix reported errors first.
