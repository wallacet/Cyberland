using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Cached entity ids for arena lights, paddle, and ball—authored in <see cref="SceneSetup"/> so
/// <see cref="LightsFillSystem"/> only reads one row per frame.
/// </summary>
public struct ArenaLightRuntime : IComponent
{
    public EntityId Paddle;
    public EntityId Ball;
    public EntityId Ambient;
    public EntityId Directional;
    public EntityId Spot;
    public EntityId PaddlePoint;
    public EntityId BallPoint;
}
