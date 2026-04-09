---
name: publish-cyberland
description: >-
  Publishes the Cyberland game (Cyberland.Host) to a Release folder under
  artifacts/publish. Use when the user wants a distributable build, release
  package, publish output, or to ship the game outside the dev bin folder.
---

# Publish Cyberland

The host project is **`src/Cyberland.Host/Cyberland.Host.csproj`**. Centralized output is configured in **`Directory.Build.props`** (`UseArtifactsOutput`, **`ArtifactsPath`** = repo **`artifacts/`**).

## Default publish (framework-dependent, Windows)

From the **repository root**:

```powershell
dotnet publish src/Cyberland.Host/Cyberland.Host.csproj -c Release
```

Or use the helper script (same steps as below, including `Mods/` copy):

```powershell
.\scripts\Publish-Cyberland.ps1
```

**VS Code / Cursor:** Command Palette (`Ctrl+Shift+P`) → **Tasks: Run Task** → **`Cyberland: Publish Release`** runs that script.

- **Publish output:** **`artifacts/publish/Cyberland.Host/release/`** (contains **`Cyberland.Host.exe`** and dependencies).
- **Mods folder:** Staging targets in the host project run **`AfterTargets="Build"`** and copy **`Mods/Cyberland.Game`** and **`Mods/Cyberland.Demo`** next to the host under **`artifacts/bin/Cyberland.Host/release/`**, not automatically into **`publish/`**. After **`dotnet publish`**, copy mods into the publish tree so the runnable folder matches what **`dotnet run`** / **`dotnet build`** produces:

```powershell
Copy-Item -Recurse -Force "artifacts/bin/Cyberland.Host/release/Mods" "artifacts/publish/Cyberland.Host/release/"
```

## Optional: self-contained single-folder (example)

For a folder that does not require a shared .NET 8 runtime (larger output):

```powershell
dotnet publish src/Cyberland.Host/Cyberland.Host.csproj -c Release -r win-x64 --self-contained true
```

Adjust **`-r`** for another [RID](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog) if needed. Publish still lands under **`artifacts/publish/Cyberland.Host/release/`** (with RID-specific layout as emitted by the SDK); copy **`Mods`** from the matching **`artifacts/bin/Cyberland.Host/release/Mods`** if staging is not present in **`publish/`**.

## Verification

- **`artifacts/publish/Cyberland.Host/release/Cyberland.Host.exe`** exists.
- **`Mods/Cyberland.Game/`** and **`Mods/Cyberland.Demo/`** exist beside the exe (each with **`manifest.json`**, **`Cyberland.*.dll`**, and **`Content/`** as applicable).

## Agent workflow

1. Run **`dotnet publish`** from repo root with **`-c Release`** unless the user asks for Debug.
2. Copy **`Mods`** from **`artifacts/bin/Cyberland.Host/<config>/Mods`** to **`artifacts/publish/Cyberland.Host/<config>/`** when **`Mods/`** is missing under **`publish/`**.
3. If publish fails, run **`dotnet build Cyberland.sln -c Release`** and fix errors first.
