using Hex1b;

string mode = "Auto";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Mode Selection Example"),
        v.Text(""),
        v.HStack(h => [
            h.Text("Mode: "),
            h.ToggleSwitch(["Manual", "Auto", "Scheduled"], selectedIndex: 1)
                .OnSelectionChanged(args => mode = args.SelectedOption)
        ]),
        v.Text($"Selected: {mode}")
    ]))
    .Build();

await terminal.RunAsync();
