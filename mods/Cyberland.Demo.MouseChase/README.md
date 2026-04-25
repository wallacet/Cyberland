# Cyberland.Demo.MouseChase

**Content layout:** the collectible uses `SpriteLocalizedAsset` with `Textures/Pickups/shard.png`. The VFS checks `Locale/<culture>/Textures/…` before the unqualified `Textures/…` path, so this mod ships the same `shard.png` under `Content/Textures/…` and under `Content/Locale/en/…` / `es/…` so a valid file wins over any other mod and locale resolution always finds a real PNG. Use a **fully opaque** PNG (alpha near 1): the G-buffer pass discards fragments with very low alpha, and fully transparent albedo is easy to read as a black or missing sample under deferred.

**Lighting:** the scene registers `AmbientLightSource` plus a broad `PointLightSource` so opaque sprites are visible in the deferred pass; without any lights, albedo in the G-buffer is multiplied by zero and opaque quads (player, pickup) go black even when semi-transparent WBOIT zones still look colored.

Playable tutorial demo for:

- mouse movement and primary click boost
- camera zoom while following action
- trigger enter/stay/exit gameplay events
- localized strings plus localized sprite assets

## Rules

- Move the courier toward the mouse cursor.
- Hold left mouse to burst toward the cursor.
- Complete tutorial objectives shown in the HUD.
- Reach target score, then enter the gate to win.
- You lose on health depletion or timer expiry.

## Controls

- Mouse move: steer courier
- Left mouse: burst speed
- Mouse wheel: camera zoom
- R / Enter: restart after win/loss
