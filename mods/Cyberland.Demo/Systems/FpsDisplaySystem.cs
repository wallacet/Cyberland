using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// Late update for the FPS HUD row: writes viewport-space position and refreshed text each frame after simulation/post work.
/// </summary>
/// <remarks>
/// Uses <see cref="GameHostServices.LastPresentDeltaSeconds"/> when present so the average tracks actual GPU/frame pacing;
/// falls back to <paramref name="deltaSeconds"/> only when the host has not recorded a present yet (cold start).
/// Presentation size (<see cref="ModLayoutViewport.VirtualSizeForPresentation"/>) differs from simulation size when letterboxing
/// or DPI scaling applies—HUD placement should follow presentation pixels for readability.
/// </remarks>
public sealed class FpsDisplaySystem : ISystem, ILateUpdate
{
    /// <inheritdoc />
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HudFpsTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private readonly FpsMovingAverage _fps = new(FpsMovingAverage.DefaultWindowSeconds);

    /// <summary>Creates the system.</summary>
    public FpsDisplaySystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query) => _ = (world, query);

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        var r = _host.Renderer!;

        var frame = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fps.AddFrameDeltaSeconds(frame);
        var label = _fps.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
        var fb = ModLayoutViewport.VirtualSizeForPresentation(r);
        foreach (var chunk in query)
        {
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var t = ref chunk.Column<Transform>()[i];
                // Bottom-right padding in presentation pixels; BitmapText uses viewport space on this entity.
                t.LocalPosition = new Vector2D<float>(fb.X - 120f, fb.Y - 26f);
                ref var bt = ref chunk.Column<BitmapText>()[i];
                bt.Visible = true;
                bt.Content = label;
            }
        }
    }
}
