using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Checkbox States:"),
        v.Text(""),
        v.Checkbox().Unchecked().Label("Unchecked ▢"),
        v.Checkbox().Checked().Label("Checked ▣"),
        v.Checkbox().Indeterminate().Label("Indeterminate ▤")
    ]))
    .Build();

await terminal.RunAsync();
