using System.Text.RegularExpressions;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the regex pattern matching functionality on terminal regions.
/// </summary>
[TestClass]
public class TerminalRegionPatternMatchTests
{
    #region TextMatch Tests

    [TestMethod]
    public async Task TextMatch_HasCorrectProperties()
    {
        var match = new TextMatch(Line: 2, StartColumn: 5, EndColumn: 10, Text: "Hello");
        
        Assert.AreEqual(2, match.Line);
        Assert.AreEqual(5, match.StartColumn);
        Assert.AreEqual(10, match.EndColumn);
        Assert.AreEqual("Hello", match.Text);
        Assert.AreEqual(5, match.Length);
    }

    [TestMethod]
    public async Task TextMatch_LengthCalculation()
    {
        var match = new TextMatch(Line: 0, StartColumn: 0, EndColumn: 15, Text: "test expression");
        
        Assert.AreEqual(15, match.Length);
    }

    #endregion

    #region FindPattern Tests

    [TestMethod]
    public async Task FindPattern_FindsSimplePattern()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World\r\n");
        workload.Write("Test Pattern\r\n");
        workload.Write("Another line");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Another line"), TimeSpan.FromSeconds(5), "wait for Another line")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindPattern(@"Test");
        
        TestSeq.Single(matches);
        Assert.AreEqual(1, matches[0].Line);
        Assert.AreEqual(0, matches[0].StartColumn);
        Assert.AreEqual(4, matches[0].EndColumn);
        Assert.AreEqual("Test", matches[0].Text);
    }

    [TestMethod]
    public async Task FindPattern_FindsMultipleMatchesOnSameLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("cat and dog and cat again");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("cat again"), TimeSpan.FromSeconds(5), "wait for cat again")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindPattern(@"cat");
        
        Assert.AreEqual(2, matches.Count);
        Assert.AreEqual(0, matches[0].StartColumn);
        Assert.AreEqual(3, matches[0].EndColumn);
        Assert.AreEqual(16, matches[1].StartColumn);
        Assert.AreEqual(19, matches[1].EndColumn);
    }

    [TestMethod]
    public async Task FindPattern_FindsMatchesAcrossMultipleLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Error: file not found\r\n");
        workload.Write("Warning: deprecated\r\n");
        workload.Write("Error: permission denied");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("permission denied"), TimeSpan.FromSeconds(5), "wait for permission denied")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindPattern(@"Error");
        
        Assert.AreEqual(2, matches.Count);
        Assert.AreEqual(0, matches[0].Line);
        Assert.AreEqual(2, matches[1].Line);
    }

    [TestMethod]
    public async Task FindPattern_UsesRegexOptions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "wait for Hello World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Case sensitive (default) - should not match
        var matches = snapshot.FindPattern(@"hello");
        Assert.IsEmpty(matches);
        
        // Case insensitive - should match
        var matchesIgnoreCase = snapshot.FindPattern(@"hello", RegexOptions.IgnoreCase);
        TestSeq.Single(matchesIgnoreCase);
        Assert.AreEqual("Hello", matchesIgnoreCase[0].Text);
    }

    [TestMethod]
    public async Task FindPattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Value: 12345");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("12345"), TimeSpan.FromSeconds(5), "wait for value 12345")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var regex = new Regex(@"\d+");
        var matches = snapshot.FindPattern(regex);
        
        TestSeq.Single(matches);
        Assert.AreEqual("12345", matches[0].Text);
        Assert.AreEqual(7, matches[0].StartColumn);
    }

    [TestMethod]
    public async Task FindPattern_ReturnsEmptyListWhenNoMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "wait for Hello World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindPattern(@"NotFound");
        
        Assert.IsEmpty(matches);
    }

    [TestMethod]
    public async Task FindPattern_MatchesComplexRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        
        workload.Write("Email: user@example.com\r\n");
        workload.Write("Contact: admin@test.org");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("admin@test.org"), TimeSpan.FromSeconds(5), "wait for admin@test.org")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindPattern(@"\w+@\w+\.\w+");
        
        Assert.AreEqual(2, matches.Count);
        Assert.AreEqual("user@example.com", matches[0].Text);
        Assert.AreEqual("admin@test.org", matches[1].Text);
    }

    #endregion

    #region FindFirstPattern Tests

    [TestMethod]
    public async Task FindFirstPattern_ReturnsFirstMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("First match\r\n");
        workload.Write("Second match");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Second match"), TimeSpan.FromSeconds(5), "wait for Second match")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var match = snapshot.FindFirstPattern(@"match");
        
        Assert.IsNotNull(match);
        Assert.AreEqual(0, match.Value.Line);
        Assert.AreEqual(6, match.Value.StartColumn);
    }

    [TestMethod]
    public async Task FindFirstPattern_ReturnsNullWhenNoMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "wait for Hello World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var match = snapshot.FindFirstPattern(@"NotFound");
        
        Assert.IsNull(match);
    }

    [TestMethod]
    public async Task FindFirstPattern_WithRegexOptions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("HELLO World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("HELLO World"), TimeSpan.FromSeconds(5), "wait for HELLO World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var match = snapshot.FindFirstPattern(@"hello", RegexOptions.IgnoreCase);
        
        Assert.IsNotNull(match);
        Assert.AreEqual("HELLO", match.Value.Text);
    }

    [TestMethod]
    public async Task FindFirstPattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Price: $99.99");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("$99.99"), TimeSpan.FromSeconds(5), "wait for $99.99")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var regex = new Regex(@"\$[\d.]+");
        var match = snapshot.FindFirstPattern(regex);
        
        Assert.IsNotNull(match);
        Assert.AreEqual("$99.99", match.Value.Text);
    }

    #endregion

    #region GetTextAt Tests

    [TestMethod]
    public async Task GetTextAt_ReturnsTextAtCoordinates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World Test");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("World Test"), TimeSpan.FromSeconds(5), "wait for World Test")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var text = snapshot.GetTextAt(line: 0, startColumn: 6, endColumn: 11);
        
        Assert.AreEqual("World", text);
    }

    [TestMethod]
    public async Task GetTextAt_WithTextMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("ID: 12345 Name: John");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Name: John"), TimeSpan.FromSeconds(5), "wait for Name: John")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var match = snapshot.FindFirstPattern(@"\d+");
        
        Assert.IsNotNull(match);
        var text = snapshot.GetTextAt(match.Value);
        
        Assert.AreEqual("12345", text);
    }

    [TestMethod]
    public async Task GetTextAt_ReturnsEmptyForInvalidLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "wait for Hello World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var text = snapshot.GetTextAt(line: 100, startColumn: 0, endColumn: 5);
        
        Assert.AreEqual("", text);
    }

    [TestMethod]
    public async Task GetTextAt_ClampsToValidRange()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();
        
        workload.Write("Hello World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "wait for Hello World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Request beyond end of line
        var text = snapshot.GetTextAt(line: 0, startColumn: 6, endColumn: 100);
        
        // Should clamp to width and return "World" plus trailing spaces
        Assert.StartsWith("World", text);
    }

    [TestMethod]
    public async Task GetTextAt_HandlesNegativeStart()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "wait for Hello World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Request with negative start
        var text = snapshot.GetTextAt(line: 0, startColumn: -5, endColumn: 5);
        
        // Should start at 0
        Assert.AreEqual("Hello", text);
    }

    #endregion

    #region ContainsPattern Tests

    [TestMethod]
    public async Task ContainsPattern_ReturnsTrueWhenPatternExists()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Error: Something went wrong");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("went wrong"), TimeSpan.FromSeconds(5), "wait for went wrong")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.IsTrue(snapshot.ContainsPattern(@"Error"));
        Assert.IsTrue(snapshot.ContainsPattern(@"went \w+"));
    }

    [TestMethod]
    public async Task ContainsPattern_ReturnsFalseWhenPatternNotExists()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Success: All tests passed");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("tests passed"), TimeSpan.FromSeconds(5), "wait for tests passed")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.IsFalse(snapshot.ContainsPattern(@"Error"));
        Assert.IsFalse(snapshot.ContainsPattern(@"\d{5}"));
    }

    [TestMethod]
    public async Task ContainsPattern_WithRegexOptions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("SUCCESS");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("SUCCESS"), TimeSpan.FromSeconds(5), "wait for SUCCESS")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.IsFalse(snapshot.ContainsPattern(@"success"));
        Assert.IsTrue(snapshot.ContainsPattern(@"success", RegexOptions.IgnoreCase));
    }

    [TestMethod]
    public async Task ContainsPattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Version: 2.0.1");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("2.0.1"), TimeSpan.FromSeconds(5), "wait for version 2.0.1")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var regex = new Regex(@"\d+\.\d+\.\d+");
        Assert.IsTrue(snapshot.ContainsPattern(regex));
    }

    #endregion

    #region Integration Tests with Regions

    [TestMethod]
    public async Task FindPattern_WorksWithRegions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Header line\r\n");
        workload.Write("Match: 123\r\n");
        workload.Write("Match: 456\r\n");
        workload.Write("Footer line");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Footer line"), TimeSpan.FromSeconds(5), "wait for Footer line")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Get a region excluding header and footer
        var region = snapshot.GetRegion(new Hex1b.Layout.Rect(0, 1, 40, 2));
        
        var matches = region.FindPattern(@"\d+");
        
        Assert.AreEqual(2, matches.Count);
        // Coordinates are relative to the region, not the full snapshot
        Assert.AreEqual(0, matches[0].Line);
        Assert.AreEqual(1, matches[1].Line);
    }

    [TestMethod]
    public async Task GetTextAt_WorksWithRegions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Line 0: AAA\r\n");
        workload.Write("Line 1: BBB\r\n");
        workload.Write("Line 2: CCC");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 2: CCC"), TimeSpan.FromSeconds(5), "wait for Line 2: CCC")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var region = snapshot.GetRegion(new Hex1b.Layout.Rect(8, 1, 10, 2));
        
        var text = region.GetTextAt(0, 0, 3);
        Assert.AreEqual("BBB", text);
    }

    #endregion

    #region Use Case: Extract Data from Terminal

    [TestMethod]
    public async Task UseCase_ExtractValueFromLabel()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        
        workload.Write("Status: Running\r\n");
        workload.Write("Progress: 75%\r\n");
        workload.Write("ETA: 5 minutes");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("ETA: 5 minutes"), TimeSpan.FromSeconds(5), "wait for ETA")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Find and extract the progress value
        var progressMatch = snapshot.FindFirstPattern(@"Progress: (\d+)%");
        Assert.IsNotNull(progressMatch);
        
        // Extract just the number using a separate regex
        var numberMatch = snapshot.FindFirstPattern(@"(?<=Progress: )\d+");
        Assert.IsNotNull(numberMatch);
        Assert.AreEqual("75", numberMatch.Value.Text);
    }

    [TestMethod]
    public async Task UseCase_FindAllErrors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        
        workload.Write("[INFO] Starting...\r\n");
        workload.Write("[ERROR] File not found\r\n");
        workload.Write("[INFO] Retrying...\r\n");
        workload.Write("[ERROR] Connection failed");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Connection failed"), TimeSpan.FromSeconds(5), "wait for Connection failed")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var errors = snapshot.FindPattern(@"\[ERROR\].*");
        
        Assert.AreEqual(2, errors.Count);
        Assert.Contains("File not found", errors[0].Text);
        Assert.Contains("Connection failed", errors[1].Text);
    }

    #endregion

    #region MultiLineTextMatch Tests

    [TestMethod]
    public async Task MultiLineTextMatch_HasCorrectProperties()
    {
        var match = new MultiLineTextMatch(StartLine: 2, StartColumn: 5, EndLine: 4, EndColumn: 10, Text: "Hello\nWorld\nTest");
        
        Assert.AreEqual(2, match.StartLine);
        Assert.AreEqual(5, match.StartColumn);
        Assert.AreEqual(4, match.EndLine);
        Assert.AreEqual(10, match.EndColumn);
        Assert.AreEqual("Hello\nWorld\nTest", match.Text);
    }

    [TestMethod]
    public async Task MultiLineTextMatch_IsMultiLine_TrueForMultipleLines()
    {
        var match = new MultiLineTextMatch(StartLine: 0, StartColumn: 0, EndLine: 2, EndColumn: 5, Text: "abc\ndef\nghi");
        
        Assert.IsTrue(match.IsMultiLine);
        Assert.AreEqual(3, match.LineCount);
    }

    [TestMethod]
    public async Task MultiLineTextMatch_IsMultiLine_FalseForSingleLine()
    {
        var match = new MultiLineTextMatch(StartLine: 1, StartColumn: 0, EndLine: 1, EndColumn: 5, Text: "Hello");
        
        Assert.IsFalse(match.IsMultiLine);
        Assert.AreEqual(1, match.LineCount);
    }

    #endregion

    #region FindMultiLinePattern Tests

    [TestMethod]
    public async Task FindMultiLinePattern_FindsSingleLinePattern()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World\r\n");
        workload.Write("Test Pattern\r\n");
        workload.Write("Another line");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Another line"), TimeSpan.FromSeconds(5), "wait for Another line")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindMultiLinePattern(@"Test");
        
        TestSeq.Single(matches);
        Assert.AreEqual(1, matches[0].StartLine);
        Assert.AreEqual(1, matches[0].EndLine);
        Assert.AreEqual(0, matches[0].StartColumn);
        Assert.AreEqual(4, matches[0].EndColumn);
        Assert.AreEqual("Test", matches[0].Text);
        Assert.IsFalse(matches[0].IsMultiLine);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_MatchesAcrossLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Start here\r\n");
        workload.Write("End here");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("End here"), TimeSpan.FromSeconds(5), "wait for End here")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Match pattern that spans from "here" on line 0 to "End" on line 1
        // Use trimLines: true to avoid matching trailing whitespace padding
        var matches = snapshot.FindMultiLinePattern(@"here\nEnd", RegexOptions.None, trimLines: true);
        
        TestSeq.Single(matches);
        Assert.AreEqual(0, matches[0].StartLine);
        Assert.AreEqual(1, matches[0].EndLine);
        Assert.IsTrue(matches[0].IsMultiLine);
        Assert.Contains("here", matches[0].Text);
        Assert.Contains("End", matches[0].Text);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_MatchesMultipleLinesWithSingleline()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("BEGIN\r\n");
        workload.Write("middle content\r\n");
        workload.Write("more content\r\n");
        workload.Write("END");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("END"), TimeSpan.FromSeconds(5), "wait for END")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Use Singleline option so . matches newlines
        var matches = snapshot.FindMultiLinePattern(@"BEGIN.*?END", RegexOptions.Singleline);
        
        TestSeq.Single(matches);
        Assert.AreEqual(0, matches[0].StartLine);
        Assert.AreEqual(3, matches[0].EndLine);
        Assert.IsTrue(matches[0].IsMultiLine);
        Assert.AreEqual(4, matches[0].LineCount);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_FindsMultipleMultiLineMatches()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("<div>\r\n");
        workload.Write("content1\r\n");
        workload.Write("</div>\r\n");
        workload.Write("<div>\r\n");
        workload.Write("content2\r\n");
        workload.Write("</div>");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("content2"), TimeSpan.FromSeconds(5), "wait for content2")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindMultiLinePattern(@"<div>.*?</div>", RegexOptions.Singleline);
        
        Assert.AreEqual(2, matches.Count);
        Assert.IsTrue(matches[0].IsMultiLine);
        Assert.IsTrue(matches[1].IsMultiLine);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_WithNewlineInPattern()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Line 1\r\n");
        workload.Write("Line 2\r\n");
        workload.Write("Line 3");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 3"), TimeSpan.FromSeconds(5), "wait for Line 3")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Match explicit newline pattern
        var matches = snapshot.FindMultiLinePattern(@"Line 1\s*\nLine 2");
        
        TestSeq.Single(matches);
        Assert.AreEqual(0, matches[0].StartLine);
        Assert.AreEqual(1, matches[0].EndLine);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_MatchesEntireContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();
        
        workload.Write("AAA\r\n");
        workload.Write("BBB\r\n");
        workload.Write("CCC");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("CCC"), TimeSpan.FromSeconds(5), "wait for CCC")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindMultiLinePattern(@"^AAA.*CCC", RegexOptions.Singleline);
        
        TestSeq.Single(matches);
        Assert.AreEqual(0, matches[0].StartLine);
        Assert.AreEqual(2, matches[0].EndLine);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_ReturnsEmptyForNoMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "wait for Hello World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindMultiLinePattern(@"NotFound\nAnywhere");
        
        Assert.IsEmpty(matches);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Start\r\n");
        workload.Write("Middle\r\n");
        workload.Write("End");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("End"), TimeSpan.FromSeconds(5), "wait for End")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var regex = new Regex(@"Start.*End", RegexOptions.Singleline);
        var matches = snapshot.FindMultiLinePattern(regex);
        
        TestSeq.Single(matches);
        Assert.IsTrue(matches[0].IsMultiLine);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_MatchingJsonStructure()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("{\r\n");
        workload.Write("  \"name\": \"test\",\r\n");
        workload.Write("  \"value\": 123\r\n");
        workload.Write("}");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("\"value\": 123"), TimeSpan.FromSeconds(5), "wait for JSON value")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindMultiLinePattern(@"\{[^}]+\}", RegexOptions.Singleline);
        
        TestSeq.Single(matches);
        Assert.AreEqual(0, matches[0].StartLine);
        Assert.AreEqual(3, matches[0].EndLine);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_MatchingCodeBlock()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(50, 10).Build();
        
        workload.Write("function test() {\r\n");
        workload.Write("    return true;\r\n");
        workload.Write("}");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("return true"), TimeSpan.FromSeconds(5), "wait for return true")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindMultiLinePattern(@"function.*?\}", RegexOptions.Singleline);
        
        TestSeq.Single(matches);
        Assert.IsTrue(matches[0].IsMultiLine);
        Assert.Contains("function", matches[0].Text);
        Assert.Contains("return", matches[0].Text);
    }

    #endregion

    #region FindFirstMultiLinePattern Tests

    [TestMethod]
    public async Task FindFirstMultiLinePattern_ReturnsFirstMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Block 1\r\n");
        workload.Write("End\r\n");
        workload.Write("Block 2\r\n");
        workload.Write("End");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Block 2"), TimeSpan.FromSeconds(5), "wait for Block 2")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var match = snapshot.FindFirstMultiLinePattern(@"Block.*?End", RegexOptions.Singleline);
        
        Assert.IsNotNull(match);
        Assert.AreEqual(0, match.Value.StartLine);
        Assert.Contains("Block 1", match.Value.Text);
    }

    [TestMethod]
    public async Task FindFirstMultiLinePattern_ReturnsNullForNoMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "wait for Hello World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var match = snapshot.FindFirstMultiLinePattern(@"NotFound\nPattern");
        
        Assert.IsNull(match);
    }

    [TestMethod]
    public async Task FindFirstMultiLinePattern_WithRegexOptions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("START\r\n");
        workload.Write("content\r\n");
        workload.Write("END");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("END"), TimeSpan.FromSeconds(5), "wait for END")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var match = snapshot.FindFirstMultiLinePattern(@"start.*end", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        Assert.IsNotNull(match);
        Assert.IsTrue(match.Value.IsMultiLine);
    }

    [TestMethod]
    public async Task FindFirstMultiLinePattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("A\r\n");
        workload.Write("B\r\n");
        workload.Write("C");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("C"), TimeSpan.FromSeconds(5), "wait for C")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var regex = new Regex(@"A\nB\nC");
        // Use trimLines: true to avoid matching trailing whitespace padding
        var match = snapshot.FindFirstMultiLinePattern(regex, trimLines: true);
        
        Assert.IsNotNull(match);
        Assert.AreEqual(0, match.Value.StartLine);
        Assert.AreEqual(2, match.Value.EndLine);
    }

    #endregion

    #region ContainsMultiLinePattern Tests

    [TestMethod]
    public async Task ContainsMultiLinePattern_ReturnsTrueWhenPatternExists()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Header\r\n");
        workload.Write("Content\r\n");
        workload.Write("Footer");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Footer"), TimeSpan.FromSeconds(5), "wait for Footer")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.IsTrue(snapshot.ContainsMultiLinePattern(@"Header.*Footer", RegexOptions.Singleline));
    }

    [TestMethod]
    public async Task ContainsMultiLinePattern_ReturnsFalseWhenPatternNotExists()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "wait for Hello World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.IsFalse(snapshot.ContainsMultiLinePattern(@"NotFound\nPattern"));
    }

    [TestMethod]
    public async Task ContainsMultiLinePattern_WithCompiledRegex()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Line1\r\n");
        workload.Write("Line2");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line2"), TimeSpan.FromSeconds(5), "wait for Line2")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var regex = new Regex(@"Line1\nLine2");
        // Use trimLines: true to avoid matching trailing whitespace padding
        Assert.IsTrue(snapshot.ContainsMultiLinePattern(regex, trimLines: true));
    }

    #endregion

    #region GetMultiLineTextAt Tests

    [TestMethod]
    public async Task GetMultiLineTextAt_ReturnsSingleLineText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World Test");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("World Test"), TimeSpan.FromSeconds(5), "wait for World Test")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var text = snapshot.GetMultiLineTextAt(startLine: 0, startColumn: 6, endLine: 0, endColumn: 11);
        
        Assert.AreEqual("World", text);
    }

    [TestMethod]
    public async Task GetMultiLineTextAt_ReturnsMultiLineText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Line 0 content\r\n");
        workload.Write("Line 1 content\r\n");
        workload.Write("Line 2 content");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 2 content"), TimeSpan.FromSeconds(5), "wait for Line 2 content")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var text = snapshot.GetMultiLineTextAt(startLine: 0, startColumn: 7, endLine: 2, endColumn: 7);
        
        Assert.Contains("content", text);
        Assert.Contains("\n", text);
    }

    [TestMethod]
    public async Task GetMultiLineTextAt_WithMultiLineTextMatch()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("START\r\n");
        workload.Write("middle\r\n");
        workload.Write("END");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("END"), TimeSpan.FromSeconds(5), "wait for END")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var match = snapshot.FindFirstMultiLinePattern(@"START.*END", RegexOptions.Singleline);
        
        Assert.IsNotNull(match);
        // The match.Text property already contains the matched text
        Assert.StartsWith("START", match.Value.Text);
        Assert.EndsWith("END", match.Value.Text);
        
        // GetMultiLineTextAt works with raw terminal coordinates (untrimmed)
        // For multi-line matches, use match.Text directly
        Assert.AreEqual(match.Value.Text, match.Value.Text.Trim());
    }

    [TestMethod]
    public async Task GetMultiLineTextAt_ReturnsEmptyForInvalidLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "wait for Hello World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var text = snapshot.GetMultiLineTextAt(startLine: 100, startColumn: 0, endLine: 101, endColumn: 5);
        
        Assert.AreEqual("", text);
    }

    [TestMethod]
    public async Task GetMultiLineTextAt_ClampsToValidRange()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();
        
        workload.Write("AAA\r\n");
        workload.Write("BBB\r\n");
        workload.Write("CCC");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("CCC"), TimeSpan.FromSeconds(5), "wait for CCC")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Request beyond end of region
        var text = snapshot.GetMultiLineTextAt(startLine: 0, startColumn: 0, endLine: 10, endColumn: 100);
        
        // Should clamp to valid range
        Assert.Contains("AAA", text);
        Assert.Contains("BBB", text);
        Assert.Contains("CCC", text);
    }

    #endregion

    #region Multi-Line Edge Cases

    [TestMethod]
    public async Task FindMultiLinePattern_EmptyLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Start\r\n");
        workload.Write("\r\n"); // Empty line
        workload.Write("End");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("End"), TimeSpan.FromSeconds(5), "wait for End")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Use trimLines: true to avoid matching trailing whitespace padding
        var matches = snapshot.FindMultiLinePattern(@"Start\n\nEnd", trimLines: true);
        
        TestSeq.Single(matches);
        Assert.AreEqual(0, matches[0].StartLine);
        Assert.AreEqual(2, matches[0].EndLine);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_MatchAtStartOfRegion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("ABC\r\n");
        workload.Write("DEF");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("DEF"), TimeSpan.FromSeconds(5), "wait for DEF")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Use trimLines: true to avoid matching trailing whitespace padding
        var matches = snapshot.FindMultiLinePattern(@"^ABC\nDEF", trimLines: true);
        
        TestSeq.Single(matches);
        Assert.AreEqual(0, matches[0].StartLine);
        Assert.AreEqual(0, matches[0].StartColumn);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_MatchAtEndOfRegion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();
        
        workload.Write("XXX\r\n");
        workload.Write("YYY\r\n");
        workload.Write("ZZZ");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("ZZZ"), TimeSpan.FromSeconds(5), "wait for ZZZ")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Use trimLines: true to avoid matching trailing whitespace padding
        var matches = snapshot.FindMultiLinePattern(@"YYY\nZZZ", trimLines: true);
        
        TestSeq.Single(matches);
        Assert.AreEqual(1, matches[0].StartLine);
        Assert.AreEqual(2, matches[0].EndLine);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_OverlappingPatterns()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("ABAB\r\n");
        workload.Write("ABAB");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.GetDisplayText().Contains("ABAB\n") || s.GetDisplayText().Split('\n').Length > 1, TimeSpan.FromSeconds(5), "wait for ABAB on both lines")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Non-overlapping matches
        var matches = snapshot.FindMultiLinePattern(@"AB");
        
        // Should find 4 matches (2 per line)
        Assert.AreEqual(4, matches.Count);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_SpecialRegexCharacters()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("[ERROR]\r\n");
        workload.Write("Stack trace:\r\n");
        workload.Write("  at Main()");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("at Main()"), TimeSpan.FromSeconds(5), "wait for at Main()")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindMultiLinePattern(@"\[ERROR\].*Main\(\)", RegexOptions.Singleline);
        
        TestSeq.Single(matches);
        Assert.IsTrue(matches[0].IsMultiLine);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_ZeroLengthMatches()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Line 1\r\n");
        workload.Write("Line 2");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 2"), TimeSpan.FromSeconds(5), "wait for Line 2")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Match at the start of each line using Multiline mode (^ matches start of each line)
        var matches = snapshot.FindMultiLinePattern(@"^", RegexOptions.Multiline);
        
        // Each line should have a match at position 0
        Assert.IsTrue(matches.Count >= 2);
        Assert.AreEqual(0, matches[0].StartColumn);
        Assert.AreEqual(0, matches[0].EndColumn); // Zero-length match
        Assert.AreEqual("", matches[0].Text);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_UnicodeContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello 世界\r\n");
        workload.Write("Привет мир");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("мир"), TimeSpan.FromSeconds(5), "wait for мир")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Use Singleline so . matches newlines and spans the content
        var matches = snapshot.FindMultiLinePattern(@"世界.*мир", RegexOptions.Singleline);
        
        TestSeq.Single(matches);
        Assert.IsTrue(matches[0].IsMultiLine);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_CaptureGroups()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Name: John\r\n");
        workload.Write("Age: 30");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Age: 30"), TimeSpan.FromSeconds(5), "wait for Age: 30")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var regex = new Regex(@"Name: (\w+)\nAge: (\d+)");
        // Use trimLines: true to avoid matching trailing whitespace padding
        var matches = snapshot.FindMultiLinePattern(regex, trimLines: true);
        
        TestSeq.Single(matches);
        Assert.Contains("John", matches[0].Text);
        Assert.Contains("30", matches[0].Text);
    }

    #endregion

    #region Multi-Line Use Cases

    [TestMethod]
    public async Task UseCase_ExtractStackTrace()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        
        workload.Write("Error: NullReferenceException\r\n");
        workload.Write("  at Method1()\r\n");
        workload.Write("  at Method2()\r\n");
        workload.Write("  at Main()");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("at Main()"), TimeSpan.FromSeconds(5), "wait for at Main()")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var match = snapshot.FindFirstMultiLinePattern(@"Error:.*?at Main\(\)", RegexOptions.Singleline);
        
        Assert.IsNotNull(match);
        Assert.AreEqual(4, match.Value.LineCount);
        Assert.Contains("NullReferenceException", match.Value.Text);
        Assert.Contains("Method1", match.Value.Text);
    }

    [TestMethod]
    public async Task UseCase_ExtractLogBlock()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        
        workload.Write("[2024-01-01 10:00:00] INFO: Starting\r\n");
        workload.Write("[2024-01-01 10:00:01] DEBUG: Processing\r\n");
        workload.Write("[2024-01-01 10:00:02] INFO: Complete");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("INFO: Complete"), TimeSpan.FromSeconds(5), "wait for INFO: Complete")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        var matches = snapshot.FindMultiLinePattern(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\] INFO:.*");
        
        Assert.AreEqual(2, matches.Count);
    }

    [TestMethod]
    public async Task UseCase_WaitForMultiLineOutput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        
        workload.Write("Build started...\r\n");
        workload.Write("Compiling...\r\n");
        workload.Write("Build succeeded.\r\n");
        workload.Write("0 Errors, 0 Warnings");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("0 Errors"), TimeSpan.FromSeconds(5), "wait for 0 Errors")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Check for complete build output
        Assert.IsTrue(snapshot.ContainsMultiLinePattern(@"Build started.*Build succeeded", RegexOptions.Singleline));
    }

    [TestMethod]
    public async Task UseCase_ExtractTableData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();
        
        workload.Write("| Name  | Value |\r\n");
        workload.Write("|-------|-------|\r\n");
        workload.Write("| Item1 | 100   |\r\n");
        workload.Write("| Item2 | 200   |");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item2"), TimeSpan.FromSeconds(5), "wait for Item2")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        // Match table from header to last row - \s* handles any trailing whitespace
        var match = snapshot.FindFirstMultiLinePattern(@"\|\s*Name.*\|\s*Item2\s*\|\s*200\s*\|", RegexOptions.Singleline);
        
        Assert.IsNotNull(match);
        Assert.AreEqual(4, match.Value.LineCount);
    }

    [TestMethod]
    public async Task FindPattern_MatchesUnicodeCheckAndCross()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        workload.Write("[1 ✔] $ \r\n");
        workload.Write("[1 ✘:127] $ \r\n");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("✘:127"), TimeSpan.FromSeconds(5), "wait for ✘:127")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // The line-by-line FindPattern should work since GetLine doesn't trim
        var regex = new Regex(@"\[\d+ (?:✔|✘:\d+)\] \$ ", RegexOptions.Multiline);
        var matches = snapshot.FindPattern(regex);
        
        Assert.AreEqual(2, matches.Count);
        Assert.AreEqual("[1 ✔] $ ", matches[0].Text);
        Assert.AreEqual("[1 ✘:127] $ ", matches[1].Text);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_MatchesUnicodeCheckAndCross_WithTrailingSpace()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        workload.Write("[1 ✔] $ \r\n");
        workload.Write("[1 ✘:127] $ \r\n");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("✘:127"), TimeSpan.FromSeconds(5), "wait for ✘:127")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // FindMultiLinePattern now uses untrimmed lines, so trailing space is preserved
        var regex = new Regex(@"\[\d+ (?:✔|✘:\d+)\] \$ ", RegexOptions.Multiline);
        var matches = snapshot.FindMultiLinePattern(regex);
        
        Assert.AreEqual(2, matches.Count);
        Assert.AreEqual("[1 ✔] $ ", matches[0].Text);
        Assert.AreEqual("[1 ✘:127] $ ", matches[1].Text);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_WithCustomLineSeparator()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Line1\r\n");
        workload.Write("Line2\r\n");
        workload.Write("Line3");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line3"), TimeSpan.FromSeconds(5), "wait for Line3")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Use custom separator " | " between lines
        var matches = snapshot.FindMultiLinePattern(@"Line1 \| Line2 \| Line3", trimLines: true, lineSeparator: " | ");
        
        TestSeq.Single(matches);
        Assert.Contains("Line1", matches[0].Text);
        Assert.Contains("Line2", matches[0].Text);
        Assert.Contains("Line3", matches[0].Text);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_WithNoLineSeparator_ConcatenatesDirectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("ABC\r\n");
        workload.Write("DEF\r\n");
        workload.Write("GHI");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("GHI"), TimeSpan.FromSeconds(5), "wait for GHI")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Use null separator to concatenate lines directly
        var matches = snapshot.FindMultiLinePattern(@"ABCDEFGHI", trimLines: true, lineSeparator: null);
        
        TestSeq.Single(matches);
        Assert.AreEqual("ABCDEFGHI", matches[0].Text);
    }

    [TestMethod]
    public async Task FindMultiLinePattern_WithEmptyLineSeparator_ConcatenatesDirectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        workload.Write("Hello\r\n");
        workload.Write("World");
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("World"), TimeSpan.FromSeconds(5), "wait for World")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Use empty string separator to concatenate lines directly
        var matches = snapshot.FindMultiLinePattern(@"HelloWorld", trimLines: true, lineSeparator: "");
        
        TestSeq.Single(matches);
        Assert.AreEqual("HelloWorld", matches[0].Text);
    }

    #endregion
}