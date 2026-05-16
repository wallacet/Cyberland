# Cyberland.Demo.Rts

## Purpose

Small **RTS-style** sample: **pannable/zoomable `Camera2D`**, one selectable **unit** with **move orders**, **deferred lighting** (ambient + child **point** light on the unit), **selection** frame sprites, and an **FPS HUD** row.

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo rts
```

## Learning path

1. **`Mod.cs`** — singleton chain: input → move → camera → selection → FPS HUD.
2. **`SceneSetup.cs`** — checkerboard **CPU texture** registration, camera entity (**`RtsCameraTag`**), background, **ambient**, **unit** + **`RtsUnitTag`**, selection sprites, session/FPS entities.
3. **`Systems/RtsInputSystem.cs`** — pan/zoom and order issuing.
4. **`Systems/RtsUnitMoveSystem.cs`** — steering the unit toward the clicked world point.
5. **`Systems/RtsCameraSystem.cs`** — viewport chase and zoom state on the camera row.
6. **`Systems/RtsSelectionFrameSystem.cs`**, **`RtsFpsHudSystem.cs`**.

## Features taught

- **`IRenderer.RegisterTextureRgba`** for procedurally generated art.
- **`Camera2D`** + custom zoom state component on the same entity as **`RtsCameraTag`**.
- **`AmbientLightSource`** / **`PointLightSource`** ECS components picked up by the engine’s stock deferred **lighting submit** systems (same path as other demos that author lights in **`SceneSetup`**—no custom **`Submit*Light`** calls required in mod phase code for the default rig).

## Content

- **`Content/`** — minimal (see folder); locale optional for HUD strings if added later.

## Further reading

- **`mods/Cyberland.Demo/README.md`** — HDR + lights overview.
- **`.cursor/rules/cyberland-world-screen-space.mdc`** — world vs viewport vs swapchain.
- **`.cursor/rules/cyberland-demo-mod-authoring.mdc`**.
