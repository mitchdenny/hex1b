using System.ComponentModel;
using Hex1b.Input;
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
        using var terminal = new Hex1bTerminal(80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Hello")),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Complete input immediately to end the app
        terminal.CompleteInput();
        
        await app.RunAsync();
        
        // Should have exited alternate screen
        Assert.False(terminal.InAlternateScreen);
    }

    [Fact]
    public async Task App_RendersInitialContent()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Hello World")),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();
        
        Assert.Contains("Hello World", terminal.RawOutput);
    }

    [Fact]
    public async Task App_RespondsToInput()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var text = "";
        
        // Wrap in VStack to get automatic focus
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBoxWidget("") { OnTextChanged = args => { text = args.NewText; return Task.CompletedTask; } }
                })
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Send some keys then complete
        terminal.SendKey(ConsoleKey.H, 'H', shift: true);
        terminal.SendKey(ConsoleKey.I, 'i');
        terminal.CompleteInput();
        
        await app.RunAsync();
        
        Assert.Equal("Hi", text);
    }

    [Fact]
    public async Task App_HandlesButtonClick()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var clicked = false;
        
        // Wrap in VStack to get automatic focus
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ButtonWidget("Click Me") { OnClick = _ => { clicked = true; return Task.CompletedTask; } }
                })
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();
        
        await app.RunAsync();
        
        Assert.True(clicked);
    }

    [Fact]
    public async Task App_HandlesCancellation()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        using var cts = new CancellationTokenSource();
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Test")),
            new Hex1bAppOptions { Terminal = terminal }
        );

        var runTask = app.RunAsync(cts.Token);
        
        // Cancel after a short delay
        await Task.Delay(50);
        cts.Cancel();
        
        // Should not throw
        await runTask;
        
        Assert.False(terminal.InAlternateScreen);
    }

    [Fact]
    public async Task App_RendersVStackLayout()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget("Line 1"),
                    new TextBlockWidget("Line 2"),
                    new TextBlockWidget("Line 3")
                })
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();
        
        Assert.Contains("Line 1", terminal.RawOutput);
        Assert.Contains("Line 2", terminal.RawOutput);
        Assert.Contains("Line 3", terminal.RawOutput);
    }

    [Fact]
    public async Task App_TabNavigatesBetweenWidgets()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var text1 = "";
        var text2 = "";
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBoxWidget("") { OnTextChanged = args => { text1 = args.NewText; return Task.CompletedTask; } },
                    new TextBoxWidget("") { OnTextChanged = args => { text2 = args.NewText; return Task.CompletedTask; } }
                })
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Type in first box
        terminal.SendKey(ConsoleKey.A, 'a');
        // Tab to second box
        terminal.SendKey(ConsoleKey.Tab, '\t');
        // Type in second box
        terminal.SendKey(ConsoleKey.B, 'b');
        terminal.CompleteInput();
        
        await app.RunAsync();
        
        Assert.Equal("a", text1);
        Assert.Equal("b", text2);
    }

    [Fact]
    public async Task App_ListNavigationWorks()
    {
        using var terminal = new Hex1bTerminal(80, 24);
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
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Navigate down twice
        terminal.SendKey(ConsoleKey.DownArrow, '\0');
        terminal.SendKey(ConsoleKey.DownArrow, '\0');
        terminal.CompleteInput();
        
        await app.RunAsync();
        
        // Verify via rendered output that third item is selected
        Assert.Contains("> Item 3", terminal.RawOutput);
    }

    [Fact]
    public async Task App_DynamicStateUpdates()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var counter = 0;
        
        using var app = new Hex1bApp(
            ctx => 
            {
                var widget = new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget($"Count: {counter}"),
                    new ButtonWidget("Increment") { OnClick = _ => { counter++; return Task.CompletedTask; } }
                });
                return Task.FromResult<Hex1bWidget>(widget);
            },
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Click the button twice
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();
        
        await app.RunAsync();
        
        Assert.Equal(2, counter);
        // The last render should show the updated count
        Assert.Contains("Count: 2", terminal.RawOutput);
    }

    [Fact]
    public async Task App_Dispose_CleansUp()
    {
        var terminal = new Hex1bTerminal(80, 24);
        
        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Test")),
            new Hex1bAppOptions { Terminal = terminal, OwnsTerminal = true }
        );

        terminal.CompleteInput();
        await app.RunAsync();
        
        // Should not throw
        app.Dispose();
    }

    [Fact]
    public async Task App_Invalidate_TriggersRerender()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var counter = 0;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget($"Count: {counter}")),
            new Hex1bAppOptions { Terminal = terminal }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        // Wait for initial render
        await Task.Delay(50);
        Assert.Contains("Count: 0", terminal.RawOutput);
        
        // Change state externally and invalidate
        counter = 42;
        terminal.ClearRawOutput();
        app.Invalidate();
        
        // Wait for re-render
        await Task.Delay(50);
        Assert.Contains("Count: 42", terminal.RawOutput);
        
        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task App_InvalidateMultipleTimes_CoalescesRerenders()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var renderCount = 0;
        
        using var app = new Hex1bApp(
            ctx => 
            {
                renderCount++;
                return Task.FromResult<Hex1bWidget>(new TextBlockWidget($"Render: {renderCount}"));
            },
            new Hex1bAppOptions { Terminal = terminal }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        // Wait for initial render
        await Task.Delay(50);
        var initialRenderCount = renderCount;
        
        // Rapid-fire multiple invalidations
        for (int i = 0; i < 100; i++)
        {
            app.Invalidate();
        }
        
        // Wait for processing
        await Task.Delay(100);
        
        // Should have coalesced - not 100 extra renders
        // At most a few extra renders (bounded channel with size 1 drops excess)
        Assert.True(renderCount < initialRenderCount + 10, 
            $"Expected coalesced renders, but got {renderCount - initialRenderCount} extra renders");
        
        cts.Cancel();
        await runTask;
    }

    [Fact]
    public async Task App_DefaultCtrlCExit_ExitsApp()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Test")),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Send CTRL-C
        terminal.SendKey(ConsoleKey.C, '\x03', control: true);
        terminal.CompleteInput();
        
        await app.RunAsync();
        
        // App should have exited gracefully
        Assert.False(terminal.InAlternateScreen);
    }

    [Fact]
    public async Task App_DefaultCtrlCExitDisabled_DoesNotExit()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var ctrlCPressed = false;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ButtonWidget("Test") { OnClick = _ => { ctrlCPressed = true; return Task.CompletedTask; } }
                }).WithInputBindings(bindings =>
                {
                    bindings.Ctrl().Key(Hex1bKey.C).Action(_ => ctrlCPressed = true);
                })
            ),
            new Hex1bAppOptions 
            { 
                Terminal = terminal,
                EnableDefaultCtrlCExit = false
            }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        // Wait for initial render
        await Task.Delay(50);
        
        // Send CTRL-C
        terminal.SendKey(ConsoleKey.C, '\x03', control: true);
        
        // Wait for processing
        await Task.Delay(50);
        
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
        using var terminal = new Hex1bTerminal(80, 24);
        var customHandlerCalled = false;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ButtonWidget("Test") { OnClick = _ => Task.CompletedTask }
                }).WithInputBindings(bindings =>
                {
                    // User's CTRL-C binding should override the default
                    bindings.Ctrl().Key(Hex1bKey.C).Action(_ => customHandlerCalled = true);
                })
            ),
            new Hex1bAppOptions 
            { 
                Terminal = terminal,
                EnableDefaultCtrlCExit = true  // Default is enabled
            }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        // Wait for initial render
        await Task.Delay(50);
        
        // Send CTRL-C
        terminal.SendKey(ConsoleKey.C, '\x03', control: true);
        
        // Wait for processing
        await Task.Delay(50);
        
        // The custom handler should have been called
        Assert.True(customHandlerCalled);
        
        // App should still be running (not exited by default binding)
        Assert.False(runTask.IsCompleted);
        
        cts.Cancel();
        await runTask;
    }

}
