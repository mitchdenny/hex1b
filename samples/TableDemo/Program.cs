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
};

int selectedIndex = 0;
bool isLoading = false;
bool isEmpty = false;
bool shouldQuit = false;

// Create the app
var app = new Hex1bApp(ctx =>
{
    if (shouldQuit) return ctx.Text("Goodbye!");
    
    var displayData = isEmpty ? [] : (isLoading ? null : products);
    
    return ctx.VStack(v => [
        v.Text("╔══════════════════════════════════════╗"),
        v.Text("║       TableWidget Demo - Phase 1     ║"),
        v.Text("╚══════════════════════════════════════╝"),
        v.Text(""),
        
        // The table
        v.Table(displayData)
            .WithHeader(h => [
                h.Cell("Product"),
                h.Cell("Category"),
                h.Cell("Price"),
                h.Cell("Stock")
            ])
            .WithRow((r, product) => [
                r.Cell(product.Name),
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
            .WithSelection(selectedIndex)
            .OnSelectionChanged(idx => selectedIndex = idx),
        
        v.Text(""),
        v.Text($"Selected: {(products.Count > 0 && selectedIndex < products.Count ? products[selectedIndex].Name : "None")}"),
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
