namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Content equality for sorted component id arrays used as archetype dictionary keys.
/// </summary>
internal sealed class SignatureComparer : IEqualityComparer<uint[]>
{
    public static readonly SignatureComparer Instance = new();

    public bool Equals(uint[]? x, uint[]? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null || x.Length != y.Length)
            return false;

        for (var i = 0; i < x.Length; i++)
        {
            if (x[i] != y[i])
                return false;
        }

        return true;
    }

    public int GetHashCode(uint[] obj)
    {
        unchecked
        {
            var h = 17;
            h = h * 31 + obj.Length;
            foreach (var u in obj)
                h = h * 31 + (int)u;
            return h;
        }
    }
}
