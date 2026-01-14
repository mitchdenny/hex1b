using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Responsive(r => [
        // Extra wide: Three columns
        r.WhenMinWidth(120, r =>
            r.HStack(h => [
                h.Text("Column 1").FillWidth(),
                h.Text(" | "),
                h.Text("Column 2").FillWidth(),
                h.Text(" | "),
                h.Text("Column 3").FillWidth()
            ])
        ),
        
        // Wide: Two columns
        r.WhenMinWidth(80, r =>
            r.HStack(h => [
                h.Text("Column 1").FillWidth(),
                h.Text(" | "),
                h.Text("Column 2").FillWidth()
            ])
        ),
        
        // Medium: Single column with details
        r.WhenMinWidth(40, r =>
            r.VStack(v => [
                v.Text("Single Column"),
                v.Text("Detailed view")
            ])
        ),
        
        // Narrow: Minimal display
        r.Otherwise(r =>
            r.Text("Compact")
        )
    ]))
    .Build();

await terminal.RunAsync();
