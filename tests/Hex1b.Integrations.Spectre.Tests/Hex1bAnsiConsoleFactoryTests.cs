using System.Text;
using Hex1b;
using Hex1b.Integrations.Spectre.SpectreConsole;
using Spectre.Console;

namespace Hex1b.Integrations.Spectre.Tests;

[TestClass]
public class Hex1bAnsiConsoleFactoryTests
{
    [TestMethod]
    public void Create_WithAdapter_ProducesUsableConsole()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var console = Hex1bAnsiConsole.Create(adapter);

        Assert.IsNotNull(console);
        Assert.IsNotNull(console.Profile);
        Assert.IsTrue(console.Profile.Out.IsTerminal);
    }

    [TestMethod]
    public void Create_WhenWritingMarkup_FlowsAnsiToAdapter()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var console = Hex1bAnsiConsole.Create(adapter);

        console.MarkupLine("[red]hello[/]");

        var captured = DrainOutput(adapter);
        StringAssert.Contains(captured, "hello");
        // SGR red foreground sequence — proof Spectre actually emitted ANSI
        // and our writer faithfully passed the escape bytes through.
        StringAssert.Contains(captured, "\x1b[");
    }

    [TestMethod]
    public void Create_WhenWritingPlainText_FlowsExpectedBytes()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var console = Hex1bAnsiConsole.Create(adapter);

        console.Write("plain text\n");

        var captured = DrainOutput(adapter);
        StringAssert.Contains(captured, "plain text");
    }

    [TestMethod]
    public void Create_OverridesInputEvenWhenSettingsProvided()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var settings = new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
        };

        var console = Hex1bAnsiConsole.Create(adapter, settings);

        Assert.IsInstanceOfType<Hex1bAnsiConsoleInput>(console.Input);
    }

    [TestMethod]
    public void Create_DefaultsAnsiSupportToYes()
    {
        using var adapter = new Hex1bAppWorkloadAdapter();
        var console = Hex1bAnsiConsole.Create(adapter);

        Assert.IsTrue(console.Profile.Capabilities.Ansi);
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
