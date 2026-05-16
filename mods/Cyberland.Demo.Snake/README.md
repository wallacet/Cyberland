# Cyberland.Demo.Snake

## Purpose

Grid **Snake** with **`Session`** state, **`Tilemap`** background, preallocated segment **`Sprite`**s, and **`SnakeInputSetup`** for default keys. Shows fixed tick + visual sync split.

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo snake
```

## Learning path

1. **`Mod.cs`** — bootstrap/input/tick/tilemap/lights/visual registration order.
2. **`SceneSetup.cs`** — camera, tilemap entity, segment pool, HUD.
3. **`Systems/TickSystem.cs`** — fixed simulation step calling **`Session.Step`**.
4. **`Systems/TilemapLayoutSystem.cs`** — why the tilemap does not drive the snake body (background only).
5. **`Systems/VisualSyncSystem.cs`** — grid-aligned quads for head/body/food.
6. **`Components/`** — **`Session`**, **`Phase`**, constants.

## Features taught

- **`ModLayoutViewport.VirtualSizeForSimulation`** in fixed; **`VirtualSizeForPresentation`** in late.
- Full-board win path in **`Session.Step`** / **`SpawnFood`**.
- **`host.Tilemaps`** for playfield art vs ECS-driven snake sprites.

## Content

- **`Content/Locale/en/snake.json`**.

## Controls

Arrow keys; **Enter** / **R** to start; **Q** to quit (when bound in **`input-bindings.json`** or mod defaults).

## Further reading

- **`mods/README.md`** — demo index.
- **`.cursor/rules/cyberland-demo-mod-authoring.mdc`**.
