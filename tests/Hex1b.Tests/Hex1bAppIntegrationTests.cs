using System.ComponentModel;
using System.Threading.Tasks;
using Xunit.Sdk;
using Hex1b.Input;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for the full Hex1bApp lifecycle using the virtual terminal.
/// </summary>
public class Hex1bAppIntegrationTests
{
    [Fact]
    public async Task App_EntersAndExitsAlternateScreen()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Hello")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Run app and exit with Ctrl+C
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Test passed - app entered alternate screen and exited cleanly
    }

    [Fact]
    public async Task App_RendersInitialContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Hello World")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        Assert.True(terminal.CreateSnapshot().ContainsText("Hello World"));
    }

    [Fact]
    public async Task App_RespondsToInput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var text = "";
        
        // Wrap in VStack to get automatic focus
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBoxWidget("").OnTextChanged(args => { text = args.NewText; return Task.CompletedTask; })
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Send some keys then exit
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.H, Hex1bModifiers.Shift)
            .Type("i")
            .WaitUntil(s => s.ContainsText("Hi"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        Assert.Equal("Hi", text);
    }

    [Fact]
    public async Task App_HandlesButtonClick()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var clicked = false;
        
        // Wrap in VStack to get automatic focus
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ButtonWidget("Click Me").OnClick(_ => { clicked = true; return Task.CompletedTask; })
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        Assert.True(clicked);
    }

    [Fact]
    public async Task App_HandlesCancellation()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        using var cts = new CancellationTokenSource();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Test")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(cts.Token);
        
        // Cancel after a short delay
        await Task.Delay(50, TestContext.Current.CancellationToken);
        cts.Cancel();
        
        // Should not throw
        await runTask;
        
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
    }

    [Fact]
    public async Task App_RendersVStackLayout()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget("Line 1"),
                    new TextBlockWidget("Line 2"),
                    new TextBlockWidget("Line 3")
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 3"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        Assert.True(terminal.CreateSnapshot().ContainsText("Line 1"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Line 2"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Line 3"));
    }

    [Fact]
    public async Task App_TabNavigatesBetweenWidgets()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var text1 = "";
        var text2 = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBoxWidget("").OnTextChanged(args => { text1 = args.NewText; return Task.CompletedTask; }),
                    new TextBoxWidget("").OnTextChanged(args => { text2 = args.NewText; return Task.CompletedTask; })
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Type in first box, tab to second, type in second
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("a")
            .Tab()
            .Type("b")
            .WaitUntil(s => s.ContainsText("b"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        Assert.Equal("a", text1);
        Assert.Equal("b", text2);
    }

    [Fact]
    public async Task App_ListNavigationWorks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        IReadOnlyList<string> items = [
            "Item 1",
            "Item 2",
            "Item 3"
        ];
        
        // Wrap in VStack to get automatic focus
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ListWidget(items)
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Navigate down twice
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2))
            .Down()
            .Down()
            .WaitUntil(s => s.ContainsText("> Item 3"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Verify via rendered output that third item is selected
        Assert.True(terminal.CreateSnapshot().ContainsText("> Item 3"));
    }

    [Fact]
    public async Task App_DynamicStateUpdates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var counter = 0;
        
        using var app = new Hex1bApp(
            ctx => 
            {
                var widget = new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget($"Count: {counter}"),
                    new ButtonWidget("Increment").OnClick(_ => { counter++; return Task.CompletedTask; })
                });
                return Task.FromResult<Hex1bWidget>(widget);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Click the button twice
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Count:"), TimeSpan.FromSeconds(2))
            .Enter()
            .Enter()
            .WaitUntil(s => s.ContainsText("Count: 2"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        Assert.Equal(2, counter);
        // The last render should show the updated count
        Assert.True(terminal.CreateSnapshot().ContainsText("Count: 2"));
    }

    [Fact]
    public async Task App_Dispose_CleansUp()
    {
        var workload = new Hex1bAppWorkloadAdapter();

        var terminal = new Hex1bTerminal(workload, 80, 24);
        
        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Test")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Should not throw
        app.Dispose();
    }

    [Fact]
    public async Task App_Invalidate_TriggersRerender()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var counter = 0;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget($"Count: {counter}")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        // Wait for initial render
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.True(terminal.CreateSnapshot().ContainsText("Count: 0"));
        
        // Change state externally and invalidate
        counter = 42;
        app.Invalidate();
        
        // Wait for re-render
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.True(terminal.CreateSnapshot().ContainsText("Count: 42"));
        
        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task App_InvalidateMultipleTimes_CoalescesRerenders()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var renderCount = 0;
        
        using var app = new Hex1bApp(
            ctx => 
            {
                renderCount++;
                return Task.FromResult<Hex1bWidget>(new TextBlockWidget($"Render: {renderCount}"));
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - Wait for initial render using proper synchronization
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Render:"), TimeSpan.FromSeconds(2), "initial render")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        var initialRenderCount = renderCount;
        
        // Rapid-fire multiple invalidations
        for (int i = 0; i < 100; i++)
        {
            app.Invalidate();
        }
        
        // Wait for processing - use a small delay then check final count
        // The key insight is that we should see FAR fewer than 100 extra renders
        await Task.Delay(200, TestContext.Current.CancellationToken);
        
        // Should have coalesced - not 100 extra renders
        // Allow up to 20 extra renders to account for timing variations in CI
        Assert.True(renderCount < initialRenderCount + 20, 
            $"Expected coalesced renders, but got {renderCount - initialRenderCount} extra renders");
        
        // Exit the app
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task App_DefaultCtrlCExit_ExitsApp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var renderTest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx =>
            {
                var test = ctx.Test();
                test.OnRender(_ => renderTest.TrySetResult());
                return Task.FromResult<Hex1bWidget>(test);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await renderTest.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        // Send CTRL-C after the first render to exercise the default binding
        new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.C, Hex1bModifiers.Control)
            .Build()
            .Apply(terminal);

        var completed = await Task.WhenAny(runTask, Task.Delay(2000, TestContext.Current.CancellationToken));
        Assert.True(completed == runTask, "Expected CTRL-C to exit the application after the initial render.");

        await runTask;

        // App should have exited gracefully
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
    }

    [Fact]
    public async Task App_DefaultCtrlCExitDisabled_DoesNotExit()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var ctrlCPressed = false;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ButtonWidget("Test").OnClick(_ => { ctrlCPressed = true; return Task.CompletedTask; })
                }).WithInputBindings(bindings =>
                {
                    bindings.Ctrl().Key(Hex1bKey.C).Action(_ => ctrlCPressed = true);
                })
            ),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload,
                EnableDefaultCtrlCExit = false
            }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        // Wait for initial render
        await Task.Delay(50, TestContext.Current.CancellationToken);
        
        // Send CTRL-C
        new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.C, Hex1bModifiers.Control)
            .Build()
            .Apply(terminal);
        
        // Wait for processing
        await Task.Delay(50, TestContext.Current.CancellationToken);
        
        // The custom binding should have been called
        Assert.True(ctrlCPressed);
        
        // App should still be running (not exited)
        Assert.False(runTask.IsCompleted);
        
        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task App_UserCtrlCBinding_OverridesDefault()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var customHandlerCalled = false;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ButtonWidget("Test").OnClick(_ => Task.CompletedTask)
                }).WithInputBindings(bindings =>
                {
                    // User's CTRL-C binding should override the default
                    bindings.Ctrl().Key(Hex1bKey.C).Action(_ => customHandlerCalled = true);
                })
            ),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload,
                EnableDefaultCtrlCExit = true  // Default is enabled
            }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        // Wait for initial render
        await Task.Delay(50, TestContext.Current.CancellationToken);
        
        // Send CTRL-C
        new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.C, Hex1bModifiers.Control)
            .Build()
            .Apply(terminal);
        
        // Wait for processing
        await Task.Delay(50, TestContext.Current.CancellationToken);
        
        // The custom handler should have been called
        Assert.True(customHandlerCalled);
        
        // App should still be running (not exited by default binding)
        Assert.False(runTask.IsCompleted);
        
        cts.Cancel();
        await runTask;
    }

}
