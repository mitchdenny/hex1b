using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ThemePanel Widget Documentation: Basic Usage
/// Demonstrates scoped theme mutations with ThemePanelWidget.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/themepanel.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ThemePanelBasicExample(ILogger<ThemePanelBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ThemePanelBasicExample> _logger = logger;

    public override string Id => "themepanel-basic";
    public override string Title => "ThemePanel Widget - Basic Usage";
    public override string Description => "Demonstrates scoped theme mutations";

    private class ThemePanelState
    {
        public int ClickCount { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating themepanel basic example widget builder");

        var state = new ThemePanelState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("ThemePanel Examples"),
                v.Text(""),
                v.Text("Default theme text"),
                v.Text(""),
                v.ThemePanel(
                    theme => theme.Clone()
                        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow)
                        .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(0, 0, 139)),
                    v.VStack(inner => [
                        inner.Text("Themed content"),
                        inner.Text("Yellow on dark blue")
                    ])
                ),
                v.Text(""),
                v.Text("Back to default theme"),
                v.Text(""),
                v.ThemePanel(
                    theme => theme.Clone()
                        .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(139, 0, 0))
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
                        .Set(ButtonTheme.ForegroundColor, Hex1bColor.White)
                        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.White),
                    v.VStack(danger => [
                        danger.Text("⚠ Danger Zone"),
                        danger.Button($"Danger Button ({state.ClickCount})").OnClick(_ => state.ClickCount++)
                    ])
                ),
                v.Text(""),
                v.Text("Press Tab to focus, Enter to click")
            ]);
        };
    }
}
