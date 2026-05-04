using Cyberland.Engine.Modding;
using Cyberland.ModPluginHelper;

namespace Cyberland.TestMod;

/// <summary>Minimal <see cref="IMod"/> used by <see cref="Cyberland.Engine.Tests"/> to exercise <see cref="ModLoader"/>.</summary>
public sealed class TestModEntry : IMod
{
    public static int OnLoadCount;
    public static int OnUnloadCount;
    public static ModLoadContext? LastContext;

    public ValueTask OnLoadAsync(ModLoadContext context)
    {
        OnLoadCount++;
        LastContext = context;
        _ = PluginHelper.Token;
        return ValueTask.CompletedTask;
    }

    public void OnUnload() =>
        OnUnloadCount++;

    public static void ResetCounters()
    {
        OnLoadCount = 0;
        OnUnloadCount = 0;
        LastContext = null;
    }
}
