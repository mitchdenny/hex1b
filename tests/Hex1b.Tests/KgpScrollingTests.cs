using Hex1b.Tokens;
using Hex1b.Reflow;

namespace Hex1b.Tests;

[TestClass]
public class KgpScrollingTests
{
    private static Hex1bTerminal CreateTerminal(
        IHex1bTerminalWorkloadAdapter workload,
        int width = 10,
        int height = 6,
        int? scrollbackCapacity = null,
        int cellPixelWidth = 10,
        int cellPixelHeight = 10,
        ITerminalReflowProvider? reflow = null)
    {
        var capabilities = new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
            CellPixelWidth = cellPixelWidth,
            CellPixelHeight = cellPixelHeight,
        };
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(capabilities)
            .WithDimensions(width, height);
        if (scrollbackCapacity is { } capacity)
            builder.WithScrollback(capacity);
        if (reflow is not null)
            builder.WithReflow(reflow);
        return builder.Build();
    }

    private static void Apply(Hex1bTerminal terminal, string value)
        => terminal.ApplyTokens(AnsiTokenizer.Tokenize(value));

    private static void PlaceImage(
        Hex1bTerminal terminal,
        uint imageId,
        uint imageWidth,
        uint imageHeight,
        uint displayColumns,
        uint displayRows,
        int row = 0,
        int column = 0)
    {
        Apply(terminal, $"\x1b[{row + 1};{column + 1}H");
        Apply(
            terminal,
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                imageId,
                imageWidth,
                imageHeight,
                displayColumns: displayColumns,
                displayRows: displayRows,
                cursorMovement: 1,
                quiet: 2));
    }

    private static KgpPlacement SinglePlacement(Hex1bTerminalSnapshot snapshot)
        => TestSeq.Single(snapshot.KgpPlacements);

    [TestMethod]
    public void FullScreenScroll_WithScrollback_ProjectsPlacementAcrossHistoryBoundary()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 10,
            height: 4,
            scrollbackCapacity: 3);
        PlaceImage(terminal, 1, 10, 20, displayColumns: 1, displayRows: 2);

        Apply(terminal, "\x1b[S");

        using var active = terminal.CreateSnapshot();
        var activePlacement = SinglePlacement(active);
        Assert.AreEqual(0, activePlacement.Row);
        Assert.AreEqual(1u, activePlacement.DisplayRows);
        Assert.AreEqual(10u, activePlacement.SourceY);
        Assert.AreEqual(10u, activePlacement.SourceHeight);

        using var withHistory = terminal.CreateSnapshot(scrollbackLines: 1);
        var historyPlacement = SinglePlacement(withHistory);
        Assert.AreEqual(0, historyPlacement.Row);
        Assert.AreEqual(2u, historyPlacement.DisplayRows);
        Assert.AreEqual(0u, historyPlacement.SourceY);
        Assert.AreEqual(20u, historyPlacement.SourceHeight);
        Assert.AreEqual(1, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(1, terminal.GetKgpHistoryReferenceCount(1));
        Assert.IsTrue(withHistory.KgpImages.ContainsKey(1));
    }

    [TestMethod]
    public void LineFeedAtBottom_FullScreenMovesPlacementIntoHistory()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 10,
            height: 4,
            scrollbackCapacity: 2);
        PlaceImage(terminal, 1, 10, 10, displayColumns: 1, displayRows: 1);

        Apply(terminal, "\x1b[4;1H\n");

        Assert.AreEqual(1, terminal.ScrollbackCount);
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);
        AssertPlacement(
            terminal.CreateSnapshot(scrollbackLines: 1),
            row: 0,
            rows: 1,
            sourceY: 0,
            sourceHeight: 10);
    }

    [TestMethod]
    public void FullScreenScrollDown_HistoryAnchoredVisibleTailRemainsPinned()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 2);
        PlaceImage(terminal, 1, 10, 20, displayColumns: 1, displayRows: 2);
        Apply(terminal, "\x1b[S");

        Apply(terminal, "\x1b[T");

        AssertPlacement(
            terminal.CreateSnapshot(),
            row: 0,
            rows: 1,
            sourceY: 10,
            sourceHeight: 10);
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);
    }

    [TestMethod]
    public void FullScreenScroll_CapacityOne_ReanchorsScaledCropUntilFullyPruned()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 10,
            height: 4,
            scrollbackCapacity: 1);
        var data = KgpTestHelper.CreatePixelData(10, 80);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,f=32,s=10,v=80,i=1,p=1,x=0,y=10,w=10,h=60,c=1,r=3,C=1,q=2",
                data));

        Apply(terminal, "\x1b[S");
        AssertPlacement(
            terminal.CreateSnapshot(scrollbackLines: 1),
            row: 0,
            rows: 3,
            sourceY: 10,
            sourceHeight: 60);
        AssertPlacement(
            terminal.CreateSnapshot(),
            row: 0,
            rows: 2,
            sourceY: 30,
            sourceHeight: 40);
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);

        Apply(terminal, "\x1b[S");
        AssertPlacement(
            terminal.CreateSnapshot(scrollbackLines: 1),
            row: 0,
            rows: 2,
            sourceY: 30,
            sourceHeight: 40);
        AssertPlacement(
            terminal.CreateSnapshot(),
            row: 0,
            rows: 1,
            sourceY: 50,
            sourceHeight: 20);
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);

        Apply(terminal, "\x1b[S");
        AssertPlacement(
            terminal.CreateSnapshot(scrollbackLines: 1),
            row: 0,
            rows: 1,
            sourceY: 50,
            sourceHeight: 20);
        using (var active = terminal.CreateSnapshot())
            Assert.IsEmpty(active.KgpPlacements);
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);

        Apply(terminal, "\x1b[S");

        using var pruned = terminal.CreateSnapshot(scrollbackLines: 1);
        Assert.IsEmpty(pruned.KgpPlacements);
        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    public void FullScreenScroll_CapacityTwo_UsesOriginalScaleAcrossRepeatedReanchors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 10,
            height: 5,
            scrollbackCapacity: 2);
        var data = KgpTestHelper.CreatePixelData(10, 100);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,f=32,s=10,v=100,i=1,p=1,x=0,y=10,w=10,h=70,c=1,r=4,C=1,q=2",
                data));

        Apply(terminal, "\x1b[S\x1b[S\x1b[S");

        AssertPlacement(
            terminal.CreateSnapshot(scrollbackLines: 2),
            row: 0,
            rows: 3,
            sourceY: 27,
            sourceHeight: 53);
        AssertPlacement(
            terminal.CreateSnapshot(),
            row: 0,
            rows: 1,
            sourceY: 62,
            sourceHeight: 18);
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);

        Apply(terminal, "\x1b[S");
        AssertPlacement(
            terminal.CreateSnapshot(scrollbackLines: 2),
            row: 0,
            rows: 2,
            sourceY: 45,
            sourceHeight: 35);
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);

        Apply(terminal, "\x1b[S");
        AssertPlacement(
            terminal.CreateSnapshot(scrollbackLines: 2),
            row: 0,
            rows: 1,
            sourceY: 62,
            sourceHeight: 18);
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);

        Apply(terminal, "\x1b[S");
        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    [DataRow("\x1b[S")]
    [DataRow("\u001bD")]
    public void PartialMargins_ScrollUp_MovesClipsThenDeletesContainedPlacement(
        string scrollSequence)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, "\x1b[2;5r");
        PlaceImage(
            terminal,
            1,
            imageWidth: 10,
            imageHeight: 20,
            displayColumns: 1,
            displayRows: 2,
            row: 2);
        Apply(terminal, "\x1b[5;1H");

        Apply(terminal, scrollSequence);
        AssertPlacement(
            terminal.CreateSnapshot(),
            row: 1,
            rows: 2,
            sourceY: 0,
            sourceHeight: 20);

        Apply(terminal, scrollSequence);
        AssertPlacement(
            terminal.CreateSnapshot(),
            row: 1,
            rows: 1,
            sourceY: 10,
            sourceHeight: 10);

        Apply(terminal, scrollSequence);
        using var snapshot = terminal.CreateSnapshot();
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    [DataRow("\x1bM")]
    [DataRow("\x1b[T")]
    public void PartialMargins_ScrollDown_ClipsBottomThenDeletesContainedPlacement(
        string scrollSequence)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, "\x1b[2;5r");
        PlaceImage(
            terminal,
            1,
            imageWidth: 10,
            imageHeight: 20,
            displayColumns: 1,
            displayRows: 2,
            row: 3);
        Apply(terminal, "\x1b[2;1H");

        Apply(terminal, scrollSequence);
        AssertPlacement(
            terminal.CreateSnapshot(),
            row: 4,
            rows: 1,
            sourceY: 0,
            sourceHeight: 10);

        Apply(terminal, scrollSequence);
        using var snapshot = terminal.CreateSnapshot();
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    public void PartialMargins_ScrollDown_ClipsScaledPreCroppedSourceProportionally()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, "\x1b[2;3r\x1b[2;1H");
        var data = KgpTestHelper.CreatePixelData(10, 80);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,f=32,s=10,v=80,i=1,p=1,x=0,y=10,w=10,h=60,c=1,r=2,C=1,q=2",
                data));

        Apply(terminal, "\x1b[T");

        AssertPlacement(
            terminal.CreateSnapshot(),
            row: 2,
            rows: 1,
            sourceY: 10,
            sourceHeight: 30);
    }

    [TestMethod]
    public void PartialMargins_StraddlingOutsideAndStatusPlacementsRemainFixed()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(1, 10, 30, quiet: 2));
        Apply(terminal, "\x1b[1;1H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 1, displayColumns: 1, displayRows: 2, cursorMovement: 1));
        Apply(terminal, "\x1b[3;1H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 2, displayColumns: 1, displayRows: 1, cursorMovement: 1));
        Apply(terminal, "\x1b[6;1H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 3, displayColumns: 1, displayRows: 1, cursorMovement: 1));
        Apply(terminal, "\x1b[2;5r\x1b[S");

        var placements = terminal.KgpPlacements.OrderBy(placement => placement.PlacementId).ToArray();
        Assert.AreEqual(3, placements.Length);
        Assert.AreEqual(0, placements[0].Row);
        Assert.AreEqual(1, placements[1].Row);
        Assert.AreEqual(5, placements[2].Row);
    }

    [TestMethod]
    public void HorizontalMargins_MoveOnlyWhollyContainedPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(1, 10, 10, quiet: 2));
        Apply(terminal, "\x1b[3;1H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 1, displayColumns: 1, displayRows: 1, cursorMovement: 1));
        Apply(terminal, "\x1b[3;4H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 2, displayColumns: 2, displayRows: 1, cursorMovement: 1));
        Apply(terminal, "\x1b[3;8H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 3, displayColumns: 2, displayRows: 1, cursorMovement: 1));

        Apply(terminal, "\x1b[?69h\x1b[3;8s\x1b[S");

        var placements = terminal.KgpPlacements.OrderBy(placement => placement.PlacementId).ToArray();
        Assert.AreEqual(3, placements.Length);
        Assert.AreEqual(2, placements[0].Row);
        Assert.AreEqual(0, placements[0].Column);
        Assert.AreEqual(1, placements[1].Row);
        Assert.AreEqual(3, placements[1].Column);
        Assert.AreEqual(2, placements[2].Row);
        Assert.AreEqual(7, placements[2].Column);
    }

    [TestMethod]
    public void InsertDeleteLines_OrdinaryPlacementsRemainFixed()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, "\x1b[2;5r");
        PlaceImage(
            terminal,
            1,
            imageWidth: 10,
            imageHeight: 10,
            displayColumns: 1,
            displayRows: 1,
            row: 2,
            column: 3);
        Apply(terminal, "\x1b[?69h\x1b[3;8s\x1b[3;4H");

        Apply(terminal, "\x1b[L");
        var afterInsert = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(2, afterInsert.Row);
        Assert.AreEqual(3, afterInsert.Column);

        Apply(terminal, "\x1b[M");
        var afterDelete = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(2, afterDelete.Row);
        Assert.AreEqual(3, afterDelete.Column);
    }

    [TestMethod]
    public void PartialMargins_MultiCountScroll_AppliesEachStepConsistently()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Apply(terminal, "\x1b[2;5r");
        PlaceImage(
            terminal,
            1,
            imageWidth: 10,
            imageHeight: 10,
            displayColumns: 1,
            displayRows: 1,
            row: 3);

        Apply(terminal, "\x1b[2S");

        var placement = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(1, placement.Row);
        Assert.AreEqual(1u, placement.DisplayRows);
        Assert.AreEqual(0u, placement.SourceY);
        Assert.AreEqual(10u, placement.SourceHeight);
    }

    [TestMethod]
    public void ClipRows_WithoutCellMetrics_UsesDeterministicCellProportions()
    {
        var image = new KgpImageData(
            imageId: 1,
            imageNumber: 0,
            KgpTestHelper.CreatePixelData(10, 20),
            width: 10,
            height: 20,
            KgpFormat.Rgba32);
        var placement = new KgpPlacement(
            imageId: 1,
            placementId: 1,
            row: 0,
            column: 0,
            displayColumns: 1,
            displayRows: 3,
            sourceX: 0,
            sourceY: 10,
            sourceWidth: 10,
            sourceHeight: 5,
            cellOffsetY: 19);

        var middle = placement.ClipRows(
            image,
            firstRow: 1,
            retainedRows: 1,
            resultRow: 0,
            cellPixelHeight: 0);

        Assert.IsNotNull(middle);
        Assert.AreEqual(11u, middle.SourceY);
        Assert.AreEqual(3u, middle.SourceHeight);
        Assert.AreEqual(0u, middle.CellOffsetY);
    }

    [TestMethod]
    public void ClipRectangle_WithoutCellMetrics_UsesFloorStartAndCeilingEndOnBothAxes()
    {
        var image = new KgpImageData(
            imageId: 1,
            imageNumber: 0,
            KgpTestHelper.CreatePixelData(5, 5),
            width: 5,
            height: 5,
            KgpFormat.Rgba32);
        var placement = new KgpPlacement(
            imageId: 1,
            placementId: 1,
            row: 0,
            column: 0,
            displayColumns: 3,
            displayRows: 3,
            cellOffsetX: 9,
            cellOffsetY: 19);

        var clipped = placement.ClipToCellRectangle(
            image,
            top: 1,
            bottomExclusive: 2,
            left: 1,
            rightExclusive: 2,
            cellPixelWidth: 0,
            cellPixelHeight: 0);

        Assert.IsNotNull(clipped);
        Assert.AreEqual(1, clipped.Row);
        Assert.AreEqual(1, clipped.Column);
        Assert.AreEqual(1u, clipped.DisplayColumns);
        Assert.AreEqual(1u, clipped.DisplayRows);
        Assert.AreEqual(1u, clipped.SourceX);
        Assert.AreEqual(1u, clipped.SourceY);
        Assert.AreEqual(3u, clipped.SourceWidth);
        Assert.AreEqual(3u, clipped.SourceHeight);
        Assert.AreEqual(0u, clipped.CellOffsetX);
        Assert.AreEqual(0u, clipped.CellOffsetY);
    }

    [TestMethod]
    public void ClipRows_WithCellOffset_UsesDestinationPixelFraction()
    {
        var image = new KgpImageData(
            imageId: 1,
            imageNumber: 0,
            KgpTestHelper.CreatePixelData(10, 30),
            width: 10,
            height: 30,
            KgpFormat.Rgba32);
        var placement = new KgpPlacement(
            imageId: 1,
            placementId: 1,
            row: 0,
            column: 0,
            displayColumns: 1,
            displayRows: 2,
            cellOffsetY: 5);

        var topClipped = placement.ClipRows(
            image,
            firstRow: 1,
            retainedRows: 1,
            resultRow: 0,
            cellPixelHeight: 10);
        var bottomClipped = placement.ClipRows(
            image,
            firstRow: 0,
            retainedRows: 1,
            resultRow: 0,
            cellPixelHeight: 10);

        Assert.IsNotNull(topClipped);
        Assert.AreEqual(10u, topClipped.SourceY);
        Assert.AreEqual(20u, topClipped.SourceHeight);
        Assert.AreEqual(0u, topClipped.CellOffsetY);
        Assert.IsNotNull(bottomClipped);
        Assert.AreEqual(0u, bottomClipped.SourceY);
        Assert.AreEqual(10u, bottomClipped.SourceHeight);
        Assert.AreEqual(5u, bottomClipped.CellOffsetY);
    }

    [TestMethod]
    public void FullScreenScroll_WithoutScrollback_ClipsMultirowPlacementAtViewport()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, height: 4);
        PlaceImage(terminal, 1, 10, 30, displayColumns: 1, displayRows: 3);

        Apply(terminal, "\x1b[S");
        AssertPlacement(
            terminal.CreateSnapshot(),
            row: 0,
            rows: 2,
            sourceY: 10,
            sourceHeight: 20);

        Apply(terminal, "\x1b[S");
        AssertPlacement(
            terminal.CreateSnapshot(),
            row: 0,
            rows: 1,
            sourceY: 20,
            sourceHeight: 10);

        Apply(terminal, "\x1b[S");
        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    public void MainHistory_Ed2ClipsVisibleTailAndEd3ReleasesHistory()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 3);
        PlaceImage(terminal, 1, 10, 30, displayColumns: 1, displayRows: 3);
        Apply(terminal, "\x1b[S");

        Apply(terminal, "\x1b[2J");

        using (var active = terminal.CreateSnapshot())
            Assert.IsEmpty(active.KgpPlacements);
        AssertPlacement(
            terminal.CreateSnapshot(scrollbackLines: 1),
            row: 0,
            rows: 1,
            sourceY: 0,
            sourceHeight: 10);
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);

        Apply(terminal, "\x1b[3J");

        Assert.AreEqual(0, terminal.ScrollbackCount);
        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    public void AlternateScreen_ScrollAndEd3_PreserveMainHistory()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 3);
        PlaceImage(terminal, 1, 10, 20, displayColumns: 1, displayRows: 2);
        Apply(terminal, "\x1b[S");

        Apply(terminal, "\x1b[?1049h");
        PlaceImage(terminal, 2, 10, 10, displayColumns: 1, displayRows: 1);
        Apply(terminal, "\x1b[S\x1b[3J");
        using (var alternate = terminal.CreateSnapshot(scrollbackLines: 3))
        {
            Assert.IsTrue(alternate.InAlternateScreen);
            Assert.IsEmpty(alternate.KgpPlacements);
            Assert.AreEqual(0, alternate.ScrollbackLineCount);
        }

        Apply(terminal, "\x1b[?1049l");

        using var restored = terminal.CreateSnapshot(scrollbackLines: 1);
        Assert.IsFalse(restored.InAlternateScreen);
        var placement = SinglePlacement(restored);
        Assert.AreEqual(1u, placement.ImageId);
        Assert.AreEqual(2u, placement.DisplayRows);
        Assert.IsTrue(restored.KgpImages.ContainsKey(1));
        Assert.AreEqual(1, terminal.ScrollbackCount);
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);
    }

    [TestMethod]
    public void Ris_ClearsHistoryPlacementsReferencesAndData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 3);
        PlaceImage(terminal, 1, 10, 20, displayColumns: 1, displayRows: 2);
        Apply(terminal, "\x1b[S");

        Apply(terminal, "\x1b" + "c");

        Assert.AreEqual(0, terminal.ScrollbackCount);
        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 3);
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsEmpty(snapshot.KgpImages);
    }

    [TestMethod]
    public void Snapshot_SubsetOfHistory_MaterializesTailWhoseAnchorIsNotSelected()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 4);
        PlaceImage(terminal, 1, 10, 30, displayColumns: 1, displayRows: 3);
        Apply(terminal, "\x1b[S\x1b[S\x1b[S");

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1);
        Assert.AreEqual(1, snapshot.ScrollbackLineCount);
        var placement = SinglePlacement(snapshot);
        Assert.AreEqual(0, placement.Row);
        Assert.AreEqual(1u, placement.DisplayRows);
        Assert.AreEqual(20u, placement.SourceY);
        Assert.AreEqual(10u, placement.SourceHeight);
        Assert.IsTrue(snapshot.KgpImages.ContainsKey(1));
    }

    [TestMethod]
    public void Snapshot_HistoryPlacementRemainsImmutableAfterLivePruning()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 1);
        PlaceImage(terminal, 1, 10, 20, displayColumns: 1, displayRows: 2);
        Apply(terminal, "\x1b[S");
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1);
        var captured = SinglePlacement(snapshot);

        Apply(terminal, "\x1b[S\x1b[S");

        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(0, captured.Row);
        Assert.AreEqual(2u, captured.DisplayRows);
        Assert.AreEqual(0u, captured.SourceY);
        Assert.AreEqual(20u, captured.SourceHeight);
        Assert.AreEqual(0xFF, snapshot.KgpImages[1].Data[0]);
    }

    [TestMethod]
    public void SharedImage_MultipleHistoryPlacementsRetainIndependentReferences()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 4);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(1, 10, 10, quiet: 2));
        Apply(terminal, "\x1b[1;1H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 1, displayColumns: 1, displayRows: 1, cursorMovement: 1));
        Apply(terminal, "\x1b[2;1H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 2, displayColumns: 1, displayRows: 1, cursorMovement: 1));

        Apply(terminal, "\x1b[S\x1b[S");

        Assert.AreEqual(2, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(2, terminal.GetKgpHistoryReferenceCount(1));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 2);
        Assert.AreEqual(2, snapshot.KgpPlacements.Count);
        TestSeq.All(
            snapshot.KgpPlacements,
            placement => Assert.IsTrue(snapshot.KgpImages.ContainsKey(placement.ImageId)));
    }

    [TestMethod]
    public void RetransmitImageId_RemovesHistoryPlacementBeforeReplacingData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 3);
        PlaceImage(terminal, 1, 10, 10, displayColumns: 1, displayRows: 1);
        Apply(terminal, "\x1b[S");
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);

        Apply(
            terminal,
            KgpTestHelper.BuildTransmitCommand(
                1,
                20,
                10,
                quiet: 2,
                fillByte: 0x44));

        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
        var replacement = terminal.KgpImageStore.GetImageById(1);
        Assert.IsNotNull(replacement);
        Assert.AreEqual(20u, replacement.Width);
        Assert.AreEqual(0x44, replacement.Data[0]);
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1);
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsEmpty(snapshot.KgpImages);
    }

    [TestMethod]
    public void FreeImageData_AfterHistoryScroll_ReconcilesHistoryWithoutDanglingPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 2);
        PlaceImage(terminal, 1, 10, 10, displayColumns: 1, displayRows: 1);
        Apply(terminal, "\x1b[S");

        Apply(terminal, KgpTestHelper.BuildDeleteCommand('A'));

        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1);
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsEmpty(snapshot.KgpImages);
    }

    [TestMethod]
    public void ExternalImageRemoval_AfterHistoryScroll_IsReconciledBeforeSnapshot()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 2);
        PlaceImage(terminal, 1, 10, 10, displayColumns: 1, displayRows: 1);
        Apply(terminal, "\x1b[S");

        Assert.IsTrue(terminal.KgpImageStore.RemoveImage(1));

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1);
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsEmpty(snapshot.KgpImages);
        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
    }

    [TestMethod]
    public void Dispose_ReleasesHistoryPlacementsAndImageData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 2);
        PlaceImage(terminal, 1, 10, 20, displayColumns: 1, displayRows: 2);
        Apply(terminal, "\x1b[S");

        terminal.Dispose();

        Assert.AreEqual(0, terminal.ScrollbackCount);
        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public void ResizeWithBuiltInReflow_PromotesHistoryAnchorWithoutLosingImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 4,
            height: 3,
            scrollbackCapacity: 4,
            reflow: KittyReflowStrategy.Instance);
        Apply(terminal, "ABCD");
        Apply(terminal, "\x1b[1;1H");
        Apply(
            terminal,
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                1,
                10,
                10,
                displayColumns: 1,
                displayRows: 1,
                cursorMovement: 1,
                quiet: 2));
        Apply(terminal, "\x1b[S");

        terminal.Resize(2, 3);

        Assert.AreEqual(0, terminal.ScrollbackCount);
        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        using var snapshot = terminal.CreateSnapshot();
        var placement = SinglePlacement(snapshot);
        Assert.AreEqual(0, placement.Row);
        Assert.AreEqual(1u, placement.DisplayRows);
        Assert.IsTrue(snapshot.KgpImages.ContainsKey(1));
    }

    [TestMethod]
    public void ResizeWithBuiltInReflow_CapacityDiscardClipsAndReanchorsHistoryTail()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 4,
            height: 2,
            scrollbackCapacity: 1,
            reflow: KittyReflowStrategy.Instance);
        Apply(terminal, "\x1b[1;1HABCD\x1b[2;1HEFGH\x1b[1;1H");
        Apply(
            terminal,
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                1,
                10,
                20,
                displayColumns: 1,
                displayRows: 2,
                cursorMovement: 1,
                quiet: 2));
        Apply(terminal, "\x1b[S\x1b[2;1HIJKL\x1b[1;1H");

        terminal.Resize(2, 2);

        Assert.AreEqual(1, terminal.ScrollbackCount);
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);
        AssertPlacement(
            terminal.CreateSnapshot(scrollbackLines: 1),
            row: 0,
            rows: 1,
            sourceY: 10,
            sourceHeight: 10);
        using var active = terminal.CreateSnapshot();
        Assert.IsEmpty(active.KgpPlacements);
    }

    [TestMethod]
    public void ResizeWithThirdPartyReflow_ReleasesUnprovableHistorySafely()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 4,
            height: 3,
            scrollbackCapacity: 4,
            reflow: new ThirdPartyNoReflowProvider());
        Apply(terminal, "ABCD");
        Apply(terminal, "\x1b[1;1H");
        Apply(
            terminal,
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                1,
                10,
                10,
                displayColumns: 1,
                displayRows: 1,
                cursorMovement: 1,
                quiet: 2));
        Apply(terminal, "\x1b[S");

        terminal.Resize(2, 3);

        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: terminal.ScrollbackCount);
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsEmpty(snapshot.KgpImages);
    }

    [TestMethod]
    public void ResizeWithCrop_ClipsActiveDestinationAndSource()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 6, height: 5);
        PlaceImage(
            terminal,
            1,
            imageWidth: 20,
            imageHeight: 20,
            displayColumns: 2,
            displayRows: 2,
            row: 2,
            column: 3);

        terminal.Resize(4, 3);

        using var snapshot = terminal.CreateSnapshot();
        var placement = SinglePlacement(snapshot);
        Assert.AreEqual(2, placement.Row);
        Assert.AreEqual(3, placement.Column);
        Assert.AreEqual(1u, placement.DisplayRows);
        Assert.AreEqual(1u, placement.DisplayColumns);
        Assert.AreEqual(10u, placement.SourceWidth);
        Assert.AreEqual(10u, placement.SourceHeight);
        Assert.AreEqual(0u, placement.SourceX);
        Assert.AreEqual(0u, placement.SourceY);
    }

    [TestMethod]
    public void Snapshot_OriginalScrollbackWidth_PreservesHistoricalPlacementColumns()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 6,
            height: 3,
            scrollbackCapacity: 2,
            reflow: NoReflowStrategy.Instance);
        PlaceImage(
            terminal,
            1,
            imageWidth: 20,
            imageHeight: 10,
            displayColumns: 2,
            displayRows: 1,
            row: 0,
            column: 4);
        Apply(terminal, "\x1b[S");
        terminal.Resize(3, 3);

        using (var currentWidth = terminal.CreateSnapshot(
            scrollbackLines: 1,
            scrollbackWidth: ScrollbackWidth.CurrentTerminal))
        {
            Assert.IsEmpty(currentWidth.KgpPlacements);
            Assert.IsEmpty(currentWidth.KgpImages);
        }

        using var originalWidth = terminal.CreateSnapshot(
            scrollbackLines: 1,
            scrollbackWidth: ScrollbackWidth.Original);
        Assert.AreEqual(6, originalWidth.Width);
        var placement = SinglePlacement(originalWidth);
        Assert.AreEqual(0, placement.Row);
        Assert.AreEqual(4, placement.Column);
        Assert.AreEqual(2u, placement.DisplayColumns);
        Assert.IsTrue(originalWidth.KgpImages.ContainsKey(1));

        terminal.Resize(6, 3);
        using var enlarged = terminal.CreateSnapshot(scrollbackLines: 1);
        var enlargedPlacement = SinglePlacement(enlarged);
        Assert.AreEqual(4, enlargedPlacement.Column);
        Assert.AreEqual(2u, enlargedPlacement.DisplayColumns);
    }

    [TestMethod]
    public void PngUnknownDimensions_PlacementUsesDestinationOnlyScrollClipping()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, height: 4);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,f=100,i=1,p=1,c=1,r=2,C=1,q=2",
                [0x89, 0x50, 0x4E, 0x47]));

        var initial = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(2u, initial.DisplayRows);
        Assert.AreEqual(0u, initial.SourceX);
        Assert.AreEqual(0u, initial.SourceY);
        Assert.AreEqual(0u, initial.SourceWidth);
        Assert.AreEqual(0u, initial.SourceHeight);
        Assert.AreEqual(0u, terminal.KgpImageStore.GetImageById(1)!.Width);
        Assert.AreEqual(0u, terminal.KgpImageStore.GetImageById(1)!.Height);

        Apply(terminal, "\x1b[S");

        var clipped = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(0, clipped.Row);
        Assert.AreEqual(1u, clipped.DisplayRows);
        Assert.AreEqual(0u, clipped.SourceX);
        Assert.AreEqual(0u, clipped.SourceY);
        Assert.AreEqual(0u, clipped.SourceWidth);
        Assert.AreEqual(0u, clipped.SourceHeight);

        Apply(terminal, "\x1b[S");
        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    public void PngUnknownDimensions_HistoryReanchorsUntilDestinationIsPruned()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 1);
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,f=100,i=1,p=1,c=1,r=2,C=1,q=2",
                [0x89, 0x50, 0x4E, 0x47]));

        Apply(terminal, "\x1b[S");
        using (var first = terminal.CreateSnapshot(scrollbackLines: 1))
        {
            var placement = SinglePlacement(first);
            Assert.AreEqual(2u, placement.DisplayRows);
            Assert.AreEqual(0u, placement.SourceWidth);
            Assert.AreEqual(0u, placement.SourceHeight);
        }
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);

        Apply(terminal, "\x1b[S");
        using (var second = terminal.CreateSnapshot(scrollbackLines: 1))
        {
            var placement = SinglePlacement(second);
            Assert.AreEqual(1u, placement.DisplayRows);
            Assert.AreEqual(0u, placement.SourceWidth);
            Assert.AreEqual(0u, placement.SourceHeight);
        }
        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);

        Apply(terminal, "\x1b[S");
        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpHistoryReferenceCount(1));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    public void Put_NonzeroPair_ReplacesExactActiveAndHistoryPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 3);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(1, 10, 10, quiet: 2));
        Apply(terminal, "\x1b[1;1H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(
            1,
            placementId: 7,
            displayColumns: 1,
            displayRows: 1,
            cursorMovement: 1));
        Apply(terminal, KgpTestHelper.BuildPutCommand(
            1,
            placementId: 8,
            displayColumns: 1,
            displayRows: 1,
            cursorMovement: 1));
        Apply(terminal, "\x1b[S");
        Assert.AreEqual(2, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(2, terminal.GetKgpHistoryReferenceCount(1));

        Apply(terminal, "\x1b[3;2H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(
            1,
            placementId: 7,
            displayColumns: 2,
            displayRows: 1,
            cursorMovement: 1));

        Assert.AreEqual(1, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(1, terminal.GetKgpHistoryReferenceCount(1));
        var active = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(7u, active.PlacementId);
        Assert.AreEqual(2, active.Row);
        Assert.AreEqual(1, active.Column);
        Assert.AreEqual(2u, active.DisplayColumns);
        using var withHistory = terminal.CreateSnapshot(scrollbackLines: 1);
        var placements = withHistory.KgpPlacements
            .OrderBy(placement => placement.PlacementId)
            .ToArray();
        Assert.AreEqual(2, placements.Length);
        Assert.AreEqual(7u, placements[0].PlacementId);
        Assert.AreEqual(8u, placements[1].PlacementId);
    }

    [TestMethod]
    public void Put_ZeroPlacementId_RemainsAppendOnlyAcrossHistory()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 2);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(1, 10, 10, quiet: 2));
        Apply(terminal, "\x1b[1;1H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(
            1,
            displayColumns: 1,
            displayRows: 1,
            cursorMovement: 1));
        Apply(terminal, "\x1b[S\x1b[2;1H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(
            1,
            displayColumns: 1,
            displayRows: 1,
            cursorMovement: 1));

        Assert.AreEqual(1, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(1, terminal.GetKgpHistoryReferenceCount(1));
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1);
        Assert.AreEqual(2, snapshot.KgpPlacements.Count);
        TestSeq.All(
            snapshot.KgpPlacements,
            placement => Assert.AreEqual(0u, placement.PlacementId));
    }

    [TestMethod]
    public void Put_NonzeroPair_InAlternateScreen_DoesNotReplaceMainHistory()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            height: 4,
            scrollbackCapacity: 2);
        Apply(terminal, KgpTestHelper.BuildTransmitCommand(1, 10, 10, quiet: 2));
        Apply(terminal, "\x1b[1;1H");
        Apply(terminal, KgpTestHelper.BuildPutCommand(
            1,
            placementId: 7,
            displayColumns: 1,
            displayRows: 1,
            cursorMovement: 1));
        Apply(terminal, "\x1b[S\x1b[?1049h");
        Apply(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=T,f=32,s=10,v=10,i=1,p=7,c=1,r=1,C=1,q=2",
                KgpTestHelper.CreatePixelData(10, 10, fillByte: 0x44)));
        Apply(terminal, "\x1b[?1049l");

        AssertHistoryOwner(terminal, expectedPlacements: 1, expectedReferences: 1);
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1);
        var placement = SinglePlacement(snapshot);
        Assert.AreEqual(7u, placement.PlacementId);
        Assert.AreEqual(0xFF, snapshot.KgpImages[1].Data[0]);
    }

    [TestMethod]
    public void NoReflow_ActiveAnchorOutsideShrinkViewport_DoesNotClampOrResurrect()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 6,
            height: 3,
            reflow: NoReflowStrategy.Instance);
        PlaceImage(
            terminal,
            1,
            imageWidth: 20,
            imageHeight: 10,
            displayColumns: 2,
            displayRows: 1,
            column: 4);

        terminal.Resize(3, 3);
        Assert.IsEmpty(terminal.KgpPlacements);

        terminal.Resize(6, 3);
        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    public void BuiltInReflow_BlankRowHeightChange_PreservesAnchorColumn()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 6,
            height: 4,
            reflow: KittyReflowStrategy.Instance);
        PlaceImage(
            terminal,
            1,
            imageWidth: 10,
            imageHeight: 10,
            displayColumns: 1,
            displayRows: 1,
            column: 4);

        terminal.Resize(6, 3);

        var placement = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(4, placement.Column);
    }

    [TestMethod]
    public void BuiltInReflow_BlankRowWidthChange_ClampsAnchorColumnInsteadOfCollapsingToZero()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 6,
            height: 3,
            reflow: KittyReflowStrategy.Instance);
        PlaceImage(
            terminal,
            1,
            imageWidth: 10,
            imageHeight: 10,
            displayColumns: 1,
            displayRows: 1,
            column: 4);

        terminal.Resize(3, 3);

        var placement = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(2, placement.Column);
    }

    [TestMethod]
    public void BuiltInReflow_PopulatedWrappedRow_MapsAnchorColumnWithText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 6,
            height: 3,
            reflow: KittyReflowStrategy.Instance);
        Apply(terminal, "ABCDEF\x1b[1;5H");
        Apply(
            terminal,
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                1,
                10,
                10,
                displayColumns: 1,
                displayRows: 1,
                cursorMovement: 1,
                quiet: 2));

        terminal.Resize(3, 3);

        var placement = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(1, placement.Column);
    }

    [TestMethod]
    public void AlternateResize_ExitCropsHiddenMainPlacementAndPreventsResurrection()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 6, height: 4);
        PlaceImage(
            terminal,
            1,
            imageWidth: 20,
            imageHeight: 20,
            displayColumns: 2,
            displayRows: 2,
            row: 2,
            column: 4);
        Apply(terminal, "\x1b[?1049h");

        terminal.Resize(3, 2);
        Apply(terminal, "\x1b[?1049l");

        Assert.IsEmpty(terminal.KgpPlacements);
        terminal.Resize(6, 4);
        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    public void AlternateResize_ExitPermanentlyClipsPartiallyVisibleMainPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 6, height: 4);
        PlaceImage(
            terminal,
            1,
            imageWidth: 20,
            imageHeight: 20,
            displayColumns: 2,
            displayRows: 2,
            row: 1,
            column: 2);
        Apply(terminal, "\x1b[?1049h");

        terminal.Resize(3, 2);
        Apply(terminal, "\x1b[?1049l");

        using (var cropped = terminal.CreateSnapshot())
        {
            var placement = SinglePlacement(cropped);
            Assert.AreEqual(1, placement.Row);
            Assert.AreEqual(2, placement.Column);
            Assert.AreEqual(1u, placement.DisplayRows);
            Assert.AreEqual(1u, placement.DisplayColumns);
            Assert.AreEqual(10u, placement.SourceWidth);
            Assert.AreEqual(10u, placement.SourceHeight);
        }

        terminal.Resize(6, 4);
        using var enlarged = terminal.CreateSnapshot();
        var enlargedPlacement = SinglePlacement(enlarged);
        Assert.AreEqual(1u, enlargedPlacement.DisplayRows);
        Assert.AreEqual(1u, enlargedPlacement.DisplayColumns);
        Assert.AreEqual(10u, enlargedPlacement.SourceWidth);
        Assert.AreEqual(10u, enlargedPlacement.SourceHeight);
    }

    [TestMethod]
    public void AlternateResize_ExitTrimsMainHistoryTailToCurrentHeight()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 6,
            height: 4,
            scrollbackCapacity: 2);
        PlaceImage(
            terminal,
            1,
            imageWidth: 10,
            imageHeight: 30,
            displayColumns: 1,
            displayRows: 3);
        Apply(terminal, "\x1b[S\x1b[?1049h");

        terminal.Resize(6, 1);
        Apply(terminal, "\x1b[?1049l");

        AssertPlacement(
            terminal.CreateSnapshot(scrollbackLines: 1),
            row: 0,
            rows: 2,
            sourceY: 0,
            sourceHeight: 20);
        terminal.Resize(6, 4);
        AssertPlacement(
            terminal.CreateSnapshot(scrollbackLines: 1),
            row: 0,
            rows: 2,
            sourceY: 0,
            sourceHeight: 20);
    }

    private static void AssertPlacement(
        Hex1bTerminalSnapshot snapshot,
        int row,
        uint rows,
        uint sourceY,
        uint sourceHeight)
    {
        using (snapshot)
        {
            var placement = SinglePlacement(snapshot);
            Assert.AreEqual(row, placement.Row);
            Assert.AreEqual(rows, placement.DisplayRows);
            Assert.AreEqual(sourceY, placement.SourceY);
            Assert.AreEqual(sourceHeight, placement.SourceHeight);
            Assert.IsTrue(snapshot.KgpImages.ContainsKey(placement.ImageId));
        }
    }

    private static void AssertHistoryOwner(
        Hex1bTerminal terminal,
        int expectedPlacements,
        int expectedReferences)
    {
        Assert.AreEqual(expectedPlacements, terminal.KgpHistoryPlacementCount);
        Assert.AreEqual(expectedReferences, terminal.GetKgpHistoryReferenceCount(1));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
    }

    private sealed class ThirdPartyNoReflowProvider : ITerminalReflowProvider
    {
        public bool ShouldClearSoftWrapOnAbsolutePosition => false;

        public ReflowResult Reflow(ReflowContext context)
            => NoReflowStrategy.Instance.Reflow(context);
    }
}
