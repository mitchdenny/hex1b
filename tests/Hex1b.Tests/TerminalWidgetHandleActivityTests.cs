using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class TerminalWidgetHandleActivityTests
{
    [TestMethod]
    public async Task StandaloneHandle_AppliedTokens_MatchesAuthoritativeReducer()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        await using var handle = new TerminalWidgetHandle(20, 5);
        var progressChanges = 0;
        var shellChanges = 0;
        var invalidations = 0;
        handle.ProgressChanged += _ => progressChanges++;
        handle.ShellIntegrationChanged += _ => shellChanges++;
        handle.OutputReceived += () => invalidations++;
        Assert.AreEqual(TerminalProgress.Default, handle.Progress);
        Assert.AreEqual(TerminalShellIntegration.Default, handle.ShellIntegration);
        Assert.AreEqual(0, progressChanges + shellChanges);

        foreach (var sequence in new[]
        {
            "\x1b]9;4;0\x07", "\x1b]9;4;1;50\x07", "\x1b]9;4;1;050\x07",
            "\x1b]9;4;2;75\x07", "\x1b]9;4;3;ignored\x07", "\x1b]9;4;4;90\x07",
            "\x1b]9;4;1;-1\x07", "\x1b]133;D;-3\x07", "\x1b]133;A\x07",
            "\x1b]133;B\x07", "\x1b]133;C\x07", "\x1b]133;C\x07",
            "\x1b]133;D\x07", "\x1b]133;D;\x07", "\x1b]133;D;invalid\x07"
        })
        {
            await handle.WriteOutputWithImpactsAsync(
                terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(sequence)),
                TestContext.Current.CancellationToken);
            Assert.AreEqual(terminal.Progress, handle.Progress);
            Assert.AreEqual(terminal.ShellIntegration, handle.ShellIntegration);
        }

        Assert.AreEqual(4, progressChanges);
        Assert.AreEqual(5, shellChanges);
        Assert.AreEqual(9, invalidations);
    }

    [TestMethod]
    public async Task StandaloneHandle_CopyMode_UpdatesLiveWithoutReplayingQueuedActivity()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        await using var handle = new TerminalWidgetHandle(20, 5);
        await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize("old")));
        handle.EnterCopyMode();
        var progressChanges = new List<TerminalProgress>();
        var shellChanges = new List<TerminalShellIntegration>();
        handle.ProgressChanged += progressChanges.Add;
        handle.ShellIntegrationChanged += shellChanges.Add;
        await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            "\x1b]9;4;3\x07\x1b]133;C\x07\x1b" + "c")));
        await handle.WriteOutputWithImpactsAsync(terminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            "\x1b]9;4;4;80\x07\x1b]133;D;5\x07new")));
        Assert.AreEqual("o", handle.GetCell(0, 0).Character);
        Assert.AreEqual(new TerminalProgress(TerminalProgressState.Warning, 80), handle.Progress);
        Assert.AreEqual(new TerminalShellIntegration(TerminalShellIntegrationPhase.Finished, 5), handle.ShellIntegration);
        Assert.AreEqual(3, progressChanges.Count);
        Assert.AreEqual(3, shellChanges.Count);

        handle.ExitCopyMode();

        Assert.AreEqual("n", handle.GetCell(0, 0).Character);
        Assert.AreEqual(3, progressChanges.Count);
        Assert.AreEqual(3, shellChanges.Count);
        Assert.AreEqual(terminal.Progress, handle.Progress);
        Assert.AreEqual(terminal.ShellIntegration, handle.ShellIntegration);
    }

    [TestMethod]
    public async Task AttachedHandle_CopyModeAndRestore_KeepsAuthoritativeLivePair()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(20, 5).WithTerminalWidget(out var handle).Build();
        var firstCell = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handle.OutputReceived += () =>
        {
            if (handle.GetCell(0, 0).Character == "x")
                firstCell.TrySetResult();
        };
        workload.Write("x");
        await firstCell.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        handle.EnterCopyMode();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handle.ShellIntegrationChanged += state =>
        {
            if (state.Phase == TerminalShellIntegrationPhase.Finished)
                received.TrySetResult();
        };
        workload.Write("\x1b]9;4;2;30\x07\x1b]133;D;-4\x07y");
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using var output = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("xy"), TimeSpan.FromSeconds(5), "child output applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        Assert.AreEqual(" ", handle.GetCell(1, 0).Character);
        Assert.AreEqual(terminal.Progress, handle.Progress);
        Assert.AreEqual(terminal.ShellIntegration, handle.ShellIntegration);

        var progress = new TerminalProgress(TerminalProgressState.Indeterminate, null);
        var shell = new TerminalShellIntegration(TerminalShellIntegrationPhase.Executing, -4);
        terminal.RestoreActivityState(progress, shell, TerminalWorkingDirectory.Default);
        var changes = 0;
        handle.ProgressChanged += _ => changes++;
        handle.ShellIntegrationChanged += _ => changes++;
        // An authoritative checkpoint supersedes metadata in any queued text.
        handle.ExitCopyMode();
        Assert.AreEqual(progress, handle.Progress);
        Assert.AreEqual(shell, handle.ShellIntegration);
        Assert.AreEqual(0, changes);
    }

    [TestMethod]
    public async Task AttachedHandle_ResetAndReplacement_UnsubscribesOldTerminalAndDropsQueuedActivity()
    {
        using var oldWorkload = new Hex1bAppWorkloadAdapter();
        using var newWorkload = new Hex1bAppWorkloadAdapter();
        await using var oldTerminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(oldWorkload).WithHeadless().WithDimensions(20, 5).Build();
        await using var newTerminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(newWorkload).WithHeadless().WithDimensions(20, 5).Build();
        await using var handle = new TerminalWidgetHandle(20, 5);
        var lifecycle = (ITerminalLifecycleAwarePresentationAdapter)handle;
        lifecycle.TerminalCreated(oldTerminal);
        oldTerminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]9;4;3\x07\x1b]133;C\x07"));
        handle.EnterCopyMode();
        await handle.WriteOutputWithImpactsAsync(oldTerminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize("stale")));

        handle.Reset();

        Assert.IsFalse(handle.IsInCopyMode);
        Assert.AreEqual(TerminalProgress.Default, handle.Progress);
        Assert.AreEqual(TerminalShellIntegration.Default, handle.ShellIntegration);
        await handle.WriteOutputWithImpactsAsync(oldTerminal.ApplyTokensWithImpacts(AnsiTokenizer.Tokenize(
            "\x1b]9;4;2;99\x07\x1b]133;D;99\x07")));
        Assert.AreEqual(TerminalProgress.Default, handle.Progress);
        Assert.AreEqual(TerminalShellIntegration.Default, handle.ShellIntegration);

        newTerminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]9;4;1;10\x07\x1b]133;B\x07"));
        lifecycle.TerminalCreated(newTerminal);
        oldTerminal.ApplyTokens([RisToken.Instance]);
        handle.ExitCopyMode();
        Assert.AreEqual(newTerminal.Progress, handle.Progress);
        Assert.AreEqual(newTerminal.ShellIntegration, handle.ShellIntegration);
        Assert.AreEqual(" ", handle.GetCell(0, 0).Character);
    }

    [TestMethod]
    public async Task AttachedHandle_RestoreNotifications_SeeFinalPairAndSuppressUnchangedReplay()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        await using var handle = new TerminalWidgetHandle(20, 5);
        ((ITerminalLifecycleAwarePresentationAdapter)handle).TerminalCreated(terminal);
        var progress = new TerminalProgress(TerminalProgressState.Normal, 50);
        var shell = new TerminalShellIntegration(TerminalShellIntegrationPhase.Prompt, -1);
        var changes = 0;
        handle.ProgressChanged += _ =>
        {
            Assert.AreEqual(shell, handle.ShellIntegration);
            changes++;
        };
        handle.ShellIntegrationChanged += _ =>
        {
            Assert.AreEqual(progress, handle.Progress);
            changes++;
        };
        for (var restore = 0; restore < 2; restore++)
        {
            terminal.BeginActivityStateRestore();
            terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b" + "c\x1b]133;C\x07"));
            terminal.RestoreActivityState(progress, shell, TerminalWorkingDirectory.Default);
            terminal.EndActivityStateRestore();
        }
        Assert.AreEqual(2, changes);
    }

    [TestMethod]
    public async Task AttachedHandle_CompletionAndDisposal_RetainsActivityAndStopsNotifications()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        await using var handle = new TerminalWidgetHandle(20, 5);
        var lifecycle = (ITerminalLifecycleAwarePresentationAdapter)handle;
        lifecycle.TerminalCreated(terminal);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]9;4;3\x07\x1b]133;C\x07"));
        var progress = handle.Progress;
        var shell = handle.ShellIntegration;
        var changes = 0;
        handle.ProgressChanged += _ => changes++;
        handle.ShellIntegrationChanged += _ => changes++;

        lifecycle.TerminalCompleted(17);
        await handle.DisposeAsync();
        terminal.ApplyTokens([RisToken.Instance]);

        Assert.AreEqual(17, handle.ExitCode);
        Assert.AreEqual(progress, handle.Progress);
        Assert.AreEqual(shell, handle.ShellIntegration);
        Assert.AreEqual(0, changes);
    }
}
