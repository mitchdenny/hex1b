using Hex1b;
using Hex1b.Input;
using Hex1b.Logging;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;
using Hex1b.Nodes;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tests;

public class BackgroundBleedThroughTests
{
    [Fact]
    public void Composite_TransparentBgPicksUpDestBackground()
    {
        var bg = Hex1bColor.FromRgb(10, 20, 30);
        var dest = new Surface(10, 1);
        dest[0, 0] = new SurfaceCell(" ", null, bg, CellAttributes.None, 1);
        var src = new Surface(10, 1);
        src[0, 0] = new SurfaceCell("H", null, null, CellAttributes.None, 1);
        dest.Composite(src, 0, 0);
        Assert.Equal("H", dest[0, 0].Character);
        Assert.Equal(bg, dest[0, 0].Background);
    }

    [Fact]
    public void FillBackground_ReplacesTransparentBg()
    {
        var bg = Hex1bColor.FromRgb(10, 20, 30);
        var surface = new Surface(5, 1);
        surface[0, 0] = new SurfaceCell("H", null, null, CellAttributes.None, 1);
        surface.FillBackground(bg);
        Assert.Equal(bg, surface[0, 0].Background);
    }

    [Fact]
    public void FillBackground_FillsEmptyCells()
    {
        var bg = Hex1bColor.FromRgb(10, 20, 30);
        var surface = new Surface(5, 1);
        surface.FillBackground(bg);
        Assert.Equal(bg, surface[0, 0].Background);
    }

    [Fact]
    public async Task BackgroundPanel_TextBlock_HasPanelBackground()
    {
        var panelBg = Hex1bColor.FromRgb(10, 20, 30);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        using var app = new Hex1bApp(
            ctx => new BackgroundPanelWidget(panelBg,
                new VStackWidget([new TextBlockWidget("Hello")])),
            new Hex1bAppOptions { WorkloadAdapter = workload });
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(10))
            .Capture("bgpanel-textblock")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        var cell = snapshot.GetCell(0, 0);
        Assert.True(cell.Background.HasValue,
            $"'H' should have bg. Row:\n{DumpRow(snapshot, 0, 20)}");
        Assert.Equal(panelBg, cell.Background.Value);
    }

    /// <summary>
    /// Core reproduction: BackgroundPanel wrapping a LoggerPanel with real log data.
    /// On top of a RED background in a ZStack. The table header and data rows
    /// must have the BLACK bg from the panel, NOT red bleed-through.
    /// </summary>
    [Fact]
    public async Task BackgroundPanel_WithLoggerTable_NoBleedThrough()
    {
        var windowBg = Hex1bColor.FromRgb(255, 0, 0);  // RED
        var panelBg = Hex1bColor.FromRgb(0, 0, 255);   // BLUE overlay

        // Create log store with real entries
        IHex1bLogStore logStore = null!;
        using var loggerFactory = LoggerFactory.Create(b =>
        {
            b.SetMinimumLevel(LogLevel.Debug);
            b.AddHex1b(out logStore);
        });
        var logger = loggerFactory.CreateLogger("TestCategory");
        logger.LogInformation("First log message");
        logger.LogWarning("Second warning message");
        logger.LogError("Third error message");
        logger.LogInformation("Fourth info message");
        logger.LogDebug("Fifth debug message");

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(100, 20).Build();

        using var app = new Hex1bApp(
            ctx => ctx.ZStack(z => [
                // Layer 1: RED background filling everything
                new BackgroundPanelWidget(windowBg,
                    ctx.VStack(v => [v.Text("RED BG")])),
                // Layer 2: BLUE panel with LoggerPanel table
                new BackgroundPanelWidget(panelBg,
                    ctx.VStack(v => [
                        v.LoggerPanel(logStore).Fill()
                    ]))
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First") || s.ContainsText("Level") || s.ContainsText("Message"),
                TimeSpan.FromSeconds(3), "table content")
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("bgpanel-loggertable")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var screenText = DumpScreen(snapshot, 20, 100);

        // Find any cell with RED (255,0,0) background - that's bleed-through
        var redCells = new List<string>();
        var nullBgCells = new List<string>();

        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 100; x++)
            {
                var cell = snapshot.GetCell(x, y);
                if (string.IsNullOrEmpty(cell.Character)) continue;

                if (cell.Background.HasValue
                    && cell.Background.Value.R == 255
                    && cell.Background.Value.G == 0
                    && cell.Background.Value.B == 0)
                {
                    redCells.Add($"({x},{y})'{cell.Character}'");
                }
                if (!cell.Background.HasValue && cell.Character != " "
                    && cell.Character != "\0")
                {
                    nullBgCells.Add($"({x},{y})'{cell.Character}'");
                }
            }
        }

        var report = $"Screen:\n{screenText}\n";
        if (redCells.Count > 0)
            report += $"\nRED bg cells ({redCells.Count}): {string.Join(", ", redCells.Take(30))}\n";
        if (nullBgCells.Count > 0)
            report += $"\nNull bg cells ({nullBgCells.Count}): {string.Join(", ", nullBgCells.Take(30))}\n";
        report += $"\nRow 0 detail:\n{DumpRow(snapshot, 0, 50)}";
        report += $"\nRow 1 detail:\n{DumpRow(snapshot, 1, 50)}";

        Assert.True(redCells.Count == 0, $"RED bleed-through detected!\n{report}");
        Assert.True(nullBgCells.Count == 0, $"Null bg = potential bleed-through!\n{report}");
    }

    /// <summary>
    /// Deep nesting: ZStack → VStack → Border → BackgroundPanel → VStack → DragBarPanel → LoggerPanel.
    /// Matches the popup rendering chain depth.
    /// </summary>
    [Fact]
    public async Task BackgroundPanel_DeeplyNested_WithDragBarAndLoggerTable()
    {
        var windowBg = Hex1bColor.FromRgb(255, 0, 0);
        var panelBg = Hex1bColor.FromRgb(0, 0, 255);

        IHex1bLogStore logStore = null!;
        using var loggerFactory = LoggerFactory.Create(b =>
        {
            b.SetMinimumLevel(LogLevel.Debug);
            b.AddHex1b(out logStore);
        });
        var logger = loggerFactory.CreateLogger("Test");
        logger.LogInformation("First log message");
        logger.LogWarning("Second warning");
        logger.LogError("Third error");

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(100, 20).Build();

        using var app = new Hex1bApp(
            ctx => ctx.ZStack(z => [
                new BackgroundPanelWidget(windowBg, ctx.VStack(v => [v.Text("RED")])),
                ctx.VStack(outer => [
                    new BackgroundPanelWidget(panelBg,
                        outer.VStack(inner => [
                            inner.DragBarPanel(
                                inner.LoggerPanel(logStore).Fill()
                            ).InitialSize(14).MinSize(6).MaxSize(20).HandleEdge(DragBarEdge.Top)
                        ]))
                ])
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First") || s.ContainsText("Level"),
                TimeSpan.FromSeconds(3), "table content")
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("deep-nesting-bleedthrough")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var redCells = new List<string>();
        var nullBgCells = new List<string>();
        // Only check rows 0-13 (the BackgroundPanel area, not below it)
        for (int y = 0; y < 14; y++)
            for (int x = 0; x < 100; x++)
            {
                var cell = snapshot.GetCell(x, y);
                if (string.IsNullOrEmpty(cell.Character)) continue;
                if (cell.Background.HasValue && cell.Background.Value.R == 255 && cell.Background.Value.G == 0)
                    redCells.Add($"({x},{y})'{cell.Character}'");
                if (!cell.Background.HasValue && cell.Character != " " && cell.Character != "\0")
                    nullBgCells.Add($"({x},{y})'{cell.Character}'");
            }

        var screen = DumpScreen(snapshot, 20, 100);
        Assert.True(redCells.Count == 0, $"RED bleed!\n{string.Join(", ", redCells.Take(20))}\n{screen}");
        Assert.True(nullBgCells.Count == 0, $"Null bg!\n{string.Join(", ", nullBgCells.Take(20))}\n{screen}");
    }

    private static string DumpRow(Hex1b.Automation.IHex1bTerminalRegion snap, int row, int cols)
    {
        var sb = new System.Text.StringBuilder();
        for (int x = 0; x < cols; x++)
        {
            var c = snap.GetCell(x, row);
            var bg = c.Background.HasValue
                ? $"rgb({c.Background.Value.R},{c.Background.Value.G},{c.Background.Value.B})"
                : "null";
            sb.AppendLine($"    [{x},{row}] '{c.Character}' bg={bg}");
        }
        return sb.ToString();
    }

    private static string DumpScreen(Hex1b.Automation.IHex1bTerminalRegion snap, int rows, int cols)
    {
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < rows; y++)
        {
            sb.Append($"{y,2}|");
            for (int x = 0; x < cols; x++)
            {
                var c = snap.GetCell(x, y);
                sb.Append(string.IsNullOrEmpty(c.Character) ? " " : c.Character);
            }
            sb.AppendLine("|");
        }
        return sb.ToString();
    }
}
