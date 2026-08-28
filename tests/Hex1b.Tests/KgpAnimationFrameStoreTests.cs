namespace Hex1b.Tests;

[TestClass]
public class KgpAnimationFrameStoreTests
{
    [TestMethod]
    public void StoreAnimationFrame_NewFrameExceedsQuota_ReturnsNoSpaceWithoutMutation()
    {
        var store = new KgpImageStore(quotaBytes: 4);
        var original = CreateImage(1, KgpFormat.Rgba32, [1, 2, 3, 4]);
        store.StoreImage(original);

        var result = store.StoreAnimationFrame(
            ParseFrame("a=f,f=32,s=1,v=1,i=1,X=1"),
            [5, 6, 7, 8]);

        Assert.AreEqual(
            KgpImageStore.AnimationFrameStatus.NoSpace,
            result.Info.Status);
        Assert.AreSame(original, store.GetImageById(1));
        Assert.AreEqual(1, original.FrameCount);
        Assert.AreEqual(4L, store.TotalSize);
    }

    [TestMethod]
    public void StoreAnimationFrame_RootEditAtQuota_SucceedsWithoutGrowth()
    {
        var store = new KgpImageStore(quotaBytes: 4);
        store.StoreImage(
            CreateImage(1, KgpFormat.Rgba32, [1, 2, 3, 4]));

        var result = store.StoreAnimationFrame(
            ParseFrame("a=f,f=32,s=1,v=1,i=1,r=1,X=1"),
            [5, 6, 7, 8]);

        Assert.AreEqual(
            KgpImageStore.AnimationFrameStatus.Success,
            result.Info.Status);
        Assert.AreEqual(4L, store.TotalSize);
        AssertFrame(result.Image!, 1, [5, 6, 7, 8], 0);
    }

    [TestMethod]
    public void StoreAnimationFrame_RgbNormalizationExceedsQuota_IsAtomic()
    {
        var store = new KgpImageStore(quotaBytes: 3);
        var original = CreateImage(1, KgpFormat.Rgb24, [1, 2, 3]);
        store.StoreImage(original);

        var result = store.StoreAnimationFrame(
            ParseFrame("a=f,f=32,s=1,v=1,i=1,r=1,X=1"),
            [5, 6, 7, 8]);

        Assert.AreEqual(
            KgpImageStore.AnimationFrameStatus.NoSpace,
            result.Info.Status);
        Assert.AreSame(original, store.GetImageById(1));
        Assert.AreEqual(KgpFormat.Rgb24, original.Format);
        Assert.AreEqual(3L, store.TotalSize);
    }

    [TestMethod]
    public void StoreAnimationFrame_MalformedPublicBaseImage_ReturnsInvalidBaseData()
    {
        var store = new KgpImageStore();
        var malformed = new KgpImageData(
            1,
            0,
            [1, 2, 3],
            width: 2,
            height: 1,
            KgpFormat.Rgba32);
        store.StoreImage(malformed);

        var result = store.StoreAnimationFrame(
            ParseFrame("a=f,f=32,s=1,v=1,i=1,r=1,X=1"),
            [5, 6, 7, 8]);

        Assert.AreEqual(
            KgpImageStore.AnimationFrameStatus.InvalidBaseData,
            result.Info.Status);
        Assert.AreSame(malformed, store.GetImageById(1));
        Assert.AreEqual(3L, store.TotalSize);
    }

    [TestMethod]
    public void StoreAnimationFrame_EditAndAppend_TracksRetainedBytes()
    {
        var store = new KgpImageStore();
        store.StoreImage(
            CreateImage(1, KgpFormat.Rgba32, [1, 2, 3, 4]));

        var appended = store.StoreAnimationFrame(
            ParseFrame("a=f,f=32,s=1,v=1,i=1,X=1"),
            [5, 6, 7, 8]);
        Assert.AreEqual(
            KgpImageStore.AnimationFrameStatus.Success,
            appended.Info.Status);
        Assert.AreEqual(8L, store.TotalSize);

        var edited = store.StoreAnimationFrame(
            ParseFrame("a=f,f=32,s=1,v=1,i=1,r=2,X=1"),
            [9, 10, 11, 12]);
        Assert.AreEqual(
            KgpImageStore.AnimationFrameStatus.Success,
            edited.Info.Status);
        Assert.AreEqual(8L, store.TotalSize);
        AssertFrame(edited.Image!, 2, [9, 10, 11, 12], 40);
    }

    [TestMethod]
    [DataRow(2, 1, 3, true)]
    [DataRow(1, 2, 3, true)]
    [DataRow(2, 3, 2, true)]
    public void DeleteAnimationFrame_RepairsCurrentFrameIndex(
        int currentFrameIndex,
        int deletedFrameNumber,
        int expectedCurrentValue,
        bool expectedCurrentFrameChanged)
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateAnimatedImage(1, currentFrameIndex));

        var result = store.DeleteAnimationFrame(
            imageId: 1,
            imageNumber: 0,
            checked((uint)deletedFrameNumber),
            freeData: false);

        Assert.AreEqual(
            KgpImageStore.AnimationFrameDeleteStatus.Deleted,
            result.Status);
        Assert.AreEqual(
            expectedCurrentFrameChanged,
            result.CurrentFrameChanged);
        Assert.IsNotNull(result.Image);
        Assert.AreEqual(2, result.Image.FrameCount);
        Assert.AreEqual(
            expectedCurrentValue,
            (int)result.Image.CurrentFrameData[0]);
        Assert.AreEqual(8L, store.TotalSize);
    }

    [TestMethod]
    [DataRow(0u, 1u, 2)]
    [DataRow(99u, 3u, 2)]
    public void DeleteAnimationFrame_FrameNumber_ClampsToStoredRange(
        uint requestedFrameNumber,
        uint expectedDeletedFrameNumber,
        int expectedFrameCount)
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateAnimatedImage(1, currentFrameIndex: 0));

        var result = store.DeleteAnimationFrame(
            imageId: 1,
            imageNumber: 0,
            requestedFrameNumber,
            freeData: false);

        Assert.AreEqual(
            KgpImageStore.AnimationFrameDeleteStatus.Deleted,
            result.Status);
        Assert.AreEqual(expectedDeletedFrameNumber, result.FrameNumber);
        Assert.AreEqual(expectedFrameCount, result.Image!.FrameCount);
    }

    [TestMethod]
    public void DeleteAnimationFrame_ByNumber_UsesNewestImage()
    {
        var store = new KgpImageStore();
        var older = CreateAnimatedImage(1, currentFrameIndex: 0, imageNumber: 42);
        var newer = CreateAnimatedImage(2, currentFrameIndex: 0, imageNumber: 42);
        store.StoreImage(older);
        store.StoreImage(newer);

        var result = store.DeleteAnimationFrame(
            imageId: 0,
            imageNumber: 42,
            frameNumber: 2,
            freeData: false);

        Assert.AreEqual(2u, result.ImageId);
        Assert.AreEqual(3, older.FrameCount);
        Assert.AreEqual(2, store.GetImageById(2)!.FrameCount);
    }

    [TestMethod]
    public void DeleteAnimationFrame_SingleFrameHonorsUppercaseBehavior()
    {
        var store = new KgpImageStore();
        var image = CreateImage(1, KgpFormat.Rgba32, [1, 2, 3, 4]);
        store.StoreImage(image);

        var lowercase = store.DeleteAnimationFrame(
            imageId: 1,
            imageNumber: 0,
            frameNumber: 0,
            freeData: false);
        Assert.AreEqual(
            KgpImageStore.AnimationFrameDeleteStatus.NoOp,
            lowercase.Status);
        Assert.AreSame(image, store.GetImageById(1));

        var uppercase = store.DeleteAnimationFrame(
            imageId: 1,
            imageNumber: 0,
            frameNumber: 0,
            freeData: true);
        Assert.AreEqual(
            KgpImageStore.AnimationFrameDeleteStatus.ImageRemoved,
            uppercase.Status);
        Assert.IsNull(store.GetImageById(1));
        Assert.AreEqual(0L, store.TotalSize);
    }

    [TestMethod]
    public void StoreAnimationFrame_OutOfBoundsRectangle_CreatesBackgroundOnlyFrame()
    {
        var store = new KgpImageStore();
        store.StoreImage(new KgpImageData(
            1,
            0,
            new byte[8],
            2,
            1,
            KgpFormat.Rgba32));

        var result = store.StoreAnimationFrame(
            ParseFrame(
                "a=f,f=32,s=1,v=1,i=1,x=4294967295,y=4294967295," +
                "X=1,Y=16909060"),
            [9, 9, 9, 9]);

        Assert.AreEqual(
            KgpImageStore.AnimationFrameStatus.Success,
            result.Info.Status);
        AssertFrame(
            result.Image!,
            2,
            [1, 2, 3, 4, 1, 2, 3, 4],
            40);
    }

    [TestMethod]
    public void Clear_WithAnimationFrames_ReleasesAllRetainedBytes()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateAnimatedImage(1, currentFrameIndex: 0));
        Assert.AreEqual(12L, store.TotalSize);

        store.Clear();

        Assert.AreEqual(0, store.ImageCount);
        Assert.AreEqual(0L, store.TotalSize);
    }

    [TestMethod]
    public void StoreImage_QuotaEviction_RemovesAllAnimationFrameBytes()
    {
        var store = new KgpImageStore(quotaBytes: 12);
        store.StoreImage(CreateAnimatedImageWithTwoFrames(1));
        Assert.AreEqual(8L, store.TotalSize);

        store.StoreImage(new KgpImageData(
            2,
            0,
            new byte[8],
            width: 2,
            height: 1,
            KgpFormat.Rgba32));

        Assert.IsNull(store.GetImageById(1));
        Assert.IsNotNull(store.GetImageById(2));
        Assert.AreEqual(8L, store.TotalSize);
    }

    [TestMethod]
    public void StoreImage_LargerAnimatedReplacement_DoesNotDoubleSubtractBytes()
    {
        var store = new KgpImageStore(quotaBytes: 8);
        store.StoreImage(CreateAnimatedImageWithTwoFrames(1));
        Assert.AreEqual(8L, store.TotalSize);

        store.StoreImage(new KgpImageData(
            1,
            0,
            new byte[12],
            width: 3,
            height: 1,
            KgpFormat.Rgba32));

        Assert.AreEqual(1, store.ImageCount);
        Assert.AreEqual(12L, store.TotalSize);
        Assert.AreEqual(12, store.GetImageById(1)!.Data.Length);
    }

    [TestMethod]
    public void ConcurrentFrameStores_OnIndependentImages_AreDeterministic()
    {
        const int imageCount = 32;
        var store = new KgpImageStore();
        for (uint imageId = 1; imageId <= imageCount; imageId++)
        {
            store.StoreImage(
                CreateImage(
                    imageId,
                    KgpFormat.Rgba32,
                    [(byte)imageId, 0, 0, 255]));
        }

        var tasks = Enumerable.Range(1, imageCount)
            .Select(imageId => Task.Run(() => store.StoreAnimationFrame(
                ParseFrame($"a=f,f=32,s=1,v=1,i={imageId},X=1"),
                [(byte)imageId, 1, 2, 255])))
            .ToArray();

        Task.WaitAll(tasks);

        TestSeq.All(
            tasks,
            task => Assert.AreEqual(
                KgpImageStore.AnimationFrameStatus.Success,
                task.Result.Info.Status));
        Assert.AreEqual(imageCount * 8L, store.TotalSize);
        for (uint imageId = 1; imageId <= imageCount; imageId++)
            Assert.AreEqual(2, store.GetImageById(imageId)!.FrameCount);
    }

    private static KgpImageData CreateImage(
        uint imageId,
        KgpFormat format,
        byte[] data,
        uint imageNumber = 0)
        => new(
            imageId,
            imageNumber,
            data,
            1,
            1,
            format);

    private static KgpImageData CreateAnimatedImage(
        uint imageId,
        int currentFrameIndex,
        uint imageNumber = 0)
    {
        var root = new KgpAnimationFrame([1, 0, 0, 255], 0);
        var frames = new[]
        {
            root,
            new KgpAnimationFrame([2, 0, 0, 255], 20),
            new KgpAnimationFrame([3, 0, 0, 255], 30),
        };
        var image = CreateImage(
            imageId,
            KgpFormat.Rgba32,
            root.Data,
            imageNumber);
        return image.WithAnimation(
            new KgpAnimationState(frames, currentFrameIndex));
    }

    private static KgpImageData CreateAnimatedImageWithTwoFrames(uint imageId)
    {
        var root = new KgpAnimationFrame([1, 0, 0, 255], 0);
        var image = CreateImage(
            imageId,
            KgpFormat.Rgba32,
            root.Data);
        return image.WithAnimation(
            new KgpAnimationState(
                [root, new KgpAnimationFrame([2, 0, 0, 255], 40)],
                currentFrameIndex: 0));
    }

    private static KgpParsedCommand.AnimationFrame ParseFrame(
        string controlData)
    {
        var success = KgpCommandParser.TryParse(
            controlData,
            out var command,
            out var failure);
        Assert.IsTrue(
            success,
            success ? null : failure.FormatReason(controlData.AsSpan()));
        return TestSeq.IsType<KgpParsedCommand.AnimationFrame>(command);
    }

    private static void AssertFrame(
        KgpImageData image,
        int frameNumber,
        byte[] expectedData,
        int expectedGapMilliseconds)
    {
        Assert.IsTrue(image.TryGetFrame(
            frameNumber,
            out var data,
            out var format,
            out var gapMilliseconds));
        Assert.AreEqual(KgpFormat.Rgba32, format);
        Assert.AreEqual(expectedGapMilliseconds, gapMilliseconds);
        TestSeq.AreEqual(expectedData, data);
    }
}
