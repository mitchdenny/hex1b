# Hex1b

**Hex1b** is a .NET library for building rich, interactive terminal user interfaces (TUI) with a React-inspired declarative API. Create beautiful console applications with widgets, layouts, theming, and more.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## ✨ Features

- **Declarative UI** - Build UIs using a widget tree pattern inspired by React and Flutter
- **Widget Library** - TextBlock, TextBox, Button, List, VStack, HStack, Splitter, and more
- **Layout System** - Flexible constraint-based layout with size hints (Fill, Content, Fixed)
- **Theming** - Built-in themes with customizable colors and styles
- **Input Handling** - Keyboard navigation, focus management, and shortcut bindings
- **Reconciliation** - Efficient diff-based updates to minimize terminal redraws

## 📦 Installation

```bash
dotnet add package Hex1b
```

## 🚀 Quick Start

Here's a simple "Hello World" application:

```csharp
using Hex1b;
using Hex1b.Widgets;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

using var app = new Hex1bApp(
    ctx => Task.FromResult<Hex1bWidget>(
        new VStackWidget([
            new TextBlockWidget("Hello, Terminal!"),
            new ButtonWidget("Exit", () => cts.Cancel())
        ])
    )
);

await app.RunAsync(cts.Token);
```

## 🎨 Widgets

Hex1b provides a variety of built-in widgets:

| Widget | Description |
|--------|-------------|
| `TextBlockWidget` | Display static or dynamic text |
| `TextBoxWidget` | Editable text input with cursor and selection |
| `ButtonWidget` | Clickable button with label and action |
| `ListWidget` | Scrollable list with selection |
| `VStackWidget` | Vertical layout container |
| `HStackWidget` | Horizontal layout container |
| `SplitterWidget` | Resizable split pane layout |

## 📐 Layout System

Hex1b uses a constraint-based layout system with size hints:

```csharp
// Children with size hints: first fills available space, second sizes to content
new VStackWidget(
    [contentWidget, statusBarWidget],
    [SizeHint.Fill, SizeHint.Content]
);
```

**Size Hints:**
- `SizeHint.Fill` - Expand to fill available space
- `SizeHint.Content` - Size to fit content
- `SizeHint.Fixed(n)` - Fixed size of n units

## 🎹 Input Bindings

Define keyboard bindings at any level of your widget tree:

```csharp
var widget = new SplitterWidget(left, right, 25) with
{
    InputBindings = [
        InputBinding.Ctrl(Hex1bKey.S, Save, "Save"),
        InputBinding.Ctrl(Hex1bKey.Q, Quit, "Quit"),
    ]
};
```

> **Picking portable bindings.** Different terminals intercept different
> combos before they reach Hex1b (e.g. Windows Terminal eats `Ctrl+Shift+↑/↓`
> for scroll, GNOME Terminal eats `Ctrl+Shift+T/N/W` for tab/window
> management). See [`docs/keybinding-portability.md`](./docs/keybinding-portability.md)
> for the per-terminal interception matrix and recommendations, and use the
> [`KeyBindingTester`](./samples/KeyBindingTester/README.md) sample to confirm
> what actually fires on your target terminals.

## 🎨 Theming

Apply built-in themes or create your own:

```csharp
using var app = new Hex1bApp(builder, new Hex1bAppOptions { Theme = Hex1bThemes.Sunset });
```

## 🏗️ Architecture

Hex1b follows a widget/node separation pattern:

1. **Widgets** - Immutable configuration objects describing what to render
2. **Nodes** - Mutable render objects that manage state and layout
3. **Reconciliation** - Efficient diffing to update nodes when widgets change

```
┌─────────────────────────────────────────────────────────────┐
│                      Hex1bApp.RunAsync()                    │
├─────────────────────────────────────────────────────────────┤
│  1. Build widget tree (your code)                           │
│  2. Reconcile → Update node tree                            │
│  3. Layout → Measure and arrange nodes                      │
│  4. Render → Draw to terminal                               │
│  5. Wait for input → Dispatch to focused node               │
│  6. Repeat from step 1                                      │
└─────────────────────────────────────────────────────────────┘
```

## 🧪 Samples

The `samples/` directory contains example applications demonstrating various features:

- **Cancellation** - Master-detail contact editor with save/cancel functionality

### Running Samples with Aspire

This repository is set up with [Aspire](https://aspire.dev/) to make it easy to run and test sample applications:

```bash
dotnet run --project apphost.cs
```

> **Note**: Aspire doesn't natively support interactive terminal applications, but this project explores techniques to make TUI app development and testing in Aspire possible.

## 🛠️ Development

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) (preview)
- A terminal emulator with good ANSI support

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Project Structure

```
hex1b/
├── src/
│   └── Hex1b/              # Main library
│       ├── Layout/         # Constraint-based layout system
│       ├── Nodes/          # Render nodes (mutable, stateful)
│       ├── Widgets/        # Widget definitions (immutable config)
│       ├── Theming/        # Theme system and built-in themes
│       └── Input/          # Keyboard input and bindings
├── samples/                # Example applications
├── tests/
│   └── Hex1b.Tests/        # Unit tests
└── apphost.cs              # Aspire app host for running samples
```

## 🤝 Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

For AI coding agents, see [AGENTS.md](AGENTS.md) for context and conventions.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔗 Related Projects

- [Spectre.Console](https://spectreconsole.net/) - Beautiful console output
- [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) - Cross-platform terminal UI toolkit
- [Aspire](https://aspire.dev/) - Cloud-ready stack for .NET

