using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using Xunit;

namespace Cyberland.Engine.Tests;

[RequiresComponent<Transform>]
[RequiresComponent<Transform>]
public struct RequiresDupCoverageComponent : IComponent;

public sealed class WorldRequiresComponentTests
{
    [Fact]
    public void GetOrAdd_BitmapText_adds_required_peer_components()
    {
        var w = new World();
        var e = w.CreateEntity();
        ref var bt = ref w.Components<BitmapText>().GetOrAdd(e);
        bt.Visible = true;
        bt.Content = "a";
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.WorldSpace;
        bt.Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f));

        Assert.True(w.Components<Transform>().Contains(e));
        Assert.True(w.Components<TextBuildFingerprint>().Contains(e));
        Assert.True(w.Components<TextSpriteCache>().Contains(e));
    }

    [Fact]
    public void GetOrAdd_BitmapText_with_initial_adds_required_peer_components()
    {
        var w = new World();
        var e = w.CreateEntity();
        ref var bt = ref w.Components<BitmapText>().GetOrAdd(e, new BitmapText
        {
            Visible = true,
            Content = "b",
            IsLocalizationKey = false,
            CoordinateSpace = CoordinateSpace.WorldSpace,
            Style = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 1f, 1f, 1f))
        });

        Assert.True(bt.Visible);
        Assert.True(w.Components<Transform>().Contains(e));
        Assert.True(w.Components<TextBuildFingerprint>().Contains(e));
        Assert.True(w.Components<TextSpriteCache>().Contains(e));
    }

    [Fact]
    public void Duplicate_RequiresComponent_attributes_dedupe_required_types()
    {
        var w = new World();
        var e = w.CreateEntity();
        _ = w.Components<RequiresDupCoverageComponent>().GetOrAdd(e);
        Assert.True(w.Components<Transform>().Contains(e));
    }

    [Fact]
    public void GetOrAdd_TriggerEvents_adds_Trigger_peer()
    {
        var w = new World();
        var e = w.CreateEntity();
        _ = w.Components<TriggerEvents>().GetOrAdd(e);
        Assert.True(w.Components<Trigger>().Contains(e));
    }
}
