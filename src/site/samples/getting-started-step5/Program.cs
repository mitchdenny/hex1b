using Hex1b;
using Hex1b.Widgets;

var state = new TodoState();

var app = new Hex1bApp(ctx =>
    ctx.VStack(v => [
        v.Border(b => [
            b.Text($"📋 Todo List ({state.RemainingCount} remaining)"),
            b.Text(""),
            b.HStack(h => [
                h.Text("New: "),
                h.TextBox(state.NewItemText).OnTextChanged(e => state.NewItemText = e.NewText),
                h.Button("Add").OnClick(_ => state.AddItem())
            ]),
            new SeparatorWidget(),
            b.List(state.FormatItems())
                .OnSelectionChanged(e => state.SelectedIndex = e.SelectedIndex)
                .OnItemActivated(e => state.ToggleItem(e.ActivatedIndex)),
            b.Text(""),
            b.Button("Delete Selected").OnClick(_ => state.DeleteItem(state.SelectedIndex))
        ]).Title("My Todos").Fill(),
        v.InfoBar("↑↓: Navigate  Space: Toggle  Tab: Focus  Del: Delete")
    ])
);

await app.RunAsync();

class TodoState
{
    public List<(string Text, bool Done)> Items { get; } = 
    [
        ("Learn Hex1b", true),
        ("Build a TUI", false),
        ("Deploy to production", false)
    ];

    public string NewItemText { get; set; } = "";
    public int SelectedIndex { get; set; }

    public int RemainingCount => Items.Count(i => !i.Done);

    public IReadOnlyList<string> FormatItems() =>
        Items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

    public void AddItem()
    {
        if (!string.IsNullOrWhiteSpace(NewItemText))
        {
            Items.Add((NewItemText, false));
            NewItemText = "";
        }
    }

    public void ToggleItem(int index)
    {
        var item = Items[index];
        Items[index] = (item.Text, !item.Done);
    }

    public void DeleteItem(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            Items.RemoveAt(index);
            if (SelectedIndex >= Items.Count && Items.Count > 0)
            {
                SelectedIndex = Items.Count - 1;
            }
        }
    }
}
