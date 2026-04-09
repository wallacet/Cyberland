---
name: clear-cyberland-artifacts
description: >-
  Deletes the repo-root artifacts folder (all MSBuild bin/obj/publish outputs).
  Use when the user wants a clean rebuild, to free disk space, clear stale
  outputs, or reset build artifacts before publishing or testing.
---

# Clear Cyberland artifacts

**`Directory.Build.props`** sets **`ArtifactsPath`** to **`artifacts/`** at the repository root. Everything produced by **`dotnet build`**, **`dotnet publish`**, intermediates, and test builds under that layout lives there (not under per-project **`bin/`** / **`obj/`** next to each `.csproj`).

## Command (repository root)

**PowerShell:**

```powershell
Remove-Item -Recurse -Force artifacts
```

If **`artifacts`** does not exist, the command errors; that is harmless to ignore or guard with **`Test-Path`**.

**Optional (only if `artifacts` exists):**

```powershell
if (Test-Path artifacts) { Remove-Item -Recurse -Force artifacts }
```

## Before deleting

- **Close** a running **`Cyberland.Host.exe`** (or any process using files under **`artifacts/`**).
- **IDE / MSBuild** can occasionally lock files; retry after closing heavy builds or wait a moment.

## After clearing

Run **`dotnet build Cyberland.sln`** or **`dotnet restore`** as needed; the next build recreates **`artifacts/`**.

## Agent workflow

When the user asks to clean artifacts or get a fresh build, run **`Remove-Item`** from the **workspace root** (or the guarded variant). Do not delete source, **`mods/`**, or project folders—only the **`artifacts`** directory at the repo root.
