using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Data;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

// ============================================================================
// TableWidget Demo - Scenario Picker
// ============================================================================

// Screenshot state
string? lastScreenshotPath = null;
Hex1bTerminal? terminalRef = null;

// Screenshot helper
async Task TakeScreenshot()
{
    if (terminalRef == null) return;
    
    var snapshot = terminalRef.CreateSnapshot();
    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var tempPath = Path.GetTempPath();
    
    var svgPath = Path.Combine(tempPath, $"hex1b_screenshot_{timestamp}.svg");
    var ansiPath = Path.Combine(tempPath, $"hex1b_screenshot_{timestamp}.ansi");
    
    await File.WriteAllTextAsync(svgPath, snapshot.ToSvg());
    await File.WriteAllTextAsync(ansiPath, snapshot.ToAnsi());
    
    lastScreenshotPath = svgPath;
}

// Available scenarios
var scenarios = new[]
{
    "Products (Static)",
    "Observable Collection",
    "Observable + Selection",
    "Large List (10k)",
    "Async (5k, 50ms)",
    "Async (1k, 0ms)",
    "Async (10k, 0ms)",
    "Async (100k, 0ms)",
    "Themed Table",
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
// Scenario 5+: Async Data Sources - Various configurations
// ============================================================================

var asyncDataSource5k50ms = new SimulatedAsyncDataSource(5000, 50); // 5000 items, 50ms delay
var asyncDataSource1k0ms = new SimulatedAsyncDataSource(1000, 0);   // 1000 items, no delay
var asyncDataSource10k0ms = new SimulatedAsyncDataSource(10000, 0); // 10000 items, no delay
var asyncDataSource100k0ms = new SimulatedAsyncDataSource(100000, 0); // 100000 items, no delay

object? asyncFocusedKey5k50ms = null;
object? asyncFocusedKey1k0ms = null;
object? asyncFocusedKey10k0ms = null;
object? asyncFocusedKey100k0ms = null;

// ============================================================================
// Scenario 9: Themed Table - Custom table styling with ThemePanel
// ============================================================================

var themedProducts = new List<Product>
{
    new("Neon Keyboard", "Electronics", 199.99m, 25),
    new("RGB Mouse", "Electronics", 79.99m, 50),
    new("Gaming Chair", "Furniture", 449.99m, 10),
    new("LED Strip", "Accessories", 29.99m, 100),
    new("Holographic Display", "Electronics", 1299.99m, 5),
    new("Ambient Light Bar", "Accessories", 89.99m, 30),
};

object? themedFocusedKey = themedProducts[0].Name;

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
        4 => BuildAsyncScenario(ctx, asyncDataSource5k50ms, () => asyncFocusedKey5k50ms, key => asyncFocusedKey5k50ms = key, "5k items, 50ms delay"),
        5 => BuildAsyncScenario(ctx, asyncDataSource1k0ms, () => asyncFocusedKey1k0ms, key => asyncFocusedKey1k0ms = key, "1k items, no delay"),
        6 => BuildAsyncScenario(ctx, asyncDataSource10k0ms, () => asyncFocusedKey10k0ms, key => asyncFocusedKey10k0ms = key, "10k items, no delay"),
        7 => BuildAsyncScenario(ctx, asyncDataSource100k0ms, () => asyncFocusedKey100k0ms, key => asyncFocusedKey100k0ms = key, "100k items, no delay"),
        8 => BuildThemedScenario(ctx),
        _ => ctx.Text("Unknown scenario")
    };
}

Hex1bWidget BuildStaticScenario<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text("Static Product List"),
        v.Text("───────────────────"),
        v.Text(""),
        v.Responsive(r => [
            r.WhenMinWidth(90, r => BuildStaticTable(r).Full()),
            r.Otherwise(r => BuildStaticTable(r).Compact())
        ]).FillHeight(),
        v.Text(""),
        v.Text($"Items: {staticProducts.Count}  |  Selected: {staticProducts.Count(p => p.IsSelected)}")
    ]);
}

TableWidget<Product> BuildStaticTable<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.Table((IReadOnlyList<Product>)staticProducts)
        .RowKey(p => p.Name)
        .Header(h => [
            h.Cell("Product").Width(SizeHint.Fill),
            h.Cell("Category").Width(SizeHint.Content),
            h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right),
            h.Cell("Stock").Width(SizeHint.Fixed(6)).Align(Alignment.Right)
        ])
        .Row((r, product, state) => [
            r.Cell(product.Name),
            r.Cell(product.Category),
            r.Cell($"${product.Price:F2}"),
            r.Cell(product.Stock.ToString())
        ])
        .Focus(staticFocusedKey)
        .OnFocusChanged(key => staticFocusedKey = key)
        .SelectionColumn(
            isSelected: p => p.IsSelected,
            onChanged: (p, selected) => p.IsSelected = selected
        )
        .OnSelectAll(() => { foreach (var p in staticProducts) p.IsSelected = true; })
        .OnDeselectAll(() => { foreach (var p in staticProducts) p.IsSelected = false; })
        .FillHeight();
}

Hex1bWidget BuildObservableScenario<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text("Observable Collection (Auto-refresh)"),
        v.Text("─────────────────────────────────────"),
        v.Text(""),
        v.Table((IReadOnlyList<Product>)observableProducts)
            .RowKey(p => p.Name)
            .Header(h => [
                h.Cell("Product").Width(SizeHint.Fill),
                h.Cell("Category").Width(SizeHint.Content),
                h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right),
                h.Cell("Stock").Width(SizeHint.Fixed(6)).Align(Alignment.Right)
            ])
            .Row((r, product, state) => [
                r.Cell(product.Name),
                r.Cell(product.Category),
                r.Cell($"${product.Price:F2}"),
                r.Cell(product.Stock.ToString())
            ])
            .Focus(observableFocusedKey)
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
            .RowKey(p => p.Name)
            .Header(h => [
                h.Cell("Product").Width(SizeHint.Fill),
                h.Cell("Category").Width(SizeHint.Content),
                h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right),
                h.Cell("Stock").Width(SizeHint.Fixed(6)).Align(Alignment.Right)
            ])
            .Row((r, product, state) => [
                r.Cell(product.Name),
                r.Cell(product.Category),
                r.Cell($"${product.Price:F2}"),
                r.Cell(product.Stock.ToString())
            ])
            .Focus(selectableFocusedKey)
            .OnFocusChanged(key => selectableFocusedKey = key)
            .SelectionColumn(
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
        v.Responsive(r => [
            r.WhenMinWidth(90, r => BuildLargeTable(r).Full()),
            r.Otherwise(r => BuildLargeTable(r).Compact())
        ]).FillHeight(),
        v.Text(""),
        v.Text($"Total items: {largeList.Count:N0}")
    ]);
}

TableWidget<Product> BuildLargeTable<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.Table((IReadOnlyList<Product>)largeList)
        .RowKey(p => p.Name)
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
        .Focus(largeFocusedKey)
        .OnFocusChanged(key => largeFocusedKey = key)
        .FillHeight();
}

Hex1bWidget BuildAsyncScenario<TParent>(
    WidgetContext<TParent> ctx, 
    SimulatedAsyncDataSource dataSource,
    Func<object?> getFocusedKey,
    Action<object?> setFocusedKey,
    string description) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text($"Async Data Source ({description})"),
        v.Text("──────────────────────────────────────────"),
        v.Text(""),
        v.Table(dataSource)
            .RowKey(p => p.Name)
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
            .Focus(getFocusedKey())
            .OnFocusChanged(key => setFocusedKey(key))
            .FillHeight(),
        v.Text(""),
        v.Text($"Total items: {dataSource.TotalCount:N0} | Delay: {dataSource.DelayMs}ms")
    ]);
}

Hex1bWidget BuildThemedScenario<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    return ctx.VStack(v => [
        v.Text("Themed Table (Custom Styling via ThemePanel)"),
        v.Text("─────────────────────────────────────────────"),
        v.Text(""),
        v.ThemePanel(
            theme => theme
                // Cyberpunk-style borders - magenta/cyan color scheme
                .Set(TableTheme.BorderColor, Hex1bColor.Magenta)
                .Set(TableTheme.FocusedBorderColor, Hex1bColor.Cyan)
                .Set(TableTheme.TableFocusedBorderColor, Hex1bColor.Yellow)
                
                // Use double-line box drawing characters
                .Set(TableTheme.TopLeft, '╔')
                .Set(TableTheme.TopRight, '╗')
                .Set(TableTheme.BottomLeft, '╚')
                .Set(TableTheme.BottomRight, '╝')
                .Set(TableTheme.Horizontal, '═')
                .Set(TableTheme.Vertical, '║')
                .Set(TableTheme.TeeDown, '╦')
                .Set(TableTheme.TeeUp, '╩')
                .Set(TableTheme.TeeRight, '╠')
                .Set(TableTheme.TeeLeft, '╣')
                .Set(TableTheme.Cross, '╬')
                
                // Stylized scrollbar
                .Set(TableTheme.ScrollbarTrack, '░')
                .Set(TableTheme.ScrollbarThumb, '█')
                .Set(TableTheme.ScrollbarTrackColor, Hex1bColor.FromRgb(128, 0, 128))
                .Set(TableTheme.ScrollbarThumbColor, Hex1bColor.Cyan)
                
                // Selection styling
                .Set(TableTheme.CheckboxUnchecked, "○")
                .Set(TableTheme.CheckboxChecked, "●")
                .Set(TableTheme.CheckboxIndeterminate, "◐")
                .Set(TableTheme.CheckboxCheckedForeground, Hex1bColor.Cyan)
                .Set(TableTheme.CheckboxUncheckedForeground, Hex1bColor.FromRgb(128, 0, 128))
                
                // Row colors
                .Set(TableTheme.FocusedRowBackground, Hex1bColor.FromRgb(60, 20, 80))
                .Set(TableTheme.SelectedRowBackground, Hex1bColor.FromRgb(30, 60, 90)),
            v2 => [
                v2.Table((IReadOnlyList<Product>)themedProducts)
                    .RowKey(p => p.Name)
                    .Header(h => [
                        h.Cell("Product").Width(SizeHint.Fill),
                        h.Cell("Category").Width(SizeHint.Content),
                        h.Cell("Price").Width(SizeHint.Fixed(12)).Align(Alignment.Right),
                        h.Cell("Stock").Width(SizeHint.Fixed(6)).Align(Alignment.Right)
                    ])
                    .Row((r, product, state) => [
                        r.Cell(product.Name),
                        r.Cell(product.Category),
                        r.Cell($"${product.Price:F2}"),
                        r.Cell(product.Stock.ToString())
                    ])
                    .Focus(themedFocusedKey)
                    .OnFocusChanged(key => themedFocusedKey = key)
                    .SelectionColumn(
                        isSelected: p => p.IsSelected,
                        onChanged: (p, selected) => p.IsSelected = selected
                    )
                    .Full()
                    .FillHeight()
            ]
        ).FillHeight(),
        v.Text(""),
        v.Text("This table uses ThemePanel to customize borders, colors, and characters.")
    ]);
}

// ============================================================================
// Main app with splitter layout
// ============================================================================

using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            new SplitterWidget(
                // Left pane: Scenario picker
                v.VStack(v2 => [
                    v2.Text("┌─────────────────┐"),
                    v2.Text("│    Scenarios    │"),
                    v2.Text("└─────────────────┘"),
                    v2.Text(""),
                    v2.List(scenarios)
                        .OnSelectionChanged(e => selectedScenario = e.SelectedIndex)
                        .OnItemActivated(e => selectedScenario = e.ActivatedIndex)
                        .FillHeight(),
                    v2.Text(""),
                    v2.Button("📷 Screenshot")
                        .OnClick(async _ => await TakeScreenshot()),
                    v2.Text(lastScreenshotPath != null ? $"Saved: {Path.GetFileName(lastScreenshotPath)}" : ""),
                    v2.Text(""),
                    v2.Text("Press Ctrl+C to quit")
                ]),
                // Right pane: Active scenario
                v.VStack(v2 => [
                    BuildScenarioContent(v2).FillHeight()
                ]),
                firstSize: 22
            ).FillHeight()
        ]);
    })
    .Build();

// Capture terminal reference for screenshots
terminalRef = terminal;

await terminal.RunAsync();

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

// ============================================================================
// Simulated Async Data Source
// ============================================================================

/// <summary>
/// Simulates an async API data source with configurable delay.
/// </summary>
class SimulatedAsyncDataSource : ITableDataSource<Product>
{
    private readonly List<Product> _allData;
    private readonly int _delayMs;
    private readonly string[] _categories = ["Electronics", "Furniture", "Accessories"];
    
    public int TotalCount => _allData.Count;
    public int DelayMs => _delayMs;
    
    public SimulatedAsyncDataSource(int itemCount, int delayMs = 50)
    {
        _delayMs = delayMs;
        _allData = Enumerable.Range(1, itemCount)
            .Select(i => new Product(
                $"API Item {i:D5}", 
                _categories[i % 3], 
                10.00m + (i % 100), 
                i * 5))
            .ToList();
    }
    
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    
    public async ValueTask<int> GetItemCountAsync(CancellationToken cancellationToken = default)
    {
        if (_delayMs > 0)
            await Task.Delay(_delayMs, cancellationToken);
        return _allData.Count;
    }
    
    public async ValueTask<IReadOnlyList<Product>> GetItemsAsync(
        int startIndex, 
        int count, 
        CancellationToken cancellationToken = default)
    {
        if (_delayMs > 0)
            await Task.Delay(_delayMs, cancellationToken);
        
        if (startIndex >= _allData.Count)
            return Array.Empty<Product>();
        
        var actualCount = Math.Min(count, _allData.Count - startIndex);
        return _allData.Skip(startIndex).Take(actualCount).ToList();
    }
}

