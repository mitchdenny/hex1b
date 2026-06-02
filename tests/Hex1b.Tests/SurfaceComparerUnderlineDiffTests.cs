using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b.Tests;

/// <summary>
/// Regression tests for <see cref="SurfaceComparer.Compare"/> covering
/// underline-style and underline-color changes. Historically these fields
/// were absent from the cell-equality fast path, so a cell that changed only
/// in its underline state was treated as unchanged - the diff would skip it
/// and the host terminal would keep displaying stale underline state.
/// </summary>
[TestClass]
public class SurfaceComparerUnderlineDiffTests
{
    [TestMethod]
    public void Compare_CellWithChangedUnderlineStyleOnly_IsIncludedInDiff()
    {
        var previous = new Surface(1, 1);
        var current = new Surface(1, 1);

        previous[0, 0] = new SurfaceCell(
            "A",
            Hex1bColor.White,
            Hex1bColor.Black,
            CellAttributes.Underline,
            UnderlineStyle: UnderlineStyle.Single);

        current[0, 0] = previous[0, 0] with { UnderlineStyle = UnderlineStyle.Curly };

        var diff = SurfaceComparer.Compare(previous, current);

        TestSeq.Single(diff.ChangedCells);
        Assert.AreEqual(UnderlineStyle.Curly, diff.ChangedCells[0].Cell.UnderlineStyle);
    }

    [TestMethod]
    public void Compare_CellWithChangedUnderlineColorOnly_IsIncludedInDiff()
    {
        var previous = new Surface(1, 1);
        var current = new Surface(1, 1);

        previous[0, 0] = new SurfaceCell(
            "A",
            Hex1bColor.White,
            Hex1bColor.Black,
            CellAttributes.Underline,
            UnderlineStyle: UnderlineStyle.Single,
            UnderlineColor: Hex1bColor.Red);

        current[0, 0] = previous[0, 0] with { UnderlineColor = Hex1bColor.Blue };

        var diff = SurfaceComparer.Compare(previous, current);

        TestSeq.Single(diff.ChangedCells);
        Assert.AreEqual(Hex1bColor.Blue, diff.ChangedCells[0].Cell.UnderlineColor);
    }

    [TestMethod]
    public void Compare_CellWithIdenticalUnderlineState_IsNotInDiff()
    {
        var previous = new Surface(1, 1);
        var current = new Surface(1, 1);

        previous[0, 0] = new SurfaceCell(
            "A",
            Hex1bColor.White,
            Hex1bColor.Black,
            CellAttributes.Underline,
            UnderlineStyle: UnderlineStyle.Curly,
            UnderlineColor: Hex1bColor.Green);

        current[0, 0] = previous[0, 0];

        var diff = SurfaceComparer.Compare(previous, current);

        Assert.IsEmpty(diff.ChangedCells);
    }
}
