using Cyberland.Engine.Rendering;
using Cyberland.Engine.UI.Core;
using Xunit;
using TextureId = System.UInt32;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Unit tests for contiguous instanced sprite batch keys (texture / clip boundaries).
/// </summary>
public sealed class SpriteBatchRunsTests
{
    [Fact]
    public void EffectiveNormalTextureIdForDeferredSprite_uses_default_when_slot_missing()
    {
        var def = (TextureId)42u;
        var sMax = new SpriteDrawRequest { NormalTextureId = TextureId.MaxValue };
        Assert.Equal(def, SpriteBatchRuns.EffectiveNormalTextureIdForDeferredSprite(in sMax, def, false));

        var sBad = new SpriteDrawRequest { NormalTextureId = 7u };
        Assert.Equal(def, SpriteBatchRuns.EffectiveNormalTextureIdForDeferredSprite(in sBad, def, false));
    }

    [Fact]
    public void EffectiveNormalTextureIdForDeferredSprite_preserves_when_slot_exists()
    {
        var def = (TextureId)1u;
        var s = new SpriteDrawRequest { AlbedoTextureId = 3u, NormalTextureId = 7u };
        Assert.Equal(7u, SpriteBatchRuns.EffectiveNormalTextureIdForDeferredSprite(in s, def, true));
    }

    [Fact]
    public void EffectiveNormalTextureIdForDeferredSprite_falls_back_when_normal_equals_albedo_even_if_slot_exists()
    {
        var def = (TextureId)99u;
        var s = new SpriteDrawRequest { AlbedoTextureId = 5u, NormalTextureId = 5u };
        Assert.Equal(def, SpriteBatchRuns.EffectiveNormalTextureIdForDeferredSprite(in s, def, true));
        Assert.Equal(def, SpriteBatchRuns.EffectiveNormalTextureIdForDeferredSprite(in s, def, false));
    }

    [Fact]
    public void DeferredOpaqueRunCanExtend_requires_matching_albedo_and_resolved_normal()
    {
        var a = new SpriteDrawRequest { AlbedoTextureId = 1u };
        var b = new SpriteDrawRequest { AlbedoTextureId = 1u };
        Assert.True(SpriteBatchRuns.DeferredOpaqueRunCanExtend(in a, in b, 10u, 10u));
        Assert.False(SpriteBatchRuns.DeferredOpaqueRunCanExtend(in a, in b, 10u, 11u));

        var c = new SpriteDrawRequest { AlbedoTextureId = 2u };
        Assert.False(SpriteBatchRuns.DeferredOpaqueRunCanExtend(in a, in c, 10u, 10u));
    }

    [Fact]
    public void DeferredTransparentRunCanExtend_requires_matching_albedo_only()
    {
        var a = new SpriteDrawRequest { AlbedoTextureId = 3u };
        var b = new SpriteDrawRequest { AlbedoTextureId = 3u };
        Assert.True(SpriteBatchRuns.DeferredTransparentRunCanExtend(in a, in b));

        var c = new SpriteDrawRequest { AlbedoTextureId = 4u };
        Assert.False(SpriteBatchRuns.DeferredTransparentRunCanExtend(in a, in c));
    }

    [Fact]
    public void DeferredEmissiveRunCanExtend_requires_albedo_emissive_slot_and_use_flags()
    {
        var a = new SpriteDrawRequest { AlbedoTextureId = 1u, EmissiveTextureId = 5u };
        var b = new SpriteDrawRequest { AlbedoTextureId = 1u, EmissiveTextureId = 5u };
        Assert.True(SpriteBatchRuns.DeferredEmissiveRunCanExtend(in a, in b, 5u, 5u, 1, 1));

        // Same resolved black slot but different "use map" semantics would still compare slots in recording;
        // helper enforces prevUse == nextUse.
        Assert.False(SpriteBatchRuns.DeferredEmissiveRunCanExtend(in a, in b, 5u, 5u, 1, 0));

        var c = new SpriteDrawRequest { AlbedoTextureId = 1u, EmissiveTextureId = 6u };
        Assert.False(SpriteBatchRuns.DeferredEmissiveRunCanExtend(in a, in c, 5u, 6u, 1, 1));

        var d = new SpriteDrawRequest { AlbedoTextureId = 2u, EmissiveTextureId = 5u };
        Assert.False(SpriteBatchRuns.DeferredEmissiveRunCanExtend(in a, in d, 5u, 5u, 1, 1));
    }

    [Fact]
    public void OverlayRunCanExtend_respects_clip_enable_and_rect()
    {
        var r = new UiRect(0f, 0f, 100f, 50f);
        var a = new SpriteDrawRequest
        {
            AlbedoTextureId = 1u,
            ViewportClipEnabled = true,
            ViewportClipRect = r
        };
        var b = new SpriteDrawRequest
        {
            AlbedoTextureId = 1u,
            ViewportClipEnabled = true,
            ViewportClipRect = r
        };
        Assert.True(SpriteBatchRuns.OverlayRunCanExtend(in a, in b, 9u, 9u));

        var c = new SpriteDrawRequest
        {
            AlbedoTextureId = 1u,
            ViewportClipEnabled = true,
            ViewportClipRect = new UiRect(1f, 0f, 100f, 50f)
        };
        Assert.False(SpriteBatchRuns.OverlayRunCanExtend(in a, in c, 9u, 9u));

        var d = new SpriteDrawRequest { AlbedoTextureId = 1u, ViewportClipEnabled = false };
        Assert.False(SpriteBatchRuns.OverlayRunCanExtend(in a, in d, 9u, 9u));
    }

    [Fact]
    public void OverlayRunCanExtend_when_clip_disabled_ignores_rect()
    {
        var a = new SpriteDrawRequest { AlbedoTextureId = 1u, ViewportClipEnabled = false };
        var b = new SpriteDrawRequest
        {
            AlbedoTextureId = 1u,
            ViewportClipEnabled = false,
            ViewportClipRect = new UiRect(0f, 0f, 999f, 999f)
        };
        Assert.True(SpriteBatchRuns.OverlayRunCanExtend(in a, in b, 3u, 3u));
    }
}
