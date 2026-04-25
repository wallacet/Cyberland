using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

/// <summary>
/// Helper methods to create entities with common components.
/// </summary>
public static class Entities
{
    /// <summary>
    /// Creates a camera entity with a <see cref="Transform"/> and <see cref="Camera2D"/> component.
    /// <paramref name="viewportSizeWorld"/> fixes the virtual canvas size in world pixels so the visible
    /// world is independent of the physical window.
    /// </summary>
    /// <param name="world">Target ECS world.</param>
    /// <param name="viewportSizeWorld">Virtual viewport size in world pixels.</param>
    /// <param name="positionWorld">
    /// Optional initial camera world position (defaults to the canvas center, which reproduces the legacy
    /// 1:1 swapchain-pixel mapping when <paramref name="viewportSizeWorld"/> matches the window).
    /// </param>
    public static EntityId CreateCamera(World world, Vector2D<int> viewportSizeWorld, Vector2D<float>? positionWorld = null)
    {
        var entity = world.CreateEntity();
        var transform = Transform.Identity;
        transform.WorldPosition = positionWorld ?? new Vector2D<float>(viewportSizeWorld.X * 0.5f, viewportSizeWorld.Y * 0.5f);
        world.GetOrAdd<Transform>(entity) = transform;
        world.GetOrAdd<Camera2D>(entity) = Camera2D.Create(viewportSizeWorld);
        return entity;
    }


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
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        return entity;
    }

    /// <summary>
    /// Creates a sprite entity with a transform and sprite component.
    /// </summary>
    public static EntityId CreateSprite(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        world.GetOrAdd<Sprite>(entity);
        return entity;
    }

    /// <summary>
    /// Creates a text entity with a transform and bitmap text component.
    /// </summary>
    public static EntityId CreateText(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        world.GetOrAdd<BitmapText>(entity);
        return entity;
    }

    /// <summary>
    /// Creates a point light entity with a transform and point-light source component.
    /// </summary>
    public static EntityId CreatePointLight(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        world.GetOrAdd<PointLightSource>(entity);
        return entity;
    }

    /// <summary>
    /// Creates a spot light entity with a transform and spot-light source component.
    /// </summary>
    public static EntityId CreateSpotLight(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        world.GetOrAdd<SpotLightSource>(entity);
        return entity;
    }

    /// <summary>
    /// Creates a directional light entity with a transform and directional-light source component.
    /// </summary>
    public static EntityId CreateDirectionalLight(World world)
    {
        var entity = world.CreateEntity();
        world.GetOrAdd<Transform>(entity) = Transform.Identity;
        world.GetOrAdd<DirectionalLightSource>(entity);
        return entity;
    }


}