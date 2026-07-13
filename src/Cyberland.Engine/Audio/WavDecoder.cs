using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Cyberland.Engine.Audio;

/// <summary>Decoded PCM clip suitable for OpenAL buffer upload.</summary>
public sealed class DecodedPcm
{
    /// <summary>Creates a PCM buffer.</summary>
    public DecodedPcm(byte[] interleavedPcm, int sampleRate, int channels, int bitsPerSample)
    {
        InterleavedPcm = interleavedPcm ?? throw new ArgumentNullException(nameof(interleavedPcm));
        SampleRate = sampleRate;
        Channels = channels;
        BitsPerSample = bitsPerSample;
    }

    /// <summary>Interleaved little-endian PCM samples.</summary>
    public byte[] InterleavedPcm { get; }

    /// <summary>Sample rate in Hz.</summary>
    public int SampleRate { get; }

    /// <summary>1 = mono, 2 = stereo.</summary>
    public int Channels { get; }

    /// <summary>8 or 16.</summary>
    public int BitsPerSample { get; }

    /// <summary>Duration in seconds.</summary>
    public double DurationSeconds =>
        SampleRate <= 0 || Channels <= 0 || BitsPerSample <= 0
            ? 0
            : InterleavedPcm.Length / (double)(SampleRate * Channels * (BitsPerSample / 8));
}

/// <summary>Decodes PCM WAV (8/16-bit, mono/stereo) from a byte buffer.</summary>
public static class WavDecoder
{
    /// <summary>Tries to decode a RIFF WAVE buffer. Returns null on failure.</summary>
    public static DecodedPcm? TryDecode(ReadOnlySpan<byte> data)
    {
        if (data.Length < 44)
            return null;
        if (!data.Slice(0, 4).SequenceEqual("RIFF"u8) || !data.Slice(8, 4).SequenceEqual("WAVE"u8))
            return null;

        var offset = 12;
        var channels = 0;
        var sampleRate = 0;
        var bitsPerSample = 0;
        ReadOnlySpan<byte> pcm = default;
        var foundFmt = false;
        var foundData = false;

        while (offset + 8 <= data.Length)
        {
            var chunkId = Encoding.ASCII.GetString(data.Slice(offset, 4));
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 4, 4));
            offset += 8;
            if (chunkSize < 0 || offset + chunkSize > data.Length)
                return null;

            if (chunkId == "fmt ")
            {
                if (chunkSize < 16)
                    return null;
                var format = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset, 2));
                if (format != 1) // PCM only
                    return null;
                channels = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset + 2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 4, 4));
                bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset + 14, 2));
                if (channels is not (1 or 2) || bitsPerSample is not (8 or 16) || sampleRate <= 0)
                    return null;
                foundFmt = true;
            }
            else if (chunkId == "data")
            {
                pcm = data.Slice(offset, chunkSize);
                foundData = true;
            }

            offset += chunkSize;
            if ((chunkSize & 1) != 0)
                offset++; // word align
        }

        if (!foundFmt || !foundData || pcm.Length == 0)
            return null;

        return new DecodedPcm(pcm.ToArray(), sampleRate, channels, bitsPerSample);
    }

    /// <summary>Decodes from a stream (reads all bytes).</summary>
    public static DecodedPcm? TryDecode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return TryDecode(ms.ToArray());
    }
}
