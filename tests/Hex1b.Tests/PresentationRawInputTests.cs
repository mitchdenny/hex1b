using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class PresentationRawInputTests
{
    [TestMethod]
    [DataRow("a", false)]
    [DataRow("\r", false)]
    [DataRow("\n", false)]
    [DataRow("a", true)]
    [DataRow("\r", true)]
    [DataRow("\n", true)]
    public async Task PresentationInput_SplitEscape_PreservesOriginalBytes(string continuation, bool defaultTimeout)
    {
        await using var presentation = new QueuedPresentation();
        var workload = new RecordingWorkload();
        var clock = new ControlledTimeProvider();
        var presentationFilter = new RecordingFilter();
        var workloadFilter = new RecordingFilter();
        var options = new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            TimeProvider = clock,
            EscapeSequenceTimeout = defaultTimeout ? null : TimeSpan.Zero
        };
        options.PresentationFilters.Add(presentationFilter);
        options.WorkloadFilters.Add(workloadFilter);
        await using var terminal = new Hex1bTerminal(options);

        presentation.Enqueue([0x1b]);
        if (defaultTimeout)
            Assert.AreEqual(TimeSpan.FromMilliseconds(50), await ReadAsync(clock.Armed));
        presentation.Enqueue(Encoding.UTF8.GetBytes(continuation));
        presentation.Enqueue("z"u8.ToArray());

        TestSeq.AreEqual(Encoding.UTF8.GetBytes("\x1b" + continuation + "z"), await ReadThroughSentinelAsync(workload));
        Assert.AreEqual("\x1b" + continuation, await ReadAsync(presentationFilter.Input));
        Assert.AreEqual("z", await ReadAsync(presentationFilter.Input));
        Assert.AreEqual("\x1b" + continuation, await ReadAsync(workloadFilter.Input));
        Assert.AreEqual("z", await ReadAsync(workloadFilter.Input));
        Assert.IsFalse(workload.Input.TryRead(out _));
        Assert.IsFalse(presentationFilter.Input.TryRead(out _));
        Assert.IsFalse(workloadFilter.Input.TryRead(out _));
    }

    [TestMethod]
    [DataRow("\x1b", "a")]
    [DataRow("\x1b", "\r")]
    [DataRow("\x1b", "\n")]
    [DataRow("x\x1b", "a")]
    [DataRow("x\x1b[", "A")]
    [DataRow("x\x1bO", "P")]
    public async Task PresentationInput_TimeoutBeforeContinuation_DoesNotLoseOrRepeatBytes(string prefix, string continuation)
    {
        await using var presentation = new QueuedPresentation();
        var workload = new RecordingWorkload();
        var clock = new ControlledTimeProvider();
        var filter = new RecordingFilter();
        var options = new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            TimeProvider = clock
        };
        options.WorkloadFilters.Add(filter);
        await using var terminal = new Hex1bTerminal(options);

        presentation.Enqueue(Encoding.UTF8.GetBytes(prefix));
        Assert.AreEqual(TimeSpan.FromMilliseconds(50), await ReadAsync(clock.Armed));
        if (prefix.StartsWith('x'))
            Assert.AreEqual("x", await ReadAsync(filter.Input));
        clock.Fire();
        Assert.AreEqual(prefix.TrimStart('x'), await ReadAsync(filter.Input));
        presentation.Enqueue(Encoding.UTF8.GetBytes(continuation));
        presentation.Enqueue("z"u8.ToArray());

        TestSeq.AreEqual(Encoding.UTF8.GetBytes(prefix + continuation + "z"), await ReadThroughSentinelAsync(workload));
        Assert.AreEqual(continuation, await ReadAsync(filter.Input));
        Assert.AreEqual("z", await ReadAsync(filter.Input));
        Assert.IsFalse(workload.Input.TryRead(out _));
        Assert.IsFalse(filter.Input.TryRead(out _));
    }

    [TestMethod]
    public async Task PresentationInput_AllReadBoundaries_PreserveEscapeAndUtf8Bytes()
    {
        byte[][] inputs =
        [
            "\u001ba\x1b\r\x1b\n"u8.ToArray(),
            "\x1b[A\x1b[1;5D\x1bOP\x1bOA"u8.ToArray(),
            "é😀界"u8.ToArray(),
            "x\x1b[Aé\x1bOP😀\x1b\r"u8.ToArray(),
            [0xff, 0xc3, 0x28, 0x80, 0x1b, 0x0d],
            "x\x1b"u8.ToArray()
        ];
        foreach (var input in inputs)
        {
            for (var split = 1; split < input.Length; split++)
            {
                await using var presentation = new QueuedPresentation();
                var workload = new RecordingWorkload();
                await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
                {
                    PresentationAdapter = presentation,
                    WorkloadAdapter = workload,
                    EscapeSequenceTimeout = TimeSpan.Zero
                });

                presentation.Enqueue(input[..split]);
                presentation.Enqueue(input[split..]);
                presentation.Enqueue("z"u8.ToArray());

                TestSeq.AreEqual(input.Concat("z"u8.ToArray()), await ReadThroughSentinelAsync(workload),
                    $"Input {Convert.ToHexString(input)}, split {split}");
                Assert.IsFalse(workload.Input.TryRead(out _));
            }
        }
    }

    [TestMethod]
    public async Task PresentationInput_OneByteReads_PreserveMixedCompleteAndIncompleteInput()
    {
        var input = "x\u001baé\x1b[A😀\x1bOP\x1b\r\x1b\n"u8.ToArray();
        await using var presentation = new QueuedPresentation();
        var workload = new RecordingWorkload();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });

        foreach (var value in input)
            presentation.Enqueue([value]);
        presentation.Enqueue("z"u8.ToArray());

        TestSeq.AreEqual(input.Concat("z"u8.ToArray()), await ReadThroughSentinelAsync(workload));
        Assert.IsFalse(workload.Input.TryRead(out _));
    }

    [TestMethod]
    [DataRow("1B")]
    [DataRow("E2")]
    [DataRow("F09F")]
    [DataRow("FF")]
    public async Task PresentationInput_RawWorkload_DoesNotWaitForTokenOrUtf8Completion(string hex)
    {
        await using var presentation = new QueuedPresentation();
        var workload = new RecordingWorkload();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });

        var input = Convert.FromHexString(hex);
        presentation.Enqueue(input);

        TestSeq.AreEqual(input, await ReadAsync(workload.Input));
        Assert.IsFalse(workload.Input.TryRead(out _));
    }

    [TestMethod]
    [DataRow("a", Hex1bKey.A)]
    [DataRow("\r", Hex1bKey.Enter)]
    [DataRow("\n", Hex1bKey.Enter)]
    public async Task PresentationInput_AppWorkload_PreservesAltDecodingBeforeTimeout(string continuation, Hex1bKey key)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter();
        var clock = new ControlledTimeProvider();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            TimeProvider = clock
        });

        presentation.Enqueue([0x1b]);
        await ReadAsync(clock.Armed);
        presentation.Enqueue(Encoding.UTF8.GetBytes(continuation));
        presentation.Enqueue("z"u8.ToArray());

        var input = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
        Assert.AreEqual(key, input.Key);
        Assert.AreEqual(Hex1bModifiers.Alt, input.Modifiers);
        Assert.AreEqual(Hex1bKey.Z, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    [DataRow("a", Hex1bKey.A)]
    [DataRow("\r", Hex1bKey.Enter)]
    [DataRow("\n", Hex1bKey.Enter)]
    public async Task PresentationInput_AppWorkload_PreservesSeparateKeysAfterTimeout(string continuation, Hex1bKey key)
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter();
        var clock = new ControlledTimeProvider();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            TimeProvider = clock
        });

        presentation.Enqueue([0x1b]);
        await ReadAsync(clock.Armed);
        clock.Fire();
        var escape = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
        Assert.AreEqual(Hex1bKey.Escape, escape.Key);
        Assert.AreEqual(Hex1bModifiers.None, escape.Modifiers);
        presentation.Enqueue(Encoding.UTF8.GetBytes(continuation));
        presentation.Enqueue("z"u8.ToArray());

        var input = TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents));
        Assert.AreEqual(key, input.Key);
        Assert.AreEqual(Hex1bModifiers.None, input.Modifiers);
        Assert.AreEqual(Hex1bKey.Z, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    public async Task PresentationInput_AppWorkload_OneByteReadsPreserveUtf8AndSpecialKeys()
    {
        await using var presentation = new QueuedPresentation();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            EscapeSequenceTimeout = TimeSpan.Zero
        });

        foreach (var value in "é😀界\x1b[A\x1bOPz"u8.ToArray())
            presentation.Enqueue([value]);

        foreach (var text in new[] { "é", "😀", "界" })
            Assert.AreEqual(text, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Text);
        Assert.AreEqual(Hex1bKey.UpArrow, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual(Hex1bKey.F1, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.AreEqual(Hex1bKey.Z, TestSeq.IsType<Hex1bKeyEvent>(await ReadAsync(workload.InputEvents)).Key);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    private static async Task<T> ReadAsync<T>(ChannelReader<T> reader)
        => await reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private static async Task<byte[]> ReadThroughSentinelAsync(RecordingWorkload workload)
    {
        var bytes = new List<byte>();
        while (true)
        {
            var chunk = await ReadAsync(workload.Input);
            bytes.AddRange(chunk);
            if (chunk.AsSpan().SequenceEqual("z"u8))
                return bytes.ToArray();
        }
    }

    private sealed class QueuedPresentation : IHex1bTerminalPresentationAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _input = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        public int Width => 80;
        public int Height => 24;
        public TerminalCapabilities Capabilities => new();
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }
        public void Enqueue(byte[] bytes) => Assert.IsTrue(_input.Writer.TryWrite(bytes));
        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
            => await _input.Reader.ReadAsync(ct);
        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
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

    private sealed class RecordingWorkload : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
        public ChannelReader<byte[]> Input => _input.Reader;
        public event Action? Disconnected { add { } remove { } }
        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => _input.Writer.WriteAsync(data.ToArray(), ct);
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync()
        {
            _input.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ControlledTimeProvider : TimeProvider
    {
        private readonly Channel<TimeSpan> _armed = Channel.CreateUnbounded<TimeSpan>();
        private TimerCallback? _callback;
        private object? _state;
        public ChannelReader<TimeSpan> Armed => _armed.Reader;
        public void Fire() => (_callback ?? throw new InvalidOperationException("Timer not created"))(_state);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.AreEqual(Timeout.InfiniteTimeSpan, dueTime);
            Assert.IsNull(_callback);
            _callback = callback;
            _state = state;
            return new ControlledTimer(_armed.Writer);
        }

        private sealed class ControlledTimer(ChannelWriter<TimeSpan> armed) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                Assert.AreEqual(Timeout.InfiniteTimeSpan, period);
                if (dueTime != Timeout.InfiniteTimeSpan)
                    Assert.IsTrue(armed.TryWrite(dueTime));
                return true;
            }
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingFilter : IHex1bTerminalWorkloadFilter, IHex1bTerminalPresentationFilter
    {
        private readonly Channel<string> _input = Channel.CreateUnbounded<string>();
        public ChannelReader<string> Input => _input.Reader;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => _input.Writer.WriteAsync(AnsiTokenSerializer.Serialize(tokens), ct);
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(IReadOnlyList<AppliedToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<AnsiToken>>(tokens.Select(t => t.Token).ToArray());
        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
    }
}
