using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// InfoBar Widget Documentation: Basic Usage
/// Demonstrates InfoBar with sections and default separator.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/infobar.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class InfoBarBasicExample(ILogger<InfoBarBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<InfoBarBasicExample> _logger = logger;

    public override string Id => "infobar-basic";
    public override string Title => "InfoBar - Basic Usage";
    public override string Description => "Demonstrates InfoBar with sections and default separator";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating InfoBar basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Border(b => [
                    b.Text("Main content area"),
                    b.Text(""),
                    b.Text("The status bar sits at the bottom of the window")
                ], title: "Application").FillHeight(),
                v.InfoBar(s => [
                    s.Section("NORMAL"),
                    s.Section("main.cs"),
                    s.Section("Ln 42, Col 8")
                ]).WithDefaultSeparator(" │ ")
            ]);
        };
    }
}
