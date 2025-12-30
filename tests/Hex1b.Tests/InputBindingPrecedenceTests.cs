using Hex1b.Input;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests that encode the input binding precedence rules:
/// 
/// BINDING SCOPE (determined by widget type):
/// - Non-focusable widget + WithInputBindings → Global bindings
/// - Focusable widget + WithInputBindings → Focus bindings
/// 
/// GLOBAL BINDINGS:
/// - Collected from all non-focusable widgets during reconciliation
/// - Last-write-wins for duplicate keys
/// - Default app bindings (Ctrl+C) added first, so user globals override them
/// 
/// FOCUS BINDINGS:
/// - Only active when that specific widget has focus
/// - Override global bindings for the same key
/// 
/// PRECEDENCE (focused widget exists):
/// 1. Focus bindings on focused widget
/// 2. HandleInput on focused widget
/// 3. HandleInput bubble up through parents
/// 4. Global bindings
/// 
/// PRECEDENCE (no focused widget):
/// 1. Global bindings only
/// </summary>
public class InputBindingPrecedenceTests
{
    #region A. No Focused Widget - Global Bindings

    [Fact]
    public async Task A1_NoFocus_GlobalBindingMatches_GlobalBindingFires()
    {
        // RULE: When no focused widget exists, global bindings are checked.
        // SETUP: VStack (non-focusable) with global binding for 'X'
        // ACTION: Press 'X'
        // EXPECTED: Global binding fires
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var globalBindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.X).Action(_ =>
                {
                    globalBindingFired = true;
                    return Task.CompletedTask;
                }, "Global X");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(globalBindingFired, "Global binding should fire when no focused widget exists");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task A2_NoFocus_NoGlobalBindingForKey_NotHandled()
    {
        // RULE: When no binding matches and no focused widget, input is not handled.
        // SETUP: VStack with global binding for 'X' only
        // ACTION: Press 'Y' (no binding)
        // EXPECTED: Binding for 'X' does NOT fire
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var anyBindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.X).Action(_ =>
                {
                    anyBindingFired = true;
                    return Task.CompletedTask;
                }, "Global X");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.Y).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.False(anyBindingFired, "No binding should fire for unbound key");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task A3_NoFocus_MultipleWidgetsDefineSameGlobalKey_LastWriteWins()
    {
        // RULE: Global bindings use last-write-wins semantics.
        // SETUP: Parent VStack defines 'X', child VStack also defines 'X'
        // ACTION: Press 'X'
        // EXPECTED: Child's binding fires (reconciled after parent)
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var parentBindingFired = false;
        var childBindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(outer => [
                outer.VStack(inner => [
                    inner.Test().OnRender(_ => renderOccurred.TrySetResult())
                ]).WithInputBindings(b =>
                {
                    b.Key(Hex1bKey.X).Action(_ =>
                    {
                        childBindingFired = true;
                        return Task.CompletedTask;
                    }, "Child X");
                })
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.X).Action(_ =>
                {
                    parentBindingFired = true;
                    return Task.CompletedTask;
                }, "Parent X");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(childBindingFired, "Child (last reconciled) binding should fire");
        Assert.False(parentBindingFired, "Parent binding should NOT fire (overridden by child)");

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region B. Focused Widget - Precedence Chain

    [Fact]
    public async Task B1_FocusedWidget_FocusBindingMatches_FocusBindingFires()
    {
        // RULE: Focus bindings are checked first when widget is focused.
        // SETUP: Button (focusable) with focus binding for 'X'
        // ACTION: Focus button, press 'X'
        // EXPECTED: Focus binding fires
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var focusBindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Button("Test").WithInputBindings(b =>
                {
                    b.Key(Hex1bKey.X).Action(_ =>
                    {
                        focusBindingFired = true;
                        return Task.CompletedTask;
                    }, "Focus X");
                }),
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        // Button should auto-focus as first focusable widget
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(focusBindingFired, "Focus binding should fire when widget is focused");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task B2_FocusedWidget_NoFocusBinding_HandleInputHandles()
    {
        // RULE: If no focus binding matches, HandleInput is called on focused widget.
        // SETUP: TextBox (focusable, handles character input via HandleInput)
        // ACTION: Focus textbox, press 'a'
        // EXPECTED: TextBox captures the character (HandleInput handles it)
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var textChanged = "";

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.TextBox("").OnTextChanged(e => { textChanged = e.NewText; return Task.CompletedTask; }),
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        // TextBox should auto-focus
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.A).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal("a", textChanged);

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task B4_FocusedWidget_NothingHandles_GlobalBindingFires()
    {
        // RULE: If focus bindings and HandleInput chain don't handle, global bindings fire.
        // SETUP: Button focused (no focus binding for 'Q'), VStack has global 'Q' binding
        // ACTION: Press 'Q'
        // EXPECTED: Global binding fires as fallback
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var globalBindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Button("Test"),
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.Q).Action(_ =>
                {
                    globalBindingFired = true;
                    return Task.CompletedTask;
                }, "Global Q");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        // Button is focused, press Q (button doesn't handle it)
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.Q).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(globalBindingFired, "Global binding should fire when focus chain doesn't handle");

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region C. Focus Bindings Override Global

    [Fact]
    public async Task C1_SameKeyFocusAndGlobal_WidgetFocused_FocusBindingFires()
    {
        // RULE: Focus bindings take precedence over global bindings for the same key.
        // SETUP: VStack has global 'X', Button has focus 'X'
        // ACTION: Focus button, press 'X'
        // EXPECTED: Focus binding fires, global does NOT
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var focusBindingFired = false;
        var globalBindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Button("Test").WithInputBindings(b =>
                {
                    b.Key(Hex1bKey.X).Action(_ =>
                    {
                        focusBindingFired = true;
                        return Task.CompletedTask;
                    }, "Focus X");
                }),
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.X).Action(_ =>
                {
                    globalBindingFired = true;
                    return Task.CompletedTask;
                }, "Global X");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(focusBindingFired, "Focus binding should fire");
        Assert.False(globalBindingFired, "Global binding should NOT fire (overridden by focus)");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task C2_SameKeyFocusAndGlobal_DifferentWidgetFocused_GlobalFires()
    {
        // RULE: When a different widget is focused, focus bindings don't apply.
        // SETUP: Button1 has focus 'X', Button2 has no bindings, VStack has global 'X'
        // ACTION: Focus Button2, press 'X'
        // EXPECTED: Global binding fires (Button1's focus binding doesn't apply)
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var button1FocusBindingFired = false;
        var globalBindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Button("Button1").WithInputBindings(b =>
                {
                    b.Key(Hex1bKey.X).Action(_ =>
                    {
                        button1FocusBindingFired = true;
                        return Task.CompletedTask;
                    }, "Button1 Focus X");
                }),
                v.Button("Button2"),
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.X).Action(_ =>
                {
                    globalBindingFired = true;
                    return Task.CompletedTask;
                }, "Global X");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        // Tab to move focus from Button1 to Button2
        await new Hex1bTerminalInputSequenceBuilder()
            .Tab().Wait(50)
            .Key(Hex1bKey.X).Wait(100)
            .Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.False(button1FocusBindingFired, "Button1's focus binding should NOT fire (not focused)");
        Assert.True(globalBindingFired, "Global binding should fire as fallback");

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region D. Global Binding Collection (Last-Write-Wins)

    [Fact]
    public async Task D1_ParentDefinesKey_ChildRedefines_ChildWins()
    {
        // RULE: Last-write-wins for global bindings (child reconciled after parent).
        // SETUP: Outer VStack 'X', Inner VStack 'X'
        // ACTION: Press 'X'
        // EXPECTED: Inner VStack's binding fires
        
        // Note: This is the same as A3, included for completeness
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var outerFired = false;
        var innerFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(outer => [
                outer.VStack(inner => [
                    inner.Test().OnRender(_ => renderOccurred.TrySetResult())
                ]).WithInputBindings(b =>
                {
                    b.Key(Hex1bKey.X).Action(_ =>
                    {
                        innerFired = true;
                        return Task.CompletedTask;
                    }, "Inner X");
                })
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.X).Action(_ =>
                {
                    outerFired = true;
                    return Task.CompletedTask;
                }, "Outer X");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(innerFired, "Inner (last reconciled) should win");
        Assert.False(outerFired, "Outer should be overridden");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task D2_DeeplyNestedWidget_OverridesRoot()
    {
        // RULE: Deeply nested widget's global binding overrides root's.
        // SETUP: Root VStack 'X' -> VStack -> VStack with 'X'
        // ACTION: Press 'X'
        // EXPECTED: Deepest VStack's binding fires
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var rootFired = false;
        var deepFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(root => [
                root.VStack(middle => [
                    middle.VStack(deep => [
                        deep.Test().OnRender(_ => renderOccurred.TrySetResult())
                    ]).WithInputBindings(b =>
                    {
                        b.Key(Hex1bKey.X).Action(_ =>
                        {
                            deepFired = true;
                            return Task.CompletedTask;
                        }, "Deep X");
                    })
                ])
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.X).Action(_ =>
                {
                    rootFired = true;
                    return Task.CompletedTask;
                }, "Root X");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(deepFired, "Deepest (last reconciled) should win");
        Assert.False(rootFired, "Root should be overridden");

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region E. Default App Bindings

    [Fact]
    public async Task E1_DefaultCtrlC_NoOverride_AppStops()
    {
        // RULE: Default Ctrl+C binding stops the app.
        // SETUP: VStack with no bindings
        // ACTION: Press Ctrl+C
        // EXPECTED: App stops gracefully
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = true }
        );

        var runTask = app.RunAsync(CancellationToken.None);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        // Send Ctrl+C
        new Hex1bTerminalInputSequenceBuilder().Ctrl().Key(Hex1bKey.C).Build().Apply(terminal);
        
        // App should stop within reasonable time
        var completed = await Task.WhenAny(runTask, Task.Delay(1000, TestContext.Current.CancellationToken)) == runTask;
        Assert.True(completed, "App should stop when Ctrl+C is pressed");
    }

    [Fact]
    public async Task E2_UserGlobalBinding_OverridesDefaultCtrlC()
    {
        // RULE: User global bindings override default app bindings.
        // SETUP: VStack with global Ctrl+C binding
        // ACTION: Press Ctrl+C
        // EXPECTED: User binding fires, app does NOT stop
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var userBindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Ctrl().Key(Hex1bKey.C).Action(_ =>
                {
                    userBindingFired = true;
                    return Task.CompletedTask;
                }, "User Ctrl+C");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = true }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        await new Hex1bTerminalInputSequenceBuilder().Ctrl().Key(Hex1bKey.C).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(userBindingFired, "User binding should fire");
        Assert.False(runTask.IsCompleted, "App should NOT stop (user overrode Ctrl+C)");

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region F. Chord Bindings

    [Fact]
    public async Task F1_FocusChordBinding_CompletesAndFires()
    {
        // RULE: Chord bindings work on focusable widgets.
        // SETUP: Button with focus chord 'g' then 'g'
        // ACTION: Focus button, press 'g', press 'g'
        // EXPECTED: Chord completes and fires
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var chordFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Button("Test").WithInputBindings(b =>
                {
                    b.Key(Hex1bKey.G).Then().Key(Hex1bKey.G).Action(_ =>
                    {
                        chordFired = true;
                        return Task.CompletedTask;
                    }, "Focus gg chord");
                }),
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.G).Wait(50)
            .Key(Hex1bKey.G).Wait(100)
            .Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(chordFired, "Chord should complete and fire");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task F2_GlobalChordBinding_CompletesAndFires()
    {
        // RULE: Chord bindings work on global level.
        // SETUP: VStack with global chord 'g' then 'g'
        // ACTION: Press 'g', press 'g'
        // EXPECTED: Chord completes and fires
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var chordFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.G).Then().Key(Hex1bKey.G).Action(_ =>
                {
                    chordFired = true;
                    return Task.CompletedTask;
                }, "Global gg chord");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.G).Wait(50)
            .Key(Hex1bKey.G).Wait(100)
            .Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(chordFired, "Global chord should complete and fire");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task F4_MidChord_EscapePressed_ChordCancelled()
    {
        // RULE: Escape cancels pending chord.
        // SETUP: VStack with global chord 'g' then 'g'
        // ACTION: Press 'g', press Escape, press 'g'
        // EXPECTED: Chord does NOT fire (was cancelled)
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var chordFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.G).Then().Key(Hex1bKey.G).Action(_ =>
                {
                    chordFired = true;
                    return Task.CompletedTask;
                }, "Global gg chord");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.G).Wait(50)
            .Escape().Wait(50)
            .Key(Hex1bKey.G).Wait(100)
            .Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.False(chordFired, "Chord should be cancelled by Escape");

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region H. API Behavior

    [Fact]
    public async Task H1_WithInputBindings_OnNonFocusable_CreatesGlobalBindings()
    {
        // RULE: WithInputBindings on non-focusable widget creates global bindings.
        // SETUP: VStack (non-focusable) with binding for 'X'
        // ACTION: No focus exists, press 'X'
        // EXPECTED: Binding fires (it's global)
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var bindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.X).Action(_ =>
                {
                    bindingFired = true;
                    return Task.CompletedTask;
                }, "VStack X");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(bindingFired, "VStack binding should fire as global binding");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task H2_WithInputBindings_OnFocusable_CreatesFocusBindings()
    {
        // RULE: WithInputBindings on focusable widget creates focus bindings.
        // SETUP: Button with binding for 'X', Button2 with no bindings
        // ACTION: Focus Button2, press 'X'
        // EXPECTED: Button1's binding does NOT fire (focus bindings only when focused)
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var button1BindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Button("Button1").WithInputBindings(b =>
                {
                    b.Key(Hex1bKey.X).Action(_ =>
                    {
                        button1BindingFired = true;
                        return Task.CompletedTask;
                    }, "Button1 X");
                }),
                v.Button("Button2"),
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        // Tab to Button2
        await new Hex1bTerminalInputSequenceBuilder()
            .Tab().Wait(50)
            .Key(Hex1bKey.X).Wait(100)
            .Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.False(button1BindingFired, "Button1's focus binding should NOT fire when Button2 is focused");

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region I. Tree Compositions

    [Fact]
    public async Task I6_RescueWidgetWrapping_DoesNotBreakBindings()
    {
        // RULE: RescueWidget (enabled by default) should not prevent bindings from working.
        // SETUP: VStack with global binding (RescueWidget wraps automatically)
        // ACTION: Press 'X'
        // EXPECTED: Binding fires
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var bindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.X).Action(_ =>
                {
                    bindingFired = true;
                    return Task.CompletedTask;
                }, "Global X");
            }),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload, 
                EnableDefaultCtrlCExit = false,
                EnableRescue = true  // Explicitly enable rescue (default)
            }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(bindingFired, "Global binding should fire even with RescueWidget wrapping");

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region Ctrl Modifier Tests

    [Fact]
    public async Task CtrlModifier_GlobalBinding_Fires()
    {
        // RULE: Ctrl+Key bindings work as global bindings.
        // SETUP: VStack with Ctrl+X binding
        // ACTION: Press Ctrl+X
        // EXPECTED: Binding fires
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var bindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Ctrl().Key(Hex1bKey.X).Action(_ =>
                {
                    bindingFired = true;
                    return Task.CompletedTask;
                }, "Global Ctrl+X");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder().Ctrl().Key(Hex1bKey.X).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(bindingFired, "Ctrl+X global binding should fire");

        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task CtrlModifier_FocusBinding_OverridesGlobal()
    {
        // RULE: Focus Ctrl+Key bindings override global Ctrl+Key bindings.
        // SETUP: VStack has global Ctrl+S, Button has focus Ctrl+S
        // ACTION: Focus button, press Ctrl+S
        // EXPECTED: Focus binding fires, global does NOT
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var focusBindingFired = false;
        var globalBindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Button("Test").WithInputBindings(b =>
                {
                    b.Ctrl().Key(Hex1bKey.S).Action(_ =>
                    {
                        focusBindingFired = true;
                        return Task.CompletedTask;
                    }, "Focus Ctrl+S");
                }),
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Ctrl().Key(Hex1bKey.S).Action(_ =>
                {
                    globalBindingFired = true;
                    return Task.CompletedTask;
                }, "Global Ctrl+S");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        
        await new Hex1bTerminalInputSequenceBuilder().Ctrl().Key(Hex1bKey.S).Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(focusBindingFired, "Focus Ctrl+S binding should fire");
        Assert.False(globalBindingFired, "Global Ctrl+S should NOT fire");

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region Shift Modifier Tests

    [Fact]
    public async Task ShiftModifier_GlobalBinding_Fires()
    {
        // RULE: Shift+Key bindings work as global bindings.
        // SETUP: VStack with Shift+Tab binding
        // ACTION: Press Shift+Tab
        // EXPECTED: Binding fires
        
        using var workload = new Hex1bAppWorkloadAdapter();

        
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var bindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b =>
            {
                b.Shift().Key(Hex1bKey.Tab).Action(_ =>
                {
                    bindingFired = true;
                    return Task.CompletedTask;
                }, "Global Shift+Tab");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder().Shift().Tab().Wait(100).Capture("final").Build().ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(bindingFired, "Shift+Tab global binding should fire");

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region Modifier Validation Tests

    [Fact]
    public void ModifierValidation_CtrlThenShift_ThrowsException()
    {
        // RULE: Cannot combine Ctrl and Shift modifiers.
        // SETUP: Try to create Ctrl+Shift binding
        // EXPECTED: InvalidOperationException is thrown
        
        var builder = new InputBindingsBuilder();
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            builder.Ctrl().Shift();
        });
        
        Assert.Contains("Cannot combine Ctrl and Shift", ex.Message);
    }

    [Fact]
    public void ModifierValidation_ShiftThenCtrl_ThrowsException()
    {
        // RULE: Cannot combine Ctrl and Shift modifiers.
        // SETUP: Try to create Shift+Ctrl binding
        // EXPECTED: InvalidOperationException is thrown
        
        var builder = new InputBindingsBuilder();
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            builder.Shift().Ctrl();
        });
        
        Assert.Contains("Cannot combine Ctrl and Shift", ex.Message);
    }

    #endregion
}
