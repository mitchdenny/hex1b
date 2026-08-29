using System.Text;
using Hex1b.Tokens;
using KgpValidation;

namespace Hex1b.Tests;

[TestClass]
public class KgpValidationSampleTests
{
    private static readonly TerminalCapabilities KgpCapabilities = new()
    {
        SupportsKgp = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20,
    };

    [TestMethod]
    public void ScenarioCatalog_AllScenarios_HaveStableDocumentedMetadata()
    {
        var scenarios = KgpScenarioCatalog.All;

        Assert.HasCount(10, scenarios);
        Assert.HasCount(
            scenarios.Count,
            scenarios.Select(scenario => scenario.Id).Distinct().ToList());
        foreach (var scenario in scenarios)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(scenario.Id));
            Assert.IsFalse(string.IsNullOrWhiteSpace(scenario.Title));
            Assert.IsFalse(string.IsNullOrWhiteSpace(scenario.Area));
            Assert.IsFalse(string.IsNullOrWhiteSpace(scenario.Expected));
            Assert.IsFalse(string.IsNullOrWhiteSpace(scenario.Protocol));
            Assert.IsGreaterThanOrEqualTo(0, scenario.ExpectedState.ImageCount);
            Assert.IsGreaterThanOrEqualTo(0, scenario.ExpectedState.PlacementCount);
            Assert.IsGreaterThanOrEqualTo(
                0,
                scenario.ExpectedState.VirtualPlacementCount);
            Assert.IsGreaterThanOrEqualTo(1, scenario.VariantCount);
            if (scenario.VariantCount > 1)
                Assert.IsFalse(string.IsNullOrWhiteSpace(scenario.ActionHint));
        }
    }

    [TestMethod]
    public void FrameRenderer_EveryScenario_ProducesExpectedKgpState()
    {
        var scenarios = KgpScenarioCatalog.All;

        for (var index = 0; index < scenarios.Count; index++)
        {
            var scenario = scenarios[index];
            using var terminal = CreateTerminal(new PassiveWorkloadAdapter());
            var frame = KgpValidationFrameRenderer.Render(
                scenario,
                index,
                scenarios.Count,
                width: 100,
                height: 32,
                enterAlternateScreen: true);

            terminal.ApplyTokens(
                AnsiTokenizer.Tokenize(Encoding.UTF8.GetString(frame)));

            using var snapshot = terminal.CreateSnapshot();
            Assert.IsTrue(
                snapshot.ContainsText(scenario.Title),
                $"{scenario.Id}: title was not rendered.");
            Assert.AreEqual(
                scenario.ExpectedState.ImageCount,
                terminal.KgpImageStore.ImageCount,
                $"{scenario.Id}: image count");
            Assert.AreEqual(
                scenario.ExpectedState.PlacementCount,
                snapshot.KgpPlacements.Count,
                $"{scenario.Id}: placement count");
            Assert.AreEqual(
                scenario.ExpectedState.VirtualPlacementCount,
                terminal.KgpVirtualPlacementCount,
                $"{scenario.Id}: virtual placement count");
            AssertScenarioGeometry(scenario.Id, snapshot);

            if (scenario.ExpectedState.FrameImageId is { } imageId)
            {
                var image = terminal.KgpImageStore.GetImageById(imageId);
                Assert.IsNotNull(image, $"{scenario.Id}: frame image");
                Assert.AreEqual(
                    scenario.ExpectedState.FrameCount,
                    image.FrameCount,
                    $"{scenario.Id}: frame count");
            }
        }
    }

    [TestMethod]
    public void FrameRenderer_ScrollingVariants_MoveMarkerAndImageTogether()
    {
        var scenarios = KgpScenarioCatalog.All;
        using var terminal = CreateTerminal(new PassiveWorkloadAdapter());

        ApplyFrame(
            terminal,
            scenarios,
            scenarioIndex: 5,
            enterAlternateScreen: true,
            variant: 0);
        using (var before = terminal.CreateSnapshot())
        {
            AssertPosition(before, 5201, 1, row: 18, column: 8);
            Assert.Contains("ANCHOR", before.GetLineTrimmed(17));
        }

        ApplyFrame(
            terminal,
            scenarios,
            scenarioIndex: 5,
            enterAlternateScreen: false,
            variant: 1);
        using var after = terminal.CreateSnapshot();
        AssertPosition(after, 5201, 1, row: 12, column: 8);
        Assert.Contains("ANCHOR", after.GetLineTrimmed(11));
        Assert.DoesNotContain("ANCHOR", after.GetLineTrimmed(17));
    }

    [TestMethod]
    public void FrameRenderer_ScenarioChange_ClearsVirtualAndAnimationState()
    {
        var scenarios = KgpScenarioCatalog.All;
        using var terminal = CreateTerminal(new PassiveWorkloadAdapter());

        ApplyFrame(terminal, scenarios, scenarioIndex: 6, enterAlternateScreen: true);
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);

        ApplyFrame(terminal, scenarios, scenarioIndex: 0, enterAlternateScreen: false);
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);

        ApplyFrame(terminal, scenarios, scenarioIndex: 8, enterAlternateScreen: false);
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Assert.AreEqual(1, terminal.KgpImageStore.GetImageById(8201)!.FrameCount);

        ApplyFrame(terminal, scenarios, scenarioIndex: 0, enterAlternateScreen: false);
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(4, 2)]
    [DataRow(79, 23)]
    public void FrameRenderer_UndersizedTerminal_RendersWithoutThrowing(
        int width,
        int height)
    {
        var frame = KgpValidationFrameRenderer.Render(
            KgpScenarioCatalog.All[1],
            scenarioIndex: 1,
            KgpScenarioCatalog.All.Count,
            width,
            height,
            enterAlternateScreen: true);

        Assert.IsNotEmpty(frame);
    }

    [TestMethod]
    public async Task Workload_NavigationAndScenarioAction_RenderThroughRawTerminalStack()
    {
        await using var workload = new KgpValidationWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(KgpCapabilities)
            .WithDimensions(100, 32)
            .Build();
        var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                screen =>
                    screen.ContainsText("Compliance overview") &&
                    screen.ContainsText("N/Space/Right"),
                TimeSpan.FromSeconds(3),
                "overview rendered")
            .Up()
            .Down()
            .Type("n")
            .WaitUntil(
                screen =>
                    screen.ContainsText("Direct and chunked transfer") &&
                    screen.ContainsText("DIRECT RGBA") &&
                    screen.ContainsText("CHUNKED RGB"),
                TimeSpan.FromSeconds(3),
                "direct and chunked scenario rendered")
            .Type("6")
            .WaitUntil(
                screen =>
                    screen.ContainsText("Scrolling and placement anchors") &&
                    screen.ContainsText("STATE: BEFORE SCROLL"),
                TimeSpan.FromSeconds(3),
                "scrolling scenario before state rendered")
            .Type("r")
            .WaitUntil(
                screen =>
                    screen.ContainsText("STATE: AFTER SIX SCROLLS") &&
                    screen.ContainsText("R toggles BEFORE / AFTER"),
                TimeSpan.FromSeconds(3),
                "scrolling scenario after state rendered")
            .Capture("kgp-validation-scrolling-after")
            .Type("q")
            .Build()
            .ApplyWithCaptureAsync(
                terminal,
                TestContext.Current.CancellationToken);

        await runTask.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);
        Assert.AreEqual(5, workload.CurrentScenarioIndex);
        Assert.AreEqual(1, workload.CurrentScenarioVariant);
        Assert.AreEqual(1, snapshot.KgpImages.Count);
        AssertPosition(snapshot, 5201, 1, row: 12, column: 8);
    }

    [TestMethod]
    public void SourceTree_DoesNotUseHex1bAppOrWidgets()
    {
        var sampleDirectory = Path.Combine(
            FindRepositoryRoot(),
            "samples",
            "KgpValidation");

        foreach (var file in Directory.GetFiles(
                     sampleDirectory,
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("WithHex1bApp(", source, file);
            Assert.DoesNotContain("new Hex1bApp", source, file);
            Assert.DoesNotContain("using Hex1b.Widgets", source, file);
        }
    }

    private static Hex1bTerminal CreateTerminal(
        IHex1bTerminalWorkloadAdapter workload)
        => Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(KgpCapabilities)
            .WithDimensions(100, 32)
            .Build();

    private static void ApplyFrame(
        Hex1bTerminal terminal,
        IReadOnlyList<KgpValidationScenario> scenarios,
        int scenarioIndex,
        bool enterAlternateScreen,
        int variant = 0)
    {
        var frame = KgpValidationFrameRenderer.Render(
            scenarios[scenarioIndex],
            scenarioIndex,
            scenarios.Count,
            width: 100,
            height: 32,
            enterAlternateScreen,
            variant);
        terminal.ApplyTokens(
            AnsiTokenizer.Tokenize(Encoding.UTF8.GetString(frame)));
    }

    private static void AssertScenarioGeometry(
        string scenarioId,
        Hex1bTerminalSnapshot snapshot)
    {
        switch (scenarioId)
        {
            case "shared-replacement":
                var replacement = Placement(snapshot, 2202, 7);
                Assert.AreEqual(17, replacement.Row);
                Assert.AreEqual(57, replacement.Column);
                break;

            case "source-crop":
                var full = Placement(snapshot, 3201, 1);
                Assert.AreEqual(0u, full.SourceX);
                Assert.AreEqual(0u, full.SourceY);
                Assert.AreEqual(80u, full.SourceWidth);
                Assert.AreEqual(60u, full.SourceHeight);
                AssertCrop(snapshot, 2, sourceX: 0, sourceY: 0);
                AssertCrop(snapshot, 3, sourceX: 40, sourceY: 0);
                AssertCrop(snapshot, 4, sourceX: 0, sourceY: 30);
                AssertCrop(snapshot, 5, sourceX: 40, sourceY: 30);
                break;

            case "z-order":
                Assert.AreEqual(-2, Placement(snapshot, 4201, 1).ZIndex);
                Assert.AreEqual(2, Placement(snapshot, 4202, 1).ZIndex);
                break;

            case "scrolling":
                var scrolled = Placement(snapshot, 5201, 1);
                Assert.AreEqual(18, scrolled.Row);
                Assert.AreEqual(8, scrolled.Column);
                break;

            case "unicode-placeholder":
                var fragments = snapshot.KgpPlacements
                    .OrderBy(placement => placement.Row)
                    .ToList();
                TestSeq.AreEqual(new[] { 11, 12, 13 }, fragments.Select(p => p.Row));
                Assert.IsTrue(fragments.All(p => p.Column == 11));
                Assert.IsTrue(fragments.All(p => p.PlacementId == 7));
                break;

            case "relative-placement":
                AssertPosition(snapshot, 7201, 1, row: 10, column: 7);
                AssertPosition(snapshot, 7202, 2, row: 14, column: 17);
                AssertPosition(snapshot, 7203, 3, row: 12, column: 15);
                break;

            case "deletion-reuse":
                AssertPosition(snapshot, 9201, 3, row: 11, column: 59);
                break;
        }
    }

    private static void AssertCrop(
        Hex1bTerminalSnapshot snapshot,
        uint placementId,
        uint sourceX,
        uint sourceY)
    {
        var placement = Placement(snapshot, 3201, placementId);
        Assert.AreEqual(sourceX, placement.SourceX);
        Assert.AreEqual(sourceY, placement.SourceY);
        Assert.AreEqual(40u, placement.SourceWidth);
        Assert.AreEqual(30u, placement.SourceHeight);
    }

    private static void AssertPosition(
        Hex1bTerminalSnapshot snapshot,
        uint imageId,
        uint placementId,
        int row,
        int column)
    {
        var placement = Placement(snapshot, imageId, placementId);
        Assert.AreEqual(row, placement.Row);
        Assert.AreEqual(column, placement.Column);
    }

    private static KgpPlacement Placement(
        Hex1bTerminalSnapshot snapshot,
        uint imageId,
        uint placementId)
        => TestSeq.Single(snapshot.KgpPlacements.Where(
            placement =>
                placement.ImageId == imageId &&
                placement.PlacementId == placementId));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(
                    directory.FullName,
                    "samples",
                    "KgpValidation")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not find the Hex1b repository root.");
    }

    private sealed class PassiveWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(
            CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ResizeAsync(
            int width,
            int height,
            CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
