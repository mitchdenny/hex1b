using Hex1b;

var state = new EditorState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.HStack(h => [
            h.Button("New Tab").OnClick(_ => state.AddTab()),
            h.Text($"  {state.Tabs.Count} tab(s) open")
        ]),
        v.Text(""),
        state.Tabs.Count == 0
            ? v.Text("No tabs open. Click 'New Tab' to add one.")
            : v.TabPanel(tp => state.Tabs.Select((tab, idx) =>
                tp.Tab(tab.Name, t => [
                    t.Text($"Content of {tab.Name}"),
                    t.Text(""),
                    t.Text($"Created at: {tab.CreatedAt:HH:mm:ss}")
                ])
                .Selected(idx == state.SelectedIndex)
                .RightActions(i => [
                    i.Icon("×").OnClick(_ => state.CloseTab(idx))
                ])
            ).ToArray())
            .OnSelectionChanged(e => state.SelectedIndex = e.SelectedIndex)
            .Selector()
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();

class EditorState
{
    public List<TabInfo> Tabs { get; } = [];
    public int SelectedIndex { get; set; }
    private int _counter = 1;

    public void AddTab()
    {
        Tabs.Add(new TabInfo($"Tab {_counter++}", DateTime.Now));
        SelectedIndex = Tabs.Count - 1;
    }

    public void CloseTab(int index)
    {
        if (index >= 0 && index < Tabs.Count)
        {
            Tabs.RemoveAt(index);
            if (SelectedIndex >= Tabs.Count)
                SelectedIndex = Math.Max(0, Tabs.Count - 1);
        }
    }
}

record TabInfo(string Name, DateTime CreatedAt);
