# Cyberland.Demo.Rts

## Purpose

Small **RTS-style** sample: **pannable/zoomable `Camera2D`**, **~10 controllable units**, **control groups (1–0)**, **marquee box select**, **formation move orders** with **circle separation** (no overlap), deferred ambient lighting, selection frame, and an **FPS HUD** row.

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo rts
```

## Controls

### Selection (mouse)

- **Left-click** on a unit — select only that unit; click empty ground to clear.
- **Left-drag** — box-select (replace selection).
- **Shift + click / box** — add to selection.
- **Ctrl + click / box** — remove from selection.
- **Right-click** (with selection) — move selected units to a **grid formation** centered on the click.

### Control groups (keyboard)

| Input | Effect |
|--------|--------|
| **1–9, 0** | Recall group (replace selection); **no camera move** on first recall |
| **Same key again** (group already fully selected) | **Center camera** on group centroid |
| **Double-tap** same key (within ~0.35s) | Select group members **visible in the camera view** |
| **Ctrl + 1–9, 0** | Assign current selection to group |
| **Shift + 1–9, 0** | Add group to selection |

### Camera

- **WASD / arrows / edge scroll** — pan
- **Mouse wheel** — zoom
- **Escape** — quit (`cyberland.common/quit`)

## Learning path

1. **`Mod.cs`** — singleton chain: input → unit-move (serial) → camera → selection → FPS HUD.
2. **`Mod.RtsPlayfield.cs`** — checkerboard, spawn 10 units, session/groups wiring.
3. **`Components/RtsControlGroups.cs`**, **`RtsControlGroupLogic`** — group storage and recall.
4. **`RtsCameraBounds.cs`** — playfield clamp shared with camera focus.
5. **`Systems/RtsInputSystem.cs`** — modifiers, hotkeys, orders.
6. **`Systems/RtsUnitMoveSystem.cs`**, **`Systems/RtsCameraSystem.cs`**, selection frame, FPS HUD.

## Features taught

- **`IRenderer.RegisterTextureRgba`** for procedurally generated art.
- **`Camera2D`** + **`PresentationViewportSizeWorld`** for HUD-stable zoom.
- Per-unit **`RtsUnitState`** and session **`RtsControlGroups`**.
- **`CameraProjection.WorldToViewportPixel`** for visible-unit selection.
- **`ISystem`** + **`RegisterSerial`** for deterministic multi-entity movement.

## Content

- **`Content/Scenes/demo_rts.json`** — camera, background, ambient, selection bars, session, HUD (units spawned in code).

## Further reading

- **`mods/Cyberland.Demo/README.md`** — HDR + lights overview.
- **`.cursor/rules/cyberland-world-screen-space.mdc`** — world vs viewport vs swapchain.
- **`.cursor/rules/cyberland-demo-mod-authoring.mdc`**.
