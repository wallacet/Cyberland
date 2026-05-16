using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Pong;

/// <summary>Scene JSON markers for Pong sprites and HUD rows (resolved via <see cref="WorldQueryExtensions.RequireSingleEntityWith{T}"/>).</summary>
public struct BackgroundSpriteTag : IComponent;

public struct TitleBarSpriteTag : IComponent;

public struct HintBarSpriteTag : IComponent;

public struct ScorePlayerSpriteTag : IComponent;

public struct ScoreCpuSpriteTag : IComponent;

public struct LeftPaddleSpriteTag : IComponent;

public struct RightPaddleSpriteTag : IComponent;

public struct BallSpriteTag : IComponent;

public struct HudTitleTextTag : IComponent;

public struct HudGameOverTextTag : IComponent;

public struct HudHintTextTag : IComponent;

public struct HudScoreYouTextTag : IComponent;

public struct HudScorePlayerNumTextTag : IComponent;

public struct HudScoreCpuLabelTextTag : IComponent;

public struct HudScoreCpuNumTextTag : IComponent;

public struct HudFpsTextTag : IComponent;
