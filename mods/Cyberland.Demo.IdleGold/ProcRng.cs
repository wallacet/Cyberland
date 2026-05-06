namespace Cyberland.Demo.IdleGold;

/// <summary>Deterministic doubles in [0,1) from a <see cref="ulong"/> state.</summary>
public static class ProcRng
{
    public static double Next01(ref ulong s)
    {
        s += 0x9E3779B97F4A7C15UL;
        var z = s;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (z >> 11) * (1.0 / (1UL << 53));
    }
}
