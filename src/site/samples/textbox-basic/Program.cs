using Hex1b;

var state = new InputState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("TextBox Widget Demo"),
        v.Text("────────────────────"),
        v.Text(""),
        v.Text("Enter your name:"),
        v.TextBox(state.Input).OnTextChanged(args => state.Input = args.NewText),
        v.Text(""),
        v.Text($"You typed: {state.Input}"),
        v.Text(""),
        v.Text("Try typing, using arrow keys, Home/End, etc.")
    ]))
    .Build();

await terminal.RunAsync();

class InputState
{
    public string Input { get; set; } = "";
}
