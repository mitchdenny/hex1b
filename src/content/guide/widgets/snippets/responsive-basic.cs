using Hex1b;

var app = new Hex1bApp(ctx =>
    ctx.Responsive(r => [
        r.WhenMinWidth(80, r => 
            r.Text("Wide layout - Terminal width >= 80 columns")
        ),
        r.Otherwise(r => 
            r.Text("Narrow layout - Terminal width < 80 columns")
        )
    ])
);

await app.RunAsync();
