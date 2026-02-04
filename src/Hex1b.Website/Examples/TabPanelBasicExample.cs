using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// TabPanel Widget Documentation: Basic Usage
/// Demonstrates simple tabbed interface with static tabs.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/tabpanel.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TabPanelBasicExample(ILogger<TabPanelBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<TabPanelBasicExample> _logger = logger;

    public override string Id => "tabpanel-basic";
    public override string Title => "TabPanel - Basic Usage";
    public override string Description => "Demonstrates basic tabbed interface with static tabs";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating TabPanel basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.TabPanel(tp => [
                tp.Tab("Overview", t => [
                    t.Text("Welcome to Hex1b!"),
                    t.Text(""),
                    t.Text("This is the Overview tab content.")
                ]),
                tp.Tab("Settings", t => [
                    t.Text("Application Settings"),
                    t.Text(""),
                    t.Text("Configure your preferences here.")
                ]),
                tp.Tab("Help", t => [
                    t.Text("Documentation and Support"),
                    t.Text(""),
                    t.Text("Visit hex1b.dev for more information.")
                ])
            ]).Selector().Fill();
        };
    }
}
