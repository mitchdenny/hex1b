using System.Security.Cryptography;
using System.Text;
using Hex1b.Kgp;

namespace Hex1b.Tests;

public class KgpImageCacheTests
{
    [Fact]
    public void AllocateImageId_ReturnsSequentialIds()
    {
        var cache = new KgpImageCache();
        Assert.Equal(1u, cache.AllocateImageId());
        Assert.Equal(2u, cache.AllocateImageId());
        Assert.Equal(3u, cache.AllocateImageId());
    }

    [Fact]
    public void TryGetImageId_ReturnsFalse_WhenNotTransmitted()
    {
        var cache = new KgpImageCache();
        var hash = SHA256.HashData(new byte[] { 1, 2, 3 });
        Assert.False(cache.TryGetImageId(hash, out _));
    }

    [Fact]
    public void TryGetImageId_ReturnsTrue_AfterRegistration()
    {
        var cache = new KgpImageCache();
        var hash = SHA256.HashData(new byte[] { 1, 2, 3 });
        cache.RegisterTransmission(hash, 42);

        Assert.True(cache.TryGetImageId(hash, out var imageId));
        Assert.Equal(42u, imageId);
    }

    [Fact]
    public void DifferentContent_GetsDifferentEntries()
    {
        var cache = new KgpImageCache();
        var hash1 = SHA256.HashData(new byte[] { 1, 2, 3 });
        var hash2 = SHA256.HashData(new byte[] { 4, 5, 6 });

        cache.RegisterTransmission(hash1, 1);
        cache.RegisterTransmission(hash2, 2);

        Assert.True(cache.TryGetImageId(hash1, out var id1));
        Assert.True(cache.TryGetImageId(hash2, out var id2));
        Assert.Equal(1u, id1);
        Assert.Equal(2u, id2);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new KgpImageCache();
        var hash = SHA256.HashData(new byte[] { 1, 2, 3 });
        cache.RegisterTransmission(hash, 1);

        cache.Clear();

        Assert.False(cache.TryGetImageId(hash, out _));
        Assert.Equal(0, cache.Count);
    }
}
