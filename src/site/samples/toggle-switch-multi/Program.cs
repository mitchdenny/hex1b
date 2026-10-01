using Hex1b;

string currentSpeed = "Normal";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text("Speed Settings"),
            v.Text(""),
            v.HStack(h => [
                h.Text("Animation Speed: ").FixedWidth(20),
                h.ToggleSwitch(["Slow", "Normal", "Fast"], selectedIndex: 1)
                    .OnSelectionChanged(args => currentSpeed = args.SelectedOption)
            ]),
            v.Text(""),
            v.Text($"Current speed: {currentSpeed}"),
            v.Text(""),
            v.Text("Use arrow keys to cycle through options")
        ])
    ]).Title("Configuration"))
    .Build();

await terminal.RunAsync();
