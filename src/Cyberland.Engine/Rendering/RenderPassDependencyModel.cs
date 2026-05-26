namespace Cyberland.Engine.Rendering;

/// <summary>
/// Declares the intended high-level frame pass ordering for the deferred path with SDF shadows.
/// This is a lightweight dependency model used by tests and debug assertions to keep pass additions from silently
/// violating required ordering constraints.
/// </summary>
/// <remarks>
/// Main chain: <c>EmissivePrepass</c> → <c>GBufferOpaque</c> → <c>ShadowSdf</c> → <c>DeferredLighting</c> →
/// <c>TransparentWboit</c> → <c>TransparentResolve</c> → <c>Bloom</c> → <c>CompositeToSwapchain</c>.
/// Independent branch: <c>TileCull</c> → <c>DeferredLighting</c> (TileCull runs independently of ShadowSdf;
/// both must complete before DeferredLighting).
/// </remarks>
internal static class RenderPassDependencyModel
{
    internal enum PassStage
    {
        EmissivePrepass = 0,
        GBufferOpaque = 1,
        ShadowSdf = 2,
        TileCull = 3,
        DeferredLighting = 4,
        TransparentWboit = 5,
        TransparentResolve = 6,
        Bloom = 7,
        CompositeToSwapchain = 8
    }

    private static readonly (PassStage Before, PassStage After)[] RequiredEdges =
    [
        (PassStage.EmissivePrepass, PassStage.GBufferOpaque),
        (PassStage.GBufferOpaque, PassStage.ShadowSdf),
        (PassStage.ShadowSdf, PassStage.DeferredLighting),
        (PassStage.TileCull, PassStage.DeferredLighting),
        (PassStage.DeferredLighting, PassStage.TransparentWboit),
        (PassStage.TransparentWboit, PassStage.TransparentResolve),
        (PassStage.TransparentResolve, PassStage.Bloom),
        (PassStage.Bloom, PassStage.CompositeToSwapchain)
    ];

    internal static ReadOnlySpan<(PassStage Before, PassStage After)> Dependencies => RequiredEdges;

    internal static bool IsExecutionOrderValid(ReadOnlySpan<PassStage> order)
    {
        Span<int> indexByStage = stackalloc int[9];
        indexByStage.Fill(-1);
        for (var i = 0; i < order.Length; i++)
        {
            var idx = (int)order[i];
            if (indexByStage[idx] >= 0)
                return false;
            indexByStage[idx] = i;
        }

        foreach (var edge in RequiredEdges)
        {
            var before = indexByStage[(int)edge.Before];
            var after = indexByStage[(int)edge.After];
            if (before < 0 || after < 0 || before >= after)
                return false;
        }

        return true;
    }
}
