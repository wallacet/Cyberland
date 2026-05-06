namespace Cyberland.Engine.UI.Core;

/// <summary>
/// Global switch for <see cref="Cyberland.Engine.Scene.Systems.UiDocumentFrameSystem"/> incremental layout (skip
/// <see cref="UiDocument.MeasureArrange(in UiRect)"/> when the document reports clean and the root rect is unchanged).
/// </summary>
/// <remarks>
/// <see cref="ApplyEnvironmentDefaults"/> reads <c>CYBERLAND_USE_INCREMENTAL_UI</c>: <c>0</c>/<c>false</c> disables
/// incremental frames (full measure every tick for A/B profiling); <c>1</c>/<c>true</c> enables. Empty/unset leaves
/// the current property value (default true). Called from <see cref="Cyberland.Engine.GameApplication"/> startup.
/// </remarks>
public static class UiLayoutGating
{
    /// <summary>When true (default), documents skip full measure when not dirty.</summary>
    public static bool UseIncrementalDocumentFrames { get; set; } = true;

    /// <summary>Applies <c>CYBERLAND_USE_INCREMENTAL_UI</c> once per process (safe to call multiple times).</summary>
    public static void ApplyEnvironmentDefaults()
    {
        var v = Environment.GetEnvironmentVariable("CYBERLAND_USE_INCREMENTAL_UI");
        if (string.IsNullOrWhiteSpace(v))
            return;

        if (v.Equals("0", StringComparison.OrdinalIgnoreCase) ||
            v.Equals("false", StringComparison.OrdinalIgnoreCase))
            UseIncrementalDocumentFrames = false;
        else if (v.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                 v.Equals("true", StringComparison.OrdinalIgnoreCase))
            UseIncrementalDocumentFrames = true;
    }
}
