using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class ViewportUiOverlayRoutingTests
{
    [Fact]
    public void IsViewportUiOverlaySprite_routes_viewport_ui_layer_even_when_marked_transparent()
    {
        var d = new SpriteDrawRequest
        {
            Layer = (int)SpriteLayer.Ui,
            Space = CoordinateSpace.ViewportSpace,
            Transparent = true,
            CenterWorld = default,
            HalfExtentsWorld = new Vector2D<float>(4f, 4f)
        };

        Assert.True(VulkanRenderer.IsViewportUiOverlaySprite(in d));
    }

    [Fact]
    public void IsViewportUiOverlaySprite_routes_swapchain_ui_layer_even_when_marked_transparent()
    {
        var d = new SpriteDrawRequest
        {
            Layer = (int)SpriteLayer.Ui,
            Space = CoordinateSpace.SwapchainSpace,
            Transparent = true,
            CenterWorld = default,
            HalfExtentsWorld = new Vector2D<float>(4f, 4f)
        };

        Assert.True(VulkanRenderer.IsViewportUiOverlaySprite(in d));
    }

    [Fact]
    public void IsViewportUiOverlaySprite_excludes_world_ui_and_below_ui_band()
    {
        var worldUi = new SpriteDrawRequest
        {
            Layer = (int)SpriteLayer.Ui,
            Space = CoordinateSpace.WorldSpace,
            Transparent = false,
            CenterWorld = default,
            HalfExtentsWorld = new Vector2D<float>(4f, 4f)
        };
        Assert.False(VulkanRenderer.IsViewportUiOverlaySprite(in worldUi));

        var viewportWorldBand = new SpriteDrawRequest
        {
            Layer = (int)SpriteLayer.World,
            Space = CoordinateSpace.ViewportSpace,
            Transparent = false,
            CenterWorld = default,
            HalfExtentsWorld = new Vector2D<float>(4f, 4f)
        };
        Assert.False(VulkanRenderer.IsViewportUiOverlaySprite(in viewportWorldBand));
    }
}
