using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the Hex1bTerminalInputSequenceBuilder and Hex1bTerminalInputSequence.
/// </summary>
[TestClass]
public class Hex1bTestSequenceTests
{
    #region Builder Basic Tests

    [TestMethod]
    public void Build_EmptySequence_ReturnsEmptySequence()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder().Build();
        
        Assert.IsEmpty(sequence.Steps);
    }

    [TestMethod]
    public void Key_AddsKeyInputStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.A)
            .Build();
        
        TestSeq.Single(sequence.Steps);
        var step = TestSeq.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.AreEqual(Hex1bKey.A, step.Key);
        Assert.AreEqual("a", step.Text);
        Assert.AreEqual(Hex1bModifiers.None, step.Modifiers);
    }

    [TestMethod]
    public void Shift_Key_AddsShiftModifier()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.A)
            .Build();
        
        var step = TestSeq.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.AreEqual(Hex1bKey.A, step.Key);
        Assert.AreEqual("A", step.Text);
        Assert.AreEqual(Hex1bModifiers.Shift, step.Modifiers);
    }

    [TestMethod]
    public void Ctrl_Key_AddsControlModifier()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build();
        
        var step = TestSeq.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.AreEqual(Hex1bKey.C, step.Key);
        Assert.AreEqual(Hex1bModifiers.Control, step.Modifiers);
    }

    [TestMethod]
    public void Ctrl_Shift_Key_AddsCombinedModifiers()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Shift().Key(Hex1bKey.Z)
            .Build();
        
        var step = TestSeq.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.AreEqual(Hex1bModifiers.Control | Hex1bModifiers.Shift, step.Modifiers);
    }

    [TestMethod]
    public void ModifiersResetAfterKey()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.A)
            .Key(Hex1bKey.B)
            .Build();
        
        var step1 = TestSeq.IsType<KeyInputStep>(sequence.Steps[0]);
        var step2 = TestSeq.IsType<KeyInputStep>(sequence.Steps[1]);
        
        Assert.AreEqual(Hex1bModifiers.Control, step1.Modifiers);
        Assert.AreEqual(Hex1bModifiers.None, step2.Modifiers);
    }

    #endregion

    #region Text Input Tests

    [TestMethod]
    public void Type_AddsTextInputStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Type("Hello")
            .Build();
        
        TestSeq.Single(sequence.Steps);
        var step = TestSeq.IsType<TextInputStep>(sequence.Steps[0]);
        Assert.AreEqual("Hello", step.Text);
        Assert.AreEqual(TimeSpan.Zero, step.DelayBetweenKeys);
    }

    [TestMethod]
    public void FastType_AddsTextInputStepWithNoDelay()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .FastType("Test")
            .Build();
        
        var step = TestSeq.IsType<TextInputStep>(sequence.Steps[0]);
        Assert.AreEqual(TimeSpan.Zero, step.DelayBetweenKeys);
    }

    [TestMethod]
    public void SlowType_AddsTextInputStepWithDefaultDelay()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .SlowType("Test")
            .Build();
        
        var step = TestSeq.IsType<TextInputStep>(sequence.Steps[0]);
        Assert.AreEqual(Hex1bTerminalInputSequenceOptions.Default.SlowTypeDelay, step.DelayBetweenKeys);
    }

    [TestMethod]
    public void SlowType_WithCustomDelay_UsesCustomDelay()
    {
        var delay = TimeSpan.FromMilliseconds(100);
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .SlowType("Test", delay)
            .Build();
        
        var step = TestSeq.IsType<TextInputStep>(sequence.Steps[0]);
        Assert.AreEqual(delay, step.DelayBetweenKeys);
    }

    #endregion

    #region Common Key Shortcuts Tests

    [TestMethod]
    public void Enter_AddsEnterKeyStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder().Enter().Build();
        
        var step = TestSeq.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.AreEqual(Hex1bKey.Enter, step.Key);
    }

    [TestMethod]
    public void Tab_AddsTabKeyStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder().Tab().Build();
        
        var step = TestSeq.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.AreEqual(Hex1bKey.Tab, step.Key);
    }

    [TestMethod]
    public void Escape_AddsEscapeKeyStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder().Escape().Build();
        
        var step = TestSeq.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.AreEqual(Hex1bKey.Escape, step.Key);
    }

    [TestMethod]
    public void ArrowKeys_AddCorrectKeySteps()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Up().Down().Left().Right()
            .Build();
        
        Assert.AreEqual(4, sequence.Steps.Count);
        Assert.AreEqual(Hex1bKey.UpArrow, ((KeyInputStep)sequence.Steps[0]).Key);
        Assert.AreEqual(Hex1bKey.DownArrow, ((KeyInputStep)sequence.Steps[1]).Key);
        Assert.AreEqual(Hex1bKey.LeftArrow, ((KeyInputStep)sequence.Steps[2]).Key);
        Assert.AreEqual(Hex1bKey.RightArrow, ((KeyInputStep)sequence.Steps[3]).Key);
    }

    [TestMethod]
    public void Shift_Tab_AddsBackTab()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Shift().Tab()
            .Build();
        
        var step = TestSeq.IsType<KeyInputStep>(sequence.Steps[0]);
        Assert.AreEqual(Hex1bKey.Tab, step.Key);
        Assert.AreEqual(Hex1bModifiers.Shift, step.Modifiers);
    }

    #endregion

    #region Mouse Input Tests

    [TestMethod]
    public void MouseMoveTo_AddsMouseMoveStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(10, 20)
            .Build();
        
        var step = TestSeq.IsType<MouseInputStep>(sequence.Steps[0]);
        Assert.AreEqual(MouseButton.None, step.Button);
        Assert.AreEqual(MouseAction.Move, step.Action);
        Assert.AreEqual(10, step.X);
        Assert.AreEqual(20, step.Y);
    }

    [TestMethod]
    public void MouseMove_AddsRelativeMove()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(10, 10)
            .MouseMove(5, -3)
            .Build();
        
        var step = TestSeq.IsType<MouseInputStep>(sequence.Steps[1]);
        Assert.AreEqual(15, step.X);
        Assert.AreEqual(7, step.Y);
    }

    [TestMethod]
    public void Click_AddsDownAndUpSteps()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(5, 5)
            .Click()
            .Build();
        
        Assert.AreEqual(3, sequence.Steps.Count);
        var downStep = TestSeq.IsType<MouseInputStep>(sequence.Steps[1]);
        var upStep = TestSeq.IsType<MouseInputStep>(sequence.Steps[2]);
        
        Assert.AreEqual(MouseButton.Left, downStep.Button);
        Assert.AreEqual(MouseAction.Down, downStep.Action);
        Assert.AreEqual(MouseButton.Left, upStep.Button);
        Assert.AreEqual(MouseAction.Up, upStep.Action);
    }

    [TestMethod]
    public void ClickAt_MovesToPositionAndClicks()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(15, 25)
            .Build();
        
        var downStep = TestSeq.IsType<MouseInputStep>(sequence.Steps[0]);
        Assert.AreEqual(15, downStep.X);
        Assert.AreEqual(25, downStep.Y);
    }

    [TestMethod]
    public void DoubleClick_SetsClickCountTo2()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(5, 5)
            .DoubleClick()
            .Build();
        
        var downStep = TestSeq.IsType<MouseInputStep>(sequence.Steps[1]);
        Assert.AreEqual(2, downStep.ClickCount);
    }

    [TestMethod]
    public void Drag_CreatesDownDragUpSequence()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Drag(10, 10, 20, 15)
            .Build();
        
        // Drag creates: Down, Wait (10ms), Drag, Up = 4 steps
        Assert.AreEqual(4, sequence.Steps.Count);
        
        var downStep = TestSeq.IsType<MouseInputStep>(sequence.Steps[0]);
        Assert.AreEqual(MouseAction.Down, downStep.Action);
        Assert.AreEqual(10, downStep.X);
        Assert.AreEqual(10, downStep.Y);
        
        // Step 1 is the wait step
        TestSeq.IsType<WaitStep>(sequence.Steps[1]);
        
        var dragStep = TestSeq.IsType<MouseInputStep>(sequence.Steps[2]);
        Assert.AreEqual(MouseAction.Drag, dragStep.Action);
        Assert.AreEqual(20, dragStep.X);
        Assert.AreEqual(15, dragStep.Y);
        
        var upStep = TestSeq.IsType<MouseInputStep>(sequence.Steps[3]);
        Assert.AreEqual(MouseAction.Up, upStep.Action);
        Assert.AreEqual(20, upStep.X);
        Assert.AreEqual(15, upStep.Y);
    }

    [TestMethod]
    public void Ctrl_Click_AddsControlModifier()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(5, 5)
            .Ctrl().Click()
            .Build();
        
        var downStep = TestSeq.IsType<MouseInputStep>(sequence.Steps[1]);
        Assert.AreEqual(Hex1bModifiers.Control, downStep.Modifiers);
    }

    [TestMethod]
    public void ScrollUp_AddsScrollSteps()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .ScrollUp(3)
            .Build();
        
        Assert.AreEqual(3, sequence.Steps.Count);
        foreach (var step in sequence.Steps)
        {
            var mouseStep = TestSeq.IsType<MouseInputStep>(step);
            Assert.AreEqual(MouseButton.ScrollUp, mouseStep.Button);
        }
    }

    [TestMethod]
    public void ScrollDown_AddsScrollSteps()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .ScrollDown(2)
            .Build();
        
        Assert.AreEqual(2, sequence.Steps.Count);
        var mouseStep = TestSeq.IsType<MouseInputStep>(sequence.Steps[0]);
        Assert.AreEqual(MouseButton.ScrollDown, mouseStep.Button);
    }

    #endregion

    #region Wait Tests

    [TestMethod]
    public void Wait_AddsWaitStep()
    {
        var duration = TimeSpan.FromMilliseconds(100);
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Wait(duration)
            .Build();
        
        var step = TestSeq.IsType<WaitStep>(sequence.Steps[0]);
        Assert.AreEqual(duration, step.Duration);
    }

    [TestMethod]
    public void Wait_Milliseconds_AddsWaitStep()
    {
        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Wait(50)
            .Build();
        
        var step = TestSeq.IsType<WaitStep>(sequence.Steps[0]);
        Assert.AreEqual(TimeSpan.FromMilliseconds(50), step.Duration);
    }

    #endregion

    #region Complex Sequence Tests

    [TestMethod]
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
        
        Assert.AreEqual(6, sequence.Steps.Count);
        TestSeq.IsType<TextInputStep>(sequence.Steps[0]);
        TestSeq.IsType<WaitStep>(sequence.Steps[1]);
        TestSeq.IsType<KeyInputStep>(sequence.Steps[2]);
        TestSeq.IsType<KeyInputStep>(sequence.Steps[3]);
        TestSeq.IsType<KeyInputStep>(sequence.Steps[4]);
        TestSeq.IsType<KeyInputStep>(sequence.Steps[5]);
    }

    #endregion

    #region Terminal Integration Tests

    [TestMethod]
    public async Task ApplyAsync_SendsKeyEventsToTerminal()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var textEntered = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new TextBoxWidget("").OnTextChanged(args => { textEntered = args.NewText; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableInputCoalescing = false }
        );

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Type("Hello")
            .Build();

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        
        cts.Cancel();
        await runTask;
        
        Assert.AreEqual("Hello", textEntered);
    }

    [TestMethod]
    public async Task ApplyAsync_SendsKeyboardShortcuts()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var ctrlCPressed = false;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget([
                    new ButtonWidget("Test")
                ]).InputBindings(b => 
                {
                    b.Ctrl().Key(Hex1bKey.X).Action(_ => ctrlCPressed = true);
                })
            ),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload,
                EnableDefaultCtrlCExit = false
            }
        );

        var sequence = new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.X)
            .Build();

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await sequence.ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        
        cts.Cancel();
        await runTask;
        
        Assert.IsTrue(ctrlCPressed);
    }

    [TestMethod]
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
        Assert.IsTrue(snapshot.ContainsText("> Item 3"));
    }

    [TestMethod]
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
        
        Assert.AreEqual("First", text1);
        Assert.AreEqual("Second", text2);
    }

    [TestMethod]
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
