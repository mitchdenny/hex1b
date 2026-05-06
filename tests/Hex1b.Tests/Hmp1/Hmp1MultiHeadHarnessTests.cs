using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using Hex1b;
using Xunit.Sdk;
using Xunit.v3;

namespace Hex1b.Tests.Hmp1;

/// <summary>
/// Tier 3 multi-head end-to-end harness tests.
///
/// <para>
/// Layer A (most tests): N bare <see cref="Hmp1WorkloadAdapter"/> peers
/// against one <see cref="Hmp1PresentationAdapter"/>, connected via in-memory
/// duplex pipe pairs. Fast, deterministic, exercises the role state machine
/// under contention.
/// </para>
/// <para>
/// Layer B (smoke): one full <see cref="Hex1bTerminal"/> built with
/// <c>WithHmp1Stream</c> + <c>WithHeadless</c> against the same producer,
/// proving that the builder + lifecycle + <see cref="Hex1bTerminal.Hmp1"/>
/// access all wire up end-to-end.
/// </para>
/// </summary>
public class Hmp1MultiHeadHarnessTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(30);

    // ---- Layer A: bare adapter pairs ----------------------------------

    [Fact]
    public async Task RoundRobin_PrimaryHandoff_AllPeersConverge()
    {
        await using var harness = await BareHarness.CreateAsync(peerCount: 4);

        var dims = new (int Cols, int Rows)[]
        {
            (80, 24),
            (100, 30),
            (120, 40),
            (90, 28),
        };

        for (var i = 0; i < harness.Peers.Count; i++)
        {
            var peer = harness.Peers[i];
            var (cols, rows) = dims[i];

            await peer.Adapter.RequestPrimaryAsync(cols, rows);
            var ok = await peer.Adapter.WaitForRoleAsync(primary: true, ShortTimeout, CancellationToken.None);
            Assert.True(ok, $"Peer {i} did not become primary within timeout.");

            await harness.WaitUntilAsync(
                () => harness.Peers.All(p => p.Adapter.PrimaryPeerId == peer.Adapter.PeerId)
                    && harness.Server.PrimaryPeerId == peer.Adapter.PeerId
                    && harness.Server.Width == cols
                    && harness.Server.Height == rows,
                ShortTimeout,
                () => $"Convergence failed for peer {i} ({cols}x{rows}). " + harness.DescribeState());
        }
    }

    [Fact]
    public async Task ConcurrentTakeOver_AllPeersConvergeOnSomeWinner()
    {
        await using var harness = await BareHarness.CreateAsync(peerCount: 4);

        // Fire RequestPrimary from all peers in parallel. Producer processes in
        // receive order; we don't assert which peer wins — only that every peer
        // (and the producer) converges on the same winner.
        var tasks = harness.Peers
            .Select((p, i) => Task.Run(async () =>
                await p.Adapter.RequestPrimaryAsync(100 + i, 30 + i)))
            .ToArray();
        await Task.WhenAll(tasks);

        await harness.WaitUntilAsync(
            () =>
            {
                var producerWinner = harness.Server.PrimaryPeerId;
                if (producerWinner is null)
                {
                    return false;
                }
                return harness.Peers.All(p => p.Adapter.PrimaryPeerId == producerWinner);
            },
            ShortTimeout,
            () => "Peers failed to converge on a single winner. " + harness.DescribeState());

        var winner = harness.Server.PrimaryPeerId;
        Assert.NotNull(winner);
        Assert.Contains(harness.Peers, p => p.Adapter.PeerId == winner);
    }

    [Fact]
    public async Task RequestPrimary_InterleavedWithPrimaryResize_ConvergesOnTakeover()
    {
        await using var harness = await BareHarness.CreateAsync(peerCount: 2);
        var primary = harness.Peers[0];
        var challenger = harness.Peers[1];

        await primary.Adapter.RequestPrimaryAsync(120, 40);
        await primary.Adapter.WaitForRoleAsync(primary: true, ShortTimeout, CancellationToken.None);
        await harness.WaitUntilAsync(
            () => challenger.Adapter.PrimaryPeerId == primary.Adapter.PeerId,
            ShortTimeout,
            () => "Challenger never observed initial primary. " + harness.DescribeState());

        using var resizeCts = new CancellationTokenSource();
        var resizeLoop = Task.Run(async () =>
        {
            var w = 120;
            try
            {
                while (!resizeCts.Token.IsCancellationRequested)
                {
                    await primary.Adapter.ResizeAsync(w, 40);
                    w = w == 120 ? 130 : 120;
                    await Task.Delay(2, resizeCts.Token);
                }
            }
            catch (OperationCanceledException) { }
        });

        // Mid-flight take-over.
        await Task.Delay(20);
        await challenger.Adapter.RequestPrimaryAsync(99, 33);
        await challenger.Adapter.WaitForRoleAsync(primary: true, ShortTimeout, CancellationToken.None);

        // Stop the loop and let any in-flight Resize-from-old-primary frames be
        // dropped server-side (they're not primary anymore).
        await resizeCts.CancelAsync();
        await resizeLoop;

        await harness.WaitUntilAsync(
            () => harness.Server.PrimaryPeerId == challenger.Adapter.PeerId
               && harness.Server.Width == 99
               && harness.Server.Height == 33,
            ShortTimeout,
            () => "Producer state did not converge on challenger's dims. " + harness.DescribeState());

        await harness.WaitUntilAsync(
            () => harness.Peers.All(p => p.Adapter.PrimaryPeerId == challenger.Adapter.PeerId),
            ShortTimeout,
            () => "Peers did not converge on challenger. " + harness.DescribeState());
    }

    [Fact]
    public async Task PrimaryDisconnect_NoAutoPromotion()
    {
        await using var harness = await BareHarness.CreateAsync(peerCount: 3);
        var primary = harness.Peers[0];

        await primary.Adapter.RequestPrimaryAsync(110, 35);
        await primary.Adapter.WaitForRoleAsync(primary: true, ShortTimeout, CancellationToken.None);

        await harness.DisconnectPeerAsync(0);

        await harness.WaitUntilAsync(
            () => harness.Server.PrimaryPeerId is null,
            ShortTimeout,
            () => "Producer did not clear primary after disconnect. " + harness.DescribeState());

        // Remaining peers should also see PrimaryPeerId become null.
        var remaining = harness.Peers.Where(p => p.IsConnected).ToList();
        Assert.NotEmpty(remaining);
        await harness.WaitUntilAsync(
            () => remaining.All(p => p.Adapter.PrimaryPeerId is null),
            ShortTimeout,
            () => "Remaining peers did not observe primary clear. " + harness.DescribeState());

        // No peer was auto-promoted.
        Assert.All(remaining, p => Assert.False(p.Adapter.IsPrimary));
    }

    [Fact]
    public async Task RosterChurn_PeerCountsConverge()
    {
        await using var harness = await BareHarness.CreateAsync(peerCount: 1);

        // Connect/disconnect a wave of peers.
        const int waveSize = 5;
        for (var round = 0; round < 3; round++)
        {
            var added = new List<HarnessPeer>();
            for (var i = 0; i < waveSize; i++)
            {
                added.Add(await harness.AddPeerAsync($"churn-{round}-{i}"));
            }

            await harness.WaitUntilAsync(
                () => harness.Server.ClientCount == 1 + added.Count,
                ShortTimeout,
                () => $"Producer client count {harness.Server.ClientCount} != expected {1 + added.Count} after add wave {round}.");

            foreach (var p in added)
            {
                await p.DisposeAsync();
            }

            await harness.WaitUntilAsync(
                () => harness.Server.ClientCount == 1,
                ShortTimeout,
                () => $"Producer client count {harness.Server.ClientCount} != 1 after disconnect wave {round}.");
        }
    }

    [Fact]
    public async Task WaitForRoleAsync_TimesOutWhenNotPromoted()
    {
        await using var harness = await BareHarness.CreateAsync(peerCount: 2);

        // Neither peer has requested primary; expect WaitForRoleAsync(true) to
        // time out within a short budget.
        var ok = await harness.Peers[0].Adapter.WaitForRoleAsync(
            primary: true, TimeSpan.FromMilliseconds(200), CancellationToken.None);
        Assert.False(ok);
    }

    [Fact]
    public async Task PrimaryResize_BroadcastsCurrentDimsToAllPeers()
    {
        await using var harness = await BareHarness.CreateAsync(peerCount: 3);
        var primary = harness.Peers[0];

        await primary.Adapter.RequestPrimaryAsync(130, 42);
        await primary.Adapter.WaitForRoleAsync(primary: true, ShortTimeout, CancellationToken.None);

        await primary.Adapter.ResizeAsync(150, 50);

        await harness.WaitUntilAsync(
            () => harness.Server.Width == 150 && harness.Server.Height == 50
               && harness.Peers.All(p => p.Adapter.CurrentWidth == 150 && p.Adapter.CurrentHeight == 50),
            ShortTimeout,
            () => "Resize broadcast did not converge. " + harness.DescribeState());
    }

    [Fact]
    public async Task StressLoop_Invariants_HoldThroughout()
    {
        var iterations = ParseEnvInt("HEX1B_MULTIHEAD_STRESS_ITERATIONS", defaultValue: 25);
        var seed = ParseEnvInt("HEX1B_MULTIHEAD_STRESS_SEED", defaultValue: Random.Shared.Next());
        var rng = new Random(seed);

        await using var harness = await BareHarness.CreateAsync(peerCount: 3);

        var failures = new List<string>();
        for (var i = 0; i < iterations; i++)
        {
            var peerIndex = rng.Next(harness.Peers.Count);
            var cols = 60 + rng.Next(80);
            var rows = 20 + rng.Next(30);

            try
            {
                await harness.Peers[peerIndex].Adapter.RequestPrimaryAsync(cols, rows);
            }
            catch (Exception ex)
            {
                failures.Add($"iter {i} (seed {seed}, peer {peerIndex}, {cols}x{rows}): {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            // Producer winner can change again before convergence; we only need
            // periodic checks that producer and peers don't get permanently stuck.
            if ((i % 10) == 0 || i == iterations - 1)
            {
                await harness.WaitUntilAsync(
                    () =>
                    {
                        var winner = harness.Server.PrimaryPeerId;
                        if (winner is null)
                        {
                            return false;
                        }
                        // All peers either agree on the producer's winner or are
                        // still observing a *prior* winner that's about to be
                        // overwritten by a later RoleChange. We accept anything
                        // that's a known peer id (no orphan / null divergence
                        // after we've established a primary).
                        return harness.Peers.All(p =>
                            p.Adapter.PrimaryPeerId == winner
                            || harness.Peers.Any(other => other.Adapter.PeerId == p.Adapter.PrimaryPeerId));
                    },
                    LongTimeout,
                    () => $"Stress iter {i} (seed {seed}) divergence. " + harness.DescribeState());
            }
        }

        Assert.True(failures.Count == 0, $"Stress run had {failures.Count} failures (seed {seed}):\n  " + string.Join("\n  ", failures));

        // Final convergence: every peer agrees on whoever the producer says is
        // primary. Per rubber-duck guidance, do not assert a specific winner —
        // under sequential RequestPrimary calls the producer may still be
        // draining earlier frames when this test issues its last call, and the
        // protocol does not guarantee LIFO ordering between issuance and the
        // producer's per-peer read pump. We only require eventual agreement.
        await harness.WaitUntilAsync(
            () =>
            {
                var winner = harness.Server.PrimaryPeerId;
                if (winner is null)
                {
                    return false;
                }
                return harness.Peers.All(p => p.Adapter.PrimaryPeerId == winner);
            },
            LongTimeout,
            () => $"Final convergence failed (seed {seed}). " + harness.DescribeState());
    }

    // ---- Layer B: one full Hex1bTerminal --------------------------------

    [Fact]
    public async Task LayerB_Hex1bTerminalSmokeTest_ExposesHmp1Adapter()
    {
        var server = new Hmp1PresentationAdapter(80, 24);
        await using var serverDispose = (IAsyncDisposable)server;

        var (s1, s2) = CreateFullDuplexPair();

        // Build a real Hex1bTerminal whose workload is HMP1 over the duplex pair.
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless()
            .WithHmp1Stream(s2, displayName: "layer-b", defaultRole: "viewer")
            .Build();
        await using var terminalDispose = terminal;

        // Connect the producer side concurrently with terminal.RunAsync.
        using var ctsRun = new CancellationTokenSource();
        var addTask = server.AddClient(s1, ctsRun.Token);
        var runTask = Task.Run(() => terminal.RunAsync(ctsRun.Token));

        var handle = await addTask.WaitAsync(ShortTimeout);
        await using var handleDispose = handle;

        // Wait for the workload adapter's handshake to complete.
        var hmp1 = await WaitForNonNullAsync(() => terminal.Hmp1, ShortTimeout)
            ?? throw new Xunit.Sdk.XunitException("terminal.Hmp1 was null after RunAsync started.");

        // Hmp1 surface is reachable and reports the connection is healthy.
        await WaitUntilAsync(
            () => !string.IsNullOrEmpty(hmp1.PeerId),
            ShortTimeout,
            () => "Hmp1 peer id never assigned.");

        Assert.False(hmp1.IsPrimary);
        Assert.Null(hmp1.PrimaryPeerId);

        // Take primary through the full Hex1bTerminal pipeline and verify it
        // reaches the producer's authoritative state.
        await hmp1.RequestPrimaryAsync(110, 35);
        await hmp1.WaitForRoleAsync(primary: true, ShortTimeout, CancellationToken.None);

        Assert.Equal(hmp1.PeerId, server.PrimaryPeerId);
        Assert.Equal(110, server.Width);
        Assert.Equal(35, server.Height);

        // Tear down cooperatively.
        await ctsRun.CancelAsync();
        try
        {
            await runTask.WaitAsync(ShortTimeout);
        }
        catch (OperationCanceledException) { }
    }

    // ---- Helpers ------------------------------------------------------

    private static int ParseEnvInt(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out var v) ? v : defaultValue;
    }

    private static async Task<T?> WaitForNonNullAsync<T>(Func<T?> probe, TimeSpan timeout) where T : class
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var v = probe();
            if (v is not null)
            {
                return v;
            }
            await Task.Delay(10);
        }
        return null;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout, Func<string> describeFailure)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (predicate())
            {
                return;
            }
            await Task.Delay(10);
        }
        if (!predicate())
        {
            throw new Xunit.Sdk.XunitException("Predicate failed: " + describeFailure());
        }
    }

    private static (Stream S1, Stream S2) CreateFullDuplexPair()
    {
        var p12 = new Pipe();
        var p21 = new Pipe();
        return (
            new DuplexPipeStream(p21.Reader.AsStream(), p12.Writer.AsStream()),
            new DuplexPipeStream(p12.Reader.AsStream(), p21.Writer.AsStream()));
    }

    private sealed class DuplexPipeStream(Stream readStream, Stream writeStream) : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => writeStream.Flush();
        public override Task FlushAsync(CancellationToken ct) => writeStream.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => readStream.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => readStream.ReadAsync(buffer, ct);
        public override void Write(byte[] buffer, int offset, int count) => writeStream.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => writeStream.WriteAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { readStream.Dispose(); } catch { }
                try { writeStream.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    private sealed class HarnessPeer : IAsyncDisposable
    {
        public Hmp1ClientHandle Handle { get; }
        public Hmp1WorkloadAdapter Adapter { get; }
        public string Label { get; }
        public bool IsConnected { get; private set; } = true;

        private CancellationTokenSource? _drainCts;
        private Task? _drainTask;

        public HarnessPeer(Hmp1ClientHandle handle, Hmp1WorkloadAdapter adapter, string label)
        {
            Handle = handle;
            Adapter = adapter;
            Label = label;
        }

        public void StartOutputDrain()
        {
            _drainCts = new CancellationTokenSource();
            var ct = _drainCts.Token;
            _drainTask = Task.Run(async () =>
            {
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        var data = await Adapter.ReadOutputAsync(ct);
                        if (data.IsEmpty)
                        {
                            // ReadOutputAsync returns empty on disconnect / cancel.
                            await Task.Delay(5, ct);
                        }
                    }
                }
                catch (OperationCanceledException) { }
            });
        }

        public async ValueTask DisposeAsync()
        {
            if (!IsConnected)
            {
                return;
            }
            IsConnected = false;
            if (_drainCts is not null)
            {
                try { await _drainCts.CancelAsync(); } catch { }
            }
            try { await Handle.DisposeAsync(); } catch { }
            try { await Adapter.DisposeAsync(); } catch { }
            if (_drainTask is not null)
            {
                try { await _drainTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
            }
            _drainCts?.Dispose();
        }
    }

    private sealed class BareHarness : IAsyncDisposable
    {
        public Hmp1PresentationAdapter Server { get; }
        public List<HarnessPeer> Peers { get; } = [];

        private BareHarness(Hmp1PresentationAdapter server)
        {
            Server = server;
        }

        public static async Task<BareHarness> CreateAsync(int peerCount, int initialCols = 80, int initialRows = 24)
        {
            var harness = new BareHarness(new Hmp1PresentationAdapter(initialCols, initialRows));
            for (var i = 0; i < peerCount; i++)
            {
                await harness.AddPeerAsync($"peer-{i}");
            }
            return harness;
        }

        public async Task<HarnessPeer> AddPeerAsync(string label)
        {
            var (s1, s2) = CreateFullDuplexPair();
            var adapter = new Hmp1WorkloadAdapter(s2, displayName: label);
            var addTask = Server.AddClient(s1, CancellationToken.None);
            var connectTask = adapter.ConnectAsync(CancellationToken.None);
            var handle = await addTask.WaitAsync(ShortTimeout);
            await connectTask.WaitAsync(ShortTimeout);
            var peer = new HarnessPeer(handle, adapter, label);
            // Drain output frames so the per-peer Output channel doesn't fill,
            // which would back-pressure the producer's per-client write pump and
            // stall further broadcasts to this peer. Layer A tests don't have a
            // real terminal consuming output, so the harness drains for them.
            peer.StartOutputDrain();
            Peers.Add(peer);
            return peer;
        }

        public async Task DisconnectPeerAsync(int index)
        {
            await Peers[index].DisposeAsync();
        }

        public async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout, Func<string> describeFailure)
        {
            await Hmp1MultiHeadHarnessTests.WaitUntilAsync(predicate, timeout, describeFailure);
        }

        public string DescribeState()
        {
            var lines = new List<string>
            {
                $"Server: PrimaryPeerId={Server.PrimaryPeerId ?? "null"}, Width={Server.Width}, Height={Server.Height}, ClientCount={Server.ClientCount}",
            };
            foreach (var peer in Peers)
            {
                lines.Add($"  {peer.Label}: PeerId={peer.Adapter.PeerId}, IsConnected={peer.IsConnected}, IsPrimary={peer.Adapter.IsPrimary}, PrimaryPeerId={peer.Adapter.PrimaryPeerId ?? "null"}, Cur={peer.Adapter.CurrentWidth}x{peer.Adapter.CurrentHeight}");
            }
            return Environment.NewLine + string.Join(Environment.NewLine, lines);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var peer in Peers)
            {
                await peer.DisposeAsync();
            }
            await Server.DisposeAsync();
        }
    }
}
