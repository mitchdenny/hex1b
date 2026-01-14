using Hex1b;

string power = "Off";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Binary Toggle Example"),
        v.Text(""),
        v.HStack(h => [
            h.Text("Power: "),
            h.ToggleSwitch(["Off", "On"])
                .OnSelectionChanged(args => power = args.SelectedOption)
        ]),
        v.Text($"Current: {power}")
    ]))
    .Build();

await terminal.RunAsync();
