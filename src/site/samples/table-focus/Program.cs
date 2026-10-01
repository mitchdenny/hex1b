using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

object? focusedKey = null;
Product? focusedProduct = null;

var products = new List<Product>
{
    new("Widget Pro", "High-end widget with premium features", 299.99m),
    new("Gadget X", "Compact gadget for everyday use", 149.50m),
    new("Tool Kit", "Complete toolkit for professionals", 89.00m),
    new("Cable Pack", "Assorted cables and adapters", 24.99m),
    new("Power Bank", "Portable 20000mAh battery pack", 79.99m)
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Border(b => [
            b.Table(products)
                .RowKey(p => p.Name)
                .Header(h => [
                    h.Cell("Product").Width(SizeHint.Fill),
                    h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right)
                ])
                .Row((r, product, state) => [
                    r.Cell(product.Name),
                    r.Cell($"${product.Price:F2}")
                ])
                .Focus(focusedKey)
                .OnFocusChanged(key =>
                {
                    focusedKey = key;
                    focusedProduct = products.FirstOrDefault(p => p.Name.Equals(key));
                })
        ]).Title("Products"),
        v.Text(""),
        v.Border(b => [
            b.Text(focusedProduct?.Description ?? "Select a product to see details")
        ]).Title("Details")
    ]))
    .Build();

await terminal.RunAsync();

record Product(string Name, string Description, decimal Price);
