using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Moq;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class ModLayoutViewportTests
{
    [Fact]
    public void ModLayoutViewport_routes_simulation_and_presentation_sizes()
    {
        var host = new GameHostServices
        {
            CameraRuntimeState = new CameraRuntimeState(
                new Vector2D<int>(1280, 720),
                default,
                0f,
                default,
                0,
                true)
        };
        var r = new Mock<IRenderer>(MockBehavior.Strict);
        r.SetupGet(x => x.ActiveCameraViewportSize).Returns(new Vector2D<int>(100, 200));

        Assert.Equal(new Vector2D<int>(1280, 720), ModLayoutViewport.VirtualSizeForSimulation(host));
        Assert.Equal(new Vector2D<int>(100, 200), ModLayoutViewport.VirtualSizeForPresentation(r.Object));
    }
}
