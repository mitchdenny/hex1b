---
title: Testing
---

# Testing Hex1b Applications

Hex1b provides first-class testing support through its virtual terminal and input sequence builder. You can write automated tests that simulate user interactions and verify your application's behavior.

::: info Framework Agnostic
Hex1b's testing APIs work with any .NET testing framework. This guide uses **xUnit** as an example, but the same patterns apply to NUnit, MSTest, or any other framework.
:::

## Overview

Testing a Hex1b app involves:

1. **Hex1bTerminal** - A virtual terminal that captures screen output
2. **Hex1bInputSequenceBuilder** - A fluent API to simulate user input
3. **Your test framework** - To run tests and make assertions

```csharp
// The pattern
using var terminal = new Hex1bTerminal(80, 24);
using var app = new Hex1bApp(/* ... */);

var sequence = new Hex1bInputSequenceBuilder()
    .Type("Hello")
    .Enter()
    .Build();

await sequence.ApplyAsync(terminal);

Assert.Contains("Hello", terminal.GetScreenText());
```

## A Sample Application

Let's create a simple counter app to test:

```csharp
// CounterApp.cs
using Hex1b;
using Hex1b.Widgets;

public static class CounterApp
{
    public static Hex1bWidget Build(WidgetContext ctx)
    {
        var count = ctx.UseState(0);
        
        return ctx.VStack(v => [
            v.Text($"Count: {count.Value}"),
            v.HStack(h => [
                h.Button("Increment")
                    .OnClick(_ => { count.Value++; return Task.CompletedTask; }),
                h.Button("Decrement")
                    .OnClick(_ => { count.Value--; return Task.CompletedTask; }),
                h.Button("Reset")
                    .OnClick(_ => { count.Value = 0; return Task.CompletedTask; })
            ])
        ]);
    }
}
```

## Setting Up the Test Project

Add the Hex1b package to your test project:

```xml
<ItemGroup>
  <PackageReference Include="Hex1b" Version="*" />
  <PackageReference Include="xunit" Version="2.9.0" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
</ItemGroup>
```

## Writing Your First Test

```csharp
using Hex1b;
using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;
using Xunit;

public class CounterAppTests
{
    [Fact]
    public async Task InitialState_ShowsZero()
    {
        // Arrange
        using var terminal = new Hex1bTerminal(80, 24);
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(CounterApp.Build(ctx)),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );

        // Act - start app and exit with Ctrl+C
        var runTask = app.RunAsync();
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Count:"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        // Assert
        Assert.Contains("Count: 0", terminal.GetScreenText());
    }
}
```

### Key Components

| Component | Purpose |
|-----------|---------|
| `Hex1bTerminal(width, height)` | Creates a virtual terminal for headless testing |
| `terminal.WorkloadAdapter` | Connect the app to the virtual terminal |
| `Hex1bTerminalInputSequenceBuilder` | Build input sequences with waits and assertions |
| `terminal.GetScreenText()` | Get the current screen content as text (auto-flushes) |

## Testing User Interactions

Use `Hex1bInputSequenceBuilder` to simulate user input:

```csharp
[Fact]
public async Task ClickIncrement_IncreasesCount()
{
    using var terminal = new Hex1bTerminal(80, 24);
    using var app = new Hex1bApp(
        ctx => Task.FromResult<Hex1bWidget>(CounterApp.Build(ctx)),
        new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
    );

    // Build the input sequence
    var sequence = new Hex1bInputSequenceBuilder()
        .Enter()  // Click the focused "Increment" button
        .Enter()  // Click again
        .Enter()  // And again
        .Build();

    using var cts = new CancellationTokenSource();
    var runTask = app.RunAsync(cts.Token);

    // Wait for initial render
    await Task.Delay(50);
    
    // Apply the input sequence
    await sequence.ApplyAsync(terminal);
    await Task.Delay(50);

    // Stop the app
    cts.Cancel();
    await runTask;

    // Assert
    Assert.Contains("Count: 3", terminal.GetScreenText());
}
```

## Input Sequence Builder API

### Keyboard Input

```csharp
var sequence = new Hex1bInputSequenceBuilder()
    // Basic keys
    .Key(Hex1bKey.A)              // Press 'a'
    .Enter()                       // Press Enter
    .Tab()                         // Press Tab
    .Escape()                      // Press Escape
    .Backspace()                   // Press Backspace
    .Delete()                      // Press Delete
    .Space()                       // Press Space
    
    // Arrow keys
    .Up()
    .Down()
    .Left()
    .Right()
    
    // Navigation
    .Home()
    .End()
    .PageUp()
    .PageDown()
    
    // Modifiers
    .Shift().Key(Hex1bKey.A)      // Shift+A (uppercase)
    .Ctrl().Key(Hex1bKey.C)       // Ctrl+C
    .Alt().Key(Hex1bKey.F)        // Alt+F
    .Ctrl().Shift().Key(Hex1bKey.Z) // Ctrl+Shift+Z
    
    // Text typing
    .Type("Hello World")          // Type text quickly
    .FastType("Quick")            // Same as Type()
    .SlowType("Slow")             // With delays between keys
    .SlowType("Custom", TimeSpan.FromMilliseconds(100))
    
    .Build();
```

### Mouse Input

```csharp
var sequence = new Hex1bInputSequenceBuilder()
    // Positioning
    .MouseMoveTo(10, 5)           // Move to absolute position
    .MouseMove(5, 0)              // Move relative (delta)
    
    // Clicks
    .Click()                       // Left click at current position
    .Click(MouseButton.Right)      // Right click
    .ClickAt(20, 10)              // Move and click
    .DoubleClick()                 // Double click
    
    // Drag
    .Drag(10, 10, 30, 10)         // Drag from (10,10) to (30,10)
    
    // Scroll
    .ScrollUp()                    // Scroll up once
    .ScrollUp(3)                   // Scroll up 3 ticks
    .ScrollDown(2)                 // Scroll down 2 ticks
    
    // Modifiers with mouse
    .Ctrl().Click()               // Ctrl+Click
    .Shift().MouseDown()          // Shift+MouseDown
    
    .Build();
```

### Timing

```csharp
var sequence = new Hex1bInputSequenceBuilder()
    .Type("search query")
    .Wait(100)                     // Wait 100ms
    .Wait(TimeSpan.FromSeconds(1)) // Wait 1 second
    .Enter()
    .Build();
```

## Testing Keyboard Navigation

```csharp
[Fact]
public async Task TabNavigation_MovesBetweenButtons()
{
    using var terminal = new Hex1bTerminal(80, 24);
    using var app = new Hex1bApp(
        ctx => Task.FromResult<Hex1bWidget>(CounterApp.Build(ctx)),
        new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
    );

    var sequence = new Hex1bInputSequenceBuilder()
        .Tab()        // Move to "Decrement" button
        .Tab()        // Move to "Reset" button
        .Enter()      // Click Reset (no effect, count is already 0)
        .Shift().Tab() // Back to "Decrement"
        .Enter()      // Click Decrement
        .Build();

    using var cts = new CancellationTokenSource();
    var runTask = app.RunAsync(cts.Token);

    await Task.Delay(50);
    await sequence.ApplyAsync(terminal);
    await Task.Delay(50);

    cts.Cancel();
    await runTask;

    Assert.Contains("Count: -1", terminal.GetScreenText());
}
```

## Testing Text Input

```csharp
[Fact]
public async Task TextBox_CapturesInput()
{
    using var terminal = new Hex1bTerminal(80, 24);
    var capturedText = "";

    using var app = new Hex1bApp(
        ctx => Task.FromResult<Hex1bWidget>(
            ctx.VStack(v => [
                v.TextBox()
                    .OnTextChanged(args => 
                    { 
                        capturedText = args.NewText; 
                        return Task.CompletedTask; 
                    })
            ])
        ),
        new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
    );

    var sequence = new Hex1bInputSequenceBuilder()
        .Type("Hello, Hex1b!")
        .Build();

    using var cts = new CancellationTokenSource();
    var runTask = app.RunAsync(cts.Token);

    await Task.Delay(50);
    await sequence.ApplyAsync(terminal);
    await Task.Delay(50);

    cts.Cancel();
    await runTask;

    Assert.Equal("Hello, Hex1b!", capturedText);
}
```

## Testing Keyboard Shortcuts

```csharp
[Fact]
public async Task CtrlS_TriggersSave()
{
    using var terminal = new Hex1bTerminal(80, 24);
    var saveTriggered = false;

    using var app = new Hex1bApp(
        ctx => Task.FromResult<Hex1bWidget>(
            ctx.VStack(v => [
                v.Text("Press Ctrl+S to save")
            ]).WithInputBindings(bindings =>
            {
                bindings.Ctrl().Key(Hex1bKey.S)
                    .Action(_ => saveTriggered = true);
            })
        ),
        new Hex1bAppOptions 
        { 
            WorkloadAdapter = terminal.WorkloadAdapter,
            EnableDefaultCtrlCExit = false
        }
    );

    var sequence = new Hex1bInputSequenceBuilder()
        .Ctrl().Key(Hex1bKey.S)
        .Build();

    using var cts = new CancellationTokenSource();
    var runTask = app.RunAsync(cts.Token);

    await Task.Delay(50);
    await sequence.ApplyAsync(terminal);
    await Task.Delay(50);

    cts.Cancel();
    await runTask;

    Assert.True(saveTriggered);
}
```

## Terminal Inspection APIs

The virtual terminal provides several ways to inspect the screen:

```csharp
// Text content
terminal.GetScreenText()         // Full screen as string
terminal.GetLine(0)              // Specific line (0-based)
terminal.GetLineTrimmed(0)       // Line without trailing spaces
terminal.GetNonEmptyLines()      // All non-blank lines

// Searching
terminal.ContainsText("Hello")   // Boolean check
terminal.FindText("Error")       // List of (line, column) positions

// Screen buffer (with colors)
terminal.GetScreenBuffer()       // 2D array of TerminalCell

// Cursor position
terminal.CursorX                 // Current X position
terminal.CursorY                 // Current Y position

// Terminal state
terminal.InAlternateScreen       // Whether in alternate screen mode
terminal.Width                   // Terminal width
terminal.Height                  // Terminal height
```

## Best Practices

### 1. Use Descriptive Sequences

```csharp
// ❌ Hard to understand
var sequence = new Hex1bInputSequenceBuilder()
    .Tab().Tab().Enter().Type("x").Tab().Enter()
    .Build();

// ✅ Clear intent
var sequence = new Hex1bInputSequenceBuilder()
    .Tab()                    // Navigate to username field
    .Tab()                    // Navigate to password field
    .Enter()                  // Focus the password input
    .Type("secret123")        // Enter password
    .Tab()                    // Navigate to login button
    .Enter()                  // Click login
    .Build();
```

### 2. Wait for Renders

Allow time for the app to process and render:

```csharp
await Task.Delay(50);  // After starting app
await sequence.ApplyAsync(terminal);
await Task.Delay(50);  // After input, before assertions
// Screen buffer reads (GetScreenText, ContainsText, etc.) auto-flush
```

### 3. Use CancellationToken for Cleanup

```csharp
using var cts = new CancellationTokenSource();
var runTask = app.RunAsync(cts.Token);

// ... test logic ...

cts.Cancel();
await runTask;  // Ensure clean shutdown
```

### 4. Test One Behavior at a Time

```csharp
// ✅ Focused test
[Fact]
public async Task Increment_WhenClicked_IncreasesCountByOne()

// ❌ Too broad
[Fact]
public async Task Counter_WorksCorrectly()
```

### 5. Consider SlowType for Timing-Sensitive Tests

```csharp
// For debounced search, autocomplete, etc.
var sequence = new Hex1bInputSequenceBuilder()
    .SlowType("search query", TimeSpan.FromMilliseconds(50))
    .Wait(200)  // Wait for debounce
    .Build();
```

## Complete Example

Here's a full test class for the counter app:

```csharp
using Hex1b;
using Hex1b.Input;
using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;
using Xunit;

public class CounterAppTests
{
    private static Hex1bApp CreateApp(Hex1bTerminal terminal)
    {
        return new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(CounterApp.Build(ctx)),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );
    }

    [Fact]
    public async Task InitialRender_DisplaysZero()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        using var app = CreateApp(terminal);

        var runTask = app.RunAsync();
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Count:"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.Contains("Count: 0", terminal.GetScreenText());
    }

    [Fact]
    public async Task Increment_IncreasesCount()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        using var app = CreateApp(terminal);

        var sequence = new Hex1bInputSequenceBuilder()
            .Enter()
            .Build();

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await Task.Delay(50);
        await sequence.ApplyAsync(terminal);
        await Task.Delay(50);

        cts.Cancel();
        await runTask;

        Assert.Contains("Count: 1", terminal.GetScreenText());
    }

    [Fact]
    public async Task Decrement_DecreasesCount()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        using var app = CreateApp(terminal);

        var sequence = new Hex1bInputSequenceBuilder()
            .Tab()      // Move to Decrement button
            .Enter()    // Click it
            .Build();

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await Task.Delay(50);
        await sequence.ApplyAsync(terminal);
        await Task.Delay(50);

        cts.Cancel();
        await runTask;

        Assert.Contains("Count: -1", terminal.GetScreenText());
    }

    [Fact]
    public async Task Reset_SetsCountToZero()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        using var app = CreateApp(terminal);

        var sequence = new Hex1bInputSequenceBuilder()
            .Enter()            // Increment to 1
            .Enter()            // Increment to 2
            .Tab().Tab()        // Move to Reset button
            .Enter()            // Click Reset
            .Build();

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await Task.Delay(50);
        await sequence.ApplyAsync(terminal);
        await Task.Delay(50);

        cts.Cancel();
        await runTask;

        Assert.Contains("Count: 0", terminal.GetScreenText());
    }
}
```

## Next Steps

- [Input Handling](/guide/input) - Learn about keyboard bindings and focus
- [Widgets & Nodes](/guide/widgets-and-nodes) - Understand the rendering model
- [Layout System](/guide/layout) - Master the constraint-based layout
