using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

namespace Cyberland.Engine.UI.Ecs;

/// <summary>
/// Marks an ECS entity that owns a <see cref="Cyberland.Engine.UI.Core.UiDocument"/> registered on <see cref="Cyberland.Engine.Hosting.GameHostServices.UiDocuments"/>.
/// </summary>
/// <remarks>
/// Assign one <see cref="UiDocumentRoot"/> per retained UI screen (main menu, inventory, toast overlay). The host-driven
/// <see cref="Cyberland.Engine.Scene.Systems.UiDocumentFrameSystem"/> then runs layout, pointer routing, and draw for each
/// visible registration once per render tick.
/// </remarks>
public struct UiDocumentRoot : IComponent
{
    /// <summary>When false, layout, pointer routing, and draws are skipped for this registration.</summary>
    public bool Visible;

    /// <summary>Sprite coordinate space for submitted quads from this document tree.</summary>
    public CoordinateSpace CoordinateSpace;

    /// <summary>How to derive the root layout rectangle.</summary>
    public UiDocumentRootPreset RootPreset;

    /// <summary>Added to per-element sort offsets so overlapping HUD documents stack predictably.</summary>
    public float SortKeyBase;
}
