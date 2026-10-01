using Hex1b;

var state = new PickerState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text($"Selected fruit: {state.SelectedFruit}"),
            v.Text(""),
            v.HStack(h => [
                h.Text("Choose: "),
                h.Picker(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
                    .OnSelectionChanged(e => state.SelectedFruit = e.SelectedText)
            ])
        ])
    ]).Title("Selection Demo"))
    .Build();

await terminal.RunAsync();

class PickerState
{
    public string SelectedFruit { get; set; } = "Apple";
}
