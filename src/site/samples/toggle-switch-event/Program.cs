using Hex1b;

var eventLog = new List<string>();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text("Settings Panel"),
            v.Text(""),
            v.HStack(h => [
                h.Text("Theme:         ").FixedWidth(16),
                h.ToggleSwitch(["Light", "Dark"], selectedIndex: 1)
                    .OnSelectionChanged(args => 
                    {
                        eventLog.Add($"Theme changed to: {args.SelectedOption}");
                    })
            ]),
            v.Text(""),
            v.HStack(h => [
                h.Text("Notifications: ").FixedWidth(16),
                h.ToggleSwitch(["Off", "On"], selectedIndex: 1)
                    .OnSelectionChanged(args => 
                    {
                        eventLog.Add($"Notifications: {args.SelectedOption}");
                    })
            ]),
            v.Text(""),
            v.Text("Event Log:"),
            ..eventLog.TakeLast(3).Select(log => v.Text($"  • {log}"))
        ])
    ]).Title("User Preferences"))
    .Build();

await terminal.RunAsync();
