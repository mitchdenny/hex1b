using Hex1b.Kgp;

namespace Hex1b.Tests;

public class KgpImageStoreTests
{
    private static KgpImageData CreateImage(uint id, uint width = 2, uint height = 2, uint number = 0, byte fill = 0xFF)
    {
        var data = KgpTestHelper.CreatePixelData(width, height, KgpFormat.Rgba32, fill);
        return new KgpImageData(id, number, data, width, height, KgpFormat.Rgba32);
    }

    // --- Basic store/retrieve ---

    [Fact]
    public void StoreImage_StoresAndRetrieves()
    {
        var store = new KgpImageStore();
        var image = CreateImage(1);

        store.StoreImage(image);

        Assert.Equal(1, store.ImageCount);
        Assert.Same(image, store.GetImageById(1));
    }

    [Fact]
    public void GetImageById_NonExistent_ReturnsNull()
    {
        var store = new KgpImageStore();
        Assert.Null(store.GetImageById(999));
    }

    [Fact]
    public void StoreImage_ReplacesSameId()
    {
        var store = new KgpImageStore();
        var image1 = CreateImage(1, fill: 0xAA);
        var image2 = CreateImage(1, fill: 0xBB);

        store.StoreImage(image1);
        store.StoreImage(image2);

        Assert.Equal(1, store.ImageCount);
        Assert.Same(image2, store.GetImageById(1));
    }

    [Fact]
    public void StoreImage_MultipleImages()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1));
        store.StoreImage(CreateImage(2));
        store.StoreImage(CreateImage(3));

        Assert.Equal(3, store.ImageCount);
        Assert.NotNull(store.GetImageById(1));
        Assert.NotNull(store.GetImageById(2));
        Assert.NotNull(store.GetImageById(3));
    }

    // --- Image numbers ---

    [Fact]
    public void StoreImage_WithNumber_RetrievableByNumber()
    {
        var store = new KgpImageStore();
        var image = CreateImage(1, number: 42);
        store.StoreImage(image);

        Assert.Same(image, store.GetImageByNumber(42));
    }

    [Fact]
    public void GetImageByNumber_ReturnsNewest()
    {
        var store = new KgpImageStore();
        var image1 = CreateImage(1, number: 42);
        var image2 = CreateImage(2, number: 42);
        store.StoreImage(image1);
        store.StoreImage(image2);

        // GetImageByNumber returns the newest (last added)
        Assert.Same(image2, store.GetImageByNumber(42));
    }

    [Fact]
    public void GetImageByNumber_NonExistent_ReturnsNull()
    {
        var store = new KgpImageStore();
        Assert.Null(store.GetImageByNumber(999));
    }

    // --- Removal ---

    [Fact]
    public void RemoveImage_RemovesById()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1));

        Assert.True(store.RemoveImage(1));
        Assert.Equal(0, store.ImageCount);
        Assert.Null(store.GetImageById(1));
    }

    [Fact]
    public void RemoveImage_NonExistent_ReturnsFalse()
    {
        var store = new KgpImageStore();
        Assert.False(store.RemoveImage(999));
    }

    [Fact]
    public void RemoveImageByNumber_RemovesNewest()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1, number: 42));
        store.StoreImage(CreateImage(2, number: 42));

        Assert.True(store.RemoveImageByNumber(42));
        Assert.Equal(1, store.ImageCount);
        // Image 1 (first with number 42) should still exist
        Assert.NotNull(store.GetImageById(1));
        Assert.Null(store.GetImageById(2));
    }

    [Fact]
    public void RemoveImage_UpdatesNumberIndex()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1, number: 42));
        store.StoreImage(CreateImage(2, number: 42));

        store.RemoveImage(2);

        // After removing image 2, the newest with number 42 is image 1
        Assert.Same(store.GetImageById(1), store.GetImageByNumber(42));
    }

    // --- Clear ---

    [Fact]
    public void Clear_RemovesAll()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1));
        store.StoreImage(CreateImage(2));

        store.Clear();

        Assert.Equal(0, store.ImageCount);
        Assert.Equal(0, store.TotalSize);
    }

    // --- ID allocation ---

    [Fact]
    public void AllocateId_ReturnsUniqueIds()
    {
        var store = new KgpImageStore();
        var id1 = store.AllocateId();
        var id2 = store.AllocateId();
        var id3 = store.AllocateId();

        Assert.NotEqual(id1, id2);
        Assert.NotEqual(id2, id3);
        Assert.True(id1 > 0);
    }

    // --- TotalSize tracking ---

    [Fact]
    public void TotalSize_TracksCorrectly()
    {
        var store = new KgpImageStore();

        store.StoreImage(CreateImage(1, 2, 2)); // 2*2*4 = 16 bytes
        Assert.Equal(16, store.TotalSize);

        store.StoreImage(CreateImage(2, 3, 3)); // 3*3*4 = 36 bytes
        Assert.Equal(52, store.TotalSize);

        store.RemoveImage(1);
        Assert.Equal(36, store.TotalSize);
    }

    [Fact]
    public void TotalSize_ReplacingImage_UpdatesCorrectly()
    {
        var store = new KgpImageStore();
        store.StoreImage(CreateImage(1, 2, 2)); // 16 bytes
        store.StoreImage(CreateImage(1, 3, 3)); // replace with 36 bytes

        Assert.Equal(36, store.TotalSize);
    }

    // --- Quota management ---

    [Fact]
    public void StoreImage_ExceedsQuota_EvictsOldest()
    {
        var store = new KgpImageStore(quotaBytes: 32); // 32 bytes quota

        store.StoreImage(CreateImage(1, 2, 2)); // 16 bytes
        store.StoreImage(CreateImage(2, 2, 2)); // 16 bytes, total 32 (at quota)
        store.StoreImage(CreateImage(3, 2, 2)); // 16 bytes, should evict image 1

        Assert.Equal(2, store.ImageCount);
        Assert.Null(store.GetImageById(1)); // evicted
        Assert.NotNull(store.GetImageById(2));
        Assert.NotNull(store.GetImageById(3));
    }

    // --- Chunked transfers ---

    [Fact]
    public void ProcessChunk_SingleChunk_ReturnsCompleteImage()
    {
        var store = new KgpImageStore();
        var data = new byte[] { 1, 2, 3, 4 };
        var cmd = new KgpCommand { Width = 1, Height = 1, Format = KgpFormat.Rgba32, ImageId = 5, MoreData = 0 };

        var result = store.ProcessChunk(cmd, data);

        Assert.NotNull(result);
        Assert.Equal(5u, result.ImageId);
        Assert.Equal(data, result.Data);
        Assert.False(store.IsChunkedTransferInProgress);
    }

    [Fact]
    public void ProcessChunk_MultipleChunks_AssemblesCorrectly()
    {
        var store = new KgpImageStore();

        var cmd1 = new KgpCommand { Width = 2, Height = 2, Format = KgpFormat.Rgba32, ImageId = 1, MoreData = 1 };
        var result1 = store.ProcessChunk(cmd1, new byte[] { 1, 2, 3, 4 });
        Assert.Null(result1);
        Assert.True(store.IsChunkedTransferInProgress);

        var cmd2 = new KgpCommand { MoreData = 1 };
        var result2 = store.ProcessChunk(cmd2, new byte[] { 5, 6, 7, 8 });
        Assert.Null(result2);

        var cmd3 = new KgpCommand { MoreData = 0 };
        var result3 = store.ProcessChunk(cmd3, new byte[] { 9, 10, 11, 12 });
        Assert.NotNull(result3);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, result3.Data);
        Assert.Equal(1u, result3.ImageId);
        Assert.Equal(2u, result3.Width);
        Assert.Equal(2u, result3.Height);
        Assert.False(store.IsChunkedTransferInProgress);
    }

    [Fact]
    public void ProcessChunk_NoImageId_AllocatesId()
    {
        var store = new KgpImageStore();
        var cmd = new KgpCommand { Width = 1, Height = 1, Format = KgpFormat.Rgba32, ImageId = 0, MoreData = 0 };

        var result = store.ProcessChunk(cmd, new byte[] { 1, 2, 3, 4 });

        Assert.NotNull(result);
        Assert.True(result.ImageId > 0);
    }

    [Fact]
    public void ProcessChunk_WithImageNumber_PreservesNumber()
    {
        var store = new KgpImageStore();
        var cmd = new KgpCommand { Width = 1, Height = 1, Format = KgpFormat.Rgba32, ImageNumber = 93, MoreData = 0 };

        var result = store.ProcessChunk(cmd, new byte[] { 1, 2, 3, 4 });

        Assert.NotNull(result);
        Assert.Equal(93u, result.ImageNumber);
    }

    [Fact]
    public void AbortChunkedTransfer_ClearsState()
    {
        var store = new KgpImageStore();
        var cmd = new KgpCommand { Width = 2, Height = 2, Format = KgpFormat.Rgba32, ImageId = 1, MoreData = 1 };
        store.ProcessChunk(cmd, new byte[] { 1, 2, 3, 4 });

        Assert.True(store.IsChunkedTransferInProgress);
        store.AbortChunkedTransfer();
        Assert.False(store.IsChunkedTransferInProgress);
    }

    [Fact]
    public void Clear_AbortsChunkedTransfer()
    {
        var store = new KgpImageStore();
        var cmd = new KgpCommand { Width = 2, Height = 2, Format = KgpFormat.Rgba32, ImageId = 1, MoreData = 1 };
        store.ProcessChunk(cmd, new byte[] { 1, 2, 3, 4 });

        store.Clear();
        Assert.False(store.IsChunkedTransferInProgress);
    }

    // --- KgpImageData validation ---

    [Fact]
    public void KgpImageData_IsDataSizeValid_CorrectSizeRgba()
    {
        var data = new byte[4]; // 1x1 RGBA = 4 bytes
        var image = new KgpImageData(1, 0, data, 1, 1, KgpFormat.Rgba32);
        Assert.True(image.IsDataSizeValid());
    }

    [Fact]
    public void KgpImageData_IsDataSizeValid_CorrectSizeRgb()
    {
        var data = new byte[3]; // 1x1 RGB = 3 bytes
        var image = new KgpImageData(1, 0, data, 1, 1, KgpFormat.Rgb24);
        Assert.True(image.IsDataSizeValid());
    }

    [Fact]
    public void KgpImageData_IsDataSizeValid_WrongSize()
    {
        var data = new byte[2]; // Too small for 1x1 RGBA
        var image = new KgpImageData(1, 0, data, 1, 1, KgpFormat.Rgba32);
        Assert.False(image.IsDataSizeValid());
    }

    [Fact]
    public void KgpImageData_IsDataSizeValid_PngAlwaysValidIfNonEmpty()
    {
        var data = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header bytes
        var image = new KgpImageData(1, 0, data, 0, 0, KgpFormat.Png);
        Assert.True(image.IsDataSizeValid());
    }

    [Fact]
    public void KgpImageData_Is4ByteAligned_Rgba()
    {
        var image = new KgpImageData(1, 0, new byte[4], 1, 1, KgpFormat.Rgba32);
        Assert.True(image.Is4ByteAligned);
    }

    [Fact]
    public void KgpImageData_Is4ByteAligned_Rgb()
    {
        var image = new KgpImageData(1, 0, new byte[3], 1, 1, KgpFormat.Rgb24);
        Assert.False(image.Is4ByteAligned);
    }

    [Fact]
    public void KgpImageData_ContentHash_DifferentData_DifferentHash()
    {
        var image1 = new KgpImageData(1, 0, new byte[] { 1, 2, 3, 4 }, 1, 1, KgpFormat.Rgba32);
        var image2 = new KgpImageData(2, 0, new byte[] { 5, 6, 7, 8 }, 1, 1, KgpFormat.Rgba32);
        Assert.NotEqual(image1.ContentHash, image2.ContentHash);
    }

    [Fact]
    public void KgpImageData_ContentHash_SameData_SameHash()
    {
        var data = new byte[] { 1, 2, 3, 4 };
        var image1 = new KgpImageData(1, 0, (byte[])data.Clone(), 1, 1, KgpFormat.Rgba32);
        var image2 = new KgpImageData(2, 0, (byte[])data.Clone(), 1, 1, KgpFormat.Rgba32);
        Assert.Equal(image1.ContentHash, image2.ContentHash);
    }

    // --- Concurrent access ---

    [Fact]
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
