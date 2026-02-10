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

var terminal = Hex1bTerminal.CreateBuilder()
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
            ])
        ])
    )
    .WithMouse()
    .Build();

await terminal.RunAsync();

record SalesRecord(string Month, double Electronics, double Clothing, double Food);
