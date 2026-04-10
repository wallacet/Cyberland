using System.Diagnostics.CodeAnalysis;
using Silk.NET.OpenAL;

namespace Cyberland.Engine.Audio;

/// <summary>
/// Minimal OpenAL Soft bootstrap: opens the default device, creates a context, and exposes <see cref="Al"/> for future playback code.
/// </summary>
/// <remarks>Constructors may throw if no audio backend is available; the host can catch and continue silent.</remarks>
[ExcludeFromCodeCoverage(Justification = "Requires an OpenAL device at runtime.")]
public sealed class OpenALAudioDevice : IDisposable
{
    private readonly ALContext _alc;
    private readonly AL _al;
    private unsafe readonly Device* _device;
    private unsafe readonly Context* _context;

    /// <summary>Opens the default output device and makes a context current on this thread.</summary>
    public unsafe OpenALAudioDevice()
    {
        _alc = ALContext.GetApi(true);
        _device = _alc.OpenDevice("");
        if (_device == null)
            throw new InvalidOperationException("OpenAL: OpenDevice failed.");

        _context = _alc.CreateContext(_device, null);
        if (_context == null)
            throw new InvalidOperationException("OpenAL: CreateContext failed.");

        _alc.MakeContextCurrent(_context);
        _al = AL.GetApi(true);
    }

    /// <summary>Silk.NET AL function table (buffers, sources, …) once the context is current.</summary>
    public AL Al => _al;

    /// <summary>Destroys context and device.</summary>
    public unsafe void Dispose()
    {
        _alc.MakeContextCurrent(null);
        if (_context != null)
            _alc.DestroyContext(_context);

        if (_device != null)
            _alc.CloseDevice(_device);

        _al.Dispose();
        _alc.Dispose();
    }
}
