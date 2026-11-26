using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// HWT1 browser key commands encoded against the producer's Kitty keyboard mode. Expected bytes are
/// written from the public Kitty keyboard protocol specification, not from another implementation.
/// </summary>
[TestClass]
public class Hwt1KittyKeyboardTests
{
    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(false, 5)]
    [DataRow(false, 127)]
    [DataRow(true, 0)]
    [DataRow(true, 127)]
    public async Task Metadata_KittyFlags_CarriesAllLowSevenBits(bool muxed, int flags)
    {
        await using var harness = await Harness.CreateAsync(muxed);
        if (flags != 0)
            harness.Apply($"\x1b[>{flags}u");

        var metadata = await harness.FrameAsync();

        Assert.AreEqual(flags, metadata.GetProperty("kittyKeyboardFlags").GetInt32());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Key_LegacyMode_KittyFieldsPreserveLegacyBytesAndDropReleases(bool muxed)
    {
        await using var harness = await Harness.CreateAsync(muxed);

        await harness.KeyAsync("Escape", "Escape");
        await harness.KeyAsync("a", "KeyA", ctrl: true);
        await harness.KeyAsync("a", "KeyA", "release");
        await harness.KeyAsync("F13", "F13");
        await harness.KeyAsync("Shift", "ShiftLeft", shift: true);
        await harness.KeyAsync("1", "Numpad1", numLock: true);
        await harness.KeyAsync("ArrowUp", "ArrowUp", "repeat");

        await harness.ExpectAsync("\x1b\x01" + "1\x1b[A");
        await harness.AssertNoMoreInputAsync();
    }

    [TestMethod]
    [DataRow("""{"type":"key","key":"Bogus"}""")]
    [DataRow("""{"type":"key","key":"Clear"}""")]
    public async Task Key_LegacyMode_UnknownKeysStillRejected(string json)
    {
        await using var harness = await Harness.CreateAsync(muxed: false);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            harness.View.HandleMessageAsync(Encoding.UTF8.GetBytes(json), TestContext.Current.CancellationToken));
        await harness.AssertNoMoreInputAsync();
    }

    [TestMethod]
    [DataRow(false, 32)]
    [DataRow(false, 64)]
    [DataRow(true, 96)]
    public async Task Key_ReservedOnlyMode_BehavesLikeLegacy(bool muxed, int flags)
    {
        await using var harness = await Harness.CreateAsync(muxed);
        harness.Apply($"\x1b[>{flags}u");

        await harness.KeyAsync("Escape", "Escape");
        await harness.KeyAsync("Escape", "Escape", "release");
        await harness.KeyAsync("F20", "F20");

        await harness.ExpectAsync("\x1b");
        await harness.AssertNoMoreInputAsync();
        Assert.AreEqual(0, harness.View.HeldKeyCount);
    }

    [TestMethod]
    [DataRow("Escape", "Escape", false, false, false, "\x1b[27u")]
    [DataRow("a", "KeyA", true, false, false, "\x1b[97;5u")]
    [DataRow("a", "KeyA", false, true, false, "\x1b[97;3u")]
    [DataRow("a", "KeyA", false, false, false, "a")]
    [DataRow("a", "KeyA", false, false, true, "a")]
    [DataRow("Enter", "Enter", false, false, false, "\r")]
    [DataRow("1", "Numpad1", false, false, false, "\x1b[57400u")]
    [DataRow("Enter", "NumpadEnter", false, false, false, "\x1b[57414u")]
    [DataRow("ArrowLeft", "Numpad4", false, false, false, "\x1b[57417u")]
    [DataRow("Clear", "Numpad5", false, false, false, "\x1b[57427~")]
    [DataRow("F13", "F13", false, false, false, "\x1b[57376u")]
    [DataRow("F35", "F35", false, false, false, "\x1b[57398u")]
    [DataRow("ContextMenu", "ContextMenu", false, false, false, "\x1b[57363u")]
    [DataRow("Pause", "Pause", false, false, false, "\x1b[57362u")]
    public async Task Key_Disambiguate_EncodesSpecifiedBytes(
        string key, string code, bool ctrl, bool alt, bool capsLock, string expected)
    {
        await using var harness = await Harness.CreateAsync(muxed: false);
        harness.Apply("\x1b[>1u");

        await harness.KeyAsync(key, code, ctrl: ctrl, alt: alt, capsLock: capsLock);

        await harness.ExpectAsync(expected);
        await harness.AssertNoMoreInputAsync();
    }

    [TestMethod]
    [DataRow("a", "KeyA", "", "\x1b[97u")]
    [DataRow("a", "KeyA", "ctrl", "\x1b[97;5u")]
    [DataRow("Enter", "Enter", "", "\x1b[13u")]
    [DataRow("Shift", "ShiftLeft", "shift", "\x1b[57441;2u")]
    [DataRow("Shift", "ShiftRight", "shift", "\x1b[57447;2u")]
    [DataRow("Control", "ControlRight", "ctrl", "\x1b[57448;5u")]
    [DataRow("Alt", "AltLeft", "alt", "\x1b[57443;3u")]
    [DataRow("Meta", "MetaLeft", "meta", "\x1b[57444;9u")]
    [DataRow("OS", "OSRight", "meta", "\x1b[57450;9u")]
    [DataRow("CapsLock", "CapsLock", "capsLock", "\x1b[57358;65u")]
    [DataRow("NumLock", "NumLock", "numLock", "\x1b[57360;129u")]
    [DataRow("ScrollLock", "ScrollLock", "", "\x1b[57359u")]
    [DataRow("PrintScreen", "PrintScreen", "", "\x1b[57361u")]
    [DataRow("1", "Numpad1", "numLock", "\x1b[57400;129u")]
    [DataRow("a", "KeyA", "capsLock", "\x1b[97;65u")]
    [DataRow("A", "KeyA", "capsLock", "\x1b[0;65u")]
    public async Task Key_ReportAll_EncodesModifierLockAndExtendedIdentities(
        string key, string code, string modifier, string expected)
    {
        await using var harness = await Harness.CreateAsync(muxed: false);
        harness.Apply("\x1b[>8u");

        await harness.KeyAsync(key, code, ctrl: modifier == "ctrl", alt: modifier == "alt",
            shift: modifier == "shift", meta: modifier == "meta",
            capsLock: modifier == "capsLock", numLock: modifier == "numLock");

        await harness.ExpectAsync(expected);
        await harness.AssertNoMoreInputAsync();
    }

    [TestMethod]
    public async Task Key_AlternateKeys_ReportsPhysicalBaseLayoutKey()
    {
        await using var harness = await Harness.CreateAsync(muxed: false);
        harness.Apply("\x1b[>12u");

        // AZERTY: the physical KeyA position produces "q".
        await harness.KeyAsync("q", "KeyA");

        await harness.ExpectAsync("\x1b[113::97u");
    }

    [TestMethod]
    [DataRow("Shift", "")]
    [DataRow("Control", "")]
    [DataRow("Meta", "")]
    [DataRow("Shift", "ControlLeft")]
    public async Task Key_ModifierWithoutKnownSide_SendsNoFabricatedIdentity(string key, string code)
    {
        await using var harness = await Harness.CreateAsync(muxed: false);
        harness.Apply("\x1b[>31u");

        await harness.KeyAsync(key, code, shift: key == "Shift");
        await harness.KeyAsync(key, code, "release");

        await harness.AssertNoMoreInputAsync();
        Assert.AreEqual(0, harness.View.HeldKeyCount);
    }

    [TestMethod]
    [DataRow(8, "\x1b[0;2u")]
    [DataRow(24, "\x1b[0;2;65u")]
    public async Task Key_ShiftedLetter_DoesNotInferUnshiftedLayoutKey(int flags, string expected)
    {
        await using var harness = await Harness.CreateAsync(muxed: false);
        harness.Apply($"\x1b[>{flags}u");

        await harness.KeyAsync("A", "KeyA", shift: true);

        await harness.ExpectAsync(expected);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Key_EventTypes_PressRepeatReleaseAndCleanup(bool muxed)
    {
        await using var harness = await Harness.CreateAsync(muxed);
        harness.Apply("\x1b[>10u");

        await harness.KeyAsync("a", "KeyA");
        await harness.KeyAsync("a", "KeyA", "repeat");
        // The release names the press identity even after Shift changed the produced character.
        await harness.KeyAsync("A", "KeyA", "release", shift: true);
        await harness.KeyAsync("a", "KeyA", "release");

        await harness.ExpectAsync("\x1b[97u\x1b[97;1:2u\x1b[97;2:3u");
        await harness.AssertNoMoreInputAsync();
        Assert.AreEqual(0, harness.View.HeldKeyCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Key_OrphanRepeatAndRelease_RepeatBecomesPressReleaseIsDropped(bool muxed)
    {
        await using var harness = await Harness.CreateAsync(muxed);
        harness.Apply("\x1b[>10u");

        await harness.KeyAsync("c", "KeyC", "release");
        await harness.KeyAsync("b", "KeyB", "repeat");
        await harness.KeyAsync("b", "KeyB", "release");

        await harness.ExpectAsync("\x1b[98u\x1b[98;1:3u");
        await harness.AssertNoMoreInputAsync();
    }

    [TestMethod]
    public async Task Key_EventTypesWithoutReportAll_KeepsTextAndShellRecoveryKeys()
    {
        await using var harness = await Harness.CreateAsync(muxed: false);
        harness.Apply("\x1b[>3u");

        await harness.KeyAsync("a", "KeyA");
        await harness.KeyAsync("a", "KeyA", "release");
        await harness.KeyAsync("Enter", "Enter");
        await harness.KeyAsync("Enter", "Enter", "release");
        await harness.KeyAsync("A", "KeyA", shift: true);

        await harness.ExpectAsync("a\x1b[97;1:3u\rA");
        await harness.AssertNoMoreInputAsync();
    }

    [TestMethod]
    public async Task Key_EventTypeModeDropped_ForgetsHeldPresses()
    {
        await using var harness = await Harness.CreateAsync(muxed: false);
        harness.Apply("\x1b[>10u");
        await harness.KeyAsync("a", "KeyA");
        await harness.ExpectAsync("\x1b[97u");

        harness.Apply("\x1b[<u");
        await harness.KeyAsync("b", "KeyB");
        harness.Apply("\x1b[>10u");
        await harness.KeyAsync("a", "KeyA", "release");

        await harness.ExpectAsync("b");
        await harness.AssertNoMoreInputAsync();
        Assert.AreEqual(0, harness.View.HeldKeyCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task IsReadOnly_HeldKeys_ReleasedWithLockStateOnly(bool muxed)
    {
        await using var harness = await Harness.CreateAsync(muxed);
        harness.Apply("\x1b[>10u");
        await harness.KeyAsync("a", "KeyA", ctrl: true, capsLock: true);
        await harness.KeyAsync("1", "Numpad1");
        await harness.ExpectAsync("\x1b[97;69u\x1b[57400u");

        harness.View.IsReadOnly = true;

        await harness.ExpectAsync("\x1b[97;65:3u\x1b[57400;1:3u");
        await harness.KeyAsync("1", "Numpad1", "release");
        harness.View.IsReadOnly = false;
        await harness.AssertNoMoreInputAsync();
        Assert.AreEqual(0, harness.View.HeldKeyCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DisposeAsync_HeldKeys_ReleasedBeforeDisconnect(bool muxed)
    {
        await using var harness = await Harness.CreateAsync(muxed);
        harness.Apply("\x1b[>10u");
        await harness.KeyAsync("Shift", "ShiftLeft", shift: true);
        await harness.KeyAsync("x", "KeyX");
        await harness.ExpectAsync("\x1b[57441;2u\x1b[120u");

        await harness.View.DisposeAsync();

        await harness.ExpectAsync("\x1b[57441;1:3u\x1b[120;1:3u");
    }

    [TestMethod]
    public async Task Key_ReleaseWriteFails_RetainsPressForRetry()
    {
        await using var harness = await Harness.CreateAsync(muxed: false);
        harness.Apply("\x1b[>10u");
        await harness.KeyAsync("a", "KeyA");
        await harness.ExpectAsync("\x1b[97u");

        harness.Workload.FailWrites = true;
        await Assert.ThrowsExactlyAsync<IOException>(() => harness.KeyAsync("a", "KeyA", "release"));
        Assert.AreEqual(1, harness.View.HeldKeyCount);

        harness.Workload.FailWrites = false;
        await harness.KeyAsync("a", "KeyA", "release");
        await harness.ExpectAsync("\x1b[97;1:3u");
        Assert.AreEqual(0, harness.View.HeldKeyCount);
    }

    [TestMethod]
    public async Task Key_PressWriteFails_DoesNotTrackUndeliveredPress()
    {
        await using var harness = await Harness.CreateAsync(muxed: false);
        harness.Apply("\x1b[>10u");

        harness.Workload.FailWrites = true;
        await Assert.ThrowsExactlyAsync<IOException>(() => harness.KeyAsync("a", "KeyA"));
        harness.Workload.FailWrites = false;
        await harness.KeyAsync("a", "KeyA", "release");

        await harness.AssertNoMoreInputAsync();
        Assert.AreEqual(0, harness.View.HeldKeyCount);
    }

    [TestMethod]
    [DataRow(false, 0, "û", "û")]
    [DataRow(false, 1, "û", "û")]
    [DataRow(false, 8, "û", "\x1b[0u")]
    [DataRow(false, 24, "û", "\x1b[0;;251u")]
    [DataRow(true, 24, "🙂", "\x1b[0;;128578u")]
    [DataRow(false, 24, "ab", "\x1b[0;;97:98u")]
    [DataRow(false, 24, "\r", "\r")]
    public async Task Input_ReportAll_SendsTextOnlyEvents(bool muxed, int flags, string text, string expected)
    {
        await using var harness = await Harness.CreateAsync(muxed);
        if (flags != 0)
            harness.Apply($"\x1b[>{flags}u");

        await harness.MessageAsync(new { type = "input", text });

        await harness.ExpectAsync(expected);
        await harness.AssertNoMoreInputAsync();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Paste_ReportAll_RemainsBracketedText(bool muxed)
    {
        await using var harness = await Harness.CreateAsync(muxed);
        harness.Apply("\x1b[?2004h\x1b[>31u");

        await harness.MessageAsync(new { type = "paste", text = "é x" });

        await harness.ExpectAsync("\x1b[200~é x\x1b[201~");
    }

    [TestMethod]
    [DataRow("""{"type":"key","key":"a","eventType":"down"}""")]
    [DataRow("""{"type":"key","key":"a","code":1}""")]
    [DataRow("""{"type":"key","key":"a","capsLock":"yes"}""")]
    [DataRow("""{"type":"key","key":"Clear","code":"Clear"}""")]
    public async Task Key_InvalidKittyFields_AreRejected(string json)
    {
        await using var harness = await Harness.CreateAsync(muxed: false);
        harness.Apply("\x1b[>31u");

        await Assert.ThrowsAsync<Exception>(() =>
            harness.View.HandleMessageAsync(Encoding.UTF8.GetBytes(json), TestContext.Current.CancellationToken));
        await harness.AssertNoMoreInputAsync();
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly Hmp1PresentationAdapter? _muxer;

        private Harness(RecordingWorkload workload, Hex1bTerminal terminal, Hwt1PresentationAdapter view,
            Hmp1PresentationAdapter? muxer)
        {
            Workload = workload;
            Terminal = terminal;
            View = view;
            _muxer = muxer;
        }

        public RecordingWorkload Workload { get; }
        public Hex1bTerminal Terminal { get; }
        public Hwt1PresentationAdapter View { get; }

        public static async Task<Harness> CreateAsync(bool muxed)
        {
            var workload = new RecordingWorkload();
            if (!muxed)
            {
                var direct = new Hwt1PresentationAdapter(20, 10);
                var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
                    .WithPresentation(direct).WithDimensions(20, 10).Build();
                return new(workload, terminal, direct, null);
            }
            var muxer = new Hmp1PresentationAdapter(20, 10);
            var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
                .WithPresentation(muxer).WithDimensions(20, 10).Build();
            var view = await muxer.CreateBrowserViewAsync("kitty", TestContext.Current.CancellationToken);
            return new(workload, producer, view, muxer);
        }

        public void Apply(string sequence) => Terminal.ApplyTokens(AnsiTokenizer.Tokenize(sequence));

        public Task MessageAsync(object message)
            => View.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(message), TestContext.Current.CancellationToken);

        public Task KeyAsync(string key, string code, string eventType = "press", bool ctrl = false,
            bool alt = false, bool shift = false, bool meta = false, bool capsLock = false, bool numLock = false)
            => MessageAsync(new { type = "key", key, code, eventType, ctrl, alt, shift, meta, capsLock, numLock });

        public async Task<JsonElement> FrameAsync()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var frame = await View.ReadFrameAsync(timeout.Token);
            using var document = JsonDocument.Parse(frame.Slice(8, BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..])));
            return document.RootElement.Clone();
        }

        public async Task<string> ReadAsync(int length)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var text = new StringBuilder();
            while (text.Length < length)
                text.Append(Encoding.UTF8.GetString(await Workload.Input.Reader.ReadAsync(timeout.Token)));
            return text.ToString();
        }

        public async Task ExpectAsync(string expected) => Assert.AreEqual(expected, await ReadAsync(expected.Length));

        /// <summary>Proves no input preceded a sentinel written through the same producer path.</summary>
        public async Task AssertNoMoreInputAsync()
        {
            await Terminal.SendInputAsync("|"u8.ToArray(), TestContext.Current.CancellationToken);
            Assert.AreEqual("|", await ReadAsync(1));
        }

        public async ValueTask DisposeAsync()
        {
            await View.DisposeAsync();
            if (_muxer is not null)
                await _muxer.DisposeAsync();
            await Terminal.DisposeAsync();
        }
    }

    private sealed class RecordingWorkload : IHex1bTerminalWorkloadAdapter
    {
        public Channel<byte[]> Output { get; } = Channel.CreateUnbounded<byte[]>();
        public Channel<byte[]> Input { get; } = Channel.CreateUnbounded<byte[]>();
        public volatile bool FailWrites;
        public event Action? Disconnected;

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => await Output.Reader.ReadAsync(ct);

        public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (FailWrites)
                throw new IOException("Simulated workload write failure.");
            await Input.Writer.WriteAsync(data.ToArray(), ct);
        }

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Output.Writer.TryComplete();
            Input.Writer.TryComplete();
            Disconnected?.Invoke();
            return ValueTask.CompletedTask;
        }
    }
}
