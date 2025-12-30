---
name: widget-creator
description: Step-by-step guide for creating new widgets in the Hex1b TUI library. Use when implementing new widgets from scratch, including widget records, nodes, extension methods, theming, reconciliation, and tests.
---

# Widget Creator Skill

This skill provides a comprehensive step-by-step guide for AI coding agents to create new widgets in the Hex1b TUI library. Widgets are the building blocks of Hex1b applications, following a declarative pattern inspired by React.

## Overview

Creating a widget in Hex1b involves several coordinated files:

| File | Purpose |
|------|---------|
| `src/Hex1b/Widgets/{Name}Widget.cs` | Immutable widget record (describes what to render) |
| `src/Hex1b/Nodes/{Name}Node.cs` | Mutable node class (manages state, renders) |
| `src/Hex1b/{Name}Extensions.cs` | Fluent API extension methods |
| `src/Hex1b/Theming/{Name}Theme.cs` | Theme elements (colors, characters) |
| `tests/Hex1b.Tests/{Name}NodeTests.cs` | Unit tests |

## Step-by-Step Process

### Step 1: Define the Widget Record

Widgets are **immutable** `record` types that describe the desired UI. They capture configuration and event handlers but contain no rendering logic.

**Location**: `src/Hex1b/Widgets/{Name}Widget.cs`

**Key principles**:
- Use primary constructor parameters for required data
- Use `internal` properties with `init` for optional configuration
- Event handlers use `Func<TEventArgs, Task>?` pattern
- Provide sync and async overloads for event handlers using `this with { }` pattern
- Implement `Reconcile()` to create/update the corresponding node
- Implement `GetExpectedNodeType()` to return the node type

**Template**:

```csharp
using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Brief description of what the widget does.
/// </summary>
/// <param name="PrimaryProperty">Description of the main property.</param>
public sealed record MyWidget(string PrimaryProperty) : Hex1bWidget
{
    /// <summary>
    /// Optional configuration property.
    /// </summary>
    internal bool SomeOption { get; init; }
    
    /// <summary>
    /// Event handler for some action.
    /// </summary>
    internal Func<MyEventArgs, Task>? ActionHandler { get; init; }

    /// <summary>
    /// Sets a synchronous action handler.
    /// </summary>
    public MyWidget OnAction(Action<MyEventArgs> handler)
        => this with { ActionHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous action handler.
    /// </summary>
    public MyWidget OnAction(Func<MyEventArgs, Task> handler)
        => this with { ActionHandler = handler };

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MyNode ?? new MyNode();
        
        // Mark dirty if properties changed
        if (node.PrimaryProperty != PrimaryProperty || node.SomeOption != SomeOption)
        {
            node.MarkDirty();
        }
        
        node.PrimaryProperty = PrimaryProperty;
        node.SomeOption = SomeOption;
        node.SourceWidget = this;
        
        // Convert typed event handler to internal handler if needed
        if (ActionHandler != null)
        {
            node.ActionCallback = async ctx => 
            {
                var args = new MyEventArgs(this, node, ctx);
                await ActionHandler(args);
            };
        }
        else
        {
            node.ActionCallback = null;
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(MyNode);
}
```

### Step 2: Create Event Args (if needed)

If your widget has event handlers, create a typed event args class.

**Location**: `src/Hex1b/Events/{Name}EventArgs.cs`

**Template**:

```csharp
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for MyWidget actions.
/// </summary>
public sealed class MyEventArgs
{
    /// <summary>
    /// The widget that raised the event.
    /// </summary>
    public MyWidget Widget { get; }
    
    /// <summary>
    /// The node that raised the event.
    /// </summary>
    public MyNode Node { get; }
    
    /// <summary>
    /// The input binding context.
    /// </summary>
    public InputBindingActionContext Context { get; }

    internal MyEventArgs(MyWidget widget, MyNode node, InputBindingActionContext context)
    {
        Widget = widget;
        Node = node;
        Context = context;
    }
}
```

### Step 3: Create the Node Class

Nodes are **mutable** classes that hold render state and perform actual rendering. They receive updates during reconciliation and implement layout and rendering.

**Location**: `src/Hex1b/Nodes/{Name}Node.cs`

**Key principles**:
- Properties are mutable (set from widget during reconciliation)
- Track state that must survive re-renders (focus, cursor position, etc.)
- Call `MarkDirty()` when internal state changes
- Implement `Measure()` to calculate size
- Implement `Render()` to draw to terminal
- Override `IsFocusable` if the widget can receive focus
- Override `ConfigureDefaultBindings()` for keyboard/mouse handling

**Template**:

```csharp
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for MyWidget.
/// </summary>
public sealed class MyNode : Hex1bNode
{
    public string PrimaryProperty { get; set; } = "";
    public bool SomeOption { get; set; }
    
    /// <summary>
    /// The source widget for typed event args.
    /// </summary>
    public MyWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// Callback for the action event.
    /// </summary>
    public Func<InputBindingActionContext, Task>? ActionCallback { get; set; }

    // Focus tracking (if widget is focusable)
    private bool _isFocused;
    public override bool IsFocused 
    { 
        get => _isFocused; 
        set 
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true; // Set to false for non-interactive widgets

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        if (ActionCallback != null)
        {
            bindings.Key(Hex1bKey.Enter).Action(ActionCallback, "Activate");
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // Calculate the desired size
        var width = PrimaryProperty.Length;
        var height = 1;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        // Get theme values
        var fg = theme.Get(MyTheme.ForegroundColor);
        var bg = theme.Get(MyTheme.BackgroundColor);
        
        // Build output string with colors
        var output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{PrimaryProperty}{theme.GetResetToGlobalCodes()}";
        
        // Use clipped rendering when a layout provider is active
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }
}
```

### Step 4: Create Theme Elements

Theme elements allow users to customize the widget's appearance.

**Location**: `src/Hex1b/Theming/{Name}Theme.cs`

**Template**:

```csharp
namespace Hex1b.Theming;

/// <summary>
/// Theme elements for MyWidget.
/// </summary>
public static class MyTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(MyTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(MyTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
    
    // For widgets with multiple states (focused, hovered, etc.)
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor = 
        new($"{nameof(MyTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor = 
        new($"{nameof(MyTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.White);
    
    // For character customization
    public static readonly Hex1bThemeElement<char> SomeCharacter = 
        new($"{nameof(MyTheme)}.{nameof(SomeCharacter)}", () => '█');
}
```

### Step 5: Create Extension Methods

Extension methods provide the fluent API for creating widgets.

**Location**: `src/Hex1b/{Name}Extensions.cs`

**Key principles**:
- Extend `WidgetContext<TParent>` for widgets that can be children
- Provide overloads for common patterns
- Use XML documentation for IntelliSense

**Template**:

```csharp
namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating MyWidget.
/// </summary>
public static class MyExtensions
{
    /// <summary>
    /// Creates a MyWidget with the specified property.
    /// </summary>
    public static MyWidget My<TParent>(
        this WidgetContext<TParent> ctx,
        string primaryProperty)
        where TParent : Hex1bWidget
        => new(primaryProperty);
    
    /// <summary>
    /// Creates a MyWidget with options.
    /// </summary>
    public static MyWidget My<TParent>(
        this WidgetContext<TParent> ctx,
        string primaryProperty,
        bool someOption)
        where TParent : Hex1bWidget
        => new(primaryProperty) { SomeOption = someOption };
}
```

### Step 6: Write Unit Tests

Tests verify that the node behaves correctly.

**Location**: `tests/Hex1b.Tests/{Name}NodeTests.cs`

**Key test scenarios**:
- Measure returns correct size
- Render outputs expected content
- Input handling works correctly
- Property changes mark node dirty
- Focus state changes work

**Template**:

```csharp
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Tests;

public class MyNodeTests
{
    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        // Arrange
        var node = new MyNode { PrimaryProperty = "Hello" };
        var constraints = new Constraints(0, 100, 0, 10);

        // Act
        var size = node.Measure(constraints);

        // Assert
        Assert.Equal(5, size.Width); // "Hello".Length
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void PropertyChange_MarksDirty()
    {
        // Arrange
        var node = new MyNode { PrimaryProperty = "Initial" };
        node.ClearDirty(); // Simulate post-render state

        // Act
        node.PrimaryProperty = "Changed";

        // Assert - if using property setter that marks dirty
        // This depends on whether your node implements dirty tracking in setters
    }

    [Fact]
    public void IsFocused_WhenSet_MarksDirty()
    {
        // Arrange
        var node = new MyNode();
        node.ClearDirty();

        // Act
        node.IsFocused = true;

        // Assert
        Assert.True(node.IsDirty);
    }
}
```

### Step 7: Build and Test

After creating all files:

```bash
# Build the library
dotnet build src/Hex1b

# Run all tests
dotnet test

# Or run specific tests
dotnet test --filter "MyNodeTests"
```

## Common Patterns

### Fill Width (Horizontal Fill)

For widgets that should fill available horizontal space by default:

```csharp
public override Size Measure(Constraints constraints)
{
    // Use all available width
    var width = constraints.MaxWidth;
    var height = 1;
    return constraints.Constrain(new Size(width, height));
}
```

### Animation / Indeterminate State

For widgets with animation (like indeterminate progress), you need to:

1. Track animation state in the node
2. Call `MarkDirty()` when animation changes
3. Use `Hex1bApp.Invalidate()` from the widget builder to trigger re-renders

### Container Widgets

For widgets that contain children, see `VStackWidget`/`VStackNode` as examples:
- Store children in `Hex1bWidget[]` (widget) / `List<Hex1bNode>` (node)
- Use `ReconcileContext.ReconcileChildren()` in widget's `Reconcile()`
- Implement `GetChildren()` in node for focus traversal

### Theming Best Practices

1. **Use `Hex1bColor.Default`** for colors that should inherit from parent
2. **Provide focused/hovered variants** for interactive widgets
3. **Use characters for customizable borders/bullets** so users can theme them

## Testing Best Practices

Testing widgets requires **two layers**: unit tests for isolated node behavior, and integration tests for real-world scenarios using `Hex1bApp`. Integration tests should export evidence in multiple formats for verification and documentation.

### Test File Organization

| File | Purpose |
|------|---------|
| `tests/Hex1b.Tests/{Name}NodeTests.cs` | Unit tests for node behavior |
| `tests/Hex1b.Tests/{Name}IntegrationTests.cs` | Integration tests with Hex1bApp |

### Unit Tests (NodeTests)

Unit tests verify isolated node behavior without running a full app.

**Key scenarios to cover**:

1. **Measure behavior** - Size calculations for various constraints
2. **Render output** - Correct ANSI sequences and text
3. **Property dirty tracking** - Changes trigger re-render
4. **Input handling** - Key/mouse events processed correctly
5. **Focus state** - Focus changes mark dirty and render differently
6. **Reconciliation** - Widget updates preserve/update node state

**Example pattern**:

```csharp
[Fact]
public void Measure_FillsAvailableWidth()
{
    var node = new ProgressNode { Value = 50, Maximum = 100 };
    var size = node.Measure(new Constraints(0, 80, 0, 10));
    Assert.Equal(80, size.Width);
    Assert.Equal(1, size.Height);
}

[Fact]
public void PropertyChange_MarksDirty()
{
    var node = new ProgressNode { Value = 50 };
    node.ClearDirty();
    
    // Simulate reconciliation with changed value
    var widget = new ProgressWidget { Value = 75 };
    widget.Reconcile(node, new ReconcileContext(...));
    
    Assert.True(node.IsDirty);
}
```

### Integration Tests (IntegrationTests)

Integration tests spin up a real `Hex1bApp` and test the widget in various layout scenarios. **These tests must export evidence files** for verification.

**Required export formats**:
- **SVG** - Vector graphics for documentation
- **HTML** - Styled HTML output
- **ANSI** - Raw terminal sequences
- **Asciinema (.cast)** - Animated recordings for dynamic behavior

#### Test Infrastructure Setup

```csharp
using Hex1b;
using Hex1b.Input;
using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;

public class MyWidgetIntegrationTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string GetTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex1b_test_{Guid.NewGuid()}.cast");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { }
        }
    }
}
```

#### Basic Integration Test Pattern

```csharp
[Fact]
public async Task MyWidget_RendersCorrectly()
{
    using var workload = new Hex1bAppWorkloadAdapter();
    using var terminal = new Hex1bTerminal(workload, 60, 10);

    using var app = new Hex1bApp(
        ctx => ctx.VStack(v => [
            v.Text("Label:"),
            v.My("Hello World")
        ]),
        new Hex1bAppOptions { WorkloadAdapter = workload }
    );

    var runTask = app.RunAsync(TestContext.Current.CancellationToken);

    await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Label:"), TimeSpan.FromSeconds(2))
        .Capture("mywidget-basic")  // Exports SVG, HTML, ANSI
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

    await runTask;
}
```

#### Layout Scenario Tests

Test the widget in all common layout containers:

```csharp
[Fact]
public async Task MyWidget_InBorder()
{
    using var workload = new Hex1bAppWorkloadAdapter();
    using var terminal = new Hex1bTerminal(workload, 60, 10);

    using var app = new Hex1bApp(
        ctx => ctx.Border(b => [
            b.Text("Content"),
            b.My("Inside border")
        ], title: "Panel"),
        new Hex1bAppOptions { WorkloadAdapter = workload }
    );

    var runTask = app.RunAsync(TestContext.Current.CancellationToken);

    await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Panel"), TimeSpan.FromSeconds(2))
        .Capture("mywidget-in-border")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

    await runTask;
}

[Fact]
public async Task MyWidget_InHStackWithFill()
{
    using var workload = new Hex1bAppWorkloadAdapter();
    using var terminal = new Hex1bTerminal(workload, 80, 10);

    using var app = new Hex1bApp(
        ctx => ctx.HStack(h => [
            h.Text("Label: "),
            h.My("Content").Fill()
        ]),
        new Hex1bAppOptions { WorkloadAdapter = workload }
    );

    var runTask = app.RunAsync(TestContext.Current.CancellationToken);

    await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Label:"), TimeSpan.FromSeconds(2))
        .Capture("mywidget-hstack-fill")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

    await runTask;
}

[Theory]
[InlineData(40)]
[InlineData(60)]
[InlineData(80)]
[InlineData(120)]
public async Task MyWidget_RespondsToTerminalWidth(int width)
{
    using var workload = new Hex1bAppWorkloadAdapter();
    using var terminal = new Hex1bTerminal(workload, width, 10);

    using var app = new Hex1bApp(
        ctx => ctx.My("Responsive content"),
        new Hex1bAppOptions { WorkloadAdapter = workload }
    );

    var runTask = app.RunAsync(TestContext.Current.CancellationToken);

    await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Responsive"), TimeSpan.FromSeconds(2))
        .Capture($"mywidget-width-{width}")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

    await runTask;
}
```

#### Asciinema Recording Tests

For widgets with animation or dynamic behavior, record asciinema sessions:

```csharp
[Fact]
public async Task MyWidget_RecordsAnimation()
{
    var tempFile = GetTempFile();
    using var workload = new Hex1bAppWorkloadAdapter();
    var terminalOptions = new Hex1bTerminalOptions
    {
        Width = 60,
        Height = 10,
        WorkloadAdapter = workload
    };
    var recorder = terminalOptions.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
    {
        Title = "MyWidget Animation Demo",
        IdleTimeLimit = 0.5f
    });
    using var terminal = new Hex1bTerminal(terminalOptions);

    var animationValue = 0.0;

    using var app = new Hex1bApp(
        ctx => ctx.My($"Value: {animationValue:F1}"),
        new Hex1bAppOptions { WorkloadAdapter = workload }
    );

    using var cts = new CancellationTokenSource();
    var runTask = app.RunAsync(cts.Token);

    recorder.AddMarker("Animation Start");

    // Animate for ~2 seconds
    for (int i = 0; i < 40; i++)
    {
        animationValue = (i % 20) / 20.0;
        app.Invalidate();
        await Task.Delay(50, TestContext.Current.CancellationToken);
    }

    recorder.AddMarker("Animation End");

    var snapshot = terminal.CreateSnapshot();
    TestCaptureHelper.Capture(snapshot, "mywidget-animated");
    await TestCaptureHelper.CaptureCastAsync(recorder, "mywidget-animation", TestContext.Current.CancellationToken);

    cts.Cancel();
    await runTask;
}
```

#### Resize Scenario Tests

Test how widgets respond to terminal resizing:

```csharp
[Fact]
public async Task MyWidget_RecordsResizeScenario()
{
    var tempFile = GetTempFile();
    using var workload = new Hex1bAppWorkloadAdapter();
    var terminalOptions = new Hex1bTerminalOptions
    {
        Width = 100,
        Height = 10,
        WorkloadAdapter = workload
    };
    var recorder = terminalOptions.AddAsciinemaRecorder(tempFile, new AsciinemaRecorderOptions
    {
        Title = "MyWidget Resize Behavior",
        IdleTimeLimit = 1.0f
    });
    using var terminal = new Hex1bTerminal(terminalOptions);

    using var app = new Hex1bApp(
        ctx => ctx.My("Resize me"),
        new Hex1bAppOptions { WorkloadAdapter = workload }
    );

    using var cts = new CancellationTokenSource();
    var runTask = app.RunAsync(cts.Token);

    recorder.AddMarker("Initial Size (100 cols)");

    await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Resize"), TimeSpan.FromSeconds(2))
        .Wait(TimeSpan.FromMilliseconds(500))
        .Build()
        .ApplyAsync(terminal, TestContext.Current.CancellationToken);

    // Resize to medium
    recorder.AddMarker("Resize to 60 cols");
    await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(60, 10, TimeSpan.FromSeconds(1));
    terminal.Resize(60, 10);
    await workload.ResizeAsync(60, 10, TestContext.Current.CancellationToken);
    await Task.Delay(300, TestContext.Current.CancellationToken);

    TestCaptureHelper.Capture(terminal, "mywidget-resize-medium");

    // Resize to narrow
    recorder.AddMarker("Resize to 40 cols");
    await ((IHex1bTerminalWorkloadFilter)recorder).OnResizeAsync(40, 10, TimeSpan.FromSeconds(2));
    terminal.Resize(40, 10);
    await workload.ResizeAsync(40, 10, TestContext.Current.CancellationToken);
    await Task.Delay(300, TestContext.Current.CancellationToken);

    TestCaptureHelper.Capture(terminal, "mywidget-resize-narrow");

    await TestCaptureHelper.CaptureCastAsync(recorder, "mywidget-resize-demo", TestContext.Current.CancellationToken);

    cts.Cancel();
    await runTask;
}
```

#### Theming Tests

Verify custom themes are applied correctly:

```csharp
[Fact]
public async Task MyWidget_RespectsCustomTheme()
{
    using var workload = new Hex1bAppWorkloadAdapter();
    using var terminal = new Hex1bTerminal(workload, 60, 10);

    var customTheme = new Hex1bTheme("CustomTest")
        .Set(MyTheme.ForegroundColor, Hex1bColor.Blue)
        .Set(MyTheme.BackgroundColor, Hex1bColor.Yellow);

    using var app = new Hex1bApp(
        ctx => ctx.My("Themed content"),
        new Hex1bAppOptions 
        { 
            WorkloadAdapter = workload,
            Theme = customTheme
        }
    );

    var runTask = app.RunAsync(TestContext.Current.CancellationToken);

    await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Themed"), TimeSpan.FromSeconds(2))
        .Capture("mywidget-custom-theme")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

    await runTask;
}
```

### Integration Test Checklist

Every widget should have integration tests covering:

- [ ] **Basic rendering** - Widget appears correctly in simple VStack
- [ ] **In Border** - Widget inside a Border container
- [ ] **In HStack** - Widget with label in HStack
- [ ] **Multiple instances** - Several widgets in a VStack
- [ ] **Fixed width** - Widget with `.FixedWidth()` constraint
- [ ] **Fill behavior** - Widget with `.Fill()` in cross-axis container
- [ ] **Various terminal widths** - Theory test with 40, 60, 80, 120 columns
- [ ] **Custom theme** - Widget with custom theme elements
- [ ] **Dynamic updates** - Widget responding to `app.Invalidate()` (if applicable)
- [ ] **Animation recording** - Asciinema recording for animated states (if applicable)
- [ ] **Resize behavior** - Recording of terminal resize handling (if fills space)

### Export Evidence Requirements

All integration tests must generate evidence files via `TestCaptureHelper`:

| Method | Output | Purpose |
|--------|--------|---------|
| `.Capture("name")` | SVG, HTML, ANSI | Static snapshot evidence |
| `TestCaptureHelper.Capture(terminal, "name")` | SVG, HTML, ANSI | Manual capture |
| `TestCaptureHelper.CaptureCastAsync(recorder, "name", ct)` | .cast file | Asciinema recording |

These files are attached to test results and can be viewed in CI artifacts for debugging and documentation purposes.

## Checklist

Before considering a widget complete:

- [ ] Widget record with all configuration properties
- [ ] Event args class (if widget has events)
- [ ] Node class with Measure, Render, and input handling
- [ ] Theme class with customizable elements
- [ ] Extension methods for fluent API
- [ ] **Unit tests** for core node functionality
- [ ] **Integration tests** with exhaustive layout scenarios
- [ ] **Export evidence** (SVG, HTML, ANSI, Asciinema) from integration tests
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes

## Example: ProgressWidget

The ProgressWidget was created following this skill. It demonstrates:

- **Determinate mode**: Shows progress from min to max value
- **Indeterminate mode**: Animated spinner for unknown completion
- **Fill width**: Uses all available horizontal space by default
- **Theming**: Customizable fill and track characters
- **Comprehensive tests**: Unit tests and integration tests with full export evidence

See the implementation files:
- [src/Hex1b/Widgets/ProgressWidget.cs](../../../../src/Hex1b/Widgets/ProgressWidget.cs)
- [src/Hex1b/Nodes/ProgressNode.cs](../../../../src/Hex1b/Nodes/ProgressNode.cs)
- [src/Hex1b/ProgressExtensions.cs](../../../../src/Hex1b/ProgressExtensions.cs)
- [src/Hex1b/Theming/ProgressTheme.cs](../../../../src/Hex1b/Theming/ProgressTheme.cs)
- [tests/Hex1b.Tests/ProgressNodeTests.cs](../../../../tests/Hex1b.Tests/ProgressNodeTests.cs) (unit tests)
- [tests/Hex1b.Tests/ProgressIntegrationTests.cs](../../../../tests/Hex1b.Tests/ProgressIntegrationTests.cs) (integration tests)
