using System.Diagnostics.CodeAnalysis;
using Silk.NET.OpenAL;

namespace Cyberland.Engine.Audio;

/// <summary>
/// OpenAL Soft via Silk: device + context + AL entry points. Playback APIs extend this later.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Requires an OpenAL device at runtime.")]
public sealed class OpenALAudioDevice : IDisposable
{
    private readonly ALContext _alc;
    private readonly AL _al;
    private unsafe readonly Device* _device;
    private unsafe readonly Context* _context;

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

    public AL Al => _al;

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
