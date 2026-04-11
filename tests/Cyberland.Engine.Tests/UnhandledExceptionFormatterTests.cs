using Cyberland.Engine.Diagnostics;

namespace Cyberland.Engine.Tests;

public sealed class UnhandledExceptionFormatterTests
{
    [Fact]
    public void FormatExceptionForDisplay_includes_type_message_and_stack()
    {
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (Exception ex)
        {
            var s = UnhandledExceptionFormatter.FormatExceptionForDisplay(ex);
            Assert.Contains("InvalidOperationException", s, StringComparison.Ordinal);
            Assert.Contains("boom", s, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FormatExceptionForDisplay_walks_inner_exceptions()
    {
        var inner = new ArgumentException("inner");
        var outer = new Exception("outer", inner);
        var s = UnhandledExceptionFormatter.FormatExceptionForDisplay(outer);
        Assert.Contains("outer", s, StringComparison.Ordinal);
        Assert.Contains("inner", s, StringComparison.Ordinal);
        Assert.Contains("Inner exception", s, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatExceptionForDisplay_non_exception_uses_ToString()
    {
        var s = UnhandledExceptionFormatter.FormatExceptionForDisplay(42);
        Assert.Contains("42", s, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatExceptionForDisplay_null_payload()
    {
        var s = UnhandledExceptionFormatter.FormatExceptionForDisplay(null!);
        Assert.Contains("(null)", s, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatExceptionForDisplay_truncates_long_text()
    {
        var longMsg = new string('x', 20_000);
        try
        {
            throw new Exception(longMsg);
        }
        catch (Exception ex)
        {
            var s = UnhandledExceptionFormatter.FormatExceptionForDisplay(ex, maxLength: 500);
            Assert.True(s.Length <= 500, s.Length.ToString());
            Assert.Contains("truncated", s, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FormatExceptionForDisplay_respects_small_maxLength_floor()
    {
        try
        {
            throw new Exception("hi");
        }
        catch (Exception ex)
        {
            var s = UnhandledExceptionFormatter.FormatExceptionForDisplay(ex, maxLength: 10);
            Assert.True(s.Length >= 10);
        }
    }
}
