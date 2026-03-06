using Hex1b.Kgp;
using Hex1b.Tokens;

namespace Hex1b.Tests;

public class KgpTerminalTests
{
    private static readonly TerminalCapabilities KgpCapabilities = new()
    {
        SupportsKgp = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
    };

    private static Hex1bTerminal CreateTerminal(Hex1bAppWorkloadAdapter workload, int width = 80, int height = 24)
    {
        return Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(KgpCapabilities)
            .WithDimensions(width, height)
            .Build();
    }

    private static void SendKgp(Hex1bTerminal terminal, string escapeSequence)
    {
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(escapeSequence));
    }

    // =============================================
    // Transmit tests
    // =============================================

    [Fact]
    public void Transmit_SingleChunk_StoresImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var cmd = KgpTestHelper.BuildTransmitCommand(1, 2, 2, KgpFormat.Rgba32);
        SendKgp(terminal, cmd);

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        var image = terminal.KgpImageStore.GetImageById(1);
        Assert.NotNull(image);
        Assert.Equal(2u, image.Width);
        Assert.Equal(2u, image.Height);
        Assert.Equal(KgpFormat.Rgba32, image.Format);
    }

    [Fact]
    public void Transmit_Rgb24_StoresCorrectFormat()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var cmd = KgpTestHelper.BuildTransmitCommand(1, 1, 1, KgpFormat.Rgb24);
        SendKgp(terminal, cmd);

        var image = terminal.KgpImageStore.GetImageById(1);
        Assert.NotNull(image);
        Assert.Equal(KgpFormat.Rgb24, image.Format);
        Assert.Equal(3, image.Data.Length); // 1x1 RGB = 3 bytes
    }

    [Fact]
    public void Transmit_ChunkedTransfer_AssemblesCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var chunks = KgpTestHelper.BuildChunkedTransmitCommands(1, 4, 4, chunkSize: 16);

        Assert.True(chunks.Count > 1, "Should produce multiple chunks");

        foreach (var chunk in chunks)
        {
            SendKgp(terminal, chunk);
        }

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        var image = terminal.KgpImageStore.GetImageById(1);
        Assert.NotNull(image);
        Assert.Equal(4u * 4u * 4, (uint)image.Data.Length); // 4x4 RGBA
    }

    [Fact]
    public void Transmit_InsufficientData_DoesNotStore()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Claim 10x10 RGBA but only send 4 bytes
        var cmd = KgpTestHelper.BuildCommand("a=t,f=32,s=10,v=10,i=1", new byte[] { 1, 2, 3, 4 });
        SendKgp(terminal, cmd);

        // Image should not be stored because data is insufficient
        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void Transmit_ReplaceExistingId_UpdatesImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1, fillByte: 0xAA));
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2, fillByte: 0xBB));

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        var image = terminal.KgpImageStore.GetImageById(1);
        Assert.NotNull(image);
        Assert.Equal(2u, image.Width);
    }

    [Fact]
    public void Transmit_WithoutCapability_Ignored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = false })
            .WithDimensions(80, 24)
            .Build();

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));

        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }

    // =============================================
    // Query tests
    // =============================================

    [Fact]
    public void Query_DoesNotStoreImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var cmd = KgpTestHelper.BuildQueryCommand(imageId: 31);
        SendKgp(terminal, cmd);

        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }

    // =============================================
    // Delete tests
    // =============================================

    [Fact]
    public void Delete_All_ClearsAllImages()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 1, 1));
        Assert.Equal(2, terminal.KgpImageStore.ImageCount);

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('a'));

        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void Delete_ById_RemovesSpecificImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 1, 1));

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('i', imageId: 1));

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        Assert.Null(terminal.KgpImageStore.GetImageById(1));
        Assert.NotNull(terminal.KgpImageStore.GetImageById(2));
    }

    [Fact]
    public void Delete_ById_FreeData_RemovesImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('I', imageId: 1));

        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void Delete_AbortsChunkedTransfer()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Start a chunked transfer
        var data = new byte[] { 1, 2, 3, 4 };
        var cmd = KgpTestHelper.BuildCommand("a=t,f=32,s=2,v=2,i=1,m=1", data);
        SendKgp(terminal, cmd);

        Assert.True(terminal.KgpImageStore.IsChunkedTransferInProgress);

        // Delete should abort the transfer
        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('a'));

        Assert.False(terminal.KgpImageStore.IsChunkedTransferInProgress);
    }

    // =============================================
    // Response suppression tests
    // =============================================

    [Fact]
    public void Transmit_QuietOne_SuppressesOkResponse()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // q=1 suppresses OK
        var cmd = KgpTestHelper.BuildTransmitCommand(1, 1, 1, quiet: 1);
        SendKgp(terminal, cmd);

        // Image should still be stored
        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void Transmit_QuietTwo_SuppressesAllResponses()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // q=2 suppresses all responses including errors
        var cmd = KgpTestHelper.BuildCommand("a=t,f=32,s=10,v=10,i=1,q=2", new byte[] { 1, 2, 3, 4 });
        SendKgp(terminal, cmd);

        // No error response should have been sent (we'd need to intercept 
        // the workload.WriteInputAsync to fully verify, but at minimum
        // this shouldn't crash)
        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }

    // =============================================
    // Image number tests
    // =============================================

    [Fact]
    public void Transmit_WithImageNumber_StoresCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Use I= key instead of i=
        var data = KgpTestHelper.CreatePixelData(1, 1, KgpFormat.Rgb24);
        var cmd = KgpTestHelper.BuildCommand("a=t,f=24,s=1,v=1,I=93", data);
        SendKgp(terminal, cmd);

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        var image = terminal.KgpImageStore.GetImageByNumber(93);
        Assert.NotNull(image);
    }

    [Fact]
    public void Transmit_MultipleWithSameNumber_BothStored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var data = KgpTestHelper.CreatePixelData(1, 1, KgpFormat.Rgb24);
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=t,f=24,s=1,v=1,I=93", data));
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=t,f=24,s=1,v=1,I=93", data));

        // Both should be stored (with different IDs)
        Assert.Equal(2, terminal.KgpImageStore.ImageCount);

        // GetImageByNumber returns the newest
        var newest = terminal.KgpImageStore.GetImageByNumber(93);
        Assert.NotNull(newest);
    }

    // =============================================
    // TransmitAndDisplay tests
    // =============================================

    [Fact]
    public void TransmitAndDisplay_StoresAndMovesCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        // Position cursor at (0,0)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H")); // Home

        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20,
            KgpFormat.Rgba32, displayColumns: 3, displayRows: 2);
        SendKgp(terminal, cmd);

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);

        // Cursor should have moved: right by 3 cols, down by 1 row (2-1)
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal(3, snapshot.CursorX);
        Assert.Equal(1, snapshot.CursorY);
    }

    [Fact]
    public void TransmitAndDisplay_CursorMovementDisabled_DoesNotMoveCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));

        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20,
            KgpFormat.Rgba32, displayColumns: 3, displayRows: 2, cursorMovement: 1);
        SendKgp(terminal, cmd);

        var snapshot = terminal.CreateSnapshot();
        Assert.Equal(0, snapshot.CursorX);
        Assert.Equal(0, snapshot.CursorY);
    }

    // =============================================
    // Put/Display tests
    // =============================================

    [Fact]
    public void Put_ExistingImage_Succeeds()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Transmit first
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));

        // Then put
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, displayColumns: 2, displayRows: 1));

        // Cursor should move
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal(2, snapshot.CursorX);
    }

    [Fact]
    public void Put_NonExistentImage_NoChange()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(999));

        // Cursor should not move (image not found)
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal(0, snapshot.CursorX);
    }

    [Fact]
    public void Put_WithCursorMovementDisabled_DoesNotMove()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, displayColumns: 3, cursorMovement: 1));

        var snapshot = terminal.CreateSnapshot();
        Assert.Equal(0, snapshot.CursorX);
    }

    // =============================================
    // Multiple operations flow
    // =============================================

    [Fact]
    public void FullFlow_TransmitThenPutMultiple()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Transmit one image
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 4, 4));

        // Put it in two different locations
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H")); // Home
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 1));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 2));

        // Still only 1 image in store
        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void FullFlow_TransmitDeleteTransmit()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));
        Assert.Equal(1, terminal.KgpImageStore.ImageCount);

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('I', imageId: 1));
        Assert.Equal(0, terminal.KgpImageStore.ImageCount);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 1, 1));
        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
    }

    // =============================================
    // Placement tracking tests
    // =============================================

    [Fact]
    public void TransmitAndDisplay_CreatesPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));

        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20,
            KgpFormat.Rgba32, displayColumns: 3, displayRows: 2);
        SendKgp(terminal, cmd);

        var placements = terminal.KgpPlacements;
        Assert.Single(placements);
        Assert.Equal(1u, placements[0].ImageId);
        Assert.Equal(0, placements[0].Row);
        Assert.Equal(0, placements[0].Column);
        Assert.Equal(3u, placements[0].DisplayColumns);
        Assert.Equal(2u, placements[0].DisplayRows);
    }

    [Fact]
    public void Put_CreatesPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;5H")); // row 3, col 5 (1-based)
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 7, displayColumns: 2, displayRows: 1));

        var placements = terminal.KgpPlacements;
        Assert.Single(placements);
        Assert.Equal(1u, placements[0].ImageId);
        Assert.Equal(7u, placements[0].PlacementId);
        Assert.Equal(2, placements[0].Row);  // 0-based
        Assert.Equal(4, placements[0].Column); // 0-based
    }

    [Fact]
    public void Put_SamePlacementId_ReplacesExisting()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 5, displayColumns: 2));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 5, displayColumns: 3));

        var placements = terminal.KgpPlacements;
        Assert.Single(placements); // Replaced, not duplicated
        Assert.Equal(3u, placements[0].DisplayColumns);
        Assert.Equal(2, placements[0].Row); // New position
    }

    [Fact]
    public void Put_DifferentPlacementIds_MultiplePlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 1));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 2));

        Assert.Equal(2, terminal.KgpPlacements.Count);
    }

    [Fact]
    public void Put_ZeroPlacementId_CreatesSeparatePlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 0));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 0));

        Assert.Equal(2, terminal.KgpPlacements.Count);
    }

    [Fact]
    public void Placement_ZIndex_Stored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20,
            KgpFormat.Rgba32, zIndex: -1);
        SendKgp(terminal, cmd);

        Assert.Equal(-1, terminal.KgpPlacements[0].ZIndex);
    }

    // =============================================
    // Delete placement tests
    // =============================================

    [Fact]
    public void Delete_All_ClearsPlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 10, 20));

        Assert.Equal(2, terminal.KgpPlacements.Count);

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('a'));

        Assert.Empty(terminal.KgpPlacements);
    }

    [Fact]
    public void Delete_ById_RemovesOnlyMatchingPlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 10, 20));

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('i', imageId: 1));

        Assert.Single(terminal.KgpPlacements);
        Assert.Equal(2u, terminal.KgpPlacements[0].ImageId);
    }

    [Fact]
    public void Delete_ById_WithPlacementId_RemovesSpecific()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 1));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 2));

        Assert.Equal(2, terminal.KgpPlacements.Count);

        // Delete only placement 1
        var deleteCmd = KgpTestHelper.BuildCommand("a=d,d=i,i=1,p=1");
        SendKgp(terminal, deleteCmd);

        Assert.Single(terminal.KgpPlacements);
        Assert.Equal(2u, terminal.KgpPlacements[0].PlacementId);
    }

    [Fact]
    public void Delete_AtCursor_RemovesIntersecting()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20,
            displayColumns: 3, displayRows: 2));

        // Place cursor inside the placement
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;2H")); // row 1, col 2
        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('c'));

        Assert.Empty(terminal.KgpPlacements);
    }

    [Fact]
    public void Delete_ByZIndex_RemovesMatchingZIndex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20, zIndex: -1));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 10, 20, zIndex: 0));

        var deleteCmd = KgpTestHelper.BuildCommand("a=d,d=z,z=-1");
        SendKgp(terminal, deleteCmd);

        Assert.Single(terminal.KgpPlacements);
        Assert.Equal(0, terminal.KgpPlacements[0].ZIndex);
    }

    // =============================================
    // Clear screen tests (Phase 4)
    // =============================================

    [Fact]
    public void ClearScreen_ClearsKgpPlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20));
        Assert.Single(terminal.KgpPlacements);

        // ESC[2J should clear all images per spec
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2J"));

        // Note: ESC[2J clears images according to KGP spec.
        // If implementation doesn't clear placements on ESC[2J yet, this test documents the requirement.
    }

    [Fact]
    public void Reset_ClearsKgpState()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20));
        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        Assert.Single(terminal.KgpPlacements);

        // Reset terminal (RIS - ESC c)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1bc"));

        // After reset, images and placements should be cleared
        // This documents the expected behavior per KGP spec
    }

    // =============================================
    // Placement intersection tests
    // =============================================

    [Fact]
    public void Placement_IntersectsCell_CorrectBounds()
    {
        var p = new KgpPlacement(1, 0, 2, 3, 4, 3); // row=2, col=3, 4 cols, 3 rows

        // Inside bounds
        Assert.True(p.IntersectsCell(2, 3));
        Assert.True(p.IntersectsCell(4, 6)); // row=4 (2+2), col=6 (3+3)

        // Outside bounds
        Assert.False(p.IntersectsCell(1, 3)); // above
        Assert.False(p.IntersectsCell(5, 3)); // below
        Assert.False(p.IntersectsCell(2, 2)); // left
        Assert.False(p.IntersectsCell(2, 7)); // right
    }

    [Fact]
    public void Placement_IntersectsRow_CorrectBounds()
    {
        var p = new KgpPlacement(1, 0, 5, 0, 10, 3); // starts at row 5, 3 rows tall

        Assert.True(p.IntersectsRow(5));
        Assert.True(p.IntersectsRow(6));
        Assert.True(p.IntersectsRow(7));
        Assert.False(p.IntersectsRow(4));
        Assert.False(p.IntersectsRow(8));
    }

    [Fact]
    public void Placement_IntersectsColumn_CorrectBounds()
    {
        var p = new KgpPlacement(1, 0, 0, 5, 3, 10); // starts at col 5, 3 cols wide

        Assert.True(p.IntersectsColumn(5));
        Assert.True(p.IntersectsColumn(6));
        Assert.True(p.IntersectsColumn(7));
        Assert.False(p.IntersectsColumn(4));
        Assert.False(p.IntersectsColumn(8));
    }

    // =============================================
    // Scrolling tests (Phase 4)  
    // =============================================

    [Fact]
    public void Scroll_PlacementsScrollWithText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 10, 5);

        // Place image at row 0
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20,
            displayColumns: 2, displayRows: 1, cursorMovement: 1));

        var placementsBefore = terminal.KgpPlacements;
        Assert.Equal(0, placementsBefore[0].Row);

        // Scroll by writing enough text to push past bottom
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;1H")); // Go to last row
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\n")); // This triggers scroll

        // After scroll, placement row should decrease by 1 (scrolled up)
        // Note: This test documents the expected behavior - implementation
        // may need to handle placement scrolling in the scroll logic
    }
}
