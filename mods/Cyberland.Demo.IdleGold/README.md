# Cyberland.Demo.IdleGold

## Purpose

**Idle / incremental** UI sample: passive income, purchases through **retained UI** commands, and a clear **ECS session row** pattern. Shows wiring **`GameHostServices.UiCommandDispatcher`** to mod code during **`OnLoadAsync`**.

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo idlegold
```

## Learning path

1. **`Mod.cs`** — synchronous **`LoadBakedMsdfAtlas`** loop (first-frame glyphs; see **cyberland-demo-mod-authoring**), **`UiCommandDispatcher`** assignment, system registration.
2. **`SceneSetup.cs`** — HUD document, session entity, **`DocumentRefs`** / **`SceneBootstrap`** returned to **`Mod`**.
3. **`UiGameCommand.cs`**, **`UiCommandHandler.cs`** — typed UI commands and dispatch into the session row.
4. **`Systems/SimulationSystem.cs`** — economy tick on the session singleton.
5. **`Systems/HudBindSystem.cs`** — binding localized strings into HUD elements.

## Features taught

- **`ILocalizedContent.MergeStringTable`** for **`idlegold.json`**.
- **`BuiltinFonts`** baked paths loaded **synchronously** so the first UI frame does not pay runtime MSDF fallback.
- **`RegisterSingleton`** for simulation + HUD bind systems.

## Content

- **`Content/Locale/en/idlegold.json`** — strings for HUD and buttons.

## Further reading

- **`mods/Cyberland.Demo.FontTest/README.md`** — contrast with async atlas policy.
- **`.cursor/rules/cyberland-demo-mod-authoring.mdc`**, **`cyberland-mod-host-architecture.mdc`** (**`GameHostServices`** table).
