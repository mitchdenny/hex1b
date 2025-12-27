using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ThemingPanel Widget Documentation: List Styling
/// Demonstrates customizing list appearance via ThemingPanel.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the code sample in:
/// src/content/guide/widgets/theming-panel.md (List Styling section)
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class PanelListStylingExample(ILogger<PanelListStylingExample> logger) : Hex1bExample
{
    private readonly ILogger<PanelListStylingExample> _logger = logger;

    public override string Id => "panel-list-styling";
    public override string Title => "ThemingPanel - List Styling";
    public override string Description => "Demonstrates customizing list appearance via ThemingPanel";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating theming panel list styling example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ThemingPanel(
                theme => theme
                    .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black)
                    .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.Yellow)
                    .Set(ListTheme.SelectedIndicator, "→ ")
                    .Set(ListTheme.UnselectedIndicator, "  "),
                ctx.List(new[] { "Option A", "Option B", "Option C" })
            );
        };
    }
}
