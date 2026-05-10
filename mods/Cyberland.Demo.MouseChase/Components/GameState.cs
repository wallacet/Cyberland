using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.MouseChase.Components;

public enum RoundPhase
{
    Tutorial = 0,
    Playing = 1,
    Won = 2,
    Lost = 3
}

public struct GameState : IComponent
{
    public RoundPhase Phase;
    public int TutorialStep;
    public float TimerSeconds;
    public float Health;
    public int Score;
    public int TargetScore;
    public bool EnterZoneSeen;
    public bool StayZoneSeen;
    public bool ExitZoneSeen;
    public bool LocaleSpriteSeen;

    /// <summary>Set from early input while the round is won/lost; consumed when fixed update resets the round.</summary>
    public bool PendingRestartRequest;
}
