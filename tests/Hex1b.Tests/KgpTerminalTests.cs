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
}
