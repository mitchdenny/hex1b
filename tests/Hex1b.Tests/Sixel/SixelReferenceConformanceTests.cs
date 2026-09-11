using System.Reflection;
using System.Text;
using System.Text.Json;
using Hex1b.Sixel;
using Hex1b.Surfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.Sixel;

[TestClass]
[TestCategory("SixelConformance")]
public class SixelReferenceConformanceTests
{
    private static readonly SixelReferenceCorpus Corpus = LoadCorpus();

    public static IEnumerable<object[]> CorpusCaseProfiles
        => Corpus.Cases.SelectMany(
            corpusCase => corpusCase.Profiles.Select(
                profile => new object[] { corpusCase.Id, profile.Reference }));

    [TestMethod]
    [DynamicData(nameof(CorpusCaseProfiles))]
    public async Task CorpusCases_MatchNormalizedReferenceOutcomes(
        string caseId,
        string referenceId)
    {
        var corpusCase = Corpus.Cases.Single(item => item.Id == caseId);
        var profile = corpusCase.Profiles.Single(item => item.Reference == referenceId);
        var wireBytes = BuildWireBytes(corpusCase);
        var baseline = await RunAsync(
            corpusCase,
            ResolvePolicy(referenceId),
            wireBytes,
            [wireBytes.Length]);

        AssertExpected(profile.Expected, baseline, $"{caseId}/{referenceId}");

        if (corpusCase.Chunking == "every-boundary")
        {
            for (var split = 1; split < wireBytes.Length; split++)
            {
                var actual = await RunAsync(
                    corpusCase,
                    ResolvePolicy(referenceId),
                    wireBytes,
                    [split, wireBytes.Length - split]);
                Assert.AreEqual(baseline, actual, $"{caseId}/{referenceId} split={split}");
            }

            var singleByte = await RunAsync(
                corpusCase,
                ResolvePolicy(referenceId),
                wireBytes,
                Enumerable.Repeat(1, wireBytes.Length).ToArray());
            Assert.AreEqual(baseline, singleByte, $"{caseId}/{referenceId} single-byte chunks");
        }
    }

    [TestMethod]
    public void CorpusManifest_HasFiniteReferencesAndCompleteTerminalContractCoverage()
    {
        Assert.AreEqual(1, Corpus.SchemaVersion);
        Assert.AreEqual(1, Corpus.NormalizationVersion);
        TestSeq.AreEqual(
            new[] { "dec-vt340", "xterm-411", "wezterm-20240203" },
            Corpus.References.Select(reference => reference.Id));

        string[] requiredAreas =
        [
            "framing-termination-cancellation-chunking",
            "commands-repeat-raster-palette-color-background-aspect",
            "cursor-decsdm-mode-8452-margins-origin-clipping-scroll",
            "placement-content-lifetime-dedup-damage-erase-reset-alternate-screen",
            "scrollback-history-resize-reflow",
            "malformed-truncated-limit-recovery",
            "native-byte-exact-passthrough",
            "snapshots-recording-internal-hmp1-reconnect",
        ];
        TestSeq.AreEqual(
            requiredAreas,
            Corpus.ContractCoverage.Select(item => item.Area));

        foreach (var item in Corpus.ContractCoverage)
            Assert.IsNotEmpty(item.Evidence, item.Area);

        string[] allowedClassifications =
        [
            "hex1b-defect",
            "reference-match",
            "documented-reference-difference",
            "implementation-defined-ambiguous",
            "unsupported-reference-behavior",
        ];
        foreach (var profile in Corpus.Cases.SelectMany(corpusCase => corpusCase.Profiles))
        {
            Assert.Contains(
                profile.Classification,
                allowedClassifications,
                $"{profile.Reference} has an unknown classification.");
        }

        var wezTermPaletteCase = Corpus.Cases.Single(item => item.Id == "unmodified-palette-register");
        Assert.Contains("5046fc225992db6ba2ef8812743fadfdfe4b184a", wezTermPaletteCase.SourceEvidence);
        Assert.Contains("term/src/terminalstate/mod.rs", wezTermPaletteCase.SourceEvidence);
    }

    [TestMethod]
    public void IgnoredSixelTests_AreExplicitlyOutsideTerminalScope()
    {
        var ignoredMethods = typeof(SixelReferenceConformanceTests).Assembly
            .GetTypes()
            .Where(type => type.FullName?.Contains("Sixel", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(method => method.GetCustomAttribute<IgnoreAttribute>() is not null)
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var explained = Corpus.IgnoredTests
            .Select(item => item.Test)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        TestSeq.AreEqual(explained, ignoredMethods);
        foreach (var item in Corpus.IgnoredTests)
        {
            Assert.AreEqual("widget-emitter", item.Scope);
            Assert.Contains("outside terminal-side issue #457", item.Reason);
        }
    }

    private static async Task<NormalizedOutcome> RunAsync(
        SixelReferenceCase corpusCase,
        SixelCompatibilityPolicy policy,
        byte[] wireBytes,
        IReadOnlyList<int> chunkSizes)
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 20,
            height: 10,
            cellPixelWidth: corpusCase.CellPixelWidth,
            cellPixelHeight: corpusCase.CellPixelHeight,
            policy: policy);

        await terminal.FeedAsync(
            wireBytes,
            chunkSizes,
            TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot =>
                (corpusCase.Suffix == ""
                    ? snapshot.ContainsSixelData()
                    : Enumerable.Range(0, snapshot.Height)
                        .Any(row => snapshot.GetLine(row).Contains('X'))),
            $"{corpusCase.Id} completion marker",
            TestContext.Current.CancellationToken);

        TestSeq.AreEqual(wireBytes, terminal.PresentationBytes);
        using var snapshot = terminal.Terminal.CreateSnapshot();
        var placement = snapshot.SixelPlacements.Count == 0
            ? null
            : snapshot.SixelPlacements[0];
        var pixels = placement?.GetVisiblePixels();
        var extents = placement?.Image.Extents;

        return new NormalizedOutcome(
            snapshot.SixelPlacements.Count,
            placement?.Row,
            placement?.Column,
            placement?.WidthInCells,
            placement?.HeightInCells,
            extents?.Logical.Width,
            extents?.Logical.Height,
            extents?.Rendered.Width,
            extents?.Rendered.Height,
            snapshot.CursorY,
            snapshot.CursorX,
            pixels is null ? null : FormatColor(pixels[0, 0]),
            pixels is null ? null : FormatColor(pixels[pixels.Width - 1, pixels.Height - 1]),
            placement?.Image.Outcome.ToString(),
            placement?.Image.RasterStatus.ToString(),
            placement is null
                ? ""
                : string.Join(",", placement.Image.Diagnostics.Select(diagnostic => diagnostic.Code)),
            placement is null
                ? ""
                : string.Join(",", placement.Image.RasterDiagnostics.Select(diagnostic => diagnostic.Code)));
    }

    private static void AssertExpected(
        SixelReferenceExpected expected,
        NormalizedOutcome actual,
        string context)
    {
        AssertOptional(expected.PlacementCount, actual.PlacementCount, context, "placement count");
        AssertOptional(expected.PlacementRow, actual.PlacementRow, context, "placement row");
        AssertOptional(expected.PlacementColumn, actual.PlacementColumn, context, "placement column");
        AssertOptional(expected.PlacementWidthInCells, actual.PlacementWidthInCells, context, "placement width");
        AssertOptional(expected.PlacementHeightInCells, actual.PlacementHeightInCells, context, "placement height");
        AssertOptional(expected.LogicalWidth, actual.LogicalWidth, context, "logical width");
        AssertOptional(expected.LogicalHeight, actual.LogicalHeight, context, "logical height");
        AssertOptional(expected.RenderedWidth, actual.RenderedWidth, context, "rendered width");
        AssertOptional(expected.RenderedHeight, actual.RenderedHeight, context, "rendered height");
        AssertOptional(expected.CursorRow, actual.CursorRow, context, "cursor row");
        AssertOptional(expected.CursorColumn, actual.CursorColumn, context, "cursor column");
        AssertOptional(expected.FirstPixel, actual.FirstPixel, context, "first pixel");
        AssertOptional(expected.LastPixel, actual.LastPixel, context, "last pixel");
        if (actual.PlacementCount > 0)
        {
            Assert.AreEqual(expected.ParseOutcome ?? "Complete", actual.ParseOutcome, $"{context}: parse outcome");
            Assert.AreEqual(expected.RasterStatus ?? "Rasterized", actual.RasterStatus, $"{context}: raster status");
            Assert.AreEqual(expected.Diagnostics ?? "", actual.Diagnostics, $"{context}: diagnostics");
            Assert.AreEqual(
                expected.RasterDiagnostics ?? "",
                actual.RasterDiagnostics,
                $"{context}: raster diagnostics");
        }
    }

    private static void AssertOptional<T>(T? expected, T? actual, string context, string field)
    {
        if (expected is not null)
            Assert.AreEqual(expected, actual, $"{context}: {field}");
    }

    private static byte[] BuildWireBytes(SixelReferenceCase corpusCase)
    {
        var fixture = SixelFixture.Load(corpusCase.Fixture, corpusCase.Id);
        using var stream = new MemoryStream();
        WriteAscii(stream, corpusCase.Prefix);
        if (corpusCase.Framing == "C1")
        {
            stream.WriteByte(0x90);
            stream.Write(fixture.Payload);
            stream.WriteByte(0x9C);
        }
        else
        {
            stream.Write([0x1B, (byte)'P']);
            stream.Write(fixture.Payload);
            stream.Write([0x1B, (byte)'\\']);
        }

        WriteAscii(stream, corpusCase.Suffix ?? "X");
        return stream.ToArray();
    }

    private static void WriteAscii(Stream stream, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            stream.Write(Encoding.ASCII.GetBytes(value));
    }

    private static SixelCompatibilityPolicy ResolvePolicy(string referenceId)
        => referenceId switch
        {
            "dec-vt340" => SixelCompatibilityPolicy.DecVt340,
            "xterm-411" => SixelCompatibilityPolicy.Xterm411,
            "wezterm-20240203" => SixelCompatibilityPolicy.WezTerm20240203,
            _ => throw new InvalidOperationException($"Unknown Sixel reference profile '{referenceId}'."),
        };

    private static string FormatColor(Rgba32 color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

    private static SixelReferenceCorpus LoadCorpus()
    {
        const string suffix = ".TestData.Sixel.Conformance.terminal-reference-matrix.json";
        var assembly = typeof(SixelReferenceConformanceTests).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        return JsonSerializer.Deserialize<SixelReferenceCorpus>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The Sixel reference corpus is empty.");
    }

    private sealed record NormalizedOutcome(
        int PlacementCount,
        int? PlacementRow,
        int? PlacementColumn,
        int? PlacementWidthInCells,
        int? PlacementHeightInCells,
        int? LogicalWidth,
        int? LogicalHeight,
        int? RenderedWidth,
        int? RenderedHeight,
        int CursorRow,
        int CursorColumn,
        string? FirstPixel,
        string? LastPixel,
        string? ParseOutcome,
        string? RasterStatus,
        string Diagnostics,
        string RasterDiagnostics);

    private sealed record SixelReferenceCorpus(
        int SchemaVersion,
        int NormalizationVersion,
        IReadOnlyList<SixelReference> References,
        IReadOnlyList<SixelReferenceCase> Cases,
        IReadOnlyList<SixelContractCoverage> ContractCoverage,
        IReadOnlyList<SixelIgnoredTest> IgnoredTests);

    private sealed record SixelReference(
        string Id,
        string Name,
        string Version,
        string Provenance,
        string CaptureKind,
        string? SourceSha256);

    private sealed record SixelReferenceCase(
        string Id,
        string Fixture,
        string Framing,
        string? Chunking,
        int CellPixelWidth,
        int CellPixelHeight,
        string? Prefix,
        string? Suffix,
        string? SourceEvidence,
        IReadOnlyList<SixelReferenceProfile> Profiles);

    private sealed record SixelReferenceProfile(
        string Reference,
        string Classification,
        SixelReferenceExpected Expected);

    private sealed record SixelReferenceExpected(
        int? PlacementCount,
        int? PlacementRow,
        int? PlacementColumn,
        int? PlacementWidthInCells,
        int? PlacementHeightInCells,
        int? LogicalWidth,
        int? LogicalHeight,
        int? RenderedWidth,
        int? RenderedHeight,
        int? CursorRow,
        int? CursorColumn,
        string? FirstPixel,
        string? LastPixel,
        string? ParseOutcome,
        string? RasterStatus,
        string? Diagnostics,
        string? RasterDiagnostics);

    private sealed record SixelContractCoverage(
        string Area,
        IReadOnlyList<string> Evidence);

    private sealed record SixelIgnoredTest(
        string Test,
        string Scope,
        string Reason);
}
