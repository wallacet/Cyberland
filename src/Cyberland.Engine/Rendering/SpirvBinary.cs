using System.Buffers.Binary;

namespace Cyberland.Engine.Rendering;

/// <summary>
/// Helpers for validating and decoding SPIR-V bytecode payloads before creating Vulkan shader modules.
/// </summary>
internal static class SpirvBinary
{
    private const uint SpirvMagic = 0x07230203u;

    /// <summary>
    /// Validates a raw SPIR-V payload and converts it into Vulkan-ready dwords.
    /// </summary>
    /// <param name="bytes">Raw little-endian SPIR-V bytes.</param>
    /// <param name="words">Decoded dwords when validation succeeds.</param>
    /// <param name="failureReason">Validation error text when decoding fails.</param>
    /// <returns><c>true</c> when <paramref name="bytes"/> is valid SPIR-V for <c>vkCreateShaderModule</c>.</returns>
    public static bool TryDecodeWords(ReadOnlySpan<byte> bytes, out uint[] words, out string failureReason)
    {
        if (bytes.Length == 0)
        {
            words = Array.Empty<uint>();
            failureReason = "SPIR-V payload is empty";
            return false;
        }

        if ((bytes.Length % sizeof(uint)) != 0)
        {
            words = Array.Empty<uint>();
            failureReason = "SPIR-V byte length is not divisible by 4";
            return false;
        }

        words = new uint[bytes.Length / sizeof(uint)];
        for (var i = 0; i < words.Length; i++)
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(i * sizeof(uint), sizeof(uint)));

        if (words[0] != SpirvMagic)
        {
            words = Array.Empty<uint>();
            failureReason = $"SPIR-V magic mismatch (expected 0x{SpirvMagic:X8}, got 0x{BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, sizeof(uint))):X8})";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }
}
