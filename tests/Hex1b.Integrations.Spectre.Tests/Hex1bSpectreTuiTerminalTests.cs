using Hex1b;
using Hex1b.Integrations.Spectre.SpectreTui;
using Spectre.Tui;
using Spectre.Tui.Ansi;

namespace Hex1b.Integrations.Spectre.Tests;

public class Hex1bSpectreTuiTerminalTests
{
    [Fact]
    public void Constructor_NullAdapter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Hex1bSpectreTuiTerminal(null!));
    }

    [Fact]
    public async Task GetSize_ReturnsAdapterDimensions_InFullscreenMode()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        await adapter.ResizeAsync(120, 40);

        using var terminal = new Hex1bSpectreTuiTerminal(adapter);

        var size = terminal.GetSize();

        Assert.Equal(120, size.Width);
        Assert.Equal(40, size.Height);
    }

    [Fact]
    public void GetSize_FallsBackToStandardDefaults_WhenAdapterIsZero()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        // Adapter starts at 0,0 until ResizeAsync is called.

        using var terminal = new Hex1bSpectreTuiTerminal(adapter);

        var size = terminal.GetSize();

        Assert.Equal(80, size.Width);
        Assert.Equal(24, size.Height);
    }

    [Fact]
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
        Assert.Contains("H", output);
        Assert.Contains("i", output);
    }

    [Fact]
    public async Task EnteringFullscreenMode_EmitsAlternateScreenSequence()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        await adapter.ResizeAsync(20, 5);

        // The base AnsiTerminal queues the alt-screen sequence into the
        // internal buffer when Mode.OnAttach runs. Flush() pushes it through.
        using var terminal = new Hex1bSpectreTuiTerminal(adapter, new FullscreenMode());
        terminal.Flush();

        var output = DrainOutput(adapter);
        Assert.Contains("\x1b[?1049h", output);
    }

    [Fact]
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
        Assert.DoesNotContain("\x1b[?25l", pending);
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

