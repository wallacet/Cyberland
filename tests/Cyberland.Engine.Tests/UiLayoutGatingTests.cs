using Cyberland.Engine.UI.Core;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class UiLayoutGatingTests
{
    [Fact]
    public void ApplyEnvironmentDefaults_disables_incremental_when_env_false()
    {
        var prevFlag = UiLayoutGating.UseIncrementalDocumentFrames;
        try
        {
            UiLayoutGating.UseIncrementalDocumentFrames = true;
            Environment.SetEnvironmentVariable("CYBERLAND_USE_INCREMENTAL_UI", "0");
            UiLayoutGating.ApplyEnvironmentDefaults();
            Assert.False(UiLayoutGating.UseIncrementalDocumentFrames);

            UiLayoutGating.UseIncrementalDocumentFrames = false;
            Environment.SetEnvironmentVariable("CYBERLAND_USE_INCREMENTAL_UI", "true");
            UiLayoutGating.ApplyEnvironmentDefaults();
            Assert.True(UiLayoutGating.UseIncrementalDocumentFrames);

            Environment.SetEnvironmentVariable("CYBERLAND_USE_INCREMENTAL_UI", "");
            UiLayoutGating.ApplyEnvironmentDefaults();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CYBERLAND_USE_INCREMENTAL_UI", null);
            UiLayoutGating.UseIncrementalDocumentFrames = prevFlag;
        }
    }
}
