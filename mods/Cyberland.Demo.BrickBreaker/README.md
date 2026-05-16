# Cyberland.Demo.BrickBreaker

## Purpose

**Breakout**-style sample: many entities, chunk-parallel **`ArenaLayoutSystem`** / **`ReactivateSystem`**, **`WinLoseSystem`**, and **`TriggerResolveSystem`** consuming **`TriggerEvents`** after paddle/ball integration in the mod’s **fixed** chain. **`InputSetup`** registers default actions.

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo brick
```

## Learning path

1. **`Mod.cs`** — full registration table (order matters for round state and win detection).
2. **`SceneSetup.cs`** — arena grid, session row, lights, HUD entities.
3. **`Systems/ArenaLayoutSystem.cs`**, **`ReactivateSystem.cs`** — parallel early/fixed layout work.
4. **`Systems/WinLoseSystem.cs`** — singleton win/lose detection after reactivation.
5. **`Systems/CellSpriteSyncSystem.cs`** (and related `brick/*` late systems) — query-driven sprite sync.

## Features taught

- **`Constants.CanvasWidth`** / **`CanvasHeight`** as the design canvas; **`ModLayoutViewport.VirtualSizeForPresentation`** for HUD.
- Honest **`IParallelSystem`** where chunks partition work; serial **`TriggerResolveSystem`** when reading trigger chunks.
- AABB side heuristic for brick bounces; separate win vs lose strings in locale JSON.

## Content

- **`Content/Locale/en/brick.json`**.

## Tags and components (high level)

Marker tags and session discovery live in **`Components/Tags.cs`**. Session singleton: **`SessionTag`** + **`GameState`** + **`ArenaLightRuntime`**; input: **`ControlTag`** + **`Control`**. Life pips use **`LifePipSlot`** with **`Transform`** + **`Sprite`**. Arena cells use **`ArenaCellState`** + **`Cell`** + **`Sprite`**.

## System registration order

Cold start lives in **`SceneSetup.SetupSceneAsync`** (awaited before **`Register*`**).

| Phase | Id | System |
|------|-----|--------|
| Early (singleton) | `cyberland.demo.brick/input` | `InputSystem` |
| Early (parallel) | `cyberland.demo.brick/layout` | `ArenaLayoutSystem` |
| Fixed (singleton) | `cyberland.demo.brick/round-start` | `RoundStartSystem` |
| Fixed (parallel) | `cyberland.demo.brick/brick-reactivate` | `ReactivateSystem` |
| Fixed (singleton) | `cyberland.demo.brick/paddle-move` | `PaddleMoveSystem` |
| Fixed (singleton) | `cyberland.demo.brick/ball-launch` | `BallLaunchSystem` |
| Fixed (singleton) | `cyberland.demo.brick/ball-integrate` | `BallIntegrateSystem` |
| Fixed (singleton) | `cyberland.demo.brick/trigger-resolve` | `TriggerResolveSystem` |
| Fixed (singleton) | `cyberland.demo.brick/winlose` | `WinLoseSystem` |
| Late (singleton) | `cyberland.demo.brick/lights` | `LightsFillSystem` |
| Late (parallel) | `cyberland.demo.brick/cell-sprites` | `CellSpriteSyncSystem` |
| Late (parallel) | `cyberland.demo.brick/background-sprite` | `BackgroundSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/paddle-sprite` | `PaddleSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/ball-sprite` | `BallSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/title-ui-sprite` | `TitleUiSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/game-over-panel-sprite` | `GameOverPanelSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/game-over-bar-sprite` | `GameOverBarSpriteSyncSystem` |
| Late (serial) | `cyberland.demo.brick/life-sprites` | `LifeSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/hud-*` | BitmapText HUD systems |
| Late (singleton) | `cyberland.demo.brick/fps-hud` | `FpsHudSystem` |

## Controls

**A/D** or arrows; **Space** / **LMB** launch; **Enter** / **R** start rounds; **Q** quit.

## Further reading

- Root **`README.md`** — staging and **`input-bindings.json`**.
- **`.cursor/rules/cyberland-demo-mod-authoring.mdc`**.
