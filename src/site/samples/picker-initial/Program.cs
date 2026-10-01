using Hex1b;

var state = new FormState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.HStack(h => [
                h.Text("Size:     "),
                h.Picker(["Small", "Medium", "Large", "X-Large"], initialSelectedIndex: 1)
                    .OnSelectionChanged(e => state.Size = e.SelectedText)
            ]),
            v.HStack(h => [
                h.Text("Priority: "),
                h.Picker(["Low", "Medium", "High", "Critical"], initialSelectedIndex: 2)
                    .OnSelectionChanged(e => state.Priority = e.SelectedText)
            ]),
            v.Text(""),
            v.Text($"Order: {state.Size} priority {state.Priority}")
        ])
    ]).Title("Order Form"))
    .Build();

await terminal.RunAsync();

class FormState
{
    public string Size { get; set; } = "Medium";
    public string Priority { get; set; } = "High";
}
