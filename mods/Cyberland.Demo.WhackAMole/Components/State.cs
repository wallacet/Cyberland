using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.WhackAMole.Components;

public enum WhackAMolePhase
{
    Ready = 0,
    Playing = 1,
    GameOver = 2
}

public struct State : IComponent
{
    public WhackAMolePhase Phase;
    public int Score;
    public float TimeRemainingSeconds;
    public bool TimerStarted;
}

public struct WhackAMoleTargetTag : IComponent;
public struct WhackAMoleScoreTextTag : IComponent;
public struct WhackAMoleTimerTextTag : IComponent;
public struct WhackAMoleOverlayTextTag : IComponent;
