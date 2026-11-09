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

await using var terminal = Hex1bTerminal.CreateBuilder().WithHex1bApp(ctx =>
    ctx.VStack(b => [
        b.Text("Hello, Terminal!"),
        b.Button("Quit").OnClick(e => e.Context.RequestStop())
    ])
).Build();

await terminal.RunAsync();
```

### Counter with State

An interactive counter demonstrating mutable state across renders:

```csharp
using Hex1b;

// State persists outside the builder function
var count = 0;

await using var terminal = Hex1bTerminal.CreateBuilder().WithHex1bApp(ctx =>
    ctx.VStack(b => [
        b.Text($"Count: {count}"),
        b.Button("Increment").OnClick(_ => count++),
        b.Button("Decrement").OnClick(_ => count--),
        b.Text(""),
        b.Button("Quit").OnClick(e => e.Context.RequestStop())
    ])
).Build();

await terminal.RunAsync();
```

### Counter with Custom Key Bindings

A counter controlled with custom keyboard shortcuts (Ctrl+A to increment, Ctrl+D to decrement):

```csharp
using Hex1b;
using Hex1b.Input;

var count = 0;

await using var terminal = Hex1bTerminal.CreateBuilder().WithHex1bApp(ctx =>
    ctx.VStack(b => [
        b.Text($"Count: {count}"),
        b.Text(""),
        b.Text("Ctrl+A: Increment"),
        b.Text("Ctrl+D: Decrement"),
        b.Text("Ctrl+Q: Quit")
    ]).InputBindings(bindings =>
    {
        bindings.Ctrl().Key(Hex1bKey.A).Action(() => count++, "Increment counter");
        bindings.Ctrl().Key(Hex1bKey.D).Action(() => count--, "Decrement counter");
        bindings.Ctrl().Key(Hex1bKey.Q).Action(ctx => ctx.RequestStop(), "Quit");
    })
).Build();

await terminal.RunAsync();
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
widget.InputBindings(bindings =>
{
    // Simple key
    bindings.Key(Hex1bKey.Delete).Action(() => DeleteItem());
    
    // Modifier keys (any combination)
    bindings.Ctrl().Key(Hex1bKey.S).Action(() => Save(), "Save");
    bindings.Shift().Key(Hex1bKey.Tab).Action(() => FocusPrevious(), "Previous");
    bindings.Ctrl().Shift().Key(Hex1bKey.S).Action(() => SaveAs(), "Save as");
    
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

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => options.Theme = Hex1bThemes.Sunset, builder)
    .Build();
await terminal.RunAsync();
```

## Construction and Native AOT

Construct applications through `Hex1bTerminal`; `Hex1bApp` constructors are
internal. The terminal owns the app lifetime. Use the `WithHex1bApp` options
callback for app configuration and its app-instance callback when you need
`Invalidate()` or `RequestStop()` outside widget event handlers.

`CreateBuilder()` keeps the normal interactive console defaults.
`CreateSlimBuilder()` registers neither presentation nor workload; explicitly
configure both. For headless Native AOT applications:

```csharp
await using var terminal = Hex1bTerminal.CreateSlimBuilder()
    .WithHeadless()
    .WithHex1bApp(ctx => ctx.Text("Hello"))
    .Build();
await terminal.RunAsync();
```

Calling `WithHeadless()` on the normal builder replaces its presentation at
runtime but can retain console code in a Native AOT binary. The slim entry
point avoids registering that console factory in the first place.

## Documentation

- [GitHub Repository](https://github.com/hex1b/hex1b)
- [Sample Applications](https://github.com/hex1b/hex1b/tree/main/samples)
- [API Documentation](https://hex1b.dev)

## License

Hex1b is licensed under the [MIT License](https://opensource.org/licenses/MIT).
