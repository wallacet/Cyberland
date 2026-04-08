using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Minimal GL bootstrap: sets viewport from the window and clears the framebuffer. Sprites plug in here later.
/// </summary>
public sealed class OpenGLRenderer : IDisposable
{
    private readonly IWindow _window;
    private GL? _gl;

    public OpenGLRenderer(IWindow window) => _window = window;

    public GL? Gl => _gl;

    public void Initialize() => _gl = GL.GetApi(_window);

    public void BeginFrame()
    {
        if (_gl is null)
            return;

        var s = _window.FramebufferSize;
        _gl.Viewport(0, 0, (uint)s.X, (uint)s.Y);
        _gl.ClearColor(0.04f, 0.02f, 0.08f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    public void EndFrame()
    {
        // Presentation is handled by the window swap chain.
    }

    public void Dispose()
    {
    }
}
