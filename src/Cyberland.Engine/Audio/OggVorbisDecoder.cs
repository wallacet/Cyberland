using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using NVorbis;

namespace Cyberland.Engine.Audio;

/// <summary>Decodes Ogg Vorbis to 16-bit PCM via NVorbis (full decode for short SFX).</summary>
[ExcludeFromCodeCoverage(Justification = "NVorbis integration requires valid Ogg bitstreams; WAV path is unit-tested.")]
public static class OggVorbisDecoder
{
    /// <summary>Tries to fully decode an Ogg Vorbis buffer to 16-bit interleaved PCM.</summary>
    public static DecodedPcm? TryDecode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var vorbis = new VorbisReader(stream, false);
            var channels = vorbis.Channels;
            var sampleRate = vorbis.SampleRate;
            if (channels is not (1 or 2) || sampleRate <= 0)
                return null;

            var totalSamples = vorbis.TotalSamples;
            if (totalSamples <= 0 || totalSamples > int.MaxValue / (channels * 2))
            {
                // Unknown length: read in chunks.
                return DecodeUnknownLength(vorbis, channels, sampleRate);
            }

            var floatBuf = new float[totalSamples * channels];
            var read = vorbis.ReadSamples(floatBuf, 0, floatBuf.Length);
            if (read <= 0)
                return null;

            var pcm = new byte[read * sizeof(short)];
            FloatToPcm16(floatBuf.AsSpan(0, read), pcm);
            return new DecodedPcm(pcm, sampleRate, channels, 16);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Tries to decode from a byte buffer.</summary>
    public static DecodedPcm? TryDecode(ReadOnlySpan<byte> data)
    {
        using var ms = new MemoryStream(data.ToArray(), writable: false);
        return TryDecode(ms);
    }

    private static DecodedPcm? DecodeUnknownLength(VorbisReader vorbis, int channels, int sampleRate)
    {
        using var ms = new MemoryStream();
        var floatBuf = new float[4096 * channels];
        int read;
        while ((read = vorbis.ReadSamples(floatBuf, 0, floatBuf.Length)) > 0)
        {
            var pcmChunk = new byte[read * sizeof(short)];
            FloatToPcm16(floatBuf.AsSpan(0, read), pcmChunk);
            ms.Write(pcmChunk);
        }

        if (ms.Length == 0)
            return null;
        return new DecodedPcm(ms.ToArray(), sampleRate, channels, 16);
    }

    private static void FloatToPcm16(ReadOnlySpan<float> samples, Span<byte> pcm)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            var s = Math.Clamp(samples[i], -1f, 1f);
            var v = (short)Math.Round(s * short.MaxValue);
            pcm[i * 2] = (byte)(v & 0xFF);
            pcm[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
    }
}
