using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for input sequence tokenization using the unified AnsiTokenizer.
/// These sequences are typically received from the terminal (keyboard/mouse input).
/// </summary>
[TestClass]
public class InputTokenizerTests
{
    // === SS3 Sequences (ESC O) ===
    
    [TestMethod]
    [DataRow("P", 'P')] // F1
    [DataRow("Q", 'Q')] // F2
    [DataRow("R", 'R')] // F3
    [DataRow("S", 'S')] // F4
    [DataRow("A", 'A')] // Up arrow (application mode)
    [DataRow("B", 'B')] // Down arrow (application mode)
    [DataRow("C", 'C')] // Right arrow (application mode)
    [DataRow("D", 'D')] // Left arrow (application mode)
    [DataRow("H", 'H')] // Home (application mode)
    [DataRow("F", 'F')] // End (application mode)
    public void Ss3Sequence_ParsesCorrectly(string charPart, char expectedChar)
    {
        var input = $"\x1bO{charPart}";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var ss3Token = TestSeq.Single(tokens);
        var token = TestSeq.IsType<Ss3Token>(ss3Token);
        Assert.AreEqual(expectedChar, token.Character);
    }
    
    [TestMethod]
    public void Ss3Sequence_Serializes_RoundTrip()
    {
        var original = "\x1bOP"; // F1
        var tokens = AnsiTokenizer.Tokenize(original);
        var serialized = AnsiTokenSerializer.Serialize(tokens);
        Assert.AreEqual(original, serialized);
    }
    
    // === Special Key Sequences (ESC [ n ~) ===
    
    [TestMethod]
    [DataRow("2", 2, 1)]   // Insert
    [DataRow("3", 3, 1)]   // Delete
    [DataRow("5", 5, 1)]   // Page Up
    [DataRow("6", 6, 1)]   // Page Down
    [DataRow("15", 15, 1)] // F5
    [DataRow("17", 17, 1)] // F6
    [DataRow("18", 18, 1)] // F7
    [DataRow("19", 19, 1)] // F8
    [DataRow("20", 20, 1)] // F9
    [DataRow("21", 21, 1)] // F10
    [DataRow("23", 23, 1)] // F11
    [DataRow("24", 24, 1)] // F12
    public void SpecialKeySequence_ParsesCorrectly(string keyCode, int expectedCode, int expectedMod)
    {
        var input = $"\x1b[{keyCode}~";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var keyToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SpecialKeyToken>(keyToken);
        Assert.AreEqual(expectedCode, token.KeyCode);
        Assert.AreEqual(expectedMod, token.Modifiers);
    }
    
    [TestMethod]
    [DataRow("3;2", 3, 2)]  // Delete with Shift
    [DataRow("3;5", 3, 5)]  // Delete with Ctrl
    [DataRow("15;3", 15, 3)] // F5 with Alt
    public void SpecialKeySequence_WithModifiers_ParsesCorrectly(string params_, int expectedCode, int expectedMod)
    {
        var input = $"\x1b[{params_}~";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var keyToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SpecialKeyToken>(keyToken);
        Assert.AreEqual(expectedCode, token.KeyCode);
        Assert.AreEqual(expectedMod, token.Modifiers);
    }
    
    [TestMethod]
    public void SpecialKeySequence_Serializes_RoundTrip()
    {
        var original = "\x1b[15~"; // F5
        var tokens = AnsiTokenizer.Tokenize(original);
        var serialized = AnsiTokenSerializer.Serialize(tokens);
        Assert.AreEqual(original, serialized);
    }
    
    [TestMethod]
    public void SpecialKeySequence_WithModifiers_Serializes_RoundTrip()
    {
        var original = "\x1b[3;5~"; // Delete with Ctrl
        var tokens = AnsiTokenizer.Tokenize(original);
        var serialized = AnsiTokenSerializer.Serialize(tokens);
        Assert.AreEqual(original, serialized);
    }
    
    // === Modified Home/End (xterm format: CSI 1;m H / CSI 1;m F) ===
    
    [TestMethod]
    [DataRow(2, Hex1bModifiers.Shift)]             // Shift+End
    [DataRow(5, Hex1bModifiers.Control)]            // Ctrl+End
    [DataRow(6, Hex1bModifiers.Control | Hex1bModifiers.Shift)] // Ctrl+Shift+End
    public void ModifiedEnd_XtermFormat_ParsesAsSpecialKeyWithModifiers(int modCode, Hex1bModifiers expectedMod)
    {
        // CSI 1;{mod}F = End with modifiers (xterm)
        var input = $"\x1b[1;{modCode}F";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var keyToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SpecialKeyToken>(keyToken);
        Assert.AreEqual(4, token.KeyCode); // 4 = End
        Assert.AreEqual(modCode, token.Modifiers);
        
        // Verify the modifier decodes correctly through SpecialKeyTokenToKeyEvent
        // (indirectly: modifier code - 1 gives bits where bit0=Shift, bit1=Alt, bit2=Ctrl)
        var decodedBits = modCode - 1;
        if (expectedMod.HasFlag(Hex1bModifiers.Shift)) Assert.AreNotEqual(0, decodedBits & 1);
        if (expectedMod.HasFlag(Hex1bModifiers.Control)) Assert.AreNotEqual(0, decodedBits & 4);
    }
    
    [TestMethod]
    public void PlainEnd_CsiF_ParsesAsCursorMove()
    {
        // CSI F (no params) = Cursor Previous Line = End key
        var input = "\x1b[F";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var token = TestSeq.Single(tokens);
        TestSeq.IsType<CursorMoveToken>(token);
    }

    // === Modified F1-F4 (xterm "PC keyboard" format: CSI 1;m P/Q/R/S) ===
    //
    // Plain F1-F4 use SS3 (ESC O P/Q/R/S). When held with a modifier, xterm and modern
    // terminals (Windows Terminal via WindowsConsoleDriver, iTerm2, GNOME Terminal, etc.)
    // emit the CSI form ESC [ 1 ; {mod} <P/Q/R/S>. The corresponding F-key codes for
    // SpecialKeyToken are 11=F1, 12=F2, 13=F3, 14=F4 (matching the CSI ~ form).

    [TestMethod]
    [DataRow('P', 11)] // F1
    [DataRow('Q', 12)] // F2
    [DataRow('R', 13)] // F3
    [DataRow('S', 14)] // F4
    public void ModifiedFunctionKey_F1ThroughF4_XtermFormat_ParsesAsSpecialKey(char terminator, int expectedCode)
    {
        // CSI 1;5{P/Q/R/S} = F1-F4 with Ctrl
        var input = $"\x1b[1;5{terminator}";
        var tokens = AnsiTokenizer.Tokenize(input);

        var token = TestSeq.Single(tokens);
        var special = TestSeq.IsType<SpecialKeyToken>(token);
        Assert.AreEqual(expectedCode, special.KeyCode);
        Assert.AreEqual(5, special.Modifiers); // 5 = Ctrl
    }

    [TestMethod]
    [DataRow(2)]  // Shift
    [DataRow(3)]  // Alt
    [DataRow(4)]  // Alt+Shift
    [DataRow(5)]  // Ctrl
    [DataRow(6)]  // Ctrl+Shift
    [DataRow(7)]  // Alt+Ctrl
    [DataRow(8)]  // Alt+Ctrl+Shift
    public void ModifiedFunctionKey_F1_AllXtermModifiers_Parse(int modCode)
    {
        var input = $"\x1b[1;{modCode}P";
        var tokens = AnsiTokenizer.Tokenize(input);

        var token = TestSeq.Single(tokens);
        var special = TestSeq.IsType<SpecialKeyToken>(token);
        Assert.AreEqual(11, special.KeyCode); // F1
        Assert.AreEqual(modCode, special.Modifiers);
    }

    [TestMethod]
    public void PlainCsiP_StillParsesAsDeleteCharacter()
    {
        // CSI P (no params or single param) = DCH; must NOT be misinterpreted as F1
        var tokens = AnsiTokenizer.Tokenize("\x1b[P");
        TestSeq.IsType<DeleteCharacterToken>(TestSeq.Single(tokens));

        var tokens2 = AnsiTokenizer.Tokenize("\x1b[3P");
        var dch = TestSeq.IsType<DeleteCharacterToken>(TestSeq.Single(tokens2));
        Assert.AreEqual(3, dch.Count);
    }

    [TestMethod]
    public void PlainCsiS_StillParsesAsScrollUp()
    {
        // CSI S (no params or single param) = SU; must NOT be misinterpreted as F4
        var tokens = AnsiTokenizer.Tokenize("\x1b[S");
        TestSeq.IsType<ScrollUpToken>(TestSeq.Single(tokens));

        var tokens2 = AnsiTokenizer.Tokenize("\x1b[2S");
        var su = TestSeq.IsType<ScrollUpToken>(TestSeq.Single(tokens2));
        Assert.AreEqual(2, su.Count);
    }

    // === SGR Mouse Sequences (ESC [ < Cb ; Cx ; Cy M/m) ===
    
    [TestMethod]
    public void SgrMouse_LeftClick_ParsesCorrectly()
    {
        var input = "\x1b[<0;10;5M"; // Left button press at (10,5) - 1-based
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SgrMouseToken>(mouseToken);
        Assert.AreEqual(MouseButton.Left, token.Button);
        Assert.AreEqual(MouseAction.Down, token.Action);
        Assert.AreEqual(9, token.X);  // 0-based
        Assert.AreEqual(4, token.Y);  // 0-based
        Assert.AreEqual(Hex1bModifiers.None, token.Modifiers);
    }
    
    [TestMethod]
    public void SgrMouse_LeftRelease_ParsesCorrectly()
    {
        var input = "\x1b[<0;10;5m"; // Left button release at (10,5)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SgrMouseToken>(mouseToken);
        Assert.AreEqual(MouseButton.Left, token.Button);
        Assert.AreEqual(MouseAction.Up, token.Action);
        Assert.AreEqual(9, token.X);
        Assert.AreEqual(4, token.Y);
    }
    
    [TestMethod]
    public void SgrMouse_RightClick_ParsesCorrectly()
    {
        var input = "\x1b[<2;1;1M"; // Right button press
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SgrMouseToken>(mouseToken);
        Assert.AreEqual(MouseButton.Right, token.Button);
        Assert.AreEqual(MouseAction.Down, token.Action);
    }
    
    [TestMethod]
    public void SgrMouse_MiddleClick_ParsesCorrectly()
    {
        var input = "\x1b[<1;1;1M"; // Middle button press
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SgrMouseToken>(mouseToken);
        Assert.AreEqual(MouseButton.Middle, token.Button);
    }
    
    [TestMethod]
    public void SgrMouse_ScrollUp_ParsesCorrectly()
    {
        var input = "\x1b[<64;1;1M"; // Scroll up
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SgrMouseToken>(mouseToken);
        Assert.AreEqual(MouseButton.ScrollUp, token.Button);
    }
    
    [TestMethod]
    public void SgrMouse_ScrollDown_ParsesCorrectly()
    {
        var input = "\x1b[<65;1;1M"; // Scroll down
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SgrMouseToken>(mouseToken);
        Assert.AreEqual(MouseButton.ScrollDown, token.Button);
    }
    
    [TestMethod]
    public void SgrMouse_WithShift_ParsesCorrectly()
    {
        var input = "\x1b[<4;1;1M"; // Left + Shift (0 + 4)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SgrMouseToken>(mouseToken);
        Assert.AreEqual(MouseButton.Left, token.Button);
        Assert.IsTrue(token.Modifiers.HasFlag(Hex1bModifiers.Shift));
    }
    
    [TestMethod]
    public void SgrMouse_WithAlt_ParsesCorrectly()
    {
        var input = "\x1b[<8;1;1M"; // Left + Alt (0 + 8)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SgrMouseToken>(mouseToken);
        Assert.AreEqual(MouseButton.Left, token.Button);
        Assert.IsTrue(token.Modifiers.HasFlag(Hex1bModifiers.Alt));
    }
    
    [TestMethod]
    public void SgrMouse_WithCtrl_ParsesCorrectly()
    {
        var input = "\x1b[<16;1;1M"; // Left + Ctrl (0 + 16)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SgrMouseToken>(mouseToken);
        Assert.AreEqual(MouseButton.Left, token.Button);
        Assert.IsTrue(token.Modifiers.HasFlag(Hex1bModifiers.Control));
    }
    
    [TestMethod]
    public void SgrMouse_Motion_ParsesCorrectly()
    {
        var input = "\x1b[<35;10;20M"; // Motion with no button (32 + 3=none)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SgrMouseToken>(mouseToken);
        Assert.AreEqual(MouseAction.Move, token.Action);
    }
    
    [TestMethod]
    public void SgrMouse_Drag_ParsesCorrectly()
    {
        var input = "\x1b[<32;10;20M"; // Drag with left button (32 + 0)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<SgrMouseToken>(mouseToken);
        Assert.AreEqual(MouseButton.Left, token.Button);
        Assert.AreEqual(MouseAction.Drag, token.Action);
    }
    
    [TestMethod]
    public void SgrMouse_Serializes_RoundTrip()
    {
        var original = "\x1b[<0;10;5M";
        var tokens = AnsiTokenizer.Tokenize(original);
        var serialized = AnsiTokenSerializer.Serialize(tokens);
        Assert.AreEqual(original, serialized);
    }
    
    // === Arrow Keys (CSI A/B/C/D) ===
    // These already work via CursorMoveToken but let's verify they still work
    
    [TestMethod]
    [DataRow("A", CursorMoveDirection.Up)]
    [DataRow("B", CursorMoveDirection.Down)]
    [DataRow("C", CursorMoveDirection.Forward)]
    [DataRow("D", CursorMoveDirection.Back)]
    public void ArrowKeys_ParseAsCursorMove(string direction, CursorMoveDirection expected)
    {
        var input = $"\x1b[{direction}";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var moveToken = TestSeq.Single(tokens);
        var token = TestSeq.IsType<CursorMoveToken>(moveToken);
        Assert.AreEqual(expected, token.Direction);
        Assert.AreEqual(1, token.Count);
    }
    
    // === Mixed Input ===
    
    [TestMethod]
    public void MixedInput_ParsesAllTokens()
    {
        // Simulate typing "hello" then pressing up arrow then clicking
        var input = "hello\x1b[A\x1b[<0;5;3M";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        Assert.AreEqual(3, tokens.Count);
        TestSeq.IsType<TextToken>(tokens[0]);
        Assert.AreEqual("hello", ((TextToken)tokens[0]).Text);
        TestSeq.IsType<CursorMoveToken>(tokens[1]);
        TestSeq.IsType<SgrMouseToken>(tokens[2]);
    }
}
