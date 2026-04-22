using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for OverridesCapture chord bindings — multi-step key sequences that
/// work even when a node (e.g., TerminalWidget) has captured all input.
/// </summary>
public class CaptureOverrideChordTests
{
    [Fact]
    public async Task OverridesCapture_Chord_FiresWhenTerminalHasCapture()
    {
        // Arrange: TerminalWidget captures input, but Ctrl+B,D chord should still work
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        var chordFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Create a child terminal that will capture input
        await using var childWorkload = StreamWorkloadAdapter.CreateHeadless(40, 10);
        using var childTerminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(childWorkload).WithHeadless().WithDimensions(40, 10)
            .WithTerminalWidget(out var handle).Build();

        using var childCts = new CancellationTokenSource();
        _ = childTerminal.RunAsync(childCts.Token);

        using var app = new Hex1bApp(
            ctx =>
            {
                var widget = ctx.VStack(v => [
                    v.Terminal(handle).Fill(),
                    v.Test().OnRender(_ => renderOccurred.TrySetResult())
                ]).WithInputBindings(bindings =>
                {
                    // Chord: Ctrl+B, then D (modifier on first step only)
                    bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.D)
                        .OverridesCapture()
                        .Action(_ =>
                        {
                            chordFired.TrySetResult();
                            return Task.CompletedTask;
                        }, "Detach");
                });

                return Task.FromResult<Hex1bWidget>(widget);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        // Act - Send the chord: Ctrl+B then D
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.B).Wait(50)
            .Key(Hex1bKey.D).Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Assert - chord should have fired
        await chordFired.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        await childCts.CancelAsync();
        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task OverridesCapture_MultipleChordsSamePrefix_BothWork()
    {
        // Arrange: Two chords sharing Ctrl+B prefix, one ends with D, other with S
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        var detachFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var switchFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var childWorkload = StreamWorkloadAdapter.CreateHeadless(40, 10);
        using var childTerminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(childWorkload).WithHeadless().WithDimensions(40, 10)
            .WithTerminalWidget(out var handle).Build();

        using var childCts = new CancellationTokenSource();
        _ = childTerminal.RunAsync(childCts.Token);

        using var app = new Hex1bApp(
            ctx =>
            {
                var widget = ctx.VStack(v => [
                    v.Terminal(handle).Fill(),
                    v.Test().OnRender(_ => renderOccurred.TrySetResult())
                ]).WithInputBindings(bindings =>
                {
                    bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.D)
                        .OverridesCapture()
                        .Action(_ =>
                        {
                            detachFired.TrySetResult();
                            return Task.CompletedTask;
                        }, "Detach");

                    bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.S)
                        .OverridesCapture()
                        .Action(_ =>
                        {
                            switchFired.TrySetResult();
                            return Task.CompletedTask;
                        }, "Switch");
                });

                return Task.FromResult<Hex1bWidget>(widget);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        // Act - Send Ctrl+B, S chord
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.B).Wait(50)
            .Key(Hex1bKey.S).Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await switchFired.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.False(detachFired.Task.IsCompleted, "Detach should not have fired");

        // Act - Send Ctrl+B, D chord
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.B).Wait(50)
            .Key(Hex1bKey.D).Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await detachFired.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        await childCts.CancelAsync();
        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task OverridesCapture_ChordCancelled_ByNonMatchingSecondKey()
    {
        // Arrange: Ctrl+B,D is bound, but we send Ctrl+B,X (no binding for X)
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        var chordFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var childWorkload = StreamWorkloadAdapter.CreateHeadless(40, 10);
        using var childTerminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(childWorkload).WithHeadless().WithDimensions(40, 10)
            .WithTerminalWidget(out var handle).Build();

        using var childCts = new CancellationTokenSource();
        _ = childTerminal.RunAsync(childCts.Token);

        using var app = new Hex1bApp(
            ctx =>
            {
                var widget = ctx.VStack(v => [
                    v.Terminal(handle).Fill(),
                    v.Test().OnRender(_ => renderOccurred.TrySetResult())
                ]).WithInputBindings(bindings =>
                {
                    bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.D)
                        .OverridesCapture()
                        .Action(_ =>
                        {
                            chordFired.TrySetResult();
                            return Task.CompletedTask;
                        }, "Detach");
                });

                return Task.FromResult<Hex1bWidget>(widget);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        // Act - Send Ctrl+B, then X (not D) — chord should NOT fire
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.B).Wait(50)
            .Key(Hex1bKey.X).Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Wait a bit to ensure chord doesn't fire
        await Task.Delay(200);
        Assert.False(chordFired.Task.IsCompleted, "Chord should not fire for non-matching second key");

        // Act - Now send the correct chord: Ctrl+B, D — should still work after failed attempt
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.B).Wait(50)
            .Key(Hex1bKey.D).Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await chordFired.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        await childCts.CancelAsync();
        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task OverridesCapture_SingleKeyBinding_StillWorks()
    {
        // Arrange: Single-key OverridesCapture binding (not a chord) should work as before
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        var keyFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var childWorkload = StreamWorkloadAdapter.CreateHeadless(40, 10);
        using var childTerminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(childWorkload).WithHeadless().WithDimensions(40, 10)
            .WithTerminalWidget(out var handle).Build();

        using var childCts = new CancellationTokenSource();
        _ = childTerminal.RunAsync(childCts.Token);

        using var app = new Hex1bApp(
            ctx =>
            {
                var widget = ctx.VStack(v => [
                    v.Terminal(handle).Fill(),
                    v.Test().OnRender(_ => renderOccurred.TrySetResult())
                ]).WithInputBindings(bindings =>
                {
                    bindings.Key(Hex1bKey.F12)
                        .OverridesCapture()
                        .Action(_ =>
                        {
                            keyFired.TrySetResult();
                            return Task.CompletedTask;
                        }, "Quick action");
                });

                return Task.FromResult<Hex1bWidget>(widget);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        // Act - Send F12
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.F12).Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Assert
        await keyFired.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        await childCts.CancelAsync();
        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }
}
