using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the Hex1bTerminalInputSequenceBuilder and Hex1bTerminalInputSequence.
/// </summary>
public class Hex1bTestSequenceTests
{
    #region Builder Basic Tests

    [Fact]
    public void Build_EmptySequence_ReturnsEmptySequence()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder().Build();
        
        Assert.Empty(sequence.Steps);
    }

    [Fact]
    public void Key_AddsKeyInputStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.A)
            .Build();
        
        Assert.Single(sequence.Steps);
        var step = Assert.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.Equal(Hex1bKey.A, step.Key);
        Assert.Equal("a", step.Text);
        Assert.Equal(Hex1bModifiers.None, step.Modifiers);
    }

    [Fact]
    public void Shift_Key_AddsShiftModifier()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.A)
            .Build();
        
        var step = Assert.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.Equal(Hex1bKey.A, step.Key);
        Assert.Equal("A", step.Text);
        Assert.Equal(Hex1bModifiers.Shift, step.Modifiers);
    }

    [Fact]
    public void Ctrl_Key_AddsControlModifier()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build();
        
        var step = Assert.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.Equal(Hex1bKey.C, step.Key);
        Assert.Equal(Hex1bModifiers.Control, step.Modifiers);
    }

    [Fact]
    public void Ctrl_Shift_Key_AddsCombinedModifiers()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Shift().Key(Hex1bKey.Z)
            .Build();
        
        var step = Assert.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.Equal(Hex1bModifiers.Control | Hex1bModifiers.Shift, step.Modifiers);
    }

    [Fact]
    public void ModifiersResetAfterKey()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.A)
            .Key(Hex1bKey.B)
            .Build();
        
        var step1 = Assert.IsType<KeyInputStep>(sequence.Steps[0]);
        var step2 = Assert.IsType<KeyInputStep>(sequence.Steps[1]);
        
        Assert.Equal(Hex1bModifiers.Control, step1.Modifiers);
        Assert.Equal(Hex1bModifiers.None, step2.Modifiers);
    }

    #endregion

    #region Text Input Tests

    [Fact]
    public void Type_AddsTextInputStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Type("Hello")
            .Build();
        
        Assert.Single(sequence.Steps);
        var step = Assert.IsType<TextInputStep>(sequence.Steps[0]);
        Assert.Equal("Hello", step.Text);
        Assert.Equal(TimeSpan.Zero, step.DelayBetweenKeys);
    }

    [Fact]
    public void FastType_AddsTextInputStepWithNoDelay()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .FastType("Test")
            .Build();
        
        var step = Assert.IsType<TextInputStep>(sequence.Steps[0]);
        Assert.Equal(TimeSpan.Zero, step.DelayBetweenKeys);
    }

    [Fact]
    public void SlowType_AddsTextInputStepWithDefaultDelay()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .SlowType("Test")
            .Build();
        
        var step = Assert.IsType<TextInputStep>(sequence.Steps[0]);
        Assert.Equal(Hex1bTerminalInputSequenceOptions.Default.SlowTypeDelay, step.DelayBetweenKeys);
    }

    [Fact]
    public void SlowType_WithCustomDelay_UsesCustomDelay()
    {
        var delay = TimeSpan.FromMilliseconds(100);
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .SlowType("Test", delay)
            .Build();
        
        var step = Assert.IsType<TextInputStep>(sequence.Steps[0]);
        Assert.Equal(delay, step.DelayBetweenKeys);
    }

    #endregion

    #region Common Key Shortcuts Tests

    [Fact]
    public void Enter_AddsEnterKeyStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder().Enter().Build();
        
        var step = Assert.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.Equal(Hex1bKey.Enter, step.Key);
    }

    [Fact]
    public void Tab_AddsTabKeyStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder().Tab().Build();
        
        var step = Assert.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.Equal(Hex1bKey.Tab, step.Key);
    }

    [Fact]
    public void Escape_AddsEscapeKeyStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder().Escape().Build();
        
        var step = Assert.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.Equal(Hex1bKey.Escape, step.Key);
    }

    [Fact]
    public void ArrowKeys_AddCorrectKeySteps()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Up().Down().Left().Right()
            .Build();
        
        Assert.Equal(4, sequence.Steps.Count);
        Assert.Equal(Hex1bKey.UpArrow, ((KeyInputStep)sequence.Steps[0]).Key);
        Assert.Equal(Hex1bKey.DownArrow, ((KeyInputStep)sequence.Steps[1]).Key);
        Assert.Equal(Hex1bKey.LeftArrow, ((KeyInputStep)sequence.Steps[2]).Key);
        Assert.Equal(Hex1bKey.RightArrow, ((KeyInputStep)sequence.Steps[3]).Key);
    }

    [Fact]
    public void Shift_Tab_AddsBackTab()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Shift().Tab()
            .Build();
        
        var step = Assert.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.Equal(Hex1bKey.Tab, step.Key);
        Assert.Equal(Hex1bModifiers.Shift, step.Modifiers);
    }

    #endregion

    #region Mouse Input Tests

    [Fact]
    public void MouseMoveTo_AddsMouseMoveStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(10, 20)
            .Build();
        
        var step = Assert.IsType<MouseInputStep>(sequence.Steps[0]);
        Assert.Equal(MouseButton.None, step.Button);
        Assert.Equal(MouseAction.Move, step.Action);
        Assert.Equal(10, step.X);
        Assert.Equal(20, step.Y);
    }

    [Fact]
    public void MouseMove_AddsRelativeMove()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(10, 10)
            .MouseMove(5, -3)
            .Build();
        
        var step = Assert.IsType<MouseInputStep>(sequence.Steps[1]);
        Assert.Equal(15, step.X);
        Assert.Equal(7, step.Y);
    }

    [Fact]
    public void Click_AddsDownAndUpSteps()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(5, 5)
            .Click()
            .Build();
        
        Assert.Equal(3, sequence.Steps.Count);
        var downStep = Assert.IsType<MouseInputStep>(sequence.Steps[1]);
        var upStep = Assert.IsType<MouseInputStep>(sequence.Steps[2]);
        
        Assert.Equal(MouseButton.Left, downStep.Button);
        Assert.Equal(MouseAction.Down, downStep.Action);
        Assert.Equal(MouseButton.Left, upStep.Button);
        Assert.Equal(MouseAction.Up, upStep.Action);
    }

    [Fact]
    public void ClickAt_MovesToPositionAndClicks()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(15, 25)
            .Build();
        
        var downStep = Assert.IsType<MouseInputStep>(sequence.Steps[0]);
        Assert.Equal(15, downStep.X);
        Assert.Equal(25, downStep.Y);
    }

    [Fact]
    public void DoubleClick_SetsClickCountTo2()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(5, 5)
            .DoubleClick()
            .Build();
        
        var downStep = Assert.IsType<MouseInputStep>(sequence.Steps[1]);
        Assert.Equal(2, downStep.ClickCount);
    }

    [Fact]
    public void Drag_CreatesDownDragUpSequence()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Drag(10, 10, 20, 15)
            .Build();
        
        // Drag creates: Down, Wait (10ms), Drag, Up = 4 steps
        Assert.Equal(4, sequence.Steps.Count);
        
        var downStep = Assert.IsType<MouseInputStep>(sequence.Steps[0]);
        Assert.Equal(MouseAction.Down, downStep.Action);
        Assert.Equal(10, downStep.X);
        Assert.Equal(10, downStep.Y);
        
        // Step 1 is the wait step
        Assert.IsType<WaitStep>(sequence.Steps[1]);
        
        var dragStep = Assert.IsType<MouseInputStep>(sequence.Steps[2]);
        Assert.Equal(MouseAction.Drag, dragStep.Action);
        Assert.Equal(20, dragStep.X);
        Assert.Equal(15, dragStep.Y);
        
        var upStep = Assert.IsType<MouseInputStep>(sequence.Steps[3]);
        Assert.Equal(MouseAction.Up, upStep.Action);
        Assert.Equal(20, upStep.X);
        Assert.Equal(15, upStep.Y);
    }

    [Fact]
    public void Ctrl_Click_AddsControlModifier()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(5, 5)
            .Ctrl().Click()
            .Build();
        
        var downStep = Assert.IsType<MouseInputStep>(sequence.Steps[1]);
        Assert.Equal(Hex1bModifiers.Control, downStep.Modifiers);
    }

    [Fact]
    public void ScrollUp_AddsScrollSteps()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .ScrollUp(3)
            .Build();
        
        Assert.Equal(3, sequence.Steps.Count);
        foreach (var step in sequence.Steps)
        {
            var mouseStep = Assert.IsType<MouseInputStep>(step);
            Assert.Equal(MouseButton.ScrollUp, mouseStep.Button);
        }
    }

    [Fact]
    public void ScrollDown_AddsScrollSteps()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .ScrollDown(2)
            .Build();
        
        Assert.Equal(2, sequence.Steps.Count);
        var mouseStep = Assert.IsType<MouseInputStep>(sequence.Steps[0]);
        Assert.Equal(MouseButton.ScrollDown, mouseStep.Button);
    }

    #endregion

    #region Wait Tests

    [Fact]
    public void Wait_AddsWaitStep()
    {
        var duration = TimeSpan.FromMilliseconds(100);
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Wait(duration)
            .Build();
        
        var step = Assert.IsType<WaitStep>(sequence.Steps[0]);
        Assert.Equal(duration, step.Duration);
    }

    [Fact]
    public void Wait_Milliseconds_AddsWaitStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Wait(50)
            .Build();
        
        var step = Assert.IsType<WaitStep>(sequence.Steps[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(50), step.Duration);
    }

    #endregion

    #region Complex Sequence Tests

    [Fact]
    public void ComplexSequence_BuildsCorrectly()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Type("search")
            .Wait(50)
            .Enter()
            .Down()
            .Down()
            .Enter()
            .Build();
        
        Assert.Equal(6, sequence.Steps.Count);
        Assert.IsType<TextInputStep>(sequence.Steps[0]);
        Assert.IsType<WaitStep>(sequence.Steps[1]);
        Assert.IsType<KeyInputStep>(sequence.Steps[2]);
        Assert.IsType<KeyInputStep>(sequence.Steps[3]);
        Assert.IsType<KeyInputStep>(sequence.Steps[4]);
        Assert.IsType<KeyInputStep>(sequence.Steps[5]);
    }

    #endregion

    #region Terminal Integration Tests

    [Fact]
    public async Task ApplyAsync_SendsKeyEventsToTerminal()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var textEntered = "";
        var textComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new TextBoxWidget("").OnTextChanged(args =>
                    {
                        textEntered = args.NewText;
                        if (textEntered == "Hello") textComplete.TrySetResult();
                        return Task.CompletedTask;
                    })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableInputCoalescing = false }
        );

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("Hello")
            .Build();

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await textComplete.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        
        cts.Cancel();
        await runTask;
        
        Assert.Equal("Hello", textEntered);
    }

    [Fact]
    public async Task ApplyAsync_SendsKeyboardShortcuts()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var ctrlXPressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new ButtonWidget("Test")
                ]).WithInputBindings(b => 
                {
                    b.Ctrl().Key(Hex1bKey.X).Action(_ => { ctrlXPressed.TrySetResult(); return Task.CompletedTask; });
                })
            ),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload,
                EnableDefaultCtrlCExit = false
            }
        );

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.X)
            .Build();

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await ctrlXPressed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        
        cts.Cancel();
        await runTask;
        
        Assert.True(ctrlXPressed.Task.IsCompleted);
    }

    [Fact]
    public async Task ApplyAsync_NavigatesWithArrowKeys()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var items = new[] { "Item 1", "Item 2", "Item 3" };
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new ListWidget(items)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableInputCoalescing = false }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for app to initialize, navigate down twice, wait for selection, then capture and exit
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("> Item 1"), TimeSpan.FromSeconds(5)) // Wait for list to render with selection
            .Down()
            .WaitUntil(s => s.ContainsText("> Item 2"), TimeSpan.FromSeconds(5)) // Wait for first navigation
            .Down()
            .WaitUntil(s => s.ContainsText("> Item 3"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;
        
        // Third item should be selected
        Assert.True(snapshot.ContainsText("> Item 3"));
    }

    [Fact]
    public async Task ApplyAsync_TabNavigatesBetweenWidgets()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var text1 = "";
        var text2 = "";
        var text1Complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var text2Complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new TextBoxWidget("").OnTextChanged(args =>
                    {
                        text1 = args.NewText;
                        if (text1 == "First") text1Complete.TrySetResult();
                        return Task.CompletedTask;
                    }),
                    new TextBoxWidget("").OnTextChanged(args =>
                    {
                        text2 = args.NewText;
                        if (text2 == "Second") text2Complete.TrySetResult();
                        return Task.CompletedTask;
                    })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableInputCoalescing = false }
        );

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("First")
            .Tab()
            .Type("Second")
            .Build();

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken);
        
        await text1Complete.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await text2Complete.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        
        cts.Cancel();
        await runTask;
        
        Assert.Equal("First", text1);
        Assert.Equal("Second", text2);
    }

    [Fact]
    public async Task ApplyAsync_WithoutAppRunning_DoesNotThrow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Type("Test")
            .Build();

        // Should not throw - just verifies ApplyAsync works without an app running
        await sequence.ApplyAsync(terminal);
    }

    #endregion
}
