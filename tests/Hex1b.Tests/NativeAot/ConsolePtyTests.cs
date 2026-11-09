using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.NativeAot;

[TestClass]
[DoNotParallelize]
public sealed class ConsolePtyTests
{
    public TestContext TestContext { get; set; } = null!;

    internal static readonly CodeFamily App = new("app implementation", @"^Hex1b_Hex1b_Hex1bApp_");
    internal static readonly CodeFamily AppOptions = new("app options", @"^Hex1b_Hex1b_Hex1bAppOptions_");
    internal static readonly CodeFamily AppWorkload = new("app workload", @"^Hex1b_Hex1b_Hex1bAppWorkloadAdapter_");
    internal static readonly CodeFamily Widgets = new("widgets", @"^Hex1b_Hex1b_Widgets_");
    internal static readonly CodeFamily Nodes = new("nodes", @"^Hex1b_Hex1b_Nodes_");
    internal static readonly CodeFamily RenderContext = new("app render context", @"^Hex1b_Hex1b_Hex1bRenderContext_");
    private static readonly CodeFamily Pool = new("surface pool", @"^Hex1b_Hex1b_Surfaces_SurfacePool_");
    internal static readonly CodeFamily[] AppFamilies = [App, AppOptions, AppWorkload, Widgets, Nodes, RenderContext, Pool];

    [TestMethod]
    [TestCategory("NativeAot")]
    [NativeAotEnabled]
    public async Task ConsoleAndPty_NativePublish_ExcludesAppAndMeasuresOptInCost()
    {
        var reports = new List<ScenarioReport>();
        var supportInventories = new List<IReadOnlyList<PublishedFile>>();
        foreach (var (normalBuilder, includeApp) in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            string[] defines = (normalBuilder, includeApp) switch
            {
                (true, true) => ["DEFAULT_BUILDER", "APP_CONTROL"],
                (true, false) => ["DEFAULT_BUILDER"],
                (false, true) => ["APP_CONTROL"],
                _ => []
            };
            CodeFamily[] core = [Scenario.Terminal, Scenario.Builder, Scenario.ChildProcess,
                HeadlessConsoleTests.ConsoleAdapter, HeadlessConsoleTests.ConsoleConstruction];
            var report = await ScenarioRunner.RunAsync(new Scenario(
                "ConsolePty" + (normalBuilder ? "Normal" : "Slim") + (includeApp ? "WithApp" : ""),
                includeApp ? [.. core, .. AppFamilies] : core, includeApp ? [] : AppFamilies)
            {
                ConsumerName = "ConsolePty",
                UsePty = true,
                Defines = defines
            }, Source, TestContext, TestContext.CancellationToken, experiment:
                "Identical consumer source and unmodified library. DEFAULT_BUILDER selects the builder; " +
                "APP_CONTROL additionally renders and stops a minimal console TUI before the same real PTY exchange.");
            reports.Add(report);
            supportInventories.Add(ReadExternalPtySupport(report));
            if (includeApp)
                Assert.IsTrue(File.Exists(Path.Combine(report.ArtifactDirectory, "app.svg")));
        }

        var baseline = reports[0];
        foreach (var report in reports.Skip(1))
        {
            Assert.AreEqual(baseline.ConsumerSourceSha256, report.ConsumerSourceSha256);
            Assert.AreEqual(baseline.LibraryInputSha256, report.LibraryInputSha256);
            Assert.AreEqual(baseline.LibraryAssemblyIdentity, report.LibraryAssemblyIdentity);
            Assert.AreEqual(baseline.LibraryProjectSha256, report.LibraryProjectSha256);
            Assert.AreEqual(baseline.Sdk, report.Sdk);
            Assert.AreEqual(baseline.IlCompilerPackage, report.IlCompilerPackage);
            Assert.AreEqual(baseline.Rid, report.Rid);
            Assert.AreEqual(File.ReadAllText(Path.Combine(baseline.ArtifactDirectory, "pty.svg")),
                File.ReadAllText(Path.Combine(report.ArtifactDirectory, "pty.svg")),
                $"PTY output changed in {report.Name}.");
        }
        foreach (var inventory in supportInventories.Skip(1))
            TestSeq.AreEqual(supportInventories[0], inventory);
        Assert.AreEqual(File.ReadAllText(Path.Combine(reports[2].ArtifactDirectory, "app.svg")),
            File.ReadAllText(Path.Combine(reports[3].ArtifactDirectory, "app.svg")));

        var comparison = reports.Select((report, index) =>
        {
            var includeApp = index >= 2;
            var withApp = reports[index % 2 + 2];
            var methods = NativeMap.Read(report.CompilerMapPath!);
            var withAppMethods = NativeMap.Read(withApp.CompilerMapPath!);
            var names = methods.Select(method => method.Name).ToHashSet(StringComparer.Ordinal);
            return new
            {
                report.Name, report.ArtifactDirectory, report.ConsumerSourceSha256, report.ConsumerDefines,
                report.LibraryInputSha256, report.ExecutableBytes, report.MethodCodeCount, report.MethodCodeLength,
                SavedBytesByOmittingAppControl = includeApp ? 0 : withApp.ExecutableBytes - report.ExecutableBytes,
                SavedPercentByOmittingAppControl = includeApp ? 0 :
                    100.0 * (withApp.ExecutableBytes - report.ExecutableBytes) / withApp.ExecutableBytes,
                SlimSavedBytesVersusNormal = index % 2 == 0 ? reports[index + 1].ExecutableBytes - report.ExecutableBytes : 0,
                Families = NativeMap.Measure(methods, [.. AppFamilies, .. CoreFamilies], [])
                    .Select(family => new { family.Family, family.Count, family.Length }).ToArray(),
                RemovedAppMethods = withAppMethods.Where(method =>
                    AppFamilies.Any(family => System.Text.RegularExpressions.Regex.IsMatch(method.Name, family.Pattern)) &&
                    !names.Contains(method.Name)).ToArray(),
                report.PublishInventory,
                ExternalPtySupport = supportInventories[index]
            };
        }).ToArray();
        var path = Path.Combine(baseline.ArtifactDirectory, "pty-app-comparison.json");
        var json = JsonSerializer.Serialize(comparison, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, TestContext.CancellationToken);
        TestContext.WriteLine(json);
        TestContext.AddResultFile(path);
    }

    private static readonly CodeFamily[] CoreFamilies =
        [HeadlessConsoleTests.ConsoleAdapter, HeadlessConsoleTests.WindowsConsole,
         HeadlessConsoleTests.UnixConsole, Scenario.ChildProcess];

    private static IReadOnlyList<PublishedFile> ReadExternalPtySupport(ScenarioReport report)
    {
        if (!OperatingSystem.IsWindows())
            return [];
        Assert.IsTrue(WindowsPtyShimLocator.TryResolveFromBaseDirectory(
            Path.GetDirectoryName(report.ExecutablePath)!, null, out var host),
            "The default Windows PTY proxy used by the native consumer must be identifiable.");
        return Directory.GetFiles(Path.GetDirectoryName(host)!, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => new PublishedFile(path, ScenarioRunner.FileKind(path, report.ExecutablePath),
                new FileInfo(path).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))).ToArray();
    }

    private const string Source = """
        using Hex1b;
        using Hex1b.Automation;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        #if APP_CONTROL
        await using (var appTerminal = Hex1bTerminal.CreateSlimBuilder()
            .WithDimensions(100, 24)
            .WithPresentation(new ConsolePresentationAdapter())
            .WithHex1bApp(ctx => ctx.Text("app-positive-control")).Build())
        {
            var appRun = appTerminal.RunAsync(cancellation.Token);
            using var appSnapshot = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("app-positive-control"), TimeSpan.FromSeconds(5), "optional app rendered")
                .Build().ApplyAsync(appTerminal, cancellation.Token);
            await File.WriteAllTextAsync(Path.Combine("..", "app.svg"),
                ((IHex1bTerminalRegion)appSnapshot).ToSvg(), cancellation.Token);
            await new Hex1bTerminalInputSequenceBuilder().Ctrl().Key(Hex1b.Input.Hex1bKey.C)
                .Build().ApplyAsync(appTerminal, cancellation.Token);
            await appRun;
        }
        #endif

        #if DEFAULT_BUILDER
        var builder = Hex1bTerminal.CreateBuilder();
        #else
        var builder = Hex1bTerminal.CreateSlimBuilder();
        #endif
        await using (var terminal = builder.WithDimensions(100, 24)
            .WithPresentation(new ConsolePresentationAdapter())
            .WithPtyProcess(options =>
            {
                if (OperatingSystem.IsWindows())
                {
                    options.FileName = "cmd.exe";
                    options.Arguments = ["/d", "/q", "/v:on", "/c",
                        "echo pty-ready&set /p reply=&echo pty-output:!reply!"];
                }
                else
                {
                    options.FileName = "/bin/sh";
                    options.Arguments = ["-c",
                        "stty -echo; printf 'pty-ready\\r\\n'; IFS= read -r reply; printf 'pty-output:%s\\r\\n' \"$reply\"; test \"$reply\" = 'pty-input'"];
                }
            }).Build())
        {
            var run = terminal.RunAsync(cancellation.Token);
            var exchange = new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("pty-ready"), TimeSpan.FromSeconds(5), "PTY child ready")
                .Type("pty-input").Enter()
                .WaitUntil(s => s.ContainsText("pty-output:pty-input"), TimeSpan.FromSeconds(5), "PTY input echoed by child")
                .Build().ApplyAsync(terminal, cancellation.Token);
            if (await Task.WhenAny(run, exchange) == run && await run != 0)
                throw new InvalidOperationException("PTY child failed before completing the exchange.");
            using var snapshot = await exchange;
            if (await run != 0)
                throw new InvalidOperationException("PTY child did not exit successfully.");
            await File.WriteAllTextAsync(Path.Combine("..", "pty.svg"),
                ((IHex1bTerminalRegion)snapshot).ToSvg(), cancellation.Token);
        }
        Console.WriteLine("NATIVE_AOT_OK:ConsolePty");
        """;
}
