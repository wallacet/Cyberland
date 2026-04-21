using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Rendering;

/// <summary>
/// Helper methods to create entities with common components.
/// </summary>
public static class Entities
{

    /// <summary>
    /// Creates an empty entity with no components.
    /// </summary>
    /// <remarks>
    /// Deliberately empty, used for logical groups and other non-visual entities.
    /// Provided for convenience and consistency, but you can create empty entities directly with world.CreateEntity().
    /// </remarks>
    public static EntityId CreateLogical(World world)
    {
        var entity = world.CreateEntity();
        return entity;
    }

    /// <summary>
    /// Creates an empty entity with a transform component.
    /// </summary>
    public static EntityId CreateEmpty(World world)
    {
        var entity = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(entity);
        return entity;
    }

    /// <summary>
    /// Creates a sprite entity with a transform and sprite component.
    /// </summary>
    public static EntityId CreateSprite(World world)
    {
        var entity = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(entity);
        world.Components<Sprite>().GetOrAdd(entity);
        return entity;
    }

    /// <summary>
    /// Creates a text entity with a transform and bitmap text component.
    /// </summary>
    public static EntityId CreateText(World world)
    {
        var entity = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(entity);
        world.Components<BitmapText>().GetOrAdd(entity);
        return entity;
    }

    /// <summary>
    /// Creates a point light entity with a transform and point light component.
    /// </summary>
    public static EntityId CreatePointLight(World world)
    {
        var entity = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(entity);
        world.Components<PointLight>().GetOrAdd(entity);
        return entity;
    }

    /// <summary>
    /// Creates a spot light entity with a transform and spot light component.
    /// </summary>
    public static EntityId CreateSpotLight(World world)
    {
        var entity = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(entity);
        world.Components<SpotLight>().GetOrAdd(entity);
        return entity;
    }

    /// <summary>
    /// Creates a directional light entity with a transform and directional light component.
    /// </summary>
    public static EntityId CreateDirectionalLight(World world)
    {
        var entity = world.CreateEntity();
        world.Components<Transform>().GetOrAdd(entity);
        world.Components<DirectionalLight>().GetOrAdd(entity);
        return entity;
    }


}