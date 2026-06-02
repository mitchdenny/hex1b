using System.Security.Cryptography;
using System.Text;

namespace Hex1b.Tests;

[TestClass]
public class KgpImageCacheTests
{
    [TestMethod]
    public void AllocateImageId_ReturnsSequentialIds()
    {
        var cache = new KgpImageCache();
        Assert.AreEqual(1u, cache.AllocateImageId());
        Assert.AreEqual(2u, cache.AllocateImageId());
        Assert.AreEqual(3u, cache.AllocateImageId());
    }

    [TestMethod]
    public void TryGetImageId_ReturnsFalse_WhenNotTransmitted()
    {
        var cache = new KgpImageCache();
        var hash = SHA256.HashData(new byte[] { 1, 2, 3 });
        Assert.IsFalse(cache.TryGetImageId(hash, out _));
    }

    [TestMethod]
    public void TryGetImageId_ReturnsTrue_AfterRegistration()
    {
        var cache = new KgpImageCache();
        var hash = SHA256.HashData(new byte[] { 1, 2, 3 });
        cache.RegisterTransmission(hash, 42);

        Assert.IsTrue(cache.TryGetImageId(hash, out var imageId));
        Assert.AreEqual(42u, imageId);
    }

    [TestMethod]
    public void DifferentContent_GetsDifferentEntries()
    {
        var cache = new KgpImageCache();
        var hash1 = SHA256.HashData(new byte[] { 1, 2, 3 });
        var hash2 = SHA256.HashData(new byte[] { 4, 5, 6 });

        cache.RegisterTransmission(hash1, 1);
        cache.RegisterTransmission(hash2, 2);

        Assert.IsTrue(cache.TryGetImageId(hash1, out var id1));
        Assert.IsTrue(cache.TryGetImageId(hash2, out var id2));
        Assert.AreEqual(1u, id1);
        Assert.AreEqual(2u, id2);
    }

    [TestMethod]
    public void Clear_RemovesAllEntries()
    {
        var cache = new KgpImageCache();
        var hash = SHA256.HashData(new byte[] { 1, 2, 3 });
        cache.RegisterTransmission(hash, 1);

        cache.Clear();

        Assert.IsFalse(cache.TryGetImageId(hash, out _));
        Assert.AreEqual(0, cache.Count);
    }
}
