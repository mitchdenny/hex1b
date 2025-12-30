using Hex1b.Input;
using Hex1b.Terminal.Automation;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the Hex1bTerminal virtual terminal emulator.
/// </summary>
public class Hex1bTerminalTests
{
    [Fact]
    public void Constructor_InitializesWithCorrectDimensions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        Assert.Equal(80, terminal.Width);
        Assert.Equal(24, terminal.Height);
    }

    [Fact]
    public void Constructor_InitializesWithEmptyScreen()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 10, 5);
        
        var line = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Equal("", line);
    }

    [Fact]
    public void Write_PlacesTextAtCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        
        workload.Write("Hello");
        
        Assert.Equal("Hello", terminal.CreateSnapshot().GetLineTrimmed(0));
        Assert.Equal(5, terminal.CreateSnapshot().CursorX);
        Assert.Equal(0, terminal.CreateSnapshot().CursorY);
    }

    [Fact]
    public void Write_HandlesNewlines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        
        workload.Write("Line1\nLine2\nLine3");
        
        Assert.Equal("Line1", terminal.CreateSnapshot().GetLineTrimmed(0));
        Assert.Equal("Line2", terminal.CreateSnapshot().GetLineTrimmed(1));
        Assert.Equal("Line3", terminal.CreateSnapshot().GetLineTrimmed(2));
    }

    [Fact]
    public void Write_WrapsAtEndOfLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 5, 3);
        
        workload.Write("HelloWorld");
        
        Assert.Equal("Hello", terminal.CreateSnapshot().GetLineTrimmed(0));
        Assert.Equal("World", terminal.CreateSnapshot().GetLineTrimmed(1));
    }

    [Fact]
    public void Clear_ResetsScreenAndCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        workload.Write("Some text");
        
        workload.Clear();
        
        Assert.Equal("", terminal.CreateSnapshot().GetLineTrimmed(0));
        Assert.Equal(0, terminal.CreateSnapshot().CursorX);
        Assert.Equal(0, terminal.CreateSnapshot().CursorY);
    }

    [Fact]
    public void SetCursorPosition_MovesCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        
        workload.SetCursorPosition(5, 2);
        workload.Write("X");
        
        var line = terminal.CreateSnapshot().GetLine(2);
        Assert.Equal('X', line[5]);
    }

    [Fact]
    public void SetCursorPosition_ClampsToBounds()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 10, 5);
        
        workload.SetCursorPosition(100, 100);
        
        Assert.Equal(9, terminal.CreateSnapshot().CursorX);
        Assert.Equal(4, terminal.CreateSnapshot().CursorY);
    }

    [Fact]
    public void EnterAlternateScreen_SetsFlag()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
        
        terminal.EnterAlternateScreen();
        
        Assert.True(terminal.CreateSnapshot().InAlternateScreen);
    }

    [Fact]
    public void ExitAlternateScreen_ClearsFlag()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        terminal.EnterAlternateScreen();
        
        terminal.ExitAlternateScreen();
        
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
    }

    [Fact]
    public void ContainsText_FindsText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        workload.Write("Hello World");
        
        Assert.True(terminal.CreateSnapshot().ContainsText("World"));
        Assert.False(terminal.CreateSnapshot().ContainsText("Foo"));
    }

    [Fact]
    public void FindText_ReturnsPositions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        workload.Write("Hello World\nHello Again");
        
        var results = terminal.CreateSnapshot().FindText("Hello");
        
        Assert.Equal(2, results.Count);
        Assert.Equal((0, 0), results[0]);
        Assert.Equal((1, 0), results[1]);
    }

    [Fact]
    public void GetNonEmptyLines_FiltersEmptyLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        workload.Write("Line 1\n\nLine 3");
        
        var lines = terminal.CreateSnapshot().GetNonEmptyLines().ToList();
        
        Assert.Equal(2, lines.Count);
        Assert.Equal("Line 1", lines[0]);
        Assert.Equal("Line 3", lines[1]);
    }

    [Fact]
    public void Resize_PreservesContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        workload.Write("Hello");
        
        terminal.Resize(40, 10);
        
        Assert.Equal(40, terminal.Width);
        Assert.Equal(10, terminal.Height);
        Assert.Equal("Hello", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void AnsiSequences_AreProcessedButNotDisplayed()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        
        workload.Write("\x1b[31mRed Text\x1b[0m");
        
        Assert.Equal("Red Text", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void AnsiCursorPosition_MovesCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        
        // ANSI positions are 1-based, so row 2, col 5
        workload.Write("\x1b[2;5HX");
        
        var line = terminal.CreateSnapshot().GetLine(1); // 0-based
        Assert.Equal('X', line[4]); // 0-based
    }

    [Fact]
    public void AnsiClearScreen_ClearsBuffer()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        workload.Write("Some content");
        
        workload.Write("\x1b[2J");
        
        Assert.Equal("", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void GetScreenBuffer_ReturnsCopyWithColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        workload.Write("\x1b[38;2;255;0;0mR\x1b[0m");
        
        var buffer = terminal.GetScreenBuffer();
        
        Assert.Equal("R", buffer[0, 0].Character);
        Assert.NotNull(buffer[0, 0].Foreground);
        Assert.Equal(255, buffer[0, 0].Foreground!.Value.R);
        Assert.Equal(0, buffer[0, 0].Foreground!.Value.G);
        Assert.Equal(0, buffer[0, 0].Foreground!.Value.B);
    }

    [Fact]
    public void AlternateScreenAnsiSequence_IsRecognized()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        
        workload.Write("\x1b[?1049h");
        Assert.True(terminal.CreateSnapshot().InAlternateScreen);
        
        workload.Write("\x1b[?1049l");
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
    }

    #region Resize Behavior

    [Fact]
    public void Constructor_SetsWorkloadDimensions()
    {
        // Workload dimensions are 0x0 before terminal is created
        using var workload = new Hex1bAppWorkloadAdapter();
        Assert.Equal(0, workload.Width);
        Assert.Equal(0, workload.Height);
        
        // Terminal sets workload dimensions during construction
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        Assert.Equal(80, workload.Width);
        Assert.Equal(24, workload.Height);
    }

    [Fact]
    public void Constructor_DoesNotFireResizeEvent()
    {
        // This is critical: the initial dimension setup should NOT fire a resize event
        // because that would trigger an extra re-render before the app even starts
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Try to read from input channel - should be empty (no resize event)
        var hasEvent = workload.InputEvents.TryRead(out var evt);
        Assert.False(hasEvent, "Constructor should not fire a resize event");
    }

    [Fact]
    public async Task ResizeAsync_AfterInitialization_FiresResizeEvent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Now call ResizeAsync again (simulating a terminal resize)
        await workload.ResizeAsync(100, 30, TestContext.Current.CancellationToken);
        
        // This should fire a resize event
        var hasEvent = workload.InputEvents.TryRead(out var evt);
        Assert.True(hasEvent, "ResizeAsync after init should fire a resize event");
        
        var resizeEvent = Assert.IsType<Hex1bResizeEvent>(evt);
        Assert.Equal(100, resizeEvent.Width);
        Assert.Equal(30, resizeEvent.Height);
    }

    [Fact]
    public async Task ResizeAsync_SameDimensions_DoesNotFireEvent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Resize to same dimensions
        await workload.ResizeAsync(80, 24, TestContext.Current.CancellationToken);
        
        // Should NOT fire event (no change)
        var hasEvent = workload.InputEvents.TryRead(out _);
        Assert.False(hasEvent, "ResizeAsync with same dimensions should not fire event");
    }

    #endregion
}
