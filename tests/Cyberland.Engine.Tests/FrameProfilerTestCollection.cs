using Xunit;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Serializes tests that mutate FrameProfiler/FrameProfilerStats global static state.
/// </summary>
[CollectionDefinition("FrameProfiler", DisableParallelization = true)]
public sealed class FrameProfilerTestCollection
{
}
