using System.Collections.Concurrent;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Tests;

/// <summary>
/// When <c>AcquireNextImage</c> returns out-of-date, <see cref="Cyberland.Engine.Rendering.VulkanRenderer.DrawFrame"/> must
/// discard pending CPU submissions before returning — otherwise <see cref="ConcurrentQueueDrain.DrainToScratch"/> never runs
/// for that tick and the next successful frame merges sprites from multiple <c>RunFrame</c> calls (extra HUD draws vs current copy).
/// </summary>
public sealed class SkippedPresentSubmissionDrainTests
{
    [Fact]
    public void RecordingRenderer_reset_pending_submissions_clears_all_submit_lists()
    {
        var renderer = new RecordingRenderer();
        renderer.SubmitSprite(new SpriteDrawRequest());
        renderer.SubmitTextGlyph(new TextGlyphDrawRequest());
        renderer.SubmitPointLight(new PointLight());
        renderer.SubmitSpotLight(new SpotLight());
        renderer.SubmitDirectionalLight(new DirectionalLight());
        renderer.SubmitAmbientLight(new AmbientLight());
        renderer.SubmitPostProcessVolume(new PostProcessVolume(), default, 0f, new Silk.NET.Maths.Vector2D<float>(1f, 1f));
        renderer.SubmitCamera(new CameraViewRequest { Enabled = true, ViewportSizeWorld = new Silk.NET.Maths.Vector2D<int>(1, 1) });

        renderer.ResetPendingSubmissionsForNewTick();

        Assert.Empty(renderer.Sprites);
        Assert.Empty(renderer.TextGlyphs);
        Assert.Empty(renderer.PointLights);
        Assert.Empty(renderer.SpotLights);
        Assert.Empty(renderer.DirectionalLights);
        Assert.Empty(renderer.AmbientLights);
        Assert.Empty(renderer.Volumes);
        Assert.Empty(renderer.Cameras);
    }

    [Fact]
    public void Without_discard_between_ticks_overlay_queue_retains_prior_tick_submissions()
    {
        var overlay = new ConcurrentQueue<int>();
        for (var i = 0; i < 50; i++)
            overlay.Enqueue(i);
        // Bug pattern: no drain when GPU record is skipped (simulates OutOfDate return before Build).
        for (var i = 0; i < 3; i++)
            overlay.Enqueue(100 + i);
        Assert.Equal(53, DrainCount(overlay));
    }

    [Fact]
    public void DiscardAll_matches_Vulkan_OutOfDate_path_so_next_tick_submits_only_current_batch()
    {
        var overlay = new ConcurrentQueue<int>();
        for (var i = 0; i < 50; i++)
            overlay.Enqueue(i);
        ConcurrentQueueDrain.DiscardAll(overlay);
        for (var i = 0; i < 3; i++)
            overlay.Enqueue(200 + i);
        Assert.Equal(new[] { 200, 201, 202 }, Materialize(overlay));
    }

    private static int DrainCount<T>(ConcurrentQueue<T> q)
    {
        var n = 0;
        while (q.TryDequeue(out _))
            n++;
        return n;
    }

    private static List<T> Materialize<T>(ConcurrentQueue<T> q)
    {
        var list = new List<T>();
        while (q.TryDequeue(out var x))
            list.Add(x);
        return list;
    }
}
