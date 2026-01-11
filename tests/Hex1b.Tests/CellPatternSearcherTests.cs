using Hex1b.Layout;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive tests for the CellPatternSearcher API.
/// </summary>
public class CellPatternSearcherTests
{
    #region Helper Methods

    private static async Task<Hex1bTerminalSnapshot> CreateSnapshotAsync(string[] lines, int width = 80, int height = 24)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(width, height).Build();

        foreach (var line in lines)
        {
            workload.Write(line + "\r\n");
        }

        // Wait for content to be processed by the output pump
        var firstLine = lines.Length > 0 ? lines[0] : "";
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => string.IsNullOrEmpty(firstLine) || s.ContainsText(firstLine), TimeSpan.FromSeconds(1), "first line content")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        return terminal.CreateSnapshot();
    }

    private static async Task<Hex1bTerminalSnapshot> CreateSnapshotAsync(string content, int width = 80, int height = 24)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(width, height).Build();
        
        if (string.IsNullOrEmpty(content))
        {
            // For empty content, just wait a short time to let the pump run
            await new Hex1bTerminalInputSequenceBuilder()
                .Wait(TimeSpan.FromMilliseconds(50))
                .Build()
                .ApplyAsync(terminal);
            return terminal.CreateSnapshot();
        }
        else
        {
            workload.Write(content);
            
            // Wait for specific content to be processed by the output pump
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText(content.Substring(0, Math.Min(content.Length, 10))), TimeSpan.FromSeconds(1), "written content")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
            
            return terminal.CreateSnapshot();
        }
    }

    #endregion

    #region TraversedCell Tests

    [Fact]
    public async Task TraversedCell_HasCorrectProperties()
    {
        var cell = new TerminalCell("A", null, null);
        var traversed = new TraversedCell(5, 10, cell, null);

        Assert.Equal(5, traversed.X);
        Assert.Equal(10, traversed.Y);
        Assert.Equal("A", traversed.Cell.Character);
        Assert.Null(traversed.CaptureNames);
    }

    [Fact]
    public async Task TraversedCell_WithCaptureNames()
    {
        var cell = new TerminalCell("B", null, null);
        var captures = new HashSet<string> { "name", "value" };
        var traversed = new TraversedCell(0, 0, cell, captures);

        Assert.NotNull(traversed.CaptureNames);
        Assert.Contains("name", traversed.CaptureNames);
        Assert.Contains("value", traversed.CaptureNames);
    }

    #endregion

    #region CellMatchContext Tests

    [Fact]
    public async Task CellMatchContext_ProvidesRegionAccess()
    {
        var snapshot = await CreateSnapshotAsync("Hello World");
        var pattern = new CellPatternSearcher()
            .Find(ctx =>
            {
                Assert.NotNull(ctx.Region);
                Assert.Equal(80, ctx.Region.Width);
                return ctx.Cell.Character == "H";
            });

        var result = pattern.Search(snapshot);
        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task CellMatchContext_ProvidesCurrentPosition()
    {
        var snapshot = await CreateSnapshotAsync("Hello");
        var positions = new List<(int X, int Y)>();

        var pattern = new CellPatternSearcher()
            .Find('H')
            .RightWhile(ctx =>
            {
                positions.Add((ctx.X, ctx.Y));
                return ctx.Cell.Character != "o";
            });

        pattern.Search(snapshot);

        Assert.Contains((1, 0), positions); // 'e'
        Assert.Contains((2, 0), positions); // 'l'
        Assert.Contains((3, 0), positions); // 'l'
    }

    [Fact]
    public async Task CellMatchContext_ProvidesMatchStartCell()
    {
        var snapshot = await CreateSnapshotAsync("ABC");
        TerminalCell? capturedStartCell = null;

        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right(ctx =>
            {
                capturedStartCell = ctx.MatchStartCell;
                return true;
            });

        pattern.Search(snapshot);

        Assert.NotNull(capturedStartCell);
        Assert.Equal("A", capturedStartCell.Value.Character);
    }

    [Fact]
    public async Task CellMatchContext_ProvidesPreviousCell()
    {
        var snapshot = await CreateSnapshotAsync("ABC");
        var previousChars = new List<string>();

        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right(ctx =>
            {
                previousChars.Add(ctx.PreviousCell.Character);
                return true;
            })
            .Right(ctx =>
            {
                previousChars.Add(ctx.PreviousCell.Character);
                return true;
            });

        pattern.Search(snapshot);

        Assert.Equal(2, previousChars.Count);
        Assert.Equal("A", previousChars[0]);
        Assert.Equal("B", previousChars[1]);
    }

    [Fact]
    public async Task CellMatchContext_GetRelative_ReturnsCorrectCell()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "ABC", "DEF", "GHI" });
        TerminalCell? relativeCell = null;

        var pattern = new CellPatternSearcher()
            .Find('E') // Center cell at (1,1)
            .Right(ctx =>
            {
                // Current position is now (2,1) = 'F'
                // GetRelative(-2, -1) from (2,1) = (0,0) = 'A'
                relativeCell = ctx.GetRelative(-2, -1);
                return true;
            });

        pattern.Search(snapshot);

        Assert.NotNull(relativeCell);
        Assert.Equal("A", relativeCell.Value.Character);
    }

    [Fact]
    public async Task CellMatchContext_GetAbsolute_ReturnsCorrectCell()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "ABC", "DEF" });
        TerminalCell? absoluteCell = null;

        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right(ctx =>
            {
                absoluteCell = ctx.GetAbsolute(2, 1); // Should be 'F'
                return true;
            });

        pattern.Search(snapshot);

        Assert.NotNull(absoluteCell);
        Assert.Equal("F", absoluteCell.Value.Character);
    }

    [Fact]
    public async Task CellMatchContext_TraversedCells_TracksProgress()
    {
        var snapshot = await CreateSnapshotAsync("ABCD");
        var traversedCounts = new List<int>();

        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right(ctx =>
            {
                traversedCounts.Add(ctx.TraversedCells.Count);
                return true;
            })
            .Right(ctx =>
            {
                traversedCounts.Add(ctx.TraversedCells.Count);
                return true;
            })
            .Right(ctx =>
            {
                traversedCounts.Add(ctx.TraversedCells.Count);
                return true;
            });

        pattern.Search(snapshot);

        Assert.Equal(3, traversedCounts.Count);
        Assert.Equal(1, traversedCounts[0]); // After Find('A')
        Assert.Equal(2, traversedCounts[1]); // After first Right
        Assert.Equal(3, traversedCounts[2]); // After second Right
    }

    #endregion

    #region Find Tests

    [Fact]
    public async Task Find_WithChar_MatchesSingleCharacter()
    {
        var snapshot = await CreateSnapshotAsync("Hello World");
        var pattern = new CellPatternSearcher().Find('W');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Single(result.Matches);
        Assert.Equal(6, result.First!.Start.X);
    }

    [Fact]
    public async Task Find_WithChar_FindsMultipleOccurrences()
    {
        var snapshot = await CreateSnapshotAsync("ababa");
        var pattern = new CellPatternSearcher().Find('a');

        var result = pattern.Search(snapshot);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Find_WithPredicate_MatchesCondition()
    {
        var snapshot = await CreateSnapshotAsync("abc123def");
        var pattern = new CellPatternSearcher()
            .Find(ctx => char.IsDigit(ctx.Cell.Character[0]));

        var result = pattern.Search(snapshot);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task FindPattern_WithRegexString_MatchesPattern()
    {
        var snapshot = await CreateSnapshotAsync("Name: John, Age: 30");
        var pattern = new CellPatternSearcher().FindPattern(@"Age:\s*");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task FindPattern_WithCompiledRegex_MatchesPattern()
    {
        var snapshot = await CreateSnapshotAsync("Error: file not found");
        var regex = new System.Text.RegularExpressions.Regex(@"Error:\s*");
        var pattern = new CellPatternSearcher().FindPattern(regex);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task Find_NoMatch_ReturnsEmptyResult()
    {
        var snapshot = await CreateSnapshotAsync("Hello World");
        var pattern = new CellPatternSearcher().Find('Z');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
        Assert.Equal(0, result.Count);
        Assert.Null(result.First);
    }

    [Fact]
    public async Task Find_AcrossMultipleLines()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "Line 1", "Line 2", "Line 3" });
        var pattern = new CellPatternSearcher().Find('L');

        var result = pattern.Search(snapshot);

        Assert.Equal(3, result.Count);
        Assert.Equal(0, result.Matches[0].Start.Y);
        Assert.Equal(1, result.Matches[1].Start.Y);
        Assert.Equal(2, result.Matches[2].Start.Y);
    }

    #endregion

    #region Directional Movement Tests

    [Fact]
    public async Task Right_WithChar_MovesAndMatches()
    {
        var snapshot = await CreateSnapshotAsync("AB");
        var pattern = new CellPatternSearcher().Find('A').Right('B');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(2, result.First!.Cells.Count);
    }

    [Fact]
    public async Task Right_WithPredicate_MovesAndMatches()
    {
        var snapshot = await CreateSnapshotAsync("A1");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right(ctx => char.IsDigit(ctx.Cell.Character[0]));

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task Right_FailsIfNoMatch()
    {
        var snapshot = await CreateSnapshotAsync("AC");
        var pattern = new CellPatternSearcher().Find('A').Right('B');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public async Task Right_WithCount_MovesMultipleCells()
    {
        var snapshot = await CreateSnapshotAsync("ABCDE");
        var pattern = new CellPatternSearcher().Find('A').Right(3);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(4, result.First!.Cells.Count); // A + 3 more
        Assert.Equal((3, 0), result.First!.End);
    }

    [Fact]
    public async Task Right_WithCountAndPredicate_AllMustMatch()
    {
        var snapshot = await CreateSnapshotAsync("A111B");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right(3, ctx => ctx.Cell.Character == "1");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task Right_WithCountAndPredicate_FailsIfAnyDontMatch()
    {
        var snapshot = await CreateSnapshotAsync("A121B");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right(3, ctx => ctx.Cell.Character == "1");

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public async Task Left_WithChar_MovesLeft()
    {
        var snapshot = await CreateSnapshotAsync("BA");
        var pattern = new CellPatternSearcher().Find('A').Left('B');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task Up_WithChar_MovesUp()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "A", "B" });
        var pattern = new CellPatternSearcher().Find('B').Up('A');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task Down_WithChar_MovesDown()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "A", "B" });
        var pattern = new CellPatternSearcher().Find('A').Down('B');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task Left_WithCount_MovesMultipleCells()
    {
        var snapshot = await CreateSnapshotAsync("ABCDE");
        var pattern = new CellPatternSearcher().Find('E').Left(4);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(5, result.First!.Cells.Count);
    }

    [Fact]
    public async Task Up_WithCount_MovesMultipleCells()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "A", "B", "C", "D", "E" });
        var pattern = new CellPatternSearcher().Find('E').Up(4);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(5, result.First!.Cells.Count);
    }

    [Fact]
    public async Task Down_WithCount_MovesMultipleCells()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "A", "B", "C", "D", "E" });
        var pattern = new CellPatternSearcher().Find('A').Down(4);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(5, result.First!.Cells.Count);
    }

    #endregion

    #region Text Sequence Tests

    [Fact]
    public async Task RightText_MatchesExactSequence()
    {
        var snapshot = await CreateSnapshotAsync("Hello World");
        var pattern = new CellPatternSearcher().Find('H').RightText("ello");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("Hello", result.First!.Text);
    }

    [Fact]
    public async Task RightText_FailsOnMismatch()
    {
        var snapshot = await CreateSnapshotAsync("Hello World");
        var pattern = new CellPatternSearcher().Find('H').RightText("allo");

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public async Task LeftText_MatchesExactSequence()
    {
        var snapshot = await CreateSnapshotAsync("Hello");
        var pattern = new CellPatternSearcher().Find('o').LeftText("lleH");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    #endregion

    #region While Tests

    [Fact]
    public async Task RightWhile_ConsumesMatchingCells()
    {
        var snapshot = await CreateSnapshotAsync("AAA123");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .RightWhile(ctx => ctx.Cell.Character == "A");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("AAA", result.First!.Text);
    }

    [Fact]
    public async Task RightWhile_StopsAtNonMatch()
    {
        var snapshot = await CreateSnapshotAsync("AAABBB");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .RightWhile(ctx => ctx.Cell.Character == "A");

        var result = pattern.Search(snapshot);

        Assert.Equal("AAA", result.First!.Text);
    }

    [Fact]
    public async Task RightWhile_ZeroMatchesIsValid()
    {
        var snapshot = await CreateSnapshotAsync("AB");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .RightWhile(ctx => ctx.Cell.Character == "X")
            .Right('B');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task LeftWhile_ConsumesMatchingCells()
    {
        var snapshot = await CreateSnapshotAsync("123AAA");
        var pattern = new CellPatternSearcher()
            .Find(ctx => ctx.X == 5 && ctx.Cell.Character == "A")
            .LeftWhile(ctx => ctx.Cell.Character == "A");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(3, result.First!.Cells.Count);
    }

    [Fact]
    public async Task UpWhile_ConsumesMatchingCells()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "1", "A", "A", "A" });
        var pattern = new CellPatternSearcher()
            .Find(ctx => ctx.Y == 3 && ctx.Cell.Character == "A")
            .UpWhile(ctx => ctx.Cell.Character == "A");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(3, result.First!.Cells.Count);
    }

    [Fact]
    public async Task DownWhile_ConsumesMatchingCells()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "A", "A", "A", "1" });
        var pattern = new CellPatternSearcher()
            .Find('A')
            .DownWhile(ctx => ctx.Cell.Character == "A");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(3, result.First!.Cells.Count);
    }

    #endregion

    #region Until Tests

    [Fact]
    public async Task RightUntil_WithPredicate_StopsAtMatch()
    {
        var snapshot = await CreateSnapshotAsync("ABC]DEF");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .RightUntil(ctx => ctx.Cell.Character == "]");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("ABC]", result.First!.Text);
    }

    [Fact]
    public async Task RightUntil_WithChar_StopsAtChar()
    {
        var snapshot = await CreateSnapshotAsync("[content]");
        var pattern = new CellPatternSearcher()
            .Find('[')
            .RightUntil(']');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("[content]", result.First!.Text);
    }

    [Fact]
    public async Task RightUntil_FailsIfNotFound()
    {
        var snapshot = await CreateSnapshotAsync("ABC");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .RightUntil(']');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public async Task LeftUntil_StopsAtMatch()
    {
        var snapshot = await CreateSnapshotAsync("[content]");
        var pattern = new CellPatternSearcher()
            .Find(']')
            .LeftUntil('[');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task UpUntil_StopsAtMatch()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "-", "A", "A", "A" });
        var pattern = new CellPatternSearcher()
            .Find(ctx => ctx.Y == 3 && ctx.Cell.Character == "A")
            .UpUntil(ctx => ctx.Cell.Character == "-");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(4, result.First!.Cells.Count);
    }

    [Fact]
    public async Task DownUntil_StopsAtMatch()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "A", "A", "A", "-" });
        var pattern = new CellPatternSearcher()
            .Find('A')
            .DownUntil(ctx => ctx.Cell.Character == "-");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(4, result.First!.Cells.Count);
    }

    #endregion

    #region Boundary Tests

    [Fact]
    public async Task RightToEnd_GoesToEndOfLine()
    {
        var snapshot = await CreateSnapshotAsync("Hello", 10, 1);
        var pattern = new CellPatternSearcher()
            .Find('H')
            .RightToEnd();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(10, result.First!.Cells.Count);
        Assert.Equal((9, 0), result.First!.End);
    }

    [Fact]
    public async Task LeftToStart_GoesToStartOfLine()
    {
        var snapshot = await CreateSnapshotAsync("Hello", 10, 1);
        var pattern = new CellPatternSearcher()
            .Find('o')
            .LeftToStart();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal((0, 0), result.First!.End);
    }

    [Fact]
    public async Task DownToBottom_GoesToBottomOfRegion()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "A", "B", "C" }, 10, 5);
        var pattern = new CellPatternSearcher()
            .Find('A')
            .DownToBottom();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal((0, 4), result.First!.End);
    }

    [Fact]
    public async Task UpToTop_GoesToTopOfRegion()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "A", "B", "C" }, 10, 5);
        var pattern = new CellPatternSearcher()
            .Find('C')
            .UpToTop();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal((0, 0), result.First!.End);
    }

    #endregion

    #region Capture Tests

    [Fact]
    public async Task Capture_CapturesTraversedCells()
    {
        var snapshot = await CreateSnapshotAsync("Name: John");
        var pattern = new CellPatternSearcher()
            .FindPattern(@"Name:\s*")
            .BeginCapture("value")
            .RightWhile(ctx => ctx.Cell.Character != " " && ctx.X < 10)
            .EndCapture();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.True(result.First!.HasCapture("value"));
        Assert.Equal("John", result.First!.GetCaptureText("value"));
    }

    [Fact]
    public async Task Capture_MultipleCapturesInOnePattern()
    {
        var snapshot = await CreateSnapshotAsync("A=1 B=2");
        var pattern = new CellPatternSearcher()
            .BeginCapture("first")
            .Find('A')
            .Right('=')
            .Right('1')
            .EndCapture()
            .Right(' ')
            .BeginCapture("second")
            .Right('B')
            .Right('=')
            .Right('2')
            .EndCapture();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("A=1", result.First!.GetCaptureText("first"));
        Assert.Equal("B=2", result.First!.GetCaptureText("second"));
    }

    [Fact]
    public async Task Capture_NestedCaptures()
    {
        var snapshot = await CreateSnapshotAsync("[ERROR] msg");
        var pattern = new CellPatternSearcher()
            .BeginCapture("all")
            .Find('[')
            .BeginCapture("tag")
            .RightUntil(']')
            .EndCapture()
            .Right(' ')
            .BeginCapture("msg")
            .RightWhile(ctx => ctx.Cell.Character != " " && ctx.X < 15)
            .EndCapture()
            .EndCapture();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.True(result.First!.HasCapture("all"));
        Assert.True(result.First!.HasCapture("tag"));
        Assert.True(result.First!.HasCapture("msg"));
    }

    [Fact]
    public async Task Capture_GetCaptureBounds_ReturnsCorrectRect()
    {
        var snapshot = await CreateSnapshotAsync("XX[val]XX");
        var pattern = new CellPatternSearcher()
            .Find('[')
            .BeginCapture("inner")
            .RightUntil(']')
            .EndCapture();

        var result = pattern.Search(snapshot);
        var bounds = result.First!.GetCaptureBounds("inner");

        Assert.Equal(3, bounds.X); // Starts at 'v'
        Assert.Equal(0, bounds.Y);
        Assert.True(bounds.Width >= 3); // 'val]'
    }

    [Fact]
    public async Task Capture_NonExistentCapture_ReturnsEmpty()
    {
        var snapshot = await CreateSnapshotAsync("test");
        var pattern = new CellPatternSearcher().Find('t');

        var result = pattern.Search(snapshot);

        Assert.False(result.First!.HasCapture("nonexistent"));
        Assert.Empty(result.First!.GetCapture("nonexistent"));
        Assert.Equal("", result.First!.GetCaptureText("nonexistent"));
    }

    #endregion

    #region Composition Tests

    [Fact]
    public async Task Then_ChainsPatterns()
    {
        var prefix = new CellPatternSearcher().Find('[').RightUntil(']');
        var suffix = new CellPatternSearcher().Right(' ').RightToEnd();

        var snapshot = await CreateSnapshotAsync("[TAG] message", 20, 1);
        var pattern = new CellPatternSearcher()
            .Then(prefix)
            .Then(suffix);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task Then_WithAction_BuildsSubPattern()
    {
        var snapshot = await CreateSnapshotAsync("ABC123");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Then(p => p.Right('B').Right('C'));

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("ABC", result.First!.Text);
    }

    [Fact]
    public async Task ThenOptional_ContinuesIfSubPatternFails()
    {
        var snapshot = await CreateSnapshotAsync("AD");
        var optional = new CellPatternSearcher().Right('B').Right('C');

        var pattern = new CellPatternSearcher()
            .Find('A')
            .ThenOptional(optional)
            .Right('D');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task ThenOptional_IncludesMatchWhenSuccessful()
    {
        var snapshot = await CreateSnapshotAsync("ABCD");
        var optional = new CellPatternSearcher().Right('B').Right('C');

        var pattern = new CellPatternSearcher()
            .Find('A')
            .ThenOptional(optional)
            .Right('D');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("ABCD", result.First!.Text);
    }

    [Fact]
    public async Task ThenEither_UsesFirstIfMatches()
    {
        var snapshot = await CreateSnapshotAsync("AB");

        var pattern = new CellPatternSearcher()
            .Find('A')
            .ThenEither(
                p => p.Right('B'),
                p => p.Right('X'));

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("AB", result.First!.Text);
    }

    [Fact]
    public async Task ThenEither_UsesSecondIfFirstFails()
    {
        var snapshot = await CreateSnapshotAsync("AX");

        var pattern = new CellPatternSearcher()
            .Find('A')
            .ThenEither(
                p => p.Right('B'),
                p => p.Right('X'));

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("AX", result.First!.Text);
    }

    [Fact]
    public async Task ThenEither_FailsIfBothFail()
    {
        var snapshot = await CreateSnapshotAsync("AZ");

        var pattern = new CellPatternSearcher()
            .Find('A')
            .ThenEither(
                p => p.Right('B'),
                p => p.Right('X'));

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public async Task ThenEither_WithMatch_MatchesTextAtCurrentPosition()
    {
        // Simulates the prompt matching scenario:
        // [1 OK] $
        // [2 OK] $
        // [3 ERR:127] $
        var snapshot = await CreateSnapshotAsync(new[] {
            "[1 OK] $",
            "[2 OK] $",
            "[3 ERR:127] $"});

        var pattern = new CellPatternSearcher()
            .Find(c => c.X == 0 && c.Cell.Character == "[")
            .BeginCapture("seqno")
                .RightWhile(c => char.IsNumber(c.Cell.Character[0]))  // Continue while it's a number
            .EndCapture()
            .ThenEither(
                p => p.RightText(" OK]"),  // Use RightText to move and match each char
                p => p.RightText(" ERR:")
                    .BeginCapture("errno")
                        .RightWhile(c => char.IsNumber(c.Cell.Character[0]))  // Continue while it's a number
                    .EndCapture()
                    .Right(']')
            );

        var results = pattern.Search(snapshot);

        Assert.True(results.HasMatches);
        Assert.Equal(3, results.Count);
        
        // First match: [1 OK]
        Assert.Equal("1", results.Matches[0].GetCaptureText("seqno"));
        Assert.False(results.Matches[0].HasCapture("errno"));
        
        // Second match: [2 OK]
        Assert.Equal("2", results.Matches[1].GetCaptureText("seqno"));
        Assert.False(results.Matches[1].HasCapture("errno"));
        
        // Third match: [3 ERR:127]
        Assert.Equal("3", results.Matches[2].GetCaptureText("seqno"));
        Assert.True(results.Matches[2].HasCapture("errno"));
        Assert.Equal("127", results.Matches[2].GetCaptureText("errno"));
    }

    [Fact]
    public async Task Match_DebugCursorPosition()
    {
        // Test Match followed by RightWhile followed by Right
        var snapshot = await CreateSnapshotAsync(":127]");
        
        var pattern = new CellPatternSearcher()
            .Find(':')
            .BeginCapture("errno")
                .RightWhile(c => char.IsNumber(c.Cell.Character[0]))  // Should capture 127
            .EndCapture()
            .Right(']');  // Should match ]

        var result = pattern.Search(snapshot);
        Assert.True(result.HasMatches, "Pattern should match :127]");
        Assert.Equal(":127]", result.First!.Text);
        Assert.Equal("127", result.First!.GetCaptureText("errno"));
    }

    [Fact]
    public async Task ThenRepeat_RepeatsPatternNTimes()
    {
        var snapshot = await CreateSnapshotAsync("ABCABCABC");
        var abc = new CellPatternSearcher().Right('A').Right('B').Right('C');

        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right('B')
            .Right('C')
            .ThenRepeat(2, abc);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("ABCABCABC", result.First!.Text);
    }

    [Fact]
    public async Task ThenRepeat_WithoutCount_RepeatsWhileMatching()
    {
        var snapshot = await CreateSnapshotAsync("AAAB");
        var singleA = new CellPatternSearcher().Right('A');

        var pattern = new CellPatternSearcher()
            .Find('A')
            .ThenRepeat(singleA);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("AAA", result.First!.Text);
    }

    #endregion

    #region CellPatternMatch Tests

    [Fact]
    public async Task Match_Bounds_CoversAllCells()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "AB", "CD" });
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right('B')
            .Down('D')
            .Left('C');

        var result = pattern.Search(snapshot);
        var bounds = result.First!.Bounds;

        Assert.Equal(0, bounds.X);
        Assert.Equal(0, bounds.Y);
        Assert.Equal(2, bounds.Width);
        Assert.Equal(2, bounds.Height);
    }

    [Fact]
    public async Task Match_Start_IsFirstCell()
    {
        var snapshot = await CreateSnapshotAsync("XXX[ABC]XXX");
        var pattern = new CellPatternSearcher()
            .Find('[')
            .RightUntil(']');

        var result = pattern.Search(snapshot);

        Assert.Equal((3, 0), result.First!.Start);
    }

    [Fact]
    public async Task Match_End_IsLastCell()
    {
        var snapshot = await CreateSnapshotAsync("XXX[ABC]XXX");
        var pattern = new CellPatternSearcher()
            .Find('[')
            .RightUntil(']');

        var result = pattern.Search(snapshot);

        Assert.Equal((7, 0), result.First!.End);
    }

    [Fact]
    public async Task Match_Text_ConcatenatesAllCells()
    {
        var snapshot = await CreateSnapshotAsync("Hello");
        var pattern = new CellPatternSearcher()
            .Find('H')
            .RightText("ello");

        var result = pattern.Search(snapshot);

        Assert.Equal("Hello", result.First!.Text);
    }

    [Fact]
    public async Task Match_CaptureNames_ListsAllCaptures()
    {
        var snapshot = await CreateSnapshotAsync("A=1");
        var pattern = new CellPatternSearcher()
            .BeginCapture("key")
            .Find('A')
            .EndCapture()
            .Right('=')
            .BeginCapture("val")
            .Right('1')
            .EndCapture();

        var result = pattern.Search(snapshot);

        Assert.Contains("key", result.First!.CaptureNames);
        Assert.Contains("val", result.First!.CaptureNames);
    }

    #endregion

    #region CellPatternSearchResult Tests

    [Fact]
    public async Task Result_HasMatches_TrueWhenMatchesExist()
    {
        var snapshot = await CreateSnapshotAsync("test");
        var pattern = new CellPatternSearcher().Find('t');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task Result_HasMatches_FalseWhenNoMatches()
    {
        var snapshot = await CreateSnapshotAsync("test");
        var pattern = new CellPatternSearcher().Find('z');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public async Task Result_Count_ReturnsMatchCount()
    {
        var snapshot = await CreateSnapshotAsync("aaa");
        var pattern = new CellPatternSearcher().Find('a');

        var result = pattern.Search(snapshot);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Result_First_ReturnsFirstMatch()
    {
        var snapshot = await CreateSnapshotAsync("abc");
        var pattern = new CellPatternSearcher().Find(ctx => true);

        var result = pattern.Search(snapshot);

        Assert.NotNull(result.First);
        Assert.Equal((0, 0), result.First!.Start);
    }

    [Fact]
    public async Task Result_Matches_AreOrderedByPosition()
    {
        var snapshot = await CreateSnapshotAsync(new[] { "a", "a", "a" });
        var pattern = new CellPatternSearcher().Find('a');

        var result = pattern.Search(snapshot);

        Assert.Equal(0, result.Matches[0].Start.Y);
        Assert.Equal(1, result.Matches[1].Start.Y);
        Assert.Equal(2, result.Matches[2].Start.Y);
    }

    #endregion

    #region SearchFirst Tests

    [Fact]
    public async Task SearchFirst_ReturnsSingleMatch()
    {
        var snapshot = await CreateSnapshotAsync("aaa");
        var pattern = new CellPatternSearcher().Find('a');

        var match = pattern.SearchFirst(snapshot);

        Assert.NotNull(match);
        Assert.Equal((0, 0), match!.Start);
    }

    [Fact]
    public async Task SearchFirst_ReturnsNullWhenNoMatch()
    {
        var snapshot = await CreateSnapshotAsync("test");
        var pattern = new CellPatternSearcher().Find('z');

        var match = pattern.SearchFirst(snapshot);

        Assert.Null(match);
    }

    #endregion

    #region Extension Methods Tests

    [Fact]
    public async Task Region_SearchPattern_Works()
    {
        var snapshot = await CreateSnapshotAsync("Hello");
        var pattern = new CellPatternSearcher().Find('H');

        var result = snapshot.SearchPattern(pattern);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task Region_SearchFirstPattern_Works()
    {
        var snapshot = await CreateSnapshotAsync("Hello");
        var pattern = new CellPatternSearcher().Find('H');

        var match = snapshot.SearchFirstPattern(pattern);

        Assert.NotNull(match);
    }

    [Fact]
    public async Task Region_CreateSnapshot_FromMatch_Works()
    {
        var snapshot = await CreateSnapshotAsync("XX[content]XX");
        var pattern = new CellPatternSearcher()
            .Find('[')
            .RightUntil(']');

        var match = pattern.SearchFirst(snapshot);
        var region = snapshot.CreateSnapshot(match!);

        Assert.Equal(match!.Bounds.Width, region.Width);
        Assert.Equal(match!.Bounds.Height, region.Height);
    }

    [Fact]
    public async Task Region_CreateSnapshot_FromCapture_Works()
    {
        var snapshot = await CreateSnapshotAsync("[TAG] message");
        var pattern = new CellPatternSearcher()
            .Find('[')
            .BeginCapture("inner")
            .RightUntil(']')
            .EndCapture();

        var match = pattern.SearchFirst(snapshot);
        var region = snapshot.CreateSnapshot(match!, "inner");

        Assert.True(region.Width >= 3);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public async Task Scenario_ParseKeyValuePairs()
    {
        var snapshot = await CreateSnapshotAsync("Name: John\r\nAge: 30");
        var kvPattern = new CellPatternSearcher()
            .BeginCapture("key")
            .Find(ctx => char.IsLetter(ctx.Cell.Character[0]))
            .RightWhile(ctx => ctx.Cell.Character != ":")
            .EndCapture()
            .Right(':')
            .RightWhile(ctx => ctx.Cell.Character == " ")
            .BeginCapture("value")
            .RightWhile(ctx => ctx.Cell.Character != " " && ctx.Cell.Character != "\n" && ctx.X < snapshot.Width - 1)
            .EndCapture();

        var result = kvPattern.Search(snapshot);

        Assert.True(result.Count >= 2);
    }

    [Fact]
    public async Task Scenario_FindBoxDrawingCorners()
    {
        var snapshot = await CreateSnapshotAsync(new[]
        {
            "┌──────┐",
            "│      │",
            "└──────┘"
        });

        var pattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "┌")
            .RightWhile(ctx => ctx.Cell.Character == "─")
            .Right(ctx => ctx.Cell.Character == "┐");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public async Task Scenario_FindStyledText()
    {
        // This would need styled content - test structure only
        var snapshot = await CreateSnapshotAsync("Normal [Bold] Normal");

        var pattern = new CellPatternSearcher()
            .Find('[')
            .BeginCapture("styled")
            .RightWhile(ctx => ctx.Cell.Character != "]")
            .EndCapture()
            .Right(']');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("Bold", result.First!.GetCaptureText("styled"));
    }

    [Fact]
    public async Task Scenario_MatchLogEntry()
    {
        var snapshot = await CreateSnapshotAsync("[2024-01-01] [INFO] Application started");

        var pattern = new CellPatternSearcher()
            .Find('[')
            .BeginCapture("timestamp")
            .RightUntil(']')
            .EndCapture()
            .Right(' ')
            .Right('[')
            .BeginCapture("level")
            .RightUntil(']')
            .EndCapture()
            .Right(' ')
            .BeginCapture("message")
            .RightToEnd()
            .EndCapture();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Contains("2024-01-01", result.First!.GetCaptureText("timestamp"));
        Assert.Contains("INFO", result.First!.GetCaptureText("level"));
    }

    [Fact]
    public async Task Scenario_TwoDimensionalPattern()
    {
        var snapshot = await CreateSnapshotAsync(new[]
        {
            "╔═══╗",
            "║ X ║",
            "╚═══╝"
        });

        var pattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "╔")
            .RightWhile(ctx => ctx.Cell.Character == "═")
            .Right(ctx => ctx.Cell.Character == "╗")
            .Down(ctx => ctx.Cell.Character == "║")
            .Down(ctx => ctx.Cell.Character == "╝");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        // Pattern traverses rows 0, 1, 2 = 3 rows of height
        Assert.Equal(3, result.First!.Bounds.Height);
    }

    [Fact]
    public async Task Scenario_MultipleMatchesWithCaptures()
    {
        var snapshot = await CreateSnapshotAsync("x=1 y=2 z=3");

        var pattern = new CellPatternSearcher()
            .BeginCapture("var")
            .Find(ctx => char.IsLetter(ctx.Cell.Character[0]))
            .EndCapture()
            .Right('=')
            .BeginCapture("val")
            .Right(ctx => char.IsDigit(ctx.Cell.Character[0]))
            .EndCapture();

        var result = pattern.Search(snapshot);

        Assert.Equal(3, result.Count);
        Assert.Equal("x", result.Matches[0].GetCaptureText("var"));
        Assert.Equal("y", result.Matches[1].GetCaptureText("var"));
        Assert.Equal("z", result.Matches[2].GetCaptureText("var"));
    }

    [Fact]
    public async Task Scenario_Immutability_PatternCanBeReused()
    {
        var basePattern = new CellPatternSearcher().Find('[').RightUntil(']');

        var snapshot1 = await CreateSnapshotAsync("[A]");
        var snapshot2 = await CreateSnapshotAsync("[BBB]");

        var result1 = basePattern.Search(snapshot1);
        var result2 = basePattern.Search(snapshot2);

        Assert.True(result1.HasMatches);
        Assert.True(result2.HasMatches);
        Assert.NotEqual(result1.First!.Text, result2.First!.Text);
    }

    [Fact]
    public async Task Scenario_EdgeCase_EmptyRegion()
    {
        var snapshot = await CreateSnapshotAsync("", 10, 1);
        var pattern = new CellPatternSearcher().Find('A');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public async Task Scenario_EdgeCase_PatternAtBoundary()
    {
        var snapshot = await CreateSnapshotAsync("ABC", 3, 1);
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right('B')
            .Right('C');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal((2, 0), result.First!.End);
    }

    [Fact]
    public async Task Scenario_EdgeCase_PatternExceedsBoundary()
    {
        var snapshot = await CreateSnapshotAsync("AB", 2, 1);
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right('B')
            .Right('C');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    #endregion

    #region Debug Tests

    [Fact]
    public async Task FindOptions_Default_HasCorrectDefaults()
    {
        var options = FindOptions.Default;
        Assert.True(options.IncludeMatchInCells, "IncludeMatchInCells should be true by default");
        Assert.Equal(FindCursorPosition.End, options.CursorPosition);
    }
    
    [Fact]
    public async Task FindOptions_Constructor_HasCorrectDefaults()
    {
        var options = new FindOptions(); // Constructor with defaults
        Assert.True(options.IncludeMatchInCells, "IncludeMatchInCells should be true by default");
        Assert.Equal(FindCursorPosition.End, options.CursorPosition);
    }

    #endregion
}