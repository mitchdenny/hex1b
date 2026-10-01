using Hex1b;

var state = new TodoState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text("Press Enter or Space to toggle items:"),
            v.Text(""),
            v.List(state.GetFormattedItems())
                .OnItemActivated(e => state.ToggleItem(e.ActivatedIndex))
        ])
    ]).Title("Todo List"))
    .Build();

await terminal.RunAsync();

class TodoState
{
    private readonly List<(string Text, bool Done)> _items =
    [
        ("Learn Hex1b", true),
        ("Build a TUI", false),
        ("Deploy to production", false)
    ];

    public IReadOnlyList<string> GetFormattedItems() =>
        _items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

    public void ToggleItem(int index)
    {
        if (index >= 0 && index < _items.Count)
        {
            var item = _items[index];
            _items[index] = (item.Text, !item.Done);
        }
    }
}
