using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Hex1b.Tests.Diagnostics;

namespace Hex1b.Tests;

[TestClass]
public class ProteinViewGraphicsStreamTests
{
    internal const string Probe = "\x1b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\\x1b[c\x1b[16t\x1b[5n";
    internal const string ProbeReplies = "\x1b_Gi=31;OK\x1b\\\x1b[?62;4c\x1b[6;20;10t\x1b[0n";
    internal const string Marker = "capture-complete";

    [TestMethod]
    [DataRow(1)]
    [DataRow(7)]
    [DataRow(4096)]
    public async Task RawPump_PickerQueries_ReturnsOrderedCapabilitiesAndGeometry(int chunkSize)
    {
        await using var workload = new CapturedWorkloadAdapter(Chunks(Encoding.UTF8.GetBytes(Probe), chunkSize));
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(presentation).Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (workload.WrittenInput.Length < Encoding.UTF8.GetByteCount(ProbeReplies))
            await Task.Delay(10, timeout.Token);
        Assert.AreEqual(ProbeReplies, Encoding.UTF8.GetString(workload.WrittenInput));
    }

    [TestMethod]
    [DataRow(1, false)]
    [DataRow(7, false)]
    [DataRow(4096, false)]
    [DataRow(1, true)]
    [DataRow(7, true)]
    public async Task RawPump_UncompressedVirtualImage_RetainsPixelsAndVisiblePlacement(int chunkSize, bool png)
    {
        var bytes = BuildImage(compressed: false, png: png);
        await using var workload = new CapturedWorkloadAdapter(Chunks(bytes, chunkSize));
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(presentation).Build();
        await WaitForMarkerAsync(terminal);
        using var snapshot = terminal.CreateSnapshot();
        var image = TestSeq.Single(snapshot.KgpImages.Values);
        Assert.AreEqual(png ? KgpFormat.Png : KgpFormat.Rgba32, image.Format);
        Assert.IsNotEmpty(snapshot.KgpPlacements);
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        if (!png)
            CollectionAssert.AreEqual(Pixels(), image.Data);
        using var metadata = Metadata(await presentation.ReadFrameAsync(TestContext.Current.CancellationToken));
        Assert.IsTrue(metadata.RootElement.GetProperty("placements").GetArrayLength() > 0);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(7)]
    [DataRow(997)]
    [DataRow(4096)]
    public async Task RawPump_CompressedVirtualImage_RetainsExactPixelsAndVisiblePlacement(int chunkSize)
    {
        await using var workload = new CapturedWorkloadAdapter(Chunks(BuildImage(compressed: true), chunkSize));
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(presentation).Build();
        await WaitForMarkerAsync(terminal);
        using var snapshot = terminal.CreateSnapshot();
        var image = TestSeq.Single(snapshot.KgpImages.Values);
        Assert.IsTrue(image.IsZlibCompressed);
        Assert.AreEqual(KgpFormat.Rgba32, image.Format);
        CollectionAssert.AreEqual(Pixels(), image.Data);
        Assert.IsNotEmpty(snapshot.KgpPlacements);
        Assert.AreEqual(1, terminal.KgpVirtualPlacementCount);
        using var metadata = Metadata(await presentation.ReadFrameAsync(TestContext.Current.CancellationToken));
        Assert.IsTrue(metadata.RootElement.GetProperty("placements").GetArrayLength() > 0);
    }

    [TestMethod]
    public async Task Capture_ExplicitSyntheticInvestigation_WritesCompressionParityEvidence()
    {
        var directory = Environment.GetEnvironmentVariable("HEX1B_GRAPHICS_EVIDENCE");
        if (string.IsNullOrEmpty(directory))
            Assert.Inconclusive("Set HEX1B_GRAPHICS_EVIDENCE to a new private directory for opt-in investigation.");
        Directory.CreateDirectory(directory);
        foreach (var (name, compressed, quiet, png) in new[]
        {
            ("rgba-control", false, 2, false),
            ("png-control", false, 2, true),
            ("zlib-original-quiet", true, 2, false),
            ("zlib-diagnostic-replies", true, 0, false)
        })
        {
            var chunks = Chunks(BuildImage(compressed, quiet, png), 7);
            await using var workload = new CapturedWorkloadAdapter(chunks);
            await using var capture = new DuplexPtyCapture(workload, 1024 * 1024, 10000);
            await using var presentation = new Hwt1PresentationAdapter(20, 10);
            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(capture).WithPresentation(presentation).Build();
            await WaitForMarkerAsync(terminal);
            using var snapshot = terminal.CreateSnapshot();
            var frame = await presentation.ReadFrameAsync(TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, $"{name}.hwt"), frame.ToArray());
            capture.Complete("synthetic-comparison");
            await capture.SaveAsync(Path.Combine(directory, $"{name}.json"), new
            {
                provenance = "synthetic: pinned ProteinView control/payload/placeholder form, not an application recording",
                compressed, quiet, png, expectedVisibleImages = 1,
                actualImages = snapshot.KgpImages.Count,
                actualPlacements = snapshot.KgpPlacements.Count,
                virtualPlacements = terminal.KgpVirtualPlacementCount,
                replies = Convert.ToBase64String(workload.WrittenInput)
            }, TestContext.Current.CancellationToken);
            Assert.IsNotEmpty(snapshot.KgpPlacements, "Compressed and uncompressed graphics must both render.");
        }
    }

    internal static byte[] BuildImage(bool compressed, int quiet = 2, bool png = false)
    {
        var pixels = Pixels();
        byte[] data;
        if (png)
            data = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAMAAAADCAYAAABWKLW/AAAAEUlEQVR4nGP4z8DwH4YZcHIAXdcR79xPMRAAAAAASUVORK5CYII=");
        else if (compressed)
        {
            using var output = new MemoryStream();
            using (var encoder = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
                encoder.Write(pixels);
            data = output.ToArray();
        }
        else
            data = pixels;
        var payload = Convert.ToBase64String(data);
        var outputText = new StringBuilder("\x1b[2J\x1b[2;2H");
        for (var offset = 0; offset < payload.Length; offset += 4096)
        {
            var count = Math.Min(4096, payload.Length - offset);
            var more = offset + count < payload.Length ? 1 : 0;
            var control = offset == 0
                ? $"q={quiet},i=1,a=T,U=1,f={(png ? 100 : 32)},{(compressed ? "o=z," : "")}t=d,s={(png ? 3 : 40)},v={(png ? 3 : 20)},c=4,r=1,m={more}"
                : $"q={quiet},m={more}";
            outputText.Append("\x1b_G").Append(control).Append(';').Append(payload, offset, count).Append("\x1b\\");
        }
        var zero = new Rune(KgpUnicodePlaceholderDiacritics.CodePoints[0]).ToString();
        outputText.Append("\x1b[s\x1b[38;2;0;0;1m\U0010EEEE")
            .Append(zero).Append(zero).Append(zero)
            .Append("\U0010EEEE\U0010EEEE\U0010EEEE\x1b[u\x1b[4C\x1b[1B")
            .Append("\x1b[0m\x1b[10;1H").Append(Marker);
        return Encoding.UTF8.GetBytes(outputText.ToString());
    }

    private static byte[] Pixels()
        => Enumerable.Range(0, 40 * 20).SelectMany(_ => new byte[] { 255, 0, 0, 255 }).ToArray();

    internal static byte[][] Chunks(byte[] bytes, int size)
        => bytes.Chunk(size).Select(chunk => chunk.ToArray()).ToArray();

    internal static async Task WaitForMarkerAsync(Hex1bTerminal terminal)
    {
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(snapshot => snapshot.ContainsText(Marker), TimeSpan.FromSeconds(5), "raw stream fully applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    internal static JsonDocument Metadata(ReadOnlyMemory<byte> frame)
    {
        var length = BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
        return JsonDocument.Parse(frame.Slice(8, length));
    }
}
