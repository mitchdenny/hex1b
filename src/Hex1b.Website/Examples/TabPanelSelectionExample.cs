using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// TabPanel Widget Documentation: Selection Tracking
/// Demonstrates tracking tab selection changes with state.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the selectionCode sample in:
/// src/content/guide/widgets/tabpanel.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TabPanelSelectionExample(ILogger<TabPanelSelectionExample> logger) : Hex1bExample
{
    private readonly ILogger<TabPanelSelectionExample> _logger = logger;

    public override string Id => "tabpanel-selection";
    public override string Title => "TabPanel - Selection Tracking";
    public override string Description => "Demonstrates tracking tab selection changes with state";

    private string _selectedTab = "Documents";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating TabPanel selection example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text($"Current tab: {_selectedTab}"),
                v.Text(""),
                v.TabPanel(tp => [
                    tp.Tab("Documents", t => [
                        t.Text("Your documents appear here")
                    ]).Selected(_selectedTab == "Documents"),
                    tp.Tab("Downloads", t => [
                        t.Text("Your downloads appear here")
                    ]).Selected(_selectedTab == "Downloads"),
                    tp.Tab("Pictures", t => [
                        t.Text("Your pictures appear here")
                    ]).Selected(_selectedTab == "Pictures")
                ])
                .OnSelectionChanged(e => _selectedTab = e.SelectedTitle)
                .Selector()
                .Fill()
            ]);
        };
    }
}
