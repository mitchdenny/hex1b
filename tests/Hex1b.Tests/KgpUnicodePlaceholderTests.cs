using System.Globalization;
using System.Text;
using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Conformance coverage derived from kitty graphics-protocol.rst and
/// Ghostty graphics_unicode.zig at the revisions pinned by issue #401.
/// </summary>
[TestClass]
public partial class KgpUnicodePlaceholderTests
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
    public void TransmitAndDisplay_UnicodePlaceholder_CreatesOnlyVirtualPrototype()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        AddVirtualImage(terminal, 42, 20, 40, columns: 2, rows: 2);

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(1, terminal.GetKgpVirtualReferenceCount(42));
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.AreEqual(0, snapshot.CursorX);
        Assert.AreEqual(0, snapshot.CursorY);
    }

    [TestMethod]
    public void Put_UnicodePlaceholder_CreatesPrototypeForExistingImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 20, 40, KgpFormat.Rgb24, quiet: 2));

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=2,r=2,q=2"));

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(0, terminal.CreateSnapshot().CursorX);
    }

    [TestMethod]
    public void Put_UnicodePlaceholderMissingImage_DoesNotCreatePrototype()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=2,r=2,q=2"));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public void Put_RelativeUnicodePlaceholder_IsRejectedWithoutMutation()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 20, 40, KgpFormat.Rgb24, quiet: 2));

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=2,r=2,P=9,q=2"));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
    }

    [TestMethod]
    public void Placeholder_ExplicitAndInheritedCoordinates_MaterializeHorizontalRuns()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 4);
        AddVirtualImage(terminal, 42, 20, 40, columns: 2, rows: 2);

        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            Placeholder() +
            "\r\n" +
            Placeholder(row: 1, column: 0) +
            Placeholder() +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(2, snapshot.KgpPlacements.Count);
        var first = snapshot.KgpPlacements[0];
        var second = snapshot.KgpPlacements[1];
        Assert.AreEqual(0, first.Row);
        Assert.AreEqual(0, first.Column);
        Assert.AreEqual(2u, first.DisplayColumns);
        Assert.AreEqual(0u, first.SourceY);
        Assert.AreEqual(20u, first.SourceHeight);
        Assert.AreEqual(1, second.Row);
        Assert.AreEqual(0, second.Column);
        Assert.AreEqual(20u, second.SourceY);
        Assert.AreEqual(20u, second.SourceHeight);
    }

    [TestMethod]
    public void Placeholder_HighByteAndTrueColorLowBits_ResolveFullImageId()
    {
        const uint imageId = 0x02123456;
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, imageId, 10, 20, columns: 1, rows: 1);

        Apply(terminal,
            Foreground(imageId) +
            Placeholder(row: 0, column: 0, high: 2) +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(imageId, TestSeq.Single(snapshot.KgpPlacements).ImageId);
        Assert.IsTrue(snapshot.KgpImages.ContainsKey(imageId));
    }

    [TestMethod]
    public void Placeholder_HighByteAndIndexedLowBits_ResolveFullImageId()
    {
        const uint imageId = 0xFF00002A;
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, imageId, 10, 20, columns: 1, rows: 1);

        Apply(terminal,
            "\x1b[38;5;42m" +
            Placeholder(row: 0, column: 0, high: 255) +
            "\x1b[0m");

        Assert.AreEqual(
            imageId,
            TestSeq.Single(terminal.CreateSnapshot().KgpPlacements).ImageId);
    }

    [TestMethod]
    [DataRow("\x1b[31m", 1u)]
    [DataRow("\x1b[91m", 9u)]
    [DataRow("\x1b[38;5;42m", 42u)]
    public void Placeholder_AnsiAndIndexedForeground_UsesPaletteIndex(
        string sgr,
        uint imageId)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, imageId, 10, 20, columns: 1, rows: 1);

        Apply(terminal, sgr + Placeholder(row: 0, column: 0) + "\x1b[0m");

        Assert.AreEqual(
            imageId,
            TestSeq.Single(terminal.CreateSnapshot().KgpPlacements).ImageId);
    }

    [TestMethod]
    public void Placeholder_UnderlineColor_SelectsExactPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 20, 20, KgpFormat.Rgb24, quiet: 2));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=1,c=1,r=1,q=2"));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=2,r=1,q=2"));

        Apply(terminal,
            Foreground(42) +
            "\x1b[58;5;7m" +
            Placeholder(row: 0, column: 0) +
            Placeholder() +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(7u, placement.PlacementId);
        Assert.AreEqual(2u, placement.DisplayColumns);
    }

    [TestMethod]
    public void Placeholder_OmittedPlacementId_SelectsOldestPrototype()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 20, 20, KgpFormat.Rgb24, quiet: 2));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=1,r=1,q=2"));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=8,c=2,r=1,q=2"));

        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            Placeholder() +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(7u, placement.PlacementId);
        Assert.AreEqual(1u, placement.DisplayColumns);
    }

    [TestMethod]
    public void Placeholder_MissingExplicitPlacementId_DoesNotFallBack()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(
            terminal, 42, 10, 20, columns: 1, rows: 1, placementId: 7);

        Apply(terminal,
            Foreground(42) +
            UnderlineColor(8) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsEmpty(snapshot.KgpImages);
    }

    [TestMethod]
    public void Placeholder_OmittedCoordinatesOnFirstCell_DefaultToZero()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 20, 20, columns: 2, rows: 1);

        Apply(terminal, Foreground(42) + Placeholder() + "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(0u, placement.SourceX);
        Assert.AreEqual(0u, placement.SourceY);
    }

    [TestMethod]
    public void Placeholder_InvalidDiacriticConsumesSlotAndExtraDiacriticsAreIgnored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 20, 20, columns: 2, rows: 1);
        var invalidRow = new Rune(0x0300).ToString();
        var ignoredFourth = Diacritic(296);

        Apply(terminal,
            Foreground(42) +
            BasePlaceholder +
            invalidRow +
            Diacritic(1) +
            Diacritic(0) +
            ignoredFourth +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(10u, placement.SourceX);
        Assert.AreEqual(10u, placement.SourceWidth);
    }

    [TestMethod]
    public void Placeholder_DiacriticAboveByteRange_IsInvalidForImageHighByte()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);

        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0, high: 296) +
            "\x1b[0m");

        Assert.AreEqual(
            42u,
            TestSeq.Single(terminal.CreateSnapshot().KgpPlacements).ImageId);
    }

    [TestMethod]
    public void Placeholder_NaturalGridUsesCurrentCellMetrics()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 20, 20, 40, columns: 0, rows: 0);

        Apply(terminal,
            Foreground(20) +
            Placeholder(row: 1, column: 1) +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(10u, placement.SourceX);
        Assert.AreEqual(20u, placement.SourceY);
        Assert.AreEqual(10u, placement.SourceWidth);
        Assert.AreEqual(20u, placement.SourceHeight);
    }

    [TestMethod]
    public void Placeholder_AspectFitProducesPartialCellDestinationGeometry()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 20, 10, columns: 2, rows: 2);

        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        var geometry = placement.RenderGeometry;
        Assert.IsNotNull(geometry);
        Assert.AreEqual(0, geometry.Value.ClipOffsetXInCells, 0.0001);
        Assert.AreEqual(0.75, geometry.Value.ClipOffsetYInCells, 0.0001);
        Assert.AreEqual(1, geometry.Value.ClipWidthInCells, 0.0001);
        Assert.AreEqual(0.25, geometry.Value.ClipHeightInCells, 0.0001);
        Assert.AreEqual(0u, placement.SourceY);
        Assert.AreEqual(5u, placement.SourceHeight);
    }

    [TestMethod]
    public void Placeholder_UnresolvedCellRemainsTextButProducesNoImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        Assert.IsTrue(KgpUnicodePlaceholder.IsPlaceholder(
            snapshot.GetCell(0, 0).Character));
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsEmpty(snapshot.KgpImages);
        var svg = snapshot.ToSvg();
        Assert.DoesNotContain("<image ", svg);
        Assert.DoesNotContain(BasePlaceholder, svg);
    }

    [TestMethod]
    public void Placeholder_GraphemeHasOneCellWidth()
    {
        var grapheme = Placeholder(row: 0, column: 0, high: 2);
        var elements = StringInfo.GetTextElementEnumerator(grapheme);

        Assert.AreEqual(1, DisplayWidth.GetGraphemeWidth(grapheme));
        Assert.IsTrue(elements.MoveNext());
        Assert.AreEqual(grapheme, elements.Current);
        Assert.IsFalse(elements.MoveNext());
    }

    private static string BasePlaceholder =>
        new Rune(KgpUnicodePlaceholder.CodePoint).ToString();

    private static Hex1bTerminal CreateTerminal(
        IHex1bTerminalWorkloadAdapter workload,
        int width = 20,
        int height = 10,
        int? scrollbackCapacity = null,
        ITerminalReflowProvider? reflow = null)
    {
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(KgpCapabilities)
            .WithDimensions(width, height);
        if (scrollbackCapacity.HasValue)
            builder.WithScrollback(scrollbackCapacity.Value);
        if (reflow is not null)
            builder.WithReflow(reflow);
        return builder.Build();
    }

    private static void Apply(Hex1bTerminal terminal, string value)
        => terminal.ApplyTokens(AnsiTokenizer.Tokenize(value));

    private static void AddVirtualImage(
        Hex1bTerminal terminal,
        uint imageId,
        uint width,
        uint height,
        uint columns,
        uint rows,
        uint placementId = 0,
        byte fillByte = 0xFF)
    {
        var control =
            $"a=T,U=1,f=24,s={width},v={height},i={imageId},c={columns},r={rows},q=2";
        if (placementId > 0)
            control += $",p={placementId}";
        Apply(terminal, KgpTestHelper.BuildCommand(
            control,
            KgpTestHelper.CreatePixelData(
                width,
                height,
                KgpFormat.Rgb24,
                fillByte)));
    }

    private static string Foreground(uint imageId)
        => $"\x1b[38;2;{(imageId >> 16) & 0xFF};{(imageId >> 8) & 0xFF};{imageId & 0xFF}m";

    private static string UnderlineColor(uint placementId)
        => $"\x1b[58;2;{(placementId >> 16) & 0xFF};{(placementId >> 8) & 0xFF};{placementId & 0xFF}m";

    private static string Placeholder(
        int? row = null,
        int? column = null,
        int? high = null)
    {
        var builder = new StringBuilder(BasePlaceholder);
        if (row.HasValue)
            builder.Append(Diacritic(row.Value));
        if (column.HasValue)
            builder.Append(Diacritic(column.Value));
        if (high.HasValue)
            builder.Append(Diacritic(high.Value));
        return builder.ToString();
    }

    private static string Diacritic(int index)
        => new Rune(KgpUnicodePlaceholderDiacritics.CodePoints[index]).ToString();
}
