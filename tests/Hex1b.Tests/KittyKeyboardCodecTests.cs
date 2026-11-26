using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class KittyKeyboardCodecTests
{
    [TestMethod]
    [DataRow(0, Hex1bModifiers.None, Hex1bKeyEventType.Press, "a")]
    [DataRow(1, Hex1bModifiers.None, Hex1bKeyEventType.Press, "a")]
    [DataRow(2, Hex1bModifiers.None, Hex1bKeyEventType.Repeat, "a")]
    [DataRow(4, Hex1bModifiers.None, Hex1bKeyEventType.Press, "a")]
    [DataRow(16, Hex1bModifiers.None, Hex1bKeyEventType.Press, "a")]
    [DataRow(8, Hex1bModifiers.None, Hex1bKeyEventType.Press, "\x1b[97u")]
    [DataRow(24, Hex1bModifiers.None, Hex1bKeyEventType.Press, "\x1b[97;;97u")]
    [DataRow(0, Hex1bModifiers.Control, Hex1bKeyEventType.Press, "\x01")]
    [DataRow(1, Hex1bModifiers.Control, Hex1bKeyEventType.Press, "\x1b[97;5u")]
    [DataRow(0, Hex1bModifiers.None, Hex1bKeyEventType.Release, "")]
    [DataRow(1, Hex1bModifiers.None, Hex1bKeyEventType.Release, "")]
    [DataRow(8, Hex1bModifiers.None, Hex1bKeyEventType.Release, "")]
    [DataRow(2, Hex1bModifiers.None, Hex1bKeyEventType.Release, "\x1b[97;1:3u")]
    [DataRow(3, Hex1bModifiers.None, Hex1bKeyEventType.Release, "\x1b[97;1:3u")]
    [DataRow(10, Hex1bModifiers.None, Hex1bKeyEventType.Repeat, "\x1b[97;1:2u")]
    [DataRow(26, Hex1bModifiers.None, Hex1bKeyEventType.Repeat, "\x1b[97;1:2;97u")]
    [DataRow(31, Hex1bModifiers.None, Hex1bKeyEventType.Release, "\x1b[97;1:3u")]
    [DataRow(8, Hex1bModifiers.Super, Hex1bKeyEventType.Press, "\x1b[97;9u")]
    [DataRow(8, Hex1bModifiers.Hyper, Hex1bKeyEventType.Press, "\x1b[97;17u")]
    [DataRow(8, Hex1bModifiers.Meta, Hex1bKeyEventType.Press, "\x1b[97;33u")]
    [DataRow(8, Hex1bModifiers.CapsLock, Hex1bKeyEventType.Press, "\x1b[97;65u")]
    [DataRow(8, Hex1bModifiers.NumLock, Hex1bKeyEventType.Press, "\x1b[97;129u")]
    [DataRow(8, (Hex1bModifiers)255, Hex1bKeyEventType.Press, "\x1b[97;256u")]
    [DataRow(1, Hex1bModifiers.CapsLock, Hex1bKeyEventType.Press, "a")]
    public void Encode_ProgressiveFlags_ProducesLiteralWire(
        int flags, Hex1bModifiers modifiers, Hex1bKeyEventType kind, string expected)
    {
        var input = new Hex1bKeyEvent(Hex1bKey.A, "a", modifiers) { EventType = kind };
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(input, Modes(flags)));
    }

    [TestMethod]
    [DataRow(0, Hex1bKey.UpArrow, Hex1bModifiers.None, false, "\x1b[A")]
    [DataRow(0, Hex1bKey.UpArrow, Hex1bModifiers.None, true, "\x1bOA")]
    [DataRow(1, Hex1bKey.UpArrow, Hex1bModifiers.None, true, "\x1b[A")]
    [DataRow(8, Hex1bKey.UpArrow, Hex1bModifiers.None, true, "\x1b[A")]
    [DataRow(2, Hex1bKey.UpArrow, Hex1bModifiers.None, true, "\x1bOA")]
    [DataRow(1, Hex1bKey.Home, Hex1bModifiers.None, true, "\x1b[H")]
    [DataRow(8, Hex1bKey.End, Hex1bModifiers.None, true, "\x1b[F")]
    [DataRow(1, Hex1bKey.LeftArrow, Hex1bModifiers.Alt, true, "\x1b[1;3D")]
    [DataRow(8, Hex1bKey.Home, Hex1bModifiers.None, false, "\x1b[H")]
    [DataRow(8, Hex1bKey.End, Hex1bModifiers.Shift, false, "\x1b[1;2F")]
    [DataRow(1, Hex1bKey.F1, Hex1bModifiers.None, false, "\x1b[P")]
    [DataRow(1, Hex1bKey.F2, Hex1bModifiers.Control, false, "\x1b[1;5Q")]
    [DataRow(1, Hex1bKey.F3, Hex1bModifiers.Control, false, "\x1b[13;5~")]
    [DataRow(8, Hex1bKey.F3, Hex1bModifiers.None, false, "\x1b[13~")]
    [DataRow(0, Hex1bKey.F3, Hex1bModifiers.Control, false, "\x1b[1;5R")]
    [DataRow(8, Hex1bKey.F4, Hex1bModifiers.Alt, false, "\x1b[1;3S")]
    [DataRow(8, Hex1bKey.F5, Hex1bModifiers.None, false, "\x1b[15~")]
    [DataRow(8, Hex1bKey.F12, Hex1bModifiers.Shift, false, "\x1b[24;2~")]
    [DataRow(8, Hex1bKey.Insert, Hex1bModifiers.None, false, "\x1b[2~")]
    [DataRow(8, Hex1bKey.Delete, Hex1bModifiers.Control, false, "\x1b[3;5~")]
    [DataRow(8, Hex1bKey.PageUp, Hex1bModifiers.None, false, "\x1b[5~")]
    [DataRow(8, Hex1bKey.PageDown, Hex1bModifiers.CapsLock, false, "\x1b[6;65~")]
    [DataRow(1, Hex1bKey.NumPad1, Hex1bModifiers.None, false, "\x1b[57400u")]
    [DataRow(8, Hex1bKey.Divide, Hex1bModifiers.None, false, "\x1b[57410u")]
    [DataRow(1, Hex1bKey.Add, Hex1bModifiers.None, false, "\x1b[57413u")]
    [DataRow(1, Hex1bKey.Escape, Hex1bModifiers.None, false, "\x1b[27u")]
    [DataRow(1, Hex1bKey.Enter, Hex1bModifiers.None, false, "\r")]
    [DataRow(8, Hex1bKey.Enter, Hex1bModifiers.None, false, "\x1b[13u")]
    [DataRow(1, Hex1bKey.Tab, Hex1bModifiers.Shift, false, "\x1b[9;2u")]
    [DataRow(1, Hex1bKey.Backspace, Hex1bModifiers.None, false, "\x7f")]
    public void Encode_FunctionalKeys_ProducesLiteralWire(
        int flags, Hex1bKey key, Hex1bModifiers modifiers, bool cursorMode, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new(key, "", modifiers), Modes(flags, cursorMode)));
    }

    [TestMethod]
    [DataRow(Hex1bKeyEventType.Repeat, 2, "\x1b[13;1:2~")]
    [DataRow(Hex1bKeyEventType.Release, 2, "\x1b[13;1:3~")]
    [DataRow(Hex1bKeyEventType.Release, 1, "")]
    public void Encode_FunctionalEvent_PreservesEventKind(Hex1bKeyEventType kind, int flags, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.F3, "", Hex1bModifiers.None) { EventType = kind }, Modes(flags)));
    }

    [TestMethod]
    [DataRow(2, Hex1bKey.Enter, "")]
    [DataRow(3, Hex1bKey.Tab, "")]
    [DataRow(2, Hex1bKey.Backspace, "")]
    [DataRow(10, Hex1bKey.Enter, "\x1b[13;1:3u")]
    [DataRow(10, Hex1bKey.Tab, "\x1b[9;1:3u")]
    [DataRow(10, Hex1bKey.Backspace, "\x1b[127;1:3u")]
    public void Encode_RecoveryKeyRelease_RequiresAllKeys(int flags, Hex1bKey key, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new(key, "", Hex1bModifiers.None) { EventType = Hex1bKeyEventType.Release }, Modes(flags)));
    }

    [TestMethod]
    [DataRow(2, Hex1bKey.UpArrow, "\x1b[1;1:3A")]
    [DataRow(1, Hex1bKey.UpArrow, "")]
    [DataRow(10, Hex1bKey.UpArrow, "\x1b[1;1:3A")]
    public void Encode_CursorKeyReleaseInCursorMode_UsesCsiForm(int flags, Hex1bKey key, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new(key, "", Hex1bModifiers.None) { EventType = Hex1bKeyEventType.Release }, Modes(flags, true)));
    }

    [TestMethod]
    [DataRow(Hex1bKey.NumPad1, 2, Hex1bModifiers.None, "\x1b[49;1:3u")]
    [DataRow(Hex1bKey.NumPad0, 2, Hex1bModifiers.None, "\x1b[48;1:3u")]
    [DataRow(Hex1bKey.NumPad9, 6, Hex1bModifiers.None, "\x1b[57;1:3u")]
    [DataRow(Hex1bKey.Decimal, 2, Hex1bModifiers.None, "\x1b[46;1:3u")]
    [DataRow(Hex1bKey.Divide, 2, Hex1bModifiers.None, "\x1b[47;1:3u")]
    [DataRow(Hex1bKey.Multiply, 2, Hex1bModifiers.None, "\x1b[42;1:3u")]
    [DataRow(Hex1bKey.Subtract, 2, Hex1bModifiers.None, "\x1b[45;1:3u")]
    [DataRow(Hex1bKey.Add, 2, Hex1bModifiers.Control, "\x1b[43;5:3u")]
    [DataRow(Hex1bKey.NumPad1, 3, Hex1bModifiers.None, "\x1b[57400;1:3u")]
    [DataRow(Hex1bKey.NumPad1, 10, Hex1bModifiers.None, "\x1b[57400;1:3u")]
    public void Encode_KeypadRelease_UsesEquivalentUnlessDisambiguated(
        Hex1bKey key, int flags, Hex1bModifiers modifiers, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new(key, "", modifiers) { EventType = Hex1bKeyEventType.Release }, Modes(flags)));
    }

    [TestMethod]
    [DataRow(57414, "")]
    [DataRow(57415, "\x1b[61;1:3u")]
    [DataRow(57416, "\x1b[44;1:3u")]
    [DataRow(57417, "\x1b[1;1:3D")]
    [DataRow(57419, "\x1b[1;1:3A")]
    [DataRow(57421, "\x1b[5;1:3~")]
    [DataRow(57426, "\x1b[3;1:3~")]
    [DataRow(57427, "\x1b[1;1:3E")]
    public void Encode_RawKeypadIdentityRelease_UsesEquivalentWithoutDisambiguate(int code, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.None, "", Hex1bModifiers.None)
            {
                UnicodeKeyCode = code,
                EventType = Hex1bKeyEventType.Release,
            }, Modes(2)));
    }

    [TestMethod]
    public void Encode_KeypadPressAndRepeatWithEventFlag_KeepLegacyText()
    {
        var press = new Hex1bKeyEvent(Hex1bKey.NumPad1, "1", Hex1bModifiers.None);
        Assert.AreEqual("1", TerminalInputEncoder.EncodeKey(press, Modes(2)));
        Assert.AreEqual("1", TerminalInputEncoder.EncodeKey(
            press with { EventType = Hex1bKeyEventType.Repeat }, Modes(2)));
        Assert.AreEqual("\x1b[57400;1:2u", TerminalInputEncoder.EncodeKey(
            press with { EventType = Hex1bKeyEventType.Repeat }, Modes(3)));
    }

    [TestMethod]
    [DataRow(1, Hex1bModifiers.None, Hex1bKeyEventType.Press, "\x1b[97u")]
    [DataRow(2, Hex1bModifiers.None, Hex1bKeyEventType.Press, "\x1b[97u")]
    [DataRow(2, Hex1bModifiers.None, Hex1bKeyEventType.Repeat, "\x1b[97;1:2u")]
    [DataRow(4, Hex1bModifiers.None, Hex1bKeyEventType.Press, "\x1b[97u")]
    [DataRow(1, Hex1bModifiers.Shift, Hex1bKeyEventType.Press, "\x1b[97;2u")]
    [DataRow(3, Hex1bModifiers.Shift, Hex1bKeyEventType.Repeat, "\x1b[97;2:2u")]
    [DataRow(0, Hex1bModifiers.None, Hex1bKeyEventType.Press, "")]
    public void Encode_KnownKeyWithoutText_ReportsIdentityWithoutInventingText(
        int flags, Hex1bModifiers modifiers, Hex1bKeyEventType kind, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.A, "", modifiers) { EventType = kind }, Modes(flags)));
    }

    [TestMethod]
    public void Encode_TextlessUnknownIdentity_StaysEmptyWithoutAllKeys()
    {
        Assert.AreEqual("", TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.None, "", Hex1bModifiers.None), Modes(1)));
    }

    [TestMethod]
    [DataRow("\x1b[97u", 1, "\x1b[97u")]
    [DataRow("\x1b[97;1:2u", 2, "\x1b[97;1:2u")]
    [DataRow("\x1b[1078u", 1, "\x1b[1078u")]
    public void Encode_DecodedTextlessEvent_IsForwardedNotDropped(string wire, int flags, string expected)
    {
        Assert.IsTrue(KittyKeyboardInput.TryDecode(wire, out var decoded));
        Assert.AreEqual("", decoded!.Text);
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(decoded, Modes(flags)));
    }

    [TestMethod]
    [DataRow(5, "A", "\x1b[97:65:113;6u")]
    [DataRow(1, "A", "\x1b[97;6u")]
    [DataRow(29, "A", "\x1b[97:65:113;6;65u")]
    public void Encode_AlternateLayout_UsesExplicitIdentities(int flags, string text, string expected)
    {
        var input = new Hex1bKeyEvent(Hex1bKey.A, text, Hex1bModifiers.Control | Hex1bModifiers.Shift)
        {
            UnicodeKeyCode = 97, ShiftedKeyCode = 65, BaseLayoutKeyCode = 113,
        };
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(input, Modes(flags)));
    }

    [TestMethod]
    public void Encode_BaseLayoutWithoutShift_LeavesEmptySubfield()
    {
        var input = new Hex1bKeyEvent(Hex1bKey.C, "", Hex1bModifiers.Control)
        {
            UnicodeKeyCode = 1089, ShiftedKeyCode = 1057, BaseLayoutKeyCode = 99,
        };
        Assert.AreEqual("\x1b[1089::99;5u", TerminalInputEncoder.EncodeKey(input, Modes(5)));
        Assert.AreEqual("A", TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.A, "A", Hex1bModifiers.Shift) { ShiftedKeyCode = 65 }, Modes(5)));
    }

    [TestMethod]
    [DataRow(Hex1bKey.A, "ab", 24, "\x1b[97;;97:98u")]
    [DataRow(Hex1bKey.None, "\u00e9\U0001f600", 24, "\x1b[0;;233:128512u")]
    [DataRow(Hex1bKey.None, "\u00fb", 8, "\x1b[0u")]
    [DataRow(Hex1bKey.None, "\u00fb", 24, "\x1b[0;;251u")]
    [DataRow(Hex1bKey.A, "\0\a\t\r\x7f\u0085a\u009fb", 24, "\x1b[97;;97:98u")]
    public void Encode_AssociatedText_UsesScalarValuesWithoutInventingIdentity(
        Hex1bKey key, string text, int flags, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(new(key, text, Hex1bModifiers.None), Modes(flags)));
    }

    [TestMethod]
    public void Encode_ControlJWithAssociatedText_PreservesPrintableText()
    {
        Assert.AreEqual("\x1b[106;5;106u", TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.J, "j", Hex1bModifiers.Control), Modes(24)));
    }

    [TestMethod]
    [DataRow(57441, Hex1bModifiers.Shift, Hex1bKeyEventType.Press, 8, "\x1b[57441;2u")]
    [DataRow(57442, Hex1bModifiers.Control, Hex1bKeyEventType.Press, 8, "\x1b[57442;5u")]
    [DataRow(57442, Hex1bModifiers.None, Hex1bKeyEventType.Release, 10, "\x1b[57442;1:3u")]
    [DataRow(57442, Hex1bModifiers.Control, Hex1bKeyEventType.Release, 10, "\x1b[57442;5:3u")]
    [DataRow(57441, Hex1bModifiers.Shift, Hex1bKeyEventType.Press, 3, "")]
    [DataRow(60000, Hex1bModifiers.None, Hex1bKeyEventType.Press, 8, "\x1b[60000u")]
    [DataRow(57376, Hex1bModifiers.None, Hex1bKeyEventType.Press, 8, "\x1b[57376u")]
    [DataRow(57398, Hex1bModifiers.None, Hex1bKeyEventType.Press, 8, "\x1b[57398u")]
    [DataRow(57414, Hex1bModifiers.None, Hex1bKeyEventType.Press, 8, "\x1b[57414u")]
    [DataRow(57428, Hex1bModifiers.None, Hex1bKeyEventType.Press, 8, "\x1b[57428u")]
    [DataRow(57454, Hex1bModifiers.None, Hex1bKeyEventType.Press, 8, "\x1b[57454u")]
    public void Encode_RawFunctionalIdentities_PreservesCallerModifierState(
        int code, Hex1bModifiers modifiers, Hex1bKeyEventType kind, int flags, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.None, "", modifiers) { UnicodeKeyCode = code, EventType = kind }, Modes(flags)));
    }

    [TestMethod]
    [DataRow("\x1b[97u", Hex1bKey.A, 0, Hex1bKeyEventType.Press)]
    [DataRow("\x1b[97;256:2u", Hex1bKey.A, 255, Hex1bKeyEventType.Repeat)]
    [DataRow("\x1b[13;5:3~", Hex1bKey.F3, 4, Hex1bKeyEventType.Release)]
    [DataRow("\x1b[1;9:2A", Hex1bKey.UpArrow, 8, Hex1bKeyEventType.Repeat)]
    [DataRow("\x1b[D", Hex1bKey.LeftArrow, 0, Hex1bKeyEventType.Press)]
    [DataRow("\x1b[1P", Hex1bKey.F1, 0, Hex1bKeyEventType.Press)]
    [DataRow("\x1b[1;17Q", Hex1bKey.F2, 16, Hex1bKeyEventType.Press)]
    [DataRow("\x1b[1;129S", Hex1bKey.F4, 128, Hex1bKeyEventType.Press)]
    [DataRow("\x1b[57400u", Hex1bKey.NumPad1, 0, Hex1bKeyEventType.Press)]
    [DataRow("\x1b[60000u", Hex1bKey.None, 0, Hex1bKeyEventType.Press)]
    [DataRow("\x1b[0u", Hex1bKey.None, 0, Hex1bKeyEventType.Press)]
    public void Decode_LiteralWire_PreservesKeyModifiersAndKind(
        string wire, Hex1bKey key, int modifiers, Hex1bKeyEventType kind)
    {
        Assert.IsTrue(KittyKeyboardInput.TryDecode(wire, out var input));
        Assert.IsNotNull(input);
        Assert.AreEqual(key, input.Key);
        Assert.AreEqual((Hex1bModifiers)modifiers, input.Modifiers);
        Assert.AreEqual(kind, input.EventType);
        Assert.AreEqual("", input.Text);
    }

    [TestMethod]
    public void Decode_LayoutAndText_PreservesIndependentFields()
    {
        Assert.IsTrue(KittyKeyboardInput.TryDecode("\x1b[1089:1057:99;6:2;233:128512u", out var input));
        Assert.IsNotNull(input);
        Assert.AreEqual(1089, input.UnicodeKeyCode);
        Assert.AreEqual(1057, input.ShiftedKeyCode);
        Assert.AreEqual(99, input.BaseLayoutKeyCode);
        Assert.AreEqual("\u00e9\U0001f600", input.Text);
        Assert.AreEqual(Hex1bKey.None, input.Key);
        Assert.IsTrue(KittyKeyboardInput.TryDecode("\x1b[0;;97u", out var textOnly));
        Assert.IsNotNull(textOnly);
        Assert.AreEqual(Hex1bKey.None, textOnly.Key);
        Assert.AreEqual(0, textOnly.UnicodeKeyCode);
        Assert.IsTrue(KittyKeyboardInput.TryDecode("\x1b[97::113u", out var baseOnly));
        Assert.IsNotNull(baseOnly);
        Assert.IsNull(baseOnly.ShiftedKeyCode);
        Assert.AreEqual(113, baseOnly.BaseLayoutKeyCode);
    }

    [TestMethod]
    [DataRow("\x1b[u")]
    [DataRow("\x1b[?1u")]
    [DataRow("\x1b[>1u")]
    [DataRow("\x1b[-1u")]
    [DataRow("\x1b[1114112u")]
    [DataRow("\x1b[55296u")]
    [DataRow("\x1b[97:55296u")]
    [DataRow("\x1b[97:::99u")]
    [DataRow("\x1b[97;0u")]
    [DataRow("\x1b[97;257u")]
    [DataRow("\x1b[97;1:0u")]
    [DataRow("\x1b[97;1:4u")]
    [DataRow("\x1b[97;1:2:3u")]
    [DataRow("\x1b[97;;u")]
    [DataRow("\x1b[97;;31u")]
    [DataRow("\x1b[97;;127u")]
    [DataRow("\x1b[97;;159u")]
    [DataRow("\x1b[97;;55296u")]
    [DataRow("\x1b[97;;65;66u")]
    [DataRow("\x1b[2:3~")]
    [DataRow("\x1b[99~")]
    [DataRow("\x1b[2A")]
    [DataRow("\x1b[1;2;97A")]
    [DataRow("\x1b[2147483648u")]
    [DataRow("\x1b[97;1:2")]
    public void Decode_MalformedWire_RejectsWithoutPartialEvent(string wire)
    {
        Assert.IsFalse(KittyKeyboardInput.TryDecode(wire, out var input));
        Assert.IsNull(input);
    }

    [TestMethod]
    public void Tokenize_EnhancedInput_PreservesFieldsAndCursorReports()
    {
        foreach (var wire in new[] { "\x1b[97:65;6:2;65u", "\x1b[1;65P", "\x1b[13;1:3~", "\x1b[1;5R" })
        {
            var token = TestSeq.IsType<UnrecognizedSequenceToken>(TestSeq.Single(AnsiTokenizer.TokenizeEnhancedInput(wire)));
            Assert.AreEqual(wire, token.Sequence);
        }
        Assert.IsTrue(KittyKeyboardInput.IsCursorPositionReport("\x1b[1;5R"));
        Assert.IsFalse(KittyKeyboardInput.TryDecode("\x1b[1;5R", out _));
        Assert.IsTrue(KittyKeyboardInput.TryDecode("\x1b[13;5~", out var f3));
        Assert.AreEqual(Hex1bKey.F3, f3!.Key);
        TestSeq.IsType<SpecialKeyToken>(TestSeq.Single(AnsiTokenizer.Tokenize("\x1b[1;5R")));
        TestSeq.IsType<RestoreCursorToken>(TestSeq.Single(AnsiTokenizer.Tokenize("\x1b[u")));
        TestSeq.IsType<CursorMoveToken>(TestSeq.Single(AnsiTokenizer.Tokenize("\x1b[A")));
    }

    [TestMethod]
    public void Encode_InvalidScalarOrEvent_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.None, "", Hex1bModifiers.None) { UnicodeKeyCode = 55296 }, Modes(8)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.A, "", Hex1bModifiers.None) { EventType = (Hex1bKeyEventType)99 }, Modes(8)));
    }

    [TestMethod]
    [DataRow(Hex1bModifiers.Super)]
    [DataRow(Hex1bModifiers.Hyper)]
    [DataRow(Hex1bModifiers.Meta)]
    [DataRow(Hex1bModifiers.CapsLock)]
    [DataRow(Hex1bModifiers.NumLock)]
    public void Encode_LegacyExtendedModifiers_PreservesExistingProfile(Hex1bModifiers modifier)
    {
        Assert.AreEqual("\x1b\x01", TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.A, "A", modifier | Hex1bModifiers.Control | Hex1bModifiers.Alt), Modes(0)));
        Assert.AreEqual("\x1b[1;5R", TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.F3, "", modifier | Hex1bModifiers.Control), Modes(0)));
    }

    [TestMethod]
    public void Encode_KeypadBeginAndControlAlternates_UsesCanonicalFields()
    {
        Assert.AreEqual("\x1b[57427~", TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.None, "", Hex1bModifiers.None) { UnicodeKeyCode = 57427 }, Modes(8)));
        Assert.AreEqual("\x1b[97;6u", TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.A, "", Hex1bModifiers.Shift | Hex1bModifiers.Control)
            { ShiftedKeyCode = 1, BaseLayoutKeyCode = 0 }, Modes(5)));
        Assert.IsTrue(KittyKeyboardInput.TryDecode("\x1b[E", out var begin));
        Assert.AreEqual(57427, begin!.UnicodeKeyCode);
    }

    [TestMethod]
    [DataRow(32, "\x03")]
    [DataRow(64, "\x03")]
    [DataRow(96, "\x03")]
    [DataRow(33, "\x1b[99;5u")]
    [DataRow(127, "\x1b[99;5;99u")]
    public void Encode_ReservedFlags_DoNotChangeKnownFlags(int flags, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.C, "c", Hex1bModifiers.Control), Modes(flags)));
    }

    [TestMethod]
    [DataRow(Hex1bKey.UpArrow, "\x1b[A")]
    [DataRow(Hex1bKey.F3, "\x1b[13~")]
    [DataRow(Hex1bKey.Enter, "\x1b[13u")]
    [DataRow(Hex1bKey.Backspace, "\x1b[127u")]
    public void Encode_FunctionalKeyWithStaleText_DoesNotAppendInvalidTextField(Hex1bKey key, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new(key, "a", Hex1bModifiers.None), Modes(24)));
    }

    [TestMethod]
    [DataRow(6, "\x1b[97:65:113;6:2u")]
    [DataRow(7, "\x1b[97:65:113;6:2u")]
    [DataRow(12, "\x1b[97:65:113;6u")]
    [DataRow(14, "\x1b[97:65:113;6:2u")]
    [DataRow(15, "\x1b[97:65:113;6:2u")]
    [DataRow(28, "\x1b[97:65:113;6;65u")]
    [DataRow(30, "\x1b[97:65:113;6:2;65u")]
    [DataRow(31, "\x1b[97:65:113;6:2;65u")]
    public void Encode_CombinedEnhancements_KeepIndependentFields(int flags, string expected)
    {
        Assert.AreEqual(expected, TerminalInputEncoder.EncodeKey(
            new(Hex1bKey.A, "A", Hex1bModifiers.Control | Hex1bModifiers.Shift)
            { ShiftedKeyCode = 65, BaseLayoutKeyCode = 113, EventType = Hex1bKeyEventType.Repeat }, Modes(flags)));
    }

    [TestMethod]
    public void Encode_KeypadTextAndApplicationKeypad_RespectsNegotiatedIdentity()
    {
        var keypad = new Hex1bKeyEvent(Hex1bKey.NumPad1, "1", Hex1bModifiers.None);
        Assert.AreEqual("\x1bOq", TerminalInputEncoder.EncodeKey(keypad, Modes(0) with { ApplicationKeypad = true }));
        Assert.AreEqual("\x1b[57400u", TerminalInputEncoder.EncodeKey(keypad, Modes(1) with { ApplicationKeypad = true }));
        Assert.AreEqual("\x1b[57400;;49u", TerminalInputEncoder.EncodeKey(keypad, Modes(24)));
        Assert.AreEqual("\x1b[57400;;49u", TerminalInputEncoder.EncodeKey(
            keypad with { Key = Hex1bKey.None, UnicodeKeyCode = 57400 }, Modes(24)));
    }

    private static TerminalInputModes Modes(int flags, bool cursorMode = false)
        => new(cursorMode, false, false, TerminalMouseTracking.None, TerminalMouseEncoding.Sgr, 80, 24, flags);
}
