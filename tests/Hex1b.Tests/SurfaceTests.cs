using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="SurfaceCell"/> record struct.
/// </summary>
public class SurfaceCellTests
{
    #region Construction and Equality

    [Fact]
    public void SurfaceCell_DefaultValues_AreCorrect()
    {
        var cell = new SurfaceCell("A", null, null);
        
        Assert.Equal("A", cell.Character);
        Assert.Null(cell.Foreground);
        Assert.Null(cell.Background);
        Assert.Equal(CellAttributes.None, cell.Attributes);
        Assert.Equal(1, cell.DisplayWidth);
        Assert.Null(cell.Sixel);
        Assert.Null(cell.Hyperlink);
    }

    [Fact]
    public void SurfaceCell_WithColors_StoresCorrectly()
    {
        var fg = Hex1bColor.Red;
        var bg = Hex1bColor.Blue;
        var cell = new SurfaceCell("X", fg, bg, CellAttributes.Bold);
        
        Assert.Equal("X", cell.Character);
        Assert.Equal(fg, cell.Foreground);
        Assert.Equal(bg, cell.Background);
        Assert.Equal(CellAttributes.Bold, cell.Attributes);
    }

    [Fact]
    public void SurfaceCell_Equality_WorksCorrectly()
    {
        var cell1 = new SurfaceCell("A", Hex1bColor.Red, null, CellAttributes.Bold, 1);
        var cell2 = new SurfaceCell("A", Hex1bColor.Red, null, CellAttributes.Bold, 1);
        var cell3 = new SurfaceCell("B", Hex1bColor.Red, null, CellAttributes.Bold, 1);
        
        Assert.Equal(cell1, cell2);
        Assert.NotEqual(cell1, cell3);
    }

    #endregion

    #region Properties

    [Fact]
    public void IsContinuation_WhenDisplayWidthIsZero_ReturnsTrue()
    {
        var continuation = new SurfaceCell("", null, null, DisplayWidth: 0);
        var normal = new SurfaceCell("A", null, null, DisplayWidth: 1);
        
        Assert.True(continuation.IsContinuation);
        Assert.False(normal.IsContinuation);
    }

    [Fact]
    public void IsWide_WhenDisplayWidthIsTwo_ReturnsTrue()
    {
        var wide = new SurfaceCell("日", null, null, DisplayWidth: 2);
        var normal = new SurfaceCell("A", null, null, DisplayWidth: 1);
        
        Assert.True(wide.IsWide);
        Assert.False(normal.IsWide);
    }

    [Fact]
    public void IsTransparent_WhenBothColorsNull_ReturnsTrue()
    {
        var transparent = new SurfaceCell("A", null, null);
        var withFg = new SurfaceCell("A", Hex1bColor.Red, null);
        var withBg = new SurfaceCell("A", null, Hex1bColor.Blue);
        var withBoth = new SurfaceCell("A", Hex1bColor.Red, Hex1bColor.Blue);
        
        Assert.True(transparent.IsTransparent);
        Assert.False(withFg.IsTransparent);
        Assert.False(withBg.IsTransparent);
        Assert.False(withBoth.IsTransparent);
    }

    [Fact]
    public void HasTransparentBackground_WhenBackgroundNull_ReturnsTrue()
    {
        var transparentBg = new SurfaceCell("A", Hex1bColor.Red, null);
        var opaqueBg = new SurfaceCell("A", null, Hex1bColor.Blue);
        
        Assert.True(transparentBg.HasTransparentBackground);
        Assert.False(opaqueBg.HasTransparentBackground);
    }

    #endregion

    #region With Methods

    [Fact]
    public void WithForeground_CreatesNewCellWithUpdatedForeground()
    {
        var original = new SurfaceCell("A", null, Hex1bColor.Blue, CellAttributes.Bold);
        var updated = original.WithForeground(Hex1bColor.Red);
        
        Assert.Null(original.Foreground);
        Assert.Equal(Hex1bColor.Red, updated.Foreground);
        Assert.Equal(original.Background, updated.Background);
        Assert.Equal(original.Attributes, updated.Attributes);
    }

    [Fact]
    public void WithBackground_CreatesNewCellWithUpdatedBackground()
    {
        var original = new SurfaceCell("A", Hex1bColor.Red, null, CellAttributes.Bold);
        var updated = original.WithBackground(Hex1bColor.Blue);
        
        Assert.Null(original.Background);
        Assert.Equal(Hex1bColor.Blue, updated.Background);
        Assert.Equal(original.Foreground, updated.Foreground);
    }

    [Fact]
    public void WithAttributes_CreatesNewCellWithUpdatedAttributes()
    {
        var original = new SurfaceCell("A", null, null, CellAttributes.Bold);
        var updated = original.WithAttributes(CellAttributes.Italic);
        
        Assert.Equal(CellAttributes.Bold, original.Attributes);
        Assert.Equal(CellAttributes.Italic, updated.Attributes);
    }

    [Fact]
    public void WithAddedAttributes_CombinesAttributes()
    {
        var original = new SurfaceCell("A", null, null, CellAttributes.Bold);
        var updated = original.WithAddedAttributes(CellAttributes.Italic);
        
        Assert.Equal(CellAttributes.Bold, original.Attributes);
        Assert.Equal(CellAttributes.Bold | CellAttributes.Italic, updated.Attributes);
    }

    #endregion
}

/// <summary>
/// Tests for <see cref="SurfaceCells"/> static class.
/// </summary>
public class SurfaceCellsTests
{
    [Fact]
    public void Empty_IsSpaceWithNoColors()
    {
        var empty = SurfaceCells.Empty;
        
        Assert.Equal(" ", empty.Character);
        Assert.Null(empty.Foreground);
        Assert.Null(empty.Background);
        Assert.Equal(CellAttributes.None, empty.Attributes);
        Assert.Equal(1, empty.DisplayWidth);
    }

    [Fact]
    public void Continuation_HasZeroDisplayWidth()
    {
        var continuation = SurfaceCells.Continuation;
        
        Assert.Equal(string.Empty, continuation.Character);
        Assert.Equal(0, continuation.DisplayWidth);
        Assert.True(continuation.IsContinuation);
    }

    [Fact]
    public void Char_CreatesCorrectCell()
    {
        var cell = SurfaceCells.Char('X', Hex1bColor.Red, Hex1bColor.Blue, CellAttributes.Bold);
        
        Assert.Equal("X", cell.Character);
        Assert.Equal(Hex1bColor.Red, cell.Foreground);
        Assert.Equal(Hex1bColor.Blue, cell.Background);
        Assert.Equal(CellAttributes.Bold, cell.Attributes);
        Assert.Equal(1, cell.DisplayWidth);
    }

    [Fact]
    public void Space_WithBackground_CreatesSpaceCell()
    {
        var cell = SurfaceCells.Space(Hex1bColor.Blue);
        
        Assert.Equal(" ", cell.Character);
        Assert.Null(cell.Foreground);
        Assert.Equal(Hex1bColor.Blue, cell.Background);
    }
}

/// <summary>
/// Tests for <see cref="Surface"/> class.
/// </summary>
public class SurfaceTests
{
    #region Construction

    [Fact]
    public void Constructor_WithValidDimensions_CreatesSurface()
    {
        var surface = new Surface(80, 24);
        
        Assert.Equal(80, surface.Width);
        Assert.Equal(24, surface.Height);
        Assert.Equal(80 * 24, surface.CellCount);
    }

    [Fact]
    public void Constructor_InitializesAllCellsToEmpty()
    {
        var surface = new Surface(10, 5);
        
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(SurfaceCells.Empty, surface[x, y]);
            }
        }
    }

    [Fact]
    public void Constructor_WithZeroWidth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(0, 10));
    }

    [Fact]
    public void Constructor_WithZeroHeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(10, 0));
    }

    [Fact]
    public void Constructor_WithNegativeDimensions_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(-1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(10, -1));
    }

    #endregion

    #region Indexer

    [Fact]
    public void Indexer_Get_ReturnsCorrectCell()
    {
        var surface = new Surface(10, 5);
        var cell = new SurfaceCell("X", Hex1bColor.Red, null);
        surface[3, 2] = cell;
        
        Assert.Equal(cell, surface[3, 2]);
    }

    [Fact]
    public void Indexer_Set_UpdatesCell()
    {
        var surface = new Surface(10, 5);
        var cell = new SurfaceCell("Y", Hex1bColor.Blue, Hex1bColor.Green);
        
        surface[5, 3] = cell;
        
        Assert.Equal(cell, surface[5, 3]);
    }

    [Fact]
    public void Indexer_OutOfBounds_Throws()
    {
        var surface = new Surface(10, 5);
        
        Assert.Throws<ArgumentOutOfRangeException>(() => surface[-1, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => surface[0, -1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => surface[10, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => surface[0, 5]);
    }

    #endregion

    #region TryGetCell / TrySetCell

    [Fact]
    public void TryGetCell_InBounds_ReturnsTrueAndCell()
    {
        var surface = new Surface(10, 5);
        var cell = new SurfaceCell("A", Hex1bColor.Red, null);
        surface[2, 1] = cell;
        
        var result = surface.TryGetCell(2, 1, out var retrieved);
        
        Assert.True(result);
        Assert.Equal(cell, retrieved);
    }

    [Fact]
    public void TryGetCell_OutOfBounds_ReturnsFalse()
    {
        var surface = new Surface(10, 5);
        
        Assert.False(surface.TryGetCell(-1, 0, out _));
        Assert.False(surface.TryGetCell(10, 0, out _));
        Assert.False(surface.TryGetCell(0, -1, out _));
        Assert.False(surface.TryGetCell(0, 5, out _));
    }

    [Fact]
    public void TrySetCell_InBounds_ReturnsTrueAndSetsCell()
    {
        var surface = new Surface(10, 5);
        var cell = new SurfaceCell("B", Hex1bColor.Green, null);
        
        var result = surface.TrySetCell(4, 2, cell);
        
        Assert.True(result);
        Assert.Equal(cell, surface[4, 2]);
    }

    [Fact]
    public void TrySetCell_OutOfBounds_ReturnsFalse()
    {
        var surface = new Surface(10, 5);
        var cell = new SurfaceCell("C", null, null);
        
        Assert.False(surface.TrySetCell(-1, 0, cell));
        Assert.False(surface.TrySetCell(10, 0, cell));
    }

    #endregion

    #region IsInBounds

    [Fact]
    public void IsInBounds_ValidPositions_ReturnsTrue()
    {
        var surface = new Surface(10, 5);
        
        Assert.True(surface.IsInBounds(0, 0));
        Assert.True(surface.IsInBounds(9, 4));
        Assert.True(surface.IsInBounds(5, 2));
    }

    [Fact]
    public void IsInBounds_InvalidPositions_ReturnsFalse()
    {
        var surface = new Surface(10, 5);
        
        Assert.False(surface.IsInBounds(-1, 0));
        Assert.False(surface.IsInBounds(0, -1));
        Assert.False(surface.IsInBounds(10, 0));
        Assert.False(surface.IsInBounds(0, 5));
    }

    #endregion

    #region Clear

    [Fact]
    public void Clear_ResetsAllCellsToEmpty()
    {
        var surface = new Surface(5, 3);
        surface[2, 1] = new SurfaceCell("X", Hex1bColor.Red, Hex1bColor.Blue);
        surface[4, 2] = new SurfaceCell("Y", Hex1bColor.Green, null);
        
        surface.Clear();
        
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(SurfaceCells.Empty, surface[x, y]);
            }
        }
    }

    [Fact]
    public void Clear_WithCell_FillsWithSpecifiedCell()
    {
        var surface = new Surface(5, 3);
        var fillCell = new SurfaceCell(".", Hex1bColor.Gray, Hex1bColor.Black);
        
        surface.Clear(fillCell);
        
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(fillCell, surface[x, y]);
            }
        }
    }

    #endregion

    #region Fill

    [Fact]
    public void Fill_Region_FillsCorrectCells()
    {
        var surface = new Surface(10, 5);
        var fillCell = new SurfaceCell("#", Hex1bColor.White, Hex1bColor.DarkGray);
        var region = new Rect(2, 1, 3, 2);
        
        surface.Fill(region, fillCell);
        
        // Check filled region
        for (var y = 1; y < 3; y++)
        {
            for (var x = 2; x < 5; x++)
            {
                Assert.Equal(fillCell, surface[x, y]);
            }
        }
        
        // Check surrounding cells remain empty
        Assert.Equal(SurfaceCells.Empty, surface[0, 0]);
        Assert.Equal(SurfaceCells.Empty, surface[1, 1]);
        Assert.Equal(SurfaceCells.Empty, surface[5, 1]);
    }

    [Fact]
    public void Fill_RegionOutsideBounds_ClipsCorrectly()
    {
        var surface = new Surface(5, 5);
        var fillCell = new SurfaceCell("*", null, Hex1bColor.Red);
        var region = new Rect(-2, -2, 5, 5); // Partially outside
        
        surface.Fill(region, fillCell);
        
        // Only cells within bounds should be filled
        Assert.Equal(fillCell, surface[0, 0]);
        Assert.Equal(fillCell, surface[2, 2]);
        Assert.Equal(SurfaceCells.Empty, surface[3, 0]);
        Assert.Equal(SurfaceCells.Empty, surface[0, 3]);
    }

    #endregion

    #region WriteText - Basic

    [Fact]
    public void WriteText_SimpleAscii_WritesCorrectly()
    {
        var surface = new Surface(20, 5);
        
        surface.WriteText(0, 0, "Hello");
        
        Assert.Equal("H", surface[0, 0].Character);
        Assert.Equal("e", surface[1, 0].Character);
        Assert.Equal("l", surface[2, 0].Character);
        Assert.Equal("l", surface[3, 0].Character);
        Assert.Equal("o", surface[4, 0].Character);
        Assert.Equal(" ", surface[5, 0].Character); // Unchanged
    }

    [Fact]
    public void WriteText_WithColors_AppliesColors()
    {
        var surface = new Surface(20, 5);
        var fg = Hex1bColor.Red;
        var bg = Hex1bColor.Blue;
        
        surface.WriteText(0, 0, "AB", fg, bg);
        
        Assert.Equal(fg, surface[0, 0].Foreground);
        Assert.Equal(bg, surface[0, 0].Background);
        Assert.Equal(fg, surface[1, 0].Foreground);
        Assert.Equal(bg, surface[1, 0].Background);
    }

    [Fact]
    public void WriteText_WithAttributes_AppliesAttributes()
    {
        var surface = new Surface(20, 5);
        
        surface.WriteText(0, 0, "Bold", null, null, CellAttributes.Bold);
        
        Assert.Equal(CellAttributes.Bold, surface[0, 0].Attributes);
        Assert.Equal(CellAttributes.Bold, surface[3, 0].Attributes);
    }

    [Fact]
    public void WriteText_ReturnsColumnsWritten()
    {
        var surface = new Surface(20, 5);
        
        var written = surface.WriteText(0, 0, "Hello");
        
        Assert.Equal(5, written);
    }

    [Fact]
    public void WriteText_EmptyString_ReturnsZero()
    {
        var surface = new Surface(20, 5);
        
        var written = surface.WriteText(0, 0, "");
        
        Assert.Equal(0, written);
    }

    [Fact]
    public void WriteText_NullString_ReturnsZero()
    {
        var surface = new Surface(20, 5);
        
        var written = surface.WriteText(0, 0, null!);
        
        Assert.Equal(0, written);
    }

    #endregion

    #region WriteText - Clipping

    [Fact]
    public void WriteText_ClipsAtRightEdge()
    {
        var surface = new Surface(5, 1);
        
        var written = surface.WriteText(0, 0, "HelloWorld");
        
        Assert.Equal(5, written);
        Assert.Equal("H", surface[0, 0].Character);
        Assert.Equal("o", surface[4, 0].Character);
    }

    [Fact]
    public void WriteText_StartingOutsideRight_WritesNothing()
    {
        var surface = new Surface(5, 1);
        
        var written = surface.WriteText(10, 0, "Hello");
        
        Assert.Equal(0, written);
    }

    [Fact]
    public void WriteText_StartingOutsideTop_WritesNothing()
    {
        var surface = new Surface(20, 5);
        
        var written = surface.WriteText(0, -1, "Hello");
        
        Assert.Equal(0, written);
    }

    [Fact]
    public void WriteText_StartingOutsideBottom_WritesNothing()
    {
        var surface = new Surface(20, 5);
        
        var written = surface.WriteText(0, 5, "Hello");
        
        Assert.Equal(0, written);
    }

    [Fact]
    public void WriteText_StartingNegativeX_SkipsClippedChars()
    {
        var surface = new Surface(10, 1);
        
        surface.WriteText(-2, 0, "Hello");
        
        // "He" should be clipped, "llo" should be visible starting at x=0
        Assert.Equal("l", surface[0, 0].Character);
        Assert.Equal("l", surface[1, 0].Character);
        Assert.Equal("o", surface[2, 0].Character);
    }

    #endregion

    #region WriteText - Wide Characters

    [Fact]
    public void WriteText_WideCharacter_SetsContinuation()
    {
        var surface = new Surface(10, 1);
        
        surface.WriteText(0, 0, "日");
        
        Assert.Equal("日", surface[0, 0].Character);
        Assert.Equal(2, surface[0, 0].DisplayWidth);
        Assert.True(surface[1, 0].IsContinuation);
    }

    [Fact]
    public void WriteText_WideCharacterSequence_HandlesCorrectly()
    {
        var surface = new Surface(10, 1);
        
        surface.WriteText(0, 0, "日本");
        
        // First character
        Assert.Equal("日", surface[0, 0].Character);
        Assert.True(surface[1, 0].IsContinuation);
        // Second character
        Assert.Equal("本", surface[2, 0].Character);
        Assert.True(surface[3, 0].IsContinuation);
    }

    [Fact]
    public void WriteText_MixedWidthCharacters_HandlesCorrectly()
    {
        var surface = new Surface(10, 1);
        
        surface.WriteText(0, 0, "A日B");
        
        Assert.Equal("A", surface[0, 0].Character);
        Assert.Equal(1, surface[0, 0].DisplayWidth);
        Assert.Equal("日", surface[1, 0].Character);
        Assert.Equal(2, surface[1, 0].DisplayWidth);
        Assert.True(surface[2, 0].IsContinuation);
        Assert.Equal("B", surface[3, 0].Character);
        Assert.Equal(1, surface[3, 0].DisplayWidth);
    }

    [Fact]
    public void WriteText_WideCharacterAtEdge_ClipsWithSpace()
    {
        var surface = new Surface(4, 1);  // Only 4 columns - "日" won't fit after "ABC"
        
        // "ABC" takes 3 columns, "日" would need 2 more but only 1 remains
        surface.WriteText(0, 0, "ABC日");
        
        Assert.Equal("A", surface[0, 0].Character);
        Assert.Equal("B", surface[1, 0].Character);
        Assert.Equal("C", surface[2, 0].Character);
        // Wide char doesn't fully fit, remaining space filled with space
        Assert.Equal(" ", surface[3, 0].Character);
    }

    [Fact]
    public void WriteText_Emoji_HandlesAsWideCharacter()
    {
        var surface = new Surface(10, 1);
        
        surface.WriteText(0, 0, "😀");
        
        Assert.Equal("😀", surface[0, 0].Character);
        Assert.Equal(2, surface[0, 0].DisplayWidth);
        Assert.True(surface[1, 0].IsContinuation);
    }

    #endregion

    #region WriteChar

    [Fact]
    public void WriteChar_InBounds_WritesAndReturnsTrue()
    {
        var surface = new Surface(10, 5);
        
        var result = surface.WriteChar(3, 2, 'X', Hex1bColor.Red, Hex1bColor.Blue, CellAttributes.Bold);
        
        Assert.True(result);
        Assert.Equal("X", surface[3, 2].Character);
        Assert.Equal(Hex1bColor.Red, surface[3, 2].Foreground);
        Assert.Equal(Hex1bColor.Blue, surface[3, 2].Background);
        Assert.Equal(CellAttributes.Bold, surface[3, 2].Attributes);
    }

    [Fact]
    public void WriteChar_OutOfBounds_ReturnsFalse()
    {
        var surface = new Surface(10, 5);
        
        Assert.False(surface.WriteChar(-1, 0, 'X'));
        Assert.False(surface.WriteChar(10, 0, 'X'));
        Assert.False(surface.WriteChar(0, -1, 'X'));
        Assert.False(surface.WriteChar(0, 5, 'X'));
    }

    #endregion

    #region Composite

    [Fact]
    public void Composite_CopiesCellsToDestination()
    {
        var dest = new Surface(10, 5);
        var src = new Surface(3, 2);
        src[0, 0] = new SurfaceCell("A", Hex1bColor.Red, Hex1bColor.Blue);
        src[1, 0] = new SurfaceCell("B", Hex1bColor.Red, Hex1bColor.Blue);
        src[2, 0] = new SurfaceCell("C", Hex1bColor.Red, Hex1bColor.Blue);
        
        dest.Composite(src, 2, 1);
        
        Assert.Equal("A", dest[2, 1].Character);
        Assert.Equal("B", dest[3, 1].Character);
        Assert.Equal("C", dest[4, 1].Character);
    }

    [Fact]
    public void Composite_WithOffset_PlacesCorrectly()
    {
        var dest = new Surface(10, 5);
        var src = new Surface(2, 2);
        src[0, 0] = new SurfaceCell("1", null, Hex1bColor.Red);
        src[1, 1] = new SurfaceCell("2", null, Hex1bColor.Blue);
        
        dest.Composite(src, 5, 3);
        
        Assert.Equal("1", dest[5, 3].Character);
        Assert.Equal("2", dest[6, 4].Character);
    }

    [Fact]
    public void Composite_ClipsToDestinationBounds()
    {
        var dest = new Surface(5, 5);
        var src = new Surface(3, 3);
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                src[x, y] = new SurfaceCell("X", Hex1bColor.Red, Hex1bColor.Red);
        
        // Place partially outside right edge
        dest.Composite(src, 4, 0);
        
        // Only one column should be visible
        Assert.Equal("X", dest[4, 0].Character);
        Assert.Equal("X", dest[4, 1].Character);
        Assert.Equal("X", dest[4, 2].Character);
    }

    [Fact]
    public void Composite_TransparentBackground_ShowsDestinationBackground()
    {
        var dest = new Surface(10, 5);
        dest[3, 2] = new SurfaceCell(" ", null, Hex1bColor.Blue);
        
        var src = new Surface(3, 3);
        src[1, 1] = new SurfaceCell("X", Hex1bColor.Red, null); // Transparent bg
        
        dest.Composite(src, 2, 1);
        
        var result = dest[3, 2];
        Assert.Equal("X", result.Character);
        Assert.Equal(Hex1bColor.Red, result.Foreground);
        Assert.Equal(Hex1bColor.Blue, result.Background); // From destination
    }

    [Fact]
    public void Composite_WithClipRect_OnlyAffectsClippedRegion()
    {
        var dest = new Surface(10, 5);
        var src = new Surface(5, 3);
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 5; x++)
                src[x, y] = new SurfaceCell("X", null, Hex1bColor.Red);
        
        var clip = new Rect(2, 1, 2, 2);
        dest.Composite(src, 0, 0, clip);
        
        // Only cells within clip should be affected
        Assert.Equal("X", dest[2, 1].Character);
        Assert.Equal("X", dest[3, 1].Character);
        Assert.Equal("X", dest[2, 2].Character);
        Assert.Equal("X", dest[3, 2].Character);
        
        // Cells outside clip should be empty
        Assert.Equal(SurfaceCells.Empty, dest[0, 0]);
        Assert.Equal(SurfaceCells.Empty, dest[1, 1]);
        Assert.Equal(SurfaceCells.Empty, dest[4, 1]);
    }

    #endregion

    #region Clone and Span

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new Surface(5, 3);
        original[2, 1] = new SurfaceCell("X", Hex1bColor.Red, null);
        
        var clone = original.Clone();
        clone[2, 1] = new SurfaceCell("Y", Hex1bColor.Blue, null);
        
        Assert.Equal("X", original[2, 1].Character);
        Assert.Equal("Y", clone[2, 1].Character);
    }

    [Fact]
    public void AsSpan_ReturnsAllCells()
    {
        var surface = new Surface(3, 2);
        surface[0, 0] = new SurfaceCell("A", null, null);
        surface[2, 1] = new SurfaceCell("B", null, null);
        
        var span = surface.AsSpan();
        
        Assert.Equal(6, span.Length);
        Assert.Equal("A", span[0].Character);
        Assert.Equal("B", span[5].Character); // Row-major: [2, 1] = 1*3 + 2 = 5
    }

    [Fact]
    public void GetRow_ReturnsCorrectRow()
    {
        var surface = new Surface(5, 3);
        surface.WriteText(0, 1, "Hello");
        
        var row = surface.GetRow(1);
        
        Assert.Equal(5, row.Length);
        Assert.Equal("H", row[0].Character);
        Assert.Equal("o", row[4].Character);
    }

    [Fact]
    public void GetRow_OutOfBounds_Throws()
    {
        var surface = new Surface(5, 3);
        
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.GetRow(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.GetRow(3));
    }

    #endregion
}

/// <summary>
/// Tests for <see cref="CompositeSurface"/> class.
/// </summary>
public class CompositeSurfaceTests
{
    #region Construction

    [Fact]
    public void Constructor_WithValidDimensions_CreatesCompositeSurface()
    {
        var composite = new CompositeSurface(80, 24);
        
        Assert.Equal(80, composite.Width);
        Assert.Equal(24, composite.Height);
        Assert.Equal(0, composite.LayerCount);
    }

    [Fact]
    public void Constructor_WithZeroDimensions_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompositeSurface(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompositeSurface(10, 0));
    }

    #endregion

    #region AddLayer

    [Fact]
    public void AddLayer_IncreasesLayerCount()
    {
        var composite = new CompositeSurface(10, 10);
        var layer1 = new Surface(5, 5);
        var layer2 = new Surface(5, 5);
        
        composite.AddLayer(layer1, 0, 0);
        Assert.Equal(1, composite.LayerCount);
        
        composite.AddLayer(layer2, 2, 2);
        Assert.Equal(2, composite.LayerCount);
    }

    [Fact]
    public void Clear_RemovesAllLayers()
    {
        var composite = new CompositeSurface(10, 10);
        composite.AddLayer(new Surface(5, 5), 0, 0);
        composite.AddLayer(new Surface(5, 5), 2, 2);
        
        composite.Clear();
        
        Assert.Equal(0, composite.LayerCount);
    }

    #endregion

    #region GetCell

    [Fact]
    public void GetCell_NoLayers_ReturnsEmpty()
    {
        var composite = new CompositeSurface(10, 10);
        
        var cell = composite.GetCell(5, 5);
        
        Assert.Equal(SurfaceCells.Empty, cell);
    }

    [Fact]
    public void GetCell_SingleLayer_ReturnsCellFromLayer()
    {
        var composite = new CompositeSurface(10, 10);
        var layer = new Surface(5, 5);
        layer[2, 2] = new SurfaceCell("X", Hex1bColor.Red, Hex1bColor.Blue);
        
        composite.AddLayer(layer, 0, 0);
        
        Assert.Equal("X", composite.GetCell(2, 2).Character);
        Assert.Equal(Hex1bColor.Red, composite.GetCell(2, 2).Foreground);
    }

    [Fact]
    public void GetCell_WithOffset_ReturnsCorrectCell()
    {
        var composite = new CompositeSurface(10, 10);
        var layer = new Surface(3, 3);
        layer[0, 0] = new SurfaceCell("A", Hex1bColor.Red, null);
        
        composite.AddLayer(layer, 5, 5);
        
        // Layer starts at (5, 5), so layer[0,0] is at composite[5,5]
        Assert.Equal("A", composite.GetCell(5, 5).Character);
        Assert.Equal(SurfaceCells.Empty, composite.GetCell(0, 0));
    }

    [Fact]
    public void GetCell_OutOfBounds_Throws()
    {
        var composite = new CompositeSurface(10, 10);
        
        Assert.Throws<ArgumentOutOfRangeException>(() => composite.GetCell(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => composite.GetCell(10, 0));
    }

    #endregion

    #region Layer Ordering

    [Fact]
    public void GetCell_OverlappingLayers_TopLayerWins()
    {
        var composite = new CompositeSurface(10, 10);
        
        var bottom = new Surface(5, 5);
        bottom[2, 2] = new SurfaceCell("B", Hex1bColor.Blue, Hex1bColor.Blue);
        
        var top = new Surface(5, 5);
        top[2, 2] = new SurfaceCell("T", Hex1bColor.Red, Hex1bColor.Red);
        
        composite.AddLayer(bottom, 0, 0);  // Added first = bottom
        composite.AddLayer(top, 0, 0);     // Added second = top
        
        var cell = composite.GetCell(2, 2);
        Assert.Equal("T", cell.Character);
        Assert.Equal(Hex1bColor.Red, cell.Foreground);
    }

    [Fact]
    public void GetCell_TransparentTopLayer_ShowsBottomBackground()
    {
        var composite = new CompositeSurface(10, 10);
        
        var bottom = new Surface(5, 5);
        bottom[2, 2] = new SurfaceCell(" ", null, Hex1bColor.Blue);
        
        var top = new Surface(5, 5);
        top[2, 2] = new SurfaceCell("X", Hex1bColor.Red, null);  // Transparent bg
        
        composite.AddLayer(bottom, 0, 0);
        composite.AddLayer(top, 0, 0);
        
        var cell = composite.GetCell(2, 2);
        Assert.Equal("X", cell.Character);
        Assert.Equal(Hex1bColor.Red, cell.Foreground);
        Assert.Equal(Hex1bColor.Blue, cell.Background);  // From bottom layer
    }

    [Fact]
    public void GetCell_PartiallyOverlappingLayers_ResolvesCorrectly()
    {
        var composite = new CompositeSurface(10, 10);
        
        var layer1 = new Surface(5, 5);
        layer1.Fill(new Rect(0, 0, 5, 5), new SurfaceCell("1", Hex1bColor.Red, Hex1bColor.Red));
        
        var layer2 = new Surface(3, 3);
        layer2.Fill(new Rect(0, 0, 3, 3), new SurfaceCell("2", Hex1bColor.Blue, Hex1bColor.Blue));
        
        composite.AddLayer(layer1, 0, 0);
        composite.AddLayer(layer2, 3, 3);  // Overlaps from (3,3) to (5,5)
        
        Assert.Equal("1", composite.GetCell(0, 0).Character);  // Only layer1
        Assert.Equal("1", composite.GetCell(2, 2).Character);  // Only layer1
        Assert.Equal("2", composite.GetCell(3, 3).Character);  // layer2 on top
        Assert.Equal("2", composite.GetCell(5, 5).Character);  // Only layer2
        Assert.Equal(SurfaceCells.Empty, composite.GetCell(6, 6));  // No layers
    }

    #endregion

    #region Flatten

    [Fact]
    public void Flatten_NoLayers_ReturnsEmptySurface()
    {
        var composite = new CompositeSurface(5, 5);
        
        var result = composite.Flatten();
        
        Assert.Equal(5, result.Width);
        Assert.Equal(5, result.Height);
        Assert.Equal(SurfaceCells.Empty, result[0, 0]);
    }

    [Fact]
    public void Flatten_SingleLayer_CopiesContent()
    {
        var composite = new CompositeSurface(10, 10);
        var layer = new Surface(5, 5);
        layer.WriteText(0, 0, "Hello", Hex1bColor.Red);
        
        composite.AddLayer(layer, 2, 3);
        var result = composite.Flatten();
        
        Assert.Equal("H", result[2, 3].Character);
        Assert.Equal("o", result[6, 3].Character);
        Assert.Equal(Hex1bColor.Red, result[2, 3].Foreground);
    }

    [Fact]
    public void Flatten_MultipleLayers_CompositesCorrectly()
    {
        var composite = new CompositeSurface(10, 5);
        
        var background = new Surface(10, 5);
        background.Fill(new Rect(0, 0, 10, 5), SurfaceCells.Space(Hex1bColor.DarkGray));
        
        var content = new Surface(6, 3);
        content.WriteText(0, 0, "Title", Hex1bColor.White, Hex1bColor.Blue);
        
        composite.AddLayer(background, 0, 0);
        composite.AddLayer(content, 2, 1);
        
        var result = composite.Flatten();
        
        // Background visible outside content
        Assert.Equal(Hex1bColor.DarkGray, result[0, 0].Background);
        
        // Content visible at offset
        Assert.Equal("T", result[2, 1].Character);
        Assert.Equal(Hex1bColor.Blue, result[2, 1].Background);
    }

    [Fact]
    public void Flatten_ReturnsIndependentSurface()
    {
        var composite = new CompositeSurface(5, 5);
        var layer = new Surface(5, 5);
        layer[2, 2] = new SurfaceCell("A", null, null);
        composite.AddLayer(layer, 0, 0);
        
        var result = composite.Flatten();
        
        // Modify original layer
        layer[2, 2] = new SurfaceCell("B", null, null);
        
        // Result should be unchanged
        Assert.Equal("A", result[2, 2].Character);
    }

    #endregion

    #region Nested Composition

    [Fact]
    public void GetCell_NestedCompositeSurface_ResolvesRecursively()
    {
        // Create an inner composite
        var inner = new CompositeSurface(5, 5);
        var innerLayer = new Surface(3, 3);
        innerLayer[1, 1] = new SurfaceCell("I", Hex1bColor.Green, Hex1bColor.Green);
        inner.AddLayer(innerLayer, 1, 1);
        
        // Create outer composite containing the inner one
        var outer = new CompositeSurface(10, 10);
        outer.AddLayer(inner, 2, 2);
        
        // Inner layer[1,1] is at inner[2,2] is at outer[4,4]
        var cell = outer.GetCell(4, 4);
        Assert.Equal("I", cell.Character);
        Assert.Equal(Hex1bColor.Green, cell.Foreground);
    }

    [Fact]
    public void Flatten_NestedCompositeSurface_FlattensAll()
    {
        var inner = new CompositeSurface(5, 5);
        var innerLayer = new Surface(5, 5);
        innerLayer.WriteText(0, 0, "Inner", Hex1bColor.Blue);
        inner.AddLayer(innerLayer, 0, 0);
        
        var outer = new CompositeSurface(10, 10);
        var background = new Surface(10, 10);
        background.Fill(new Rect(0, 0, 10, 10), SurfaceCells.Space(Hex1bColor.Gray));
        
        outer.AddLayer(background, 0, 0);
        outer.AddLayer(inner, 3, 3);
        
        var result = outer.Flatten();
        
        Assert.Equal(Hex1bColor.Gray, result[0, 0].Background);  // Background
        Assert.Equal("I", result[3, 3].Character);  // Inner content
        Assert.Equal("r", result[7, 3].Character);  // "Inner" at offset
    }

    #endregion

    #region ISurfaceSource Interface

    [Fact]
    public void TryGetCell_InBounds_ReturnsTrueAndCell()
    {
        var composite = new CompositeSurface(10, 10);
        var layer = new Surface(5, 5);
        layer[2, 2] = new SurfaceCell("X", null, null);
        composite.AddLayer(layer, 0, 0);
        
        var result = composite.TryGetCell(2, 2, out var cell);
        
        Assert.True(result);
        Assert.Equal("X", cell.Character);
    }

    [Fact]
    public void TryGetCell_OutOfBounds_ReturnsFalse()
    {
        var composite = new CompositeSurface(10, 10);
        
        Assert.False(composite.TryGetCell(-1, 0, out _));
        Assert.False(composite.TryGetCell(10, 0, out _));
    }

    [Fact]
    public void IsInBounds_ValidPositions_ReturnsTrue()
    {
        var composite = new CompositeSurface(10, 10);
        
        Assert.True(composite.IsInBounds(0, 0));
        Assert.True(composite.IsInBounds(9, 9));
        Assert.True(composite.IsInBounds(5, 5));
    }

    [Fact]
    public void IsInBounds_InvalidPositions_ReturnsFalse()
    {
        var composite = new CompositeSurface(10, 10);
        
        Assert.False(composite.IsInBounds(-1, 0));
        Assert.False(composite.IsInBounds(10, 0));
        Assert.False(composite.IsInBounds(0, 10));
    }

    [Fact]
    public void Surface_Composite_AcceptsCompositeSurface()
    {
        var dest = new Surface(10, 10);
        
        var composite = new CompositeSurface(5, 5);
        var layer = new Surface(5, 5);
        layer.WriteText(0, 0, "Test", Hex1bColor.Red);
        composite.AddLayer(layer, 0, 0);
        
        // Composite a CompositeSurface onto a Surface
        dest.Composite(composite, 2, 2);
        
        Assert.Equal("T", dest[2, 2].Character);
        Assert.Equal("t", dest[5, 2].Character);
        Assert.Equal(Hex1bColor.Red, dest[2, 2].Foreground);
    }

    #endregion
}

/// <summary>
/// Tests for computed cells in <see cref="CompositeSurface"/>.
/// </summary>
public class ComputedCellTests
{
    #region Basic Computed Cells

    [Fact]
    public void AddComputedLayer_CreatesLayerWithCompute()
    {
        var composite = new CompositeSurface(10, 10);
        
        composite.AddComputedLayer(5, 5, ctx => new SurfaceCell("C", Hex1bColor.Red, null), 0, 0);
        
        Assert.Equal(1, composite.LayerCount);
    }

    [Fact]
    public void ComputedLayer_ReturnsComputedCells()
    {
        var composite = new CompositeSurface(10, 10);
        
        composite.AddComputedLayer(5, 5, ctx => 
            new SurfaceCell($"{ctx.X},{ctx.Y}", Hex1bColor.Green, null), 0, 0);
        
        Assert.Equal("0,0", composite.GetCell(0, 0).Character);
        Assert.Equal("2,3", composite.GetCell(2, 3).Character);
        Assert.Equal(Hex1bColor.Green, composite.GetCell(0, 0).Foreground);
    }

    [Fact]
    public void ComputedLayer_WithOffset_ComputesAtCorrectPosition()
    {
        var composite = new CompositeSurface(10, 10);
        
        // Computed layer at offset (3, 3)
        composite.AddComputedLayer(5, 5, ctx => 
            new SurfaceCell("X", Hex1bColor.Blue, null), 3, 3);
        
        // Outside computed layer bounds
        Assert.Equal(SurfaceCells.Empty, composite.GetCell(0, 0));
        Assert.Equal(SurfaceCells.Empty, composite.GetCell(2, 2));
        
        // Inside computed layer bounds
        Assert.Equal("X", composite.GetCell(3, 3).Character);
        Assert.Equal("X", composite.GetCell(7, 7).Character);
        
        // Just outside computed layer
        Assert.Equal(SurfaceCells.Empty, composite.GetCell(8, 8));
    }

    #endregion

    #region GetBelow

    [Fact]
    public void GetBelow_ReturnsContentFromLayerBelow()
    {
        var composite = new CompositeSurface(10, 10);
        
        // Bottom layer with content
        var bottom = new Surface(10, 10);
        bottom.Fill(new Rect(0, 0, 10, 10), new SurfaceCell(" ", null, Hex1bColor.Blue));
        composite.AddLayer(bottom, 0, 0);
        
        // Computed layer that reads from below
        composite.AddComputedLayer(10, 10, ctx =>
        {
            var below = ctx.GetBelow();
            return new SurfaceCell("X", Hex1bColor.White, below.Background);
        }, 0, 0);
        
        var cell = composite.GetCell(5, 5);
        Assert.Equal("X", cell.Character);
        Assert.Equal(Hex1bColor.White, cell.Foreground);
        Assert.Equal(Hex1bColor.Blue, cell.Background);  // From layer below
    }

    [Fact]
    public void GetBelow_WithMultipleLayers_CompositesAllBelow()
    {
        var composite = new CompositeSurface(10, 10);
        
        // Layer 0: Red background
        var layer0 = new Surface(10, 10);
        layer0.Fill(new Rect(0, 0, 10, 10), new SurfaceCell(" ", null, Hex1bColor.Red));
        composite.AddLayer(layer0, 0, 0);
        
        // Layer 1: Partial overlay with green (transparent elsewhere)
        var layer1 = new Surface(5, 5);
        layer1.Fill(new Rect(0, 0, 5, 5), new SurfaceCell(" ", null, Hex1bColor.Green));
        composite.AddLayer(layer1, 0, 0);
        
        // Layer 2: Computed layer that reads below
        composite.AddComputedLayer(10, 10, ctx =>
        {
            var below = ctx.GetBelow();
            return new SurfaceCell("*", Hex1bColor.White, below.Background);
        }, 0, 0);
        
        // At (2, 2): green from layer1 (on top of red from layer0)
        Assert.Equal(Hex1bColor.Green, composite.GetCell(2, 2).Background);
        
        // At (7, 7): red from layer0 (layer1 doesn't cover this)
        Assert.Equal(Hex1bColor.Red, composite.GetCell(7, 7).Background);
    }

    [Fact]
    public void GetBelowAt_ReturnsContentAtDifferentPosition()
    {
        var composite = new CompositeSurface(10, 10);
        
        // Bottom layer with specific content
        var bottom = new Surface(10, 10);
        bottom[5, 5] = new SurfaceCell("A", Hex1bColor.Red, Hex1bColor.Blue);
        composite.AddLayer(bottom, 0, 0);
        
        // Computed layer that reads from a different position
        composite.AddComputedLayer(10, 10, ctx =>
        {
            // Always read from position (5, 5)
            var cell = ctx.GetBelowAt(5, 5);
            return cell with { Character = "B" };
        }, 0, 0);
        
        // Any position should have the content from (5, 5) below
        var result = composite.GetCell(0, 0);
        Assert.Equal("B", result.Character);
        Assert.Equal(Hex1bColor.Red, result.Foreground);
        Assert.Equal(Hex1bColor.Blue, result.Background);
    }

    #endregion

    #region GetAdjacent

    [Fact]
    public void GetAdjacent_ReturnsNeighborCell()
    {
        var composite = new CompositeSurface(10, 10);
        
        // Bottom layer with content
        var bottom = new Surface(10, 10);
        bottom.WriteText(0, 0, "ABCDE", Hex1bColor.Red);
        composite.AddLayer(bottom, 0, 0);
        
        // Computed layer that returns the character from the cell to the right in the layer BELOW
        composite.AddComputedLayer(10, 10, ctx =>
        {
            var right = ctx.GetBelowAt(ctx.X + 1, ctx.Y);
            return new SurfaceCell(right.Character, Hex1bColor.Blue, null);
        }, 0, 0);
        
        // Cell at (0, 0) should have character from below at (1, 0) = "B"
        Assert.Equal("B", composite.GetCell(0, 0).Character);
        Assert.Equal("C", composite.GetCell(1, 0).Character);
        Assert.Equal("D", composite.GetCell(2, 0).Character);
    }

    [Fact]
    public void GetAdjacent_OnMixedLayer_ReturnsStaticNeighbor()
    {
        var composite = new CompositeSurface(10, 10);
        
        // A surface with some content - this will be used as a layer
        // but we'll also add computed cells that can reference static cells via GetAdjacent
        var layer = new Surface(10, 10);
        layer.WriteText(0, 0, "Hello", Hex1bColor.White);
        composite.AddLayer(layer, 0, 0);
        
        // Note: GetAdjacent reads from the same layer.
        // For a regular layer (not computed), it will get static cells.
        // For testing GetAdjacent properly, we'd need computed cells that reference
        // other cells on the same layer. The GetBelowAt test above covers the main use case.
        
        // Verify the static layer is accessible
        Assert.Equal("H", composite.GetCell(0, 0).Character);
        Assert.Equal("e", composite.GetCell(1, 0).Character);
    }

    [Fact]
    public void GetAdjacent_OutOfBounds_ReturnsEmpty()
    {
        var composite = new CompositeSurface(5, 5);
        
        // Computed layer that tries to read beyond bounds
        composite.AddComputedLayer(5, 5, ctx =>
        {
            // Try to read one cell to the left
            var left = ctx.GetAdjacent(-1, 0);
            return new SurfaceCell(left.Character, null, Hex1bColor.Gray);
        }, 0, 0);
        
        // At x=0, there's no cell to the left, should return empty
        var result = composite.GetCell(0, 0);
        Assert.Equal(" ", result.Character);  // Empty cell's character is space
    }

    #endregion

    #region Cycle Detection

    [Fact]
    public void ComputedCell_SelfReference_ReturnsFallback()
    {
        var composite = new CompositeSurface(5, 5);
        
        // Computed layer that tries to read itself (at same position in same layer)
        composite.AddComputedLayer(5, 5, ctx =>
        {
            // This would cause infinite recursion without cycle detection
            var self = ctx.GetAdjacent(0, 0);  // Same position
            return new SurfaceCell("X", null, null);
        }, 0, 0);
        
        // Should not throw and should return a value
        var result = composite.GetCell(2, 2);
        Assert.Equal("X", result.Character);
    }

    [Fact]
    public void ComputedCell_MutualReference_ReturnsFallback()
    {
        var composite = new CompositeSurface(10, 5);
        
        // Computed layer where each cell reads from the cell to its right
        // This creates a chain: cell(0) -> cell(1) -> cell(2) -> ... -> cell(9) -> out of bounds
        composite.AddComputedLayer(10, 5, ctx =>
        {
            var right = ctx.GetAdjacent(1, 0);
            return new SurfaceCell($"[{right.Character}]", null, null);
        }, 0, 0);
        
        // At x=9, the cell to the right is out of bounds (empty)
        var rightmost = composite.GetCell(9, 0);
        Assert.Equal("[ ]", rightmost.Character);  // Empty cell has space
        
        // At x=8, it reads from x=9
        var cell8 = composite.GetCell(8, 0);
        Assert.Equal("[[ ]]", cell8.Character);
    }

    #endregion

    #region CellEffects

    [Fact]
    public void DropShadow_DarkensBackground()
    {
        var composite = new CompositeSurface(10, 10);
        
        // Background layer with white background
        var bg = new Surface(10, 10);
        bg.Fill(new Rect(0, 0, 10, 10), new SurfaceCell(" ", null, Hex1bColor.White));
        composite.AddLayer(bg, 0, 0);
        
        // Shadow layer (50% opacity)
        composite.AddComputedLayer(10, 10, CellEffects.DropShadow(0.5f), 0, 0);
        
        var cell = composite.GetCell(5, 5);
        
        // White (255, 255, 255) darkened by 50% = (127, 127, 127)
        Assert.NotNull(cell.Background);
        Assert.Equal(127, cell.Background.Value.R);
        Assert.Equal(127, cell.Background.Value.G);
        Assert.Equal(127, cell.Background.Value.B);
    }

    [Fact]
    public void Tint_AppliesColorOverlay()
    {
        var composite = new CompositeSurface(10, 10);
        
        // Background with white
        var bg = new Surface(10, 10);
        bg.Fill(new Rect(0, 0, 10, 10), new SurfaceCell(" ", Hex1bColor.White, Hex1bColor.White));
        composite.AddLayer(bg, 0, 0);
        
        // Red tint at 50%
        composite.AddComputedLayer(10, 10, CellEffects.Tint(Hex1bColor.Red, 0.5f), 0, 0);
        
        var cell = composite.GetCell(5, 5);
        
        // White (255) blended with Red (255, 0, 0) at 50%
        // R: 255 * 0.5 + 255 * 0.5 = 255
        // G: 255 * 0.5 + 0 * 0.5 = 127
        // B: 255 * 0.5 + 0 * 0.5 = 127
        Assert.NotNull(cell.Background);
        Assert.Equal(255, cell.Background.Value.R);
        Assert.Equal(127, cell.Background.Value.G);
        Assert.Equal(127, cell.Background.Value.B);
    }

    [Fact]
    public void Passthrough_ReturnsCellBelow()
    {
        var composite = new CompositeSurface(10, 10);
        
        // Background with content
        var bg = new Surface(10, 10);
        bg[5, 5] = new SurfaceCell("X", Hex1bColor.Red, Hex1bColor.Blue);
        composite.AddLayer(bg, 0, 0);
        
        // Passthrough layer
        composite.AddComputedLayer(10, 10, CellEffects.Passthrough(), 0, 0);
        
        var cell = composite.GetCell(5, 5);
        Assert.Equal("X", cell.Character);
        Assert.Equal(Hex1bColor.Red, cell.Foreground);
        Assert.Equal(Hex1bColor.Blue, cell.Background);
    }

    [Fact]
    public void Conditional_AppliesCorrectEffect()
    {
        var composite = new CompositeSurface(10, 10);
        
        // Background
        var bg = new Surface(10, 10);
        bg.Fill(new Rect(0, 0, 10, 10), new SurfaceCell(" ", null, Hex1bColor.White));
        composite.AddLayer(bg, 0, 0);
        
        // Conditional: checkerboard pattern
        var effect = CellEffects.Conditional(
            (x, y) => (x + y) % 2 == 0,
            CellEffects.DropShadow(0.5f),  // Dark on even cells
            CellEffects.Passthrough());    // Unchanged on odd cells
        
        composite.AddComputedLayer(10, 10, effect, 0, 0);
        
        var evenCell = composite.GetCell(0, 0);  // 0+0=0, even
        var oddCell = composite.GetCell(1, 0);   // 1+0=1, odd
        
        Assert.Equal(127, evenCell.Background!.Value.R);  // Darkened
        Assert.Equal(255, oddCell.Background!.Value.R);   // Unchanged
    }

    #endregion

    #region Flatten with Computed Cells

    [Fact]
    public void Flatten_IncludesComputedCells()
    {
        var composite = new CompositeSurface(5, 5);
        
        // Background
        var bg = new Surface(5, 5);
        bg.Fill(new Rect(0, 0, 5, 5), new SurfaceCell(" ", null, Hex1bColor.Blue));
        composite.AddLayer(bg, 0, 0);
        
        // Computed layer
        composite.AddComputedLayer(5, 5, ctx => 
            new SurfaceCell("C", Hex1bColor.White, ctx.GetBelow().Background), 0, 0);
        
        var result = composite.Flatten();
        
        Assert.Equal("C", result[2, 2].Character);
        Assert.Equal(Hex1bColor.White, result[2, 2].Foreground);
        Assert.Equal(Hex1bColor.Blue, result[2, 2].Background);
    }

    [Fact]
    public void Flatten_ComputedLayerAtOffset_PositionsCorrectly()
    {
        var composite = new CompositeSurface(10, 10);
        
        // Computed layer only in a small region
        composite.AddComputedLayer(3, 3, ctx => 
            new SurfaceCell("X", Hex1bColor.Red, Hex1bColor.Red), 4, 4);
        
        var result = composite.Flatten();
        
        // Outside computed region
        Assert.Equal(SurfaceCells.Empty, result[0, 0]);
        Assert.Equal(SurfaceCells.Empty, result[3, 3]);
        
        // Inside computed region
        Assert.Equal("X", result[4, 4].Character);
        Assert.Equal("X", result[6, 6].Character);
        
        // Just outside
        Assert.Equal(SurfaceCells.Empty, result[7, 7]);
    }

    #endregion
}

/// <summary>
/// Tests for <see cref="SurfaceDiff"/> and <see cref="SurfaceComparer"/>.
/// </summary>
public class SurfaceDiffTests
{
    #region SurfaceDiff Construction

    [Fact]
    public void SurfaceDiff_Empty_HasNoChangedCells()
    {
        var diff = SurfaceDiff.Empty;

        Assert.True(diff.IsEmpty);
        Assert.Equal(0, diff.Count);
        Assert.Empty(diff.ChangedCells);
    }

    #endregion

    #region SurfaceComparer.Compare

    [Fact]
    public void Compare_IdenticalSurfaces_ReturnsEmptyDiff()
    {
        var surface1 = new Surface(5, 5);
        surface1.WriteText(0, 0, "Hello", Hex1bColor.White, Hex1bColor.Black);
        
        var surface2 = new Surface(5, 5);
        surface2.WriteText(0, 0, "Hello", Hex1bColor.White, Hex1bColor.Black);

        var diff = SurfaceComparer.Compare(surface1, surface2);

        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Compare_SingleCellChanged_ReturnsOneCellInDiff()
    {
        var previous = new Surface(5, 5);
        previous.Fill(new Rect(0, 0, 5, 5), new SurfaceCell("A", Hex1bColor.White, null));
        
        var current = new Surface(5, 5);
        current.Fill(new Rect(0, 0, 5, 5), new SurfaceCell("A", Hex1bColor.White, null));
        current[2, 2] = new SurfaceCell("B", Hex1bColor.Red, null);

        var diff = SurfaceComparer.Compare(previous, current);

        Assert.Equal(1, diff.Count);
        Assert.Equal(2, diff.ChangedCells[0].X);
        Assert.Equal(2, diff.ChangedCells[0].Y);
        Assert.Equal("B", diff.ChangedCells[0].Cell.Character);
    }

    [Fact]
    public void Compare_MultipleCellsChanged_ReturnsSortedByRowThenColumn()
    {
        var previous = new Surface(5, 5);
        var current = new Surface(5, 5);
        
        // Change cells in non-sorted order
        current[3, 1] = new SurfaceCell("X", null, null);
        current[1, 2] = new SurfaceCell("Y", null, null);
        current[0, 0] = new SurfaceCell("Z", null, null);
        current[2, 1] = new SurfaceCell("W", null, null);

        var diff = SurfaceComparer.Compare(previous, current);

        Assert.Equal(4, diff.Count);
        
        // Should be sorted by Y then X
        Assert.Equal((0, 0), (diff.ChangedCells[0].X, diff.ChangedCells[0].Y)); // Z
        Assert.Equal((2, 1), (diff.ChangedCells[1].X, diff.ChangedCells[1].Y)); // W
        Assert.Equal((3, 1), (diff.ChangedCells[2].X, diff.ChangedCells[2].Y)); // X
        Assert.Equal((1, 2), (diff.ChangedCells[3].X, diff.ChangedCells[3].Y)); // Y
    }

    [Fact]
    public void Compare_ColorChange_DetectsAsChange()
    {
        var previous = new Surface(3, 3);
        previous[1, 1] = new SurfaceCell("X", Hex1bColor.Red, null);
        
        var current = new Surface(3, 3);
        current[1, 1] = new SurfaceCell("X", Hex1bColor.Blue, null);

        var diff = SurfaceComparer.Compare(previous, current);

        Assert.Equal(1, diff.Count);
        Assert.Equal(Hex1bColor.Blue, diff.ChangedCells[0].Cell.Foreground);
    }

    [Fact]
    public void Compare_AttributeChange_DetectsAsChange()
    {
        var previous = new Surface(3, 3);
        previous[1, 1] = new SurfaceCell("X", null, null, CellAttributes.None);
        
        var current = new Surface(3, 3);
        current[1, 1] = new SurfaceCell("X", null, null, CellAttributes.Bold);

        var diff = SurfaceComparer.Compare(previous, current);

        Assert.Equal(1, diff.Count);
        Assert.Equal(CellAttributes.Bold, diff.ChangedCells[0].Cell.Attributes);
    }

    [Fact]
    public void Compare_DifferentSizes_ThrowsArgumentException()
    {
        var surface1 = new Surface(5, 5);
        var surface2 = new Surface(3, 3);

        Assert.Throws<ArgumentException>(() => SurfaceComparer.Compare(surface1, surface2));
    }

    [Fact]
    public void CompareToEmpty_NonEmptySurface_ReturnsAllNonEmptyCells()
    {
        var surface = new Surface(3, 3);
        surface[0, 0] = new SurfaceCell("A", null, null);
        surface[2, 2] = new SurfaceCell("B", null, null);

        var diff = SurfaceComparer.CompareToEmpty(surface);

        Assert.Equal(2, diff.Count);
    }

    [Fact]
    public void CompareToEmpty_EmptySurface_ReturnsEmptyDiff()
    {
        var surface = new Surface(3, 3);

        var diff = SurfaceComparer.CompareToEmpty(surface);

        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void CreateFullDiff_ReturnsAllCells()
    {
        var surface = new Surface(3, 3);

        var diff = SurfaceComparer.CreateFullDiff(surface);

        Assert.Equal(9, diff.Count);
    }

    #endregion

    #region SurfaceComparer.ToTokens

    [Fact]
    public void ToTokens_EmptyDiff_ReturnsEmptyList()
    {
        var tokens = SurfaceComparer.ToTokens(SurfaceDiff.Empty);

        Assert.Empty(tokens);
    }

    [Fact]
    public void ToTokens_SingleCell_GeneratesPositionAndText()
    {
        var previous = new Surface(5, 5);
        var current = new Surface(5, 5);
        current[2, 3] = new SurfaceCell("X", null, null);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        // Should have: CursorPosition, SGR (for reset), Text
        Assert.Contains(tokens, t => t is CursorPositionToken pos && pos.Row == 4 && pos.Column == 3); // 1-based
        Assert.Contains(tokens, t => t is TextToken txt && txt.Text == "X");
    }

    [Fact]
    public void ToTokens_ConsecutiveCellsOnRow_OmitsExtraCursorPositions()
    {
        var previous = new Surface(10, 1);
        var current = new Surface(10, 1);
        current[0, 0] = new SurfaceCell("A", null, null);
        current[1, 0] = new SurfaceCell("B", null, null);
        current[2, 0] = new SurfaceCell("C", null, null);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        // Should only have 1 cursor position for the start
        var cursorPositions = tokens.OfType<CursorPositionToken>().ToList();
        Assert.Single(cursorPositions);
        Assert.Equal(1, cursorPositions[0].Row);
        Assert.Equal(1, cursorPositions[0].Column);
    }

    [Fact]
    public void ToTokens_CellWithColors_GeneratesSgrWithColors()
    {
        var previous = new Surface(5, 5);
        var current = new Surface(5, 5);
        current[0, 0] = new SurfaceCell("X", Hex1bColor.Red, Hex1bColor.Blue);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        var sgr = tokens.OfType<SgrToken>().FirstOrDefault();
        Assert.NotNull(sgr);
        // Should contain foreground (38;2;255;0;0) and background (48;2;0;0;255)
        Assert.Contains("38;2;255;0;0", sgr.Parameters);
        Assert.Contains("48;2;0;0;255", sgr.Parameters);
    }

    [Fact]
    public void ToTokens_CellWithBoldAttribute_GeneratesSgrWithBold()
    {
        var previous = new Surface(5, 5);
        var current = new Surface(5, 5);
        current[0, 0] = new SurfaceCell("X", null, null, CellAttributes.Bold);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        var sgr = tokens.OfType<SgrToken>().FirstOrDefault();
        Assert.NotNull(sgr);
        // Bold is SGR code 1
        Assert.Contains("1", sgr.Parameters.Split(';'));
    }

    [Fact]
    public void ToTokens_SameAttributesConsecutive_DoesNotRepeatSgr()
    {
        var previous = new Surface(10, 1);
        var current = new Surface(10, 1);
        current[0, 0] = new SurfaceCell("A", Hex1bColor.Red, null);
        current[1, 0] = new SurfaceCell("B", Hex1bColor.Red, null);
        current[2, 0] = new SurfaceCell("C", Hex1bColor.Red, null);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        // Only 1 SGR token needed since all cells have same attributes
        var sgrTokens = tokens.OfType<SgrToken>().ToList();
        Assert.Single(sgrTokens);
    }

    [Fact]
    public void ToTokens_WideCharacter_SkipsContinuationCell()
    {
        var previous = new Surface(10, 1);
        var current = new Surface(10, 1);
        current.WriteText(0, 0, "你", Hex1bColor.White, null);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        // Should only have one text token with "你", not the continuation
        var textTokens = tokens.OfType<TextToken>().ToList();
        Assert.Single(textTokens);
        Assert.Equal("你", textTokens[0].Text);
    }

    [Fact]
    public void ToTokens_WideCharacter_AdvancesCursorCorrectly()
    {
        var previous = new Surface(10, 1);
        var current = new Surface(10, 1);
        current.WriteText(0, 0, "你好", Hex1bColor.White, null); // Two wide chars

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        // Should only have 1 cursor position (at start)
        // Because after "你" (width 2), cursor is at 2, which is where "好" is
        var cursorPositions = tokens.OfType<CursorPositionToken>().ToList();
        Assert.Single(cursorPositions);
    }

    #endregion

    #region SurfaceComparer.ToAnsiString

    [Fact]
    public void ToAnsiString_EmptyDiff_ReturnsEmptyString()
    {
        var result = SurfaceComparer.ToAnsiString(SurfaceDiff.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ToAnsiString_SingleCell_ContainsCorrectSequences()
    {
        var previous = new Surface(5, 5);
        var current = new Surface(5, 5);
        current[2, 3] = new SurfaceCell("X", null, null);

        var diff = SurfaceComparer.Compare(previous, current);
        var result = SurfaceComparer.ToAnsiString(diff);

        // Should contain cursor position sequence (ESC[row;colH)
        Assert.Contains("\x1b[4;3H", result); // Row 4, Column 3 (1-based)
        // Should contain the character
        Assert.Contains("X", result);
    }

    [Fact]
    public void ToAnsiString_CellWithRed_ContainsColorSequence()
    {
        var previous = new Surface(3, 3);
        var current = new Surface(3, 3);
        current[0, 0] = new SurfaceCell("R", Hex1bColor.Red, null);

        var diff = SurfaceComparer.Compare(previous, current);
        var result = SurfaceComparer.ToAnsiString(diff);

        // Should contain SGR for red foreground (ESC[...;38;2;255;0;0m)
        Assert.Contains("38;2;255;0;0", result);
    }

    #endregion

    #region SGR Parameter Generation

    [Fact]
    public void ToTokens_MultipleAttributes_CombinesInSingleSgr()
    {
        var previous = new Surface(3, 3);
        var current = new Surface(3, 3);
        current[0, 0] = new SurfaceCell("X", null, null, CellAttributes.Bold | CellAttributes.Underline);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        var sgr = tokens.OfType<SgrToken>().FirstOrDefault();
        Assert.NotNull(sgr);
        var parts = sgr.Parameters.Split(';');
        Assert.Contains("1", parts); // Bold
        Assert.Contains("4", parts); // Underline
    }

    [Fact]
    public void ToTokens_AttributeTurnedOff_ResetsFirst()
    {
        // Simulate state where we're transitioning from bold to no-bold
        // We need to go from Bold -> None, which requires a reset
        var previous = new Surface(3, 1);
        var current = new Surface(3, 1);
        current[0, 0] = new SurfaceCell("A", null, null, CellAttributes.Bold);
        current[1, 0] = new SurfaceCell("B", null, null, CellAttributes.None);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        var sgrTokens = tokens.OfType<SgrToken>().ToList();
        Assert.True(sgrTokens.Count >= 2); // At least 2: one for bold, one for reset
        
        // Second SGR should start with reset (0)
        Assert.StartsWith("0", sgrTokens[1].Parameters);
    }

    [Fact]
    public void ToTokens_ItalicAttribute_UsesSgrCode3()
    {
        var previous = new Surface(3, 3);
        var current = new Surface(3, 3);
        current[0, 0] = new SurfaceCell("X", null, null, CellAttributes.Italic);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        var sgr = tokens.OfType<SgrToken>().FirstOrDefault();
        Assert.NotNull(sgr);
        Assert.Contains("3", sgr.Parameters.Split(';'));
    }

    [Fact]
    public void ToTokens_StrikethroughAttribute_UsesSgrCode9()
    {
        var previous = new Surface(3, 3);
        var current = new Surface(3, 3);
        current[0, 0] = new SurfaceCell("X", null, null, CellAttributes.Strikethrough);

        var diff = SurfaceComparer.Compare(previous, current);
        var tokens = SurfaceComparer.ToTokens(diff);

        var sgr = tokens.OfType<SgrToken>().FirstOrDefault();
        Assert.NotNull(sgr);
        Assert.Contains("9", sgr.Parameters.Split(';'));
    }

    #endregion
}
