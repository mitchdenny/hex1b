using Hex1b;
using Hex1b.Widgets;

var state = new TodoState();

var app = new Hex1bApp(ctx =>
    ctx.VStack(v => [
        v.Border(b => [
            b.HStack(h => [
                h.Text("New task: "),
                h.TextBox(state.NewItemText).OnTextChanged(e => state.NewItemText = e.NewText),
                h.Button("Add").OnClick(_ => state.AddItem())
            ]),
            new SeparatorWidget(),
            b.List(state.FormatItems()).OnItemActivated(e => state.ToggleItem(e.ActivatedIndex))
        ]).Title("📋 Todo").Fill(),
        v.InfoBar("Tab: Focus next  Space: Toggle")
    ])
);

await app.RunAsync();

class TodoState
{
    public List<(string Text, bool Done)> Items { get; } = 
    [
        ("Learn Hex1b", true),
        ("Build a TUI", false)
    ];

    public string NewItemText { get; set; } = "";

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
}
