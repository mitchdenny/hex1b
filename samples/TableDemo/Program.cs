using System.Collections.ObjectModel;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

// ============================================================================
// TableWidget Demo - Scenario Picker
// ============================================================================

// Available scenarios
var scenarios = new[]
{
    "Products (Static)",
    "Observable Collection",
    "Observable + Selection",
    "Large List (10k)",
};

int selectedScenario = 0;

// ============================================================================
// Scenario 1: Products (Static) - Current demo with static list
// ============================================================================

const int MaxStock = 500;
var categories = new[] { "Electronics", "Furniture", "Accessories" };

var staticProducts = new List<Product>
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

object? staticFocusedKey = staticProducts[0].Name;

// ============================================================================
// Scenario 2: Observable Collection - Auto-refresh on changes
// ============================================================================

var observableProducts = new ObservableCollection<Product>
{
    new("Widget A", "Electronics", 10.00m, 100),
    new("Widget B", "Furniture", 20.00m, 50),
    new("Widget C", "Accessories", 5.00m, 200),
};

object? observableFocusedKey = observableProducts[0].Name;
int nextProductId = 4;

// ============================================================================
// Scenario 3: Observable + Selection - Selectable observable collection
// ============================================================================

var selectableObservable = new ObservableCollection<Product>
{
    new("Item Alpha", "Electronics", 25.00m, 80),
    new("Item Beta", "Furniture", 50.00m, 40),
    new("Item Gamma", "Accessories", 12.50m, 150),
};

object? selectableFocusedKey = selectableObservable[0].Name;
int nextSelectableId = 4;

// ============================================================================
// Scenario 4: Large List - 10k items for virtualization testing
// ============================================================================

var largeList = Enumerable.Range(1, 10000)
    .Select(i => new Product($"Product {i:D5}", categories[i % 3], 10.00m + (i % 100), i * 10))
    .ToList();

object? largeFocusedKey = largeList[0].Name;

// ============================================================================
// Build scenario content
// ============================================================================

Hex1bWidget BuildScenarioContent<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return selectedScenario switch
    {
        0 => BuildStaticScenario(ctx),
        1 => BuildObservableScenario(ctx),
        2 => BuildSelectableObservableScenario(ctx),
        3 => BuildLargeListScenario(ctx),
        _ => ctx.Text("Unknown scenario")
    };
}

Hex1bWidget BuildStaticScenario<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text("Static Product List"),
        v.Text("───────────────────"),
        v.Text(""),
        v.Table((IReadOnlyList<Product>)staticProducts)
            .WithRowKey(p => p.Name)
            .WithHeader(h => [
                h.Cell("Product").Width(SizeHint.Fill),
                h.Cell("Category").Width(SizeHint.Content),
                h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right),
                h.Cell("Stock").Width(SizeHint.Fixed(6)).Align(Alignment.Right)
            ])
            .WithRow((r, product, state) => [
                r.Cell(product.Name),
                r.Cell(product.Category),
                r.Cell($"${product.Price:F2}"),
                r.Cell(product.Stock.ToString())
            ])
            .WithFocus(staticFocusedKey)
            .OnFocusChanged(key => staticFocusedKey = key)
            .WithSelectionColumn(
                isSelected: p => p.IsSelected,
                onChanged: (p, selected) => p.IsSelected = selected
            )
            .OnSelectAll(() => { foreach (var p in staticProducts) p.IsSelected = true; })
            .OnDeselectAll(() => { foreach (var p in staticProducts) p.IsSelected = false; })
            .FillHeight(),
        v.Text(""),
        v.Text($"Items: {staticProducts.Count}  |  Selected: {staticProducts.Count(p => p.IsSelected)}")
    ]);
}

Hex1bWidget BuildObservableScenario<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text("Observable Collection (Auto-refresh)"),
        v.Text("─────────────────────────────────────"),
        v.Text(""),
        v.Table((IReadOnlyList<Product>)observableProducts)
            .WithRowKey(p => p.Name)
            .WithHeader(h => [
                h.Cell("Product").Width(SizeHint.Fill),
                h.Cell("Category").Width(SizeHint.Content),
                h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right),
                h.Cell("Stock").Width(SizeHint.Fixed(6)).Align(Alignment.Right)
            ])
            .WithRow((r, product, state) => [
                r.Cell(product.Name),
                r.Cell(product.Category),
                r.Cell($"${product.Price:F2}"),
                r.Cell(product.Stock.ToString())
            ])
            .WithFocus(observableFocusedKey)
            .OnFocusChanged(key => observableFocusedKey = key)
            .FillHeight(),
        v.Text(""),
        v.Text($"Items: {observableProducts.Count}"),
        v.Text(""),
        v.HStack(h => [
            h.Button("[A] Add Item").OnClick(_ => {
                observableProducts.Add(new Product($"Widget {nextProductId++}", "Electronics", 15.00m, 75));
            }),
            h.Text(" "),
            h.Button("[R] Remove Last").OnClick(_ => {
                if (observableProducts.Count > 0)
                    observableProducts.RemoveAt(observableProducts.Count - 1);
            }),
            h.Text(" "),
            h.Button("[C] Clear All").OnClick(_ => {
                observableProducts.Clear();
            })
        ])
    ]);
}

Hex1bWidget BuildSelectableObservableScenario<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text("Observable + Selection"),
        v.Text("──────────────────────"),
        v.Text(""),
        v.Table((IReadOnlyList<Product>)selectableObservable)
            .WithRowKey(p => p.Name)
            .WithHeader(h => [
                h.Cell("Product").Width(SizeHint.Fill),
                h.Cell("Category").Width(SizeHint.Content),
                h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right),
                h.Cell("Stock").Width(SizeHint.Fixed(6)).Align(Alignment.Right)
            ])
            .WithRow((r, product, state) => [
                r.Cell(product.Name),
                r.Cell(product.Category),
                r.Cell($"${product.Price:F2}"),
                r.Cell(product.Stock.ToString())
            ])
            .WithFocus(selectableFocusedKey)
            .OnFocusChanged(key => selectableFocusedKey = key)
            .WithSelectionColumn(
                isSelected: p => p.IsSelected,
                onChanged: (p, selected) => p.IsSelected = selected
            )
            .OnSelectAll(() => { foreach (var p in selectableObservable) p.IsSelected = true; })
            .OnDeselectAll(() => { foreach (var p in selectableObservable) p.IsSelected = false; })
            .FillHeight(),
        v.Text(""),
        v.Text($"Items: {selectableObservable.Count}  |  Selected: {selectableObservable.Count(p => p.IsSelected)}"),
        v.Text(""),
        v.HStack(h => [
            h.Button("[A] Add Item").OnClick(_ => {
                selectableObservable.Add(new Product($"Item {nextSelectableId++}", "Electronics", 15.00m, 75));
            }),
            h.Text(" "),
            h.Button("[R] Remove Last").OnClick(_ => {
                if (selectableObservable.Count > 0)
                    selectableObservable.RemoveAt(selectableObservable.Count - 1);
            }),
            h.Text(" "),
            h.Button("[C] Clear All").OnClick(_ => {
                selectableObservable.Clear();
            })
        ])
    ]);
}

Hex1bWidget BuildLargeListScenario<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text("Large List (10,000 items - Virtualized)"),
        v.Text("───────────────────────────────────────"),
        v.Text(""),
        v.Table((IReadOnlyList<Product>)largeList)
            .WithRowKey(p => p.Name)
            .WithHeader(h => [
                h.Cell("Product").Width(SizeHint.Fill),
                h.Cell("Category").Width(SizeHint.Content),
                h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right),
                h.Cell("Stock").Width(SizeHint.Fixed(8)).Align(Alignment.Right)
            ])
            .WithRow((r, product, state) => [
                r.Cell(product.Name),
                r.Cell(product.Category),
                r.Cell($"${product.Price:F2}"),
                r.Cell(product.Stock.ToString())
            ])
            .WithFocus(largeFocusedKey)
            .OnFocusChanged(key => largeFocusedKey = key)
            .FillHeight(),
        v.Text(""),
        v.Text($"Total items: {largeList.Count:N0}")
    ]);
}

// ============================================================================
// Main app with splitter layout
// ============================================================================

var app = new Hex1bApp(ctx =>
{
    return new SplitterWidget(
        // Left pane: Scenario picker
        ctx.VStack(v => [
            v.Text("┌─────────────────┐"),
            v.Text("│    Scenarios    │"),
            v.Text("└─────────────────┘"),
            v.Text(""),
            v.List(scenarios)
                .OnSelectionChanged(e => selectedScenario = e.SelectedIndex)
                .OnItemActivated(e => selectedScenario = e.ActivatedIndex)
                .FillHeight(),
            v.Text(""),
            v.Text("Press Ctrl+C to quit")
        ]),
        // Right pane: Active scenario
        ctx.VStack(v => [
            BuildScenarioContent(v).FillHeight()
        ]),
        firstSize: 22
    );
}, new Hex1bAppOptions { EnableMouse = true });

await app.RunAsync();

// ============================================================================
// Data model
// ============================================================================

class Product(string name, string category, decimal price, int stock)
{
    public string Name { get; set; } = name;
    public string Category { get; set; } = category;
    public decimal Price { get; set; } = price;
    public int Stock { get; set; } = stock;
    public bool IsSelected { get; set; }
}

