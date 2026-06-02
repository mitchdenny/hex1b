using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

[TestClass]
public class DocumentCursorTests
{
    [TestMethod]
    public void Position_DefaultIsZero()
    {
        var cursor = new DocumentCursor();
        Assert.AreEqual(DocumentOffset.Zero, cursor.Position);
    }

    [TestMethod]
    public void SelectionAnchor_DefaultIsNull()
    {
        var cursor = new DocumentCursor();
        Assert.IsNull(cursor.SelectionAnchor);
    }

    [TestMethod]
    public void HasSelection_NoAnchor_ReturnsFalse()
    {
        var cursor = new DocumentCursor();
        Assert.IsFalse(cursor.HasSelection);
    }

    [TestMethod]
    public void HasSelection_AnchorSameAsPosition_ReturnsFalse()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(5),
            SelectionAnchor = new DocumentOffset(5)
        };
        Assert.IsFalse(cursor.HasSelection);
    }

    [TestMethod]
    public void HasSelection_AnchorDifferentFromPosition_ReturnsTrue()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(5),
            SelectionAnchor = new DocumentOffset(2)
        };
        Assert.IsTrue(cursor.HasSelection);
    }

    // --- SelectionStart / SelectionEnd ---

    [TestMethod]
    public void SelectionStart_AnchorBeforePosition_ReturnsAnchor()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(8),
            SelectionAnchor = new DocumentOffset(3)
        };
        Assert.AreEqual(new DocumentOffset(3), cursor.SelectionStart);
    }

    [TestMethod]
    public void SelectionEnd_AnchorBeforePosition_ReturnsPosition()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(8),
            SelectionAnchor = new DocumentOffset(3)
        };
        Assert.AreEqual(new DocumentOffset(8), cursor.SelectionEnd);
    }

    [TestMethod]
    public void SelectionStart_AnchorAfterPosition_ReturnsPosition()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(3),
            SelectionAnchor = new DocumentOffset(8)
        };
        Assert.AreEqual(new DocumentOffset(3), cursor.SelectionStart);
    }

    [TestMethod]
    public void SelectionEnd_AnchorAfterPosition_ReturnsAnchor()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(3),
            SelectionAnchor = new DocumentOffset(8)
        };
        Assert.AreEqual(new DocumentOffset(8), cursor.SelectionEnd);
    }

    [TestMethod]
    public void SelectionStart_NoSelection_ReturnsPosition()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(5) };
        Assert.AreEqual(new DocumentOffset(5), cursor.SelectionStart);
    }

    [TestMethod]
    public void SelectionEnd_NoSelection_ReturnsPosition()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(5) };
        Assert.AreEqual(new DocumentOffset(5), cursor.SelectionEnd);
    }

    // --- SelectionRange ---

    [TestMethod]
    public void SelectionRange_WithSelection_ReturnsCorrectRange()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(8),
            SelectionAnchor = new DocumentOffset(3)
        };
        var range = cursor.SelectionRange;
        Assert.AreEqual(new DocumentOffset(3), range.Start);
        Assert.AreEqual(new DocumentOffset(8), range.End);
    }

    [TestMethod]
    public void SelectionRange_NoSelection_ReturnsEmptyRangeAtPosition()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(5) };
        var range = cursor.SelectionRange;
        Assert.IsTrue(range.IsEmpty);
        Assert.AreEqual(new DocumentOffset(5), range.Start);
    }

    // --- ClearSelection ---

    [TestMethod]
    public void ClearSelection_RemovesAnchor()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(8),
            SelectionAnchor = new DocumentOffset(3)
        };
        cursor.ClearSelection();
        Assert.IsNull(cursor.SelectionAnchor);
        Assert.IsFalse(cursor.HasSelection);
    }

    [TestMethod]
    public void ClearSelection_WhenNoSelection_IsNoOp()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(5) };
        cursor.ClearSelection();
        Assert.IsNull(cursor.SelectionAnchor);
    }

    // --- Clamp ---

    [TestMethod]
    public void Clamp_PositionWithinRange_NoChange()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(3) };
        cursor.Clamp(10);
        Assert.AreEqual(new DocumentOffset(3), cursor.Position);
    }

    [TestMethod]
    public void Clamp_PositionBeyondRange_ClampsToEnd()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(15) };
        cursor.Clamp(10);
        Assert.AreEqual(new DocumentOffset(10), cursor.Position);
    }

    [TestMethod]
    public void Clamp_AnchorBeyondRange_ClampsToEnd()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(3),
            SelectionAnchor = new DocumentOffset(15)
        };
        cursor.Clamp(10);
        Assert.AreEqual(new DocumentOffset(3), cursor.Position);
        Assert.AreEqual(new DocumentOffset(10), cursor.SelectionAnchor);
    }

    [TestMethod]
    public void Clamp_BothBeyondRange_BothClamped()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(20),
            SelectionAnchor = new DocumentOffset(25)
        };
        cursor.Clamp(10);
        Assert.AreEqual(new DocumentOffset(10), cursor.Position);
        Assert.AreEqual(new DocumentOffset(10), cursor.SelectionAnchor);
    }

    [TestMethod]
    public void Clamp_NoAnchor_DoesNotCreateAnchor()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(15) };
        cursor.Clamp(10);
        Assert.IsNull(cursor.SelectionAnchor);
    }

    [TestMethod]
    public void Clamp_ZeroLength_PositionClampsToZero()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(5) };
        cursor.Clamp(0);
        Assert.AreEqual(DocumentOffset.Zero, cursor.Position);
    }
}
