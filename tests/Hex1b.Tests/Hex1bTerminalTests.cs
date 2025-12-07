namespace Hex1b.Tests;

/// <summary>
/// Tests for the Hex1bTerminal virtual terminal emulator.
/// </summary>
public class Hex1bTerminalTests
{
    [Fact]
    public void Constructor_InitializesWithCorrectDimensions()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        
        Assert.Equal(80, terminal.Width);
        Assert.Equal(24, terminal.Height);
    }

    [Fact]
    public void Constructor_InitializesWithEmptyScreen()
    {
        using var terminal = new Hex1bTerminal(10, 5);
        
        var line = terminal.GetLineTrimmed(0);
        Assert.Equal("", line);
    }

    [Fact]
    public void Write_PlacesTextAtCursor()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        
        terminal.Write("Hello");
        
        Assert.Equal("Hello", terminal.GetLineTrimmed(0));
        Assert.Equal(5, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
    }

    [Fact]
    public void Write_HandlesNewlines()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        
        terminal.Write("Line1\nLine2\nLine3");
        
        Assert.Equal("Line1", terminal.GetLineTrimmed(0));
        Assert.Equal("Line2", terminal.GetLineTrimmed(1));
        Assert.Equal("Line3", terminal.GetLineTrimmed(2));
    }

    [Fact]
    public void Write_WrapsAtEndOfLine()
    {
        using var terminal = new Hex1bTerminal(5, 3);
        
        terminal.Write("HelloWorld");
        
        Assert.Equal("Hello", terminal.GetLineTrimmed(0));
        Assert.Equal("World", terminal.GetLineTrimmed(1));
    }

    [Fact]
    public void Clear_ResetsScreenAndCursor()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        terminal.Write("Some text");
        
        terminal.Clear();
        
        Assert.Equal("", terminal.GetLineTrimmed(0));
        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
    }

    [Fact]
    public void SetCursorPosition_MovesCursor()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        
        terminal.SetCursorPosition(5, 2);
        terminal.Write("X");
        
        var line = terminal.GetLine(2);
        Assert.Equal('X', line[5]);
    }

    [Fact]
    public void SetCursorPosition_ClampsToBounds()
    {
        using var terminal = new Hex1bTerminal(10, 5);
        
        terminal.SetCursorPosition(100, 100);
        
        Assert.Equal(9, terminal.CursorX);
        Assert.Equal(4, terminal.CursorY);
    }

    [Fact]
    public void EnterAlternateScreen_SetsFlag()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        
        Assert.False(terminal.InAlternateScreen);
        
        terminal.EnterAlternateScreen();
        
        Assert.True(terminal.InAlternateScreen);
    }

    [Fact]
    public void ExitAlternateScreen_ClearsFlag()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        terminal.EnterAlternateScreen();
        
        terminal.ExitAlternateScreen();
        
        Assert.False(terminal.InAlternateScreen);
    }

    [Fact]
    public void ContainsText_FindsText()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        terminal.Write("Hello World");
        
        Assert.True(terminal.ContainsText("World"));
        Assert.False(terminal.ContainsText("Foo"));
    }

    [Fact]
    public void FindText_ReturnsPositions()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        terminal.Write("Hello World\nHello Again");
        
        var results = terminal.FindText("Hello");
        
        Assert.Equal(2, results.Count);
        Assert.Equal((0, 0), results[0]);
        Assert.Equal((1, 0), results[1]);
    }

    [Fact]
    public void GetNonEmptyLines_FiltersEmptyLines()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        terminal.Write("Line 1\n\nLine 3");
        
        var lines = terminal.GetNonEmptyLines().ToList();
        
        Assert.Equal(2, lines.Count);
        Assert.Equal("Line 1", lines[0]);
        Assert.Equal("Line 3", lines[1]);
    }

    [Fact]
    public async Task SendKeyAsync_InjectsInputEvent()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        
        await terminal.SendKeyAsync(ConsoleKey.A, 'a');
        terminal.CompleteInput();
        
        var events = new List<Hex1bInputEvent>();
        await foreach (var evt in terminal.InputEvents.ReadAllAsync())
        {
            events.Add(evt);
        }
        
        Assert.Single(events);
        var keyEvent = Assert.IsType<KeyInputEvent>(events[0]);
        Assert.Equal(ConsoleKey.A, keyEvent.Key);
        Assert.Equal('a', keyEvent.KeyChar);
    }

    [Fact]
    public async Task TypeTextAsync_InjectsMultipleEvents()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        
        await terminal.TypeTextAsync("abc");
        terminal.CompleteInput();
        
        var events = new List<Hex1bInputEvent>();
        await foreach (var evt in terminal.InputEvents.ReadAllAsync())
        {
            events.Add(evt);
        }
        
        Assert.Equal(3, events.Count);
    }

    [Fact]
    public void Resize_PreservesContent()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        terminal.Write("Hello");
        
        terminal.Resize(40, 10);
        
        Assert.Equal(40, terminal.Width);
        Assert.Equal(10, terminal.Height);
        Assert.Equal("Hello", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void RawOutput_CapturesEverything()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        
        terminal.Write("Hello\x1b[31mRed\x1b[0mNormal");
        
        var raw = terminal.RawOutput;
        Assert.Contains("\x1b[31m", raw);
        Assert.Contains("\x1b[0m", raw);
    }

    [Fact]
    public void AnsiSequences_AreProcessedButNotDisplayed()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        
        terminal.Write("\x1b[31mRed Text\x1b[0m");
        
        Assert.Equal("Red Text", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void AnsiCursorPosition_MovesCursor()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        
        // ANSI positions are 1-based, so row 2, col 5
        terminal.Write("\x1b[2;5HX");
        
        var line = terminal.GetLine(1); // 0-based
        Assert.Equal('X', line[4]); // 0-based
    }

    [Fact]
    public void AnsiClearScreen_ClearsBuffer()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        terminal.Write("Some content");
        
        terminal.Write("\x1b[2J");
        
        Assert.Equal("", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void GetScreenBuffer_ReturnsCopyWithColors()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        terminal.Write("\x1b[38;2;255;0;0mR\x1b[0m");
        
        var buffer = terminal.GetScreenBuffer();
        
        Assert.Equal('R', buffer[0, 0].Character);
        Assert.NotNull(buffer[0, 0].Foreground);
        Assert.Equal(255, buffer[0, 0].Foreground!.Value.R);
        Assert.Equal(0, buffer[0, 0].Foreground!.Value.G);
        Assert.Equal(0, buffer[0, 0].Foreground!.Value.B);
    }

    [Fact]
    public void AlternateScreenAnsiSequence_IsRecognized()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        
        terminal.Write("\x1b[?1049h");
        Assert.True(terminal.InAlternateScreen);
        
        terminal.Write("\x1b[?1049l");
        Assert.False(terminal.InAlternateScreen);
    }
}
