using Cyberland.Engine.Localization;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class LocalizationEdgeTests
{
    [Fact]
    public void LocalizationManager_merge_maps_null_json_string_to_empty()
    {
        var loc = new LocalizationManager();
        var json = """{"k":null}"""u8.ToArray();
        loc.MergeJson(json);
        Assert.Equal("", loc.Get("k"));
    }
}
