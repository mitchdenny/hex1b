---
name: api-reviewer
description: Guidelines for reviewing API design in the Hex1b codebase. Use when evaluating public APIs, reviewing accessibility modifiers, or assessing whether new APIs follow project conventions.
---

# API Reviewer Skill

This skill captures API design preferences for the Hex1b codebase. Use it when reviewing public APIs, evaluating whether code should be public or internal, or assessing API design decisions made by AI agents.

## Codebase Architecture

Hex1b is structured into distinct layers, each with different API design considerations:

### Layer 1: Hex1bTerminal (Core)

The foundational layer maintaining an in-memory representation of terminal state.

| Component | Purpose | Accessibility |
|-----------|---------|---------------|
| `Hex1bTerminal` | Core terminal state management | Public |
| `Hex1bTerminalBuilder` | Fluent terminal construction | Public |
| Presentation adapters | Output to real terminals, tests, web | Public interfaces |
| Workload adapters | Input/output abstraction | Public interfaces |
| `Surface`, `SurfaceCell` | Low-level cell storage | Public (low-level API) |

**Design note**: Hex1bTerminal emerged from the need to properly test TUI components. The adapter pattern enables use in real terminals, unit tests, or projecting state across networks.

### Layer 2: Hex1bApp/TUI

The widget/node tree layer, inspired by React/Ink but not beholden to it.

| Component | Purpose | Accessibility |
|-----------|---------|---------------|
| `Hex1bApp` | Root of TUI applications | Public |
| `*Widget` records | Declarative UI configuration | Public |
| `*Node` classes | DOM-like render elements | Generally public (for extensibility) |
| Reconciliation logic | Widget→Node synchronization | Internal |
| Layout algorithms | Measure/arrange implementation | Internal |

### Layer 3: Automation

Testing and automation infrastructure.

| Component | Purpose | Accessibility |
|-----------|---------|---------------|
| `Hex1bTerminalInputSequenceBuilder` | Fluent input automation | Public |
| `CellPatternSearcher` | Visual assertion matching | Public |
| `Hex1bTerminalSnapshot` | Captured terminal state | Public |

**Design note**: Automation APIs are explicitly for external consumers, not just internal testing.

---

## Public vs Internal Decision Framework

When reviewing accessibility modifiers, apply this framework:

### Make it PUBLIC when:

1. **External consumers need it** - Adapters, extension points, widget customization
2. **It's part of the conceptual API** - Users think in terms of this abstraction
3. **It enables extensibility** - Custom widgets, themes, adapters
4. **It's stable** - You're confident the design won't need breaking changes

### Make it INTERNAL when:

1. **It's implementation detail** - Only Hex1b itself (or agents working on it) would invoke it
2. **The abstraction isn't proven** - Uncertain if this is the right level of abstraction
3. **It could change** - Design may evolve with more usage
4. **It exposes internals** - Would leak implementation details to consumers

### Make it PRIVATE when:

1. **Single class usage** - Only used within one type
2. **Helper methods** - Implementation helpers with no external meaning

### Review Questions

When reviewing agent-generated code, ask:

- [ ] Is this API an internal implementation detail?
- [ ] Does this method/property make sense on this type?
- [ ] Is the abstraction level right?
- [ ] Would making this public lock us into a design we're not confident about?
- [ ] Can this be internal for now until we're confident in the design?

---

## API Design Patterns

### Builders: Classic `With...()` Pattern

For `Hex1bTerminalBuilder` and similar construction scenarios:

```csharp
// ✅ PREFERRED: Classic builder pattern
var terminal = Hex1bTerminal.CreateBuilder()
    .WithWorkload(workload)
    .WithPresentation(presentation)
    .WithDimensions(80, 24)
    .WithHeadless()
    .Build();
```

### Widgets: SwiftUI-Inspired Minimal Constructors

For widget APIs, use minimal constructors with essential arguments only, then fluent extension methods:

```csharp
// ✅ PREFERRED: Minimal constructor + fluent methods
ctx.Table(columns, rows)
   .SelectedRow(selectedIndex)
   .OnSelectionChanged(HandleSelection)

// ✅ PREFERRED: Cross-cutting concerns as extension methods
ctx.Progress(value, max).Fill()

// ❌ AVOID: Overloaded constructors with many parameters
new TableWidget(columns, rows, selectedIndex, onSelectionChanged, sortColumn, sortDirection, ...)
```

**Guideline**: Only include the most essential arguments in the constructor. Make usage "bleeding obvious." Use extension methods to splice in extra behavior.

### Options Types for Complex Configuration

When you start creating many overloads, use an options type:

```csharp
// ✅ PREFERRED: Options type for complex configuration
public class Hex1bAppOptions
{
    public Hex1bTheme? Theme { get; init; }
    public IHex1bTerminalWorkloadAdapter? WorkloadAdapter { get; init; }
    // ... extensible without breaking changes
}

// ❌ AVOID: Many overloads
public Hex1bApp(Func<...> builder) { }
public Hex1bApp(Func<...> builder, Hex1bTheme theme) { }
public Hex1bApp(Func<...> builder, Hex1bTheme theme, IWorkloadAdapter adapter) { }
```

**But**: Don't have options classes everywhere. Be conservative with what you require.

---

## Async Patterns

### Prefer Task, Use ValueTask for Performance

```csharp
// ✅ PREFERRED: Task as default
public Task<Hex1bWidget> BuildAsync(WidgetContext ctx);

// ✅ Use ValueTask when there's a performance reason (hot paths, avoiding allocations)
public ValueTask HandleInputAsync(Hex1bKeyEvent key);
```

### Sync + Async Overloads for Callbacks

Provide both sync and async overloads for event handlers. Implement everything assuming async is possible, then wrap sync handlers:

```csharp
// ✅ PREFERRED: Both overloads, sync wraps to async
public ButtonWidget OnClick(Action<ButtonClickedEventArgs> handler)
    => this with { ClickHandler = args => { handler(args); return Task.CompletedTask; } };

public ButtonWidget OnClick(Func<ButtonClickedEventArgs, Task> handler)
    => this with { ClickHandler = handler };
```

**Note**: Samples often favor sync versions for simplicity.

---

## Naming and Nullability

### Types Over Magic Strings

```csharp
// ✅ PREFERRED: Strongly typed
public void SetColor(Hex1bColor color);

// ❌ AVOID: Magic strings
public void SetColor(string colorName);
```

### Avoid Forcing Null Arguments

```csharp
// ❌ AVOID: Forcing callers to pass null
public void Configure(string? requiredArg, string? optionalArg);
Configure("value", null);  // Awkward

// ✅ PREFERRED: Overloads or optional parameters
public void Configure(string requiredArg);
public void Configure(string requiredArg, string optionalArg);
```

### Avoid Primitive Obsession

Be wary of using too many primitive types, as it makes creating overloads harder in the future:

```csharp
// Consider whether a type is warranted
public void SetPosition(int x, int y);        // OK for simple cases
public void SetPosition(Point position);       // Better if Point is meaningful elsewhere
```

---

## Extension Methods

Use extension methods when:

1. **They don't need internal access** - Can work with public API surface
2. **They represent cross-cutting concerns** - e.g., `.Fill()`, `.FixedWidth()`
3. **They add optional behavior** - Not core to the type's identity

Use instance methods when:

1. **They need internal state** - Avoiding would leak implementation details
2. **They're core to the type** - Essential behavior, not optional configuration

---

## Namespace Design

### Discoverability First

Minimize the namespaces developers need to know:

```csharp
// ✅ PREFERRED: Most types in root namespace
using Hex1b;

// Specialized namespaces for specific concerns
using Hex1b.Theming;
using Hex1b.Input;
```

**Guideline**: Ideally, most developers just need `using Hex1b;` and discover other types through methods on types in that namespace.

---

## Documentation Standards

> **📘 See the `doc-writer` skill** for comprehensive documentation guidelines including XML API docs and end-user guides.

### XML Documentation Requirements

All public APIs must have XML documentation:

```csharp
/// <summary>
/// Creates a button widget with the specified label.
/// </summary>
/// <remarks>
/// Buttons are focusable widgets that respond to Enter key or click events.
/// Use <see cref="OnClick"/> to register a handler for button activation.
/// 
/// Buttons automatically display a focus indicator when focused and can be
/// styled using <see cref="ButtonTheme"/> elements.
/// </remarks>
/// <param name="label">The text displayed on the button.</param>
/// <example>
/// <description>A simple quit button that stops the application:</description>
/// <code>
/// using Hex1b;
/// 
/// await using var terminal = Hex1bTerminal.CreateBuilder()
///     .WithHex1bApp((app, options) => ctx => 
///         ctx.Button("Quit").OnClick(e => e.Context.RequestStop()))
///     .Build();
/// 
/// await terminal.RunAsync();
/// </code>
/// </example>
public sealed record ButtonWidget(string Label) : Hex1bWidget
```

### Documentation Guidelines

| Element | Guideline |
|---------|-----------|
| `<summary>` | Concise and accurate - what it does, not how |
| `<remarks>` | Detailed, useful from end-user perspective, no irrelevant internals |
| `<param>` | Required for all parameters |
| `<returns>` | Required for non-void methods |
| `<example>` | Complete, runnable mini-apps when possible |
| `<code>` | Cut-and-paste ready, not just method invocation |

**Anti-pattern**: Examples that just show invoking the method without context. Always provide complete, runnable examples.

---

## Error Handling

### Exceptions for Irrecoverable Errors

Throw exceptions when something is truly irrecoverable:

```csharp
// ✅ Use existing exception types when they fit
throw new ArgumentNullException(nameof(widget));
throw new InvalidOperationException("Cannot render before Measure()");

// ✅ Custom exceptions when debugging info is needed
throw new Hex1bRenderException(widget, node, phase, "Render failed", innerException);
```

**Guideline**: Think about what information developers need to debug. Attach widget/node/phase information when it helps.

**Note**: The `RescueWidget` exists to handle and display errors gracefully in the UI.

---

## Code Organization

### One Type Per File

Each type should be in its own file. This makes it easier to:
- Review changes
- Track modifications
- Support concurrent agent work

### Avoid Statics

```csharp
// ❌ AVOID: Static state (causes testability bugs)
private static readonly Dictionary<string, Widget> _cache = new();

// ✅ PREFERRED: Instance state
private readonly Dictionary<string, Widget> _cache = new();
```

**Rationale**: AI agents tend to use statics, which introduce subtle testability bugs.

### Constants Are Fine

```csharp
// ✅ OK: Constants
public const int DefaultWidth = 80;
public const int DefaultHeight = 24;
```

---

## Review Checklist

When reviewing APIs, check:

### Accessibility
- [ ] Is this the right accessibility level (public/internal/private)?
- [ ] Are we exposing implementation details unnecessarily?
- [ ] Is the API stable enough to be public?

### Design
- [ ] Does the method/property make sense on this type?
- [ ] Is the abstraction level right?
- [ ] Are we using options types where there are many parameters?
- [ ] Are we avoiding primitive obsession?
- [ ] Types over magic strings?

### Patterns
- [ ] Builders use `With...()` pattern?
- [ ] Widgets use minimal constructors + fluent extensions?
- [ ] Callbacks have sync + async overloads?
- [ ] ValueTask preferred over Task?

### Documentation
- [ ] Summary is concise and accurate?
- [ ] Remarks provide useful detail (not internal implementation)?
- [ ] Examples are complete, runnable mini-apps?

### Organization
- [ ] One type per file?
- [ ] Avoiding unnecessary statics?
- [ ] Namespaces minimized for discoverability?

---

## Anti-Patterns to Flag

### Overly Public Agent-Generated Code

AI agents often make things public by default. Flag for review:

```csharp
// ❌ REVIEW: Should this be internal?
public void ReconcileChildNodes(List<Hex1bNode> children) { }

// ❌ REVIEW: Implementation detail exposed
public int CalculateInternalLayoutOffset() { }
```

### Too Many Overloads

When you see many overloads, suggest an options type:

```csharp
// ❌ REVIEW: Consider options type
public Table(columns);
public Table(columns, selectedRow);
public Table(columns, selectedRow, sortColumn);
public Table(columns, selectedRow, sortColumn, sortDirection);
```

### Magic Strings

Flag string parameters that should be types:

```csharp
// ❌ REVIEW: Should be enum or type
public void SetAlignment(string alignment);  // "left", "center", "right"

// ✅ Better
public void SetAlignment(Alignment alignment);
```

### Incomplete Documentation

Flag public APIs without proper documentation:

```csharp
// ❌ REVIEW: Missing documentation
public Hex1bWidget CreateWidget(WidgetContext ctx);

// ❌ REVIEW: Example not runnable
/// <example>
/// <code>
/// widget.OnClick(handler);
/// </code>
/// </example>
```
