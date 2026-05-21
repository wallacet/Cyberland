using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Snake;

/// <summary>Marker on the point light that follows the snake head (separates it from the food point light in queries).</summary>
public struct HeadFollowPointLightTag : IComponent;

/// <summary>Marker on the point light that follows the food cell.</summary>
public struct FoodFollowPointLightTag : IComponent;
