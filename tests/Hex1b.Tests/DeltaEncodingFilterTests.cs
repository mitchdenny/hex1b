using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Tests;

public class DeltaEncodingFilterTests
{
    [Fact]
    public async Task OnOutputAsync_WithNoShadowBuffer_PassesThroughAllTokens()
    {
        // Arrange
        var filter = new DeltaEncodingFilter();
        var appliedTokens = new List<AppliedToken>
        {
            AppliedToken.WithNoCellImpacts(new TextToken("Hello"), 0, 0, 0, 5)
        };

        // Act - no session start, so no shadow buffer
        var result = await filter.OnOutputAsync(appliedTokens, TimeSpan.Zero);

        // Assert
        Assert.Single(result);
        Assert.IsType<TextToken>(result[0]);
        Assert.Equal("Hello", ((TextToken)result[0]).Text);
    }

    [Fact]
    public async Task OnOutputAsync_WithShadowBuffer_FiltersUnchangedCells()
    {
        // Arrange
        var filter = new DeltaEncodingFilter();
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
        var filter = new DeltaEncodingFilter();
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
        var filter = new DeltaEncodingFilter();
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
        var filter = new DeltaEncodingFilter();
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
        var filter = new DeltaEncodingFilter();
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
    }

    [Fact]
    public async Task OnOutputAsync_WithAttributeChange_ProducesOutput()
    {
        // Arrange
        var filter = new DeltaEncodingFilter();
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
        var filter = new DeltaEncodingFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);

        // Act
        await filter.OnSessionEndAsync(TimeSpan.FromMinutes(1));

        // After session end, tokens should pass through (no shadow buffer)
        var appliedTokens = new List<AppliedToken>
        {
            AppliedToken.WithNoCellImpacts(new TextToken("Test"), 0, 0, 0, 4)
        };
        var result = await filter.OnOutputAsync(appliedTokens, TimeSpan.Zero);

        // Assert - should pass through because no shadow buffer
        Assert.Single(result);
        Assert.IsType<TextToken>(result[0]);
    }
}
