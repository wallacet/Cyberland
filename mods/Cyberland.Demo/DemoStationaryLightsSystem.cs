using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// Submits fixed world lights each frame. The renderer packs one ambient, directional, point, and spot into the
/// lighting UBO (see <c>sprite_lit.frag.glsl</c>); this demo uses all four for a simple multi-pool look.
/// </summary>
internal sealed class DemoStationaryLightsSystem : ISystem
{
    private readonly GameHostServices _host;

    public DemoStationaryLightsSystem(GameHostServices host) =>
        _host = host;

    public void OnUpdate(World world, float deltaSeconds)
    {
        _ = world;
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        var fb = r.SwapchainPixelSize;
        var w = fb.X;
        var h = fb.Y;
        if (w <= 0 || h <= 0)
            return;

        // Low fill so normals on the flat sprites still read; keeps the scene off pure black.
        r.SubmitAmbientLight(new AmbientLight
        {
            Color = new Vector3D<float>(0.38f, 0.4f, 0.48f),
            Intensity = 0.11f
        });

        r.SubmitDirectionalLight(new DirectionalLight
        {
            DirectionWorld = new Vector2D<float>(0.4f, -0.55f),
            Color = new Vector3D<float>(0.5f, 0.48f, 0.44f),
            Intensity = 0.14f,
            CastsShadow = false
        });

        r.SubmitPointLight(new PointLight
        {
            PositionWorld = new Vector2D<float>(w * 0.76f, h * 0.3f),
            Radius = w * 0.4f,
            Color = new Vector3D<float>(1f, 0.65f, 0.38f),
            Intensity = 0.48f,
            FalloffExponent = 2.1f,
            CastsShadow = false
        });

        var spotPos = new Vector2D<float>(w * 0.2f, h * 0.58f);
        var center = new Vector2D<float>(w * 0.5f, h * 0.5f);
        var dx = center.X - spotPos.X;
        var dy = center.Y - spotPos.Y;
        var dLen = MathF.Sqrt(dx * dx + dy * dy);
        var dir = dLen > 1e-4f
            ? new Vector2D<float>(dx / dLen, dy / dLen)
            : new Vector2D<float>(1f, 0f);

        r.SubmitSpotLight(new SpotLight
        {
            PositionWorld = spotPos,
            DirectionWorld = dir,
            Radius = w * 0.46f,
            InnerConeRadians = MathF.PI / 3.5f,
            OuterConeRadians = MathF.PI / 2.15f,
            Color = new Vector3D<float>(0.42f, 0.62f, 1f),
            Intensity = 0.42f,
            CastsShadow = false
        });
    }
}
