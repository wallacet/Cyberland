using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Baseline HDR post settings applied when the renderer becomes available. Mods may call
/// <see cref="IRenderer.SetGlobalPostProcess"/> later (e.g. in <see cref="Modding.IMod.OnLoad"/>) to replace them.
/// </summary>
public static class EngineDefaultGlobalPostProcess
{
    /// <summary>Engine baseline; not pushed every frame.</summary>
    public static GlobalPostProcessSettings DefaultSettings => new()
    {
        BloomEnabled = true,
        BloomRadius = 1.1f,
        BloomGain = 0.28f,
        BloomExtractThreshold = 0.32f,
        BloomExtractKnee = 0.5f,
        EmissiveToHdrGain = 0.45f,
        EmissiveToBloomGain = 0.45f,
        Exposure = 1f,
        Saturation = 1.04f,
        TonemapEnabled = true,
        ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f),
        ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f),
        ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
    };

    /// <summary>Applies <see cref="DefaultSettings"/> if <paramref name="renderer"/> is non-null.</summary>
    public static void Apply(IRenderer? renderer)
    {
        if (renderer is null)
            return;
        var s = DefaultSettings;
        renderer.SetGlobalPostProcess(in s);
    }
}
