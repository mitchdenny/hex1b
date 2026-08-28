using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Hex1b.Reflow;

namespace Hex1b.Tests;

public partial class KgpUnicodePlaceholderTests
{
    [TestMethod]
    public void TransmitAndDisplay_RelativeUnicodePlaceholder_StoresImageButRejectsPrototype()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=T,U=1,f=24,s=10,v=20,i=42,p=7,c=1,r=1,P=9,q=2",
            KgpTestHelper.CreatePixelData(10, 20, KgpFormat.Rgb24)));

        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(42));
        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
    }

    [TestMethod]
    public void TransmitAndDisplay_ImageNumber_CreatesAddressableVirtualPrototype()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=T,U=1,f=24,s=10,v=20,I=99,p=7,c=1,r=1,q=2",
            KgpTestHelper.CreatePixelData(10, 20, KgpFormat.Rgb24)));
        var image = terminal.KgpImageStore.GetImageByNumber(99);
        Assert.IsNotNull(image);

        Apply(terminal,
            Foreground(image.ImageId) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(image.ImageId, placement.ImageId);
        Assert.AreEqual(7u, placement.PlacementId);
    }

    [TestMethod]
    public void TransmitAndDisplay_AnonymousUnicodePlaceholder_IsNotAddressable()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=T,U=1,f=24,s=10,v=20,c=1,r=1,q=2",
            KgpTestHelper.CreatePixelData(10, 20, KgpFormat.Rgb24)));

        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
    }

    [TestMethod]
    public void Put_ZeroPlacementId_AppendsDistinctVirtualPrototypes()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 20, 20, KgpFormat.Rgb24, quiet: 2));

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,c=1,r=1,q=2"));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,c=2,r=1,q=2"));

        Assert.AreEqual(2, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(2, terminal.GetKgpVirtualReferenceCount(42));
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            Placeholder() +
            "\x1b[0m");
        Assert.AreEqual(
            1u,
            TestSeq.Single(terminal.CreateSnapshot().KgpPlacements).DisplayColumns);
    }

    [TestMethod]
    public void Put_NonZeroPlacementId_ReplacesPrototypeInPlace()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 20, 20, KgpFormat.Rgb24, quiet: 2));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=1,r=1,q=2"));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=2,r=1,q=2"));

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(1, terminal.GetKgpVirtualReferenceCount(42));
        Apply(terminal,
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            Placeholder() +
            "\x1b[0m");
        Assert.AreEqual(
            2u,
            TestSeq.Single(terminal.CreateSnapshot().KgpPlacements).DisplayColumns);
    }

    [TestMethod]
    public void Put_VirtualPlacement_ReplacesOrdinaryPlacementWithSameIdentity()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 10, 20, KgpFormat.Rgb24, quiet: 2));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=42,p=7,c=1,r=1,C=1,q=2"));

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=1,r=1,q=2"));

        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        Assert.IsEmpty(terminal.CreateSnapshot().KgpPlacements);
        Apply(terminal,
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");
        Assert.AreEqual(
            -1,
            TestSeq.Single(terminal.CreateSnapshot().KgpPlacements).ZIndex);
    }

    [TestMethod]
    public void Put_OrdinaryPlacement_ReplacesVirtualPlacementWithSameIdentity()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 4, height: 3);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 10, 20, KgpFormat.Rgb24, quiet: 2));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=1,r=1,q=2"));
        Apply(terminal,
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m\x1b[3;2H");

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=42,p=7,c=1,r=1,C=1,q=2"));

        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(2, placement.Row);
        Assert.AreEqual(1, placement.Column);
        Assert.AreEqual(0, placement.ZIndex);
    }

    [TestMethod]
    public void Snapshot_PrototypeReplacementDoesNotMutateCapturedFragment()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 20, 20, KgpFormat.Rgb24, quiet: 2));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=1,r=1,q=2"));
        Apply(terminal,
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            Placeholder() +
            "\x1b[0m");
        using var before = terminal.CreateSnapshot();

        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=2,r=1,q=2"));

        Assert.AreEqual(1u, TestSeq.Single(before.KgpPlacements).DisplayColumns);
        Assert.AreEqual(
            2u,
            TestSeq.Single(terminal.CreateSnapshot().KgpPlacements).DisplayColumns);
    }

    [TestMethod]
    public void Put_VirtualPlacement_IgnoresSourceOffsetsCursorAndZIndex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(
            42, 20, 20, KgpFormat.Rgb24, quiet: 2));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,U=1,i=42,p=7,c=2,r=1,x=5,y=5,w=5,h=5,X=3,Y=4,C=0,z=99,q=2"));

        Apply(terminal,
            Foreground(42) +
            UnderlineColor(7) +
            Placeholder(row: 0, column: 0) +
            Placeholder() +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(-1, placement.ZIndex);
        Assert.AreEqual(0u, placement.SourceX);
        Assert.AreEqual(0u, placement.SourceY);
        Assert.AreEqual(20u, placement.SourceWidth);
        Assert.AreEqual(20u, placement.SourceHeight);
        Assert.AreEqual(2, terminal.CreateSnapshot().CursorX);
    }

    [TestMethod]
    public void Placeholder_CombiningDiacriticsSplitAcrossFeeds_ReevaluateCell()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 20, 20, columns: 2, rows: 1);

        Apply(terminal, Foreground(42) + BasePlaceholder);
        Apply(terminal, Diacritic(0));
        Apply(terminal, Diacritic(1) + "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(10u, placement.SourceX);
        Assert.AreEqual(10u, placement.SourceWidth);
    }

    [TestMethod]
    public void Placeholder_GraphemeModeDisabled_StillAccumulatesDiacritics()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 20, 20, columns: 2, rows: 1);

        Apply(terminal,
            "\x1b[?2027l" +
            Foreground(42) +
            Placeholder(row: 0, column: 1) +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(10u, placement.SourceX);
    }

    [TestMethod]
    public void Placeholder_NonPlaceholderAndIdentityChanges_ResetInheritance()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 2);
        AddVirtualImage(terminal, 42, 20, 20, columns: 2, rows: 1);
        AddVirtualImage(terminal, 43, 20, 20, columns: 2, rows: 1);

        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 1) +
            "X" +
            Placeholder() +
            Foreground(43) +
            Placeholder() +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(3, snapshot.KgpPlacements.Count);
        Assert.AreEqual(10u, snapshot.KgpPlacements[0].SourceX);
        Assert.AreEqual(0u, snapshot.KgpPlacements[1].SourceX);
        Assert.AreEqual(0u, snapshot.KgpPlacements[2].SourceX);
    }

    [TestMethod]
    public void Placeholder_RowOnlyAndRowColumnForms_InheritRemainingFields()
    {
        const uint imageId = 0x0200002A;
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 6, height: 2);
        AddVirtualImage(terminal, imageId, 30, 20, columns: 3, rows: 1);
        Apply(terminal,
            Foreground(imageId) +
            Placeholder(row: 0, column: 0, high: 2) +
            Placeholder(row: 0) +
            Placeholder(row: 0, column: 2) +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(imageId, placement.ImageId);
        Assert.AreEqual(3u, placement.DisplayColumns);
        Assert.AreEqual(30u, placement.SourceWidth);
    }

    [TestMethod]
    public void Placeholder_ExplicitIncompatibleColumn_StartsNewRun()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 30, 20, columns: 3, rows: 1);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            Placeholder(row: 0, column: 2) +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(2, snapshot.KgpPlacements.Count);
        Assert.AreEqual(0u, snapshot.KgpPlacements[0].SourceX);
        Assert.AreEqual(20u, snapshot.KgpPlacements[1].SourceX);
    }

    [TestMethod]
    public void Placeholder_AdjacentWideGrapheme_PreservesCellBoundary()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            "界" +
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(2, placement.Column);
    }

    [TestMethod]
    public void Placeholder_AnonymousRelocationAndExplicitPrototype_KeepGenerationsSeparate()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 4, height: 2);
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=T,f=24,s=10,v=20,c=1,r=1,C=1",
            KgpTestHelper.CreatePixelData(
                10, 20, KgpFormat.Rgb24, fillByte: 0x11)));
        Assert.AreEqual(1u, TestSeq.Single(terminal.KgpPlacements).ImageId);

        AddVirtualImage(
            terminal, 1, 10, 20, columns: 1, rows: 1, fillByte: 0x22);
        Apply(terminal,
            Foreground(1) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(2, snapshot.KgpPlacements.Count);
        Assert.IsTrue(snapshot.KgpImages.ContainsKey(1));
        Assert.IsTrue(snapshot.KgpImages.ContainsKey(2));
        Assert.AreEqual(0x22, snapshot.KgpImages[1].Data[0]);
        Assert.AreEqual(0x11, snapshot.KgpImages[2].Data[0]);
    }

    [TestMethod]
    public void Placeholder_ThirdPartyReflow_UsesReturnedTextCellsWithoutKgpSidecar()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 4,
            height: 2,
            reflow: new ThirdPartyNoReflowProvider());
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            "\x1b[1;2H" +
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        terminal.Resize(3, 2);

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        Assert.AreEqual(1, placement.Column);
    }

    [TestMethod]
    public void Placeholder_ExternalImageRemoval_ProducesNoDanglingFragment()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 10, 20, columns: 1, rows: 1);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        Assert.IsTrue(terminal.KgpImageStore.RemoveImage(42));

        using var snapshot = terminal.CreateSnapshot();
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsEmpty(snapshot.KgpImages);
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
    }

    [TestMethod]
    public void Svg_RealizedPlaceholder_UsesPartialDestinationAndSuppressesGlyph()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 20, 10, columns: 2, rows: 2);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        Assert.Contains("<rect x=\"0\" y=\"15\" width=\"10\" height=\"5\"", svg);
        Assert.Contains("<use href=\"#kgp-placeholder-image-0\" x=\"0\" y=\"15\" width=\"20\" height=\"10\"", svg);
        Assert.DoesNotContain(BasePlaceholder, svg);
        Assert.Contains("class=\"terminal-bg\"", svg);
    }

    [TestMethod]
    public void Svg_MultiplePlaceholderRows_EmbedsImageDataOnce()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 10, 40, columns: 1, rows: 2);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\r\n" +
            Placeholder(row: 1, column: 0) +
            "\x1b[0m");

        var svg = terminal.CreateSnapshot().ToSvg();
        Assert.AreEqual(
            1,
            svg.Split("data:image/bmp;base64,", StringSplitOptions.None).Length - 1);
        Assert.AreEqual(
            2,
            svg.Split("<use href=\"#kgp-placeholder-image-0\"", StringSplitOptions.None).Length - 1);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(128)]
    [DataRow(255)]
    public void Svg_RgbaPlaceholder_PreservesAlphaOverCellBackground(
        int alpha)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=T,U=1,f=32,s=1,v=1,i=42,c=1,r=1,q=2",
            [0xD1, 0x42, 0x73, checked((byte)alpha)]));
        Apply(terminal,
            Foreground(42) +
            "\x1b[48;2;17;34;51m" +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        var cell = snapshot.GetCell(0, 0);
        Assert.AreEqual(17, cell.Background!.Value.R);
        Assert.AreEqual(34, cell.Background.Value.G);
        Assert.AreEqual(51, cell.Background.Value.B);

        var svg = snapshot.ToSvg();
        Assert.Contains("fill=\"rgb(17,34,51)\"", svg);
        Assert.Contains("data:image/png;base64,", svg);
        Assert.IsLessThan(
            svg.IndexOf("class=\"terminal-images\"", StringComparison.Ordinal),
            svg.IndexOf("class=\"terminal-bg\"", StringComparison.Ordinal));

        var decoded = DecodeRgbaPng(ExtractDataUri(svg, "data:image/png;base64,"));
        Assert.AreEqual(1, decoded.Width);
        Assert.AreEqual(1, decoded.Height);
        TestSeq.AreEqual(
            new byte[] { 0xD1, 0x42, 0x73, checked((byte)alpha) },
            decoded.Pixels);
    }

    [TestMethod]
    [DataRow(true, 20u, 10u)]
    [DataRow(false, 20u, 10u)]
    [DataRow(true, 10u, 20u)]
    [DataRow(false, 10u, 20u)]
    public void Svg_EqualZOrdinaryAndVirtualPlacements_SortByImageId(
        bool ordinaryFirst,
        uint ordinaryImageId,
        uint virtualImageId)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        void AddOrdinary()
        {
            Apply(terminal, KgpTestHelper.BuildTransmitCommand(
                ordinaryImageId,
                1,
                1,
                KgpFormat.Rgb24,
                quiet: 2,
                fillByte: 0x11));
            Apply(terminal, KgpTestHelper.BuildCommand(
                $"a=p,i={ordinaryImageId},p=1,c=1,r=1,C=1,z=-1,q=2"));
        }

        void AddVirtual()
            => AddVirtualImage(
                terminal,
                virtualImageId,
                1,
                1,
                columns: 1,
                rows: 1,
                placementId: 2,
                fillByte: 0x22);

        if (ordinaryFirst)
        {
            AddOrdinary();
            AddVirtual();
        }
        else
        {
            AddVirtual();
            AddOrdinary();
        }

        Apply(terminal,
            "\x1b[H" +
            Foreground(virtualImageId) +
            UnderlineColor(2) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(2, snapshot.KgpPlacements.Count);
        TestSeq.All(snapshot.KgpPlacements, placement =>
        {
            Assert.AreEqual(-1, placement.ZIndex);
            Assert.AreEqual(0, placement.Row);
            Assert.AreEqual(0, placement.Column);
        });

        var svg = snapshot.ToSvg();
        var lowerId = Math.Min(ordinaryImageId, virtualImageId);
        var higherId = Math.Max(ordinaryImageId, virtualImageId);
        var lowerIndex = svg.IndexOf(
            $"data-image-id=\"{lowerId}\"",
            StringComparison.Ordinal);
        var higherIndex = svg.IndexOf(
            $"data-image-id=\"{higherId}\"",
            StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, lowerIndex);
        Assert.IsGreaterThanOrEqualTo(0, higherIndex);
        Assert.IsLessThan(higherIndex, lowerIndex);
    }

    [TestMethod]
    public void Svg_FractionalSourceBoundary_ClipsFullImageWithoutRasterCropDistortion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 3, 3, columns: 2, rows: 1);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        var svg = terminal.CreateSnapshot().ToSvg();
        Assert.Contains("<rect x=\"0\" y=\"0\" width=\"10\" height=\"20\"", svg);
        Assert.Contains("<use href=\"#kgp-placeholder-image-0\" x=\"0\" y=\"0\" width=\"20\" height=\"20\"", svg);
        var marker = "data:image/bmp;base64,";
        var start = svg.IndexOf(marker, StringComparison.Ordinal);
        var end = svg.IndexOf('"', start);
        var bmp = Convert.FromBase64String(
            svg[(start + marker.Length)..end]);
        Assert.AreEqual(3, BitConverter.ToInt32(bmp, 18));
        Assert.AreEqual(3, BitConverter.ToInt32(bmp, 22));
    }

    [TestMethod]
    public void Svg_PngNormalizedFullSourceExtent_RendersAndDeduplicates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 5);
        var png = TestPng();
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=100,i=42,q=2",
            png));
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=42,p=1,x=0,y=0,w=100,h=80,c=2,r=2,C=1,q=2"));
        Apply(terminal, "\x1b[2;2H");
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=42,p=2,x=0,y=0,w=100,h=80,c=2,r=2,C=1,q=2"));

        var svg = terminal.CreateSnapshot().ToSvg();
        Assert.AreEqual(
            1,
            svg.Split("data:image/png;base64,", StringSplitOptions.None).Length - 1);
        Assert.AreEqual(
            2,
            svg.Split("<use href=\"#kgp-placeholder-image-0\"", StringSplitOptions.None).Length - 1);
        Assert.Contains(
            $"data:image/png;base64,{Convert.ToBase64String(png)}",
            svg);
        Assert.Contains(
            "<use href=\"#kgp-placeholder-image-0\" x=\"0\" y=\"0\" width=\"20\" height=\"40\"",
            svg);
        Assert.Contains(
            "<use href=\"#kgp-placeholder-image-0\" x=\"10\" y=\"20\" width=\"20\" height=\"40\"",
            svg);
    }

    [TestMethod]
    public void Svg_PngPartialSourceExtent_UsesPositionedClippedOriginalImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 8, height: 5);
        var png = TestPng();
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=100,s=100,v=80,i=42,q=2",
            png));
        Apply(terminal, "\x1b[2;2H");
        Apply(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=42,p=1,x=25,y=20,w=50,h=40,c=2,r=2,C=1,q=2"));

        var svg = terminal.CreateSnapshot().ToSvg();
        Assert.Contains(
            $"data:image/png;base64,{Convert.ToBase64String(png)}",
            svg);
        Assert.Contains(
            "<rect x=\"10\" y=\"20\" width=\"20\" height=\"40\"",
            svg);
        Assert.Contains(
            "<use href=\"#kgp-placeholder-image-0\" x=\"0\" y=\"0\" width=\"40\" height=\"80\"",
            svg);
        Assert.Contains("clip-path=\"url(#kgp-png-clip-", svg);
    }

    [TestMethod]
    public void Placeholder_PortraitAspectFitClipsHorizontalDeadZone()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        AddVirtualImage(terminal, 42, 10, 40, columns: 2, rows: 2);
        Apply(terminal,
            Foreground(42) +
            Placeholder(row: 0, column: 0) +
            "\x1b[0m");

        var placement = TestSeq.Single(terminal.CreateSnapshot().KgpPlacements);
        var geometry = placement.RenderGeometry!.Value;
        Assert.AreEqual(0.5, geometry.ClipOffsetXInCells, 0.0001);
        Assert.AreEqual(0.5, geometry.ClipWidthInCells, 0.0001);
        Assert.AreEqual(0u, placement.SourceX);
        Assert.AreEqual(5u, placement.SourceWidth);
    }

    private sealed class ThirdPartyNoReflowProvider : ITerminalReflowProvider
    {
        public bool ShouldClearSoftWrapOnAbsolutePosition => false;

        public ReflowResult Reflow(ReflowContext context)
            => NoReflowStrategy.Instance.Reflow(context);
    }

    private static byte[] ExtractDataUri(string svg, string marker)
    {
        var start = svg.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start);
        start += marker.Length;
        var end = svg.IndexOf('"', start);
        Assert.IsGreaterThan(start, end);
        return Convert.FromBase64String(svg[start..end]);
    }

    private static (int Width, int Height, byte[] Pixels) DecodeRgbaPng(
        byte[] png)
    {
        TestSeq.AreEqual(
            new byte[]
            {
                0x89, (byte)'P', (byte)'N', (byte)'G',
                0x0D, 0x0A, 0x1A, 0x0A,
            },
            png[..8]);

        var width = 0;
        var height = 0;
        using var compressed = new MemoryStream();
        for (var offset = 8; offset < png.Length;)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(
                png.AsSpan(offset, 4)));
            var type = png.AsSpan(offset + 4, 4);
            var payload = png.AsSpan(offset + 8, length);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(
                png.AsSpan(offset + 8 + length, 4));
            Assert.AreEqual(expectedCrc, ComputePngCrc(type, payload));

            if (type.SequenceEqual("IHDR"u8))
            {
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload[4..]));
                Assert.AreEqual(8, payload[8]);
                Assert.AreEqual(6, payload[9]);
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                compressed.Write(payload);
            }

            offset += checked(12 + length);
        }

        Assert.IsGreaterThan(0, width);
        Assert.IsGreaterThan(0, height);
        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var decoded = new MemoryStream();
        zlib.CopyTo(decoded);
        var scanlines = decoded.ToArray();
        var pixels = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
        {
            var scanlineOffset = y * (width * 4 + 1);
            Assert.AreEqual(0, scanlines[scanlineOffset]);
            scanlines.AsSpan(scanlineOffset + 1, width * 4)
                .CopyTo(pixels.AsSpan(y * width * 4));
        }

        return (width, height, pixels);
    }

    private static uint ComputePngCrc(
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> payload)
    {
        var crc = uint.MaxValue;
        foreach (var value in type)
            UpdatePngCrc(ref crc, value);
        foreach (var value in payload)
            UpdatePngCrc(ref crc, value);
        return ~crc;
    }

    private static void UpdatePngCrc(ref uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0
                ? (crc >> 1) ^ 0xEDB88320u
                : crc >> 1;
        }
    }

    private static byte[] TestPng()
        => Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
}
