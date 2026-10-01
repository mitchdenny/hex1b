using Hex1b;

var state = new ListSelectionState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text($"Selected: {state.SelectedItem ?? "None"}"),
            v.Text(""),
            v.List(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
                .OnSelectionChanged(e => state.SelectedItem = e.SelectedText)
        ])
    ]).Title("Selection Demo"))
    .Build();

await terminal.RunAsync();

class ListSelectionState
{
    public string? SelectedItem { get; set; }
}
