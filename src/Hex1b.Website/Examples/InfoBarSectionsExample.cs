using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// InfoBar Widget Documentation: Multiple Sections
/// Demonstrates an info bar with multiple sections for keyboard shortcuts.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the sectionsCode sample in:
/// src/content/guide/widgets/infobar.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class InfoBarSectionsExample(ILogger<InfoBarSectionsExample> logger) : Hex1bExample
{
    private readonly ILogger<InfoBarSectionsExample> _logger = logger;

    public override string Id => "infobar-sections";
    public override string Title => "InfoBar Widget - Multiple Sections";
    public override string Description => "Demonstrates sections for keyboard shortcuts";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating infobar sections example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Border(b => [
                    v.Text("Use Tab to navigate between fields"),
                    v.Text("Use Enter to submit"),
                    v.Text("Use Esc to cancel")
                ], title: "Instructions").Fill(),
                v.InfoBar([
                    "Tab", "Navigate",
                    "Enter", "Submit",
                    "Esc", "Cancel"
                ])
            ]);
        };
    }
}
