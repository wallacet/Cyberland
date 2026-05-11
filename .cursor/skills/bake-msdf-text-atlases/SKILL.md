---
name: bake-msdf-text-atlases
description: >-
  Regenerate embedded builtin MSDF text atlas manifests and PNG pages using
  tools/Cyberland.MsdfAtlasBaker. Use when adding/changing UiSans or Mono baked
  atlas sizes in BuiltinFonts, after editing the baker’s Program.cs atlas list,
  or when glyphs should ship pre-baked instead of runtime MSDF fallback.
---

# Bake MSDF text atlases (Cyberland)

## What it does

**`tools/Cyberland.MsdfAtlasBaker`** rasterizes the Latin Extended glyph range (plus em dash `U+2014`) into packed **2048²** RGBA MSDF atlas pages, writes **`*.manifest.json`** + **`*.pageN.png`** under **`src/Cyberland.Engine/Rendering/Text/Baked/`**, matching names registered in **`BuiltinFonts.CreateBakedAtlasResourceRows`**.

**`Cyberland.Engine.csproj`** embeds **`Rendering\Text\Baked\*.json`** and **`*.png`** automatically—no csproj edit when adding new atlas files in that folder.

## When to run

- After editing **`Program.cs`** in **`Cyberland.MsdfAtlasBaker`** (add/remove **`BakeFamily(...)`** rows).
- After changing **`GlyphRasterizer.RasterRevision`** or MSDF parameters that invalidate old baked UVs (rebake everything).
- Before merging UI font size changes that should hit baked coverage at startup.

## Command (repository root)

```powershell
Set-Location <repoRoot>
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Generate-BakedMsdfAtlases.ps1
```

Optional output directory (default: **`src/Cyberland.Engine/Rendering/Text/Baked`**):

```powershell
.\scripts\Generate-BakedMsdfAtlases.ps1 -OutputDir "src/Cyberland.Engine/Rendering/Text/Baked"
```

Direct **`dotnet run`** (same as script):

```powershell
dotnet run --project tools/Cyberland.MsdfAtlasBaker/Cyberland.MsdfAtlasBaker.csproj -c Release -- "src/Cyberland.Engine/Rendering/Text/Baked"
```

Use **`-c Release`** for the baker so generation is faster.

## After baking

1. Register new atlases in **`src/Cyberland.Engine/Rendering/Text/BuiltinFonts.cs`** (`CreateBakedAtlasResourceRows`) if you added **`BakeFamily`** names.
2. Commit new/updated **`*.manifest.json`** and **`*.page*.png`** under **`Baked/`** (watch repo binary-size / pre-commit limits).
3. Run **`dotnet test tests/Cyberland.Engine.Tests/Cyberland.Engine.Tests.csproj -c Debug /p:CollectCoverage=true`**.

## Agent workflow

1. Edit baker **`BakeFamily`** list and **`BuiltinFonts`** rows together.
2. Run **`Generate-BakedMsdfAtlases.ps1`** from repo root; confirm console lines per atlas (glyph/page counts).
3. Build engine and run tests with coverage.
