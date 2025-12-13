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
        
        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Hello")),
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
        
        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Hello World")),
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
        var textState = new TextBoxState { Text = "" };
        
        // Wrap in VStack to get automatic focus
        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBoxWidget(textState)
                })
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Send some keys then complete
        terminal.SendKey(ConsoleKey.H, 'H', shift: true);
        terminal.SendKey(ConsoleKey.I, 'i');
        terminal.CompleteInput();
        
        await app.RunAsync();
        
        Assert.Equal("Hi", textState.Text);
    }

    [Fact]
    public async Task App_HandlesButtonClick()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var clicked = false;
        
        // Wrap in VStack to get automatic focus
        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ButtonWidget("Click Me", () => clicked = true)
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
        
        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Test")),
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
        
        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
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
        var textState1 = new TextBoxState { Text = "" };
        var textState2 = new TextBoxState { Text = "" };
        
        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBoxWidget(textState1),
                    new TextBoxWidget(textState2)
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
        
        Assert.Equal("a", textState1.Text);
        Assert.Equal("b", textState2.Text);
    }

    [Fact]
    public async Task App_ListNavigationWorks()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var listState = new ListState
        {
            Items = new[]
            {
                new ListItem("1", "Item 1"),
                new ListItem("2", "Item 2"),
                new ListItem("3", "Item 3")
            }
        };
        
        // Wrap in VStack to get automatic focus
        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ListWidget(listState)
                })
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Navigate down twice
        terminal.SendKey(ConsoleKey.DownArrow, '\0');
        terminal.SendKey(ConsoleKey.DownArrow, '\0');
        terminal.CompleteInput();
        
        await app.RunAsync();
        
        Assert.Equal(2, listState.SelectedIndex);
    }

    [Fact]
    public async Task App_DynamicStateUpdates()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var counter = 0;
        
        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => 
            {
                var widget = new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget($"Count: {counter}"),
                    new ButtonWidget("Increment", () => counter++)
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
        
        var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(new TextBlockWidget("Test")),
            new Hex1bAppOptions { Terminal = terminal, OwnsTerminal = true }
        );

        terminal.CompleteInput();
        await app.RunAsync();
        
        // Should not throw
        app.Dispose();
    }
}
