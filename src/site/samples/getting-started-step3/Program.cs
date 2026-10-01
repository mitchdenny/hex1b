using Hex1b;

var state = new TodoState();

var app = new Hex1bApp(ctx =>
    ctx.VStack(v => [
        v.Border(b => [
            b.Text("📋 My Todos"),
            b.Text(""),
            b.List(state.FormatItems()).OnItemActivated(e => state.ToggleItem(e.ActivatedIndex))
        ]).Title("Todo List").Fill(),
        v.InfoBar("↑↓ Navigate  Space: Toggle")
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

    public IReadOnlyList<string> FormatItems() =>
        Items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

    public void ToggleItem(int index)
    {
        var item = Items[index];
        Items[index] = (item.Text, !item.Done);
    }
}
