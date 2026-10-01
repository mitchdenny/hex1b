using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

// Sample product data
var products = new List<Product>
{
    new("Widget Pro", "Electronics", 299.99m, 42),
    new("Gadget X", "Electronics", 149.50m, 128),
    new("Tool Kit", "Hardware", 89.00m, 56),
    new("Cable Pack", "Accessories", 24.99m, 200),
    new("Power Bank", "Electronics", 79.99m, 85)
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.Table(products)
            .Header(h => [
                h.Cell("Product").Width(SizeHint.Fill),
                h.Cell("Category").Width(SizeHint.Content),
                h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right),
                h.Cell("Stock").Width(SizeHint.Fixed(8)).Align(Alignment.Right)
            ])
            .Row((r, product, state) => [
                r.Cell(product.Name),
                r.Cell(product.Category),
                r.Cell($"${product.Price:F2}"),
                r.Cell(product.Stock.ToString())
            ])
    ]).Title("Product Inventory"))
    .Build();

await terminal.RunAsync();

record Product(string Name, string Category, decimal Price, int Stock);
