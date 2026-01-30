using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Table Widget Documentation: Focus Tracking
/// Demonstrates row key, focus state, and focus changed events.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the focusCode sample in:
/// src/content/guide/widgets/table.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TableFocusExample(ILogger<TableFocusExample> logger) : Hex1bExample
{
    private readonly ILogger<TableFocusExample> _logger = logger;

    public override string Id => "table-focus";
    public override string Title => "Table Widget - Focus Tracking";
    public override string Description => "Demonstrates row key, focus state, and focus changed events";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating table focus example widget builder");

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

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
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
                ], title: "Products"),
                v.Text(""),
                v.Border(b => [
                    b.Text(focusedProduct?.Description ?? "Select a product to see details")
                ], title: "Details")
            ]);
        };
    }

    private record Product(string Name, string Description, decimal Price);
}
