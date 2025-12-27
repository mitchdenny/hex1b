using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ThemingPanel Widget Documentation: Panel Background
/// Demonstrates setting background color via ThemingPanel.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the code sample in:
/// src/content/guide/widgets/theming-panel.md (Panel Background section)
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class PanelBackgroundExample(ILogger<PanelBackgroundExample> logger) : Hex1bExample
{
    private readonly ILogger<PanelBackgroundExample> _logger = logger;

    public override string Id => "panel-background";
    public override string Title => "ThemingPanel - Background Color";
    public override string Description => "Demonstrates setting background color via ThemingPanel";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating theming panel background example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ThemingPanel(
                theme => theme
                    .Set(ThemingPanelTheme.BackgroundColor, Hex1bColor.FromRgb(0, 0, 139))
                    .Set(ThemingPanelTheme.ForegroundColor, Hex1bColor.White),
                ctx.Text("White text on dark blue")
            );
        };
    }
}
