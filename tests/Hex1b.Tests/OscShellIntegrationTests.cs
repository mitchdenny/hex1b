using System.Text;
using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class OscShellIntegrationTests
{
    [TestMethod]
    [DataRow("A", TerminalShellIntegrationPhase.Prompt, null)]
    [DataRow("B", TerminalShellIntegrationPhase.CommandLine, null)]
    [DataRow("C", TerminalShellIntegrationPhase.Executing, null)]
    [DataRow("D", TerminalShellIntegrationPhase.Finished, null)]
    [DataRow("D;", TerminalShellIntegrationPhase.Finished, null)]
    [DataRow("D;0", TerminalShellIntegrationPhase.Finished, 0)]
    [DataRow("D;17", TerminalShellIntegrationPhase.Finished, 17)]
    [DataRow("D;-1", TerminalShellIntegrationPhase.Finished, -1)]
    [DataRow("D;+7", TerminalShellIntegrationPhase.Finished, 7)]
    [DataRow("D;0007", TerminalShellIntegrationPhase.Finished, 7)]
    [DataRow("D;-2147483648", TerminalShellIntegrationPhase.Finished, int.MinValue)]
    [DataRow("D;2147483647", TerminalShellIntegrationPhase.Finished, int.MaxValue)]
    public async Task Osc133_ValidMarker_AppliesWithoutRequiringEarlierMarkers(
        string body, TerminalShellIntegrationPhase phase, int? exitCode)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var token = TestSeq.IsType<OscToken>(TestSeq.Single(
            AnsiTokenizer.Tokenize($"\x1b]133;{body}\x07")));
        if (body == "D")
        {
            Assert.AreEqual("", token.Parameters);
            Assert.AreEqual("D", token.Payload);
        }
        var impacts = TestSeq.Single(terminal.ApplyTokensWithImpacts([token]));
        Assert.AreEqual(phase, terminal.ShellIntegration.Phase);
        Assert.AreEqual(exitCode, terminal.ShellIntegration.LastExitCode);
        Assert.AreEqual(0, impacts.CellImpacts.Count);
        Assert.AreEqual(impacts.CursorXBefore, impacts.CursorXAfter);
        Assert.AreEqual(impacts.CursorYBefore, impacts.CursorYAfter);
        Assert.AreEqual(TerminalProgress.Default, terminal.Progress);

        var roundTrip = TestSeq.Single(AnsiTokenizer.Tokenize(AnsiTokenSerializer.Serialize(token)));
        terminal.ApplyTokens([RisToken.Instance, roundTrip]);
        Assert.AreEqual(phase, terminal.ShellIntegration.Phase);
        Assert.AreEqual(exitCode, terminal.ShellIntegration.LastExitCode);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("E")]
    [DataRow("a")]
    [DataRow(";A")]
    [DataRow(";B")]
    [DataRow(";C")]
    [DataRow(";D")]
    [DataRow(";D;0")]
    [DataRow(";D;")]
    [DataRow("D; ")]
    [DataRow("D;0 ")]
    [DataRow("D; 0")]
    [DataRow("D;+")]
    [DataRow("D;-")]
    [DataRow("D;1.0")]
    [DataRow("D;bad")]
    [DataRow("D;１２")]
    [DataRow("D;2147483648")]
    [DataRow("D;-2147483649")]
    [DataRow("D;999999999999999999999999")]
    [DataRow("D;0;")]
    [DataRow("D;;")]
    public async Task Osc133_InvalidMarkerOrArguments_DoesNotMutateState(string body)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens([new OscToken("133", "D", "27")]);
        var before = terminal.ShellIntegration;
        var changes = 0;
        terminal.ShellIntegrationChanged += _ => changes++;
        var tokens = AnsiTokenizer.Tokenize($"\x1b]133;{body}\x07");
        terminal.ApplyTokens(tokens);
        Assert.AreEqual(before, terminal.ShellIntegration);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(string.Concat(tokens.Select(AnsiTokenSerializer.Serialize))));
        Assert.AreEqual(before, terminal.ShellIntegration);
        Assert.AreEqual(0, changes);
    }

    [TestMethod]
    public async Task ShellIntegration_MarkersRetainLatestResult_AndDoNotChangeProgress()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens([new OscToken("9", "4", "2;40")]);
        var changes = new List<TerminalShellIntegration>();
        terminal.ShellIntegrationChanged += state =>
        {
            Assert.AreEqual(state, terminal.ShellIntegration);
            changes.Add(state);
        };
        Assert.AreEqual(TerminalShellIntegration.Default, terminal.ShellIntegration);
        Assert.AreEqual(0, changes.Count);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]133;D;-9\x07\x1b]133;A\x07\x1b]133;B\x07\x1b]133;C\x07" +
            "\x1b]133;C\x07\x1b]133;A\x07\x1b]133;D\x07\x1b]133;D;\x07"));
        TestSeq.AreEqual(new[]
        {
            new TerminalShellIntegration(TerminalShellIntegrationPhase.Finished, -9),
            new TerminalShellIntegration(TerminalShellIntegrationPhase.Prompt, -9),
            new TerminalShellIntegration(TerminalShellIntegrationPhase.CommandLine, -9),
            new TerminalShellIntegration(TerminalShellIntegrationPhase.Executing, -9),
            new TerminalShellIntegration(TerminalShellIntegrationPhase.Prompt, -9),
            new TerminalShellIntegration(TerminalShellIntegrationPhase.Finished, null)
        }, changes);
        Assert.AreEqual(new TerminalProgress(TerminalProgressState.Error, 40), terminal.Progress);
        terminal.ApplyTokens([new OscToken("9", "4", "0")]);
        Assert.AreEqual(TerminalShellIntegrationPhase.Finished, terminal.ShellIntegration.Phase);
    }

    [TestMethod]
    [DataRow("\x1b]", "\x07")]
    [DataRow("\x1b]", "\x1b\\")]
    [DataRow("\u009d", "\u009c")]
    public async Task RawOutput_EveryByteSplit_ParsesShellResult(string introducer, string terminator)
    {
        var bytes = Encoding.UTF8.GetBytes($"{introducer}133;D;-2147483648{terminator}");
        for (var split = 1; split < bytes.Length; split++)
        {
            using var workload = new Hex1bAppWorkloadAdapter();
            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
            var changed = new TaskCompletionSource<TerminalShellIntegration>(TaskCreationOptions.RunContinuationsAsynchronously);
            terminal.ShellIntegrationChanged += state => changed.TrySetResult(state);
            workload.Write(bytes.AsMemory(0, split));
            workload.Write(bytes.AsMemory(split));
            var state = await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.AreEqual(TerminalShellIntegrationPhase.Finished, state.Phase);
            Assert.AreEqual(int.MinValue, state.LastExitCode);
        }
    }

    [TestMethod]
    [DataRow("A")]
    [DataRow("B")]
    [DataRow("C")]
    [DataRow("D")]
    [DataRow("D;0")]
    public async Task RawOutput_EveryByteSplit_EmptyMarkerDoesNotBecomeValid(string remainder)
    {
        var bytes = Encoding.UTF8.GetBytes($"\x1b]133;;{remainder}\x1b\\done");
        for (var split = 1; split < bytes.Length; split++)
        {
            using var workload = new Hex1bAppWorkloadAdapter();
            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
            terminal.ApplyTokens([new OscToken("133", "D", "-7")]);
            workload.Write(bytes.AsMemory(0, split));
            workload.Write(bytes.AsMemory(split));
            using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("done"), TimeSpan.FromSeconds(5), "invalid marker consumed")
                .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
            Assert.AreEqual(TerminalShellIntegrationPhase.Finished, snapshot.ShellIntegration.Phase);
            Assert.AreEqual(-7, snapshot.ShellIntegration.LastExitCode);
        }
    }

    [TestMethod]
    public async Task ActivityRestore_IntermediateRisAndMarkers_OnlyPublishesFinalPair()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]9;4;1;10\x07\x1b]133;C\x07"));
        var progress = new TerminalProgress(TerminalProgressState.Warning, 80);
        var shell = new TerminalShellIntegration(TerminalShellIntegrationPhase.Prompt, 3);
        var progressEvents = new List<TerminalProgress>();
        var shellEvents = new List<TerminalShellIntegration>();
        terminal.ProgressChanged += value =>
        {
            Assert.AreEqual(shell, terminal.ShellIntegration);
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual(progress, snapshot.Progress);
            Assert.AreEqual(shell, snapshot.ShellIntegration);
            progressEvents.Add(value);
        };
        terminal.ShellIntegrationChanged += shellEvents.Add;

        terminal.BeginActivityStateRestore();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b" + "c\x1b]9;4;3\x07\x1b]133;D\x07"));
        terminal.BeginActivityStateRestore();
        terminal.RestoreActivityState(progress, shell, TerminalWorkingDirectory.Default);
        terminal.EndActivityStateRestore();
        Assert.AreEqual(0, progressEvents.Count);
        Assert.AreEqual(0, shellEvents.Count);
        terminal.EndActivityStateRestore();
        TestSeq.AreEqual(new[] { progress }, progressEvents);
        TestSeq.AreEqual(new[] { shell }, shellEvents);

        terminal.BeginActivityStateRestore();
        terminal.ApplyTokens([RisToken.Instance]);
        terminal.RestoreActivityState(progress, shell, TerminalWorkingDirectory.Default);
        terminal.EndActivityStateRestore();
        Assert.AreEqual(1, progressEvents.Count);
        Assert.AreEqual(1, shellEvents.Count);
    }

    [TestMethod]
    public async Task CreateSnapshot_ConcurrentActivityRestore_CapturesOneConsistentPair()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = Task.Run(async () =>
        {
            await started.Task;
            for (var i = 0; i < 500; i++)
            {
                var value = i % 2;
                terminal.RestoreActivityState(
                    new TerminalProgress(TerminalProgressState.Normal, value),
                    new TerminalShellIntegration(TerminalShellIntegrationPhase.Finished, value),
                    TerminalWorkingDirectory.Default);
            }
        }, TestContext.Current.CancellationToken);
        started.SetResult();
        for (var i = 0; i < 500; i++)
        {
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual(snapshot.Progress.Percentage, snapshot.ShellIntegration.LastExitCode);
        }
        await writer.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [TestMethod]
    public async Task IndependentTerminals_Disposal_DoesNotClearOrFabricateActivity()
    {
        using var firstWorkload = new Hex1bAppWorkloadAdapter();
        using var secondWorkload = new Hex1bAppWorkloadAdapter();
        await using var first = Hex1bTerminal.CreateBuilder()
            .WithWorkload(firstWorkload).WithHeadless().WithDimensions(20, 5).Build();
        await using var second = Hex1bTerminal.CreateBuilder()
            .WithWorkload(secondWorkload).WithHeadless().WithDimensions(20, 5).Build();
        first.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]9;4;3\x07\x1b]133;C\x07"));
        var changes = 0;
        first.ProgressChanged += _ => changes++;
        first.ShellIntegrationChanged += _ => changes++;
        await first.DisposeAsync();

        Assert.AreEqual(TerminalProgressState.Indeterminate, first.Progress.State);
        Assert.AreEqual(TerminalShellIntegrationPhase.Executing, first.ShellIntegration.Phase);
        Assert.AreEqual(0, changes);
        Assert.AreEqual(TerminalProgress.Default, second.Progress);
        Assert.AreEqual(TerminalShellIntegration.Default, second.ShellIntegration);
    }
}
