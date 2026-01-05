using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for input sequence tokenization using the unified AnsiTokenizer.
/// These sequences are typically received from the terminal (keyboard/mouse input).
/// </summary>
public class InputTokenizerTests
{
    // === SS3 Sequences (ESC O) ===
    
    [Theory]
    [InlineData("P", 'P')] // F1
    [InlineData("Q", 'Q')] // F2
    [InlineData("R", 'R')] // F3
    [InlineData("S", 'S')] // F4
    [InlineData("A", 'A')] // Up arrow (application mode)
    [InlineData("B", 'B')] // Down arrow (application mode)
    [InlineData("C", 'C')] // Right arrow (application mode)
    [InlineData("D", 'D')] // Left arrow (application mode)
    [InlineData("H", 'H')] // Home (application mode)
    [InlineData("F", 'F')] // End (application mode)
    public void Ss3Sequence_ParsesCorrectly(string charPart, char expectedChar)
    {
        var input = $"\x1bO{charPart}";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var ss3Token = Assert.Single(tokens);
        var token = Assert.IsType<Ss3Token>(ss3Token);
        Assert.Equal(expectedChar, token.Character);
    }
    
    [Fact]
    public void Ss3Sequence_Serializes_RoundTrip()
    {
        var original = "\x1bOP"; // F1
        var tokens = AnsiTokenizer.Tokenize(original);
        var serialized = AnsiTokenSerializer.Serialize(tokens);
        Assert.Equal(original, serialized);
    }
    
    // === Special Key Sequences (ESC [ n ~) ===
    
    [Theory]
    [InlineData("2", 2, 1)]   // Insert
    [InlineData("3", 3, 1)]   // Delete
    [InlineData("5", 5, 1)]   // Page Up
    [InlineData("6", 6, 1)]   // Page Down
    [InlineData("15", 15, 1)] // F5
    [InlineData("17", 17, 1)] // F6
    [InlineData("18", 18, 1)] // F7
    [InlineData("19", 19, 1)] // F8
    [InlineData("20", 20, 1)] // F9
    [InlineData("21", 21, 1)] // F10
    [InlineData("23", 23, 1)] // F11
    [InlineData("24", 24, 1)] // F12
    public void SpecialKeySequence_ParsesCorrectly(string keyCode, int expectedCode, int expectedMod)
    {
        var input = $"\x1b[{keyCode}~";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var keyToken = Assert.Single(tokens);
        var token = Assert.IsType<SpecialKeyToken>(keyToken);
        Assert.Equal(expectedCode, token.KeyCode);
        Assert.Equal(expectedMod, token.Modifiers);
    }
    
    [Theory]
    [InlineData("3;2", 3, 2)]  // Delete with Shift
    [InlineData("3;5", 3, 5)]  // Delete with Ctrl
    [InlineData("15;3", 15, 3)] // F5 with Alt
    public void SpecialKeySequence_WithModifiers_ParsesCorrectly(string params_, int expectedCode, int expectedMod)
    {
        var input = $"\x1b[{params_}~";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var keyToken = Assert.Single(tokens);
        var token = Assert.IsType<SpecialKeyToken>(keyToken);
        Assert.Equal(expectedCode, token.KeyCode);
        Assert.Equal(expectedMod, token.Modifiers);
    }
    
    [Fact]
    public void SpecialKeySequence_Serializes_RoundTrip()
    {
        var original = "\x1b[15~"; // F5
        var tokens = AnsiTokenizer.Tokenize(original);
        var serialized = AnsiTokenSerializer.Serialize(tokens);
        Assert.Equal(original, serialized);
    }
    
    [Fact]
    public void SpecialKeySequence_WithModifiers_Serializes_RoundTrip()
    {
        var original = "\x1b[3;5~"; // Delete with Ctrl
        var tokens = AnsiTokenizer.Tokenize(original);
        var serialized = AnsiTokenSerializer.Serialize(tokens);
        Assert.Equal(original, serialized);
    }
    
    // === SGR Mouse Sequences (ESC [ < Cb ; Cx ; Cy M/m) ===
    
    [Fact]
    public void SgrMouse_LeftClick_ParsesCorrectly()
    {
        var input = "\x1b[<0;10;5M"; // Left button press at (10,5) - 1-based
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = Assert.Single(tokens);
        var token = Assert.IsType<SgrMouseToken>(mouseToken);
        Assert.Equal(MouseButton.Left, token.Button);
        Assert.Equal(MouseAction.Down, token.Action);
        Assert.Equal(9, token.X);  // 0-based
        Assert.Equal(4, token.Y);  // 0-based
        Assert.Equal(Hex1bModifiers.None, token.Modifiers);
    }
    
    [Fact]
    public void SgrMouse_LeftRelease_ParsesCorrectly()
    {
        var input = "\x1b[<0;10;5m"; // Left button release at (10,5)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = Assert.Single(tokens);
        var token = Assert.IsType<SgrMouseToken>(mouseToken);
        Assert.Equal(MouseButton.Left, token.Button);
        Assert.Equal(MouseAction.Up, token.Action);
        Assert.Equal(9, token.X);
        Assert.Equal(4, token.Y);
    }
    
    [Fact]
    public void SgrMouse_RightClick_ParsesCorrectly()
    {
        var input = "\x1b[<2;1;1M"; // Right button press
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = Assert.Single(tokens);
        var token = Assert.IsType<SgrMouseToken>(mouseToken);
        Assert.Equal(MouseButton.Right, token.Button);
        Assert.Equal(MouseAction.Down, token.Action);
    }
    
    [Fact]
    public void SgrMouse_MiddleClick_ParsesCorrectly()
    {
        var input = "\x1b[<1;1;1M"; // Middle button press
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = Assert.Single(tokens);
        var token = Assert.IsType<SgrMouseToken>(mouseToken);
        Assert.Equal(MouseButton.Middle, token.Button);
    }
    
    [Fact]
    public void SgrMouse_ScrollUp_ParsesCorrectly()
    {
        var input = "\x1b[<64;1;1M"; // Scroll up
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = Assert.Single(tokens);
        var token = Assert.IsType<SgrMouseToken>(mouseToken);
        Assert.Equal(MouseButton.ScrollUp, token.Button);
    }
    
    [Fact]
    public void SgrMouse_ScrollDown_ParsesCorrectly()
    {
        var input = "\x1b[<65;1;1M"; // Scroll down
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = Assert.Single(tokens);
        var token = Assert.IsType<SgrMouseToken>(mouseToken);
        Assert.Equal(MouseButton.ScrollDown, token.Button);
    }
    
    [Fact]
    public void SgrMouse_WithShift_ParsesCorrectly()
    {
        var input = "\x1b[<4;1;1M"; // Left + Shift (0 + 4)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = Assert.Single(tokens);
        var token = Assert.IsType<SgrMouseToken>(mouseToken);
        Assert.Equal(MouseButton.Left, token.Button);
        Assert.True(token.Modifiers.HasFlag(Hex1bModifiers.Shift));
    }
    
    [Fact]
    public void SgrMouse_WithAlt_ParsesCorrectly()
    {
        var input = "\x1b[<8;1;1M"; // Left + Alt (0 + 8)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = Assert.Single(tokens);
        var token = Assert.IsType<SgrMouseToken>(mouseToken);
        Assert.Equal(MouseButton.Left, token.Button);
        Assert.True(token.Modifiers.HasFlag(Hex1bModifiers.Alt));
    }
    
    [Fact]
    public void SgrMouse_WithCtrl_ParsesCorrectly()
    {
        var input = "\x1b[<16;1;1M"; // Left + Ctrl (0 + 16)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = Assert.Single(tokens);
        var token = Assert.IsType<SgrMouseToken>(mouseToken);
        Assert.Equal(MouseButton.Left, token.Button);
        Assert.True(token.Modifiers.HasFlag(Hex1bModifiers.Control));
    }
    
    [Fact]
    public void SgrMouse_Motion_ParsesCorrectly()
    {
        var input = "\x1b[<35;10;20M"; // Motion with no button (32 + 3=none)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = Assert.Single(tokens);
        var token = Assert.IsType<SgrMouseToken>(mouseToken);
        Assert.Equal(MouseAction.Move, token.Action);
    }
    
    [Fact]
    public void SgrMouse_Drag_ParsesCorrectly()
    {
        var input = "\x1b[<32;10;20M"; // Drag with left button (32 + 0)
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var mouseToken = Assert.Single(tokens);
        var token = Assert.IsType<SgrMouseToken>(mouseToken);
        Assert.Equal(MouseButton.Left, token.Button);
        Assert.Equal(MouseAction.Drag, token.Action);
    }
    
    [Fact]
    public void SgrMouse_Serializes_RoundTrip()
    {
        var original = "\x1b[<0;10;5M";
        var tokens = AnsiTokenizer.Tokenize(original);
        var serialized = AnsiTokenSerializer.Serialize(tokens);
        Assert.Equal(original, serialized);
    }
    
    // === Arrow Keys (CSI A/B/C/D) ===
    // These already work via CursorMoveToken but let's verify they still work
    
    [Theory]
    [InlineData("A", CursorMoveDirection.Up)]
    [InlineData("B", CursorMoveDirection.Down)]
    [InlineData("C", CursorMoveDirection.Forward)]
    [InlineData("D", CursorMoveDirection.Back)]
    public void ArrowKeys_ParseAsCursorMove(string direction, CursorMoveDirection expected)
    {
        var input = $"\x1b[{direction}";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        var moveToken = Assert.Single(tokens);
        var token = Assert.IsType<CursorMoveToken>(moveToken);
        Assert.Equal(expected, token.Direction);
        Assert.Equal(1, token.Count);
    }
    
    // === Mixed Input ===
    
    [Fact]
    public void MixedInput_ParsesAllTokens()
    {
        // Simulate typing "hello" then pressing up arrow then clicking
        var input = "hello\x1b[A\x1b[<0;5;3M";
        var tokens = AnsiTokenizer.Tokenize(input);
        
        Assert.Equal(3, tokens.Count);
        Assert.IsType<TextToken>(tokens[0]);
        Assert.Equal("hello", ((TextToken)tokens[0]).Text);
        Assert.IsType<CursorMoveToken>(tokens[1]);
        Assert.IsType<SgrMouseToken>(tokens[2]);
    }
}
