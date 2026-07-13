using Cyberland.Demo.Audio.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.Audio.Systems;

/// <summary>Copies <see cref="DemoStatus"/> into the HUD <see cref="BitmapText"/> row.</summary>
public sealed class HudSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc />
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HudRootTag, BitmapText>();

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity singleton) => _ = singleton;

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity singleton, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref var text = ref singleton.Get<BitmapText>();
        text.Content = DemoStatus.Text + "\n" + DemoStatus.Stats
            + "\nKeys: U ui | F foot | D dialogue | S spam | M music | C cinematic | L listener | P pause | arrows move";
    }
}
