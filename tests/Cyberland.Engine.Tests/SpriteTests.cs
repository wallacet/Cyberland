using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using Xunit;
using TextureId = System.UInt32;

namespace Cyberland.Engine.Tests;

public sealed class SpriteTests
{
    [Fact]
    public void Sprite_parameterless_ctor_uses_MaxValue_emissive_sentinel()
    {
        var s = new Sprite();
        Assert.Equal(TextureId.MaxValue, s.EmissiveTextureId);
        Assert.Equal(1f, s.Alpha);
    }

    [Fact]
    public void DefaultWhiteUnlit_sets_full_uv_rect_and_MaxValue_emissive()
    {
        var s = Sprite.DefaultWhiteUnlit(3u, 7u, new Vector2D<float>(10f, 12f));
        Assert.Equal(new Vector4D<float>(0f, 0f, 1f, 1f), s.UvRect);
        Assert.Equal(TextureId.MaxValue, s.EmissiveTextureId);
        Assert.Equal(3u, s.AlbedoTextureId);
        Assert.Equal(7u, s.NormalTextureId);
    }
}
