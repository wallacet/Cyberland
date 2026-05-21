using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts;

/// <summary>Playfield clamp for <see cref="Camera2D"/> center so letterboxed viewports stay inside the RTS map.</summary>
public static class RtsCameraBounds
{
    /// <summary>Clamps a camera center in world space (+Y up) for the given virtual viewport size.</summary>
    public static void ClampCenter(ref Transform tf, int viewportW, int viewportH)
    {
        var play = RtsConstants.PlaySize;
        var halfW = viewportW * 0.5f;
        var halfH = viewportH * 0.5f;
        var p = tf.WorldPosition;
        float cx = p.X;
        float cy = p.Y;

        var minCx = halfW;
        var maxCx = play - halfW;
        if (minCx > maxCx)
            cx = play * 0.5f;
        else
            cx = Math.Clamp(cx, minCx, maxCx);

        var minCy = halfH;
        var maxCy = play - halfH;
        if (minCy > maxCy)
            cy = play * 0.5f;
        else
            cy = Math.Clamp(cy, minCy, maxCy);

        tf.WorldPosition = new Vector2D<float>(cx, cy);
    }

    /// <summary>Clamps a world position as if it were a camera center (used before queueing focus).</summary>
    public static Vector2D<float> ClampCenterPosition(Vector2D<float> worldCenter, int viewportW, int viewportH)
    {
        var tf = Transform.Identity;
        tf.WorldPosition = worldCenter;
        ClampCenter(ref tf, viewportW, viewportH);
        return tf.WorldPosition;
    }
}
