using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ThemingPanel Widget Documentation: Multiple Theme Scopes
/// Demonstrates side-by-side theme variations using ThemingPanel.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example should demonstrate multiple theme scopes side by side.
/// src/content/guide/widgets/theming-panel.md
/// </remarks>
public class PanelInteractiveExample(ILogger<PanelInteractiveExample> logger) : Hex1bExample
{
    private readonly ILogger<PanelInteractiveExample> _logger = logger;

    public override string Id => "panel-interactive";
    public override string Title => "ThemingPanel - Multiple Theme Scopes";
    public override string Description => "Demonstrates side-by-side theme variations";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating theming panel interactive example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.HStack(h => [
                // Normal theme section
                h.Border(b => [
                    b.VStack(v => [
                        v.Text("Default Theme"),
                        v.Button("Normal"),
                        v.Button("Buttons")
                    ])
                ], title: "Standard"),
                
                // Success themed section
                h.ThemingPanel(
                    theme => theme
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Green)
                        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
                        .Set(BorderTheme.BorderColor, Hex1bColor.Green),
                    h.Border(b => [
                        b.VStack(v => [
                            v.Text("Success Theme"),
                            v.Button("Green"),
                            v.Button("Buttons")
                        ])
                    ], title: "Styled")
                ),
                
                // Danger themed section
                h.ThemingPanel(
                    theme => theme
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
                        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.White)
                        .Set(BorderTheme.BorderColor, Hex1bColor.Red),
                    h.Border(b => [
                        b.VStack(v => [
                            v.Text("Danger Theme"),
                            v.Button("Red"),
                            v.Button("Buttons")
                        ])
                    ], title: "Warning")
                )
            ]);
        };
    }
}
