using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Layout;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Engine.Tests;

public sealed class UiControlsTests
{
    [Fact]
    public void UiRadioGroup_keeps_single_selection()
    {
        var group = new UiRadioGroup();
        var a = new UiRadioButton(group, "a", 140f, 32f);
        var b = new UiRadioButton(group, "b", 140f, 32f);

        string? last = null;
        group.SelectionChanged += (_, id) => last = id;

        b.SelectFromUiSystem();
        Assert.Equal("b", group.SelectedOptionId);
        Assert.Equal("b", last);

        a.SelectFromUiSystem();
        Assert.Equal("a", group.SelectedOptionId);
        Assert.Equal("a", last);
    }

    [Fact]
    public void UiRadioGroup_Select_same_id_is_noop()
    {
        var group = new UiRadioGroup();
        _ = new UiRadioButton(group, "a", 140f, 32f);
        group.Select("a");
        group.Select("a");
        Assert.Equal("a", group.SelectedOptionId);
    }

    [Fact]
    public void UiRadioButton_non_interactable_ignores_select()
    {
        var group = new UiRadioGroup();
        var a = new UiRadioButton(group, "a", 140f, 32f) { Interactable = false };
        a.SelectFromUiSystem();
        Assert.Null(group.SelectedOptionId);
    }

    [Fact]
    public void UiHitTest_prefers_higher_sort_key_sibling()
    {
        var doc = new UiDocument();
        var row = new UiHorizontalStack { Spacing = -80f };
        UiLayoutPresets.StretchAll(row);
        var back = new UiButton { SortKey = 1f };
        var front = new UiButton { SortKey = 5f };
        UiLayoutPresets.TopLeftFixed(back, 120f, 48f);
        UiLayoutPresets.TopLeftFixed(front, 120f, 48f);
        row.AddChild(back);
        row.AddChild(front);
        doc.Root.AddChild(row);

        doc.MeasureArrange(new Vector2D<float>(200f, 200f));
        var hit = doc.HitTest(new Vector2D<float>(60f, 10f), new UiRect(0f, 0f, 200f, 200f));
        Assert.Same(front, hit);
    }

    [Fact]
    public void UiHitTest_returns_null_when_root_invisible_or_pointer_outside_clip()
    {
        var doc = new UiDocument();
        var btn = new UiButton();
        UiLayoutPresets.TopLeftFixed(btn, 120f, 48f);
        doc.Root.AddChild(btn);
        doc.MeasureArrange(new Vector2D<float>(200f, 200f));

        doc.Root.Visible = false;
        Assert.Null(doc.HitTest(new Vector2D<float>(10f, 10f), new UiRect(0f, 0f, 200f, 200f)));

        doc.Root.Visible = true;
        Assert.Null(doc.HitTest(new Vector2D<float>(10f, 10f), new UiRect(0f, 0f, 0f, 0f)));
    }

    [Fact]
    public void UiImage_draw_submits_when_texture_assigned()
    {
        var renderer = new RecordingRenderer();
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var cache = new TextGlyphCache();

        var img = new UiImage { SourceTextureId = renderer.WhiteTextureId, Tint = new Vector4D<float>(1f, 0f, 1f, 1f) };
        UiLayoutPresets.CenterFixed(img, 40f, 40f);

        var doc = new UiDocument();
        doc.Root.AddChild(img);
        doc.MeasureArrange(new Vector2D<float>(120f, 120f));

        var rootClip = new UiRect(0f, 0f, 120f, 120f);
        doc.DrawVisuals(renderer, fonts, cache, CoordinateSpace.ViewportSpace, 300f, rootClip);
        Assert.Single(renderer.Sprites);
    }

    [Fact]
    public void UiImage_draw_skips_when_texture_missing_or_alpha_zero()
    {
        var renderer = new RecordingRenderer();
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var cache = new TextGlyphCache();

        var doc = new UiDocument();

        var missing = new UiImage { SourceTextureId = TextureId.MaxValue };
        UiLayoutPresets.TopLeftFixed(missing, 20f, 20f);
        doc.Root.AddChild(missing);

        var transparent = new UiImage { SourceTextureId = renderer.WhiteTextureId, Tint = new Vector4D<float>(1f, 1f, 1f, 0f) };
        UiLayoutPresets.TopLeftFixed(transparent, 20f, 20f);
        transparent.AnchoredPosition = new Vector2D<float>(30f, 0f);
        doc.Root.AddChild(transparent);

        doc.MeasureArrange(new Vector2D<float>(120f, 60f));
        doc.DrawVisuals(renderer, fonts, cache, CoordinateSpace.ViewportSpace, 0f, new UiRect(0f, 0f, 120f, 60f));
        Assert.Empty(renderer.Sprites);
    }

    [Fact]
    public void UiLabel_exposes_stretched_text_child()
    {
        var label = new UiLabel();
        Assert.Same(label.Children[0], label.Text);
    }

    [Fact]
    public void UiPanel_DrawSelfVisuals_skips_when_intersected_viewport_clip_empty()
    {
        var renderer = new RecordingRenderer();
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var cache = new TextGlyphCache();

        var panel = new UiPanel { BackgroundColor = new Vector4D<float>(1f, 1f, 1f, 1f) };
        UiLayoutPresets.TopLeftFixed(panel, 80f, 40f);
        panel.Measure(UiSizeConstraints.Loose(300f, 300f));
        panel.Arrange(new UiRect(10f, 10f, 300f, 300f));

        var disjointClip = new UiRect(0f, 500f, 200f, 80f);
        panel.DrawVisuals(renderer, fonts, cache, CoordinateSpace.ViewportSpace, 0f, disjointClip);
        Assert.Empty(renderer.Sprites);
    }

    [Fact]
    public void UiImage_DrawSelfVisuals_skips_when_intersected_viewport_clip_empty()
    {
        var renderer = new RecordingRenderer();
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var cache = new TextGlyphCache();

        var img = new UiImage { SourceTextureId = renderer.WhiteTextureId, Tint = new Vector4D<float>(1f, 1f, 1f, 1f) };
        UiLayoutPresets.TopLeftFixed(img, 40f, 40f);
        img.Measure(UiSizeConstraints.Loose(200f, 200f));
        img.Arrange(new UiRect(5f, 5f, 200f, 200f));

        img.DrawVisuals(renderer, fonts, cache, CoordinateSpace.ViewportSpace, 0f, new UiRect(0f, 600f, 100f, 100f));
        Assert.Empty(renderer.Sprites);
    }

    [Fact]
    public void UiScrollView_DrawVisuals_skips_when_invisible()
    {
        var renderer = new RecordingRenderer();
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var cache = new TextGlyphCache();

        var scroll = new UiScrollView { Visible = false };
        UiLayoutPresets.StretchAll(scroll);
        var inner = new UiPanel { BackgroundColor = new Vector4D<float>(1f, 0f, 0f, 1f) };
        UiLayoutPresets.TopStretch(inner, 400f);
        scroll.Content.AddChild(inner);

        var doc = new UiDocument();
        doc.Root.AddChild(scroll);
        doc.MeasureArrange(new Vector2D<float>(100f, 200f));

        scroll.DrawVisuals(renderer, fonts, cache, CoordinateSpace.ViewportSpace, 0f, new UiRect(0f, 0f, 100f, 200f));
        Assert.Empty(renderer.Sprites);
    }

    [Fact]
    public void UiElement_DrawVisuals_skips_when_invisible()
    {
        var renderer = new RecordingRenderer();
        var fonts = new FontLibrary();
        BuiltinFonts.AddTo(fonts);
        var cache = new TextGlyphCache();

        var e = new UiPanel { Visible = false, BackgroundColor = new Vector4D<float>(1f, 1f, 1f, 1f) };
        UiLayoutPresets.TopLeftFixed(e, 50f, 20f);
        e.Measure(UiSizeConstraints.Loose(100f, 100f));
        e.Arrange(new UiRect(0f, 0f, 100f, 100f));
        e.DrawVisuals(renderer, fonts, cache, CoordinateSpace.ViewportSpace, 0f, new UiRect(0f, 0f, 100f, 100f));
        Assert.Empty(renderer.Sprites);
    }

    [Fact]
    public void UiButton_internal_press_helpers_cover_non_interactable_and_cancel()
    {
        var btn = new UiButton { Interactable = false };
        var t = typeof(UiButton);
        t.GetMethod("NotifyPressStarted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(btn, null);

        t.GetMethod("NotifyCancelPress", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(btn, null);
    }

    [Fact]
    public void UiCommandQueue_Enqueue_null_throws_TryPeek_orders_before_dequeue()
    {
        var q = new Cyberland.Engine.Hosting.UiCommandQueue();
        Assert.Throws<ArgumentNullException>(() => q.Enqueue(null!));

        q.Enqueue(new TestUiCommand("seven"));
        Assert.True(q.TryPeek(out var peeked));
        Assert.Equal("seven", Assert.IsType<TestUiCommand>(peeked).Name);
        Assert.True(q.TryDequeue(out var popped));
        Assert.Equal("seven", Assert.IsType<TestUiCommand>(popped).Name);
        Assert.False(q.TryPeek(out _));
    }

    [Fact]
    public void UiCommandQueue_TrimToMaxCount_drops_oldest_entries()
    {
        var q = new Cyberland.Engine.Hosting.UiCommandQueue();
        q.Enqueue(new TestUiCommand("a"));
        q.Enqueue(new TestUiCommand("b"));
        q.Enqueue(new TestUiCommand("c"));
        var removed = q.TrimToMaxCount(1);
        Assert.Equal(2, removed);
        Assert.Equal(1, q.Count);
        Assert.True(q.TryDequeue(out var remaining));
        Assert.Equal("c", Assert.IsType<TestUiCommand>(remaining).Name);
    }

    [Fact]
    public void UiCommandQueue_TrimToMaxCount_negative_throws()
    {
        var q = new Cyberland.Engine.Hosting.UiCommandQueue();
        Assert.Throws<ArgumentOutOfRangeException>(() => q.TrimToMaxCount(-1));
    }

    private sealed record TestUiCommand(string Name) : IUiCommand;
}
