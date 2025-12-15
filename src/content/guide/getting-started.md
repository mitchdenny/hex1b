# Getting Started

Hex1b is a .NET library for building terminal user interfaces (TUI) with a React-inspired declarative API.

## Installation

Install Hex1b from NuGet:

```bash
dotnet add package Hex1b
```

## Prerequisites

- .NET 8.0 or later
- A terminal that supports ANSI escape sequences (most modern terminals do)

## Your First App

Create a simple "Hello World" terminal app:

```csharp
using Hex1b;

// Create the app with no state (using object as placeholder)
var app = new Hex1bApp<object>(
    initialState: new object(),
    buildWidget: (ctx, ct) => new TextBlockWidget("Hello, Hex1b!")
);

// Run the app
await app.RunAsync();
```

Press `Ctrl+C` to exit.

## Interactive Example

Here's a slightly more interesting example with state:

```csharp
using Hex1b;

var app = new Hex1bApp<int>(
    initialState: 0,
    buildWidget: (ctx, ct) => 
        new BorderWidget(
            new VStackWidget([
                new TextBlockWidget($"Button pressed {ctx.State} times"),
                new ButtonWidget("Click me!", () => ctx.SetState(ctx.State + 1))
                    .OnKey(Hex1bKey.Escape, () => Environment.Exit(0))
            ])
        ).Title("Counter Demo")
);

await app.RunAsync();
```

<TerminalDemo exhibit="hello-world" title="Hello World Demo" />

## Next Steps

- [Your First App](/guide/first-app) - Build a complete app step by step
- [Widgets & Nodes](/guide/widgets-and-nodes) - Understand the core architecture
- [Layout System](/guide/layout) - Learn how to arrange widgets
