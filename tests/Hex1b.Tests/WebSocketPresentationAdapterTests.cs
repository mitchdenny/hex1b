using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace Hex1b.Tests;

[TestClass]
public class WebSocketPresentationAdapterTests
{
    [TestMethod]
    public void Resize_WithInvalidTracePath_DoesNotThrow()
    {
        using var webSocket = new StubWebSocket();
        var originalTracePath = Environment.GetEnvironmentVariable("HEX1B_WEBSOCKET_RESIZE_TRACE_FILE");

        try
        {
            Environment.SetEnvironmentVariable(
                "HEX1B_WEBSOCKET_RESIZE_TRACE_FILE",
                Path.GetTempPath());

            var adapter = new WebSocketPresentationAdapter(webSocket, 80, 24);

            adapter.Resize(120, 40, 9, 18, 9.5);

            Assert.AreEqual(120, adapter.Width);
            Assert.AreEqual(40, adapter.Height);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEX1B_WEBSOCKET_RESIZE_TRACE_FILE", originalTracePath);
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task WriteOutputAsync_SplitUtf8Scalar_UsesFragmentedTextMessage(
        int splitIndex)
    {
        using var webSocket = new StubWebSocket();
        await using var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        var bytes = "\U0010EEEE"u8.ToArray();

        await adapter.WriteOutputAsync(bytes.AsMemory(0, splitIndex));
        Assert.IsEmpty(webSocket.SentFrames);
        await adapter.WriteOutputAsync(bytes.AsMemory(splitIndex));

        Assert.AreEqual(1, webSocket.SentFrames.Count);
        Assert.AreEqual(WebSocketMessageType.Text, webSocket.SentFrames[0].MessageType);
        Assert.IsTrue(webSocket.SentFrames[0].EndOfMessage);
        TestSeq.AreEqual(bytes, webSocket.SentFrames[0].Data);
        AssertValidUtf8(webSocket.SentFrames);
    }

    [TestMethod]
    public async Task WriteOutputAsync_CompletePrefixBeforeSplitScalar_SendsPrefixImmediately()
    {
        using var webSocket = new StubWebSocket();
        await using var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        var scalar = "\U0010EEEE"u8.ToArray();

        await adapter.WriteOutputAsync(
            "ready:"u8.ToArray().Concat(scalar[..1]).ToArray());
        await adapter.WriteOutputAsync(scalar.AsMemory(1));

        Assert.AreEqual(2, webSocket.SentFrames.Count);
        TestSeq.AreEqual("ready:"u8.ToArray(), webSocket.SentFrames[0].Data);
        TestSeq.AreEqual(scalar, webSocket.SentFrames[1].Data);
        AssertValidUtf8(webSocket.SentFrames);
    }

    [TestMethod]
    public async Task WriteOutputAsync_InvalidContinuation_UsesReplacementBeforeFollowingEscape()
    {
        using var webSocket = new StubWebSocket();
        await using var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);

        await adapter.WriteOutputAsync(new byte[] { 0xF0 });
        await adapter.WriteOutputAsync(new byte[] { 0x1B });

        var frame = TestSeq.Single(webSocket.SentFrames);
        Assert.AreEqual("\uFFFD\x1b", Encoding.UTF8.GetString(frame.Data));
        AssertValidUtf8(webSocket.SentFrames);
    }

    [TestMethod]
    public async Task DisposeAsync_IncompleteScalar_FlushesValidReplacementWithoutDeadlock()
    {
        using var webSocket = new StubWebSocket();
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        await adapter.WriteOutputAsync(new byte[] { 0xF0 });

        await adapter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        var frame = TestSeq.Single(webSocket.SentFrames);
        Assert.AreEqual("\uFFFD", Encoding.UTF8.GetString(frame.Data));
        AssertValidUtf8(webSocket.SentFrames);
    }

    [TestMethod]
    public async Task WriteOutputAsync_CancelledSend_DoesNotCommitPendingUtf8()
    {
        using var webSocket = new StubWebSocket();
        await using var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        using var cancellation = new CancellationTokenSource();
        webSocket.BlockSends = true;

        var cancelledWrite = adapter.WriteOutputAsync(
            new byte[] { (byte)'A', 0xF0 },
            cancellation.Token).AsTask();
        await webSocket.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await cancelledWrite;
        webSocket.BlockSends = false;
        webSocket.ReleaseSend.TrySetResult();

        await adapter.WriteOutputAsync(new byte[] { 0x1B })
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2));

        var frame = TestSeq.Single(webSocket.SentFrames);
        Assert.AreEqual("\x1b", Encoding.UTF8.GetString(frame.Data));
        AssertValidUtf8(webSocket.SentFrames);
    }

    [TestMethod]
    public async Task WriteOutputAsync_ConcurrentCompleteWrites_AreSerializedAndValid()
    {
        using var webSocket = new StubWebSocket();
        await using var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        var expected = Enumerable.Range(0, 32)
            .Select(index => $"value-{index:D2}-\U0010EEEE")
            .ToArray();

        await Task.WhenAll(expected.Select(
                value => adapter.WriteOutputAsync(
                    Encoding.UTF8.GetBytes(value)).AsTask()))
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(expected.Length, webSocket.SentFrames.Count);
        AssertValidUtf8(webSocket.SentFrames);
        TestSeq.AreEqual(
            expected.Order(),
            webSocket.SentFrames
                .Select(frame => Encoding.UTF8.GetString(frame.Data))
                .Order());
    }

    [TestMethod]
    public async Task DisposeAsync_WriterAdmittedBeforeDisposal_DrainsBeforeClose()
    {
        using var webSocket = new StubWebSocket();
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        using var releaseState = new ManualResetEventSlim();
        var firstStateRead = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stateReadCount = 0;
        webSocket.StateProvider = () =>
        {
            if (Interlocked.Increment(ref stateReadCount) == 1)
            {
                firstStateRead.TrySetResult();
                releaseState.Wait();
            }
            return WebSocketState.Open;
        };

        var write = Task.Run(
            async () => await adapter.WriteOutputAsync("value"u8.ToArray()));
        await firstStateRead.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var disposal = adapter.DisposeAsync().AsTask();
        releaseState.Set();
        await Task.WhenAll(write, disposal).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(
            "value",
            Encoding.UTF8.GetString(TestSeq.Single(webSocket.SentFrames).Data));
        Assert.AreEqual(1, webSocket.CloseOutputCount);
    }

    [TestMethod]
    public async Task DisposeAsync_OutstandingReceive_FlushesAndClosesBeforeCancellation()
    {
        using var webSocket = new StubWebSocket
        {
            BlockReceivesUntilCancelled = true,
        };
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        var receive = adapter.ReadInputAsync().AsTask();
        await webSocket.ReceiveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await adapter.WriteOutputAsync(new byte[] { 0xF0 });

        await adapter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsTrue((await receive.WaitAsync(TimeSpan.FromSeconds(2))).IsEmpty);
        await adapter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(
            "\uFFFD",
            Encoding.UTF8.GetString(TestSeq.Single(webSocket.SentFrames).Data));
        AssertValidUtf8(webSocket.SentFrames);
        Assert.AreEqual(1, webSocket.CloseOutputCount);
        var lifecycle = webSocket.LifecycleEvents.ToArray();
        var sendIndex = Array.IndexOf(lifecycle, "send");
        var closeIndex = Array.IndexOf(lifecycle, "close-output");
        var cancelIndex = Array.IndexOf(lifecycle, "receive-cancelled");
        Assert.IsGreaterThanOrEqualTo(0, sendIndex);
        Assert.IsGreaterThan(sendIndex, closeIndex);
        Assert.IsGreaterThan(closeIndex, cancelIndex);

        await adapter.WriteOutputAsync("ignored"u8.ToArray());
        Assert.IsTrue((await adapter.ReadInputAsync()).IsEmpty);
        Assert.AreEqual(1, webSocket.SentFrames.Count);
    }

    [TestMethod]
    public async Task DisposeAsync_CloseReceived_SendsCloseAcknowledgement()
    {
        using var webSocket = new StubWebSocket
        {
            CurrentState = WebSocketState.CloseReceived,
        };
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);

        await adapter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        await adapter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(1, webSocket.CloseOutputCount);
        Assert.IsEmpty(webSocket.SentFrames);
    }

    [TestMethod]
    public async Task DisposeAsync_DisconnectedHandlerReentersDisposal_DoesNotDeadlock()
    {
        using var webSocket = new StubWebSocket();
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        var disconnected = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.Disconnected += () =>
        {
            adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
            disconnected.TrySetResult();
        };

        var first = adapter.DisposeAsync().AsTask();
        var second = adapter.DisposeAsync().AsTask();
        Assert.AreSame(first, second);
        await Task.WhenAll(first, second)
            .WaitAsync(TimeSpan.FromSeconds(2));
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(1, webSocket.CloseOutputCount);
    }

    [TestMethod]
    public async Task DisposeAsync_ResizeReaderReentersWithOutstandingReceive_DoesNotDeadlock()
    {
        using var webSocket = new StubWebSocket
        {
            BlockReceivesUntilCancelled = true,
            ResizeOnReceiveCall = 2,
        };
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        adapter.Resized += (_, _) =>
            adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
        var outstandingReceive = adapter.ReadInputAsync().AsTask();
        await webSocket.ReceiveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var resizeReceive = adapter.ReadInputAsync().AsTask();
        await Task.WhenAll(outstandingReceive, resizeReceive)
            .WaitAsync(TimeSpan.FromSeconds(2));
        await adapter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(1, webSocket.CloseOutputCount);
        Assert.AreEqual(WebSocketState.Aborted, webSocket.CurrentState);
    }

    [TestMethod]
    public async Task DisposeAsync_DetachedResizeDescendantAwaitsSharedCleanup()
    {
        using var webSocket = new StubWebSocket
        {
            BlockSends = true,
            ResizeOnReceiveCall = 1,
        };
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        var detachedStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task? detachedDisposal = null;
        adapter.Resized += (_, _) =>
        {
            detachedDisposal = Task.Run(async () =>
            {
                var disposal = adapter.DisposeAsync().AsTask();
                detachedStarted.TrySetResult();
                await disposal;
            });
            detachedStarted.Task.GetAwaiter().GetResult();
        };
        var write = adapter.WriteOutputAsync("value"u8.ToArray()).AsTask();
        await webSocket.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var resize = adapter.ReadInputAsync().AsTask();
        await detachedStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsNotNull(detachedDisposal);
        Assert.IsFalse(detachedDisposal.IsCompleted);
        webSocket.BlockSends = false;
        webSocket.ReleaseSend.TrySetResult();

        await Task.WhenAll(write, resize, detachedDisposal!)
            .WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(1, webSocket.CloseOutputCount);
    }

    [TestMethod]
    public async Task DisposeAsync_UnexpectedCleanupFailurePropagatesToAllCallers()
    {
        var failure = new InvalidOperationException("close failed");
        using var webSocket = new StubWebSocket
        {
            CloseOutputException = failure,
        };
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        var first = adapter.DisposeAsync().AsTask();
        var second = adapter.DisposeAsync().AsTask();
        Assert.AreSame(first, second);

        var firstError = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await first);
        var secondError = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await second);
        var repeatedError = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await adapter.DisposeAsync());

        Assert.AreSame(failure, firstError);
        Assert.AreSame(failure, secondError);
        Assert.AreSame(failure, repeatedError);
        await adapter.WriteOutputAsync("ignored"u8.ToArray());
        Assert.IsTrue((await adapter.ReadInputAsync()).IsEmpty);
    }

    [TestMethod]
    public async Task DisposeAsync_SynchronousWaitOnSingleThreadedContext_DoesNotDeadlock()
    {
        using var webSocket = new StubWebSocket();
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);

        await Task.Run(() =>
            {
                var previous = SynchronizationContext.Current;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new NonPumpingSynchronizationContext());
                    adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                }
            })
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(1, webSocket.CloseOutputCount);
    }

    [TestMethod]
    public async Task DisposeAsync_SynchronousWaitWithInflightIo_DoesNotDeadlock()
    {
        using var webSocket = new StubWebSocket
        {
            BlockReceivesUntilCancelled = true,
            BlockSends = true,
        };
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);

        var worker = Task.Run(() =>
        {
            var previous = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(
                    new NonPumpingSynchronizationContext());
                var write = adapter.WriteOutputAsync("value"u8.ToArray()).AsTask();
                var read = adapter.ReadInputAsync().AsTask();
                adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
                write.GetAwaiter().GetResult();
                Assert.IsTrue(read.GetAwaiter().GetResult().IsEmpty);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        });

        await Task.WhenAll(
            webSocket.SendStarted.Task,
            webSocket.ReceiveStarted.Task).WaitAsync(TimeSpan.FromSeconds(2));
        webSocket.BlockSends = false;
        webSocket.ReleaseSend.TrySetResult();
        await worker.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(1, webSocket.CloseOutputCount);
    }

    private static void AssertValidUtf8(
        IEnumerable<StubWebSocket.SentFrame> frames)
    {
        var strictUtf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);
        foreach (var frame in frames)
        {
            Assert.AreEqual(WebSocketMessageType.Text, frame.MessageType);
            Assert.IsTrue(frame.EndOfMessage);
            _ = strictUtf8.GetString(frame.Data);
        }
    }

    private sealed class NonPumpingSynchronizationContext :
        SynchronizationContext
    {
        public override void Post(
            SendOrPostCallback callback,
            object? state)
        {
        }
    }

    private sealed class StubWebSocket : WebSocket
    {
        internal readonly record struct SentFrame(
            byte[] Data,
            WebSocketMessageType MessageType,
            bool EndOfMessage);

        internal List<SentFrame> SentFrames { get; } = [];
        internal bool BlockSends { get; set; }
        internal TaskCompletionSource SendStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseSend { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReceiveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ConcurrentQueue<string> LifecycleEvents { get; } = new();
        internal bool BlockReceivesUntilCancelled { get; init; }
        internal int ResizeOnReceiveCall { get; init; }
        internal Exception? CloseOutputException { get; init; }
        internal int CloseOutputCount { get; private set; }
        internal WebSocketState CurrentState { get; set; } =
            WebSocketState.Open;

        public override WebSocketCloseStatus? CloseStatus => null;

        public override string? CloseStatusDescription => null;

        internal Func<WebSocketState>? StateProvider { get; set; }

        public override WebSocketState State =>
            StateProvider?.Invoke() ?? CurrentState;

        public override string? SubProtocol => null;

        public override void Abort()
        {
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            CurrentState = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            if (CloseOutputException is not null)
                throw CloseOutputException;
            CloseOutputCount++;
            LifecycleEvents.Enqueue("close-output");
            CurrentState = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var receiveCall = Interlocked.Increment(ref _receiveCallCount);
            if (receiveCall == ResizeOnReceiveCall)
            {
                var resize = """{"type":"resize","cols":100,"rows":40}"""u8;
                resize.CopyTo(buffer.Span);
                return new ValueWebSocketReceiveResult(
                    resize.Length,
                    WebSocketMessageType.Text,
                    endOfMessage: true);
            }

            if (!BlockReceivesUntilCancelled)
                throw new NotSupportedException();

            ReceiveStarted.TrySetResult();
            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    "A blocked receive unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
                LifecycleEvents.Enqueue("receive-cancelled");
                CurrentState = WebSocketState.Aborted;
                throw;
            }
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            => SendAsync(
                buffer.AsMemory(),
                messageType,
                endOfMessage,
                cancellationToken).AsTask();

        public override async ValueTask SendAsync(
            ReadOnlyMemory<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken = default)
        {
            if (BlockSends)
            {
                SendStarted.TrySetResult();
                await ReleaseSend.Task.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            SentFrames.Add(new SentFrame(
                buffer.ToArray(),
                messageType,
                endOfMessage));
            LifecycleEvents.Enqueue("send");
        }

        private int _receiveCallCount;
    }
}
