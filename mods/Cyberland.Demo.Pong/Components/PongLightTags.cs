using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Pong;

/// <summary>Marker on the accent point light that tracks the ball position during play.</summary>
public struct BallAccentPointLightTag : IComponent;

/// <summary>Marker on the accent point light that tracks the left paddle.</summary>
public struct LeftPaddleAccentPointLightTag : IComponent;
