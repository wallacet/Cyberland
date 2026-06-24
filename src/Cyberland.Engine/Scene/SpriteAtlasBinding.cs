using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Binds a sprite to a named region, animation, or sheet inside a localized sprite atlas manifest.
/// </summary>
/// <remarks>
/// <para>Resolved by <see cref="Systems.SpriteAtlasBindingSystem"/> into <see cref="Sprite"/> texture ids and UV rects; animated clips are advanced by <see cref="Systems.SpriteAtlasAnimationSystem"/> after binding is applied.</para>
/// <para><b>Binding precedence:</b> <see cref="SheetName"/> &gt; <see cref="AnimationName"/> &gt; <see cref="RegionName"/> (first non-empty wins).</para>
/// <para><b>Locale:</b> author canonical manifest paths only (e.g. <c>Textures/Atlases/game.atlas.json</c>); locale overlays live under <c>Locale/&lt;culture&gt;/Textures/Atlases/</c>. When <see cref="LocaleInvariant"/> is true, overlays are skipped and the base manifest is loaded.</para>
/// </remarks>
[RequiresComponent<Sprite>]
public struct SpriteAtlasBinding : IComponent
{
    /// <summary>Canonical manifest path inside mod content.</summary>
    public string CanonicalManifestPath;

    /// <summary>Static region name when not using <see cref="AnimationName"/> or <see cref="SheetName"/>.</summary>
    public string RegionName;

    /// <summary>Named frame-list animation from the manifest.</summary>
    public string AnimationName;

    /// <summary>Named uniform-grid sheet clip from the manifest.</summary>
    public string SheetName;

    /// <summary>Generation incremented to request reload (dev hot-reload or locale asset refresh).</summary>
    public int ReloadGeneration;

    /// <summary>Last generation resolved into <see cref="Sprite"/> fields.</summary>
    public int LoadedGeneration;

    /// <summary>When true, skip <c>Locale/…</c> overlays and load the base manifest only.</summary>
    public bool LocaleInvariant;

    /// <summary>Accumulated playback time for animation/sheet clips.</summary>
    public float ElapsedSeconds;
}
