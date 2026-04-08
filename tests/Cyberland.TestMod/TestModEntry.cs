using Cyberland.Engine.Modding;

namespace Cyberland.TestMod;

/// <summary>Minimal <see cref="IMod"/> used by <see cref="Cyberland.Engine.Tests"/> to exercise <see cref="ModLoader"/>.</summary>
public sealed class TestModEntry : IMod
{
    public static int OnLoadCount;
    public static int OnUnloadCount;
    public static ModLoadContext? LastContext;

    public ModManifest Manifest { get; } = new()
    {
        Id = "test.mod",
        Name = "Test mod",
        Version = "1.0.0",
        EntryAssembly = "Cyberland.TestMod.dll",
        ContentRoot = "Content",
        LoadOrder = 5
    };

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
