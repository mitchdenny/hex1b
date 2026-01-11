using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

// Suppress xUnit1051 - these unit tests don't benefit from CancellationToken propagation
// since they're testing synchronous filter logic with simple async wrappers
#pragma warning disable xUnit1051

public class Hex1bAppRenderOptimizationFilterTests
{
    [Fact]
    public async Task OnOutputAsync_WithNoShadowBuffer_PassesThroughAllTokens()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        var appliedTokens = new List<AppliedToken>
        {
            AppliedToken.WithNoCellImpacts(new TextToken("Hello"), 0, 0, 0, 5)
        };

        // Act - no session start, so no shadow buffer
        var result = await filter.OnOutputAsync(appliedTokens, TimeSpan.Zero);

        // Assert - passes through with SGR reset and clear screen prepended (no shadow buffer triggers force refresh path)
        Assert.Equal(3, result.Count);
        Assert.IsType<SgrToken>(result[0]); // SGR reset
        Assert.IsType<ClearScreenToken>(result[1]); // Clear screen
        Assert.IsType<TextToken>(result[2]);
        Assert.Equal("Hello", ((TextToken)result[2]).Text);
    }

    [Fact]
    public async Task OnOutputAsync_WithShadowBuffer_FiltersUnchangedCells()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);

        // First write - all cells are new, should produce output
        var cellImpacts1 = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("H", null, null)),
            new(1, 0, new TerminalCell("i", null, null))
        };
        var appliedTokens1 = new List<AppliedToken>
        {
            new(new TextToken("Hi"), cellImpacts1, 0, 0, 2, 0)
        };

        var result1 = await filter.OnOutputAsync(appliedTokens1, TimeSpan.Zero);
        Assert.NotEmpty(result1); // Should have output for new cells

        // Second write - same cells, same content - should produce no output
        var result2 = await filter.OnOutputAsync(appliedTokens1, TimeSpan.FromMilliseconds(100));
        Assert.Empty(result2); // Nothing changed
    }

    [Fact]
    public async Task OnOutputAsync_WithChangedCell_ProducesOutput()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);

        // First write
        var cellImpacts1 = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("A", null, null))
        };
        var appliedTokens1 = new List<AppliedToken>
        {
            new(new TextToken("A"), cellImpacts1, 0, 0, 1, 0)
        };
        await filter.OnOutputAsync(appliedTokens1, TimeSpan.Zero);

        // Second write - different character at same position
        var cellImpacts2 = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("B", null, null))
        };
        var appliedTokens2 = new List<AppliedToken>
        {
            new(new TextToken("B"), cellImpacts2, 0, 0, 1, 0)
        };

        // Act
        var result = await filter.OnOutputAsync(appliedTokens2, TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.NotEmpty(result);
        // Should have cursor position and text token
        Assert.Contains(result, t => t is CursorPositionToken);
        Assert.Contains(result, t => t is TextToken);
    }

    [Fact]
    public async Task OnOutputAsync_WithColorChange_ProducesOutput()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);

        // First write with no color
        var cellImpacts1 = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("X", null, null))
        };
        var appliedTokens1 = new List<AppliedToken>
        {
            new(new TextToken("X"), cellImpacts1, 0, 0, 1, 0)
        };
        await filter.OnOutputAsync(appliedTokens1, TimeSpan.Zero);

        // Second write - same character but with color
        var cellImpacts2 = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("X", Hex1bColor.Red, null))
        };
        var appliedTokens2 = new List<AppliedToken>
        {
            new(new TextToken("X"), cellImpacts2, 0, 0, 1, 0)
        };

        // Act
        var result = await filter.OnOutputAsync(appliedTokens2, TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.NotEmpty(result);
        // Should have SGR token for color change
        Assert.Contains(result, t => t is SgrToken);
    }

    [Fact]
    public async Task OnOutputAsync_GeneratesOptimalCursorMovement()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);

        // First frame - clears the forceFullRefresh flag
        var initialImpacts = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("X", null, null))
        };
        var initialTokens = new List<AppliedToken>
        {
            new(new TextToken("X"), initialImpacts, 0, 0, 1, 0)
        };
        await filter.OnOutputAsync(initialTokens, TimeSpan.Zero);

        // Second frame - write three cells at different positions
        var cellImpacts = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("A", null, null)),  // (0,0) - change from X
            new(1, 0, new TerminalCell("B", null, null)),  // (1,0) - adjacent, new
            new(5, 0, new TerminalCell("C", null, null))   // (5,0) - gap, new
        };
        var appliedTokens = new List<AppliedToken>
        {
            new(new TextToken("ABC"), cellImpacts, 0, 0, 6, 0)
        };

        // Act
        var result = await filter.OnOutputAsync(appliedTokens, TimeSpan.FromMilliseconds(100));

        // Assert
        // Should have cursor positions before A and C (B follows A naturally)
        var cursorTokens = result.OfType<CursorPositionToken>().ToList();
        Assert.Equal(2, cursorTokens.Count); // One for (0,0), one for (5,0)
    }

    [Fact]
    public async Task OnResizeAsync_ClearsShadowBuffer()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);

        // Write a cell
        var cellImpacts1 = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("A", null, null))
        };
        var appliedTokens1 = new List<AppliedToken>
        {
            new(new TextToken("A"), cellImpacts1, 0, 0, 1, 0)
        };
        await filter.OnOutputAsync(appliedTokens1, TimeSpan.Zero);

        // Resize
        await filter.OnResizeAsync(100, 30, TimeSpan.FromSeconds(1));

        // Write the same cell again - should produce output because shadow buffer was reset
        var result = await filter.OnOutputAsync(appliedTokens1, TimeSpan.FromSeconds(2));

        // Assert
        Assert.NotEmpty(result);
        // First token should be SGR reset to prevent color bleeding after resize
        Assert.IsType<SgrToken>(result[0]);
        var sgrToken = (SgrToken)result[0];
        Assert.Equal("0", sgrToken.Parameters);
    }

    [Fact]
    public async Task OnOutputAsync_AfterResize_PrependsSgrReset()
    {
        // Arrange - this tests the fix for color bleeding after resize
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);

        // Simulate some colored content being written
        var coloredImpacts = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("X", Hex1b.Theming.Hex1bColor.FromRgb(255, 0, 0), Hex1b.Theming.Hex1bColor.FromRgb(255, 255, 255)))
        };
        var coloredTokens = new List<AppliedToken>
        {
            new(new TextToken("X"), coloredImpacts, 0, 0, 1, 0)
        };
        await filter.OnOutputAsync(coloredTokens, TimeSpan.Zero);

        // Resize the terminal
        await filter.OnResizeAsync(100, 30, TimeSpan.FromSeconds(1));

        // Write plain content after resize
        var plainImpacts = new List<CellImpact>
        {
            new(0, 0, new TerminalCell(" ", null, null))
        };
        var plainTokens = new List<AppliedToken>
        {
            new(new TextToken(" "), plainImpacts, 0, 0, 1, 0)
        };
        var result = await filter.OnOutputAsync(plainTokens, TimeSpan.FromSeconds(2));

        // Assert - should start with SGR reset to prevent color bleeding
        Assert.True(result.Count >= 1, "Should have at least the SGR reset token");
        Assert.IsType<SgrToken>(result[0]);
        var sgrToken = (SgrToken)result[0];
        Assert.Equal("0", sgrToken.Parameters);
    }

    [Fact]
    public async Task OnResizeAsync_DuringFrameBuffering_CancelsCurrentFrame()
    {
        // Arrange - this tests the fix for resize during frame buffering
        // When a resize happens mid-frame, the buffered content is invalid
        // (it was for the old terminal dimensions) so we must cancel the frame
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);

        // Start a frame
        var frameBegin = new List<AppliedToken>
        {
            new(FrameBeginToken.Instance, [], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(frameBegin, TimeSpan.Zero);

        // Write some content during the frame
        var cellImpacts = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("A", null, null))
        };
        var contentTokens = new List<AppliedToken>
        {
            new(new TextToken("A"), cellImpacts, 0, 0, 1, 0)
        };
        await filter.OnOutputAsync(contentTokens, TimeSpan.FromMilliseconds(10));

        // Resize happens mid-frame - this should cancel the buffering
        await filter.OnResizeAsync(100, 30, TimeSpan.FromMilliseconds(20));

        // End the frame - since buffering was cancelled by resize, this FrameEndToken
        // is orphaned and should be handled gracefully without consuming forceFullRefresh
        var frameEnd = new List<AppliedToken>
        {
            new(FrameEndToken.Instance, [], 0, 0, 0, 0)
        };
        var result = await filter.OnOutputAsync(frameEnd, TimeSpan.FromMilliseconds(30));

        // Assert - orphaned FrameEndToken should return empty (no content to output)
        // and should NOT consume the forceFullRefresh flag
        Assert.Empty(result);
    }

    [Fact]
    public async Task OnResizeAsync_DuringFrameBuffering_NextFrameIsFullRefresh()
    {
        // Arrange - verify that after resize, the next content is a full refresh
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);

        // Write initial content (before any buffering) - this consumes forceFullRefresh
        var cellImpacts1 = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("X", null, null))
        };
        var contentTokens1 = new List<AppliedToken>
        {
            new(new TextToken("X"), cellImpacts1, 0, 0, 1, 0)
        };
        await filter.OnOutputAsync(contentTokens1, TimeSpan.Zero);

        // Start a frame
        var frameBegin = new List<AppliedToken>
        {
            new(FrameBeginToken.Instance, [], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(frameBegin, TimeSpan.FromMilliseconds(10));

        // Resize mid-frame - sets forceFullRefresh = true and cancels buffering
        await filter.OnResizeAsync(100, 30, TimeSpan.FromMilliseconds(20));

        // Orphaned FrameEndToken - should NOT consume forceFullRefresh
        var frameEnd = new List<AppliedToken>
        {
            new(FrameEndToken.Instance, [], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(frameEnd, TimeSpan.FromMilliseconds(25));

        // Write the same content again (the cell "X" at position 0,0)
        // Since forceFullRefresh should still be true, this should produce output
        var result = await filter.OnOutputAsync(contentTokens1, TimeSpan.FromMilliseconds(30));

        // Assert - should produce output because resize triggered force refresh
        // and the orphaned FrameEndToken did NOT consume it
        Assert.NotEmpty(result);
        Assert.IsType<SgrToken>(result[0]); // SGR reset prepended
        Assert.IsType<ClearScreenToken>(result[1]); // Clear screen
        Assert.IsType<TextToken>(result[2]); // Original text token passes through
    }

    [Fact]
    public async Task OnOutputAsync_WithAttributeChange_ProducesOutput()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);

        // First write - no attributes
        var cellImpacts1 = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("X", null, null, CellAttributes.None))
        };
        var appliedTokens1 = new List<AppliedToken>
        {
            new(new TextToken("X"), cellImpacts1, 0, 0, 1, 0)
        };
        await filter.OnOutputAsync(appliedTokens1, TimeSpan.Zero);

        // Second write - same character but bold
        var cellImpacts2 = new List<CellImpact>
        {
            new(0, 0, new TerminalCell("X", null, null, CellAttributes.Bold))
        };
        var appliedTokens2 = new List<AppliedToken>
        {
            new(new TextToken("X"), cellImpacts2, 0, 0, 1, 0)
        };

        // Act
        var result = await filter.OnOutputAsync(appliedTokens2, TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.NotEmpty(result);
        var sgrToken = result.OfType<SgrToken>().FirstOrDefault();
        Assert.NotNull(sgrToken);
        Assert.Contains("1", sgrToken.Parameters); // Bold is SGR 1
    }

    [Fact]
    public async Task OnSessionEndAsync_CleansUp()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);

        // Act
        await filter.OnSessionEndAsync(TimeSpan.FromMinutes(1));

        // After session end, tokens should pass through (no shadow buffer)
        var appliedTokens = new List<AppliedToken>
        {
            AppliedToken.WithNoCellImpacts(new TextToken("Test"), 0, 0, 0, 4)
        };
        var result = await filter.OnOutputAsync(appliedTokens, TimeSpan.Zero);

        // Assert - should pass through with SGR reset and clear screen prepended (no shadow buffer triggers force refresh path)
        Assert.Equal(3, result.Count);
        Assert.IsType<SgrToken>(result[0]); // SGR reset
        Assert.IsType<ClearScreenToken>(result[1]); // Clear screen
        Assert.IsType<TextToken>(result[2]);
    }

    [Fact]
    public async Task OnOutputAsync_StandaloneCursorPosition_PassesThrough()
    {
        // Arrange - this tests the mouse cursor tracking scenario
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(10, 5, DateTimeOffset.UtcNow);

        // First frame to clear _forceFullRefresh
        var firstFrame = new List<AppliedToken>
        {
            new AppliedToken(new TextToken("Hi"), [new CellImpact(0, 0, new TerminalCell { Character = "H" })], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(firstFrame, TimeSpan.Zero);

        // Act - send a standalone cursor position with no cell impacts (mouse cursor tracking)
        var cursorMoveTokens = new List<AppliedToken>
        {
            AppliedToken.WithNoCellImpacts(new CursorPositionToken(3, 5), 0, 0, 0, 0)
        };
        var result = await filter.OnOutputAsync(cursorMoveTokens, TimeSpan.Zero);

        // Assert - cursor position should pass through for mouse tracking
        Assert.Single(result);
        Assert.IsType<CursorPositionToken>(result[0]);
        var cpt = (CursorPositionToken)result[0];
        Assert.Equal(3, cpt.Row);
        Assert.Equal(5, cpt.Column);
    }

    [Fact]
    public async Task OnOutputAsync_CursorPositionWithCellImpacts_NotDuplicatedInOutput()
    {
        // Arrange - cursor position used for rendering (not standalone)
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(10, 5, DateTimeOffset.UtcNow);

        // First frame to clear _forceFullRefresh
        var firstFrame = new List<AppliedToken>
        {
            new AppliedToken(new TextToken("Hi"), [new CellImpact(0, 0, new TerminalCell { Character = "H" })], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(firstFrame, TimeSpan.Zero);

        // Act - send a cursor position WITH cell impacts (normal rendering)
        var renderTokens = new List<AppliedToken>
        {
            new AppliedToken(
                new CursorPositionToken(2, 3), 
                [], // No cell impacts on the cursor token itself
                0, 0, 0, 0),
            new AppliedToken(
                new TextToken("X"), 
                [new CellImpact(2, 1, new TerminalCell { Character = "X" })], // Cell at row 2, col 3 (0-indexed: 1, 2)
                0, 0, 0, 0)
        };
        var result = await filter.OnOutputAsync(renderTokens, TimeSpan.Zero);

        // Assert - there should be tokens for the cell update
        // The cursor position in the renderTokens had no impacts, so it passes through
        // Then the cell update adds its own cursor position
        Assert.True(result.Count >= 1, $"Expected at least 1 token but got {result.Count}");
    }

    [Fact]
    public async Task FrameBuffering_NoOutputUntilFrameEnd()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(10, 5, DateTimeOffset.Now);
        
        // Consume the first frame (force refresh) with actual content
        var consumeForceRefresh = new List<AppliedToken>
        {
            new AppliedToken(new TextToken("X"), [new CellImpact(9, 4, new TerminalCell { Character = "X" })], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(consumeForceRefresh, TimeSpan.Zero);
        
        // Act - send FrameBegin and some content (but no FrameEnd)
        var frameTokens = new List<AppliedToken>
        {
            new AppliedToken(FrameBeginToken.Instance, [], 0, 0, 0, 0),
            new AppliedToken(new TextToken("A"), [new CellImpact(0, 0, new TerminalCell { Character = "A" })], 0, 0, 0, 0)
        };
        var result = await filter.OnOutputAsync(frameTokens, TimeSpan.Zero);
        
        // Assert - no output should be emitted while buffering
        Assert.Empty(result);
    }

    [Fact]
    public async Task FrameBuffering_OutputOnFrameEnd()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(10, 5, DateTimeOffset.Now);
        
        // Consume force refresh with actual content
        var consumeForceRefresh = new List<AppliedToken>
        {
            new AppliedToken(new TextToken("X"), [new CellImpact(9, 4, new TerminalCell { Character = "X" })], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(consumeForceRefresh, TimeSpan.Zero);
        
        // Act - send a complete frame
        var beginFrame = new List<AppliedToken>
        {
            new AppliedToken(FrameBeginToken.Instance, [], 0, 0, 0, 0),
            new AppliedToken(new TextToken("A"), [new CellImpact(0, 0, new TerminalCell { Character = "A" })], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(beginFrame, TimeSpan.Zero);
        
        var endFrame = new List<AppliedToken>
        {
            new AppliedToken(FrameEndToken.Instance, [], 0, 0, 0, 0)
        };
        var result = await filter.OnOutputAsync(endFrame, TimeSpan.Zero);
        
        // Assert - output should be emitted on frame end
        Assert.NotEmpty(result);
        Assert.Contains(result, t => t is TextToken tt && tt.Text == "A");
    }

    [Fact]
    public async Task FrameBuffering_ClearThenRender_OnlyEmitsNetChanges()
    {
        // Arrange - This is the key flicker fix test!
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(10, 5, DateTimeOffset.Now);
        
        // First, render some content and commit it
        var initialFrame = new List<AppliedToken>
        {
            new AppliedToken(FrameBeginToken.Instance, [], 0, 0, 0, 0),
            new AppliedToken(new TextToken("Hello"), [
                new CellImpact(0, 0, new TerminalCell { Character = "H" }),
                new CellImpact(1, 0, new TerminalCell { Character = "e" }),
                new CellImpact(2, 0, new TerminalCell { Character = "l" }),
                new CellImpact(3, 0, new TerminalCell { Character = "l" }),
                new CellImpact(4, 0, new TerminalCell { Character = "o" }),
            ], 0, 0, 0, 0),
            new AppliedToken(FrameEndToken.Instance, [], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(initialFrame, TimeSpan.Zero);
        
        // Act - simulate the flicker scenario: clear then re-render same content
        var flickerFrame = new List<AppliedToken>
        {
            new AppliedToken(FrameBeginToken.Instance, [], 0, 0, 0, 0),
            // Clear screen (what ClearDirtyRegions does)
            new AppliedToken(new ClearScreenToken(ClearMode.All), [], 0, 0, 0, 0),
            // Re-render same content
            new AppliedToken(new TextToken("Hello"), [
                new CellImpact(0, 0, new TerminalCell { Character = "H" }),
                new CellImpact(1, 0, new TerminalCell { Character = "e" }),
                new CellImpact(2, 0, new TerminalCell { Character = "l" }),
                new CellImpact(3, 0, new TerminalCell { Character = "l" }),
                new CellImpact(4, 0, new TerminalCell { Character = "o" }),
            ], 0, 0, 0, 0),
            new AppliedToken(FrameEndToken.Instance, [], 0, 0, 0, 0)
        };
        var result = await filter.OnOutputAsync(flickerFrame, TimeSpan.Zero);
        
        // Assert - since the net result is the same as before, no output needed!
        // This is the key: clear + re-render same content = no visible change
        Assert.Empty(result);
    }

    [Fact]
    public async Task FrameBuffering_ClearThenRenderDifferent_EmitsOnlyDifference()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(10, 5, DateTimeOffset.Now);
        
        // First, render "Hello"
        var initialFrame = new List<AppliedToken>
        {
            new AppliedToken(FrameBeginToken.Instance, [], 0, 0, 0, 0),
            new AppliedToken(new TextToken("Hello"), [
                new CellImpact(0, 0, new TerminalCell { Character = "H" }),
                new CellImpact(1, 0, new TerminalCell { Character = "e" }),
                new CellImpact(2, 0, new TerminalCell { Character = "l" }),
                new CellImpact(3, 0, new TerminalCell { Character = "l" }),
                new CellImpact(4, 0, new TerminalCell { Character = "o" }),
            ], 0, 0, 0, 0),
            new AppliedToken(FrameEndToken.Instance, [], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(initialFrame, TimeSpan.Zero);
        
        // Act - clear and render "Hella" (one character different)
        var changeFrame = new List<AppliedToken>
        {
            new AppliedToken(FrameBeginToken.Instance, [], 0, 0, 0, 0),
            new AppliedToken(new ClearScreenToken(ClearMode.All), [], 0, 0, 0, 0),
            new AppliedToken(new TextToken("Hella"), [
                new CellImpact(0, 0, new TerminalCell { Character = "H" }),
                new CellImpact(1, 0, new TerminalCell { Character = "e" }),
                new CellImpact(2, 0, new TerminalCell { Character = "l" }),
                new CellImpact(3, 0, new TerminalCell { Character = "l" }),
                new CellImpact(4, 0, new TerminalCell { Character = "a" }),
            ], 0, 0, 0, 0),
            new AppliedToken(FrameEndToken.Instance, [], 0, 0, 0, 0)
        };
        var result = await filter.OnOutputAsync(changeFrame, TimeSpan.Zero);
        
        // Assert - should only emit the one changed character
        var textTokens = result.OfType<TextToken>().ToList();
        Assert.Single(textTokens);
        Assert.Equal("a", textTokens[0].Text);
    }

    [Fact]
    public async Task FrameBuffering_ControlTokensPreserved()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(10, 5, DateTimeOffset.Now);
        await filter.OnOutputAsync([], TimeSpan.Zero); // Consume force refresh
        
        // Act - send a frame with control tokens
        var frameTokens = new List<AppliedToken>
        {
            new AppliedToken(FrameBeginToken.Instance, [], 0, 0, 0, 0),
            new AppliedToken(new PrivateModeToken(25, false), [], 0, 0, 0, 0), // Hide cursor
            new AppliedToken(new TextToken("A"), [new CellImpact(0, 0, new TerminalCell { Character = "A" })], 0, 0, 0, 0),
            new AppliedToken(new PrivateModeToken(25, true), [], 0, 0, 0, 0), // Show cursor
            new AppliedToken(FrameEndToken.Instance, [], 0, 0, 0, 0)
        };
        var result = await filter.OnOutputAsync(frameTokens, TimeSpan.Zero);
        
        // Assert - control tokens should be in output
        var privateModeTokens = result.OfType<PrivateModeToken>().ToList();
        Assert.Equal(2, privateModeTokens.Count);
    }

    [Fact]
    public async Task FrameBuffering_FrameEndWithoutBegin_IsIgnored()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(10, 5, DateTimeOffset.Now);
        
        // Consume the first frame (force refresh) with empty content
        await filter.OnOutputAsync([], TimeSpan.Zero);
        
        // Act - send a FrameEnd without FrameBegin
        var tokens = new List<AppliedToken>
        {
            new AppliedToken(FrameEndToken.Instance, [], 0, 0, 0, 0)
        };
        var result = await filter.OnOutputAsync(tokens, TimeSpan.Zero);
        
        // Assert - should just be ignored, empty output
        Assert.Empty(result);
    }

    [Fact]
    public async Task FrameBuffering_NestedFrameBegin_FlushesFirst()
    {
        // Arrange
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(10, 5, DateTimeOffset.Now);
        
        // Consume force refresh with actual content
        var consumeForceRefresh = new List<AppliedToken>
        {
            new AppliedToken(new TextToken("X"), [new CellImpact(9, 4, new TerminalCell { Character = "X" })], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(consumeForceRefresh, TimeSpan.Zero);
        
        // Act - send FrameBegin, some content, then another FrameBegin (should flush first frame)
        var firstBegin = new List<AppliedToken>
        {
            new AppliedToken(FrameBeginToken.Instance, [], 0, 0, 0, 0),
            new AppliedToken(new TextToken("A"), [new CellImpact(0, 0, new TerminalCell { Character = "A" })], 0, 0, 0, 0)
        };
        var result1 = await filter.OnOutputAsync(firstBegin, TimeSpan.Zero, TestContext.Current.CancellationToken);
        
        // No output yet (still buffering)
        Assert.Empty(result1);
        
        // Second FrameBegin should flush the first
        var secondBegin = new List<AppliedToken>
        {
            new AppliedToken(FrameBeginToken.Instance, [], 0, 0, 0, 0),
            new AppliedToken(new TextToken("B"), [new CellImpact(1, 0, new TerminalCell { Character = "B" })], 0, 0, 0, 0)
        };
        var result2 = await filter.OnOutputAsync(secondBegin, TimeSpan.Zero, TestContext.Current.CancellationToken);
        
        // Assert - first frame content should have been flushed
        Assert.Contains(result2, t => t is TextToken tt && tt.Text == "A");
    }

    [Fact]
    public async Task FrameBuffering_TokensAfterFrameEnd_AreProcessed()
    {
        // Arrange - tests that tokens like ?2026l that follow FrameEnd in the same batch are not lost
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(10, 5, DateTimeOffset.Now, TestContext.Current.CancellationToken);
        
        // Consume force refresh with actual content
        var consumeForceRefresh = new List<AppliedToken>
        {
            new AppliedToken(new TextToken("X"), [new CellImpact(9, 4, new TerminalCell { Character = "X" })], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(consumeForceRefresh, TimeSpan.Zero, TestContext.Current.CancellationToken);
        
        // Act - send a complete frame with FrameEnd followed by ?2026l in the same batch
        var frameTokens = new List<AppliedToken>
        {
            new AppliedToken(FrameBeginToken.Instance, [], 0, 0, 0, 0),
            new AppliedToken(new TextToken("A"), [new CellImpact(0, 0, new TerminalCell { Character = "A" })], 0, 0, 0, 0),
            new AppliedToken(FrameEndToken.Instance, [], 0, 0, 0, 0),
            new AppliedToken(new PrivateModeToken(2026, false), [], 0, 0, 0, 0), // ?2026l - end sync update
        };
        var result = await filter.OnOutputAsync(frameTokens, TimeSpan.Zero, TestContext.Current.CancellationToken);
        
        // Assert - the ?2026l token should be in the output
        var syncEndTokens = result.OfType<PrivateModeToken>()
            .Where(pm => pm.Mode == 2026 && !pm.Enable)
            .ToList();
        Assert.Single(syncEndTokens);
    }

    [Fact]
    public async Task FrameBuffering_SeparateBatchAfterFrameEnd_TokensPassThrough()
    {
        // Arrange - tests that tokens like ?2026l that come in a separate batch after FrameEnd pass through
        var filter = new Hex1bAppRenderOptimizationFilter();
        await filter.OnSessionStartAsync(10, 5, DateTimeOffset.Now, TestContext.Current.CancellationToken);
        
        // Consume force refresh with actual content
        var consumeForceRefresh = new List<AppliedToken>
        {
            new AppliedToken(new TextToken("X"), [new CellImpact(9, 4, new TerminalCell { Character = "X" })], 0, 0, 0, 0)
        };
        await filter.OnOutputAsync(consumeForceRefresh, TimeSpan.Zero, TestContext.Current.CancellationToken);
        
        // Act - send frame tokens without the ?2026l
        var frameTokens = new List<AppliedToken>
        {
            new AppliedToken(FrameBeginToken.Instance, [], 0, 0, 0, 0),
            new AppliedToken(new TextToken("A"), [new CellImpact(0, 0, new TerminalCell { Character = "A" })], 0, 0, 0, 0),
            new AppliedToken(FrameEndToken.Instance, [], 0, 0, 0, 0),
        };
        await filter.OnOutputAsync(frameTokens, TimeSpan.Zero, TestContext.Current.CancellationToken);
        
        // Now send the ?2026l in a separate batch (as happens in real scenario)
        var syncEndTokens = new List<AppliedToken>
        {
            new AppliedToken(new PrivateModeToken(2026, false), [], 0, 0, 0, 0), // ?2026l - end sync update
        };
        var result = await filter.OnOutputAsync(syncEndTokens, TimeSpan.Zero, TestContext.Current.CancellationToken);
        
        // Assert - the ?2026l token should be in the output (passes through in immediate mode)
        var syncEnd = result.OfType<PrivateModeToken>()
            .Where(pm => pm.Mode == 2026 && !pm.Enable)
            .ToList();
        Assert.Single(syncEnd);
    }
}
