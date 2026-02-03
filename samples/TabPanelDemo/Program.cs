using Hex1b;

// Demo showcasing TabPanel widget with multiple tabs

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            v.Text("TabPanel Demo"),
            v.Separator(),
            
            // TabPanel with composable tab content
            v.TabPanel(tp => [
                tp.Tab("Overview", t => [
                    t.Text("Welcome to the TabPanel demo!"),
                    t.Text(""),
                    t.Text("This tab shows an overview of the feature."),
                    t.Text("Use Alt+Right/Left to switch between tabs.")
                ]),
                
                tp.Tab("Details", t => [
                    t.Text("Details Tab"),
                    t.Text(""),
                    t.Text("This tab contains detailed information."),
                    t.Button("Action Button").OnClick(_ => { })
                ]),
                
                tp.Tab("Settings", t => [
                    t.Text("Settings Tab"),
                    t.Text(""),
                    t.Checkbox(false, "Enable feature"),
                    t.Checkbox(true, "Show notifications")
                ]),
                
                tp.Tab("About", t => [
                    t.Text("About Tab"),
                    t.Text(""),
                    t.Text("Hex1b TabPanel Widget v1.0")
                ])
            ]).Fill(),
            
            v.InfoBar(s => [
                s.Section("TabPanel Demo"),
                s.Spacer(),
                s.Section("Alt+Right: Next | Alt+Left: Previous")
            ])
        ]);
    })
    .Build();

await terminal.RunAsync();
