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
2. **Hex1bTerminalInputSequenceBuilder** - A fluent API to build and run input sequences
3. **Hex1bTerminalAutomator** - An imperative async API for complex tests with rich error diagnostics
4. **Your test framework** - To run tests and make assertions

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

## Imperative Testing with Hex1bTerminalAutomator

For complex integration tests, the `Hex1bTerminalAutomator` provides an imperative, async API that executes each step immediately. When a step fails, the exception includes a full breadcrumb trail of completed steps with timings and a terminal snapshot — making failures much easier to diagnose.

### When to Use the Automator

| Approach | Best For |
|----------|----------|
| `Hex1bTerminalInputSequenceBuilder` | Short, self-contained sequences (5-10 steps) |
| `Hex1bTerminalAutomator` | Long integration tests, multi-step workflows, tests where debugging failures matters |

### Basic Usage

```csharp
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

[Fact]
public async Task MenuItem_NavigatesToNextItem()
{
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) => ctx => CreateMenuBar(ctx))
        .WithHeadless()
        .WithDimensions(80, 24)
        .Build();

    var runTask = terminal.RunAsync(TestContext.Current.CancellationToken);
    var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));

    // Each line executes and completes before the next
    await auto.WaitUntilTextAsync("File");       // step 1
    await auto.EnterAsync();                      // step 2 - open menu
    await auto.WaitUntilTextAsync("New");          // step 3
    await auto.DownAsync();                        // step 4 - navigate to Open
    await auto.WaitUntilAsync(                     // step 5
        s => s.ContainsText("▶ Open"),
        description: "Open to be selected");
    await auto.EnterAsync();                       // step 6 - activate

    await auto.Ctrl().KeyAsync(Hex1bKey.C);
    await runTask;
}
```

### Rich Error Diagnostics

When a step fails, `Hex1bAutomationException` includes everything you need to diagnose the failure:

```
Hex1b.Automation.Hex1bAutomationException: Step 5 of 5 failed — WaitUntil timed out after 00:00:05
  Condition: Open to be selected
  at MenuBarTests.cs:42

Completed steps (4 of 5):
  [1] WaitUntilText("File")           — 120ms   ✓
  [2] Key(Enter)                       — 0ms     ✓
  [3] WaitUntilText("New")             — 340ms   ✓
  [4] Key(DownArrow)                   — 0ms     ✓
  [5] WaitUntil("Open to be selected") — FAILED after 5,000ms

Total elapsed: 5,460ms

Terminal snapshot at failure (80x24, cursor at 5,3, alternate screen):
┌──────────────────────────────────────────────────────────────────────────────┐
│ File  Edit  View  Help                                                     │
│┌─────────┐                                                                 │
││ New     │                                                                 │
││ Open    │                                                                 │
│└─────────┘                                                                 │
└──────────────────────────────────────────────────────────────────────────────┘
```

The exception also exposes structured properties for programmatic inspection:

```csharp
catch (Hex1bAutomationException ex)
{
    ex.FailedStepIndex        // 1-based index of the failing step
    ex.FailedStepDescription  // e.g., "WaitUntilText(\"File\")"
    ex.CompletedSteps         // IReadOnlyList<AutomationStepRecord>
    ex.TotalElapsed           // Total time across all steps
    ex.TerminalSnapshot       // Terminal state at failure
    ex.CallerFilePath         // Source file where the step was called
    ex.CallerLineNumber       // Line number where the step was called
    ex.InnerException         // Original exception (e.g., WaitUntilTimeoutException)
}
```

### Automator API

#### Waiting

```csharp
// Wait for a condition
await auto.WaitUntilAsync(s => s.ContainsText("Ready"), description: "app to be ready");

// Convenience methods
await auto.WaitUntilTextAsync("Hello");        // Wait for text to appear
await auto.WaitUntilNoTextAsync("Loading");    // Wait for text to disappear
await auto.WaitUntilAlternateScreenAsync();    // Wait for alternate screen

// Custom timeout (overrides the default)
await auto.WaitUntilTextAsync("Slow result", timeout: TimeSpan.FromSeconds(30));
```

#### Keyboard

```csharp
// Individual keys
await auto.EnterAsync();
await auto.TabAsync();
await auto.EscapeAsync();
await auto.SpaceAsync();
await auto.BackspaceAsync();
await auto.DeleteAsync();

// Arrow keys
await auto.UpAsync();
await auto.DownAsync();
await auto.LeftAsync();
await auto.RightAsync();

// Navigation
await auto.HomeAsync();
await auto.EndAsync();
await auto.PageUpAsync();
await auto.PageDownAsync();

// Any key
await auto.KeyAsync(Hex1bKey.F1);

// Modifiers (consumed by the next key call)
await auto.Ctrl().KeyAsync(Hex1bKey.S);           // Ctrl+S
await auto.Shift().TabAsync();                     // Shift+Tab
await auto.Ctrl().Shift().KeyAsync(Hex1bKey.Z);   // Ctrl+Shift+Z

// Typing
await auto.TypeAsync("Hello World");               // Fast type
await auto.SlowTypeAsync("search", delay: TimeSpan.FromMilliseconds(50));
```

#### Mouse

```csharp
await auto.ClickAtAsync(10, 5);
await auto.DoubleClickAtAsync(10, 5);
await auto.MouseMoveToAsync(20, 10);
await auto.DragAsync(10, 10, 30, 10);
await auto.ScrollUpAsync(3);
await auto.ScrollDownAsync();
```

#### Timing and Snapshots

```csharp
// Pause between steps
await auto.WaitAsync(100);                          // 100ms
await auto.WaitAsync(TimeSpan.FromSeconds(1));      // 1 second

// Inspect the terminal at any point
using var snapshot = auto.CreateSnapshot();

// Review completed steps
foreach (var step in auto.CompletedSteps)
{
    Console.WriteLine($"[{step.Index}] {step.Description} — {step.Elapsed.TotalMilliseconds}ms");
}
```

#### Composability with SequenceAsync

You can run a pre-built sequence or inline builder through the automator. The sequence is tracked as a single step in the automator's history:

```csharp
// Inline builder
await auto.SequenceAsync(b => b
    .Type("aspire new")
    .Enter(),
    description: "Run aspire new command");

// Pre-built sequence
var openMenu = new Hex1bTerminalInputSequenceBuilder()
    .Enter()
    .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(5))
    .Build();

await auto.SequenceAsync(openMenu, description: "Open file menu");
```

### Building Extension Methods

The automator is designed for domain-specific extension methods. This is how you build reusable test helpers for your application:

```csharp
public static class MyAppAutomatorExtensions
{
    public static async Task LoginAsync(
        this Hex1bTerminalAutomator auto, string username, string password)
    {
        await auto.WaitUntilTextAsync("Username:");
        await auto.TypeAsync(username);
        await auto.TabAsync();
        await auto.TypeAsync(password);
        await auto.EnterAsync();
        await auto.WaitUntilTextAsync("Welcome");
    }
}

// Usage in tests:
await auto.LoginAsync("admin", "secret");
await auto.WaitUntilTextAsync("Dashboard");
```

Each call inside the extension method is individually tracked in the step history, so if `LoginAsync` fails at the password tab, you'll see exactly which step timed out.

## Complete Example

Here's a full test class for the counter app:

```csharp
using Hex1b;
using Hex1b.Input;
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
- [Terminal Emulator](/guide/terminal-emulator#docker-container-integration) - Test inside Docker containers
