
namespace Hex1b.Tests;

[TestClass]
public class KgpImageStoreTests
{
    private static KgpImageData CreateImage(uint id, uint width = 2, uint height = 2, uint number = 0, byte fill = 0xFF)
    {
        var data = KgpTestHelper.CreatePixelData(width, height, KgpFormat.Rgba32, fill);
        return new KgpImageData(id, number, data, width, height, KgpFormat.Rgba32);
    }

    // --- Basic store/retrieve ---

    [TestMethod]
    public void StoreImage_StoresAndRetrieves()
    {
        var store = new KgpImageStore();
        var image = CreateImage(1);

        store.StoreImage(image);

        Assert.AreEqual(1, store.ImageCount);
        Assert.AreSame(image, store.GetImageById(1));
    }

    [TestMethod]
    public void GetImageById_NonExistent_ReturnsNull()
    {
        var store = new KgpImageStore();
        Assert.IsNull(store.GetImageById(999));
    }

    [TestMethod]
    public void StoreImage_ReplacesSameId()
    {
        var store = new KgpImageStore();
        var image1 = CreateImage(1, fill: 0xAA);
        var image2 = CreateImage(1, fill: 0xBB);

        store.StoreImage(image1);
        store.StoreImage(image2);

        Assert.AreEqual(1, store.ImageCount);
        Assert.AreSame(image2, store.GetImageById(1));
    }

    [TestMethod]
    public void StoreImage_MultipleImages()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1));
        store.StoreImage(CreateImage(2));
        store.StoreImage(CreateImage(3));

        Assert.AreEqual(3, store.ImageCount);
        Assert.IsNotNull(store.GetImageById(1));
        Assert.IsNotNull(store.GetImageById(2));
        Assert.IsNotNull(store.GetImageById(3));
    }

    // --- Image numbers ---

    [TestMethod]
    public void StoreImage_WithNumber_RetrievableByNumber()
    {
        var store = new KgpImageStore();
        var image = CreateImage(1, number: 42);
        store.StoreImage(image);

        Assert.AreSame(image, store.GetImageByNumber(42));
    }

    [TestMethod]
    public void GetImageByNumber_ReturnsNewest()
    {
        var store = new KgpImageStore();
        var image1 = CreateImage(1, number: 42);
        var image2 = CreateImage(2, number: 42);
        store.StoreImage(image1);
        store.StoreImage(image2);

        // GetImageByNumber returns the newest (last added)
        Assert.AreSame(image2, store.GetImageByNumber(42));
    }

    [TestMethod]
    public void GetImageByNumber_NonExistent_ReturnsNull()
    {
        var store = new KgpImageStore();
        Assert.IsNull(store.GetImageByNumber(999));
    }

    // --- Removal ---

    [TestMethod]
    public void RemoveImage_RemovesById()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1));

        Assert.IsTrue(store.RemoveImage(1));
        Assert.AreEqual(0, store.ImageCount);
        Assert.IsNull(store.GetImageById(1));
    }

    [TestMethod]
    public void RemoveImage_NonExistent_ReturnsFalse()
    {
        var store = new KgpImageStore();
        Assert.IsFalse(store.RemoveImage(999));
    }

    [TestMethod]
    public void RemoveImageByNumber_RemovesNewest()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1, number: 42));
        store.StoreImage(CreateImage(2, number: 42));

        Assert.IsTrue(store.RemoveImageByNumber(42));
        Assert.AreEqual(1, store.ImageCount);
        // Image 1 (first with number 42) should still exist
        Assert.IsNotNull(store.GetImageById(1));
        Assert.IsNull(store.GetImageById(2));
    }

    [TestMethod]
    public void RemoveImage_UpdatesNumberIndex()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1, number: 42));
        store.StoreImage(CreateImage(2, number: 42));

        store.RemoveImage(2);

        // After removing image 2, the newest with number 42 is image 1
        Assert.AreSame(store.GetImageById(1), store.GetImageByNumber(42));
    }

    // --- Clear ---

    [TestMethod]
    public void Clear_RemovesAll()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1));
        store.StoreImage(CreateImage(2));

        store.Clear();

        Assert.AreEqual(0, store.ImageCount);
        Assert.AreEqual(0, store.TotalSize);
    }

    // --- ID allocation ---

    [TestMethod]
    public void AllocateId_ReturnsUniqueIds()
    {
        var store = new KgpImageStore();
        var id1 = store.AllocateId();
        var id2 = store.AllocateId();
        var id3 = store.AllocateId();

        Assert.AreNotEqual(id1, id2);
        Assert.AreNotEqual(id2, id3);
        Assert.IsTrue(id1 > 0);
    }

    // --- TotalSize tracking ---

    [TestMethod]
    public void TotalSize_TracksCorrectly()
    {
        var store = new KgpImageStore();

        store.StoreImage(CreateImage(1, 2, 2)); // 2*2*4 = 16 bytes
        Assert.AreEqual(16, store.TotalSize);

        store.StoreImage(CreateImage(2, 3, 3)); // 3*3*4 = 36 bytes
        Assert.AreEqual(52, store.TotalSize);

        store.RemoveImage(1);
        Assert.AreEqual(36, store.TotalSize);
    }

    [TestMethod]
    public void TotalSize_ReplacingImage_UpdatesCorrectly()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1, 2, 2)); // 16 bytes
        store.StoreImage(CreateImage(1, 3, 3)); // replace with 36 bytes

        Assert.AreEqual(36, store.TotalSize);
    }

    // --- Quota management ---

    [TestMethod]
    public void StoreImage_ExceedsQuota_EvictsOldest()
    {
        var store = new KgpImageStore(quotaBytes: 32); // 32 bytes quota

        store.StoreImage(CreateImage(1, 2, 2)); // 16 bytes
        store.StoreImage(CreateImage(2, 2, 2)); // 16 bytes, total 32 (at quota)
        store.StoreImage(CreateImage(3, 2, 2)); // 16 bytes, should evict image 1

        Assert.AreEqual(2, store.ImageCount);
        Assert.IsNull(store.GetImageById(1)); // evicted
        Assert.IsNotNull(store.GetImageById(2));
        Assert.IsNotNull(store.GetImageById(3));
    }

    // --- Chunked transfers ---

    [TestMethod]
    public void ProcessChunk_SingleChunk_ReturnsCompleteImage()
    {
        var store = new KgpImageStore();
        var data = new byte[] { 1, 2, 3, 4 };
        var cmd = new KgpCommand { Width = 1, Height = 1, Format = KgpFormat.Rgba32, ImageId = 5, MoreData = 0 };

        var result = store.ProcessChunk(cmd, data);

        Assert.IsNotNull(result);
        Assert.AreEqual(5u, result.ImageId);
        TestSeq.AreEqual(data, result.Data);
        Assert.IsFalse(store.IsChunkedTransferInProgress);
    }

    [TestMethod]
    public void ProcessChunk_MultipleChunks_AssemblesCorrectly()
    {
        var store = new KgpImageStore();

        var cmd1 = new KgpCommand { Width = 2, Height = 2, Format = KgpFormat.Rgba32, ImageId = 1, MoreData = 1 };
        var result1 = store.ProcessChunk(cmd1, new byte[] { 1, 2, 3, 4 });
        Assert.IsNull(result1);
        Assert.IsTrue(store.IsChunkedTransferInProgress);

        var cmd2 = new KgpCommand { MoreData = 1 };
        var result2 = store.ProcessChunk(cmd2, new byte[] { 5, 6, 7, 8 });
        Assert.IsNull(result2);

        var cmd3 = new KgpCommand { MoreData = 0 };
        var result3 = store.ProcessChunk(cmd3, new byte[] { 9, 10, 11, 12 });
        Assert.IsNotNull(result3);

        TestSeq.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, result3.Data);
        Assert.AreEqual(1u, result3.ImageId);
        Assert.AreEqual(2u, result3.Width);
        Assert.AreEqual(2u, result3.Height);
        Assert.IsFalse(store.IsChunkedTransferInProgress);
    }

    [TestMethod]
    public void ProcessChunk_NoImageId_AllocatesId()
    {
        var store = new KgpImageStore();
        var cmd = new KgpCommand { Width = 1, Height = 1, Format = KgpFormat.Rgba32, ImageId = 0, MoreData = 0 };

        var result = store.ProcessChunk(cmd, new byte[] { 1, 2, 3, 4 });

        Assert.IsNotNull(result);
        Assert.IsTrue(result.ImageId > 0);
    }

    [TestMethod]
    public void ProcessChunk_WithImageNumber_PreservesNumber()
    {
        var store = new KgpImageStore();
        var cmd = new KgpCommand { Width = 1, Height = 1, Format = KgpFormat.Rgba32, ImageNumber = 93, MoreData = 0 };

        var result = store.ProcessChunk(cmd, new byte[] { 1, 2, 3, 4 });

        Assert.IsNotNull(result);
        Assert.AreEqual(93u, result.ImageNumber);
    }

    [TestMethod]
    public void AbortChunkedTransfer_ClearsState()
    {
        var store = new KgpImageStore();
        var cmd = new KgpCommand { Width = 2, Height = 2, Format = KgpFormat.Rgba32, ImageId = 1, MoreData = 1 };
        store.ProcessChunk(cmd, new byte[] { 1, 2, 3, 4 });

        Assert.IsTrue(store.IsChunkedTransferInProgress);
        store.AbortChunkedTransfer();
        Assert.IsFalse(store.IsChunkedTransferInProgress);
    }

    [TestMethod]
    public void Clear_AbortsChunkedTransfer()
    {
        var store = new KgpImageStore();
        var cmd = new KgpCommand { Width = 2, Height = 2, Format = KgpFormat.Rgba32, ImageId = 1, MoreData = 1 };
        store.ProcessChunk(cmd, new byte[] { 1, 2, 3, 4 });

        store.Clear();
        Assert.IsFalse(store.IsChunkedTransferInProgress);
    }

    // --- KgpImageData validation ---

    [TestMethod]
    public void KgpImageData_IsDataSizeValid_CorrectSizeRgba()
    {
        var data = new byte[4]; // 1x1 RGBA = 4 bytes
        var image = new KgpImageData(1, 0, data, 1, 1, KgpFormat.Rgba32);
        Assert.IsTrue(image.IsDataSizeValid());
    }

    [TestMethod]
    public void KgpImageData_IsDataSizeValid_CorrectSizeRgb()
    {
        var data = new byte[3]; // 1x1 RGB = 3 bytes
        var image = new KgpImageData(1, 0, data, 1, 1, KgpFormat.Rgb24);
        Assert.IsTrue(image.IsDataSizeValid());
    }

    [TestMethod]
    public void KgpImageData_IsDataSizeValid_WrongSize()
    {
        var data = new byte[2]; // Too small for 1x1 RGBA
        var image = new KgpImageData(1, 0, data, 1, 1, KgpFormat.Rgba32);
        Assert.IsFalse(image.IsDataSizeValid());
    }

    [TestMethod]
    public void KgpImageData_IsDataSizeValid_PngAlwaysValidIfNonEmpty()
    {
        var data = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header bytes
        var image = new KgpImageData(1, 0, data, 0, 0, KgpFormat.Png);
        Assert.IsTrue(image.IsDataSizeValid());
    }

    [TestMethod]
    public void KgpImageData_Is4ByteAligned_Rgba()
    {
        var image = new KgpImageData(1, 0, new byte[4], 1, 1, KgpFormat.Rgba32);
        Assert.IsTrue(image.Is4ByteAligned);
    }

    [TestMethod]
    public void KgpImageData_Is4ByteAligned_Rgb()
    {
        var image = new KgpImageData(1, 0, new byte[3], 1, 1, KgpFormat.Rgb24);
        Assert.IsFalse(image.Is4ByteAligned);
    }

    [TestMethod]
    public void KgpImageData_ContentHash_DifferentData_DifferentHash()
    {
        var image1 = new KgpImageData(1, 0, new byte[] { 1, 2, 3, 4 }, 1, 1, KgpFormat.Rgba32);
        var image2 = new KgpImageData(2, 0, new byte[] { 5, 6, 7, 8 }, 1, 1, KgpFormat.Rgba32);
        Assert.AreNotEqual(image1.ContentHash, image2.ContentHash);
    }

    [TestMethod]
    public void KgpImageData_ContentHash_SameData_SameHash()
    {
        var data = new byte[] { 1, 2, 3, 4 };
        var image1 = new KgpImageData(1, 0, (byte[])data.Clone(), 1, 1, KgpFormat.Rgba32);
        var image2 = new KgpImageData(2, 0, (byte[])data.Clone(), 1, 1, KgpFormat.Rgba32);
        TestSeq.AreEqual(image1.ContentHash, image2.ContentHash);
    }

    // --- Concurrent access ---

    [TestMethod]
    public void ConcurrentAccess_DoesNotThrow()
    {
        var store = new KgpImageStore();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            var id = (uint)(i + 1);
            tasks.Add(Task.Run(() =>
            {
                store.StoreImage(CreateImage(id, 1, 1));
                store.GetImageById(id);
                store.RemoveImage(id);
            }));
        }

        Task.WaitAll(tasks.ToArray());
    }
}
