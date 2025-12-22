using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Testing;
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
            State = new ToggleSwitchState
            {
                Options = ["Manual", "Auto", "Delayed"]
            }
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
            State = new ToggleSwitchState { Options = [] }
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
            State = new ToggleSwitchState { Options = ["Only"] }
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
            State = new ToggleSwitchState { Options = ["On", "Off"] }
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
            State = new ToggleSwitchState
            {
                Options = ["VeryLongOption1", "VeryLongOption2"]
            }
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
            State = new ToggleSwitchState
            {
                Options = ["A", "B", "C"],
                SelectedIndex = 1
            },
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
            State = new ToggleSwitchState
            {
                Options = ["On", "Off"],
                SelectedIndex = 0
            },
            IsFocused = true
        };
        node.Arrange(new Rect(0, 0, 40, 1));

        node.Render(context);

        // Should contain ANSI escape codes for styling
        Assert.Contains("\x1b[", terminal.CreateSnapshot().RawOutput);
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
            State = new ToggleSwitchState { Options = ["A", "B"] },
            IsFocused = true
        };
        var unfocusedNode = new ToggleSwitchNode
        {
            State = new ToggleSwitchState { Options = ["A", "B"] },
            IsFocused = false
        };
        focusedNode.Arrange(new Rect(0, 0, 40, 1));
        unfocusedNode.Arrange(new Rect(0, 0, 40, 1));

        focusedNode.Render(focusedContext);
        unfocusedNode.Render(unfocusedContext);

        Assert.NotEqual(focusedTerminal.RawOutput, unfocusedTerminal.RawOutput);
    }

    [Fact]
    public void Render_EmptyOptions_DoesNotThrow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new ToggleSwitchNode
        {
            State = new ToggleSwitchState { Options = [] },
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
            State = new ToggleSwitchState
            {
                Options = ["A", "B", "C"],
                SelectedIndex = 0
            },
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.State.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_LeftArrow_MovesToPreviousOption()
    {
        var node = new ToggleSwitchNode
        {
            State = new ToggleSwitchState
            {
                Options = ["A", "B", "C"],
                SelectedIndex = 2
            },
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.State.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_RightArrow_WrapsToFirstOption()
    {
        var node = new ToggleSwitchNode
        {
            State = new ToggleSwitchState
            {
                Options = ["A", "B", "C"],
                SelectedIndex = 2
            },
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.State.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_LeftArrow_WrapsToLastOption()
    {
        var node = new ToggleSwitchNode
        {
            State = new ToggleSwitchState
            {
                Options = ["A", "B", "C"],
                SelectedIndex = 0
            },
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(2, node.State.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_OtherKey_NotHandled()
    {
        var node = new ToggleSwitchNode
        {
            State = new ToggleSwitchState
            {
                Options = ["A", "B"],
                SelectedIndex = 0
            },
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));

        Assert.Equal(InputResult.NotHandled, result);
        Assert.Equal(0, node.State.SelectedIndex);
    }

    [Fact]
    public async Task HandleInput_WhenNotFocused_BindingsStillExecute()
    {
        // Note: With the new input binding architecture, bindings execute at the node level
        // regardless of focus. Focus is a tree concept handled by InputRouter.RouteInput().
        var node = new ToggleSwitchNode
        {
            State = new ToggleSwitchState
            {
                Options = ["A", "B"],
                SelectedIndex = 0
            },
            IsFocused = false
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));

        // Bindings execute regardless of focus state when using RouteInputToNode
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.State.SelectedIndex);  // Selection changed
    }

    [Fact]
    public async Task HandleInput_SelectionChanged_CallsCallback()
    {
        var callbackInvoked = false;
        var callbackIndex = -1;
        var callbackValue = "";
        
        var node = new ToggleSwitchNode
        {
            State = new ToggleSwitchState
            {
                Options = ["Manual", "Auto", "Delayed"],
                SelectedIndex = 0
            },
            IsFocused = true
        };
        
        node.SelectionChangedAction = _ =>
        {
            callbackInvoked = true;
            callbackIndex = node.State.SelectedIndex;
            callbackValue = node.State.Options.ElementAtOrDefault(node.State.SelectedIndex) ?? "";
            return Task.CompletedTask;
        };

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));

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
            State = new ToggleSwitchState { Options = ["A", "B"] }
        };
        var bounds = new Rect(5, 10, 20, 1);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    #endregion

    #region State Tests

    [Fact]
    public void State_SelectedOption_ReturnsCorrectValue()
    {
        var state = new ToggleSwitchState
        {
            Options = ["Manual", "Auto", "Delayed"],
            SelectedIndex = 1
        };

        Assert.Equal("Auto", state.SelectedOption);
    }

    [Fact]
    public void State_SelectedOption_EmptyOptions_ReturnsNull()
    {
        var state = new ToggleSwitchState { Options = [] };

        Assert.Null(state.SelectedOption);
    }

    [Fact]
    public void State_SetSelection_UpdatesIndex()
    {
        var state = new ToggleSwitchState
        {
            Options = ["A", "B", "C"],
            SelectedIndex = 0
        };

        state.SetSelection(2);

        Assert.Equal(2, state.SelectedIndex);
        Assert.Equal("C", state.SelectedOption);
    }

    [Fact]
    public void State_SetSelection_InvalidIndex_DoesNotChange()
    {
        var state = new ToggleSwitchState
        {
            Options = ["A", "B"],
            SelectedIndex = 0
        };

        state.SetSelection(5);

        Assert.Equal(0, state.SelectedIndex);
    }

    [Fact]
    public void State_SetSelection_NegativeIndex_DoesNotChange()
    {
        var state = new ToggleSwitchState
        {
            Options = ["A", "B"],
            SelectedIndex = 1
        };

        state.SetSelection(-1);

        Assert.Equal(1, state.SelectedIndex);
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_ToggleSwitch_RendersViaHex1bApp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var state = new ToggleSwitchState
        {
            Options = ["Manual", "Auto", "Delayed"]
        };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.ToggleSwitch(state)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Manual"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
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
        var state = new ToggleSwitchState
        {
            Options = ["Off", "On"]
        };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.ToggleSwitch(state)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Off"), TimeSpan.FromSeconds(2))
            .Right()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.Equal(1, state.SelectedIndex);
        Assert.Equal("On", state.SelectedOption);
    }

    [Fact]
    public async Task Integration_ToggleSwitch_MultipleNavigations()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var state = new ToggleSwitchState
        {
            Options = ["Low", "Medium", "High"]
        };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.ToggleSwitch(state)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Low"), TimeSpan.FromSeconds(2))
            .Right().Right()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.Equal(2, state.SelectedIndex);
        Assert.Equal("High", state.SelectedOption);
    }

    [Fact]
    public async Task Integration_ToggleSwitch_WithOtherWidgets_TabNavigates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var toggleState = new ToggleSwitchState { Options = ["A", "B"] };
        var buttonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.ToggleSwitch(toggleState),
                    v.Button("Click").OnClick(_ => { buttonClicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        // Navigate right on toggle, then tab to button, then click
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click"), TimeSpan.FromSeconds(2))
            .Right().Tab().Enter()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.Equal(1, toggleState.SelectedIndex);
        Assert.True(buttonClicked);
    }

    [Fact]
    public async Task Integration_ToggleSwitch_CallbackTriggered()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var lastSelectedValue = "";
        var state = new ToggleSwitchState
        {
            Options = ["Mode1", "Mode2"]
        };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.ToggleSwitch(state)
                        .OnSelectionChanged(args => lastSelectedValue = args.SelectedOption)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Mode1"), TimeSpan.FromSeconds(2))
            .Right()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
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
            State = new ToggleSwitchState { Options = ["Manual", "Auto", "Delayed"] }
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 27, 1));

        // Click on "Auto" (local X position within "Auto" range: starts at 11)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 12, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(12, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.State.SelectedIndex);
    }

    [Fact]
    public void HandleMouseClick_FirstOption_SelectsIndex0()
    {
        var node = new ToggleSwitchNode
        {
            State = new ToggleSwitchState { Options = ["On", "Off"] }
        };
        node.State.SelectedIndex = 1; // Start with second selected
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 13, 1));

        // Click on "On" (starts at X=2)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 3, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(3, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.State.SelectedIndex);
    }

    [Fact]
    public void HandleMouseClick_OnBracket_ReturnsNotHandled()
    {
        var node = new ToggleSwitchNode
        {
            State = new ToggleSwitchState { Options = ["On", "Off"] }
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
            State = new ToggleSwitchState { Options = [] }
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 1));

        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(5, 0, mouseEvent);

        Assert.Equal(InputResult.NotHandled, result);
    }

    #endregion
}
