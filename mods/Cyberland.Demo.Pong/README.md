# Cyberland.Demo.Pong

## Purpose

Single-session Pong on one **`State`** + **`Control`** row: explicit sprite/text handles from cold start, **`VisualSyncSystem`** tying simulation to draws, and **`PongInputSetup`** for mod-owned default bindings.

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo pong
```

Or enable **`mods/Cyberland.Demo.Pong/manifest.json`** temporarily and run **Cyberland.Host**.

## Learning path

1. **`Mod.cs`** — locale merge, atlas kickoff, singleton registration chain.
2. **`SceneSetup.cs`** — session entity, arena visuals, HUD **`BitmapText`** ids.
3. **`Systems/SimulationSystem.cs`** — circle-vs-rectangle paddle hits (not engine **`TriggerSystem`** in the same fixed ordering — see engine remarks on **`TriggerSystem`** vs mod fixed).
4. **`Systems/VisualSyncSystem.cs`** — interpolation with **`GameHostServices.FixedAccumulatorSeconds`**.
5. **`Systems/LightsFillSystem.cs`** — deferred lights from session state.

## Features taught

- **`ModLayoutViewport`** in simulation and in late-phase lights/visuals.
- **`ISingletonSystem`** drivers on the session archetype (**`State`**, **`Control`**).
- Why Pong avoids **`Trigger`** for paddle hits (ordering vs **`cyberland.engine/trigger`**).

## Content

- **`Content/Locale/en/pong.json`** — UI strings.

## Further reading

- **`mods/Cyberland.Demo/README.md`** — HDR baseline.
- **`.cursor/rules/cyberland-demo-mod-authoring.mdc`**, **`.cursor/rules/cyberland-mod-system-lifecycle.mdc`**.
