using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for applying ANSI tokens to the terminal buffer.
/// </summary>
public class AnsiTokenApplyTests
{
    #region TextToken Tests

    [Fact]
    public void ApplyTokens_TextToken_WritesToBuffer()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var tokens = new AnsiToken[] { new TextToken("Hello") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("Hello", snapshot.GetLine(0).TrimEnd());
    }

    [Fact]
    public void ApplyTokens_TextToken_AdvancesCursor()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var tokens = new AnsiToken[] { new TextToken("Hi") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        Assert.Equal(2, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
    }

    [Fact]
    public void ApplyTokens_TextToken_WrapsAtEdge()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 5, 3);
        var tokens = new AnsiToken[] { new TextToken("Hello World") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("Hello", snapshot.GetLine(0));
        Assert.Equal(" Worl", snapshot.GetLine(1));
    }

    #endregion

    #region ControlCharacterToken Tests

    [Fact]
    public void ApplyTokens_NewLine_MovesCursorDown()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        // LF only moves down, CR returns to column 0 (strict VT100 behavior)
        var tokens = new AnsiToken[] { new TextToken("A"), ControlCharacterToken.CarriageReturn, ControlCharacterToken.LineFeed, new TextToken("B") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal('A', snapshot.GetLine(0)[0]);
        Assert.Equal('B', snapshot.GetLine(1)[0]);
    }

    [Fact]
    public void ApplyTokens_CarriageReturn_MovesCursorToStart()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var tokens = new AnsiToken[] { new TextToken("ABC"), ControlCharacterToken.CarriageReturn, new TextToken("X") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("XBC", snapshot.GetLine(0).TrimEnd());
    }

    [Fact]
    public void ApplyTokens_Tab_MovesToNextTabStop()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var tokens = new AnsiToken[] { new TextToken("A"), ControlCharacterToken.Tab, new TextToken("B") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        Assert.Equal(9, terminal.CursorX); // 'B' written at position 8, cursor at 9
    }

    #endregion

    #region CursorPositionToken Tests

    [Fact]
    public void ApplyTokens_CursorPosition_MovesCursor()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 10);
        var tokens = new AnsiToken[] { new CursorPositionToken(3, 5), new TextToken("X") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal('X', snapshot.GetCell(4, 2).Character[0]); // 0-based: col 4, row 2
    }

    [Fact]
    public void ApplyTokens_CursorPosition_ClampsToScreen()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 10, 5);
        var tokens = new AnsiToken[] { new CursorPositionToken(100, 100) };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        Assert.Equal(4, terminal.CursorY); // 0-based, clamped to height-1
        Assert.Equal(9, terminal.CursorX); // 0-based, clamped to width-1
    }

    #endregion

    #region SgrToken Tests

    [Fact]
    public void ApplyTokens_SgrBold_SetsAttribute()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var tokens = new AnsiToken[] { new SgrToken("1"), new TextToken("X") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        var cell = snapshot.GetCell(0, 0);
        Assert.True((cell.Attributes & CellAttributes.Bold) != 0);
    }

    [Fact]
    public void ApplyTokens_SgrReset_ClearsAttributes()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var tokens = new AnsiToken[] { new SgrToken("1"), new TextToken("A"), new SgrToken("0"), new TextToken("B") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.True((snapshot.GetCell(0, 0).Attributes & CellAttributes.Bold) != 0);
        Assert.True((snapshot.GetCell(1, 0).Attributes & CellAttributes.Bold) == 0);
    }

    [Fact]
    public void ApplyTokens_SgrForeground_SetsColor()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var tokens = new AnsiToken[] { new SgrToken("38;2;255;0;0"), new TextToken("X") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        var cell = snapshot.GetCell(0, 0);
        Assert.NotNull(cell.Foreground);
        Assert.Equal(255, cell.Foreground.Value.R);
        Assert.Equal(0, cell.Foreground.Value.G);
        Assert.Equal(0, cell.Foreground.Value.B);
    }

    #endregion

    #region ClearScreenToken Tests

    [Fact]
    public void ApplyTokens_ClearScreenToEnd_ClearsFromCursor()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 10, 3);
        // Fill buffer
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("AAAAAAAAAA") });
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("BBBBBBBBBB") });
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("CCCCCCCCCC") });
        // Position cursor at row 1, col 5 and clear to end
        terminal.ApplyTokens(new AnsiToken[] { new CursorPositionToken(2, 6), new ClearScreenToken(ClearMode.ToEnd) });

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("AAAAAAAAAA", snapshot.GetLine(0));
        Assert.Equal("BBBBB", snapshot.GetLine(1).TrimEnd()); // Cleared from position 5
        Assert.Equal("", snapshot.GetLine(2).TrimEnd()); // Fully cleared
    }

    [Fact]
    public void ApplyTokens_ClearScreenAll_ClearsEntireBuffer()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 10, 3);
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("Hello") });
        terminal.ApplyTokens(new AnsiToken[] { new ClearScreenToken(ClearMode.All) });

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("", snapshot.GetLine(0).TrimEnd());
    }

    #endregion

    #region ClearLineToken Tests

    [Fact]
    public void ApplyTokens_ClearLineToEnd_ClearsFromCursor()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 10, 3);
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("ABCDEFGHIJ") });
        terminal.ApplyTokens(new AnsiToken[] { new CursorPositionToken(1, 4), new ClearLineToken(ClearMode.ToEnd) });

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("ABC", snapshot.GetLine(0).TrimEnd());
    }

    [Fact]
    public void ApplyTokens_ClearLineAll_ClearsEntireLine()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 10, 3);
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("ABCDEFGHIJ") });
        terminal.ApplyTokens(new AnsiToken[] { new CursorPositionToken(1, 5), new ClearLineToken(ClearMode.All) });

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("", snapshot.GetLine(0).TrimEnd());
    }

    #endregion

    #region PrivateModeToken Tests

    [Fact]
    public void ApplyTokens_AlternateScreenEnable_EntersAlternateScreen()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("Main") });
        
        // Act
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, true) });

        // Assert
        Assert.True(terminal.InAlternateScreen);
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("", snapshot.GetLine(0).TrimEnd()); // Alt screen is blank
    }

    [Fact]
    public void ApplyTokens_AlternateScreenDisable_ExitsAlternateScreen()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, true) });
        
        // Act
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, false) });

        // Assert
        Assert.False(terminal.InAlternateScreen);
    }

    #endregion

    #region SaveCursor / RestoreCursor Tests

    [Fact]
    public void ApplyTokens_SaveAndRestoreCursor_PreservesPosition()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 10);
        
        // Act
        terminal.ApplyTokens(new AnsiToken[]
        {
            new CursorPositionToken(5, 10),
            new SaveCursorToken(true),
            new CursorPositionToken(1, 1),
            new RestoreCursorToken(true)
        });

        // Assert
        Assert.Equal(9, terminal.CursorX); // 0-based from column 10
        Assert.Equal(4, terminal.CursorY); // 0-based from row 5
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void ApplyTokens_RoundTrip_TokenizeAndApply_ProducesExpectedResult()
    {
        // Arrange - complex ANSI sequence
        var ansi = "\x1b[2J\x1b[H\x1b[1;31mHello\x1b[0m World\r\n\x1b[4mLine 2\x1b[0m";
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);

        // Act - tokenize and apply
        var tokens = AnsiTokenizer.Tokenize(ansi);
        terminal.ApplyTokens(tokens);

        // Assert - buffer should have expected content
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("Hello World", snapshot.GetLine(0).TrimEnd());
        Assert.Equal("Line 2", snapshot.GetLine(1).TrimEnd());
    }

    [Fact]
    public void ApplyTokens_ComplexSequence_AppliesCorrectly()
    {
        // Arrange
        var ansi = "\x1b[38;2;255;128;64mOrange\x1b[0m \x1b[1;3mBold Italic\x1b[0m";
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 30, 5);

        // Act
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(ansi));

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("Orange Bold Italic", snapshot.GetLine(0).TrimEnd());
        
        // Check cell attributes
        var cell = snapshot.GetCell(0, 0);
        Assert.Equal(Hex1bColor.FromRgb(255, 128, 64), cell.Foreground);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ApplyTokens_EmptyList_DoesNothing()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);

        // Act
        terminal.ApplyTokens(Array.Empty<AnsiToken>());

        // Assert - no crash, cursor at origin
        Assert.Equal(0, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
    }

    [Fact]
    public void ApplyTokens_UnrecognizedSequence_Ignored()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var tokens = new AnsiToken[] { new UnrecognizedSequenceToken("\x1b[999z"), new TextToken("OK") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("OK", snapshot.GetLine(0).TrimEnd());
    }

    #endregion
}
