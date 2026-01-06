using Hex1b;

var app = new Hex1bApp(ctx =>
    ctx.Responsive(r => [
        // Wide layout: Horizontal split
        r.WhenMinWidth(100, r =>
            r.HStack(h => [
                h.Border(b => [
                    b.Text("Main Content"),
                    b.Text(""),
                    b.Text("This appears on the left"),
                    b.Text("when there's enough width.")
                ], title: "Content").FillWidth(2),
                
                h.Border(b => [
                    b.Text("Sidebar"),
                    b.Text(""),
                    b.Text("Additional info")
                ], title: "Info").FillWidth(1)
            ])
        ),
        
        // Narrow layout: Vertical stack
        r.Otherwise(r =>
            r.VStack(v => [
                v.Border(b => [
                    b.Text("Main Content"),
                    b.Text("Stacked vertically")
                ], title: "Content"),
                
                v.Border(b => [
                    b.Text("Sidebar below")
                ], title: "Info")
            ])
        )
    ])
);

await app.RunAsync();
