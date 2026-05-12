using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
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
    public async Task Measure_ReturnsCorrectSize_ThreeOptions()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["Manual", "Auto", "Delayed"]
        };

        var size = node.Measure(Constraints.Unbounded);

        // Per-option chips: " Manual " (8) + " Auto " (6) + " Delayed " (9) = 23
        Assert.Equal(23, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public async Task Measure_EmptyOptions_ReturnsZeroWidth()
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
    public async Task Measure_SingleOption_ReturnsCorrectSize()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["Only"]
        };

        var size = node.Measure(Constraints.Unbounded);

        // " Only " = 1 + 4 + 1 = 6
        Assert.Equal(6, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public async Task Measure_TwoOptions_ReturnsCorrectSize()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["On", "Off"]
        };

        var size = node.Measure(Constraints.Unbounded);

        // " On " (4) + " Off " (5) = 9
        Assert.Equal(9, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public async Task Measure_CJKCharacters_CorrectSize()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["汉字", "Auto", "한글"]
        };

        var size = node.Measure(Constraints.Unbounded);

        // Per-option chips: " 汉字 " (6) + " Auto " (6) + " 한글 " (6) = 18
        Assert.Equal(18, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public async Task Measure_RespectsMaxWidthConstraint()
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
    public async Task Render_Unfocused_ShowsOptions()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new ToggleSwitchNode
        {
            Options = ["A", "B", "C"],
            SelectedIndex = 1,
            IsFocused = false
        };
        node.Arrange(new Rect(0, 0, 40, 1));

        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("A") && s.ContainsText("B") && s.ContainsText("C"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);

        var line = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Contains("A", line);
        Assert.Contains("B", line);
        Assert.Contains("C", line);
    }

    [Fact]
    public async Task Render_Focused_ContainsAnsiCodes()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
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
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("On") && s.ContainsText("Off"), TimeSpan.FromSeconds(5), "On/Off options visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
    }

    [Fact]
    public async Task Render_FocusedAndUnfocused_ProduceDifferentOutput()
    {
        using var focusedWorkload = new Hex1bAppWorkloadAdapter();

        using var focusedTerminal = Hex1bTerminal.CreateBuilder().WithWorkload(focusedWorkload).WithHeadless().WithDimensions(40, 5).Build();
        using var unfocusedWorkload = new Hex1bAppWorkloadAdapter();

        using var unfocusedTerminal = Hex1bTerminal.CreateBuilder().WithWorkload(unfocusedWorkload).WithHeadless().WithDimensions(40, 5).Build();
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

        var pattern = new CellPatternSearcher().Find("A");
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(focusedTerminal);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(unfocusedTerminal);

        // Focused toggle should have different styling (colors or attributes)
        var focusedMatch = focusedTerminal.CreateSnapshot().SearchPattern(pattern).First;
        Assert.NotNull(focusedMatch);
        
        var focusedCells = focusedMatch.Cells;
        var focusedHasStyling = focusedCells.Any(c => 
            c.Cell.IsReverse || 
            c.Cell.Foreground.HasValue || 
            c.Cell.Background.HasValue);
        
        Assert.True(focusedHasStyling, "Focused toggle should have styling applied");
    }

    [Fact]
    public async Task Render_EmptyOptions_DoesNotThrow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
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

    [Fact]
    public async Task Render_UnselectedOption_PaintsUnselectedBackground()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new ToggleSwitchNode
        {
            Options = ["On", "Off"],
            SelectedIndex = 0,
            IsFocused = false
        };
        // Layout: " On " (chip 0, cells 0-3) + " Off " (chip 1, cells 4-8) = 9 cells
        node.Arrange(new Rect(0, 0, 9, 1));
        node.Render(context);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("On") && s.ContainsText("Off"), TimeSpan.FromSeconds(5), "options visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var expectedUnselectedBg = context.Theme.Get(ToggleSwitchTheme.UnselectedBackgroundColor);
        // Chip 1 ("Off") is unselected — every cell of the chip should
        // sit on the unselected background.
        for (var x = 4; x <= 8; x++)
        {
            Assert.Equal(expectedUnselectedBg, snapshot.GetCell(x, 0).Background);
        }
        Assert.Equal("O", snapshot.GetCell(5, 0).Character);
        Assert.Equal("f", snapshot.GetCell(6, 0).Character);
        Assert.Equal("f", snapshot.GetCell(7, 0).Character);
    }

    [Fact]
    public async Task Render_FocusedSelectedOption_PaintsFocusedSelectedBackground()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new ToggleSwitchNode
        {
            Options = ["On", "Off"],
            SelectedIndex = 0,
            IsFocused = true
        };
        // Layout: " On " (chip 0, cells 0-3, selected) + " Off " (chip 1, cells 4-8)
        node.Arrange(new Rect(0, 0, 9, 1));
        node.Render(context);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("On") && s.ContainsText("Off"), TimeSpan.FromSeconds(5), "options visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var expectedSelectedBg = context.Theme.Get(ToggleSwitchTheme.FocusedSelectedBackgroundColor);
        var expectedUnselectedBg = context.Theme.Get(ToggleSwitchTheme.UnselectedBackgroundColor);

        // Chip 0 ("On", selected, focused) — every cell sits on the focused selected background.
        for (var x = 0; x <= 3; x++)
        {
            Assert.Equal(expectedSelectedBg, snapshot.GetCell(x, 0).Background);
        }
        Assert.Equal("O", snapshot.GetCell(1, 0).Character);
        Assert.Equal("n", snapshot.GetCell(2, 0).Character);

        // Chip 1 ("Off", unselected) — sits on the unselected background.
        for (var x = 4; x <= 8; x++)
        {
            Assert.Equal(expectedUnselectedBg, snapshot.GetCell(x, 0).Background);
        }
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
    public async Task IsFocusable_ReturnsTrue()
    {
        var node = new ToggleSwitchNode();

        Assert.True(node.IsFocusable);
    }

    #endregion

    #region Layout Tests

    [Fact]
    public async Task Arrange_SetsBounds()
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
    public async Task SelectedOption_ReturnsCorrectValue()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["Manual", "Auto", "Delayed"],
            SelectedIndex = 1
        };

        Assert.Equal("Auto", node.SelectedOption);
    }

    [Fact]
    public async Task SelectedOption_EmptyOptions_ReturnsNull()
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.ToggleSwitch(["Manual", "Auto", "Delayed"])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Manual"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("Manual"));
        Assert.True(snapshot.ContainsText("Auto"));
        Assert.True(snapshot.ContainsText("Delayed"));
    }

    [Fact]
    public async Task Integration_ToggleSwitch_ArrowNavigates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.ContainsText("Off"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.ContainsText("Low"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.ContainsText("Click"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.ContainsText("Mode1"), TimeSpan.FromSeconds(5))
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
    public async Task HandleMouseClick_SelectsClickedOption()
    {
        // Layout: " Manual  Auto  Delayed " — per-option chips:
        // 0-7 = " Manual " (chip 0), 8-13 = " Auto " (chip 1), 14-22 = " Delayed " (chip 2)
        var node = new ToggleSwitchNode
        {
            Options = ["Manual", "Auto", "Delayed"]
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 23, 1));

        // Click anywhere inside chip 1 ("Auto", cells 8-13)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(10, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleMouseClick_FirstOption_SelectsIndex0()
    {
        // Layout: " On  Off " — chip 0 covers cells 0-3, chip 1 covers cells 4-8
        var node = new ToggleSwitchNode
        {
            Options = ["On", "Off"],
            SelectedIndex = 1 // Start with second selected
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 9, 1));

        // Click on the "On" chip (any cell in 0-3)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 1, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(1, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleMouseClick_OnLeadingPadding_SelectsFirstOption()
    {
        // Padding cells are part of their owning chip, so clicking the
        // leading padding cell of chip 0 should select option 0.
        var node = new ToggleSwitchNode
        {
            Options = ["On", "Off"],
            SelectedIndex = 1
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 9, 1));

        // Click on chip 0's leading padding cell (X=0)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 0, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(0, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleMouseClick_CJKCharacters_UsesDisplayWidth()
    {
        // Layout: " 汉字  Off " — chip 0 covers cells 0-5 because
        // "汉字" is 4 display cells wide, and chip 1 starts at cell 6.
        var node = new ToggleSwitchNode
        {
            Options = ["汉字", "Off"],
            SelectedIndex = 1
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 11, 1));

        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 4, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(4, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.SelectedIndex);
    }

    [Fact]
    public async Task HandleMouseClick_PastLastChip_ReturnsNotHandled()
    {
        var node = new ToggleSwitchNode
        {
            Options = ["On", "Off"]
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 20, 1));

        // Click past the last chip (chip 1 ends at X=8, so X=9+ is outside any chip)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 12, 0, Hex1bModifiers.None);
        var result = node.HandleMouseClick(12, 0, mouseEvent);

        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public async Task HandleMouseClick_EmptyOptions_ReturnsNotHandled()
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
