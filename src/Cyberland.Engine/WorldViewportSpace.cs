using Silk.NET.Maths;

namespace Cyberland.Engine;

/// <summary>
/// Helpers for flipping between a <b>+Y up</b> coordinate frame (gameplay world / viewport-anchored world math)
/// and a <b>+Y down</b> pixel frame (viewport-style top-left origin) inside a single canvas of a given size.
/// </summary>
/// <remarks>
/// <para>
/// Under the camera model, the canvas is either the active camera's <b>virtual viewport</b>
/// (<see cref="Rendering.IRenderer.ActiveCameraViewportSize"/>) for HUD / UI math, or the <b>world box</b> that
/// the camera views for gameplay layout. The math is identical: <c>y → canvasSize.Y - y</c>.
/// </para>
/// <para>
/// Gameplay code should keep <see cref="Rendering.SpriteDrawRequest.CenterWorld"/> in <b>world space</b> (+Y up,
/// <see cref="Scene.CoordinateSpace.WorldSpace"/>); the renderer applies the active camera transform and
/// letterbox mapping internally. Use these helpers when translating between viewport-style pixel math (e.g. "top
/// inset Y" = pixels from the top) and world / viewport-anchored +Y up math.
/// </para>
/// </remarks>
public static class WorldViewportSpace
{
    /// <summary>Converts a +Y up center to the corresponding +Y down pixel within a canvas of <paramref name="canvasSize"/>.</summary>
    /// <param name="worldCenter">Position in pixels with Y increasing upward.</param>
    /// <param name="canvasSize">Canvas size in pixels (either active camera viewport or world box extent).</param>
    public static Vector2D<float> WorldCenterToViewportPixel(Vector2D<float> worldCenter, Vector2D<int> canvasSize) =>
        new(worldCenter.X, canvasSize.Y - worldCenter.Y);

    /// <summary>Flips vertical velocity between +Y up and +Y down frames (X unchanged).</summary>
    /// <param name="worldVelocity">Velocity per second (Y positive = up).</param>
    public static Vector2D<float> WorldVelocityToViewportVelocity(Vector2D<float> worldVelocity) =>
        new(worldVelocity.X, -worldVelocity.Y);

    /// <summary>
    /// Inverse of <see cref="WorldCenterToViewportPixel"/>: maps a +Y down pixel (top-left origin) back to +Y up
    /// coordinates within a canvas of <paramref name="canvasSize"/>.
    /// </summary>
    /// <param name="viewportCenter">Pixel coordinates with origin top-left.</param>
    /// <param name="canvasSize">Canvas size in pixels.</param>
    public static Vector2D<float> ViewportPixelToWorldCenter(Vector2D<float> viewportCenter, Vector2D<int> canvasSize) =>
        new(viewportCenter.X, canvasSize.Y - viewportCenter.Y);
}
