using System.Runtime.CompilerServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

// Purpose: Lighting uniform buffer and point/directional/spot SSBO upload used by deferred passes.

/// <summary>Lighting buffers and UBO/SSBO updates for deferred rendering (partial).</summary>
public sealed unsafe partial class VulkanRenderer
{
    private void CreateLightingBuffer()
    {
        if (_lightingBuffer.Handle != default)
            return;
        CreateHostVisibleBuffer((ulong)sizeof(LightingUbo), BufferUsageFlags.UniformBufferBit, out _lightingBuffer, out _lightingBufferMemory);
        void* p;
        if (_vk!.MapMemory(_device, _lightingBufferMemory, 0, (ulong)sizeof(LightingUbo), 0, &p) != Result.Success)
            throw new GraphicsInitializationException("map lighting ubo (persistent)");
        _lightingBufferMapped = p;
    }

    private void DestroyLightingBuffer()
    {
        if (_lightingBufferMemory.Handle != default && _lightingBufferMapped != null)
        {
            _vk!.UnmapMemory(_device, _lightingBufferMemory);
            _lightingBufferMapped = null;
        }
        if (_lightingBuffer.Handle != default)
        {
            _vk!.DestroyBuffer(_device, _lightingBuffer, null);
            _lightingBuffer = default;
        }
        if (_lightingBufferMemory.Handle != default)
        {
            _vk!.FreeMemory(_device, _lightingBufferMemory, null);
            _lightingBufferMemory = default;
        }
    }

    private void UpdateLightingFrameData(in FramePlan framePlan)
    {
        if (_vk is null || _lightingBufferMapped == null)
            return;

        float ar = 0f, ag = 0f, ab = 0f;
        for (var i = 0; i < framePlan.AmbientLightCount; i++)
        {
            var a = framePlan.AmbientLights[i];
            ar += a.Color.X * a.Intensity;
            ag += a.Color.Y * a.Intensity;
            ab += a.Color.Z * a.Intensity;
        }

        var nDir = Math.Min(framePlan.DirectionalLightCount, DeferredRenderingConstants.MaxDirectionalLights);
        var nSpot = Math.Min(framePlan.SpotLightCount, DeferredRenderingConstants.MaxSpotLights);
        var ubo = new LightingUbo
        {
            Ambient = new Vector4D<float>(ar, ag, ab, 1f),
            Counts = new Vector4D<float>(nDir, nSpot, 0f, 0f)
        };
        Unsafe.Write(_lightingBufferMapped, ubo);
        UploadDirectionalSpotLightSsboData(in framePlan, nDir, nSpot);
    }

    private void UploadDirectionalSpotLightSsboData(in FramePlan framePlan, int nDir, int nSpot)
    {
        if (_directionalLightSsboMapped is null || _spotLightSsboMapped is null)
            return;

        if (nDir > 0)
        {
            var dirSpan = new Span<Vector4D<float>>(
                (Vector4D<float>*)_directionalLightSsboMapped, DeferredRenderingConstants.MaxDirectionalLights * 2);
            var src = framePlan.DirectionalLights;
            for (var i = 0; i < nDir; i++)
            {
                ref readonly var d = ref src[i];
                var w = d.DirectionWorld;
                var len = MathF.Sqrt(w.X * w.X + w.Y * w.Y);
                if (len < 1e-6f)
                {
                    w = new Vector2D<float>(0.25f, 0.35f);
                }
                else
                {
                    w = new Vector2D<float>(w.X / len, w.Y / len);
                }
                dirSpan[i * 2] = new Vector4D<float>(w.X, w.Y, d.Intensity, 0f);
                dirSpan[i * 2 + 1] = new Vector4D<float>(d.Color.X, d.Color.Y, d.Color.Z, d.CastsShadow ? 1f : 0f);
            }
        }

        if (nSpot > 0)
        {
            var spotSpan = new Span<Vector4D<float>>(
                (Vector4D<float>*)_spotLightSsboMapped, DeferredRenderingConstants.MaxSpotLights * 3);
            var ssrc = framePlan.SpotLights;
            for (var j = 0; j < nSpot; j++)
            {
                ref readonly var sp = ref ssrc[j];
                var sdir = sp.DirectionWorld;
                var sLen = MathF.Sqrt(sdir.X * sdir.X + sdir.Y * sdir.Y);
                if (sLen < 1e-6f)
                {
                    sdir = new Vector2D<float>(1f, 0f);
                }
                else
                {
                    sdir = new Vector2D<float>(sdir.X / sLen, sdir.Y / sLen);
                }
                spotSpan[j * 3] = new Vector4D<float>(sp.PositionWorld.X, sp.PositionWorld.Y, sp.Radius, sp.CastsShadow ? 1f : 0f);
                spotSpan[j * 3 + 1] = new Vector4D<float>(sdir.X, sdir.Y, MathF.Cos(sp.InnerConeRadians), MathF.Cos(sp.OuterConeRadians));
                spotSpan[j * 3 + 2] = new Vector4D<float>(sp.Color.X, sp.Color.Y, sp.Color.Z, sp.Intensity);
            }
        }
    }

    private void UploadPointLightSsboData(in FramePlan framePlan)
    {
        if (_pointLightSsboMapped == null)
            return;

        var pts = framePlan.PointLights;
        var n = Math.Min(framePlan.PointLightCount, DeferredRenderingConstants.MaxPointLights);
        var span = new Span<Vector4D<float>>((Vector4D<float>*)_pointLightSsboMapped, DeferredRenderingConstants.MaxPointLights * 2);
        for (var i = 0; i < n; i++)
        {
            ref readonly var pl = ref pts[i];
            var fall = pl.FalloffExponent > 1e-6f ? pl.FalloffExponent : 2f;
            span[i * 2] = new Vector4D<float>(pl.PositionWorld.X, pl.PositionWorld.Y, pl.Radius, fall);
            span[i * 2 + 1] = new Vector4D<float>(pl.Color.X, pl.Color.Y, pl.Color.Z, pl.Intensity);
        }
    }
}
