using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Tokens;

namespace Hex1b.Tests;

public class WindowGhostingTests
{
    [Fact]
    public void WindowMove_OldBorderCells_AppearInDiff()
    {
        // Simulate a window border moving right by 1 column
        var width = 30;
        var height = 15;

        // Frame 1: window at (5, 3) with size 10x5
        var surface1 = new Surface(width, height, new CellMetrics(8, 16));
        DrawBorder(surface1, 5, 3, 10, 5);

        // Frame 2: window moved to (6, 3) with size 10x5
        var surface2 = new Surface(width, height, new CellMetrics(8, 16));
        DrawBorder(surface2, 6, 3, 10, 5);

        // Diff
        var diff = SurfaceComparer.Compare(surface1, surface2);

        // The old left border at (5, 3) through (5, 7) should be in the diff
        // because it was "│" in surface1 and is now Empty in surface2
        var changedPositions = diff.ChangedCells.Select(c => (c.X, c.Y)).ToHashSet();

        // Old left border column
        for (int y = 3; y < 8; y++)
        {
            Assert.True(changedPositions.Contains((5, y)),
                $"Old left border at (5, {y}) should be in diff but isn't. " +
                $"prev='{surface1[5, y].Character}', curr='{surface2[5, y].Character}'");
        }
    }

    [Fact]
    public void WindowMove_TokenOutput_ClearsOldBorder()
    {
        var width = 30;
        var height = 15;

        // Frame 1
        var surface1 = new Surface(width, height, new CellMetrics(8, 16));
        DrawBorder(surface1, 5, 3, 10, 5);

        // Frame 2 (moved right by 1)
        var surface2 = new Surface(width, height, new CellMetrics(8, 16));
        DrawBorder(surface2, 6, 3, 10, 5);

        var diff = SurfaceComparer.Compare(surface1, surface2);
        var tokens = SurfaceComparer.ToTokens(diff, surface2, surface1);

        // Serialize to text
        var output = AnsiTokenUtf8Serializer.Serialize(tokens);
        var text = System.Text.Encoding.UTF8.GetString(output.Span);

        // The output should contain cursor positioning to the old border positions
        // and write spaces there (UnwrittenMarker → space conversion)
        // Check that position (5, 3) gets a space written
        // In ANSI: row=4, col=6 (1-based)
        Assert.Contains("\x1b[4;6H", text); // cursor to old border top-left
    }

    [Fact]
    public void WindowMove_FullSurface_ClearsOldPosition()
    {
        // Simulate full surface rendering with child surface composite
        var width = 30;
        var height = 15;

        // Frame 1: simulate rendering with Clear + Composite
        var rootSurface1 = new Surface(width, height, new CellMetrics(8, 16));
        rootSurface1.Clear();
        var childSurface1 = new Surface(10, 5, new CellMetrics(8, 16));
        DrawBorder(childSurface1, 0, 0, 10, 5);
        rootSurface1.Composite(childSurface1, 5, 3);

        // Frame 2: same but window moved right by 1
        var rootSurface2 = new Surface(width, height, new CellMetrics(8, 16));
        rootSurface2.Clear();
        var childSurface2 = new Surface(10, 5, new CellMetrics(8, 16));
        DrawBorder(childSurface2, 0, 0, 10, 5);
        rootSurface2.Composite(childSurface2, 6, 3);

        // At position (5, 3), rootSurface1 has "┌", rootSurface2 has Empty
        Assert.Equal("┌", rootSurface1[5, 3].Character);
        Assert.Equal(SurfaceCells.UnwrittenMarker, rootSurface2[5, 3].Character);

        var diff = SurfaceComparer.Compare(rootSurface1, rootSurface2);
        var changedPositions = diff.ChangedCells.Select(c => (c.X, c.Y)).ToHashSet();

        // Old top-left corner must be in diff
        Assert.True(changedPositions.Contains((5, 3)),
            $"Old top-left corner (5,3) must be in diff");
    }

    private static void DrawBorder(Surface surface, int x, int y, int w, int h)
    {
        // Draw a simple box border
        surface[x, y] = new SurfaceCell("┌", null, null);
        surface[x + w - 1, y] = new SurfaceCell("┐", null, null);
        surface[x, y + h - 1] = new SurfaceCell("└", null, null);
        surface[x + w - 1, y + h - 1] = new SurfaceCell("┘", null, null);

        for (int i = x + 1; i < x + w - 1; i++)
        {
            surface[i, y] = new SurfaceCell("─", null, null);
            surface[i, y + h - 1] = new SurfaceCell("─", null, null);
        }
        for (int j = y + 1; j < y + h - 1; j++)
        {
            surface[x, j] = new SurfaceCell("│", null, null);
            surface[x + w - 1, j] = new SurfaceCell("│", null, null);
        }

        // Fill interior with spaces
        for (int j = y + 1; j < y + h - 1; j++)
            for (int i = x + 1; i < x + w - 1; i++)
                surface[i, j] = new SurfaceCell(" ", null, null);
    }
}
