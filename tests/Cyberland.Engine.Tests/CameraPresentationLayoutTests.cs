using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class CameraPresentationLayoutTests
{
    [Fact]
    public void ResolvePresentationViewportSize_falls_back_when_unconfigured()
    {
        var view = new Vector2D<int>(560, 315);
        Assert.Equal(view, CameraPresentationLayout.ResolvePresentationViewportSize(view, default));
        Assert.Equal(view, CameraPresentationLayout.ResolvePresentationViewportSize(view, new Vector2D<int>(0, 720)));
        Assert.Equal(view, CameraPresentationLayout.ResolvePresentationViewportSize(view, new Vector2D<int>(1280, 0)));
    }

    [Fact]
    public void ResolvePresentationViewportSize_uses_explicit_canvas_when_positive()
    {
        var view = new Vector2D<int>(560, 315);
        var pres = new Vector2D<int>(1280, 720);
        Assert.Equal(pres, CameraPresentationLayout.ResolvePresentationViewportSize(view, pres));
    }

    [Fact]
    public void ResolvePresentationViewportSize_from_runtime_state_matches_pair()
    {
        var state = new CameraRuntimeState(
            new Vector2D<int>(800, 450),
            default,
            0f,
            default,
            0,
            true,
            new Vector2D<int>(1280, 720));
        Assert.Equal(new Vector2D<int>(1280, 720), CameraPresentationLayout.ResolvePresentationViewportSize(state));
    }
}
