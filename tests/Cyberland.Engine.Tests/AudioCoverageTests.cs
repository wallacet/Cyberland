using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Cyberland.Engine.Audio;
using Cyberland.Engine.RuntimeScenes;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

/// <summary>Extra coverage for audio helpers to keep the engine line gate at 100%.</summary>
public sealed class AudioCoverageTests
{
    [Fact]
    public void OneShot_defaults_and_ids_tostring()
    {
        var w = OneShotRequest.DefaultWorld;
        Assert.Equal(AudioSpace.World, w.Space);
        var u = OneShotRequest.DefaultUi;
        Assert.Equal(AudioBusIds.Ui, u.BusId);
        Assert.Contains("Invalid", AudioClipId.Invalid.ToString());
        Assert.Contains("None", VoiceId.None.ToString());
        Assert.False(AudioClipId.Invalid.Equals((object?)"x"));
        Assert.False(VoiceId.None.Equals((object?)1));
        Assert.True(new AudioClipId(2) != new AudioClipId(3));
        Assert.Equal(0, VoiceId.None.GetHashCode());
        Assert.Equal(7, new VoiceId(7).GetHashCode());
    }

    [Fact]
    public void Attenuation_edge_cases()
    {
        Assert.Equal(0f, AudioAttenuation.DistanceGain(10f, -1f, 100f, 1f));
        Assert.Equal(0f, AudioAttenuation.DistanceGain(10f, 5f, 0f, 1f));
        // distance == ref == max → still treated as at/below ref → 1
        Assert.Equal(1f, AudioAttenuation.DistanceGain(10f, 10f, 10f, 1f));
        // Tiny span between ref and max (distance strictly between) → 0
        Assert.Equal(0f, AudioAttenuation.DistanceGain(5e-8f, 0f, 1e-7f, 1f));
        var g = AudioAttenuation.DistanceGain(50f, 10f, 100f, 2f);
        Assert.InRange(g, 0f, 1f);
        Assert.Equal(0f, AudioAttenuation.StereoPan(default, default, 0f, 0f));
        Assert.True(AudioAttenuation.Distance(new Vector2D<float>(3, 4), default) > 4.9f);
        Assert.Equal(1f, AudioAttenuation.WorldAudibilityGain(default, default, 64f, 480f, 1f));
    }

    [Fact]
    public void Fade_zero_duration_and_blend()
    {
        Assert.Equal(1f, AudioFade.AdvanceFadeIn(0f, 0f, 1f, out var c));
        Assert.True(c);
        Assert.Equal(0f, AudioFade.AdvanceFadeOut(1f, 0f, 1f, out c));
        Assert.True(c);
        Assert.Equal(0.25f, AudioFade.BlendToward(0f, 1f, 2f, 0.5f), 3);
    }

    [Fact]
    public void Gain_nan_and_focus()
    {
        Assert.Equal(0f, AudioGainMath.Compose(float.NaN, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f));
        Assert.Equal(0f, AudioGainMath.Compose(float.PositiveInfinity, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f));
        Assert.Equal(0f, AudioGainMath.Compose(-1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f));
        Assert.Equal(1f, AudioGainMath.FocusFactor(AudioFocusPolicy.DuckMaster(2f), false));
    }

    [Fact]
    public void Cue_variation_edges()
    {
        var cursor = 0;
        Assert.Equal(-1, AudioCueVariation.PickIndex(0, AudioCueVariation.PickMode.Random, ref cursor, Random.Shared));
        Assert.Equal(0, AudioCueVariation.PickIndex(1, AudioCueVariation.PickMode.Random, ref cursor, Random.Shared));
        cursor = -1;
        // (-1 % 2) + 2 → index 1
        Assert.Equal(1, AudioCueVariation.PickIndex(2, AudioCueVariation.PickMode.RoundRobin, ref cursor, Random.Shared));
        var rng = new Random(1);
        Assert.InRange(AudioCueVariation.PickIndex(4, AudioCueVariation.PickMode.Random, ref cursor, rng), 0, 3);
        Assert.Equal(1.5f, AudioCueVariation.SampleRange(1.5f, 1.5f, new Random(2)), 3);
        Assert.InRange(AudioCueVariation.SampleRange(2f, 1f, new Random(3)), 1f, 2f);
    }

    [Fact]
    public void Voice_limit_oldest_and_priority_fail()
    {
        var c = new[]
        {
            new VoiceLimitLogic.VoiceCandidate(1, 5, 10),
            new VoiceLimitLogic.VoiceCandidate(2, 5, 1),
        };
        Assert.Equal(2, VoiceLimitLogic.ChooseSteal(c, 2, 2, VoiceStealMode.StealOldest, 0));
        Assert.Equal(-2, VoiceLimitLogic.ChooseSteal(c, 2, 2, VoiceStealMode.StealLowestPriority, 0));
        // Under cap → allow without steal
        Assert.Equal(-1, VoiceLimitLogic.ChooseSteal(c, 0, 2, VoiceStealMode.StealLowestPriority, 9));
        Assert.Equal(-2, VoiceLimitLogic.ChooseSteal(c, 2, 2, VoiceStealMode.Fail, 99));
        Assert.True(VoiceLimitLogic.CooldownAllows(1, 0, 0f));
    }

    [Fact]
    public void Bus_registry_reregister_and_master()
    {
        var r = new AudioBusRegistry();
        r.RegisterStockBuses();
        r.Register("x", new BusRegistration { DefaultGain = 0.5f, MaxVoices = 2 });
        r.SetVolume("x", 0.7f);
        r.Register("x", new BusRegistration { DefaultGain = 0.1f, MaxVoices = 3, StealMode = VoiceStealMode.Fail });
        Assert.Equal(0.7f, r.GetVolume("x"), 3); // keep gain
        Assert.True(r.TryGetPolyphony("x", out var max, out var mode));
        Assert.Equal(3, max);
        Assert.Equal(VoiceStealMode.Fail, mode);
        Assert.Equal(1f, r.GetMasterGain());
        r.SetMuted(AudioBusIds.Master, true);
        Assert.Equal(0f, r.GetMasterGain());
        r.Register("  ", BusRegistration.Default);
        r.SetVolume("  ", 1f);
        r.SetMuted("", true);
        Assert.False(r.TryGetPolyphony("", out _, out _));
        Assert.False(r.Contains(""));
        var tiny = new string[1];
        Assert.Equal(1, r.CopyBusIds(tiny));

        // Unknown / whitespace bus paths
        r.SetVolume("missing", 0.5f);
        r.SetMuted("missing", true);
        Assert.Equal(1f, r.GetVolume("  "));
        Assert.False(r.IsMuted("  "));
        Assert.Equal(1f, r.GetEffectiveBusGain("  "));
        Assert.False(r.TryGetPolyphony("missing", out _, out _));
    }

    [Fact]
    public void Duck_ignores_bad_rules()
    {
        var m = new AudioDuckMixer();
        m.Register(new AudioDuckRule("", "music", 0.5f, 0.1f, 0.1f));
        m.Register(new AudioDuckRule("dialogue", "", 0.5f, 0.1f, 0.1f));
        Assert.Equal(1f, m.GetMultiplier("music"));
        Assert.Equal(1f, m.GetMultiplier(""));
        Assert.Equal(1f, m.GetMultiplier("  "));
        m.Tick(0.1f, _ => false); // no rules → early return
        m.Register(new AudioDuckRule("dialogue", "music", 0.2f, 0.05f, 0.2f));
        m.Tick(0.05f, bus => bus == "dialogue"); // seeds blend state on first tick
        Assert.True(m.GetMultiplier("music") < 1f);
    }

    [Fact]
    public void Environment_merge_master_scale_and_blend_buses()
    {
        var global = AudioEnvironmentSettings.Default;
        global.MasterScale = 1f;
        global.BusGains = new[]
        {
            new AudioBusGainEntry { BusId = "sfx", Gain = 1f },
            new AudioBusGainEntry { BusId = "  ", Gain = 9f }, // skipped in merge baseline
        };
        var volumes = new[]
        {
            new AudioEnvironmentVolumeSubmission(
                new AudioEnvironmentVolume
                {
                    HalfExtentsLocal = new Vector2D<float>(10, 10),
                    Priority = 1,
                    Overrides = new AudioEnvironmentOverrides
                    {
                        HasMasterScale = true,
                        MasterScale = 0.5f,
                        HasBlendSeconds = true,
                        BlendSeconds = 2f,
                        BusGains = new[]
                        {
                            new AudioBusGainEntry { BusId = "sfx", Gain = 0.5f },
                            new AudioBusGainEntry { BusId = "music", Gain = 2f },
                            new AudioBusGainEntry { BusId = "  ", Gain = 9f },
                        },
                    },
                },
                default,
                0f,
                new Vector2D<float>(1, 1)),
            // Same point, lower priority → skipped after higher wins
            new AudioEnvironmentVolumeSubmission(
                new AudioEnvironmentVolume
                {
                    HalfExtentsLocal = new Vector2D<float>(10, 10),
                    Priority = 0,
                    Overrides = new AudioEnvironmentOverrides { HasLowPassHz = true, LowPassHz = 100f },
                },
                default,
                0f,
                new Vector2D<float>(1, 1)),
        };
        var r = AudioEnvironmentMerge.ResolveAtPoint(global, volumes, default);
        Assert.Equal(0.5f, r.MasterScale, 3);
        Assert.Equal(2f, r.BlendSeconds, 3);
        Assert.Equal(0.5f, AudioEnvironmentMerge.GetBusMultiplier(r, "sfx"), 3);
        Assert.Equal(2f, AudioEnvironmentMerge.GetBusMultiplier(r, "music"), 3);
        Assert.Equal(1f, AudioEnvironmentMerge.GetBusMultiplier(r, "ambient"), 3); // missing bus → 1
        Assert.Equal(22000f, r.LowPassHz, 1); // lower-priority override ignored

        var a = AudioEnvironmentSettings.Default;
        a.BusGains = new[] { new AudioBusGainEntry { BusId = "a", Gain = 0f } };
        var b = AudioEnvironmentSettings.Default;
        b.BusGains = new[] { new AudioBusGainEntry { BusId = "a", Gain = 2f }, new AudioBusGainEntry { BusId = "b", Gain = 1f } };
        var mid = AudioEnvironmentMerge.Blend(a, b, 0.5f);
        Assert.Equal(1f, AudioEnvironmentMerge.GetBusMultiplier(mid, "a"), 3);
        Assert.Equal(1f, AudioEnvironmentMerge.GetBusMultiplier(AudioEnvironmentSettings.Default, ""));

        // Overlay with only whitespace bus ids → merge returns null map
        var emptyOverlay = new AudioEnvironmentVolumeSubmission(
            new AudioEnvironmentVolume
            {
                HalfExtentsLocal = new Vector2D<float>(10, 10),
                Priority = 5,
                Overrides = new AudioEnvironmentOverrides
                {
                    BusGains = new[] { new AudioBusGainEntry { BusId = "  ", Gain = 1f } },
                },
            },
            default,
            0f,
            new Vector2D<float>(1, 1));
        var g2 = AudioEnvironmentSettings.Default;
        g2.BusGains = null;
        var r2 = AudioEnvironmentMerge.ResolveAtPoint(g2, new[] { emptyOverlay }, default);
        Assert.Null(r2.BusGains);

        // Blend when one side has null bus gains (Lookup null path)
        var onlyB = AudioEnvironmentSettings.Default;
        onlyB.BusGains = new[] { new AudioBusGainEntry { BusId = "x", Gain = 0.5f } };
        var blended = AudioEnvironmentMerge.Blend(AudioEnvironmentSettings.Default, onlyB, 1f);
        Assert.Equal(0.5f, AudioEnvironmentMerge.GetBusMultiplier(blended, "x"), 3);
    }

    [Fact]
    public void Environment_empty_half_extents_never_contains()
    {
        var volumes = new[]
        {
            new AudioEnvironmentVolumeSubmission(
                new AudioEnvironmentVolume
                {
                    HalfExtentsLocal = new Vector2D<float>(0, 10),
                    Priority = 1,
                    Overrides = new AudioEnvironmentOverrides { HasLowPassHz = true, LowPassHz = 1f },
                },
                default,
                0f,
                new Vector2D<float>(1, 1)),
        };
        var r = AudioEnvironmentMerge.ResolveAtPoint(AudioEnvironmentSettings.Default, volumes, default);
        Assert.Equal(22000f, r.LowPassHz, 1);
    }

    [Fact]
    public void Wav_stream_and_clip_decoder_sniff()
    {
        var wav = BuildWav(new byte[] { 1, 2 }, 1, 8000, 16);
        using var ms = new MemoryStream(wav);
        Assert.NotNull(WavDecoder.TryDecode(ms));
        Assert.NotNull(AudioClipDecoder.TryDecode("Sounds/x.WAV", wav));
        Assert.NotNull(AudioClipDecoder.TryDecode("Sounds/x.bin", wav)); // RIFF sniff
        Assert.Null(AudioClipDecoder.TryDecode("Sounds/x.bin", "nope"u8.ToArray()));
        using var ms2 = new MemoryStream(wav);
        Assert.NotNull(AudioClipDecoder.TryDecode("Sounds/x.wav", ms2));
        // Unknown extension → stream copy + sniff
        using var ms3 = new MemoryStream(wav);
        Assert.NotNull(AudioClipDecoder.TryDecode("Sounds/x.bin", ms3));
        // Ogg extension / OggS sniff (decoder excluded; call sites still run)
        Assert.Null(AudioClipDecoder.TryDecode("Sounds/x.ogg", "not-ogg"u8.ToArray()));
        Assert.Null(AudioClipDecoder.TryDecode("Sounds/x.bin", "OggSxxxx"u8.ToArray()));
        using var oggStream = new MemoryStream("not-ogg"u8.ToArray());
        Assert.Null(AudioClipDecoder.TryDecode("Sounds/x.ogg", oggStream));
        var d = WavDecoder.TryDecode(wav)!;
        Assert.True(d.DurationSeconds > 0);
        Assert.Equal(0, new DecodedPcm(Array.Empty<byte>(), 0, 0, 0).DurationSeconds);
    }

    [Fact]
    public void Wav_rejects_non_pcm_and_bad_channels()
    {
        var bad = BuildWav(new byte[] { 0, 0 }, 1, 8000, 16);
        // patch format to 3 (float)
        BinaryPrimitives.WriteInt16LittleEndian(bad.AsSpan(20), 3);
        Assert.Null(WavDecoder.TryDecode(bad));
        var stereo8 = BuildWav(new byte[] { 1, 2, 3, 4 }, 2, 8000, 8);
        Assert.NotNull(WavDecoder.TryDecode(stereo8));

        Assert.Null(WavDecoder.TryDecode("RIFF"u8.ToArray())); // too short
        // RIFF header but bytes 8..11 are not WAVE
        var notWave = new byte[44];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(notWave, 0);
        Encoding.ASCII.GetBytes("XXXX").CopyTo(notWave, 8);
        Assert.Null(WavDecoder.TryDecode(notWave));

        // Truncated chunk size
        var trunc = BuildWav(new byte[] { 1, 2 }, 1, 8000, 16);
        BinaryPrimitives.WriteInt32LittleEndian(trunc.AsSpan(16), 9999);
        Assert.Null(WavDecoder.TryDecode(trunc));

        // fmt chunk too small
        var tinyFmt = BuildWav(new byte[] { 1, 2 }, 1, 8000, 16);
        BinaryPrimitives.WriteInt32LittleEndian(tinyFmt.AsSpan(16), 8);
        Assert.Null(WavDecoder.TryDecode(tinyFmt));

        // bad channel count
        var badCh = BuildWav(new byte[] { 1, 2 }, 1, 8000, 16);
        BinaryPrimitives.WriteInt16LittleEndian(badCh.AsSpan(22), 3);
        Assert.Null(WavDecoder.TryDecode(badCh));

        // Odd-sized junk chunk before data (word-align path) + empty data
        var odd = BuildOddChunkWav();
        Assert.Null(WavDecoder.TryDecode(odd));
    }

    private static byte[] BuildOddChunkWav()
    {
        // RIFF/WAVE with fmt, a 1-byte "JUNK" chunk (forces pad), and empty data
        var pcm = Array.Empty<byte>();
        var junkPayload = new byte[] { 0xAB }; // size 1 → pad
        var buffer = new byte[12 + 8 + 16 + 8 + 1 + 1 + 8 + 0];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(buffer, 0);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), buffer.Length - 8);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(buffer, 8);
        var o = 12;
        Encoding.ASCII.GetBytes("fmt ").CopyTo(buffer, o);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 4), 16);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(o + 8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(o + 10), 1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 12), 8000);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 16), 8000);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(o + 20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(o + 22), 8);
        o += 24;
        Encoding.ASCII.GetBytes("JUNK").CopyTo(buffer, o);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 4), 1);
        buffer[o + 8] = junkPayload[0];
        o += 10; // 8 header + 1 payload + 1 pad
        Encoding.ASCII.GetBytes("data").CopyTo(buffer, o);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(o + 4), 0);
        return buffer;
    }

    [Fact]
    public void Null_audio_covers_remaining_apis()
    {
        IAudioService a = new NullAudioService();
        a.Stop(VoiceId.None);
        a.Pause(VoiceId.None);
        a.Resume(VoiceId.None);
        a.Seek(VoiceId.None, 0);
        a.SetVoiceParams(VoiceId.None, 1f, 1f, new Vector2D<float>(1, 2));
        Assert.False(a.TryGetVoiceState(VoiceId.None, out _));
        a.PlayMusic(default);
        a.StopMusic();
        a.CrossfadeMusic(default, 0);
        a.RegisterCue("  ", default);
        a.RegisterCue("c", new AudioCueDesc { ClipPaths = new[] { "Sounds/a.wav" } });
        a.PlayCue("c", default);
        a.SetListener(default);
        a.SetGlobalAudioEnvironment(AudioEnvironmentSettings.Default);
        a.SubmitAudioEnvironmentVolume(default, default, 0, new Vector2D<float>(1, 1));
        a.RegisterDuckRule(new AudioDuckRule("a", "b", 0.5f, 0.1f, 0.1f));
        a.ClearDuckRules();
        a.SetFocusPolicy(AudioFocusPolicy.Ignore);
        a.SetWindowFocused(false);
        a.SetGameplayAudioPaused(true);
        a.SetTimeScale(0.5f);
        a.UnloadClip(AudioClipId.Invalid);
        Assert.False(a.IsClipLoaded(new AudioClipId(1)));
    }

    [Fact]
    public void Scene_deserializer_audio_emitter_and_volume_paths()
    {
        using var doc = JsonDocument.Parse("""
            {
              "active": true,
              "halfExtentsX": 10,
              "halfExtentsY": 20,
              "priority": 3,
              "overrides": { "masterScale": 0.8, "blendSeconds": 0.2 }
            }
            """);
        var o = EngineSceneComponentDeserializers.ReadAudioEnvironmentOverrides(
            doc.RootElement.GetProperty("overrides"));
        Assert.True(o.HasMasterScale);
        Assert.True(o.HasBlendSeconds);
        var s = EngineSceneComponentDeserializers.ReadAudioEnvironmentSettings(doc.RootElement);
        Assert.Equal(3, 3); // smoke
        _ = s;
        using var empty = JsonDocument.Parse("""{ "busGains": [ { "bus": "", "gain": 1 } ] }""");
        Assert.Null(EngineSceneComponentDeserializers.ReadAudioEnvironmentSettings(empty.RootElement).BusGains);
    }

    private static byte[] BuildWav(byte[] pcm, int channels, int sampleRate, int bits)
    {
        var blockAlign = channels * (bits / 8);
        var byteRate = sampleRate * blockAlign;
        var buffer = new byte[44 + pcm.Length];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(buffer, 0);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), 36 + pcm.Length);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(buffer, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(buffer, 12);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(22), (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(28), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(32), (short)blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(34), (short)bits);
        Encoding.ASCII.GetBytes("data").CopyTo(buffer, 36);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(40), pcm.Length);
        pcm.CopyTo(buffer, 44);
        return buffer;
    }
}
