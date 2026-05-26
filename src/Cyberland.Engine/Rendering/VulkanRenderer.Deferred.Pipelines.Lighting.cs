using System.Runtime.CompilerServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Rendering;

// Purpose: Lighting uniform buffer and point/directional/spot SSBO upload used by deferred passes.
//
// COORDINATE SPACE
// ----------------
// Point and spot attenuation is computed in WORLD space (zoom-invariant). Spot SSBO rows also carry a swapchain-pixel
// position (row 0) for viewport culling, but radial falloff uses the world radius in row 3.
// `WorldLightToSwapchainPixel` delegates to `ShadowSdfCamera.WorldToSwapchainPx` — the single canonical
// world→swapchain conversion site; shader code uses the shared `worldToSwapchainPx` helper.

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
        var ambientCount = System.Math.Min(framePlan.AmbientLightCount, DeferredRenderingConstants.MaxAmbientLights);
        for (var i = 0; i < ambientCount; i++)
        {
            var a = framePlan.AmbientLights[i];
            ar += a.Color.X * a.Intensity;
            ag += a.Color.Y * a.Intensity;
            ab += a.Color.Z * a.Intensity;
        }

        var nDir = framePlan.DirectionalLightCount;
        var nSpot = framePlan.SpotLightCount;
        var ubo = new LightingUbo
        {
            Ambient = new Vector4D<float>(ar, ag, ab, 1f),
            Counts = new Vector4D<float>(nDir, nSpot, 0f, 0f)
        };
        Unsafe.Write(_lightingBufferMapped, ubo);
        UploadDirectionalSpotLightSsboData(in framePlan, nDir, nSpot);
    }

    private static Vector2D<float> WorldLightToSwapchainPixel(Vector2D<float> positionWorld, in FramePlan plan)
        => plan.ShadowCamera.WorldToSwapchainPx(positionWorld);

    // Stale tail rows beyond nDir/nSpot are safe because the tiled deferred lighting shader reads
    // counts from the UBO and iterates only that many SSBO entries; full clear is unnecessary.
    private void UploadDirectionalSpotLightSsboData(in FramePlan framePlan, int nDir, int nSpot)
    {
        if (_directionalLightSsboMapped is null || _spotLightSsboMapped is null)
            return;

        var camRot = framePlan.Camera.RotationRadians;
        var cr = MathF.Cos(-camRot);
        var sr = MathF.Sin(-camRot);
        if (nDir > 0)
        {
            // 3 vec4 per directional:
            //  row 0: camera-rotated dir (.xy) + intensity (.z)  — used for N·L in shader
            //  row 1: color (.rgb) + castsShadow flag (.w, 1/0)
            //  row 2: world direction (.xy) + padding            — used for SDF shadow trace
            var dirSpan = new Span<Vector4D<float>>(
                (Vector4D<float>*)_directionalLightSsboMapped, DeferredRenderingConstants.MaxDirectionalLights * 3);
            var src = framePlan.DirectionalLights;
            for (var i = 0; i < nDir; i++)
            {
                ref readonly var d = ref src[i];
                var wWorld = d.DirectionWorld;
                var len = MathF.Sqrt(wWorld.X * wWorld.X + wWorld.Y * wWorld.Y);
                if (!float.IsFinite(len) || len < 1e-6f)
                    wWorld = new Vector2D<float>(0.25f, 0.35f);
                else
                    wWorld = new Vector2D<float>(wWorld.X / len, wWorld.Y / len);
                var rx = wWorld.X * cr - wWorld.Y * sr;
                var ry = wWorld.X * sr + wWorld.Y * cr;
                dirSpan[i * 3] = new Vector4D<float>(rx, ry, d.Intensity, 0f);
                dirSpan[i * 3 + 1] = new Vector4D<float>(d.Color.X, d.Color.Y, d.Color.Z, d.CastsShadow ? 1f : 0f);
                dirSpan[i * 3 + 2] = new Vector4D<float>(wWorld.X, wWorld.Y, 0f, 0f);
            }
        }

        if (nSpot > 0)
        {
            // 4 vec4 per spot:
            //  row 0: posSwapchainPx.xy, radiusSwapchainPx, castsShadow (1/0)
            //  row 1: dirWorld.xy, innerCos, outerCos
            //  row 2: color.rgb, intensity
            //  row 3: posWorld.xy, worldRadius, falloffExponent
            var spotSpan = new Span<Vector4D<float>>(
                (Vector4D<float>*)_spotLightSsboMapped, DeferredRenderingConstants.MaxSpotLights * 4);
            var ssrc = framePlan.SpotLights;
            for (var j = 0; j < nSpot; j++)
            {
                ref readonly var sp = ref ssrc[j];
                var sdir = sp.DirectionWorld;
                var sLen = MathF.Sqrt(sdir.X * sdir.X + sdir.Y * sdir.Y);
                if (!float.IsFinite(sLen) || sLen < 1e-6f)
                    sdir = new Vector2D<float>(1f, 0f);
                else
                    sdir = new Vector2D<float>(sdir.X / sLen, sdir.Y / sLen);
                var swPos = WorldLightToSwapchainPixel(sp.PositionWorld, in framePlan);
                var radiusPx = sp.Radius * framePlan.Physical.Scale;
                var cosInner = float.IsNaN(sp.CosInnerCone) ? MathF.Cos(sp.InnerConeRadians) : sp.CosInnerCone;
                var cosOuter = float.IsNaN(sp.CosOuterCone) ? MathF.Cos(sp.OuterConeRadians) : sp.CosOuterCone;
                var fall = sp.FalloffExponent > 1e-6f ? sp.FalloffExponent : 2f;
                spotSpan[j * 4] = new Vector4D<float>(swPos.X, swPos.Y, radiusPx, sp.CastsShadow ? 1f : 0f);
                spotSpan[j * 4 + 1] = new Vector4D<float>(sdir.X, sdir.Y, cosInner, cosOuter);
                spotSpan[j * 4 + 2] = new Vector4D<float>(sp.Color.X, sp.Color.Y, sp.Color.Z, sp.Intensity);
                spotSpan[j * 4 + 3] = new Vector4D<float>(sp.PositionWorld.X, sp.PositionWorld.Y, sp.Radius, fall);
            }
        }
    }

    // Stale tail rows beyond PointLightCount are safe because the tiled deferred lighting shader
    // iterates only the per-tile bin count entries; full clear is unnecessary.
    private void UploadPointLightSsboData(in FramePlan framePlan)
    {
        if (_pointLightSsboMapped == null)
            return;

        var pts = framePlan.PointLights;
        var n = framePlan.PointLightCount;
        var span = new Span<Vector4D<float>>(
            (Vector4D<float>*)_pointLightSsboMapped,
            DeferredRenderingConstants.MaxPointLights * 3);
        for (var i = 0; i < n; i++)
        {
            ref readonly var pl = ref pts[i];
            var fall = pl.FalloffExponent > 1e-6f ? pl.FalloffExponent : 2f;
            // Row 0: WORLD position (.xy) + WORLD radius (.z) + falloff exponent (.w).
            // The tiled deferred lighting fragment shader reads world position for attenuation and
            // feeds it into the SDF cone-trace for shadow visibility.
            span[i * 3] = new Vector4D<float>(pl.PositionWorld.X, pl.PositionWorld.Y, pl.Radius, fall);
            span[i * 3 + 1] = new Vector4D<float>(pl.Color.X, pl.Color.Y, pl.Color.Z, pl.Intensity);
            span[i * 3 + 2] = new Vector4D<float>(pl.CastsShadow ? 1f : 0f, 0f, 0f, 0f);
        }
    }
}
