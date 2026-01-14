using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Unfocused:"),
        v.ToggleSwitch(["Off", "On"], selectedIndex: 1),
        v.Text(""),
        v.Text("Focused:"),
        v.ToggleSwitch(["Light", "Dark"], selectedIndex: 1)
    ]))
    .Build();

await terminal.RunAsync();
