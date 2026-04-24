using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// 2D scene camera component. Pairs with <see cref="Transform"/>: the transform's world position and rotation
/// place the camera in the world, while <see cref="ViewportSizeWorld"/> defines the camera's virtual canvas in
/// world pixels (independent of the physical window size).
/// </summary>
/// <remarks>
/// <para>
/// Multiple cameras can exist in a scene; each frame the renderer selects the highest-<see cref="Priority"/>
/// entry with <see cref="Enabled"/> = <c>true</c>. The physical window letterboxes / pillarboxes the active
/// camera's viewport (no distortion, no extra world shown).
/// </para>
/// <para>
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures
/// <see cref="Transform"/> exists (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </para>
/// </remarks>
[RequiresComponent<Transform>]
public struct Camera2D : IComponent
{
    /// <summary>When <c>false</c>, <see cref="Systems.CameraSubmitSystem"/> skips the entity.</summary>
    public bool Enabled;

    /// <summary>Highest enabled <see cref="Priority"/> wins the frame; ties are broken by submit order.</summary>
    public int Priority;

    /// <summary>Virtual viewport size in world pixels (width, height); must be positive.</summary>
    public Vector2D<int> ViewportSizeWorld;

    /// <summary>Scene clear / pillar-letterbox bar color (linear RGBA).</summary>
    public Vector4D<float> BackgroundColor;

    /// <summary>Builds a camera with standard defaults (enabled, priority 0, dark bluish background).</summary>
    /// <param name="viewportSizeWorld">Virtual canvas size in world pixels.</param>
    public static Camera2D Create(Vector2D<int> viewportSizeWorld) => new()
    {
        Enabled = true,
        Priority = 0,
        ViewportSizeWorld = viewportSizeWorld,
        BackgroundColor = new Vector4D<float>(0.02f, 0.02f, 0.06f, 1f)
    };
}
