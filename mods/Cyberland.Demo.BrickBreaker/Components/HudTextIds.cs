using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>BitmapText entity handles for the HUD (not registered as components).</summary>
public readonly record struct HudTextIds(
    EntityId Title,
    EntityId HintTitle,
    EntityId GameOver,
    EntityId HintGameOver,
    EntityId PlayingScore,
    EntityId ScoreNum,
    EntityId Fps);
