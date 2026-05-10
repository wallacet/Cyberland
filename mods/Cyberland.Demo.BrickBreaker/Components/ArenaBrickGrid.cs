using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Dense brick entity lookup authored in <see cref="SceneSetup"/>; index <c>cx + cy * <see cref="Constants.Cols"/></c>.
/// </summary>
/// <remarks>
/// Avoids per-hit trigger overlap work: gameplay resolves ball-vs-brick with circle/AABB against layout metrics in
/// <see cref="GameState"/> instead of registering brick <see cref="Cyberland.Engine.Scene.Trigger"/> volumes.
/// </remarks>
public struct ArenaBrickGrid : IComponent
{
    /// <summary>Length <see cref="Constants.Cols"/> × <see cref="Constants.Rows"/>; parallel to cell coordinates.</summary>
    public EntityId[] CellEntities;
}
