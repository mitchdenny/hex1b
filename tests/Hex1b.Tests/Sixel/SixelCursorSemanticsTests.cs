using System.Text;
using Hex1b.Sixel;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Covers how <c>Hex1bTerminal</c> interprets an incoming Sixel sequence: where the
/// placement is anchored, how it is clipped, and where the text cursor ends up.
/// </summary>
/// <remarks>
/// These tests exercise the terminal-model direction of the data flow. Hex1b's own
/// emitter behavior is covered by <c>SixelEmitterCursorTests</c>.
/// </remarks>
[TestClass]
public class SixelCursorSemanticsTests
{
    private const string SquareBand = "7q#1;2;100;0;0";

    /// <summary>
    /// Builds a square-aspect graphic that declares an exact pixel extent, so the
    /// rendered extent used for occupancy is fully deterministic.
    /// </summary>
    private static byte[] Graphic(int pixelWidth, int pixelHeight, string prefix = "")
    {
        var payload = new StringBuilder();
        payload.Append(prefix);
        payload.Append("\x1bP");
        payload.Append(SquareBand);

        // DECGRA declares the raster; a single painted sixel keeps the graphic
        // valid without changing the declared extent.
        payload.Append($"\"1;1;{pixelWidth};{pixelHeight}");
        payload.Append("#1@");
        payload.Append("\x1b\\");
        return Encoding.ASCII.GetBytes(payload.ToString());
    }

    private static SixelCellMetrics Metrics(double width, double height) => new(
        width,
        height,
        SixelCellMetricsSource.Direct,
        SixelCellMetricsReliability.Authoritative);

    private static async Task<SixelTerminalObservation> RunAsync(
        SixelTestTerminal terminal,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await terminal.FeedAsync(bytes, cancellationToken: cancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "Sixel placement",
            cancellationToken);
        return terminal.Observe();
    }

    [TestMethod]
    // One pixel below, exactly on, and one pixel above a cell boundary for each
    // metric, so ceiling division is pinned at every edge.
    [DataRow(9d, 20d, 17, 39, 2, 2)]
    [DataRow(9d, 20d, 18, 40, 2, 2)]
    [DataRow(9d, 20d, 19, 41, 3, 3)]
    [DataRow(10d, 20d, 19, 39, 2, 2)]
    [DataRow(10d, 20d, 20, 40, 2, 2)]
    [DataRow(10d, 20d, 21, 41, 3, 3)]
    [DataRow(8d, 16d, 15, 31, 2, 2)]
    [DataRow(8d, 16d, 16, 32, 2, 2)]
    [DataRow(8d, 16d, 17, 33, 3, 3)]
    [DataRow(12d, 24d, 23, 47, 2, 2)]
    [DataRow(12d, 24d, 24, 48, 2, 2)]
    [DataRow(12d, 24d, 25, 49, 3, 3)]
    // Fractional metrics must not be truncated to integers before dividing.
    [DataRow(7.5d, 16.5d, 15, 33, 2, 2)]
    [DataRow(7.5d, 16.5d, 16, 34, 3, 3)]
    [DataRow(9.6d, 20.4d, 20, 41, 3, 3)]
    public async Task ProcessSixelData_RenderedExtentAndMetrics_UsesCeilingDivision(
        double cellWidth,
        double cellHeight,
        int pixelWidth,
        int pixelHeight,
        int expectedColumns,
        int expectedRows)
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 40,
            height: 20,
            cellMetrics: Metrics(cellWidth, cellHeight));

        var observation = await RunAsync(
            terminal,
            Graphic(pixelWidth, pixelHeight),
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(expectedColumns, placement.WidthInCells);
        Assert.AreEqual(expectedRows, placement.HeightInCells);
    }

    [TestMethod]
    public async Task ProcessSixelData_InjectedMetrics_AreCapturedOnThePlacement()
    {
        await using var terminal = SixelTestTerminal.Create(cellMetrics: Metrics(9, 20));

        var observation = await RunAsync(
            terminal,
            Graphic(18, 20),
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(9d, placement.CellMetrics.Width);
        Assert.AreEqual(20d, placement.CellMetrics.Height);
        Assert.AreEqual(SixelCellMetricsSource.Direct, placement.CellMetrics.Source);
        Assert.AreEqual(SixelCellMetricsReliability.Authoritative, placement.CellMetrics.Reliability);
        Assert.IsTrue(placement.CellMetrics.IsAuthoritative);
    }

    [TestMethod]
    public void SixelCellMetrics_DerivedFromCapabilities_IsReportedAsEstimated()
    {
        var capabilities = new TerminalCapabilities
        {
            CellPixelWidth = 10,
            CellPixelHeight = 20,
            ActualCellPixelWidth = 9.5,
        };

        var metrics = SixelCellMetrics.FromCapabilities(capabilities);

        Assert.AreEqual(9.5d, metrics.Width);
        Assert.AreEqual(20d, metrics.Height);
        Assert.AreEqual(SixelCellMetricsSource.Derived, metrics.Source);
        Assert.AreEqual(SixelCellMetricsReliability.Estimated, metrics.Reliability);
        Assert.IsFalse(metrics.IsAuthoritative);
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(-4d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void SixelCellMetrics_NonPositiveOrNonFinite_FallsBackToTheAssumedGrid(double invalid)
    {
        var metrics = new SixelCellMetrics(
            invalid,
            invalid,
            SixelCellMetricsSource.Assumed,
            SixelCellMetricsReliability.Estimated);

        Assert.AreEqual(SixelCellMetrics.Unknown.Width, metrics.SafeWidth);
        Assert.AreEqual(SixelCellMetrics.Unknown.Height, metrics.SafeHeight);
        Assert.AreEqual(1, metrics.ColumnsFor(1));
        Assert.AreEqual(0, metrics.RowsFor(0));
    }

    [TestMethod]
    public async Task ProcessSixelData_MultiRowGraphic_LeavesCursorBelowAtOriginalColumn()
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 20,
            height: 12,
            cellMetrics: Metrics(10, 20));

        var observation = await RunAsync(
            terminal,
            Graphic(30, 60, "\x1b[4;6H"),
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(5, placement.OriginColumn);
        Assert.AreEqual(3, placement.OriginRow);
        Assert.AreEqual(3, placement.WidthInCells);
        Assert.AreEqual(3, placement.HeightInCells);
        Assert.AreEqual(5, observation.CursorX);
        Assert.AreEqual(6, observation.CursorY);
    }

    [TestMethod]
    public async Task ProcessSixelData_OneRowGraphic_LeavesCursorOnTheNextRow()
    {
        await using var terminal = SixelTestTerminal.Create(cellMetrics: Metrics(10, 20));

        var observation = await RunAsync(
            terminal,
            Graphic(10, 20, "\x1b[2;3H"),
            TestContext.Current.CancellationToken);

        Assert.AreEqual(1, TestSeq.Single(observation.Placements).HeightInCells);
        Assert.AreEqual(2, observation.CursorX);
        Assert.AreEqual(2, observation.CursorY);
    }

    [TestMethod]
    public async Task ProcessSixelData_PartialFinalBand_RoundsUpToAWholeRow()
    {
        await using var terminal = SixelTestTerminal.Create(cellMetrics: Metrics(10, 20));

        var observation = await RunAsync(
            terminal,
            Graphic(10, 21, "\x1b[2;3H"),
            TestContext.Current.CancellationToken);

        Assert.AreEqual(2, TestSeq.Single(observation.Placements).HeightInCells);
        Assert.AreEqual(3, observation.CursorY);
    }

    [TestMethod]
    public async Task ProcessSixelData_DeclaredExtentWithoutPaintedPixels_StillOccupiesCells()
    {
        await using var terminal = SixelTestTerminal.Create(cellMetrics: Metrics(10, 20));

        // A raster attribute with no sixel data at all: the declared extent is the
        // only geometry available and must still occupy cells.
        var bytes = Encoding.ASCII.GetBytes("\x1b[2;2H\x1bP7q\"1;1;30;40\x1b\\");
        var observation = await RunAsync(terminal, bytes, TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(3, placement.WidthInCells);
        Assert.AreEqual(2, placement.HeightInCells);
        Assert.AreEqual(1, observation.CursorX);
        Assert.AreEqual(3, observation.CursorY);
    }

    [TestMethod]
    public async Task ProcessSixelData_NonScrollingMode_UsesPageOriginAndLeavesTheCursorAlone()
    {
        await using var terminal = SixelTestTerminal.Create(cellMetrics: Metrics(10, 20));

        var observation = await RunAsync(
            terminal,
            Graphic(20, 40, "\x1b[?80l\x1b[5;7H"),
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(0, placement.OriginColumn);
        Assert.AreEqual(0, placement.OriginRow);
        Assert.AreEqual(6, observation.CursorX);
        Assert.AreEqual(4, observation.CursorY);
    }

    [TestMethod]
    public async Task ProcessSixelData_NonScrollingModeWithMargins_IgnoresTextMargins()
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 20,
            height: 12,
            cellMetrics: Metrics(10, 20));

        // Margins constrain text, but DECSDM display mode paints the whole page.
        var observation = await RunAsync(
            terminal,
            Graphic(60, 80, "\x1b[?69h\x1b[4;10s\x1b[3;8r\x1b[?80l"),
            TestContext.Current.CancellationToken);

        Assert.IsTrue(observation.OccupiedCells.Any(cell => cell.Column == 0 && cell.Row == 0));
        Assert.IsTrue(observation.OccupiedCells.Any(cell => cell.Row == 3));
    }

    [TestMethod]
    public async Task ProcessSixelData_Mode8452Set_LeavesCursorRightOfTheGraphic()
    {
        await using var terminal = SixelTestTerminal.Create(cellMetrics: Metrics(10, 20));

        var observation = await RunAsync(
            terminal,
            Graphic(30, 40, "\x1b[?8452h\x1b[2;3H"),
            TestContext.Current.CancellationToken);

        Assert.AreEqual(5, observation.CursorX);
        Assert.AreEqual(3, observation.CursorY);
    }

    [TestMethod]
    public async Task ProcessSixelData_AtBottomMargin_ScrollsTheRegionAndKeepsTheCursorInside()
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 20,
            height: 10,
            cellMetrics: Metrics(10, 20));

        // Rows 2..6 (1-based 3..7) form the region; the cursor starts on its last
        // row, so a two-row graphic has to scroll the region twice.
        var observation = await RunAsync(
            terminal,
            Graphic(10, 40, "\x1b[3;7r\x1b[7;1H"),
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(2, placement.HeightInCells);
        Assert.AreEqual(4, placement.OriginRow);
        Assert.AreEqual(0, observation.CursorX);
        Assert.AreEqual(6, observation.CursorY);
        Assert.IsTrue(observation.OccupiedRows.All(row => row.Row is >= 2 and <= 6));
    }

    [TestMethod]
    public async Task ProcessSixelData_TallerThanTheRegion_ClipsInsteadOfEscapingTheMargins()
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 20,
            height: 10,
            cellMetrics: Metrics(10, 20));

        var observation = await RunAsync(
            terminal,
            Graphic(10, 200, "\x1b[3;6r\x1b[4;1H"),
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(observation.Placements);

        // The placement keeps the full source geometry; only the painted cells are
        // clipped, so the raster is never corrupted by the margins.
        Assert.AreEqual(10, placement.HeightInCells);
        Assert.IsTrue(observation.OccupiedRows.All(row => row.Row is >= 2 and <= 5));
        Assert.AreEqual(5, observation.CursorY);
    }

    [TestMethod]
    public async Task ProcessSixelData_WithLeftRightMargins_ClipsHorizontally()
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 20,
            height: 10,
            cellMetrics: Metrics(10, 20));

        var observation = await RunAsync(
            terminal,
            Graphic(120, 20, "\x1b[?69h\x1b[3;8s\x1b[1;3H"),
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(12, placement.WidthInCells);
        Assert.IsTrue(observation.OccupiedCells.All(cell => cell.Column is >= 2 and <= 7));
    }

    [TestMethod]
    public async Task ProcessSixelData_ExceedingTheViewport_ClipsToTheVisibleCells()
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 8,
            height: 6,
            cellMetrics: Metrics(10, 20));

        var observation = await RunAsync(
            terminal,
            Graphic(200, 200, "\x1b[1;1H"),
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(20, placement.WidthInCells);
        Assert.AreEqual(10, placement.HeightInCells);
        Assert.IsTrue(observation.OccupiedCells.All(cell => cell.Column < 8 && cell.Row < 6));
        Assert.AreEqual(5, observation.CursorY);
    }

    [TestMethod]
    public async Task ProcessSixelData_OriginModeWithMargins_AnchorsRelativeToTheRegion()
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 20,
            height: 12,
            cellMetrics: Metrics(10, 20));

        var observation = await RunAsync(
            terminal,
            Graphic(10, 20, "\x1b[?69h\x1b[4;12s\x1b[3;9r\x1b[?6h\x1b[1;1H"),
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(3, placement.OriginColumn);
        Assert.AreEqual(2, placement.OriginRow);
        Assert.AreEqual(3, observation.CursorY);
    }

    [TestMethod]
    public async Task ProcessSixelData_FollowedByText_WritesAtTheReportedCursor()
    {
        await using var terminal = SixelTestTerminal.Create(cellMetrics: Metrics(10, 20));

        await terminal.FeedAsync(
            Graphic(20, 40, "\x1b[2;3H"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic",
            TestContext.Current.CancellationToken);
        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("ok"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.GetLine(3).Contains("ok", StringComparison.Ordinal),
            "text after graphic",
            TestContext.Current.CancellationToken);

        var observation = terminal.Observe();
        Assert.StartsWith("  ok", observation.Lines[3]);
        Assert.AreEqual(4, observation.CursorX);
        Assert.AreEqual(3, observation.CursorY);
    }

    [TestMethod]
    [DataRow("\r", 0, 3)]
    [DataRow("\n", 2, 4)]
    [DataRow("\x1b[1;1H", 0, 0)]
    public async Task ProcessSixelData_FollowedByCursorControl_AppliesFromTheFinalPosition(
        string suffix,
        int expectedCursorX,
        int expectedCursorY)
    {
        await using var terminal = SixelTestTerminal.Create(cellMetrics: Metrics(10, 20));

        var bytes = Graphic(20, 40, "\x1b[2;3H")
            .Concat(Encoding.ASCII.GetBytes(suffix))
            .ToArray();
        var observation = await RunAsync(terminal, bytes, TestContext.Current.CancellationToken);

        Assert.AreEqual(expectedCursorX, observation.CursorX);
        Assert.AreEqual(expectedCursorY, observation.CursorY);
    }

    [TestMethod]
    public async Task ProcessSixelData_FollowedByAnotherSixel_StacksBelowTheFirst()
    {
        await using var terminal = SixelTestTerminal.Create(cellMetrics: Metrics(10, 20));

        var bytes = Graphic(20, 40, "\x1b[2;3H")
            .Concat(Graphic(30, 20))
            .ToArray();
        var observation = await RunAsync(terminal, bytes, TestContext.Current.CancellationToken);

        Assert.HasCount(2, observation.Placements);
        var first = observation.Placements.First(p => p.OriginRow == 1);
        var second = observation.Placements.First(p => p.OriginRow == 3);
        Assert.AreEqual(2, first.OriginColumn);
        Assert.AreEqual(2, second.OriginColumn);
        Assert.AreEqual(4, observation.CursorY);
    }

    [TestMethod]
    public async Task ProcessSixelData_AfterAPendingWrap_ClearsTheDeferredWrap()
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 4,
            height: 6,
            cellMetrics: Metrics(10, 20));

        // Fill the row so the cursor is parked in the deferred-wrap state.
        var bytes = Encoding.ASCII.GetBytes("\x1b[1;1Habcd")
            .Concat(Graphic(10, 20))
            .ToArray();
        var observation = await RunAsync(terminal, bytes, TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(3, placement.OriginColumn);
        Assert.AreEqual(3, observation.CursorX);
        Assert.AreEqual(1, observation.CursorY);
    }

    [TestMethod]
    [DataRow("\x1b[?80l", "\x1b[!p")]
    [DataRow("\x1b[?8452h", "\x1b[!p")]
    public async Task SoftReset_AfterChangingSixelModes_RestoresTheDefaults(
        string modeChange,
        string reset)
    {
        await using var terminal = SixelTestTerminal.Create(cellMetrics: Metrics(10, 20));

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes(modeChange + reset),
            cancellationToken: TestContext.Current.CancellationToken);
        var observation = await RunAsync(
            terminal,
            Graphic(20, 40, "\x1b[2;3H"),
            TestContext.Current.CancellationToken);

        Assert.IsTrue(terminal.Terminal.SixelScrollingModeEnabled);
        Assert.IsFalse(terminal.Terminal.SixelCursorToRightModeEnabled);
        Assert.AreEqual(2, TestSeq.Single(observation.Placements).OriginColumn);
        Assert.AreEqual(2, observation.CursorX);
        Assert.AreEqual(3, observation.CursorY);
    }

    [TestMethod]
    public async Task Ris_AfterChangingSixelModes_RestoresTheDefaults()
    {
        await using var terminal = SixelTestTerminal.Create(cellMetrics: Metrics(10, 20));

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[?80l\x1b[?8452h"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.FeedPreTokenizedAsync(
            Encoding.ASCII.GetBytes("\x1bc"),
            [Hex1b.Tokens.RisToken.Instance],
            TestContext.Current.CancellationToken);

        // Feeding a graphic afterwards proves the reset reached the model before
        // the next sequence was interpreted.
        var observation = await RunAsync(
            terminal,
            Graphic(20, 40, "\x1b[2;3H"),
            TestContext.Current.CancellationToken);

        Assert.AreEqual(2, TestSeq.Single(observation.Placements).OriginColumn);
        Assert.AreEqual(2, observation.CursorX);
        Assert.IsTrue(terminal.Terminal.SixelScrollingModeEnabled);
        Assert.IsFalse(terminal.Terminal.SixelCursorToRightModeEnabled);
    }

    [TestMethod]
    public void ResolveSixelScrolling_DecPolarity_SetEnablesScrolling()
    {
        var policy = SixelCompatibilityPolicy.Default;

        Assert.AreEqual(SixelDecsdmPolarity.Dec, policy.DecsdmPolarity);
        Assert.IsTrue(policy.ResolveSixelScrolling(decsdmEnabled: true));
        Assert.IsFalse(policy.ResolveSixelScrolling(decsdmEnabled: false));
    }

    [TestMethod]
    public void ResolveSixelScrolling_XtermPolarity_InvertsSetAndReset()
    {
        var policy = SixelCompatibilityPolicy.Default with
        {
            DecsdmPolarity = SixelDecsdmPolarity.Xterm,
        };

        Assert.IsFalse(policy.ResolveSixelScrolling(decsdmEnabled: true));
        Assert.IsTrue(policy.ResolveSixelScrolling(decsdmEnabled: false));
    }

    [TestMethod]
    [DataRow(999999999, 999999999)]
    [DataRow(int.MaxValue, int.MaxValue)]
    public async Task ProcessSixelData_DeclaredExtentFarBeyondViewport_PaintsOnlyTheVisibleIntersection(
        int pixelWidth,
        int pixelHeight)
    {
        // A geometry-only frame can declare hundreds of millions of cells. Painting
        // must be bounded by the viewport rather than walking the declared extent,
        // otherwise the terminal stalls before any output reaches the presentation.
        await using var terminal = SixelTestTerminal.Create(
            width: 40,
            height: 20,
            cellMetrics: Metrics(10, 20));

        var observation = await RunAsync(
            terminal,
            Graphic(pixelWidth, pixelHeight),
            TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // The placement still records its unclipped source geometry.
        var placement = TestSeq.Single(observation.Placements);
        Assert.AreEqual(0, placement.OriginColumn);
        Assert.AreEqual(0, placement.OriginRow);

        // The cursor is clamped into the region instead of overflowing.
        Assert.IsGreaterThanOrEqualTo(0, observation.CursorY);
        Assert.IsLessThan(20, observation.CursorY);
        Assert.IsGreaterThanOrEqualTo(0, observation.CursorX);
        Assert.IsLessThan(40, observation.CursorX);
    }
}
