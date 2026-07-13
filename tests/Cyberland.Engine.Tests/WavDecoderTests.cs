using System.Buffers.Binary;
using System.Text;
using Cyberland.Engine.Audio;

namespace Cyberland.Engine.Tests;

public sealed class WavDecoderTests
{
    [Fact]
    public void TryDecode_mono16_round_trips()
    {
        var pcm = new byte[] { 0x00, 0x10, 0xFF, 0x7F };
        var wav = BuildWav(pcm, channels: 1, sampleRate: 22050, bits: 16);
        var decoded = WavDecoder.TryDecode(wav);
        Assert.NotNull(decoded);
        Assert.Equal(22050, decoded!.SampleRate);
        Assert.Equal(1, decoded.Channels);
        Assert.Equal(16, decoded.BitsPerSample);
        Assert.Equal(pcm, decoded.InterleavedPcm);
    }

    [Fact]
    public void TryDecode_rejects_bad_header()
    {
        Assert.Null(WavDecoder.TryDecode("not a wav"u8.ToArray()));
        Assert.Null(WavDecoder.TryDecode(Array.Empty<byte>()));
    }

    [Fact]
    public void AudioClipDecoder_dispatches_wav_by_extension()
    {
        var pcm = new byte[] { 0x00, 0x00 };
        var wav = BuildWav(pcm, 1, 8000, 16);
        var d = AudioClipDecoder.TryDecode("Sounds/beep.wav", wav);
        Assert.NotNull(d);
    }

    private static byte[] BuildWav(byte[] pcm, int channels, int sampleRate, int bits)
    {
        var blockAlign = channels * (bits / 8);
        var byteRate = sampleRate * blockAlign;
        var dataSize = pcm.Length;
        var buffer = new byte[44 + dataSize];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(buffer, 0);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), 36 + dataSize);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(buffer, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(buffer, 12);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(22), (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(28), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(32), (short)blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(34), (short)bits);
        Encoding.ASCII.GetBytes("data").CopyTo(buffer, 36);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(40), dataSize);
        pcm.CopyTo(buffer, 44);
        return buffer;
    }
}
