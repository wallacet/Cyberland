# Cyberland.Demo.WhackAMole

## Purpose

Minimal **clicker** sample: one active target square, score-on-click, and a short countdown round after the first hit. Fits in a **single gameplay `ISingletonSystem`** plus cold start in **`SceneSetup`**.

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo whackamole
```

## Learning path

1. **`Mod.cs`** — **`SeedHudMsdfAtlases`** uses **`LoadBakedMsdfAtlas`** (sync) so HUD sizes exist before frame 1; single **`RegisterSingleton`** for **`WhackAMoleGameSystem`**.
2. **`SceneSetup.cs`** — arena, cursor proxy, HUD **`BitmapText`** rows passed into the game system.
3. **`Systems/WhackAMoleGameSystem.cs`** — input read, spawn/jitter target, timer, score.

## Features taught

- **`RegisterDefaultBindings`** via **`WhackAMoleInputSetup`**.
- Keeping **`OnLoadAsync`** non-deadlocked while still guaranteeing glyph pages (sync load).
- Tight **`QuerySpec`** for one session row driving all gameplay.

## Content

- No shipped **`Content/`** tree yet; strings are authored in code / components. Add **`Content/Locale/`** when you grow localized copy.

## Further reading

- **`mods/Cyberland.Demo.MouseChase/README.md`** — larger retained-UI sample.
- **`.cursor/rules/cyberland-demo-mod-authoring.mdc`**.
