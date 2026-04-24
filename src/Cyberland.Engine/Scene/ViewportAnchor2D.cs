using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Declarative placement of an entity's <see cref="Transform"/> relative to the swapchain rectangle, optionally syncing a fullscreen <see cref="Sprite"/>.
/// </summary>
/// <remarks>
/// Pair <see cref="CoordinateSpace.ScreenSpace"/> with <see cref="BitmapText"/> for HUD rows.
/// Use <see cref="CoordinateSpace.WorldSpace"/> for world sprites (backgrounds, strips) drawn by <see cref="Systems.SpriteRenderSystem"/>.
/// Applied each frame by <see cref="Systems.ViewportAnchorSystem"/>.
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> exists
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </remarks>
[RequiresComponent<Transform>]
public struct ViewportAnchor2D : IComponent
{
    /// <summary>When false, <see cref="Systems.ViewportAnchorSystem"/> skips the entity.</summary>
    public bool Active;

    /// <summary>Whether to write world (+Y up) or screen (+Y down) positions.</summary>
    public CoordinateSpace ContentSpace;

    /// <summary>Which corner or edge the offsets are relative to.</summary>
    public ViewportAnchorPreset Anchor;

    /// <summary>First horizontal inset (pixels): from left for left anchors, from right for right anchors.</summary>
    public float OffsetX;

    /// <summary>First vertical inset (pixels): from top for top anchors, from bottom for bottom anchors.</summary>
    public float OffsetY;

    /// <summary>When true, sets <see cref="Sprite.HalfExtents"/> to half the framebuffer size (full-screen quad centered).</summary>
    public bool SyncSpriteHalfExtentsToViewport;
}
