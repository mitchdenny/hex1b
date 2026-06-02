using Hex1b.Layout;

namespace Hex1b.Tests;

/// <summary>
/// Tests for WindowPositionSpec positioning calculations.
/// </summary>
[TestClass]
public class WindowPositionTests
{
    private readonly Rect _panelBounds = new(0, 0, 100, 50);
    private const int WindowWidth = 40;
    private const int WindowHeight = 20;

    [TestMethod]
    public void Center_PositionsInMiddle()
    {
        var spec = WindowPositionSpec.Center;

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        // Center: (100-40)/2 = 30, (50-20)/2 = 15
        Assert.AreEqual(30, x);
        Assert.AreEqual(15, y);
    }

    [TestMethod]
    public void TopLeft_PositionsAtOrigin()
    {
        var spec = WindowPositionSpec.TopLeft;

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(0, x);
        Assert.AreEqual(0, y);
    }

    [TestMethod]
    public void TopRight_PositionsAtTopRight()
    {
        var spec = WindowPositionSpec.TopRight;

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(60, x); // 100 - 40
        Assert.AreEqual(0, y);
    }

    [TestMethod]
    public void BottomLeft_PositionsAtBottomLeft()
    {
        var spec = WindowPositionSpec.BottomLeft;

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(0, x);
        Assert.AreEqual(30, y); // 50 - 20
    }

    [TestMethod]
    public void BottomRight_PositionsAtBottomRight()
    {
        var spec = WindowPositionSpec.BottomRight;

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(60, x); // 100 - 40
        Assert.AreEqual(30, y); // 50 - 20
    }

    [TestMethod]
    public void CenterTop_CentersHorizontallyAtTop()
    {
        var spec = new WindowPositionSpec(WindowPosition.CenterTop);

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(30, x); // (100-40)/2
        Assert.AreEqual(0, y);
    }

    [TestMethod]
    public void CenterBottom_CentersHorizontallyAtBottom()
    {
        var spec = new WindowPositionSpec(WindowPosition.CenterBottom);

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(30, x); // (100-40)/2
        Assert.AreEqual(30, y); // 50 - 20
    }

    [TestMethod]
    public void CenterLeft_CentersVerticallyAtLeft()
    {
        var spec = new WindowPositionSpec(WindowPosition.CenterLeft);

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(0, x);
        Assert.AreEqual(15, y); // (50-20)/2
    }

    [TestMethod]
    public void CenterRight_CentersVerticallyAtRight()
    {
        var spec = new WindowPositionSpec(WindowPosition.CenterRight);

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(60, x); // 100 - 40
        Assert.AreEqual(15, y); // (50-20)/2
    }

    [TestMethod]
    public void Absolute_UsesExplicitCoordinates()
    {
        var spec = new WindowPositionSpec(WindowPosition.Absolute);

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight, absoluteX: 25, absoluteY: 10);

        Assert.AreEqual(25, x);
        Assert.AreEqual(10, y);
    }

    [TestMethod]
    public void WithOffset_AppliesOffset()
    {
        var spec = new WindowPositionSpec(WindowPosition.Center, OffsetX: 5, OffsetY: -3);

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(35, x); // 30 + 5
        Assert.AreEqual(12, y); // 15 - 3
    }

    [TestMethod]
    public void CenterWithOffset_AppliesOffset()
    {
        var spec = WindowPositionSpec.CenterWithOffset(10, 5);

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(40, x); // 30 + 10
        Assert.AreEqual(20, y); // 15 + 5
    }

    [TestMethod]
    public void ClampsToLeftBound()
    {
        var spec = new WindowPositionSpec(WindowPosition.Center, OffsetX: -100);

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(0, x); // Clamped to 0
    }

    [TestMethod]
    public void ClampsToRightBound()
    {
        var spec = new WindowPositionSpec(WindowPosition.Center, OffsetX: 100);

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(60, x); // Clamped to 100 - 40
    }

    [TestMethod]
    public void ClampsToTopBound()
    {
        var spec = new WindowPositionSpec(WindowPosition.Center, OffsetY: -100);

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(0, y); // Clamped to 0
    }

    [TestMethod]
    public void ClampsToBottomBound()
    {
        var spec = new WindowPositionSpec(WindowPosition.Center, OffsetY: 100);

        var (x, y) = spec.Calculate(_panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(30, y); // Clamped to 50 - 20
    }

    [TestMethod]
    public void WithNonZeroPanelOrigin_PositionsCorrectly()
    {
        var panelBounds = new Rect(10, 5, 100, 50);
        var spec = WindowPositionSpec.Center;

        var (x, y) = spec.Calculate(panelBounds, WindowWidth, WindowHeight);

        // Center relative to panel origin: 10 + (100-40)/2 = 40, 5 + (50-20)/2 = 20
        Assert.AreEqual(40, x);
        Assert.AreEqual(20, y);
    }

    [TestMethod]
    public void TopLeft_WithNonZeroOrigin_PositionsAtPanelOrigin()
    {
        var panelBounds = new Rect(10, 5, 100, 50);
        var spec = WindowPositionSpec.TopLeft;

        var (x, y) = spec.Calculate(panelBounds, WindowWidth, WindowHeight);

        Assert.AreEqual(10, x);
        Assert.AreEqual(5, y);
    }

    [TestMethod]
    public void WindowLargerThanPanel_ClampsToOrigin()
    {
        var smallPanel = new Rect(0, 0, 30, 15);
        var spec = WindowPositionSpec.Center;

        var (x, y) = spec.Calculate(smallPanel, WindowWidth, WindowHeight);

        // Window is larger than panel, so it should clamp to origin
        // Max x = 0 + 30 - 40 = -10, clamped to 0
        // Max y = 0 + 15 - 20 = -5, clamped to 0
        Assert.AreEqual(0, x);
        Assert.AreEqual(0, y);
    }
}
