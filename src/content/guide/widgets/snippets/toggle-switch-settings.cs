using Hex1b;

string theme = "Light";
string sound = "On";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Settings Panel"),
        v.Text(""),
        v.HStack(h => [
            h.Text("Theme:  ").FixedWidth(12),
            h.ToggleSwitch(["Light", "Dark"])
                .OnSelectionChanged(args => theme = args.SelectedOption)
        ]),
        v.HStack(h => [
            h.Text("Sound:  ").FixedWidth(12),
            h.ToggleSwitch(["Off", "On"], selectedIndex: 1)
                .OnSelectionChanged(args => sound = args.SelectedOption)
        ])
    ]))
    .Build();

await terminal.RunAsync();
