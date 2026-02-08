using Hex1b.Documents;

namespace Hex1b.Tests.Documents;

public class DocumentCursorTests
{
    [Fact]
    public void Position_DefaultIsZero()
    {
        var cursor = new DocumentCursor();
        Assert.Equal(DocumentOffset.Zero, cursor.Position);
    }

    [Fact]
    public void SelectionAnchor_DefaultIsNull()
    {
        var cursor = new DocumentCursor();
        Assert.Null(cursor.SelectionAnchor);
    }

    [Fact]
    public void HasSelection_NoAnchor_ReturnsFalse()
    {
        var cursor = new DocumentCursor();
        Assert.False(cursor.HasSelection);
    }

    [Fact]
    public void HasSelection_AnchorSameAsPosition_ReturnsFalse()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(5),
            SelectionAnchor = new DocumentOffset(5)
        };
        Assert.False(cursor.HasSelection);
    }

    [Fact]
    public void HasSelection_AnchorDifferentFromPosition_ReturnsTrue()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(5),
            SelectionAnchor = new DocumentOffset(2)
        };
        Assert.True(cursor.HasSelection);
    }

    // --- SelectionStart / SelectionEnd ---

    [Fact]
    public void SelectionStart_AnchorBeforePosition_ReturnsAnchor()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(8),
            SelectionAnchor = new DocumentOffset(3)
        };
        Assert.Equal(new DocumentOffset(3), cursor.SelectionStart);
    }

    [Fact]
    public void SelectionEnd_AnchorBeforePosition_ReturnsPosition()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(8),
            SelectionAnchor = new DocumentOffset(3)
        };
        Assert.Equal(new DocumentOffset(8), cursor.SelectionEnd);
    }

    [Fact]
    public void SelectionStart_AnchorAfterPosition_ReturnsPosition()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(3),
            SelectionAnchor = new DocumentOffset(8)
        };
        Assert.Equal(new DocumentOffset(3), cursor.SelectionStart);
    }

    [Fact]
    public void SelectionEnd_AnchorAfterPosition_ReturnsAnchor()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(3),
            SelectionAnchor = new DocumentOffset(8)
        };
        Assert.Equal(new DocumentOffset(8), cursor.SelectionEnd);
    }

    [Fact]
    public void SelectionStart_NoSelection_ReturnsPosition()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(5) };
        Assert.Equal(new DocumentOffset(5), cursor.SelectionStart);
    }

    [Fact]
    public void SelectionEnd_NoSelection_ReturnsPosition()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(5) };
        Assert.Equal(new DocumentOffset(5), cursor.SelectionEnd);
    }

    // --- SelectionRange ---

    [Fact]
    public void SelectionRange_WithSelection_ReturnsCorrectRange()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(8),
            SelectionAnchor = new DocumentOffset(3)
        };
        var range = cursor.SelectionRange;
        Assert.Equal(new DocumentOffset(3), range.Start);
        Assert.Equal(new DocumentOffset(8), range.End);
    }

    [Fact]
    public void SelectionRange_NoSelection_ReturnsEmptyRangeAtPosition()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(5) };
        var range = cursor.SelectionRange;
        Assert.True(range.IsEmpty);
        Assert.Equal(new DocumentOffset(5), range.Start);
    }

    // --- ClearSelection ---

    [Fact]
    public void ClearSelection_RemovesAnchor()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(8),
            SelectionAnchor = new DocumentOffset(3)
        };
        cursor.ClearSelection();
        Assert.Null(cursor.SelectionAnchor);
        Assert.False(cursor.HasSelection);
    }

    [Fact]
    public void ClearSelection_WhenNoSelection_IsNoOp()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(5) };
        cursor.ClearSelection();
        Assert.Null(cursor.SelectionAnchor);
    }

    // --- Clamp ---

    [Fact]
    public void Clamp_PositionWithinRange_NoChange()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(3) };
        cursor.Clamp(10);
        Assert.Equal(new DocumentOffset(3), cursor.Position);
    }

    [Fact]
    public void Clamp_PositionBeyondRange_ClampsToEnd()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(15) };
        cursor.Clamp(10);
        Assert.Equal(new DocumentOffset(10), cursor.Position);
    }

    [Fact]
    public void Clamp_AnchorBeyondRange_ClampsToEnd()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(3),
            SelectionAnchor = new DocumentOffset(15)
        };
        cursor.Clamp(10);
        Assert.Equal(new DocumentOffset(3), cursor.Position);
        Assert.Equal(new DocumentOffset(10), cursor.SelectionAnchor);
    }

    [Fact]
    public void Clamp_BothBeyondRange_BothClamped()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(20),
            SelectionAnchor = new DocumentOffset(25)
        };
        cursor.Clamp(10);
        Assert.Equal(new DocumentOffset(10), cursor.Position);
        Assert.Equal(new DocumentOffset(10), cursor.SelectionAnchor);
    }

    [Fact]
    public void Clamp_NoAnchor_DoesNotCreateAnchor()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(15) };
        cursor.Clamp(10);
        Assert.Null(cursor.SelectionAnchor);
    }

    [Fact]
    public void Clamp_ZeroLength_PositionClampsToZero()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(5) };
        cursor.Clamp(0);
        Assert.Equal(DocumentOffset.Zero, cursor.Position);
    }
}
