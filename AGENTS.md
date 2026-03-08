# AI Coding Agent Guidelines for Hex1b

This document provides context and conventions for AI coding agents (GitHub Copilot, Claude, Cursor, etc.) working with the Hex1b codebase.

## 🛠️ Available Skills

This repository includes specialized skills in `.github/skills/` that provide detailed guidance for specific tasks. **Invoke these skills when working on related tasks** - they contain step-by-step procedures, templates, and best practices.

| Skill | When to Use |
|-------|-------------|
| **widget-creator** | Creating new widgets (widget records, nodes, theming, tests) |
| **writing-unit-tests** | Writing unit tests for widgets, nodes, or terminal functionality |
| **test-fixer** | Diagnosing flaky tests, especially timing-related failures in CI |
| **api-reviewer** | Reviewing API design, accessibility modifiers, and naming conventions |
| **doc-writer** | Writing XML API documentation or end-user guides |
| **doc-tester** | Validating documentation accuracy against library behavior |
| **surface-benchmarker** | Running performance benchmarks after modifying `src/Hex1b/Surfaces/` |
| **aspire** | Working with .NET Aspire (running samples, debugging, MCP tools) |

Skills are invoked automatically by AI agents based on the task context. They contain comprehensive procedures that complement the high-level guidance in this file.

## 📋 Project Overview

**Hex1b** is a .NET library for building terminal user interfaces (TUI) with a React-inspired declarative API. The library ships to NuGet as `Hex1b`.

### Key Technologies
- **.NET 10.0** (preview) - Target framework
- **C# 12+** - Modern C# features enabled
- **xUnit** - Testing framework
- **.NET Aspire** - Used for running sample applications

### Repository Layout
```
src/Hex1b/           → Main library (THE shipped package)
tests/Hex1b.Tests/   → Unit tests
samples/             → Example applications
apphost.cs           → Aspire app host for samples
```

## 🏗️ Architecture Concepts

### Widget/Node Pattern

Hex1b separates **configuration (widgets)** from **rendering (nodes)**:

| Concept | Location | Mutability | Purpose |
|---------|----------|------------|---------|
| **Widget** | `src/Hex1b/Widgets/` | Immutable | Describes what to render |
| **Node** | `src/Hex1b/Nodes/` | Mutable | Manages state, handles input, renders |

**Important**: Widgets are `record` types; Nodes are `class` types with mutable properties.

### Reconciliation

The `Hex1bApp.Reconcile()` method diffs widgets against nodes:
- Same widget type → Update existing node's properties
- Different widget type → Create new node
- This preserves state (focus, cursor position) across re-renders

### Layout System

Located in `src/Hex1b/Layout/`:
- `Constraints` - Min/max width/height bounds
- `Size` - Measured dimensions
- `Rect` - Position and size for arrangement
- `SizeHint` - Fill, Content, or Fixed sizing

### Render Loop (in Hex1bApp.cs)
```
Build widgets → Reconcile → Measure → Arrange → Render → Wait for input → Repeat
```

## 📝 Code Conventions

### Naming
- Widgets: `*Widget` (e.g., `TextBlockWidget`, `ButtonWidget`)
- Nodes: `*Node` (e.g., `TextBlockNode`, `ButtonNode`)
- State objects: `*State` (e.g., `TextBoxState`) - for widgets requiring user-owned mutable state

### Widget Definition Pattern
```csharp
// Widgets are records with fluent methods for event handlers
public record ButtonWidget(string Label) : Hex1bWidget
{
    internal Func<ButtonClickedEventArgs, Task>? ClickHandler { get; init; }
    
    public ButtonWidget OnClick(Action<ButtonClickedEventArgs> handler)
        => this with { ClickHandler = args => { handler(args); return Task.CompletedTask; } };
    
    public ButtonWidget OnClick(Func<ButtonClickedEventArgs, Task> handler)
        => this with { ClickHandler = handler };
}
```

### Node Definition Pattern
```csharp
public class ButtonNode : Hex1bNode
{
    // Properties reconciled from widget
    public string Label { get; set; } = "";
    public Func<InputBindingActionContext, Task>? ClickAction { get; set; }
    
    // Focus state (mutable, preserved across reconciliation)
    public bool IsFocused { get; set; }
    
    // Required overrides
    public override void Measure(Constraints constraints) { /* ... */ }
    public override void Arrange(Rect rect) { /* ... */ }
    public override void Render(Hex1bRenderContext context) { /* ... */ }
}
```

### Keybinding Rebinding Pattern

When widgets define input bindings in `ConfigureDefaultBindings`, use `Triggers(ActionId, handler, desc)` instead of `Action(handler, desc)` to make bindings rebindable:

```csharp
// In Widget record:
public static readonly ActionId MyAction = new(nameof(MyAction));

// In Node's ConfigureDefaultBindings:
bindings.Key(Hex1bKey.Enter).Triggers(MyWidget.MyAction, handler, "Description");
```

Users can then remap/alias/disable actions via `WithInputBindings`:
```csharp
widget.WithInputBindings(b =>
{
    b.Remove(MyWidget.MyAction);  // remove default key
    b.Key(Hex1bKey.X).Triggers(MyWidget.MyAction);  // rebind to X
});
```

### Adding New Widgets

> **📘 Use the `widget-creator` skill** for comprehensive step-by-step guidance including templates, theming, and test patterns.

Quick checklist:
1. Create `XxxWidget` record in `src/Hex1b/Widgets/`
2. Create `XxxNode` class in `src/Hex1b/Nodes/`
3. Add extension methods in `src/Hex1b/XxxExtensions.cs`
4. Add theme elements in `src/Hex1b/Theming/XxxTheme.cs`
5. Write tests in `tests/Hex1b.Tests/XxxNodeTests.cs`

### Test Conventions

> **📘 Use the `test-fixer` skill** when tests pass locally but fail in CI, or exhibit timing-sensitive behavior.

Follow the `MethodName_Scenario_ExpectedBehavior` naming pattern:
```csharp
[Fact]
public void Measure_WithConstraints_ReturnsExpectedSize()
{
    // Arrange, Act, Assert
}
```

## 🔧 Common Tasks

### Building
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

### Running a Sample
```bash
dotnet run --project samples/Cancellation
```

## 🚀 .NET Aspire

> **📘 Use the `aspire` skill** for detailed Aspire workflows, MCP tools, debugging, and integration guidance.

Aspire orchestrates sample applications. Resources are defined in `apphost.cs`.

### Quick Commands
```bash
aspire run              # Run the app host
aspire run --detach     # Run in background (for agent environments)
aspire stop             # Stop running instances
aspire update           # Update Aspire packages
```

### Key Points
- Changes to `apphost.cs` require restart
- Use Aspire MCP tools (`list_resources`, `list_structured_logs`, etc.) for debugging
- Avoid persistent containers early in development

## ⚠️ Important Constraints

### Terminal Limitations
- No mouse support currently (keyboard-only navigation)
- Relies on ANSI escape sequences
- Alternate screen buffer used (`\x1b[?1049h` / `\x1b[?1049l`)

### Focus System
- Focusable nodes: `TextBoxNode`, `ButtonNode`, `ListNode`
- Focus navigation: Tab (forward), Shift+Tab (backward), Escape (up to parent)
- Parent containers manage focus among children

### Input Handling
- Input flows from `IHex1bTerminal` → `Hex1bApp` → `InputRouter` → focused `Node`
- InputBindings are checked before standard input handling
- Return `InputResult.Handled` from `HandleInput` if the input was consumed

## 🎯 When Making Changes

### DO:
- ✅ Run tests after changes: `dotnet test`
- ✅ Follow existing patterns for new widgets/nodes
- ✅ Keep widgets immutable (use `record`)
- ✅ Preserve node state during reconciliation
- ✅ Use nullable reference types properly

### DON'T:
- ❌ Add mutable state to widgets
- ❌ Forget to add reconciliation for new widget types
- ❌ Skip writing tests for new functionality
- ❌ Break the render loop sequence

## 📚 Key Files to Understand

| File | Purpose |
|------|---------|
| `src/Hex1b/Hex1bApp.cs` | Main entry point, reconciliation, render loop |
| `src/Hex1b/Hex1bRenderContext.cs` | Terminal rendering abstraction |
| `src/Hex1b/IHex1bTerminal.cs` | Terminal interface (mockable for tests) |
| `src/Hex1b/Nodes/Hex1bNode.cs` | Base class for all nodes |
| `src/Hex1b/Widgets/Hex1bWidget.cs` | Base class for all widgets |
| `src/Hex1b/Layout/Constraints.cs` | Layout constraint system |

## 🧪 Testing

### Running Tests
```bash
dotnet test
```

### Unit Testing Nodes
- Create node directly, set properties, verify behavior
- Use `Hex1bKeyEvent` to simulate input
- Check measured size after `Measure()`

### Integration Testing
- Use `Hex1bApp` with `Hex1bAppWorkloadAdapter`
- Test full widget → node → render cycle
- See `tests/Hex1b.Tests/` for examples

## 💬 Asking for Help

When asking questions about this codebase:
1. Reference specific file paths
2. Mention whether you're working with widgets or nodes
3. Include relevant test context if debugging
4. Note any Aspire-specific requirements

---

*This file is intended for AI coding agents. Humans should refer to [CONTRIBUTING.md](CONTRIBUTING.md) for contribution guidelines.*
