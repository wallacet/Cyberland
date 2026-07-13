using Cyberland.Demo.Audio.Components;
using Cyberland.Engine.Audio;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Audio.Systems;

/// <summary>
/// Demo input: UI click, footsteps cue, dialogue duck, spam/steal, music, cinematic, listener, pause, move.
/// </summary>
public sealed class AudioDemoInputSystem : ISingletonSystem, ISingletonEarlyUpdate, ISingletonLateUpdate
{
    private readonly GameHostServices _host;
    private bool _combatMusic;
    private string _status = "Audio demo ready";

    /// <inheritdoc />
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, Transform, AudioListenerOverride>();

    /// <summary>Creates the demo input system.</summary>
    public AudioDemoInputSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity singleton)
    {
        _ = singleton;
        var audio = _host.Audio;
        audio.RegisterBus("dialogue");
        audio.RegisterBus("footsteps", new BusRegistration
        {
            DefaultGain = 0.85f,
            MaxVoices = 8,
            StealMode = VoiceStealMode.StealLowestPriority,
        });
        audio.RegisterDuckRule(new AudioDuckRule("dialogue", AudioBusIds.Music, 0.35f, 0.1f, 0.4f));
        audio.SetFocusPolicy(AudioFocusPolicy.DuckMaster(0.2f));
        audio.RegisterCue("footstep.concrete", new AudioCueDesc
        {
            BusId = "footsteps",
            Space = AudioSpace.World,
            ClipPaths =
            [
                "Sounds/foley/footstep_01.wav",
                "Sounds/foley/footstep_02.wav",
                "Sounds/foley/footstep_03.wav",
            ],
            PitchMin = 0.95f,
            PitchMax = 1.05f,
            GainMin = 0.9f,
            GainMax = 1f,
            MaxInstances = 4,
            CooldownSeconds = 0.08f,
            Priority = 10,
            PickMode = AudioCueVariation.PickMode.Random,
            RefDistance = 64f,
            MaxDistance = 480f,
            Rolloff = 1f,
        });
    }

    /// <inheritdoc />
    public void OnSingletonEarlyUpdate(in SingletonEntity singleton, float deltaSeconds)
    {
        var input = _host.Input;
        var audio = _host.Audio;
        ref var tf = ref singleton.Get<Transform>();

        var mx = input.ReadAxis("cyberland.demo.audio/move_x");
        var my = input.ReadAxis("cyberland.demo.audio/move_y");
        if (mx != 0f || my != 0f)
        {
            var pos = tf.LocalPosition;
            pos.X += mx * 220f * deltaSeconds;
            pos.Y += my * 220f * deltaSeconds;
            tf.LocalPosition = pos;
        }

        if (input.ConsumePressed("cyberland.demo.audio/ui_click"))
        {
            audio.PlayOneShot(new OneShotRequest
            {
                ClipPath = "Sounds/ui/click.wav",
                Space = AudioSpace.Direct,
                BusId = AudioBusIds.Ui,
                Gain = 1f,
                PauseWithGameplay = false,
                ApplyTimeScale = false,
                Priority = 50,
            });
            _status = "UI click (Direct / ui bus)";
        }

        if (input.ConsumePressed("cyberland.demo.audio/footstep"))
        {
            audio.PlayCue("footstep.concrete", new PlayCueRequest
            {
                HasSpace = true,
                Space = AudioSpace.World,
                PositionWorld = tf.WorldPosition,
                Priority = int.MinValue,
            });
            _status = "Footstep cue";
        }

        if (input.ConsumePressed("cyberland.demo.audio/spam"))
        {
            for (var i = 0; i < 12; i++)
            {
                audio.PlayOneShot(new OneShotRequest
                {
                    ClipPath = "Sounds/combat/hit.wav",
                    Space = AudioSpace.Direct,
                    BusId = AudioBusIds.Sfx,
                    Gain = 0.4f,
                    Priority = i,
                    PauseWithGameplay = true,
                });
            }

            audio.GetStats(out var stats);
            _status = $"Spam SFX — voices={stats.ActiveVoices} steals~={stats.StealsRecent}";
        }

        if (input.ConsumePressed("cyberland.demo.audio/music_toggle"))
        {
            _combatMusic = !_combatMusic;
            audio.CrossfadeMusic(new MusicRequest
            {
                ClipPath = _combatMusic ? "Sounds/music/combat.wav" : "Sounds/music/explore.wav",
                BusId = AudioBusIds.Music,
                Loop = true,
                FadeInSeconds = 0.8f,
                PauseWithGameplay = false,
            }, fadeOutSeconds: 0.8f);
            _status = _combatMusic ? "Music → combat" : "Music → explore";
        }

        if (input.ConsumePressed("cyberland.demo.audio/cinematic"))
        {
            audio.PlayOneShot(new OneShotRequest
            {
                ClipPath = "Sounds/cinematic/sting.wav",
                Space = AudioSpace.Cinematic,
                BusId = AudioBusIds.Cinematic,
                PauseWithGameplay = false,
                Priority = 80,
            });
            _status = "Cinematic sting";
        }

        if (input.ConsumePressed("cyberland.demo.audio/listener_toggle"))
        {
            ref var listener = ref singleton.Get<AudioListenerOverride>();
            listener.Active = !listener.Active;
            _status = listener.Active ? "Listener = player" : "Listener = camera";
        }

        if (input.ConsumePressed("cyberland.demo.audio/pause_toggle"))
        {
            _host.SessionClock.Paused = !_host.SessionClock.Paused;
            _status = _host.SessionClock.Paused ? "Gameplay paused (UI still plays)" : "Gameplay resumed";
        }

        if (input.ConsumePressed("cyberland.demo.audio/dialogue"))
        {
            audio.PlayOneShot(new OneShotRequest
            {
                ClipPath = "Sounds/vo/vendor_greet.wav",
                Space = AudioSpace.Direct,
                BusId = "dialogue",
                Gain = 1f,
                PauseWithGameplay = false,
                Priority = 90,
            });
            _status = "Dialogue (ducks music)";
        }
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity singleton, float deltaSeconds)
    {
        _ = singleton;
        _ = deltaSeconds;
        DemoStatus.Text = _status + $" | ready={_host.Audio.IsReady}";
        _host.Audio.GetStats(out var stats);
        DemoStatus.Stats = $"voices={stats.ActiveVoices} steals={stats.StealsRecent} buses={stats.RegisteredBusCount}";
    }
}

/// <summary>Simple cross-system status for the demo HUD.</summary>
public static class DemoStatus
{
    /// <summary>Primary status line.</summary>
    public static string Text { get; set; } = "";

    /// <summary>Mixer stats line.</summary>
    public static string Stats { get; set; } = "";
}
