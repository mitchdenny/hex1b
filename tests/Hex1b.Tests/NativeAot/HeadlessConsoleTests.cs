using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.NativeAot;

[TestClass]
[DoNotParallelize]
public sealed class HeadlessConsoleTests
{
    public TestContext TestContext { get; set; } = null!;

    internal static readonly CodeFamily ConsoleAdapter = new("console presentation adapter",
        @"^Hex1b_Hex1b_ConsolePresentationAdapter_");
    internal static readonly CodeFamily ConsoleConstruction = new("console construction",
        @"^Hex1b_Hex1b_ConsolePresentationAdapter__(?:_ctor|CreateConsoleDriver)");
    internal static readonly CodeFamily WindowsConsole = new("Windows console driver",
        @"^Hex1b_Hex1b_WindowsConsoleDriver_");
    internal static readonly CodeFamily UnixConsole = new("Unix console driver",
        @"^Hex1b_Hex1b_(?:UnixConsoleDriver|UnixTerminalInterop)_");
    private static readonly CodeFamily App = new("app", @"^Hex1b_Hex1b_Hex1bApp_");
    private static readonly CodeFamily Chart = new("bar chart node", @"^Hex1b_Hex1b_Nodes_BarChartNode");
    private static readonly CodeFamily Pool = new("surface pool", @"^Hex1b_Hex1b_Surfaces_SurfacePool_");

    [TestMethod]
    [TestCategory("NativeAot")]
    [NativeAotEnabled]
    public async Task CreateSlimBuilder_HeadlessTextAndChart_ExcludesConsoleCode()
    {
        var reports = new List<ScenarioReport>();
        foreach (var slim in new[] { false, true })
        {
            reports.Add(await ScenarioRunner.RunAsync(new Scenario(slim ? "HeadlessSlim" : "HeadlessNormal",
                slim ? [Scenario.Terminal, Scenario.Builder, App, Chart, Pool]
                    : [Scenario.Terminal, Scenario.Builder, App, Chart, Pool, ConsoleAdapter, ConsoleConstruction],
                slim ? [ConsoleAdapter, ConsoleConstruction, WindowsConsole, UnixConsole] : [])
            {
                ConsumerName = "HeadlessConsole",
                Defines = slim ? ["SLIM_BUILDER"] : []
            }, HeadlessSource, TestContext, TestContext.CancellationToken));
        }
        var normal = reports[0];
        var slimReport = reports[1];
        Assert.AreEqual(normal.ConsumerSourceSha256, slimReport.ConsumerSourceSha256);
        Assert.AreEqual(normal.LibraryInputSha256, slimReport.LibraryInputSha256);
        Assert.AreEqual(normal.Sdk, slimReport.Sdk);
        Assert.AreEqual(normal.IlCompilerPackage, slimReport.IlCompilerPackage);
        Assert.AreEqual(normal.Rid, slimReport.Rid);
        foreach (var snapshot in new[] { "text.svg", "chart.svg" })
            Assert.AreEqual(File.ReadAllText(Path.Combine(normal.ArtifactDirectory, snapshot)),
                File.ReadAllText(Path.Combine(slimReport.ArtifactDirectory, snapshot)));
        var comparison = new
        {
            Normal = normal,
            Slim = slimReport,
            SavedExecutableBytes = normal.ExecutableBytes - slimReport.ExecutableBytes,
            SavedExecutablePercent = 100.0 * (normal.ExecutableBytes - slimReport.ExecutableBytes) / normal.ExecutableBytes
        };
        var path = Path.Combine(normal.ArtifactDirectory, "headless-comparison.json");
        await File.WriteAllTextAsync(path,
            JsonSerializer.Serialize(comparison, new JsonSerializerOptions { WriteIndented = true }),
            TestContext.CancellationToken);
        TestContext.AddResultFile(path);
    }

    [TestMethod]
    [TestCategory("NativeAot")]
    [NativeAotEnabled]
    public Task InteractiveConsole_NativePublish_PreservesDefaultAndExplicitSlimPresentation()
        => ScenarioRunner.RunAsync(new Scenario("InteractiveConsole",
            [Scenario.Terminal, Scenario.Builder, App, ConsoleAdapter, ConsoleConstruction], [])
        {
            UsePty = true,
            PtyStopPrompts = ["builder-console-control", "slim-console-control"]
        }, """
            using Hex1b;

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using (var terminal = Hex1bTerminal.CreateBuilder()
                .WithHex1bApp(ctx => ctx.Text("builder-console-control")).Build())
            {
                await terminal.RunAsync(cancellation.Token);
            }
            await using (var terminal = Hex1bTerminal.CreateSlimBuilder()
                .WithPresentation(new ConsolePresentationAdapter())
                .WithHex1bApp(ctx => ctx.Text("slim-console-control")).Build())
            {
                await terminal.RunAsync(cancellation.Token);
            }
            Console.WriteLine("NATIVE_AOT_OK:InteractiveConsole");
            """, TestContext, TestContext.CancellationToken);

    private const string HeadlessSource = """
        using Hex1b;
        using Hex1b.Automation;
        using Hex1b.Charts;

        static Hex1bTerminalBuilder CreateBuilder()
        {
        #if SLIM_BUILDER
            return Hex1bTerminal.CreateSlimBuilder();
        #else
            return Hex1bTerminal.CreateBuilder();
        #endif
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Hex1bApp? currentApp = null;
        await using (var terminal = CreateBuilder()
            .WithHeadless().WithDimensions(100, 24)
            .WithHex1bApp(options => options.EnableDefaultCtrlCExit = false, app =>
            {
                currentApp = app;
                return ctx => ctx.Text("headless-text-snapshot");
            }).Build())
        {
            var run = terminal.RunAsync(cancellation.Token);
            using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("headless-text-snapshot"), TimeSpan.FromSeconds(5), "text rendered")
                .Build().ApplyAsync(terminal, cancellation.Token);
            var svg = snapshot.ToSvg();
            if (snapshot.Width != 100 || snapshot.Height != 24 || !svg.Contains("<text "))
                throw new InvalidOperationException("Text snapshot was not rendered.");
            await File.WriteAllTextAsync(Path.Combine("..", "text.svg"), svg, cancellation.Token);
            currentApp!.RequestStop();
            await run;
        }
        await using (var terminal = CreateBuilder()
            .WithHeadless().WithDimensions(100, 24)
            .WithHex1bApp(options => options.EnableDefaultCtrlCExit = false, app =>
            {
                currentApp = app;
                return ctx => ctx.BarChart([new ChartItem("Alpha", 8), new ChartItem("Beta", 4)])
                    .Title("headless-chart-snapshot").ShowValues();
            }).Build())
        {
            var run = terminal.RunAsync(cancellation.Token);
            using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("headless-chart-snapshot") && s.ContainsText("Alpha") &&
                    s.ContainsText("Beta") && s.ContainsText("8") && s.ContainsText("4") &&
                    s.ContainsText("\u2588"), TimeSpan.FromSeconds(5), "chart rendered")
                .Build().ApplyAsync(terminal, cancellation.Token);
            var svg = snapshot.ToSvg();
            if (snapshot.Width != 100 || snapshot.Height != 24 || !svg.Contains("<text ") || !snapshot.HasForegroundColor())
                throw new InvalidOperationException("Chart snapshot was not rendered.");
            await File.WriteAllTextAsync(Path.Combine("..", "chart.svg"), svg, cancellation.Token);
            currentApp!.RequestStop();
            await run;
        }
        Console.WriteLine("NATIVE_AOT_OK:HeadlessConsole");
        """;
}
