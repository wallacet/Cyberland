# Mods folder

Shipped assemblies under **`mods/`** are loaded like third-party mods: **`Cyberland.Host`** references **`Cyberland.Engine`** only; **`scripts/StageModsForHost.ps1`** copies each enabled mod into **`Mods/<Name>/`** beside the host after build or publish.

## Base campaign

| Folder | README | Notes |
|--------|--------|-------|
| **`Cyberland.Game/`** | — | **`cyberland.base`**: locale and future campaign **`Content/`**. Enabled in git. |

## Tutorial demo mods (`Cyberland.Demo*`)

Each demo is a **guided sample** for engine APIs. In git, **`manifest.json`** sets **`"disabled": true`** so normal runs load only the base game (pre-commit enforces this).

**Run one demo locally:** from repo root,

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo <name>
```

| `-Demo` | Folder | Per-mod README |
|---------|--------|----------------|
| **`hdr`** | **`Cyberland.Demo/`** | [README.md](Cyberland.Demo/README.md) |
| **`pong`** | **`Cyberland.Demo.Pong/`** | [README.md](Cyberland.Demo.Pong/README.md) |
| **`snake`** | **`Cyberland.Demo.Snake/`** | [README.md](Cyberland.Demo.Snake/README.md) |
| **`brick`** | **`Cyberland.Demo.BrickBreaker/`** | [README.md](Cyberland.Demo.BrickBreaker/README.md) |
| **`mousechase`** | **`Cyberland.Demo.MouseChase/`** | [README.md](Cyberland.Demo.MouseChase/README.md) |
| **`idlegold`** | **`Cyberland.Demo.IdleGold/`** | [README.md](Cyberland.Demo.IdleGold/README.md) |
| **`fonttest`** | **`Cyberland.Demo.FontTest/`** | [README.md](Cyberland.Demo.FontTest/README.md) |
| **`spritegallery`** | **`Cyberland.Demo.SpriteGallery/`** | [README.md](Cyberland.Demo.SpriteGallery/README.md) |
| **`whackamole`** | **`Cyberland.Demo.WhackAMole/`** | [README.md](Cyberland.Demo.WhackAMole/README.md) |
| **`rts`** | **`Cyberland.Demo.Rts/`** | [README.md](Cyberland.Demo.Rts/README.md) |

**Authoring contract for agents:** **`.cursor/rules/cyberland-demo-mod-authoring.mdc`**.

**Retained UI (FontTest, MouseChase, IdleGold, SpriteGallery):** **`Content/Ui/*.json`** + scene **`uiPath`** on **`ui-document-root`** — see **`.cursor/rules/cyberland-ui-json-authoring.mdc`**.

**Engine patterns (all mods):** **`.cursor/rules/cyberland-mod-host-architecture.mdc`**, **`.cursor/rules/cyberland-mod-patterns-hdr.mdc`**, **`.cursor/rules/cyberland-mod-system-lifecycle.mdc`**.
