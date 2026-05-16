using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

public struct SessionTag : IComponent;

public struct ControlTag : IComponent;

public struct BackgroundTag : IComponent;

public struct BallTag : IComponent;

public struct TitleUiTag : IComponent;

public struct GameOverPanelTag : IComponent;

public struct GameOverBarTag : IComponent;

public struct HudTitleTag : IComponent;

public struct HudHintTitleTag : IComponent;

public struct HudGameOverTag : IComponent;

public struct HudHintEndTag : IComponent;

public struct HudPlayingScoreTag : IComponent;

public struct HudScoreNumTag : IComponent;

public struct HudFpsTag : IComponent;

public struct AmbientLightTag : IComponent;

/// <summary>Key directional for the arena.</summary>
public struct DirectionalLightTag : IComponent;

public struct ArenaSpotLightTag : IComponent;

public struct PaddlePointLightTag : IComponent;

public struct BallPointLightTag : IComponent;

/// <summary>Resolves the session entity after scene JSON spawn.</summary>
internal static class Session
{
    public static EntityId RequireStateEntity(World world) =>
        world.QueryChunks(SystemQuerySpec.All<SessionTag>())
            .RequireSingleEntityWith<SessionTag>("cyberland.demo.brick session");
}
