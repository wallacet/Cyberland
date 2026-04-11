using Cyberland.Engine.Modding;

namespace Cyberland.Engine.Tests;

public sealed class ExcludeModsParserTests
{
    [Fact]
    public void TryParse_returns_null_when_flag_absent()
    {
        Assert.Null(ExcludeModsParser.TryParse(new[] { "other", "x" }));
    }

    [Fact]
    public void TryParse_parses_comma_separated_ids_case_insensitive_flag()
    {
        var r = ExcludeModsParser.TryParse(new[] { "a", "--EXCLUDE-MODS", "one, two ", "tail" });
        Assert.NotNull(r);
        Assert.Equal(2, r!.Length);
        Assert.Equal("one", r[0]);
        Assert.Equal("two", r[1]);
    }

    [Fact]
    public void TryParse_empty_when_no_values_after_flag()
    {
        var r = ExcludeModsParser.TryParse(new[] { "--exclude-mods" });
        Assert.NotNull(r);
        Assert.Empty(r!);
    }

    [Fact]
    public void TryParse_empty_when_whitespace_token_after_flag()
    {
        var r = ExcludeModsParser.TryParse(new[] { "--exclude-mods", "   " });
        Assert.NotNull(r);
        Assert.Empty(r!);
    }

    [Fact]
    public void TryParse_empty_when_only_commas()
    {
        var r = ExcludeModsParser.TryParse(new[] { "--exclude-mods", "," });
        Assert.NotNull(r);
        Assert.Empty(r!);
    }
}
