# Cyberland.Demo.SpriteGallery

## Purpose

**Visual atlas/texture matrix** demo: each labeled row exercises one sprite or UI texture feature from the engine expansion—static atlas regions, frame-list animations, uniform-grid sheet clips, locale-aware atlas overlays, localized single-PNG sprites, intentional missing-texture fallbacks, and a 9-slice HUD panel. Complements the single atlas binding in [MouseChase](../Cyberland.Demo.MouseChase/README.md).

## Prerequisites

Binary PNGs are **not** in git. Before the first run:

```powershell
.\scripts\Sync-CyberlandAssets.ps1
```

This fetches `content.release.manifest.json` from GitHub Releases into `Content/`. Without assets, most preview rows show the magenta/green checkerboard (`MissingTextureId`).

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo spritegallery
```

Compare locale overlay rows D vs E with German:

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo spritegallery -- --lang=de
```

Spanish HUD strings:

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo spritegallery -- --lang=es
```

## Learning path

1. **`Mod.cs`** — mount content, merge locale strings, sync MSDF + sprite atlas preload, spawn `Content/Scenes/spritegallery.json`, register `HudUiSystem`.
2. **`Mod.UiBind.cs`** + **`HudDocumentRefs`** — resolve retained HUD nodes after spawn (`hud.fps` only; static copy uses `locKey` in JSON).
3. **`Systems/HudUiSystem.cs`** — FPS-only updates (MouseChase pattern).
4. **`SceneComponentDeserializers.cs`** — mod marker components for scene JSON.
5. **`LoadBuiltinFonts`** — sync `LoadBakedMsdfAtlas` (IdleGold pattern); avoid `bold: true` on builtin UiSans without Bold TTF.
6. **`Content/Scenes/spritegallery.json`** — rows A–H: `sprite-atlas-binding`, `sprite-localized-asset`, intentional failure cases. World-space `BitmapText` labels vs viewport HUD (see **cyberland-world-screen-space**).
7. **`Content/Ui/spritegallery_hud.json`** — header band with 9-slice panel (`nineSlice` from manifest, not JSON override).
8. Engine (no mod registration): `SpriteAtlasAnimationSystem` (early), `SpriteAtlasBindingSystem` + `SpriteLocalizedAssetSystem` (late serial).

## Features taught

- **`SpriteAtlasBinding`** — static `region`, `animation`, or `sheet` clip keys; binding precedence sheet &gt; animation &gt; region.
- **`localeInvariant`** — row E keeps base manifest; row D follows active locale overlay.
- **`SpriteLocalizedAsset`** — row F loads `Textures/Gallery/icon_static.png` via VFS (DE locale PNG override under `Locale/de/Textures/Gallery/`).
- **`MissingTextureId`** — row G (missing atlas manifest), row H (missing PNG path), HUD `sourceTexture: "missing"`.
- **9-slice UI** — `ui_panel.atlas.json` `nineSlice` on `panel_bg`; HUD image references manifest region only.
- **`TextureSourceResolver`** — atlas manifest `#region` references and builtins (`white`, `missing`) in UI JSON.

## Content

| Path | Role |
|------|------|
| `content.release.manifest.json` | GitHub Release zip + sha256 for `Sync-CyberlandAssets.ps1` |
| `Content/Textures/Source/Gallery/` | Source PNGs for atlas bake |
| `Content/Textures/Source/UiPanel/` | `panel_bg.png` for UI atlas |
| `Content/Textures/Atlases/gallery.atlas.json` | Main gallery atlas + `animations` / `sheets` |
| `Content/Textures/Atlases/ui_panel.atlas.json` | Panel region with manifest `nineSlice` |
| `Content/Textures/Gallery/icon_static.png` | Standalone PNG for row F |
| `Content/Locale/de/Textures/Atlases/` | DE atlas overlay + green-tinted `gallery.page0.png` |
| `Content/Locale/de/Textures/Gallery/icon_static.png` | Green-tinted row F contrast PNG |
| `Content/Locale/en|de|es/sprite_gallery.json` | Row labels and HUD strings |

### Regenerating atlases

```powershell
.\scripts\Generate-SpriteAtlases.ps1 `
  -InputFolder "mods\Cyberland.Demo.SpriteGallery\Content\Textures\Source\Gallery" `
  -OutputManifest "mods\Cyberland.Demo.SpriteGallery\Content\Textures\Atlases\gallery.atlas.json"
```

After baking, re-add `animations` and `sheets` blocks to `gallery.atlas.json` and verify `nineSlice` on `ui_panel.atlas.json`. The baker tool targets **net9.0**; use a matching SDK or bake manually.

### Publishing release assets (maintainers)

```powershell
.\scripts\Package-SpriteGallery-Assets.ps1
```

1. Upload `artifacts/dist/cyberland.demo.spritegallery.content.v0.1.0.zip` to the GitHub release tag in `content.release.manifest.json`.
2. Set `sha256` in the manifest to the digest printed by the script.
3. For a new DE row-F icon tint: `.\scripts\Tint-SpriteGalleryDeIcon.ps1` before packaging.

## Verification

1. `.\scripts\Sync-CyberlandAssets.ps1` (after manifest is published).
2. Rows A–F display colored sprites; B/C animate; F shows the gold icon PNG.
3. Rows **G** and **H** show the checkerboard (`MissingTextureId`).
4. HUD title/FPS/locale hint inside the gray 9-slice band; gallery grid below; window resize stretches header.
5. With `--lang=de`: row D green blink vs E red/blue; German HUD strings; row F green-tinted PNG.
6. With `--lang=es`: Spanish HUD strings; row D still uses DE atlas overlay only when culture is `de`.

## Further reading

- **`mods/Cyberland.Demo.MouseChase/README.md`** — gameplay demo with a single atlas binding.
- **`.cursor/rules/cyberland-sprite-atlas-authoring.mdc`** — atlas manifest schema and bake workflow.
- **`.cursor/rules/cyberland-ui-json-authoring.mdc`** — retained HUD JSON authoring.
- **`.cursor/rules/cyberland-world-screen-space.mdc`** — world vs viewport coordinate spaces.
