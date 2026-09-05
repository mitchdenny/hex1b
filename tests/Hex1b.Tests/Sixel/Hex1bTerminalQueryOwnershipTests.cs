using System.Text;
using System.Threading.Channels;
using Hex1b.Sixel;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Coverage for stage #455's query-ownership model at the <see cref="Hex1bTerminal"/>
/// level: who answers a hosted workload's Primary Device Attributes (<c>CSI c</c>)
/// and XTWINOPS window-operation (<c>CSI 14/16/18 t</c>) queries, and how effective
/// Sixel support is advertised back to that workload.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Hex1bTerminal"/> only stays silent for presentations whose
/// <see cref="IHex1bTerminalPresentationAdapter.AnswersProtocolQueriesDirectly"/> is
/// <see langword="true"/> (raw upstream passthrough, where a real terminal already
/// answers these queries itself); every other presentation — headless, WebSocket, or
/// a hand-written fake — gets a synthesized reply from <see cref="Hex1bTerminal"/>'s
/// own authoritative model, so exactly one answerer always exists and duplicate
/// responses are impossible.
/// </para>
/// <para>
/// These tests intentionally build presentation fakes with hand-picked
/// <see cref="TerminalCapabilities"/> rather than exercising
/// <see cref="ConsolePresentationAdapter"/>'s probe engine (covered exhaustively in
/// <see cref="SixelCapabilityDiscoveryTests"/>); the concern here is purely "given
/// some resolved capability, does Hex1bTerminal answer correctly and exactly once".
/// </para>
/// </remarks>
[TestClass]
public class Hex1bTerminalQueryOwnershipTests
{
    private const string Da1Query = "\x1b[c";
    private const string Csi18Query = "\x1b[18t";
    private const string Csi16Query = "\x1b[16t";
    private const string Csi14Query = "\x1b[14t";

    private static readonly TimeSpan NoReplyGracePeriod = TimeSpan.FromMilliseconds(200);

    // Test doubles ------------------------------------------------------------

    /// <summary>
    /// A minimal raw-byte workload adapter: queued "output" bytes are what a
    /// hosted application would write (here, protocol queries); bytes the
    /// terminal writes back via <see cref="WriteInputAsync"/> are captured for
    /// assertions, mirroring how a real workload would receive a synthesized
    /// reply as ordinary input.
    /// </summary>
    private sealed class QueuedOutputWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly List<byte> _written = [];
        private TaskCompletionSource _writtenChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public void EnqueueOutput(string text) => _output.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

        public byte[] WrittenBytes
        {
            get
            {
                lock (_written)
                {
                    return [.. _written];
                }
            }
        }

        public async Task WaitForWrittenLengthAsync(int minimumLength, CancellationToken ct)
        {
            while (true)
            {
                Task changed;
                lock (_written)
                {
                    if (_written.Count >= minimumLength)
                        return;
                    changed = _writtenChanged.Task;
                }

                await changed.WaitAsync(ct);
            }
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            while (await _output.Reader.WaitToReadAsync(ct))
            {
                if (_output.Reader.TryRead(out var item))
                    return item;
            }

            return ReadOnlyMemory<byte>.Empty;
        }

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            TaskCompletionSource changed;
            lock (_written)
            {
                _written.AddRange(data.ToArray());
                changed = _writtenChanged;
                _writtenChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            changed.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            _output.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A minimal presentation fake that is NOT a real upstream terminal (leaves
    /// <see cref="IHex1bTerminalPresentationAdapter.AnswersProtocolQueriesDirectly"/>
    /// at its default <see langword="false"/>), so <see cref="Hex1bTerminal"/> must
    /// own query answering for it.
    /// </summary>
    private class FakePresentationAdapter(TerminalCapabilities capabilities) : IHex1bTerminalPresentationAdapter
    {
        public int Width => 80;
        public int Height => 24;
        public TerminalCapabilities Capabilities { get; } = capabilities;

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
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public virtual bool AnswersProtocolQueriesDirectly => false;
    }

    /// <summary>
    /// The same fake, but with <see cref="AnswersProtocolQueriesDirectly"/>
    /// overridden to <see langword="true"/> -- Hex1bTerminal must stay silent for
    /// DA1/window-op queries since a real terminal on the other end of this
    /// presentation would already answer them.
    /// </summary>
    private sealed class NativeFakePresentationAdapter(TerminalCapabilities capabilities)
        : FakePresentationAdapter(capabilities)
    {
        public override bool AnswersProtocolQueriesDirectly => true;
    }

    private static (Hex1bTerminal Terminal, QueuedOutputWorkloadAdapter Workload) CreateTerminal(
        IHex1bTerminalPresentationAdapter presentation)
    {
        var workload = new QueuedOutputWorkloadAdapter();
        var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = workload,
            Width = 80,
            Height = 24,
        });
        return (terminal, workload);
    }

    private static TerminalCapabilities SixelCapabilities(
        SixelPresentationSupport support,
        bool supportsSixel,
        SixelCellMetrics? metrics = null) => new()
    {
        SupportsSixel = supportsSixel,
        SixelSupport = support,
        SixelCellMetrics = metrics,
    };

    // DA1 -----------------------------------------------------------------------

    [TestMethod]
    public async Task Da1Query_NonNativePresentationWithSixelSupport_RepliesDeclaringParameter4()
    {
        var presentation = new FakePresentationAdapter(
            SixelCapabilities(SixelPresentationSupport.Native, supportsSixel: true));
        var (terminal, workload) = CreateTerminal(presentation);
        await using var t = terminal;

        workload.EnqueueOutput(Da1Query);
        await workload.WaitForWrittenLengthAsync(1, TestContext.Current.CancellationToken);

        Assert.AreEqual("\x1b[?62;4c", Encoding.UTF8.GetString(workload.WrittenBytes));
    }

    [TestMethod]
    public async Task Da1Query_NonNativePresentationWithoutSixelSupport_RepliesWithoutParameter4()
    {
        var presentation = new FakePresentationAdapter(
            SixelCapabilities(SixelPresentationSupport.None, supportsSixel: false));
        var (terminal, workload) = CreateTerminal(presentation);
        await using var t = terminal;

        workload.EnqueueOutput(Da1Query);
        await workload.WaitForWrittenLengthAsync(1, TestContext.Current.CancellationToken);

        Assert.AreEqual("\x1b[?62c", Encoding.UTF8.GetString(workload.WrittenBytes));
    }

    [TestMethod]
    public async Task Da1Query_NonNativePresentationWithUnknownSixelSupport_RepliesWithoutParameter4()
    {
        // A no-probe state (discovery has not run, or is still pending) must be
        // just as conservative as a confirmed-unsupported one: parser capability
        // alone is not sufficient grounds to advertise Sixel to the workload.
        var presentation = new FakePresentationAdapter(
            SixelCapabilities(SixelPresentationSupport.Unknown, supportsSixel: false));
        var (terminal, workload) = CreateTerminal(presentation);
        await using var t = terminal;

        workload.EnqueueOutput(Da1Query);
        await workload.WaitForWrittenLengthAsync(1, TestContext.Current.CancellationToken);

        Assert.AreEqual("\x1b[?62c", Encoding.UTF8.GetString(workload.WrittenBytes));
    }

    [TestMethod]
    public async Task Da1Query_NativeUpstreamPresentation_ReceivesNoSynthesizedReply()
    {
        var presentation = new NativeFakePresentationAdapter(
            SixelCapabilities(SixelPresentationSupport.Native, supportsSixel: true));
        var (terminal, workload) = CreateTerminal(presentation);
        await using var t = terminal;

        workload.EnqueueOutput(Da1Query);
        await Task.Delay(NoReplyGracePeriod, TestContext.Current.CancellationToken);

        Assert.IsEmpty(workload.WrittenBytes,
            "A native upstream presentation's own real terminal answers DA1; Hex1bTerminal must not duplicate it.");
    }

    // Window operations -----------------------------------------------------------

    [TestMethod]
    public async Task WindowOperation18_NonNativePresentation_RepliesWithRowsAndColumns()
    {
        var presentation = new FakePresentationAdapter(TerminalCapabilities.Minimal);
        var (terminal, workload) = CreateTerminal(presentation);
        await using var t = terminal;

        workload.EnqueueOutput(Csi18Query);
        await workload.WaitForWrittenLengthAsync(1, TestContext.Current.CancellationToken);

        Assert.AreEqual($"\x1b[8;{terminal.Height};{terminal.Width}t", Encoding.UTF8.GetString(workload.WrittenBytes));
    }

    [TestMethod]
    public async Task WindowOperation16_NonNativePresentation_RepliesWithCellPixelSizeFromCapabilities()
    {
        var metrics = new SixelCellMetrics(12, 24, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative);
        var presentation = new FakePresentationAdapter(
            SixelCapabilities(SixelPresentationSupport.Headless, supportsSixel: true, metrics));
        var (terminal, workload) = CreateTerminal(presentation);
        await using var t = terminal;

        workload.EnqueueOutput(Csi16Query);
        await workload.WaitForWrittenLengthAsync(1, TestContext.Current.CancellationToken);

        // BuildCellPixelSizeResponse reports "CSI 6 ; height ; width t".
        Assert.AreEqual("\x1b[6;24;12t", Encoding.UTF8.GetString(workload.WrittenBytes));
    }

    [TestMethod]
    public async Task WindowOperation14_NonNativePresentation_RepliesWithTextAreaPixelSize()
    {
        var metrics = new SixelCellMetrics(10, 20, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative);
        var presentation = new FakePresentationAdapter(
            SixelCapabilities(SixelPresentationSupport.Headless, supportsSixel: true, metrics));
        var (terminal, workload) = CreateTerminal(presentation);
        await using var t = terminal;

        workload.EnqueueOutput(Csi14Query);
        await workload.WaitForWrittenLengthAsync(1, TestContext.Current.CancellationToken);

        // 80 columns * 10px = 800; 24 rows * 20px = 480.
        Assert.AreEqual("\x1b[4;480;800t", Encoding.UTF8.GetString(workload.WrittenBytes));
    }

    [TestMethod]
    [DataRow(Csi14Query, DisplayName = "CSI 14 t")]
    [DataRow(Csi16Query, DisplayName = "CSI 16 t")]
    [DataRow(Csi18Query, DisplayName = "CSI 18 t")]
    public async Task WindowOperation_NativeUpstreamPresentation_ReceivesNoSynthesizedReply(string query)
    {
        var presentation = new NativeFakePresentationAdapter(TerminalCapabilities.Modern);
        var (terminal, workload) = CreateTerminal(presentation);
        await using var t = terminal;

        workload.EnqueueOutput(query);
        await Task.Delay(NoReplyGracePeriod, TestContext.Current.CancellationToken);

        Assert.IsEmpty(workload.WrittenBytes,
            "A native upstream presentation's own real terminal answers window operations; Hex1bTerminal must not duplicate it.");
    }

    // Effective-support advertisement: unknown/unsupported stay distinct from supported ------

    [TestMethod]
    public async Task HeadlessPresentation_DefaultMinimalCapabilities_DoesNotAdvertiseSixel()
    {
        // Hex1b's own Sixel parser/model support is unconditional, but a default
        // headless presentation has never declared authoritative Sixel support,
        // so the workload-facing DA1 reply must not claim it either.
        await using var presentation = new HeadlessPresentationAdapter(80, 24);
        var (terminal, workload) = CreateTerminal(presentation);
        await using var t = terminal;

        workload.EnqueueOutput(Da1Query);
        await workload.WaitForWrittenLengthAsync(1, TestContext.Current.CancellationToken);

        Assert.AreEqual("\x1b[?62c", Encoding.UTF8.GetString(workload.WrittenBytes));
    }

    [TestMethod]
    public async Task HeadlessPresentation_WithAuthoritativeSixelSupport_AdvertisesSixel()
    {
        var capabilities = SixelCapabilities(SixelPresentationSupport.Headless, supportsSixel: true);
        await using var presentation = new HeadlessPresentationAdapter(80, 24, capabilities);
        var (terminal, workload) = CreateTerminal(presentation);
        await using var t = terminal;

        workload.EnqueueOutput(Da1Query);
        await workload.WaitForWrittenLengthAsync(1, TestContext.Current.CancellationToken);

        Assert.AreEqual("\x1b[?62;4c", Encoding.UTF8.GetString(workload.WrittenBytes));
    }

    [TestMethod]
    public void WebSocketPresentationAdapter_DefaultCapabilities_AdvertisesNativeSixelSupport()
    {
        // WebSocketPresentationAdapter.Capabilities is a pure, stateless computed
        // property that never touches the underlying socket, so a throwaway
        // WebSocket that is never actually used for I/O is sufficient here; the
        // end-to-end DA1-reply behavior for a given Capabilities value is already
        // covered generically above.
        using var socket = new NeverUsedWebSocket();
        var adapter = new WebSocketPresentationAdapter(socket, 80, 24);

        Assert.IsTrue(adapter.Capabilities.SupportsSixel);
        Assert.AreEqual(SixelPresentationSupport.Native, adapter.Capabilities.SixelSupport);
    }

    private sealed class NeverUsedWebSocket : System.Net.WebSockets.WebSocket
    {
        public override System.Net.WebSockets.WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override System.Net.WebSockets.WebSocketState State => System.Net.WebSockets.WebSocketState.Open;
        public override string? SubProtocol => null;

        public override void Abort() => throw new NotSupportedException();
        public override Task CloseAsync(System.Net.WebSockets.WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public override Task CloseOutputAsync(System.Net.WebSockets.WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public override void Dispose()
        {
        }
        public override Task<System.Net.WebSockets.WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public override Task SendAsync(ArraySegment<byte> buffer, System.Net.WebSockets.WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
