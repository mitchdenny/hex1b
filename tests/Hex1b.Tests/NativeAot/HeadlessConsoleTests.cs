using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private static readonly CodeFamily[] ConsoleFamilies =
    [
        ConsoleAdapter, WindowsConsole, UnixConsole,
        new("System.Console", @"^System_Console_")
    ];

    [TestMethod]
    [TestCategory("NativeAot")]
    [NativeAotEnabled]
    public async Task HeadlessTextAndChart_NativePublish_MeasuresIsolatedConsoleRemoval()
    {
        var reports = new List<ScenarioReport>();
        foreach (var variant in new[] { "Baseline", "CopiedSourcesBaseline", "AppFallbackRemoved", "BothConstructionPathsRemoved" })
        {
            var scenario = new Scenario("HeadlessConsole" + variant,
                variant == "BothConstructionPathsRemoved"
                    ? [Scenario.Terminal, Scenario.Builder, App, Chart, Pool]
                    : [Scenario.Terminal, Scenario.Builder, App, Chart, Pool, ConsoleAdapter, ConsoleConstruction],
                variant == "BothConstructionPathsRemoved" ? [ConsoleAdapter, ConsoleConstruction, WindowsConsole, UnixConsole] : [])
            {
                ConsumerName = "HeadlessConsole"
            };
            reports.Add(await ScenarioRunner.RunAsync(scenario, HeadlessSource, TestContext,
                TestContext.CancellationToken, variant == "Baseline" ? null : CreateOverrides(
                    removeAppFallback: variant is "AppFallbackRemoved" or "BothConstructionPathsRemoved",
                    removeBuilderDefault: variant == "BothConstructionPathsRemoved"),
                variant == "Baseline" ? "Unmodified main library and existing headless API."
                    : $"Measurement only: {variant}. Patched library compile inputs; not a supported construction path."));
        }
        var restored = await RunInteractiveControlAsync("InteractiveConsoleAfterExperiment");

        var baseline = reports[0];
        Assert.AreEqual(baseline.LibraryInputSha256, restored.LibraryInputSha256,
            "The normal library build must be restored after experimental compile inputs.");
        Assert.AreEqual(baseline.ExecutableBytes, reports[1].ExecutableBytes,
            "Copying unmodified sources must not manufacture executable savings.");
        Assert.AreEqual(baseline.MethodCodeCount, reports[1].MethodCodeCount);
        Assert.AreEqual(baseline.MethodCodeLength, reports[1].MethodCodeLength);
        foreach (var report in reports.Skip(1))
        {
            Assert.AreEqual(baseline.ConsumerSourceSha256, report.ConsumerSourceSha256);
            Assert.AreEqual(baseline.Sdk, report.Sdk);
            Assert.AreEqual(baseline.IlCompilerPackage, report.IlCompilerPackage);
            Assert.AreEqual(baseline.Rid, report.Rid);
            Assert.AreEqual(baseline.LibraryAssemblyIdentity, report.LibraryAssemblyIdentity);
            Assert.AreEqual(baseline.LibraryProjectSha256, report.LibraryProjectSha256);
            Assert.AreNotEqual(baseline.LibraryInputSha256, report.LibraryInputSha256);
            foreach (var snapshot in new[] { "text.svg", "chart.svg" })
                Assert.AreEqual(File.ReadAllText(Path.Combine(baseline.ArtifactDirectory, snapshot)),
                    File.ReadAllText(Path.Combine(report.ArtifactDirectory, snapshot)),
                    $"Snapshot changed in {report.Name}: {snapshot}.");
        }

        var baselineMethods = NativeMap.Read(baseline.CompilerMapPath!);
        var comparison = reports.Select(report =>
        {
            var methods = NativeMap.Read(report.CompilerMapPath!);
            var names = methods.Select(method => method.Name).ToHashSet(StringComparer.Ordinal);
            return new
            {
                report.Name,
                report.ArtifactDirectory,
                report.ExecutableBytes,
                report.MethodCodeCount,
                report.MethodCodeLength,
                SavedExecutableBytes = baseline.ExecutableBytes - report.ExecutableBytes,
                SavedExecutablePercent = 100.0 * (baseline.ExecutableBytes - report.ExecutableBytes) / baseline.ExecutableBytes,
                report.ConsumerSourceSha256,
                report.LibraryInputSha256,
                Snapshots = new[] { "text.svg", "chart.svg" }.Select(name => new
                {
                    Name = name,
                    Sha256 = Convert.ToHexString(SHA256.HashData(
                        File.ReadAllBytes(Path.Combine(report.ArtifactDirectory, name))))
                }),
                ConsoleFamilies = ConsoleFamilies.Select(family =>
                {
                    var matcher = new Regex(family.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                    var emitted = methods.Where(method => matcher.IsMatch(method.Name)).ToArray();
                    var removed = baselineMethods.Where(method => matcher.IsMatch(method.Name) && !names.Contains(method.Name)).ToArray();
                    return new
                    {
                        family.Name,
                        EmittedCount = emitted.Length,
                        EmittedBytes = emitted.Sum(method => method.Length),
                        RemovedCount = removed.Length,
                        RemovedBytes = removed.Sum(method => method.Length),
                        RemovedMethods = removed
                    };
                }),
                report.PublishInventory
            };
        }).ToArray();
        var path = Path.Combine(baseline.ArtifactDirectory, "console-comparison.json");
        var json = JsonSerializer.Serialize(comparison, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, TestContext.CancellationToken);
        TestContext.WriteLine(json);
        TestContext.AddResultFile(path);
    }

    [TestMethod]
    [TestCategory("NativeAot")]
    [NativeAotEnabled]
    public Task InteractiveConsole_NativePublish_PreservesDefaultBuilderAndStandaloneApp()
        => RunInteractiveControlAsync("InteractiveConsole");

    private Task<ScenarioReport> RunInteractiveControlAsync(string name)
        => ScenarioRunner.RunAsync(new Scenario(name,
            [Scenario.Terminal, Scenario.Builder, App, ConsoleAdapter, ConsoleConstruction], [])
        {
            UsePty = true,
            ConsumerName = "InteractiveConsole",
            PtyStopPrompts = ["builder-console-control", "standalone-console-control"]
        }, """
            using Hex1b;

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using (var terminal = Hex1bTerminal.CreateBuilder()
                .WithHex1bApp(ctx => ctx.Text("builder-console-control")).Build())
            {
                await terminal.RunAsync(cancellation.Token);
            }
            await using (var standalone = new Hex1bApp(ctx => ctx.Text("standalone-console-control")))
            {
                await standalone.RunAsync(cancellation.Token);
            }
            Console.WriteLine("NATIVE_AOT_OK:InteractiveConsole");
            """, TestContext, TestContext.CancellationToken);

    [TestMethod]
    public void CreateOverrides_ConsoleExperiments_ChangeOnlyConstructionRegions()
    {
        var baseline = CreateOverrides(false, false);
        var appOnly = CreateOverrides(true, false);
        var both = CreateOverrides(true, true);
        Assert.AreEqual(baseline["Hex1bTerminalBuilder.cs"], appOnly["Hex1bTerminalBuilder.cs"]);
        Assert.AreEqual(appOnly["Hex1bApp.cs"], both["Hex1bApp.cs"]);
        StringAssert.Contains(baseline["Hex1bApp.cs"], "new ConsolePresentationAdapter(enableMouse: _mouseEnabled)");
        Assert.IsFalse(appOnly["Hex1bApp.cs"].Contains("new ConsolePresentationAdapter", StringComparison.Ordinal));
        Assert.IsFalse(both["Hex1bTerminalBuilder.cs"].Contains("new ConsolePresentationAdapter", StringComparison.Ordinal));
        StringAssert.Contains(both["Hex1bTerminalBuilder.cs"], "presentation is ConsolePresentationAdapter console");
        foreach (var sources in new[] { baseline, appOnly, both })
        {
            var pooling = sources["Hex1bApp.cs"];
            StringAssert.Contains(pooling, """
                _surfacePool = options.EnableSurfacePooling
                            ? new SurfacePool(options.SurfacePoolMaxSurfacesPerBucket, options.SurfacePoolMaxIdleFrames)
                            : null;
                """.ReplaceLineEndings("\n"));
        }
    }

    internal static IReadOnlyDictionary<string, string> CreateOverrides(bool removeAppFallback, bool removeBuilderDefault)
    {
        var app = File.ReadAllText(Path.Combine(ScenarioRunner.RepositoryRoot, "src", "Hex1b", "Hex1bApp.cs")).ReplaceLineEndings("\n");
        var builder = File.ReadAllText(Path.Combine(ScenarioRunner.RepositoryRoot, "src", "Hex1b", "Hex1bTerminalBuilder.cs")).ReplaceLineEndings("\n");
        if (removeAppFallback)
        {
            const string start = "        // Create or use provided adapter\n";
            const string end = "        // Propagate the app's mouse-enabled option";
            var first = app.IndexOf(start, StringComparison.Ordinal);
            var last = app.IndexOf(end, StringComparison.Ordinal);
            if (first < 0 || last <= first || app.IndexOf(start, first + start.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidDataException("App construction changed; review the measurement-only source replacement.");
            app = app[..first] + """
                        _adapter = options.WorkloadAdapter
                            ?? throw new InvalidOperationException("Measurement-only app requires a supplied workload adapter.");
                        _ownedTerminal = null;


                """ + app[last..];
        }
        if (removeBuilderDefault)
        {
            const string construction = "builder => new ConsolePresentationAdapter(enableMouse: builder._enableMouse, preserveOPost: builder._preserveOPost);";
            if (builder.Split(construction, StringSplitOptions.None).Length != 2)
                throw new InvalidDataException("Builder construction changed; review the measurement-only source replacement.");
            builder = builder.Replace(construction,
                "builder => { _ = builder._preserveOPost; throw new InvalidOperationException(\"Measurement-only builder requires explicit presentation.\"); };",
                StringComparison.Ordinal);
        }
        return new Dictionary<string, string>
        {
            ["Hex1bApp.cs"] = app,
            ["Hex1bTerminalBuilder.cs"] = builder
        };
    }

    private const string HeadlessSource = """
        using Hex1b;
        using Hex1b.Automation;
        using Hex1b.Charts;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Hex1bApp? currentApp = null;
        await using (var terminal = Hex1bTerminal.CreateBuilder()
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
        await using (var terminal = Hex1bTerminal.CreateBuilder()
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
