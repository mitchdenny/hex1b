using Hex1b;

var state = new ChatState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Chat Demo"),
        v.Text("──────────"),
        v.Text(""),
        v.Text("Type a message and press Enter:"),
        v.TextBox(state.Input)
            .OnTextChanged(args => state.Input = args.NewText)
            .OnSubmit(args => {
                if (!string.IsNullOrWhiteSpace(state.Input))
                {
                    state.Messages.Add(state.Input);
                    state.Input = "";
                }
            }),
        v.Text(""),
        v.Text("Messages:"),
        ..state.Messages.TakeLast(5).Select(m => v.Text($"  • {m}"))
    ]))
    .Build();

await terminal.RunAsync();

class ChatState
{
    public string Input { get; set; } = "";
    public List<string> Messages { get; } = [];
}
