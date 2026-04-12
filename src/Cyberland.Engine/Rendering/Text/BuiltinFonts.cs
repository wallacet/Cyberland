using System.Reflection;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Embedded Apache-licensed Roboto subset faces shipped with the engine for UI text without mod assets.
/// </summary>
/// <remarks>
/// Attribution: see <c>Rendering/Text/Builtin/NOTICE.txt</c>. Mods should prefer
/// <see cref="FontLibrary.RegisterFamilyFromBytes"/> for branding; these ids remain stable for samples and tools.
/// </remarks>
public static class BuiltinFonts
{
    /// <summary>Logical id for the embedded UI sans face (Roboto subset).</summary>
    public const string UiSans = "cyberland.engine/ui";

    /// <summary>Logical id for the embedded monospace face (Roboto Mono subset).</summary>
    public const string Mono = "cyberland.engine/mono";

    private const string UiSansResource = "Cyberland.Engine.Rendering.Text.Builtin.Roboto-Regular.ttf";
    private const string MonoResource = "Cyberland.Engine.Rendering.Text.Builtin.RobotoMono-Regular.ttf";

    /// <summary>Registers both built-in families on <paramref name="library"/> (no-op if streams are missing).</summary>
    public static void AddTo(FontLibrary library)
    {
        if (library is null)
            throw new ArgumentNullException(nameof(library));

        var asm = Assembly.GetExecutingAssembly();
        if (Read(asm, UiSansResource) is { } ui)
            library.RegisterFamilyFromBytes(UiSans, ui);
        if (Read(asm, MonoResource) is { } mono)
            library.RegisterFamilyFromBytes(Mono, mono);
    }

    private static ReadOnlyMemory<byte>? Read(Assembly asm, string name)
    {
        using var s = asm.GetManifestResourceStream(name);
        if (s is null)
            return null;

        using var ms = new MemoryStream((int)Math.Min(s.Length, int.MaxValue));
        s.CopyTo(ms);
        return new ReadOnlyMemory<byte>(ms.ToArray());
    }
}
