using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b.Tests;

/// <summary>
/// Covers the <see cref="Surface.IsFastPathEligible"/> latch and the matching
/// fast diff path in <see cref="SurfaceComparer"/>. The fast path skips the
/// underline / display-width / tracked-ref branches in per-cell equality, so
/// the latch MUST flip to false whenever any such complexity is introduced.
/// Conversely, when both surfaces remain in the simple lane, the diff output
/// must be identical to the slow path.
/// </summary>
[TestClass]
public class SurfaceFastPathTests
{
    [TestMethod]
    public void NewSurface_HasFastPathEligibleTrue()
    {
        var surface = new Surface(4, 2);
        Assert.IsTrue(surface.IsFastPathEligible);
    }

    [TestMethod]
    public void SimpleAsciiWrite_KeepsFastPathEligible()
    {
        var surface = new Surface(4, 2);
        surface[0, 0] = new SurfaceCell("A", Hex1bColor.White, Hex1bColor.Black);
        surface.WriteText(1, 0, "BCD", Hex1bColor.White, Hex1bColor.Black);
        Assert.IsTrue(surface.IsFastPathEligible);
    }

    [TestMethod]
    [DataRow("e\u0301")]   // combining acute => grapheme length > 1
    public void MultiCodePointGrapheme_MarksComplex(string grapheme)
    {
        var surface = new Surface(4, 1);
        surface[0, 0] = new SurfaceCell(grapheme, null, null);
        Assert.IsFalse(surface.IsFastPathEligible);
    }

    [TestMethod]
    public void UnderlineStyle_MarksComplex()
    {
        var surface = new Surface(4, 1);
        surface[0, 0] = new SurfaceCell(
            "A", Hex1bColor.White, Hex1bColor.Black,
            UnderlineStyle: UnderlineStyle.Single);
        Assert.IsFalse(surface.IsFastPathEligible);
    }

    [TestMethod]
    public void UnderlineColor_MarksComplex()
    {
        var surface = new Surface(4, 1);
        surface[0, 0] = new SurfaceCell(
            "A", Hex1bColor.White, Hex1bColor.Black,
            UnderlineColor: Hex1bColor.Red);
        Assert.IsFalse(surface.IsFastPathEligible);
    }

    [TestMethod]
    public void WideCharacter_MarksComplex()
    {
        var surface = new Surface(4, 1);
        // "あ" is a wide CJK char with DisplayWidth=2.
        surface.WriteText(0, 0, "あ", Hex1bColor.White, Hex1bColor.Black);
        Assert.IsFalse(surface.IsFastPathEligible);
    }

    [TestMethod]
    public void Clear_ResetsFastPathFlag()
    {
        var surface = new Surface(4, 1);
        surface[0, 0] = new SurfaceCell(
            "A", Hex1bColor.White, Hex1bColor.Black,
            UnderlineStyle: UnderlineStyle.Single);
        Assert.IsFalse(surface.IsFastPathEligible);

        surface.Clear();

        Assert.IsTrue(surface.IsFastPathEligible);
    }

    [TestMethod]
    public void ClearWithComplexCell_LeavesFlagSet()
    {
        var surface = new Surface(4, 1);
        var complex = new SurfaceCell(
            "A", Hex1bColor.White, Hex1bColor.Black,
            UnderlineStyle: UnderlineStyle.Single);

        surface.Clear(complex);

        Assert.IsFalse(surface.IsFastPathEligible);
    }

    [TestMethod]
    public void ClearWithSimpleCell_KeepsFlagClear()
    {
        var surface = new Surface(4, 1);
        surface[0, 0] = new SurfaceCell(
            "A", Hex1bColor.White, Hex1bColor.Black,
            UnderlineStyle: UnderlineStyle.Single);
        Assert.IsFalse(surface.IsFastPathEligible);

        surface.Clear(new SurfaceCell(" ", null, Hex1bColor.Black));

        Assert.IsTrue(surface.IsFastPathEligible);
    }

    [TestMethod]
    public void Compare_FastPath_ProducesIdenticalDiffToSlowPath()
    {
        // Two surfaces filled with simple ASCII content; both are fast-eligible.
        // The fast path output must exactly match what the slow path would produce.
        var prev = new Surface(8, 3);
        var curr = new Surface(8, 3);

        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                prev[x, y] = new SurfaceCell("a", Hex1bColor.White, Hex1bColor.Black);
                // Change every other cell to "b" with a different bg.
                curr[x, y] = ((x + y) % 2 == 0)
                    ? new SurfaceCell("a", Hex1bColor.White, Hex1bColor.Black)
                    : new SurfaceCell("b", Hex1bColor.White, Hex1bColor.Red);
            }
        }

        Assert.IsTrue(prev.IsFastPathEligible);
        Assert.IsTrue(curr.IsFastPathEligible);

        var diff = SurfaceComparer.Compare(prev, curr);

        // Manually compute the expected changed-cell set.
        var expected = new List<(int X, int Y, string Char)>();
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 8; x++)
                if ((x + y) % 2 != 0)
                    expected.Add((x, y, "b"));

        Assert.AreEqual(expected.Count, diff.ChangedCells.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i].X, diff.ChangedCells[i].X);
            Assert.AreEqual(expected[i].Y, diff.ChangedCells[i].Y);
            Assert.AreEqual(expected[i].Char, diff.ChangedCells[i].Cell.Character);
        }
    }

    [TestMethod]
    public void Compare_MixedEligibility_FallsBackToSlowPathAndStillDetectsUnderlineChange()
    {
        // prev has only a plain cell; curr has an underline-changed cell.
        // Because curr is NOT fast-eligible, the slow path runs and the underline
        // change must be detected (this is the same scenario as the existing
        // underline regression test, but through the new gating).
        var prev = new Surface(1, 1);
        var curr = new Surface(1, 1);

        prev[0, 0] = new SurfaceCell("A", Hex1bColor.White, Hex1bColor.Black);
        curr[0, 0] = new SurfaceCell(
            "A", Hex1bColor.White, Hex1bColor.Black,
            UnderlineStyle: UnderlineStyle.Single);

        Assert.IsTrue(prev.IsFastPathEligible);
        Assert.IsFalse(curr.IsFastPathEligible);

        var diff = SurfaceComparer.Compare(prev, curr);

        TestSeq.Single(diff.ChangedCells);
        Assert.AreEqual(UnderlineStyle.Single, diff.ChangedCells[0].Cell.UnderlineStyle);
    }

    [TestMethod]
    public void CompareToEmpty_FastPath_MatchesSlowPath()
    {
        var surface = new Surface(4, 2);
        surface[0, 0] = new SurfaceCell("A", Hex1bColor.White, Hex1bColor.Black);
        surface[3, 1] = new SurfaceCell("Z", Hex1bColor.White, Hex1bColor.Red);

        Assert.IsTrue(surface.IsFastPathEligible);

        var diff = SurfaceComparer.CompareToEmpty(surface);

        Assert.AreEqual(2, diff.ChangedCells.Count);
        Assert.AreEqual(0, diff.ChangedCells[0].X);
        Assert.AreEqual(0, diff.ChangedCells[0].Y);
        Assert.AreEqual("A", diff.ChangedCells[0].Cell.Character);
        Assert.AreEqual(3, diff.ChangedCells[1].X);
        Assert.AreEqual(1, diff.ChangedCells[1].Y);
        Assert.AreEqual("Z", diff.ChangedCells[1].Cell.Character);
    }
}
