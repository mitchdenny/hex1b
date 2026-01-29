using Hex1b;
using Hex1b.Widgets;

var products = new List<Product>
{
    new("Laptop", "Electronics", 999.99m, 15),
    new("Mechanical Keyboard", "Electronics", 149.99m, 50),
    new("Wireless Mouse", "Electronics", 49.99m, 100),
    new("Desk Chair", "Furniture", 299.99m, 8),
    new("Standing Desk", "Furniture", 549.99m, 12),
    new("Monitor 27\"", "Electronics", 349.99m, 25),
    new("USB-C Hub", "Accessories", 79.99m, 75),
    new("Webcam HD", "Electronics", 89.99m, 40),
    new("Desk Lamp", "Furniture", 45.99m, 60),
    new("Cable Management Kit", "Accessories", 24.99m, 200),
    new("Ergonomic Mouse Pad", "Accessories", 29.99m, 150),
    new("Blue Light Glasses", "Accessories", 34.99m, 80),
    new("Laptop Stand", "Furniture", 79.99m, 45),
    new("Wireless Charger", "Electronics", 39.99m, 120),
    new("Noise Cancelling Headphones", "Electronics", 249.99m, 30),
    new("Mechanical Numpad", "Electronics", 59.99m, 35),
    new("Monitor Light Bar", "Electronics", 69.99m, 55),
    new("Wrist Rest", "Accessories", 19.99m, 200),
    new("Cable Clips Pack", "Accessories", 9.99m, 500),
    new("Portable SSD 1TB", "Electronics", 119.99m, 65),
};

object? focusedKey = 0;  // Key-based focus (using row index as key by default)
bool isLoading = false;
bool isEmpty = false;
bool shouldQuit = false;

// Create the app
var app = new Hex1bApp(ctx =>
{
    if (shouldQuit) return ctx.Text("Goodbye!");
    
    var displayData = isEmpty ? [] : (isLoading ? null : products);
    
    return ctx.VStack(v => [
        v.Text("╔════════════════════════════════════════════════════════════╗"),
        v.Text("║  TableWidget Demo - Phase 3: Scrolling with Mouse Support  ║"),
        v.Text("╚════════════════════════════════════════════════════════════╝"),
        v.Text(""),
        v.Text("Use Up/Down arrows, Page Up/Down, Home/End, or mouse wheel to scroll."),
        v.Text("Click and drag the scrollbar thumb. Click arrows or track to scroll."),
        v.Text(""),
        
        // The table (will scroll when there are more rows than fit)
        // FillHeight() is critical for scrolling - gives the table a constrained height
        v.Table(displayData)
            .WithHeader(h => [
                h.Cell("Product"),
                h.Cell("Category"),
                h.Cell("Price"),
                h.Cell("Stock")
            ])
            .WithRow((r, product, state) => [
                r.Cell(state.IsFocused ? $"> {product.Name}" : product.Name),
                r.Cell(product.Category),
                r.Cell($"${product.Price:F2}"),
                r.Cell(product.Stock.ToString())
            ])
            .WithFooter(f => [
                f.Cell("Total Products"),
                f.Cell(products.Count.ToString()),
                f.Cell($"${products.Sum(p => p.Price):F2}"),
                f.Cell(products.Sum(p => p.Stock).ToString())
            ])
            .WithLoading((l, idx) => [
                l.Cell("████████████"),
                l.Cell("████████"),
                l.Cell("██████"),
                l.Cell("████")
            ], rowCount: 5)
            .WithFocus(focusedKey)
            .OnFocusChanged(key => focusedKey = key)
            .FillHeight(),
        
        v.Text(""),
        v.Text($"Focused Row: {(focusedKey != null && focusedKey is int idx && idx < products.Count ? products[idx].Name : "None")}"),
        v.Text("Press Tab to focus table, then use Up/Down arrows to scroll."),
        v.Text(""),
        v.HStack(h => [
            h.Button("[L] Toggle Loading").OnClick(_ => { isLoading = !isLoading; }),
            h.Text(" "),
            h.Button("[E] Toggle Empty").OnClick(_ => { isEmpty = !isEmpty; })
        ]),
        v.Text(""),
        v.Text("Press Ctrl+C to quit")
    ]);
});

// Run the app
await app.RunAsync();

// Sample data model - must be after top-level statements
record Product(string Name, string Category, decimal Price, int Stock);
