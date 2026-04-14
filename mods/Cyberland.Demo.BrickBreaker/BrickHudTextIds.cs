using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Engine <see cref="Cyberland.Engine.Scene.BitmapText"/> entities for Brick HUD.</summary>
public readonly record struct BrickHudTextIds(
    EntityId Title,
    EntityId HintTitle,
    EntityId GameOver,
    EntityId HintGameOver,
    EntityId PlayingScore,
    EntityId ScoreNum,
    EntityId Fps);
