using System;
using System.IO;

namespace Cyberland.Engine.Audio;

/// <summary>Dispatches WAV / Ogg decode from bytes or a stream based on path extension.</summary>
public static class AudioClipDecoder
{
    /// <summary>Tries to decode using the file extension of <paramref name="canonicalPath"/>.</summary>
    public static DecodedPcm? TryDecode(string canonicalPath, ReadOnlySpan<byte> data)
    {
        var ext = Path.GetExtension(canonicalPath);
        if (ext.Equals(".wav", StringComparison.OrdinalIgnoreCase))
            return WavDecoder.TryDecode(data);
        if (ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
            return OggVorbisDecoder.TryDecode(data);
        // Sniff: RIFF/WAVE or OggS
        if (data.Length >= 4 && data.Slice(0, 4).SequenceEqual("RIFF"u8))
            return WavDecoder.TryDecode(data);
        if (data.Length >= 4 && data.Slice(0, 4).SequenceEqual("OggS"u8))
            return OggVorbisDecoder.TryDecode(data);
        return null;
    }

    /// <summary>Tries to decode from a stream (extension preferred).</summary>
    public static DecodedPcm? TryDecode(string canonicalPath, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var ext = Path.GetExtension(canonicalPath);
        if (ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
            return OggVorbisDecoder.TryDecode(stream);
        if (ext.Equals(".wav", StringComparison.OrdinalIgnoreCase))
            return WavDecoder.TryDecode(stream);

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return TryDecode(canonicalPath, ms.ToArray());
    }
}
