using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Kitty keyboard support detection at the console boundary. The specification's
/// detection recipe is: send <c>CSI ? u</c> followed by DA1; a <c>CSI ? flags u</c>
/// reply means support, and DA1 arriving first means no support. Under ConPTY the
/// console host answers DA1 itself before the outer terminal's Kitty reply, so the
/// Windows driver waits for the deadline instead of treating DA1 as conclusive.
/// </summary>
[TestClass]
public class KittyConsoleProbeTests
{
    private static readonly TimeSpan ShortProbe = TimeSpan.FromMilliseconds(40);

    private static async Task<(ConsolePresentationAdapter Adapter, string Input)> ProbeAsync(
        FakeConsoleDriver driver, bool kittyDa1Conclusive = true)
    {
        var adapter = new ConsolePresentationAdapter(
            driver, kgpProbeTimeout: ShortProbe, kittyDa1Conclusive: kittyDa1Conclusive);
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        return (adapter, await DrainPrefetchedAsync(adapter));
    }

    private static async Task<string> DrainPrefetchedAsync(ConsolePresentationAdapter adapter)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var bytes = await adapter.ReadInputAsync(cancellation.Token);
        return Encoding.ASCII.GetString(bytes.Span);
    }

    private static string[] Bytewise(string text) => text.Select(c => c.ToString()).ToArray();

    [TestMethod]
    public async Task EnterRawModeAsync_WritesKittyQueryBeforeDa1()
    {
        using var driver = new FakeConsoleDriver();
        var (adapter, _) = await ProbeAsync(driver);
        await adapter.DisposeAsync();

        var written = driver.WrittenText;
        var kitty = written.IndexOf("\x1b[?u", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, kitty);
        Assert.IsGreaterThan(kitty, written.IndexOf("\x1b[c", StringComparison.Ordinal));
        Assert.DoesNotContain("\x1b[>", written, "Support detection must never push keyboard flags.");
        Assert.DoesNotContain("\x1b[<u", written);
    }

    // Any well-formed reply means the protocol is supported, including flags 0 (nothing
    // currently enabled) and reserved bits: the effective flags are negotiated after push.
    [TestMethod]
    [DataRow("\x1b[?0u")]
    [DataRow("\x1b[?1u")]
    [DataRow("\x1b[?31u")]
    [DataRow("\x1b[?0031u")]
    [DataRow("\x1b[?32u")]
    [DataRow("\x1b[?255u")]
    public async Task EnterRawModeAsync_ValidKittyReply_EnablesSupportAndConsumesReply(string reply)
    {
        using var driver = new FakeConsoleDriver($"a{reply}b\x1b[?62;22cc");
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("abc", input);
    }

    [TestMethod]
    [DataRow("\x1b[?;1u")]
    [DataRow("\x1b[?1;2u")]
    [DataRow("\x1b[?1:2u")]
    [DataRow("\x1b[?u")]
    [DataRow("\x1b[?1234567890u")]
    [DataRow("\x1b[?99999999999999999999u")]
    public async Task EnterRawModeAsync_MalformedKittyReply_IsConsumedWithoutEnablingSupport(string reply)
    {
        using var driver = new FakeConsoleDriver($"a{reply}bc");
        var (adapter, input) = await ProbeAsync(driver, kittyDa1Conclusive: false);
        await using var _ = adapter;

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("abc", input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_MalformedThenValidReply_EnablesSupport()
    {
        using var driver = new FakeConsoleDriver("\x1b[?;u", "\x1b[?3u", "\x1b[?62c");
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("", input);
    }

    [TestMethod]
    [DataRow(false, true, true, DisplayName = "Kitty then DA1, spec-conclusive DA1")]
    [DataRow(false, false, true, DisplayName = "Kitty then DA1, ConPTY")]
    [DataRow(true, true, false, DisplayName = "DA1 then Kitty, spec-conclusive DA1")]
    [DataRow(true, false, true, DisplayName = "DA1 then Kitty, ConPTY")]
    public async Task EnterRawModeAsync_ReplyOrder_FollowsDa1Conclusiveness(
        bool da1First, bool da1Conclusive, bool expected)
    {
        const string kitty = "\x1b[?1u";
        const string da1 = "\x1b[?61;6;7;21;22;23;24;28;32;42c";
        using var driver = new FakeConsoleDriver(da1First ? [da1, kitty, "x"] : [kitty, da1, "x"]);
        var (adapter, input) = await ProbeAsync(driver, da1Conclusive);
        await using var _ = adapter;

        Assert.AreEqual(expected, adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("x", input, "Both replies belong to the probe and must not reach the app.");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task EnterRawModeAsync_ByteByByteReplies_AreReassembled(bool da1First)
    {
        var replies = da1First ? "a\x1b[?62;4cb\x1b[?15uc" : "a\x1b[?15ub\x1b[?62;4cc";
        using var driver = new FakeConsoleDriver(Bytewise(replies));
        var (adapter, input) = await ProbeAsync(driver, kittyDa1Conclusive: false);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("abc", input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_AdjacentUnrelatedInput_IsPreservedInOrder()
    {
        const string before = "x\x1b[A\x1b[<0;3;4M\x1bOP\x1b[97;5u";
        const string after = "\x1b[13~\x1b[1;1:3A\x1b[57441;2u\x1b[?1;2$yy";
        using var driver = new FakeConsoleDriver(before + "\x1b[?31u" + after);
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(before + after, input);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EnterRawModeAsync_EscapeKeyBeforeReply_DetectsReplyAndPreservesEscape(bool separateReads)
    {
        using var driver = separateReads
            ? new FakeConsoleDriver("\x1b", "\x1b[?1u", "z")
            : new FakeConsoleDriver("\x1b\x1b[?1uz");
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("\x1bz", input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_EscapeKeyBeforePaste_KeepsPastedReplyAsInput()
    {
        const string input = "\x1b\x1b[200~\x1b[?1u\x1b[201~";
        using var driver = new FakeConsoleDriver(input);
        var (adapter, received) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(input, received);
    }
    public static IEnumerable<object[]> FramedReplies =>
    [
        ["\x1b[200~\x1b[?1u\x1b[?62;4c\x1b[201~"],
        ["\x1b]52;c;\x1b[?1u\x1b[?62c\x07"],
        ["\x1b]0;title \x1b[?1u\x1b[?62c\x1b\\"],
        ["\x1b_Gi=7;\x1b[?1u\x1b[?62c\x1b\\"],
        ["\x1bP1$r\x1b[?1u\x1b[?62c\x1b\\"],
        ["\x1bXsos \x1b[?1u\x1b\\\x1b^pm \x1b[?1u\x1b\\"],
    ];

    [TestMethod]
    [DynamicData(nameof(FramedReplies))]
    public async Task EnterRawModeAsync_ReplyInsideFrame_IsIgnoredAndPreservedVerbatim(string frame)
    {
        using var driver = new FakeConsoleDriver("a" + frame + "b");
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("a" + frame + "b", input);
    }

    [TestMethod]
    [DynamicData(nameof(FramedReplies))]
    public async Task EnterRawModeAsync_FragmentedFrameWithAdjacentGenuineReply_ConsumesOnlyGenuineReply(string frame)
    {
        using var driver = new FakeConsoleDriver(Bytewise("a" + frame + "\x1b[?5ub\x1b[?62c"));
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("a" + frame + "b", input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_GenuineReplyBeforeFrame_DoesNotDisturbFrame()
    {
        const string paste = "\x1b[200~\x1b[?7u\x1b[201~";
        using var driver = new FakeConsoleDriver("\x1b[?5u" + paste, "\x1b[?62c");
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(paste, input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_UnterminatedFrame_IsPreservedWithoutEnablingSupport()
    {
        const string open = "\x1b[200~text \x1b[?1u\x1b[?62c";
        using var driver = new FakeConsoleDriver(open);
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(open, input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_PrefetchedNonUtf8Bytes_ArePreservedByteForByte()
    {
        byte[] input = [0x61, 0x1b, (byte)'[', (byte)'2', (byte)'0', (byte)'0', (byte)'~', 0xc3, 0xa9,
            0x1b, (byte)'[', (byte)'?', (byte)'1', (byte)'u', 0x1b, (byte)'[', (byte)'2', (byte)'0', (byte)'1',
            (byte)'~', 0xe2, 0x82, 0xac];
        using var driver = FakeConsoleDriver.FromByteChunks(Encoding.UTF8, input);
        var (adapter, _) = await ProbeAsyncRaw(driver);
        await using var __ = adapter;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var prefetched = await adapter.ReadInputAsync(cancellation.Token);

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        TestSeq.AreEqual(input, prefetched.ToArray());
    }

    private static async Task<(ConsolePresentationAdapter Adapter, bool Unused)> ProbeAsyncRaw(FakeConsoleDriver driver)
    {
        var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ShortProbe, kittyDa1Conclusive: false);
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        return (adapter, false);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_SixelDeclared_StillDetectsKittyWithoutDa1()
    {
        using var driver = new FakeConsoleDriver("\x1b[?1u");
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ShortProbe)
            .WithSixelSupport(SixelPresentationSupport.None);
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.DoesNotContain("\x1b[c", driver.WrittenText);
        Assert.Contains("\x1b[?u", driver.WrittenText);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_UnsupportedHost_FallsBackWithinDefaultDeadline()
    {
        using var driver = new FakeConsoleDriver();
        await using var adapter = new ConsolePresentationAdapter(driver, kittyDa1Conclusive: false);
        var stopwatch = Stopwatch.StartNew();
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.IsLessThan(TimeSpan.FromSeconds(2), stopwatch.Elapsed,
            "Silence must fall back at the existing combined probe deadline (~150ms).");
    }

    [TestMethod]
    public async Task EnterRawModeAsync_CallerCancellation_LeavesSupportDisabled()
    {
        using var driver = new FakeConsoleDriver();
        await using var adapter = new ConsolePresentationAdapter(
            driver, kgpProbeTimeout: TimeSpan.FromSeconds(30), kittyDa1Conclusive: false);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        var stopwatch = Stopwatch.StartNew();
        await adapter.EnterRawModeAsync(cancellation.Token);
        Assert.IsLessThan(TimeSpan.FromSeconds(5), stopwatch.Elapsed);
        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
    }

    // --- Actual app/console path: support probe, then layer-4 push + effective-flags query ---

    [TestMethod]
    [DataRow(31, true)]
    [DataRow(27, true)]
    [DataRow(10, true)]
    [DataRow(15, true)]
    [DataRow(8, false)]
    [DataRow(2, false)]
    [DataRow(1, false)]
    [DataRow(0, false)]
    public async Task App_ConsoleHost_EffectiveFlagsDecideReleaseGuarantee(int effective, bool releases)
    {
        await using var host = new KittyConsoleHost { EffectiveFlags = effective };
        var app = await RunAppAsync(host, async (app, _) =>
        {
            await WaitForAsync(() => app.InputSourceReportsKeyReleases == releases, "effective flags applied");
        });

        AssertBalanced(host, expectedPushes: 1);
        Assert.AreEqual(releases, host.ReleaseReportsObserved);
    }

    [TestMethod]
    public async Task App_ConsoleHost_ProbeReplyIsNotMistakenForEffectiveFlags()
    {
        // The support reply reports 31 (flags already active in the host) but the host never
        // answers the post-push query; releases stay unconfirmed.
        await using var host = new KittyConsoleHost { ProbeReply = "\x1b[?31u", EffectiveFlags = null };
        await RunAppAsync(host, async (app, _) =>
        {
            await WaitForAsync(() => host.Pushes == 1, "push");
            await Task.Delay(100);
            Assert.IsNull(app.InputSourceReportsKeyReleases);
        });

        AssertBalanced(host, expectedPushes: 1);
        var written = host.Written;
        var probe = written.IndexOf("\x1b[?u", StringComparison.Ordinal);
        var push = written.IndexOf("\x1b[>31u\x1b[?u", StringComparison.Ordinal);
        var pop = written.IndexOf("\x1b[<u", StringComparison.Ordinal);
        var altExit = written.LastIndexOf("\x1b[?1049l", StringComparison.Ordinal);
        Assert.IsTrue(probe >= 0 && probe < push && push < pop && pop < altExit,
            $"Expected probe < push+query < pop < alternate-screen exit: {Escape(written)}");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task App_ConPtyOrdering_Da1BeforeKittyReply_StillPushes(bool da1First)
    {
        await using var host = new KittyConsoleHost { Da1First = da1First, Da1Conclusive = false };
        await RunAppAsync(host, async (app, _) =>
            await WaitForAsync(() => app.InputSourceReportsKeyReleases == true, "releases confirmed"));

        AssertBalanced(host, expectedPushes: 1);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "silent host")]
    [DataRow("\x1b[?;u", DisplayName = "malformed reply")]
    public async Task App_UnsupportedConsoleHost_NeverPushesOrPops(string? probeReply)
    {
        await using var host = new KittyConsoleHost { ProbeReply = probeReply, Da1Conclusive = false };
        await RunAppAsync(host, async (app, _) =>
        {
            await WaitForAsync(() => host.Written.Contains("\x1b[?1049h", StringComparison.Ordinal), "tui entered");
            Assert.IsNull(app.InputSourceReportsKeyReleases);
        });

        Assert.AreEqual(0, host.Pushes);
        Assert.AreEqual(0, host.Pops);
        Assert.DoesNotContain("\x1b[>31u", host.Written);
        Assert.DoesNotContain("\x1b[<u", host.Written);
        Assert.AreEqual(4, host.MainFlags);
        Assert.AreEqual(8, host.AlternateFlags);
    }

    [TestMethod]
    public async Task App_ConsoleHost_CancellationPopsBeforeLeavingAlternateScreen()
    {
        await using var host = new KittyConsoleHost();
        await RunAppAsync(host, async (_, cancel) =>
        {
            await WaitForAsync(() => host.Pushes == 1, "push");
            cancel();
        }, stopByCancellation: true);

        AssertBalanced(host, expectedPushes: 1);
    }

    [TestMethod]
    public async Task App_ConsoleHost_FailedPushWrite_DoesNotLaterPop()
    {
        await using var host = new KittyConsoleHost { FailPush = true };
        Hex1bApp? app = null;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(_ => { }, instance =>
            {
                app = instance;
                return context => context.Text("Kitty console");
            })
            .WithPresentation(host.Adapter).WithDimensions(30, 6).Build();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = terminal.RunAsync(cancellation.Token);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.AreEqual(0, host.Pushes);
        Assert.AreEqual(0, host.Pops, "A rejected push must not be followed by an unbalanced pop.");
        Assert.AreEqual(4, host.MainFlags);
        Assert.AreEqual(8, host.AlternateFlags);
    }

    private static void AssertBalanced(KittyConsoleHost host, int expectedPushes)
    {
        Assert.AreEqual(expectedPushes, host.Pushes);
        Assert.AreEqual(expectedPushes, host.Pops);
        Assert.IsTrue(host.PushesOnAlternate == expectedPushes && host.PopsOnAlternate == expectedPushes,
            "Push and pop must both target the alternate screen's stack.");
        Assert.AreEqual(4, host.MainFlags, "The main screen's stack must be untouched.");
        Assert.AreEqual(8, host.AlternateFlags, "The alternate screen's stack must be restored.");
    }

    private static async Task<Hex1bApp> RunAppAsync(
        KittyConsoleHost host, Func<Hex1bApp, Action, Task> body, bool stopByCancellation = false)
    {
        Hex1bApp? app = null;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(_ => { }, instance =>
            {
                app = instance;
                return context => context.Text("Kitty console");
            })
            .WithPresentation(host.Adapter).WithDimensions(30, 6).Build();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            await WaitForAsync(() => app is not null && host.Written.Contains("\x1b[?1049h", StringComparison.Ordinal),
                "app started");
            await body(app!, cancellation.Cancel);
            if (!stopByCancellation)
                app!.RequestStop();
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException) when (stopByCancellation)
            {
            }
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException)
            {
            }
        }
        return app!;
    }

    private static async Task WaitForAsync(Func<bool> condition, string description)
    {
        var deadline = Stopwatch.StartNew();
        while (!condition())
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(5))
                Assert.Fail($"Timed out waiting for {description}.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static string Escape(string text) => text.Replace("\x1b", "\\e");

    /// <summary>
    /// A scripted outer terminal behind the real <see cref="ConsolePresentationAdapter"/>.
    /// It tracks Kitty keyboard stacks per screen in a headless Hex1b terminal (main preset
    /// to 4, alternate to 8), answers the support probe and DA1 in a chosen order, and
    /// answers the post-push effective-flags query independently.
    /// </summary>
    private sealed class KittyConsoleHost : IConsoleDriver, IAsyncDisposable
    {
        private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
        private readonly StringBuilder _written = new();
        private readonly Hex1bTerminal _state = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().Build();
        private ConsolePresentationAdapter? _adapter;
        private bool _probeAnswered;

        public KittyConsoleHost()
        {
            _state.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=4u\x1b[?1049h\x1b[=8u\x1b[?1049l"));
        }

        public string? ProbeReply { get; init; } = "\x1b[?0u";
        public bool Da1First { get; init; }
        public bool Da1Conclusive { get; init; } = true;
        public int? EffectiveFlags { get; init; } = 31;
        public bool FailPush { get; init; }

        public ConsolePresentationAdapter Adapter => _adapter ??= new ConsolePresentationAdapter(
            this, kittyDa1Conclusive: Da1Conclusive);

        public int Pushes { get; private set; }
        public int Pops { get; private set; }
        public int PushesOnAlternate { get; private set; }
        public int PopsOnAlternate { get; private set; }
        public bool ReleaseReportsObserved { get; private set; }
        public int MainFlags
        {
            get { lock (_written) return _state.InAlternateScreen ? -1 : _state.InputModes.KittyKeyboardFlags; }
        }

        public int AlternateFlags { get; private set; } = 8;

        public string Written
        {
            get { lock (_written) return _written.ToString(); }
        }
        public void Write(ReadOnlySpan<byte> data)
        {
            var text = Encoding.UTF8.GetString(data);
            if (FailPush && text.Contains("\x1b[>31u", StringComparison.Ordinal))
                throw new IOException("push write rejected");
            lock (_written)
            {
                _written.Append(text);
                foreach (var token in AnsiTokenizer.Tokenize(text))
                {
                    var sequence = (token as UnrecognizedSequenceToken)?.Sequence;
                    var effectiveQuery = sequence == "\x1b[?u" && _probeAnswered;
                    _state.ApplyTokens([token]);
                    if (_state.InAlternateScreen)
                        AlternateFlags = _state.InputModes.KittyKeyboardFlags;
                    if (sequence == "\x1b[>31u")
                    {
                        Pushes++;
                        if (_state.InAlternateScreen) PushesOnAlternate++;
                    }
                    else if (sequence == "\x1b[<u")
                    {
                        Pops++;
                        if (_state.InAlternateScreen) PopsOnAlternate++;
                    }
                    else if (sequence == "\x1b[?u" && !effectiveQuery)
                    {
                        _probeAnswered = true;
                        if (!Da1First && ProbeReply is not null)
                            Enqueue(ProbeReply);
                    }
                    else if (effectiveQuery && EffectiveFlags is int flags)
                    {
                        ReleaseReportsObserved = (flags & 10) == 10;
                        Enqueue($"\x1b[?{flags}u");
                    }
                }

                if (text == "\x1b[c")
                {
                    Enqueue("\x1b[?61;6;7;21;22;23;24;28;32;42c");
                    if (Da1First && ProbeReply is not null)
                        Enqueue(ProbeReply);
                }
            }
        }

        private void Enqueue(string text) => _input.Writer.TryWrite(Encoding.ASCII.GetBytes(text));

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var chunk = await _input.Reader.ReadAsync(ct);
            chunk.CopyTo(buffer);
            return chunk.Length;
        }

        public Encoding InputEncoding => Encoding.UTF8;
        public bool DataAvailable => false;
        public int Width => 30;
        public int Height => 6;
        public event Action<int, int>? Resized { add { } remove { } }
        public void EnterRawMode(bool preserveOPost = false) { }
        public void ExitRawMode() { }
        public void Flush() { }
        public void DrainInput() { }
        public bool TryGetWindowPixelSize(out int pixelWidth, out int pixelHeight)
        {
            pixelWidth = pixelHeight = 0;
            return false;
        }

        public void Dispose() { }

        public async ValueTask DisposeAsync()
        {
            if (_adapter is not null)
                await _adapter.DisposeAsync();
            await _state.DisposeAsync();
        }
    }
}
