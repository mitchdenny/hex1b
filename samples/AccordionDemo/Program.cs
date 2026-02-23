using Hex1b;
using Hex1b.Theming;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text(" Accordion Demo "),
        v.Separator(),

        v.HStack(h => [
            // Left sidebar with accordion
            h.Border(b => [
                b.Accordion(a => [
                    a.Section(s => [
                        s.Text("  src/"),
                        s.Text("    Program.cs"),
                        s.Text("    Utils.cs"),
                        s.Text("    Models/"),
                        s.Text("      User.cs"),
                        s.Text("      Product.cs"),
                    ]).Title("EXPLORER")
                      .RightActions(ra => [
                          ra.Icon("+"),
                          ra.Icon("⟳"),
                      ]),

                    a.Section(s => [
                        s.Text("  ▸ Properties"),
                        s.Text("  ▸ Methods"),
                        s.Text("  ▸ Fields"),
                    ]).Title("OUTLINE"),

                    a.Section(s => [
                        s.Text("  ● Updated README.md"),
                        s.Text("  ● Fixed build script"),
                        s.Text("  ● Added accordion widget"),
                    ]).Title("TIMELINE"),

                    a.Section(s => [
                        s.Text("  main"),
                        s.Text("  feature/accordion"),
                        s.Text("  develop"),
                    ]).Title("SOURCE CONTROL")
                      .Collapsed(),
                ])
            ]).Title("Sidebar").FixedWidth(35),

            // Main content area
            h.Border(b => [
                b.VStack(inner => [
                    inner.Text("Main content area"),
                    inner.Text(""),
                    inner.Text("Use ↑/↓ to navigate sections"),
                    inner.Text("Press Enter or Space to toggle"),
                    inner.Text("Click headers to expand/collapse"),
                    inner.Text(""),
                    inner.Text("Press Ctrl+C to exit"),
                ])
            ]).Title("Editor"),
        ]),
    ]))
    .Build();

await terminal.RunAsync();
