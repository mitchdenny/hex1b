using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for applying ANSI tokens to the terminal buffer.
/// </summary>
[TestClass]
public class AnsiTokenApplyTests
{
    #region TextToken Tests

    [TestMethod]
    public void ApplyTokens_TextToken_WritesToBuffer()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var tokens = new AnsiToken[] { new TextToken("Hello") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("Hello", snapshot.GetLine(0).TrimEnd());
    }

    [TestMethod]
    public void ApplyTokens_TextToken_AdvancesCursor()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var tokens = new AnsiToken[] { new TextToken("Hi") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        Assert.AreEqual(2, terminal.CursorX);
        Assert.AreEqual(0, terminal.CursorY);
    }

    [TestMethod]
    public void ApplyTokens_TextToken_WrapsAtEdge()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(5, 3).Build();
        var tokens = new AnsiToken[] { new TextToken("Hello World") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("Hello", snapshot.GetLine(0));
        Assert.AreEqual(" Worl", snapshot.GetLine(1));
    }

    #endregion

    #region ControlCharacterToken Tests

    [TestMethod]
    public void ApplyTokens_NewLine_MovesCursorDown()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        // LF only moves down, CR returns to column 0 (strict VT100 behavior)
        var tokens = new AnsiToken[] { new TextToken("A"), ControlCharacterToken.CarriageReturn, ControlCharacterToken.LineFeed, new TextToken("B") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual('A', snapshot.GetLine(0)[0]);
        Assert.AreEqual('B', snapshot.GetLine(1)[0]);
    }

    [TestMethod]
    public void ApplyTokens_CarriageReturn_MovesCursorToStart()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var tokens = new AnsiToken[] { new TextToken("ABC"), ControlCharacterToken.CarriageReturn, new TextToken("X") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("XBC", snapshot.GetLine(0).TrimEnd());
    }

    [TestMethod]
    public void ApplyTokens_Tab_MovesToNextTabStop()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var tokens = new AnsiToken[] { new TextToken("A"), ControlCharacterToken.Tab, new TextToken("B") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        Assert.AreEqual(9, terminal.CursorX); // 'B' written at position 8, cursor at 9
    }

    #endregion

    #region CursorPositionToken Tests

    [TestMethod]
    public void ApplyTokens_CursorPosition_MovesCursor()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();
        var tokens = new AnsiToken[] { new CursorPositionToken(3, 5), new TextToken("X") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual('X', snapshot.GetCell(4, 2).Character[0]); // 0-based: col 4, row 2
    }

    [TestMethod]
    public void ApplyTokens_CursorPosition_ClampsToScreen()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();
        var tokens = new AnsiToken[] { new CursorPositionToken(100, 100) };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        Assert.AreEqual(4, terminal.CursorY); // 0-based, clamped to height-1
        Assert.AreEqual(9, terminal.CursorX); // 0-based, clamped to width-1
    }

    #endregion

    #region SgrToken Tests

    [TestMethod]
    public void ApplyTokens_SgrBold_SetsAttribute()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var tokens = new AnsiToken[] { new SgrToken("1"), new TextToken("X") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        var cell = snapshot.GetCell(0, 0);
        Assert.IsTrue((cell.Attributes & CellAttributes.Bold) != 0);
    }

    [TestMethod]
    public void ApplyTokens_SgrReset_ClearsAttributes()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var tokens = new AnsiToken[] { new SgrToken("1"), new TextToken("A"), new SgrToken("0"), new TextToken("B") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.IsTrue((snapshot.GetCell(0, 0).Attributes & CellAttributes.Bold) != 0);
        Assert.IsTrue((snapshot.GetCell(1, 0).Attributes & CellAttributes.Bold) == 0);
    }

    [TestMethod]
    public void ApplyTokens_SgrForeground_SetsColor()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var tokens = new AnsiToken[] { new SgrToken("38;2;255;0;0"), new TextToken("X") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        var cell = snapshot.GetCell(0, 0);
        Assert.IsNotNull(cell.Foreground);
        Assert.AreEqual(255, cell.Foreground.Value.R);
        Assert.AreEqual(0, cell.Foreground.Value.G);
        Assert.AreEqual(0, cell.Foreground.Value.B);
    }

    #endregion

    #region ClearScreenToken Tests

    [TestMethod]
    public void ApplyTokens_ClearScreenToEnd_ClearsFromCursor()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 3).Build();
        // Fill buffer
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("AAAAAAAAAA") });
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("BBBBBBBBBB") });
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("CCCCCCCCCC") });
        // Position cursor at row 1, col 5 and clear to end
        terminal.ApplyTokens(new AnsiToken[] { new CursorPositionToken(2, 6), new ClearScreenToken(ClearMode.ToEnd) });

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("AAAAAAAAAA", snapshot.GetLine(0));
        Assert.AreEqual("BBBBB", snapshot.GetLine(1).TrimEnd()); // Cleared from position 5
        Assert.AreEqual("", snapshot.GetLine(2).TrimEnd()); // Fully cleared
    }

    [TestMethod]
    public void ApplyTokens_ClearScreenAll_ClearsEntireBuffer()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 3).Build();
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("Hello") });
        terminal.ApplyTokens(new AnsiToken[] { new ClearScreenToken(ClearMode.All) });

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("", snapshot.GetLine(0).TrimEnd());
    }

    #endregion

    #region ClearLineToken Tests

    [TestMethod]
    public void ApplyTokens_ClearLineToEnd_ClearsFromCursor()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 3).Build();
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("ABCDEFGHIJ") });
        terminal.ApplyTokens(new AnsiToken[] { new CursorPositionToken(1, 4), new ClearLineToken(ClearMode.ToEnd) });

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("ABC", snapshot.GetLine(0).TrimEnd());
    }

    [TestMethod]
    public void ApplyTokens_ClearLineAll_ClearsEntireLine()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 3).Build();
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("ABCDEFGHIJ") });
        terminal.ApplyTokens(new AnsiToken[] { new CursorPositionToken(1, 5), new ClearLineToken(ClearMode.All) });

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("", snapshot.GetLine(0).TrimEnd());
    }

    #endregion

    #region PrivateModeToken Tests

    [TestMethod]
    public void ApplyTokens_AlternateScreenEnable_EntersAlternateScreen()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("Main") });
        
        // Act
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, true) });

        // Assert
        Assert.IsTrue(terminal.InAlternateScreen);
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("", snapshot.GetLine(0).TrimEnd()); // Alt screen is blank
    }

    [TestMethod]
    public void ApplyTokens_AlternateScreenDisable_ExitsAlternateScreen()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, true) });
        
        // Act
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, false) });

        // Assert
        Assert.IsFalse(terminal.InAlternateScreen);
    }

    [TestMethod]
    public void ApplyTokens_AlternateScreen_SavesAndRestoresCursorPosition()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();
        
        // Move cursor to a specific position and write some text
        terminal.ApplyTokens(new AnsiToken[]
        {
            new CursorPositionToken(5, 10), // Row 5 (1-based), Column 10 (1-based)
            new TextToken("Main Screen")
        });
        
        // Cursor should be at end of "Main Screen" (5-1=4, 10-1+11=20, but clamped to 19)
        var cursorXBeforeAltScreen = terminal.CursorX;
        var cursorYBeforeAltScreen = terminal.CursorY;
        
        // Act - Enter alternate screen
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, true) });
        
        // Verify cursor position is preserved from primary screen
        Assert.AreEqual(cursorXBeforeAltScreen, terminal.CursorX);
        Assert.AreEqual(cursorYBeforeAltScreen, terminal.CursorY);
        
        // Move cursor in alternate screen
        terminal.ApplyTokens(new AnsiToken[] { new CursorPositionToken(3, 5) });
        Assert.AreEqual(4, terminal.CursorX); // 0-based
        Assert.AreEqual(2, terminal.CursorY); // 0-based
        
        // Exit alternate screen
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, false) });
        
        // Assert - Cursor should be restored to position before entering alt screen
        Assert.AreEqual(cursorXBeforeAltScreen, terminal.CursorX);
        Assert.AreEqual(cursorYBeforeAltScreen, terminal.CursorY);
    }

    [TestMethod]
    public void ApplyTokens_AlternateScreen_SavesAndRestoresMainScreenBuffer()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        // Write text to main screen
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("Main Screen") });
        var mainScreenText = terminal.CreateSnapshot().GetLine(0);
        Assert.StartsWith("Main Screen", mainScreenText);
        
        // Act - Enter alternate screen and write different text
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, true) });
        // Alt screen preserves cursor from primary; reset to home for clean write
        terminal.ApplyTokens(new AnsiToken[] { new CursorPositionToken(1, 1) });
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("Alt Screen") });
        
        // Verify alternate screen has alt text
        var altScreenText = terminal.CreateSnapshot().GetLine(0);
        Assert.StartsWith("Alt Screen", altScreenText);
        
        // Exit alternate screen
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, false) });
        
        // Assert - Main screen text should be restored
        var restoredText = terminal.CreateSnapshot().GetLine(0);
        Assert.StartsWith("Main Screen", restoredText);
    }
    
    [TestMethod]
    public void ApplyTokens_AlternateScreen_ExitWithoutEnter_DoesNotClearScreen()
    {
        // Arrange - create terminal with content
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        
        terminal.ApplyTokens(new AnsiToken[] { new TextToken("Important Data") });
        var originalText = terminal.CreateSnapshot().GetLine(0);
        Assert.StartsWith("Important Data", originalText);
        
        // Save original cursor position
        terminal.ApplyTokens(new AnsiToken[] { new CursorPositionToken(3, 10) });
        Assert.AreEqual(9, terminal.CursorX); // 0-based
        Assert.AreEqual(2, terminal.CursorY); // 0-based
        
        // Act - Exit alternate screen WITHOUT having entered it
        // This should NOT clear the screen or change cursor position
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, false) });
        
        // Assert - Content should be preserved
        var afterText = terminal.CreateSnapshot().GetLine(0);
        Assert.StartsWith("Important Data", afterText);
        
        // Cursor position should also be preserved
        Assert.AreEqual(9, terminal.CursorX);
        Assert.AreEqual(2, terminal.CursorY);
    }
    
    [TestMethod]
    public void ApplyTokens_AlternateScreen_SeparateCursorSaveFromDECSC()
    {
        // Arrange - verify alternate screen cursor save doesn't conflict with DECSC
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();
        
        // Set initial position and use DECSC to save
        terminal.ApplyTokens(new AnsiToken[] 
        { 
            new CursorPositionToken(2, 5), // Row 2, Col 5 -> y=1, x=4
            new SaveCursorToken(true)      // DECSC saves cursor
        });
        
        // Move to different position for alternate screen entry
        terminal.ApplyTokens(new AnsiToken[] { new CursorPositionToken(6, 15) }); // y=5, x=14
        
        // Enter alternate screen (should save y=5, x=14 in separate fields)
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, true) });
        
        // Cursor position is preserved from primary screen on alt screen entry
        Assert.AreEqual(14, terminal.CursorX);
        Assert.AreEqual(5, terminal.CursorY);
        
        // Move cursor in alternate screen
        terminal.ApplyTokens(new AnsiToken[] { new CursorPositionToken(4, 10) }); // y=3, x=9
        
        // Use DECRC to restore the DECSC position (should be 4, 1 from before)
        terminal.ApplyTokens(new AnsiToken[] { new RestoreCursorToken(true) });
        Assert.AreEqual(4, terminal.CursorX); // from DECSC
        Assert.AreEqual(1, terminal.CursorY); // from DECSC
        
        // Exit alternate screen
        terminal.ApplyTokens(new AnsiToken[] { new PrivateModeToken(1049, false) });
        
        // Cursor should be restored to position when we ENTERED alternate screen (5, 14 -> y=5, x=14)
        // NOT the DECSC position
        Assert.AreEqual(14, terminal.CursorX);
        Assert.AreEqual(5, terminal.CursorY);
    }

    #endregion

    #region SaveCursor / RestoreCursor Tests

    [TestMethod]
    public void ApplyTokens_SaveAndRestoreCursor_PreservesPosition()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();
        
        // Act
        terminal.ApplyTokens(new AnsiToken[]
        {
            new CursorPositionToken(5, 10),
            new SaveCursorToken(true),
            new CursorPositionToken(1, 1),
            new RestoreCursorToken(true)
        });

        // Assert
        Assert.AreEqual(9, terminal.CursorX); // 0-based from column 10
        Assert.AreEqual(4, terminal.CursorY); // 0-based from row 5
    }

    #endregion

    #region Round-trip Tests

    [TestMethod]
    public void ApplyTokens_RoundTrip_TokenizeAndApply_ProducesExpectedResult()
    {
        // Arrange - complex ANSI sequence
        var ansi = "\x1b[2J\x1b[H\x1b[1;31mHello\x1b[0m World\r\n\x1b[4mLine 2\x1b[0m";
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        // Act - tokenize and apply
        var tokens = AnsiTokenizer.Tokenize(ansi);
        terminal.ApplyTokens(tokens);

        // Assert - buffer should have expected content
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("Hello World", snapshot.GetLine(0).TrimEnd());
        Assert.AreEqual("Line 2", snapshot.GetLine(1).TrimEnd());
    }

    [TestMethod]
    public void ApplyTokens_ComplexSequence_AppliesCorrectly()
    {
        // Arrange
        var ansi = "\x1b[38;2;255;128;64mOrange\x1b[0m \x1b[1;3mBold Italic\x1b[0m";
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(30, 5).Build();

        // Act
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(ansi));

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("Orange Bold Italic", snapshot.GetLine(0).TrimEnd());
        
        // Check cell attributes
        var cell = snapshot.GetCell(0, 0);
        Assert.AreEqual(Hex1bColor.FromRgb(255, 128, 64), cell.Foreground);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void ApplyTokens_EmptyList_DoesNothing()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        // Act
        terminal.ApplyTokens(Array.Empty<AnsiToken>());

        // Assert - no crash, cursor at origin
        Assert.AreEqual(0, terminal.CursorX);
        Assert.AreEqual(0, terminal.CursorY);
    }

    [TestMethod]
    public void ApplyTokens_UnrecognizedSequence_Ignored()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var tokens = new AnsiToken[] { new UnrecognizedSequenceToken("\x1b[999z"), new TextToken("OK") };

        // Act
        terminal.ApplyTokens(tokens);

        // Assert
        var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("OK", snapshot.GetLine(0).TrimEnd());
    }

    #endregion
}
