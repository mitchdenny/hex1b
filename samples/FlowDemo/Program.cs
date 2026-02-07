using Hex1b;
using Hex1b.Flow;
using Hex1b.Layout;
using Hex1b.Widgets;

// Hex1b Flow Demo
// Demonstrates the normal-buffer TUI model with inline slices and full-screen transitions.

Console.WriteLine("=== Hex1b Flow Demo ===");
Console.WriteLine("This demo shows inline micro-TUI slices and a full-screen TUI transition.");
Console.WriteLine();

// Capture cursor position before entering the terminal (raw mode)
var cursorRow = Console.GetCursorPosition().Top;

// Sample data for the table
var inventory = new List<InventoryItem>
{
    new("Laptop Pro 16\"", "Electronics", 1999.99m, 12, "USA"),
    new("Wireless Mouse", "Electronics", 29.99m, 150, "China"),
    new("Standing Desk", "Furniture", 549.00m, 23, "Sweden"),
    new("Monitor 27\" 4K", "Electronics", 449.99m, 45, "South Korea"),
    new("Keyboard Mechanical", "Electronics", 129.99m, 88, "Japan"),
    new("Office Chair Ergonomic", "Furniture", 399.00m, 17, "Germany"),
    new("USB-C Hub 7-in-1", "Electronics", 49.99m, 200, "China"),
    new("Webcam HD 1080p", "Electronics", 79.99m, 62, "Taiwan"),
    new("Desk Lamp LED", "Furniture", 34.99m, 95, "China"),
    new("Cable Management Kit", "Accessories", 19.99m, 300, "USA"),
    new("Headphones Noise Cancel", "Electronics", 299.99m, 33, "Japan"),
    new("Footrest Adjustable", "Furniture", 44.99m, 55, "Germany"),
    new("Screen Protector 27\"", "Accessories", 24.99m, 120, "China"),
    new("Docking Station", "Electronics", 189.99m, 28, "Taiwan"),
    new("Wrist Rest Gel", "Accessories", 14.99m, 180, "USA"),
    new("Whiteboard 48x36", "Furniture", 89.99m, 40, "China"),
    new("Power Strip Smart", "Electronics", 39.99m, 75, "China"),
    new("Monitor Arm Dual", "Furniture", 119.99m, 35, "Taiwan"),
    new("Portable SSD 1TB", "Electronics", 109.99m, 60, "South Korea"),
    new("Pen Holder Bamboo", "Accessories", 12.99m, 250, "China"),
};

await Hex1bTerminal.CreateBuilder()
    .WithHex1bFlow(async flow =>
    {
        // Step 1: Inline slice — pick a color
        var choice = "";

        await flow.SliceAsync(
            builder: ctx => ctx.VStack(v =>
            [
                v.Text("Pick your favorite color:"),
                v.Button("Red").OnClick(e => { choice = "Red"; e.Context.RequestStop(); }),
                v.Button("Green").OnClick(e => { choice = "Green"; e.Context.RequestStop(); }),
                v.Button("Blue").OnClick(e => { choice = "Blue"; e.Context.RequestStop(); }),
            ]),
            @yield: ctx => ctx.Text($"✓ You picked: {choice}")
        );

        // Step 2: Inline slice — scrollable table with selection
        var selectedItems = new HashSet<string>();
        InventoryItem? activatedItem = null;

        await flow.SliceAsync(
            builder: ctx => ctx.VStack(v =>
            [
                v.Text($"Select items to order (your color preference: {choice}):"),
                v.Table(inventory)
                    .RowKey(item => item.Name)
                    .Header(h =>
                    [
                        h.Cell("Product").Fill(),
                        h.Cell("Category").Fixed(14),
                        h.Cell("Price").Fixed(12).AlignRight(),
                        h.Cell("Stock").Fixed(7).AlignRight(),
                        h.Cell("Origin").Fixed(14),
                    ])
                    .Row((r, item, state) =>
                    [
                        r.Cell(item.Name),
                        r.Cell(item.Category),
                        r.Cell($"${item.Price:F2}"),
                        r.Cell(item.Stock.ToString()),
                        r.Cell(item.Origin),
                    ])
                    .SelectionColumn(
                        isSelected: item => selectedItems.Contains(item.Name),
                        onChanged: (item, selected) =>
                        {
                            if (selected) selectedItems.Add(item.Name);
                            else selectedItems.Remove(item.Name);
                        })
                    .OnRowActivated((key, item) =>
                    {
                        activatedItem = item;
                    })
                    .Compact()
                    .FixedHeight(10),
                v.Text($"Selected: {selectedItems.Count} items"),
                v.Button("Continue with selection").OnClick(e => e.Context.RequestStop()),
            ]),
            @yield: ctx => ctx.Text($"✓ Selected {selectedItems.Count} items: {string.Join(", ", selectedItems)}")
        );

        // Step 3: Inline slice — spinner simulating processing
        var processing = true;
        var processed = 0;
        var totalItems = Math.Max(selectedItems.Count, 1);

        await flow.SliceAsync(
            configure: app =>
            {
                // Start background work
                _ = Task.Run(async () =>
                {
                    for (int i = 0; i < totalItems; i++)
                    {
                        await Task.Delay(5000 / totalItems);
                        processed = i + 1;
                        app.Invalidate();
                    }
                    processing = false;
                    app.Invalidate();
                    await Task.Delay(200);
                    app.RequestStop();
                });

                return ctx => ctx.VStack(v =>
                [
                    v.HStack(h =>
                    [
                        processing ? h.Spinner(SpinnerStyle.Dots) : h.Text("✓"),
                        h.Text(processing
                            ? $" Processing {processed}/{totalItems} items..."
                            : $" Processed {totalItems} items!"),
                    ]),
                ]);
            },
            @yield: ctx => ctx.Text($"✓ Processed {totalItems} items successfully")
        );

        // Step 4: Full-screen TUI — order summary (enters alt-buffer)
        var fullScreenResult = "";

        await flow.FullScreenAsync((app, options) =>
        {
            return ctx => ctx.VStack(v =>
            [
                v.Text("=== Order Summary ===").FillWidth(),
                v.Separator(),
                v.Text($"Color preference: {choice}"),
                v.Text($"Items ordered: {selectedItems.Count}"),
                v.Text(""),
                .. selectedItems.Select(name =>
                {
                    var item = inventory.First(i => i.Name == name);
                    return (Hex1bWidget)v.Text($"  • {item.Name} — ${item.Price:F2}");
                }),
                v.Text(""),
                v.Text($"Total: ${inventory.Where(i => selectedItems.Contains(i.Name)).Sum(i => i.Price):F2}"),
                v.Text(""),
                v.Text("This is a full-screen TUI running in the alternate buffer."),
                v.Text(""),
                v.HStack(h =>
                [
                    h.Button("Place Order").OnClick(e =>
                    {
                        fullScreenResult = "Order placed!";
                        e.Context.RequestStop();
                    }),
                    h.Button("Cancel").OnClick(e =>
                    {
                        fullScreenResult = "Order cancelled";
                        e.Context.RequestStop();
                    }),
                ]),
            ]);
        });

        // Step 5: Inline slice — final result (back in normal buffer)
        var done = false;

        await flow.SliceAsync(
            builder: ctx => ctx.VStack(v =>
            [
                v.Text($"Result: {fullScreenResult}"),
                v.Button("Finish").OnClick(e => { done = true; e.Context.RequestStop(); }),
            ]),
            @yield: ctx => ctx.Text(done ? $"✓ {fullScreenResult}" : "✗ Aborted.")
        );
    }, options => options.InitialCursorRow = cursorRow)
    .Build()
    .RunAsync();

Console.WriteLine();
Console.WriteLine("Flow complete! Back to normal terminal.");

record InventoryItem(string Name, string Category, decimal Price, int Stock, string Origin);
