using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for SGR 8/28 (hidden) and SGR 9/29 (strikethrough) attribute roundtrip behavior.
/// Validates that these attributes are set, cleared, and interact correctly with SGR 0 reset
/// and with combined attributes (bold + hidden + strikethrough).
/// Inspired by psmux's test_issue155_sgr_attrs.rs and test_issue155_rendering.rs.
/// </summary>
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

    [Fact]
    public void Sgr9_SetsStrikethrough_OnSubsequentCells()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[9mabc");

        var snap = t.Terminal.CreateSnapshot();
        for (int i = 0; i < 3; i++)
        {
            var cell = snap.GetCell(i, 0);
            Assert.True(cell.Attributes.HasFlag(CellAttributes.Strikethrough),
                $"Cell {i} should have strikethrough");
        }
    }

    [Fact]
    public void Sgr29_ClearsStrikethrough()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[9mab\x1b[29mcd");

        var snap = t.Terminal.CreateSnapshot();
        Assert.True(snap.GetCell(0, 0).Attributes.HasFlag(CellAttributes.Strikethrough));
        Assert.True(snap.GetCell(1, 0).Attributes.HasFlag(CellAttributes.Strikethrough));
        Assert.False(snap.GetCell(2, 0).Attributes.HasFlag(CellAttributes.Strikethrough));
        Assert.False(snap.GetCell(3, 0).Attributes.HasFlag(CellAttributes.Strikethrough));
    }

    [Fact]
    public void Sgr8_SetsHidden_OnSubsequentCells()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[8mhidden");

        var snap = t.Terminal.CreateSnapshot();
        for (int i = 0; i < 6; i++)
        {
            var cell = snap.GetCell(i, 0);
            Assert.True(cell.Attributes.HasFlag(CellAttributes.Hidden),
                $"Cell {i} should have hidden attribute");
        }
    }

    [Fact]
    public void Sgr28_ClearsHidden()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[8mab\x1b[28mcd");

        var snap = t.Terminal.CreateSnapshot();
        Assert.True(snap.GetCell(0, 0).Attributes.HasFlag(CellAttributes.Hidden));
        Assert.True(snap.GetCell(1, 0).Attributes.HasFlag(CellAttributes.Hidden));
        Assert.False(snap.GetCell(2, 0).Attributes.HasFlag(CellAttributes.Hidden));
        Assert.False(snap.GetCell(3, 0).Attributes.HasFlag(CellAttributes.Hidden));
    }

    [Fact]
    public void Sgr0_ResetsBothHiddenAndStrikethrough()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[8;9mab\x1b[0mcd");

        var snap = t.Terminal.CreateSnapshot();

        // First 2 cells: both hidden and strikethrough
        var cellA = snap.GetCell(0, 0);
        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Hidden));
        Assert.True(cellA.Attributes.HasFlag(CellAttributes.Strikethrough));

        // After SGR 0 reset: neither
        var cellC = snap.GetCell(2, 0);
        Assert.False(cellC.Attributes.HasFlag(CellAttributes.Hidden));
        Assert.False(cellC.Attributes.HasFlag(CellAttributes.Strikethrough));
    }

    [Fact]
    public void CombinedBoldHiddenStrikethrough_AllActive()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[1;8;9mX");

        var snap = t.Terminal.CreateSnapshot();
        var cell = snap.GetCell(0, 0);

        Assert.True(cell.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Hidden));
        Assert.True(cell.Attributes.HasFlag(CellAttributes.Strikethrough));
    }

    [Fact]
    public void HiddenCell_PreservesCharacterContent()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[8mABC");

        var snap = t.Terminal.CreateSnapshot();

        // The cell should still store the character even though it's hidden
        Assert.Equal("A", snap.GetCell(0, 0).Character);
        Assert.Equal("B", snap.GetCell(1, 0).Character);
        Assert.Equal("C", snap.GetCell(2, 0).Character);
    }

    [Fact]
    public void StrikethroughCell_PreservesCharacterContent()
    {
        using var t = new TestTerminal();
        t.Write("\x1b[9mXYZ");

        var snap = t.Terminal.CreateSnapshot();

        // Strikethrough cells show actual content (unlike hidden which renders as spaces)
        Assert.Equal("X", snap.GetCell(0, 0).Character);
        Assert.Equal("Y", snap.GetCell(1, 0).Character);
        Assert.Equal("Z", snap.GetCell(2, 0).Character);
    }
}
