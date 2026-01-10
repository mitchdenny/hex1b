using Hex1b.Input;
using Hex1b.Terminal.Automation;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the Hex1bTerminal virtual terminal emulator.
/// </summary>
public class Hex1bTerminalTests
{
    [Fact]
    public async Task Constructor_InitializesWithCorrectDimensions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        Assert.Equal(80, terminal.Width);
        Assert.Equal(24, terminal.Height);
    }

    [Fact]
    public async Task Constructor_InitializesWithEmptyScreen()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();
        
        var line = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Equal("", line);
    }

    [Fact]
    public async Task Write_PlacesTextAtCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        workload.Write("Hello");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.Equal("Hello", terminal.CreateSnapshot().GetLineTrimmed(0));
        Assert.Equal(5, terminal.CreateSnapshot().CursorX);
        Assert.Equal(0, terminal.CreateSnapshot().CursorY);
    }

    [Fact]
    public async Task Write_HandlesNewlines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        // Use \r\n (CRLF) - real terminals expect ONLCR translation to happen in PTY layer
        workload.Write("Line1\r\nLine2\r\nLine3");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.Equal("Line1", terminal.CreateSnapshot().GetLineTrimmed(0));
        Assert.Equal("Line2", terminal.CreateSnapshot().GetLineTrimmed(1));
        Assert.Equal("Line3", terminal.CreateSnapshot().GetLineTrimmed(2));
    }

    [Fact]
    public async Task Write_WrapsAtEndOfLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(5, 3).Build();
        
        workload.Write("HelloWorld");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.Equal("Hello", terminal.CreateSnapshot().GetLineTrimmed(0));
        Assert.Equal("World", terminal.CreateSnapshot().GetLineTrimmed(1));
    }

    [Fact]
    public async Task Clear_ResetsScreenAndCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("Some text");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        workload.Clear();
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.Equal("", terminal.CreateSnapshot().GetLineTrimmed(0));
        Assert.Equal(0, terminal.CreateSnapshot().CursorX);
        Assert.Equal(0, terminal.CreateSnapshot().CursorY);
    }

    [Fact]
    public async Task SetCursorPosition_MovesCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        workload.SetCursorPosition(5, 2);
        workload.Write("X");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        var line = terminal.CreateSnapshot().GetLine(2);
        Assert.Equal('X', line[5]);
    }

    [Fact]
    public async Task SetCursorPosition_ClampsToBounds()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();
        
        workload.SetCursorPosition(100, 100);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.Equal(9, terminal.CreateSnapshot().CursorX);
        Assert.Equal(4, terminal.CreateSnapshot().CursorY);
    }

    [Fact]
    public async Task EnterAlternateScreen_SetsFlag()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
        
        terminal.EnterAlternateScreen();
        
        Assert.True(terminal.CreateSnapshot().InAlternateScreen);
    }

    [Fact]
    public async Task ExitAlternateScreen_ClearsFlag()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.EnterAlternateScreen();
        
        terminal.ExitAlternateScreen();
        
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
    }

    [Fact]
    public async Task ContainsText_FindsText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        workload.Write("Hello World");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.True(terminal.CreateSnapshot().ContainsText("World"));
        Assert.False(terminal.CreateSnapshot().ContainsText("Foo"));
    }

    [Fact]
    public async Task FindText_ReturnsPositions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        // Use \r\n - terminal emulator expects explicit CR before LF
        workload.Write("Hello World\r\nHello Again");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        var results = terminal.CreateSnapshot().FindText("Hello");
        
        Assert.Equal(2, results.Count);
        Assert.Equal((0, 0), results[0]); // (Line, Column) = row 0, col 0
        Assert.Equal((1, 0), results[1]); // (Line, Column) = row 1, col 0
    }

    [Fact]
    public async Task GetNonEmptyLines_FiltersEmptyLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        // Use \r\n for proper line endings
        workload.Write("Line 1\r\n\r\nLine 3");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        var lines = terminal.CreateSnapshot().GetNonEmptyLines().ToList();
        
        Assert.Equal(2, lines.Count);
        Assert.Equal("Line 1", lines[0]);
        Assert.Equal("Line 3", lines[1]);
    }

    [Fact]
    public async Task Resize_PreservesContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("Hello");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        terminal.Resize(40, 10);
        
        Assert.Equal(40, terminal.Width);
        Assert.Equal(10, terminal.Height);
        Assert.Equal("Hello", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public async Task AnsiSequences_AreProcessedButNotDisplayed()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        
        workload.Write("\x1b[31mRed Text\x1b[0m");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.Equal("Red Text", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public async Task AnsiCursorPosition_MovesCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        // ANSI positions are 1-based, so row 2, col 5
        workload.Write("\x1b[2;5HX");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        var line = terminal.CreateSnapshot().GetLine(1); // 0-based
        Assert.Equal('X', line[4]); // 0-based
    }

    [Fact]
    public async Task AnsiClearScreen_ClearsBuffer()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("Some content");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        workload.Write("\x1b[2J");
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);
        
        Assert.Equal("", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public async Task GetScreenBuffer_ReturnsCopyWithColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        workload.Write("\x1b[38;2;255;0;0mR\x1b[0m");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !string.IsNullOrWhiteSpace(s.GetDisplayText()), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        
        var buffer = terminal.GetScreenBuffer();
        
        Assert.Equal("R", buffer[0, 0].Character);
        Assert.NotNull(buffer[0, 0].Foreground);
        Assert.Equal(255, buffer[0, 0].Foreground!.Value.R);
        Assert.Equal(0, buffer[0, 0].Foreground!.Value.G);
        Assert.Equal(0, buffer[0, 0].Foreground!.Value.B);
    }

    [Fact]
    public async Task AlternateScreenAnsiSequence_IsRecognized()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        workload.Write("\x1b[?1049h");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        Assert.True(terminal.CreateSnapshot().InAlternateScreen);
        
        workload.Write("\x1b[?1049l");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => !s.InAlternateScreen, TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
    }

    #region Resize Behavior

    [Fact]
    public async Task Constructor_SetsWorkloadDimensions()
    {
        // Workload dimensions are 0x0 before terminal is created
        using var workload = new Hex1bAppWorkloadAdapter();
        Assert.Equal(0, workload.Width);
        Assert.Equal(0, workload.Height);
        
        // Terminal sets workload dimensions during construction
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        Assert.Equal(80, workload.Width);
        Assert.Equal(24, workload.Height);
    }

    [Fact]
    public async Task Constructor_DoesNotFireResizeEvent()
    {
        // This is critical: the initial dimension setup should NOT fire a resize event
        // because that would trigger an extra re-render before the app even starts
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        // Try to read from input channel - should be empty (no resize event)
        var hasEvent = workload.InputEvents.TryRead(out var evt);
        Assert.False(hasEvent, "Constructor should not fire a resize event");
    }

    [Fact]
    public async Task ResizeAsync_AfterInitialization_FiresResizeEvent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
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
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        // Resize to same dimensions
        await workload.ResizeAsync(80, 24, TestContext.Current.CancellationToken);
        
        // Should NOT fire event (no change)
        var hasEvent = workload.InputEvents.TryRead(out _);
        Assert.False(hasEvent, "ResizeAsync with same dimensions should not fire event");
    }

    #endregion
}
