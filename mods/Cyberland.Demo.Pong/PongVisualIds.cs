using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Pong;

/// <summary>Sprite entities drawn by the engine <see cref="Cyberland.Engine.Scene2D.Systems.SpriteRenderSystem"/> after <see cref="PongVisualSyncSystem"/> updates them.</summary>
public readonly record struct PongVisualIds(
    EntityId Background,
    EntityId TitleBar,
    EntityId HintBar,
    EntityId ScorePlayer,
    EntityId ScoreCpu,
    EntityId LeftPad,
    EntityId RightPad,
    EntityId Ball);
