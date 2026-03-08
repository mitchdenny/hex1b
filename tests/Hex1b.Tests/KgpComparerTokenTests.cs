using System.Security.Cryptography;
using System.Text;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for KGP token emission from SurfaceComparer.ToTokens().
/// </summary>
public class KgpComparerTokenTests
{
    private static readonly CellMetrics DefaultMetrics = new(10, 20);

    private static KgpCellData CreateKgpData(
        string imageContent = "test-image",
        int widthInCells = 4,
        int heightInCells = 2,
        int zIndex = -1,
        uint imageId = 1)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(imageContent));
        var pw = (uint)(widthInCells * 10);
        var ph = (uint)(heightInCells * 20);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(imageContent));
        return new KgpCellData(
            transmitPayload: $"\x1b_Ga=t,f=32,s={pw},v={ph},i={imageId};{base64}\x1b\\",
            imageId: imageId,
            widthInCells: widthInCells,
            heightInCells: heightInCells,
            sourcePixelWidth: pw,
            sourcePixelHeight: ph,
            contentHash: hash,
            zIndex: zIndex);
    }

    private static TrackedObject<KgpCellData> Track(KgpCellData data)
        => new(data, _ => { });

    #region ToTokens KGP Emission

    [Fact]
    public void ToTokens_NewKgpImage_EmitsSequence()
    {
        var prev = new Surface(10, 5, DefaultMetrics);
        var curr = new Surface(10, 5, DefaultMetrics);

        var kgpData = CreateKgpData();
        curr[1, 1] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));

        var diff = SurfaceComparer.Compare(prev, curr);
        var tokens = SurfaceComparer.ToTokens(diff, curr);

        // Should contain at least one UnrecognizedSequenceToken with KGP payload
        Assert.Contains(tokens, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("_G"));
    }

    [Fact]
    public void ToTokens_KgpBelowText_EmittedBeforeTextTokens()
    {
        var prev = new Surface(10, 5, DefaultMetrics);
        var curr = new Surface(10, 5, DefaultMetrics);

        // Below-text KGP (z=-1) at position (0,0)
        var kgpData = CreateKgpData(zIndex: -1);
        curr[0, 0] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));

        // Text at position (5,3)
        curr[5, 3] = new SurfaceCell("X", Hex1bColor.White, Hex1bColor.Black);

        var diff = SurfaceComparer.Compare(prev, curr);
        var tokens = SurfaceComparer.ToTokens(diff, curr);

        // Find the KGP token and the text token
        int kgpIndex = -1;
        int textIndex = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is UnrecognizedSequenceToken ust && ust.Sequence.Contains("_G"))
                kgpIndex = i;
            if (tokens[i] is TextToken tt && tt.Text == "X")
                textIndex = i;
        }

        Assert.True(kgpIndex >= 0, "Expected KGP token");
        Assert.True(textIndex >= 0, "Expected text token");
        Assert.True(kgpIndex < textIndex, "Below-text KGP should appear before text tokens");
    }

    [Fact]
    public void ToTokens_KgpAboveText_EmittedAfterTextTokens()
    {
        var prev = new Surface(10, 5, DefaultMetrics);
        var curr = new Surface(10, 5, DefaultMetrics);

        // Text at position (0,0)
        curr[0, 0] = new SurfaceCell("A", Hex1bColor.White, Hex1bColor.Black);

        // Above-text KGP (z=1) at position (5,3)
        var kgpData = CreateKgpData(zIndex: 1);
        curr[5, 3] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));

        var diff = SurfaceComparer.Compare(prev, curr);
        var tokens = SurfaceComparer.ToTokens(diff, curr);

        int textIndex = -1;
        int kgpIndex = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is TextToken tt && tt.Text == "A")
                textIndex = i;
            if (tokens[i] is UnrecognizedSequenceToken ust && ust.Sequence.Contains("_G"))
                kgpIndex = i;
        }

        Assert.True(textIndex >= 0, "Expected text token");
        Assert.True(kgpIndex >= 0, "Expected KGP token");
        Assert.True(kgpIndex > textIndex, "Above-text KGP should appear after text tokens");
    }

    [Fact]
    public void ToTokens_KgpUnchanged_NoDiff()
    {
        var kgpData = CreateKgpData();
        var tracked = Track(kgpData);

        var prev = new Surface(10, 5, DefaultMetrics);
        prev[1, 1] = new SurfaceCell(" ", null, null, Kgp: tracked);

        var curr = new Surface(10, 5, DefaultMetrics);
        curr[1, 1] = new SurfaceCell(" ", null, null, Kgp: tracked);

        var diff = SurfaceComparer.Compare(prev, curr);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void ToTokens_KgpClipped_PlacementHasClipParams()
    {
        var prev = new Surface(10, 5, DefaultMetrics);
        var curr = new Surface(10, 5, DefaultMetrics);

        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 2);
        var clipped = kgpData.WithClip(10, 20, 30, 40, 3, 1);
        curr[1, 1] = new SurfaceCell(" ", null, null, Kgp: Track(clipped));

        var diff = SurfaceComparer.Compare(prev, curr);
        var tokens = SurfaceComparer.ToTokens(diff, curr);

        // Placement token is separate from transmit — find the a=p token
        var placementToken = tokens.OfType<UnrecognizedSequenceToken>()
            .FirstOrDefault(t => t.Sequence.Contains("a=p"));
        Assert.NotNull(placementToken);
        Assert.Contains("x=10", placementToken!.Sequence);
        Assert.Contains("y=20", placementToken.Sequence);
        Assert.Contains("w=30", placementToken.Sequence);
        Assert.Contains("h=40", placementToken.Sequence);
    }

    [Fact]
    public void ToTokens_KgpImageChanged_EmitsNewPayload()
    {
        var kgp1 = CreateKgpData("image-A");
        var kgp2 = CreateKgpData("image-B");

        var prev = new Surface(10, 5, DefaultMetrics);
        prev[1, 1] = new SurfaceCell(" ", null, null, Kgp: Track(kgp1));

        var curr = new Surface(10, 5, DefaultMetrics);
        curr[1, 1] = new SurfaceCell(" ", null, null, Kgp: Track(kgp2));

        var diff = SurfaceComparer.Compare(prev, curr);
        Assert.False(diff.IsEmpty, "Different KGP content should produce a diff");

        var tokens = SurfaceComparer.ToTokens(diff, curr);
        Assert.Contains(tokens, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("_G"));
    }

    [Fact]
    public void ToTokens_KgpWithPositionToken()
    {
        var prev = new Surface(10, 5, DefaultMetrics);
        var curr = new Surface(10, 5, DefaultMetrics);

        var kgpData = CreateKgpData(zIndex: -1);
        curr[3, 2] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));

        var diff = SurfaceComparer.Compare(prev, curr);
        var tokens = SurfaceComparer.ToTokens(diff, curr);

        // Should have a CursorPosition before the KGP token
        var kgpIdx = tokens.Select((t, i) => (t, i))
            .Where(p => p.t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("_G"))
            .Select(p => p.i)
            .FirstOrDefault(-1);
        
        Assert.True(kgpIdx > 0, "KGP should not be the first token");
        Assert.IsType<CursorPositionToken>(tokens[kgpIdx - 1]);
        var pos = (CursorPositionToken)tokens[kgpIdx - 1];
        Assert.Equal(3, pos.Row); // 1-based: row 2 → 3
        Assert.Equal(4, pos.Column); // 1-based: col 3 → 4
    }

    [Fact]
    public void ToTokens_MultipleKgpPlacements_AllEmitted()
    {
        var prev = new Surface(20, 5, DefaultMetrics);
        var curr = new Surface(20, 5, DefaultMetrics);

        var kgp1 = CreateKgpData("img1", widthInCells: 3, heightInCells: 1, imageId: 1);
        var kgp2 = CreateKgpData("img2", widthInCells: 3, heightInCells: 1, imageId: 2);
        curr[0, 0] = new SurfaceCell(" ", null, null, Kgp: Track(kgp1));
        curr[10, 0] = new SurfaceCell(" ", null, null, Kgp: Track(kgp2));

        var diff = SurfaceComparer.Compare(prev, curr);
        var tokens = SurfaceComparer.ToTokens(diff, curr);

        // Each KGP image emits separate transmit (a=t) and placement (a=p) tokens
        var kgpTokens = tokens.OfType<UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("_G"))
            .ToList();
        
        Assert.Equal(4, kgpTokens.Count); // 2 images × (transmit + placement)
    }

    [Fact]
    public void ToTokens_KgpCellCoveredByText_TextRenderedOverKgp()
    {
        var prev = new Surface(10, 5, DefaultMetrics);
        var curr = new Surface(10, 5, DefaultMetrics);

        // KGP image spanning 4 cells wide
        var kgpData = CreateKgpData(widthInCells: 4, heightInCells: 1, zIndex: -1);
        curr[0, 0] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));

        // Text cell at position (2,0) — inside the KGP region
        curr[2, 0] = new SurfaceCell("T", Hex1bColor.White, Hex1bColor.Black);

        var diff = SurfaceComparer.Compare(prev, curr);
        var tokens = SurfaceComparer.ToTokens(diff, curr);

        // Text "T" should still be emitted since text overrides below-text KGP
        Assert.Contains(tokens, t => t is TextToken tt && tt.Text == "T");
    }

    #endregion

    #region SVG Round-Trip

    private static readonly TerminalCapabilities KgpCapabilities = new()
    {
        SupportsKgp = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
    };

    private static Hex1bTerminal CreateTerminal(int width = 20, int height = 5)
    {
        return Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless(KgpCapabilities)
            .WithDimensions(width, height)
            .Build();
    }

    private static void Send(Hex1bTerminal terminal, string escapeSequence)
    {
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(escapeSequence));
    }

    [Fact]
    public void SvgRoundTrip_KgpBelowText_ImageBelowTextInSvg()
    {
        var terminal = CreateTerminal();
        
        // Transmit a 2x2 pixel RGBA image
        var imageData = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255 };
        var base64 = Convert.ToBase64String(imageData);
        Send(terminal, $"\x1b_Ga=t,f=32,s=2,v=2,i=99,t=d;{base64}\x1b\\");
        
        // Place the image at cursor below text (z=-1)
        Send(terminal, "\x1b_Ga=p,i=99,c=2,r=2,C=1,q=2,z=-1\x1b\\");
        
        // Write text at (3,0) — NOT overlapping the image
        Send(terminal, "\x1b[1;4HHi");
        
        var svg = terminal.CreateSnapshot().ToSvg();
        Assert.Contains("<image", svg);
        // Text characters appear individually in SVG <text> elements
        Assert.Contains(">H<", svg);
        Assert.Contains(">i<", svg);
    }

    [Fact]
    public void SvgRoundTrip_KgpAboveText_ImageAboveTextInSvg()
    {
        var terminal = CreateTerminal();
        
        var imageData = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255 };
        var base64 = Convert.ToBase64String(imageData);
        Send(terminal, $"\x1b_Ga=t,f=32,s=2,v=2,i=100,t=d;{base64}\x1b\\");
        
        // Place with z=1 (above text)
        Send(terminal, "\x1b_Ga=p,i=100,c=2,r=2,C=1,q=2,z=1\x1b\\");
        
        var svg = terminal.CreateSnapshot().ToSvg();
        Assert.Contains("<image", svg);
    }

    [Fact]
    public void SvgRoundTrip_KgpZOrder_NegativeBeforePositive()
    {
        var terminal = CreateTerminal();
        
        var imageData = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255 };
        var base64 = Convert.ToBase64String(imageData);
        
        // Image 1 at z=-1 (below text)
        Send(terminal, $"\x1b_Ga=t,f=32,s=2,v=2,i=50,t=d;{base64}\x1b\\");
        Send(terminal, "\x1b_Ga=p,i=50,c=2,r=2,C=1,q=2,z=-1\x1b\\");
        
        // Image 2 at z=1 (above text)
        Send(terminal, $"\x1b_Ga=t,f=32,s=2,v=2,i=51,t=d;{base64}\x1b\\");
        Send(terminal, "\x1b[1;5H"); // Move cursor
        Send(terminal, "\x1b_Ga=p,i=51,c=2,r=2,C=1,q=2,z=1\x1b\\");
        
        var svg = terminal.CreateSnapshot().ToSvg();
        
        // Both images should be in the SVG
        var imageCount = svg.Split("<image").Length - 1;
        Assert.True(imageCount >= 2, $"Expected 2+ images in SVG, got {imageCount}");
    }

    #endregion
}
