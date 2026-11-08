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
    ]).InputBindings(bindings =>
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

using var app = new Hex1bApp(
    builder,
    new Hex1bAppOptions { Theme = Hex1bThemes.Sunset }
);
```

## Publish-time surface pooling opt-out

Temporary surface pooling is enabled by default. Ordinary builds retain the
per-app `Hex1bAppOptions.EnableSurfacePooling` option (default `true`); setting
it to `false` disables reuse but does not by itself remove pooling code.

For short-lived, size-oriented headless text/chart applications, add this item
to the **consuming application's** project file:

```xml
<ItemGroup>
  <RuntimeHostConfigurationOption Include="Hex1b.SurfacePooling.IsSupported"
                                  Value="false" Trim="true" />
</ItemGroup>
```

Publish with .NET 9 or later trimming/Native AOT tooling, for example
`dotnet publish -c Release -r win-x64 -p:PublishAot=true`.
Native AOT requires the platform's native compiler/linker prerequisites.
Hex1b continues to target .NET 8; its internal compatibility attribute lets newer
publish tools recognize this switch without consumer ILC substitutions.

The application-wide switch defaults to enabled. When disabled, rendering uses
unpooled temporary surfaces and `EnableSurfacePooling` is an intentional no-op,
including its default and explicit `true` values. The property still returns its
configured value, not feature availability. Pool sizing options are also ignored.
Runtime options or `AppContext.SetSwitch` cannot restore implementation removed
at publish time. No exception is thrown just for using default options.
Render caching and unrelated array/token pools are unaffected.

Without trimming, the same AppContext switch can disable pooling, but does not
remove code; configure it before constructing applications and do not change it
while they are rendering. Actual native savings depend on the workload and
toolchain, and apply only to otherwise-unused pooling implementation.

## Documentation

- [GitHub Repository](https://github.com/hex1b/hex1b)
- [Sample Applications](https://github.com/hex1b/hex1b/tree/main/samples)
- [API Documentation](https://hex1b.dev)

## License

Hex1b is licensed under the [MIT License](https://opensource.org/licenses/MIT).
