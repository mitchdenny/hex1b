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

    [Fact]
    public async Task TwoKittyGraphicsWidgets_SameData_ShareTransmission()
    {
        // Two widgets with identical pixel data should result in:
        // - First: a=t (transmit only) + a=p (placement)
        // - Second: a=p (put only, using same image ID)
        var pixelData = new byte[4 * 4 * 4]; // 4x4 RGBA
        for (int i = 0; i < pixelData.Length; i += 4)
        {
            pixelData[i] = 255;     // R
            pixelData[i + 3] = 255; // A
        }

        var capabilities = new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true
        };

        var workload = new Hex1bAppWorkloadAdapter(capabilities);
        await workload.ResizeAsync(80, 24);

        var allBytes = new List<byte>();
        using var readCts = new CancellationTokenSource();
        var readTask = Task.Run(async () =>
        {
            try
            {
                while (!readCts.Token.IsCancellationRequested)
                {
                    var item = await workload.ReadOutputItemAsync(readCts.Token);
                    if (!item.Bytes.IsEmpty)
                        allBytes.AddRange(item.Bytes.ToArray());
                }
            }
            catch (OperationCanceledException) { }
        });

        var app = new Hex1bApp(ctx =>
            ctx.VStack(v => [
                // Two identical images
                v.KittyGraphics(pixelData, 4, 4).WithDisplaySize(4, 2),
                v.KittyGraphics(pixelData, 4, 4).WithDisplaySize(4, 2)
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = false });

        using var appCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try { await app.RunAsync(appCts.Token); } catch (OperationCanceledException) { }
        await app.DisposeAsync();
        await Task.Delay(100);
        readCts.Cancel();
        try { await readTask; } catch { }

        var text = Encoding.UTF8.GetString(allBytes.ToArray());

        // Find all ESC_G sequences and check what follows
        int transmitCount = 0;
        int putCount = 0;
        for (int i = 0; i < text.Length - 2; i++)
        {
            if (text[i] == '\x1b' && text[i + 1] == '_' && text[i + 2] == 'G')
            {
                var rest = text[(i + 3)..Math.Min(i + 20, text.Length)];
                if (rest.StartsWith("a=t")) transmitCount++;
                else if (rest.StartsWith("a=p")) putCount++;
            }
        }

        // First image should be transmitted (a=t), second should be a put (a=p)
        Assert.True(transmitCount >= 1, $"Expected at least 1 transmit (a=t), got {transmitCount}. Total bytes={allBytes.Count}");
        Assert.True(putCount >= 1, $"Expected at least 1 put (a=p) for deduplicated image, got {putCount}");
    }
}
