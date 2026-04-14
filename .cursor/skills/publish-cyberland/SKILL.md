---
name: publish-cyberland
description: >-
  Publishes the Cyberland game (Cyberland.Host) to a Release folder under
  artifacts/publish. Use when the user wants a distributable build, release
  package, publish output, or to ship the game outside the dev bin folder.
---

# Publish Cyberland

The host project is **`src/Cyberland.Host/Cyberland.Host.csproj`**. Centralized output is configured in **`Directory.Build.props`** (`UseArtifactsOutput`, **`ArtifactsPath`** = repo **`artifacts/`**).

**Mods:** After **Build** and after **Publish**, MSBuild runs **`scripts/StageModsForHost.ps1`**, which stages enabled **`mods/*/`** into **`Mods/`** next to the host output (copies **`manifest.json`** + **`Content/`**; runs **`dotnet build`** only for mods that declare **`entryAssembly`**). Mods with **`"disabled": true`** in **`manifest.json`** are not staged.

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

- **Publish output (portable):** **`artifacts/publish/Cyberland.Host/release/`** (contains **`Cyberland.Host.exe`** on Windows, dependencies, and **`Mods/`** from staging).

## Per-platform RID publish (small; uses shared .NET)

**Self-contained is optional.** By default, **`Publish-Cyberland.ps1 -RuntimeIdentifier <rid>`** uses **`--self-contained false`**: the game ships as a small folder of assemblies plus native deps (Silk.NET, etc.) and relies on the user’s **installed .NET 8 runtime** (same model as portable publish, but with RID-specific native assets). Add **`-SelfContained`** only when you need a fully offline bundle (larger).

```powershell
.\scripts\Publish-Cyberland.ps1 -RuntimeIdentifier win-x64
.\scripts\Publish-Cyberland.ps1 -RuntimeIdentifier win-x64 -SelfContained   # optional: bundle CoreCLR + shared framework
```

Output: **`artifacts/publish/Cyberland.Host/<config>_<rid>/`** (for example **`release_win-x64`**). Other RIDs: **`win-arm64`**, **`linux-x64`**, **`linux-arm64`**, **`osx-x64`**, **`osx-arm64`**. Optional **`-Archive`** zips to **`artifacts/dist/Cyberland-Host-<config>-<rid>.zip`**.

**VS Code / Cursor:** **Tasks: Run Task** includes per-RID **Publish Release** and **… + zip** tasks (framework-dependent by default).

## Verification

- Portable: **`artifacts/publish/Cyberland.Host/release/`** has **`Cyberland.Host.exe`** (Windows) or **`Cyberland.Host`** (Unix) and **`Mods/`**.
- Self-contained RID: **`artifacts/publish/Cyberland.Host/release_<rid>/`** has the host entrypoint and **`Mods/`** (e.g. **`Mods/Cyberland.Game/`** with **`manifest.json`** and **`Cyberland.Game.dll`** when that mod is not disabled).

## Agent workflow

1. Run **`dotnet publish`** from repo root with **`-c Release`** unless the user asks for Debug.
2. If **`Mods/`** is missing under **`publish/`**, run **`dotnet build`** on the host project and inspect **`scripts/StageModsForHost.ps1`** output for errors.
3. If publish fails, run **`dotnet build Cyberland.sln -c Release`** and fix errors first.
