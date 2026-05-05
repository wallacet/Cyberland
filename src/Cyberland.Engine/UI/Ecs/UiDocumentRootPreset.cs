namespace Cyberland.Engine.UI.Ecs;

/// <summary>
/// How an ECS-driven <see cref="UiDocumentRoot"/> resolves its layout rectangle each frame.
/// </summary>
public enum UiDocumentRootPreset
{
    /// <summary>
    /// Full virtual viewport: available rect is <c>(0,0)</c>–<see cref="Cyberland.Engine.Rendering.IRenderer.ActiveCameraViewportSize"/> (+Y down).
    /// </summary>
    FullViewport,
}
