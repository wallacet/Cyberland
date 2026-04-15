namespace Cyberland.Engine.Tests;

/// <summary>
/// Guardrail: runtime ECS systems shipped in the engine iterate scheduler-provided <see cref="Cyberland.Engine.Core.Ecs.ChunkQueryAll"/>
/// instead of issuing ad-hoc <c>world.QueryChunks(...)</c> from <c>Scene/Systems</c>.
/// </summary>
public sealed class SceneSystemsQueryPolicyTests
{
    [Fact]
    public void Engine_scene_systems_do_not_call_world_QueryChunks()
    {
        var root = FindRepoRoot();
        var systemsDir = Path.Combine(root, "src", "Cyberland.Engine", "Scene", "Systems");
        Assert.True(Directory.Exists(systemsDir), $"Missing {systemsDir}");

        foreach (var path in Directory.EnumerateFiles(systemsDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("QueryChunks(", text, StringComparison.Ordinal);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var sln = Path.Combine(dir.FullName, "Cyberland.sln");
            if (File.Exists(sln))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate Cyberland.sln from test base directory.");
    }
}
