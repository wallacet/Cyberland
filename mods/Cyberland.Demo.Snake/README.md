# Cyberland.Demo.Snake

**Purpose:** grid game with `Session` state, `Tilemap` background, preallocated segment sprites, and `SnakeInputSetup` for mod-owned default keys.

**Run:** enable the mod in `manifest.json` (`disabled: false`) and run **Cyberland.Host**.

**Teaches:** fixed tick loop in `TickSystem`, `ModLayoutViewport` (`VirtualSizeForSimulation` in fixed, `VirtualSizeForPresentation` in late), full-board win in `Session.Step` / `SpawnFood`, and why the tilemap does not drive the snake (see `TilemapLayoutSystem` summary).

**Controls:** Arrow keys, Enter / R to start, Q to quit (when bound in `input-bindings.json` or defaults).

See root `README.md` for the mod pipeline and staging.
