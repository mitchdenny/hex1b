using System.Text;
using Hex1b;
using Hex1b.Integrations.Spectre.SpectreConsole;
using Spectre.Console;

namespace Hex1b.Integrations.Spectre.Tests;

public class Hex1bAnsiConsoleFactoryTests
{
    [Fact]
    public void Create_WithAdapter_ProducesUsableConsole()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var console = Hex1bAnsiConsole.Create(adapter);

        Assert.NotNull(console);
        Assert.NotNull(console.Profile);
        Assert.True(console.Profile.Out.IsTerminal);
    }

    [Fact]
    public void Create_WhenWritingMarkup_FlowsAnsiToAdapter()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var console = Hex1bAnsiConsole.Create(adapter);

        console.MarkupLine("[red]hello[/]");

        var captured = DrainOutput(adapter);
        Assert.Contains("hello", captured);
        // SGR red foreground sequence — proof Spectre actually emitted ANSI
        // and our writer faithfully passed the escape bytes through.
        Assert.Contains("\x1b[", captured);
    }

    [Fact]
    public void Create_WhenWritingPlainText_FlowsExpectedBytes()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var console = Hex1bAnsiConsole.Create(adapter);

        console.Write("plain text\n");

        var captured = DrainOutput(adapter);
        Assert.Contains("plain text", captured);
    }

    [Fact]
    public void Create_OverridesInputEvenWhenSettingsProvided()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var settings = new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
        };

        var console = Hex1bAnsiConsole.Create(adapter, settings);

        Assert.IsType<Hex1bAnsiConsoleInput>(console.Input);
    }

    [Fact]
    public void Create_DefaultsAnsiSupportToYes()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var console = Hex1bAnsiConsole.Create(adapter);

        Assert.True(console.Profile.Capabilities.Ansi);
    }

    private static string DrainOutput(Hex1bAppWorkloadAdapter adapter)
    {
        var sb = new StringBuilder();
        while (adapter.TryReadOutput(out var bytes))
        {
            sb.Append(Encoding.UTF8.GetString(bytes.Span));
        }
        return sb.ToString();
    }
}
