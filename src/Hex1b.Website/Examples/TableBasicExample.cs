using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Table Widget Documentation: Basic Usage
/// Demonstrates table creation with headers, rows, and column sizing.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/table.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TableBasicExample(ILogger<TableBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<TableBasicExample> _logger = logger;

    public override string Id => "table-basic";
    public override string Title => "Table Widget - Basic Usage";
    public override string Description => "Demonstrates table creation with headers, rows, and column sizing";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating table basic example widget builder");

        var products = new List<Product>
        {
            new("Widget Pro", "Electronics", 299.99m, 42),
            new("Gadget X", "Electronics", 149.50m, 128),
            new("Tool Kit", "Hardware", 89.00m, 56),
            new("Cable Pack", "Accessories", 24.99m, 200),
            new("Power Bank", "Electronics", 79.99m, 85)
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
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
            ], title: "Product Inventory");
        };
    }

    private record Product(string Name, string Category, decimal Price, int Stock);
}
