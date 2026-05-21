# Cyberland.Demo.Rts

## Purpose

Small **RTS-style** sample: **pannable/zoomable `Camera2D`**, **~10 controllable units**, **marquee box select**, **formation move orders** with **circle separation** (no overlap), deferred ambient lighting, selection frame, and an **FPS HUD** row.

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo rts
```

## Controls

- **Left-click** on a unit — select only that unit; click empty ground to clear.
- **Left-drag** — box-select all units whose bounds intersect the marquee.
- **Right-click** (with selection) — move selected units to a **grid formation** centered on the click.
- **WASD / arrows / edge scroll** — pan camera; **mouse wheel** — zoom.
- **Escape** — quit (via `cyberland.common/quit` binding).

Shift-click additive selection is not implemented.

## Learning path

1. **`Mod.cs`** — singleton chain: input → unit-move (serial) → camera → selection → FPS HUD.
2. **`Mod.RtsPlayfield.cs`** — checkerboard texture, spawn 10 units, session/selection-bar wiring.
3. **`RtsFormation.cs`**, **`RtsUnitCollision.cs`** — formation slots and separation helpers.
4. **`Systems/RtsInputSystem.cs`** — box/click select and move orders.
5. **`Systems/RtsUnitMoveSystem.cs`** — steering + global separation pass.
6. **`Systems/RtsCameraSystem.cs`**, **`RtsSelectionFrameSystem.cs`**, **`RtsFpsHudSystem.cs`**.

## Features taught

- **`IRenderer.RegisterTextureRgba`** for procedurally generated art.
- **`Camera2D`** + **`PresentationViewportSizeWorld`** for HUD-stable zoom.
- Per-unit **`RtsUnitState`** (selection + independent move targets).
- **`ISystem`** + **`RegisterSerial`** for deterministic multi-entity simulation at small scale.
- **`AmbientLightSource`** from scene JSON (stock deferred lighting path).

## Content

- **`Content/Scenes/demo_rts.json`** — camera, background, ambient, selection bars, session, HUD (units spawned in code).

## Further reading

- **`mods/Cyberland.Demo/README.md`** — HDR + lights overview.
- **`.cursor/rules/cyberland-world-screen-space.mdc`** — world vs viewport vs swapchain.
- **`.cursor/rules/cyberland-demo-mod-authoring.mdc`**.
