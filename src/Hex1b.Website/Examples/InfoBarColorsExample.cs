using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// InfoBar Widget Documentation: Custom Colors
/// Demonstrates an info bar with custom colored sections.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the colorsCode sample in:
/// src/content/guide/widgets/infobar.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class InfoBarColorsExample(ILogger<InfoBarColorsExample> logger) : Hex1bExample
{
    private readonly ILogger<InfoBarColorsExample> _logger = logger;

    public override string Id => "infobar-colors";
    public override string Title => "InfoBar Widget - Custom Colors";
    public override string Description => "Demonstrates sections with custom colors";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating infobar colors example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Border(b => [
                    b.Text("Application content here...")
                ], title: "Editor").Fill(),
                v.InfoBar([
                    new InfoBarSection("Mode: Normal"),
                    new InfoBarSection(" | "),
                    new InfoBarSection("ERROR", Hex1bColor.Red, Hex1bColor.Yellow),
                    new InfoBarSection(" | "),
                    new InfoBarSection("Ln 42, Col 7")
                ])
            ]);
        };
    }
}
