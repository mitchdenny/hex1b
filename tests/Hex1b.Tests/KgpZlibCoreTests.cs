using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class KgpZlibCoreTests
{
    [TestMethod]
    [DataRow(KgpFormat.Rgb24, false)]
    [DataRow(KgpFormat.Rgba32, false)]
    [DataRow(KgpFormat.Png, false)]
    [DataRow(KgpFormat.Rgb24, true)]
    [DataRow(KgpFormat.Rgba32, true)]
    [DataRow(KgpFormat.Png, true)]
    public void Transmission_Formats_PreserveFormatBytes(KgpFormat format, bool compressed)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var data = FormatBytes(format);
        var encoded = compressed ? Compress(data) : data;
        Send(terminal, $"a=t,i=1,f={(int)format},s=1,v=1{(compressed ? ",o=z" : "")}", encoded);

        var image = terminal.KgpImageStore.GetImageById(1)!;
        Assert.IsNotNull(image);
        Assert.AreEqual(format, image.Format);
        Assert.AreEqual(1u, image.Width);
        Assert.AreEqual(1u, image.Height);
        Assert.AreEqual(compressed, image.IsZlibCompressed);
        TestSeq.AreEqual(data, image.Data);
        TestSeq.AreEqual(SHA256.HashData(data), image.ContentHash);
        Assert.IsTrue(image.IsDataSizeValid());
        Assert.AreEqual((long)encoded.Length, image.StorageSize);
        Assert.AreEqual(compressed ? (long)data.Length : 0, image.ReservedDecodedBytes);
        Assert.AreEqual(compressed ? Convert.ToHexString(SHA256.HashData(data)) : null,
            image.CurrentFrameDataHash);
        AssertAccounting(terminal.KgpImageStore, encoded.Length, compressed ? data.Length : 0);
    }

    [TestMethod]
    public void Validation_EveryTruncatedPrefixAndBadChecksum_IsRejectedBeforePublication()
    {
        var store = new KgpImageStore();
        var data = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        var encoded = Compress(data);
        var command = Command(width: 4, height: 4);
        for (var length = 0; length < encoded.Length; length++)
        {
            Assert.IsNull(store.ProcessChunk(command, encoded[..length]),
                $"Accepted truncated prefix {length}/{encoded.Length}");
            Assert.IsFalse(store.IsChunkedTransferInProgress);
        }
        for (var index = encoded.Length - 4; index < encoded.Length; index++)
        {
            var corrupted = encoded.ToArray();
            corrupted[index] ^= 1;
            Assert.IsNull(store.ProcessChunk(command, corrupted), $"Accepted bad checksum at {index}");
        }
        var badHeader = encoded.ToArray();
        badHeader[0] = 0;
        Assert.IsNull(store.ProcessChunk(command, badHeader));
        // RFC 1950 header with FDICT; this stream cannot be decoded without its dictionary.
        Assert.IsNull(store.ProcessChunk(command, [0x78, 0x20, 0, 0, 0, 1, .. encoded[2..]]));
        AssertAccounting(store, 0, 0);
        TestSeq.AreEqual(data, store.ProcessChunk(command, encoded)!.Data);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(3)]
    [DataRow(5)]
    [DataRow(8192)]
    public void Validation_WrongDecodedLength_IsRejected(int length)
    {
        var store = new KgpImageStore();
        Assert.IsNull(store.ProcessChunk(Command(), Compress(new byte[length])));
        AssertAccounting(store, 0, 0);
    }

    [TestMethod]
    public void Validation_TrailingBytesAndSecondMember_AreRetainedButNotInterpreted()
    {
        var store = new KgpImageStore();
        byte[] data = [1, 2, 3, 4];
        byte[] encoded = [.. Compress(data), .. Compress([5, 6, 7, 8]), 0xFF, 0];
        var image = store.ProcessChunk(Command(), encoded)!;
        Assert.IsNotNull(image);
        TestSeq.AreEqual(data, image.Data);
        TestSeq.AreEqual(encoded, image.EncodedData.ToArray());
        store.StoreImage(image);
        AssertAccounting(store, encoded.Length, data.Length);
    }

    [TestMethod]
    public void PublicProcessChunk_ContinuationPreservesCompressionAndOwnsInput()
    {
        var store = new KgpImageStore();
        var encoded = Compress([1, 2, 3, 4]);
        for (var i = 0; i < encoded.Length; i++)
        {
            var piece = new[] { encoded[i] };
            var command = i == 0
                ? Command(moreData: 1)
                : new KgpCommand { MoreData = i != encoded.Length - 1 ? 1 : 0 };
            var image = store.ProcessChunk(command, piece);
            piece[0] ^= 255;
            if (i == encoded.Length - 1)
            {
                Assert.IsNotNull(image);
                Assert.IsTrue(image.IsZlibCompressed);
                TestSeq.AreEqual(new byte[] { 1, 2, 3, 4 }, image.Data);
            }
            else
            {
                Assert.IsNull(image);
            }
        }
        Assert.AreEqual(0L, store.PendingUploadBytes);
        Assert.AreEqual(0L, store.PendingUploadCapacity);
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(28)]
    [DataRow(996)]
    [DataRow(4096)]
    public void Transmission_ChunkedBase64_PreservesInitialMetadata(int chunkSize)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var data = Enumerable.Range(0, 4000).Select(i => (byte)(i * 31)).ToArray();
        var encoded = Compress(data);
        var base64 = Convert.ToBase64String(encoded);
        for (var offset = 0; offset < base64.Length; offset += chunkSize)
        {
            var piece = base64.Substring(offset, Math.Min(chunkSize, base64.Length - offset));
            var more = offset + piece.Length < base64.Length ? 1 : 0;
            var controls = offset == 0
                ? $"a=t,i=1,f=32,s=100,v=10,o=z,q=2,m={more}"
                : $"m={more}";
            Apply(terminal, $"\x1b_G{controls};{piece}\x1b\\");
        }
        var image = terminal.KgpImageStore.GetImageById(1)!;
        Assert.IsNotNull(image);
        TestSeq.AreEqual(data, image.Data);
        AssertAccounting(terminal.KgpImageStore, encoded.Length, data.Length);
        Assert.AreEqual(0L, terminal.KgpImageStore.PendingUploadCapacity);
    }

    [TestMethod]
    public async Task Transmission_MalformedQuietAndQuery_ReturnExpectedResponsesWithoutStorage()
    {
        var workload = new ResponseWorkload();
        using var terminal = CreateTerminal(workload);
        var encoded = Compress([1, 2, 3, 4]);
        Send(terminal, "a=q,i=1,f=32,s=1,v=1,o=z", encoded);
        Assert.AreEqual("\x1b_Gi=1;OK\x1b\\", await workload.ReadAsync());
        AssertAccounting(terminal.KgpImageStore, 0, 0);

        Send(terminal, "a=q,i=2,f=32,s=1,v=1,o=z,q=1", encoded[..^1]);
        StringAssert.Contains(await workload.ReadAsync(), "i=2;EINVAL:");
        Send(terminal, "a=t,i=3,f=32,s=1,v=1,o=z,q=2", encoded[..^1]);
        Send(terminal, "a=q,i=4,f=32,s=1,v=1,o=z,q=1", encoded);
        Send(terminal, "a=q,i=5,f=32,s=1,v=1,o=z", encoded);
        Assert.AreEqual("\x1b_Gi=5;OK\x1b\\", await workload.ReadAsync());
        Assert.IsFalse(workload.Responses.TryRead(out _));
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public async Task Transmission_ContinuationQuietAndInvalidControls_AbortAndRecover()
    {
        var workload = new ResponseWorkload();
        using var terminal = CreateTerminal(workload);
        var encoded = Compress([1, 2, 3, 4]);
        Send(terminal, "a=t,i=1,f=32,s=1,v=1,o=z,q=1,m=1", encoded[..3]);
        Send(terminal, "m=0", encoded[3..^1]);
        StringAssert.Contains(await workload.ReadAsync(), "i=1;EINVAL:");
        Assert.IsFalse(terminal.KgpImageStore.IsChunkedTransferInProgress);
        Send(terminal, "a=t,i=1,f=32,s=1,v=1,o=z,q=2,m=1", encoded[..3]);
        Send(terminal, "m=0,f=32", encoded[3..]);
        Send(terminal, "a=t,i=2,f=32,s=1,v=1,o=z", encoded);
        Assert.AreEqual("\x1b_Gi=2;OK\x1b\\", await workload.ReadAsync());
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(2));
        Assert.AreEqual(0L, terminal.KgpImageStore.PendingUploadCapacity);
    }

    [TestMethod]
    public void Transmission_InputRasterAndPendingBounds_RejectWithoutLeakingAssembly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var encoded = Compress([1, 2, 3, 4]);
        using var terminal = CreateTerminal(workload, graphics =>
        {
            graphics.MaximumRetainedInputBytesPerImage = encoded.Length - 1;
            graphics.MaximumRasterPixelsPerImage = 1;
        });
        Send(terminal, "a=t,i=1,f=32,s=1,v=1,o=z", encoded);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Send(terminal, "a=t,i=1,f=32,s=1,v=1,o=z,m=1", encoded[..3]);
        Assert.AreEqual(3L, terminal.KgpImageStore.PendingUploadBytes);
        Send(terminal, "m=0", encoded[3..]);
        Assert.AreEqual(0L, terminal.KgpImageStore.PendingUploadCapacity);
        Send(terminal, "a=t,i=1,f=32,s=2,v=1,o=z", Compress(new byte[8]));
        Send(terminal, "a=t,i=1,f=32,s=4294967295,v=4294967295,o=z", encoded);
        AssertAccounting(terminal.KgpImageStore, 0, 0);

        // The compressed-only raster/input limits do not retrofit legacy raw uploads.
        Send(terminal, "a=t,i=1,f=32,s=2,v=1", new byte[8]);
        AssertAccounting(terminal.KgpImageStore, 8, 0);
    }

    [TestMethod]
    public void Validation_PngMetadataAndInflatedByteBounds_AreEnforced()
    {
        var png = FormatBytes(KgpFormat.Png);
        var store = new KgpImageStore();
        var command = Command(format: KgpFormat.Png);
        var invalid = png.ToArray();
        invalid[29] ^= 1;
        Assert.IsNull(store.ProcessChunk(command, Compress(invalid)));
        Assert.IsNull(store.ProcessChunk(command, Compress(png[..32])));
        var encoded = Compress(png);
        var exact = new KgpImageStore(encoded.Length + png.Length);
        Assert.IsNotNull(exact.ProcessChunk(command, encoded));
        var below = new KgpImageStore(encoded.Length + png.Length - 1);
        Assert.IsNull(below.ProcessChunk(command, encoded));
    }

    [TestMethod]
    public void Store_ExactQuotaAndOneByteOver_KeepActualAndReservedDistinct()
    {
        byte[] data = [1, 2, 3, 4];
        var encoded = Compress(data);
        var cost = encoded.Length + data.Length;
        var exact = new KgpImageStore(cost);
        var image = exact.ProcessChunk(Command(), encoded)!;
        Assert.IsNotNull(exact.StoreImage(image));
        AssertAccounting(exact, encoded.Length, data.Length);
        var below = new KgpImageStore(cost - 1);
        Assert.IsNull(below.StoreImage(image));
        AssertAccounting(below, 0, 0);
        exact.RemoveImage(1);
        AssertAccounting(exact, 0, 0);
    }

    [TestMethod]
    public void Store_ReplacementEvictionAndClear_ReleaseBothCharges()
    {
        var encoded = Compress([1, 2, 3, 4]);
        var store = new KgpImageStore(encoded.Length + 4);
        var first = store.ProcessChunk(Command(), encoded)!;
        store.StoreImage(first);
        store.StoreImage(first.WithImageId(2));
        Assert.IsNull(store.GetImageById(1));
        AssertAccounting(store, encoded.Length, 4);
        store.StoreImage(new KgpImageData(2, 0, [5, 6, 7, 8], 1, 1, KgpFormat.Rgba32));
        AssertAccounting(store, 4, 0);
        store.StoreImage(first.WithImageId(2));
        AssertAccounting(store, encoded.Length, 4);
        store.Clear();
        AssertAccounting(store, 0, 0);
    }

    [TestMethod]
    public void Terminal_ResetAndDispose_ClearBothScreenAccounting()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var encoded = Compress([1, 2, 3, 4]);
        Send(terminal, "a=t,i=1,f=32,s=1,v=1,o=z", encoded);
        var main = terminal.KgpImageStore;
        Apply(terminal, "\x1b[?1049h");
        Send(terminal, "a=t,i=1,f=32,s=1,v=1,o=z", encoded);
        var alternate = terminal.KgpImageStore;
        AssertAccounting(main, encoded.Length, 4);
        AssertAccounting(alternate, encoded.Length, 4);
        Apply(terminal, "\u001bc");
        AssertAccounting(main, 0, 0);
        AssertAccounting(alternate, 0, 0);
        Send(terminal, "a=t,i=1,f=32,s=1,v=1,o=z", encoded);
        var active = terminal.KgpImageStore;
        terminal.Dispose();
        AssertAccounting(active, 0, 0);
    }

    [TestMethod]
    public void SharedBudget_CompressedReservation_BlocksSixelAndPreservesResidentSixel()
    {
        var encoded = Compress([1, 2, 3, 4]);
        var cost = encoded.Length + 4;
        var budget = new TerminalGraphicsRetainedBudget(cost + 10);
        var store = new KgpImageStore(budget);
        var image = store.ProcessChunk(Command(), encoded)!;
        budget.SetSixelBytes(11);
        Assert.IsNull(store.StoreImage(image));
        Assert.AreEqual(11L, budget.SixelBytes);
        budget.SetSixelBytes(10);
        Assert.IsNotNull(store.StoreImage(image));
        Assert.AreEqual((long)encoded.Length, budget.KgpBytes);
        Assert.AreEqual(10L, budget.MaximumSixelBytes);
        Assert.IsFalse(budget.CanSetSixelBytes(11));
        store.Clear();
        Assert.AreEqual((long)cost + 10, budget.MaximumSixelBytes);
        Assert.AreEqual(10L, budget.SixelBytes);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SharedBudget_TerminalSixelAndCompressedImage_UseExactCombinedCapacity(bool sixelFirst)
    {
        const string sixel = "\x1bP0;0;0q\"1;1;1;6#1;2;100;0;0#1@\x1b\\";
        using var measureWorkload = new Hex1bAppWorkloadAdapter();
        using var measure = CreateTerminal(measureWorkload);
        Apply(measure, sixel);
        var sixelBytes = measure.SixelRetainedByteCount;
        Assert.IsTrue(sixelBytes > 0);
        var encoded = Compress([1, 2, 3, 4]);
        var capacity = sixelBytes + encoded.Length + 4;
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload,
            options => options.MaximumRetainedBytesPerScreen = capacity);
        if (sixelFirst)
            Apply(terminal, sixel);
        Send(terminal, "a=t,i=1,f=32,s=1,v=1,o=z", encoded);
        if (!sixelFirst)
            Apply(terminal, sixel);
        Assert.AreEqual(sixelBytes, terminal.SixelRetainedByteCount);
        AssertAccounting(terminal.KgpImageStore, encoded.Length, 4);
        Assert.AreEqual(sixelBytes + encoded.Length, terminal.GraphicsRetainedByteCount);
        Assert.AreEqual(capacity, terminal.GraphicsChargedByteCount);

        Send(terminal, "a=t,i=2,f=32,s=1,v=1,o=z", [.. encoded, 0]);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(2));
        Assert.IsNotNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(sixelBytes, terminal.SixelRetainedByteCount);
        terminal.KgpImageStore.Clear();
        Assert.AreEqual(sixelBytes, terminal.GraphicsChargedByteCount);
    }

    [TestMethod]
    public void Store_AnonymousRelocationAndNumberGenerations_PreserveCharges()
    {
        var store = new KgpImageStore();
        var encoded = Compress([1, 2, 3, 4]);
        var anonymous = Command(imageId: 0);
        var first = store.StoreImage(anonymous.ToTransmissionData(), encoded);
        var explicitCommand = Command(imageId: first.Image.ImageId);
        var second = store.StoreImage(explicitCommand.ToTransmissionData(), encoded);
        Assert.IsNotNull(second.Relocation);
        AssertAccounting(store, encoded.Length * 2, 8);
        TestSeq.AreEqual(new byte[] { 1, 2, 3, 4 }, store.GetImageById(second.Relocation.Value.CurrentId)!.Data);
        var numbered = Command(imageId: 0, imageNumber: 5);
        store.StoreImage(numbered.ToTransmissionData(), encoded);
        var newest = store.StoreImage(numbered.ToTransmissionData(), encoded);
        Assert.AreSame(newest.Image, store.GetImageByNumber(5));
        AssertAccounting(store, encoded.Length * 4, 16);
        store.Clear();
        AssertAccounting(store, 0, 0);
    }

    [TestMethod]
    public void Transmission_ExplicitReplacementStart_RemovesOldImageBeforeMalformedCompletion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        var encoded = Compress([1, 2, 3, 4]);
        Send(terminal, "a=t,i=1,f=32,s=1,v=1,o=z", encoded);
        Send(terminal, "a=t,i=1,f=32,s=1,v=1,o=z,m=1", encoded[..3]);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        AssertAccounting(terminal.KgpImageStore, 0, 0);
        Send(terminal, "m=0", encoded[3..^1]);
        Assert.IsNull(terminal.KgpImageStore.GetImageById(1));
        Assert.AreEqual(0L, terminal.KgpImageStore.PendingUploadCapacity);
    }

    [TestMethod]
    [DataRow(KgpFormat.Rgb24)]
    [DataRow(KgpFormat.Rgba32)]
    public void Animation_CompressedRootAndFrame_PromoteUsingReservation(KgpFormat format)
    {
        var data = FormatBytes(format);
        var encoded = Compress(data);
        var store = new KgpImageStore(Math.Max(encoded.Length + data.Length, 8));
        var original = store.ProcessChunk(Command(format: format), encoded)!;
        store.StoreImage(original);
        var command = ParseFrame($"a=f,i=1,f={(int)format},o=z,X=1");
        var result = store.StoreAnimationFrame(command, Compress(data));
        Assert.AreEqual(KgpImageStore.AnimationFrameStatus.Success, result.Info.Status);
        Assert.IsNotNull(result.Image);
        Assert.IsFalse(result.Image.IsZlibCompressed);
        Assert.AreEqual(KgpFormat.Rgba32, result.Image.Format);
        Assert.AreEqual(2, result.Image.FrameCount);
        AssertAccounting(store, 8, 0);
        TestSeq.AreEqual(data, original.Data);
        Assert.IsTrue(original.IsZlibCompressed);
        var edited = store.StoreAnimationFrame(ParseFrame("a=f,i=1,f=32,r=1,o=z,X=1"), Compress([9, 8, 7, 6]));
        Assert.AreEqual(KgpImageStore.AnimationFrameStatus.Success, edited.Info.Status);
        TestSeq.AreEqual(new byte[] { 9, 8, 7, 6 }, edited.Image!.Data);
        AssertAccounting(store, 8, 0);
        store.DeleteAnimationFrame(1, 0, 2, true);
        AssertAccounting(store, 4, 0);
        store.DeleteAnimationFrame(1, 0, 1, true);
        AssertAccounting(store, 0, 0);
    }

    [TestMethod]
    [DataRow(8191, false)]
    [DataRow(8192, true)]
    public void Animation_PromotionAtExactQuota_UsesNetChargedDelta(int quota, bool accepted)
    {
        var encoded = Compress(new byte[4096]);
        var store = new KgpImageStore(quota);
        var original = store.ProcessChunk(Command(width: 32, height: 32), encoded)!;
        store.StoreImage(original);
        var result = store.StoreAnimationFrame(
            ParseFrame("a=f,i=1,f=32,s=1,v=1,o=z,X=1"), Compress([1, 2, 3, 4]));
        Assert.AreEqual(accepted ? KgpImageStore.AnimationFrameStatus.Success :
            KgpImageStore.AnimationFrameStatus.NoSpace, result.Info.Status);
        if (accepted)
        {
            AssertAccounting(store, 8192, 0);
            Assert.IsFalse(result.Image!.IsZlibCompressed);
        }
        else
        {
            Assert.AreSame(original, store.GetImageById(1));
            AssertAccounting(store, encoded.Length, 4096);
        }
    }

    [TestMethod]
    public async Task Animation_MalformedCompressedFrame_ReturnsDecodedErrorWithoutMutation()
    {
        var workload = new ResponseWorkload();
        using var terminal = CreateTerminal(workload);
        var encoded = Compress([1, 2, 3, 4]);
        Send(terminal, "a=t,i=1,f=32,s=1,v=1,o=z,q=2", encoded);
        var original = terminal.KgpImageStore.GetImageById(1);
        Send(terminal, "a=f,i=1,f=32,o=z", encoded[..^1]);
        StringAssert.Contains(await workload.ReadAsync(), "EINVAL:");
        Assert.AreSame(original, terminal.KgpImageStore.GetImageById(1));
        Send(terminal, "a=f,i=1,f=32,o=z", Compress([1, 2, 3]));
        StringAssert.Contains(await workload.ReadAsync(), "ENODATA:Insufficient decoded image data");
        AssertAccounting(terminal.KgpImageStore, encoded.Length, 4);
    }

    [TestMethod]
    public void Validation_LargeStaticRoot_UsesBoundedScratchInsteadOfDecodedArray()
    {
        var encoded = Compress(new byte[4 * 1024 * 1024]);
        var store = new KgpImageStore();
        // Warm up stream/hash initialization outside the allocation measurement.
        Assert.IsNotNull(store.ProcessChunk(Command(), Compress([1, 2, 3, 4])));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var image = store.ProcessChunk(Command(width: 1024, height: 1024), encoded)!;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.IsNotNull(image);
        Assert.AreEqual(4L * 1024 * 1024, image.ReservedDecodedBytes);
        Assert.IsTrue(allocated < 1024 * 1024, $"Validation allocated {allocated} bytes.");
        var identity = image.CurrentFrameDataHash;
        image.ContentHash[0] ^= 255;
        Assert.AreEqual(identity, image.WithImageId(2).CurrentFrameDataHash);
    }

    [TestMethod]
    public async Task Data_MaterializationDiagnostics_CountSuccessfulCompressedReadsOnly()
    {
        var store = new KgpImageStore();
        var image = store.ProcessChunk(Command(), Compress([1, 2, 3, 4]))!;
        Assert.AreEqual(0L, image.MaterializationCount);
        Assert.AreEqual(0L, image.MaterializedBytes);
        Assert.IsTrue(image.IsDataSizeValid());
        _ = image.CurrentFrameDataHash;
        _ = image.EncodedData;
        Assert.AreEqual(0L, image.MaterializationCount);
        var first = image.Data;
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            TestSeq.AreEqual(first, image.CurrentFrameData);
        }, TestContext.Current.CancellationToken)));
        Assert.AreEqual(9L, image.MaterializationCount);
        Assert.AreEqual(36L, image.MaterializedBytes);
        Assert.AreEqual(0L, image.WithImageId(2).MaterializationCount);
        var raw = new KgpImageData(1, 0, first, 1, 1, KgpFormat.Rgba32);
        Assert.AreSame(first, raw.Data);
        Assert.AreEqual(0L, raw.MaterializationCount);
        Assert.AreEqual(0L, raw.MaterializedBytes);
    }

    [TestMethod]
    public void Animation_ControlOnlyPromotion_ReplacesCompressedChargeAndPlaybackRestores()
    {
        var encoded = Compress([1, 2, 3, 4]);
        var store = new KgpImageStore(encoded.Length + 4);
        var original = store.ProcessChunk(Command(), encoded)!;
        store.StoreImage(original);
        Assert.IsTrue(KgpCommandParser.TryParse("a=a,i=1,r=1,z=40,s=2", out var parsed, out _));
        var result = store.ControlAnimation(((KgpParsedCommand.AnimationControl)parsed!).Control);
        Assert.AreEqual(KgpImageStore.AnimationControlStatus.Success, result.Status);
        AssertAccounting(store, 4, 0);
        Assert.IsFalse(store.GetImageById(1)!.IsZlibCompressed);
        Assert.IsTrue(original.IsZlibCompressed);
        var playback = new KgpAnimationPlaybackSnapshot(1, 0, 1,
            KgpParsedCommand.AnimationPlaybackState.Running, 1, 0, 0);
        store.RestoreAnimationPlayback(playback, DateTimeOffset.UtcNow);
        Assert.AreEqual(KgpParsedCommand.AnimationPlaybackState.Running,
            store.GetImageById(1)!.AnimationState!.PlaybackState);
        AssertAccounting(store, 4, 0);
        store.Clear();
        AssertAccounting(store, 0, 0);
    }

    [TestMethod]
    public void Animation_CompressedFrameChunks_CanBeLargerThanDecodedFrame()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Send(terminal, "a=t,i=1,f=32,s=1,v=1", [1, 2, 3, 4]);
        var encoded = Compress([5, 6, 7, 8]);
        Assert.IsTrue(encoded.Length > 4);
        Send(terminal, "a=f,i=1,f=32,s=1,v=1,o=z,X=1,m=1", encoded[..3]);
        Send(terminal, "a=f,m=0", encoded[3..]);
        var image = terminal.KgpImageStore.GetImageById(1)!;
        Assert.AreEqual(2, image.FrameCount);
        Assert.IsTrue(image.TryGetFrame(2, out var data, out _, out _));
        TestSeq.AreEqual(new byte[] { 5, 6, 7, 8 }, data);
        AssertAccounting(terminal.KgpImageStore, 8, 0);
    }

    private static Hex1bTerminal CreateTerminal(
        IHex1bTerminalWorkloadAdapter workload,
        Action<Hex1bTerminalGraphicsOptions>? configure = null)
    {
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true, SupportsSixel = true, CellPixelWidth = 10, CellPixelHeight = 20 })
            .WithDimensions(80, 24);
        if (configure is not null)
            builder.WithGraphics(configure);
        return builder.Build();
    }

    private static KgpCommand Command(uint width = 1, uint height = 1,
        KgpFormat format = KgpFormat.Rgba32, uint imageId = 1, uint imageNumber = 0, int moreData = 0)
        => new() { ImageId = imageId, ImageNumber = imageNumber, Width = width, Height = height,
            Format = format, Compression = 'z', MoreData = moreData };

    private static KgpParsedCommand.AnimationFrame ParseFrame(string controls)
    {
        Assert.IsTrue(KgpCommandParser.TryParse(controls, out var command, out _));
        return (KgpParsedCommand.AnimationFrame)command!;
    }

    private static byte[] FormatBytes(KgpFormat format)
        => format switch
        {
            KgpFormat.Rgb24 => [1, 2, 3],
            KgpFormat.Rgba32 => [1, 2, 3, 4],
            _ => Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a5xQAAAAASUVORK5CYII="),
        };

    private static byte[] Compress(byte[] data)
    {
        using var stream = new MemoryStream();
        using (var zlib = new ZLibStream(stream, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(data);
        return stream.ToArray();
    }

    private static void Send(Hex1bTerminal terminal, string controls, byte[] data)
        => Apply(terminal, KgpTestHelper.BuildCommand(controls, data));

    private static void Apply(Hex1bTerminal terminal, string text)
        => terminal.ApplyTokens(AnsiTokenizer.Tokenize(text));

    private static void AssertAccounting(KgpImageStore store, long expectedActual, long reserved)
    {
        Assert.AreEqual(expectedActual, store.TotalSize);
        Assert.AreEqual(reserved, store.ReservedDecodedBytes);
        Assert.AreEqual(expectedActual + reserved, store.ChargedSize);
    }

    private sealed class ResponseWorkload : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<string> _responses = Channel.CreateUnbounded<string>();
        internal ChannelReader<string> Responses => _responses.Reader;
        public event Action? Disconnected { add { } remove { } }
        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            _responses.Writer.TryWrite(Encoding.UTF8.GetString(data.Span));
            return ValueTask.CompletedTask;
        }
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        internal async Task<string> ReadAsync()
            => await _responses.Reader.ReadAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
}
