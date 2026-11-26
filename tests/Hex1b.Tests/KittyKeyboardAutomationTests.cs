using System.Text;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class KittyKeyboardAutomationTests
{
    // Independent wire expectations from the public Kitty specification. No encoder oracle.
    // Reference intents: Ghostty key_encode.zig (events, alternates, associated text),
    // Windows Terminal kittyKeyboardProtocol.cpp (repeat and US base identity),
    // crossterm parse.rs and Neovim tui_spec.lua (event types, modifiers, Escape, keypad).
    [TestMethod]
    [DataRow(0, Hex1bKeyEventType.Press, "x")]
    [DataRow(0, Hex1bKeyEventType.Repeat, "x")]
    [DataRow(0, Hex1bKeyEventType.Release, "")]
    [DataRow(1, Hex1bKeyEventType.Repeat, "x")]
    [DataRow(1, Hex1bKeyEventType.Release, "")]
    [DataRow(3, Hex1bKeyEventType.Press, "x")]
    [DataRow(3, Hex1bKeyEventType.Repeat, "x")]
    [DataRow(3, Hex1bKeyEventType.Release, "\x1b[120;1:3u")]
    [DataRow(8, Hex1bKeyEventType.Repeat, "\x1b[120u")]
    [DataRow(8, Hex1bKeyEventType.Release, "")]
    [DataRow(11, Hex1bKeyEventType.Press, "\x1b[120u")]
    [DataRow(11, Hex1bKeyEventType.Repeat, "\x1b[120;1:2u")]
    [DataRow(11, Hex1bKeyEventType.Release, "\x1b[120;1:3u")]
    public async Task KeyEvent_BothAutomationPaths_RespectEventFlags(
        int flags, Hex1bKeyEventType type, string expected)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[={flags}u"));
        var input = new Hex1bKeyEvent(Hex1bKey.X, "x", Hex1bModifiers.None) { EventType = type };
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));

        using (await new Hex1bTerminalInputSequenceBuilder().Key(input).Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken))
            workload.AssertAndClear(expected);
        await automator.KeyAsync(input, TestContext.Current.CancellationToken);
        workload.AssertAndClear(expected);
    }

    [TestMethod]
    [DataRow(Hex1bKey.Enter, "\r", "\x1b[13;1:3u")]
    [DataRow(Hex1bKey.Tab, "\t", "\x1b[9;1:3u")]
    [DataRow(Hex1bKey.Backspace, "\x7f", "\x1b[127;1:3u")]
    public async Task KeyEvent_RecoveryKeys_SuppressReleaseUnlessAllKeys(
        Hex1bKey key, string legacy, string release)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        var input = new Hex1bKeyEvent(key, "", Hex1bModifiers.None) { EventType = Hex1bKeyEventType.Release };
        var sequence = new Hex1bTerminalInputSequenceBuilder().Key(key).Key(input).Build();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=3u"));
        using (await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken))
            workload.AssertAndClear(legacy);
        await automator.KeyAsync(input, TestContext.Current.CancellationToken);
        workload.AssertAndClear("");

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=11u"));
        using (await new Hex1bTerminalInputSequenceBuilder().Key(input).Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken))
            workload.AssertAndClear(release);
        await automator.KeyAsync(input, TestContext.Current.CancellationToken);
        workload.AssertAndClear(release);
    }

    [TestMethod]
    [DataRow(Hex1bKeyEventType.Press, "\x1b[955:923:108;256;923:769u")]
    [DataRow(Hex1bKeyEventType.Repeat, "\x1b[955:923:108;256:2;923:769u")]
    [DataRow(Hex1bKeyEventType.Release, "\x1b[955:923:108;256:3u")]
    public async Task KeyEvent_ExtendedModifiersAndLayoutMetadata_ArePreservedAndConsumedOnce(
        Hex1bKeyEventType type, string expected)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=31u"));
        var input = new Hex1bKeyEvent(Hex1bKey.None, "\u039b\u0301",
            Hex1bModifiers.Shift | Hex1bModifiers.Control | Hex1bModifiers.Alt |
            Hex1bModifiers.CapsLock | Hex1bModifiers.NumLock)
        {
            EventType = type,
            UnicodeKeyCode = 955,
            ShiftedKeyCode = 923,
            BaseLayoutKeyCode = 108
        };
        var sequence = new Hex1bTerminalInputSequenceBuilder().Meta().Hyper().Super().Super()
            .Key(input).Key(Hex1bKey.B).Build();
        using (await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken))
            workload.AssertAndClear(expected + "\x1b[98;;98u");

        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        await automator.Super().Meta().Hyper().Super().KeyAsync(input, TestContext.Current.CancellationToken);
        workload.AssertAndClear(expected);
        await automator.KeyAsync(Hex1bKey.B, TestContext.Current.CancellationToken);
        workload.AssertAndClear("\x1b[98;;98u");
        Assert.AreEqual(Hex1bModifiers.Shift | Hex1bModifiers.Control | Hex1bModifiers.Alt |
            Hex1bModifiers.CapsLock | Hex1bModifiers.NumLock, input.Modifiers);
    }

    [TestMethod]
    public async Task KeyEvent_AppWorkload_PreservesMetadataWithoutEncoding()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var input = new Hex1bKeyEvent(Hex1bKey.None, "\u03a9", Hex1bModifiers.Shift)
        {
            EventType = Hex1bKeyEventType.Release,
            UnicodeKeyCode = 969,
            ShiftedKeyCode = 937,
            BaseLayoutKeyCode = 119
        };
        var sequence = new Hex1bTerminalInputSequenceBuilder().Meta().Key(input).Key(Hex1bKey.B).Build();
        var step = TestSeq.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.AreEqual(input.EventType, step.EventType);
        Assert.AreEqual(input.UnicodeKeyCode, step.UnicodeKeyCode);
        Assert.AreEqual(input.ShiftedKeyCode, step.ShiftedKeyCode);
        Assert.AreEqual(input.BaseLayoutKeyCode, step.BaseLayoutKeyCode);
        using (await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken))
        {
            Assert.IsTrue(workload.InputEvents.TryRead(out var received));
            Assert.AreEqual(input with { Modifiers = Hex1bModifiers.Shift | Hex1bModifiers.Meta }, received);
            Assert.IsTrue(workload.InputEvents.TryRead(out var next));
            Assert.AreEqual(new Hex1bKeyEvent(Hex1bKey.B, "b", Hex1bModifiers.None), next);
        }
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        await automator.Meta().KeyAsync(input, TestContext.Current.CancellationToken);
        Assert.IsTrue(workload.InputEvents.TryRead(out var automated));
        Assert.AreEqual(input with { Modifiers = Hex1bModifiers.Shift | Hex1bModifiers.Meta }, automated);
        Assert.IsFalse(workload.InputEvents.TryRead(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Key_ExtendedModifierPrefixes_CombineWithExplicitModifiersAndReset(bool explicitModifiers)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=1u"));
        var builder = new Hex1bTerminalInputSequenceBuilder().Hyper().Super().Meta().Hyper().Ctrl();
        if (explicitModifiers)
            builder.Key(Hex1bKey.D1, Hex1bModifiers.Control);
        else
            builder.Key(Hex1bKey.D1);
        using (await builder.Key(Hex1bKey.D1).Build().ApplyAsync(terminal, TestContext.Current.CancellationToken))
            workload.AssertAndClear("\x1b[49;61u1");

        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        automator.Meta().Super().Hyper().Meta().Ctrl();
        if (explicitModifiers)
            await automator.KeyAsync(Hex1bKey.D1, Hex1bModifiers.Control, TestContext.Current.CancellationToken);
        else
            await automator.KeyAsync(Hex1bKey.D1, TestContext.Current.CancellationToken);
        workload.AssertAndClear("\x1b[49;61u");
        await automator.KeyAsync(Hex1bKey.D1, TestContext.Current.CancellationToken);
        workload.AssertAndClear("1");
    }

    [TestMethod]
    public async Task KeyEvent_SuppressedRelease_StillConsumesPendingModifiers()
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=1u"));
        var release = new Hex1bKeyEvent(Hex1bKey.X, "x", Hex1bModifiers.None)
        {
            EventType = Hex1bKeyEventType.Release
        };
        using (await new Hex1bTerminalInputSequenceBuilder().Super().Hyper().Meta().Key(release)
            .Key(Hex1bKey.D1).Build().ApplyAsync(terminal, TestContext.Current.CancellationToken))
            workload.AssertAndClear("1");
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        await automator.Super().Hyper().Meta().KeyAsync(release, TestContext.Current.CancellationToken);
        workload.AssertAndClear("");
        await automator.KeyAsync(Hex1bKey.D1, TestContext.Current.CancellationToken);
        workload.AssertAndClear("1");
    }

    [TestMethod]
    [DataRow(0, "aA!:\u00e9\U0001f642")]
    [DataRow(1, "aA!:\u00e9\U0001f642")]
    [DataRow(2, "aA!:\u00e9\U0001f642")]
    [DataRow(4, "aA!:\u00e9\U0001f642")]
    [DataRow(5, "aA!:\u00e9\U0001f642")]
    [DataRow(8, "\x1b[97u\x1b[97;2u\x1b[49;2u\x1b[59;2u\x1b[0u\x1b[0u")]
    [DataRow(12, "\x1b[97u\x1b[97:65;2u\x1b[49:33;2u\x1b[59:58;2u\x1b[0u\x1b[0u")]
    [DataRow(16, "aA!:\u00e9\U0001f642")]
    [DataRow(24, "\x1b[97;;97u\x1b[97;2;65u\x1b[49;2;33u\x1b[59;2;58u\x1b[0;;233u\x1b[0;;128578u")]
    [DataRow(31, "\x1b[97;;97u\x1b[97:65;2;65u\x1b[49:33;2;33u\x1b[59:58;2;58u\x1b[0;;233u\x1b[0;;128578u")]
    public async Task Type_BothAutomationPaths_UseKnownUSIdentitiesButNotInventedUnicodeKeys(int flags, string expected)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[={flags}u"));
        const string text = "aA!:\u00e9\U0001f642";
        using (await new Hex1bTerminalInputSequenceBuilder().Type(text).Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken))
            workload.AssertAndClear(expected);
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        await automator.TypeAsync(text, TestContext.Current.CancellationToken);
        workload.AssertAndClear(expected);
    }

    [TestMethod]
    [DataRow(Hex1bKey.A, "\x1b[97:65;6u")]
    [DataRow(Hex1bKey.D2, "\x1b[50:64;6u")]
    [DataRow(Hex1bKey.OemPlus, "\x1b[61:43;6u")]
    [DataRow(Hex1bKey.Oem1, "\x1b[59:58;6u")]
    public async Task Key_ShiftedSyntheticShortcuts_PreserveKnownShiftedScalar(Hex1bKey key, string expected)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=5u"));
        using (await new Hex1bTerminalInputSequenceBuilder().Shift().Ctrl().Key(key).Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken))
            workload.AssertAndClear(expected);
        using (await new Hex1bTerminalInputSequenceBuilder().Shift().Key(key, Hex1bModifiers.Control).Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken))
            workload.AssertAndClear(expected);
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        await automator.Shift().KeyAsync(key, Hex1bModifiers.Control, TestContext.Current.CancellationToken);
        workload.AssertAndClear(expected);
        await automator.Shift().Ctrl().KeyAsync(key, TestContext.Current.CancellationToken);
        workload.AssertAndClear(expected);
    }

    [TestMethod]
    public async Task KeyEvent_BaseLayoutOnlyAndUnknownText_DoNotInferShiftedIdentity()
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=31u"));
        var input = new Hex1bKeyEvent(Hex1bKey.None, "\u03a9", Hex1bModifiers.Shift)
        {
            UnicodeKeyCode = 969,
            BaseLayoutKeyCode = 119
        };
        using (await new Hex1bTerminalInputSequenceBuilder().Key(input).Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken))
            workload.AssertAndClear("\x1b[969::119;2;937u");
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        await automator.KeyAsync(input, TestContext.Current.CancellationToken);
        workload.AssertAndClear("\x1b[969::119;2;937u");
        Assert.IsNull(TextInputStep.CharToKeyEvent('\u00c9').ShiftedKeyCode);
        Assert.IsNull(TextInputStep.CharToKeyEvent('\u00c9').UnicodeKeyCode);
        Assert.IsNull(TextInputStep.CharToKeyEvent('\u00c9').BaseLayoutKeyCode);
    }

    [TestMethod]
    public async Task Automation_ReplayedSequenceAndCachedKeys_UseLiveModes()
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Key(new Hex1bKeyEvent(Hex1bKey.Escape, "", Hex1bModifiers.None)
            {
                EventType = Hex1bKeyEventType.Release
            })
            .Key(Hex1bKey.NumPad1).Type("!").Build();

        foreach (var (flags, enhanced) in new[]
        {
            (0, false), (31, true), (32, false), (63, true),
            (64, false), (95, true), (0, false)
        })
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[={flags}u"));
            using (await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken))
                workload.AssertAndClear(enhanced
                    ? "\x1b[99;5u\x1b[27;1:3u\x1b[57400;;49u\x1b[49:33;2;33u"
                    : "\x03" + "1!");
            await automator.EnterAsync(TestContext.Current.CancellationToken);
            workload.AssertAndClear(enhanced ? "\x1b[13u" : "\r");
        }
    }

    // Kitty spec: left/right modifier keys are distinct functional keys (Ctrl 57442/57448) that are
    // reported only with report-all-keys (8); repeat/release types additionally need flag 2.
    // The caller supplies post-event modifier state, so Ctrl stays set until the last Ctrl releases.
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ModifierKeyEvents_LeftAndRightCtrl_ReplayAcrossLiveModesOnBothPaths(bool releaseLeftFirst)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        static Hex1bKeyEvent Ctrl(int code, Hex1bKeyEventType type, Hex1bModifiers modifiers)
            => new(Hex1bKey.None, "", modifiers) { UnicodeKeyCode = code, EventType = type };
        var (first, second) = releaseLeftFirst ? (57442, 57448) : (57448, 57442);
        Hex1bKeyEvent[] events =
        [
            Ctrl(57442, Hex1bKeyEventType.Press, Hex1bModifiers.Control),
            Ctrl(57448, Hex1bKeyEventType.Press, Hex1bModifiers.Control),
            Ctrl(57442, Hex1bKeyEventType.Repeat, Hex1bModifiers.Control),
            Ctrl(57448, Hex1bKeyEventType.Repeat, Hex1bModifiers.Control),
            Ctrl(first, Hex1bKeyEventType.Release, Hex1bModifiers.Control),
            Ctrl(second, Hex1bKeyEventType.Release, Hex1bModifiers.None)
        ];
        var builder = new Hex1bTerminalInputSequenceBuilder();
        foreach (var input in events)
            builder.Key(input);
        var sequence = builder.Build();

        const string presses = "\x1b[57442;5u\x1b[57448;5u";
        var reportAll = presses + "\x1b[57442;5u\x1b[57448;5u";
        var allEvents = presses + "\x1b[57442;5:2u\x1b[57448;5:2u" +
            $"\x1b[{first};5:3u\x1b[{second};1:3u";
        foreach (var (flags, expected) in new[]
        {
            (0, ""), (1, ""), (2, ""), (3, ""), (8, reportAll), (10, allEvents), (11, allEvents),
            (31, allEvents), (32, ""), (40, reportAll), (63, allEvents), (9, reportAll), (0, "")
        })
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[={flags}u"));
            using (await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken))
                workload.AssertAndClear(expected);
            foreach (var input in events)
                await automator.KeyAsync(input, TestContext.Current.CancellationToken);
            workload.AssertAndClear(expected);
        }
    }

    [TestMethod]
    [DataRow(57441, 57447, Hex1bModifiers.Shift, 2)]
    [DataRow(57442, 57448, Hex1bModifiers.Control, 5)]
    [DataRow(57443, 57449, Hex1bModifiers.Alt, 3)]
    [DataRow(57444, 57450, Hex1bModifiers.Super, 9)]
    [DataRow(57445, 57451, Hex1bModifiers.Hyper, 17)]
    [DataRow(57446, 57452, Hex1bModifiers.Meta, 33)]
    public async Task ModifierKeyEvents_LeftAndRightIdentities_AreDistinctOnBothPaths(
        int left, int right, Hex1bModifiers modifier, int encoded)
    {
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=10u"));
        var automator = new Hex1bTerminalAutomator(terminal, TimeSpan.FromSeconds(5));
        foreach (var code in new[] { left, right })
        {
            var expected = $"\x1b[{code};{encoded}u\x1b[{code};{encoded}:2u\x1b[{code};1:3u";
            Hex1bKeyEvent[] events =
            [
                new(Hex1bKey.None, "", modifier) { UnicodeKeyCode = code },
                new(Hex1bKey.None, "", modifier) { UnicodeKeyCode = code, EventType = Hex1bKeyEventType.Repeat },
                new(Hex1bKey.None, "", Hex1bModifiers.None)
                {
                    UnicodeKeyCode = code, EventType = Hex1bKeyEventType.Release
                }
            ];
            var builder = new Hex1bTerminalInputSequenceBuilder();
            foreach (var input in events)
                builder.Key(input);
            using (await builder.Build().ApplyAsync(terminal, TestContext.Current.CancellationToken))
                workload.AssertAndClear(expected);
            foreach (var input in events)
                await automator.KeyAsync(input, TestContext.Current.CancellationToken);
            workload.AssertAndClear(expected);
        }
    }

    private sealed class RecordingWorkload : IHex1bTerminalWorkloadAdapter
    {
        private readonly List<byte> _input = [];

        internal void AssertAndClear(string expected)
        {
            Assert.AreEqual(Convert.ToHexString(Encoding.UTF8.GetBytes(expected)),
                Convert.ToHexString(_input.ToArray()));
            _input.Clear();
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            _input.AddRange(data.Span);
            return ValueTask.CompletedTask;
        }
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;
        public event Action? Disconnected { add { } remove { } }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
