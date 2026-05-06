using Cyberland.Engine.Diagnostics;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class ProfileCommandLineTests
{
    [Fact]
    public void TryParseProfileSeconds_equals_form()
    {
        ReadOnlySpan<string> args = new[] { ProfileCommandLine.SecondsFlag + "=2.5" };
        Assert.Equal(2.5, ProfileCommandLine.TryParseProfileSeconds(args));
    }

    [Fact]
    public void TryParseProfileSeconds_next_token_form()
    {
        ReadOnlySpan<string> args = new[] { ProfileCommandLine.SecondsFlag, "3" };
        Assert.Equal(3.0, ProfileCommandLine.TryParseProfileSeconds(args));
    }

    [Fact]
    public void TryParseProfileSeconds_case_insensitive_flag()
    {
        ReadOnlySpan<string> args = new[] { "--PROFILE-SECONDS=1" };
        Assert.Equal(1.0, ProfileCommandLine.TryParseProfileSeconds(args));
    }

    [Fact]
    public void TryParseProfileSeconds_invalid_returns_null()
    {
        ReadOnlySpan<string> args = new[] { ProfileCommandLine.SecondsFlag + "=0" };
        Assert.Null(ProfileCommandLine.TryParseProfileSeconds(args));
    }

    [Fact]
    public void TryParseProfileSeconds_missing_value_returns_null()
    {
        ReadOnlySpan<string> args = new[] { ProfileCommandLine.SecondsFlag };
        Assert.Null(ProfileCommandLine.TryParseProfileSeconds(args));
    }

    [Fact]
    public void TryParseProfileSeconds_next_token_invalid_returns_null()
    {
        ReadOnlySpan<string> args = new[] { ProfileCommandLine.SecondsFlag, "not-a-number" };
        Assert.Null(ProfileCommandLine.TryParseProfileSeconds(args));
    }

    [Fact]
    public void TryParseProfileDump_equals_form()
    {
        ReadOnlySpan<string> args = new[] { ProfileCommandLine.DumpFlag + "=C:\\tmp\\out.txt" };
        Assert.Equal(@"C:\tmp\out.txt", ProfileCommandLine.TryParseProfileDump(args));
    }

    [Fact]
    public void TryParseProfileDump_next_token_form()
    {
        ReadOnlySpan<string> args = new[] { ProfileCommandLine.DumpFlag, "artifacts/profiles/x.txt" };
        Assert.Equal("artifacts/profiles/x.txt", ProfileCommandLine.TryParseProfileDump(args));
    }

    [Fact]
    public void TryParseProfileDump_missing_returns_null()
    {
        ReadOnlySpan<string> args = new[] { "other" };
        Assert.Null(ProfileCommandLine.TryParseProfileDump(args));
    }
}
