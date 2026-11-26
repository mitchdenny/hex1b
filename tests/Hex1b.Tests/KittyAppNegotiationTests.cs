using System.Text;
using System.Threading.Channels;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class KittyAppNegotiationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EnterExitTuiMode_CapabilityControlsBalancedPushPop(bool supported)
    {
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = supported });
        workload.ExitTuiMode();
        Assert.IsFalse(workload.TryReadOutput(out _));
        workload.EnterTuiMode();
        workload.EnterTuiMode();
        Assert.IsTrue(workload.TryReadOutput(out var enter));
        Assert.IsFalse(workload.TryReadOutput(out _));
        var text = Encoding.UTF8.GetString(enter.Span);
        Assert.AreEqual(supported, text.Contains("\x1b[>31u", StringComparison.Ordinal));
        if (supported)
            Assert.IsTrue(text.IndexOf("\x1b[?1049h", StringComparison.Ordinal) < text.IndexOf("\x1b[>31u", StringComparison.Ordinal));
        workload.ExitTuiMode();
        workload.ExitTuiMode();
        Assert.IsTrue(workload.TryReadOutput(out var exit));
        text = Encoding.UTF8.GetString(exit.Span);
        Assert.AreEqual(supported, text.Contains("\x1b[<u", StringComparison.Ordinal));
        if (supported)
            Assert.IsTrue(text.IndexOf("\x1b[<u", StringComparison.Ordinal) < text.IndexOf("\x1b[?1049l", StringComparison.Ordinal));
        Assert.IsFalse(workload.TryReadOutput(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_NormalExit_DrainsPopBeforeRawModeExit(bool filtered)
    {
        await using var presentation = new KeyboardHost(supported: true);
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        var options = new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            RunCallback = _ =>
            {
                workload.EnterTuiMode();
                workload.ExitTuiMode();
                workload.Dispose();
                return Task.FromResult(0);
            }
        };
        if (filtered)
            options.PresentationFilters.Add(new PassThroughFilter());
        await using var terminal = new Hex1bTerminal(options);
        Assert.AreEqual(0, await terminal.RunAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, presentation.Pushes);
        Assert.AreEqual(1, presentation.Pops);
        Assert.AreEqual(8, presentation.FlagsAfterPop);
        Assert.AreEqual(4, presentation.Host.InputModes.KittyKeyboardFlags);
        Assert.IsTrue(presentation.Trace.IndexOf("pop") < presentation.Trace.IndexOf("alt-exit"));
        Assert.IsTrue(presentation.Trace.IndexOf("alt-exit") < presentation.Trace.IndexOf("raw-exit"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Dispose_AfterSuccessfulPush_RestoresPreexistingStacks(bool synchronous)
    {
        await using var presentation = new KeyboardHost(supported: true);
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload
        });
        workload.EnterTuiMode();
        await presentation.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (synchronous)
            terminal.Dispose();
        else
            await terminal.DisposeAsync();
        Assert.AreEqual(1, presentation.Pushes);
        Assert.AreEqual(1, presentation.Pops);
        Assert.AreEqual(8, presentation.FlagsAfterPop);
        Assert.AreEqual(4, presentation.Host.InputModes.KittyKeyboardFlags);
        Assert.IsTrue(presentation.Trace.IndexOf("pop") < presentation.Trace.IndexOf("alt-exit"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_CancellationOrFailure_AfterPushRestoresOwnership(bool fail)
    {
        await using var presentation = new KeyboardHost(supported: true);
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        using var cancellation = new CancellationTokenSource();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            RunCallback = async ct =>
            {
                workload.EnterTuiMode();
                await presentation.Pushed.Task.WaitAsync(ct);
                if (fail)
                    throw new IOException("workload failed");
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            }
        });
        var run = terminal.RunAsync(cancellation.Token);
        await presentation.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (fail)
            await Assert.ThrowsExactlyAsync<IOException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
        else
        {
            cancellation.Cancel();
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        await terminal.DisposeAsync();
        Assert.AreEqual(1, presentation.Pops);
        Assert.AreEqual(8, presentation.FlagsAfterPop);
        Assert.IsTrue(presentation.Trace.IndexOf("pop") < presentation.Trace.IndexOf("raw-exit"));
    }

    [TestMethod]
    public async Task RunAsync_PushWriteFails_DoesNotPopUnownedHostStack()
    {
        await using var presentation = new KeyboardHost(supported: true) { FailPush = true };
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            RunCallback = async ct =>
            {
                workload.EnterTuiMode();
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            }
        });
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await terminal.RunAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        await terminal.DisposeAsync();
        Assert.AreEqual(0, presentation.Pushes);
        Assert.AreEqual(0, presentation.Pops);
        Assert.AreEqual(4, presentation.Host.InputModes.KittyKeyboardFlags);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_DelayedCapabilityStartup_AwaitsBeforeAppNegotiates(bool supported)
    {
        await using var presentation = new KeyboardHost(supported) { DelayStartup = true };
        using var workload = new Hex1bAppWorkloadAdapter(presentation);
        var called = false;
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            RunCallback = _ =>
            {
                called = true;
                workload.EnterTuiMode();
                workload.ExitTuiMode();
                workload.Dispose();
                return Task.FromResult(0);
            }
        });
        var run = terminal.RunAsync();
        await presentation.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(called);
        presentation.Ready.TrySetResult();
        Assert.AreEqual(0, await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(supported ? 1 : 0, presentation.Pushes);
        Assert.AreEqual(supported ? 1 : 0, presentation.Pops);
        Assert.AreEqual(1, presentation.RawEntries);
    }

    [TestMethod]
    public async Task Start_DelayedRawMode_DoesNotPresentAppOutputBeforeReady()
    {
        await using var presentation = new KeyboardHost(supported: true) { DelayStartup = true };
        using var workload = new Hex1bAppWorkloadAdapter(new TerminalCapabilities { SupportsKittyKeyboard = true });
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation, WorkloadAdapter = workload
        });
        await presentation.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        workload.EnterTuiMode();
        Assert.AreEqual(0, presentation.Pushes);
        presentation.Ready.TrySetResult();
        await presentation.Pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await terminal.DisposeAsync();
        Assert.AreEqual(1, presentation.RawEntries);
        Assert.AreEqual(1, presentation.Pops);
    }

    private sealed class KeyboardHost(bool supported) : IHex1bTerminalPresentationAdapter
    {
        public Hex1bTerminal Host { get; } = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().Build();
        public TaskCompletionSource Pushed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool DelayStartup { get; init; }
        public bool FailPush { get; init; }
        private bool _ready;
        private bool _initialized;
        public int Pushes { get; private set; }
        public int Pops { get; private set; }
        public int FlagsAfterPop { get; private set; }
        public int RawEntries { get; private set; }
        public List<string> Trace { get; } = [];
        public int Width => 80;
        public int Height => 24;
        public TerminalCapabilities Capabilities => new() { SupportsKittyKeyboard = supported && (!DelayStartup || _ready) };
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }
        public async ValueTask EnterRawModeAsync(CancellationToken ct = default)
        {
            RawEntries++;
            Started.TrySetResult();
            if (DelayStartup)
                await Ready.Task.WaitAsync(ct);
            _ready = true;
        }
        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (!_initialized)
            {
                Host.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=4u\x1b[?1049h\x1b[=8u\x1b[?1049l"));
                _initialized = true;
            }
            var text = Encoding.UTF8.GetString(data.Span);
            if (FailPush && text.Contains("\x1b[>31u", StringComparison.Ordinal))
                throw new IOException("push write rejected");
            foreach (var token in AnsiTokenizer.Tokenize(text))
            {
                Host.ApplyTokens([token]);
                if (token is UnrecognizedSequenceToken { Sequence: "\x1b[>31u" })
                {
                    Pushes++;
                    Trace.Add("push");
                }
                if (token is UnrecognizedSequenceToken { Sequence: "\x1b[<u" })
                {
                    Pops++;
                    FlagsAfterPop = Host.InputModes.KittyKeyboardFlags;
                    Trace.Add("pop");
                }
                if (token is PrivateModeToken { Mode: 1049, Enable: false })
                    Trace.Add("alt-exit");
            }
            if (Pushes > 0)
                Pushed.TrySetResult();
            return ValueTask.CompletedTask;
        }
        public ValueTask ExitRawModeAsync(CancellationToken ct = default)
        {
            Trace.Add("raw-exit");
            return ValueTask.CompletedTask;
        }
        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return default;
        }
        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => Host.DisposeAsync();
    }

    private sealed class PassThroughFilter : IHex1bTerminalPresentationFilter
    {
        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
            IReadOnlyList<AppliedToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<AnsiToken>>(tokens.Select(t => t.Token).ToArray());
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }
}
