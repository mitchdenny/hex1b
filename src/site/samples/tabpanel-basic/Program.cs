using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.TabPanel(tp => [
        tp.Tab("Overview", t => [
            t.Text("Welcome to Hex1b!"),
            t.Text(""),
            t.Text("This is the Overview tab content.")
        ]),
        tp.Tab("Settings", t => [
            t.Text("Application Settings"),
            t.Text(""),
            t.Text("Configure your preferences here.")
        ]),
        tp.Tab("Help", t => [
            t.Text("Documentation and Support"),
            t.Text(""),
            t.Text("Visit hex1b.dev for more information.")
        ])
    ]).Selector().Fill())
    .Build();

await terminal.RunAsync();
