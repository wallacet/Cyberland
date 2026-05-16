# Cyberland.Demo.MouseChase

## Purpose

Tutorial game: mouse steering, burst click, **camera zoom** while following, **trigger** enter/stay/exit, **localized** strings + **localized sprite** assets, and **retained HUD** document updates.

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo mousechase
```

## Learning path

1. **`Mod.cs`** — registration: input → reset → movement → zoom → **serial** **`TriggerResolveSystem`** → round state → **`HudUiSystem`**.
2. **`SceneSetup.cs`** — player, pickup, gate, HUD document refs, lights.
3. **`Systems/PlayerMovementSystem.cs`**, **`CameraZoomSystem.cs`** — follow and zoom.
4. **`Systems/TriggerResolveSystem.cs`** — **`RegisterSerial`** over trigger chunks.
5. **`Systems/HudUiSystem.cs`**, **`HudDocumentRefs.cs`** — retained UI wiring.

## Features taught

- **`SpriteLocalizedAsset`** with **`Textures/Pickups/shard.png`** under both **`Content/Textures/…`** and **`Content/Locale/<culture>/Textures/…`** so locale resolution always finds a real PNG. Use a **fully opaque** PNG: very low alpha fragments are discarded in the G-buffer pass.
- **`AmbientLightSource`** + broad **`PointLightSource`** so opaque deferred sprites are visible.
- **`ILocalizedContent`** string tables (**`mouse_chase.json`**, **`Content/Locale/es/`**).

## Content

- **`Content/Locale/en/mouse_chase.json`**, **`Content/Locale/es/mouse_chase.json`**.
- **`Content/Textures/`**, **`Content/Locale/.../Textures/`** — pickup shard art.

## Rules (gameplay)

- Move the courier toward the cursor; hold **LMB** to burst.
- Complete HUD objectives; reach the score target and enter the gate to win.
- Lose on health depletion or timer expiry.

## Controls

- **Mouse move** — steer
- **Left mouse** — burst
- **Mouse wheel** — camera zoom
- **R** / **Enter** — restart after win/loss

## Further reading

- **`mods/README.md`**, **`.cursor/rules/cyberland-demo-mod-authoring.mdc`**.
