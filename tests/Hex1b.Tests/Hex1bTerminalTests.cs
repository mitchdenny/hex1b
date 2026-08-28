using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the Hex1bTerminal virtual terminal emulator.
/// </summary>
[TestClass]
public class Hex1bTerminalTests
{
    private sealed class QueuedInputPresentationAdapter : IHex1bTerminalPresentationAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _input = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly List<byte> _output = [];
        private TaskCompletionSource _outputChanged =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Width => 80;
        public int Height => 24;
        public TerminalCapabilities Capabilities => new()
        {
            SupportsMouse = true,
            Supports256Colors = true,
            SupportsTrueColor = true,
            SupportsKgp = true
        };

        public event Action<int, int>? Resized
        {
            add { }
            remove { }
        }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public void EnqueueInput(string text)
            => _input.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

        public byte[] CapturedOutput
        {
            get
            {
                lock (_output)
                {
                    return [.. _output];
                }
            }
        }

        public async Task WaitForOutputLengthAsync(
            int minimumLength,
            CancellationToken ct)
        {
            while (true)
            {
                Task outputChanged;
                lock (_output)
                {
                    if (_output.Count >= minimumLength)
                        return;

                    outputChanged = _outputChanged.Task;
                }

                await outputChanged.WaitAsync(ct);
            }
        }

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            TaskCompletionSource outputChanged;
            lock (_output)
            {
                foreach (var value in data.Span)
                    _output.Add(value);

                outputChanged = _outputChanged;
                _outputChanged = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            outputChanged.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            while (await _input.Reader.WaitToReadAsync(ct))
            {
                if (_input.Reader.TryRead(out var data))
                    return data;
            }

            return ReadOnlyMemory<byte>.Empty;
        }

        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);

        public ValueTask DisposeAsync()
        {
            _input.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingInputPresentationAdapter(Exception exception) : IHex1bTerminalPresentationAdapter
    {
        private readonly Exception _exception = exception;
        private bool _hasThrown;

        public int Width => 80;
        public int Height => 24;
        public TerminalCapabilities Capabilities => new()
        {
            SupportsMouse = true,
            Supports256Colors = true,
            SupportsTrueColor = true
        };

        public event Action<int, int>? Resized
        {
            add { }
            remove { }
        }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            if (!_hasThrown)
            {
                _hasThrown = true;
                return ValueTask.FromException<ReadOnlyMemory<byte>>(_exception);
            }

            return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        }

        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingWorkloadAdapter(Exception exception) : IHex1bTerminalWorkloadAdapter
    {
        private readonly Exception _exception = exception;

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.FromException(_exception);

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingResizeWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        public int? ResizeWidth { get; private set; }
        public int? ResizeHeight { get; private set; }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        {
            ResizeWidth = width;
            ResizeHeight = height;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingDisposePresentationAdapter : IHex1bTerminalPresentationAdapter
    {
        public bool DisposeAsyncCalled { get; private set; }

        public int Width => 80;
        public int Height => 24;
        public TerminalCapabilities Capabilities => new()
        {
            SupportsMouse = true,
            Supports256Colors = true,
            SupportsTrueColor = true
        };

        public event Action<int, int>? Resized
        {
            add { }
            remove { }
        }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default) => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCalled = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingDisposeWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        public bool DisposeAsyncCalled { get; private set; }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCalled = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DelayingRecordingWorkloadAdapter(ReadOnlyMemory<byte> firstOutput) : IHex1bTerminalWorkloadAdapter
    {
        private readonly ReadOnlyMemory<byte> _firstOutput = firstOutput;
        private readonly List<string> _writes = [];
        private readonly TaskCompletionSource _writesObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;
        private int _activeWrites;
        private int _writeCount;
        private int _concurrentWriteDetected;

        public bool ConcurrentWriteDetected => Volatile.Read(ref _concurrentWriteDetected) != 0;

        public IReadOnlyList<string> Writes
        {
            get
            {
                lock (_writes)
                {
                    return _writes.ToArray();
                }
            }
        }

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref _readCount, 1) == 0)
            {
                return ValueTask.FromResult(_firstOutput);
            }

            return WaitForCancellationAsync(ct);
        }

        public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _activeWrites) > 1)
            {
                Interlocked.Exchange(ref _concurrentWriteDetected, 1);
            }

            try
            {
                await Task.Delay(30, ct);

                lock (_writes)
                {
                    _writes.Add(Encoding.UTF8.GetString(data.Span));
                }

                if (Interlocked.Increment(ref _writeCount) >= 2)
                {
                    _writesObserved.TrySetResult();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeWrites);
            }
        }

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task WaitForTwoWritesAsync() => _writesObserved.Task;

        private static async ValueTask<ReadOnlyMemory<byte>> WaitForCancellationAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
            }

            return ReadOnlyMemory<byte>.Empty;
        }
    }

    [TestMethod]
    public async Task Constructor_InitializesWithCorrectDimensions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        Assert.AreEqual(80, terminal.Width);
        Assert.AreEqual(24, terminal.Height);
    }

    [TestMethod]
    public async Task Constructor_InitializesWithEmptyScreen()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();
        
        var line = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.AreEqual("", line);
    }

    [TestMethod]
    public async Task Constructor_WithResizedTerminalWidgetHandle_UsesHandleDimensionsForInitialWorkloadResize()
    {
        await using var presentation = new TerminalWidgetHandle(80, 24);
        await using var workload = new RecordingResizeWorkloadAdapter();

        presentation.Resize(132, 41);

        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        Assert.AreEqual(132, terminal.Width);
        Assert.AreEqual(41, terminal.Height);
        Assert.AreEqual(132, workload.ResizeWidth);
        Assert.AreEqual(41, workload.ResizeHeight);
    }

    [TestMethod]
    public void Dispose_SynchronouslyDisposesPresentationAndWorkload()
    {
        var presentation = new RecordingDisposePresentationAdapter();
        var workload = new RecordingDisposeWorkloadAdapter();

        var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        terminal.Dispose();

        Assert.IsTrue(presentation.DisposeAsyncCalled);
        Assert.IsTrue(workload.DisposeAsyncCalled);
    }

    [TestMethod]
    public async Task Write_PlacesTextAtCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        workload.Write("Hello");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"),
                TimeSpan.FromSeconds(1), "Hello text")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.AreEqual("Hello", snapshot.GetLineTrimmed(0));
        Assert.AreEqual(5, snapshot.CursorX);
        Assert.AreEqual(0, snapshot.CursorY);
    }

    [TestMethod]
    [DataRow("\x1b_Gi=123", ";OK\x1b\\")]
    [DataRow("\x1b_Gi=123;OK\x1b", "\\")]
    public async Task PresentationInput_SplitKgpResponse_DoesNotEmitEscapeKeyEvent(string firstChunk, string secondChunk)
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        presentation.EnqueueInput(firstChunk);
        presentation.EnqueueInput(secondChunk);
        presentation.EnqueueInput("a");

        var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        var keyEvent = TestSeq.IsType<Hex1bKeyEvent>(evt);
        Assert.AreEqual(Hex1bKey.A, keyEvent.Key);
        Assert.AreEqual("a", keyEvent.Text);
        Assert.AreEqual(Hex1bModifiers.None, keyEvent.Modifiers);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    public async Task PresentationInput_SplitSs3Sequence_DoesNotEmitAltKeyEvent()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        presentation.EnqueueInput("\x1bO");
        presentation.EnqueueInput("A");

        var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        var keyEvent = TestSeq.IsType<Hex1bKeyEvent>(evt);
        Assert.AreEqual(Hex1bKey.UpArrow, keyEvent.Key);
        Assert.AreEqual(Hex1bModifiers.None, keyEvent.Modifiers);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    public async Task RunAsync_WhenPresentationInputPumpThrows_SurfacesTheFailure()
    {
        await using var presentation = new ThrowingInputPresentationAdapter(
            new InvalidOperationException("synthetic input failure"));
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => terminal.RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains("presentation input", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotNull(ex.InnerException);
        Assert.Contains("synthetic input failure", ex.InnerException!.Message);
    }

    [TestMethod]
    public async Task RunAsync_WhenWorkloadWriteInputThrows_SurfacesTheFailure()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        await using var workload = new ThrowingWorkloadAdapter(
            new IOException("synthetic shim send failure"));
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        presentation.EnqueueInput("x");

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => terminal.RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains("presentation input", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotNull(ex.InnerException);
        Assert.Contains("synthetic shim send failure", ex.InnerException!.Message);
    }

    [TestMethod]
    public async Task RunAsync_WhenCursorPositionResponseOverlapsTyping_SerializesWorkloadWrites()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        await using var workload = new DelayingRecordingWorkloadAdapter(Encoding.UTF8.GetBytes("\x1b[6n"));
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        using var cts = new CancellationTokenSource();
        var runTask = terminal.RunAsync(cts.Token);

        presentation.EnqueueInput("abc");

        await workload.WaitForTwoWritesAsync()
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.IsFalse(workload.ConcurrentWriteDetected);

        var writes = workload.Writes;
        Assert.IsTrue(writes.Any(write => write.StartsWith("\x1b[", StringComparison.Ordinal) && write.EndsWith("R", StringComparison.Ordinal)));
        Assert.IsTrue(writes.Any(write => write.Contains("abc", StringComparison.Ordinal)));

        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await runTask);
    }

    [TestMethod]
    public async Task RunAsync_KgpScreenLifecycle_ForwardsOriginalBytesAndUpdatesSnapshots()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 20,
            Height = 8
        });

        using var cts = new CancellationTokenSource();
        var runTask = terminal.RunAsync(cts.Token);
        var expectedOutput = new List<byte>();

        async Task WriteAndAssertForwardedAsync(string output)
        {
            var bytes = Encoding.UTF8.GetBytes(output);
            expectedOutput.AddRange(bytes);
            workload.Write(bytes);

            await presentation.WaitForOutputLengthAsync(
                    expectedOutput.Count,
                    TestContext.Current.CancellationToken)
                .WaitAsync(
                    TimeSpan.FromSeconds(2),
                    TestContext.Current.CancellationToken);
            TestSeq.AreEqual(expectedOutput, presentation.CapturedOutput);
        }

        async Task<Hex1bTerminalSnapshot> CaptureWhenAsync(
            Func<Hex1bTerminalSnapshot, bool> predicate,
            string description)
            => await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(
                    predicate,
                    TimeSpan.FromSeconds(2),
                    description)
                .Build()
                .ApplyWithCaptureAsync(
                    terminal,
                    TestContext.Current.CancellationToken);

        var mainOutput =
            "REMOVE-MAIN" +
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                1, 1, 1, cursorMovement: 1, quiet: 2, fillByte: 0x11) +
            "\x1b[2J" +
            "MAIN" +
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                2, 1, 1, cursorMovement: 1, quiet: 2, fillByte: 0x22);
        await WriteAndAssertForwardedAsync(mainOutput);

        using (var mainSnapshot = await CaptureWhenAsync(
            snapshot => !snapshot.InAlternateScreen &&
                snapshot.ContainsText("MAIN") &&
                !snapshot.ContainsText("REMOVE-MAIN") &&
                snapshot.KgpPlacements.Count == 1 &&
                snapshot.KgpImages.ContainsKey(2),
            "main KGP state applied"))
        {
            Assert.IsFalse(mainSnapshot.InAlternateScreen);
            Assert.IsTrue(mainSnapshot.ContainsText("MAIN"));
            Assert.IsFalse(mainSnapshot.ContainsText("REMOVE-MAIN"));
            var placement = TestSeq.Single(mainSnapshot.KgpPlacements);
            Assert.AreEqual(2u, placement.ImageId);
            Assert.AreEqual(0x22, mainSnapshot.KgpImages[2].Data[0]);
        }

        var alternateOutput =
            "\x1b[?1049h\x1b[H" +
            "REMOVE-ALT" +
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                3, 1, 1, cursorMovement: 1, quiet: 2, fillByte: 0x33) +
            "\x1b[3J\x1b[H" +
            "SECONDARY" +
            KgpTestHelper.BuildTransmitAndDisplayCommand(
                4, 1, 1, cursorMovement: 1, quiet: 2, fillByte: 0x44);
        await WriteAndAssertForwardedAsync(alternateOutput);

        using (var alternateSnapshot = await CaptureWhenAsync(
            snapshot => snapshot.InAlternateScreen &&
                snapshot.ContainsText("SECONDARY") &&
                !snapshot.ContainsText("REMOVE-ALT") &&
                snapshot.KgpPlacements.Count == 1 &&
                snapshot.KgpImages.ContainsKey(4),
            "alternate KGP state applied"))
        {
            Assert.IsTrue(alternateSnapshot.InAlternateScreen);
            Assert.IsTrue(alternateSnapshot.ContainsText("SECONDARY"));
            Assert.IsFalse(alternateSnapshot.ContainsText("REMOVE-ALT"));
            var placement = TestSeq.Single(alternateSnapshot.KgpPlacements);
            Assert.AreEqual(4u, placement.ImageId);
            Assert.AreEqual(0x44, alternateSnapshot.KgpImages[4].Data[0]);
        }

        const string exitAlternate = "\x1b[?1049l";
        await WriteAndAssertForwardedAsync(exitAlternate);

        using (var restoredSnapshot = await CaptureWhenAsync(
            snapshot => !snapshot.InAlternateScreen &&
                snapshot.ContainsText("MAIN") &&
                !snapshot.ContainsText("SECONDARY") &&
                snapshot.KgpPlacements.Count == 1 &&
                snapshot.KgpImages.ContainsKey(2),
            "main KGP state restored"))
        {
            Assert.IsFalse(restoredSnapshot.InAlternateScreen);
            Assert.IsTrue(restoredSnapshot.ContainsText("MAIN"));
            Assert.IsFalse(restoredSnapshot.ContainsText("SECONDARY"));
            var placement = TestSeq.Single(restoredSnapshot.KgpPlacements);
            Assert.AreEqual(2u, placement.ImageId);
            Assert.AreEqual(0x22, restoredSnapshot.KgpImages[2].Data[0]);
        }

        const string scrollingAndHistoryClear =
            "\x1b[?69h\x1b[2;10s\x1b[2;7r\x1b[S\x1b[T\x1b[3J";
        await WriteAndAssertForwardedAsync(scrollingAndHistoryClear);
        using (var clearedSnapshot = await CaptureWhenAsync(
            snapshot => snapshot.KgpPlacements.Count == 0 &&
                snapshot.KgpImages.Count == 0,
            "KGP state cleared"))
        {
            Assert.IsEmpty(clearedSnapshot.KgpPlacements);
            Assert.IsEmpty(clearedSnapshot.KgpImages);
        }

        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await runTask);
    }

    [TestMethod]
    public async Task RunAsync_KgpUnicodePlaceholder_ForwardsExactUtf8AndUpdatesSnapshot()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 20,
            Height = 8
        });

        using var cts = new CancellationTokenSource();
        var runTask = terminal.RunAsync(cts.Token);
        var placeholder = char.ConvertFromUtf32(0x10EEEE) + "\u0305\u0305";
        var output =
            KgpTestHelper.BuildCommand(
                "a=T,U=1,f=24,s=10,v=20,i=42,c=1,r=1,q=2",
                KgpTestHelper.CreatePixelData(10, 20, KgpFormat.Rgb24)) +
            "\x1b[38;5;42m" +
            placeholder +
            "\x1b[39m";
        var bytes = Encoding.UTF8.GetBytes(output);

        workload.Write(bytes);
        await presentation.WaitForOutputLengthAsync(
                bytes.Length,
                TestContext.Current.CancellationToken)
            .WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);

        TestSeq.AreEqual(bytes, presentation.CapturedOutput);
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                value => value.KgpPlacements.Count == 1,
                TimeSpan.FromSeconds(2),
                "placeholder realized after raw forwarding")
            .Build()
            .ApplyWithCaptureAsync(
                terminal,
                TestContext.Current.CancellationToken);
        var placement = TestSeq.Single(snapshot.KgpPlacements);
        Assert.AreEqual(42u, placement.ImageId);
        Assert.AreEqual(placeholder, snapshot.GetCell(0, 0).Character);

        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await runTask);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task RunAsync_IncompletePlaceholderUtf8Read_ForwardsEveryByte(
        int splitIndex)
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 20,
            Height = 8
        });

        using var cts = new CancellationTokenSource();
        var runTask = terminal.RunAsync(cts.Token);
        var expectedOutput = new List<byte>();

        async Task WriteAndAssertForwardedAsync(ReadOnlyMemory<byte> bytes)
        {
            expectedOutput.AddRange(bytes.Span);
            workload.Write(bytes.ToArray());
            await presentation.WaitForOutputLengthAsync(
                    expectedOutput.Count,
                    TestContext.Current.CancellationToken)
                .WaitAsync(
                    TimeSpan.FromSeconds(2),
                    TestContext.Current.CancellationToken);
            TestSeq.AreEqual(expectedOutput, presentation.CapturedOutput);
        }

        await WriteAndAssertForwardedAsync(Encoding.UTF8.GetBytes(
            KgpTestHelper.BuildCommand(
                "a=T,U=1,f=24,s=1,v=1,i=42,c=1,r=1,q=2",
                [0x11, 0x22, 0x33])));
        await WriteAndAssertForwardedAsync(
            "\x1b[38;5;42m"u8.ToArray());

        var placeholderBytes = Encoding.UTF8.GetBytes(
            char.ConvertFromUtf32(0x10EEEE));
        await WriteAndAssertForwardedAsync(
            placeholderBytes.AsMemory(0, splitIndex));
        await WriteAndAssertForwardedAsync(
            placeholderBytes.AsMemory(splitIndex)
                .ToArray()
                .Concat(Encoding.UTF8.GetBytes("\u0305\u0305\x1b[39m"))
                .ToArray());

        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                value => value.KgpPlacements.Count == 1,
                TimeSpan.FromSeconds(2),
                "split placeholder realized")
            .Build()
            .ApplyWithCaptureAsync(
                terminal,
                TestContext.Current.CancellationToken);
        Assert.AreEqual(
            char.ConvertFromUtf32(0x10EEEE) + "\u0305\u0305",
            snapshot.GetCell(0, 0).Character);

        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await runTask);
    }

    [TestMethod]
    [DataRow("esc")]
    [DataRow("apc-prefix")]
    [DataRow("terminator")]
    public async Task RunAsync_IncompleteKgpApcRead_ForwardsEveryByte(
        string split)
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 20,
            Height = 8
        });

        using var cts = new CancellationTokenSource();
        var runTask = terminal.RunAsync(cts.Token);
        var expectedOutput = new List<byte>();

        async Task WriteAndAssertForwardedAsync(ReadOnlyMemory<byte> bytes)
        {
            expectedOutput.AddRange(bytes.Span);
            workload.Write(bytes.ToArray());
            await presentation.WaitForOutputLengthAsync(
                    expectedOutput.Count,
                    TestContext.Current.CancellationToken)
                .WaitAsync(
                    TimeSpan.FromSeconds(2),
                    TestContext.Current.CancellationToken);
            TestSeq.AreEqual(expectedOutput, presentation.CapturedOutput);
        }

        var command = Encoding.UTF8.GetBytes(KgpTestHelper.BuildCommand(
            "a=T,U=1,f=24,s=1,v=1,i=42,c=1,r=1,q=2",
            [0x11, 0x22, 0x33]));
        var splitIndex = split switch
        {
            "esc" => 1,
            "apc-prefix" => 2,
            "terminator" => command.Length - 1,
            _ => throw new InvalidOperationException(split),
        };
        await WriteAndAssertForwardedAsync(
            command.AsMemory(0, splitIndex));
        Assert.AreEqual(0, terminal.KgpImageStore.ImageCount);
        await WriteAndAssertForwardedAsync(
            command.AsMemory(splitIndex));
        await WriteAndAssertForwardedAsync(Encoding.UTF8.GetBytes(
            "\x1b[38;5;42m" +
            char.ConvertFromUtf32(0x10EEEE) +
            "\u0305\u0305\x1b[39m"));

        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                value => value.KgpPlacements.Count == 1,
                TimeSpan.FromSeconds(2),
                "split KGP APC realized")
            .Build()
            .ApplyWithCaptureAsync(
                terminal,
                TestContext.Current.CancellationToken);
        Assert.AreEqual(42u, TestSeq.Single(snapshot.KgpPlacements).ImageId);

        await WriteAndAssertForwardedAsync(Encoding.UTF8.GetBytes(
            KgpTestHelper.BuildCommand(
                "a=t,f=24,s=1,v=1,i=43,q=2",
                [0x44, 0x55, 0x66])));
        var relativeCommand = Encoding.UTF8.GetBytes(
            KgpTestHelper.BuildCommand(
                "a=p,i=43,p=8,c=1,r=1,P=42,H=1,V=1,C=0,q=2"));
        var relativeSplitIndex = split switch
        {
            "esc" => 1,
            "apc-prefix" => 2,
            "terminator" => relativeCommand.Length - 1,
            _ => throw new InvalidOperationException(split),
        };
        await WriteAndAssertForwardedAsync(
            relativeCommand.AsMemory(0, relativeSplitIndex));
        await WriteAndAssertForwardedAsync(
            relativeCommand.AsMemory(relativeSplitIndex));

        using var relativeSnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(
                value => value.KgpPlacements.Any(
                    placement => placement.ImageId == 43),
                TimeSpan.FromSeconds(2),
                "split relative KGP APC applied")
            .Build()
            .ApplyWithCaptureAsync(
                terminal,
                TestContext.Current.CancellationToken);
        var relative = TestSeq.Single(relativeSnapshot.KgpPlacements.Where(
            placement => placement.ImageId == 43));
        Assert.AreEqual(1, relative.Row);
        Assert.AreEqual(1, relative.Column);

        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await runTask);
    }

    [TestMethod]
    [DataRow("\x1b_Gi=123", ";OK\x1b\\")]
    [DataRow("\x1b_Gi=123;OK\x1b", "\\")]
    public async Task AppInput_SplitKgpResponse_DoesNotTriggerEscapeBinding(string firstChunk, string secondChunk)
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        var normalKeyHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var escapeTriggered = false;
        var status = "Ready";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new TextBlockWidget(status)
                ]).InputBindings(bindings =>
                {
                    bindings.Key(Hex1bKey.Escape).Action(_ =>
                    {
                        escapeTriggered = true;
                        status = "Escape triggered";
                        return Task.CompletedTask;
                    }, "Escape binding");

                    bindings.Key(Hex1bKey.A).Action(_ =>
                    {
                        status = "Normal key handled";
                        normalKeyHandled.TrySetResult();
                        return Task.CompletedTask;
                    }, "A binding");
                })
            ),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                EnableDefaultCtrlCExit = false
            }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(2), "initial render")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        presentation.EnqueueInput(firstChunk);
        presentation.EnqueueInput(secondChunk);
        presentation.EnqueueInput("a");

        await normalKeyHandled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Normal key handled"), TimeSpan.FromSeconds(2), "normal key handled")
            .Capture("after-split-kgp-response")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.IsFalse(escapeTriggered);
        Assert.IsTrue(snapshot.ContainsText("Normal key handled"));

        cts.Cancel();
        await runTask;
    }

    [TestMethod]
    public async Task PresentationInput_BareEscape_FlushedAfterTimeout()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        // Send just \x1b with no continuation — after the timeout the
        // terminal should flush it as a standalone Escape key event.
        presentation.EnqueueInput("\x1b");

        var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var keyEvent = TestSeq.IsType<Hex1bKeyEvent>(evt);
        Assert.AreEqual(Hex1bKey.Escape, keyEvent.Key);
        Assert.AreEqual(Hex1bModifiers.None, keyEvent.Modifiers);
    }

    [TestMethod]
    public async Task PresentationInput_DoubleEscape_DoesNotKillEventPump()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        // Send two bare escapes back-to-back, then a normal key.
        // The pump must survive both timeouts and still dispatch the 'a'.
        presentation.EnqueueInput("\x1b");
        await Task.Delay(TimeSpan.FromMilliseconds(70));
        presentation.EnqueueInput("\x1b");
        await Task.Delay(TimeSpan.FromMilliseconds(70));
        presentation.EnqueueInput("a");

        var events = new List<Hex1bKeyEvent>();
        for (int i = 0; i < 3; i++)
        {
            var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            events.Add(TestSeq.IsType<Hex1bKeyEvent>(evt));
        }

        Assert.AreEqual(Hex1bKey.Escape, events[0].Key);
        Assert.AreEqual(Hex1bKey.Escape, events[1].Key);
        Assert.AreEqual(Hex1bKey.A, events[2].Key);
    }

    [TestMethod]
    public async Task PresentationInput_CustomEscapeTimeout_UsesConfiguredValue()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24,
            EscapeSequenceTimeout = TimeSpan.FromMilliseconds(10)
        });

        presentation.EnqueueInput("\x1b");

        var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var keyEvent = TestSeq.IsType<Hex1bKeyEvent>(evt);
        Assert.AreEqual(Hex1bKey.Escape, keyEvent.Key);
    }

    [TestMethod]
    public async Task PresentationInput_ZeroEscapeTimeout_DisablesFlush()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24,
            EscapeSequenceTimeout = TimeSpan.Zero
        });

        // Send bare \x1b — with timeout disabled, it should stay buffered.
        presentation.EnqueueInput("\x1b");

        // Wait well beyond the default 50ms timeout
        await Task.Delay(150);

        // No event should have been dispatched
        Assert.IsFalse(workload.InputEvents.TryRead(out _), "With EscapeSequenceTimeout=Zero, bare ESC should stay buffered");

        // Sending a continuation byte should produce the combined sequence (Alt+A)
        presentation.EnqueueInput("a");

        var evt = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var keyEvent = TestSeq.IsType<Hex1bKeyEvent>(evt);
        Assert.AreEqual(Hex1bKey.A, keyEvent.Key);
        Assert.AreEqual(Hex1bModifiers.Alt, keyEvent.Modifiers);
    }

    [TestMethod]
    public async Task AppInput_BareEscape_TriggersEscapeBinding()
    {
        await using var presentation = new QueuedInputPresentationAdapter();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24
        });

        var escapeTriggered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var status = "Ready";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new TextBlockWidget(status)
                ]).InputBindings(bindings =>
                {
                    bindings.Key(Hex1bKey.Escape).Action(_ =>
                    {
                        status = "Escape handled";
                        escapeTriggered.TrySetResult();
                        return Task.CompletedTask;
                    }, "Escape binding");
                })
            ),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                EnableDefaultCtrlCExit = false
            }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(2), "initial render")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Send bare \x1b — should be flushed as Escape after timeout
        presentation.EnqueueInput("\x1b");

        await escapeTriggered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.IsTrue(escapeTriggered.Task.IsCompletedSuccessfully, "Escape binding should have fired from bare \\x1b");

        cts.Cancel();
        await runTask;
    }

    [TestMethod]
    public async Task Write_HandlesNewlines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        // Use \r\n (CRLF) - real terminals expect ONLCR translation to happen in PTY layer
        workload.Write("Line1\r\nLine2\r\nLine3");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line1") && s.ContainsText("Line2") && s.ContainsText("Line3"),
                TimeSpan.FromSeconds(1), "all three lines")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.AreEqual("Line1", snapshot.GetLineTrimmed(0));
        Assert.AreEqual("Line2", snapshot.GetLineTrimmed(1));
        Assert.AreEqual("Line3", snapshot.GetLineTrimmed(2));
    }

    [TestMethod]
    public async Task Write_WrapsAtEndOfLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(5, 3).Build();
        
        workload.Write("HelloWorld");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello") && s.ContainsText("World"),
                TimeSpan.FromSeconds(1), "wrapped text")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.AreEqual("Hello", snapshot.GetLineTrimmed(0));
        Assert.AreEqual("World", snapshot.GetLineTrimmed(1));
    }

    [TestMethod]
    public async Task Clear_ResetsScreenAndCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("Some text");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Some text"),
                TimeSpan.FromSeconds(1), "initial text")
            .Build()
            .ApplyAsync(terminal);
        
        workload.Clear();
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.AreEqual("", terminal.CreateSnapshot().GetLineTrimmed(0));
        Assert.AreEqual(0, terminal.CreateSnapshot().CursorX);
        Assert.AreEqual(0, terminal.CreateSnapshot().CursorY);
    }

    [TestMethod]
    public async Task SetCursorPosition_MovesCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        workload.SetCursorPosition(5, 2);
        workload.Write("X");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("X"),
                TimeSpan.FromSeconds(1), "X at cursor position")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var line = snapshot.GetLine(2);
        Assert.AreEqual('X', line[5]);
    }

    [TestMethod]
    public async Task SetCursorPosition_ClampsToBounds()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();
        
        workload.SetCursorPosition(100, 100);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.AreEqual(9, terminal.CreateSnapshot().CursorX);
        Assert.AreEqual(4, terminal.CreateSnapshot().CursorY);
    }

    [TestMethod]
    public async Task EnterAlternateScreen_SetsFlag()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        Assert.IsFalse(terminal.CreateSnapshot().InAlternateScreen);
        
        terminal.EnterAlternateScreen();
        
        Assert.IsTrue(terminal.CreateSnapshot().InAlternateScreen);
    }

    [TestMethod]
    public async Task ExitAlternateScreen_ClearsFlag()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.EnterAlternateScreen();
        
        terminal.ExitAlternateScreen();
        
        Assert.IsFalse(terminal.CreateSnapshot().InAlternateScreen);
    }

    [TestMethod]
    public async Task ContainsText_FindsText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        workload.Write("Hello World");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"),
                TimeSpan.FromSeconds(1), "Hello World text")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.IsTrue(snapshot.ContainsText("World"));
        Assert.IsFalse(snapshot.ContainsText("Foo"));
    }

    [TestMethod]
    public async Task FindText_ReturnsPositions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        // Use \r\n - terminal emulator expects explicit CR before LF
        workload.Write("Hello World\r\nHello Again");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World") && s.ContainsText("Hello Again"),
                TimeSpan.FromSeconds(1), "both Hello lines")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var results = snapshot.FindText("Hello");
        
        Assert.AreEqual(2, results.Count);
        Assert.AreEqual((0, 0), results[0]); // (Line, Column) = row 0, col 0
        Assert.AreEqual((1, 0), results[1]); // (Line, Column) = row 1, col 0
    }

    [TestMethod]
    public async Task GetNonEmptyLines_FiltersEmptyLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        // Use \r\n for proper line endings
        workload.Write("Line 1\r\n\r\nLine 3");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 1") && s.ContainsText("Line 3"),
                TimeSpan.FromSeconds(1), "Line 1 and Line 3")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var lines = snapshot.GetNonEmptyLines().ToList();
        
        Assert.AreEqual(2, lines.Count);
        Assert.AreEqual("Line 1", lines[0]);
        Assert.AreEqual("Line 3", lines[1]);
    }

    [TestMethod]
    public async Task Resize_PreservesContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("Hello");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"),
                TimeSpan.FromSeconds(1), "Hello text")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        terminal.Resize(40, 10);
        
        Assert.AreEqual(40, terminal.Width);
        Assert.AreEqual(10, terminal.Height);
        Assert.AreEqual("Hello", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [TestMethod]
    public async Task AnsiSequences_AreProcessedButNotDisplayed()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        
        workload.Write("\x1b[31mRed Text\x1b[0m");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Red Text"),
                TimeSpan.FromSeconds(1), "Red Text")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        Assert.AreEqual("Red Text", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    public async Task AnsiCursorPosition_MovesCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        // ANSI positions are 1-based, so row 2, col 5
        workload.Write("\x1b[2;5HX");
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("X"),
                TimeSpan.FromSeconds(1), "X at ANSI position")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var line = snapshot.GetLine(1); // 0-based
        Assert.AreEqual('X', line[4]); // 0-based
    }

    [TestMethod]
    public async Task AnsiClearScreen_ClearsBuffer()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("Some content");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Some content"),
                TimeSpan.FromSeconds(1), "initial content")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        workload.Write("\x1b[2J");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.AreEqual("", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [TestMethod]
    public async Task GetScreenBuffer_ReturnsCopyWithColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("\x1b[38;2;255;0;0mR\x1b[0m");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("R"),
                TimeSpan.FromSeconds(1), "R with red color")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        var buffer = terminal.GetScreenBuffer();
        
        Assert.AreEqual("R", buffer[0, 0].Character);
        Assert.IsNotNull(buffer[0, 0].Foreground);
        Assert.AreEqual(255, buffer[0, 0].Foreground!.Value.R);
        Assert.AreEqual(0, buffer[0, 0].Foreground!.Value.G);
        Assert.AreEqual(0, buffer[0, 0].Foreground!.Value.B);
    }

    [TestMethod]
    public async Task AlternateScreenAnsiSequence_IsRecognized()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        workload.Write("\x1b[?1049h");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);
        Assert.IsTrue(terminal.CreateSnapshot().InAlternateScreen);
        
        workload.Write("\x1b[?1049l");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);
        Assert.IsFalse(terminal.CreateSnapshot().InAlternateScreen);
    }

    #region Resize Behavior

    [TestMethod]
    public async Task Constructor_SetsWorkloadDimensions()
    {
        // Workload dimensions are 0x0 before terminal is created
        using var workload = new Hex1bAppWorkloadAdapter();
        Assert.AreEqual(0, workload.Width);
        Assert.AreEqual(0, workload.Height);
        
        // Terminal sets workload dimensions during construction
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        Assert.AreEqual(80, workload.Width);
        Assert.AreEqual(24, workload.Height);
    }

    [TestMethod]
    public async Task Constructor_DoesNotFireResizeEvent()
    {
        // This is critical: the initial dimension setup should NOT fire a resize event
        // because that would trigger an extra re-render before the app even starts
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        // Try to read from input channel - should be empty (no resize event)
        var hasEvent = workload.InputEvents.TryRead(out var evt);
        Assert.IsFalse(hasEvent, "Constructor should not fire a resize event");
    }

    [TestMethod]
    public async Task ResizeAsync_AfterInitialization_FiresResizeEvent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        // Now call ResizeAsync again (simulating a terminal resize)
        await workload.ResizeAsync(100, 30, TestContext.Current.CancellationToken);
        
        // This should fire a resize event
        var hasEvent = workload.InputEvents.TryRead(out var evt);
        Assert.IsTrue(hasEvent, "ResizeAsync after init should fire a resize event");
        
        var resizeEvent = TestSeq.IsType<Hex1bResizeEvent>(evt);
        Assert.AreEqual(100, resizeEvent.Width);
        Assert.AreEqual(30, resizeEvent.Height);
    }

    [TestMethod]
    public async Task ResizeAsync_SameDimensions_DoesNotFireEvent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        // Resize to same dimensions
        await workload.ResizeAsync(80, 24, TestContext.Current.CancellationToken);
        
        // Should NOT fire event (no change)
        var hasEvent = workload.InputEvents.TryRead(out _);
        Assert.IsFalse(hasEvent, "ResizeAsync with same dimensions should not fire event");
    }

    #endregion
}
