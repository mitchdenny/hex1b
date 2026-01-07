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
}
