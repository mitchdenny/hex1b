using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

// Max stock level for progress bar scaling
const int MaxStock = 500;

// Available categories for the picker
var categories = new[] { "Electronics", "Furniture", "Accessories" };

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

// Build the product table with the specified render mode
TableWidget<Product> BuildProductTable<TParent>(
    WidgetContext<TParent> ctx, 
    IReadOnlyList<Product>? data, 
    TableRenderMode renderMode) where TParent : Hex1bWidget
{
    var table = ctx.Table(data)
        .WithHeader(h => [
            h.Cell("Product").Width(SizeHint.Fill),
            h.Cell("Category").Width(SizeHint.Content),
            h.Cell("Price").Width(SizeHint.Fixed(12)).Align(Alignment.Right),
            h.Cell("Stock").Width(SizeHint.Fixed(6)).Align(Alignment.Right),
            h.Cell("Level").Width(SizeHint.Fixed(12))
        ])
        .WithRow((r, product, state) => [
            r.Cell(product.Name),
            r.Cell(c => c.Picker(categories, Array.IndexOf(categories, product.Category))
                .OnSelectionChanged(e => product.Category = e.SelectedText ?? product.Category)),
            r.Cell($"${product.Price:F2}"),
            r.Cell(product.Stock.ToString()),
            r.Cell(c => c.Progress(product.Stock, 0, MaxStock))
        ])
        .WithFooter(f => [
            f.Cell("Total Products"),
            f.Cell(products.Count.ToString()),
            f.Cell($"${products.Sum(p => p.Price):F2}"),
            f.Cell(products.Sum(p => p.Stock).ToString()),
            f.Cell("")
        ])
        .WithLoading((l, idx) => [
            l.Cell("████████████"),
            l.Cell("████████"),
            l.Cell("██████"),
            l.Cell("████"),
            l.Cell("████████")
        ], rowCount: 5)
        .WithFocus(focusedKey)
        .OnFocusChanged(key => focusedKey = key)
        .WithSelectionColumn();
    
    // Apply render mode
    return renderMode == TableRenderMode.Full ? table.Full() : table.Compact();
}

// Create the app
var app = new Hex1bApp(ctx =>
{
    var displayData = isEmpty ? [] : (isLoading ? null : products);
    
    return ctx.VStack(v => [
        v.Text("╔════════════════════════════════════════════════════════════╗"),
        v.Text("║  TableWidget Demo - Responsive Compact/Full Mode           ║"),
        v.Text("╚════════════════════════════════════════════════════════════╝"),
        v.Text(""),
        v.Text("Resize terminal: >= 100 cols = Full mode, < 100 cols = Compact mode"),
        v.Text(""),
        
        // Responsive table: Full mode when wide, Compact mode when narrow
        v.Responsive(r => [
            r.WhenMinWidth(100, c => BuildProductTable(c, displayData, TableRenderMode.Full).FillHeight()),
            r.Otherwise(c => BuildProductTable(c, displayData, TableRenderMode.Compact).FillHeight())
        ]).FillHeight(),
        
        v.Text(""),
        v.Text($"Focused Row: {(focusedKey != null && focusedKey is int idx && idx < products.Count ? products[idx].Name : "None")}"),
        v.Text(""),
        v.HStack(h => [
            h.Button("[L] Toggle Loading").OnClick(_ => { isLoading = !isLoading; }),
            h.Text(" "),
            h.Button("[E] Toggle Empty").OnClick(_ => { isEmpty = !isEmpty; })
        ]),
        v.Text(""),
        v.Text("Press Ctrl+C to quit")
    ]);
}, new Hex1b.Hex1bAppOptions { EnableMouse = true });

// Run the app
await app.RunAsync();

// Sample data model
class Product(string name, string category, decimal price, int stock)
{
    public string Name { get; set; } = name;
    public string Category { get; set; } = category;
    public decimal Price { get; set; } = price;
    public int Stock { get; set; } = stock;
}
