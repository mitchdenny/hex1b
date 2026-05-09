# API Design Guidelines

These guidelines describe how the public API of Hex1b is shaped — and why the shape is the way it is. They apply both to the library itself and to any third-party packages that ship widgets or extensions on top of it.

The conventions on this page are enforced by Roslyn analyzers (HEX1B0001–HEX1B0009) so that the library and its ecosystem stay consistent over time. Each rule below names the analyzer that polices it.

## Two builders, two patterns

Hex1b has **two distinct fluent surfaces**, and they look different on purpose:

| Surface | Receiver type | Method-name shape | Example |
|---|---|---|---|
| **Terminal builder** | `Hex1bTerminalBuilder` | `With*` | `terminal.WithMouse().WithHex1bApp(...)` |
| **Widget composition** | `WidgetContext<T>` and widget instances | bare verb-noun (no `With*`) | `context.Border(b => [...]).Title("Hi")` |

Both are fluent, but they do different jobs:

- **`Hex1bTerminalBuilder`** wires together the host process — terminal plumbing, presentation/workload adapters, recording, mouse, MCP diagnostics, etc. It is configured **once at startup**, returns the same builder for chaining, and uses `With*` because each call mutates a builder configuration object.
- **Widget composition** describes the **UI tree on every render pass**. Widgets are immutable `record`s, so a "fluent" call returns a new widget value (or constructs a new one in an extension method) — there is nothing to "be `With`'d into". The `With*` prefix is reserved for the terminal builder so that, when you read a chain of calls, you can tell at a glance which surface you're on.

This is why `HEX1B0001` will reject any `With*` method declared on a widget extension or instance. If you find yourself wanting `WithTitle(...)` on a widget, use `Title(...)` instead.

## Anatomy of a widget

A widget has two halves:

```csharp
// Configuration — immutable, declarative.
public record ButtonWidget(string Label) : Hex1bWidget
{
    internal Func<ButtonClickedEventArgs, Task>? ClickHandler { get; init; }

    // Event handlers use On*.
    public ButtonWidget OnClick(Action<ButtonClickedEventArgs> handler)
        => this with { ClickHandler = args => { handler(args); return Task.CompletedTask; } };

    public ButtonWidget OnClick(Func<ButtonClickedEventArgs, Task> handler)
        => this with { ClickHandler = handler };
}

// Behavior — mutable, stateful, owned by the renderer.
public class ButtonNode : Hex1bNode { /* … */ }
```

Naming rules:

| Rule | Convention | Analyzer |
|---|---|---|
| Widget type names | end in `Widget` | HEX1B0002 |
| Node type names | end in `Node` | HEX1B0003 |
| Widget kind | declared as `record` | HEX1B0004 |
| Node kind | declared as `class` | HEX1B0005 |
| Event handlers on a widget | `On<Verb>` (`OnClick`, `OnSelectionChanged`) | — |
| Configuration on a widget | bare verb-noun (`Title`, `MaxFloating`, `Disabled`) | — |
| Never on a widget | `With*` (reserved for `Hex1bTerminalBuilder`) | HEX1B0001 |

## The `WidgetContext<T>` family

Most widgets are produced by an extension method that takes a `WidgetContext<TParent>` and returns the widget. The context gives child widgets a typed handle to their lexical parent, which the layout system uses to make sensible defaults (e.g. an `HStack` child knows it lives inside a horizontal container).

```csharp
public static BorderWidget Border<TParent>(
    this WidgetContext<TParent> context,
    Func<WidgetContext<BorderWidget>, Hex1bWidget[]> builder,
    string? title = null)
    where TParent : Hex1bWidget
    => /* … */;
```

Two parameter naming rules apply:

- **`HEX1B0006`** — the `WidgetContext<T>` receiver is always named **`context`**.
- **`HEX1B0008`** — a single widget-builder callback is always named **`builder`**.

So every call site reads the same way:

```csharp
context.Border(b => [
    b.Text("Hello"),
    b.Button("Click me").OnClick(_ => DoStuff())
])
```

Inside the lambda, the parameter is the **child** context (here named `b` for brevity). Lambda variable names are unconstrained — the analyzer only governs the names declared in the extension method signature, so callers can use whatever short alias they like.

## Widget instance extensions

Some operations apply to an existing widget rather than building a new one — for example, decorating an editor with a language server, or adding input bindings to anything. These are extensions on a widget instance, not on a context:

```csharp
public static EditorWidget LanguageServer(
    this EditorWidget widget,
    Action<LanguageServerConfiguration> configure)
    => /* … */;
```

- **`HEX1B0007`** — when the receiver is a `Hex1bWidget`-derived type (or a generic constrained to `Hex1bWidget`), it is named **`widget`**, never `editor`, `target`, `self`, or a role-name.

This keeps every widget-on-widget operation reading the same way regardless of which widget it acts on:

```csharp
context.Editor(state)
       .LanguageServer(workspace, "file:///app/Program.cs")
       .Decorations(provider);
```

## One builder per method

Most widget-builder methods take exactly one builder callback. A handful of widgets — splitters, for example — genuinely need two, one per pane:

```csharp
[SuppressMessage(
    "Hex1b.ApiDesign",
    "HEX1B0009",
    Justification = "A splitter inherently has two independent panes; each side takes its own builder callback.")]
public static SplitterWidget HSplitter<TParent>(
    this WidgetContext<TParent> context,
    Func<WidgetContext<VStackWidget>, Hex1bWidget[]> leftBuilder,
    Func<WidgetContext<VStackWidget>, Hex1bWidget[]> rightBuilder,
    int leftWidth = 30)
    where TParent : Hex1bWidget
    => /* … */;
```

- **`HEX1B0009`** — declaring two or more widget-builder callbacks on the same method is a warning, suppressible with a `Justification`. The suppression is the documentation: it tells the next reader that the multi-builder shape is deliberate, not an oversight.

When you have a choice, prefer a single `builder` that returns multiple children (e.g. `Func<…, Hex1bWidget[]>`) over multiple positional builders. Splitters are the exception because the two panes have different roles, not just different positions.

### `On*` methods are not builders

Both `HEX1B0008` and `HEX1B0009` ignore methods whose name matches `On<Verb>`. These are event-handler decorators — they happen to take a callback that returns a widget, but they fire in response to an event rather than participating in the widget tree's static composition:

```csharp
// Allowed: OnBlock takes a Func that returns a widget, but it's an event handler.
public MarkdownWidget OnBlock<TBlock>(
    Func<MarkdownBlockContext, TBlock, Hex1bWidget> handler) => /* … */;
```

If you find yourself adding a non-`On*` method that takes more than one widget-producing callback and the suppression message would feel awkward to write, that's a hint to rethink the shape — perhaps the widget itself should expose a child collection, or the method should be split.

## Action callbacks vs. widget builders

Some widgets take an `Action<TWidget>` / `Action<TBuilder>` style configuration callback that mutates a builder rather than returning a widget tree. These are not "widget builders" by the analyzer's definition — they don't return `Hex1bWidget` — and the convention for them is `configure`:

```csharp
public static FormWidget Form<TParent>(
    this WidgetContext<TParent> context,
    Action<FormBuilder> configure)
    => /* … */;
```

Use `builder` only when the callback **returns a widget shape** (a widget, an array of widget, or `IEnumerable<Hex1bWidget>`). Use `configure` when the callback mutates a builder object in place.

## Quick reference

| Scenario | Receiver name | Callback name | Method-name prefix |
|---|---|---|---|
| Extension on `Hex1bTerminalBuilder` | `builder` (the terminal builder itself) | depends | `With*` (required) |
| Extension on `WidgetContext<T>` returning a widget | `context` | `builder` (if exactly one) | bare verb-noun |
| Extension on a widget instance | `widget` | `builder` (if any) | bare verb-noun |
| Event handler on a widget | n/a | `handler` | `On<Verb>` |
| Builder-mutating configuration callback | n/a | `configure` | bare verb-noun |

## Why the rules exist

Every rule on this page exists to remove a small daily friction:

- You can read any widget call site without checking the type of the receiver, because `context` and `widget` always mean the same thing.
- You can spot the boundary between "wiring up the host" and "describing the UI" at a glance, because `With*` only appears on the terminal builder.
- You don't have to remember whether this widget calls its callback `childBuilder`, `contentBuilder`, or `fallbackBuilder` — it's always `builder`.
- When a widget genuinely needs a non-default shape (two builders, an Action callback, an `On*` handler), the deviation carries its own justification, so it doesn't read as an inconsistency.

The analyzers turn these conventions into compile-time guarantees rather than style guidelines, which means the rules can evolve without leaving the codebase littered with stragglers — the build will tell you what's left to update.
