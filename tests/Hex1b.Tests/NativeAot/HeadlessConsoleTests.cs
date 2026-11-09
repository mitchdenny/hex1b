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
    public async Task CreateSlimBuilder_CurrentLibrary_MeasuresActualConstructionChanges()
    {
        var source = HeadlessSource.Replace("CreateBuilder()", "CreateSlimBuilder()", StringComparison.Ordinal);
        var baseline = ConsoleConstructionBaseline.ReadAll();
        baseline["Hex1bTerminal.cs"] = ReplaceRequired(baseline["Hex1bTerminal.cs"],
            "public static Hex1bTerminalBuilder CreateBuilder() => new();",
            """
            public static Hex1bTerminalBuilder CreateBuilder() => new();

                public static Hex1bTerminalBuilder CreateSlimBuilder() => CreateBuilder();
            """);
        var normalBuilder = ConsoleConstructionBaseline.ReadCurrent();
        normalBuilder["Hex1bTerminal.cs"] = ReplaceRequired(normalBuilder["Hex1bTerminal.cs"],
            "CreateSlimBuilder() => Hex1bTerminalBuilder.CreateSlim();", "CreateSlimBuilder() => CreateBuilder();");
        var variants = new (string Name, IReadOnlyDictionary<string, string>? Sources, bool ConsoleRemoved)[]
        {
            ("PreChange", baseline, false),
            ("InternalAppNormalBuilder", normalBuilder, false),
            ("SlimCopiedSources", ConsoleConstructionBaseline.ReadCurrent(), true),
            ("SlimActualLibrary", null, true)
        };
        var reports = new List<ScenarioReport>();
        foreach (var (name, sources, consoleRemoved) in variants)
        {
            reports.Add(await ScenarioRunner.RunAsync(new Scenario("SlimActual" + name,
                consoleRemoved ? [Scenario.Terminal, Scenario.Builder, App, Chart, Pool]
                    : [Scenario.Terminal, Scenario.Builder, App, Chart, Pool, ConsoleAdapter, ConsoleConstruction],
                consoleRemoved ? [ConsoleAdapter, ConsoleConstruction, WindowsConsole, UnixConsole] : [])
            {
                ConsumerName = "HeadlessConsole"
            }, source, TestContext, TestContext.CancellationToken, sources,
                name == "PreChange"
                    ? "Pre-change construction regions restored; CreateSlimBuilder forwards to the old CreateBuilder. Explicit WithHeadless in identical consumer."
                    : name == "InternalAppNormalBuilder"
                        ? "Actual internal adapter-required app; CreateSlimBuilder forwards to current normal CreateBuilder to isolate app change."
                        : "Actual empty slim builder and internal adapter-required app. No console-removal substitutions."));
        }
        foreach (var report in reports.Skip(1))
        {
            Assert.AreEqual(reports[0].ConsumerSourceSha256, report.ConsumerSourceSha256);
            Assert.AreEqual(reports[0].Sdk, report.Sdk);
            Assert.AreEqual(reports[0].IlCompilerPackage, report.IlCompilerPackage);
            Assert.AreEqual(reports[0].Rid, report.Rid);
            Assert.AreEqual(reports[0].LibraryAssemblyIdentity, report.LibraryAssemblyIdentity);
            Assert.AreEqual(reports[0].LibraryProjectSha256, report.LibraryProjectSha256);
            foreach (var snapshot in new[] { "text.svg", "chart.svg" })
                Assert.AreEqual(File.ReadAllText(Path.Combine(reports[0].ArtifactDirectory, snapshot)),
                    File.ReadAllText(Path.Combine(report.ArtifactDirectory, snapshot)));
        }
        Assert.AreEqual(reports[2].ExecutableBytes, reports[3].ExecutableBytes);
        Assert.AreEqual(reports[2].MethodCodeCount, reports[3].MethodCodeCount);
        Assert.AreEqual(reports[2].MethodCodeLength, reports[3].MethodCodeLength);
        var interactive = await RunInteractiveControlAsync("SlimActualInteractiveConsole", actualLibrary: true);
        Assert.AreEqual(reports[3].LibraryInputSha256, interactive.LibraryInputSha256,
            "The unmodified actual library must be used by both headless and interactive consumers.");
        await WriteComparisonAsync(reports, "slim-actual-comparison.json");
    }

    [TestMethod]
    [TestCategory("NativeAot")]
    [NativeAotEnabled]
    public async Task HeadlessTextAndChart_NativePublish_MeasuresIsolatedConsoleRemoval()
    {
        var original = await RunInteractiveControlAsync("InteractiveConsoleBeforeExperiment", actualLibrary: true);
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
                TestContext.CancellationToken, CreateOverrides(
                    removeAppFallback: variant is "AppFallbackRemoved" or "BothConstructionPathsRemoved",
                    removeBuilderDefault: variant == "BothConstructionPathsRemoved"),
                variant == "Baseline" ? "Reconstructed pre-experiment main construction and existing headless API."
                    : $"Measurement only: {variant}. Patched library compile inputs; not a supported construction path."));
        }
        var restored = await RunInteractiveControlAsync("InteractiveConsoleAfterExperiment", actualLibrary: true);

        var baseline = reports[0];
        Assert.AreEqual(original.LibraryInputSha256, restored.LibraryInputSha256,
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
            if (report.Name != "HeadlessConsoleCopiedSourcesBaseline")
                Assert.AreNotEqual(baseline.LibraryInputSha256, report.LibraryInputSha256);
            foreach (var snapshot in new[] { "text.svg", "chart.svg" })
                Assert.AreEqual(File.ReadAllText(Path.Combine(baseline.ArtifactDirectory, snapshot)),
                    File.ReadAllText(Path.Combine(report.ArtifactDirectory, snapshot)),
                    $"Snapshot changed in {report.Name}: {snapshot}.");
        }

        await WriteComparisonAsync(reports, "console-comparison.json");
    }

    [TestMethod]
    [TestCategory("NativeAot")]
    [NativeAotEnabled]
    public Task CreateBuilder_HeadlessTrue_NativePublish_MeasuresConstructorBranch()
        => RunBuilderExperimentAsync(slim: false);

    [TestMethod]
    [TestCategory("NativeAot")]
    [NativeAotEnabled]
    public Task CreateSlimBuilder_NativePublish_MeasuresSeparateConstructor()
        => RunBuilderExperimentAsync(slim: true);

    private async Task RunBuilderExperimentAsync(bool slim)
    {
        var source = HeadlessSource.Replace("CreateBuilder()", slim ? "CreateSlimBuilder()" : "CreateBuilder(headless: true)", StringComparison.Ordinal)
            .Replace(".WithHeadless().WithDimensions", ".WithDimensions", StringComparison.Ordinal);
        var prefix = slim ? "HeadlessSlim" : "HeadlessBool";
        var reports = new List<ScenarioReport>();
        var variants = slim
            ? new[] { "RuntimeReplacement", "SeparateConstruction", "SeparateConstructionWithoutAppFallback" }
            : ["RuntimeReplacement", "ConstructorSelection", "ConstructorSelectionWithoutAppFallback"];
        foreach (var variant in variants)
        {
            var separate = variant != "RuntimeReplacement";
            var removeFallback = variant.EndsWith("WithoutAppFallback", StringComparison.Ordinal);
            var consoleRemoved = slim && removeFallback;
            reports.Add(await ScenarioRunner.RunAsync(new Scenario(prefix + variant,
                consoleRemoved ? [Scenario.Terminal, Scenario.Builder, App, Chart, Pool]
                    : [Scenario.Terminal, Scenario.Builder, App, Chart, Pool, ConsoleAdapter, ConsoleConstruction],
                consoleRemoved ? [ConsoleAdapter, ConsoleConstruction, WindowsConsole, UnixConsole] : [])
            {
                ConsumerName = "HeadlessConsole"
            }, source, TestContext, TestContext.CancellationToken,
                slim ? CreateSlimOverrides(separate, removeFallback) : CreateBoolOverrides(separate, removeFallback),
                $"Test-only {(slim ? "CreateSlimBuilder()" : "CreateBuilder(headless: true)")} prototype: {variant}."));
        }
        await RunInteractiveControlAsync(prefix + "DefaultInteractiveControl",
            slim ? CreateSlimOverrides(true, false) : CreateBoolOverrides(true, false));
        if (!slim)
            await RunInteractiveControlAsync(prefix + "FalseInteractiveControl", CreateBoolOverrides(true, false),
                explicitHeadlessFalse: true);
        await RunInteractiveControlAsync(prefix + "RestoredInteractiveControl", actualLibrary: true);
        var baseline = reports[0];
        foreach (var report in reports.Skip(1))
        {
            Assert.AreEqual(baseline.ConsumerSourceSha256, report.ConsumerSourceSha256);
            Assert.AreEqual(baseline.Sdk, report.Sdk);
            Assert.AreEqual(baseline.IlCompilerPackage, report.IlCompilerPackage);
            Assert.AreEqual(baseline.Rid, report.Rid);
            Assert.AreEqual(baseline.LibraryAssemblyIdentity, report.LibraryAssemblyIdentity);
            Assert.AreEqual(baseline.LibraryProjectSha256, report.LibraryProjectSha256);
            foreach (var snapshot in new[] { "text.svg", "chart.svg" })
                Assert.AreEqual(File.ReadAllText(Path.Combine(baseline.ArtifactDirectory, snapshot)),
                    File.ReadAllText(Path.Combine(report.ArtifactDirectory, snapshot)),
                    $"Snapshot changed in {report.Name}: {snapshot}.");
        }
        await WriteComparisonAsync(reports, slim ? "headless-slim-comparison.json" : "headless-bool-comparison.json");
    }

    private async Task WriteComparisonAsync(IReadOnlyList<ScenarioReport> reports, string fileName)
    {
        var baseline = reports[0];
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
        var path = Path.Combine(baseline.ArtifactDirectory, fileName);
        var json = JsonSerializer.Serialize(comparison, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, TestContext.CancellationToken);
        TestContext.WriteLine(json);
        TestContext.AddResultFile(path);
    }

    [TestMethod]
    [TestCategory("NativeAot")]
    [NativeAotEnabled]
    public async Task InteractiveConsole_NativePublish_PreservesDefaultBuilderAndStandaloneApp()
    {
        await RunInteractiveControlAsync("InteractiveConsole");
        await RunInteractiveControlAsync("InteractiveConsoleCurrentLibrary", actualLibrary: true);
    }

    private Task<ScenarioReport> RunInteractiveControlAsync(string name,
        IReadOnlyDictionary<string, string>? sourceOverrides = null, bool explicitHeadlessFalse = false,
        bool actualLibrary = false)
        => ScenarioRunner.RunAsync(new Scenario(name,
            [Scenario.Terminal, Scenario.Builder, App, ConsoleAdapter, ConsoleConstruction], [])
        {
            UsePty = true,
            ConsumerName = "InteractiveConsole",
            PtyStopPrompts = actualLibrary ? ["builder-console-control", "slim-console-control"]
                : ["builder-console-control", "standalone-console-control"]
        }, actualLibrary ? ActualInteractiveSource : explicitHeadlessFalse
            ? InteractiveSource.Replace("CreateBuilder()", "CreateBuilder(headless: false)", StringComparison.Ordinal)
            : InteractiveSource, TestContext, TestContext.CancellationToken,
                actualLibrary ? null : sourceOverrides ?? ConsoleConstructionBaseline.ReadAll());

    private const string ActualInteractiveSource = """
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
            """;

    private const string InteractiveSource = """
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
            """;

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
        var sources = ConsoleConstructionBaseline.ReadAll();
        var app = sources["Hex1bApp.cs"];
        var builder = sources["Hex1bTerminalBuilder.cs"];
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
        sources["Hex1bApp.cs"] = app;
        sources["Hex1bTerminalBuilder.cs"] = builder;
        return sources;
    }

    [TestMethod]
    public void CreateBoolOverrides_ConstructorSelection_RemovesSharedConsoleInitializer()
    {
        var baseline = CreateBoolOverrides(false, false);
        var constructor = CreateBoolOverrides(true, false);
        var isolated = CreateBoolOverrides(true, true);
        Assert.AreEqual(baseline["Hex1bApp.cs"], constructor["Hex1bApp.cs"]);
        Assert.AreEqual(constructor["Hex1bTerminalBuilder.cs"], isolated["Hex1bTerminalBuilder.cs"]);
        StringAssert.Contains(constructor["Hex1bTerminalBuilder.cs"],
            "private Func<Hex1bTerminalBuilder, IHex1bTerminalPresentationAdapter> _presentationFactory;");
        StringAssert.Contains(constructor["Hex1bTerminalBuilder.cs"], "public Hex1bTerminalBuilder(bool headless)");
        StringAssert.Contains(constructor["Hex1bTerminalBuilder.cs"], "_presentationFactory = headless");
        StringAssert.Contains(constructor["Hex1bTerminalBuilder.cs"], "new ConsolePresentationAdapter");
        StringAssert.Contains(constructor["Hex1bTerminal.cs"], "CreateBuilder(bool headless) => new(headless);");
        Assert.IsFalse(isolated["Hex1bApp.cs"].Contains("new ConsolePresentationAdapter", StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, string> CreateBoolOverrides(bool selectInConstructor, bool removeAppFallback)
    {
        var sources = CreateOverrides(removeAppFallback, false).ToDictionary(pair => pair.Key, pair => pair.Value);
        var terminal = sources["Hex1bTerminal.cs"];
        const string createBuilder = "public static Hex1bTerminalBuilder CreateBuilder() => new();";
        terminal = ReplaceRequired(terminal, createBuilder, createBuilder + "\n\n    " + (selectInConstructor
            ? "public static Hex1bTerminalBuilder CreateBuilder(bool headless) => new(headless);"
            : """
              public static Hex1bTerminalBuilder CreateBuilder(bool headless)
                  {
                      var builder = new Hex1bTerminalBuilder();
                      return headless ? builder.WithHeadless() : builder;
                  }
              """.ReplaceLineEndings("\n")));
        sources["Hex1bTerminal.cs"] = terminal;
        if (selectInConstructor)
        {
            var builder = sources["Hex1bTerminalBuilder.cs"];
            builder = ReplaceRequired(builder,
                "private Func<Hex1bTerminalBuilder, IHex1bTerminalPresentationAdapter> _presentationFactory = \n" +
                "        builder => new ConsolePresentationAdapter(enableMouse: builder._enableMouse, preserveOPost: builder._preserveOPost);",
                "private Func<Hex1bTerminalBuilder, IHex1bTerminalPresentationAdapter> _presentationFactory;");
            builder = ReplaceRequired(builder, "public Hex1bTerminalBuilder()\n    {\n    }", BoolConstructors.ReplaceLineEndings("\n"));
            sources["Hex1bTerminalBuilder.cs"] = builder;
        }
        return sources;
    }

    private const string BoolConstructors = """
                public Hex1bTerminalBuilder() : this(headless: false)
                    {
                    }

                    public Hex1bTerminalBuilder(bool headless)
                    {
                        _presentationFactory = headless
                            ? builder => new HeadlessPresentationAdapter(builder._width, builder._height,
                                (builder._workloadAdapter as IHex1bAppTerminalWorkloadAdapter)?.Capabilities)
                            : builder => new ConsolePresentationAdapter(enableMouse: builder._enableMouse, preserveOPost: builder._preserveOPost);
                    }
                """;

    private static IReadOnlyDictionary<string, string> CreateSlimOverrides(bool separateConstruction, bool removeAppFallback)
    {
        var sources = CreateBoolOverrides(separateConstruction, removeAppFallback).ToDictionary(pair => pair.Key, pair => pair.Value);
        if (!separateConstruction)
        {
            sources["Hex1bTerminal.cs"] = ReplaceRequired(sources["Hex1bTerminal.cs"],
                "public static Hex1bTerminalBuilder CreateBuilder(bool headless)", "public static Hex1bTerminalBuilder CreateSlimBuilder()");
            sources["Hex1bTerminal.cs"] = ReplaceRequired(sources["Hex1bTerminal.cs"],
                "return headless ? builder.WithHeadless() : builder;", "return builder.WithHeadless();");
        }
        else
        {
            sources["Hex1bTerminal.cs"] = ReplaceRequired(sources["Hex1bTerminal.cs"],
                "public static Hex1bTerminalBuilder CreateBuilder(bool headless) => new(headless);",
                "public static Hex1bTerminalBuilder CreateSlimBuilder() => Hex1bTerminalBuilder.CreateSlim();");
            sources["Hex1bTerminalBuilder.cs"] = ReplaceRequired(sources["Hex1bTerminalBuilder.cs"],
                BoolConstructors.ReplaceLineEndings("\n"), """
                public Hex1bTerminalBuilder()
                        : this(builder => new ConsolePresentationAdapter(enableMouse: builder._enableMouse, preserveOPost: builder._preserveOPost))
                    {
                    }

                    private Hex1bTerminalBuilder(Func<Hex1bTerminalBuilder, IHex1bTerminalPresentationAdapter> presentationFactory)
                    {
                        _presentationFactory = presentationFactory;
                    }

                    internal static Hex1bTerminalBuilder CreateSlim()
                        => new(builder => new HeadlessPresentationAdapter(builder._width, builder._height,
                            (builder._workloadAdapter as IHex1bAppTerminalWorkloadAdapter)?.Capabilities));
                """.ReplaceLineEndings("\n"));
        }
        return sources;
    }

    [TestMethod]
    public void CreateSlimOverrides_SeparateConstruction_HasNoBooleanFactorySelection()
    {
        var sources = CreateSlimOverrides(true, false);
        StringAssert.Contains(sources["Hex1bTerminal.cs"], "CreateSlimBuilder() => Hex1bTerminalBuilder.CreateSlim();");
        StringAssert.Contains(sources["Hex1bTerminalBuilder.cs"], "private Hex1bTerminalBuilder(Func<");
        StringAssert.Contains(sources["Hex1bTerminalBuilder.cs"], "internal static Hex1bTerminalBuilder CreateSlim()");
        Assert.IsFalse(sources["Hex1bTerminalBuilder.cs"].Contains("_presentationFactory = headless", StringComparison.Ordinal));
        StringAssert.Contains(sources["Hex1bApp.cs"], "new ConsolePresentationAdapter");
    }

    private static string ReplaceRequired(string source, string before, string after)
    {
        if (source.Split(before, StringSplitOptions.None).Length != 2)
            throw new InvalidDataException("Construction source changed; review the boolean headless experiment.");
        return source.Replace(before, after, StringComparison.Ordinal);
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
