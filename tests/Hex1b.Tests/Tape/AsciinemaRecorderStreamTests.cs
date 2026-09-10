using System.Text;
using System.Text.Json;
using Hex1b.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hex1b.Tests.Tape;

[TestClass]
public class AsciinemaRecorderStreamTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [TestMethod]
    public async Task FlushAsync_BorrowedStream_KeepsOneHeaderAndLeavesStreamOpen()
    {
        using var stream = new MemoryStream();
        await using var recorder = new AsciinemaRecorder(stream,
            new AsciinemaRecorderOptions { CaptureEnvironment = false }, leaveOpen: true);
        var filter = (IHex1bTerminalWorkloadFilter)recorder;
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UnixEpoch, Cancellation);
        await filter.OnOutputAsync([new TextToken("first")], TimeSpan.Zero, Cancellation);
        await filter.OnOutputAsync([new TextToken("second")], TimeSpan.FromSeconds(1), Cancellation);
        await recorder.FlushAsync(Cancellation);
        await recorder.StopRecordingAsync(Cancellation);
        await recorder.DisposeAsync();

        Assert.IsTrue(stream.CanWrite);
        AssertRecording(stream.ToArray(), "first", "second");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StopOrDisposeAsync_OwnedStream_ClosesStream(bool stop)
    {
        var stream = new MemoryStream();
        await using var recorder = new AsciinemaRecorder(stream,
            new AsciinemaRecorderOptions { CaptureEnvironment = false });
        var filter = (IHex1bTerminalWorkloadFilter)recorder;
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UnixEpoch, Cancellation);
        await filter.OnOutputAsync([new TextToken("first")], TimeSpan.Zero, Cancellation);
        Assert.IsTrue(stream.CanWrite, "Auto-flush must not close the stream.");
        if (stop)
            await recorder.StopRecordingAsync(Cancellation);
        else
            await recorder.DisposeAsync();
        Assert.IsFalse(stream.CanWrite);
        Assert.IsFalse(recorder.IsRecording);
        AssertRecording(stream.ToArray(), "first");
    }

    [TestMethod]
    public async Task FlushAsync_NonSeekableStream_DoesNotSeekOrReopen()
    {
        await using var stream = new GatedWriteStream(blockFirstWrite: false);
        await using var recorder = new AsciinemaRecorder(stream,
            new AsciinemaRecorderOptions { CaptureEnvironment = false }, leaveOpen: true);
        var filter = (IHex1bTerminalWorkloadFilter)recorder;
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UnixEpoch, Cancellation);
        await filter.OnOutputAsync([new TextToken("first")], TimeSpan.Zero, Cancellation);
        await filter.OnOutputAsync([new TextToken("second")], TimeSpan.FromSeconds(1), Cancellation);
        await recorder.StopRecordingAsync(Cancellation);

        Assert.IsTrue(stream.CanWrite);
        AssertRecording(stream.ToArray(), "first", "second");
        Assert.ThrowsExactly<InvalidOperationException>(() => recorder.StartRecording("unused.cast"));
    }

    [TestMethod]
    public async Task FlushAsync_CreateNewStreamAndReplacedPath_NeverTouchesReplacement()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), $"recorder-{Guid.NewGuid():N}.cast");
        var moved = path + ".moved";
        try
        {
            var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            await using var recorder = new AsciinemaRecorder(stream,
                new AsciinemaRecorderOptions { CaptureEnvironment = false });
            var filter = (IHex1bTerminalWorkloadFilter)recorder;
            await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UnixEpoch, Cancellation);
            await filter.OnOutputAsync([new TextToken("first")], TimeSpan.Zero, Cancellation);

            File.Move(path, moved);
            await File.WriteAllTextAsync(path, "replacement", Cancellation);
            await filter.OnOutputAsync([new TextToken("second")], TimeSpan.FromSeconds(1), Cancellation);
            await recorder.StopRecordingAsync(Cancellation);

            Assert.AreEqual("replacement", await File.ReadAllTextAsync(path, Cancellation));
            AssertRecording(await File.ReadAllBytesAsync(moved, Cancellation), "first", "second");
        }
        finally
        {
            File.Delete(path);
            File.Delete(moved);
        }
    }

    [TestMethod]
    public async Task FlushAsync_CancelledBehindAnotherFlush_RetainsQueuedEventsForRetry()
    {
        await using var stream = new GatedWriteStream(blockFirstWrite: true);
        await using var recorder = new AsciinemaRecorder(stream,
            new AsciinemaRecorderOptions { AutoFlush = false, CaptureEnvironment = false }, leaveOpen: true);
        var filter = (IHex1bTerminalWorkloadFilter)recorder;
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UnixEpoch, Cancellation);
        await filter.OnOutputAsync([new TextToken("first")], TimeSpan.Zero, Cancellation);
        var firstFlush = recorder.FlushAsync(Cancellation);
        await stream.Entered.Task.WaitAsync(Cancellation);
        await filter.OnOutputAsync([new TextToken("second")], TimeSpan.FromSeconds(1), Cancellation);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var secondFlush = recorder.FlushAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => secondFlush);
        Assert.AreEqual(1, recorder.PendingEventCount);
        stream.Release.TrySetResult();
        await firstFlush;
        await recorder.FlushAsync(Cancellation);

        AssertRecording(stream.ToArray(), "first", "second");
    }

    [TestMethod]
    public void Constructor_ReadOnlyStream_RejectsWithoutClosingCallerStream()
    {
        using var stream = new MemoryStream([1, 2, 3], writable: false);
        Assert.ThrowsExactly<ArgumentException>(() => new AsciinemaRecorder(stream));
        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    public async Task FlushAsync_CancelledStreamWrite_ReportsCancellationAndAllowsCleanup()
    {
        await using var stream = new GatedWriteStream(blockFirstWrite: true);
        await using var recorder = new AsciinemaRecorder(stream,
            new AsciinemaRecorderOptions { AutoFlush = false, CaptureEnvironment = false });
        var filter = (IHex1bTerminalWorkloadFilter)recorder;
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UnixEpoch, Cancellation);
        await filter.OnOutputAsync([new TextToken("first")], TimeSpan.Zero, Cancellation);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var flush = recorder.FlushAsync(cancellation.Token);
        await stream.Entered.Task.WaitAsync(Cancellation);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => flush);
        await recorder.DisposeAsync();
        Assert.IsFalse(stream.CanWrite);
    }

    private static void AssertRecording(byte[] bytes, params string[] output)
    {
        var lines = Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(output.Length + 1, lines.Length);
        using var header = JsonDocument.Parse(lines[0]);
        Assert.AreEqual(2, header.RootElement.GetProperty("version").GetInt32());
        Assert.AreEqual(80, header.RootElement.GetProperty("width").GetInt32());
        Assert.AreEqual(24, header.RootElement.GetProperty("height").GetInt32());
        for (var index = 0; index < output.Length; index++)
        {
            using var item = JsonDocument.Parse(lines[index + 1]);
            Assert.AreEqual("o", item.RootElement[1].GetString());
            Assert.AreEqual(output[index], item.RootElement[2].GetString());
            Assert.AreEqual((double)index, item.RootElement[0].GetDouble());
        }
    }

    private sealed class GatedWriteStream(bool blockFirstWrite) : Stream
    {
        private readonly MemoryStream _content = new();
        private bool _blocked;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal byte[] ToArray() => _content.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _content.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => _content.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _content.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _content.Write(buffer, offset, count);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (blockFirstWrite && !_blocked)
            {
                _blocked = true;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            await _content.WriteAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _content.Dispose();
            base.Dispose(disposing);
        }
    }
}
