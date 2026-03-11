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

// Temporary test class for KGP move scenario
public class KgpMoveTests
{
    private static readonly CellMetrics DefaultMetrics = new(10, 20);

    private static KgpCellData CreateKgpData(
        string imageContent = "test-image",
        int widthInCells = 4,
        int heightInCells = 2,
        int zIndex = -1,
        uint imageId = 1)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(imageContent));
        var pw = (uint)(widthInCells * 10);
        var ph = (uint)(heightInCells * 20);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(imageContent));
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

    [Fact]
    public void ToTokens_KgpMoved_EmitsDeleteAndNewPlacement()
    {
        // Frame N: KGP at (2, 1)
        var prev = new Surface(20, 10, DefaultMetrics);
        var kgpData = CreateKgpData(imageId: 42);
        prev[2, 1] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));
        // Fill remaining cells
        for (var dy = 0; dy < 2; dy++)
            for (var dx = 0; dx < 4; dx++)
                if (dx != 0 || dy != 0)
                    prev[2 + dx, 1 + dy] = new SurfaceCell(" ", null, null);

        // Frame N+1: KGP at (5, 1) — moved right by 3 cells
        var curr = new Surface(20, 10, DefaultMetrics);
        var kgpData2 = CreateKgpData(imageId: 42); // Same image, same ID
        curr[5, 1] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData2));
        for (var dy = 0; dy < 2; dy++)
            for (var dx = 0; dx < 4; dx++)
                if (dx != 0 || dy != 0)
                    curr[5 + dx, 1 + dy] = new SurfaceCell(" ", null, null);

        var diff = SurfaceComparer.Compare(prev, curr);
        Assert.False(diff.IsEmpty, "Moving KGP should produce a diff");

        var tokens = SurfaceComparer.ToTokens(diff, curr, prev);

        // Diagnostic output
        Console.WriteLine($"Total tokens: {tokens.Count}");
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t is UnrecognizedSequenceToken ust)
                Console.WriteLine($"  [{i}] UnrecognizedSequence: {ust.Sequence.Replace("\x1b", "ESC")}");
            else if (t is CursorPositionToken cpt)
                Console.WriteLine($"  [{i}] CursorPosition: row={cpt.Row}, col={cpt.Column}");
            else if (t is TextToken tt)
                Console.WriteLine($"  [{i}] Text: '{tt.Text}'");
            else
                Console.WriteLine($"  [{i}] {t.GetType().Name}");
        }

        // 1. Should have a delete command for the old position
        var deleteTokens = tokens.OfType<UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("a=d"))
            .ToList();
        Assert.True(deleteTokens.Count > 0, "Expected KGP delete command for moved image");

        // 2. Should have a placement at the NEW position (row=2, col=6 in 1-based)
        var placementTokens = tokens.OfType<UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("a=p"))
            .ToList();
        Assert.True(placementTokens.Count > 0, "Expected KGP placement at new position");

        // 3. Should have a cursor position token before the placement pointing to (5,1) = row 2, col 6
        var cursorBeforePlacement = false;
        for (int i = 0; i < tokens.Count - 1; i++)
        {
            if (tokens[i] is CursorPositionToken cp && cp.Row == 2 && cp.Column == 6)
            {
                // Check if a KGP token follows within the next few tokens
                if (i + 1 < tokens.Count && tokens[i + 1] is UnrecognizedSequenceToken ust2 && ust2.Sequence.Contains("_G"))
                {
                    cursorBeforePlacement = true;
                }
            }
        }
        Assert.True(cursorBeforePlacement, "Expected cursor position (2, 6) before KGP placement tokens");

        // 4. Verify the transmit token has the correct image ID
        var transmitTokens = tokens.OfType<UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("a=t"))
            .ToList();
        Assert.True(transmitTokens.Count > 0, "Expected transmit token for image re-transmission");
    }

    [Fact]
    public void Composite_KgpSurvivesCompositingAtDifferentOffsets()
    {
        // Simulate window drag: child surface has KGP, composited at different parent offsets

        // Child surface (window content) with KGP at (0, 0)
        var child = new Surface(10, 5, DefaultMetrics);
        var kgpData = CreateKgpData(imageId: 42);
        child[0, 0] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));

        // Frame N: composite child at offset (5, 3) in parent
        var parentA = new Surface(30, 15, DefaultMetrics);
        parentA.Composite(child, 5, 3);

        // Verify KGP landed at (5, 3) in parent
        Assert.True(parentA[5, 3].HasKgp, "KGP should be at (5,3) in parentA after compositing");
        Assert.Equal(kgpData.ImageId, parentA[5, 3].Kgp!.Data.ImageId);

        // Frame N+1: composite same child at offset (8, 3) — window moved right
        var parentB = new Surface(30, 15, DefaultMetrics);
        parentB.Composite(child, 8, 3);

        // Verify KGP landed at (8, 3) in parent
        Assert.True(parentB[8, 3].HasKgp, "KGP should be at (8,3) in parentB after compositing");
        Assert.Equal(kgpData.ImageId, parentB[8, 3].Kgp!.Data.ImageId);

        // Compare and verify tokens
        var diff = SurfaceComparer.Compare(parentA, parentB);
        Assert.False(diff.IsEmpty, "Moving KGP via compositing should produce a diff");

        var tokens = SurfaceComparer.ToTokens(diff, parentB, parentA);

        // Should have delete for old position + placement at new position
        var deleteTokens = tokens.OfType<UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("a=d")).ToList();
        Assert.True(deleteTokens.Count > 0, "Expected KGP delete for moved image");

        var placementTokens = tokens.OfType<UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("a=p")).ToList();
        Assert.True(placementTokens.Count > 0, "Expected KGP placement at new position");

        // Cursor should point to new position (8, 3) = row 4, col 9 (1-based)
        var cursorTokens = tokens.OfType<CursorPositionToken>()
            .Where(t => t.Row == 4 && t.Column == 9).ToList();
        Assert.True(cursorTokens.Count > 0, "Expected cursor position (4, 9) for new KGP location");
    }

    [Fact]
    public void Composite_KgpSurvivesNestedCompositing()
    {
        // Simulate: KGP in window content → window surface → WindowPanel surface → root surface

        // Level 1: KGP image at (1, 1) in window content surface
        var windowContent = new Surface(15, 8, DefaultMetrics);
        var kgpData = CreateKgpData(imageId: 99);
        windowContent[1, 1] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));

        // Level 2: window composited at (3, 2) into WindowPanel surface
        var windowPanel = new Surface(40, 20, DefaultMetrics);
        windowPanel.Composite(windowContent, 3, 2);

        // Verify KGP at (3+1, 2+1) = (4, 3)
        Assert.True(windowPanel[4, 3].HasKgp, "KGP should be at (4,3) after first compositing");

        // Level 3: WindowPanel composited at (0, 1) into root surface
        var root = new Surface(40, 20, DefaultMetrics);
        root.Composite(windowPanel, 0, 1);

        // Verify KGP at (0+4, 1+3) = (4, 4)
        Assert.True(root[4, 4].HasKgp, "KGP should be at (4,4) after nested compositing");
        Assert.NotNull(root[4, 4].Kgp?.Data.TransmitPayload);
        Assert.Equal(kgpData.ImageId, root[4, 4].Kgp!.Data.ImageId);

        // Verify the transmit payload survived all compositing steps
        var rootKgpData = root[4, 4].Kgp!.Data;
        var chunks = rootKgpData.BuildTransmitChunks();
        Assert.True(chunks.Count > 0, "TransmitPayload should survive compositing (needed for emission)");
    }

    /// <summary>
    /// Full round-trip test: Surface diff → tokens → serialize → Hex1bTerminal → verify placements.
    /// This exercises the exact bytes that would be sent to a real terminal during a window drag.
    /// </summary>
    [Fact]
    public void RoundTrip_KgpMoveSequence_TerminalSeesCorrectPlacements()
    {
        // Create a 4-pixel RGBA test image (2x2)
        var imageData = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255 };
        var contentHash = System.Security.Cryptography.SHA256.HashData(imageData);
        var imageId = (uint)(contentHash[0] << 24 | contentHash[1] << 16 | contentHash[2] << 8 | contentHash[3]);
        var base64 = Convert.ToBase64String(imageData);
        var transmitPayload = $"\x1b_Ga=t,f=32,s=2,v=2,i={imageId},t=d,q=2;{base64}\x1b\\";

        var kgpData1 = new KgpCellData(transmitPayload, imageId, 3, 2, 2, 2, contentHash, zIndex: -1);

        // --- Frame 1: KGP image at (2, 1) ---
        var empty = new Surface(20, 10, DefaultMetrics);
        var frame1 = new Surface(20, 10, DefaultMetrics);
        frame1[2, 1] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData1));

        var diff1 = SurfaceComparer.CompareToEmpty(frame1);
        var tokens1 = SurfaceComparer.ToTokens(diff1, frame1);
        var bytes1 = Hex1b.Tokens.AnsiTokenUtf8Serializer.Serialize(tokens1);

        // Create terminal and feed frame 1
        var caps = new TerminalCapabilities { SupportsKgp = true, SupportsTrueColor = true };
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless(caps)
            .WithDimensions(20, 10)
            .Build();

        var text1 = System.Text.Encoding.UTF8.GetString(bytes1.ToArray());
        terminal.ApplyTokens(Hex1b.Tokens.AnsiTokenizer.Tokenize(text1));

        // Verify frame 1: terminal should have the image placed
        var snapshot1 = terminal.CreateSnapshot();
        var svg1 = snapshot1.ToSvg();
        Assert.Contains("<image", svg1); // Image should exist in SVG

        // --- Frame 2: KGP image moved to (5, 1) ---
        var kgpData2 = new KgpCellData(transmitPayload, imageId, 3, 2, 2, 2, contentHash, zIndex: -1);
        var frame2 = new Surface(20, 10, DefaultMetrics);
        frame2[5, 1] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData2));

        var diff2 = SurfaceComparer.Compare(frame1, frame2);
        Assert.False(diff2.IsEmpty, "Moving KGP should produce a diff");

        var tokens2 = SurfaceComparer.ToTokens(diff2, frame2, frame1);
        var bytes2 = Hex1b.Tokens.AnsiTokenUtf8Serializer.Serialize(tokens2);

        // Verify we have delete, transmit, and placement tokens
        var hasDelete = tokens2.OfType<UnrecognizedSequenceToken>().Any(t => t.Sequence.Contains("a=d"));
        var hasTransmit = tokens2.OfType<UnrecognizedSequenceToken>().Any(t => t.Sequence.Contains("a=t"));
        var hasPlacement = tokens2.OfType<UnrecognizedSequenceToken>().Any(t => t.Sequence.Contains("a=p"));
        Assert.True(hasDelete, "Frame 2 should emit KGP delete");
        Assert.True(hasTransmit || hasPlacement, "Frame 2 should emit KGP transmit or placement");

        // Feed frame 2 to terminal
        var text2 = System.Text.Encoding.UTF8.GetString(bytes2.ToArray());
        terminal.ApplyTokens(Hex1b.Tokens.AnsiTokenizer.Tokenize(text2));

        // Verify frame 2: terminal should still have the image, now at new position
        var snapshot2 = terminal.CreateSnapshot();
        var svg2 = snapshot2.ToSvg();
        Assert.Contains("<image", svg2); // Image should still exist after move
    }
}
