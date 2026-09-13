using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for OSC 7 (working directory) terminal state and events.
/// </summary>
[TestClass]
public class OscWorkingDirectoryTests
{
    [TestMethod]
    public async Task Osc7_ValidFileUri_UpdatesWorkingDirectoryAndFiresEvent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        Assert.AreEqual(TerminalWorkingDirectory.Default, terminal.WorkingDirectory);

        var changes = new List<TerminalWorkingDirectory>();
        terminal.WorkingDirectoryChanged += changes.Add;
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]7;file:///home/user/project\x07"));

        Assert.AreEqual("file:///home/user/project", terminal.WorkingDirectory.Uri);
        Assert.AreEqual("", terminal.WorkingDirectory.Host);
        Assert.AreEqual("/home/user/project", terminal.WorkingDirectory.Path);
        Assert.AreEqual(1, changes.Count);

        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(terminal.WorkingDirectory, snapshot.WorkingDirectory);
    }

    [TestMethod]
    public async Task Osc7_WithHostname_ReportsHost()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]7;file://myhost/home/user\x07"));

        Assert.AreEqual("myhost", terminal.WorkingDirectory.Host);
        Assert.AreEqual("myhost/home/user", terminal.WorkingDirectory.Uri!["file://".Length..]);
    }

    [TestMethod]
    public async Task Osc7_PercentEncodedPath_IsDecoded()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]7;file:///home/user/my%20project\x07"));

        Assert.AreEqual("/home/user/my project", terminal.WorkingDirectory.Path);
    }

    [TestMethod]
    [DataRow("not-a-uri")]
    [DataRow("http://example.com/path")]
    [DataRow("")]
    public async Task Osc7_MalformedOrNonFileUri_DoesNotMutateState(string payload)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var changes = 0;
        terminal.WorkingDirectoryChanged += _ => changes++;
        terminal.ApplyTokens([new OscToken("7", "", payload)]);

        Assert.AreEqual(TerminalWorkingDirectory.Default, terminal.WorkingDirectory);
        Assert.AreEqual(0, changes);
    }

    [TestMethod]
    public async Task Osc7_RIS_ClearsWorkingDirectory()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]7;file:///tmp\x07"));
        Assert.AreNotEqual(TerminalWorkingDirectory.Default, terminal.WorkingDirectory);

        terminal.ApplyTokens([RisToken.Instance]);
        Assert.AreEqual(TerminalWorkingDirectory.Default, terminal.WorkingDirectory);
    }
}

/// <summary>
/// Tests for OSC 133 command marks: history entries anchored to text rows, distinct from the
/// current-phase-only <see cref="TerminalShellIntegration"/> state.
/// </summary>
[TestClass]
public class OscCommandMarkTests
{
    [TestMethod]
    public async Task Osc133_BareMarkers_RecordMarksWithoutRawParameters()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var added = new List<TerminalCommandMark>();
        terminal.CommandMarkAdded += added.Add;

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;A\x07\x1b]133;B\x07\x1b]133;C\x07\x1b]133;D;3\x07"));

        Assert.AreEqual(4, added.Count);
        Assert.AreEqual(TerminalShellIntegrationPhase.Prompt, added[0].Phase);
        Assert.AreEqual(TerminalShellIntegrationPhase.CommandLine, added[1].Phase);
        Assert.AreEqual(TerminalShellIntegrationPhase.Executing, added[2].Phase);
        Assert.AreEqual(TerminalShellIntegrationPhase.Finished, added[3].Phase);
        Assert.AreEqual(3, added[3].ExitCode);
        foreach (var mark in added)
        {
            Assert.IsNull(mark.RawParameters);
            Assert.AreEqual(0, mark.Parameters.Count);
            Assert.IsFalse(mark.HasCmdlineUrl);
            Assert.IsNull(mark.CmdlineUrl);
        }
        TestSeq.AreEqual(added, terminal.CommandMarks);
    }

    [TestMethod]
    public async Task Osc133_MarkerCWithCmdlineUrl_CapturesRawAndParsedValueVerbatim()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;C;cmdline_url=ls%20-la\x07"));

        var mark = TestSeq.Single(terminal.CommandMarks);
        Assert.AreEqual(TerminalShellIntegrationPhase.Executing, mark.Phase);
        Assert.AreEqual("cmdline_url=ls%20-la", mark.RawParameters);
        Assert.IsTrue(mark.HasCmdlineUrl);
        Assert.AreEqual("ls%20-la", mark.CmdlineUrl);
        Assert.AreEqual(1, mark.Parameters.Count);
        Assert.AreEqual("ls%20-la", mark.Parameters["cmdline_url"]);
    }

    [TestMethod]
    public async Task Osc133_MarkerCWithMultipleParameters_ParsesAllKeys()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;C;foo=bar;baz=qux\x07"));

        var mark = TestSeq.Single(terminal.CommandMarks);
        Assert.AreEqual("foo=bar;baz=qux", mark.RawParameters);
        Assert.AreEqual(2, mark.Parameters.Count);
        Assert.AreEqual("bar", mark.Parameters["foo"]);
        Assert.AreEqual("qux", mark.Parameters["baz"]);
        Assert.IsFalse(mark.HasCmdlineUrl);
    }

    [TestMethod]
    public async Task Osc133_ParametersDictionary_IsLazyAndCached()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;C;a=1\x07"));
        var mark = TestSeq.Single(terminal.CommandMarks);

        // Accessing Parameters twice must return the same cached instance, not re-parse.
        Assert.AreSame(mark.Parameters, mark.Parameters);
    }

    [TestMethod]
    public async Task Osc133_MarkerDWithTrailingContent_IsRejectedAndNoMarkIsRecorded()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var added = 0;
        terminal.CommandMarkAdded += _ => added++;
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;D;0;foo=bar\x07"));

        Assert.AreEqual(0, added);
        Assert.AreEqual(0, terminal.CommandMarks.Count);
        Assert.AreEqual(TerminalShellIntegrationPhase.Unknown, terminal.ShellIntegration.Phase);
    }

    [TestMethod]
    public async Task CommandMarks_ExceedingCapacity_EvictsOldestFirst()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5)
            .Build();
        for (var i = 0; i < 205; i++)
            terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;A\x07"));

        Assert.AreEqual(200, terminal.CommandMarks.Count);
    }

    [TestMethod]
    public async Task CommandMarks_AnchorsAreStableUntilTextGenerationChanges()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;A\x07"));
        var first = TestSeq.Single(terminal.CommandMarks);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;B\x07"));
        var second = terminal.CommandMarks[1];

        // Same row (cursor hasn't moved), same generation: identical anchor.
        Assert.AreEqual(first.TextGeneration, second.TextGeneration);
        Assert.AreEqual(first.TextRowId, second.TextRowId);

        // RIS invalidates the text-row identity space (bumps the generation).
        terminal.ApplyTokens([RisToken.Instance]);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;C\x07"));
        var third = terminal.CommandMarks[2];
        Assert.AreNotEqual(first.TextGeneration, third.TextGeneration);
    }
}
