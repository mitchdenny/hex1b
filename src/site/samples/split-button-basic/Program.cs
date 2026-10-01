using Hex1b;

var state = new EditorState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Split Button Demo"),
        v.Text(""),
        v.Text($"Last action: {state.LastAction}"),
        v.Text(""),
        v.SplitButton()
           .PrimaryAction("Save", _ => state.LastAction = "Saved file")
           .SecondaryAction("Save As...", _ => state.LastAction = "Save As dialog")
           .SecondaryAction("Save All", _ => state.LastAction = "Saved all files")
           .SecondaryAction("Save Copy", _ => state.LastAction = "Saved copy"),
        v.Text(""),
        v.Text("Click the button or press ▼ for more options")
    ]))
    .Build();

await terminal.RunAsync();

class EditorState
{
    public string LastAction { get; set; } = "(none)";
}
