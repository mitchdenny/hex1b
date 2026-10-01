using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Accordion(a => [
        a.Section(s => [
            s.Text("  src/"),
            s.Text("    Program.cs"),
            s.Text("    Utils.cs"),
            s.Text("    Models/"),
        ]).Title("EXPLORER"),

        a.Section(s => [
            s.Text("  ▸ Properties"),
            s.Text("  ▸ Methods"),
            s.Text("  ▸ Fields"),
        ]).Title("OUTLINE"),

        a.Section(s => [
            s.Text("  ● Updated README.md"),
            s.Text("  ● Fixed build script"),
        ]).Title("TIMELINE"),
    ]))
    .Build();

await terminal.RunAsync();
