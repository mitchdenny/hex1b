# AI Coding Agent Guidelines for Hex1b

This document provides context and conventions for AI coding agents (GitHub Copilot, Claude, Cursor, etc.) working with the Hex1b codebase.

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
- State objects: `*State` (e.g., `TextBoxState`, `ListState`)

### Widget Definition Pattern
```csharp
// Widgets are records with optional InputBindings
public record ButtonWidget(string Label, Action OnClick) : Hex1bWidget;
```

### Node Definition Pattern
```csharp
public class ButtonNode : Hex1bNode
{
    // Properties reconciled from widget
    public string Label { get; set; } = "";
    public Action? OnClick { get; set; }
    
    // Focus state (mutable, preserved across reconciliation)
    public bool IsFocused { get; set; }
    
    // Required overrides
    public override void Measure(Constraints constraints) { /* ... */ }
    public override void Arrange(Rect rect) { /* ... */ }
    public override void Render(Hex1bRenderContext context) { /* ... */ }
}
```

### Adding New Widgets

When adding a new widget type, you must:

1. Create `XxxWidget` record in `src/Hex1b/Widgets/`
2. Create `XxxNode` class in `src/Hex1b/Nodes/`
3. Add reconciliation case in `Hex1bApp.Reconcile()` switch expression
4. Add `ReconcileXxx()` method in `Hex1bApp.cs`
5. Write tests in `tests/Hex1b.Tests/XxxNodeTests.cs`

### Test Conventions
```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var node = new ButtonNode { Label = "Test" };
    
    // Act
    var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));
    
    // Assert
    Assert.Equal(InputResult.Handled, result);
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

Aspire is the orchestrator for the entire application, handling dependency configuration, building, and running. Resources are defined in `apphost.cs`.

### Running with Aspire
```bash
aspire run
```

If there is already an instance running, it will prompt to stop the existing instance. You only need to restart if `apphost.cs` changes, but restarting can reset everything to a known state.

### General Aspire Workflow
1. **Before making changes**: Run `aspire run` and inspect resource state to build from a known state
2. **Make changes incrementally**: Validate with `aspire run` after each change
3. **Use MCP tools**: Check resource status and debug issues using Aspire MCP tools

### Aspire MCP Tools

| Tool | Purpose |
|------|---------|
| `list_resources` | Check status of resources in the app model |
| `execute_resource_command` | Restart resources or perform other actions |
| `list_integrations` | Get available integrations with versions |
| `get_integration_docs` | Fetch documentation for specific integrations |
| `list_structured_logs` | Get structured log details for debugging |
| `list_console_logs` | Get console log output for debugging |
| `list_traces` | Get distributed trace information |
| `list_trace_structured_logs` | Get logs related to a specific trace |
| `select_apphost` | Switch between multiple app hosts |
| `list_apphosts` | View active app hosts |

### Adding Integrations
**IMPORTANT**: When adding a resource to the app model:
1. Use `list_integrations` to get current versions of available integrations
2. Match the integration version to the Aspire.AppHost.Sdk version (some may have preview suffix)
3. Use `get_integration_docs` to fetch the latest documentation
4. Follow documentation links for additional guidance

### Debugging with Aspire
Aspire captures rich logs and telemetry. Use diagnostic tools **before** making changes:
1. `list_structured_logs` - Detailed structured logs
2. `list_console_logs` - Console output
3. `list_traces` - Distributed traces
4. `list_trace_structured_logs` - Logs related to a specific trace

### Updating Aspire
```bash
aspire update
```
This updates the apphost and some Aspire packages. You may need to manually update other packages. Consider using `dotnet-outdated` with user consent:
```bash
dotnet tool install --global dotnet-outdated-tool
```

### Aspire Constraints
- ⚠️ **Persistent containers**: Avoid early in development to prevent state management issues
- ⚠️ **Aspire workload is OBSOLETE**: Never install or use the Aspire workload
- ✅ Changes to `apphost.cs` require application restart

### Playwright Integration
The Playwright MCP server is configured for functional testing. Use `list_resources` to get endpoints for navigation with Playwright.

### Official Aspire Documentation
1. https://aspire.dev
2. https://learn.microsoft.com/dotnet/aspire
3. https://nuget.org (for integration package details)

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

## 🧪 Testing Strategies

### Unit Testing Nodes
- Create node directly, set properties, verify behavior
- Use `Hex1bKeyEvent` to simulate input
- Check measured size after `Measure()`
- Verify rendering output if needed

### Integration Testing
- Use `Hex1bApp` with mock `IHex1bTerminal`
- Test full widget → node → render cycle
- See `Hex1bAppIntegrationTests.cs` for examples

## 💬 Asking for Help

When asking questions about this codebase:
1. Reference specific file paths
2. Mention whether you're working with widgets or nodes
3. Include relevant test context if debugging
4. Note any Aspire-specific requirements

---

*This file is intended for AI coding agents. Humans should refer to [CONTRIBUTING.md](CONTRIBUTING.md) for contribution guidelines.*
