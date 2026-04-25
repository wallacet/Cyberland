# Cyberland.Demo.BrickBreaker

**Purpose:** many entities, chunk-parallel `ArenaLayoutSystem` / `WinLoseSystem`, and `TriggerResolveSystem` reading `TriggerEvents` from the engine after paddle/ball integration in the mod’s fixed chain. `Phase.Won` clears the board. `BrickInputSetup` registers default actions.

**Run:** enable in `manifest.json`, run **Cyberland.Host**.

**Teaches:** parallel `IParallelEarlyUpdate` / `IParallelFixedUpdate` as a stress pattern (not required for 60 bricks on weak CPUs), AABB side heuristic for brick bounces, separate win vs lose UI strings in `Content/Locale/en/brick.json`.

**Controls:** A/D or arrows, Space / LMB to launch, Enter / R to start rounds, Q to quit (common action).

See root `README.md` for staging and `input-bindings.json` behavior.
