using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase.Systems;

internal static class MouseChaseRoundLogic
{
    public const float RoundDurationSeconds = 70f;
    public const float StartingHealth = 100f;
    public const int StartingTargetScore = 140;

    public static void ResetState(ref GameState state)
    {
        state.Phase = RoundPhase.Tutorial;
        state.TutorialStep = 0;
        state.TimerSeconds = RoundDurationSeconds;
        state.Health = StartingHealth;
        state.Score = 0;
        state.TargetScore = StartingTargetScore;
        state.EnterZoneSeen = false;
        state.StayZoneSeen = false;
        state.ExitZoneSeen = false;
        state.LocaleSpriteSeen = false;
    }

    public static void RespawnCollectible(ref Transform collectible, Random rng)
    {
        collectible.WorldPosition = new Vector2D<float>(
            (float)rng.NextDouble() * 980f + 150f,
            (float)rng.NextDouble() * 500f + 120f);
        collectible.LocalPosition = collectible.WorldPosition;
    }
}
