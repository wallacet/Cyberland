# Cyberland.Demo (HDR tutorial)

## Purpose

Smallest engine-faithful sample: **`Camera2D`**, **`Sprite`** + **`BitmapText`**, deferred lights, **`PostProcessVolumeSource`**, **`GlobalPostProcessSource`**, and mixed **`RegisterSingleton`** / **`RegisterParallel`** scheduling. Use this mod first when learning the repo.

## Run

From repo root (restores **`disabled: true`** after exit):

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo hdr
```

Manifests stay **`"disabled": true`** in git (pre-commit). To toggle by hand, edit **`mods/Cyberland.Demo/manifest.json`**, rebuild **Cyberland.Host**, and run the executable.

## Learning path

1. **`Mod.cs`** — registration order, shader smoke-test, baked atlas kickoff.
2. **`SceneSetup.cs`** — cold-start entities (camera, player, HUD, lights, bloom volume, global post).
3. **`Systems/InputSystem.cs`** — parallel velocity SoA + scheduler-thread axis read between barriers.
4. **`Systems/IntegrateSystem.cs`** — **`ISingletonSystem`** + fixed-step motion.
5. **`Systems/VelocityDampSystem.cs`** — honest **`IParallelSystem`** over **`QueryChunks<Velocity>`**.
6. **`Systems/HdrPostVolumeFillSystem.cs`**, **`FpsDisplaySystem.cs`** — late singleton presentation.

## Features taught

- **`ModLayoutViewport`** for virtual canvas size; **F9** toggles **`cyberland.demo/velocity-damp`** (see **`Mod.cs`** comments).
- Integrate-then-damp ordering in the **fixed** phase.
- **`ISingletonSystem`** for single-row work (**`IntegrateSystem`**, **`FpsDisplaySystem`**, **`HdrPostVolumeFillSystem`**).
- **`IParallelSystem`** + **`QueryChunks`** for packed component columns.

## Content

- **`Content/Locale/en/demo_hdr.json`** — HUD strings.
- **`content.release.manifest.json`** — no required binary bundle for local dev beyond engine defaults.

## Further reading

- **`.cursor/rules/cyberland-demo-mod-authoring.mdc`** — tutorial mod contract.
- **`.cursor/rules/cyberland-mod-patterns-hdr.mdc`** — **`SceneSetup`** + query-first patterns.
- Root **`README.md`** — host pipeline, staging, **`Run-CyberlandDemo-Test.ps1`**.
