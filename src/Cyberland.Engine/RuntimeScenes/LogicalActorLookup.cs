using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

namespace Cyberland.Engine.RuntimeScenes;

/// <summary>Resolves <see cref="LogicalActorId"/> GUID strings to live <see cref="EntityId"/> values after scene spawn.</summary>
public static class LogicalActorLookup
{
    /// <summary>Finds the first entity whose <see cref="LogicalActorId.Guid"/> matches <paramref name="guid"/>.</summary>
    public static bool TryResolve(World world, string guid, out EntityId entityId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(guid);

        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<LogicalActorId>()))
        {
            var ids = chunk.Entities;
            var logicals = chunk.Column<LogicalActorId>();
            for (var i = 0; i < ids.Length; i++)
            {
                if (string.Equals(logicals[i].Guid, guid, StringComparison.Ordinal))
                {
                    entityId = ids[i];
                    return true;
                }
            }
        }

        entityId = default;
        return false;
    }

    /// <summary>Resolves <paramref name="guid"/> or throws when no matching logical actor exists.</summary>
    public static EntityId Resolve(World world, string guid)
    {
        if (TryResolve(world, guid, out var id))
            return id;
        throw new InvalidOperationException($"Logical actor '{guid}' was not found in the world.");
    }
}
