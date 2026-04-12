using System.Reflection;
using Cyberland.Engine.Modding;

namespace Cyberland.Engine.Tests;

public sealed class ModLoaderResolveSatelliteTests
{
    [Fact]
    public void DefaultLoadContextResolving_returns_null_when_mod_directory_unset()
    {
        ModLoader.SatelliteResolutionModDirectory = null;
        Assert.Null(ModLoader.DefaultLoadContextResolving(null, new AssemblyName("Cyberland.ModPluginHelper")));
    }

    [Fact]
    public void DefaultLoadContextResolving_delegates_to_ResolveSatelliteAssembly_when_directory_set()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyb defres " + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var helperSrc = typeof(Cyberland.ModPluginHelper.PluginHelper).Assembly.Location;
        File.Copy(helperSrc, Path.Combine(dir, "Cyberland.ModPluginHelper.dll"), overwrite: true);
        try
        {
            ModLoader.SatelliteResolutionModDirectory = dir;
            var asm = ModLoader.DefaultLoadContextResolving(null, new AssemblyName("Cyberland.ModPluginHelper"));
            Assert.NotNull(asm);
            Assert.Equal("Cyberland.ModPluginHelper", asm.GetName().Name);
        }
        finally
        {
            ModLoader.SatelliteResolutionModDirectory = null;
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ResolveSatelliteAssembly_returns_null_for_empty_assembly_name()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyb rsat empty " + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var an = new AssemblyName { Name = "" };
            Assert.Null(ModLoader.ResolveSatelliteAssembly(dir, an));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ResolveSatelliteAssembly_returns_null_for_Cyberland_Engine()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyb rsat eng " + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(ModLoader.ResolveSatelliteAssembly(dir, new AssemblyName("Cyberland.Engine")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ResolveSatelliteAssembly_loads_from_mod_root_first()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyb rsat root " + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var helperSrc = typeof(Cyberland.ModPluginHelper.PluginHelper).Assembly.Location;
        File.Copy(helperSrc, Path.Combine(dir, "Cyberland.ModPluginHelper.dll"), overwrite: true);
        try
        {
            var asm = ModLoader.ResolveSatelliteAssembly(dir, new AssemblyName("Cyberland.ModPluginHelper"));
            Assert.NotNull(asm);
            Assert.Equal("Cyberland.ModPluginHelper", asm.GetName().Name);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ResolveSatelliteAssembly_falls_back_to_lib_subfolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyb rsat lib " + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "lib"));
        var helperSrc = typeof(Cyberland.ModPluginHelper.PluginHelper).Assembly.Location;
        File.Copy(helperSrc, Path.Combine(dir, "lib", "Cyberland.ModPluginHelper.dll"), overwrite: true);
        try
        {
            var asm = ModLoader.ResolveSatelliteAssembly(dir, new AssemblyName("Cyberland.ModPluginHelper"));
            Assert.NotNull(asm);
            Assert.Equal("Cyberland.ModPluginHelper", asm.GetName().Name);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ResolveSatelliteAssembly_returns_null_when_dll_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyb rsat miss " + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(ModLoader.ResolveSatelliteAssembly(dir, new AssemblyName("NoSuch.Assembly")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
