# Cyberland.Demo.FontTest

## Purpose

**Font validation** demo: engine **built-in** MSDF atlases plus a **mod-registered** family (**Jost**) with mod-shipped MSDF bakes under **`Content/Fonts/`**. Teaches **`IFontLibrary.RegisterFamilyFromVirtualPathsAsync`** and the **`LoadBakedMsdfAtlasAsync`** vs **`OnLoadAsync`** deadlock hazard.

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo fonttest
```

## Learning path

1. **`Mod.cs`** — **`RegisterJostFamilyAsync`** before scene; **fire-and-forget** **`LoadBakedMsdfAtlasAsync`** (do **not** await from **`OnLoadAsync`**).
2. **`FontTestFonts.cs`** — VFS paths and family id constants.
3. **`Content/Scenes/demo_fonttest.json`** + **`Content/Ui/fonttest_matrix.json`** — scene shell and font matrix HUD (`uiPath` on **`ui-document-root`**).
4. **`tools/FontTestUiJsonGen/`** — optional regenerator for the large matrix JSON.
4. **`Content/Fonts/Baked/*.manifest.json`** + PNG pages — how optional mod bakes layer on builtins.

## Features taught

- **`AssetManager`** + **`VirtualFileSystem`** for font file reads during registration.
- Large matrix of **`BuiltinFonts.BakedAtlasManifestPath`** entries kicked async without blocking **`ModLoader`**.
- **`ConfigureAwait(false)`** on I/O helpers (library-style mod code).

## Content

- **`Content/Fonts/Source/`** — TTF sources (not always required at runtime if bakes exist).
- **`Content/Fonts/Baked/`** — Jost MSDF manifests + pages.
- **`Content/Ui/fonttest_matrix.json`** — retained HUD grid (engine + Jost samples).

## Further reading

- **`mods/Cyberland.Demo.IdleGold/README.md`** — synchronous atlas preload pattern.
- **`.cursor/rules/cyberland-demo-mod-authoring.mdc`** (MSDF bootstrap section).
