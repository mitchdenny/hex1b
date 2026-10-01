using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class KgpCompressedLifetimeTests
{
    [TestMethod]
    public async Task Data_CompressedImage_ReturnsIndependentConcurrentCallerOwnedArrays()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var pixels = Pixels(17);
        var encoded = Upload(terminal, pixels);
        var image = terminal.KgpImageStore.GetImageById(1)!;
        Assert.IsNotNull(image);
        Assert.IsTrue(image.IsZlibCompressed);

        var first = image.Data;
        var second = image.Data;
        Assert.AreNotSame(first, second);
        CollectionAssert.AreEqual(pixels, first);
        first[0] ^= 255;
        CollectionAssert.AreEqual(pixels, second);
        CollectionAssert.AreEqual(pixels, image.Data);
        CollectionAssert.AreEqual(SHA256.HashData(pixels), image.ContentHash);

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => image.Data, TestContext.Current.CancellationToken)));
        foreach (var data in concurrent)
            CollectionAssert.AreEqual(pixels, data);
        for (var i = 1; i < concurrent.Length; i++)
            Assert.AreNotSame(concurrent[0], concurrent[i]);

        Assert.AreEqual((long)encoded.Length, terminal.KgpImageStore.TotalSize);
        Assert.AreEqual((long)pixels.Length, terminal.KgpImageStore.ReservedDecodedBytes);
        Assert.AreEqual((long)encoded.Length + pixels.Length, terminal.KgpImageStore.ChargedSize);
        terminal.Dispose();
        CollectionAssert.AreEqual(pixels, image.Data);
        CollectionAssert.AreEqual(pixels, second);
    }

    [TestMethod]
    public void Data_CompressedImage_DoesNotRetainMaterializedArray()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Upload(terminal, Pixels(42));
        var image = terminal.KgpImageStore.GetImageById(1)!;
        var decoded = MaterializeWithoutRetaining(image);

        Collect();

        Assert.IsFalse(decoded.TryGetTarget(out _), "The live image must not cache caller-owned decoded arrays.");
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        GC.KeepAlive(image);
    }

    [TestMethod]
    public void Replacement_FiveHundredGenerations_ReleasesOldOwnersAndAccountsCurrentVersion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var oldOwners = ExerciseGenerations(terminal);

        Collect();

        foreach (var owner in oldOwners)
            Assert.IsFalse(owner.TryGetTarget(out _), "An obsolete image remains reachable after all snapshots were released.");
        Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
        Apply(terminal, "\x1b_Ga=d,d=A,q=2\x1b\\");
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        Assert.AreEqual(0L, terminal.KgpImageStore.TotalSize);
        Assert.AreEqual(0L, terminal.KgpImageStore.ReservedDecodedBytes);
        Assert.AreEqual(0L, terminal.KgpImageStore.ChargedSize);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Screens_ResetAndDisposal_ReleaseLiveChargesWithoutInvalidatingSnapshots(bool asyncDispose)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminal = CreateTerminal(workload);
        try
        {
            Upload(terminal, Pixels(7));
            var mainStore = terminal.KgpImageStore;
            using var main = terminal.CreateSnapshot();
            Apply(terminal, "\x1b[?1049h");
            Upload(terminal, Pixels(8));
            var alternateStore = terminal.KgpImageStore;
            using var alternate = terminal.CreateSnapshot();
            Apply(terminal, "\x1b[?1049l");

            Assert.AreEqual(0L, alternateStore.TotalSize);
            Assert.AreEqual(0L, alternateStore.ReservedDecodedBytes);
            CollectionAssert.AreEqual(Pixels(7), main.KgpImages[1].Data);
            CollectionAssert.AreEqual(Pixels(8), alternate.KgpImages[1].Data);
            Apply(terminal, "\u001bc");
            Assert.AreEqual(0L, mainStore.ChargedSize);
            Upload(terminal, Pixels(9));
            if (asyncDispose)
                await terminal.DisposeAsync();
            else
                terminal.Dispose();

            Assert.AreEqual(0L, mainStore.TotalSize);
            Assert.AreEqual(0L, mainStore.ReservedDecodedBytes);
            Assert.AreEqual(0L, alternateStore.ChargedSize);
            main.Dispose();
            alternate.Dispose();
            CollectionAssert.AreEqual(Pixels(7), main.KgpImages[1].Data);
            CollectionAssert.AreEqual(Pixels(8), alternate.KgpImages[1].Data);
        }
        finally
        {
            await terminal.DisposeAsync();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<KgpImageData>[] ExerciseGenerations(Hex1bTerminal terminal)
    {
        Hex1bTerminalSnapshot? first = null;
        Hex1bTerminalSnapshot? middle = null;
        var oldOwners = new List<WeakReference<KgpImageData>>();
        try
        {
            for (var generation = 0; generation < 500; generation++)
            {
                var pixels = Pixels((byte)(generation % 251));
                var encoded = Upload(terminal, pixels);
                Assert.AreEqual(1, terminal.KgpImageStore.ImageCount);
                Assert.AreEqual((long)encoded.Length, terminal.KgpImageStore.TotalSize);
                Assert.AreEqual((long)pixels.Length, terminal.KgpImageStore.ReservedDecodedBytes);
                Assert.AreEqual((long)encoded.Length + pixels.Length, terminal.KgpImageStore.ChargedSize);
                if (generation == 0)
                    first = terminal.CreateSnapshot();
                if (generation == 249)
                    middle = terminal.CreateSnapshot();
                if (generation < 499)
                    oldOwners.Add(new(terminal.KgpImageStore.GetImageById(1)!));
            }

            Assert.IsNotNull(first);
            Assert.IsNotNull(middle);
            CollectionAssert.AreEqual(Pixels(0), first.KgpImages[1].Data);
            CollectionAssert.AreEqual(Pixels(249), middle.KgpImages[1].Data);
            first.Dispose();
            middle.Dispose();
            CollectionAssert.AreEqual(Pixels(0), first.KgpImages[1].Data);
            CollectionAssert.AreEqual(Pixels(249), middle.KgpImages[1].Data);
        }
        finally
        {
            first?.Dispose();
            middle?.Dispose();
        }
        return oldOwners.ToArray();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<byte[]> MaterializeWithoutRetaining(KgpImageData image)
        => new(image.Data);

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static Hex1bTerminal CreateTerminal(IHex1bTerminalWorkloadAdapter workload)
        => Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true, SupportsTrueColor = true })
            .WithDimensions(20, 10).Build();

    private static void Apply(Hex1bTerminal terminal, string text)
        => terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));

    private static byte[] Upload(Hex1bTerminal terminal, byte[] pixels)
    {
        using var output = new MemoryStream();
        using (var encoder = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
            encoder.Write(pixels);
        var encoded = output.ToArray();
        Apply(terminal, $"\x1b[1;1H\x1b_Ga=T,i=1,f=32,o=z,s=32,v=16,c=4,r=1,q=2;{Convert.ToBase64String(encoded)}\x1b\\");
        return encoded;
    }

    private static byte[] Pixels(byte seed)
        => Enumerable.Range(0, 32 * 16).SelectMany(i =>
            new byte[] { seed, (byte)(i % 251), (byte)(255 - seed), 255 }).ToArray();
}
