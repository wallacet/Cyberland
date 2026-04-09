# Cyberland

Cyberland is a **cyberpunk 2D single-player RPG** built in C# on **.NET 8**. The codebase separates a reusable **engine**, a thin **host** executable, and **gameplay delivered as mods** (including the shipped base game). Rendering uses **Vulkan** (via Silk.NET); audio uses **OpenAL**.

Design goals: **small footprint**, **predictable load**, and **scaling from integrated GPUs to modern hardware**—see `.cursor/rules/cyberland-design-goals.mdc` for detail.

---

## Requirements

| Requirement | Notes |
|-------------|--------|
| **.NET 8 SDK** | Required to build and run. |
| **Vulkan 1.x + a working driver** | The host clears and draws a simple sprite; init failures show a user-facing message instead of crashing silently. |
| **Windows** | Primary target; input and error UI are written with that in mind (other platforms may work where Silk.NET + Vulkan do). |

---

## Quick start

From the repository root:

```powershell
dotnet build Cyberland.sln -c Debug
dotnet run --project src/Cyberland.Host/Cyberland.Host.csproj -c Debug
```

Or run via script:

```powershell
.\scripts\Run-Cyberland.ps1
.\scripts\Run-Cyberland.ps1 -Watch   # dotnet watch run
```

**Visual Studio Code / Cursor:** default build task builds the solution; **Run** / **Watch** tasks run the host; launch configuration **Cyberland.Host** debugs under **`artifacts/bin/Cyberland.Host/debug/`** so `Mods/` resolves next to the executable.

Open the **Command Palette** (`Ctrl+Shift+P`) → **Tasks: Run Task** → pick a **Cyberland:** task (same commands as the Cursor skills):

| Task | Action |
|------|--------|
| **Cyberland: Run** | `dotnet run` the host (Debug) — *run-cyberland* skill |
| **Cyberland: Test Engine** | Engine tests with coverlet — *test-cyberland-engine* skill |
| **Cyberland: Publish Release** | `dotnet publish` + copy `Mods/` into publish output — *publish-cyberland* skill (see `scripts/Publish-Cyberland.ps1`) |
| **Cyberland: Clear Artifacts** | Delete repo-root `artifacts/` — *clear-cyberland-artifacts* skill (see `scripts/Clear-CyberlandArtifacts.ps1`) |

---

## Asset setup (GitHub Releases)

Large game media does not live in git. Asset bundles are published in GitHub Releases and mapped by per-mod manifests:

- `mods/Cyberland.Game/content.release.manifest.json`
- `mods/Cyberland.Demo/content.release.manifest.json`

Each bundle is tied to one mod and extracts into that mod's `Content/` folder:

- `mods/Cyberland.Game/Content/`
- `mods/Cyberland.Demo/Content/`

From repository root:

```powershell
.\scripts\Sync-CyberlandAssets.ps1
```

The script discovers each mod manifest, downloads release archives, verifies SHA256, and extracts to the mod-owned content folders above.

### Git hook setup (recommended)

This repo includes a pre-commit hook that blocks staged files larger than 4 MiB by default.

```powershell
.\scripts\Setup-GitHooks.ps1
```

Override options for exceptional cases:

- One-off bypass: `git commit --no-verify`
- Maintainer local override: `CYBERLAND_ALLOW_LARGE_FILES=1 git commit ...`
- Temporary threshold override: `CYBERLAND_MAX_FILE_MB=8 git commit ...`

If you bypass, follow up by moving large media into the GitHub Releases asset flow.

### Build output (`artifacts/`)

The solution uses **.NET 8 [artifacts output layout](https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output)** (`UseArtifactsOutput` + `ArtifactsPath` in **`Directory.Build.props`**). **All** compiled output, intermediates, and **`dotnet publish`** output go under **`artifacts/`** at the repo root—**not** into `bin/` / `obj/` next to each `.csproj`.

| Path | Contents |
|------|----------|
| **`artifacts/bin/<ProjectName>/debug/`** or **`.../release/`** | Built assemblies and deps (e.g. **`artifacts/bin/Cyberland.Host/debug/Cyberland.Host.exe`**). Mod staging runs on **build** and places **`Mods/`** here next to the host. |
| **`artifacts/obj/...`** | MSBuild intermediate files per project. |
| **`artifacts/publish/<ProjectName>/debug/`** or **`.../release/`** | **`dotnet publish`** output for that project (e.g. **`Cyberland.Host.exe`** and dependencies). **`Mods/`** is not copied here automatically; see [Clean build and packaging](#clean-build-and-packaging) below. |

After **Cyberland.Host** builds, mods are **staged** next to the host executable under `Mods/`:

| Folder | Contents |
|--------|----------|
| **`Mods/Cyberland.Game/`** | Base campaign mod: `Cyberland.Game.dll`, `manifest.json`, `Content/` (synced from release bundles). |
| **`Mods/Cyberland.Demo/`** | Optional Vulkan sprite + ECS sample: `Cyberland.Demo.dll`, `manifest.json`, `Content/` (synced from release bundles). |

`Cyberland.Host.csproj` copies these on build. Remove **`Mods/Cyberland.Demo`** from the output if you do not want the sprite sample.

### Clean build and packaging

Use this when you want a **fresh tree** or a **folder you can zip** and run elsewhere (framework-dependent builds still need the **.NET 8** runtime on the target machine unless you publish self-contained).

1. **Optional — wipe build outputs** (close any running **`Cyberland.Host.exe`** first):

   ```powershell
   if (Test-Path artifacts) { Remove-Item -Recurse -Force artifacts }
   ```

2. **Publish Release** from the repository root:

   ```powershell
   dotnet publish src/Cyberland.Host/Cyberland.Host.csproj -c Release
   ```

   Output: **`artifacts/publish/Cyberland.Host/release/`** (executable + dependencies).

3. **Sync mod media assets** (if not already synced):

   ```powershell
   .\scripts\Sync-CyberlandAssets.ps1
   ```

4. **Stage mods into the publish folder.** Staging targets run **`AfterTargets="Build"`**, so **`Mods/`** lands under **`artifacts/bin/Cyberland.Host/release/`**, not automatically under **`publish/`**. Copy it beside the published exe:

   ```powershell
   Copy-Item -Recurse -Force "artifacts/bin/Cyberland.Host/release/Mods" "artifacts/publish/Cyberland.Host/release/"
   ```

5. **Package** — archive **`artifacts/publish/Cyberland.Host/release/`** (e.g. zip that folder). **`keybindings.json`** is created at runtime next to the exe if missing.

**Self-contained** (larger, no shared runtime on the target), example for Windows x64:

```powershell
dotnet publish src/Cyberland.Host/Cyberland.Host.csproj -c Release -r win-x64 --self-contained true
Copy-Item -Recurse -Force "artifacts/bin/Cyberland.Host/release/Mods" "artifacts/publish/Cyberland.Host/release/"
```

Project-specific notes for agents live in **`.cursor/skills/publish-cyberland/`** and **`.cursor/skills/clear-cyberland-artifacts/`**.

---

## Testing (engine)

The **`Cyberland.Engine.Tests`** project targets **`Cyberland.Engine`** only (not the host, mods, or GPU paths). It enforces **100% line coverage** on that assembly via **coverlet**.

GitHub Actions runs this automatically in the **`Engine Tests`** workflow for pull requests and pushes to `main` when engine test-related files change (plus manual `workflow_dispatch`).

```powershell
dotnet test tests/Cyberland.Engine.Tests/Cyberland.Engine.Tests.csproj -c Debug /p:CollectCoverage=true
```

Use the command above for local feedback before pushing. CI remains the merge gate for engine coverage.

Coverage outputs **`coverage.cobertura.xml`** next to the test project output (ignored by git).

Types that require a real **window**, **Vulkan**, **OpenAL**, or **Win32 MessageBox** are marked **`[ExcludeFromCodeCoverage]`** (`VulkanRenderer`, `GameApplication`, `OpenALAudioDevice`, parts of `GlslSpirvCompiler`, `UserMessageDialog.ShowError`). When you change those, add or extend **manual / integration** checks; keep pure logic testable in isolation.

---

## Repository layout

```
Cyberland.sln
Directory.Build.props          # Shared SDK, language settings, artifacts output root
artifacts/                     # Build outputs (gitignored): bin/, obj/, publish/
tests/
  Cyberland.Engine.Tests/      # xUnit + coverlet (100% line coverage on Cyberland.Engine)
  Cyberland.TestMod/           # Minimal IMod assembly used by ModLoader tests
src/
  Cyberland.Host/              # Executable: references Engine + staged mods (build)
  Cyberland.Engine/            # Engine library (ECS, Vulkan, input, mods, assets, …)
mods/
  Cyberland.Game/              # Base campaign mod (IMod, locale Content/)
  Cyberland.Demo/              # Optional Vulkan sprite + parallel ECS sample mod
scripts/
  Run-Cyberland.ps1
  Publish-Cyberland.ps1
  Clear-CyberlandArtifacts.ps1
.vscode/
  tasks.json, launch.json
.cursor/rules/                 # Optional agent / team conventions
```

| Project | Role |
|---------|------|
| **Cyberland.Host** | Entry point (`Program.cs` → `GameApplication`). Builds and stages **`Cyberland.Game`** and **`Cyberland.Demo`** into `$(OutDir)Mods/`. |
| **Cyberland.Engine** | All shared runtime: windowing, Vulkan renderer, ECS, task scheduler, virtual FS, assets, localization, OpenAL, mod loader, `GameHostServices`. |
| **Cyberland.Game** | Base campaign mod → `Cyberland.Game.dll` (locale and future core data). |
| **Cyberland.Demo** | Optional sample mod → `Cyberland.Demo.dll` (sprite movement, velocity damp ECS). Ship without it by not staging or deleting that folder from output. |

---

## High-level architecture

```mermaid
flowchart TB
    subgraph Host["Host process"]
        GA[GameApplication]
        GA --> Window["Silk.NET Window"]
        GA --> VK[VulkanRenderer]
        GA --> Sched[SystemScheduler]
        GA --> World["World ECS"]
        GA --> VFS[VirtualFileSystem]
        ML[ModLoader]
        GA --> ML
        ML --> ModDll["IMod DLLs (Game, Demo)"]
        ModDll --> Systems["ISystem / IParallelSystem"]
        Systems --> World
        Systems --> HostSvc[GameHostServices]
        HostSvc --> VK
        HostSvc --> Input[IInputContext]
        HostSvc --> Keys[KeyBindingStore]
    end
```

1. **Host** creates the window, graphics, input, keybindings, ECS world, scheduler, and VFS, then calls **`ModLoader.LoadAll`** on `AppContext.BaseDirectory/Mods`.
2. Each mod’s **`IMod.OnLoad`** receives a **`ModLoadContext`**: world, scheduler, localization, VFS, and **`Host`** (`GameHostServices`).
3. Mods **register systems** on the scheduler and optionally spawn entities, mount extra paths, etc.
4. Every frame, **`GameApplication`** runs **`SystemScheduler.RunFrame(world, dt)`** (sequential systems, then parallel systems), handles **host-only** input (e.g. Escape → exit), and **presents** the swapchain.

**Rule of thumb:** *If it is gameplay, it belongs in a mod (or a new mod assembly), not in `GameApplication`.*

---

## Engine subsystems (Cyberland.Engine)

### ECS (`Core/Ecs`)

- **`World`** — entity creation/destruction, **`Components<T>()`** stores per component type.
- **`ComponentStore<T>`** — dense storage, **`GetOrAdd`**, **`AsSpan()`** for hot loops.
- **`EntityId`** — opaque id from **`EntityRegistry`**.

Components are **`struct`** types; define them in your mod assembly (see `Velocity` in **`Cyberland.Demo`**).

### Task scheduler (`Core/Tasks`)

- **`SystemScheduler`** — registers **`ISystem`** (main thread, deterministic order) and **`IParallelSystem`** (runs after all sequential systems, with **`ParallelOptions`** from **`ParallelismSettings`**).
- **`ParallelismSettings.MaxConcurrency`** — `0` means use all logical processors.

Frame order:

1. Every **`ISystem.OnUpdate(world, deltaSeconds)`** in registration order.
2. Every **`IParallelSystem.OnParallelUpdate(world, parallelOptions)`** in registration order.

### Rendering (`Rendering/`)

- **`VulkanRenderer`** — swapchain, render pass, pipeline, indexed quad, push constants for sprite position/size in **pixel space** after conversion from **world space** via **`SetSpriteWorld`**.
- **`WorldScreenSpace`** — **world** (origin bottom-left, +Y up) vs **screen / framebuffer** (top-left, +Y down). **`SetSpriteWorld`** applies **`WorldCenterToScreenPixel`** inside the renderer—gameplay should stay in world space and call **`SetSpriteWorld`**, not hand-convert in multiple places.

### Input (`Input/`)

- **`KeyBindingStore`** — maps action ids (`move_up`, `move_left`, …) to **`Silk.NET.Input.Key`**, loaded from `keybindings.json` under the app base directory.

### Assets (`Assets/`)

- **`VirtualFileSystem`** — ordered mount points; **later mounts override earlier** (mod content over base). **`BlockPath`** hides a relative path globally (even if an earlier mount had the file).
- **`AssetManager`** — async **`LoadBytesAsync`**, **`LoadTextAsync`**, **`LoadJsonAsync`**, streaming **`OpenReadOrThrow`**.

### Localization (`Localization/`)

- **`LocalizationManager`** — merged key → string tables (JSON), culture fallback; **`TryRemoveKey`** / **`RemoveKey`** drop keys for later mods.
- Mods merge strings through the load pipeline / your own loads as needed.

### Audio (`Audio/`)

- **`OpenALAudioDevice`** — optional; host continues without audio if OpenAL is missing.

### Modding (`Modding/`)

- **`IMod`** — **`OnLoad(ModLoadContext)`**, **`OnUnload()`**.
- **`ModManifest`** — id, version, **`entryAssembly`**, **`contentRoot`**, **`loadOrder`**, optional **`contentBlocklist`** (see `manifest.json`).
- **`ModLoader`** — discovers `Mods/*/manifest.json`, mounts content (then applies each mod’s blocklist), loads **`entryAssembly`**, finds one concrete **`IMod`**, invokes **`OnLoad`**.

### Hosting (`Hosting/`)

- **`GameHostServices`** — **`KeyBindings`**, **`Renderer`** (**`VulkanRenderer?`**), **`Input`** (**`IInputContext?`**). Populated by **`GameApplication`** after the window and device exist, then passed into **`ModLoadContext`** so mods do not use static globals.

---

## Mod system (convention)

### Folder layout on disk

```
Mods/
  Cyberland.Game/         # loadOrder 0 — locale, future core assets
  Cyberland.Demo/         # loadOrder 10 — optional sprite / ECS sample
    manifest.json
    *.dll
    Content/                # mounted to VFS (last mod wins for same path)
```

### `manifest.json`

Example (see `mods/Cyberland.Game/manifest.json`):

- **`id`** — stable string id.
- **`entryAssembly`** — DLL name containing an **`IMod`** implementation.
- **`contentRoot`** — relative folder mounted for this mod (often `Content`).
- **`loadOrder`** — lower runs earlier (manifests sorted by load order, then id).
- **`contentBlocklist`** (optional) — array of virtual relative paths to hide after this mod’s content is mounted (blocks win over all mounts; use normal file overrides when you want to replace content instead).

### `IMod` implementation

- Ship a **public non-abstract class** implementing **`IMod`** (the loader picks the first exported type assignable to **`IMod`**).
- **`OnLoad`**: register systems (with stable **logical ids**), spawn entities, merge localization, call **`context.MountDefaultContent()`** if you rely on `Content/` under the mod folder.

### Systems: ids, extend, replace, remove

Every ECS system is registered with a **non-empty logical id** (convention: `"<modId>/<purpose>"`, e.g. `cyberland.demo/sprite-move`). Mods load in **`loadOrder`** order; a later mod can:

- **Extend** — register **new** ids.
- **Replace** — call **`RegisterSequential`** / **`RegisterParallel`** again with an id already used; the implementation is swapped **in place** (frame order among other systems stays the same).
- **Remove** — **`TryUnregister(logicalId)`** drops that system from the sequential or parallel pass.

Use **`context.RegisterSequential`**, **`context.RegisterParallel`**, and **`context.TryUnregister`** (wrappers around **`SystemScheduler`**). Do not reuse the same id across sequential vs parallel registration.

### Content and localization overrides

- **Override a file** — ship a file at the same virtual path from a **later** mod (VFS last mount wins).
- **Hide a path** — **`context.HideContentPath("relative/path")`** or declare **`contentBlocklist`** in **`manifest.json`** so the path does not resolve.
- **Remove a localization key** — **`context.TryRemoveLocalizationKey("key")`** after earlier mods merged strings.

### `GameHostServices` (via `context.Host`)

| Member | Use |
|--------|-----|
| **`KeyBindings`** | **`IsDown(keyboard, "move_up")`** etc. |
| **`Input`** | Raw **`IKeyboard`** / mice if needed. |
| **`Renderer`** | **`SwapchainPixelSize`**, **`SetSpriteWorld`** (e.g. **`Cyberland.Demo`** sprite sample). |

The host sets **`Renderer`** and **`Input`** only after successful window/input setup; systems should null-check when relevant.

---

## Developing new game systems

### 1. Prefer a system in the mod, not the host

Add logic under **`mods/<YourMod>/`** with a **`manifest.json`** and stage it from **`Cyberland.Host.csproj`** like **`Cyberland.Game`** / **`Cyberland.Demo`**.

### 2. Define data as components

```csharp
namespace MyMod;

public struct MyComponent
{
    public float Value;
}
```

Use **`world.Components<MyComponent>().GetOrAdd(entity)`** (or **`TryGet`**) to associate state with entities.

### 3. Implement `ISystem` and/or `IParallelSystem`

- **`ISystem`** — single-threaded; use for input, gameplay ordering, talking to **`GameHostServices`**, or anything that must not race the ECS stores without care.
- **`IParallelSystem`** — use for CPU-heavy work over **`ComponentStore<T>.AsSpan()`**; follow the pattern in **`VelocityDampSystem`** in **`Cyberland.Demo`** (copy/rent/write back if you cannot share spans safely across **`Parallel.For`**).

### 4. Register in your mod’s `IMod.OnLoad` (e.g. `BaseGameMod`, `DemoMod`)

```csharp
context.RegisterSequential("my.mod/main", new MySystem(context.Host));
context.RegisterParallel("my.mod/batch", new MyParallelSystem());
```

First-time registration order is the run order within each category (sequential vs parallel). Replacing an existing **logical id** keeps that system’s position in the list.

### 5. Use the ECS world from context

```csharp
var id = context.World.CreateEntity();
ref var c = ref context.World.Components<MyComponent>().GetOrAdd(id);
c = new MyComponent { Value = 1f };
```

### 6. Input and rendering

- Read actions through **`context.Host.KeyBindings`** and **`context.Host.Input`**.
- For the current sprite API, update position in **world space** and call **`context.Host.Renderer?.SetSpriteWorld(x, y, halfExtent)`** once per frame from your system when appropriate.

### 7. Assets and localization

- Resolve paths against the **`VirtualFileSystem`** (mounts include mod **`Content/`** roots in load order).
- Use **`AssetManager`** with the same VFS instance the host constructed (passed through localization/bootstrap as in **`GameApplication`**).

### 8. New mod assembly (optional)

1. Add a project under **`mods/YourMod/`** referencing **`Cyberland.Engine`**.
2. Implement **`IMod`**.
3. Add **`manifest.json`**.
4. Reference the mod from **`Cyberland.Host.csproj`** and add a **`Stage*Mod`** **Copy** target (mirror **`StageBaseMod`** / **`StageDemoMod`**) so **`Mods/YourMod/`** is populated in the output directory.

---

## Reference examples in this repo

| Example | Location | Shows |
|---------|----------|--------|
| Base mod entry | `mods/Cyberland.Game/BaseGameMod.cs` | Minimal **`IMod`**, locale **`Content/`** |
| Demo mod entry | `mods/Cyberland.Demo/DemoMod.cs` | **`IMod`**, entity spawn, **`RegisterSequential`** / **`RegisterParallel`** with logical ids |
| Sequential + input + renderer | `mods/Cyberland.Demo/SpriteMoveSystem.cs` | **`ISystem`**, **`GameHostServices`**, **`SetSpriteWorld`** |
| Parallel ECS | `mods/Cyberland.Demo/VelocityDampSystem.cs` | **`IParallelSystem`**, **`Velocity`**, scratch buffer pattern |
| Host bootstrap | `src/Cyberland.Engine/GameApplication.cs` | Lifecycle, **`LoadAll`**, menu key |

---

## Configuration

- **`keybindings.json`** — lives next to the host executable (see **`KeyBindingStore.LoadDefaults`** for action ids). First run creates the file if missing.

---

## Troubleshooting

| Issue | Suggestion |
|-------|------------|
| **Vulkan / GPU errors on startup** | Update GPU drivers; ensure Vulkan is supported. The engine surfaces a message via **`UserMessageDialog`** / **`GraphicsInitializationException`**. |
| **Mod not loading** | Check **`Mods/<Id>/manifest.json`**, **`entryAssembly`** name, and that the DLL is staged next to **`manifest.json`**. |
| **Empty or missing content** | Confirm **`contentRoot`** exists and **`ModLoader`** mount order; later mods override earlier paths for the same relative path. |

---

## Further reading (in-repo)

- **`.cursor/rules/cyberland-mod-host-architecture.mdc`** — host vs mod boundaries and checklists.
- **`.cursor/rules/cyberland-world-screen-space.mdc`** — world vs screen Y conventions.
- **`.cursor/rules/cyberland-code-style.mdc`** — comments and readability expectations.

---

## License

*Add your license here if applicable.*
