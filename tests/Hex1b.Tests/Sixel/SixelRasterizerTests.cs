using System.Text;
using Hex1b.Sixel;
using Hex1b.Surfaces;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Focused tests for the deterministic bounded Sixel rasterizer.
/// </summary>
/// <remarks>
/// Every expected raster in this file is authored by hand from the DEC grammar.
/// <c>SixelEncoder</c> is never used to produce an expectation.
/// </remarks>
[TestClass]
public class SixelRasterizerTests
{
    private const string Red = "#1;2;100;0;0";
    private const string Green = "#2;2;0;100;0";
    private const string Blue = "#3;2;0;0;100";

    #region Sixel bit orientation and masks

    [TestMethod]
    [DataRow('?', "K", "K", "K", "K", "K", "K")]
    [DataRow('@', "R", "K", "K", "K", "K", "K")]
    [DataRow('A', "K", "R", "K", "K", "K", "K")]
    [DataRow('C', "K", "K", "R", "K", "K", "K")]
    [DataRow('G', "K", "K", "K", "R", "K", "K")]
    [DataRow('O', "K", "K", "K", "K", "R", "K")]
    [DataRow('_', "K", "K", "K", "K", "K", "R")]
    [DataRow('B', "R", "R", "K", "K", "K", "K")]
    [DataRow('F', "R", "R", "R", "K", "K", "K")]
    [DataRow('N', "R", "R", "R", "R", "K", "K")]
    [DataRow('^', "R", "R", "R", "R", "R", "K")]
    [DataRow('~', "R", "R", "R", "R", "R", "R")]
    [DataRow('D', "R", "K", "R", "K", "K", "K")]
    [DataRow('a', "K", "R", "K", "K", "K", "R")]
    public void DataByte_UsesLeastSignificantBitAsTopPixel(char data, params string[] rows)
    {
        var result = Raster($"0;0q{Red}#1{data}");

        AssertPixels(result, rows);
    }

    [TestMethod]
    public void EverySixBitMask_PaintsExactlyItsSetBits()
    {
        for (var mask = 0; mask < 64; mask++)
        {
            var result = Raster($"0;1q{Red}#1{(char)('?' + mask)}");
            var image = RequireImage(result);
            Assert.AreEqual(1, image.Width, $"mask {mask}");
            Assert.AreEqual(6, image.Height, $"mask {mask}");

            for (var bit = 0; bit < 6; bit++)
            {
                var expected = (mask & (1 << bit)) != 0
                    ? new Rgba32(255, 0, 0, 255)
                    : Rgba32.Transparent;
                Assert.AreEqual(expected, image[0, bit], $"mask {mask} bit {bit}");
            }
        }
    }

    [TestMethod]
    public void SingleBitMasks_MapToTheirBandRowInEveryBand()
    {
        var result = Raster($"0;1q{Red}#1@-A-C-_");
        var image = RequireImage(result);

        Assert.AreEqual(24, image.Height);
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), image[0, 0]);
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), image[0, 7]);
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), image[0, 14]);
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), image[0, 23]);
        Assert.AreEqual(Rgba32.Transparent, image[0, 1]);
        Assert.AreEqual(Rgba32.Transparent, image[0, 6]);
    }

    #endregion

    #region Heights and partial bands

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(10)]
    [DataRow(11)]
    [DataRow(12)]
    [DataRow(13)]
    public void PartialBands_PaintExactlyTheRequestedHeight(int height)
    {
        var bands = ((height - 1) / 6) + 1;
        var body = new StringBuilder();
        for (var band = 0; band < bands; band++)
        {
            if (band > 0)
            {
                body.Append('-');
            }

            var remaining = height - (band * 6);
            var mask = remaining >= 6 ? 63 : (1 << remaining) - 1;
            body.Append((char)('?' + mask));
        }

        var result = Raster($"0;1q\"1;1;1;{height}{Red}#1{body}");
        var image = RequireImage(result);

        Assert.AreEqual(new SixelBounds(0, 0, 1, height), result.Extents.Painted);
        Assert.AreEqual(Math.Max(height, bands * 6), image.Height);
        for (var y = 0; y < image.Height; y++)
        {
            var expected = y < height ? new Rgba32(255, 0, 0, 255) : Rgba32.Transparent;
            Assert.AreEqual(expected, image[0, y], $"row {y}");
        }
    }

    [TestMethod]
    public void PartialFinalBand_KeepsTheExactPaintedHeight()
    {
        var result = Raster($"0;1q\"1;1;1;8{Red}#1~-B");
        var image = RequireImage(result);

        Assert.AreEqual(12, image.Height);
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), image[0, 7]);
        Assert.AreEqual(Rgba32.Transparent, image[0, 8]);
        Assert.AreEqual(new SixelExtent(1, 12), result.Extents.Data);
        Assert.AreEqual(new SixelBounds(0, 0, 1, 8), result.Extents.Painted);
    }

    [TestMethod]
    public void DataBeyondDeclaredExtent_GrowsTheLogicalCanvas()
    {
        var result = Raster($"0;1q\"1;1;1;1{Red}#1~~-~~");

        Assert.AreEqual(new SixelExtent(1, 1), result.Extents.Declared);
        Assert.AreEqual(new SixelExtent(2, 12), result.Extents.Data);
        Assert.AreEqual(new SixelExtent(2, 12), result.Extents.Logical);
        Assert.AreEqual(12, RequireImage(result).Height);
    }

    [TestMethod]
    public void DataSmallerThanDeclaredExtent_KeepsTheDeclaredCanvas()
    {
        var result = Raster($"0;1q\"1;1;4;7{Red}#1@");
        var image = RequireImage(result);

        Assert.AreEqual(new SixelExtent(4, 7), result.Extents.Logical);
        Assert.AreEqual(new SixelExtent(1, 6), result.Extents.Data);
        Assert.AreEqual(new SixelBounds(0, 0, 1, 1), result.Extents.Painted);
        Assert.AreEqual(4, image.Width);
        Assert.AreEqual(7, image.Height);
    }

    #endregion

    #region Overprint, carriage return, and run length

    [TestMethod]
    public void Decgcr_OverprintsTheSameBandWithMultipleColors()
    {
        var result = Raster($"0;1q{Red}{Green}#1@$#2A");

        AssertPixels(
            result,
            "R",
            "G",
            ".",
            ".",
            ".",
            ".");
    }

    [TestMethod]
    public void Decgcr_LastWriteWinsForOverlappingPixels()
    {
        var result = Raster($"0;1q{Red}{Green}#1~$#2@");

        AssertPixels(
            result,
            "G",
            "R",
            "R",
            "R",
            "R",
            "R");
    }

    [TestMethod]
    public void Decgnl_AdvancesOneLogicalBandPerNewline()
    {
        var result = Raster($"0;1q{Red}{Green}#1@-#2@");

        AssertPixels(
            result,
            "R",
            ".",
            ".",
            ".",
            ".",
            ".",
            "G",
            ".",
            ".",
            ".",
            ".",
            ".");
    }

    [TestMethod]
    public void RunLength_MatchesTheExpandedFormExactly()
    {
        var repeated = Raster($"0;0q{Red}#1!5~");
        var expanded = Raster($"0;0q{Red}#1~~~~~");

        AssertImagesEqual(RequireImage(expanded), RequireImage(repeated));
    }

    [TestMethod]
    public void RunLength_OmittedOrZeroCountPaintsOneColumn()
    {
        var omitted = Raster($"0;0q{Red}#1!~");
        var zero = Raster($"0;0q{Red}#1!0~");
        var single = Raster($"0;0q{Red}#1~");

        AssertImagesEqual(RequireImage(single), RequireImage(omitted));
        AssertImagesEqual(RequireImage(single), RequireImage(zero));
    }

    [TestMethod]
    public void RunLength_AcrossMixedColorsMatchesTheExpandedForm()
    {
        var repeated = Raster($"0;1q{Red}{Green}#1!3~#2!2A");
        var expanded = Raster($"0;1q{Red}{Green}#1~~~#2AA");

        AssertImagesEqual(RequireImage(expanded), RequireImage(repeated));
    }

    #endregion

    #region RGB conversion

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 3)]
    [DataRow(20, 51)]
    [DataRow(50, 128)]
    [DataRow(51, 130)]
    [DataRow(80, 204)]
    [DataRow(99, 252)]
    [DataRow(100, 255)]
    public void RgbPercent_UsesNearestRounding(int percent, int expected)
    {
        Assert.AreEqual((byte)expected, SixelColorConverter.PercentToComponent(percent));
    }

    [TestMethod]
    [DataRow(-5, 0)]
    [DataRow(101, 255)]
    [DataRow(999, 255)]
    public void RgbPercent_ClampsToTheDecDomain(int percent, int expected)
    {
        Assert.AreEqual((byte)expected, SixelColorConverter.PercentToComponent(percent));
    }

    [TestMethod]
    public void RgbDefinition_OutOfRangePercentageIsClampedInTheRaster()
    {
        var result = Raster("0;1q#1;2;200;50;0#1@");

        Assert.AreEqual(new Rgba32(255, 128, 0, 255), RequireImage(result)[0, 0]);
    }

    [TestMethod]
    public void RgbDefinition_MidpointProducesTheRoundedComponent()
    {
        var result = Raster("0;1q#1;2;50;50;50#1@");

        Assert.AreEqual(new Rgba32(128, 128, 128, 255), RequireImage(result)[0, 0]);
    }

    #endregion

    #region DEC HLS conversion

    [TestMethod]
    [DataRow(0, 0, 0, 255)]
    [DataRow(120, 255, 0, 0)]
    [DataRow(240, 0, 255, 0)]
    [DataRow(60, 255, 0, 255)]
    [DataRow(180, 255, 255, 0)]
    [DataRow(300, 0, 255, 255)]
    public void Hls_UsesTheDecHueWheelWithBlueAtZero(int hue, int r, int g, int b)
    {
        Assert.AreEqual(
            new Rgba32((byte)r, (byte)g, (byte)b, 255),
            SixelColorConverter.FromHls(hue, 50, 100));
    }

    [TestMethod]
    [DataRow(360, 0, 0, 255)]
    [DataRow(480, 255, 0, 0)]
    [DataRow(720, 0, 0, 255)]
    [DataRow(-120, 0, 255, 0)]
    public void Hls_HueWrapsAroundTheWheel(int hue, int r, int g, int b)
    {
        Assert.AreEqual(
            new Rgba32((byte)r, (byte)g, (byte)b, 255),
            SixelColorConverter.FromHls(hue, 50, 100));
    }

    [TestMethod]
    [DataRow(0, 0, 0, 0)]
    [DataRow(25, 64, 64, 64)]
    [DataRow(50, 128, 128, 128)]
    [DataRow(75, 191, 191, 191)]
    [DataRow(100, 255, 255, 255)]
    public void Hls_ZeroSaturationProducesGray(int lightness, int r, int g, int b)
    {
        Assert.AreEqual(
            new Rgba32((byte)r, (byte)g, (byte)b, 255),
            SixelColorConverter.FromHls(120, lightness, 0));
    }

    [TestMethod]
    public void Hls_LightnessExtremesIgnoreHueAndSaturation()
    {
        Assert.AreEqual(new Rgba32(0, 0, 0, 255), SixelColorConverter.FromHls(120, 0, 100));
        Assert.AreEqual(new Rgba32(255, 255, 255, 255), SixelColorConverter.FromHls(120, 100, 100));
    }

    [TestMethod]
    public void Hls_ClampsSaturationAndLightnessToTheDecDomain()
    {
        Assert.AreEqual(
            SixelColorConverter.FromHls(120, 100, 100),
            SixelColorConverter.FromHls(120, 150, 400));
        Assert.AreEqual(
            SixelColorConverter.FromHls(120, 0, 0),
            SixelColorConverter.FromHls(120, -20, -20));
    }

    [TestMethod]
    public void Hls_HalfSaturationInterpolatesDeterministically()
    {
        Assert.AreEqual(new Rgba32(191, 64, 64, 255), SixelColorConverter.FromHls(120, 50, 50));
        Assert.AreEqual(new Rgba32(64, 64, 191, 255), SixelColorConverter.FromHls(0, 50, 50));
    }

    [TestMethod]
    public void Hls_BoundaryHuesRoundConsistently()
    {
        // 60 degrees either side of a primary reaches the secondary exactly.
        Assert.AreEqual(new Rgba32(255, 0, 255, 255), SixelColorConverter.FromHls(60, 50, 100));
        // One degree inside the ramp is strictly below the full component.
        var nearlyMagenta = SixelColorConverter.FromHls(59, 50, 100);
        Assert.AreEqual((byte)251, nearlyMagenta.R);
        Assert.AreEqual((byte)0, nearlyMagenta.G);
        Assert.AreEqual((byte)255, nearlyMagenta.B);
    }

    [TestMethod]
    public void HlsDefinition_IsAppliedByTheRasterizer()
    {
        var result = Raster("0;1q#3;1;0;50;100#3@");

        Assert.AreEqual(new Rgba32(0, 0, 255, 255), RequireImage(result)[0, 0]);
    }

    #endregion

    #region Color registers

    [TestMethod]
    public void SelectionWithoutDefinition_UsesTheDefaultPalette()
    {
        var result = Raster("0;1q#2@");

        Assert.AreEqual(SixelDefaultPalette.Get(2), RequireImage(result)[0, 0]);
        Assert.AreEqual(new Rgba32(204, 33, 33, 255), SixelDefaultPalette.Get(2));
    }

    [TestMethod]
    public void DefaultPalette_RegisterZeroIsBlack()
    {
        Assert.AreEqual(new Rgba32(0, 0, 0, 255), SixelDefaultPalette.Get(0));
    }

    [TestMethod]
    public void DefaultPalette_ExtendsBeyondTheVt340RegistersWithinPolicy()
    {
        var registers = new SixelColorRegisters();

        Assert.AreEqual(256, registers.Count);
        Assert.IsTrue(registers.IsWithinPolicy(255));
        Assert.IsFalse(registers.IsWithinPolicy(256));
        Assert.IsFalse(registers.IsWithinPolicy(-1));
        Assert.AreEqual(new Rgba32(255, 255, 255, 255), registers.Get(231));
        Assert.AreEqual(new Rgba32(238, 238, 238, 255), registers.Get(255));
    }

    [TestMethod]
    public void DecgciDefinition_AlsoSelectsTheRegister()
    {
        var result = Raster($"0;1q{Red}@");

        Assert.AreEqual(new Rgba32(255, 0, 0, 255), RequireImage(result)[0, 0]);
    }

    [TestMethod]
    public void ColorRegisters_PersistBetweenSequencesOnTheSameEnvironment()
    {
        var environment = SixelRasterEnvironment.CreateDefault();
        _ = SixelRasterizer.Rasterize(SixelParser.ParsePayload($"0;1q{Red}@"), environment);

        var second = SixelRasterizer.Rasterize(SixelParser.ParsePayload("0;1q#1@"), environment);

        Assert.AreEqual(new Rgba32(255, 0, 0, 255), RequireImage(second)[0, 0]);
    }

    [TestMethod]
    public void Prepare_AppliesPersistentPaletteAndCapturesIndependentRasterState()
    {
        var environment = SixelRasterEnvironment.CreateDefault();
        _ = SixelRasterizer.Prepare(
            SixelParser.ParsePayload($"0;1q{Red}"),
            environment);
        var selectRed = SixelParser.ParsePayload("0;1q#1@");
        var preparation = SixelRasterizer.Prepare(selectRed, environment);

        Assert.AreEqual(new Rgba32(255, 0, 0, 255), environment.Registers.Get(1));

        environment.Registers.Reset();
        var raster = SixelRasterizer.Rasterize(selectRed, preparation.Environment);
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), RequireImage(raster)[0, 0]);
    }

    [TestMethod]
    public void Prepare_AppliesDefinitionsBeyondPaletteMutationRetentionLimit()
    {
        var environment = SixelRasterEnvironment.CreateDefault();
        var payload = $"0;1q{string.Concat(Enumerable.Repeat("#1", 4_096))}{Red}";
        var parse = SixelParser.ParsePayload(payload);

        _ = SixelRasterizer.Prepare(parse, environment);

        Assert.AreEqual(SixelParseOutcome.LimitDowngraded, parse.Outcome);
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), environment.Registers.Get(1));
    }

    [TestMethod]
    public void Prepare_AppliesRetainedPaletteDefinitionsAfterCommandRetentionLimit()
    {
        var environment = SixelRasterEnvironment.CreateDefault();
        var commands = string.Concat(Enumerable.Repeat("@A", 32_768));
        var parse = SixelParser.ParsePayload($"0;1q{commands}{Red}");

        _ = SixelRasterizer.Prepare(parse, environment);

        Assert.AreEqual(SixelParseOutcome.LimitDowngraded, parse.Outcome);
        Assert.IsFalse(parse.CommandsComplete);
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), environment.Registers.Get(1));
    }

    [TestMethod]
    public void ColorRegisters_ResetRestoresTheDefaultPalette()
    {
        var environment = SixelRasterEnvironment.CreateDefault();
        _ = SixelRasterizer.Rasterize(SixelParser.ParsePayload($"0;1q{Red}@"), environment);
        environment.Registers.Reset();

        var afterReset = SixelRasterizer.Rasterize(SixelParser.ParsePayload("0;1q#1@"), environment);

        Assert.AreEqual(SixelDefaultPalette.Get(1), RequireImage(afterReset)[0, 0]);
        Assert.AreNotEqual(new Rgba32(255, 0, 0, 255), RequireImage(afterReset)[0, 0]);
    }

    [TestMethod]
    public void ColorRegisters_OutOfPolicyRegisterIsRejectedExplicitly()
    {
        var result = Raster("0;1q#999;2;100;0;0#999@");

        Assert.IsTrue(result.Diagnostics.Any(
            item => item.Code == SixelRasterDiagnosticCode.ColorRegisterOutOfPolicy));
        // The rejected selection never becomes the paint color; register 0 remains selected.
        Assert.AreEqual(SixelDefaultPalette.Get(0), RequireImage(result)[0, 0]);
    }

    [TestMethod]
    public void ColorRegisters_SnapshotIsIndependent()
    {
        var registers = new SixelColorRegisters();
        var snapshot = registers.Snapshot();
        registers.Define(1, new Rgba32(1, 2, 3, 255));

        Assert.AreEqual(SixelDefaultPalette.Get(1), snapshot.Get(1));
        Assert.AreEqual(new Rgba32(1, 2, 3, 255), registers.Get(1));
    }

    #endregion

    #region Background

    [TestMethod]
    public void OpaqueBackground_FillsUnpaintedPixelsAcrossTheDeclaredExtent()
    {
        var environment = EnvironmentWithBackground(new Rgba32(0, 0, 255, 255));
        var result = SixelRasterizer.Rasterize(
            SixelParser.ParsePayload($"0;0q\"1;1;3;6{Red}#1@"),
            environment);

        AssertPixels(
            result,
            "RBB",
            "BBB",
            "BBB",
            "BBB",
            "BBB",
            "BBB");
    }

    [TestMethod]
    public void OpaqueBackground_FillsUnpaintedPixelsWithoutADeclaredExtent()
    {
        var environment = EnvironmentWithBackground(new Rgba32(0, 0, 255, 255));
        var result = SixelRasterizer.Rasterize(
            SixelParser.ParsePayload($"0;0q{Red}#1@@"),
            environment);

        AssertPixels(
            result,
            "RR",
            "BB",
            "BB",
            "BB",
            "BB",
            "BB");
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("2")]
    [DataRow("")]
    public void OpaqueBackgroundSelectors_AreAllOpaque(string p2)
    {
        var environment = EnvironmentWithBackground(new Rgba32(0, 0, 255, 255));
        var result = SixelRasterizer.Rasterize(
            SixelParser.ParsePayload($"0;{p2}q{Red}#1@"),
            environment);

        Assert.AreEqual(SixelBackgroundMode.Opaque, result.BackgroundMode);
        Assert.AreEqual(new Rgba32(0, 0, 255, 255), RequireImage(result)[0, 1]);
    }

    [TestMethod]
    public void TransparentBackground_LeavesUnpaintedPixelsTransparent()
    {
        var environment = EnvironmentWithBackground(new Rgba32(0, 0, 255, 255));
        var result = SixelRasterizer.Rasterize(
            SixelParser.ParsePayload($"0;1q\"1;1;2;2{Red}#1@"),
            environment);

        Assert.AreEqual(SixelBackgroundMode.Transparent, result.BackgroundMode);
        AssertPixels(
            result,
            "R.",
            "..",
            "..",
            "..",
            "..",
            "..");
    }

    [TestMethod]
    public void UnsetTerminalBackground_UsesDeterministicBlack()
    {
        var result = Raster($"0;0q{Red}#1@");

        Assert.AreEqual(new Rgba32(0, 0, 0, 255), result.UnpaintedPixel);
        Assert.AreEqual(new Rgba32(0, 0, 0, 255), RequireImage(result)[0, 1]);
    }

    [TestMethod]
    public void CapturedBackground_IsIndependentOfPaletteRegisterZero()
    {
        var environment = EnvironmentWithBackground(new Rgba32(0, 0, 255, 255));
        var result = SixelRasterizer.Rasterize(
            SixelParser.ParsePayload($"0;0q#0;2;0;100;0{Red}#1@"),
            environment);

        // Register zero is now green, but the captured background stays blue.
        Assert.AreEqual(new Rgba32(0, 0, 255, 255), RequireImage(result)[0, 1]);
    }

    #endregion

    #region Aspect and extents

    [TestMethod]
    [DataRow("", 2, 1)]
    [DataRow("0", 2, 1)]
    [DataRow("1", 2, 1)]
    [DataRow("2", 5, 1)]
    [DataRow("3", 3, 1)]
    [DataRow("4", 3, 1)]
    [DataRow("5", 2, 1)]
    [DataRow("6", 2, 1)]
    [DataRow("7", 1, 1)]
    [DataRow("8", 1, 1)]
    [DataRow("9", 1, 1)]
    public void PixelAspectMacro_FollowsTheDecTable(string p1, int numerator, int denominator)
    {
        var result = Raster($"{p1};1q#1~");

        Assert.AreEqual(new SixelAspectRatio(numerator, denominator), result.Extents.Aspect);
        Assert.AreEqual(6, result.Extents.Logical.Height);
        Assert.AreEqual(6 * numerator / denominator, result.Extents.Rendered.Height);
    }

    [TestMethod]
    [DataRow(1, 1, 6)]
    [DataRow(2, 1, 12)]
    [DataRow(3, 1, 18)]
    [DataRow(1, 2, 3)]
    [DataRow(5, 2, 15)]
    [DataRow(7, 3, 14)]
    public void DecgraPanAndPad_OverrideThePixelAspectMacro(int pan, int pad, int renderedHeight)
    {
        var result = Raster($"2;1q\"{pan};{pad};1;6#1~");

        Assert.AreEqual(new SixelAspectRatio(pan, pad), result.Extents.Aspect);
        Assert.AreEqual(6, result.Extents.Logical.Height);
        Assert.AreEqual(renderedHeight, result.Extents.Rendered.Height);
    }

    [TestMethod]
    public void LogicalStorage_IsNeverResampledByTheAspectRatio()
    {
        var square = Raster($"7;1q{Red}#1~");
        var tall = Raster($"2;1q{Red}#1~");

        AssertImagesEqual(RequireImage(square), RequireImage(tall));
        Assert.AreEqual(6, RequireImage(tall).Height);
        Assert.AreEqual(30, tall.Extents.Rendered.Height);
    }

    [TestMethod]
    public void DecgraPhAndPv_AreHorizontalThenVertical()
    {
        var result = Raster("0;1q\"1;1;9;4#1@");

        Assert.AreEqual(new SixelExtent(9, 4), result.Extents.Declared);
        Assert.AreEqual(9, RequireImage(result).Width);
        Assert.AreEqual(6, RequireImage(result).Height);
        Assert.AreEqual(new SixelExtent(9, 6), result.Extents.Logical);
    }

    [TestMethod]
    public void Extents_ArePreservedIndependentlyOfEachOther()
    {
        var result = Raster($"2;1q\"1;1;10;3{Red}#1!4A");

        Assert.AreEqual(new SixelExtent(10, 3), result.Extents.Declared);
        Assert.AreEqual(new SixelExtent(4, 6), result.Extents.Data);
        Assert.AreEqual(new SixelBounds(0, 1, 4, 1), result.Extents.Painted);
        Assert.AreEqual(new SixelExtent(10, 6), result.Extents.Logical);
        Assert.AreEqual(new SixelExtent(10, 6), result.Extents.Rendered);
    }

    #endregion

    #region Bounded storage and explicit degradation

    [TestMethod]
    public void HugeTransparentDeclaration_DoesNotAllocateProportionally()
    {
        var result = Raster("0;1q\"1;1;4000;4000#1@");
        var image = RequireImage(result);

        Assert.AreEqual(4000, image.Width);
        Assert.AreEqual(4000, image.Height);
        Assert.AreEqual(16_000_000, image.PixelCount);
        Assert.AreEqual(1, image.AllocatedTileCount);
    }

    [TestMethod]
    public void HugeOpaqueDeclaration_DoesNotAllocateProportionally()
    {
        var result = Raster("0;0q\"1;1;4000;4000#1@");
        var image = RequireImage(result);

        Assert.AreEqual(new Rgba32(0, 0, 0, 255), image[3999, 3999]);
        Assert.AreEqual(1, image.AllocatedTileCount);
    }

    [TestMethod]
    public void ExtentBeyondPolicy_ReturnsGeometryOnlyAndPreservesGeometry()
    {
        var result = Raster("0;1q\"1;1;999999999;999999999#1@");

        Assert.AreEqual(SixelRasterStatus.GeometryOnly, result.Status);
        Assert.IsNull(result.Image);
        Assert.AreEqual(new SixelExtent(999999999, 999999999), result.Extents.Declared);
        Assert.AreEqual(new SixelExtent(999999999, 999999999), result.Extents.Logical);
        Assert.IsTrue(result.Diagnostics.Any(
            item => item.Code == SixelRasterDiagnosticCode.RasterPixelLimitExceeded));
    }

    [TestMethod]
    public void ExtentOverflow_IsDetectedWithoutWrapping()
    {
        var result = Raster("7;1q" + string.Concat(Enumerable.Repeat("!999999999~", 3)));

        Assert.AreEqual(SixelRasterStatus.GeometryOnly, result.Status);
        Assert.AreEqual(int.MaxValue, result.Extents.Logical.Width);
        Assert.IsTrue(result.Extents.Logical.Height > 0);
        Assert.IsTrue(result.Diagnostics.Any(
            item => item.Code == SixelRasterDiagnosticCode.RasterExtentOverflow));
    }

    [TestMethod]
    public void RasterOperationBudget_ReturnsGeometryOnly()
    {
        var result = Raster("7;1q" + string.Concat(Enumerable.Repeat("!2000000~$", 6)));

        Assert.AreEqual(SixelRasterStatus.GeometryOnly, result.Status);
        Assert.IsTrue(result.Diagnostics.Any(
            item => item.Code == SixelRasterDiagnosticCode.RasterOperationLimitExceeded));
        Assert.AreEqual(2_000_000, result.Extents.Logical.Width);
    }

    [TestMethod]
    public void SparseTileBudget_ReturnsGeometryOnlyInsteadOfAPartialRaster()
    {
        var policy = SixelCompatibilityPolicy.Default with
        {
            MaximumRasterTiles = 1,
        };
        var result = SixelRasterizer.Rasterize(
            SixelParser.ParsePayload("7;1q!65~"),
            new SixelRasterEnvironment(
                policy.DefaultBackground,
                new SixelColorRegisters(policy),
                policy));

        Assert.AreEqual(SixelRasterStatus.GeometryOnly, result.Status);
        Assert.IsNull(result.Image);
        Assert.AreEqual(new SixelExtent(65, 6), result.Extents.Logical);
        Assert.IsTrue(result.Diagnostics.Any(
            item => item.Code == SixelRasterDiagnosticCode.RasterTileLimitExceeded));
    }

    [TestMethod]
    public void GeometryOnly_StillAppliesPaletteDefinitions()
    {
        var environment = SixelRasterEnvironment.CreateDefault();
        var refused = SixelRasterizer.Rasterize(
            SixelParser.ParsePayload($"0;1q\"1;1;999999999;999999999{Red}#1@"),
            environment);

        Assert.AreEqual(SixelRasterStatus.GeometryOnly, refused.Status);
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), environment.Registers.Get(1));
    }

    [TestMethod]
    public void MalformedSequence_ReturnsGeometryOnlyWithAnExplicitDiagnostic()
    {
        var result = Raster("0;1q#");

        Assert.AreEqual(SixelRasterStatus.GeometryOnly, result.Status);
        Assert.IsTrue(result.Diagnostics.Any(
            item => item.Code == SixelRasterDiagnosticCode.ParseOutcomeNotRasterable));
    }

    [TestMethod]
    public void SequenceWithoutExtent_ReturnsGeometryOnly()
    {
        var result = Raster("0;1q");

        Assert.AreEqual(SixelRasterStatus.GeometryOnly, result.Status);
        Assert.IsTrue(result.Diagnostics.Any(
            item => item.Code == SixelRasterDiagnosticCode.NoRasterableExtent));
    }

    #endregion

    #region Determinism, equivalence, and materialization

    [TestMethod]
    public void Rasterizing_TheSameParseResultTwice_ProducesEqualPixels()
    {
        var parse = SixelParser.ParsePayload($"0;0q\"1;1;5;5{Red}{Green}#1!3~$#2A");
        var first = SixelRasterizer.Rasterize(parse, SixelRasterEnvironment.CreateDefault());
        var second = SixelRasterizer.Rasterize(parse, SixelRasterEnvironment.CreateDefault());

        AssertImagesEqual(RequireImage(first), RequireImage(second));
        TestSeq.AreEqual(first.Identity, second.Identity);
    }

    [TestMethod]
    public void Materialize_IsRepeatable()
    {
        var image = RequireImage(Raster($"0;0q\"1;1;4;4{Red}#1!2~"));

        var first = image.Materialize();
        var second = image.Materialize();

        Assert.AreEqual(first.Width, second.Width);
        Assert.AreEqual(first.Height, second.Height);
        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                Assert.AreEqual(first[x, y], second[x, y], $"({x},{y})");
            }
        }
    }

    [TestMethod]
    public void SparseLookupAndDenseMaterialization_Agree()
    {
        var image = RequireImage(Raster($"0;0q\"1;1;70;70{Red}{Green}#1!65~-#2!3A"));
        var dense = image.Materialize();

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                Assert.AreEqual(image[x, y], dense[x, y], $"({x},{y})");
            }
        }
    }

    [TestMethod]
    public void SixelData_GetPixels_IsCachedAndRepeatable()
    {
        var payload = $"\x1bP0;0q\"1;1;3;3{Red}#1@\x1b\\";
        var store = new TrackedObjectStore();
        var tracked = store.GetOrCreateSixel(payload, 1, 1);

        var first = tracked.Data.GetPixels();
        var second = tracked.Data.GetPixels();

        Assert.IsNotNull(first);
        Assert.AreSame(first, second);
        Assert.AreEqual(3, first.Width);
        Assert.AreEqual(6, first.Height);
        Assert.AreEqual(new Rgba32(255, 0, 0, 255), first[0, 0]);
        Assert.AreEqual(new Rgba32(0, 0, 0, 255), first[2, 5]);

        tracked.Release();
    }

    [TestMethod]
    public async Task SixelData_GetPixels_ConcurrentCallersShareOneMaterialization()
    {
        var payload = $"\x1bP0;0q\"1;1;70;70{Red}#1!70~\x1b\\";
        var parse = SixelParser.ParsePayload(payload);
        var store = new TrackedObjectStore();
        var tracked = store.GetOrCreateSixel(
            payload,
            1,
            1,
            parse,
            SixelRasterizer.Prepare(parse, SixelRasterEnvironment.CreateDefault()));
        var calls = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(tracked.Data.GetPixels))
            .ToArray();

        var results = await Task.WhenAll(calls).WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.IsNotNull(results[0]);
        foreach (var result in results)
        {
            Assert.AreSame(results[0], result);
        }

        tracked.Release();
    }

    [TestMethod]
    public void SixelData_GetPixels_ReturnsNullForGeometryOnlyResults()
    {
        var store = new TrackedObjectStore();
        var tracked = store.GetOrCreateSixel(
            "\x1bP0;1q\"1;1;999999999;999999999#1@\x1b\\",
            1,
            1);

        Assert.IsNull(tracked.Data.GetPixels());
        Assert.AreEqual(SixelRasterStatus.GeometryOnly, tracked.Data.Raster.Status);

        tracked.Release();
    }

    [TestMethod]
    public void TrackedSixel_IdenticalPayloadWithDifferentRasterState_IsNotReused()
    {
        var payload = "\x1bP0;0q#1@\x1b\\";
        var parse = SixelParser.ParsePayload(payload);
        var store = new TrackedObjectStore();

        var onBlack = store.GetOrCreateSixel(
            payload,
            1,
            1,
            parse,
            SixelRasterizer.Prepare(
                parse,
                EnvironmentWithBackground(new Rgba32(0, 0, 0, 255))));
        var onBlue = store.GetOrCreateSixel(
            payload,
            1,
            1,
            parse,
            SixelRasterizer.Prepare(
                parse,
                EnvironmentWithBackground(new Rgba32(0, 0, 255, 255))));

        Assert.AreNotSame(onBlack, onBlue);
        Assert.AreEqual(new Rgba32(0, 0, 0, 255), onBlack.Data.GetPixels()![0, 1]);
        Assert.AreEqual(new Rgba32(0, 0, 255, 255), onBlue.Data.GetPixels()![0, 1]);

        onBlack.Release();
        onBlue.Release();
    }

    [TestMethod]
    public void TrackedSixel_IdenticalPayloadAndRasterState_IsReused()
    {
        var payload = "\x1bP0;0q#1@\x1b\\";
        var parse = SixelParser.ParsePayload(payload);
        var store = new TrackedObjectStore();
        var environment = EnvironmentWithBackground(new Rgba32(0, 0, 0, 255));

        var first = store.GetOrCreateSixel(
            payload,
            1,
            1,
            parse,
            SixelRasterizer.Prepare(
                parse,
                environment));
        var second = store.GetOrCreateSixel(
            payload,
            1,
            1,
            parse,
            SixelRasterizer.Prepare(
                parse,
                environment));

        Assert.AreSame(first, second);

        first.Release();
        second.Release();
    }

    [TestMethod]
    public void TrackedSixel_PreDefinitionPaletteUseProducesDistinctIdentity()
    {
        var payload = $"\x1bP0;1q#1@{Red}@\x1b\\";
        var parse = SixelParser.ParsePayload(payload);
        var store = new TrackedObjectStore();
        var environment = SixelRasterEnvironment.CreateDefault();

        var first = store.GetOrCreateSixel(
            payload,
            1,
            1,
            parse,
            SixelRasterizer.Prepare(parse, environment));
        var second = store.GetOrCreateSixel(
            payload,
            1,
            1,
            parse,
            SixelRasterizer.Prepare(parse, environment));

        Assert.AreNotSame(first, second);

        first.Release();
        second.Release();
    }

    [TestMethod]
    public void AutomationDecoder_DelegatesToTheAuthoritativeRasterizer()
    {
        var payload = $"0;0q\"1;1;2;2{Red}#1@";
        var image = Hex1b.Automation.SixelDecoder.Decode(payload);
        var raster = RequireImage(Raster(payload));

        Assert.IsNotNull(image);
        Assert.AreEqual(raster.Width, image.Width);
        Assert.AreEqual(raster.Height, image.Height);
        for (var y = 0; y < raster.Height; y++)
        {
            for (var x = 0; x < raster.Width; x++)
            {
                var index = ((y * raster.Width) + x) * 4;
                var expected = raster[x, y];
                Assert.AreEqual(expected.R, image.Pixels[index], $"({x},{y}).R");
                Assert.AreEqual(expected.G, image.Pixels[index + 1], $"({x},{y}).G");
                Assert.AreEqual(expected.B, image.Pixels[index + 2], $"({x},{y}).B");
                Assert.AreEqual(expected.A, image.Pixels[index + 3], $"({x},{y}).A");
            }
        }
    }

    [TestMethod]
    public void AutomationDecoder_ReturnsNullOnlyForDocumentedDegradation()
    {
        Assert.IsNull(Hex1b.Automation.SixelDecoder.Decode(""));
        Assert.IsNull(Hex1b.Automation.SixelDecoder.Decode("0;1q#"));
        Assert.IsNull(Hex1b.Automation.SixelDecoder.Decode("0;1q\"1;1;999999999;999999999#1@"));
        Assert.IsNotNull(Hex1b.Automation.SixelDecoder.Decode($"0;1q{Red}#1@"));
    }

    #endregion

    #region Helpers

    private static SixelRasterResult Raster(string payload) =>
        SixelRasterizer.Rasterize(
            SixelParser.ParsePayload(payload),
            SixelRasterEnvironment.CreateDefault());

    private static SixelRasterEnvironment EnvironmentWithBackground(Rgba32 background) =>
        new(background, new SixelColorRegisters(), SixelCompatibilityPolicy.Default);

    private static SixelRasterImage RequireImage(SixelRasterResult result)
    {
        Assert.AreEqual(SixelRasterStatus.Rasterized, result.Status);
        Assert.IsNotNull(result.Image);
        return result.Image;
    }

    private static void AssertPixels(SixelRasterResult result, params string[] rows)
    {
        var image = RequireImage(result);
        Assert.AreEqual(rows.Length, image.Height, "raster height");
        Assert.AreEqual(rows[0].Length, image.Width, "raster width");

        var actual = new StringBuilder();
        var expected = new StringBuilder();
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                actual.Append(Symbol(image[x, y]));
                expected.Append(rows[y][x]);
            }

            actual.Append('\n');
            expected.Append('\n');
        }

        Assert.AreEqual(expected.ToString(), actual.ToString());
    }

    private static char Symbol(Rgba32 pixel) => pixel switch
    {
        { A: 0 } => '.',
        { R: 0, G: 0, B: 0, A: 255 } => 'K',
        { R: 255, G: 0, B: 0, A: 255 } => 'R',
        { R: 0, G: 255, B: 0, A: 255 } => 'G',
        { R: 0, G: 0, B: 255, A: 255 } => 'B',
        { R: 255, G: 255, B: 255, A: 255 } => 'W',
        _ => '?',
    };

    private static void AssertImagesEqual(SixelRasterImage expected, SixelRasterImage actual)
    {
        Assert.AreEqual(expected.Width, actual.Width, "width");
        Assert.AreEqual(expected.Height, actual.Height, "height");
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.AreEqual(expected[x, y], actual[x, y], $"({x},{y})");
            }
        }
    }

    #endregion
}
