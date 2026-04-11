using Cyberland.Engine.Diagnostics;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class EngineDiagnosticsTests
{
    private sealed class RecordingSink : IEngineDiagnosticSink
    {
        public readonly List<(EngineErrorSeverity Severity, string Title, string Message)> Calls = new();

        public void Deliver(EngineErrorSeverity severity, string title, string message) =>
            Calls.Add((severity, title, message));
    }

    [Fact]
    public void Report_Major_delivers_once()
    {
        var prev = EngineDiagnostics.SinkOverride;
        var sink = new RecordingSink();
        EngineDiagnostics.SinkOverride = sink;
        try
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "t", "m");
            Assert.Single(sink.Calls);
            Assert.Equal(EngineErrorSeverity.Major, sink.Calls[0].Severity);
        }
        finally
        {
            EngineDiagnostics.SinkOverride = prev;
        }
    }

    [Fact]
    public void Report_Minor_dedupes_identical_title_and_message()
    {
        var prev = EngineDiagnostics.SinkOverride;
        var sink = new RecordingSink();
        EngineDiagnostics.SinkOverride = sink;
        try
        {
            EngineDiagnostics.ClearMinorDedupe();
            EngineDiagnostics.Report(EngineErrorSeverity.Minor, "t", "m");
            EngineDiagnostics.Report(EngineErrorSeverity.Minor, "t", "m");
            Assert.Single(sink.Calls);
        }
        finally
        {
            EngineDiagnostics.SinkOverride = prev;
        }
    }

    [Fact]
    public void Report_Minor_after_ClearMinorDedupe_can_repeat()
    {
        var prev = EngineDiagnostics.SinkOverride;
        var sink = new RecordingSink();
        EngineDiagnostics.SinkOverride = sink;
        try
        {
            EngineDiagnostics.ClearMinorDedupe();
            EngineDiagnostics.Report(EngineErrorSeverity.Minor, "t", "m");
            EngineDiagnostics.ClearMinorDedupe();
            EngineDiagnostics.Report(EngineErrorSeverity.Minor, "t", "m");
            Assert.Equal(2, sink.Calls.Count);
        }
        finally
        {
            EngineDiagnostics.SinkOverride = prev;
        }
    }

    [Fact]
    public void Report_Warning_does_not_dedupe()
    {
        var prev = EngineDiagnostics.SinkOverride;
        var sink = new RecordingSink();
        EngineDiagnostics.SinkOverride = sink;
        try
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Warning, "t", "a");
            EngineDiagnostics.Report(EngineErrorSeverity.Warning, "t", "a");
            Assert.Equal(2, sink.Calls.Count);
        }
        finally
        {
            EngineDiagnostics.SinkOverride = prev;
        }
    }

    [Fact]
    public void Report_throws_on_Fatal_severity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EngineDiagnostics.Report(EngineErrorSeverity.Fatal, "t", "m"));
    }

    [Fact]
    public void Report_throws_on_invalid_enum_value()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EngineDiagnostics.Report((EngineErrorSeverity)999, "t", "m"));
    }

    [Fact]
    public void Report_throws_on_null_title()
    {
        Assert.Throws<ArgumentNullException>(() =>
            EngineDiagnostics.Report(EngineErrorSeverity.Major, null!, "m"));
    }

    [Fact]
    public void Stderr_sink_writes_Major_Minor_Warning()
    {
        lock (ConsoleTestSync.ErrorRedirectLock)
        {
            var prev = Console.Error;
            try
            {
                using var sw = new StringWriter();
                Console.SetError(sw);
                StderrEngineDiagnosticSink.Instance.Deliver(EngineErrorSeverity.Major, "T", "M1");
                StderrEngineDiagnosticSink.Instance.Deliver(EngineErrorSeverity.Minor, "T", "M2");
                StderrEngineDiagnosticSink.Instance.Deliver(EngineErrorSeverity.Warning, "T", "M3");
                var s = sw.ToString();
                Assert.Contains("[MAJOR]", s, StringComparison.Ordinal);
                Assert.Contains("[MINOR]", s, StringComparison.Ordinal);
                Assert.Contains("[WARNING]", s, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetError(prev);
            }
        }
    }

    [Fact]
    public void Stderr_sink_throws_on_invalid_severity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StderrEngineDiagnosticSink.Instance.Deliver((EngineErrorSeverity)42, "t", "m"));
    }
}
