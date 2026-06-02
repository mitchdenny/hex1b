using System.Text;

namespace Hex1b.Tests;

/// <summary>
/// Regression tests for <see cref="Hex1bAppWorkloadAdapter"/>'s TUI mode sequences,
/// specifically that mouse-enable bytes are only emitted when <see cref="Hex1bAppWorkloadAdapter.EnableMouse"/>
/// is set. This matters for embedded usage (e.g. an app used as a <c>WithPlaceholderHex1bApp</c>):
/// unconditional mouse-enable would flip the host's <c>TerminalWidgetHandle._mouseTrackingEnabled</c>
/// and leak host mouse events into the embedded workload after a swap.
/// </summary>
[TestClass]
public class Hex1bAppWorkloadAdapterMouseModeTests
{
    private static string Drain(Hex1bAppWorkloadAdapter adapter)
    {
        var sb = new StringBuilder();
        while (adapter.TryReadOutput(out var data))
        {
            sb.Append(Encoding.UTF8.GetString(data.Span));
        }
        return sb.ToString();
    }

    [TestMethod]
    public void EnterTuiMode_DoesNotEmitMouseEnable_WhenEnableMouseIsFalse()
    {
        var adapter = new Hex1bAppWorkloadAdapter();
        Assert.IsFalse(adapter.EnableMouse, "Precondition: EnableMouse defaults to false.");

        adapter.EnterTuiMode();
        var output = Drain(adapter);

        StringAssert.Contains(output, "\x1b[?1049h", "Should still enter alternate screen.");
        Assert.IsFalse(output.Contains("\x1b[?1003h"),
            "Mouse-enable DECSET 1003h must not be emitted when EnableMouse is false. " +
            "Otherwise an embedded host TerminalWidgetHandle will latch mouse-tracking on and forward host mouse events to the workload.");
        Assert.IsFalse(output.Contains("\x1b[?1006h"),
            "SGR mouse mode DECSET 1006h must not be emitted when EnableMouse is false.");
    }

    [TestMethod]
    public void EnterTuiMode_EmitsMouseEnable_WhenEnableMouseIsTrue()
    {
        var adapter = new Hex1bAppWorkloadAdapter { EnableMouse = true };

        adapter.EnterTuiMode();
        var output = Drain(adapter);

        StringAssert.Contains(output, "\x1b[?1003h");
        StringAssert.Contains(output, "\x1b[?1006h");
    }

    [TestMethod]
    public void ExitTuiMode_DoesNotEmitMouseDisable_WhenEnableMouseIsFalse()
    {
        var adapter = new Hex1bAppWorkloadAdapter();
        adapter.EnterTuiMode();
        _ = Drain(adapter);

        adapter.ExitTuiMode();
        var output = Drain(adapter);

        Assert.IsFalse(output.Contains("\x1b[?1003l"),
            "Mouse-disable should not be emitted when mouse was never enabled.");
        Assert.IsFalse(output.Contains("\x1b[?1006l"),
            "SGR mouse-disable should not be emitted when mouse was never enabled.");
        StringAssert.Contains(output, "\x1b[?1049l", "Should still exit alternate screen.");
    }

    [TestMethod]
    public void ExitTuiMode_EmitsMouseDisable_WhenEnableMouseIsTrue()
    {
        var adapter = new Hex1bAppWorkloadAdapter { EnableMouse = true };
        adapter.EnterTuiMode();
        _ = Drain(adapter);

        adapter.ExitTuiMode();
        var output = Drain(adapter);

        StringAssert.Contains(output, "\x1b[?1003l");
        StringAssert.Contains(output, "\x1b[?1006l");
    }
}
