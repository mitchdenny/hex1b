<script setup>
const columnBasicCode = `using Hex1b;
using Hex1b.Charts;

var sales = new[]
{
    new ChartItem("Jan", 42),
    new ChartItem("Feb", 58),
    new ChartItem("Mar", 35),
    new ChartItem("Apr", 71),
    new ChartItem("May", 49),
    new ChartItem("Jun", 63),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ColumnChart(sales)
        .Title("Monthly Sales")
        .ShowValues()
    )
    .Build();

await terminal.RunAsync();`

const columnMultiSeriesCode = `using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;

var data = new[]
{
    new SalesRecord("Jan", 50, 30, 20),
    new SalesRecord("Feb", 65, 40, 25),
    new SalesRecord("Mar", 45, 35, 30),
    new SalesRecord("Apr", 70, 50, 35),
};

var blue = Hex1bColor.FromRgb(66, 133, 244);
var red = Hex1bColor.FromRgb(234, 67, 53);
var green = Hex1bColor.FromRgb(52, 168, 83);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ColumnChart(data)
        .Label(s =&gt; s.Month)
        .Series("Electronics", s =&gt; s.Electronics, blue)
        .Series("Clothing", s =&gt; s.Clothing, red)
        .Series("Food", s =&gt; s.Food, green)
        .Layout(ChartLayout.Stacked)
        .Title("Sales by Category")
        .ShowValues()
    )
    .Build();

await terminal.RunAsync();

record SalesRecord(string Month, double Electronics, double Clothing, double Food);`

const barBasicCode = `using Hex1b;
using Hex1b.Charts;

var sales = new[]
{
    new ChartItem("Jan", 42),
    new ChartItem("Feb", 58),
    new ChartItem("Mar", 35),
    new ChartItem("Apr", 71),
    new ChartItem("May", 49),
    new ChartItem("Jun", 63),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.BarChart(sales)
        .Title("Monthly Sales")
        .ShowValues()
    )
    .Build();

await terminal.RunAsync();`

const breakdownCode = `using Hex1b;
using Hex1b.Charts;

var diskUsage = new[]
{
    new ChartItem("Data", 42),
    new ChartItem("Packages", 18),
    new ChartItem("Temp", 9),
    new ChartItem("System", 15),
    new ChartItem("Other", 3),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.BreakdownChart(diskUsage)
        .Title("Disk Usage")
        .ShowPercentages()
        .ShowValues()
    )
    .Build();

await terminal.RunAsync();`

const genericBindingCode = `using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;

var metrics = new[]
{
    new ServerMetric("web-01", 78.5, 62.3),
    new ServerMetric("web-02", 45.1, 38.7),
    new ServerMetric("db-01", 92.0, 85.4),
    new ServerMetric("cache", 23.8, 51.2),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.BarChart(metrics)
        .Label(m =&gt; m.Host)
        .Series("CPU %", m =&gt; m.Cpu, Hex1bColor.FromRgb(234, 67, 53))
        .Series("Memory %", m =&gt; m.Memory, Hex1bColor.FromRgb(66, 133, 244))
        .Layout(ChartLayout.Grouped)
        .Title("Server Resources")
        .Range(0, 100)
    )
    .Build();

await terminal.RunAsync();

record ServerMetric(string Host, double Cpu, double Memory);`

</script>

# Charts

Visualize data with column charts, bar charts, and breakdown charts. All chart widgets use a generic data-binding pattern that works with any data type, with convenience overloads for ad-hoc data using `ChartItem`.

## Column Chart

Display vertical columns for comparing values across categories.

### Basic Usage

Use `ChartItem` for quick ad-hoc charts. The label and value selectors are pre-wired automatically:

<CodeBlock lang="csharp" :code="columnBasicCode" command="dotnet run" example="chart-column-basic" exampleTitle="Column Chart - Basic Usage" />

### Multi-Series

Add multiple series for comparative analysis. Use `.Layout()` to control how series are arranged:

<CodeBlock lang="csharp" :code="columnMultiSeriesCode" command="dotnet run" example="chart-column-multiseries" exampleTitle="Column Chart - Multi-Series" />

### Layouts

The `ChartLayout` enum controls how multi-series data is displayed:

| Layout | Description |
|--------|-------------|
| `Simple` | Single series — one column per category (default) |
| `Stacked` | Segments stacked end-to-end; column height = sum of values |
| `Stacked100` | Stacked and normalized — every column fills to 100% |
| `Grouped` | Series placed side-by-side within each category |

## Bar Chart

Display horizontal bars — the same data-binding API as `ColumnChart`, oriented horizontally. Bars automatically scale to fill available vertical space with fractional block character edges for sub-cell precision.

<CodeBlock lang="csharp" :code="barBasicCode" command="dotnet run" example="chart-bar-basic" exampleTitle="Bar Chart - Basic Usage" />

Bar charts support all the same layouts and multi-series features as column charts.

## Breakdown Chart

Display a proportional segmented bar showing how parts contribute to a whole. Each segment's width is proportional to its value relative to the total. Unlike column and bar charts, breakdown charts only support a single series.

<CodeBlock lang="csharp" :code="breakdownCode" command="dotnet run" example="chart-breakdown" exampleTitle="Breakdown Chart" />

## Generic Data Binding

All chart widgets are generic (`ColumnChartWidget<T>`, `BarChartWidget<T>`, `BreakdownChartWidget<T>`). Bind any data type by providing selector functions:

<CodeBlock lang="csharp" :code="genericBindingCode" command="dotnet run" example="chart-bar-grouped" exampleTitle="Bar Chart - Grouped with Generic Binding" />

### Data Binding Approaches

| Method | Use Case |
|--------|----------|
| `.Label(T → string)` | Extract category label from each item |
| `.Value(T → double)` | Single-series value extraction |
| `.Series(name, T → double, color?)` | Multiple named series from flat/wide data |
| `.GroupBy(T → string)` | Pivot long-form data into series at runtime |

**Single series**: Use `.Label()` and `.Value()` together.

**Multi-series (flat/wide)**: Use `.Label()` and multiple `.Series()` calls. Each series extracts a different property from the same data items.

**Multi-series (long/normalized)**: Use `.Label()`, `.Value()`, and `.GroupBy()`. The group-by selector determines which series each data point belongs to.

## API Reference

### Common Fluent Methods (Column &amp; Bar)

| Method | Description |
|--------|-------------|
| `.Label(T → string)` | Category label selector |
| `.Value(T → double)` | Single-series value selector |
| `.Series(name, T → double, color?)` | Add a named series |
| `.GroupBy(T → string)` | Pivot long-form data into series |
| `.Layout(ChartLayout)` | Set layout: Simple, Stacked, Stacked100, Grouped |
| `.Title(string)` | Chart title |
| `.ShowValues(bool)` | Display numeric values |
| `.ShowGridLines(bool)` | Display grid lines |
| `.Min(double)` | Explicit axis minimum |
| `.Max(double)` | Explicit axis maximum |
| `.Range(min, max)` | Explicit axis range |
| `.FormatValue(double → string)` | Custom value formatter |

### Breakdown-Specific Methods

| Method | Description |
|--------|-------------|
| `.Label(T → string)` | Segment label selector |
| `.Value(T → double)` | Segment value selector |
| `.Title(string)` | Chart title |
| `.ShowValues(bool)` | Show absolute values in legend |
| `.ShowPercentages(bool)` | Show percentages in legend |

### Rendering Details

Charts render using the [Surface](/guide/widgets/surface) API internally. They use Unicode block characters for sub-cell precision:

- **Column charts**: Vertical blocks (`▁▂▃▄▅▆▇█`) for fractional top edges
- **Bar charts**: Horizontal blocks (`▏▎▍▌▋▊▉█`) for fractional right edges, vertical blocks for bar height edges
- **Stacked segments**: Complementary foreground/background colors at segment boundaries for smooth visual transitions

## Related Widgets

- [Table](/guide/widgets/table) — For displaying the underlying data alongside charts
- [Surface](/guide/widgets/surface) — The low-level rendering API charts are built on
- [Progress](/guide/widgets/progress) — For single-value progress visualization
- [TabPanel](/guide/widgets/tabpanel) — For organizing multiple charts into tabs
