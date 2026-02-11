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

const timeSeriesBasicCode = `using Hex1b;
using Hex1b.Charts;

var data = new[]
{
    new ChartItem("Jan", 2), new ChartItem("Feb", 4),
    new ChartItem("Mar", 9), new ChartItem("Apr", 14),
    new ChartItem("May", 18), new ChartItem("Jun", 22),
    new ChartItem("Jul", 25), new ChartItem("Aug", 24),
    new ChartItem("Sep", 20), new ChartItem("Oct", 14),
    new ChartItem("Nov", 8), new ChartItem("Dec", 3),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.TimeSeriesChart(data)
        .Title("Monthly Temperature (°C)")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();`

const timeSeriesMultiCode = `using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;

var data = new[]
{
    new FinRec("Jan", 150, 90), new FinRec("Feb", 130, 110),
    new FinRec("Mar", 105, 120), new FinRec("Apr", 95, 135),
    new FinRec("May", 120, 125), new FinRec("Jun", 160, 110),
    new FinRec("Jul", 175, 130), new FinRec("Aug", 140, 145),
    new FinRec("Sep", 110, 150), new FinRec("Oct", 130, 125),
    new FinRec("Nov", 170, 115), new FinRec("Dec", 190, 100),
};

var blue = Hex1bColor.FromRgb(66, 133, 244);
var red = Hex1bColor.FromRgb(234, 67, 53);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.TimeSeriesChart(data)
        .Label(d =&gt; d.Month)
        .Series("Revenue", d =&gt; d.Revenue, blue)
        .Series("Expenses", d =&gt; d.Expenses, red)
        .Title("Revenue vs Expenses")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();

record FinRec(string Month, double Revenue, double Expenses);`

const timeSeriesFillCode = `using Hex1b;
using Hex1b.Charts;

var data = new[]
{
    new ChartItem("00:00", 120), new ChartItem("04:00", 60),
    new ChartItem("08:00", 450), new ChartItem("12:00", 580),
    new ChartItem("16:00", 490), new ChartItem("20:00", 310),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.TimeSeriesChart(data)
        .Fill(FillStyle.Braille)
        .Title("Request Volume (24h)")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();`

const timeSeriesStackedCode = `using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;

var data = new[]
{
    new RegionSales("Jan", 40, 25, 15), new RegionSales("Feb", 45, 30, 20),
    new RegionSales("Mar", 38, 35, 25), new RegionSales("Apr", 55, 32, 18),
    new RegionSales("May", 48, 40, 30), new RegionSales("Jun", 60, 38, 22),
    new RegionSales("Jul", 52, 45, 28), new RegionSales("Aug", 58, 42, 35),
    new RegionSales("Sep", 50, 48, 30), new RegionSales("Oct", 65, 40, 25),
    new RegionSales("Nov", 55, 50, 32), new RegionSales("Dec", 70, 45, 28),
};

var blue = Hex1bColor.FromRgb(66, 133, 244);
var red = Hex1bColor.FromRgb(234, 67, 53);
var green = Hex1bColor.FromRgb(52, 168, 83);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =&gt; ctx =&gt; ctx.TimeSeriesChart(data)
        .Label(d =&gt; d.Month)
        .Series("North", d =&gt; d.North, blue)
        .Series("South", d =&gt; d.South, red)
        .Series("West", d =&gt; d.West, green)
        .Layout(ChartLayout.Stacked)
        .Fill(FillStyle.Braille)
        .Title("Regional Sales (Stacked)")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();

record RegionSales(string Month, double North, double South, double West);`

const scatterBasicCode = `using Hex1b;
using Hex1b.Charts;

var random = new Random(42);
var data = Enumerable.Range(0, 60).Select(_ =>
{
    var height = 150 + random.NextDouble() * 40;
    var weight = (height - 100) * 0.8 + random.NextDouble() * 20 - 10;
    return new Measurement(height, weight);
}).ToArray();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ScatterChart(data)
        .X(d =&gt; d.Height)
        .Y(d =&gt; d.Weight)
        .Title("Height vs Weight")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();

record Measurement(double Height, double Weight);`

const scatterGroupedCode = `using Hex1b;
using Hex1b.Charts;

var random = new Random(42);
var data = Enumerable.Range(0, 90).Select(i =>
{
    var group = i &lt; 30 ? "Young" : i &lt; 60 ? "Middle" : "Senior";
    var income = (group switch { "Young" => 30, "Middle" => 55, _ => 45 })
        + random.NextDouble() * 30;
    var spending = income * (0.5 + random.NextDouble() * 0.4);
    return new DemoPoint(income, spending, group);
}).ToArray();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ScatterChart(data)
        .X(d =&gt; d.Income)
        .Y(d =&gt; d.Spending)
        .GroupBy(d =&gt; d.Group)
        .Title("Income vs Spending by Age Group")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();

record DemoPoint(double Income, double Spending, string Group);`

</script>

# Charts

Visualize data with column charts, bar charts, breakdown charts, time series line charts, and scatter plots. All chart widgets use a generic data-binding pattern that works with any data type, with convenience overloads for ad-hoc data using `ChartItem`.

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

## Time Series Chart

Plot one or more value series over an ordered X axis using braille characters for sub-cell precision line drawing. Each terminal cell provides a 2×4 dot grid (8 sub-pixels).

### Basic Usage

<CodeBlock lang="csharp" :code="timeSeriesBasicCode" command="dotnet run" example="chart-timeseries-basic" exampleTitle="Time Series - Basic" />

### Multi-Series

Add multiple series to compare trends. Lines are drawn independently and colors blend at intersection points:

<CodeBlock lang="csharp" :code="timeSeriesMultiCode" command="dotnet run" example="chart-timeseries-multi" exampleTitle="Time Series - Multi-Series" />

### Area Fill

Use `.Fill()` to shade the area below the line. `FillStyle.Braille` fills with braille dots; `FillStyle.Solid` uses block characters:

<CodeBlock lang="csharp" :code="timeSeriesFillCode" command="dotnet run" example="chart-timeseries-fill" exampleTitle="Time Series - Area Fill" />

### Stacked Area

Use `.Layout(ChartLayout.Stacked)` with `.Fill()` to create stacked area charts. Each series' area sits on top of the previous, showing cumulative totals. Switch between `FillStyle.Braille` (dot-based) and `FillStyle.Solid` (block-character) rendering:

<CodeBlock lang="csharp" :code="timeSeriesStackedCode" command="dotnet run" example="chart-timeseries-stacked" exampleTitle="Time Series - Stacked Area" />

## Scatter Chart

Plot independent (x, y) data points using braille dots. Points are NOT connected by lines — each plots a single dot. Both axes are numeric.

### Basic Usage

<CodeBlock lang="csharp" :code="scatterBasicCode" command="dotnet run" example="chart-scatter-basic" exampleTitle="Scatter Chart - Basic" />

### Grouped Series

Use `.GroupBy()` to color-code data points by category:

<CodeBlock lang="csharp" :code="scatterGroupedCode" command="dotnet run" example="chart-scatter-grouped" exampleTitle="Scatter Chart - Grouped" />

## Generic Data Binding

All chart widgets are generic (`ColumnChartWidget<T>`, `BarChartWidget<T>`, `BreakdownChartWidget<T>`, `TimeSeriesChartWidget<T>`, `ScatterChartWidget<T>`). Bind any data type by providing selector functions:

<CodeBlock lang="csharp" :code="genericBindingCode" command="dotnet run" example="chart-bar-grouped" exampleTitle="Bar Chart - Grouped with Generic Binding" />

### Data Binding Approaches

| Method | Use Case |
|--------|----------|
| `.Label(T → string)` | Extract category label from each item |
| `.Value(T → double)` | Single-series value extraction |
| `.Series(name, T → double, color?)` | Multiple named series from flat/wide data |
| `.GroupBy(T → string)` | Pivot long-form data into series at runtime |
| `.X(T → double)` | X-axis numeric value (scatter charts) |
| `.Y(T → double)` | Y-axis numeric value (scatter charts) |

**Single series**: Use `.Label()` and `.Value()` together.

**Multi-series (flat/wide)**: Use `.Label()` and multiple `.Series()` calls. Each series extracts a different property from the same data items.

**Multi-series (long/normalized)**: Use `.Label()`, `.Value()`, and `.GroupBy()`. The group-by selector determines which series each data point belongs to.

**Scatter charts**: Use `.X()` and `.Y()` for numeric axes. Optionally add `.GroupBy()` for color-coded series.

## Animation

Charts work well with live data. Use `.RedrawAfter(milliseconds)` to schedule periodic re-renders. The widget builder runs on each frame, so update your data source and the chart re-draws automatically:

```csharp
var liveData = new List<ChartItem>();
var lastUpdate = DateTime.MinValue;

var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        // Throttle data updates to one per interval
        if ((DateTime.Now - lastUpdate).TotalMilliseconds >= 450)
        {
            lastUpdate = DateTime.Now;
            liveData.Add(new ChartItem(DateTime.Now.ToString("ss"), Random.Shared.Next(100)));
            if (liveData.Count > 40) liveData.RemoveAt(0);
        }

        return ctx.TimeSeriesChart(liveData.ToArray())
            .Fill(FillStyle.Braille)
            .Title("Live Data")
            .RedrawAfter(500);
    })
    .Build();
```

::: tip
`RedrawAfter` is a one-shot timer — it schedules a single re-render after the specified delay. Since the widget builder calls it on every frame, the effect is continuous animation. Throttle your data mutations to avoid adding points on re-renders triggered by resize or input events.
:::

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

### Time Series Methods

| Method | Description |
|--------|-------------|
| `.Label(T → string)` | X-axis label selector |
| `.Value(T → double)` | Single-series Y value selector |
| `.Series(name, T → double, color?)` | Add a named Y series |
| `.Fill(FillStyle)` | Area fill: `Solid` (block chars) or `Braille` (dot fill) |
| `.Layout(ChartLayout)` | `Stacked` for cumulative stacked areas |
| `.Title(string)` | Chart title |
| `.ShowValues(bool)` | Display Y values at data points |
| `.ShowGridLines(bool)` | Display horizontal grid lines |
| `.Min(double)` / `.Max(double)` | Explicit Y-axis range |
| `.Range(min, max)` | Explicit Y-axis range |
| `.FormatValue(double → string)` | Custom Y value formatter |

### Scatter Chart Methods

| Method | Description |
|--------|-------------|
| `.X(T → double)` | X-axis value selector |
| `.Y(T → double)` | Y-axis value selector |
| `.GroupBy(T → string)` | Series grouping selector |
| `.Title(string)` | Chart title |
| `.ShowGridLines(bool)` | Display grid lines |
| `.XRange(min, max)` | Explicit X-axis range |
| `.YRange(min, max)` | Explicit Y-axis range |
| `.FormatX(double → string)` | Custom X value formatter |
| `.FormatY(double → string)` | Custom Y value formatter |

### Rendering Details

Charts render using the [Surface](/guide/widgets/surface) API internally. They use Unicode block characters for sub-cell precision:

- **Column charts**: Vertical blocks (`▁▂▃▄▅▆▇█`) for fractional top edges
- **Bar charts**: Horizontal blocks (`▏▎▍▌▋▊▉█`) for fractional right edges, vertical blocks for bar height edges
- **Time series &amp; scatter charts**: Braille characters (`U+2800–U+28FF`) with a 2×4 dot grid per cell for sub-cell point and line plotting
- **Stacked segments**: Complementary foreground/background colors at segment boundaries for smooth visual transitions

## Related Widgets

- [Table](/guide/widgets/table) — For displaying the underlying data alongside charts
- [Surface](/guide/widgets/surface) — The low-level rendering API charts are built on
- [Progress](/guide/widgets/progress) — For single-value progress visualization
- [TabPanel](/guide/widgets/tabpanel) — For organizing multiple charts into tabs
