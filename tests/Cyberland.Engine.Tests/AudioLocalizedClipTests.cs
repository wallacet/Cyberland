using Cyberland.Engine.Assets;
using Cyberland.Engine.Audio;
using Cyberland.Engine.Localization;

namespace Cyberland.Engine.Tests;

public sealed class AudioLocalizedClipTests
{
    [Fact]
    public async Task Localized_overlay_wins_for_sound_bytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-audio-loc-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Sounds", "ui"));
        Directory.CreateDirectory(Path.Combine(root, "Locale", "de", "Sounds", "ui"));
        await File.WriteAllBytesAsync(Path.Combine(root, "Sounds", "ui", "click.wav"), BuildTinyWav(880));
        await File.WriteAllBytesAsync(Path.Combine(root, "Locale", "de", "Sounds", "ui", "click.wav"), BuildTinyWav(440));
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "de");
            var path = lc.TryResolveLocalizedPath("Sounds/ui/click.wav");
            Assert.Equal("Locale/de/Sounds/ui/click.wav", path);

            var bytes = await lc.TryLoadLocalizedBytesAsync("Sounds/ui/click.wav");
            Assert.NotNull(bytes);
            var decoded = AudioClipDecoder.TryDecode("Sounds/ui/click.wav", bytes!);
            Assert.NotNull(decoded);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static byte[] BuildTinyWav(int hz)
    {
        // Minimal valid mono16 WAV (8 samples).
        var pcm = new byte[16];
        for (var i = 0; i < 8; i++)
        {
            var v = (short)(Math.Sin(2 * Math.PI * hz * i / 8000.0) * 10000);
            pcm[i * 2] = (byte)(v & 0xFF);
            pcm[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + pcm.Length);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(8000);
        w.Write(16000);
        w.Write((short)2);
        w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(pcm.Length);
        w.Write(pcm);
        return ms.ToArray();
    }
}
