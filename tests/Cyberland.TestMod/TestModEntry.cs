using Cyberland.Engine.Modding;

namespace Cyberland.TestMod;

/// <summary>Minimal <see cref="IMod"/> used by <see cref="Cyberland.Engine.Tests"/> to exercise <see cref="ModLoader"/>.</summary>
public sealed class TestModEntry : IMod
{
    public static int OnLoadCount;
    public static int OnUnloadCount;
    public static ModLoadContext? LastContext;

    public void OnLoad(ModLoadContext context)
    {
        OnLoadCount++;
        LastContext = context;
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
