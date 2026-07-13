using Cyberland.Demo.Audio.Systems;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.Audio;

/// <summary>
/// Tutorial mod for <see cref="Cyberland.Engine.Audio.IAudioService"/>: buses, cues, ducking, environments, localization.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> private <see cref="SetupSceneAsync"/>, <c>Content/Scenes/audio.json</c>, then <see cref="AudioDemoInputSystem"/>.</para>
/// <para><b>Frame flow:</b> early input → late engine audio systems (listener/env/emitters/music) → HUD text.</para>
/// </remarks>
public sealed class Mod : IMod
{
    /// <summary>Root scene path.</summary>
    public const string ScenePath = "Scenes/audio.json";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        InputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("audio.json");

        await SetupSceneAsync(context);

        var host = context.Host;
        context.RegisterSingleton("cyberland.demo.audio/input", new AudioDemoInputSystem(host));
        context.RegisterSingleton("cyberland.demo.audio/hud", new HudSystem());
    }

    /// <inheritdoc />
    public void OnUnload() { }

    private static async ValueTask SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        var scenes = context.Scenes ?? throw new InvalidOperationException("ISceneRuntime required.");
        SceneComponentDeserializers.Register(scenes);
        var result = await scenes.SpawnIntoWorldAsync(
            context.World,
            ScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "Audio demo scene spawn failed.");
    }
}
