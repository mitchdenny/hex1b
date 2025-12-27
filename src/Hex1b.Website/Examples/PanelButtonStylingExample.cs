using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ThemingPanel Widget Documentation: Button Styling
/// Demonstrates customizing button appearance via ThemingPanel.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the code sample in:
/// src/content/guide/widgets/theming-panel.md (Button Styling section)
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class PanelButtonStylingExample(ILogger<PanelButtonStylingExample> logger) : Hex1bExample
{
    private readonly ILogger<PanelButtonStylingExample> _logger = logger;

    public override string Id => "panel-button-styling";
    public override string Title => "ThemingPanel - Button Styling";
    public override string Description => "Demonstrates customizing button appearance via ThemingPanel";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating theming panel button styling example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ThemingPanel(
                theme => theme
                    .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Cyan)
                    .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
                    .Set(ButtonTheme.BackgroundColor, Hex1bColor.DarkGray)
                    .Set(ButtonTheme.LeftBracket, "< ")
                    .Set(ButtonTheme.RightBracket, " >"),
                ctx.Button("Custom Button Style")
            );
        };
    }
}
