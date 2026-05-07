namespace Cyberland.Engine.Rendering;

/// <summary>
/// Declares the intended high-level frame pass ordering for the deferred path.
/// This is a lightweight dependency model used by tests and debug assertions to keep
/// pass additions from silently violating required ordering constraints.
/// </summary>
internal static class RenderPassDependencyModel
{
    internal enum PassStage
    {
        EmissivePrepass = 0,
        GBufferOpaque = 1,
        DeferredLighting = 2,
        TransparentWboit = 3,
        TransparentResolve = 4,
        Bloom = 5,
        CompositeToSwapchain = 6
    }

    private static readonly (PassStage Before, PassStage After)[] RequiredEdges =
    [
        (PassStage.EmissivePrepass, PassStage.GBufferOpaque),
        (PassStage.GBufferOpaque, PassStage.DeferredLighting),
        (PassStage.DeferredLighting, PassStage.TransparentWboit),
        (PassStage.TransparentWboit, PassStage.TransparentResolve),
        (PassStage.TransparentResolve, PassStage.Bloom),
        (PassStage.Bloom, PassStage.CompositeToSwapchain)
    ];

    internal static ReadOnlySpan<(PassStage Before, PassStage After)> Dependencies => RequiredEdges;

    internal static bool IsExecutionOrderValid(ReadOnlySpan<PassStage> order)
    {
        Span<int> indexByStage = stackalloc int[7];
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
