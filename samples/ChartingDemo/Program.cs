using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;
using Hex1b.Widgets;

// Sample data
var monthlySales = new[]
{
    new ChartItem("Jan", 42),
    new ChartItem("Feb", 58),
    new ChartItem("Mar", 35),
    new ChartItem("Apr", 71),
    new ChartItem("May", 49),
    new ChartItem("Jun", 63),
};

var multiSeriesData = new[]
{
    new SalesRecord("Jan", 50, 30, 20),
    new SalesRecord("Feb", 65, 40, 25),
    new SalesRecord("Mar", 45, 35, 30),
    new SalesRecord("Apr", 70, 50, 35),
    new SalesRecord("May", 55, 45, 28),
    new SalesRecord("Jun", 80, 55, 40),
};

var diskUsage = new[]
{
    new ChartItem("Data", 42),
    new ChartItem("Packages", 18),
    new ChartItem("Temp", 9),
    new ChartItem("System", 15),
    new ChartItem("Other", 3),
};

var blue = Hex1bColor.FromRgb(66, 133, 244);
var red = Hex1bColor.FromRgb(234, 67, 53);
var green = Hex1bColor.FromRgb(52, 168, 83);

// Time series data
var temperatureData = new[]
{
    new MonthlyTemp("Jan", 2), new MonthlyTemp("Feb", 4), new MonthlyTemp("Mar", 9),
    new MonthlyTemp("Apr", 14), new MonthlyTemp("May", 18), new MonthlyTemp("Jun", 22),
    new MonthlyTemp("Jul", 25), new MonthlyTemp("Aug", 24), new MonthlyTemp("Sep", 20),
    new MonthlyTemp("Oct", 14), new MonthlyTemp("Nov", 8), new MonthlyTemp("Dec", 3),
};

var financialData = new[]
{
    new FinancialRecord("Jan", 150, 90), new FinancialRecord("Feb", 130, 110),
    new FinancialRecord("Mar", 105, 120), new FinancialRecord("Apr", 95, 135),
    new FinancialRecord("May", 120, 125), new FinancialRecord("Jun", 160, 110),
    new FinancialRecord("Jul", 175, 130), new FinancialRecord("Aug", 140, 145),
    new FinancialRecord("Sep", 110, 150), new FinancialRecord("Oct", 130, 125),
    new FinancialRecord("Nov", 170, 115), new FinancialRecord("Dec", 190, 100),
};


// Live request volume data (animated)
var liveRequests = new List<HourlyRequests>();
var liveRandom = new Random();
var liveStartTime = DateTime.Now;
var lastLiveUpdate = DateTime.MinValue;
void AddLivePoint()
{
    var now = DateTime.Now;
    if ((now - lastLiveUpdate).TotalMilliseconds < 450) return;
    lastLiveUpdate = now;
    var elapsed = now - liveStartTime;
    var label = elapsed.ToString(@"mm\:ss");
    var baseline = 300 + 200 * Math.Sin(elapsed.TotalSeconds * 0.3);
    var noise = liveRandom.NextDouble() * 100 - 50;
    liveRequests.Add(new HourlyRequests(label, Math.Max(0, baseline + noise)));
    if (liveRequests.Count > 40) liveRequests.RemoveAt(0);
}
// Seed initial data
for (int i = 20; i > 0; i--)
{
    var t = DateTime.Now - TimeSpan.FromMilliseconds(i * 500);
    var elapsed = t - liveStartTime;
    var label = elapsed.ToString(@"mm\:ss");
    var baseline = 300 + 200 * Math.Sin(elapsed.TotalSeconds * 0.3);
    var noise = liveRandom.NextDouble() * 100 - 50;
    liveRequests.Add(new HourlyRequests(label, Math.Max(0, baseline + noise)));
}
var random = new Random(42);
var heightWeight = Enumerable.Range(0, 60).Select(_ =>
{
    var height = 150 + random.NextDouble() * 40;
    var weight = (height - 100) * 0.8 + random.NextDouble() * 20 - 10;
    return new Measurement(height, weight);
}).ToArray();

var ageGroupData = Enumerable.Range(0, 90).Select(i =>
{
    var group = i < 30 ? "Young" : i < 60 ? "Middle" : "Senior";
    var income = (group switch { "Young" => 30, "Middle" => 55, _ => 45 })
        + random.NextDouble() * 30;
    var spending = income * (0.5 + random.NextDouble() * 0.4);
    return new DemographicPoint(income, spending, group);
}).ToArray();

// Stacked area state
var stackedFillStyle = FillStyle.Solid;

// Three-series data for stacked area
var regionData = new[]
{
    new RegionSales("Jan", 40, 25, 15), new RegionSales("Feb", 45, 30, 20),
    new RegionSales("Mar", 38, 35, 25), new RegionSales("Apr", 55, 32, 18),
    new RegionSales("May", 48, 40, 30), new RegionSales("Jun", 60, 38, 22),
    new RegionSales("Jul", 52, 45, 28), new RegionSales("Aug", 58, 42, 35),
    new RegionSales("Sep", 50, 48, 30), new RegionSales("Oct", 65, 40, 25),
    new RegionSales("Nov", 55, 50, 32), new RegionSales("Dec", 70, 45, 28),
};

var terminal = Hex1bTerminal.CreateBuilder()
    .WithDiagnostics()
    .WithHex1bApp((app, options) => ctx =>
        ctx.TabPanel(tp => [
            tp.Tab("Simple Columns", t => [
                t.ColumnChart(monthlySales)
                    .Title("Monthly Sales")
                    .ShowValues()
                    .FillHeight(),
                t.Table<ChartItem, VStackWidget>(monthlySales)
                    .Header(h => [h.Cell("Month"), h.Cell("Sales")])
                    .Row((r, item, state) => [r.Cell(item.Label), r.Cell(item.Value.ToString("N0"))])
            ]),
            tp.Tab("Stacked Columns", t => [
                t.ColumnChart(multiSeriesData)
                    .Label(s => s.Month)
                    .Series("Electronics", s => s.Electronics, blue)
                    .Series("Clothing", s => s.Clothing, red)
                    .Series("Food", s => s.Food, green)
                    .Layout(ChartLayout.Stacked)
                    .Title("Sales by Category (Stacked)")
                    .ShowValues()
                    .FillHeight(),
                t.Table<SalesRecord, VStackWidget>(multiSeriesData)
                    .Header(h => [h.Cell("Month"), h.Cell("Electronics"), h.Cell("Clothing"), h.Cell("Food"), h.Cell("Total")])
                    .Row((r, item, state) => [
                        r.Cell(item.Month),
                        r.Cell(item.Electronics.ToString("N0")),
                        r.Cell(item.Clothing.ToString("N0")),
                        r.Cell(item.Food.ToString("N0")),
                        r.Cell((item.Electronics + item.Clothing + item.Food).ToString("N0")),
                    ])
            ]),
            tp.Tab("Stacked 100% Columns", t => [
                t.ColumnChart(multiSeriesData)
                    .Label(s => s.Month)
                    .Series("Electronics", s => s.Electronics, blue)
                    .Series("Clothing", s => s.Clothing, red)
                    .Series("Food", s => s.Food, green)
                    .Layout(ChartLayout.Stacked100)
                    .Title("Sales by Category (100% Stacked)")
                    .FillHeight(),
                t.Table<SalesRecord, VStackWidget>(multiSeriesData)
                    .Header(h => [h.Cell("Month"), h.Cell("Electronics"), h.Cell("Clothing"), h.Cell("Food")])
                    .Row((r, item, state) =>
                    {
                        var total = item.Electronics + item.Clothing + item.Food;
                        return [
                            r.Cell(item.Month),
                            r.Cell($"{item.Electronics / total:P0}"),
                            r.Cell($"{item.Clothing / total:P0}"),
                            r.Cell($"{item.Food / total:P0}"),
                        ];
                    })
            ]),
            tp.Tab("Grouped Columns", t => [
                t.ColumnChart(multiSeriesData)
                    .Label(s => s.Month)
                    .Series("Electronics", s => s.Electronics, blue)
                    .Series("Clothing", s => s.Clothing, red)
                    .Series("Food", s => s.Food, green)
                    .Layout(ChartLayout.Grouped)
                    .Title("Sales by Category (Grouped)")
                    .FillHeight(),
                t.Table<SalesRecord, VStackWidget>(multiSeriesData)
                    .Header(h => [h.Cell("Month"), h.Cell("Electronics"), h.Cell("Clothing"), h.Cell("Food")])
                    .Row((r, item, state) => [
                        r.Cell(item.Month),
                        r.Cell(item.Electronics.ToString("N0")),
                        r.Cell(item.Clothing.ToString("N0")),
                        r.Cell(item.Food.ToString("N0")),
                    ])
            ]),
            tp.Tab("Simple Bars", t => [
                t.BarChart(monthlySales)
                    .Title("Monthly Sales")
                    .ShowValues()
                    .FillHeight(),
                t.Table<ChartItem, VStackWidget>(monthlySales)
                    .Header(h => [h.Cell("Month"), h.Cell("Sales")])
                    .Row((r, item, state) => [r.Cell(item.Label), r.Cell(item.Value.ToString("N0"))])
            ]),
            tp.Tab("Stacked Bars", t => [
                t.BarChart(multiSeriesData)
                    .Label(s => s.Month)
                    .Series("Electronics", s => s.Electronics, blue)
                    .Series("Clothing", s => s.Clothing, red)
                    .Series("Food", s => s.Food, green)
                    .Layout(ChartLayout.Stacked)
                    .Title("Sales by Category (Stacked)")
                    .ShowValues()
                    .FillHeight(),
                t.Table<SalesRecord, VStackWidget>(multiSeriesData)
                    .Header(h => [h.Cell("Month"), h.Cell("Electronics"), h.Cell("Clothing"), h.Cell("Food"), h.Cell("Total")])
                    .Row((r, item, state) => [
                        r.Cell(item.Month),
                        r.Cell(item.Electronics.ToString("N0")),
                        r.Cell(item.Clothing.ToString("N0")),
                        r.Cell(item.Food.ToString("N0")),
                        r.Cell((item.Electronics + item.Clothing + item.Food).ToString("N0")),
                    ])
            ]),
            tp.Tab("Stacked 100% Bars", t => [
                t.BarChart(multiSeriesData)
                    .Label(s => s.Month)
                    .Series("Electronics", s => s.Electronics, blue)
                    .Series("Clothing", s => s.Clothing, red)
                    .Series("Food", s => s.Food, green)
                    .Layout(ChartLayout.Stacked100)
                    .Title("Sales by Category (100% Stacked)")
                    .FillHeight(),
                t.Table<SalesRecord, VStackWidget>(multiSeriesData)
                    .Header(h => [h.Cell("Month"), h.Cell("Electronics"), h.Cell("Clothing"), h.Cell("Food")])
                    .Row((r, item, state) =>
                    {
                        var total = item.Electronics + item.Clothing + item.Food;
                        return [
                            r.Cell(item.Month),
                            r.Cell($"{item.Electronics / total:P0}"),
                            r.Cell($"{item.Clothing / total:P0}"),
                            r.Cell($"{item.Food / total:P0}"),
                        ];
                    })
            ]),
            tp.Tab("Grouped Bars", t => [
                t.BarChart(multiSeriesData)
                    .Label(s => s.Month)
                    .Series("Electronics", s => s.Electronics, blue)
                    .Series("Clothing", s => s.Clothing, red)
                    .Series("Food", s => s.Food, green)
                    .Layout(ChartLayout.Grouped)
                    .Title("Sales by Category (Grouped)")
                    .FillHeight(),
                t.Table<SalesRecord, VStackWidget>(multiSeriesData)
                    .Header(h => [h.Cell("Month"), h.Cell("Electronics"), h.Cell("Clothing"), h.Cell("Food")])
                    .Row((r, item, state) => [
                        r.Cell(item.Month),
                        r.Cell(item.Electronics.ToString("N0")),
                        r.Cell(item.Clothing.ToString("N0")),
                        r.Cell(item.Food.ToString("N0")),
                    ])
            ]),
            tp.Tab("Breakdown", t => [
                t.BreakdownChart(diskUsage)
                    .Title("Disk Usage")
                    .ShowPercentages()
                    .ShowValues(),
                t.Separator(),
                t.Table<ChartItem, VStackWidget>(diskUsage)
                    .Header(h => [h.Cell("Category"), h.Cell("Size (GB)"), h.Cell("Percentage")])
                    .Row((r, item, state) =>
                    {
                        var total = diskUsage.Sum(d => d.Value);
                        var pct = item.Value / total * 100;
                        return [
                            r.Cell(item.Label),
                            r.Cell(item.Value.ToString("N0")),
                            r.Cell($"{pct:F1}%"),
                        ];
                    })
            ]),
            tp.Tab("Time Series", t => [
                t.TimeSeriesChart(temperatureData)
                    .Label(d => d.Month)
                    .Value(d => d.Temp)
                    .Title("Monthly Temperature (°C)")
                    .ShowGridLines()
                    .FillHeight(),
            ]),
            tp.Tab("Multi-Series", t => [
                t.TimeSeriesChart(financialData)
                    .Label(d => d.Month)
                    .Series("Revenue", d => d.Revenue, blue)
                    .Series("Expenses", d => d.Expenses, red)
                    .Title("Revenue vs Expenses")
                    .ShowGridLines()
                    .FillHeight(),
            ]),
            tp.Tab("Area Fill", t =>
            {
                AddLivePoint();
                var chart = t.TimeSeriesChart<HourlyRequests, VStackWidget>(liveRequests.ToArray())
                    .Label(d => d.Hour)
                    .Value(d => d.Requests)
                    .Fill(FillStyle.Braille)
                    .Title("Live Request Volume")
                    .ShowGridLines()
                    .FillHeight()
                    .RedrawAfter(500);
                return [chart];
            }),
            tp.Tab("Scatter", t => [
                t.ScatterChart(heightWeight)
                    .X(d => d.Height)
                    .Y(d => d.Weight)
                    .Title("Height vs Weight")
                    .ShowGridLines()
                    .FillHeight(),
            ]),
            tp.Tab("Scatter (Grouped)", t => [
                t.ScatterChart(ageGroupData)
                    .X(d => d.Income)
                    .Y(d => d.Spending)
                    .GroupBy(d => d.Group)
                    .Title("Income vs Spending by Age Group")
                    .ShowGridLines()
                    .FillHeight(),
            ]),
            tp.Tab("Stacked Area", t => [
                t.TimeSeriesChart(regionData)
                    .Label(d => d.Month)
                    .Series("North", d => d.North, blue)
                    .Series("South", d => d.South, red)
                    .Series("West", d => d.West, green)
                    .Layout(ChartLayout.Stacked)
                    .Fill(stackedFillStyle)
                    .Title("Regional Sales (Stacked)")
                    .ShowGridLines()
                    .FillHeight(),
            ])
        ])
    )
    .WithMouse()
    .Build();

await terminal.RunAsync();

record SalesRecord(string Month, double Electronics, double Clothing, double Food);
record MonthlyTemp(string Month, double Temp);
record FinancialRecord(string Month, double Revenue, double Expenses);
record HourlyRequests(string Hour, double Requests);
record Measurement(double Height, double Weight);
record DemographicPoint(double Income, double Spending, string Group);
record RegionSales(string Month, double North, double South, double West);
