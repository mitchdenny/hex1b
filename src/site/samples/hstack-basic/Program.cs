using Hex1b;

var state = new FormState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.HStack(h => [
        h.Text("Name:"),
        h.Text("  "),
        h.TextBox(state.Name)
            .OnTextChanged(args => state.Name = args.NewText)
            .Fill(),
        h.Text("  "),
        h.Button("Save").OnClick(_ => state.Save())
    ]))
    .Build();

await terminal.RunAsync();

class FormState
{
    public string Name { get; set; } = "";
    public void Save() { /* ... */ }
}
