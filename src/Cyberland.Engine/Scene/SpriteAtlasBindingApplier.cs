using Cyberland.Engine.Assets;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Applies atlas regions, animations, and sheet frames onto <see cref="Sprite"/> components.
/// </summary>
public static class SpriteAtlasBindingApplier
{
    /// <summary>
    /// Writes albedo, UV, and half-extents from the binding's initial frame/region.
    /// </summary>
    public static void ApplyInitial(SpriteAtlas atlas, ref SpriteAtlasBinding binding, ref Sprite sprite, IRenderer renderer)
    {
        if (!string.IsNullOrWhiteSpace(binding.SheetName) &&
            atlas.TryGetSheet(binding.SheetName, out var sheet) &&
            atlas.TryGetRegion(sheet.RegionName, out var sheetRegion))
        {
            ApplyRegion(sheetRegion, ref sprite, frameIndex: 0, sheet);
            binding.ElapsedSeconds = 0f;
            return;
        }

        if (!string.IsNullOrWhiteSpace(binding.AnimationName) &&
            atlas.TryGetAnimation(binding.AnimationName, out var anim) &&
            anim.RegionNames.Length > 0 &&
            atlas.TryGetRegion(anim.RegionNames[0], out var animRegion))
        {
            ApplyRegion(animRegion, ref sprite);
            binding.ElapsedSeconds = 0f;
            return;
        }

        if (!string.IsNullOrWhiteSpace(binding.RegionName) &&
            atlas.TryGetRegion(binding.RegionName, out var region))
        {
            ApplyRegion(region, ref sprite);
            return;
        }

        sprite.AlbedoTextureId = renderer.MissingTextureId;
        sprite.UvRect = new Vector4D<float>(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// Advances animated bindings and updates <see cref="Sprite.UvRect"/> (and region swaps for frame-list clips).
    /// </summary>
    public static void ApplyAnimatedFrame(SpriteAtlas atlas, ref SpriteAtlasBinding binding, ref Sprite sprite, float deltaSeconds)
    {
        if (!string.IsNullOrWhiteSpace(binding.SheetName) &&
            atlas.TryGetSheet(binding.SheetName, out var sheet) &&
            sheet.SecondsPerFrame > 0f &&
            atlas.TryGetRegion(sheet.RegionName, out var sheetRegion))
        {
            binding.ElapsedSeconds += deltaSeconds;
            var frame = ComputeFrameIndex(binding.ElapsedSeconds, sheet.SecondsPerFrame, sheet.FrameCount, sheet.Loop);
            ApplyRegion(sheetRegion, ref sprite, frame, sheet);
            return;
        }

        if (!string.IsNullOrWhiteSpace(binding.AnimationName) &&
            atlas.TryGetAnimation(binding.AnimationName, out var anim) &&
            anim.SecondsPerFrame > 0f &&
            anim.RegionNames.Length > 0)
        {
            binding.ElapsedSeconds += deltaSeconds;
            var frame = ComputeFrameIndex(binding.ElapsedSeconds, anim.SecondsPerFrame, anim.RegionNames.Length, anim.Loop);
            if (atlas.TryGetRegion(anim.RegionNames[frame], out var frameRegion))
                ApplyRegion(frameRegion, ref sprite);
        }
    }

    /// <summary>True when the binding drives a time-varying clip.</summary>
    public static bool IsAnimated(in SpriteAtlasBinding binding) =>
        !string.IsNullOrWhiteSpace(binding.SheetName) || !string.IsNullOrWhiteSpace(binding.AnimationName);

    private static int ComputeFrameIndex(float elapsed, float secondsPerFrame, int frameCount, bool loop)
    {
        var frame = (int)MathF.Floor(elapsed / secondsPerFrame);
        if (loop)
            return (int)((frame % frameCount + frameCount) % frameCount);
        return Math.Clamp(frame, 0, frameCount - 1);
    }

    private static void ApplyRegion(
        SpriteAtlasRegion region,
        ref Sprite sprite,
        int frameIndex = 0,
        SpriteAtlasSheetClip? sheet = null)
    {
        sprite.AlbedoTextureId = region.PageTextureId;
        sprite.UvRect = sheet is not null
            ? SpriteAtlasManifestParser.SheetFrameUvRect(region.UvRect, sheet.Columns, frameIndex, sheet.FrameCount)
            : region.UvRect;
        sprite.HalfExtents = region.HalfExtentsWorld;
    }
}
