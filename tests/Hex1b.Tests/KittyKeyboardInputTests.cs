using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class KittyKeyboardInputTests
{
    [TestMethod]
    public void Decode_ReleaseWithAlternatesAndText_PreservesMetadata()
    {
        Assert.IsTrue(KittyKeyboardInput.TryDecode("\x1b[97:65:113;70:3;65:128512u", out var input));
        Assert.IsNotNull(input);
        Assert.AreEqual(Hex1bKey.A, input.Key);
        Assert.AreEqual(Hex1bKeyEventType.Release, input.EventType);
        Assert.AreEqual(Hex1bModifiers.Shift | Hex1bModifiers.Control | Hex1bModifiers.CapsLock, input.Modifiers);
        Assert.AreEqual(97, input.UnicodeKeyCode);
        Assert.AreEqual(65, input.ShiftedKeyCode);
        Assert.AreEqual(113, input.BaseLayoutKeyCode);
        Assert.AreEqual("A😀", input.Text);
    }

    [TestMethod]
    [DataRow("\x1b[1;5:2A", Hex1bKey.UpArrow, Hex1bKeyEventType.Repeat)]
    [DataRow("\x1b[15;1:3~", Hex1bKey.F5, Hex1bKeyEventType.Release)]
    [DataRow("\x1b[57413;1u", Hex1bKey.Add, Hex1bKeyEventType.Press)]
    [DataRow("\x1b[13~", Hex1bKey.F3, Hex1bKeyEventType.Press)]
    [DataRow("\x1b[13;5:2~", Hex1bKey.F3, Hex1bKeyEventType.Repeat)]
    [DataRow("\x1b[13;1:3~", Hex1bKey.F3, Hex1bKeyEventType.Release)]
    [DataRow("\x1b[R", Hex1bKey.F3, Hex1bKeyEventType.Press)]
    [DataRow("\x1b[1;5:2R", Hex1bKey.F3, Hex1bKeyEventType.Repeat)]
    [DataRow("\x1b[1;1:3R", Hex1bKey.F3, Hex1bKeyEventType.Release)]
    public void Decode_FunctionalKeys_RecognizesEvents(string sequence, Hex1bKey key, Hex1bKeyEventType type)
    {
        Assert.IsTrue(KittyKeyboardInput.TryDecode(sequence, out var input));
        Assert.IsNotNull(input);
        Assert.AreEqual(key, input.Key);
        Assert.AreEqual(type, input.EventType);
    }

    [TestMethod]
    [DataRow("\x1b[44;5u", Hex1bKey.OemComma)]
    [DataRow("\x1b[46;5u", Hex1bKey.OemPeriod)]
    [DataRow("\x1b[45;5u", Hex1bKey.OemMinus)]
    [DataRow("\x1b[61;5u", Hex1bKey.OemPlus)]
    [DataRow("\x1b[47;5u", Hex1bKey.OemQuestion)]
    [DataRow("\x1b[59;5u", Hex1bKey.Oem1)]
    [DataRow("\x1b[91;5u", Hex1bKey.Oem4)]
    [DataRow("\x1b[92;5u", Hex1bKey.Oem5)]
    [DataRow("\x1b[93;5u", Hex1bKey.Oem6)]
    [DataRow("\x1b[39;5u", Hex1bKey.Oem7)]
    [DataRow("\x1b[96;5u", Hex1bKey.OemTilde)]
    public void Decode_PunctuationShortcut_UsesKeyBindingIdentity(string sequence, Hex1bKey key)
    {
        Assert.IsTrue(KittyKeyboardInput.TryDecode(sequence, out var input));
        Assert.IsNotNull(input);
        Assert.AreEqual(key, input.Key);
        Assert.AreEqual(Hex1bModifiers.Control, input.Modifiers);
    }

    [TestMethod]
    [DataRow("\x1b[97;256u", 97, 255)]
    [DataRow("\x1b[97::113;65u", 97, 64)]
    [DataRow("\x1b[57428u", 57428, 0)]
    [DataRow("\x1b[57376;1:3u", 57376, 0)]
    public void Decode_ExtendedModifiersAndUnmappedFunctionalCodes_PreservesWireValues(
        string sequence, int keyCode, int modifierMask)
    {
        Assert.IsTrue(KittyKeyboardInput.TryDecode(sequence, out var input));
        Assert.IsNotNull(input);
        Assert.AreEqual(keyCode, input.UnicodeKeyCode);
        Assert.AreEqual((Hex1bModifiers)modifierMask, input.Modifiers);
    }

    [TestMethod]
    [DataRow("\x1b[?1u")]
    [DataRow("\x1b[97;1:4u")]
    [DataRow("\x1b[1114112u")]
    [DataRow("\x1b[97;1;10u")]
    [DataRow("\x1b[97;1;127u")]
    [DataRow("\x1b[97;1;159u")]
    [DataRow("\x1b[97;257u")]
    [DataRow("\x1b[0u")]
    [DataRow("\x1b[1;5R")]
    public void Decode_InvalidOrProtocolResponse_ReturnsFalse(string sequence)
        => Assert.IsFalse(KittyKeyboardInput.TryDecode(sequence, out _));

    [TestMethod]
    [DataRow("\x1b[97;5:3u")]
    [DataRow("\x1b[1;5:2A")]
    [DataRow("\x1b[15;1:3~")]
    public void Tokenize_EnhancedKey_PreservesSequence(string sequence)
    {
        var tokens = AnsiTokenizer.Tokenize(sequence);
        Assert.AreEqual(sequence, TestSeq.IsType<UnrecognizedSequenceToken>(TestSeq.Single(tokens)).Sequence);
        Assert.AreEqual(sequence, AnsiTokenSerializer.Serialize(tokens));
    }

    [TestMethod]
    public void Tokenize_UnparameterizedRestore_StillRestoresCursor()
        => TestSeq.IsType<RestoreCursorToken>(TestSeq.Single(AnsiTokenizer.Tokenize("\x1b[u")));

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
    [DataRow(1, Hex1bKey.C, "", Hex1bModifiers.Control, Hex1bKeyEventType.Press, "\x1b[99;5u")]
    [DataRow(1, Hex1bKey.Escape, "", Hex1bModifiers.None, Hex1bKeyEventType.Press, "\x1b[27u")]
    [DataRow(3, Hex1bKey.UpArrow, "", Hex1bModifiers.Control, Hex1bKeyEventType.Repeat, "\x1b[1;5:2A")]
    [DataRow(3, Hex1bKey.F5, "", Hex1bModifiers.None, Hex1bKeyEventType.Release, "\x1b[15;1:3~")]
    [DataRow(3, Hex1bKey.F3, "", Hex1bModifiers.None, Hex1bKeyEventType.Press, "\x1b[13~")]
    [DataRow(3, Hex1bKey.F3, "", Hex1bModifiers.Control, Hex1bKeyEventType.Press, "\x1b[13;5~")]
    [DataRow(3, Hex1bKey.F3, "", Hex1bModifiers.Control, Hex1bKeyEventType.Repeat, "\x1b[13;5:2~")]
    [DataRow(3, Hex1bKey.F3, "", Hex1bModifiers.None, Hex1bKeyEventType.Release, "\x1b[13;1:3~")]
    [DataRow(3, Hex1bKey.A, "a", Hex1bModifiers.None, Hex1bKeyEventType.Release, "")]
    [DataRow(1, Hex1bKey.NumPad1, "1", Hex1bModifiers.None, Hex1bKeyEventType.Press, "1")]
    [DataRow(1, Hex1bKey.NumPad1, "", Hex1bModifiers.None, Hex1bKeyEventType.Press, "\x1b[57400u")]
    [DataRow(8, Hex1bKey.NumPad1, "1", Hex1bModifiers.None, Hex1bKeyEventType.Press, "\x1b[57400u")]
    [DataRow(25, Hex1bKey.A, "A😀", Hex1bModifiers.Shift, Hex1bKeyEventType.Press, "\x1b[97;2;65:128512u")]
    [DataRow(27, Hex1bKey.A, "a", Hex1bModifiers.None, Hex1bKeyEventType.Release, "\x1b[97;1:3u")]
    [DataRow(0, Hex1bKey.A, "a", Hex1bModifiers.None, Hex1bKeyEventType.Release, "")]
    public void Encode_NegotiatedFlags_UsesExpectedWireForm(
        int flags, Hex1bKey key, string text, Hex1bModifiers modifiers, Hex1bKeyEventType type, string expected)
    {
        var modes = new TerminalInputModes(false, false, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, flags);
        var input = new Hex1bKeyEvent(key, text, modifiers) { EventType = type };
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(input, modes));
    }

    [TestMethod]
    [DataRow(Hex1bKey.F1, "\x1b[P")]
    [DataRow(Hex1bKey.F2, "\x1b[Q")]
    [DataRow(Hex1bKey.F3, "\x1b[13~")]
    [DataRow(Hex1bKey.F4, "\x1b[S")]
    [DataRow(Hex1bKey.F5, "\x1b[15~")]
    [DataRow(Hex1bKey.F6, "\x1b[17~")]
    [DataRow(Hex1bKey.F7, "\x1b[18~")]
    [DataRow(Hex1bKey.F8, "\x1b[19~")]
    [DataRow(Hex1bKey.F9, "\x1b[20~")]
    [DataRow(Hex1bKey.F10, "\x1b[21~")]
    [DataRow(Hex1bKey.F11, "\x1b[23~")]
    [DataRow(Hex1bKey.F12, "\x1b[24~")]
    [DataRow(Hex1bKey.UpArrow, "\x1b[A")]
    [DataRow(Hex1bKey.DownArrow, "\x1b[B")]
    [DataRow(Hex1bKey.RightArrow, "\x1b[C")]
    [DataRow(Hex1bKey.LeftArrow, "\x1b[D")]
    [DataRow(Hex1bKey.Home, "\x1b[H")]
    [DataRow(Hex1bKey.End, "\x1b[F")]
    [DataRow(Hex1bKey.Insert, "\x1b[2~")]
    [DataRow(Hex1bKey.Delete, "\x1b[3~")]
    [DataRow(Hex1bKey.PageUp, "\x1b[5~")]
    [DataRow(Hex1bKey.PageDown, "\x1b[6~")]
    public void Encode_Disambiguation_FunctionalKeysUseKittySequences(Hex1bKey key, string expected)
    {
        var modes = new TerminalInputModes(false, false, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, 1);
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new Hex1bKeyEvent(key, "", Hex1bModifiers.None), modes));
    }

    [TestMethod]
    [DataRow(Hex1bKey.NumPad0, 57399)]
    [DataRow(Hex1bKey.NumPad1, 57400)]
    [DataRow(Hex1bKey.NumPad2, 57401)]
    [DataRow(Hex1bKey.NumPad3, 57402)]
    [DataRow(Hex1bKey.NumPad4, 57403)]
    [DataRow(Hex1bKey.NumPad5, 57404)]
    [DataRow(Hex1bKey.NumPad6, 57405)]
    [DataRow(Hex1bKey.NumPad7, 57406)]
    [DataRow(Hex1bKey.NumPad8, 57407)]
    [DataRow(Hex1bKey.NumPad9, 57408)]
    [DataRow(Hex1bKey.Decimal, 57409)]
    [DataRow(Hex1bKey.Divide, 57410)]
    [DataRow(Hex1bKey.Multiply, 57411)]
    [DataRow(Hex1bKey.Subtract, 57412)]
    [DataRow(Hex1bKey.Add, 57413)]
    public void Encode_ReportAllKeys_KeypadUsesFunctionalKeyCodes(Hex1bKey key, int code)
    {
        var modes = new TerminalInputModes(false, false, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, 8);
        Assert.AreEqual($"\x1b[{code}u", TerminalInputEncoder.EncodeKey(
            new Hex1bKeyEvent(key, "", Hex1bModifiers.None), modes));
    }

    [TestMethod]
    [DataRow(2, Hex1bKey.A, "a", Hex1bKeyEventType.Press, "a")]
    [DataRow(2, Hex1bKey.A, "a", Hex1bKeyEventType.Repeat, "a")]
    [DataRow(2, Hex1bKey.A, "a", Hex1bKeyEventType.Release, "")]
    [DataRow(1, Hex1bKey.A, "a", Hex1bKeyEventType.Repeat, "a")]
    [DataRow(1, Hex1bKey.Tab, "\t", Hex1bKeyEventType.Press, "\t")]
    [DataRow(1, Hex1bKey.Tab, "\t", Hex1bKeyEventType.Release, "")]
    [DataRow(8, Hex1bKey.Enter, "\r", Hex1bKeyEventType.Press, "\x1b[13u")]
    [DataRow(10, Hex1bKey.Enter, "\r", Hex1bKeyEventType.Release, "\x1b[13;1:3u")]
    [DataRow(27, Hex1bKey.A, "a", Hex1bKeyEventType.Release, "\x1b[97;1:3u")]
    [DataRow(8, Hex1bKey.None, "û", Hex1bKeyEventType.Press, "\x1b[0u")]
    public void Encode_ProgressiveFlags_PreservesTextAndReleaseCompatibility(
        int flags, Hex1bKey key, string text, Hex1bKeyEventType type, string expected)
    {
        var modes = new TerminalInputModes(false, false, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, flags);
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new Hex1bKeyEvent(key, text, Hex1bModifiers.None) { EventType = type }, modes));
    }

    [TestMethod]
    public void Encode_ReportAssociatedText_FiltersControlsAndOmitsReleaseText()
    {
        var modes = new TerminalInputModes(false, false, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, 27);
        var press = new Hex1bKeyEvent(Hex1bKey.A, "a\u0001\u007f\u0080b", Hex1bModifiers.None);
        var release = press with { EventType = Hex1bKeyEventType.Release };
        var modifiedText = new Hex1bKeyEvent(Hex1bKey.J, "j", Hex1bModifiers.Control);

        Assert.AreEqual("\x1b[97;1;97:98u", TerminalInputEncoder.EncodeKey(press, modes));
        Assert.AreEqual("\x1b[97;1:3u", TerminalInputEncoder.EncodeKey(release, modes));
        Assert.AreEqual("\x1b[106;5;106u", TerminalInputEncoder.EncodeKey(modifiedText, modes));
    }

    [TestMethod]
    public void Encode_ReportAlternateKeys_BaseLayoutOnlyUsesEmptyShiftedField()
    {
        var modes = new TerminalInputModes(false, false, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, 13);
        var input = new Hex1bKeyEvent(Hex1bKey.A, "я", Hex1bModifiers.Control)
        {
            UnicodeKeyCode = 0x44f,
            BaseLayoutKeyCode = 'q',
        };
        Assert.AreEqual("\x1b[1103::113;5u", TerminalInputEncoder.EncodeKey(input, modes));
    }

    [TestMethod]
    public void Encode_ReportAllKeys_PreservesExtendedModifierMask()
    {
        var modes = new TerminalInputModes(false, false, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, 8);
        var input = new Hex1bKeyEvent(Hex1bKey.A, "a",
            Hex1bModifiers.Control | Hex1bModifiers.CapsLock | Hex1bModifiers.NumLock);

        Assert.AreEqual("\x1b[97;197u", TerminalInputEncoder.EncodeKey(input, modes));
        Assert.AreEqual("\x1b[97;256u", TerminalInputEncoder.EncodeKey(
            new Hex1bKeyEvent(Hex1bKey.A, "a", (Hex1bModifiers)byte.MaxValue), modes));
        Assert.AreEqual("\x1b[57441u", TerminalInputEncoder.EncodeKey(
            new Hex1bKeyEvent(Hex1bKey.None, "", Hex1bModifiers.None) { UnicodeKeyCode = 57441 }, modes));
    }

    [TestMethod]
    public void Encode_ReportAlternateKeys_DoesNotForceTextIntoCsi()
    {
        var modes = new TerminalInputModes(false, false, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, 4);
        var input = new Hex1bKeyEvent(Hex1bKey.A, "a", Hex1bModifiers.None)
        {
            UnicodeKeyCode = 97,
            ShiftedKeyCode = 65,
        };
        Assert.AreEqual("a", TerminalInputEncoder.EncodeKey(input, modes));
    }

    [TestMethod]
    public void Encode_AlternateKeysAndExtendedModifiers_UsesReportedValues()
    {
        var modes = new TerminalInputModes(false, false, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, 31);
        var input = new Hex1bKeyEvent(Hex1bKey.A, "A", Hex1bModifiers.Shift | Hex1bModifiers.Super)
        {
            UnicodeKeyCode = 97, ShiftedKeyCode = 65, BaseLayoutKeyCode = 113,
        };
        Assert.AreEqual("\x1b[97:65:113;10;65u", TerminalInputEncoder.EncodeKey(input, modes));
    }

    [TestMethod]
    public void Encode_TextProducingKeypadWithDisambiguation_IgnoresApplicationKeypadMode()
    {
        var modes = new TerminalInputModes(false, true, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, 1);
        Assert.AreEqual("1", TerminalInputEncoder.EncodeKey(
            new Hex1bKeyEvent(Hex1bKey.NumPad1, "1", Hex1bModifiers.None), modes));
    }

    [TestMethod]
    public void Encode_ShiftedAlternateWithoutShift_OmitsAlternateField()
    {
        var modes = new TerminalInputModes(false, false, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, 12);
        var input = new Hex1bKeyEvent(Hex1bKey.A, "a", Hex1bModifiers.None)
        {
            UnicodeKeyCode = 97, ShiftedKeyCode = 65,
        };
        Assert.AreEqual("\x1b[97u", TerminalInputEncoder.EncodeKey(input, modes));
    }

    [TestMethod]
    public void Encode_PureTextWithAssociatedText_UsesZeroKeyCode()
    {
        var modes = new TerminalInputModes(false, false, false, TerminalMouseTracking.None,
            TerminalMouseEncoding.Legacy, 80, 24, 24);
        Assert.AreEqual("\x1b[0;1;233:128512u",
            TerminalInputEncoder.EncodeKey(Hex1bKeyEvent.FromText("é😀"), modes));
    }
}
