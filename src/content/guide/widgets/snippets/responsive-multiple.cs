using Hex1b;

var app = new Hex1bApp(ctx =>
    ctx.Responsive(r => [
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
                h.Text("Single Column"),
                h.Text("Detailed view")
            ])
        ),
        
        // Narrow: Minimal display
        r.Otherwise(r =>
            r.Text("Compact")
        )
    ])
);

await app.RunAsync();
