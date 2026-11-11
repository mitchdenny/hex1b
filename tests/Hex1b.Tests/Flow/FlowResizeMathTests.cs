using Hex1b.Flow;

namespace Hex1b.Tests.Flow;

/// <summary>
/// Tests for <see cref="FlowResizeMath"/> — the pure helpers that decide,
/// on a flow resize, how tall the active step should be and which rows the
/// runner should clear.
/// </summary>
[TestClass]
public class FlowResizeMathTests
{
    [TestMethod]
    [DataRow(80, 2, 78)] // Initial layout reserves the configured slack
    [DataRow(5, 2, 3)] // Narrow terminals retain a usable width
    public void ComputeInitialContentWidth_ReservesSlack(
        int terminalWidth, int widthSlack, int expected)
    {
        var actual = FlowResizeMath.ComputeInitialContentWidth(terminalWidth, widthSlack);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(78, 100, 2, 78, 78)] // Growth is capped at the original width
    [DataRow(78, 79, 2, 78, 78)] // Shrink within slack preserves the layout
    [DataRow(78, 70, 2, 78, 68)] // Crossing the boundary reclaims slack
    [DataRow(68, 71, 2, 78, 68)] // Small growth does not cause relayout
    [DataRow(68, 72, 2, 78, 70)] // Growth recovers a full slack interval
    [DataRow(68, 80, 2, 78, 78)] // Restoring terminal width restores content
    [DataRow(77, 80, 2, 78, 78)] // Final recovery may be less than the slack
    [DataRow(5, 1, 2, 78, 1)] // Narrow terminals still get a usable width
    [DataRow(80, 70, 0, 80, 70)] // Zero slack preserves legacy behavior
    [DataRow(70, 100, 0, 80, 100)]
    public void ComputeContentWidth_UsesReservedSlack(
        int currentContentWidth, int terminalWidth, int widthSlack, int preferredContentWidth, int expected)
    {
        var actual = FlowResizeMath.ComputeContentWidth(
            currentContentWidth, terminalWidth, widthSlack, preferredContentWidth);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(null, 24, 24)] // No max → fills terminal
    [DataRow(10, 24, 10)]   // Max smaller than terminal → max wins
    [DataRow(40, 24, 24)]   // Max larger than terminal → terminal wins
    [DataRow(0, 24, 1)]     // Zero clamps to 1
    [DataRow(-5, 24, 1)]    // Negative clamps to 1
    public void ComputeStepHeight_ClampsToTerminalAndMinimumOfOne(
        int? maxHeight, int terminalHeight, int expected)
    {
        var actual = FlowResizeMath.ComputeStepHeight(maxHeight, terminalHeight);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ComputeClearRegion_LegacyPath_ClearsEntireVisibleArea()
    {
        // Legacy behaviour: cell-positioned tombstones can't survive a resize,
        // so the runner wipes the whole visible area to avoid leaving
        // half-reflowed garbage on screen.
        var (origin, height) = FlowResizeMath.ComputeClearRegion(
            useSoftWrapTombstones: false,
            terminalHeight: 24,
            stepHeight: 5);

        Assert.AreEqual(0, origin);
        Assert.AreEqual(24, height);
    }

    [TestMethod]
    public void ComputeClearRegion_LegacyPath_StepHeightDoesNotShrinkClear()
    {
        // Even when the step is small, the legacy path still clears the
        // entire visible area — tombstones can't be preserved either way.
        var (origin, height) = FlowResizeMath.ComputeClearRegion(
            useSoftWrapTombstones: false,
            terminalHeight: 100,
            stepHeight: 1);

        Assert.AreEqual(0, origin);
        Assert.AreEqual(100, height);
    }

    [TestMethod]
    public void ComputeClearRegion_SoftWrapPath_ClearsOnlyTheActiveStepRegion()
    {
        // Soft-wrap behaviour: tombstones above the active step are real
        // logical lines that the host terminal has already reflowed. The
        // runner must scope its clear to the bottom-aligned active-step
        // region; clearing higher rows would erase tombstones the user is
        // actively reading.
        var (origin, height) = FlowResizeMath.ComputeClearRegion(
            useSoftWrapTombstones: true,
            terminalHeight: 24,
            stepHeight: 5);

        Assert.AreEqual(19, origin); // 24 - 5
        Assert.AreEqual(5, height);
    }

    [TestMethod]
    public void ComputeClearRegion_SoftWrapPath_StepFillsTerminal_ClearsEntireArea()
    {
        // When the step occupies the full terminal there is no tombstone
        // history to preserve, so the soft-wrap path effectively matches the
        // legacy clear region — but it gets there via the bottom-anchored
        // calculation, not by special-casing.
        var (origin, height) = FlowResizeMath.ComputeClearRegion(
            useSoftWrapTombstones: true,
            terminalHeight: 24,
            stepHeight: 24);

        Assert.AreEqual(0, origin);
        Assert.AreEqual(24, height);
    }

    [TestMethod]
    public void ComputeClearRegion_SoftWrapPath_StepLargerThanTerminal_ClampsRowOriginAtZero()
    {
        // Defensive: callers should already have clamped via
        // ComputeStepHeight, but if they pass a step taller than the
        // terminal we still produce a valid (non-negative) row origin.
        var (origin, height) = FlowResizeMath.ComputeClearRegion(
            useSoftWrapTombstones: true,
            terminalHeight: 10,
            stepHeight: 20);

        Assert.AreEqual(0, origin);
        Assert.AreEqual(20, height);
    }

    [TestMethod]
    public void ComputeClearRegion_SoftWrapPath_PreservesTombstonesAboveOrigin()
    {
        // The crux of the fix: with a 24-row terminal and a 3-row active
        // step, rows 0..20 hold tombstones and must NOT be cleared.
        var (origin, _) = FlowResizeMath.ComputeClearRegion(
            useSoftWrapTombstones: true,
            terminalHeight: 24,
            stepHeight: 3);

        // Origin is 21, so rows 0..20 are untouched — exactly the tombstone
        // history the terminal just reflowed for us.
        Assert.AreEqual(21, origin);
    }
}
