using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// TabPanel Widget Documentation: Dynamic Tabs
/// Demonstrates adding and removing tabs at runtime.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the dynamicCode sample in:
/// src/content/guide/widgets/tabpanel.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TabPanelDynamicExample(ILogger<TabPanelDynamicExample> logger) : Hex1bExample
{
    private readonly ILogger<TabPanelDynamicExample> _logger = logger;

    public override string Id => "tabpanel-dynamic";
    public override string Title => "TabPanel - Dynamic Tabs";
    public override string Description => "Demonstrates adding and removing tabs at runtime";

    private readonly List<TabInfo> _tabs = [];
    private int _selectedIndex;
    private int _counter = 1;

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating TabPanel dynamic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.HStack(h => [
                    h.Button("New Tab").OnClick(_ => AddTab()),
                    h.Text($"  {_tabs.Count} tab(s) open")
                ]),
                v.Text(""),
                _tabs.Count == 0
                    ? v.Text("No tabs open. Click 'New Tab' to add one.")
                    : v.TabPanel(tp => _tabs.Select((tab, idx) =>
                        tp.Tab(tab.Name, t => [
                            t.Text($"Content of {tab.Name}"),
                            t.Text(""),
                            t.Text($"Created at: {tab.CreatedAt:HH:mm:ss}")
                        ])
                        .Selected(idx == _selectedIndex)
                        .WithRightIcons(i => [
                            i.Icon("×").OnClick(_ => CloseTab(idx))
                        ])
                    ).ToArray())
                    .OnSelectionChanged(e => _selectedIndex = e.SelectedIndex)
                    .Selector()
                    .Fill()
            ]);
        };
    }

    private void AddTab()
    {
        _tabs.Add(new TabInfo($"Tab {_counter++}", DateTime.Now));
        _selectedIndex = _tabs.Count - 1;
    }

    private void CloseTab(int index)
    {
        if (index >= 0 && index < _tabs.Count)
        {
            _tabs.RemoveAt(index);
            if (_selectedIndex >= _tabs.Count)
                _selectedIndex = Math.Max(0, _tabs.Count - 1);
        }
    }

    private record TabInfo(string Name, DateTime CreatedAt);
}
