using System.Text;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests.Sixel;

[TestClass]
public class SixelGrammarParserTests
{
    [TestMethod]
    public void Process_AllGrammarCommandsAtEverySplit_ProducesEquivalentResult()
    {
        var bytes = Encoding.ASCII.GetBytes(
            "\x1bP2;1;9q\"1;1;2;3#4;2;100;0;0@?$!2~-#4\x1b\\");
        var baseline = ParseFramed(bytes).SixelResult;

        for (var split = 0; split <= bytes.Length; split++)
        {
            var parser = new DcsByteStreamParser();
            var frames = new List<DcsFrame>();
            frames.AddRange(parser.Process(bytes.AsSpan(0, split)).Frames.Select(x => x.Frame));
            frames.AddRange(parser.Process(bytes.AsSpan(split)).Frames.Select(x => x.Frame));
            frames.AddRange(parser.Complete().Frames.Select(x => x.Frame));

            var actual = TestSeq.Single(frames).SixelResult;
            Assert.AreEqual(Fingerprint(baseline), Fingerprint(actual), $"split {split}");
        }

        Assert.AreEqual(SixelParseOutcome.Complete, baseline.Outcome);
        Assert.AreEqual(new SixelPoint(0, 6), baseline.GraphicsCursor);
        Assert.AreEqual(new SixelPoint(2, 6), baseline.MaximumCommandOrDataPosition);
        Assert.AreEqual(new SixelExtent(2, 3), baseline.DeclaredExtent);
        Assert.AreEqual(new SixelExtent(2, 6), baseline.DataExtent);
        Assert.AreEqual(new SixelBounds(0, 0, 2, 6), baseline.PaintedBounds);
        Assert.AreEqual(new SixelExtent(2, 6), baseline.LogicalCanvasExtent);
    }

    [TestMethod]
    public void Framing_StandardAndC1ProduceEquivalentGrammarResult()
    {
        var payload = Encoding.ASCII.GetBytes(
            "2;1;9q\"1;1;2;3#4;2;100;0;0@?$!2~-#4");
        var standard = ParseFramed([0x1b, (byte)'P', .. payload, 0x1b, (byte)'\\']);
        var c1 = ParseFramed([0x90, .. payload, 0x9c]);

        Assert.AreEqual(
            Fingerprint(standard.SixelResult),
            Fingerprint(c1.SixelResult));
    }

    [TestMethod]
    [DataRow("", 2)]
    [DataRow("0", 2)]
    [DataRow("1", 2)]
    [DataRow("2", 5)]
    [DataRow("3", 3)]
    [DataRow("4", 3)]
    [DataRow("5", 2)]
    [DataRow("6", 2)]
    [DataRow("7", 1)]
    [DataRow("8", 1)]
    [DataRow("9", 1)]
    [DataRow("99", 2)]
    public void Header_P1AspectMacro_UsesDecTable(string p1, int verticalScale)
    {
        var result = Parse($"{p1}q@").SixelResult;

        Assert.AreEqual(SixelParseOutcome.Complete, result.Outcome);
        Assert.AreEqual(new SixelAspectRatio(verticalScale, 1), result.Header.AspectRatio);
        Assert.AreEqual(new SixelExtent(1, 6 * verticalScale), result.DataExtent);
    }

    [TestMethod]
    public void Header_EmptyParameters_UseDecDefaultsAndRetainMetadata()
    {
        var result = Parse(";;q@").SixelResult;

        Assert.AreEqual(0, result.Header.PixelAspectMacro);
        Assert.AreEqual(0, result.Header.BackgroundSelection);
        Assert.AreEqual(0, result.Header.HorizontalGridSize);
        Assert.AreEqual(SixelBackgroundMode.Opaque, result.Header.BackgroundMode);
        Assert.AreEqual(new SixelAspectRatio(2, 1), result.Header.AspectRatio);
    }

    [TestMethod]
    public void Header_TransparentAndGridParameters_AreRetained()
    {
        var result = Parse("7;1;42q@").SixelResult;

        Assert.AreEqual(7, result.Header.PixelAspectMacro);
        Assert.AreEqual(1, result.Header.BackgroundSelection);
        Assert.AreEqual(42, result.Header.HorizontalGridSize);
        Assert.AreEqual(SixelBackgroundMode.Transparent, result.Header.BackgroundMode);
    }

    [TestMethod]
    [DataRow("?1q@", (int)SixelParseOutcome.Rejected)]
    [DataRow("1 q@", (int)SixelParseOutcome.Rejected)]
    [DataRow("1p@", (int)SixelParseOutcome.Rejected)]
    [DataRow("1;2;3;4q@", (int)SixelParseOutcome.Malformed)]
    [DataRow("1000000000q@", (int)SixelParseOutcome.Malformed)]
    public void Header_InvalidForms_AreNotAcceptedAsSixel(
        string payload,
        int expected)
    {
        var result = Parse(payload).SixelResult;

        Assert.AreEqual((SixelParseOutcome)expected, result.Outcome);
        Assert.IsNotEmpty(result.Diagnostics);
    }

    [TestMethod]
    public void Data_TransparentColumnsAdvanceWithoutPainting()
    {
        var result = Parse("q??").SixelResult;

        Assert.AreEqual(new SixelPoint(2, 0), result.GraphicsCursor);
        Assert.AreEqual(new SixelPoint(2, 0), result.MaximumCommandOrDataPosition);
        Assert.AreEqual(new SixelExtent(2, 12), result.DataExtent);
        Assert.AreEqual(SixelBounds.Empty, result.PaintedBounds);
        Assert.AreEqual(new SixelExtent(2, 12), result.LogicalCanvasExtent);
    }

    [TestMethod]
    public void CursorCommands_MoveCursorWithoutGrowingDataExtent()
    {
        var result = Parse("7q@-$").SixelResult;

        Assert.AreEqual(new SixelPoint(0, 6), result.GraphicsCursor);
        Assert.AreEqual(new SixelPoint(1, 6), result.MaximumCommandOrDataPosition);
        Assert.AreEqual(new SixelExtent(1, 6), result.DataExtent);
        Assert.AreEqual(new SixelExtent(1, 6), result.LogicalCanvasExtent);
    }

    [TestMethod]
    public void Repeat_ExpandedAndCompressedDataHaveEquivalentGeometryAndEvents()
    {
        var expanded = Parse("7q~~~").SixelResult;
        var repeated = Parse("7q!3~").SixelResult;

        Assert.AreEqual(Fingerprint(expanded), Fingerprint(repeated));
        Assert.AreEqual(3, TestSeq.Single(repeated.Commands).RepeatCount);
    }

    [TestMethod]
    public void Repeat_OmittedAndZeroCountsMeanOne()
    {
        var omitted = Parse("7q!~").SixelResult;
        var zero = Parse("7q!0~").SixelResult;

        Assert.AreEqual(new SixelPoint(1, 0), omitted.GraphicsCursor);
        Assert.AreEqual(Fingerprint(omitted), Fingerprint(zero));
    }

    [TestMethod]
    public void RasterAttributes_OverrideAspectAndDistinguishAllExtents()
    {
        var result = Parse("q\"1;1;1;1~~-~~").SixelResult;

        Assert.AreEqual(new SixelRasterAttributes(1, 1, 1, 1), result.RasterAttributes);
        Assert.AreEqual(new SixelAspectRatio(1, 1), result.Header.AspectRatio);
        Assert.AreEqual(new SixelExtent(1, 1), result.DeclaredExtent);
        Assert.AreEqual(new SixelExtent(2, 12), result.DataExtent);
        Assert.AreEqual(new SixelBounds(0, 0, 2, 12), result.PaintedBounds);
        Assert.AreEqual(new SixelExtent(2, 12), result.LogicalCanvasExtent);
    }

    [TestMethod]
    public void RasterAttributes_DeclaredExtentCanExceedDataAndPaint()
    {
        var result = Parse("q\"1;1;40;7@").SixelResult;

        Assert.AreEqual(new SixelExtent(40, 7), result.DeclaredExtent);
        Assert.AreEqual(new SixelExtent(1, 6), result.DataExtent);
        Assert.AreEqual(new SixelBounds(0, 0, 1, 1), result.PaintedBounds);
        Assert.AreEqual(new SixelExtent(40, 7), result.LogicalCanvasExtent);
    }

    [TestMethod]
    public void RasterAttributes_FractionalAspectUsesOutwardPaintedBounds()
    {
        var result = Parse("q\"1;2;0;0A").SixelResult;

        Assert.AreEqual(new SixelExtent(1, 3), result.DataExtent);
        Assert.AreEqual(new SixelBounds(0, 0, 1, 1), result.PaintedBounds);
    }

    [TestMethod]
    public void RasterAttributes_OmittedTrailingExtentParametersRemainValid()
    {
        var result = Parse("q\"1;1@").SixelResult;

        Assert.AreEqual(SixelParseOutcome.Complete, result.Outcome);
        Assert.AreEqual(new SixelAspectRatio(1, 1), result.Header.AspectRatio);
        Assert.AreEqual(SixelExtent.Empty, result.DeclaredExtent);
        Assert.AreEqual(new SixelExtent(1, 6), result.DataExtent);
    }

    [TestMethod]
    public void RasterAttributes_HugeAspectSaturatesWithoutWrapping()
    {
        var result = Parse("q\"999999999;1;1;1~").SixelResult;

        Assert.AreEqual(SixelParseOutcome.LimitDowngraded, result.Outcome);
        Assert.AreEqual(int.MaxValue, result.DataExtent.Height);
        Assert.IsTrue(result.Diagnostics.Any(
            item => item.Code == SixelDiagnosticCode.GeometrySaturated));
    }

    [TestMethod]
    public void TrackedSixel_DeclaredPixelCanvasIsNotInflatedToFinalBand()
    {
        var store = new TrackedObjectStore();
        var tracked = store.GetOrCreateSixel(
            "\x1bP7q\"1;1;1;20@-@-@-@\x1b\\",
            widthInCells: 1,
            heightInCells: 1);

        Assert.AreEqual(1, tracked.Data.PixelWidth);
        Assert.AreEqual(20, tracked.Data.PixelHeight);

        tracked.Release();
    }

    [TestMethod]
    public void TrackedSixel_CellSpanUsesLogicalCanvasBeyondDeclaredHint()
    {
        var store = new TrackedObjectStore();
        var tracked = store.GetOrCreateSixel(
            "\x1bP7q\"1;1;1;1~~-~~\x1b\\",
            widthInCells: 1,
            heightInCells: 1);

        Assert.AreEqual(
            (2, 12),
            tracked.Data.GetCellSpan(new Hex1b.Surfaces.CellMetrics(1, 1)));

        tracked.Release();
    }

    [TestMethod]
    public void Palette_DefinitionsAndSelectionsAreOrderedAndSelectRegister()
    {
        var result = Parse("q#7;1;120;50;100#8;2;100;25;0#7@").SixelResult;

        Assert.AreEqual(SixelParseOutcome.Complete, result.Outcome);
        Assert.AreEqual(7, result.SelectedColorRegister);
        Assert.HasCount(3, result.PaletteMutations);
        Assert.AreEqual(
            new SixelPaletteCommand(7, SixelColorSpace.Hls, 120, 50, 100),
            result.PaletteMutations[0]);
        Assert.AreEqual(
            new SixelPaletteCommand(8, SixelColorSpace.Rgb, 100, 25, 0),
            result.PaletteMutations[1]);
        Assert.AreEqual(
            new SixelPaletteCommand(7, null, null, null, null),
            result.PaletteMutations[2]);
    }

    [TestMethod]
    public void MetadataOnlySequence_RetainsRasterAndPaletteState()
    {
        var result = Parse("q\"1;1;40;7#9;2;1;2;3").SixelResult;

        Assert.AreEqual(SixelParseOutcome.Complete, result.Outcome);
        Assert.AreEqual(new SixelExtent(40, 7), result.LogicalCanvasExtent);
        Assert.AreEqual(SixelExtent.Empty, result.DataExtent);
        Assert.AreEqual(SixelBounds.Empty, result.PaintedBounds);
        Assert.AreEqual(9, result.SelectedColorRegister);
        Assert.HasCount(1, result.PaletteMutations);
    }

    [TestMethod]
    [DataRow("q!3#1@", (int)SixelDiagnosticCode.ReplacedCommand)]
    [DataRow("q!", (int)SixelDiagnosticCode.IncompleteCommand)]
    [DataRow("q#", (int)SixelDiagnosticCode.InvalidPaletteCommand)]
    [DataRow("q\"1;1;2;3;4", (int)SixelDiagnosticCode.InvalidRasterAttributes)]
    [DataRow("q\u0001@", (int)SixelDiagnosticCode.InvalidByte)]
    public void MalformedCommands_ReportDiagnosticsAndBoundedRecovery(
        string payload,
        int diagnostic)
    {
        var result = Parse(payload).SixelResult;

        Assert.AreEqual(SixelParseOutcome.Malformed, result.Outcome);
        Assert.IsTrue(result.Diagnostics.Any(item => item.Code == (SixelDiagnosticCode)diagnostic));
    }

    [TestMethod]
    public void CancelledSequence_ReportsCancelledOutcome()
    {
        var result = ParseFramed(Encoding.ASCII.GetBytes("\x1bPq@\x18")).SixelResult;

        Assert.AreEqual(SixelParseOutcome.Cancelled, result.Outcome);
    }

    [TestMethod]
    public void UnterminatedSequence_ReportsMalformedOutcome()
    {
        var parser = new DcsByteStreamParser();
        _ = parser.Process("\x1bPq@"u8);

        var result = TestSeq.Single(parser.Complete().Frames).Frame.SixelResult;

        Assert.AreEqual(SixelParseOutcome.Malformed, result.Outcome);
        Assert.IsTrue(result.Diagnostics.Any(
            item => item.Code == SixelDiagnosticCode.UnterminatedSequence));
    }

    [TestMethod]
    public void HugeRepeat_SaturatesGeometryWithoutWrapping()
    {
        var result = Parse("7q!999999999~!999999999~!999999999~").SixelResult;

        Assert.AreEqual(SixelParseOutcome.LimitDowngraded, result.Outcome);
        Assert.AreEqual(int.MaxValue, result.GraphicsCursor.X);
        Assert.AreEqual(int.MaxValue, result.DataExtent.Width);
        Assert.AreEqual(int.MaxValue, result.PaintedBounds.Width);
        Assert.IsTrue(result.Diagnostics.Any(
            item => item.Code == SixelDiagnosticCode.GeometrySaturated));
    }

    [TestMethod]
    public void NumericOverflow_IsBoundedAndDiagnostic()
    {
        var result = Parse("7q!999999999999999999999999~").SixelResult;

        Assert.AreEqual(SixelParseOutcome.LimitDowngraded, result.Outcome);
        Assert.AreEqual(SixelParser.MaximumNumericValue, result.GraphicsCursor.X);
        Assert.IsTrue(result.Diagnostics.Any(
            item => item.Code == SixelDiagnosticCode.NumericLimitExceeded));
        Assert.IsLessThanOrEqualTo(SixelParser.MaximumDiagnosticCount, result.Diagnostics.Count);
    }

    [TestMethod]
    public void Decoder_RepeatedOverprintsBeyondWorkBudget_DowngradesWithoutLooping()
    {
        var payload = "7q" + string.Concat(Enumerable.Repeat("!2000000~$", 6));

        Assert.IsNull(Hex1b.Automation.SixelDecoder.Decode(payload));
    }

    [TestMethod]
    public void Decoder_RawCommandBody_RemainsSupported()
    {
        var image = Hex1b.Automation.SixelDecoder.Decode("#1;2;100;0;0@");

        Assert.IsNotNull(image);
        Assert.AreEqual(1, image.Width);
        Assert.AreEqual(6, image.Height);
        Assert.AreEqual(255, image.Pixels[0]);
    }

    [TestMethod]
    public void RetentionLimitExceeded_ContinuesGeometryWithoutRetainingRasterEvents()
    {
        var result = Parse($"7q{new string('~', 100)}", retentionLimit: 8).SixelResult;

        Assert.AreEqual(SixelParseOutcome.LimitDowngraded, result.Outcome);
        Assert.AreEqual(new SixelPoint(100, 0), result.GraphicsCursor);
        Assert.AreEqual(new SixelExtent(100, 6), result.DataExtent);
        Assert.AreEqual(new SixelBounds(0, 0, 100, 6), result.PaintedBounds);
        Assert.IsFalse(result.CommandsComplete);
    }

    private static DcsFrame Parse(string payload, int retentionLimit = DcsByteStreamParser.DefaultRetentionLimit) =>
        DcsByteStreamParser.ParseCompleteContent(
            Encoding.Latin1.GetBytes(payload),
            retentionLimit);

    private static DcsFrame ParseFramed(byte[] bytes)
    {
        var parser = new DcsByteStreamParser();
        var frames = new List<DcsFrame>();
        frames.AddRange(parser.Process(bytes).Frames.Select(x => x.Frame));
        frames.AddRange(parser.Complete().Frames.Select(x => x.Frame));
        return TestSeq.Single(frames);
    }

    private static string Fingerprint(SixelParseResult result) => string.Join(
        "|",
        result.Header,
        result.RasterAttributes,
        result.GraphicsCursor,
        result.MaximumCommandOrDataPosition,
        result.DeclaredExtent,
        result.DataExtent,
        result.PaintedBounds,
        result.LogicalCanvasExtent,
        result.SelectedColorRegister,
        string.Join(",", result.PaletteMutations),
        string.Join(",", result.Commands),
        result.CommandsComplete,
        result.Outcome,
        string.Join(",", result.Diagnostics));
}
