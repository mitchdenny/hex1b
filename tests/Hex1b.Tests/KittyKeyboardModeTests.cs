using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Tests.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class KittyKeyboardModeTests
{
    [TestMethod]
    [DataRow("", "\x1b[?0u")]
    [DataRow("\x1b[=9u", "\x1b[?9u")]
    [DataRow("\x1b[=31u\x1b[=9;1u", "\x1b[?9u")]
    [DataRow("\x1b[=9u\x1b[=6;2u", "\x1b[?15u")]
    [DataRow("\x1b[=15u\x1b[=6;3u", "\x1b[?9u")]
    [DataRow("\x1b[=31u\x1b[=u", "\x1b[?0u")]
    [DataRow("\x1b[=31u\x1b[=;2u", "\x1b[?31u")]
    [DataRow("\x1b[=31u\x1b[=;3u", "\x1b[?31u")]
    [DataRow("\x1b[=31u\x1b[=9;u", "\x1b[?9u")]
    [DataRow("\x1b[>31u\x1b[>u", "\x1b[?0u")]
    [DataRow("\x1b[>1u\x1b[>2u\x1b[<u", "\x1b[?1u")]
    [DataRow("\x1b[>1u\x1b[>2u\x1b[<0u", "\x1b[?2u")]
    [DataRow("\x1b[>1u\x1b[>2u\x1b[<2u", "\x1b[?0u")]
    [DataRow("\x1b[>1u\x1b[>2u\x1b[<2147483647u", "\x1b[?0u")]
    [DataRow("\x1b[<u\x1b[<9u", "\x1b[?0u")]
    public async Task TerminalModes_Commands_ReplyWithLiteralCurrentFlags(string commands, string expected)
        => await AssertQueryReplyAsync(commands, expected);

    [TestMethod]
    [DataRow("\x1b[=33u\x1b[=96;1u", "\x1b[?96u")]
    [DataRow("\x1b[=1u\x1b[=32;2u", "\x1b[?33u")]
    [DataRow("\x1b[=97u\x1b[=32;3u", "\x1b[?65u")]
    [DataRow("\x1b[=97u\x1b[=1;3u", "\x1b[?96u")]
    [DataRow("\x1b[>33u\x1b[>64u\x1b[<u", "\x1b[?33u")]
    [DataRow("\x1b[>96u\x1b[=1;2u\x1b[>2u\x1b[<u", "\x1b[?97u")]
    public async Task TerminalModes_ReservedBits_ArePreservedThroughSetPushPopAndQuery(string commands, string expected)
        => await AssertQueryReplyAsync(commands, expected);

    [TestMethod]
    [DataRow("\x1b[=127u", "\x1b[?127u")]
    [DataRow("\x1b[>63u", "\x1b[?63u")]
    [DataRow("\x1b[=128u", "\x1b[?0u")]
    [DataRow("\x1b[=161u", "\x1b[?33u")]
    [DataRow("\x1b[>2147483647u", "\x1b[?127u")]
    [DataRow("\x1b[=1u\x1b[=2147483647;3u", "\x1b[?0u")]
    [DataRow("\x1b[=1u\x1b[=2147483648u", "\x1b[?1u")]
    [DataRow("\x1b[>1u\x1b[<2147483648u", "\x1b[?1u")]
    public async Task TerminalModes_NumericBounds_RetainLowSevenBitsAndRejectInt32Overflow(string commands, string expected)
        => await AssertQueryReplyAsync(commands, expected);

    private static async Task AssertQueryReplyAsync(string commands, string expected)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(commands + "\x1b[?u"));

        var reply = await workload.Input.ReadAsync(TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        TestSeq.AreEqual(Encoding.ASCII.GetBytes(expected), reply);
        Assert.IsFalse(workload.Input.TryRead(out _));
    }

    [TestMethod]
    public void TerminalModes_CommandsAndScreens_TrackIndependentStacks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=1u\x1b[>3u\x1b[=4;2u"));
        Assert.AreEqual(7, terminal.InputModes.KittyKeyboardFlags);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h\x1b[>8u"));
        Assert.AreEqual(8, terminal.InputModes.KittyKeyboardFlags);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l\x1b[<u"));
        Assert.AreEqual(1, terminal.InputModes.KittyKeyboardFlags);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[<u"));
        Assert.AreEqual(0, terminal.InputModes.KittyKeyboardFlags);
    }

    [TestMethod]
    public void KeyboardMode_OverflowAndInvalidCommands_PreserveBoundedState()
    {
        var mode = new KittyKeyboardMode();
        for (var i = 0; i < 130; i++)
            Assert.IsTrue(mode.Apply($"\x1b[>{i % 2 + 1}u", out _));
        Assert.IsFalse(mode.Apply("\x1b[=1;4u", out _));
        Assert.AreEqual(2, mode.Flags);
        Assert.IsTrue(mode.Apply("\x1b[<127u", out _));
        Assert.AreEqual(1, mode.Flags);
        Assert.IsTrue(mode.Apply("\x1b[<u", out _));
        Assert.AreEqual(0, mode.Flags);
    }

    [TestMethod]
    public async Task TerminalModes_Overflow_EvictsOldestOf128Entries()
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>1u\x1b[>2u"));
        for (var i = 0; i < 127; i++)
            terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>4u"));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[<127u\x1b[?u"));
        var retained = await workload.Input.ReadAsync(TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        TestSeq.AreEqual(Encoding.ASCII.GetBytes("\x1b[?2u"), retained);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[<u\x1b[?u"));
        var empty = await workload.Input.ReadAsync(TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        TestSeq.AreEqual(Encoding.ASCII.GetBytes("\x1b[?0u"), empty);
    }

    [TestMethod]
    [DataRow(47)]
    [DataRow(1047)]
    [DataRow(1049)]
    public async Task TerminalModes_ScreenSwitches_RetainIndependentStacksAndSnapshotFlags(int screenMode)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>1u\x1b[>3u"));
        using var mainSnapshot = terminal.CreateSnapshot();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[?{screenMode}h\x1b[?u"));
        var initial = await workload.Input.ReadAsync(TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        TestSeq.AreEqual(Encoding.ASCII.GetBytes("\x1b[?0u"), initial);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>8u\x1b[>24u"));
        using var alternateSnapshot = terminal.CreateSnapshot();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[?{screenMode}l\x1b[<u"));
        Assert.AreEqual(1, terminal.InputModes.KittyKeyboardFlags);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[?{screenMode}h\x1b[<u"));
        Assert.AreEqual(8, terminal.InputModes.KittyKeyboardFlags);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[?{screenMode}l"));
        Assert.AreEqual(1, terminal.InputModes.KittyKeyboardFlags);
        Assert.AreEqual(3, mainSnapshot.KittyKeyboardFlags);
        Assert.AreEqual(24, alternateSnapshot.KittyKeyboardFlags);
    }

    [TestMethod]
    public void KeyboardMode_Reset_ClearsAllEntries()
    {
        var mode = new KittyKeyboardMode();
        mode.Apply("\x1b[>1u", out _);
        mode.Apply("\x1b[>3u", out _);

        mode.Reset();
        Assert.AreEqual(0, mode.Flags);
        mode.Apply("\x1b[>8u", out _);
        mode.Apply("\x1b[<u", out _);
        Assert.AreEqual(0, mode.Flags);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TerminalModes_Ris_ClearsBothScreenStacks(bool resetFromAlternate)
    {
        await using var fixture = SixelTestTerminal.Create();
        var ct = TestContext.Current.CancellationToken;
        await fixture.FeedAsync(Encoding.ASCII.GetBytes("\x1b[>1u\x1b[?1049h\x1b[>3u"), cancellationToken: ct);
        await fixture.WaitForAsync(s => s.InAlternateScreen && s.KittyKeyboardFlags == 3, "flags before RIS", ct);
        if (!resetFromAlternate)
            fixture.Terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));

        // ESC c is not decoded by the raw workload path yet (#453).
        await fixture.FeedPreTokenizedAsync(Encoding.ASCII.GetBytes("\x1b" + "c"), [RisToken.Instance], ct);
        await fixture.WaitForAsync(s => !s.InAlternateScreen && s.KittyKeyboardFlags == 0, "main flags after RIS", ct);
        fixture.Terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h"));
        Assert.AreEqual(0, fixture.Terminal.InputModes.KittyKeyboardFlags);
        fixture.Terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>8u\x1b[<u\x1b[?1049l"));
        Assert.AreEqual(0, fixture.Terminal.InputModes.KittyKeyboardFlags);
    }

    [TestMethod]
    public async Task TerminalModes_WorkloadPushThenKey_EmitsLiteralWireBytes()
    {
        await using var fixture = SixelTestTerminal.Create();
        var ct = TestContext.Current.CancellationToken;
        await fixture.FeedAsync(Encoding.ASCII.GetBytes("\x1b[>1u"), cancellationToken: ct);
        await fixture.WaitForAsync(s => s.KittyKeyboardFlags == 1, "workload enabled disambiguation", ct);

        await fixture.Terminal.SendEventAsync(new Hex1bKeyEvent(Hex1bKey.C, "", Hex1bModifiers.Control), ct);

        Assert.AreEqual("\x1b[99;5u", fixture.WorkloadInput);
    }

    [TestMethod]
    public void TerminalModes_ReservedBits_AreExposedInInputModesAndSnapshots()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>65u\x1b[?1049h\x1b[>96u"));
        using var alternate = terminal.CreateSnapshot();
        Assert.AreEqual(96, terminal.InputModes.KittyKeyboardFlags);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));
        using var main = terminal.CreateSnapshot();

        Assert.AreEqual(65, terminal.InputModes.KittyKeyboardFlags);
        Assert.AreEqual(96, alternate.KittyKeyboardFlags);
        Assert.AreEqual(65, main.KittyKeyboardFlags);
    }

    [TestMethod]
    [DataRow("\x1b[>32u", "\x03")]
    [DataRow("\x1b[>33u", "\x1b[99;5u")]
    public async Task TerminalModes_ReservedBits_DoNotChangeKnownFlagEncoding(string push, string expected)
    {
        await using var fixture = SixelTestTerminal.Create();
        var ct = TestContext.Current.CancellationToken;
        await fixture.FeedAsync(Encoding.ASCII.GetBytes(push), cancellationToken: ct);
        await fixture.WaitForAsync(s => s.KittyKeyboardFlags != 0, "workload pushed flags", ct);

        await fixture.Terminal.SendEventAsync(new Hex1bKeyEvent(Hex1bKey.C, "", Hex1bModifiers.Control), ct);

        Assert.AreEqual(expected, fixture.WorkloadInput);
    }

    [TestMethod]
    public void TerminalModes_SoftReset_PreservesBothStacks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>1u\x1b[?1049h\x1b[>8u\x1b[!p"));
        Assert.AreEqual(8, terminal.InputModes.KittyKeyboardFlags);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l\x1b[!p"));
        Assert.AreEqual(1, terminal.InputModes.KittyKeyboardFlags);
    }

    [TestMethod]
    [DataRow("\x1b[=2;0u")]
    [DataRow("\x1b[=2;4u")]
    [DataRow("\x1b[=2;1;3u")]
    [DataRow("\x1b[>2;3u")]
    [DataRow("\x1b[<2;3u")]
    [DataRow("\x1b[=2147483648u")]
    [DataRow("\x1b[=1:2u")]
    [DataRow("\x1b[97u")]
    [DataRow("\x1b[u")]
    public void KeyboardMode_InvalidCommands_DoNotMutateState(string command)
    {
        var mode = new KittyKeyboardMode();
        mode.Apply("\x1b[>9u", out _);

        Assert.IsFalse(mode.Apply(command, out var query));
        Assert.IsFalse(query);
        Assert.AreEqual(9, mode.Flags);
        mode.Apply("\x1b[<u", out _);
        Assert.AreEqual(0, mode.Flags);
    }

    [TestMethod]
    public async Task TerminalModes_UpstreamQueryOwner_DoesNotReceiveSyntheticReply()
    {
        var workload = new RecordingWorkload { HandlesProtocolQueries = true };
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>3u\x1b[?u"));
        Assert.AreEqual(3, terminal.InputModes.KittyKeyboardFlags);
        Assert.IsFalse(workload.Input.TryRead(out _));
    }

    private sealed class RecordingWorkload : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
        internal ChannelReader<byte[]> Input => _input.Reader;
        public bool HandlesProtocolQueries { get; init; }
        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => _input.Writer.WriteAsync(data.ToArray(), ct);
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public event Action? Disconnected { add { } remove { } }
        public ValueTask DisposeAsync()
        {
            _input.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
