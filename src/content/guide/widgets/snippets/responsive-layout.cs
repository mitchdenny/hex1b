using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Responsive(r => [
        // Wide layout: Horizontal split
        r.WhenMinWidth(100, r =>
            r.HStack(h => [
                h.Border(b => [
                    b.Text("Main Content"),
                    b.Text(""),
                    b.Text("This appears on the left"),
                    b.Text("when there's enough width.")
                ]).Title("Content").FillWidth(2),
                
                h.Border(b => [
                    b.Text("Sidebar"),
                    b.Text(""),
                    b.Text("Additional info")
                ]).Title("Info").FillWidth(1)
            ])
        ),
        
        // Narrow layout: Vertical stack
        r.Otherwise(r =>
            r.VStack(v => [
                v.Border(b => [
                    b.Text("Main Content"),
                    b.Text("Stacked vertically")
                ]).Title("Content"),
                
                v.Border(b => [
                    b.Text("Sidebar below")
                ]).Title("Info")
            ])
        )
    ]))
    .Build();

await terminal.RunAsync();
