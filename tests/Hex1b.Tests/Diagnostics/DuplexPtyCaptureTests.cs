using System.Diagnostics;
using System.Text.Json;

namespace Hex1b.Tests.Diagnostics;

[TestClass]
public class DuplexPtyCaptureTests
{
    [TestMethod]
    public async Task Capture_BinaryChunksAndInput_PreservesExactBytesAndOrdering()
    {
        byte[][] chunks = [[0xff, 0, 0xc3], [], [0xa9, 0x1b, 0x5b]];
        await using var replay = new CapturedWorkloadAdapter(chunks);
        await using var capture = new DuplexPtyCapture(replay, maximumBytes: 100, maximumEvents: 20);
        foreach (var chunk in chunks)
            TestSeq.AreEqual(chunk, (await capture.ReadOutputAsync()).ToArray());
        await capture.WriteInputAsync(new byte[] { 0, 0xfe, 0x1b });

        var records = capture.Records;
        TestSeq.AreEqual(chunks, records.Where(record => record.Kind == "output")
            .Select(record => record.Data).ToArray());
        TestSeq.AreEqual(new byte[] { 0, 0xfe, 0x1b }, replay.WrittenInput);
        TestSeq.AreEqual(Enumerable.Range(1, records.Count).Select(value => (long)value),
            records.Select(record => record.Sequence));
        Assert.IsTrue(records.Zip(records.Skip(1)).All(pair =>
            pair.First.Timestamp <= pair.Second.Timestamp));
        Assert.AreEqual(records[^2].OperationId, records[^1].OperationId);
        Assert.AreEqual("input-start", records[^2].Kind);
        Assert.AreEqual("input-complete", records[^1].Kind);
    }

    [TestMethod]
    public async Task WriteInputAsync_WhileOutputReadBlocked_ProgressesIndependently()
    {
        var inner = new ControlledAdapter();
        await using var capture = new DuplexPtyCapture(inner, 100, 20);
        var read = capture.ReadOutputAsync().AsTask();
        await inner.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await capture.WriteInputAsync(new byte[] { 7 }).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(read.IsCompleted);
        inner.Output.TrySetResult(new byte[] { 8 });
        TestSeq.AreEqual(new byte[] { 8 }, (await read).ToArray());
        TestSeq.AreEqual(new[] { "input-start", "input-complete", "output" },
            capture.Records.Select(record => record.Kind));
        Assert.AreNotEqual(capture.Records[0].OperationId, capture.Records[2].OperationId);
    }

    [TestMethod]
    public async Task Records_CallerAndSnapshotMutation_DoesNotChangeRecording()
    {
        var output = new byte[] { 1, 2 };
        var input = new byte[] { 3, 4 };
        var inner = new ControlledAdapter();
        inner.Output.TrySetResult(output);
        await using var capture = new DuplexPtyCapture(inner, 100, 20);
        _ = await capture.ReadOutputAsync();
        await capture.WriteInputAsync(input);
        output[0] = 9;
        input[0] = 9;
        var snapshot = capture.Records;
        snapshot[0].Bytes![0] = 9;
        snapshot[1].Bytes![0] = 9;
        TestSeq.AreEqual(new byte[] { 1, 2 }, capture.Records[0].Bytes!);
        TestSeq.AreEqual(new byte[] { 3, 4 }, capture.Records[1].Bytes!);
    }

    [TestMethod]
    public async Task Replay_OriginalAndWrittenSnapshotMutation_DoesNotChangeBytes()
    {
        var chunk = new byte[] { 0xff, 1 };
        await using var replay = new CapturedWorkloadAdapter([chunk]);
        chunk[0] = 0;
        TestSeq.AreEqual(new byte[] { 0xff, 1 }, (await replay.ReadOutputAsync()).ToArray());
        var input = new byte[] { 2, 3 };
        await replay.WriteInputAsync(input);
        input[0] = 0;
        replay.WrittenBytes[0] = 0;
        await replay.WriteInputAsync(new byte[] { 4 });
        TestSeq.AreEqual(new byte[] { 2, 3, 4 }, replay.WrittenBytes);
    }

    [TestMethod]
    public async Task WriteInputAsync_FaultAndCancellation_HaveDistinctOutcomes()
    {
        var inner = new ControlledAdapter { WriteFailure = new IOException("write failed") };
        await using var capture = new DuplexPtyCapture(inner, 100, 20);
        await Assert.ThrowsExactlyAsync<IOException>(() =>
            capture.WriteInputAsync(new byte[] { 1 }).AsTask());
        inner.WriteFailure = new OperationCanceledException();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            capture.WriteInputAsync(new byte[] { 2 }).AsTask());
        TestSeq.AreEqual(new[]
        {
            "input-start", "input-faulted",
            "input-start", "input-canceled"
        }, capture.Records.Select(record => record.Kind));
        Assert.AreEqual(capture.Records[0].OperationId, capture.Records[1].OperationId);
        Assert.AreEqual(capture.Records[2].OperationId, capture.Records[3].OperationId);
        Assert.IsFalse(capture.IsComplete);
    }

    [TestMethod]
    public async Task ReadOutputAsync_CanceledWhileBlocked_RecordsCancellation()
    {
        await using var replay = new CapturedWorkloadAdapter([]);
        await using var capture = new DuplexPtyCapture(replay, 100, 20);
        using var canceled = new CancellationTokenSource();
        var read = capture.ReadOutputAsync(canceled.Token).AsTask();
        Assert.IsFalse(read.IsCompleted);
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => read);
        Assert.AreEqual("output-canceled", TestSeq.Single(capture.Records).Kind);
    }

    [TestMethod]
    public async Task ReadOutputAsync_Faulted_RecordsFaultWithoutInventingOutput()
    {
        var inner = new ControlledAdapter();
        inner.Output.TrySetException(new IOException("read failed"));
        await using var capture = new DuplexPtyCapture(inner, 100, 20);
        await Assert.ThrowsExactlyAsync<IOException>(() => capture.ReadOutputAsync().AsTask());
        var record = TestSeq.Single(capture.Records);
        Assert.AreEqual("output-faulted", record.Kind);
        Assert.IsNull(record.Bytes);
    }

    [TestMethod]
    [DataRow(1L, 20, false)]
    [DataRow(100L, 1, true)]
    public async Task Capture_LimitExceeded_FailsDurablyAndSignalsRunner(
        long maxBytes, int maxEvents, bool completionEventOverflows)
    {
        await using var replay = new CapturedWorkloadAdapter([]);
        await using var capture = new DuplexPtyCapture(replay, maxBytes, maxEvents);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            capture.WriteInputAsync(new byte[] { 1, 2 }).AsTask());
        Assert.IsTrue(capture.FailureToken.IsCancellationRequested);
        Assert.IsNotNull(capture.Failure);
        Assert.AreEqual("failed", capture.Status);
        capture.Complete("success");
        Assert.IsFalse(capture.IsComplete);
        Assert.AreEqual("failed", capture.Status);
        Assert.AreEqual(completionEventOverflows ? 2 : 0, replay.WrittenBytes.Length);
        Assert.IsTrue(capture.Records.Count <= maxEvents);
        Assert.IsTrue(capture.Records.Sum(record => record.Bytes?.Length ?? 0) <= maxBytes);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            capture.ResizeAsync(80, 24).AsTask());
    }

    [TestMethod]
    public async Task ReadOutputAsync_ByteLimitExceeded_DoesNotReturnUnrecordedSuccess()
    {
        await using var replay = new CapturedWorkloadAdapter([new byte[] { 1, 2 }]);
        await using var capture = new DuplexPtyCapture(replay, 1, 20);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => capture.ReadOutputAsync().AsTask());
        Assert.AreEqual(0, capture.Records.Count);
        Assert.IsTrue(capture.FailureToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task ResizeDisconnectAndDispose_ForwardsLifecycleWithoutCompletingCapture()
    {
        var inner = new ControlledAdapter();
        var capture = new DuplexPtyCapture(inner, 100, 20);
        var disconnected = 0;
        capture.Disconnected += () => disconnected++;
        await capture.ResizeAsync(192, 54);
        Assert.AreEqual((192, 54), inner.LastResize);
        inner.Disconnect();
        Assert.AreEqual(1, disconnected);
        await capture.DisposeAsync();
        await capture.DisposeAsync();
        inner.Disconnect();
        Assert.AreEqual(1, disconnected);
        Assert.AreEqual(1, inner.DisposeCount);
        Assert.IsFalse(capture.IsComplete);
        TestSeq.AreEqual(new[] { "resize-start", "resize-completed", "disconnected", "disposed" },
            capture.Records.Select(record => record.Kind));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            capture.WriteInputAsync(new byte[] { 1 }).AsTask());
    }

    [TestMethod]
    public async Task Replay_ExhaustedOutput_WaitsForCancellationOrDispose()
    {
        var replay = new CapturedWorkloadAdapter([]);
        using var canceled = new CancellationTokenSource();
        var canceledRead = replay.ReadOutputAsync(canceled.Token).AsTask();
        Assert.IsFalse(canceledRead.IsCompleted);
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => canceledRead);
        var disposedRead = replay.ReadOutputAsync().AsTask();
        Assert.IsFalse(disposedRead.IsCompleted);
        var disconnects = 0;
        replay.Disconnected += () => disconnects++;
        await replay.ResizeAsync(90, 30);
        Assert.AreEqual((90, 30), replay.LastResize);
        await replay.DisposeAsync();
        await replay.DisposeAsync();
        Assert.IsTrue((await disposedRead.WaitAsync(TimeSpan.FromSeconds(5))).IsEmpty);
        Assert.AreEqual(1, disconnects);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => replay.ReadOutputAsync().AsTask());
    }

    [TestMethod]
    public async Task Replay_PreCanceledOperations_DoNotConsumeOrWrite()
    {
        await using var replay = new CapturedWorkloadAdapter([new byte[] { 1 }]);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            replay.ReadOutputAsync(canceled.Token).AsTask());
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            replay.WriteInputAsync(new byte[] { 2 }, canceled.Token).AsTask());
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            replay.ResizeAsync(90, 30, canceled.Token).AsTask());
        TestSeq.AreEqual(new byte[] { 1 }, (await replay.ReadOutputAsync()).ToArray());
        Assert.AreEqual(0, replay.WrittenBytes.Length);
        Assert.IsNull(replay.LastResize);
    }

    [TestMethod]
    public async Task SaveAsync_WithoutExplicitCompletion_PersistsIncompleteState()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), $"duplex-incomplete-{Guid.NewGuid():N}.json");
        try
        {
            await using var capture = new DuplexPtyCapture(new CapturedWorkloadAdapter([]), 100, 20);
            await capture.DisposeAsync();
            await capture.SaveAsync(path, new { }, TestContext.Current.CancellationToken);
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Assert.IsFalse(json.RootElement.GetProperty("IsComplete").GetBoolean());
            Assert.AreEqual("incomplete", json.RootElement.GetProperty("Status").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SaveAsync_CompleteOrFailed_UsesVersionedPrivateCreateNewTranscript(bool fail)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), $"duplex-capture-{Guid.NewGuid():N}.json");
        try
        {
            await using var replay = new CapturedWorkloadAdapter([new byte[] { 0, 0xff, 0xc3 }]);
            await using var capture = new DuplexPtyCapture(replay, 3, 20);
            _ = await capture.ReadOutputAsync();
            Assert.IsFalse(capture.IsComplete);
            Assert.AreEqual("incomplete", capture.Status);
            if (fail)
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                    capture.WriteInputAsync(new byte[] { 1 }).AsTask());
            capture.Complete("success");
            await capture.SaveAsync(path, new { Fixture = "binary" }, TestContext.Current.CancellationToken);
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            var root = json.RootElement;
            Assert.AreEqual(1, root.GetProperty("Version").GetInt32());
            Assert.AreEqual(Stopwatch.Frequency, root.GetProperty("StopwatchFrequency").GetInt64());
            Assert.AreEqual(!fail, root.GetProperty("IsComplete").GetBoolean());
            Assert.AreEqual(fail ? "failed" : "success", root.GetProperty("Status").GetString());
            Assert.AreEqual(fail ? JsonValueKind.String : JsonValueKind.Null,
                root.GetProperty("Failure").ValueKind);
            TestSeq.AreEqual(new byte[] { 0, 0xff, 0xc3 },
                root.GetProperty("Records")[0].GetProperty("Bytes").GetBytesFromBase64());
            Assert.AreEqual("binary", root.GetProperty("Metadata").GetProperty("Fixture").GetString());
            Assert.IsFalse(root.TryGetProperty("Environment", out _));
            if (!OperatingSystem.IsWindows())
                Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            await Assert.ThrowsExactlyAsync<IOException>(() =>
                capture.SaveAsync(path, new { }, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class ControlledAdapter : IHex1bTerminalWorkloadAdapter
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ReadOnlyMemory<byte>> Output { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? WriteFailure { get; set; }
        public (int Width, int Height)? LastResize { get; private set; }
        public int DisposeCount { get; private set; }
        public event Action? Disconnected;

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            ReadStarted.TrySetResult();
            return await Output.Task.WaitAsync(ct);
        }

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) =>
            WriteFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(WriteFailure);

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        {
            LastResize = (width, height);
            return ValueTask.CompletedTask;
        }

        public void Disconnect() => Disconnected?.Invoke();

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            Output.TrySetResult(ReadOnlyMemory<byte>.Empty);
            return ValueTask.CompletedTask;
        }
    }
}
