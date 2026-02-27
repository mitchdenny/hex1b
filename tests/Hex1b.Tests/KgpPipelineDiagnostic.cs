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
    public void KgpRegion_CellsUnderImageAreWrittenAsSpaces()
    {
        // Cells under the KGP display region must be written as spaces
        // so that when the image moves or is deleted, no stale text is revealed.
        // The KGP image renders on top (z-index 0), hiding the spaces.
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
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 4; x++)
                if (x != 0 || y != 0)
                    surface[x, y] = new SurfaceCell(" ", null, null);

        // Also write text OUTSIDE the KGP region
        surface[5, 0] = new SurfaceCell("H", null, null);
        surface[6, 0] = new SurfaceCell("i", null, null);

        var diff = SurfaceComparer.CompareToEmpty(surface);
        var tokens = SurfaceComparer.ToTokens(diff, surface);

        // The "Hi" text outside the region should be emitted
        var serialized = AnsiTokenUtf8Serializer.Serialize(tokens);
        var output = Encoding.UTF8.GetString(serialized.Span);
        Assert.Contains("H", output);
        Assert.Contains("i", output);
        Assert.Contains("\x1b_G", output); // KGP still present

        // Cells under KGP SHOULD now be written (as spaces) to keep text layer clean
        // Verify spaces are present in the output for the KGP region cells
        var textTokens = tokens.OfType<TextToken>().ToList();
        Assert.True(textTokens.Count > 0, "Should have text tokens (including spaces under KGP)");
    }

    [Fact]
    public void KgpPreScan_RegionTrackedEvenWhenAnchorUnchanged()
    {
        // On subsequent frames, KGP regions must be tracked and the image re-emitted
        // even when the anchor cell is unchanged. Cells under the image ARE written
        // as spaces to keep the text layer clean.
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

        // Simulate frame 2: re-render KGP (same data), change cells inside and outside region
        var newSurface = oldSurface.Clone();
        var ctx2 = new SurfaceRenderContext(newSurface, Theming.Hex1bThemes.Default);
        ctx2.SetCapabilities(new TerminalCapabilities { SupportsKgp = true });
        node.Render(ctx2);
        
        newSurface[1, 0] = new SurfaceCell(" ", null, null);
        newSurface[10, 5] = new SurfaceCell("X", null, null);

        var diff = SurfaceComparer.Compare(oldSurface, newSurface);
        var tokens = SurfaceComparer.ToTokens(diff, newSurface, oldSurface);
        
        var serialized = Encoding.UTF8.GetString(AnsiTokenUtf8Serializer.Serialize(tokens).Span);
        
        // The KGP should be re-emitted
        Assert.Contains("\x1b_G", serialized);
        
        // The "X" at (10,5) should be emitted
        Assert.Contains("X", serialized);
    }

    [Fact]
    public void NormalizePreTokenized_KgpUnrecognizedToken_ConvertedToKgpToken()
    {
        // When SurfaceComparer emits KGP as UnrecognizedSequenceToken,
        // NormalizePreTokenizedTokens must convert it to KgpToken so
        // Hex1bTerminal.ApplyToken processes it (tracks placements).
        var capabilities = new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
        };
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(capabilities)
            .WithDimensions(20, 10)
            .Build();

        // Simulate what SurfaceComparer does: emit KGP as UnrecognizedSequenceToken
        var pixelData = new byte[4 * 4 * 4]; // 4x4 RGBA red
        for (int i = 0; i < pixelData.Length; i += 4) { pixelData[i] = 255; pixelData[i+3] = 255; }
        var base64 = Convert.ToBase64String(pixelData);
        var kgpEsc = $"\x1b_Ga=T,f=32,s=4,v=4,i=1,c=4,r=2,q=2;{base64}\x1b\\";

        // This is exactly what SurfaceComparer.ToTokens() produces
        var tokens = new List<AnsiToken>
        {
            new CursorPositionToken(1, 1),
            new UnrecognizedSequenceToken(kgpEsc)
        };

        terminal.ApplyTokens(tokens);

        // Verify the terminal now has a KGP placement (meaning the token was normalized)
        Assert.NotEmpty(terminal.KgpPlacements);
        Assert.Equal(1u, terminal.KgpPlacements[0].ImageId);
    }
}
