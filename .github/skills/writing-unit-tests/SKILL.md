---
name: writing-unit-tests
description: Guidelines for writing unit tests in the Hex1b TUI library. Use when creating new tests for widgets, nodes, or terminal functionality.
---

# Writing Unit Tests Skill

This skill provides guidelines for AI agents writing unit tests for the Hex1b TUI library. It outlines the preferred testing approach, patterns, and anti-patterns to avoid.

## Core Philosophy

1. **Prefer full terminal stack testing** - Use `Hex1bTerminal.CreateBuilder()` to create complete terminal environments
2. **Use `.WithHex1bApp()`** for TUI functionality tests - This wires up the full app lifecycle
3. **Keep tests simple and linear** - Avoid excessive abstractions; repeating patterns are beneficial for AI agents
4. **Assert on visual behavior** - Use `CellPatternSearcher` and color assertions for render verification
5. **Update this skill** when discovering new patterns - Build the body of knowledge as part of PRs

## When to Use Full Stack vs Isolation

| Test Type | Approach |
|-----------|----------|
| Widget behavior, layout, rendering | Full stack with `Hex1bTerminal.CreateBuilder()` |
| Input handling, focus navigation | Full stack with `WithHex1bApp()` |
| Low-level APIs (Surface, SurfaceCell) | Test in isolation (dependencies of Hex1bApp) |
| Color/theme verification | Full stack with snapshot color assertions |

---

## Standard Test Structure

### Full Stack Integration Test

This is the **preferred pattern** for most tests:

```csharp
[Fact]
public async Task WidgetName_Scenario_ExpectedBehavior()
{
    // Arrange - Build the terminal with the app
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) => ctx => new VStackWidget([
            new TextBlockWidget("Hello"),
            new ButtonWidget("Click Me")
        ]))
        .WithHeadless()
        .WithDimensions(80, 24)
        .Build();

    // Act & Assert - Use input sequencer with WaitUntil
    var snapshot = await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(2), "initial render")
        .Down()  // Navigate to button
        .WaitUntil(s => s.ContainsText("> Click Me"), TimeSpan.FromSeconds(2), "button focused")
        .Capture("focused-button")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

    // Assert (often redundant if WaitUntil already verified)
    Assert.True(snapshot.ContainsText("> Click Me"));
}
```

### Key Elements

1. **`await using var terminal`** - Ensures proper disposal
2. **`.WithHeadless()`** - No actual terminal output (CI-safe)
3. **`.WithDimensions(80, 24)`** - Explicit terminal size
4. **`WaitUntil` before assertions** - Prevents timing issues
5. **`.Capture("name")`** - Saves SVG/HTML for debugging
6. **`Ctrl().Key(Hex1bKey.C)`** - Clean exit

---

## Input Sequencing Patterns

### Basic Navigation

```csharp
await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2), "list rendered")
    .Down()
    .WaitUntil(s => s.ContainsText("> Item 2"), TimeSpan.FromSeconds(2), "moved to item 2")
    .Down()
    .WaitUntil(s => s.ContainsText("> Item 3"), TimeSpan.FromSeconds(2), "moved to item 3")
    .Capture("navigation-result")
    .Ctrl().Key(Hex1bKey.C)
    .Build()
    .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
```

### Text Input

```csharp
await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("Name:"), TimeSpan.FromSeconds(2), "form rendered")
    .Type("John Doe")
    .WaitUntil(s => s.ContainsText("John Doe"), TimeSpan.FromSeconds(2), "text entered")
    .Capture("text-input")
    .Ctrl().Key(Hex1bKey.C)
    .Build()
    .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
```

### Keyboard Shortcuts

```csharp
await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(2), "app ready")
    .Ctrl().Key(Hex1bKey.S)  // Ctrl+S
    .WaitUntil(s => s.ContainsText("Saved"), TimeSpan.FromSeconds(2), "save completed")
    .Capture("after-save")
    .Ctrl().Key(Hex1bKey.C)
    .Build()
    .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
```

---

## Visual Assertion Patterns

### Using CellPatternSearcher

For precise cell-level assertions:

```csharp
// Find a specific character
var pattern = new CellPatternSearcher().Find('█');
var result = pattern.Search(snapshot);
Assert.True(result.HasMatches);
Assert.Equal(expectedX, result.First!.Start.X);

// Find with regex pattern
var pattern = new CellPatternSearcher().FindPattern(@"Count:\s*\d+");
var result = pattern.Search(snapshot);
Assert.True(result.HasMatches);

// Find with predicate
var pattern = new CellPatternSearcher()
    .Find(ctx => char.IsDigit(ctx.Cell.Character[0]));
var result = pattern.Search(snapshot);
Assert.Equal(3, result.Count);
```

### Color Assertions

For verifying themed/styled output:

```csharp
// Check if any cell has a specific background color
Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(0, 100, 200)),
    "Button should have blue background");

// Check if any cell has a specific foreground color
Assert.True(snapshot.HasForegroundColor(Hex1bColor.FromRgb(255, 255, 255)),
    "Text should be white");

// Get color at specific position
var bgColor = snapshot.GetBackgroundColor(10, 5);
Assert.Equal(Hex1bColor.FromRgb(255, 0, 0), bgColor);

// Check uniform row background
Assert.True(snapshot.HasUniformBackgroundColor(0, Hex1bColor.FromRgb(50, 50, 50)),
    "Header row should have dark background");
```

### Available Color Extension Methods

| Method | Purpose |
|--------|---------|
| `HasBackgroundColor()` | Any cell has a background color |
| `HasBackgroundColor(Hex1bColor)` | Any cell has specific background |
| `HasForegroundColor()` | Any cell has a foreground color |
| `HasForegroundColor(Hex1bColor)` | Any cell has specific foreground |
| `GetBackgroundColor(x, y)` | Get background at position |
| `GetForegroundColor(x, y)` | Get foreground at position |
| `HasUniformBackgroundColor(y, color)` | All cells in row have same background |
| `VisualizeBackgroundColors()` | Debug helper with visual representation |

---

## Anti-Patterns to Avoid

> **📘 See the `test-fixer` skill** for detailed diagnosis and fixes when tests become flaky.

### ❌ Insufficient WaitUntil Conditions (Partial Render)

```csharp
// BROKEN: Waits for partial content, but rest of screen may not be rendered
await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("Header"), TimeSpan.FromSeconds(2))  // ❌ Only checks header
    .Capture("screen")
    .Build()
    .ApplyAsync(terminal, ct);

// Assertion on footer may fail - it wasn't part of the WaitUntil!
Assert.True(snapshot.ContainsText("Footer"));
```

**Problem**: Rendering is inherently async. Finding "Header" doesn't guarantee "Footer" has rendered yet. This is especially problematic when testing other terminal frameworks (like Spectre Console) which may drop input if they're not ready to receive it.

**Fix**: Over-specify the `WaitUntil` condition to ensure everything you need is present:

```csharp
// ✅ Wait for ALL content you'll assert on
await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("Header") && s.ContainsText("Footer"), 
               TimeSpan.FromSeconds(2), "full screen rendered")
    .Capture("screen")
    .Build()
    .ApplyAsync(terminal, ct);
```

**Guideline**: If you're going to assert on specific screen content, include it in the `WaitUntil` condition. Don't assume the rest of the screen is ready just because one part appeared.

### ❌ Snapshot After Exit

```csharp
// BROKEN: Snapshot taken AFTER Ctrl+C clears the buffer
var snapshot = await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(2))
    .Capture("final")
    .Ctrl().Key(Hex1bKey.C)  // Buffer may be cleared before snapshot!
    .Build()
    .ApplyWithCaptureAsync(terminal, ct);

Assert.True(snapshot.ContainsText("Hello"));  // ❌ May fail on Linux CI
```

**Fix**: The `WaitUntil` already verified the content. If you need to assert, the `WaitUntil` serves as the assertion.

### ❌ Missing WaitUntil After Action

```csharp
// BROKEN: No wait for render after Down()
await new Hex1bTerminalInputSequenceBuilder()
    .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2))
    .Down()
    .Capture("after-down")  // ❌ Render may not be complete!
    .Ctrl().Key(Hex1bKey.C)
    .Build()
    .ApplyAsync(terminal, ct);
```

**Fix**: Always add `WaitUntil` after any action that changes state:

```csharp
.Down()
.WaitUntil(s => s.ContainsText("> Item 2"), TimeSpan.FromSeconds(2), "moved down")
.Capture("after-down")
```

### ❌ Task.Delay for Async Events

```csharp
// BROKEN: Fixed delay may not be long enough on slow CI
await terminal.SendKeyAsync(Hex1bKey.Enter);
await Task.Delay(100);  // ❌ Arbitrary delay
Assert.True(eventFired);
```

**Fix**: Use `TaskCompletionSource` to signal completion:

```csharp
var eventSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

// In event handler:
eventSignal.TrySetResult();

// In test:
await eventSignal.Task.WaitAsync(TimeSpan.FromSeconds(2), ct);
```

### ❌ Over-Abstracted Test Helpers

```csharp
// AVOID: Too many layers of abstraction
var result = await TestHelpers.CreateTerminalAndRunScenario(
    widgets: WidgetFactory.CreateStandardList(),
    actions: ActionBuilder.NavigateAndSelect(3),
    assertions: AssertionBuilder.SelectedItem("Item 3")
);
```

**Prefer**: Simple, linear, self-contained tests. Repetition is acceptable and helps AI agents understand patterns.

---

## Widget Test Dimensions

When writing tests for widgets, consider all the **dimensions** that affect behavior. Each widget should have tests covering these scenarios:

### 1. Terminal Size Variations

Widgets must work across different terminal sizes. Test the realistic range:

```csharp
[Theory]
[InlineData(40, 10)]   // Minimum realistic size
[InlineData(80, 24)]   // Standard terminal
[InlineData(120, 40)]  // Large terminal
[InlineData(200, 60)]  // Very large terminal
public async Task ListWidget_VariousTerminalSizes_RendersCorrectly(int width, int height)
{
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) => ctx => new ListWidget(["Item 1", "Item 2", "Item 3"]))
        .WithHeadless()
        .WithDimensions(width, height)
        .Build();

    await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2), "list rendered")
        .Capture($"list-{width}x{height}")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyAsync(terminal, TestContext.Current.CancellationToken);
}
```

**Key questions to answer:**
- What is the realistic minimum terminal size for this widget?
- Does the widget truncate, scroll, or wrap when space is limited?
- Does the widget expand appropriately in large terminals?
- Are there edge cases at specific sizes?

### 2. Container Widget Context

Widgets behave differently depending on their parent container. Test inside various layouts:

```csharp
[Fact]
public async Task ProgressWidget_InsideBorder_RendersWithCorrectWidth()
{
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) => ctx => new BorderWidget(
            new ProgressWidget { Value = 50, Maximum = 100 },
            title: "Loading"
        ))
        .WithHeadless()
        .WithDimensions(60, 10)
        .Build();

    await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Loading"), TimeSpan.FromSeconds(2), "border rendered")
        .Capture("progress-in-border")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyAsync(terminal, TestContext.Current.CancellationToken);
}

[Fact]
public async Task Button_InsideHStack_SharesSpaceCorrectly()
{
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) => ctx => new HStackWidget([
            new ButtonWidget("Cancel"),
            new ButtonWidget("OK")
        ]))
        .WithHeadless()
        .WithDimensions(40, 5)
        .Build();

    await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Cancel") && s.ContainsText("OK"), TimeSpan.FromSeconds(2))
        .Capture("buttons-in-hstack")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyAsync(terminal, TestContext.Current.CancellationToken);
}
```

**Common container scenarios to test:**
- Inside `VStackWidget` (vertical stacking)
- Inside `HStackWidget` (horizontal stacking)
- Inside `BorderWidget` (reduced available space)
- Inside `ScrollPanelWidget` (scrollable content)
- Inside `SplitterWidget` (resizable panes)
- Nested containers (e.g., Border inside VStack inside Splitter)

### 3. Theming Behavior

Verify that widgets respect theme colors and can be customized:

```csharp
[Fact]
public async Task Button_WithCustomTheme_UsesThemeColors()
{
    var customTheme = new Hex1bTheme("TestTheme")
        .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(255, 0, 0))
        .Set(ButtonTheme.ForegroundColor, Hex1bColor.FromRgb(255, 255, 255));

    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) =>
        {
            options.Theme = customTheme;
            return ctx => new ButtonWidget("Test Button");
        })
        .WithHeadless()
        .WithDimensions(40, 5)
        .Build();

    var snapshot = await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Test Button"), TimeSpan.FromSeconds(2))
        .Capture("themed-button")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

    Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(255, 0, 0)),
        "Button should have red background from theme");
    Assert.True(snapshot.HasForegroundColor(Hex1bColor.FromRgb(255, 255, 255)),
        "Button should have white text from theme");
}

[Fact]
public async Task Button_FocusedState_UsesFocusedThemeColors()
{
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp((app, options) => ctx => new VStackWidget([
            new TextBlockWidget("Header"),
            new ButtonWidget("Focusable Button")
        ]))
        .WithHeadless()
        .WithDimensions(40, 5)
        .Build();

    var snapshot = await new Hex1bTerminalInputSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Focusable Button"), TimeSpan.FromSeconds(2))
        .Tab()  // Focus the button
        .WaitUntil(s => s.ContainsText(">"), TimeSpan.FromSeconds(2), "button focused")
        .Capture("focused-button-theme")
        .Ctrl().Key(Hex1bKey.C)
        .Build()
        .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

    // Verify focused state uses different colors than unfocused
    Assert.True(snapshot.HasBackgroundColor(), "Focused button should have background color");
}
```

**Theming scenarios to test:**
- Default theme renders correctly
- Custom theme colors are applied
- Focused vs unfocused states use appropriate theme values
- Disabled state styling (if applicable)
- Theme inheritance from parent widgets

### 4. Widget Test Matrix

For comprehensive widget coverage, consider this matrix:

| Dimension | Variations to Test |
|-----------|-------------------|
| **Terminal Size** | Minimum (40×10), Standard (80×24), Large (120×40), Very Large (200×60) |
| **Container** | Root, VStack, HStack, Border, Scroll, Splitter, Nested |
| **Theme** | Default, Custom colors, Focused state, Disabled state |
| **Content** | Empty, Minimal, Typical, Maximum/overflow |
| **State** | Initial, After interaction, Edge cases |

Not every widget needs every combination, but consider which dimensions are relevant for the widget's behavior.

---

## Low-Level API Testing (Isolation)

For APIs that are dependencies of `Hex1bApp` (like `Surface`), test in isolation:

```csharp
[Fact]
public void Surface_WriteText_SetsCorrectCells()
{
    // Arrange
    var surface = new Surface(80, 24);
    
    // Act
    surface.WriteText(0, 0, "Hello");
    
    // Assert
    Assert.Equal('H', surface[0, 0].Character[0]);
    Assert.Equal('e', surface[1, 0].Character[0]);
    Assert.Equal('l', surface[2, 0].Character[0]);
    Assert.Equal('l', surface[3, 0].Character[0]);
    Assert.Equal('o', surface[4, 0].Character[0]);
}

[Fact]
public void SurfaceCell_WithColor_PreservesColor()
{
    // Arrange
    var cell = new SurfaceCell('X', Hex1bColor.Red, Hex1bColor.Blue);
    
    // Assert
    Assert.Equal('X', cell.Character[0]);
    Assert.Equal(Hex1bColor.Red, cell.Foreground);
    Assert.Equal(Hex1bColor.Blue, cell.Background);
}
```

---

## Test Naming Convention

Follow `MethodName_Scenario_ExpectedBehavior`:

```csharp
[Fact]
public async Task ListWidget_DownArrow_SelectsNextItem() { }

[Fact]
public async Task TextBox_TypeText_DisplaysInput() { }

[Fact]
public async Task Button_EnterKey_TriggersClickHandler() { }

[Fact]
public void Surface_Fill_SetsAllCellsInRegion() { }
```

---

## Updating This Skill

When you discover a new testing pattern while writing tests:

1. **Add the pattern to this skill** as part of the same PR
2. **Include a concrete example** with comments
3. **Explain when to use it** (what problem does it solve?)
4. **If it's an anti-pattern**, add it to the anti-patterns section with the fix

This builds the body of knowledge available to AI agents working on the codebase.

### Examples of Patterns to Document

- New assertion helpers or extension methods
- Patterns for testing specific widget types
- Workarounds for platform-specific behavior
- Performance testing patterns
- Patterns for testing async behavior

---

## Checklist for New Tests

- [ ] Uses `Hex1bTerminal.CreateBuilder()` with `.WithHeadless()`
- [ ] Uses `.WithHex1bApp()` for TUI functionality (unless testing low-level APIs)
- [ ] Has `WaitUntil` after every action that changes state
- [ ] Has `WaitUntil` immediately before `.Capture()`
- [ ] Uses descriptive wait messages (third parameter to `WaitUntil`)
- [ ] Exits cleanly with `Ctrl().Key(Hex1bKey.C)`
- [ ] Follows `MethodName_Scenario_ExpectedBehavior` naming
- [ ] Is simple and linear (no unnecessary abstractions)
- [ ] Asserts on colors when testing themed/styled widgets
- [ ] Uses `CellPatternSearcher` for precise cell assertions when needed
