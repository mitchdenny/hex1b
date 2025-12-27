using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ThemingPanel Widget Documentation: Semantic Sections
/// Demonstrates using ThemingPanels for semantic UI zones.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the code sample in:
/// src/content/guide/widgets/theming-panel.md (Semantic Sections pattern)
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class PanelSemanticExample(ILogger<PanelSemanticExample> logger) : Hex1bExample
{
    private readonly ILogger<PanelSemanticExample> _logger = logger;

    public override string Id => "panel-semantic";
    public override string Title => "ThemingPanel - Semantic Sections";
    public override string Description => "Demonstrates using ThemingPanels for semantic UI zones";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating theming panel semantic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                // Primary action area
                v.ThemingPanel(
                    theme => theme
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Green)
                        .Set(BorderTheme.BorderColor, Hex1bColor.Green),
                    v.Border(b => [ b.Button("Confirm") ], title: "Primary")
                ),
                
                // Destructive action area  
                v.ThemingPanel(
                    theme => theme
                        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
                        .Set(BorderTheme.BorderColor, Hex1bColor.Red),
                    v.Border(b => [ b.Button("Delete") ], title: "Danger")
                )
            ]);
        };
    }
}
