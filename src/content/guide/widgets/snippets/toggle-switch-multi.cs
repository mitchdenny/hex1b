using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Speed Settings:"),
        v.ToggleSwitch(["Slow", "Normal", "Fast"], selectedIndex: 1)
    ]))
    .Build();

await terminal.RunAsync();
