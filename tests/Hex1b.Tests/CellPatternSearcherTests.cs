using Hex1b.Layout;
using Hex1b.Terminal.Automation;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive tests for the CellPatternSearcher API.
/// </summary>
public class CellPatternSearcherTests
{
    #region Helper Methods

    private static Hex1bTerminalSnapshot CreateSnapshot(string[] lines, int width = 80, int height = 24)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, width, height);

        foreach (var line in lines)
        {
            workload.Write(line + "\r\n");
        }

        return terminal.CreateSnapshot();
    }

    private static Hex1bTerminalSnapshot CreateSnapshot(string content, int width = 80, int height = 24)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, width, height);
        workload.Write(content);
        return terminal.CreateSnapshot();
    }

    #endregion

    #region TraversedCell Tests

    [Fact]
    public void TraversedCell_HasCorrectProperties()
    {
        var cell = new TerminalCell("A", null, null);
        var traversed = new TraversedCell(5, 10, cell, null);

        Assert.Equal(5, traversed.X);
        Assert.Equal(10, traversed.Y);
        Assert.Equal("A", traversed.Cell.Character);
        Assert.Null(traversed.CaptureNames);
    }

    [Fact]
    public void TraversedCell_WithCaptureNames()
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
    public void CellMatchContext_ProvidesRegionAccess()
    {
        var snapshot = CreateSnapshot("Hello World");
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
    public void CellMatchContext_ProvidesCurrentPosition()
    {
        var snapshot = CreateSnapshot("Hello");
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
    public void CellMatchContext_ProvidesMatchStartCell()
    {
        var snapshot = CreateSnapshot("ABC");
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
    public void CellMatchContext_ProvidesPreviousCell()
    {
        var snapshot = CreateSnapshot("ABC");
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
    public void CellMatchContext_GetRelative_ReturnsCorrectCell()
    {
        var snapshot = CreateSnapshot(new[] { "ABC", "DEF", "GHI" });
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
    public void CellMatchContext_GetAbsolute_ReturnsCorrectCell()
    {
        var snapshot = CreateSnapshot(new[] { "ABC", "DEF" });
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
    public void CellMatchContext_TraversedCells_TracksProgress()
    {
        var snapshot = CreateSnapshot("ABCD");
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
    public void Find_WithChar_MatchesSingleCharacter()
    {
        var snapshot = CreateSnapshot("Hello World");
        var pattern = new CellPatternSearcher().Find('W');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Single(result.Matches);
        Assert.Equal(6, result.First!.Start.X);
    }

    [Fact]
    public void Find_WithChar_FindsMultipleOccurrences()
    {
        var snapshot = CreateSnapshot("ababa");
        var pattern = new CellPatternSearcher().Find('a');

        var result = pattern.Search(snapshot);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Find_WithPredicate_MatchesCondition()
    {
        var snapshot = CreateSnapshot("abc123def");
        var pattern = new CellPatternSearcher()
            .Find(ctx => char.IsDigit(ctx.Cell.Character[0]));

        var result = pattern.Search(snapshot);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Find_WithRegexString_MatchesPattern()
    {
        var snapshot = CreateSnapshot("Name: John, Age: 30");
        var pattern = new CellPatternSearcher().Find(@"Age:\s*");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void Find_WithRegex_MatchesCompiledPattern()
    {
        var snapshot = CreateSnapshot("Error: file not found");
        var regex = new System.Text.RegularExpressions.Regex(@"Error:\s*");
        var pattern = new CellPatternSearcher().Find(regex);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void Find_NoMatch_ReturnsEmptyResult()
    {
        var snapshot = CreateSnapshot("Hello World");
        var pattern = new CellPatternSearcher().Find('Z');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
        Assert.Equal(0, result.Count);
        Assert.Null(result.First);
    }

    [Fact]
    public void Find_AcrossMultipleLines()
    {
        var snapshot = CreateSnapshot(new[] { "Line 1", "Line 2", "Line 3" });
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
    public void Right_WithChar_MovesAndMatches()
    {
        var snapshot = CreateSnapshot("AB");
        var pattern = new CellPatternSearcher().Find('A').Right('B');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(2, result.First!.Cells.Count);
    }

    [Fact]
    public void Right_WithPredicate_MovesAndMatches()
    {
        var snapshot = CreateSnapshot("A1");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right(ctx => char.IsDigit(ctx.Cell.Character[0]));

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void Right_FailsIfNoMatch()
    {
        var snapshot = CreateSnapshot("AC");
        var pattern = new CellPatternSearcher().Find('A').Right('B');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public void Right_WithCount_MovesMultipleCells()
    {
        var snapshot = CreateSnapshot("ABCDE");
        var pattern = new CellPatternSearcher().Find('A').Right(3);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(4, result.First!.Cells.Count); // A + 3 more
        Assert.Equal((3, 0), result.First!.End);
    }

    [Fact]
    public void Right_WithCountAndPredicate_AllMustMatch()
    {
        var snapshot = CreateSnapshot("A111B");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right(3, ctx => ctx.Cell.Character == "1");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void Right_WithCountAndPredicate_FailsIfAnyDontMatch()
    {
        var snapshot = CreateSnapshot("A121B");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right(3, ctx => ctx.Cell.Character == "1");

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public void Left_WithChar_MovesLeft()
    {
        var snapshot = CreateSnapshot("BA");
        var pattern = new CellPatternSearcher().Find('A').Left('B');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void Up_WithChar_MovesUp()
    {
        var snapshot = CreateSnapshot(new[] { "A", "B" });
        var pattern = new CellPatternSearcher().Find('B').Up('A');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void Down_WithChar_MovesDown()
    {
        var snapshot = CreateSnapshot(new[] { "A", "B" });
        var pattern = new CellPatternSearcher().Find('A').Down('B');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void Left_WithCount_MovesMultipleCells()
    {
        var snapshot = CreateSnapshot("ABCDE");
        var pattern = new CellPatternSearcher().Find('E').Left(4);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(5, result.First!.Cells.Count);
    }

    [Fact]
    public void Up_WithCount_MovesMultipleCells()
    {
        var snapshot = CreateSnapshot(new[] { "A", "B", "C", "D", "E" });
        var pattern = new CellPatternSearcher().Find('E').Up(4);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(5, result.First!.Cells.Count);
    }

    [Fact]
    public void Down_WithCount_MovesMultipleCells()
    {
        var snapshot = CreateSnapshot(new[] { "A", "B", "C", "D", "E" });
        var pattern = new CellPatternSearcher().Find('A').Down(4);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(5, result.First!.Cells.Count);
    }

    #endregion

    #region Text Sequence Tests

    [Fact]
    public void RightText_MatchesExactSequence()
    {
        var snapshot = CreateSnapshot("Hello World");
        var pattern = new CellPatternSearcher().Find('H').RightText("ello");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("Hello", result.First!.Text);
    }

    [Fact]
    public void RightText_FailsOnMismatch()
    {
        var snapshot = CreateSnapshot("Hello World");
        var pattern = new CellPatternSearcher().Find('H').RightText("allo");

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public void LeftText_MatchesExactSequence()
    {
        var snapshot = CreateSnapshot("Hello");
        var pattern = new CellPatternSearcher().Find('o').LeftText("lleH");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    #endregion

    #region While Tests

    [Fact]
    public void RightWhile_ConsumesMatchingCells()
    {
        var snapshot = CreateSnapshot("AAA123");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .RightWhile(ctx => ctx.Cell.Character == "A");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("AAA", result.First!.Text);
    }

    [Fact]
    public void RightWhile_StopsAtNonMatch()
    {
        var snapshot = CreateSnapshot("AAABBB");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .RightWhile(ctx => ctx.Cell.Character == "A");

        var result = pattern.Search(snapshot);

        Assert.Equal("AAA", result.First!.Text);
    }

    [Fact]
    public void RightWhile_ZeroMatchesIsValid()
    {
        var snapshot = CreateSnapshot("AB");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .RightWhile(ctx => ctx.Cell.Character == "X")
            .Right('B');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void LeftWhile_ConsumesMatchingCells()
    {
        var snapshot = CreateSnapshot("123AAA");
        var pattern = new CellPatternSearcher()
            .Find(ctx => ctx.X == 5 && ctx.Cell.Character == "A")
            .LeftWhile(ctx => ctx.Cell.Character == "A");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(3, result.First!.Cells.Count);
    }

    [Fact]
    public void UpWhile_ConsumesMatchingCells()
    {
        var snapshot = CreateSnapshot(new[] { "1", "A", "A", "A" });
        var pattern = new CellPatternSearcher()
            .Find(ctx => ctx.Y == 3 && ctx.Cell.Character == "A")
            .UpWhile(ctx => ctx.Cell.Character == "A");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(3, result.First!.Cells.Count);
    }

    [Fact]
    public void DownWhile_ConsumesMatchingCells()
    {
        var snapshot = CreateSnapshot(new[] { "A", "A", "A", "1" });
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
    public void RightUntil_WithPredicate_StopsAtMatch()
    {
        var snapshot = CreateSnapshot("ABC]DEF");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .RightUntil(ctx => ctx.Cell.Character == "]");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("ABC]", result.First!.Text);
    }

    [Fact]
    public void RightUntil_WithChar_StopsAtChar()
    {
        var snapshot = CreateSnapshot("[content]");
        var pattern = new CellPatternSearcher()
            .Find('[')
            .RightUntil(']');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("[content]", result.First!.Text);
    }

    [Fact]
    public void RightUntil_FailsIfNotFound()
    {
        var snapshot = CreateSnapshot("ABC");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .RightUntil(']');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public void LeftUntil_StopsAtMatch()
    {
        var snapshot = CreateSnapshot("[content]");
        var pattern = new CellPatternSearcher()
            .Find(']')
            .LeftUntil('[');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void UpUntil_StopsAtMatch()
    {
        var snapshot = CreateSnapshot(new[] { "-", "A", "A", "A" });
        var pattern = new CellPatternSearcher()
            .Find(ctx => ctx.Y == 3 && ctx.Cell.Character == "A")
            .UpUntil(ctx => ctx.Cell.Character == "-");

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(4, result.First!.Cells.Count);
    }

    [Fact]
    public void DownUntil_StopsAtMatch()
    {
        var snapshot = CreateSnapshot(new[] { "A", "A", "A", "-" });
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
    public void RightToEnd_GoesToEndOfLine()
    {
        var snapshot = CreateSnapshot("Hello", 10, 1);
        var pattern = new CellPatternSearcher()
            .Find('H')
            .RightToEnd();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal(10, result.First!.Cells.Count);
        Assert.Equal((9, 0), result.First!.End);
    }

    [Fact]
    public void LeftToStart_GoesToStartOfLine()
    {
        var snapshot = CreateSnapshot("Hello", 10, 1);
        var pattern = new CellPatternSearcher()
            .Find('o')
            .LeftToStart();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal((0, 0), result.First!.End);
    }

    [Fact]
    public void DownToBottom_GoesToBottomOfRegion()
    {
        var snapshot = CreateSnapshot(new[] { "A", "B", "C" }, 10, 5);
        var pattern = new CellPatternSearcher()
            .Find('A')
            .DownToBottom();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal((0, 4), result.First!.End);
    }

    [Fact]
    public void UpToTop_GoesToTopOfRegion()
    {
        var snapshot = CreateSnapshot(new[] { "A", "B", "C" }, 10, 5);
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
    public void Capture_CapturesTraversedCells()
    {
        var snapshot = CreateSnapshot("Name: John");
        var pattern = new CellPatternSearcher()
            .Find(@"Name:\s*")
            .BeginCapture("value")
            .RightWhile(ctx => ctx.Cell.Character != " " && ctx.X < 10)
            .EndCapture();

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.True(result.First!.HasCapture("value"));
        Assert.Equal("John", result.First!.GetCaptureText("value"));
    }

    [Fact]
    public void Capture_MultipleCapturesInOnePattern()
    {
        var snapshot = CreateSnapshot("A=1 B=2");
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
    public void Capture_NestedCaptures()
    {
        var snapshot = CreateSnapshot("[ERROR] msg");
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
    public void Capture_GetCaptureBounds_ReturnsCorrectRect()
    {
        var snapshot = CreateSnapshot("XX[val]XX");
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
    public void Capture_NonExistentCapture_ReturnsEmpty()
    {
        var snapshot = CreateSnapshot("test");
        var pattern = new CellPatternSearcher().Find('t');

        var result = pattern.Search(snapshot);

        Assert.False(result.First!.HasCapture("nonexistent"));
        Assert.Empty(result.First!.GetCapture("nonexistent"));
        Assert.Equal("", result.First!.GetCaptureText("nonexistent"));
    }

    #endregion

    #region Composition Tests

    [Fact]
    public void Then_ChainsPatterns()
    {
        var prefix = new CellPatternSearcher().Find('[').RightUntil(']');
        var suffix = new CellPatternSearcher().Right(' ').RightToEnd();

        var snapshot = CreateSnapshot("[TAG] message", 20, 1);
        var pattern = new CellPatternSearcher()
            .Then(prefix)
            .Then(suffix);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void Then_WithAction_BuildsSubPattern()
    {
        var snapshot = CreateSnapshot("ABC123");
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Then(p => p.Right('B').Right('C'));

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("ABC", result.First!.Text);
    }

    [Fact]
    public void ThenOptional_ContinuesIfSubPatternFails()
    {
        var snapshot = CreateSnapshot("AD");
        var optional = new CellPatternSearcher().Right('B').Right('C');

        var pattern = new CellPatternSearcher()
            .Find('A')
            .ThenOptional(optional)
            .Right('D');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void ThenOptional_IncludesMatchWhenSuccessful()
    {
        var snapshot = CreateSnapshot("ABCD");
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
    public void ThenEither_UsesFirstIfMatches()
    {
        var snapshot = CreateSnapshot("AB");
        var first = new CellPatternSearcher().Right('B');
        var second = new CellPatternSearcher().Right('X');

        var pattern = new CellPatternSearcher()
            .Find('A')
            .ThenEither(first, second);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("AB", result.First!.Text);
    }

    [Fact]
    public void ThenEither_UsesSecondIfFirstFails()
    {
        var snapshot = CreateSnapshot("AX");
        var first = new CellPatternSearcher().Right('B');
        var second = new CellPatternSearcher().Right('X');

        var pattern = new CellPatternSearcher()
            .Find('A')
            .ThenEither(first, second);

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal("AX", result.First!.Text);
    }

    [Fact]
    public void ThenEither_FailsIfBothFail()
    {
        var snapshot = CreateSnapshot("AZ");
        var first = new CellPatternSearcher().Right('B');
        var second = new CellPatternSearcher().Right('X');

        var pattern = new CellPatternSearcher()
            .Find('A')
            .ThenEither(first, second);

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public void ThenRepeat_RepeatsPatternNTimes()
    {
        var snapshot = CreateSnapshot("ABCABCABC");
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
    public void ThenRepeat_WithoutCount_RepeatsWhileMatching()
    {
        var snapshot = CreateSnapshot("AAAB");
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
    public void Match_Bounds_CoversAllCells()
    {
        var snapshot = CreateSnapshot(new[] { "AB", "CD" });
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
    public void Match_Start_IsFirstCell()
    {
        var snapshot = CreateSnapshot("XXX[ABC]XXX");
        var pattern = new CellPatternSearcher()
            .Find('[')
            .RightUntil(']');

        var result = pattern.Search(snapshot);

        Assert.Equal((3, 0), result.First!.Start);
    }

    [Fact]
    public void Match_End_IsLastCell()
    {
        var snapshot = CreateSnapshot("XXX[ABC]XXX");
        var pattern = new CellPatternSearcher()
            .Find('[')
            .RightUntil(']');

        var result = pattern.Search(snapshot);

        Assert.Equal((7, 0), result.First!.End);
    }

    [Fact]
    public void Match_Text_ConcatenatesAllCells()
    {
        var snapshot = CreateSnapshot("Hello");
        var pattern = new CellPatternSearcher()
            .Find('H')
            .RightText("ello");

        var result = pattern.Search(snapshot);

        Assert.Equal("Hello", result.First!.Text);
    }

    [Fact]
    public void Match_CaptureNames_ListsAllCaptures()
    {
        var snapshot = CreateSnapshot("A=1");
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
    public void Result_HasMatches_TrueWhenMatchesExist()
    {
        var snapshot = CreateSnapshot("test");
        var pattern = new CellPatternSearcher().Find('t');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void Result_HasMatches_FalseWhenNoMatches()
    {
        var snapshot = CreateSnapshot("test");
        var pattern = new CellPatternSearcher().Find('z');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public void Result_Count_ReturnsMatchCount()
    {
        var snapshot = CreateSnapshot("aaa");
        var pattern = new CellPatternSearcher().Find('a');

        var result = pattern.Search(snapshot);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Result_First_ReturnsFirstMatch()
    {
        var snapshot = CreateSnapshot("abc");
        var pattern = new CellPatternSearcher().Find(ctx => true);

        var result = pattern.Search(snapshot);

        Assert.NotNull(result.First);
        Assert.Equal((0, 0), result.First!.Start);
    }

    [Fact]
    public void Result_Matches_AreOrderedByPosition()
    {
        var snapshot = CreateSnapshot(new[] { "a", "a", "a" });
        var pattern = new CellPatternSearcher().Find('a');

        var result = pattern.Search(snapshot);

        Assert.Equal(0, result.Matches[0].Start.Y);
        Assert.Equal(1, result.Matches[1].Start.Y);
        Assert.Equal(2, result.Matches[2].Start.Y);
    }

    #endregion

    #region SearchFirst Tests

    [Fact]
    public void SearchFirst_ReturnsSingleMatch()
    {
        var snapshot = CreateSnapshot("aaa");
        var pattern = new CellPatternSearcher().Find('a');

        var match = pattern.SearchFirst(snapshot);

        Assert.NotNull(match);
        Assert.Equal((0, 0), match!.Start);
    }

    [Fact]
    public void SearchFirst_ReturnsNullWhenNoMatch()
    {
        var snapshot = CreateSnapshot("test");
        var pattern = new CellPatternSearcher().Find('z');

        var match = pattern.SearchFirst(snapshot);

        Assert.Null(match);
    }

    #endregion

    #region Extension Methods Tests

    [Fact]
    public void Region_SearchPattern_Works()
    {
        var snapshot = CreateSnapshot("Hello");
        var pattern = new CellPatternSearcher().Find('H');

        var result = snapshot.SearchPattern(pattern);

        Assert.True(result.HasMatches);
    }

    [Fact]
    public void Region_SearchFirstPattern_Works()
    {
        var snapshot = CreateSnapshot("Hello");
        var pattern = new CellPatternSearcher().Find('H');

        var match = snapshot.SearchFirstPattern(pattern);

        Assert.NotNull(match);
    }

    [Fact]
    public void Region_CreateSnapshot_FromMatch_Works()
    {
        var snapshot = CreateSnapshot("XX[content]XX");
        var pattern = new CellPatternSearcher()
            .Find('[')
            .RightUntil(']');

        var match = pattern.SearchFirst(snapshot);
        var region = snapshot.CreateSnapshot(match!);

        Assert.Equal(match!.Bounds.Width, region.Width);
        Assert.Equal(match!.Bounds.Height, region.Height);
    }

    [Fact]
    public void Region_CreateSnapshot_FromCapture_Works()
    {
        var snapshot = CreateSnapshot("[TAG] message");
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
    public void Scenario_ParseKeyValuePairs()
    {
        var snapshot = CreateSnapshot("Name: John\r\nAge: 30");
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
    public void Scenario_FindBoxDrawingCorners()
    {
        var snapshot = CreateSnapshot(new[]
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
    public void Scenario_FindStyledText()
    {
        // This would need styled content - test structure only
        var snapshot = CreateSnapshot("Normal [Bold] Normal");

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
    public void Scenario_MatchLogEntry()
    {
        var snapshot = CreateSnapshot("[2024-01-01] [INFO] Application started");

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
    public void Scenario_TwoDimensionalPattern()
    {
        var snapshot = CreateSnapshot(new[]
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
    public void Scenario_MultipleMatchesWithCaptures()
    {
        var snapshot = CreateSnapshot("x=1 y=2 z=3");

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
    public void Scenario_Immutability_PatternCanBeReused()
    {
        var basePattern = new CellPatternSearcher().Find('[').RightUntil(']');

        var snapshot1 = CreateSnapshot("[A]");
        var snapshot2 = CreateSnapshot("[BBB]");

        var result1 = basePattern.Search(snapshot1);
        var result2 = basePattern.Search(snapshot2);

        Assert.True(result1.HasMatches);
        Assert.True(result2.HasMatches);
        Assert.NotEqual(result1.First!.Text, result2.First!.Text);
    }

    [Fact]
    public void Scenario_EdgeCase_EmptyRegion()
    {
        var snapshot = CreateSnapshot("", 10, 1);
        var pattern = new CellPatternSearcher().Find('A');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    [Fact]
    public void Scenario_EdgeCase_PatternAtBoundary()
    {
        var snapshot = CreateSnapshot("ABC", 3, 1);
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right('B')
            .Right('C');

        var result = pattern.Search(snapshot);

        Assert.True(result.HasMatches);
        Assert.Equal((2, 0), result.First!.End);
    }

    [Fact]
    public void Scenario_EdgeCase_PatternExceedsBoundary()
    {
        var snapshot = CreateSnapshot("AB", 2, 1);
        var pattern = new CellPatternSearcher()
            .Find('A')
            .Right('B')
            .Right('C');

        var result = pattern.Search(snapshot);

        Assert.False(result.HasMatches);
    }

    #endregion
}
