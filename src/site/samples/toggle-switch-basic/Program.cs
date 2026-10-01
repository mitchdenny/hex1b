using Hex1b;

string currentSelection = "Off";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("ToggleSwitch Examples"),
        v.Text(""),
        v.Text($"Power: {currentSelection}"),
        v.Text(""),
        v.HStack(h => [
            h.Text("Status: "),
            h.ToggleSwitch(["Off", "On"])
                .OnSelectionChanged(args => currentSelection = args.SelectedOption)
        ]),
        v.Text(""),
        v.Text("Use Left/Right arrows or click to toggle")
    ]))
    .Build();

await terminal.RunAsync();
