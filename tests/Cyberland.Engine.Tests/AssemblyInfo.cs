using Xunit;

// Static telemetry in TextGlyphCache is process-wide; serial execution avoids flaky parallel interference.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
