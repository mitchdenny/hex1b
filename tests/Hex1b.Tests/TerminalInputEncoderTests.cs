using System.Text;
using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class TerminalInputEncoderTests
{
    private const string ControlLetters =
        "\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u0009\u000a\u000b\u000c\u000d" +
        "\u000e\u000f\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001a";

    [TestMethod]
    [DataRow(Hex1bModifiers.None, "abcdefghijklmnopqrstuvwxyz", "")]
    [DataRow(Hex1bModifiers.Shift, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", "")]
    [DataRow(Hex1bModifiers.Alt, "abcdefghijklmnopqrstuvwxyz", "\u001b")]
    [DataRow(Hex1bModifiers.Alt | Hex1bModifiers.Shift, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", "\u001b")]
    [DataRow(Hex1bModifiers.Control, ControlLetters, "")]
    [DataRow(Hex1bModifiers.Control | Hex1bModifiers.Shift, ControlLetters, "")]
    [DataRow(Hex1bModifiers.Control | Hex1bModifiers.Alt, ControlLetters, "\u001b")]
    [DataRow(Hex1bModifiers.Control | Hex1bModifiers.Alt | Hex1bModifiers.Shift, ControlLetters, "\u001b")]
    public async Task KeyAsync_ProcessWorkload_AllLettersPreserveModifiers(
        Hex1bModifiers modifiers, string characters, string prefix)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));

        for (var index = 0; index < characters.Length; index++)
        {
            var key = Hex1bKey.A + index;
            foreach (var fluent in new[] { false, true })
            {
                if (fluent)
                {
                    if ((modifiers & Hex1bModifiers.Control) != 0) automator.Ctrl();
                    if ((modifiers & Hex1bModifiers.Alt) != 0) automator.Alt();
                    if ((modifiers & Hex1bModifiers.Shift) != 0) automator.Shift();
                    await automator.KeyAsync(key, TestContext.Current.CancellationToken);
                }
                else
                {
                    await automator.KeyAsync(key, modifiers, TestContext.Current.CancellationToken);
                }

                Assert.IsTrue(workload.Input.TryRead(out var bytes), $"Missing {modifiers}+{key} (fluent={fluent}).");
                TestSeq.AreEqual(Encoding.UTF8.GetBytes(prefix + characters[index]), bytes);

                await automator.KeyAsync(Hex1bKey.B, TestContext.Current.CancellationToken);
                Assert.IsTrue(workload.Input.TryRead(out var next));
                TestSeq.AreEqual(new byte[] { 0x62 }, next);
                Assert.IsFalse(workload.Input.TryRead(out _));
            }
        }
    }

    [TestMethod]
    [DataRow(Hex1bModifiers.Alt, "\u001be")]
    [DataRow(Hex1bModifiers.Alt | Hex1bModifiers.Shift, "\u001bE")]
    [DataRow(Hex1bModifiers.Control | Hex1bModifiers.Alt, "\u001b\u0005")]
    [DataRow(Hex1bModifiers.Control | Hex1bModifiers.Alt | Hex1bModifiers.Shift, "\u001b\u0005")]
    public async Task InputSequence_ProcessWorkload_AltLettersPreserveBytesAndResetModifiers(
        Hex1bModifiers modifiers, string expected)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        foreach (var fluent in new[] { false, true })
        {
            var builder = new Hex1bTerminalInputSequenceBuilder();
            if (fluent)
            {
                if ((modifiers & Hex1bModifiers.Control) != 0) builder.Ctrl();
                builder.Alt();
                if ((modifiers & Hex1bModifiers.Shift) != 0) builder.Shift();
                builder.Key(Hex1bKey.E);
            }
            else
            {
                builder.Key(Hex1bKey.E, modifiers);
            }

            using var snapshot = await builder.Key(Hex1bKey.B).Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);

            Assert.IsTrue(workload.Input.TryRead(out var bytes));
            TestSeq.AreEqual(Encoding.UTF8.GetBytes(expected), bytes);
            Assert.IsTrue(workload.Input.TryRead(out var next));
            TestSeq.AreEqual(new byte[] { 0x62 }, next);
            Assert.IsFalse(workload.Input.TryRead(out _));
        }
    }

    [TestMethod]
    [DataRow(Hex1bModifiers.Alt)]
    [DataRow(Hex1bModifiers.Alt | Hex1bModifiers.Shift)]
    [DataRow(Hex1bModifiers.Control | Hex1bModifiers.Alt)]
    [DataRow(Hex1bModifiers.Control | Hex1bModifiers.Alt | Hex1bModifiers.Shift)]
    public async Task KeyAsync_AppWorkload_AltLettersRemainShortcutEvents(Hex1bModifiers modifiers)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));

        await automator.KeyAsync(Hex1bKey.E, modifiers, TestContext.Current.CancellationToken);

        Assert.IsTrue(workload.InputEvents.TryRead(out var input));
        Assert.AreEqual(new Hex1bKeyEvent(Hex1bKey.E, "", modifiers), input);
    }

    [TestMethod]
    [DataRow(Hex1bKey.UpArrow, "", Hex1bModifiers.None, false, "\x1b[A")]
    [DataRow(Hex1bKey.UpArrow, "", Hex1bModifiers.None, true, "\x1bOA")]
    [DataRow(Hex1bKey.Home, "", Hex1bModifiers.None, false, "\x1b[H")]
    [DataRow(Hex1bKey.End, "", Hex1bModifiers.None, true, "\x1bOF")]
    [DataRow(Hex1bKey.LeftArrow, "", Hex1bModifiers.Control, true, "\x1b[1;5D")]
    [DataRow(Hex1bKey.Home, "", Hex1bModifiers.Shift, true, "\x1b[1;2H")]
    [DataRow(Hex1bKey.End, "", Hex1bModifiers.Alt, false, "\x1b[1;3F")]
    [DataRow(Hex1bKey.F1, "", Hex1bModifiers.Shift, false, "\x1b[1;2P")]
    [DataRow(Hex1bKey.F4, "", Hex1bModifiers.Control, false, "\x1b[1;5S")]
    [DataRow(Hex1bKey.F5, "", Hex1bModifiers.None, false, "\x1b[15~")]
    [DataRow(Hex1bKey.F12, "", Hex1bModifiers.Alt, false, "\x1b[24;3~")]
    [DataRow(Hex1bKey.Tab, "\t", Hex1bModifiers.Shift, false, "\x1b[Z")]
    [DataRow(Hex1bKey.Enter, "\n", Hex1bModifiers.Alt, false, "\x1b\r")]
    [DataRow(Hex1bKey.Backspace, "", Hex1bModifiers.None, false, "\x7f")]
    [DataRow(Hex1bKey.Backspace, "\b", Hex1bModifiers.None, false, "\b")]
    [DataRow(Hex1bKey.Backspace, "\x7f", Hex1bModifiers.None, false, "\x7f")]
    [DataRow(Hex1bKey.Backspace, "", Hex1bModifiers.Control, false, "\b")]
    [DataRow(Hex1bKey.Backspace, "\x7f", Hex1bModifiers.Alt | Hex1bModifiers.Control, false, "\x1b\b")]
    [DataRow(Hex1bKey.C, "", Hex1bModifiers.Control, false, "\x03")]
    [DataRow(Hex1bKey.E, "", Hex1bModifiers.Alt, false, "\u001be")]
    [DataRow(Hex1bKey.E, "", Hex1bModifiers.Alt | Hex1bModifiers.Shift, false, "\u001bE")]
    [DataRow(Hex1bKey.E, "", Hex1bModifiers.Control | Hex1bModifiers.Alt, false, "\u001b\u0005")]
    [DataRow(Hex1bKey.E, "", Hex1bModifiers.Control | Hex1bModifiers.Alt | Hex1bModifiers.Shift, false, "\u001b\u0005")]
    [DataRow(Hex1bKey.E, "E", Hex1bModifiers.Alt, false, "\u001bE")]
    [DataRow(Hex1bKey.E, "e", Hex1bModifiers.Alt | Hex1bModifiers.Shift, false, "\u001be")]
    [DataRow(Hex1bKey.E, "\u00e9", Hex1bModifiers.Alt, false, "\u001b\u00e9")]
    [DataRow(Hex1bKey.E, "ab", Hex1bModifiers.Alt, false, "\u001bab")]
    [DataRow(Hex1bKey.None, "e", Hex1bModifiers.Alt, false, "\u001be")]
    [DataRow(Hex1bKey.D1, "1", Hex1bModifiers.Alt, false, "\u001b1")]
    [DataRow(Hex1bKey.OemPeriod, ".", Hex1bModifiers.Alt, false, "\u001b.")]
    [DataRow(Hex1bKey.C, "c", Hex1bModifiers.Alt | Hex1bModifiers.Control, false, "\x1b\x03")]
    [DataRow(Hex1bKey.Spacebar, " ", Hex1bModifiers.Control, false, "\0")]
    [DataRow(Hex1bKey.Oem4, "[", Hex1bModifiers.Control, false, "\x1b")]
    [DataRow(Hex1bKey.None, "😀", Hex1bModifiers.None, false, "😀")]
    public async Task SendEventAsync_ProcessWorkload_UsesModeAwareKeyEncoding(
        Hex1bKey key, string text, Hex1bModifiers modifiers, bool applicationCursor, string expected)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(300, 100).Build();
        if (applicationCursor)
            terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1h"));

        await terminal.SendEventAsync(new Hex1bKeyEvent(key, text, modifiers), TestContext.Current.CancellationToken);

        Assert.IsTrue(workload.Input.TryRead(out var bytes), $"Missing {modifiers}+{key}.");
        TestSeq.AreEqual(Encoding.UTF8.GetBytes(expected), bytes);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SendEvent_ProcessWorkload_MouseUsesLiveModesAndRawLegacyBytes(bool asynchronous)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(300, 100).Build();
        var click = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 222, 95, Hex1bModifiers.None);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1000h"));

        if (asynchronous)
            await terminal.SendEventAsync(click, TestContext.Current.CancellationToken);
        else
            terminal.SendEvent(click);
        var legacy = await workload.Input.ReadAsync(TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        TestSeq.AreEqual(new byte[] { 27, 91, 77, 32, 255, 128 }, legacy);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1006h"));
        if (asynchronous)
            await terminal.SendEventAsync(click, TestContext.Current.CancellationToken);
        else
            terminal.SendEvent(click);
        var sgr = await workload.Input.ReadAsync(TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("\x1b[<0;223;96M", Encoding.ASCII.GetString(sgr));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1000l"));
        await terminal.SendEventAsync(click, TestContext.Current.CancellationToken);
        Assert.IsFalse(workload.Input.TryRead(out _));
    }

    [TestMethod]
    [DataRow(9, MouseAction.Down, MouseButton.Left, true)]
    [DataRow(9, MouseAction.Up, MouseButton.Left, false)]
    [DataRow(9, MouseAction.Down, MouseButton.ScrollUp, false)]
    [DataRow(1000, MouseAction.Move, MouseButton.Left, false)]
    [DataRow(1002, MouseAction.Drag, MouseButton.Left, true)]
    [DataRow(1002, MouseAction.Move, MouseButton.None, false)]
    [DataRow(1003, MouseAction.Move, MouseButton.None, true)]
    public async Task SendEventAsync_ProcessMouse_FiltersUnrequestedTrackingEvents(
        int mode, MouseAction action, MouseButton button, bool reported)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[?{mode};1006h"));

        await terminal.SendEventAsync(new Hex1bMouseEvent(button, action, 4, 2, Hex1bModifiers.None),
            TestContext.Current.CancellationToken);

        Assert.AreEqual(reported, workload.Input.TryRead(out _));
        terminal.Resize(2, 1);
        await terminal.SendEventAsync(new Hex1bMouseEvent(button, action, 4, 2, Hex1bModifiers.None),
            TestContext.Current.CancellationToken);
        Assert.IsFalse(workload.Input.TryRead(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SendEvent_AppWorkload_DispatchesOriginalEventsWithoutEncoding(bool asynchronous)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        Hex1bEvent[] events =
        [
            new Hex1bKeyEvent(Hex1bKey.Enter, "\n", Hex1bModifiers.Alt),
            new Hex1bKeyEvent(Hex1bKey.None, "😀\0", Hex1bModifiers.None),
            new Hex1bMouseEvent(MouseButton.None, MouseAction.Move, 500, 500, Hex1bModifiers.Shift),
            new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 0, 0, Hex1bModifiers.None, 3)
        ];

        foreach (var input in events)
        {
            if (asynchronous)
                await terminal.SendEventAsync(input, TestContext.Current.CancellationToken);
            else
                terminal.SendEvent(input);
            var received = await workload.InputEvents.ReadAsync(TestContext.Current.CancellationToken);
            Assert.AreSame(input, received);
        }
    }

    [TestMethod]
    [DataRow(Hex1bKey.UpArrow, Hex1bModifiers.None, "\u001bOA")]
    [DataRow(Hex1bKey.E, Hex1bModifiers.Alt, "\u001be")]
    [DataRow(Hex1bKey.E, Hex1bModifiers.Alt | Hex1bModifiers.Shift, "\u001bE")]
    [DataRow(Hex1bKey.E, Hex1bModifiers.Alt | Hex1bModifiers.Control, "\u001b\u0005")]
    public async Task SendEvent_ProcessKeys_UsesSameEncodingAsAsyncPath(
        Hex1bKey key, Hex1bModifiers modifiers, string expected)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1h"));
        terminal.SendEvent(new Hex1bKeyEvent(key, "", modifiers));
        var bytes = await workload.Input.ReadAsync(TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        TestSeq.AreEqual(Encoding.UTF8.GetBytes(expected), bytes);
    }

    [TestMethod]
    [DataRow(Hex1bKey.E, Hex1bModifiers.None)]
    [DataRow(Hex1bKey.E, Hex1bModifiers.Shift)]
    [DataRow(Hex1bKey.None, Hex1bModifiers.Alt)]
    [DataRow(Hex1bKey.D1, Hex1bModifiers.Alt)]
    public async Task SendEventAsync_EmptyNonAltLetter_DoesNotInventText(
        Hex1bKey key, Hex1bModifiers modifiers)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();

        await terminal.SendEventAsync(new Hex1bKeyEvent(key, "", modifiers), TestContext.Current.CancellationToken);

        Assert.IsFalse(workload.Input.TryRead(out _));
    }

    [TestMethod]
    public async Task InputSequence_ProcessWorkload_UsesSharedEncoderWithoutChangingTextOrClickCount()
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1;1000;1006h"));

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Alt().Key(Hex1bKey.C)
            .Shift().Tab()
            .Up()
            .Type("é")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await terminal.SendEventAsync(
            new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 0, 0, Hex1bModifiers.None, 3),
            TestContext.Current.CancellationToken);

        var bytes = new List<byte>();
        while (workload.Input.TryRead(out var input))
            bytes.AddRange(input);
        Assert.AreEqual("\x1b\x03\x1b[Z\x1bOAé\x1b[<0;1;1M", Encoding.UTF8.GetString(bytes.ToArray()));
    }

    [TestMethod]
    public async Task SendEventAsync_WorkloadFilter_PreservesEncodedKeysAndSgrMouseTokens()
    {
        var workload = new RecordingWorkload();
        var filter = new InputRecordingFilter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().AddWorkloadFilter(filter).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1000;1006h"));
        Hex1bEvent[] events =
        [
            new Hex1bKeyEvent(Hex1bKey.LeftArrow, "", Hex1bModifiers.Control),
            new Hex1bKeyEvent(Hex1bKey.F1, "", Hex1bModifiers.Shift),
            new Hex1bKeyEvent(Hex1bKey.Tab, "\t", Hex1bModifiers.Shift | Hex1bModifiers.Alt),
            new Hex1bKeyEvent(Hex1bKey.C, "c", Hex1bModifiers.Alt),
            new Hex1bKeyEvent(Hex1bKey.E, "", Hex1bModifiers.Alt),
            new Hex1bKeyEvent(Hex1bKey.E, "", Hex1bModifiers.Alt | Hex1bModifiers.Shift),
            Hex1bKeyEvent.FromText("\x1b[200~literal\x1b[201~"),
            new Hex1bMouseEvent(MouseButton.Right, MouseAction.Up, 4, 2, Hex1bModifiers.Control)
        ];

        foreach (var input in events)
        {
            await terminal.SendEventAsync(input, TestContext.Current.CancellationToken);
            var bytes = await workload.Input.ReadAsync(TestContext.Current.CancellationToken);
            Assert.AreEqual(Encoding.UTF8.GetString(bytes), AnsiTokenSerializer.Serialize(filter.LastInput));
        }
        TestSeq.IsType<SgrMouseToken>(TestSeq.Single(filter.LastInput));
    }

    private sealed class InputRecordingFilter : IHex1bTerminalWorkloadFilter
    {
        internal IReadOnlyList<AnsiToken> LastInput { get; private set; } = [];

        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            LastInput = tokens;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    private sealed class RecordingWorkload : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
        internal ChannelReader<byte[]> Input => _input.Reader;

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
