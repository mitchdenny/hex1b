# API Design Guidelines

These guidelines describe how the public API of Hex1b is shaped — and why the shape is the way it is. They apply to the library itself and inform any widgets or extensions you build on top of it.

## Two builders, two patterns

Hex1b has **two distinct fluent surfaces**, and they look different on purpose:

| Surface | Receiver type | Method-name shape | Example |
|---|---|---|---|
| **Terminal builder** | `Hex1bTerminalBuilder` | `With*` | `terminal.WithMouse().WithHex1bApp(...)` |
| **Widget composition** | `WidgetContext<T>` and widget instances | bare verb-noun (no `With*`) | `context.Border(b => [...]).Title("Hi")` |

Both are fluent, but they do different jobs:

- **`Hex1bTerminalBuilder`** wires together the host process — terminal plumbing, presentation/workload adapters, recording, mouse, MCP diagnostics, etc. It is configured **once at startup**, returns the same builder for chaining, and uses `With*` because each call mutates a builder configuration object.
- **Widget composition** describes the **UI tree on every render pass**. Widgets are immutable `record`s, so a "fluent" call returns a new widget value (or constructs a new one in an extension method) — there is nothing to "be `With`'d into". The `With*` prefix is reserved for the terminal builder so that, when you read a chain of calls, you can tell at a glance which surface you're on.

If you find yourself wanting `WithTitle(...)` on a widget, use `Title(...)` instead.

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

| Rule | Convention |
|---|---|
| Widget type names | end in `Widget` |
| Node type names | end in `Node` |
| Widget kind | declared as `record` |
| Node kind | declared as `class` |
| Event handlers on a widget | `On<Verb>` (`OnClick`, `OnSelectionChanged`) |
| Configuration on a widget | bare verb-noun (`Title`, `MaxFloating`, `Disabled`) |
| Never on a widget | `With*` (reserved for `Hex1bTerminalBuilder`) |

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

Two parameter naming conventions apply:

- The `WidgetContext<T>` receiver is always named **`context`**.
- A single widget-builder callback is always named **`builder`**.

So every call site reads the same way:

```csharp
context.Border(b => [
    b.Text("Hello"),
    b.Button("Click me").OnClick(_ => DoStuff())
])
```

Inside the lambda, the parameter is the **child** context (here named `b` for brevity). Lambda variable names are unconstrained — the convention only governs the names declared in the extension method signature, so callers can use whatever short alias they like.

## Widget instance extensions

Some operations apply to an existing widget rather than building a new one — for example, decorating an editor with a language server, or adding input bindings to anything. These are extensions on a widget instance, not on a context:

```csharp
public static EditorWidget LanguageServer(
    this EditorWidget widget,
    Action<LanguageServerConfiguration> configure)
    => /* … */;
```

When the receiver is a `Hex1bWidget`-derived type (or a generic constrained to `Hex1bWidget`), it is named **`widget`**, never `editor`, `target`, `self`, or a role-name. This keeps every widget-on-widget operation reading the same way regardless of which widget it acts on:

```csharp
context.Editor(state)
       .LanguageServer(workspace, "file:///app/Program.cs")
       .Decorations(provider);
```

## One builder per method

Most widget-builder methods take exactly one builder callback. A handful of widgets — splitters, for example — genuinely need two, one per pane:

```csharp
public static SplitterWidget HSplitter<TParent>(
    this WidgetContext<TParent> context,
    Func<WidgetContext<VStackWidget>, Hex1bWidget[]> leftBuilder,
    Func<WidgetContext<VStackWidget>, Hex1bWidget[]> rightBuilder,
    int leftWidth = 30)
    where TParent : Hex1bWidget
    => /* … */;
```

When you have a choice, prefer a single `builder` that returns multiple children (e.g. `Func<…, Hex1bWidget[]>`) over multiple positional builders. Splitters are the exception because the two panes have different roles, not just different positions — so they get role-named builders (`leftBuilder` / `rightBuilder`, `topBuilder` / `bottomBuilder`).

### `On*` methods are not builders

Event-handler decorators sometimes take a callback that returns a widget — they fire in response to an event rather than participating in the widget tree's static composition, so they don't follow the `builder` convention:

```csharp
// OnBlock takes a Func that returns a widget, but it's an event handler.
public MarkdownWidget OnBlock<TBlock>(
    Func<MarkdownBlockContext, TBlock, Hex1bWidget> handler) => /* … */;
```

If you find yourself adding a non-`On*` method that takes more than one widget-producing callback, that's a hint to rethink the shape — perhaps the widget itself should expose a child collection, or the method should be split.

## Action callbacks vs. widget builders

Some widgets take an `Action<TWidget>` / `Action<TBuilder>` style configuration callback that mutates a builder rather than returning a widget tree. These are not "widget builders" — they don't return `Hex1bWidget` — and the convention for them is `configure`:

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
- When a widget genuinely needs a non-default shape (two builders, an Action callback, an `On*` handler), the deviation is small enough to read as a deliberate exception rather than an inconsistency.
