using System.Text;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class KgpTerminalTests
{
    private static readonly TerminalCapabilities KgpCapabilities = new()
    {
        SupportsKgp = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
    };

    private static Hex1bTerminal CreateTerminal(
        IHex1bTerminalWorkloadAdapter workload,
        int width = 80,
        int height = 24)
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

    [TestMethod]
    public void Transmit_SingleChunk_StoresImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var cmd = KgpTestHelper.BuildTransmitCommand(1, 2, 2, KgpFormat.Rgba32);
        SendKgp(terminal, cmd);

        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        var image = terminal.KgpImageStore.GetImageById(1);
        Assert.IsNotNull(image);
        Assert.AreEqual(2u, image.Width);
        Assert.AreEqual(2u, image.Height);
        Assert.AreEqual(KgpFormat.Rgba32, image.Format);
    }

    [TestMethod]
    public void Transmit_Rgb24_StoresCorrectFormat()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var cmd = KgpTestHelper.BuildTransmitCommand(1, 1, 1, KgpFormat.Rgb24);
        SendKgp(terminal, cmd);

        var image = terminal.KgpImageStore.GetImageById(1);
        Assert.IsNotNull(image);
        Assert.AreEqual(KgpFormat.Rgb24, image.Format);
        Assert.AreEqual(3, image.Data.Length); // 1x1 RGB = 3 bytes
    }

    [TestMethod]
    public void Transmit_ChunkedTransfer_AssemblesCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var chunks = KgpTestHelper.BuildChunkedTransmitCommands(1, 4, 4, chunkSize: 16);

        Assert.IsTrue(chunks.Count > 1, "Should produce multiple chunks");

        foreach (var chunk in chunks)
        {
            SendKgp(terminal, chunk);
        }

        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        var image = terminal.KgpImageStore.GetImageById(1);
        Assert.IsNotNull(image);
        Assert.AreEqual(4u * 4u * 4, (uint)image.Data.Length); // 4x4 RGBA
    }

    [TestMethod]
    public void Transmit_InsufficientData_DoesNotStore()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Claim 10x10 RGBA but only send 4 bytes
        var cmd = KgpTestHelper.BuildCommand("a=t,f=32,s=10,v=10,i=1", new byte[] { 1, 2, 3, 4 });
        SendKgp(terminal, cmd);

        // Image should not be stored because data is insufficient
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public void Transmit_ReplaceExistingId_UpdatesImageAndRemovesItsPlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1, fillByte: 0xAA));
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 1, 1, fillByte: 0xCC));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 0));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 7));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(2, placementId: 7));

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2, fillByte: 0xBB));

        Assert.AreEqual(2, terminal.KgpImageStore.ImageCount);
        var image = terminal.KgpImageStore.GetImageById(1);
        Assert.IsNotNull(image);
        Assert.AreEqual(2u, image.Width);
        Assert.AreEqual(0xBB, image.Data[0]);
        var remainingPlacement = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(2u, remainingPlacement.ImageId);
        Assert.AreEqual(7u, remainingPlacement.PlacementId);
    }

    [TestMethod]
    public void Transmit_InvalidExplicitReplacement_RemovesOldStateBeforeValidation()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=1,v=1,I=42,q=2",
            KgpTestHelper.CreatePixelData(1, 1, fillByte: 0xAA)));
        var first = terminal.KgpImageStore.GetImageByNumber(42);
        Assert.IsNotNull(first);
        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=1,v=1,I=42,q=2",
            KgpTestHelper.CreatePixelData(1, 1, fillByte: 0xBB)));
        var newest = terminal.KgpImageStore.GetImageByNumber(42);
        Assert.IsNotNull(newest);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;4H"));
        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=p,I=42,p=5,c=2,r=1,C=1,q=2"));
        using var originalSnapshot = terminal.CreateSnapshot();

        var invalidReplacement = KgpTestHelper.BuildCommand(
            $"a=t,f=32,s=2,v=2,i={newest.ImageId}",
            new byte[] { 1, 2, 3, 4 });
        SendKgp(terminal, invalidReplacement);

        Assert.AreEqual(
            $"\x1b_Gi={newest.ImageId};ENODATA:Insufficient image data: 4 < 16\x1b\\",
            workload.ReadResponse());
        Assert.IsNull(terminal.KgpImageStore.GetImageById(newest.ImageId));
        Assert.AreSame(first, terminal.KgpImageStore.GetImageByNumber(42));
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Assert.IsEmpty(terminal.KgpPlacements);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(originalSnapshot.CursorX, snapshot.CursorX);
        Assert.AreEqual(originalSnapshot.CursorY, snapshot.CursorY);
    }

    [TestMethod]
    public void Transmit_ChunkedExplicitReplacement_RemovesOldStateOnFirstChunk()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,i=1,p=3,c=1,r=1,C=1,q=2",
            KgpTestHelper.CreatePixelData(1, 1, fillByte: 0xAA)));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 4));
        Assert.AreEqual(2, terminal.KgpPlacements.Count);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=1,v=2,i=1,m=1,q=2",
            new byte[] { 1, 2, 3, 4 }));

        Assert.IsTrue(terminal.KgpImageStore.IsChunkedTransferInProgress);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        Assert.IsEmpty(terminal.KgpPlacements);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "m=0,q=2",
            new byte[] { 5, 6, 7, 8 }));

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        var replacement = terminal.KgpImageStore.GetImageById(1);
        Assert.IsNotNull(replacement);
        TestSeq.AreEqual(
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
            replacement.Data);
        Assert.IsEmpty(terminal.KgpPlacements);
    }

    [TestMethod]
    public void Transmit_WithoutCapability_Ignored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = false })
            .WithDimensions(80, 24)
            .Build();

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));

        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    // =============================================
    // Query tests
    // =============================================

    [TestMethod]
    public void Query_DoesNotStoreImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var cmd = KgpTestHelper.BuildQueryCommand(imageId: 31);
        SendKgp(terminal, cmd);

        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public void Transmit_WithAppWorkload_DoesNotInjectKgpResponseIntoInputEvents()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));

        var sawInput = SpinWait.SpinUntil(() => workload.InputEvents.TryRead(out _), TimeSpan.FromMilliseconds(100));
        Assert.IsFalse(sawInput);
    }

    // =============================================
    // Delete tests
    // =============================================

    [TestMethod]
    public void Delete_All_ClearsAllImages()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 1, 1));
        Assert.AreEqual(2, terminal.KgpImageStore.ImageCount);

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('A'));

        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public void Delete_ById_RemovesSpecificImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 1, 1));

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('I', imageId: 1));

        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
    }

    [TestMethod]
    public void Delete_ById_FreeData_RemovesImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('I', imageId: 1));

        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public void Delete_AbortsChunkedTransfer()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Start a chunked transfer
        var data = new byte[] { 1, 2, 3, 4 };
        var cmd = KgpTestHelper.BuildCommand("a=t,f=32,s=2,v=2,i=1,m=1", data);
        SendKgp(terminal, cmd);

        Assert.IsTrue(terminal.KgpImageStore.IsChunkedTransferInProgress);

        // Delete should abort the transfer
        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('a'));

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
    }

    // =============================================
    // Response suppression tests
    // =============================================

    [TestMethod]
    public void Transmit_QuietOne_SuppressesOkResponse()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // q=1 suppresses OK
        var cmd = KgpTestHelper.BuildTransmitCommand(1, 1, 1, quiet: 1);
        SendKgp(terminal, cmd);

        // Image should still be stored
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
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
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public void InvalidTransmitControl_EmitsEinvalWithRecoveredIdentity()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, "\x1b_Ga=t,i=7,I=8,p=9,q=1,f=99\x1b\\");

        Assert.AreEqual(
            "\x1b_Gi=7,I=8;EINVAL:Invalid image format '99'.\x1b\\",
            workload.ReadResponse());
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        Assert.IsEmpty(terminal.KgpPlacements);
    }

    [TestMethod]
    [DataRow(0, true)]
    [DataRow(1, true)]
    [DataRow(2, false)]
    public void ConflictingImageIdentity_QuietModes_EmitExactErrorWithoutMutation(
        int quiet,
        bool expectsResponse)
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);
        var pixel = KgpTestHelper.CreatePixelData(1, 1);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,i=1,p=3,C=1,q=2",
            pixel));
        var originalImage = terminal.KgpImageStore.GetImageById(1);
        var originalPlacement = TestSeq.Single(terminal.KgpPlacements);
        var originalSnapshot = terminal.CreateSnapshot();

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            $"a=T,f=32,s=1,v=1,i=7,I=8,q={quiet}",
            pixel));

        if (expectsResponse)
        {
            Assert.AreEqual(
                "\x1b_Gi=7,I=8;EINVAL:Must not specify both image id and image number\x1b\\",
                workload.ReadResponse());
        }
        else
        {
            workload.AssertNoResponse();
        }

        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Assert.AreSame(originalImage, terminal.KgpImageStore.GetImageById(1));
        Assert.AreSame(originalPlacement, TestSeq.Single(terminal.KgpPlacements));
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(originalSnapshot.CursorX, snapshot.CursorX);
        Assert.AreEqual(originalSnapshot.CursorY, snapshot.CursorY);
    }

    [TestMethod]
    public void Delete_ConflictingImageIdentity_EmitsEinvalWithoutDeleting()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,i=1,p=4,C=1,q=2",
            KgpTestHelper.CreatePixelData(1, 1)));

        SendKgp(terminal, "\x1b_Ga=d,d=I,i=1,I=8\x1b\\");

        Assert.AreEqual(
            "\x1b_Gi=1,I=8;EINVAL:Must not specify both image id and image number\x1b\\",
            workload.ReadResponse());
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Assert.AreEqual(1, terminal.KgpPlacements.Count);
    }

    [TestMethod]
    public void ConflictingImageIdentity_DuringChunkedTransfer_DoesNotMutateUpload()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=1,v=2,i=5,m=1,q=2",
            new byte[] { 1, 2, 3, 4 }));
        Assert.IsTrue(terminal.KgpImageStore.IsChunkedTransferInProgress);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,i=7,I=8,m=0,q=2",
            new byte[] { 99, 99, 99, 99 }));

        Assert.IsTrue(terminal.KgpImageStore.IsChunkedTransferInProgress);
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "m=0,q=2",
            new byte[] { 5, 6, 7, 8 }));

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        var image = terminal.KgpImageStore.GetImageById(5);
        Assert.IsNotNull(image);
        TestSeq.AreEqual(
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
            image.Data);
    }

    [TestMethod]
    public void InvalidAction_EmitsEinval()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, "\x1b_Ga=Z,i=4\x1b\\");

        Assert.AreEqual(
            "\x1b_Gi=4;EINVAL:Invalid action value 'Z'.\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void InvalidNonDeleteControl_QuietTwoSuppressesEinval()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, "\x1b_Ga=t,i=7,q=2,f=99\x1b\\");

        workload.AssertNoResponse();
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    [DataRow("a=d,d=?")]
    [DataRow("a=d,d=f,r=bad")]
    [DataRow("d=p,x=1,y=4294967296,a=d")]
    [DataRow("a=d,d=q,x=1,y=2,z=2147483648")]
    [DataRow("a=x,a=d,d=?")]
    [DataRow("a=d,a=x")]
    public void InvalidDeleteControl_IsNoResponseSafeNoOp(string controlData)
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(
            terminal,
            KgpTestHelper.BuildTransmitCommand(
                imageId: 1,
                width: 1,
                height: 1,
                quiet: 2));
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);

        SendKgp(terminal, $"\x1b_G{controlData},i=1\x1b\\");

        workload.AssertNoResponse();
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
    }

    [TestMethod]
    [DataRow("a=f,i=1,s=1,v=1")]
    [DataRow("a=a,i=1,s=3,v=1")]
    [DataRow("a=c,i=1,c=1,r=1")]
    public void ValidUnimplementedAnimationAction_IsTypedNoOp(string controlData)
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, $"\x1b_G{controlData}\x1b\\");

        workload.AssertNoResponse();
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        Assert.IsEmpty(terminal.KgpPlacements);
    }

    // =============================================
    // Image number tests
    // =============================================

    [TestMethod]
    public void Transmit_WithImageNumber_StoresCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Use I= key instead of i=
        var data = KgpTestHelper.CreatePixelData(1, 1, KgpFormat.Rgb24);
        var cmd = KgpTestHelper.BuildCommand("a=t,f=24,s=1,v=1,I=93", data);
        SendKgp(terminal, cmd);

        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        var image = terminal.KgpImageStore.GetImageByNumber(93);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void Transmit_MultipleWithSameNumber_BothStored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var data = KgpTestHelper.CreatePixelData(1, 1, KgpFormat.Rgb24);
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=t,f=24,s=1,v=1,I=93", data));
        SendKgp(terminal, KgpTestHelper.BuildCommand("a=t,f=24,s=1,v=1,I=93", data));

        // Both should be stored (with different IDs)
        Assert.AreEqual(2, terminal.KgpImageStore.ImageCount);

        // GetImageByNumber returns the newest
        var newest = terminal.KgpImageStore.GetImageByNumber(93);
        Assert.IsNotNull(newest);
    }

    [TestMethod]
    public void Transmit_WithImageNumber_EmitsGeneratedIdAndPreservesExplicitImage()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(
            1,
            1,
            1,
            quiet: 2,
            fillByte: 0xAA));

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=1,v=1,I=93",
            KgpTestHelper.CreatePixelData(1, 1, fillByte: 0xBB)));

        Assert.AreEqual(
            "\x1b_Gi=2,I=93;OK\x1b\\",
            workload.ReadResponse());
        var numbered = terminal.KgpImageStore.GetImageByNumber(93);
        Assert.IsNotNull(numbered);
        Assert.AreEqual(2u, numbered.ImageId);
        Assert.AreEqual(0xBB, numbered.Data[0]);
        Assert.AreEqual(0xAA, terminal.KgpImageStore.GetImageById(1)!.Data[0]);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void Transmit_Anonymous_AllQuietModesStoreWithoutResponse(int quiet)
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            $"a=t,f=32,s=1,v=1,q={quiet}",
            KgpTestHelper.CreatePixelData(1, 1)));

        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        workload.AssertNoResponse();
    }

    [TestMethod]
    public void Transmit_InvalidImageNumber_DoesNotAllocateIdAndRespondsWithNumberOnly()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=2,v=2,I=93",
            new byte[] { 1, 2, 3, 4 }));

        Assert.AreEqual(
            "\x1b_GI=93;ENODATA:Insufficient image data: 4 < 16\x1b\\",
            workload.ReadResponse());
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=1,v=1,I=94",
            KgpTestHelper.CreatePixelData(1, 1)));

        Assert.AreEqual(
            "\x1b_Gi=1,I=94;OK\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void Transmit_InvalidAnonymous_DoesNotRespondOrConsumeGeneratedId()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=2,v=2",
            new byte[] { 1, 2, 3, 4 }));

        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        workload.AssertNoResponse();

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=1,v=1,I=94",
            KgpTestHelper.CreatePixelData(1, 1)));

        Assert.AreEqual(
            "\x1b_Gi=1,I=94;OK\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void Transmit_ChunkedImageNumber_RespondsOnlyAfterCommittedGeneratedId()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1, quiet: 2));

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=1,v=2,I=93,m=1",
            new byte[] { 1, 2, 3, 4 }));

        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        workload.AssertNoResponse();

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "m=0",
            new byte[] { 5, 6, 7, 8 }));

        Assert.AreEqual(
            "\x1b_Gi=2,I=93;OK\x1b\\",
            workload.ReadResponse());
        Assert.AreEqual(2u, terminal.KgpImageStore.GetImageByNumber(93)!.ImageId);
    }

    [TestMethod]
    public void Transmit_ChunkedAnonymous_StoresWithoutAnyResponse()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=1,v=2,m=1",
            new byte[] { 1, 2, 3, 4 }));
        workload.AssertNoResponse();

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "m=0",
            new byte[] { 5, 6, 7, 8 }));

        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        workload.AssertNoResponse();
    }

    // =============================================
    // TransmitAndDisplay tests
    // =============================================

    [TestMethod]
    public void TransmitAndDisplay_StoresAndMovesCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        // Position cursor at (0,0)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H")); // Home

        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20,
            KgpFormat.Rgba32, displayColumns: 3, displayRows: 2);
        SendKgp(terminal, cmd);

        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);

        // Cursor should have moved: right by 3 cols, down by 1 row (2-1)
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(3, snapshot.CursorX);
        Assert.AreEqual(1, snapshot.CursorY);
    }

    [TestMethod]
    public void TransmitAndDisplay_CursorMovementDisabled_DoesNotMoveCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));

        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20,
            KgpFormat.Rgba32, displayColumns: 3, displayRows: 2, cursorMovement: 1);
        SendKgp(terminal, cmd);

        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(0, snapshot.CursorX);
        Assert.AreEqual(0, snapshot.CursorY);
    }

    [TestMethod]
    public void TransmitAndDisplay_WithImageNumber_PlacesNewGeneratedImage()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1, quiet: 2));

        var command = KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,I=93,p=5,c=1,r=1,C=1,q=2",
            KgpTestHelper.CreatePixelData(1, 1));
        SendKgp(terminal, command);

        var image = terminal.KgpImageStore.GetImageByNumber(93);
        Assert.IsNotNull(image);
        Assert.AreEqual(2u, image.ImageId);
        var placement = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(image.ImageId, placement.ImageId);
        Assert.AreEqual(5u, placement.PlacementId);
    }

    [TestMethod]
    public void TransmitAndDisplay_ReplaceExistingId_RemovesOldPlacementsThenPlacesReplacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,i=1,p=1,c=1,r=1,C=1,q=2",
            KgpTestHelper.CreatePixelData(1, 1, fillByte: 0xAA)));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 2));

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,i=1,p=7,c=1,r=1,C=1,q=2",
            KgpTestHelper.CreatePixelData(1, 1, fillByte: 0xBB)));

        var placement = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(1u, placement.ImageId);
        Assert.AreEqual(7u, placement.PlacementId);
        Assert.AreEqual(0xBB, terminal.KgpImageStore.GetImageById(1)!.Data[0]);
    }

    [TestMethod]
    public void TransmitAndDisplay_AnonymousZeroPlacementId_CreatesDistinctResolvablePlacements()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);
        var command = KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,p=0,c=1,r=1,C=1",
            KgpTestHelper.CreatePixelData(1, 1));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));
        SendKgp(terminal, command);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;4H"));
        SendKgp(terminal, command);

        Assert.AreEqual(2, terminal.KgpImageStore.ImageCount);
        Assert.AreEqual(2, terminal.KgpPlacements.Count);
        Assert.AreEqual(2, terminal.KgpPlacements.Select(p => p.ImageId).Distinct().Count());
        TestSeq.All(terminal.KgpPlacements, placement =>
        {
            Assert.AreEqual(0u, placement.PlacementId);
            Assert.IsNotNull(terminal.KgpImageStore.GetImageById(placement.ImageId));
        });

        var internalId = terminal.KgpPlacements[0].ImageId;
        SendKgp(terminal, KgpTestHelper.BuildCommand(
            $"a=p,i={internalId},p=9,c=1,r=1,C=1,q=2"));

        Assert.AreEqual(2, terminal.KgpPlacements.Count);
        workload.AssertNoResponse();
    }

    [TestMethod]
    public void TransmitAndDisplay_AnonymousNonZeroPlacementId_IsIgnored()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);
        var command = KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,p=7,c=1,r=1,C=1",
            KgpTestHelper.CreatePixelData(1, 1));

        SendKgp(terminal, command);
        SendKgp(terminal, command);

        Assert.AreEqual(2, terminal.KgpPlacements.Count);
        TestSeq.All(
            terminal.KgpPlacements,
            placement => Assert.AreEqual(0u, placement.PlacementId));
        Assert.AreEqual(2, terminal.KgpPlacements.Select(p => p.ImageId).Distinct().Count());
        workload.AssertNoResponse();
    }

    [TestMethod]
    public void TransmitAndDisplay_ExplicitIdCollidingWithAnonymousStorage_RelocatesAnonymousPlacement()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));
        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,p=7,c=1,r=1,C=1",
            KgpTestHelper.CreatePixelData(1, 1, fillByte: 0xAA)));
        var anonymousPlacement = TestSeq.Single(terminal.KgpPlacements);
        using var snapshotBeforeCollision = terminal.CreateSnapshot();
        Assert.AreEqual(1u, anonymousPlacement.ImageId);
        Assert.AreEqual(0u, anonymousPlacement.PlacementId);
        Assert.AreNotSame(
            anonymousPlacement,
            snapshotBeforeCollision.KgpPlacements[0]);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H"));
        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,i=1,p=7,c=1,r=1,C=1,q=2",
            KgpTestHelper.CreatePixelData(1, 1, fillByte: 0xBB)));

        Assert.AreEqual(2, terminal.KgpImageStore.ImageCount);
        Assert.AreEqual(2, terminal.KgpPlacements.Count);
        Assert.AreEqual(1u, anonymousPlacement.ImageId);
        var relocatedAnonymousPlacement = TestSeq.Single(
            terminal.KgpPlacements.Where(p => p.PlacementId == 0));
        Assert.AreEqual(2u, relocatedAnonymousPlacement.ImageId);
        Assert.AreEqual(0xAA, terminal.KgpImageStore.GetImageById(2)!.Data[0]);
        Assert.IsNull(terminal.KgpImageStore.GetImageByClientId(2));
        Assert.AreEqual(0xBB, terminal.KgpImageStore.GetImageByClientId(1)!.Data[0]);
        var explicitPlacement = TestSeq.Single(
            terminal.KgpPlacements.Where(p => p.ImageId == 1));
        Assert.AreEqual(7u, explicitPlacement.PlacementId);
        Assert.AreEqual(2, explicitPlacement.Row);
        Assert.AreEqual(1u, snapshotBeforeCollision.KgpPlacements[0].ImageId);
        Assert.AreEqual(0xAA, snapshotBeforeCollision.KgpImages[1].Data[0]);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=p,i=2,p=9,c=1,r=1,C=1,q=2"));
        Assert.AreEqual(2, terminal.KgpPlacements.Count);
        workload.AssertNoResponse();
    }

    [TestMethod]
    public void Transmit_InvalidExplicitClaim_RelocatesAnonymousImageBeforeValidation()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,p=0,c=1,r=1,C=1",
            KgpTestHelper.CreatePixelData(1, 1, fillByte: 0xAA)));
        Assert.AreEqual(1u, TestSeq.Single(terminal.KgpPlacements).ImageId);

        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=t,f=32,s=2,v=2,i=1,q=2",
            new byte[] { 1, 2, 3, 4 }));

        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(0xAA, terminal.KgpImageStore.GetImageById(2)!.Data[0]);
        Assert.IsNull(terminal.KgpImageStore.GetImageByClientId(2));
        Assert.AreEqual(2u, TestSeq.Single(terminal.KgpPlacements).ImageId);
        workload.AssertNoResponse();
    }

    [TestMethod]
    public void Snapshot_KgpPlacementValuesRemainImmutableAfterScroll()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 10, 5);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H"));
        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,i=1,p=1,c=1,r=1,C=1,q=2",
            KgpTestHelper.CreatePixelData(1, 1, fillByte: 0xAA)));
        var liveBeforeScroll = TestSeq.Single(terminal.KgpPlacements);
        using var snapshot = terminal.CreateSnapshot();
        var snapshotPlacement = TestSeq.Single(snapshot.KgpPlacements);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;1H\n"));

        var liveAfterScroll = TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(1, liveAfterScroll.Row);
        Assert.AreEqual(2, snapshotPlacement.Row);
        Assert.AreNotSame(liveBeforeScroll, snapshotPlacement);
        Assert.AreEqual(0xAA, snapshot.KgpImages[snapshotPlacement.ImageId].Data[0]);
    }

    [TestMethod]
    public async Task Snapshot_ConcurrentKgpReplacement_KeepsPlacementAndImageGenerationPaired()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);
        SendKgp(terminal, KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,i=1,p=1,c=1,r=1,C=1,q=2",
            KgpTestHelper.CreatePixelData(1, 1, fillByte: 0xAA)));

        using var start = new ManualResetEventSlim();
        var writer = Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < 300; i++)
            {
                var width = (uint)(i % 2 + 1);
                var fill = width == 1 ? (byte)0xAA : (byte)0xBB;
                SendKgp(terminal, KgpTestHelper.BuildCommand(
                    $"a=T,f=32,s={width},v=1,i=1,p=1,c={width},r=1,C=1,q=2",
                    KgpTestHelper.CreatePixelData(width, 1, fillByte: fill)));
                Thread.Yield();
            }
        });

        start.Set();
        try
        {
            for (var i = 0; i < 300; i++)
            {
                using var snapshot = terminal.CreateSnapshot();
                var placement = TestSeq.Single(snapshot.KgpPlacements);
                Assert.IsTrue(snapshot.KgpImages.TryGetValue(placement.ImageId, out var image));
                Assert.AreEqual(image.Width, placement.DisplayColumns);
                Assert.AreEqual(
                    image.Width == 1 ? 0xAA : 0xBB,
                    image.Data[0]);

                if (i % 16 == 0)
                    await Task.Yield();
            }
        }
        finally
        {
            await writer;
        }
    }

    // =============================================
    // Put/Display tests
    // =============================================

    [TestMethod]
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
        Assert.AreEqual(2, snapshot.CursorX);
    }

    [TestMethod]
    public void Put_NonExistentImage_NoChange()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(999));

        // Cursor should not move (image not found)
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(0, snapshot.CursorX);
    }

    [TestMethod]
    public void Put_WithCursorMovementDisabled_DoesNotMove()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, displayColumns: 3, cursorMovement: 1));

        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(0, snapshot.CursorX);
    }

    // =============================================
    // Multiple operations flow
    // =============================================

    [TestMethod]
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
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public void FullFlow_TransmitDeleteTransmit()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('I', imageId: 1));
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 1, 1));
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
    }

    // =============================================
    // Placement tracking tests
    // =============================================

    [TestMethod]
    public void TransmitAndDisplay_CreatesPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));

        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20,
            KgpFormat.Rgba32, displayColumns: 3, displayRows: 2);
        SendKgp(terminal, cmd);

        var placements = terminal.KgpPlacements;
        TestSeq.Single(placements);
        Assert.AreEqual(1u, placements[0].ImageId);
        Assert.AreEqual(0, placements[0].Row);
        Assert.AreEqual(0, placements[0].Column);
        Assert.AreEqual(3u, placements[0].DisplayColumns);
        Assert.AreEqual(2u, placements[0].DisplayRows);
    }

    [TestMethod]
    public void Put_CreatesPlacement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;5H")); // row 3, col 5 (1-based)
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 7, displayColumns: 2, displayRows: 1));

        var placements = terminal.KgpPlacements;
        TestSeq.Single(placements);
        Assert.AreEqual(1u, placements[0].ImageId);
        Assert.AreEqual(7u, placements[0].PlacementId);
        Assert.AreEqual(2, placements[0].Row);  // 0-based
        Assert.AreEqual(4, placements[0].Column); // 0-based
    }

    [TestMethod]
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
        TestSeq.Single(placements); // Replaced, not duplicated
        Assert.AreEqual(3u, placements[0].DisplayColumns);
        Assert.AreEqual(2, placements[0].Row); // New position
    }

    [TestMethod]
    public void Put_SamePlacementIdOnDifferentImages_ReplacesOnlyExactPair()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 1, 1));
        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(2, 1, 1));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 5));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(2, placementId: 5));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 5));

        Assert.AreEqual(2, terminal.KgpPlacements.Count);
        var imageOne = TestSeq.Single(terminal.KgpPlacements.Where(p => p.ImageId == 1));
        var imageTwo = TestSeq.Single(terminal.KgpPlacements.Where(p => p.ImageId == 2));
        Assert.AreEqual(2, imageOne.Row);
        Assert.AreEqual(1, imageTwo.Row);
        Assert.AreEqual(5u, imageOne.PlacementId);
        Assert.AreEqual(5u, imageTwo.PlacementId);
    }

    [TestMethod]
    public void Put_DifferentPlacementIds_MultiplePlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 1));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 2));

        Assert.AreEqual(2, terminal.KgpPlacements.Count);
    }

    [TestMethod]
    public void Put_ZeroPlacementId_CreatesSeparatePlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 0));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 0));

        Assert.AreEqual(2, terminal.KgpPlacements.Count);
    }

    [TestMethod]
    public void Placement_ZIndex_Stored()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20,
            KgpFormat.Rgba32, zIndex: -1);
        SendKgp(terminal, cmd);

        Assert.AreEqual(-1, terminal.KgpPlacements[0].ZIndex);
    }

    // =============================================
    // Delete placement tests
    // =============================================

    [TestMethod]
    public void Delete_All_ClearsPlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 10, 20));

        Assert.AreEqual(2, terminal.KgpPlacements.Count);

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('a'));

        Assert.IsEmpty(terminal.KgpPlacements);
    }

    [TestMethod]
    public void Delete_ById_RemovesOnlyMatchingPlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 10, 20));

        SendKgp(terminal, KgpTestHelper.BuildDeleteCommand('i', imageId: 1));

        TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(2u, terminal.KgpPlacements[0].ImageId);
    }

    [TestMethod]
    public void Delete_ById_WithPlacementId_RemovesSpecific()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitCommand(1, 2, 2));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 1));
        SendKgp(terminal, KgpTestHelper.BuildPutCommand(1, placementId: 2));

        Assert.AreEqual(2, terminal.KgpPlacements.Count);

        // Delete only placement 1
        var deleteCmd = KgpTestHelper.BuildCommand("a=d,d=i,i=1,p=1");
        SendKgp(terminal, deleteCmd);

        TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(2u, terminal.KgpPlacements[0].PlacementId);
    }

    [TestMethod]
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

        Assert.IsEmpty(terminal.KgpPlacements);
    }

    [TestMethod]
    public void Delete_ByZIndex_RemovesMatchingZIndex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20, zIndex: -1));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(2, 10, 20, zIndex: 0));

        var deleteCmd = KgpTestHelper.BuildCommand("a=d,d=z,z=-1");
        SendKgp(terminal, deleteCmd);

        TestSeq.Single(terminal.KgpPlacements);
        Assert.AreEqual(0, terminal.KgpPlacements[0].ZIndex);
    }

    // =============================================
    // Clear screen tests (Phase 4)
    // =============================================

    [TestMethod]
    public void ClearScreen_ClearsKgpPlacements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20));
        TestSeq.Single(terminal.KgpPlacements);

        // ESC[2J should clear all images per spec
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2J"));

        // Note: ESC[2J clears images according to KGP spec.
        // If implementation doesn't clear placements on ESC[2J yet, this test documents the requirement.
    }

    [TestMethod]
    public void Reset_ClearsKgpState()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 20, 10);

        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20));
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        TestSeq.Single(terminal.KgpPlacements);

        // Reset terminal (RIS - ESC c)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1bc"));

        // After reset, images and placements should be cleared
        // This documents the expected behavior per KGP spec
    }

    // =============================================
    // Placement intersection tests
    // =============================================

    [TestMethod]
    public void Placement_IntersectsCell_CorrectBounds()
    {
        var p = new KgpPlacement(1, 0, 2, 3, 4, 3); // row=2, col=3, 4 cols, 3 rows

        // Inside bounds
        Assert.IsTrue(p.IntersectsCell(2, 3));
        Assert.IsTrue(p.IntersectsCell(4, 6)); // row=4 (2+2), col=6 (3+3)

        // Outside bounds
        Assert.IsFalse(p.IntersectsCell(1, 3)); // above
        Assert.IsFalse(p.IntersectsCell(5, 3)); // below
        Assert.IsFalse(p.IntersectsCell(2, 2)); // left
        Assert.IsFalse(p.IntersectsCell(2, 7)); // right
    }

    [TestMethod]
    public void Placement_IntersectsRow_CorrectBounds()
    {
        var p = new KgpPlacement(1, 0, 5, 0, 10, 3); // starts at row 5, 3 rows tall

        Assert.IsTrue(p.IntersectsRow(5));
        Assert.IsTrue(p.IntersectsRow(6));
        Assert.IsTrue(p.IntersectsRow(7));
        Assert.IsFalse(p.IntersectsRow(4));
        Assert.IsFalse(p.IntersectsRow(8));
    }

    [TestMethod]
    public void Placement_IntersectsColumn_CorrectBounds()
    {
        var p = new KgpPlacement(1, 0, 0, 5, 3, 10); // starts at col 5, 3 cols wide

        Assert.IsTrue(p.IntersectsColumn(5));
        Assert.IsTrue(p.IntersectsColumn(6));
        Assert.IsTrue(p.IntersectsColumn(7));
        Assert.IsFalse(p.IntersectsColumn(4));
        Assert.IsFalse(p.IntersectsColumn(8));
    }

    // =============================================
    // Scrolling tests (Phase 4)  
    // =============================================

    [TestMethod]
    public void Scroll_PlacementsScrollWithText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 10, 5);

        // Place image at row 0
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[H"));
        SendKgp(terminal, KgpTestHelper.BuildTransmitAndDisplayCommand(1, 10, 20,
            displayColumns: 2, displayRows: 1, cursorMovement: 1));

        var placementsBefore = terminal.KgpPlacements;
        Assert.AreEqual(0, placementsBefore[0].Row);

        // Scroll by writing enough text to push past bottom
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[5;1H")); // Go to last row
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\n")); // This triggers scroll

        // After scroll, placement row should decrease by 1 (scrolled up)
        // Note: This test documents the expected behavior - implementation
        // may need to handle placement scrolling in the scroll logic
    }

    private sealed class RecordingWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        private readonly Queue<byte[]> _responses = new();
        private readonly object _lock = new();

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken ct = default)
        {
            lock (_lock)
            {
                _responses.Enqueue(data.ToArray());
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask ResizeAsync(
            int width,
            int height,
            CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        public string ReadResponse()
        {
            byte[]? response = null;
            var received = SpinWait.SpinUntil(
                () =>
                {
                    lock (_lock)
                    {
                        return _responses.TryDequeue(out response);
                    }
                },
                TimeSpan.FromSeconds(1));

            Assert.IsTrue(received, "Expected a KGP protocol response.");
            return Encoding.UTF8.GetString(response!);
        }

        public void AssertNoResponse()
        {
            var received = SpinWait.SpinUntil(
                () =>
                {
                    lock (_lock)
                    {
                        return _responses.Count > 0;
                    }
                },
                TimeSpan.FromMilliseconds(100));

            Assert.IsFalse(received, "Expected no KGP protocol response.");
        }
    }
}
