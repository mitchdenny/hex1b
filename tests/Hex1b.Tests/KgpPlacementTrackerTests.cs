using System.Security.Cryptography;
using System.Text;
using Hex1b.Kgp;
using Hex1b.Surfaces;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="KgpPlacementTracker"/> lifecycle management.
/// </summary>
public class KgpPlacementTrackerTests
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
            transmitPayload: $"\x1b_Ga=t,f=32,s={pw},v={ph},i={imageId},t=d,q=2;{base64}\x1b\\",
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

    private static Surface CreateSurfaceWithKgp(uint imageId, int x, int y, int zIndex = -1)
    {
        var surface = new Surface(30, 15, DefaultMetrics);
        var kgpData = CreateKgpData(imageId: imageId, zIndex: zIndex);
        surface[x, y] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));
        return surface;
    }

    [Fact]
    public void FirstFrame_EmitsTransmitAndPlacement()
    {
        var tracker = new KgpPlacementTracker();
        var surface = CreateSurfaceWithKgp(imageId: 42, x: 3, y: 2);

        var (before, after) = tracker.GenerateCommands(surface);

        // Below-text (z=-1) should be in beforeText
        Assert.NotEmpty(before);
        Assert.Empty(after);

        // Should have: cursor + transmit + placement
        Assert.Contains(before, t => t is CursorPositionToken cp && cp.Row == 3 && cp.Column == 4);
        Assert.Contains(before, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=t"));
        Assert.Contains(before, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=p"));
    }

    [Fact]
    public void SecondFrame_SamePosition_EmitsNothing()
    {
        var tracker = new KgpPlacementTracker();
        var surface = CreateSurfaceWithKgp(imageId: 42, x: 3, y: 2);

        // Frame 1
        tracker.GenerateCommands(surface);

        // Frame 2 — same position
        var (before, after) = tracker.GenerateCommands(surface);

        // Nothing changed — no tokens needed
        Assert.Empty(before);
        Assert.Empty(after);
    }

    [Fact]
    public void SecondFrame_TransmitOnce_PlaceOnly()
    {
        var tracker = new KgpPlacementTracker();
        var surface1 = CreateSurfaceWithKgp(imageId: 42, x: 3, y: 2);

        // Frame 1 — first appearance
        var (before1, _) = tracker.GenerateCommands(surface1);
        Assert.Contains(before1, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=t"));

        // Frame 2 — moved to new position
        var surface2 = CreateSurfaceWithKgp(imageId: 42, x: 6, y: 2);
        var (before2, _) = tracker.GenerateCommands(surface2);

        // Should have delete + placement but NO transmit (image already transmitted)
        Assert.Contains(before2, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=d"));
        Assert.Contains(before2, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=p"));
        Assert.DoesNotContain(before2, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=t"));
    }

    [Fact]
    public void Move_EmitsDeleteAndPlacement()
    {
        var tracker = new KgpPlacementTracker();

        // Frame 1 at (3, 2)
        tracker.GenerateCommands(CreateSurfaceWithKgp(imageId: 42, x: 3, y: 2));

        // Frame 2 at (6, 2) — moved right
        var (before, _) = tracker.GenerateCommands(CreateSurfaceWithKgp(imageId: 42, x: 6, y: 2));

        // Should have delete for old position
        var deleteToken = before.OfType<UnrecognizedSequenceToken>()
            .First(t => t.Sequence.Contains("a=d"));
        Assert.Contains("i=42", deleteToken.Sequence);

        // Should have cursor + placement for new position (row=3, col=7 in 1-based)
        Assert.Contains(before, t => t is CursorPositionToken cp && cp.Row == 3 && cp.Column == 7);
        Assert.Contains(before, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=p"));
    }

    [Fact]
    public void Removal_EmitsDeleteOnly()
    {
        var tracker = new KgpPlacementTracker();

        // Frame 1 — KGP exists
        tracker.GenerateCommands(CreateSurfaceWithKgp(imageId: 42, x: 3, y: 2));

        // Frame 2 — KGP removed (empty surface)
        var emptySurface = new Surface(30, 15, DefaultMetrics);
        var (before, _) = tracker.GenerateCommands(emptySurface);

        // Should have delete, no placement
        Assert.Contains(before, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=d"));
        Assert.DoesNotContain(before, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=p"));
    }

    [Fact]
    public void AboveText_EmitsInAfterTextList()
    {
        var tracker = new KgpPlacementTracker();
        var surface = CreateSurfaceWithKgp(imageId: 42, x: 3, y: 2, zIndex: 1);

        var (before, after) = tracker.GenerateCommands(surface);

        // Above-text (z=1) should be in afterText
        Assert.Empty(before);
        Assert.NotEmpty(after);
        Assert.Contains(after, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=p"));
    }

    [Fact]
    public void Reset_ForcesRetransmission()
    {
        var tracker = new KgpPlacementTracker();
        var surface = CreateSurfaceWithKgp(imageId: 42, x: 3, y: 2);

        // Frame 1 — transmit + place
        tracker.GenerateCommands(surface);

        // Reset (simulates resize)
        tracker.Reset();

        // Frame 2 — should re-transmit because reset cleared transmitted images
        var (before, _) = tracker.GenerateCommands(surface);
        Assert.Contains(before, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=t"));
    }

    [Fact]
    public void MultipleImages_TrackedIndependently()
    {
        var tracker = new KgpPlacementTracker();

        // Frame 1: two images
        var surface1 = new Surface(30, 15, DefaultMetrics);
        surface1[3, 2] = new SurfaceCell(" ", null, null, Kgp: Track(CreateKgpData("img1", imageId: 10)));
        surface1[15, 5] = new SurfaceCell(" ", null, null, Kgp: Track(CreateKgpData("img2", imageId: 20)));
        tracker.GenerateCommands(surface1);

        // Frame 2: image 10 moved, image 20 unchanged
        var surface2 = new Surface(30, 15, DefaultMetrics);
        surface2[6, 2] = new SurfaceCell(" ", null, null, Kgp: Track(CreateKgpData("img1", imageId: 10)));
        surface2[15, 5] = new SurfaceCell(" ", null, null, Kgp: Track(CreateKgpData("img2", imageId: 20)));
        var (before, _) = tracker.GenerateCommands(surface2);

        // Should only have delete+place for image 10, nothing for image 20
        var deleteTokens = before.OfType<UnrecognizedSequenceToken>()
            .Where(t => t.Sequence.Contains("a=d")).ToList();
        Assert.Single(deleteTokens);
        Assert.Contains("i=10", deleteTokens[0].Sequence);

        // Placement for image 10 at new position
        Assert.Contains(before, t => t is CursorPositionToken cp && cp.Row == 3 && cp.Column == 7);
    }

    [Fact]
    public void ActivePlacementCount_TracksCorrectly()
    {
        var tracker = new KgpPlacementTracker();

        Assert.Equal(0, tracker.ActivePlacementCount);

        tracker.GenerateCommands(CreateSurfaceWithKgp(imageId: 42, x: 3, y: 2));
        Assert.Equal(1, tracker.ActivePlacementCount);

        tracker.GenerateCommands(new Surface(30, 15, DefaultMetrics));
        Assert.Equal(0, tracker.ActivePlacementCount);
    }

    [Fact]
    public void RoundTrip_MoveSequence_TerminalDisplaysCorrectly()
    {
        // Full round-trip: Surface → KgpPlacementTracker → serialize → Hex1bTerminal
        var imageData = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255 };
        var contentHash = SHA256.HashData(imageData);
        var imageId = (uint)(contentHash[0] << 24 | contentHash[1] << 16 | contentHash[2] << 8 | contentHash[3]);
        var base64 = Convert.ToBase64String(imageData);
        var transmitPayload = $"\x1b_Ga=t,f=32,s=2,v=2,i={imageId},t=d,q=2;{base64}\x1b\\";
        var kgpData = new KgpCellData(transmitPayload, imageId, 3, 2, 2, 2, contentHash, zIndex: -1);

        var tracker = new KgpPlacementTracker();

        var caps = new TerminalCapabilities { SupportsKgp = true, SupportsTrueColor = true };
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithHeadless(caps)
            .WithDimensions(20, 10)
            .Build();

        // Frame 1: place at (2, 1)
        var s1 = new Surface(20, 10, DefaultMetrics);
        s1[2, 1] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));
        var (before1, after1) = tracker.GenerateCommands(s1);
        var tokens1 = new List<AnsiToken>();
        tokens1.AddRange(before1);
        tokens1.AddRange(after1);
        var bytes1 = AnsiTokenUtf8Serializer.Serialize(tokens1);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(Encoding.UTF8.GetString(bytes1.ToArray())));

        var svg1 = terminal.CreateSnapshot().ToSvg();
        Assert.Contains("<image", svg1);

        // Frame 2: move to (5, 1) — tracker should emit delete + place (no transmit)
        var s2 = new Surface(20, 10, DefaultMetrics);
        s2[5, 1] = new SurfaceCell(" ", null, null, Kgp: Track(kgpData));
        var (before2, after2) = tracker.GenerateCommands(s2);
        var tokens2 = new List<AnsiToken>();
        tokens2.AddRange(before2);
        tokens2.AddRange(after2);

        // Verify: no transmit on second frame (image already sent)
        Assert.DoesNotContain(tokens2, t => t is UnrecognizedSequenceToken ust && ust.Sequence.Contains("a=t"));

        var bytes2 = AnsiTokenUtf8Serializer.Serialize(tokens2);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(Encoding.UTF8.GetString(bytes2.ToArray())));

        var svg2 = terminal.CreateSnapshot().ToSvg();
        Assert.Contains("<image", svg2); // Image should still be visible after move
    }
}
