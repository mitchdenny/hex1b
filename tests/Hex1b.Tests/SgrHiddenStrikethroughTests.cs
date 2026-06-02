using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for SGR 8/28 (hidden) and SGR 9/29 (strikethrough) attribute roundtrip behavior.
/// Validates that these attributes are set, cleared, and interact correctly with SGR 0 reset
/// and with combined attributes (bold + hidden + strikethrough).
/// Inspired by psmux's test_issue155_sgr_attrs.rs and test_issue155_rendering.rs.
/// </summary>
[TestClass]
public class SgrHiddenStrikethroughTests
{
    private sealed class TestTerminal : IDisposable
    {
        private readonly StreamWorkloadAdapter _workload;
        public Hex1bTerminal Terminal { get; }

        public TestTerminal(int width = 80, int height = 24)
        {
            _workload = StreamWorkloadAdapter.CreateHeadless(width, height);
            Terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(_workload).WithHeadless().WithDimensions(width, height).Build();
        }

        public void Write(string text)
        {
            Terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));
        }

        public void Dispose()
        {
            Terminal.Dispose();
            _workload.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    [TestMethod]
    public void Sgr9_SetsStrikethrough_OnSubsequentCells()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[9mabc");

        var snap = t.Terminal.CreateSnapshot();
        for (int i = 0; i < 3; i++)
        {
            var cell = snap.GetCell(i, 0);
            Assert.IsTrue(cell.Attributes.HasFlag(CellAttributes.Strikethrough), $"Cell {i} should have strikethrough");
        }
    }

    [TestMethod]
    public void Sgr29_ClearsStrikethrough()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[9mab\x1b[29mcd");

        var snap = t.Terminal.CreateSnapshot();
        Assert.IsTrue(snap.GetCell(0, 0).Attributes.HasFlag(CellAttributes.Strikethrough));
        Assert.IsTrue(snap.GetCell(1, 0).Attributes.HasFlag(CellAttributes.Strikethrough));
        Assert.IsFalse(snap.GetCell(2, 0).Attributes.HasFlag(CellAttributes.Strikethrough));
        Assert.IsFalse(snap.GetCell(3, 0).Attributes.HasFlag(CellAttributes.Strikethrough));
    }

    [TestMethod]
    public void Sgr8_SetsHidden_OnSubsequentCells()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[8mhidden");

        var snap = t.Terminal.CreateSnapshot();
        for (int i = 0; i < 6; i++)
        {
            var cell = snap.GetCell(i, 0);
            Assert.IsTrue(cell.Attributes.HasFlag(CellAttributes.Hidden), $"Cell {i} should have hidden attribute");
        }
    }

    [TestMethod]
    public void Sgr28_ClearsHidden()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[8mab\x1b[28mcd");

        var snap = t.Terminal.CreateSnapshot();
        Assert.IsTrue(snap.GetCell(0, 0).Attributes.HasFlag(CellAttributes.Hidden));
        Assert.IsTrue(snap.GetCell(1, 0).Attributes.HasFlag(CellAttributes.Hidden));
        Assert.IsFalse(snap.GetCell(2, 0).Attributes.HasFlag(CellAttributes.Hidden));
        Assert.IsFalse(snap.GetCell(3, 0).Attributes.HasFlag(CellAttributes.Hidden));
    }

    [TestMethod]
    public void Sgr0_ResetsBothHiddenAndStrikethrough()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[8;9mab\x1b[0mcd");

        var snap = t.Terminal.CreateSnapshot();

        // First 2 cells: both hidden and strikethrough
        var cellA = snap.GetCell(0, 0);
        Assert.IsTrue(cellA.Attributes.HasFlag(CellAttributes.Hidden));
        Assert.IsTrue(cellA.Attributes.HasFlag(CellAttributes.Strikethrough));

        // After SGR 0 reset: neither
        var cellC = snap.GetCell(2, 0);
        Assert.IsFalse(cellC.Attributes.HasFlag(CellAttributes.Hidden));
        Assert.IsFalse(cellC.Attributes.HasFlag(CellAttributes.Strikethrough));
    }

    [TestMethod]
    public void CombinedBoldHiddenStrikethrough_AllActive()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[1;8;9mX");

        var snap = t.Terminal.CreateSnapshot();
        var cell = snap.GetCell(0, 0);

        Assert.IsTrue(cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.IsTrue(cell.Attributes.HasFlag(CellAttributes.Hidden));
        Assert.IsTrue(cell.Attributes.HasFlag(CellAttributes.Strikethrough));
    }

    [TestMethod]
    public void HiddenCell_PreservesCharacterContent()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[8mABC");

        var snap = t.Terminal.CreateSnapshot();

        // The cell should still store the character even though it's hidden
        Assert.AreEqual("A", snap.GetCell(0, 0).Character);
        Assert.AreEqual("B", snap.GetCell(1, 0).Character);
        Assert.AreEqual("C", snap.GetCell(2, 0).Character);
    }

    [TestMethod]
    public void StrikethroughCell_PreservesCharacterContent()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[9mXYZ");

        var snap = t.Terminal.CreateSnapshot();

        // Strikethrough cells show actual content (unlike hidden which renders as spaces)
        Assert.AreEqual("X", snap.GetCell(0, 0).Character);
        Assert.AreEqual("Y", snap.GetCell(1, 0).Character);
        Assert.AreEqual("Z", snap.GetCell(2, 0).Character);
    }
}
