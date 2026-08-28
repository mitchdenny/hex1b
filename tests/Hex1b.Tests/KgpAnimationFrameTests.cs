using System.Text;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Conformance coverage derived from Kitty graphics.c/graphics.py and Ghostty
/// graphics_exec.zig at the revisions pinned by issue #403.
/// </summary>
[TestClass]
public class KgpAnimationFrameTests
{
    private static readonly TerminalCapabilities KgpCapabilities = new()
    {
        SupportsKgp = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20,
    };

    [TestMethod]
    public void AnimationFrame_FullRgba_AppendsFrameAndReturnsResolvedNumber()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 2,
            height: 1,
            KgpFormat.Rgba32,
            [10, 20, 30, 255, 40, 50, 60, 255]);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=2,v=1,i=1",
                [255, 0, 0, 255, 0, 255, 0, 255]));

        var image = GetImage(terminal, 1);
        Assert.AreEqual(2, image.FrameCount);
        AssertFrame(
            image,
            2,
            [255, 0, 0, 255, 0, 255, 0, 255],
            expectedGapMilliseconds: 40);
        Assert.AreEqual(16L, image.StorageSize);
        Assert.AreEqual(16L, terminal.KgpImageStore.TotalSize);
        Assert.AreEqual(
            "\x1b_Gi=1,r=2;OK\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void AnimationFrame_RgbRootAndPayload_NormalizesFramesToRgba()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgb24,
            [1, 2, 3]);

        SendKgp(
            terminal,
            FrameCommand("f=24,s=1,v=1,i=1,q=2", [4, 5, 6]));

        var image = GetImage(terminal, 1);
        Assert.AreEqual(KgpFormat.Rgba32, image.Format);
        AssertFrame(image, 1, [1, 2, 3, 255], 0);
        AssertFrame(image, 2, [4, 5, 6, 255], 40);
        Assert.AreEqual(8L, image.StorageSize);
        Assert.AreEqual(8L, terminal.KgpImageStore.TotalSize);
        workload.AssertNoResponse();
    }

    [TestMethod]
    public void AnimationFrame_PartialRectangle_FillsBackgroundInRgbaOrder()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 2,
            height: 2,
            KgpFormat.Rgba32,
            new byte[16]);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,x=1,y=0,X=1,Y=287454020,q=2",
                [170, 187, 204, 221]));

        AssertFrame(
            GetImage(terminal, 1),
            2,
            [
                17, 34, 51, 68,
                170, 187, 204, 221,
                17, 34, 51, 68,
                17, 34, 51, 68,
            ],
            40);
    }

    [TestMethod]
    public void AnimationFrame_AlphaBlend_UsesSourceOverAndTransparentNoOp()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [0, 0, 0, 0]);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,Y=1680998655,q=2",
                [200, 100, 50, 128]));
        AssertFrame(GetImage(terminal, 1), 2, [150, 75, 25, 255], 40);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,Y=1680998655,q=2",
                [255, 255, 255, 0]));
        AssertFrame(GetImage(terminal, 1), 3, [100, 50, 0, 255], 40);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,Y=1680998528,q=2",
                [200, 100, 50, 128]));
        AssertFrame(GetImage(terminal, 1), 4, [166, 83, 33, 191], 40);
    }

    [TestMethod]
    public void AnimationFrame_Overwrite_TransparentSourceReplacesBackground()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [0, 0, 0, 0]);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,X=1,Y=1680998655,q=2",
                [255, 255, 255, 0]));

        AssertFrame(GetImage(terminal, 1), 2, [255, 255, 255, 0], 40);
    }

    [TestMethod]
    public void AnimationFrame_OmittedDimensions_DefaultToWholeImage()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 2,
            height: 1,
            KgpFormat.Rgba32,
            new byte[8]);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,i=1,X=1,q=2",
                [1, 2, 3, 4, 5, 6, 7, 8]));

        AssertFrame(
            GetImage(terminal, 1),
            2,
            [1, 2, 3, 4, 5, 6, 7, 8],
            40);
    }

    [TestMethod]
    public void AnimationFrame_BaseFrame_ComposesOntoSelectedFrame()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 2,
            height: 1,
            KgpFormat.Rgba32,
            [0, 0, 255, 255, 0, 0, 255, 255]);
        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=2,v=1,i=1,X=1,q=2",
                [255, 0, 0, 255, 255, 0, 0, 255]));

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,c=2,x=1,X=1,q=2",
                [0, 255, 0, 255]));

        AssertFrame(
            GetImage(terminal, 1),
            3,
            [255, 0, 0, 255, 0, 255, 0, 255],
            40);
    }

    [TestMethod]
    public void AnimationFrame_EditFrame_ReplacesPixelsAndAppliesGapRules()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 2,
            height: 1,
            KgpFormat.Rgba32,
            new byte[8]);
        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=2,v=1,i=1,X=1,z=77,q=2",
                [1, 2, 3, 4, 5, 6, 7, 8]));

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,r=2,c=999,x=1,X=1,q=2",
                [9, 10, 11, 12]));
        AssertFrame(
            GetImage(terminal, 1),
            2,
            [1, 2, 3, 4, 9, 10, 11, 12],
            77);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,r=2,X=1,z=-1,q=2",
                [13, 14, 15, 16]));
        AssertFrame(
            GetImage(terminal, 1),
            2,
            [13, 14, 15, 16, 9, 10, 11, 12],
            0);
    }

    [TestMethod]
    public void AnimationFrame_EditRoot_UpdatesRootAndKeepsFrameCount()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgb24,
            [1, 2, 3]);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,r=1,X=1,z=25",
                [9, 8, 7, 6]));

        var image = GetImage(terminal, 1);
        Assert.AreEqual(1, image.FrameCount);
        AssertFrame(image, 1, [9, 8, 7, 6], 25);
        Assert.AreEqual(
            "\x1b_Gi=1,r=1;OK\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void AnimationFrame_OutOfRangeEditNumber_AppendsNextFrame()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [0, 0, 0, 0]);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,r=99,X=1",
                [1, 2, 3, 4]));

        Assert.AreEqual(2, GetImage(terminal, 1).FrameCount);
        Assert.AreEqual(
            "\x1b_Gi=1,r=2;OK\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void AnimationFrame_ImageNumber_TargetsNewestAndReturnsBothIdentities()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreNumberedBaseImage(terminal, 42, [1, 1, 1, 255]);
        var first = terminal.KgpImageStore.GetImageByNumber(42);
        Assert.IsNotNull(first);
        StoreNumberedBaseImage(terminal, 42, [2, 2, 2, 255]);
        var newest = terminal.KgpImageStore.GetImageByNumber(42);
        Assert.IsNotNull(newest);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,I=42,X=1",
                [9, 9, 9, 255]));

        Assert.AreEqual(1, first.FrameCount);
        Assert.AreEqual(2, GetImage(terminal, newest.ImageId).FrameCount);
        Assert.AreEqual(
            $"\x1b_Gi={newest.ImageId},I=42,r=2;OK\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void AnimationFrame_MissingIdentityAndImage_ReturnProtocolErrors()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1", [1, 2, 3, 4]));
        workload.AssertNoResponse();

        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=9", [1, 2, 3, 4]));
        Assert.AreEqual(
            "\x1b_Gi=9;ENOENT:Image not found\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void AnimationFrame_InvalidBaseDimensionsAndPayload_AreAtomic()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [1, 2, 3, 4]);
        var originalSize = terminal.KgpImageStore.TotalSize;

        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=1,c=3", [1, 2, 3, 4]));
        Assert.AreEqual(
            "\x1b_Gi=1,r=2;EINVAL:Base frame not found\x1b\\",
            workload.ReadResponse());

        SendKgp(
            terminal,
            FrameCommand("f=32,s=2,v=1,i=1", new byte[8]));
        Assert.AreEqual(
            "\x1b_Gi=1;EINVAL:Frame dimensions exceed image dimensions\x1b\\",
            workload.ReadResponse());

        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=1", [1, 2, 3]));
        Assert.AreEqual(
            "\x1b_Gi=1,r=2;ENODATA:Insufficient frame data: 3 < 4\x1b\\",
            workload.ReadResponse());

        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=1", [1, 2, 3, 4, 5]));
        Assert.AreEqual(
            "\x1b_Gi=1,r=2;EFBIG:Too much frame data: 5 > 4\x1b\\",
            workload.ReadResponse());

        var image = GetImage(terminal, 1);
        Assert.AreEqual(1, image.FrameCount);
        Assert.AreEqual(originalSize, terminal.KgpImageStore.TotalSize);
        TestSeq.AreEqual(new byte[] { 1, 2, 3, 4 }, image.Data);
    }

    [TestMethod]
    public void AnimationFrame_QuietModes_SuppressOnlyConfiguredResponses()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [1, 2, 3, 4]);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,X=1,q=1",
                [5, 6, 7, 8]));
        workload.AssertNoResponse();

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,c=99,q=1",
                [5, 6, 7, 8]));
        Assert.AreEqual(
            "\x1b_Gi=1,r=3;EINVAL:Base frame not found\x1b\\",
            workload.ReadResponse());

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,c=99,q=2",
                [5, 6, 7, 8]));
        workload.AssertNoResponse();
    }

    [TestMethod]
    [DataRow("t=f", "EINVAL:Animation frame transmission requires direct data")]
    [DataRow("o=z", "EINVAL:Animation frame compression is not supported")]
    [DataRow("f=100", "EINVAL:Animation frames require RGB or RGBA data")]
    public void AnimationFrame_UnsupportedTransferShape_ReturnsError(
        string control,
        string expectedError)
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [1, 2, 3, 4]);

        SendKgp(
            terminal,
            FrameCommand($"{control},s=1,v=1,i=1", [1, 2, 3, 4]));

        Assert.AreEqual(
            $"\x1b_Gi=1;{expectedError}\x1b\\",
            workload.ReadResponse());
        Assert.AreEqual(1, GetImage(terminal, 1).FrameCount);
    }

    [TestMethod]
    public void AnimationFrame_PngBaseWithoutDecoder_ReturnsError()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=t,f=100,i=1,q=2",
                [0x89, 0x50, 0x4E, 0x47]));

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1",
                [1, 2, 3, 4]));

        Assert.AreEqual(
            "\x1b_Gi=1;EINVAL:Animation requires a decoded RGB or RGBA base image\x1b\\",
            workload.ReadResponse());
        Assert.AreEqual(1, GetImage(terminal, 1).FrameCount);
    }

    [TestMethod]
    public void AnimationFrame_ChunkedUpload_MutatesAndRespondsOnlyAtFinalChunk()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [0, 0, 0, 0]);

        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=1,m=1", [1, 2, 3]));

        Assert.AreEqual(1, GetImage(terminal, 1).FrameCount);
        Assert.IsTrue(terminal.KgpImageStore.IsChunkedTransferInProgress);
        workload.AssertNoResponse();

        SendKgp(
            terminal,
            FrameCommand("m=0", [4]));

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        AssertFrame(GetImage(terminal, 1), 2, [1, 2, 3, 4], 40);
        Assert.AreEqual(
            "\x1b_Gi=1,r=2;OK\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void AnimationFrame_ChunkedUpload_RetainsFirstMetadataAndQuiet()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [0, 0, 0, 0]);

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,X=1,z=91,m=1,q=1",
                [9, 8, 7]));
        SendKgp(
            terminal,
            FrameCommand("m=0", [6]));

        AssertFrame(GetImage(terminal, 1), 2, [9, 8, 7, 6], 91);
        workload.AssertNoResponse();
    }

    [TestMethod]
    [DataRow("m=0")]
    [DataRow("a=f,m=0,r=2")]
    public void AnimationFrame_InvalidContinuation_AbortsWithoutMutation(
        string continuationControls)
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [0, 0, 0, 0]);
        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=1,m=1", [1, 2, 3]));

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(continuationControls, [4]));

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        Assert.AreEqual(1, GetImage(terminal, 1).FrameCount);
        Assert.AreEqual(
            "\x1b_Gi=1,r=2;EINVAL:Invalid chunk continuation controls\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void AnimationFrame_OrphanContinuation_ReturnsSequenceError()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        SendKgp(
            terminal,
            FrameCommand("m=0", [1, 2, 3, 4]));

        workload.AssertNoResponse();
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public void AnimationFrame_ChunkedPayloadExceedsExpectedSize_AbortsAtomically()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [0, 0, 0, 0]);
        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=1,m=1", [1, 2, 3]));

        SendKgp(
            terminal,
            FrameCommand("m=0", [4, 5]));

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        Assert.AreEqual(1, GetImage(terminal, 1).FrameCount);
        Assert.AreEqual(
            "\x1b_Gi=1,r=2;EFBIG:Too much frame data: 5 > 4\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void DeleteAnimationFrame_AbortsPendingFrameUploadBeforeDeletion()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [1, 2, 3, 4]);
        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=1,m=1", [5, 6, 7]));
        Assert.IsTrue(terminal.KgpImageStore.IsChunkedTransferInProgress);

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=f,i=1"));

        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        Assert.AreEqual(1, GetImage(terminal, 1).FrameCount);
        workload.AssertNoResponse();
    }

    [TestMethod]
    public void AnimationFrame_InvalidBase64_ReturnsErrorWithoutMutation()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [0, 0, 0, 0]);

        SendKgp(
            terminal,
            "\x1b_Ga=f,f=32,s=1,v=1,i=1;%%%\x1b\\");

        Assert.AreEqual(1, GetImage(terminal, 1).FrameCount);
        Assert.AreEqual(
            "\x1b_Gi=1,r=2;EINVAL:Invalid Base64 payload\x1b\\",
            workload.ReadResponse());
    }

    [TestMethod]
    public void DeleteAnimationFrame_Root_PromotesSecondFrameAndRepairsAccounting()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [1, 1, 1, 255]);
        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,X=1,z=70,q=2",
                [2, 2, 2, 255]));
        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,X=1,z=80,q=2",
                [3, 3, 3, 255]));
        Assert.AreEqual(12L, terminal.KgpImageStore.TotalSize);

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=f,i=1,r=1"));

        var image = GetImage(terminal, 1);
        Assert.AreEqual(2, image.FrameCount);
        AssertFrame(image, 1, [2, 2, 2, 255], 70);
        AssertFrame(image, 2, [3, 3, 3, 255], 80);
        Assert.AreEqual(8L, terminal.KgpImageStore.TotalSize);
        workload.AssertNoResponse();
    }

    [TestMethod]
    public void DeleteAnimationFrame_NonRootAndOutOfRange_DeleteSelectedFrames()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [1, 1, 1, 255]);
        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,X=1,q=2",
                [2, 2, 2, 255]));
        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,X=1,q=2",
                [3, 3, 3, 255]));

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=F,i=1,r=2"));
        var image = GetImage(terminal, 1);
        Assert.AreEqual(2, image.FrameCount);
        AssertFrame(image, 2, [3, 3, 3, 255], 40);

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=f,i=1,r=99"));
        image = GetImage(terminal, 1);
        Assert.AreEqual(1, image.FrameCount);
        AssertFrame(image, 1, [1, 1, 1, 255], 0);
        Assert.AreEqual(4L, terminal.KgpImageStore.TotalSize);
    }

    [TestMethod]
    public void DeleteAnimationFrame_ImageNumber_AlwaysTargetsNewestImage()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreNumberedBaseImage(terminal, 42, [1, 1, 1, 255]);
        var older = terminal.KgpImageStore.GetImageByNumber(42);
        Assert.IsNotNull(older);
        StoreNumberedBaseImage(terminal, 42, [2, 2, 2, 255]);
        var newer = terminal.KgpImageStore.GetImageByNumber(42);
        Assert.IsNotNull(newer);
        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,I=42,X=1,q=2",
                [3, 3, 3, 255]));

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=f,I=42,r=2"));

        Assert.AreEqual(1, GetImage(terminal, older.ImageId).FrameCount);
        Assert.AreEqual(1, GetImage(terminal, newer.ImageId).FrameCount);

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=F,I=42"));

        Assert.IsNull(terminal.KgpImageStore.GetImageById(newer.ImageId));
        Assert.AreSame(
            terminal.KgpImageStore.GetImageById(older.ImageId),
            terminal.KgpImageStore.GetImageByNumber(42));
    }

    [TestMethod]
    public void DeleteAnimationFrame_SingleFrame_LowercaseNoOpUppercaseCascadesGraph()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 10, height: 5);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [1, 1, 1, 255]);
        StoreBaseImage(
            terminal,
            imageId: 2,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [2, 2, 2, 255]);
        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=p,i=1,p=10,c=1,r=1,C=1,q=2"));
        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=p,i=2,p=20,c=1,r=1,P=1,Q=10,H=1,V=0,C=1,q=2"));
        Assert.AreEqual(2, terminal.KgpPlacements.Count);

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=f,i=1"));

        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(2, terminal.KgpPlacements.Count);

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=F,i=1"));

        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.IsNull(terminal.KgpImageStore.GetImageById(2));
        Assert.IsEmpty(terminal.KgpPlacements);
        Assert.AreEqual(0L, terminal.KgpImageStore.TotalSize);
    }

    [TestMethod]
    public void DeleteAnimationFrame_UppercaseSingleFrame_RemovesVirtualOwner()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 4, height: 2);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 10,
            height: 20,
            KgpFormat.Rgb24,
            new byte[10 * 20 * 3]);
        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=p,U=1,i=1,p=7,c=1,r=1,q=2"));
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=F,i=1"));

        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(0, terminal.KgpVirtualPlacementCount);
        Assert.AreEqual(0, terminal.GetKgpVirtualReferenceCount(1));
    }

    [TestMethod]
    public void DeleteAnimationFrame_UppercaseSingleFrame_RemovesHistoryOwner()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(
            workload,
            width: 4,
            height: 2,
            scrollbackCapacity: 2);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 10,
            height: 20,
            KgpFormat.Rgb24,
            new byte[10 * 20 * 3]);
        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=p,i=1,p=7,c=1,r=1,C=1,q=2"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[S"));
        Assert.AreEqual(1, terminal.KgpHistoryPlacementCount);

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand("a=d,d=F,i=1"));

        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(0, terminal.KgpHistoryPlacementCount);
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1);
        Assert.IsEmpty(snapshot.KgpPlacements);
        Assert.IsEmpty(snapshot.KgpImages);
    }

    [TestMethod]
    public void AnimationFrame_ExplicitBaseRetransmission_ResetsAnimationState()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [1, 2, 3, 4]);
        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=1,q=2", [5, 6, 7, 8]));
        Assert.AreEqual(2, GetImage(terminal, 1).FrameCount);

        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [9, 10, 11, 12]);

        var image = GetImage(terminal, 1);
        Assert.AreEqual(1, image.FrameCount);
        Assert.IsNull(image.AnimationFrames);
        TestSeq.AreEqual(new byte[] { 9, 10, 11, 12 }, image.Data);
        Assert.AreEqual(4L, terminal.KgpImageStore.TotalSize);
    }

    [TestMethod]
    public void AnimationFrame_InvalidExplicitRetransmission_RemovesAnimationAndPlacements()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 5, height: 3);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [1, 2, 3, 4]);
        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=1,q=2", [5, 6, 7, 8]));
        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=p,i=1,p=1,c=1,r=1,C=1,q=2"));

        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=t,f=32,s=2,v=1,i=1",
                [9, 10, 11, 12]));

        Assert.AreEqual(
            "\x1b_Gi=1;ENODATA:Insufficient image data: 4 < 8\x1b\\",
            workload.ReadResponse());
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(0L, terminal.KgpImageStore.TotalSize);
        Assert.IsEmpty(terminal.KgpPlacements);
    }

    [TestMethod]
    public void AnimationFrame_MainAndAlternateScreens_AreIndependent()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [1, 1, 1, 255]);
        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=1,q=2", [2, 2, 2, 255]));
        var mainStore = terminal.KgpImageStore;

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h"));
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [3, 3, 3, 255]);
        var alternateStore = terminal.KgpImageStore;
        Assert.AreNotSame(mainStore, alternateStore);
        Assert.AreEqual(1, GetImage(terminal, 1).FrameCount);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));

        Assert.AreSame(mainStore, terminal.KgpImageStore);
        Assert.AreEqual(2, GetImage(terminal, 1).FrameCount);
        Assert.AreEqual(0, alternateStore.ImageCount);
        Assert.AreEqual(0L, alternateStore.TotalSize);
    }

    [TestMethod]
    public void AnimationFrame_RootEdit_PreservesEarlierSnapshotAndUpdatesSvg()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 5, height: 3);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [255, 0, 0, 255]);
        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=p,i=1,p=1,c=1,r=1,C=1,q=2"));
        using var before = terminal.CreateSnapshot();
        var beforeSvg = before.ToSvg();

        SendKgp(
            terminal,
            FrameCommand(
                "f=32,s=1,v=1,i=1,r=1,X=1,q=2",
                [0, 0, 255, 255]));
        using var after = terminal.CreateSnapshot();
        var afterSvg = after.ToSvg();

        TestSeq.AreEqual(
            new byte[] { 255, 0, 0, 255 },
            before.KgpImages[1].CurrentFrameData);
        TestSeq.AreEqual(
            new byte[] { 0, 0, 255, 255 },
            after.KgpImages[1].CurrentFrameData);
        Assert.AreNotEqual(beforeSvg, afterSvg);
        Assert.Contains("data-image-id=\"1\"", afterSvg);
    }

    [TestMethod]
    public void AnimationFrame_SnapshotAndSvg_UseCurrentFramePlaceholder()
    {
        var workload = new RecordingWorkloadAdapter();
        using var terminal = CreateTerminal(workload, width: 5, height: 3);
        var root = new KgpAnimationFrame([255, 0, 0, 255], 0);
        var current = new KgpAnimationFrame([0, 0, 255, 255], 40);
        var image = new KgpImageData(
            1,
            0,
            root.Data,
            1,
            1,
            KgpFormat.Rgba32).WithAnimation(
                new KgpAnimationState([root, current], currentFrameIndex: 1));
        terminal.KgpImageStore.StoreImage(image);
        SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(
                "a=p,i=1,p=1,c=1,r=1,C=1,q=2"));

        using var snapshot = terminal.CreateSnapshot();
        var captured = snapshot.KgpImages[1];

        Assert.AreEqual(2, captured.FrameCount);
        Assert.AreEqual(2, captured.CurrentFrameNumber);
        TestSeq.AreEqual(
            new byte[] { 0, 0, 255, 255 },
            captured.CurrentFrameData);
        Assert.Contains("data-image-id=\"1\"", snapshot.ToSvg());
    }

    [TestMethod]
    public void Dispose_WithAnimationFrames_ReleasesPerScreenStorage()
    {
        var workload = new RecordingWorkloadAdapter();
        var terminal = CreateTerminal(workload);
        StoreBaseImage(
            terminal,
            imageId: 1,
            width: 1,
            height: 1,
            KgpFormat.Rgba32,
            [1, 2, 3, 4]);
        SendKgp(
            terminal,
            FrameCommand("f=32,s=1,v=1,i=1,q=2", [5, 6, 7, 8]));
        var store = terminal.KgpImageStore;
        Assert.AreEqual(8L, store.TotalSize);

        terminal.Dispose();

        Assert.AreEqual(0, store.ImageCount);
        Assert.AreEqual(0L, store.TotalSize);
    }

    private static Hex1bTerminal CreateTerminal(
        IHex1bTerminalWorkloadAdapter workload,
        int width = 80,
        int height = 24,
        int? scrollbackCapacity = null)
    {
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(KgpCapabilities)
            .WithDimensions(width, height);
        if (scrollbackCapacity is { } capacity)
            builder.WithScrollback(capacity);
        return builder.Build();
    }

    private static void StoreBaseImage(
        Hex1bTerminal terminal,
        uint imageId,
        uint width,
        uint height,
        KgpFormat format,
        byte[] data)
        => SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(
                $"a=t,f={(int)format},s={width},v={height},i={imageId},q=2",
                data));

    private static void StoreNumberedBaseImage(
        Hex1bTerminal terminal,
        uint imageNumber,
        byte[] data)
        => SendKgp(
            terminal,
            KgpTestHelper.BuildCommand(
                $"a=t,f=32,s=1,v=1,I={imageNumber},q=2",
                data));

    private static string FrameCommand(string controls, byte[] data)
        => KgpTestHelper.BuildCommand($"a=f,{controls}", data);

    private static void SendKgp(
        Hex1bTerminal terminal,
        string escapeSequence)
        => terminal.ApplyTokens(AnsiTokenizer.Tokenize(escapeSequence));

    private static KgpImageData GetImage(
        Hex1bTerminal terminal,
        uint imageId)
    {
        var image = terminal.KgpImageStore.GetImageById(imageId);
        Assert.IsNotNull(image);
        return image;
    }

    private static void AssertFrame(
        KgpImageData image,
        int frameNumber,
        byte[] expectedData,
        int expectedGapMilliseconds)
    {
        Assert.IsTrue(
            image.TryGetFrame(
                frameNumber,
                out var data,
                out var format,
                out var gapMilliseconds));
        Assert.AreEqual(KgpFormat.Rgba32, format);
        Assert.AreEqual(expectedGapMilliseconds, gapMilliseconds);
        TestSeq.AreEqual(expectedData, data);
    }

    private sealed class RecordingWorkloadAdapter :
        IHex1bTerminalWorkloadAdapter
    {
        private readonly Queue<byte[]> _responses = new();
        private readonly object _lock = new();

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(
            CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken ct = default)
        {
            lock (_lock)
                _responses.Enqueue(data.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask ResizeAsync(
            int width,
            int height,
            CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        internal string ReadResponse()
        {
            byte[]? response = null;
            var received = SpinWait.SpinUntil(
                () =>
                {
                    lock (_lock)
                        return _responses.TryDequeue(out response);
                },
                TimeSpan.FromSeconds(1));
            Assert.IsTrue(received, "Expected a KGP protocol response.");
            return Encoding.UTF8.GetString(response!);
        }

        internal void AssertNoResponse()
        {
            var received = SpinWait.SpinUntil(
                () =>
                {
                    lock (_lock)
                        return _responses.Count > 0;
                },
                TimeSpan.FromMilliseconds(100));
            Assert.IsFalse(received, "Expected no KGP protocol response.");
        }
    }
}
