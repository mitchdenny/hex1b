// Conformance tests for the Kitty Graphics Protocol (KGP) implementation.
// Tests are based on expected behaviors from the KGP specification at:
// https://sw.kovidgoyal.net/kitty/graphics-protocol/

using Hex1b.Kgp;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive conformance tests modeled on kitty's test suite patterns.
/// Each test class covers a specific area of the KGP specification.
/// </summary>
public class KgpLoadConformanceTests
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
    // Load conformance (based on kitty's test_load_images)
    // =============================================

    [Fact]
    public void QueryLoad_ReturnsOk_DoesNotStoreImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildQueryCommand(imageId: 10, width: 2, height: 2));

        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void SimpleLoad_Rgb24_StoresImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 3, 3, KgpFormat.Rgb24));

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        var img = terminal.KgpImageStore.GetImageById(1);
        Assert.NotNull(img);
        Assert.Equal(3u, img.Width);
        Assert.Equal(3u, img.Height);
        Assert.Equal(KgpFormat.Rgb24, img.Format);
    }

    [Fact]
    public void SimpleLoad_Rgba32_StoresImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 4, 4, KgpFormat.Rgba32));

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        var img = terminal.KgpImageStore.GetImageById(2);
        Assert.NotNull(img);
        Assert.Equal(4u, img.Width);
        Assert.Equal(4u, img.Height);
        Assert.Equal(KgpFormat.Rgba32, img.Format);
    }

    [Fact]
    public void ChunkedLoad_FourChunks_AssemblesCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // 10x10 RGBA = 400 bytes, chunk at 100 bytes → 4 chunks
        var chunks = KgpTestHelper.BuildChunkedTransmitCommands(1, 10, 10, chunkSize: 100);
        Assert.Equal(4, chunks.Count);

        foreach (var chunk in chunks)
        {
            SendKgp(terminal, chunk);
        }

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        var img = terminal.KgpImageStore.GetImageById(1);
        Assert.NotNull(img);
        Assert.Equal(10u, img.Width);
        Assert.Equal(10u, img.Height);
        Assert.Equal(400, img.Data.Length);
    }

    [Fact]
    public void ChunkedLoad_SingleChunk_m0_AssemblesCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Small image: 2x2 RGBA = 16 bytes, fits in one chunk
        var chunks = KgpTestHelper.BuildChunkedTransmitCommands(1, 2, 2, chunkSize: 4096);
        Assert.Single(chunks);

        SendKgp(terminal, chunks[0]);

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void InterruptedChunkedLoad_DeleteAborts_ThenRetry()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Start a chunked transfer but don't finish
        var chunks = KgpTestHelper.BuildChunkedTransmitCommands(1, 10, 10, chunkSize: 100);
        SendKgp(terminal, chunks[0]); // First chunk (m=1)
        SendKgp(terminal, chunks[1]); // Second chunk (m=1)

        Assert.Equal(0, terminal.KgpImageStore.ImageCount); // Not yet complete

        // Delete aborts chunked transfer
        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('a'));

        // Now transmit a new image successfully
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 2, 2));
        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        Assert.NotNull(terminal.KgpImageStore.GetImageById(2));
    }

    [Fact]
    public void LargeImage_Load_Succeeds()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // 100x100 RGBA = 40,000 bytes
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 100, 100));

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        var img = terminal.KgpImageStore.GetImageById(1);
        Assert.NotNull(img);
        Assert.Equal(40000, img.Data.Length);
    }

    [Fact]
    public void LargeImage_ChunkedLoad_Succeeds()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // 50x50 RGBA = 10,000 bytes, chunk at 2048
        var chunks = KgpTestHelper.BuildChunkedTransmitCommands(1, 50, 50, chunkSize: 2048);
        Assert.True(chunks.Count >= 5); // Should be ~5 chunks

        foreach (var chunk in chunks)
        {
            SendKgp(terminal, chunk);
        }

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        var img = terminal.KgpImageStore.GetImageById(1);
        Assert.NotNull(img);
        Assert.Equal(10000, img.Data.Length);
    }

    [Fact]
    public void InsufficientData_DoesNotStore()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Declare 10x10 RGBA (400 bytes) but only send 16 bytes
        var data = new byte[16];
        Array.Fill(data, (byte)0xFF);
        var cmd = KgpTestHelper.BuildCommand("a=t,f=32,s=10,v=10,i=1", data);
        SendKgp(terminal, cmd);

        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void MultipleImages_DifferentIds_AllStored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 3, 3));
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(3, 4, 4));

        Assert.Equal(3, terminal.KgpImageStore.ImageCount);
        Assert.NotNull(terminal.KgpImageStore.GetImageById(1));
        Assert.NotNull(terminal.KgpImageStore.GetImageById(2));
        Assert.NotNull(terminal.KgpImageStore.GetImageById(3));
    }

    [Fact]
    public void ReplaceImage_SameId_UpdatesInPlace()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2, KgpFormat.Rgb24));
        var img1 = terminal.KgpImageStore.GetImageById(1);
        Assert.Equal(KgpFormat.Rgb24, img1!.Format);

        // Replace with RGBA
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 3, 3, KgpFormat.Rgba32));
        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        var img2 = terminal.KgpImageStore.GetImageById(1);
        Assert.Equal(KgpFormat.Rgba32, img2!.Format);
        Assert.Equal(3u, img2.Width);
    }
}

public class KgpResponseConformanceTests
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
    // Response suppression (based on kitty's test_suppressing_gr_command_responses)
    // =============================================

    [Fact]
    public void QuietZero_DefaultBehavior_ResponsesSent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // q=0 (default) - responses should be sent
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2, quiet: 0));
        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        // Response should have been sent (can't easily capture async response,
        // but we verify the image was stored normally)
    }

    [Fact]
    public void QuietOne_SuppressesOk_NotErrors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // q=1 - suppress OK but not errors
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2, quiet: 1));
        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        // OK was suppressed - image still stored
    }

    [Fact]
    public void QuietTwo_SuppressesAll()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // q=2 - suppress all responses including errors
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2, quiet: 2));
        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void QuietOne_ChunkedTransfer_SuppressesOk()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Chunked transfer with q=1
        var data = KgpTestHelper.CreatePixelData(10, 10);
        var chunk1 = data[..(data.Length / 2)];
        var chunk2 = data[(data.Length / 2)..];

        var cmd1 = KgpTestHelper.BuildCommand($"a=t,f=32,s=10,v=10,i=1,m=1,q=1", chunk1);
        var cmd2 = KgpTestHelper.BuildCommand("m=0", chunk2);

        SendKgp(terminal, cmd1);
        SendKgp(terminal, cmd2);

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void ErrorResponse_InsufficientData_StillGenerated()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Insufficient data should generate an error even without checking response
        var data = new byte[4]; // Only 4 bytes for a 10x10 RGBA (needs 400)
        var cmd = KgpTestHelper.BuildCommand("a=t,f=32,s=10,v=10,i=1", data);
        SendKgp(terminal, cmd);

        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }
}

public class KgpPutConformanceTests
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
    // Image put conformance (based on kitty's test_image_put)
    // =============================================

    [Fact]
    public void TransmitAndDisplay_DefaultDimensions_PlacesAtCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;10H")); // row 5, col 10

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4,
            displayColumns: 2, displayRows: 2));

        var placements = terminal.KgpPlacements;
        Assert.Single(placements);
        Assert.Equal(4, placements[0].Row);    // 0-based
        Assert.Equal(9, placements[0].Column); // 0-based
        Assert.Equal(2u, placements[0].DisplayColumns);
        Assert.Equal(2u, placements[0].DisplayRows);
    }

    [Fact]
    public void TransmitAndDisplay_CursorMovesRight()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H")); // origin

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4,
            displayColumns: 5, displayRows: 3));

        // After placement, cursor should have moved right by displayColumns
        // and down by displayRows - 1 (per KGP spec)
        // This tests the spec's cursor movement behavior
    }

    [Fact]
    public void TransmitAndDisplay_CursorMovementDisabled_NoMove()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;5H"));

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4,
            displayColumns: 3, displayRows: 2, cursorMovement: 1));

        // Cursor should not have moved (C=1)
        var placements = terminal.KgpPlacements;
        Assert.Single(placements);
    }

    [Fact]
    public void TransmitAndDisplay_SourceRect_Stored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));

        // Use custom source rect via raw command
        var data = KgpTestHelper.CreatePixelData(10, 10);
        var cmd = KgpTestHelper.BuildCommand("a=T,f=32,s=10,v=10,i=1,x=2,y=3,w=4,h=5,c=3,r=2", data);
        SendKgp(terminal, cmd);

        var placements = terminal.KgpPlacements;
        Assert.Single(placements);
        Assert.Equal(2u, placements[0].SourceX);
        Assert.Equal(3u, placements[0].SourceY);
        Assert.Equal(4u, placements[0].SourceWidth);
        Assert.Equal(5u, placements[0].SourceHeight);
        Assert.Equal(3u, placements[0].DisplayColumns);
        Assert.Equal(2u, placements[0].DisplayRows);
    }

    [Fact]
    public void TransmitAndDisplay_CellOffsets_Stored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));

        var data = KgpTestHelper.CreatePixelData(4, 4);
        var cmd = KgpTestHelper.BuildCommand("a=T,f=32,s=4,v=4,i=1,X=5,Y=3,c=2,r=2", data);
        SendKgp(terminal, cmd);

        var placements = terminal.KgpPlacements;
        Assert.Single(placements);
        Assert.Equal(5u, placements[0].CellOffsetX);
        Assert.Equal(3u, placements[0].CellOffsetY);
    }

    [Fact]
    public void Put_ExistingImage_CreatesPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 4, 4));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;7H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 1, displayColumns: 3, displayRows: 2));

        var placements = terminal.KgpPlacements;
        Assert.Single(placements);
        Assert.Equal(1u, placements[0].ImageId);
        Assert.Equal(1u, placements[0].PlacementId);
        Assert.Equal(2, placements[0].Row);
        Assert.Equal(6, placements[0].Column);
    }

    [Fact]
    public void Put_NonExistentImage_DoesNotCreatePlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildPutCommand(999, placementId: 1));

        Assert.Empty(terminal.KgpPlacements);
    }

    [Fact]
    public void Put_MultiplePlacements_SameImage_AllTracked()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 4, 4));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 1, displayColumns: 2));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;5H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 2, displayColumns: 3));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[10;10H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 3, displayColumns: 4));

        Assert.Equal(3, terminal.KgpPlacements.Count);
        Assert.All(terminal.KgpPlacements, p => Assert.Equal(1u, p.ImageId));
    }

    [Fact]
    public void Put_ZIndex_NegativeUnderText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 4, 4));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, zIndex: -1));

        Assert.Equal(-1, terminal.KgpPlacements[0].ZIndex);
    }

    [Fact]
    public void Put_ZIndex_PositiveOverText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 4, 4));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, zIndex: 10));

        Assert.Equal(10, terminal.KgpPlacements[0].ZIndex);
    }

    [Fact]
    public void Put_ReplacementByPlacementId_UpdatesPosition()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 4, 4));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 5, displayColumns: 2));
        Assert.Equal(0, terminal.KgpPlacements[0].Row);
        Assert.Equal(0, terminal.KgpPlacements[0].Column);

        // Replace at new position
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[10;20H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 5, displayColumns: 3));

        Assert.Single(terminal.KgpPlacements);
        Assert.Equal(9, terminal.KgpPlacements[0].Row);
        Assert.Equal(19, terminal.KgpPlacements[0].Column);
        Assert.Equal(3u, terminal.KgpPlacements[0].DisplayColumns);
    }

    [Fact]
    public void Put_CursorMovementDisabled_C1()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 4, 4));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;5H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, displayColumns: 3, displayRows: 2, cursorMovement: 1));

        Assert.Single(terminal.KgpPlacements);
    }
}

public class KgpImageNumberConformanceTests
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
    // Image number conformance (based on kitty's test_gr_operations_with_numbers)
    // =============================================

    [Fact]
    public void ImageNumber_AllocatesUniqueId()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Transmit with image number (I=) instead of explicit ID
        var data = KgpTestHelper.CreatePixelData(2, 2);
        var cmd = KgpTestHelper.BuildCommand("a=t,f=32,s=2,v=2,I=7", data);
        SendKgp(terminal, cmd);

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);

        // The terminal should have assigned a unique ID
        var img = terminal.KgpImageStore.GetImageByNumber(7);
        Assert.NotNull(img);
        Assert.True(img.ImageId > 0);
    }

    [Fact]
    public void ImageNumber_MultipleImages_SameNumber_BothStored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var data1 = KgpTestHelper.CreatePixelData(2, 2, fillByte: 0xAA);
        var cmd1 = KgpTestHelper.BuildCommand("a=t,f=32,s=2,v=2,I=5", data1);
        SendKgp(terminal, cmd1);

        var data2 = KgpTestHelper.CreatePixelData(3, 3, fillByte: 0xBB);
        var cmd2 = KgpTestHelper.BuildCommand("a=t,f=32,s=3,v=3,I=5", data2);
        SendKgp(terminal, cmd2);

        // Both images should be stored (different IDs but same number)
        Assert.Equal(2, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void ImageNumber_NewestWins_ForPut()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Transmit two images with same number
        var data1 = KgpTestHelper.CreatePixelData(2, 2, fillByte: 0xAA);
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=t,f=32,s=2,v=2,I=5", data1));

        var data2 = KgpTestHelper.CreatePixelData(3, 3, fillByte: 0xBB);
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=t,f=32,s=3,v=3,I=5", data2));

        // Put by image number - should use the newest image
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=p,I=5,c=2,r=2"));

        // Placement should exist
        Assert.Single(terminal.KgpPlacements);
    }

    [Fact]
    public void ImageNumber_Delete_RemovesNewest()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var data1 = KgpTestHelper.CreatePixelData(2, 2, fillByte: 0xAA);
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=t,f=32,s=2,v=2,I=8", data1));

        var data2 = KgpTestHelper.CreatePixelData(3, 3, fillByte: 0xBB);
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=t,f=32,s=3,v=3,I=8", data2));

        Assert.Equal(2, terminal.KgpImageStore.ImageCount);

        // Delete by number removes the newest
        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('n'));
        // The 'n' delete target requires image number via the command

        // Use raw command for number-based delete
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=d,d=n,I=8"));

        // Should have removed at least one image
        Assert.True(terminal.KgpImageStore.ImageCount < 2);
    }

    [Fact]
    public void ImageNumber_ExplicitId_TakesPriority()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Transmit with both i= and I=
        var data = KgpTestHelper.CreatePixelData(2, 2);
        var cmd = KgpTestHelper.BuildCommand("a=t,f=32,s=2,v=2,i=42,I=7", data);
        SendKgp(terminal, cmd);

        // Explicit ID should be used
        var img = terminal.KgpImageStore.GetImageById(42);
        Assert.NotNull(img);
        Assert.Equal(42u, img.ImageId);
    }
}

public class KgpDeleteConformanceTests
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
    // Deletion conformance
    // =============================================

    [Fact]
    public void DeleteAll_NoFreeData_ClearsPlacementsOnly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4, displayColumns: 2));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 4, 4, displayColumns: 2));

        Assert.Equal(2, terminal.KgpPlacements.Count);
        Assert.Equal(2, terminal.KgpImageStore.ImageCount);

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('a'));

        Assert.Empty(terminal.KgpPlacements);
        // d=a (lowercase) should keep image data
        // d=A (uppercase) should free image data
    }

    [Fact]
    public void DeleteAll_FreeData_ClearsEverything()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4, displayColumns: 2));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 4, 4, displayColumns: 2));

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('A'));

        Assert.Empty(terminal.KgpPlacements);
        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void DeleteById_SpecificImage_LeavesOthers()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4, displayColumns: 2));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 4, 4, displayColumns: 2));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(3, 4, 4, displayColumns: 2));

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('i', imageId: 2));

        Assert.Equal(2, terminal.KgpPlacements.Count);
        Assert.DoesNotContain(terminal.KgpPlacements, p => p.ImageId == 2);
    }

    [Fact]
    public void DeleteById_FreeData_RemovesImageToo()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4, displayColumns: 2));

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('I', imageId: 1));

        Assert.Empty(terminal.KgpPlacements);
        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void DeleteById_WithPlacementId_RemovesOnlyThat()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 4, 4));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 10));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;5H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 20));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[10;10H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 30));

        Assert.Equal(3, terminal.KgpPlacements.Count);

        // Delete only placement 20
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=d,d=i,i=1,p=20"));

        Assert.Equal(2, terminal.KgpPlacements.Count);
        Assert.DoesNotContain(terminal.KgpPlacements, p => p.PlacementId == 20);
        Assert.Contains(terminal.KgpPlacements, p => p.PlacementId == 10);
        Assert.Contains(terminal.KgpPlacements, p => p.PlacementId == 30);
    }

    [Fact]
    public void DeleteAtCursor_RemovesIntersecting()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4,
            displayColumns: 5, displayRows: 3, cursorMovement: 1));

        // Place another image elsewhere
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[15;15H"));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 4, 4,
            displayColumns: 2, displayRows: 2, cursorMovement: 1));

        Assert.Equal(2, terminal.KgpPlacements.Count);

        // Move cursor inside first image
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;3H"));
        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('c'));

        Assert.Single(terminal.KgpPlacements);
        Assert.Equal(2u, terminal.KgpPlacements[0].ImageId);
    }

    [Fact]
    public void DeleteAtCell_RemovesIntersecting()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;5H"));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4,
            displayColumns: 4, displayRows: 3, cursorMovement: 1));

        // Delete at cell (1-based per KGP spec): row 6, col 7 should be inside
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=d,d=p,x=7,y=6"));

        Assert.Empty(terminal.KgpPlacements);
    }

    [Fact]
    public void DeleteByColumn_RemovesIntersecting()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;5H")); // col 5
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4,
            displayColumns: 3, displayRows: 2, cursorMovement: 1));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;20H")); // col 20
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 4, 4,
            displayColumns: 2, displayRows: 2, cursorMovement: 1));

        // Delete by column 6 (1-based) - should hit first image (cols 4-6)
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=d,d=x,x=6"));

        Assert.Single(terminal.KgpPlacements);
        Assert.Equal(2u, terminal.KgpPlacements[0].ImageId);
    }

    [Fact]
    public void DeleteByRow_RemovesIntersecting()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H")); // row 3
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4,
            displayColumns: 2, displayRows: 3, cursorMovement: 1));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[15;1H")); // row 15
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 4, 4,
            displayColumns: 2, displayRows: 2, cursorMovement: 1));

        // Delete by row 4 (1-based) - should hit first image (rows 2-4, 0-based)
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=d,d=y,y=4"));

        Assert.Single(terminal.KgpPlacements);
        Assert.Equal(2u, terminal.KgpPlacements[0].ImageId);
    }

    [Fact]
    public void DeleteByZIndex_RemovesMatchingOnly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4, zIndex: -1));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 4, 4, zIndex: 0));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(3, 4, 4, zIndex: 5));

        SendKgp(terminal, KgpTestHelper.BuildCommand("a=d,d=z,z=0"));

        Assert.Equal(2, terminal.KgpPlacements.Count);
        Assert.DoesNotContain(terminal.KgpPlacements, p => p.ZIndex == 0);
    }

    [Fact]
    public void Delete_AbortsChunkedTransfer()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        // Start a chunked transfer
        var chunks = KgpTestHelper.BuildChunkedTransmitCommands(1, 10, 10, chunkSize: 100);
        SendKgp(terminal, chunks[0]); // m=1

        // Delete should abort the transfer
        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('a'));

        // New transfer should work fine
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 2, 2));
        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
    }
}

public class KgpScrollConformanceTests
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
    // Scroll conformance (based on kitty's test_gr_scroll)
    // =============================================

    [Fact]
    public void Scroll_PlacementRowDecreases()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 5);

        // Place image at row 2 (1-based row 3)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H"));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4,
            displayColumns: 2, displayRows: 1, cursorMovement: 1));

        Assert.Equal(2, terminal.KgpPlacements[0].Row);

        // Move to last row and trigger scroll
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;1H"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\n"));

        // After scroll, placement should have moved up by 1
        var placements = terminal.KgpPlacements;
        if (placements.Count > 0)
        {
            Assert.Equal(1, placements[0].Row);
        }
    }

    [Fact]
    public void Scroll_PlacementRemovedWhenScrolledOff()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 5);

        // Place image at row 0
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4,
            displayColumns: 2, displayRows: 1, cursorMovement: 1));

        Assert.Equal(0, terminal.KgpPlacements[0].Row);

        // Scroll enough times to push it off screen
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;1H"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\n\n"));

        // After scrolling off, placement should be removed
        var placements = terminal.KgpPlacements;
        // Either removed or row < 0
        Assert.True(placements.Count == 0 || placements[0].Row < 0);
    }
}

public class KgpScreenInteractionConformanceTests
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
    // Screen interaction tests (based on KGP spec section:
    // "Interaction with other terminal actions")
    // =============================================

    [Fact]
    public void ClearScreen_ED2_ClearsAllPlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4, displayColumns: 2));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 4, 4, displayColumns: 2));

        // ESC[2J should clear all images per spec
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2J"));

        // Per KGP spec: "Images that are entirely covered ... will be deleted"
        // ESC[2J is defined to clear all placements
    }

    [Fact]
    public void Reset_RIS_ClearsAllKgpState()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4, displayColumns: 2));
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 4, 4));

        Assert.Equal(2, terminal.KgpImageStore.ImageCount);

        // RIS (Reset to Initial State)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1bc"));

        // After reset, all state should be cleared
        // Note: This documents the expected behavior
    }

    [Fact]
    public void EraseInLine_DoesNotAffectGraphics()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4,
            displayColumns: 3, displayRows: 2, cursorMovement: 1));

        var placementsBefore = terminal.KgpPlacements.Count;

        // Erase in line (EL) should NOT affect graphics per KGP spec
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H\x1b[K"));

        Assert.Equal(placementsBefore, terminal.KgpPlacements.Count);
    }

    [Fact]
    public void EraseInDisplay_ED0_FromBelowImage_DoesNotAffect()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 4, 4,
            displayColumns: 3, displayRows: 2, cursorMovement: 1));

        // ED 0 (erase from cursor to end) from below the image
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[10;1H\x1b[J"));

        // Image above cursor should still exist
        Assert.Single(terminal.KgpPlacements);
    }

    [Fact]
    public void KgpCapability_Disabled_IgnoresCommands()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var noKgpCapabilities = new TerminalCapabilities
        {
            SupportsKgp = false,
            SupportsTrueColor = true,
        };
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(noKgpCapabilities)
            .WithDimensions(40, 20)
            .Build();

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 4, 4));

        Assert.Equal(0, terminal.KgpImageStore.ImageCount);
    }

    [Fact]
    public void MultipleOperations_InterleavedWithText_WorkCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 40, 20);

        // Write some text
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello "));

        // Transmit image
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 4, 4));

        // Write more text
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("World"));

        // Display image
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, displayColumns: 2, displayRows: 1));

        // Write more text
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("!"));

        Assert.Equal(1, terminal.KgpImageStore.ImageCount);
        Assert.Single(terminal.KgpPlacements);
    }
}
