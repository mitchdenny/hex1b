using Hex1b;

const string DefaultText = "Hello 🎉 日本語 émoji 🚀 中文 ✨";
var state = new UnicodeState { Input = DefaultText };

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Unicode Text Editing"),
        v.Text("─────────────────────"),
        v.Text(""),
        v.TextBox(state.Input).OnTextChanged(args => state.Input = args.NewText),
        v.Text(""),
        v.Text("Try navigating with arrow keys, deleting emoji,"),
        v.Text("or adding your own Unicode characters!"),
        v.Text(""),
        v.Button("Reset to Default").OnClick(_ => state.Input = DefaultText)
    ]))
    .Build();

await terminal.RunAsync();

class UnicodeState
{
    public string Input { get; set; } = "";
}
