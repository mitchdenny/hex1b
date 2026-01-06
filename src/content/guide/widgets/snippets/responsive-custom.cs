using Hex1b;

var app = new Hex1bApp(ctx =>
    ctx.Responsive(r => [
        // Custom condition: Both width and height
        r.When(
            (width, height) => width >= 100 && height >= 20,
            r => r.Border(b => [
                b.Text("Large Terminal"),
                b.Text($"Plenty of space for content")
            ], title: "Full View")
        ),
        
        // Width-only condition
        r.WhenWidth(
            width => width >= 60,
            r => r.Text("Medium width terminal")
        ),
        
        // Fallback
        r.Otherwise(r => r.Text("Small terminal"))
    ])
);

await app.RunAsync();
