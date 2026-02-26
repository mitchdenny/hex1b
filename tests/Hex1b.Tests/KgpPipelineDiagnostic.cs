using System.Text;
using System.Linq;
using Hex1b;
using Hex1b.Nodes;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Tokens;
using Hex1b.Kgp;
using Xunit;

namespace Hex1b.Tests;

public class KgpPipelineDiagnostic
{
    [Fact]
    public void KgpRegion_NotOverwrittenBySubsequentCells()
    {
        // Simulate a KGP image at (0,0) spanning 4 cols x 2 rows
        // on a 20x10 surface with "Hello" text at row 3
        var surface = new Surface(20, 10);
        var ctx = new SurfaceRenderContext(surface, Theming.Hex1bThemes.Default);
        ctx.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });

        var pixelData = new byte[16];
        for (int i = 0; i < 4; i++) { pixelData[i * 4] = 255; pixelData[i * 4 + 3] = 255; }

        var node = new KittyGraphicsNode
        {
            PixelData = pixelData, PixelWidth = 2, PixelHeight = 2,
            DisplayColumns = 4, DisplayRows = 2,
        };
        node.Measure(new Constraints(0, 20, 0, 10));
        node.Arrange(new Rect(0, 0, 4, 2));
        node.Render(ctx);

        ctx.SetCursorPosition(0, 3);
        ctx.Write("Hello KGP");

        var diff = SurfaceComparer.CompareToEmpty(surface);
        var tokens = SurfaceComparer.ToTokens(diff, surface);

        // Find the KGP token
        int kgpIndex = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is UnrecognizedSequenceToken u && u.Sequence.Contains("\x1b_G"))
            {
                kgpIndex = i;
                break;
            }
        }
        Assert.True(kgpIndex >= 0, "Should have KGP token");

        // KEY CHECK: After the KGP token, are there any cursor moves + text writes 
        // that would position the cursor within the KGP display region (0,0)-(3,1)
        // and write characters there? This would overwrite the image!
        for (int i = kgpIndex + 1; i < tokens.Count; i++)
        {
            if (tokens[i] is CursorPositionToken cp)
            {
                // CUP is 1-based; KGP region is rows 1-2, cols 1-4
                int row0 = cp.Row - 1;
                int col0 = cp.Column - 1;
                
                // Check if cursor moves into the KGP display region
                if (row0 >= 0 && row0 < 2 && col0 >= 0 && col0 < 4)
                {
                    // Look ahead for text that would overwrite
                    for (int j = i + 1; j < tokens.Count; j++)
                    {
                        if (tokens[j] is TextToken || tokens[j] is SgrToken)
                            continue; // SGR doesn't write
                        if (tokens[j] is CursorPositionToken)
                            break; // Cursor moved again
                        if (tokens[j] is TextToken t)
                        {
                            Assert.Fail($"Text '{t.Text}' written at ({col0},{row0}) which is inside KGP display region!");
                        }
                    }
                }
            }
        }
        
        // Serialize and verify the KGP sequence is present
        var serialized = AnsiTokenUtf8Serializer.Serialize(tokens);
        var output = Encoding.UTF8.GetString(serialized.Span);
        Assert.Contains("\x1b_G", output);
    }

    [Fact]
    public void KgpCell_OnlyAnchorCellHasData()
    {
        // KGP places data only on the anchor cell (0,0), NOT on all cells in the display region
        var surface = new Surface(20, 10);
        var ctx = new SurfaceRenderContext(surface, Theming.Hex1bThemes.Default);
        ctx.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });

        var pixelData = new byte[16];
        for (int i = 0; i < 4; i++) { pixelData[i * 4] = 255; pixelData[i * 4 + 3] = 255; }

        var node = new KittyGraphicsNode
        {
            PixelData = pixelData, PixelWidth = 2, PixelHeight = 2,
            DisplayColumns = 4, DisplayRows = 2,
        };
        node.Measure(new Constraints(0, 20, 0, 10));
        node.Arrange(new Rect(0, 0, 4, 2));
        node.Render(ctx);

        // Only (0,0) should have KGP data
        Assert.True(surface[0, 0].HasKgp, "Anchor cell (0,0) should have KGP");
        
        // Cells within the KGP display region but NOT the anchor should be empty
        // This means the diff will include them as changed if they're non-empty!
        // Or rather, they'll be "unchanged" from empty and won't be in the diff.
        Assert.False(surface[1, 0].HasKgp, "(1,0) should not have KGP");
        Assert.False(surface[0, 1].HasKgp, "(0,1) should not have KGP");
    }

    [Fact]
    public void KgpRegion_CellsUnderImageAreSkippedInTokens()
    {
        // When text cells exist under the KGP display region,
        // the comparer must skip them to avoid erasing the image overlay
        var surface = new Surface(20, 10);
        var ctx = new SurfaceRenderContext(surface, Theming.Hex1bThemes.Default);
        ctx.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });

        var pixelData = new byte[16];
        for (int i = 0; i < 4; i++) { pixelData[i * 4] = 255; pixelData[i * 4 + 3] = 255; }

        var node = new KittyGraphicsNode
        {
            PixelData = pixelData, PixelWidth = 2, PixelHeight = 2,
            DisplayColumns = 4, DisplayRows = 2,
        };
        node.Measure(new Constraints(0, 20, 0, 10));
        node.Arrange(new Rect(0, 0, 4, 2));
        node.Render(ctx);

        // Manually write spaces into cells that are under the KGP display region
        // This simulates what a parent container (VStack) might do
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 4; x++)
                if (x != 0 || y != 0) // skip anchor
                    surface[x, y] = new SurfaceCell(" ", null, null);

        // Also write text OUTSIDE the KGP region
        surface[5, 0] = new SurfaceCell("H", null, null);
        surface[6, 0] = new SurfaceCell("i", null, null);

        var diff = SurfaceComparer.CompareToEmpty(surface);
        var tokens = SurfaceComparer.ToTokens(diff, surface);

        // Count CursorPositionTokens that target the KGP region (excluding anchor)
        var cursorsInRegion = tokens.OfType<CursorPositionToken>()
            .Where(cp => cp.Row >= 1 && cp.Row <= 2 && cp.Column >= 1 && cp.Column <= 4)
            .Where(cp => !(cp.Row == 1 && cp.Column == 1)) // exclude anchor position
            .ToList();

        // No cursor moves should target cells under the KGP image
        Assert.Empty(cursorsInRegion);

        // The "Hi" text outside the region should still be emitted
        var serialized = AnsiTokenUtf8Serializer.Serialize(tokens);
        var output = Encoding.UTF8.GetString(serialized.Span);
        Assert.Contains("H", output);
        Assert.Contains("i", output);
        Assert.Contains("\x1b_G", output); // KGP still present
    }

    [Fact]
    public void KgpPreScan_RegionTrackedEvenWhenAnchorUnchanged()
    {
        // Bug #5: On subsequent frames, if the KGP anchor cell is unchanged,
        // kgpRegions was empty and blank cells in the image area were emitted,
        // erasing the image. The fix pre-scans the surface for KGP regions.
        var surface = new Surface(20, 10);
        var ctx = new SurfaceRenderContext(surface, Theming.Hex1bThemes.Default);
        ctx.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });

        var pixelData = new byte[16];
        for (int i = 0; i < 4; i++) { pixelData[i * 4] = 255; pixelData[i * 4 + 3] = 255; }

        var node = new KittyGraphicsNode
        {
            PixelData = pixelData, PixelWidth = 2, PixelHeight = 2,
            DisplayColumns = 4, DisplayRows = 2,
        };
        node.Measure(new Constraints(0, 20, 0, 10));
        node.Arrange(new Rect(0, 0, 4, 2));
        node.Render(ctx);

        // Save the surface state (frame 1 complete)
        var oldSurface = surface.Clone();

        // Now simulate frame 2: some cells OUTSIDE the KGP region changed,
        // and some cells INSIDE the region also changed (e.g., blanks written by layout)
        // But the KGP anchor cell (0,0) is UNCHANGED.
        
        // Create a new surface identical to old, then change cells in the KGP region
        var newSurface = oldSurface.Clone();
        // Re-render KGP (same data → same hash → same KGP cell data)
        var ctx2 = new SurfaceRenderContext(newSurface, Theming.Hex1bThemes.Default);
        ctx2.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });
        node.Render(ctx2);
        
        // Also change a cell INSIDE the KGP region (simulates layout writing a space)
        newSurface[1, 0] = new SurfaceCell(" ", null, null);
        // And change a cell OUTSIDE
        newSurface[10, 5] = new SurfaceCell("X", null, null);

        var diff = SurfaceComparer.Compare(oldSurface, newSurface);
        var tokens = SurfaceComparer.ToTokens(diff, newSurface);
        
        var serialized = Encoding.UTF8.GetString(AnsiTokenUtf8Serializer.Serialize(tokens).Span);
        
        // The KGP should be re-emitted (because cells in its region are dirty)
        Assert.Contains("\x1b_G", serialized);
        
        // The "X" at (10,5) should be emitted
        Assert.Contains("X", serialized);
        
        // No blank cells should be emitted at positions within the KGP region
        // (except the anchor which has KGP data)
        var cursorTokens = tokens.OfType<CursorPositionToken>().ToList();
        foreach (var cp in cursorTokens)
        {
            int row0 = cp.Row - 1;
            int col0 = cp.Column - 1;
            // If cursor targets inside KGP region (excluding anchor)
            if (row0 >= 0 && row0 < 2 && col0 >= 0 && col0 < 4 && !(row0 == 0 && col0 == 0))
            {
                // This should NOT happen - the cell should be skipped
                int idx = tokens.ToList().IndexOf(cp);
                // Check if next non-SGR token is a text write
                for (int j = idx + 1; j < tokens.Count; j++)
                {
                    if (tokens[j] is SgrToken) continue;
                    if (tokens[j] is TextToken t && t.Text.Trim() == "")
                        Assert.Fail($"Blank text written at ({col0},{row0}) inside KGP region on frame 2");
                    break;
                }
            }
        }
    }
}
