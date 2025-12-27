using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ThemingPanel Widget Documentation: Basic Usage
/// Demonstrates scoped button styling via ThemingPanel.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/theming-panel.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class PanelBasicExample(ILogger<PanelBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<PanelBasicExample> _logger = logger;

    public override string Id => "panel-basic";
    public override string Title => "ThemingPanel - Scoped Button Styles";
    public override string Description => "Demonstrates scoped button styling via ThemingPanel";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating theming panel basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ThemingPanel(
                theme => theme
                    .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Cyan)
                    .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black),
                ctx.VStack(v => [
                    v.Text("Buttons in this panel have cyan focus:"),
                    v.Button("Styled Button"),
                    v.Button("Another Button")
                ])
            );
        };
    }
}
