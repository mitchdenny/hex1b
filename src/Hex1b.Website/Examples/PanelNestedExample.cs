using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ThemingPanel Widget Documentation: Nested Theme Scopes
/// Demonstrates nested ThemingPanels with overrides.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the code sample in:
/// src/content/guide/widgets/theming-panel.md (Nested Theme Scopes section)
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class PanelNestedExample(ILogger<PanelNestedExample> logger) : Hex1bExample
{
    private readonly ILogger<PanelNestedExample> _logger = logger;

    public override string Id => "panel-nested";
    public override string Title => "ThemingPanel - Nested Scopes";
    public override string Description => "Demonstrates nested ThemingPanels with overrides";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating theming panel nested example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ThemingPanel(
                theme => theme.Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Blue),
                ctx.VStack(v => [
                    v.Button("Blue focus"),
                    v.ThemingPanel(
                        theme => theme.Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red),
                        v.Button("Red focus (nested override)")
                    ),
                    v.Button("Blue focus again")
                ])
            );
        };
    }
}
