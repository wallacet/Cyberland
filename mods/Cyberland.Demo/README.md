# Cyberland.Demo (HDR tutorial)

**Purpose:** smallest engine-faithful sample: `Camera2D`, `Sprite` + `BitmapText`, lights, `PostProcessVolumeSource`, `GlobalPostProcessSource`, and mixed sequential + parallel systems.

**Run:** set `"disabled": false` in `manifest.json` (or stage manually), build **Cyberland.Host**, run the executable. Default key bindings are registered in `Mod.OnLoad` via `DemoInputSetup` and `ModLoadContext.AddDefaultInputBinding`.

**Teaches:** `ModLayoutViewport` for virtual canvas size, F9 toggles `cyberland.demo/velocity-damp`, integrate-then-damp order in `Mod.cs` header, `TagQueryShowcaseSystem` for a tag-based chunk query.

**Content:** `Content/Locale/en/demo_hdr.json` only; `content.release.manifest.json` has no required binary bundle for local dev.

See root `README.md` → *Reference examples* for file paths.
