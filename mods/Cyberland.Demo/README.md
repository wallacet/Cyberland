# Cyberland.Demo (HDR tutorial)

**Purpose:** smallest engine-faithful sample: `Camera2D`, `Sprite` + `BitmapText`, lights, `PostProcessVolumeSource`, `GlobalPostProcessSource`, and mixed sequential + parallel systems.

**Run:** set `"disabled": false` in `manifest.json` (or stage manually), build **Cyberland.Host**, run the executable. Default key bindings are registered in `Mod.OnLoadAsync` via `DemoInputSetup` and `ModLoadContext.AddDefaultInputBinding`. Cold-start entities are authored in **`SceneSetup.SetupSceneAsync`** (awaited before systems register).

**Teaches:** `ModLayoutViewport` for virtual canvas size, F9 toggles `cyberland.demo/velocity-damp`, integrate-then-damp order in `Mod.cs` header, `ISingletonSystem` for single-row work (`IntegrateSystem`, `FpsDisplaySystem`, `HdrPostVolumeFillSystem`), `VelocityDampSystem` for a parallel `IParallelSystem` + `QueryChunks<Velocity>` example.

**Content:** `Content/Locale/en/demo_hdr.json` only; `content.release.manifest.json` has no required binary bundle for local dev.

See root `README.md` → *Reference examples* for file paths.
