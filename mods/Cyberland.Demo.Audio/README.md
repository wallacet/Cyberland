# Cyberland.Demo.Audio

## Purpose

Teaching mod for the engine audio stack: open-ended mix buses, sound cues, ducking, spatial/direct/cinematic playback, music crossfade, ambient environment volumes, listener override, gameplay pause vs UI, window focus ducking, and localized `Sounds/` overlays.

## Run

```powershell
.\scripts\Run-CyberlandDemo-Test.ps1 -Demo audio
```

Manifests stay `"disabled": true` in git; the script toggles them for the run.

Optional: set language to hear the German UI/VO overlay:

```powershell
# language.json beside the host, or:
dotnet run --project src/Cyberland.Host -- --lang de
```

(with the audio demo enabled).

## Learning path

1. [`Mod.cs`](Mod.cs) — mount content, register buses/cues in `AudioDemoInputSystem.OnSingletonStart`, spawn scene, register systems.
2. [`Content/Scenes/audio.json`](Content/Scenes/audio.json) — camera, player, ambient emitter, music, global + street/club environment volumes, HUD text.
3. [`Systems/AudioDemoInputSystem.cs`](Systems/AudioDemoInputSystem.cs) — imperative `IAudioService` usage.
4. Engine late systems: `cyberland.engine/audio-listener`, `audio-session`, `global-audio-environment`, `audio-environment-volumes`, `audio-emitters`, `music`.

## Features taught

- `IAudioService` / `GameHostServices.Audio` (never null; `NullAudioService` when OpenAL missing)
- `RegisterBus` / `SetBusVolume` / custom buses (`dialogue`, `footsteps`)
- `RegisterCue` / `PlayCue` with pitch/gain jitter and cooldown
- Voice limits / steal (spam key + `GetStats`)
- Duck rules (dialogue → music)
- `AudioSpace.World` / `Direct` / `Cinematic`
- `MusicSource` + `CrossfadeMusic`
- Audio environment volumes (sparse `busGains`, low-pass blend)
- `AudioListenerOverride` vs camera listener
- Gameplay pause vs UI bus
- Localized clips under `Content/Locale/{culture}/Sounds/`

## Content

- `Content/Sounds/**` — tiny generated WAV tones (git-friendly)
- `Content/Locale/de/Sounds/**` — overlay click + VO
- `Content/Scenes/audio.json`
- `Content/Locale/en/audio.json` — string table stub

## Further reading

- `.cursor/rules/cyberland-demo-mod-authoring.mdc`
- `.cursor/rules/cyberland-mod-host-architecture.mdc`
- Root `README.md` Audio section
