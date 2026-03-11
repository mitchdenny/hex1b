using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class Hex1bAppResizeRaceTests
{
    [Fact]
    public async Task RenderFrame_WhenTerminalSizeChangesMidFrame_UsesCapturedFrameSize()
    {
        await using var workload = new RacingDimensionsWorkloadAdapter(
            firstWidth: 60,
            firstHeight: 20,
            secondWidth: 40,
            secondHeight: 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Align(Alignment.BottomRight, ctx.Text("RACE"))
                    .Fill()),
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                EnableInputCoalescing = false,
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = app.RunAsync(cts.Token);

        var output = await workload.WaitForOutputContainingAsync("RACE", cts.Token);

        app.RequestStop();
        await runTask;

        Assert.Contains("RACE", output);
    }

    private sealed class RacingDimensionsWorkloadAdapter : IHex1bAppTerminalWorkloadAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly Channel<Hex1bEvent> _input = Channel.CreateUnbounded<Hex1bEvent>();
        private readonly Channel<bool> _writeSignals = Channel.CreateUnbounded<bool>();
        private readonly object _gate = new();
        private readonly StringBuilder _captured = new();
        private readonly int _firstWidth;
        private readonly int _firstHeight;
        private readonly int _secondWidth;
        private readonly int _secondHeight;
        private int _dimensionStage;
        private bool _readWidthInFirstStage;
        private bool _readHeightInFirstStage;
        private int _outputQueueDepth;

        public RacingDimensionsWorkloadAdapter(int firstWidth, int firstHeight, int secondWidth, int secondHeight)
        {
            _firstWidth = firstWidth;
            _firstHeight = firstHeight;
            _secondWidth = secondWidth;
            _secondHeight = secondHeight;
        }

        public ChannelReader<Hex1bEvent> InputEvents => _input.Reader;

        public int Width
        {
            get
            {
                if (_dimensionStage == 0)
                {
                    _readWidthInFirstStage = true;
                    AdvanceDimensionStageIfReady();
                    return _firstWidth;
                }

                return _secondWidth;
            }
        }

        public int Height
        {
            get
            {
                if (_dimensionStage == 0)
                {
                    _readHeightInFirstStage = true;
                    AdvanceDimensionStageIfReady();
                    return _firstHeight;
                }

                return _secondHeight;
            }
        }

        public TerminalCapabilities Capabilities => TerminalCapabilities.Modern;

        public int OutputQueueDepth => Volatile.Read(ref _outputQueueDepth);

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public void Write(string text)
            => Record(Encoding.UTF8.GetBytes(text));

        public void Write(ReadOnlySpan<byte> data)
            => Record(data.ToArray());

        public void Flush()
        {
        }

        public void EnterTuiMode()
        {
        }

        public void ExitTuiMode()
        {
        }

        public void Clear()
            => Write("\x1b[2J");

        public void SetCursorPosition(int left, int top)
            => Write($"\x1b[{top + 1};{left + 1}H");

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            return ReadOutputAsyncCore(ct);
        }

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public async ValueTask DisposeAsync()
        {
            _output.Writer.TryComplete();
            _input.Writer.TryComplete();
            _writeSignals.Writer.TryComplete();
            await Task.CompletedTask;
            GC.SuppressFinalize(this);
        }

        public async Task<string> WaitForOutputContainingAsync(string text, CancellationToken cancellationToken)
        {
            while (true)
            {
                string captured;
                lock (_gate)
                {
                    captured = _captured.ToString();
                }

                if (captured.Contains(text, StringComparison.Ordinal))
                    return captured;

                await _writeSignals.Reader.ReadAsync(cancellationToken);
            }
        }

        private void AdvanceDimensionStageIfReady()
        {
            if (_readWidthInFirstStage && _readHeightInFirstStage)
                _dimensionStage = 1;
        }

        private void Record(ReadOnlyMemory<byte> data)
        {
            lock (_gate)
            {
                _captured.Append(Encoding.UTF8.GetString(data.Span));
            }

            if (_output.Writer.TryWrite(data))
                Interlocked.Increment(ref _outputQueueDepth);

            _writeSignals.Writer.TryWrite(true);
        }

        private async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsyncCore(CancellationToken cancellationToken)
        {
            var data = await _output.Reader.ReadAsync(cancellationToken);
            Interlocked.Decrement(ref _outputQueueDepth);
            return data;
        }
    }
}
