using System.Runtime.CompilerServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

// Purpose: Lighting uniform buffer and point-light SSBO upload used by deferred passes.

/// <summary>Lighting buffers and UBO/SSBO updates for deferred rendering (partial).</summary>
public sealed unsafe partial class VulkanRenderer
{
    private void CreateLightingBuffer()
    {
        if (_lightingBuffer.Handle != default)
            return;
        CreateHostVisibleBuffer((ulong)sizeof(LightingUbo), BufferUsageFlags.UniformBufferBit, out _lightingBuffer, out _lightingBufferMemory);
    }

    private void DestroyLightingBuffer()
    {
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
        if (_vk is null || _lightingBufferMemory.Handle == default)
            return;

        var ambient = framePlan.AmbientLights.Length > 0 ? framePlan.AmbientLights[^1] : default;
        var directional = framePlan.DirectionalLights.Length > 0 ? framePlan.DirectionalLights[^1] : default;
        var spot = framePlan.SpotLights.Length > 0 ? framePlan.SpotLights[^1] : default;
        var dir = directional.DirectionWorld;
        var dirLen = MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
        if (dirLen < 1e-6f)
            dir = new Vector2D<float>(0.25f, 0.35f);
        else
            dir = new Vector2D<float>(dir.X / dirLen, dir.Y / dirLen);
        var sdir = spot.DirectionWorld;
        var sLen = MathF.Sqrt(sdir.X * sdir.X + sdir.Y * sdir.Y);
        if (sLen < 1e-6f)
            sdir = new Vector2D<float>(1f, 0f);
        else
            sdir = new Vector2D<float>(sdir.X / sLen, sdir.Y / sLen);

        var ubo = new LightingUbo
        {
            Ambient = new Vector4D<float>(ambient.Color.X, ambient.Color.Y, ambient.Color.Z, ambient.Intensity),
            DirectionalDirIntensity = new Vector4D<float>(dir.X, dir.Y, directional.Intensity, 0f),
            DirectionalColor = new Vector4D<float>(directional.Color.X, directional.Color.Y, directional.Color.Z, directional.CastsShadow ? 1f : 0f),
            PointPosRadius = default,
            PointColorIntensity = default,
            PointFalloff = default,
            SpotPosRadius = new Vector4D<float>(spot.PositionWorld.X, spot.PositionWorld.Y, spot.Radius, spot.CastsShadow ? 1f : 0f),
            SpotDirCosOuter = new Vector4D<float>(sdir.X, sdir.Y, MathF.Cos(spot.InnerConeRadians), MathF.Cos(spot.OuterConeRadians)),
            SpotColorIntensity = new Vector4D<float>(spot.Color.X, spot.Color.Y, spot.Color.Z, spot.Intensity)
        };

        void* p;
        if (_vk.MapMemory(_device, _lightingBufferMemory, 0, (ulong)sizeof(LightingUbo), 0, &p) != Result.Success)
            throw new GraphicsInitializationException("map lighting ubo");
        Unsafe.Write(p, ubo);
        _vk.UnmapMemory(_device, _lightingBufferMemory);
    }

    private void UploadPointLightSsboData(in FramePlan framePlan)
    {
        if (_vk is null || _pointLightSsboMemory.Handle == default)
            return;

        var pts = framePlan.PointLights;
        var n = Math.Min(pts.Length, DeferredRenderingConstants.MaxPointLights);
        void* p;
        if (_vk.MapMemory(_device, _pointLightSsboMemory, 0, (ulong)(DeferredRenderingConstants.MaxPointLights * 2 * sizeof(Vector4D<float>)), 0, &p) != Result.Success)
            throw new GraphicsInitializationException("map point ssbo");

        var span = new Span<Vector4D<float>>((Vector4D<float>*)p, DeferredRenderingConstants.MaxPointLights * 2);
        span.Clear();
        for (var i = 0; i < n; i++)
        {
            ref readonly var pl = ref pts[i];
            var fall = pl.FalloffExponent > 1e-6f ? pl.FalloffExponent : 2f;
            span[i * 2] = new Vector4D<float>(pl.PositionWorld.X, pl.PositionWorld.Y, pl.Radius, fall);
            span[i * 2 + 1] = new Vector4D<float>(pl.Color.X, pl.Color.Y, pl.Color.Z, pl.Intensity);
        }

        _vk.UnmapMemory(_device, _pointLightSsboMemory);
    }
}
