using Hex1b;
using Hex1b.Integrations.Spectre.SpectreTui;
using Spectre.Tui;
using Spectre.Tui.Ansi;

namespace Hex1b.Integrations.Spectre.Tests;

[TestClass]
public class Hex1bSpectreTuiTerminalTests
{
    [TestMethod]
    public void Constructor_NullAdapter_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Hex1bSpectreTuiTerminal(null!));
    }

    [TestMethod]
    public async Task GetSize_ReturnsAdapterDimensions_InFullscreenMode()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        await adapter.ResizeAsync(120, 40);

        using var terminal = new Hex1bSpectreTuiTerminal(adapter);

        var size = terminal.GetSize();

        Assert.AreEqual(120, size.Width);
        Assert.AreEqual(40, size.Height);
    }

    [TestMethod]
    public void GetSize_FallsBackToStandardDefaults_WhenAdapterIsZero()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        // Adapter starts at 0,0 until ResizeAsync is called.

        using var terminal = new Hex1bSpectreTuiTerminal(adapter);

        var size = terminal.GetSize();

        Assert.AreEqual(80, size.Width);
        Assert.AreEqual(24, size.Height);
    }

    [TestMethod]
    public async Task Flush_WritesRenderedBufferToAdapter()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        await adapter.ResizeAsync(20, 5);

        using var terminal = new Hex1bSpectreTuiTerminal(adapter);

        // Use the high-level API the way Spectre.Tui itself does.
        terminal.Clear();
        terminal.MoveTo(0, 0);
        terminal.Write(new Cell().SetSymbol('H'));
        terminal.Write(new Cell().SetSymbol('i'));
        terminal.Flush();

        var output = DrainOutput(adapter);
        StringAssert.Contains(output, "H");
        StringAssert.Contains(output, "i");
    }

    [TestMethod]
    public async Task EnteringFullscreenMode_EmitsAlternateScreenSequence()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        await adapter.ResizeAsync(20, 5);

        // The base AnsiTerminal queues the alt-screen sequence into the
        // internal buffer when Mode.OnAttach runs. Flush() pushes it through.
        using var terminal = new Hex1bSpectreTuiTerminal(adapter, new FullscreenMode());
        terminal.Flush();

        var output = DrainOutput(adapter);
        StringAssert.Contains(output, "\x1b[?1049h");
    }

    [TestMethod]
    public void HideCursor_DoesNotEmitCursorHideSequence()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bSpectreTuiTerminal(adapter);

        // Drain anything emitted during construction (alt-screen attach etc.)
        terminal.Flush();
        DrainOutput(adapter);

        terminal.HideCursor();
        terminal.Flush();

        var pending = DrainOutput(adapter);
        Assert.IsFalse(pending.Contains("\x1b[?25l"));
    }

    private static string DrainOutput(Hex1bAppWorkloadAdapter adapter)
    {
        var sb = new System.Text.StringBuilder();
        while (adapter.TryReadOutput(out var chunk))
        {
            sb.Append(System.Text.Encoding.UTF8.GetString(chunk.Span));
        }
        return sb.ToString();
    }
}
