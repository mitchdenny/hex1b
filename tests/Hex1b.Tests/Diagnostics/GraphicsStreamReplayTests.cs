using System.Text;

namespace Hex1b.Tests.Diagnostics;

[TestClass]
public class GraphicsStreamReplayTests
{
    [TestMethod]
    public async Task Replay_ExplicitReducedStream_WritesStateAndBrowserFrame()
    {
        var path = Environment.GetEnvironmentVariable("HEX1B_GRAPHICS_STREAM");
        var directory = Environment.GetEnvironmentVariable("HEX1B_GRAPHICS_REPLAY_OUTPUT");
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory))
            Assert.Inconclusive("Set HEX1B_GRAPHICS_STREAM and HEX1B_GRAPHICS_REPLAY_OUTPUT for reviewed-stream replay.");
        Assert.IsTrue(File.Exists(path));
        Assert.IsTrue(new FileInfo(path).Length <= 8 * 1024 * 1024, "Reviewed replay input must fit the 8 MiB budget.");
        Assert.IsFalse(Directory.Exists(directory), "Use a new output directory.");
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(directory);
        else
            Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        var marker = Encoding.UTF8.GetBytes($"\x1b[24;1H{ProteinViewGraphicsStreamTests.Marker}");
        var chunks = ProteinViewGraphicsStreamTests.Chunks(bytes, 997).Append(marker).ToArray();
        await using var workload = new CapturedWorkloadAdapter(chunks);
        await using var capture = new DuplexPtyCapture(workload, 8 * 1024 * 1024, 10000);
        await using var producer = new Hmp1PresentationAdapter(80, 24);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(capture).WithPresentation(producer).WithDimensions(80, 24).Build();
        await using var presentation = await producer.CreateBrowserViewAsync("Reduced stream", TestContext.Current.CancellationToken);
        await ProteinViewGraphicsStreamTests.WaitForMarkerAsync(terminal);
        var frame = await presentation.ReadFrameAsync(TestContext.Current.CancellationToken);
        using var snapshot = terminal.CreateSnapshot();
        await File.WriteAllBytesAsync(Path.Combine(directory, "frame.hwt"), frame.ToArray(), TestContext.Current.CancellationToken);
        capture.Complete("reviewed-stream-replayed");
        await capture.SaveAsync(Path.Combine(directory, "transcript.json"), new
        {
            provenance = "explicit external reduced stream; inspect its reduction manifest for transformations",
            inputName = Path.GetFileName(path),
            inputSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
            replayChunkSize = 997,
            addedMarker = "cursor move and text on last row to establish processing boundary",
            images = snapshot.KgpImages.Count,
            placements = snapshot.KgpPlacements.Count,
            virtualPlacements = terminal.KgpVirtualPlacementCount,
            imageData = snapshot.KgpImages.Values.Select(image => new
            {
                image.ImageId, image.Width, image.Height, format = image.Format.ToString(),
                length = image.Data.Length,
                sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.Data))
            }),
            replies = Convert.ToBase64String(workload.WrittenInput)
        }, TestContext.Current.CancellationToken);
    }
}
