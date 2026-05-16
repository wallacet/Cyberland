using System.Text.Json;
using Silk.NET.Maths;

namespace Cyberland.Engine.RuntimeScenes.Serialization;

/// <summary>Shared JSON field readers for scene component deserializers.</summary>
public static class SceneComponentJson
{
    /// <summary>Reads a single-precision float property or returns <paramref name="fallback"/>.</summary>
    public static float ReadFloat(JsonElement data, string name, float fallback)
    {
        if (data.TryGetProperty(name, out var p) && p.TryGetSingle(out var v))
            return v;
        return fallback;
    }

    /// <summary>Reads a 32-bit integer property or returns <paramref name="fallback"/>.</summary>
    public static int ReadInt(JsonElement data, string name, int fallback)
    {
        if (data.TryGetProperty(name, out var p) && p.TryGetInt32(out var v))
            return v;
        return fallback;
    }

    /// <summary>Reads a boolean property or returns <paramref name="fallback"/>.</summary>
    public static bool ReadBool(JsonElement data, string name, bool fallback)
    {
        if (data.TryGetProperty(name, out var p) && (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False))
            return p.GetBoolean();
        return fallback;
    }

    /// <summary>Reads a string property when present.</summary>
    public static string? ReadString(JsonElement data, string name)
    {
        if (data.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
            return p.GetString();
        return null;
    }

    /// <summary>Reads an enum from a string property (case-insensitive) or returns <paramref name="fallback"/>.</summary>
    public static TEnum ReadEnum<TEnum>(JsonElement data, string name, TEnum fallback)
        where TEnum : struct, Enum
    {
        var s = ReadString(data, name);
        return s is not null && Enum.TryParse<TEnum>(s, ignoreCase: true, out var e) ? e : fallback;
    }

    /// <summary>Reads an object with <c>x</c>/<c>y</c> floats into a 2D vector.</summary>
    public static bool TryReadVec2(JsonElement data, string name, out Vector2D<float> value)
    {
        value = default;
        if (!data.TryGetProperty(name, out var obj))
            return false;
        var x = ReadFloat(obj, "x", 0f);
        var y = ReadFloat(obj, "y", 0f);
        value = new Vector2D<float>(x, y);
        return true;
    }

    /// <summary>Reads an object with <c>x</c>/<c>y</c>/<c>z</c> floats into a 3D vector.</summary>
    public static bool TryReadVec3(JsonElement data, string name, out Vector3D<float> value)
    {
        value = default;
        if (!data.TryGetProperty(name, out var obj))
            return false;
        value = new Vector3D<float>(
            ReadFloat(obj, "x", 0f),
            ReadFloat(obj, "y", 0f),
            ReadFloat(obj, "z", 0f));
        return true;
    }

    /// <summary>Reads an object with <c>x</c>/<c>y</c>/<c>z</c>/<c>w</c> floats into a 4D vector.</summary>
    public static bool TryReadVec4(JsonElement data, string name, out Vector4D<float> value)
    {
        value = default;
        if (!data.TryGetProperty(name, out var obj))
            return false;
        value = new Vector4D<float>(
            ReadFloat(obj, "x", 0f),
            ReadFloat(obj, "y", 0f),
            ReadFloat(obj, "z", 0f),
            ReadFloat(obj, "w", 1f));
        return true;
    }
}
