using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class EmissiveLightPromotionCpuTests
{
    private static SpriteDrawRequest MakeSprite(
        Vector2D<float> centerWorld,
        Vector2D<float> halfExtentsWorld,
        Vector3D<float> emissiveTint,
        float emissiveIntensity,
        CoordinateSpace space = CoordinateSpace.WorldSpace)
    {
        return new SpriteDrawRequest
        {
            CenterWorld = centerWorld,
            HalfExtentsWorld = halfExtentsWorld,
            EmissiveTint = emissiveTint,
            EmissiveIntensity = emissiveIntensity,
            Space = space,
            ColorMultiply = new Vector4D<float>(1, 1, 1, 1),
            Alpha = 1f,
            UvRect = new Vector4D<float>(0, 0, 1, 1),
            CastsShadow = true,
        };
    }

    [Fact]
    public void Promote_returns_zero_when_inputs_invalid()
    {
        Span<PointLight> dst = stackalloc PointLight[8];
        Assert.Equal(0, EmissiveLightPromotionCpu.Promote(null!, 0, 1f, 3f, 1f, 4, dst));

        var sprites = new SpriteDrawRequest[] { MakeSprite(default, new(10, 10), new(1, 1, 1), 2f) };
        Assert.Equal(0, EmissiveLightPromotionCpu.Promote(sprites, 0, 1f, 3f, 1f, 4, dst));
        Assert.Equal(0, EmissiveLightPromotionCpu.Promote(sprites, 1, 1f, 3f, 1f, 0, dst));
        Assert.Equal(0, EmissiveLightPromotionCpu.Promote(sprites, 1, 1f, 3f, 1f, 4, Span<PointLight>.Empty));
        Assert.Equal(0, EmissiveLightPromotionCpu.Promote(sprites, 1, 0f, 3f, 1f, 4, dst));
    }

    [Fact]
    public void Promote_emits_one_point_light_per_bright_emissive_sprite()
    {
        var sprites = new[]
        {
            MakeSprite(new(100f, 200f), new(20f, 30f), new(0.2f, 0.9f, 0.5f), 2.5f),
            MakeSprite(new(0f, 0f), new(5f, 5f), new(0.1f, 0.1f, 0.1f), 0.1f), // below threshold
        };
        Span<PointLight> dst = stackalloc PointLight[8];
        var n = EmissiveLightPromotionCpu.Promote(sprites, 2, 1.0f, 3f, 1f, 4, dst);
        Assert.Equal(1, n);
        Assert.Equal(100f, dst[0].PositionWorld.X);
        Assert.Equal(200f, dst[0].PositionWorld.Y);
        Assert.True(dst[0].Radius > 0f);
        Assert.True(dst[0].CastsShadow);
    }

    [Fact]
    public void Promote_skips_viewport_space_sprites()
    {
        var sprites = new[]
        {
            MakeSprite(new(100f, 200f), new(20f, 30f), new(1f, 1f, 1f), 3f, CoordinateSpace.ViewportSpace),
        };
        Span<PointLight> dst = stackalloc PointLight[4];
        Assert.Equal(0, EmissiveLightPromotionCpu.Promote(sprites, 1, 1f, 3f, 1f, 4, dst));
    }

    [Fact]
    public void Promote_skips_zero_tint()
    {
        var sprites = new[]
        {
            MakeSprite(new(0, 0), new(10, 10), new(0, 0, 0), 5f),
        };
        Span<PointLight> dst = stackalloc PointLight[4];
        Assert.Equal(0, EmissiveLightPromotionCpu.Promote(sprites, 1, 1f, 3f, 1f, 4, dst));
    }

    [Fact]
    public void Promote_truncates_at_maxPromoted_and_dst_cap()
    {
        var sprites = new SpriteDrawRequest[6];
        for (var i = 0; i < sprites.Length; i++)
            sprites[i] = MakeSprite(new(i * 10f, 0f), new(10, 10), new(1, 1, 1), 3f);

        Span<PointLight> dst = stackalloc PointLight[3];
        var n = EmissiveLightPromotionCpu.Promote(sprites, 6, 1f, 3f, 1f, 10, dst);
        Assert.Equal(3, n);

        Span<PointLight> dst2 = stackalloc PointLight[8];
        var n2 = EmissiveLightPromotionCpu.Promote(sprites, 6, 1f, 3f, 1f, 2, dst2);
        Assert.Equal(2, n2);
    }

    [Fact]
    public void Promote_radius_floors_at_8_for_tiny_sprites()
    {
        var sprites = new[]
        {
            MakeSprite(new(0, 0), new(0.1f, 0.1f), new(1, 1, 1), 3f),
        };
        Span<PointLight> dst = stackalloc PointLight[2];
        EmissiveLightPromotionCpu.Promote(sprites, 1, 1f, 3f, 1f, 4, dst);
        Assert.True(dst[0].Radius >= 8f);
    }

    [Fact]
    public void Promote_with_sort_indices_reorders_output()
    {
        const int count = 10;
        var sprites = new SpriteDrawRequest[count];
        var sortIndices = new int[count];
        for (var i = 0; i < count; i++)
        {
            sprites[i] = MakeSprite(new(i * 10f, 0f), new(10, 10), new(1, 1, 1), 3f);
            sortIndices[i] = count - 1 - i;
        }

        var dst = new PointLight[count];
        var n = EmissiveLightPromotionCpu.Promote(sprites, count, new ReadOnlySpan<int>(sortIndices),
            1f, 3f, 1f, count, dst);
        Assert.Equal(count, n);
        Assert.Equal((count - 1) * 10f, dst[0].PositionWorld.X);
    }

    [Fact]
    public void Promote_serial_skips_non_world_and_zero_tint_and_low_intensity()
    {
        const int count = 400;
        var sprites = new SpriteDrawRequest[count];
        for (var i = 0; i < count; i++)
        {
            if (i % 4 == 0)
                sprites[i] = MakeSprite(new(i, 0), new(10, 10), new(0, 0, 0), 3f);
            else if (i % 4 == 1)
                sprites[i] = MakeSprite(new(i, 0), new(10, 10), new(1, 1, 1), 3f, CoordinateSpace.ViewportSpace);
            else if (i % 4 == 2)
                sprites[i] = MakeSprite(new(i, 0), new(10, 10), new(1, 1, 1), 0.1f);
            else
                sprites[i] = MakeSprite(new(i, 0), new(10, 10), new(1, 1, 1), 3f);
        }

        var dst = new PointLight[count];
        var n = EmissiveLightPromotionCpu.Promote(sprites, count, 1f, 3f, 1f, count, dst);
        Assert.Equal(100, n);
    }

    [Fact]
    public void Promote_neon_strip_above_default_threshold_creates_point_light()
    {
        var sprites = new[]
        {
            MakeSprite(new(50f, 75f), new(40f, 2f), new(0f, 1f, 0.8f), 2.4f),
        };
        Span<PointLight> dst = stackalloc PointLight[4];
        var n = EmissiveLightPromotionCpu.Promote(sprites, 1, 1.5f, 3f, 1f, 4, dst);
        Assert.Equal(1, n);
        Assert.Equal(50f, dst[0].PositionWorld.X);
        Assert.Equal(75f, dst[0].PositionWorld.Y);
        Assert.True(dst[0].Radius > 0f);
        Assert.True(dst[0].Intensity > 0f);
        Assert.True(dst[0].CastsShadow);
        Assert.Equal(0f, dst[0].Color.X);
        Assert.True(dst[0].Color.Y > 0f);
        Assert.True(dst[0].Color.Z > 0f);
    }

    [Fact]
    public void Promote_sets_CastsShadow_true_and_caller_strips_when_shadows_disabled()
    {
        // Promote always marks promoted lights as shadow-casters.
        var sprites = new[]
        {
            MakeSprite(new(80f, 40f), new(15f, 15f), new(1f, 0.5f, 0.2f), 3f),
        };
        Span<PointLight> dst = stackalloc PointLight[4];
        var n = EmissiveLightPromotionCpu.Promote(sprites, 1, 1f, 3f, 1f, 4, dst);
        Assert.Equal(1, n);
        Assert.True(dst[0].CastsShadow);

        // FramePlanBuilder strips CastsShadow when shadows are globally disabled.
        for (var i = 0; i < n; i++)
            dst[i].CastsShadow = false;

        for (var i = 0; i < n; i++)
        {
            Assert.False(dst[i].CastsShadow);
            Assert.True(dst[i].Intensity > 0f, "Light still illuminates even with shadows disabled.");
        }
    }
}
