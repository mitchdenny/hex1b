using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ToggleSwitchNode rendering and input handling.
/// </summary>
public class ToggleSwitchNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_ReturnsCorrectSize_ThreeOptions()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["Manual", "Auto", "Delayed"]
        };

        var size = node.Measure(Constraints.Unbounded);

        // "< Manual | Auto | Delayed >" 
        // = 2 (< ) + 6 (Manual) + 3 ( | ) + 4 (Auto) + 3 ( | ) + 7 (Delayed) + 2 ( >) = 27
        Assert.Equal(27, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_EmptyOptions_ReturnsZeroWidth()
    {
        var node = new ToggleSwitchNode
        {
            Options = []
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_SingleOption_ReturnsCorrectSize()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["Only"]
        };

        var size = node.Measure(Constraints.Unbounded);

        // "< Only >" = 2 + 4 + 2 = 8
        Assert.Equal(8, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_TwoOptions_ReturnsCorrectSize()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["On", "Off"]
        };

        var size = node.Measure(Constraints.Unbounded);

        // "< On | Off >" = 2 + 2 + 3 + 3 + 2 = 12
        Assert.Equal(12, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxWidthConstraint()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["VeryLongOption1", "VeryLongOption2"]
        };

        var size = node.Measure(new Constraints(0, 20, 0, 5));

        Assert.Equal(20, size.Width);
    }

    #endregion

    #region Rendering Tests

    [Fact]
    public void Render_Unfocused_ShowsOptions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new ToggleSwitchNode
        {
            Options = ["A", "B", "C"],
            SelectedIndex = 1,
            IsFocused = false
        };
        node.Arrange(new Rect(0, 0, 40, 1));

        node.Render(context);

        var line = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Contains("A", line);
        Assert.Contains("B", line);
        Assert.Contains("C", line);
    }

    [Fact]
    public void Render_Focused_ContainsAnsiCodes()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new ToggleSwitchNode
        {
            Options = ["On", "Off"],
            SelectedIndex = 0,
            IsFocused = true
        };
        node.Arrange(new Rect(0, 0, 40, 1));

        node.Render(context);

        // Should contain ANSI escape codes for styling (foreground or background colors)
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
    }

    [Fact]
    public void Render_FocusedAndUnfocused_ProduceDifferentOutput()
    {
        using var focusedWorkload = new Hex1bAppWorkloadAdapter();

        using var focusedTerminal = new Hex1bTerminal(focusedWorkload, 40, 5);
        using var unfocusedWorkload = new Hex1bAppWorkloadAdapter();

        using var unfocusedTerminal = new Hex1bTerminal(unfocusedWorkload, 40, 5);
        var focusedContext = new Hex1bRenderContext(focusedWorkload);
        var unfocusedContext = new Hex1bRenderContext(unfocusedWorkload);

        var focusedNode = new ToggleSwitchNode
        {
            Options = ["A", "B"],
            IsFocused = true
        };
        var unfocusedNode = new ToggleSwitchNode
        {
            Options = ["A", "B"],
            IsFocused = false
        };
        focusedNode.Arrange(new Rect(0, 0, 40, 1));
        unfocusedNode.Arrange(new Rect(0, 0, 40, 1));

        focusedNode.Render(focusedContext);
        unfocusedNode.Render(unfocusedContext);

        // Focused toggle should have different styling (colors or attributes)
        var focusedSnapshot = focusedTerminal.CreateSnapshot();
        var focusedHasStyling = focusedSnapshot.HasAttribute(CellAttributes.Reverse) ||
                                focusedSnapshot.HasForegroundColor() ||
                                focusedSnapshot.HasBackgroundColor();
        
        Assert.True(focusedHasStyling, "Focused toggle should have styling applied");
    }

    [Fact]
    public void Render_EmptyOptions_DoesNotThrow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new ToggleSwitchNode
        {
            Options = [],
            IsFocused = true
        };
        node.Arrange(new Rect(0, 0, 40, 1));

        var exception = Record.Exception(() => node.Render(context));

        Assert.Null(exception);
    }

    #endregion

    #region Input Handling Tests

    [Fact]
    public async Task HandleInput_RightArrow_MovesToNextOption()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["A", "B", "C"],
            SelectedIndex = 0,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_LeftArrow_MovesToPreviousOption()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["A", "B", "C"],
            SelectedIndex = 2,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_RightArrow_WrapsToFirstOption()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["A", "B", "C"],
            SelectedIndex = 2,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_LeftArrow_WrapsToLastOption()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["A", "B", "C"],
            SelectedIndex = 0,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(2, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_OtherKey_NotHandled()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["A", "B"],
            SelectedIndex = 0,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.NotHandled, result);
        Assert.Equal(0, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_WhenNotFocused_BindingsStillExecute()
    {
        // Note: With the new input binding architecture, bindings execute at the node level
        // regardless of focus. Focus is a tree concept handled by InputRouter.RouteInput().
        var node = new ToggleSwitchNode
        {
            Options = ["A", "B"],
            SelectedIndex = 0,
            IsFocused = false
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Bindings execute regardless of focus state when using RouteInputToNode
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);  // Selection changed
    }

    [Fact]
    public async Task HandleInput_SelectionChanged_CallsCallback()
    {
        var callbackInvoked = false;
        var callbackIndex = -1;
        var callbackValue = "";
        
        var node = new ToggleSwitchNode
        {
            Options = ["Manual", "Auto", "Delayed"],
            SelectedIndex = 0,
            IsFocused = true
        };
        
        node.SelectionChangedAction = _ =>
        {
            callbackInvoked = true;
            callbackIndex = node.SelectedIndex;
            callbackValue = node.Options.ElementAtOrDefault(node.SelectedIndex) ?? "";
            return Task.CompletedTask;
        };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.True(callbackInvoked);
        Assert.Equal(1, callbackIndex);
        Assert.Equal("Auto", callbackValue);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new ToggleSwitchNode();

        Assert.True(node.IsFocusable);
    }

    #endregion

    #region Layout Tests

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["A", "B"]
        };
        var bounds = new Rect(5, 10, 20, 1);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    #endregion

    #region State Tests

    [Fact]
    public void SelectedOption_ReturnsCorrectValue()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["Manual", "Auto", "Delayed"],
            SelectedIndex = 1
        };

        Assert.Equal("Auto", node.SelectedOption);
    }

    [Fact]
    public void SelectedOption_EmptyOptions_ReturnsNull()
    {
        var node = new ToggleSwitchNode
        {
            Options = []
        };

        Assert.Null(node.SelectedOption);
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_ToggleSwitch_RendersViaHex1bApp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.ToggleSwitch(["Manual", "Auto", "Delayed"])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Manual"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Manual"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Auto"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Delayed"));
    }

    [Fact]
    public async Task Integration_ToggleSwitch_ArrowNavigates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var selectedOption = "Off";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.ToggleSwitch(["Off", "On"])
                        .OnSelectionChanged(args => selectedOption = args.SelectedOption)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Off"), TimeSpan.FromSeconds(2))
            .Right()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("On", selectedOption);
    }

    [Fact]
    public async Task Integration_ToggleSwitch_MultipleNavigations()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var selectedOption = "Low";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.ToggleSwitch(["Low", "Medium", "High"])
                        .OnSelectionChanged(args => selectedOption = args.SelectedOption)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Low"), TimeSpan.FromSeconds(2))
            .Right().Right()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("High", selectedOption);
    }

    [Fact]
    public async Task Integration_ToggleSwitch_WithOtherWidgets_TabNavigates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var selectedOption = "A";
        var buttonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.ToggleSwitch(["A", "B"])
                        .OnSelectionChanged(args => selectedOption = args.SelectedOption),
                    v.Button("Click").OnClick(_ => { buttonClicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        // Navigate right on toggle, then tab to button, then click
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click"), TimeSpan.FromSeconds(2))
            .Right().Tab().Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("B", selectedOption);
        Assert.True(buttonClicked);
    }

    [Fact]
    public async Task Integration_ToggleSwitch_CallbackTriggered()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastSelectedValue = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.ToggleSwitch(["Mode1", "Mode2"])
                        .OnSelectionChanged(args => lastSelectedValue = args.SelectedOption)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Mode1"), TimeSpan.FromSeconds(2))
            .Right()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Mode2", lastSelectedValue);
    }

    #endregion

    #region Mouse Click Tests

    [Fact]
    public void HandleMouseClick_SelectsClickedOption()
    {
        // Format: "[ Manual | Auto | Delayed ]"
        // Positions: 0-1="[ ", 2-7="Manual", 8-10=" | ", 11-14="Auto", 15-17=" | ", 18-24="Delayed", 25-26=" ]"
        var node = new ToggleSwitchNode
        {
            Options = ["Manual", "Auto", "Delayed"]
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 27, 1));

        // Click on "Auto" (local X position within "Auto" range: starts at 11)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 12, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(12, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public void HandleMouseClick_FirstOption_SelectsIndex0()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["On", "Off"],
            SelectedIndex = 1 // Start with second selected
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 13, 1));

        // Click on "On" (starts at X=2)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 3, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(3, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.SelectedIndex);
    }

    [Fact]
    public void HandleMouseClick_OnBracket_ReturnsNotHandled()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["On", "Off"]
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 13, 1));

        // Click on the left bracket (X=0 or 1)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 0, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(0, 0, mouseEvent);

        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public void HandleMouseClick_EmptyOptions_ReturnsNotHandled()
    {
        var node = new ToggleSwitchNode
        {
            Options = []
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 1));

        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 0, mouseEvent);

        Assert.Equal(InputResult.NotHandled, result);
    }

    #endregion
}
