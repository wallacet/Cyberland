---
name: publish-cyberland
description: >-
  Publishes the Cyberland game (Cyberland.Host) to a Release folder under
  artifacts/publish. Use when the user wants a distributable build, release
  package, publish output, or to ship the game outside the dev bin folder.
---

# Publish Cyberland

The host project is **`src/Cyberland.Host/Cyberland.Host.csproj`**. Centralized output is configured in **`Directory.Build.props`** (`UseArtifactsOutput`, **`ArtifactsPath`** = repo **`artifacts/`**).

**Mods:** After **Build** and after **Publish**, MSBuild runs **`scripts/StageModsForHost.ps1`**, which builds each **`mods/*/`** project and copies enabled mods into **`Mods/`** next to the host output. Mods with **`"disabled": true`** in **`manifest.json`** are not staged.

## Default publish (framework-dependent, Windows)

From the **repository root**:

```powershell
dotnet publish src/Cyberland.Host/Cyberland.Host.csproj -c Release
```

Or use the helper script:

```powershell
.\scripts\Publish-Cyberland.ps1
```

**Distribution zip** (same publish folder, plus a zip under **`artifacts/dist/`** for store/CDN upload):

```powershell
.\scripts\Publish-Cyberland.ps1 -Archive
```

Or zip an already-published tree without rebuilding:

```powershell
.\scripts\Archive-CyberlandPublish.ps1
```

**VS Code / Cursor:** Command Palette (`Ctrl+Shift+P`) → **Tasks: Run Task** → **`Cyberland: Publish Release`** or **`Cyberland: Publish Release + distribution zip`**.

- **Publish output:** **`artifacts/publish/Cyberland.Host/release/`** (contains **`Cyberland.Host.exe`**, dependencies, and **`Mods/`** from staging).

## Optional: self-contained single-folder (example)

For a folder that does not require a shared .NET 8 runtime (larger output):

```powershell
dotnet publish src/Cyberland.Host/Cyberland.Host.csproj -c Release -r win-x64 --self-contained true
```

Adjust **`-r`** for another [RID](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog) if needed. Publish still lands under **`artifacts/publish/Cyberland.Host/release/`** (with RID-specific layout as emitted by the SDK); **`Mods/`** is staged into that same folder by the publish step.

## Verification

- **`artifacts/publish/Cyberland.Host/release/Cyberland.Host.exe`** exists.
- **`Mods/`** exists beside the exe (e.g. **`Mods/Cyberland.Game/`** with **`manifest.json`** and **`Cyberland.Game.dll`** when that mod is not disabled).

## Agent workflow

1. Run **`dotnet publish`** from repo root with **`-c Release`** unless the user asks for Debug.
2. If **`Mods/`** is missing under **`publish/`**, run **`dotnet build`** on the host project and inspect **`scripts/StageModsForHost.ps1`** output for errors.
3. If publish fails, run **`dotnet build Cyberland.sln -c Release`** and fix errors first.
