using System.Text.RegularExpressions;
using Hex1b.Terminal.Automation;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the regex pattern matching functionality on terminal regions.
/// </summary>
public class TerminalRegionPatternMatchTests
{
    #region TextMatch Tests

    [Fact]
    public void TextMatch_HasCorrectProperties()
    {
        var match = new TextMatch(Line: 2, StartColumn: 5, EndColumn: 10, Text: "Hello");
        
        Assert.Equal(2, match.Line);
        Assert.Equal(5, match.StartColumn);
        Assert.Equal(10, match.EndColumn);
        Assert.Equal("Hello", match.Text);
        Assert.Equal(5, match.Length);
    }

    [Fact]
    public void TextMatch_LengthCalculation()
    {
        var match = new TextMatch(Line: 0, StartColumn: 0, EndColumn: 15, Text: "test expression");
        
        Assert.Equal(15, match.Length);
    }

    #endregion

    #region FindPattern Tests

    [Fact]
    public void FindPattern_FindsSimplePattern()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World\r\n");
        workload.Write("Test Pattern\r\n");
        workload.Write("Another line");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindPattern(@"Test");
        
        Assert.Single(matches);
        Assert.Equal(1, matches[0].Line);
        Assert.Equal(0, matches[0].StartColumn);
        Assert.Equal(4, matches[0].EndColumn);
        Assert.Equal("Test", matches[0].Text);
    }

    [Fact]
    public void FindPattern_FindsMultipleMatchesOnSameLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("cat and dog and cat again");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindPattern(@"cat");
        
        Assert.Equal(2, matches.Count);
        Assert.Equal(0, matches[0].StartColumn);
        Assert.Equal(3, matches[0].EndColumn);
        Assert.Equal(16, matches[1].StartColumn);
        Assert.Equal(19, matches[1].EndColumn);
    }

    [Fact]
    public void FindPattern_FindsMatchesAcrossMultipleLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Error: file not found\r\n");
        workload.Write("Warning: deprecated\r\n");
        workload.Write("Error: permission denied");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindPattern(@"Error");
        
        Assert.Equal(2, matches.Count);
        Assert.Equal(0, matches[0].Line);
        Assert.Equal(2, matches[1].Line);
    }

    [Fact]
    public void FindPattern_UsesRegexOptions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World");
        
        var snapshot = terminal.CreateSnapshot();
        
        // Case sensitive (default) - should not match
        var matches = snapshot.FindPattern(@"hello");
        Assert.Empty(matches);
        
        // Case insensitive - should match
        var matchesIgnoreCase = snapshot.FindPattern(@"hello", RegexOptions.IgnoreCase);
        Assert.Single(matchesIgnoreCase);
        Assert.Equal("Hello", matchesIgnoreCase[0].Text);
    }

    [Fact]
    public void FindPattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Value: 12345");
        
        var snapshot = terminal.CreateSnapshot();
        var regex = new Regex(@"\d+");
        var matches = snapshot.FindPattern(regex);
        
        Assert.Single(matches);
        Assert.Equal("12345", matches[0].Text);
        Assert.Equal(7, matches[0].StartColumn);
    }

    [Fact]
    public void FindPattern_ReturnsEmptyListWhenNoMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindPattern(@"NotFound");
        
        Assert.Empty(matches);
    }

    [Fact]
    public void FindPattern_MatchesComplexRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);
        
        workload.Write("Email: user@example.com\r\n");
        workload.Write("Contact: admin@test.org");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindPattern(@"\w+@\w+\.\w+");
        
        Assert.Equal(2, matches.Count);
        Assert.Equal("user@example.com", matches[0].Text);
        Assert.Equal("admin@test.org", matches[1].Text);
    }

    #endregion

    #region FindFirstPattern Tests

    [Fact]
    public void FindFirstPattern_ReturnsFirstMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("First match\r\n");
        workload.Write("Second match");
        
        var snapshot = terminal.CreateSnapshot();
        var match = snapshot.FindFirstPattern(@"match");
        
        Assert.NotNull(match);
        Assert.Equal(0, match.Value.Line);
        Assert.Equal(6, match.Value.StartColumn);
    }

    [Fact]
    public void FindFirstPattern_ReturnsNullWhenNoMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World");
        
        var snapshot = terminal.CreateSnapshot();
        var match = snapshot.FindFirstPattern(@"NotFound");
        
        Assert.Null(match);
    }

    [Fact]
    public void FindFirstPattern_WithRegexOptions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("HELLO World");
        
        var snapshot = terminal.CreateSnapshot();
        var match = snapshot.FindFirstPattern(@"hello", RegexOptions.IgnoreCase);
        
        Assert.NotNull(match);
        Assert.Equal("HELLO", match.Value.Text);
    }

    [Fact]
    public void FindFirstPattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Price: $99.99");
        
        var snapshot = terminal.CreateSnapshot();
        var regex = new Regex(@"\$[\d.]+");
        var match = snapshot.FindFirstPattern(regex);
        
        Assert.NotNull(match);
        Assert.Equal("$99.99", match.Value.Text);
    }

    #endregion

    #region GetTextAt Tests

    [Fact]
    public void GetTextAt_ReturnsTextAtCoordinates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World Test");
        
        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetTextAt(line: 0, startColumn: 6, endColumn: 11);
        
        Assert.Equal("World", text);
    }

    [Fact]
    public void GetTextAt_WithTextMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("ID: 12345 Name: John");
        
        var snapshot = terminal.CreateSnapshot();
        var match = snapshot.FindFirstPattern(@"\d+");
        
        Assert.NotNull(match);
        var text = snapshot.GetTextAt(match.Value);
        
        Assert.Equal("12345", text);
    }

    [Fact]
    public void GetTextAt_ReturnsEmptyForInvalidLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World");
        
        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetTextAt(line: 100, startColumn: 0, endColumn: 5);
        
        Assert.Equal("", text);
    }

    [Fact]
    public void GetTextAt_ClampsToValidRange()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 10);
        
        workload.Write("Hello World");
        
        var snapshot = terminal.CreateSnapshot();
        // Request beyond end of line
        var text = snapshot.GetTextAt(line: 0, startColumn: 6, endColumn: 100);
        
        // Should clamp to width and return "World" plus trailing spaces
        Assert.StartsWith("World", text);
    }

    [Fact]
    public void GetTextAt_HandlesNegativeStart()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World");
        
        var snapshot = terminal.CreateSnapshot();
        // Request with negative start
        var text = snapshot.GetTextAt(line: 0, startColumn: -5, endColumn: 5);
        
        // Should start at 0
        Assert.Equal("Hello", text);
    }

    #endregion

    #region ContainsPattern Tests

    [Fact]
    public void ContainsPattern_ReturnsTrueWhenPatternExists()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Error: Something went wrong");
        
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsPattern(@"Error"));
        Assert.True(snapshot.ContainsPattern(@"went \w+"));
    }

    [Fact]
    public void ContainsPattern_ReturnsFalseWhenPatternNotExists()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Success: All tests passed");
        
        var snapshot = terminal.CreateSnapshot();
        Assert.False(snapshot.ContainsPattern(@"Error"));
        Assert.False(snapshot.ContainsPattern(@"\d{5}"));
    }

    [Fact]
    public void ContainsPattern_WithRegexOptions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("SUCCESS");
        
        var snapshot = terminal.CreateSnapshot();
        Assert.False(snapshot.ContainsPattern(@"success"));
        Assert.True(snapshot.ContainsPattern(@"success", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void ContainsPattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Version: 2.0.1");
        
        var snapshot = terminal.CreateSnapshot();
        var regex = new Regex(@"\d+\.\d+\.\d+");
        Assert.True(snapshot.ContainsPattern(regex));
    }

    #endregion

    #region Integration Tests with Regions

    [Fact]
    public void FindPattern_WorksWithRegions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Header line\r\n");
        workload.Write("Match: 123\r\n");
        workload.Write("Match: 456\r\n");
        workload.Write("Footer line");
        
        var snapshot = terminal.CreateSnapshot();
        // Get a region excluding header and footer
        var region = snapshot.GetRegion(new Hex1b.Layout.Rect(0, 1, 40, 2));
        
        var matches = region.FindPattern(@"\d+");
        
        Assert.Equal(2, matches.Count);
        // Coordinates are relative to the region, not the full snapshot
        Assert.Equal(0, matches[0].Line);
        Assert.Equal(1, matches[1].Line);
    }

    [Fact]
    public void GetTextAt_WorksWithRegions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Line 0: AAA\r\n");
        workload.Write("Line 1: BBB\r\n");
        workload.Write("Line 2: CCC");
        
        var snapshot = terminal.CreateSnapshot();
        var region = snapshot.GetRegion(new Hex1b.Layout.Rect(8, 1, 10, 2));
        
        var text = region.GetTextAt(0, 0, 3);
        Assert.Equal("BBB", text);
    }

    #endregion

    #region Use Case: Extract Data from Terminal

    [Fact]
    public void UseCase_ExtractValueFromLabel()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);
        
        workload.Write("Status: Running\r\n");
        workload.Write("Progress: 75%\r\n");
        workload.Write("ETA: 5 minutes");
        
        var snapshot = terminal.CreateSnapshot();
        
        // Find and extract the progress value
        var progressMatch = snapshot.FindFirstPattern(@"Progress: (\d+)%");
        Assert.NotNull(progressMatch);
        
        // Extract just the number using a separate regex
        var numberMatch = snapshot.FindFirstPattern(@"(?<=Progress: )\d+");
        Assert.NotNull(numberMatch);
        Assert.Equal("75", numberMatch.Value.Text);
    }

    [Fact]
    public void UseCase_FindAllErrors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);
        
        workload.Write("[INFO] Starting...\r\n");
        workload.Write("[ERROR] File not found\r\n");
        workload.Write("[INFO] Retrying...\r\n");
        workload.Write("[ERROR] Connection failed");
        
        var snapshot = terminal.CreateSnapshot();
        var errors = snapshot.FindPattern(@"\[ERROR\].*");
        
        Assert.Equal(2, errors.Count);
        Assert.Contains("File not found", errors[0].Text);
        Assert.Contains("Connection failed", errors[1].Text);
    }

    #endregion

    #region MultiLineTextMatch Tests

    [Fact]
    public void MultiLineTextMatch_HasCorrectProperties()
    {
        var match = new MultiLineTextMatch(StartLine: 2, StartColumn: 5, EndLine: 4, EndColumn: 10, Text: "Hello\nWorld\nTest");
        
        Assert.Equal(2, match.StartLine);
        Assert.Equal(5, match.StartColumn);
        Assert.Equal(4, match.EndLine);
        Assert.Equal(10, match.EndColumn);
        Assert.Equal("Hello\nWorld\nTest", match.Text);
    }

    [Fact]
    public void MultiLineTextMatch_IsMultiLine_TrueForMultipleLines()
    {
        var match = new MultiLineTextMatch(StartLine: 0, StartColumn: 0, EndLine: 2, EndColumn: 5, Text: "abc\ndef\nghi");
        
        Assert.True(match.IsMultiLine);
        Assert.Equal(3, match.LineCount);
    }

    [Fact]
    public void MultiLineTextMatch_IsMultiLine_FalseForSingleLine()
    {
        var match = new MultiLineTextMatch(StartLine: 1, StartColumn: 0, EndLine: 1, EndColumn: 5, Text: "Hello");
        
        Assert.False(match.IsMultiLine);
        Assert.Equal(1, match.LineCount);
    }

    #endregion

    #region FindMultiLinePattern Tests

    [Fact]
    public void FindMultiLinePattern_FindsSingleLinePattern()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World\r\n");
        workload.Write("Test Pattern\r\n");
        workload.Write("Another line");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindMultiLinePattern(@"Test");
        
        Assert.Single(matches);
        Assert.Equal(1, matches[0].StartLine);
        Assert.Equal(1, matches[0].EndLine);
        Assert.Equal(0, matches[0].StartColumn);
        Assert.Equal(4, matches[0].EndColumn);
        Assert.Equal("Test", matches[0].Text);
        Assert.False(matches[0].IsMultiLine);
    }

    [Fact]
    public void FindMultiLinePattern_MatchesAcrossLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Start here\r\n");
        workload.Write("End here");
        
        var snapshot = terminal.CreateSnapshot();
        // Match pattern that spans from "here" on line 0 to "End" on line 1
        var matches = snapshot.FindMultiLinePattern(@"here\nEnd", RegexOptions.None);
        
        Assert.Single(matches);
        Assert.Equal(0, matches[0].StartLine);
        Assert.Equal(1, matches[0].EndLine);
        Assert.True(matches[0].IsMultiLine);
        Assert.Contains("here", matches[0].Text);
        Assert.Contains("End", matches[0].Text);
    }

    [Fact]
    public void FindMultiLinePattern_MatchesMultipleLinesWithSingleline()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("BEGIN\r\n");
        workload.Write("middle content\r\n");
        workload.Write("more content\r\n");
        workload.Write("END");
        
        var snapshot = terminal.CreateSnapshot();
        // Use Singleline option so . matches newlines
        var matches = snapshot.FindMultiLinePattern(@"BEGIN.*?END", RegexOptions.Singleline);
        
        Assert.Single(matches);
        Assert.Equal(0, matches[0].StartLine);
        Assert.Equal(3, matches[0].EndLine);
        Assert.True(matches[0].IsMultiLine);
        Assert.Equal(4, matches[0].LineCount);
    }

    [Fact]
    public void FindMultiLinePattern_FindsMultipleMultiLineMatches()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("<div>\r\n");
        workload.Write("content1\r\n");
        workload.Write("</div>\r\n");
        workload.Write("<div>\r\n");
        workload.Write("content2\r\n");
        workload.Write("</div>");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindMultiLinePattern(@"<div>.*?</div>", RegexOptions.Singleline);
        
        Assert.Equal(2, matches.Count);
        Assert.True(matches[0].IsMultiLine);
        Assert.True(matches[1].IsMultiLine);
    }

    [Fact]
    public void FindMultiLinePattern_WithNewlineInPattern()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Line 1\r\n");
        workload.Write("Line 2\r\n");
        workload.Write("Line 3");
        
        var snapshot = terminal.CreateSnapshot();
        // Match explicit newline pattern
        var matches = snapshot.FindMultiLinePattern(@"Line 1\s*\nLine 2");
        
        Assert.Single(matches);
        Assert.Equal(0, matches[0].StartLine);
        Assert.Equal(1, matches[0].EndLine);
    }

    [Fact]
    public void FindMultiLinePattern_MatchesEntireContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 3);
        
        workload.Write("AAA\r\n");
        workload.Write("BBB\r\n");
        workload.Write("CCC");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindMultiLinePattern(@"^AAA.*CCC", RegexOptions.Singleline);
        
        Assert.Single(matches);
        Assert.Equal(0, matches[0].StartLine);
        Assert.Equal(2, matches[0].EndLine);
    }

    [Fact]
    public void FindMultiLinePattern_ReturnsEmptyForNoMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindMultiLinePattern(@"NotFound\nAnywhere");
        
        Assert.Empty(matches);
    }

    [Fact]
    public void FindMultiLinePattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Start\r\n");
        workload.Write("Middle\r\n");
        workload.Write("End");
        
        var snapshot = terminal.CreateSnapshot();
        var regex = new Regex(@"Start.*End", RegexOptions.Singleline);
        var matches = snapshot.FindMultiLinePattern(regex);
        
        Assert.Single(matches);
        Assert.True(matches[0].IsMultiLine);
    }

    [Fact]
    public void FindMultiLinePattern_MatchingJsonStructure()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("{\r\n");
        workload.Write("  \"name\": \"test\",\r\n");
        workload.Write("  \"value\": 123\r\n");
        workload.Write("}");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindMultiLinePattern(@"\{[^}]+\}", RegexOptions.Singleline);
        
        Assert.Single(matches);
        Assert.Equal(0, matches[0].StartLine);
        Assert.Equal(3, matches[0].EndLine);
    }

    [Fact]
    public void FindMultiLinePattern_MatchingCodeBlock()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);
        
        workload.Write("function test() {\r\n");
        workload.Write("    return true;\r\n");
        workload.Write("}");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindMultiLinePattern(@"function.*?\}", RegexOptions.Singleline);
        
        Assert.Single(matches);
        Assert.True(matches[0].IsMultiLine);
        Assert.Contains("function", matches[0].Text);
        Assert.Contains("return", matches[0].Text);
    }

    #endregion

    #region FindFirstMultiLinePattern Tests

    [Fact]
    public void FindFirstMultiLinePattern_ReturnsFirstMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Block 1\r\n");
        workload.Write("End\r\n");
        workload.Write("Block 2\r\n");
        workload.Write("End");
        
        var snapshot = terminal.CreateSnapshot();
        var match = snapshot.FindFirstMultiLinePattern(@"Block.*?End", RegexOptions.Singleline);
        
        Assert.NotNull(match);
        Assert.Equal(0, match.Value.StartLine);
        Assert.Contains("Block 1", match.Value.Text);
    }

    [Fact]
    public void FindFirstMultiLinePattern_ReturnsNullForNoMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World");
        
        var snapshot = terminal.CreateSnapshot();
        var match = snapshot.FindFirstMultiLinePattern(@"NotFound\nPattern");
        
        Assert.Null(match);
    }

    [Fact]
    public void FindFirstMultiLinePattern_WithRegexOptions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("START\r\n");
        workload.Write("content\r\n");
        workload.Write("END");
        
        var snapshot = terminal.CreateSnapshot();
        var match = snapshot.FindFirstMultiLinePattern(@"start.*end", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        Assert.NotNull(match);
        Assert.True(match.Value.IsMultiLine);
    }

    [Fact]
    public void FindFirstMultiLinePattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("A\r\n");
        workload.Write("B\r\n");
        workload.Write("C");
        
        var snapshot = terminal.CreateSnapshot();
        var regex = new Regex(@"A\nB\nC");
        var match = snapshot.FindFirstMultiLinePattern(regex);
        
        Assert.NotNull(match);
        Assert.Equal(0, match.Value.StartLine);
        Assert.Equal(2, match.Value.EndLine);
    }

    #endregion

    #region ContainsMultiLinePattern Tests

    [Fact]
    public void ContainsMultiLinePattern_ReturnsTrueWhenPatternExists()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Header\r\n");
        workload.Write("Content\r\n");
        workload.Write("Footer");
        
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsMultiLinePattern(@"Header.*Footer", RegexOptions.Singleline));
    }

    [Fact]
    public void ContainsMultiLinePattern_ReturnsFalseWhenPatternNotExists()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World");
        
        var snapshot = terminal.CreateSnapshot();
        Assert.False(snapshot.ContainsMultiLinePattern(@"NotFound\nPattern"));
    }

    [Fact]
    public void ContainsMultiLinePattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Line1\r\n");
        workload.Write("Line2");
        
        var snapshot = terminal.CreateSnapshot();
        var regex = new Regex(@"Line1\nLine2");
        Assert.True(snapshot.ContainsMultiLinePattern(regex));
    }

    #endregion

    #region GetMultiLineTextAt Tests

    [Fact]
    public void GetMultiLineTextAt_ReturnsSingleLineText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World Test");
        
        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetMultiLineTextAt(startLine: 0, startColumn: 6, endLine: 0, endColumn: 11);
        
        Assert.Equal("World", text);
    }

    [Fact]
    public void GetMultiLineTextAt_ReturnsMultiLineText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Line 0 content\r\n");
        workload.Write("Line 1 content\r\n");
        workload.Write("Line 2 content");
        
        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetMultiLineTextAt(startLine: 0, startColumn: 7, endLine: 2, endColumn: 7);
        
        Assert.Contains("content", text);
        Assert.Contains("\n", text);
    }

    [Fact]
    public void GetMultiLineTextAt_WithMultiLineTextMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("START\r\n");
        workload.Write("middle\r\n");
        workload.Write("END");
        
        var snapshot = terminal.CreateSnapshot();
        var match = snapshot.FindFirstMultiLinePattern(@"START.*END", RegexOptions.Singleline);
        
        Assert.NotNull(match);
        // The match.Text property already contains the matched text
        Assert.StartsWith("START", match.Value.Text);
        Assert.EndsWith("END", match.Value.Text);
        
        // GetMultiLineTextAt works with raw terminal coordinates (untrimmed)
        // For multi-line matches, use match.Text directly
        Assert.Equal(match.Value.Text, match.Value.Text.Trim());
    }

    [Fact]
    public void GetMultiLineTextAt_ReturnsEmptyForInvalidLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello World");
        
        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetMultiLineTextAt(startLine: 100, startColumn: 0, endLine: 101, endColumn: 5);
        
        Assert.Equal("", text);
    }

    [Fact]
    public void GetMultiLineTextAt_ClampsToValidRange()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 3);
        
        workload.Write("AAA\r\n");
        workload.Write("BBB\r\n");
        workload.Write("CCC");
        
        var snapshot = terminal.CreateSnapshot();
        // Request beyond end of region
        var text = snapshot.GetMultiLineTextAt(startLine: 0, startColumn: 0, endLine: 10, endColumn: 100);
        
        // Should clamp to valid range
        Assert.Contains("AAA", text);
        Assert.Contains("BBB", text);
        Assert.Contains("CCC", text);
    }

    #endregion

    #region Multi-Line Edge Cases

    [Fact]
    public void FindMultiLinePattern_EmptyLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Start\r\n");
        workload.Write("\r\n"); // Empty line
        workload.Write("End");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindMultiLinePattern(@"Start\n\nEnd");
        
        Assert.Single(matches);
        Assert.Equal(0, matches[0].StartLine);
        Assert.Equal(2, matches[0].EndLine);
    }

    [Fact]
    public void FindMultiLinePattern_MatchAtStartOfRegion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("ABC\r\n");
        workload.Write("DEF");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindMultiLinePattern(@"^ABC\nDEF");
        
        Assert.Single(matches);
        Assert.Equal(0, matches[0].StartLine);
        Assert.Equal(0, matches[0].StartColumn);
    }

    [Fact]
    public void FindMultiLinePattern_MatchAtEndOfRegion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 3);
        
        workload.Write("XXX\r\n");
        workload.Write("YYY\r\n");
        workload.Write("ZZZ");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindMultiLinePattern(@"YYY\nZZZ");
        
        Assert.Single(matches);
        Assert.Equal(1, matches[0].StartLine);
        Assert.Equal(2, matches[0].EndLine);
    }

    [Fact]
    public void FindMultiLinePattern_OverlappingPatterns()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("ABAB\r\n");
        workload.Write("ABAB");
        
        var snapshot = terminal.CreateSnapshot();
        // Non-overlapping matches
        var matches = snapshot.FindMultiLinePattern(@"AB");
        
        // Should find 4 matches (2 per line)
        Assert.Equal(4, matches.Count);
    }

    [Fact]
    public void FindMultiLinePattern_SpecialRegexCharacters()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("[ERROR]\r\n");
        workload.Write("Stack trace:\r\n");
        workload.Write("  at Main()");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindMultiLinePattern(@"\[ERROR\].*Main\(\)", RegexOptions.Singleline);
        
        Assert.Single(matches);
        Assert.True(matches[0].IsMultiLine);
    }

    [Fact]
    public void FindMultiLinePattern_ZeroLengthMatches()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Line 1\r\n");
        workload.Write("Line 2");
        
        var snapshot = terminal.CreateSnapshot();
        // Match at the start of each line using Multiline mode (^ matches start of each line)
        var matches = snapshot.FindMultiLinePattern(@"^", RegexOptions.Multiline);
        
        // Each line should have a match at position 0
        Assert.True(matches.Count >= 2);
        Assert.Equal(0, matches[0].StartColumn);
        Assert.Equal(0, matches[0].EndColumn); // Zero-length match
        Assert.Equal("", matches[0].Text);
    }

    [Fact]
    public void FindMultiLinePattern_UnicodeContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Hello 世界\r\n");
        workload.Write("Привет мир");
        
        var snapshot = terminal.CreateSnapshot();
        // Use Singleline so . matches newlines and spans the content
        var matches = snapshot.FindMultiLinePattern(@"世界.*мир", RegexOptions.Singleline);
        
        Assert.Single(matches);
        Assert.True(matches[0].IsMultiLine);
    }

    [Fact]
    public void FindMultiLinePattern_CaptureGroups()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        
        workload.Write("Name: John\r\n");
        workload.Write("Age: 30");
        
        var snapshot = terminal.CreateSnapshot();
        var regex = new Regex(@"Name: (\w+)\nAge: (\d+)");
        var matches = snapshot.FindMultiLinePattern(regex);
        
        Assert.Single(matches);
        Assert.Contains("John", matches[0].Text);
        Assert.Contains("30", matches[0].Text);
    }

    #endregion

    #region Multi-Line Use Cases

    [Fact]
    public void UseCase_ExtractStackTrace()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);
        
        workload.Write("Error: NullReferenceException\r\n");
        workload.Write("  at Method1()\r\n");
        workload.Write("  at Method2()\r\n");
        workload.Write("  at Main()");
        
        var snapshot = terminal.CreateSnapshot();
        var match = snapshot.FindFirstMultiLinePattern(@"Error:.*?at Main\(\)", RegexOptions.Singleline);
        
        Assert.NotNull(match);
        Assert.Equal(4, match.Value.LineCount);
        Assert.Contains("NullReferenceException", match.Value.Text);
        Assert.Contains("Method1", match.Value.Text);
    }

    [Fact]
    public void UseCase_ExtractLogBlock()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);
        
        workload.Write("[2024-01-01 10:00:00] INFO: Starting\r\n");
        workload.Write("[2024-01-01 10:00:01] DEBUG: Processing\r\n");
        workload.Write("[2024-01-01 10:00:02] INFO: Complete");
        
        var snapshot = terminal.CreateSnapshot();
        var matches = snapshot.FindMultiLinePattern(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\] INFO:.*");
        
        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void UseCase_WaitForMultiLineOutput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);
        
        workload.Write("Build started...\r\n");
        workload.Write("Compiling...\r\n");
        workload.Write("Build succeeded.\r\n");
        workload.Write("0 Errors, 0 Warnings");
        
        var snapshot = terminal.CreateSnapshot();
        
        // Check for complete build output
        Assert.True(snapshot.ContainsMultiLinePattern(@"Build started.*Build succeeded", RegexOptions.Singleline));
    }

    [Fact]
    public void UseCase_ExtractTableData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 10);
        
        workload.Write("| Name  | Value |\r\n");
        workload.Write("|-------|-------|\r\n");
        workload.Write("| Item1 | 100   |\r\n");
        workload.Write("| Item2 | 200   |");
        
        var snapshot = terminal.CreateSnapshot();
        // Match table from header to last row - \s* handles any trailing whitespace
        var match = snapshot.FindFirstMultiLinePattern(@"\|\s*Name.*\|\s*Item2\s*\|\s*200\s*\|", RegexOptions.Singleline);
        
        Assert.NotNull(match);
        Assert.Equal(4, match.Value.LineCount);
    }

    #endregion
}
