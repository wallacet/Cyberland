using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase.Systems;

public sealed class SimulationSystem : ISystem, IFixedUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly EntityId _stateEntity;
    private readonly EntityId _controlEntity;
    private readonly EntityId _cameraEntity;
    private readonly EntityId _playerEntity;
    private readonly EntityId _collectibleEntity;
    private readonly EntityId _enterZoneEntity;
    private readonly EntityId _stayZoneEntity;
    private readonly EntityId _exitZoneEntity;
    private readonly EntityId _gateZoneEntity;
    private readonly Random _rng = new(424242);
    private World _world = null!;

    public SimulationSystem(EntityId stateEntity, EntityId controlEntity, EntityId cameraEntity, EntityId playerEntity, EntityId collectibleEntity,
        EntityId enterZoneEntity, EntityId stayZoneEntity, EntityId exitZoneEntity, EntityId gateZoneEntity)
    {
        _stateEntity = stateEntity;
        _controlEntity = controlEntity;
        _cameraEntity = cameraEntity;
        _playerEntity = playerEntity;
        _collectibleEntity = collectibleEntity;
        _enterZoneEntity = enterZoneEntity;
        _stayZoneEntity = stayZoneEntity;
        _exitZoneEntity = exitZoneEntity;
        _gateZoneEntity = gateZoneEntity;
    }

    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
    }

    public void OnFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds)
    {
        _ = query;
        ref var state = ref _world.Components<GameState>().Get(_stateEntity);
        ref readonly var control = ref _world.Components<ControlState>().Get(_controlEntity);
        ref var playerTransform = ref _world.Components<Transform>().Get(_playerEntity);
        ref var camera = ref _world.Components<Camera2D>().Get(_cameraEntity);

        if (control.RestartPressed)
            ResetState(ref state, ref playerTransform);

        if (state.Phase is RoundPhase.Won or RoundPhase.Lost)
            return;

        var toMouse = control.MouseWorld - playerTransform.WorldPosition;
        var len = MathF.Sqrt(toMouse.X * toMouse.X + toMouse.Y * toMouse.Y);
        if (len > 1f)
        {
            var speed = control.PrimaryPressed ? 420f : 300f;
            var dir = toMouse / len;
            playerTransform.WorldPosition += dir * speed * fixedDeltaSeconds;
            playerTransform.LocalPosition = playerTransform.WorldPosition;
        }

        if (MathF.Abs(control.ZoomDelta) > 0.001f)
        {
            var step = control.ZoomDelta > 0f ? 0.93f : 1.07f;
            var width = Math.Clamp((int)(camera.ViewportSizeWorld.X * step), 880, 1600);
            var height = Math.Clamp((int)(camera.ViewportSizeWorld.Y * step), 500, 900);
            camera.ViewportSizeWorld = new Vector2D<int>(width, height);
        }

        state.TimerSeconds -= fixedDeltaSeconds;
        if (state.TimerSeconds <= 0f)
        {
            state.Phase = RoundPhase.Lost;
            return;
        }

        ResolveTriggerEvents(ref state);
        if (state.Health <= 0f)
        {
            state.Phase = RoundPhase.Lost;
            return;
        }

        if (state.Phase is RoundPhase.Won or RoundPhase.Lost)
            return;

        if (state.Phase == RoundPhase.Tutorial
            && state.Score >= state.TargetScore
            && state.EnterZoneSeen
            && state.StayZoneSeen
            && state.ExitZoneSeen)
            state.Phase = RoundPhase.Playing;
    }

    private void ResolveTriggerEvents(ref GameState state)
    {
        if (!_world.Components<TriggerEvents>().TryGet(_playerEntity, out var triggerEvents) || triggerEvents.Events is null)
            return;

        foreach (var ev in triggerEvents.Events)
        {
            if (ev.Other == _collectibleEntity && ev.Kind == TriggerEventKind.OnTriggerEnter)
            {
                state.Score += 10;
                state.LocaleSpriteSeen = true;
                RespawnCollectible();
                continue;
            }

            if (ev.Other == _enterZoneEntity && ev.Kind == TriggerEventKind.OnTriggerEnter)
            {
                state.EnterZoneSeen = true;
                continue;
            }

            if (ev.Other == _stayZoneEntity && ev.Kind == TriggerEventKind.OnTriggerStay)
            {
                state.StayZoneSeen = true;
                state.Score += 1;
                continue;
            }

            if (ev.Other == _exitZoneEntity && ev.Kind == TriggerEventKind.OnTriggerExit)
            {
                state.ExitZoneSeen = true;
                state.Health -= 15f;
                continue;
            }

            if (ev.Other == _gateZoneEntity && ev.Kind == TriggerEventKind.OnTriggerEnter && state.Score >= state.TargetScore)
            {
                state.Phase = RoundPhase.Won;
            }
        }
    }

    private void RespawnCollectible()
    {
        ref var collectible = ref _world.Components<Transform>().Get(_collectibleEntity);
        collectible.WorldPosition = new Vector2D<float>(
            (float)_rng.NextDouble() * 980f + 150f,
            (float)_rng.NextDouble() * 500f + 120f);
        collectible.LocalPosition = collectible.WorldPosition;
    }

    private void ResetState(ref GameState state, ref Transform playerTransform)
    {
        state.Phase = RoundPhase.Tutorial;
        state.TutorialStep = 0;
        state.TimerSeconds = 70f;
        state.Health = 100f;
        state.Score = 0;
        state.TargetScore = 140;
        state.EnterZoneSeen = false;
        state.StayZoneSeen = false;
        state.ExitZoneSeen = false;
        state.LocaleSpriteSeen = false;
        playerTransform.WorldPosition = new Vector2D<float>(260f, 360f);
        playerTransform.LocalPosition = playerTransform.WorldPosition;
        RespawnCollectible();
    }

}
