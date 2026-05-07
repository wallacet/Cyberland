namespace Cyberland.Engine.UI.Core;

/// <summary>Shared numeric thresholds for retained UI layout calculations.</summary>
internal static class UiLayoutConstants
{
    /// <summary>Anchor/size epsilon used to distinguish collapsed versus stretched axes.</summary>
    public const float AxisEpsilon = 1e-4f;

    /// <summary>Upper bound for fixed-height floor application in collapsed vertical layouts.</summary>
    public const float CollapsedHeightFloorMaxPx = 256f;
}
