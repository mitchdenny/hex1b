using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ThemingPanel Widget Documentation: Border Styling
/// Demonstrates customizing border appearance via ThemingPanel.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the code sample in:
/// src/content/guide/widgets/theming-panel.md (Border Styling section)
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class PanelBorderStylingExample(ILogger<PanelBorderStylingExample> logger) : Hex1bExample
{
    private readonly ILogger<PanelBorderStylingExample> _logger = logger;

    public override string Id => "panel-border-styling";
    public override string Title => "ThemingPanel - Border Styling";
    public override string Description => "Demonstrates customizing border appearance via ThemingPanel";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating theming panel border styling example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ThemingPanel(
                theme => theme
                    .Set(BorderTheme.BorderColor, Hex1bColor.Cyan)
                    .Set(BorderTheme.TitleColor, Hex1bColor.White)
                    .Set(BorderTheme.TopLeftCorner, "╔")
                    .Set(BorderTheme.TopRightCorner, "╗")
                    .Set(BorderTheme.BottomLeftCorner, "╚")
                    .Set(BorderTheme.BottomRightCorner, "╝")
                    .Set(BorderTheme.HorizontalLine, "═")
                    .Set(BorderTheme.VerticalLine, "║"),
                ctx.Border(b => [ b.Text("Double-line border") ], title: "Fancy")
            );
        };
    }
}
