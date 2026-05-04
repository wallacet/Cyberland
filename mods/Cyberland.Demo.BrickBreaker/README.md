# Cyberland.Demo.BrickBreaker

**Purpose:** many entities, chunk-parallel `ArenaLayoutSystem` / `WinLoseSystem` / `ReactivateSystem`, and `TriggerResolveSystem` reading `TriggerEvents` after paddle/ball integration in the mod’s fixed chain. `Phase.Won` clears the board. `InputSetup` registers default actions.

**Run:** enable in `manifest.json`, run **Cyberland.Host**.

**Viewport:** Simulation and camera layout use the fixed design canvas `Constants.CanvasWidth` / `CanvasHeight` (see `Constants.cs`). Viewport-space HUD and the FPS row use `ModLayoutViewport.VirtualSizeForPresentation` (same world-vs-presentation split as the HDR demo). Do not trust raw swapchain or renderer viewport sizes from parallel early systems before camera submission.

**Teaches:** query-driven late systems (sprite and `BitmapText` rows with honest `QuerySpec`s), parallel `IParallelEarlyUpdate` / `IParallelFixedUpdate` / `IParallelLateUpdate` where justified, AABB side heuristic for block bounces, separate win vs lose UI strings in `Content/Locale/en/brick.json`.

**Controls:** A/D or arrows, Space / LMB to launch, Enter / R to start rounds, Q to quit (common action).

See root `README.md` for staging and `input-bindings.json` behavior.

## Tags and components (high level)

Marker tags and session discovery live in `Components/Tags.cs`. Session singleton: `SessionTag` + `GameState` + `ArenaLightRuntime` (light target ids from cold start); input latch: `ControlTag` + `Control`. Life pips use `LifePipSlot` (index 0..2) with `Transform` + `Sprite`. Arena grid cells use `ArenaCellState` + `Cell` + `Sprite`. See `Tags.cs` for HUD and overlay tags.

## System registration order (`Mod.cs`)

Order matters for round state and win detection (round start and reactivation complete before `WinLoseSystem` in the same fixed pass).

Cold-start entities and lights are authored in **`SceneSetup.SetupSceneAsync`** (awaited from **`Mod.OnLoadAsync`** before any systems register).

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
| Fixed (parallel) | `cyberland.demo.brick/winlose` | `WinLoseSystem` |
| Late (singleton) | `cyberland.demo.brick/lights` | `LightsFillSystem` (session row: `SessionTag` + `GameState` + `ArenaLightRuntime`) |
| Late (parallel) | `cyberland.demo.brick/cell-sprites` | `CellSpriteSyncSystem` |
| Late (parallel) | `cyberland.demo.brick/background-sprite` | `BackgroundSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/paddle-sprite` | `PaddleSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/ball-sprite` | `BallSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/title-ui-sprite` | `TitleUiSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/game-over-panel-sprite` | `GameOverPanelSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/game-over-bar-sprite` | `GameOverBarSpriteSyncSystem` |
| Late (serial) | `cyberland.demo.brick/life-sprites` | `LifeSpriteSyncSystem` |
| Late (singleton) | `cyberland.demo.brick/hud-*` | BitmapText HUD systems (`HudTitleTextSystem`, …) |
| Late (singleton) | `cyberland.demo.brick/fps-hud` | `FpsHudSystem` |
