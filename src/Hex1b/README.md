# Hex1b

**Hex1b** is a .NET library for building TUI apps using an approachable code-first API.

[![NuGet](https://img.shields.io/nuget/v/Hex1b.svg)](https://www.nuget.org/packages/Hex1b)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Installation

```bash
dotnet add package Hex1b
```

## Quick Start

### Hello World

A minimal application that displays text and a quit button:

```csharp
using Hex1b;

using var app = new Hex1bApp(ctx =>
    ctx.VStack(b => [
        b.Text("Hello, Terminal!"),
        b.Button("Quit").OnClick(e => e.Context.RequestStop())
    ])
);

await app.RunAsync();
```

### Counter with State

An interactive counter demonstrating mutable state across renders:

```csharp
using Hex1b;

// State persists outside the builder function
var count = 0;

using var app = new Hex1bApp(ctx =>
    ctx.VStack(b => [
        b.Text($"Count: {count}"),
        b.Button("Increment").OnClick(_ => count++),
        b.Button("Decrement").OnClick(_ => count--),
        b.Text(""),
        b.Button("Quit").OnClick(e => e.Context.RequestStop())
    ])
);

await app.RunAsync();
```

### Counter with Custom Key Bindings

A counter controlled with custom keyboard shortcuts (Ctrl+A to increment, Ctrl+D to decrement):

```csharp
using Hex1b;
using Hex1b.Input;

var count = 0;

using var app = new Hex1bApp(ctx =>
    ctx.VStack(b => [
        b.Text($"Count: {count}"),
        b.Text(""),
        b.Text("Ctrl+A: Increment"),
        b.Text("Ctrl+D: Decrement"),
        b.Text("Ctrl+Q: Quit")
    ]).WithInputBindings(bindings =>
    {
        bindings.Ctrl().Key(Hex1bKey.A).Action(() => count++, "Increment counter");
        bindings.Ctrl().Key(Hex1bKey.D).Action(() => count--, "Decrement counter");
        bindings.Ctrl().Key(Hex1bKey.Q).Action(ctx => ctx.RequestStop(), "Quit");
    })
);

await app.RunAsync();
```

## Layout System

Hex1b uses a constraint-based layout system with size hints:

```csharp
// Vertical stack with flexible sizing
new VStackWidget(
    children: [contentWidget, statusBarWidget],
    sizeHints: [SizeHint.Fill, SizeHint.Content]
);
```

**Size Hints:**
- `SizeHint.Fill` – Expand to fill available space
- `SizeHint.Content` – Size to fit content
- `SizeHint.Fixed(n)` – Fixed size of n units
- `SizeHint.Weighted(n)` – Proportional sizing with weight n

## Widgets

| Widget | Description |
|--------|-------------|
| `TextBlockWidget` | Display static or dynamic text |
| `TextBoxWidget` | Editable text input with cursor and selection |
| `ButtonWidget` | Clickable button with label and action |
| `ListWidget` | Scrollable list with selection support |
| `VStackWidget` | Vertical layout container |
| `HStackWidget` | Horizontal layout container |
| `SplitterWidget` | Resizable split pane layout |
| `BorderWidget` | Container with border and optional title |
| `ScrollPanelWidget` | Scrollable content area |

## Input Bindings

Define keyboard shortcuts using the fluent builder API:

```csharp
widget.WithInputBindings(bindings =>
{
    // Simple key
    bindings.Key(Hex1bKey.Delete).Action(() => DeleteItem());
    
    // Modifier keys (Ctrl or Shift, but not both)
    bindings.Ctrl().Key(Hex1bKey.S).Action(() => Save(), "Save");
    bindings.Shift().Key(Hex1bKey.Tab).Action(() => FocusPrevious(), "Previous");
    
    // Multi-step chords
    bindings.Ctrl().Key(Hex1bKey.K)
        .Then().Key(Hex1bKey.C)
        .Action(() => CommentLine(), "Comment line");
});
```

## Theming

Apply built-in themes or create your own:

```csharp
using Hex1b.Theming;

using var app = new Hex1bApp(
    builder,
    new Hex1bAppOptions { Theme = Hex1bThemes.Sunset }
);
```

## Documentation

- [GitHub Repository](https://github.com/hex1b/hex1b)
- [Sample Applications](https://github.com/hex1b/hex1b/tree/main/samples)
- [API Documentation](https://hex1b.dev)

## License

Hex1b is licensed under the [MIT License](https://opensource.org/licenses/MIT).
