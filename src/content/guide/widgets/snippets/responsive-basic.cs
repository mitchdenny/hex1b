using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Responsive(r => [
        r.WhenMinWidth(80, r => 
            r.Text("Wide layout - Terminal width >= 80 columns")
        ),
        r.Otherwise(r => 
            r.Text("Narrow layout - Terminal width < 80 columns")
        )
    ]))
    .Build();

await terminal.RunAsync();
