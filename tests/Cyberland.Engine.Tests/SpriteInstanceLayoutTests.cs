using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Tests;

public sealed class SpriteInstanceLayoutTests
{
    [Fact]
    public void Typical_layout_has_disjoint_regions_and_correct_capacity()
    {
        var (em, op, tr, sh, ov, cap) =
            DeferredRenderingConstants.ComputeSpriteInstanceLayout(100, 20);

        Assert.Equal(0, em);
        Assert.Equal(100, op);
        Assert.Equal(200, tr);
        Assert.Equal(300, sh);
        Assert.Equal(400, ov);
        Assert.Equal(420, cap);
    }

    [Fact]
    public void Zero_deferred_sprites_yields_overlay_only_capacity()
    {
        var (em, op, tr, sh, ov, cap) =
            DeferredRenderingConstants.ComputeSpriteInstanceLayout(0, 64);

        Assert.Equal(0, em);
        Assert.Equal(0, op);
        Assert.Equal(0, tr);
        Assert.Equal(0, sh);
        Assert.Equal(0, ov);
        Assert.Equal(64, cap);
    }

    [Fact]
    public void Regions_never_overlap()
    {
        const int n = 250;
        const int overlayCap = 40;
        var (em, op, tr, sh, ov, cap) =
            DeferredRenderingConstants.ComputeSpriteInstanceLayout(n, overlayCap);

        var regions = new (int Start, int Count)[]
        {
            (em, n), (op, n), (tr, n), (sh, n), (ov, overlayCap)
        };

        for (var i = 0; i < regions.Length; i++)
        {
            var (startA, countA) = regions[i];
            var endA = startA + countA;
            Assert.True(endA <= cap, $"Region {i} [{startA}..{endA}) exceeds capacity {cap}");

            for (var j = i + 1; j < regions.Length; j++)
            {
                var (startB, countB) = regions[j];
                var endB = startB + countB;
                Assert.True(endA <= startB || endB <= startA,
                    $"Regions {i} [{startA}..{endA}) and {j} [{startB}..{endB}) overlap");
            }
        }
    }

    [Fact]
    public void Overlay_base_comes_after_all_deferred_regions()
    {
        var (_, op, tr, sh, ov, _) =
            DeferredRenderingConstants.ComputeSpriteInstanceLayout(50, 10);

        Assert.True(ov > sh, "Overlay must follow shadow occluder region");
        Assert.True(ov > tr, "Overlay must follow transparent region");
        Assert.True(ov > op, "Overlay must follow opaque region");
    }
}
