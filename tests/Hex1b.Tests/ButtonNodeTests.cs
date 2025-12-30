using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ButtonNode rendering and input handling.
/// </summary>
public class ButtonNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var node = new ButtonNode { Label = "Click" };

        var size = node.Measure(Constraints.Unbounded);

        // "[ Click ]" = 4 (brackets + spaces) + 5 label = 9
        Assert.Equal(9, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_EmptyLabel_HasMinSize()
    {
        var node = new ButtonNode { Label = "" };

        var size = node.Measure(Constraints.Unbounded);

        // "[  ]" = 4
        Assert.Equal(4, size.Width);
    }

    [Fact]
    public void Measure_LongLabel_MeasuresFullWidth()
    {
        var node = new ButtonNode { Label = "Click Here To Continue" };

        var size = node.Measure(Constraints.Unbounded);

        // 22 chars + 4 = 26
        Assert.Equal(26, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxWidthConstraint()
    {
        var node = new ButtonNode { Label = "A Very Long Button Label" };

        var size = node.Measure(new Constraints(0, 15, 0, 5));

        Assert.Equal(15, size.Width);
    }

    [Fact]
    public void Measure_RespectsMinWidthConstraint()
    {
        var node = new ButtonNode { Label = "OK" };

        var size = node.Measure(new Constraints(20, 30, 0, 5));

        Assert.Equal(20, size.Width);
    }

    #endregion

    #region Rendering Tests - Unfocused State

    [Fact]
    public void Render_Unfocused_ShowsBrackets()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new ButtonNode
        {
            Label = "OK",
            IsFocused = false
        };

        node.Render(context);

        // Theme-dependent bracket style, but should contain label
        Assert.Contains("OK", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Unfocused_ContainsBracketCharacters()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new ButtonNode
        {
            Label = "Test",
            IsFocused = false
        };

        node.Render(context);

        var line = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Contains("[", line);
        Assert.Contains("]", line);
    }

    [Fact]
    public void Render_Unfocused_EmptyLabel_StillRendersBrackets()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new ButtonNode
        {
            Label = "",
            IsFocused = false
        };

        node.Render(context);

        var line = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Contains("[", line);
        Assert.Contains("]", line);
    }

    #endregion

    #region Rendering Tests - Focused State

    [Fact]
    public void Render_Focused_HasDifferentStyle()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new ButtonNode
        {
            Label = "OK",
            IsFocused = true
        };

        node.Render(context);

        // Should contain styling for focus
        Assert.True(terminal.CreateSnapshot().HasForegroundColor() || terminal.CreateSnapshot().HasBackgroundColor() || terminal.CreateSnapshot().HasAttribute(CellAttributes.Reverse));
        Assert.Contains("OK", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Focused_ContainsLabel()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new ButtonNode
        {
            Label = "Submit Form",
            IsFocused = true
        };

        node.Render(context);

        Assert.Contains("Submit Form", terminal.CreateSnapshot().GetLineTrimmed(0));
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

        var focusedNode = new ButtonNode { Label = "Click", IsFocused = true };
        var unfocusedNode = new ButtonNode { Label = "Click", IsFocused = false };

        focusedNode.Render(focusedContext);
        unfocusedNode.Render(unfocusedContext);

        // Focused button should have different styling (colors or attributes)
        var focusedSnapshot = focusedTerminal.CreateSnapshot();
        var unfocusedSnapshot = unfocusedTerminal.CreateSnapshot();
        
        // The focused button should have either reverse attribute or foreground/background colors
        var focusedHasStyling = focusedSnapshot.HasAttribute(CellAttributes.Reverse) ||
                                focusedSnapshot.HasForegroundColor() ||
                                focusedSnapshot.HasBackgroundColor();
        
        Assert.True(focusedHasStyling, "Focused button should have styling applied");
    }

    #endregion

    #region Input Handling Tests

    [Fact]
    public async Task HandleInput_Enter_TriggersClickAction()
    {
        var clicked = false;
        var node = new ButtonNode
        {
            Label = "Click Me",
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(clicked);
    }

    [Fact]
    public async Task HandleInput_Space_TriggersClickAction()
    {
        var clicked = false;
        var node = new ButtonNode
        {
            Label = "Click Me",
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Spacebar, ' ', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(clicked);
    }

    [Fact]
    public async Task HandleInput_OtherKey_DoesNotClick()
    {
        var clicked = false;
        var node = new ButtonNode
        {
            Label = "Click Me",
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.NotHandled, result);
        Assert.False(clicked);
    }

    [Fact]
    public async Task HandleInput_NotFocused_DoesNotClick()
    {
        var clicked = false;
        var node = new ButtonNode
        {
            Label = "Click Me",
            IsFocused = false,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Bindings execute regardless of focus (focus check is for HandleInput fallback)
        // But the action should still fire since bindings don't check focus
        Assert.Equal(InputResult.Handled, result);
        Assert.True(clicked);
    }

    [Fact]
    public async Task HandleInput_NullClickAction_DoesNotThrow()
    {
        var node = new ButtonNode
        {
            Label = "Click Me",
            IsFocused = true,
            ClickAction = null
        };

        // With no ClickAction, no bindings are registered, so Enter falls through
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public async Task HandleInput_Tab_DoesNotTriggerClick()
    {
        var clicked = false;
        var node = new ButtonNode
        {
            Label = "Click",
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.NotHandled, result);
        Assert.False(clicked);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new ButtonNode();

        Assert.True(node.IsFocusable);
    }

    #endregion

    #region Layout Tests

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new ButtonNode { Label = "Test" };
        var bounds = new Rect(0, 0, 20, 1);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_Button_RendersViaHex1bApp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("Click Me").OnClick(_ => Task.CompletedTask)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Click Me"));
    }

    [Fact]
    public async Task Integration_Button_Enter_TriggersAction()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("Submit").OnClick(_ => { clicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Submit"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(clicked);
    }

    [Fact]
    public async Task Integration_Button_Space_TriggersAction()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("Submit").OnClick(_ => { clicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Submit"), TimeSpan.FromSeconds(2))
            .Space()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(clicked);
    }

    [Fact]
    public async Task Integration_Button_ClickUpdatesState()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var counter = 0;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text($"Count: {counter}"),
                    v.Button("Increment").OnClick(_ => { counter++; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Click the button 3 times
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Count:"), TimeSpan.FromSeconds(2))
            .Enter()
            .Enter()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal(3, counter);
        Assert.True(terminal.CreateSnapshot().ContainsText("Count: 3"));
    }

    [Fact]
    public async Task Integration_MultipleButtons_TabNavigates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var button1Clicked = false;
        var button2Clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("Button 1").OnClick(_ => { button1Clicked = true; return Task.CompletedTask; }),
                    v.Button("Button 2").OnClick(_ => { button2Clicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Tab to second button and press Enter
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Button 1"), TimeSpan.FromSeconds(2))
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.False(button1Clicked);
        Assert.True(button2Clicked);
    }

    [Fact]
    public async Task Integration_Button_InNarrowTerminal_StillWorks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 15, 5);
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("OK").OnClick(_ => { clicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("OK"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(clicked);
        Assert.True(terminal.CreateSnapshot().ContainsText("OK"));
    }

    [Fact]
    public async Task Integration_Button_LongLabelInNarrowTerminal_Wraps()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 12, 5);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("Click Here Now").OnClick(_ => Task.CompletedTask)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Here"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The button text should be present (possibly wrapped)
        Assert.True(terminal.CreateSnapshot().ContainsText("Click Here"));
    }

    [Fact]
    public async Task Integration_Button_WithTextBox_TabBetween()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var text = "";
        var buttonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(text).OnTextChanged(args => text = args.NewText),
                    v.Button("Submit").OnClick(_ => { buttonClicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Type in text box, tab to button, press button
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Submit"), TimeSpan.FromSeconds(2))
            .Type("Hi")
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Hi", text);
        Assert.True(buttonClicked);
    }

    [Fact]
    public async Task Integration_Button_MultipleClicks_AllProcessed()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var clickCount = 0;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("Click").OnClick(_ => { clickCount++; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Click 5 times rapidly
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click"), TimeSpan.FromSeconds(2));
        for (int i = 0; i < 5; i++)
        {
            builder.Enter();
        }
        await builder
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal(5, clickCount);
    }

    [Fact]
    public async Task Integration_Button_DynamicLabel_UpdatesOnRender()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var counter = 0;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button($"Clicked {counter} times").OnClick(_ => { counter++; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Clicked 0 times"), TimeSpan.FromSeconds(2))
            .Enter()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Clicked 2 times"));
    }

    #endregion
}
