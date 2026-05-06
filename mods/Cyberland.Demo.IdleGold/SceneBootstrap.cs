using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.IdleGold;

/// <summary>Handles produced by <see cref="SceneSetup.SetupSceneAsync"/> for mod wiring.</summary>
public sealed record SceneBootstrap(DocumentRefs Refs, EntityId SessionEntity);
