namespace Cyberland.Engine.Rendering;

/// <summary>
/// Opaque shader-module lifetime token returned by <see cref="IRenderer"/> custom shader module creation APIs.
/// </summary>
/// <remarks>
/// Keep this handle on the render/window thread and dispose it when no longer needed so native shader module memory is released.
/// </remarks>
public interface IShaderModuleHandle : IDisposable
{
}
