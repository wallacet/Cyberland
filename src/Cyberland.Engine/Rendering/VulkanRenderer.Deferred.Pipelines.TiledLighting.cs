using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Glslang.NET;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace Cyberland.Engine.Rendering;

// Purpose: Tile SSBO lifecycle, descriptor set layout, pipeline, and per-frame tile bin upload for tiled deferred lighting.
// The tiled pass performs all deferred lighting in a single fullscreen triangle that reads per-tile light index
// lists built by TiledLightCullingCpu on the CPU.

/// <summary>Tiled deferred lighting resources and per-frame tile bin upload (partial).</summary>
public sealed unsafe partial class VulkanRenderer
{
    private void CreateTiledLightingResources()
    {
        EnsureTileSsbos();
        CreateTiledLightingDescriptorLayout();
        AllocateTiledLightingDescriptorSet();
        UpdateTiledLightingDescriptorSet();
    }

    private void EnsureTileSsbos()
    {
        if (_ssboTileBins.Handle != default)
            return;

        // Worst-case tile count capped by MaxTileGridCells.
        // Actual frame writes only touch the live tile count; stale tail bytes are harmless.
        const int maxTiles = DeferredRenderingConstants.MaxTileGridCells;
        var binBytes = (ulong)(maxTiles * sizeof(int) * 2);
        CreateHostVisibleBuffer(binBytes, BufferUsageFlags.StorageBufferBit, out _ssboTileBins, out _memTileBins);
        void* p;
        if (_vk!.MapMemory(_device, _memTileBins, 0, binBytes, 0, &p) != Result.Success)
            throw new GraphicsInitializationException("map tile bins ssbo (persistent)");
        _tileBinsMapped = p;

        var maxIndices = maxTiles * DeferredRenderingConstants.MaxLightsPerTile;
        var idxBytes = (ulong)(maxIndices * sizeof(int));
        CreateHostVisibleBuffer(idxBytes, BufferUsageFlags.StorageBufferBit, out _ssboTileIndices, out _memTileIndices);
        if (_vk.MapMemory(_device, _memTileIndices, 0, idxBytes, 0, &p) != Result.Success)
            throw new GraphicsInitializationException("map tile indices ssbo (persistent)");
        _tileIndicesMapped = p;

        // Spot light tile bins (same grid dimensions, different per-tile cap).
        CreateHostVisibleBuffer(binBytes, BufferUsageFlags.StorageBufferBit, out _ssboSpotTileBins, out _memSpotTileBins);
        if (_vk.MapMemory(_device, _memSpotTileBins, 0, binBytes, 0, &p) != Result.Success)
            throw new GraphicsInitializationException("map spot tile bins ssbo (persistent)");
        _spotTileBinsMapped = p;

        var maxSpotIndices = maxTiles * DeferredRenderingConstants.MaxSpotLightsPerTile;
        var spotIdxBytes = (ulong)(maxSpotIndices * sizeof(int));
        CreateHostVisibleBuffer(spotIdxBytes, BufferUsageFlags.StorageBufferBit, out _ssboSpotTileIndices, out _memSpotTileIndices);
        if (_vk.MapMemory(_device, _memSpotTileIndices, 0, spotIdxBytes, 0, &p) != Result.Success)
            throw new GraphicsInitializationException("map spot tile indices ssbo (persistent)");
        _spotTileIndicesMapped = p;
    }

    private void CreateTiledLightingDescriptorLayout()
    {
        if (_dslTiledLighting.Handle != default)
            return;

        // binding 0: LightingUbo (uniform buffer)
        // binding 1: DirectionalSsbo
        // binding 2: SpotSsbo
        // binding 3: PointSsbo
        // binding 4: TileBinSsbo (point)
        // binding 5: TileIndexSsbo (point)
        // binding 6: SpotTileBinSsbo
        // binding 7: SpotTileIndexSsbo
        Span<DescriptorSetLayoutBinding> bindings = stackalloc DescriptorSetLayoutBinding[8];
        bindings[0] = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit
        };
        for (var i = 1; i <= 7; i++)
        {
            bindings[i] = new DescriptorSetLayoutBinding
            {
                Binding = (uint)i,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit
            };
        }

        VulkanGraphicsPipelineHelpers.CreateDescriptorSetLayoutOrThrow(
            _vk!, _device, bindings, out _dslTiledLighting, "dsl tiled lighting failed.");
    }

    private void AllocateTiledLightingDescriptorSet()
    {
        if (_dsTiledLighting.Handle != default)
            return;

        fixed (DescriptorSetLayout* dsl = &_dslTiledLighting)
        {
            DescriptorSetAllocateInfo ai = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = dsl
            };
            if (_vk!.AllocateDescriptorSets(_device, in ai, out _dsTiledLighting) != Result.Success)
                throw new GraphicsInitializationException("alloc ds tiled lighting");
        }
    }

    private void UpdateTiledLightingDescriptorSet()
    {
        if (_vk is null || _dsTiledLighting.Handle == default)
            return;

        const int maxTiles = DeferredRenderingConstants.MaxTileGridCells;
        var binBytes = (ulong)(maxTiles * sizeof(int) * 2);
        var maxIndices = maxTiles * DeferredRenderingConstants.MaxLightsPerTile;
        var idxBytes = (ulong)(maxIndices * sizeof(int));
        var maxSpotIndices = maxTiles * DeferredRenderingConstants.MaxSpotLightsPerTile;
        var spotIdxBytes = (ulong)(maxSpotIndices * sizeof(int));

        DescriptorBufferInfo biUbo = new() { Buffer = _lightingBuffer, Offset = 0, Range = (ulong)sizeof(LightingUbo) };
        DescriptorBufferInfo biDir = new() { Buffer = _directionalLightSsbo, Offset = 0, Range = (ulong)(DeferredRenderingConstants.MaxDirectionalLights * 3 * sizeof(Vector4D<float>)) };
        DescriptorBufferInfo biSpot = new() { Buffer = _spotLightSsbo, Offset = 0, Range = (ulong)(DeferredRenderingConstants.MaxSpotLights * 4 * sizeof(Vector4D<float>)) };
        DescriptorBufferInfo biPt = new() { Buffer = _pointLightSsbo, Offset = 0, Range = (ulong)(DeferredRenderingConstants.MaxPointLights * 3 * sizeof(Vector4D<float>)) };
        DescriptorBufferInfo biBins = new() { Buffer = _ssboTileBins, Offset = 0, Range = binBytes };
        DescriptorBufferInfo biIdx = new() { Buffer = _ssboTileIndices, Offset = 0, Range = idxBytes };
        DescriptorBufferInfo biSpotBins = new() { Buffer = _ssboSpotTileBins, Offset = 0, Range = binBytes };
        DescriptorBufferInfo biSpotIdx = new() { Buffer = _ssboSpotTileIndices, Offset = 0, Range = spotIdxBytes };

        var writes = stackalloc WriteDescriptorSet[8];
        writes[0] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsTiledLighting, DstBinding = 0, DescriptorCount = 1, DescriptorType = DescriptorType.UniformBuffer, PBufferInfo = &biUbo };
        writes[1] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsTiledLighting, DstBinding = 1, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &biDir };
        writes[2] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsTiledLighting, DstBinding = 2, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &biSpot };
        writes[3] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsTiledLighting, DstBinding = 3, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &biPt };
        writes[4] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsTiledLighting, DstBinding = 4, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &biBins };
        writes[5] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsTiledLighting, DstBinding = 5, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &biIdx };
        writes[6] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsTiledLighting, DstBinding = 6, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &biSpotBins };
        writes[7] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _dsTiledLighting, DstBinding = 7, DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &biSpotIdx };
        _vk!.UpdateDescriptorSets(_device, 8, writes, 0, null);
    }

    private void CreateTiledLightingPipeline()
    {
        _modFragTiledDeferredLighting = CreateEngineShaderModule(
            EngineShaderSources.TiledDeferredLightingFrag, ShaderStage.Fragment, "shader.TiledDeferred.Frag");

        var pushRange = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = (uint)sizeof(TiledLightingPush)
        };

        var dslSets = stackalloc DescriptorSetLayout[3];
        dslSets[0] = _dslGbufferRead;
        dslSets[1] = _dslTiledLighting;
        dslSets[2] = _dslShadowSdf;

        PipelineLayoutCreateInfo plci = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 3,
            PSetLayouts = dslSets,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushRange
        };

        if (_vk!.CreatePipelineLayout(_device, in plci, null, out _plTiledDeferredLighting) != Result.Success)
            throw new GraphicsInitializationException("pl tiled deferred lighting failed.");

        var mainName = Marshal.StringToHGlobalAnsi("main");
        PipelineShaderStageCreateInfo vs = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = _modVertFullscreenTriangle,
            PName = (byte*)mainName
        };
        PipelineShaderStageCreateInfo fs = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = _modFragTiledDeferredLighting,
            PName = (byte*)mainName
        };

        VulkanGraphicsPipelineHelpers.CreateFullscreenTrianglePostProcessPipeline(
            _vk!, _device, _plTiledDeferredLighting, _rpOffscreenInitialUndefined, 0,
            vs, fs, out _pipeTiledDeferredLighting, "pipe tiled deferred lighting failed.");

        Marshal.FreeHGlobal(mainName);
    }

    private void DestroyTiledLightingResources()
    {
        if (_memTileBins.Handle != default && _tileBinsMapped != null)
        {
            _vk!.UnmapMemory(_device, _memTileBins);
            _tileBinsMapped = null;
        }
        if (_ssboTileBins.Handle != default)
        {
            _vk!.DestroyBuffer(_device, _ssboTileBins, null);
            _ssboTileBins = default;
        }
        if (_memTileBins.Handle != default)
        {
            _vk!.FreeMemory(_device, _memTileBins, null);
            _memTileBins = default;
        }

        if (_memTileIndices.Handle != default && _tileIndicesMapped != null)
        {
            _vk!.UnmapMemory(_device, _memTileIndices);
            _tileIndicesMapped = null;
        }
        if (_ssboTileIndices.Handle != default)
        {
            _vk!.DestroyBuffer(_device, _ssboTileIndices, null);
            _ssboTileIndices = default;
        }
        if (_memTileIndices.Handle != default)
        {
            _vk!.FreeMemory(_device, _memTileIndices, null);
            _memTileIndices = default;
        }

        if (_memSpotTileBins.Handle != default && _spotTileBinsMapped != null)
        {
            _vk!.UnmapMemory(_device, _memSpotTileBins);
            _spotTileBinsMapped = null;
        }
        if (_ssboSpotTileBins.Handle != default)
        {
            _vk!.DestroyBuffer(_device, _ssboSpotTileBins, null);
            _ssboSpotTileBins = default;
        }
        if (_memSpotTileBins.Handle != default)
        {
            _vk!.FreeMemory(_device, _memSpotTileBins, null);
            _memSpotTileBins = default;
        }

        if (_memSpotTileIndices.Handle != default && _spotTileIndicesMapped != null)
        {
            _vk!.UnmapMemory(_device, _memSpotTileIndices);
            _spotTileIndicesMapped = null;
        }
        if (_ssboSpotTileIndices.Handle != default)
        {
            _vk!.DestroyBuffer(_device, _ssboSpotTileIndices, null);
            _ssboSpotTileIndices = default;
        }
        if (_memSpotTileIndices.Handle != default)
        {
            _vk!.FreeMemory(_device, _memSpotTileIndices, null);
            _memSpotTileIndices = default;
        }
    }

    /// <summary>
    /// Runs <see cref="TiledLightCullingCpu.Bin"/> for the current frame's point lights and writes the results into
    /// the persistent mapped tile SSBOs. Called after <see cref="UploadPointLightSsboData"/>.
    /// Returns the resolved effective tile size and tile counts so callers reuse them for spot binning and push constants.
    /// </summary>
    private (int EffectiveTileSizePx, Vector2D<int> TileCounts) UpdateTileLightBins(in FramePlan framePlan)
    {
        var cam = framePlan.ShadowCamera;
        var (tileSizePx, tileCounts) = TiledLightCullingCpu.ResolveEffectiveTileGrid(
            in cam, DeferredRenderingConstants.TileSizeSwapchainPx);

        if (_tileBinsMapped == null || _tileIndicesMapped == null)
            return (tileSizePx, tileCounts);

        var maxPerTile = DeferredRenderingConstants.MaxLightsPerTile;
        var totalTiles = tileCounts.X * tileCounts.Y;

        var binSpan = new Span<TiledLightCullingCpu.TileBin>(_tileBinsMapped, totalTiles);
        var maxIndices = totalTiles * maxPerTile;
        var idxSpan = new Span<int>(_tileIndicesMapped, maxIndices);

        TiledLightCullingCpu.Bin(
            framePlan.PointLights,
            framePlan.PointLightCount,
            in cam,
            tileSizePx,
            maxPerTile,
            binSpan,
            idxSpan,
            _parallelism?.CreateParallelOptions());

        return (tileSizePx, tileCounts);
    }

    /// <summary>
    /// Runs <see cref="TiledLightCullingCpu.BinSpotLights"/> for the current frame's spot lights and writes the
    /// results into the persistent mapped spot tile SSBOs. Called after <see cref="UpdateTileLightBins"/>.
    /// </summary>
    private void UpdateSpotTileLightBins(in FramePlan framePlan, int effectiveTileSizePx, Vector2D<int> tileCounts)
    {
        if (_spotTileBinsMapped == null || _spotTileIndicesMapped == null)
            return;

        var cam = framePlan.ShadowCamera;
        var maxPerTile = DeferredRenderingConstants.MaxSpotLightsPerTile;
        var totalTiles = tileCounts.X * tileCounts.Y;

        var binSpan = new Span<TiledLightCullingCpu.TileBin>(_spotTileBinsMapped, totalTiles);
        var maxIndices = totalTiles * maxPerTile;
        var idxSpan = new Span<int>(_spotTileIndicesMapped, maxIndices);

        TiledLightCullingCpu.BinSpotLights(
            framePlan.SpotLights,
            framePlan.SpotLightCount,
            in cam,
            effectiveTileSizePx,
            maxPerTile,
            binSpan,
            idxSpan,
            _parallelism?.CreateParallelOptions());
    }

    private static TiledLightingPush BuildTiledLightingPush(in FramePlan plan, int effectiveTileSizePx, Vector2D<int> tileCounts)
    {
        var post = plan.ResolvedPost;

        return new TiledLightingPush
        {
            ScreenSizeSwapchainPx_Pad = new Vector4D<float>(plan.Screen.X, plan.Screen.Y, 0f, 0f),
            CameraPosWorld_CameraRotRad = new Vector4D<float>(
                plan.Camera.PositionWorld.X, plan.Camera.PositionWorld.Y, plan.Camera.RotationRadians, 0f),
            ViewportSizeWorld_PhysicalScale = new Vector4D<float>(
                plan.Camera.ViewportSizeWorld.X, plan.Camera.ViewportSizeWorld.Y, plan.Physical.Scale, 0f),
            PhysicalRectSwapchainPx = new Vector4D<float>(
                plan.Physical.OffsetPixels.X, plan.Physical.OffsetPixels.Y,
                plan.Physical.SizePixels.X, plan.Physical.SizePixels.Y),
            ShadowSettings = new Vector4D<float>(post.Shadows.Enabled ? 1f : 0f, 0f, 0f, 0f),
            TileSizeAndCounts = new Vector4D<float>(
                effectiveTileSizePx,
                tileCounts.X,
                tileCounts.Y,
                DeferredRenderingConstants.MaxLightsPerTile)
        };
    }

}
