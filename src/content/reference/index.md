# API Reference

This section contains automatically generated API documentation from the Hex1b library's XML doc comments.

## Namespaces

Browse the API by namespace:

### Core

| Namespace | Description |
|-----------|-------------|
| [Hex1b](Hex1b.md) | Core application types, extension methods, and render context |

### Widgets & Nodes

| Namespace | Description |
|-----------|-------------|
| [Hex1b.Widgets](Hex1b.Widgets.md) | Widget records that describe UI components |
| [Hex1b.Nodes](Hex1b.Nodes.md) | Mutable node classes that manage state and rendering |

### Layout

| Namespace | Description |
|-----------|-------------|
| [Hex1b.Layout](Hex1b.Layout.md) | Layout primitives: Constraints, Size, Rect, SizeHint |

### Input & Events

| Namespace | Description |
|-----------|-------------|
| [Hex1b.Input](Hex1b.Input.md) | Input handling, key bindings, and input routing |
| [Hex1b.Events](Hex1b.Events.md) | Event argument types for widget interactions |

### Theming

| Namespace | Description |
|-----------|-------------|
| [Hex1b.Theming](Hex1b.Theming.md) | Theme system for customizing colors and characters |
| [Hex1b.Tokens](Hex1b.Tokens.md) | Design tokens for consistent theming |

### Terminal

| Namespace | Description |
|-----------|-------------|
| [Hex1b.Terminal](Hex1b.Terminal.md) | Terminal abstraction layer and implementations |

---

## Quick Links

### Core Types

- [Hex1bApp](Hex1b.Hex1bApp.md) — Main application class that runs the TUI event loop
- [WidgetContext\<TParent\>](Hex1b.WidgetContext-1.md) — Provides access to app state within the widget builder
- [Hex1bAppOptions](Hex1b.Hex1bAppOptions.md) — Configuration for the Hex1b application

### Common Widgets

- [TextBlockWidget](Hex1b.Widgets.TextBlockWidget.md) — Display static or dynamic text
- [ButtonWidget](Hex1b.Widgets.ButtonWidget.md) — Clickable button with focus support
- [TextBoxWidget](Hex1b.Widgets.TextBoxWidget.md) — Text input field
- [ListWidget](Hex1b.Widgets.ListWidget.md) — Selectable list of items
- [VStackWidget](Hex1b.Widgets.VStackWidget.md) — Vertical stack layout
- [HStackWidget](Hex1b.Widgets.HStackWidget.md) — Horizontal stack layout
- [BorderWidget](Hex1b.Widgets.BorderWidget.md) — Draw borders around content

### Layout Types

- [Constraints](Hex1b.Layout.Constraints.md) — Min/max width/height bounds for layout
- [Size](Hex1b.Layout.Size.md) — Measured dimensions
- [Rect](Hex1b.Layout.Rect.md) — Position and size for arrangement
- [SizeHint](Hex1b.Layout.SizeHint.md) — Fill, Content, or Fixed sizing

### Extension Methods

Extension methods are documented on their defining class and also listed in the "Extension Methods" section of each type they extend:

- [ButtonExtensions](Hex1b.ButtonExtensions.md) — Button widget factory methods
- [TextExtensions](Hex1b.TextExtensions.md) — Text widget factory methods
- [LayoutExtensions](Hex1b.LayoutExtensions.md) — Layout configuration methods
- [SizeHintExtensions](Hex1b.SizeHintExtensions.md) — Width/Height sizing methods
- [InputBindingExtensions](Hex1b.InputBindingExtensions.md) — Custom input binding methods

---

::: tip Using the API Reference
Each type page includes:
- **Syntax** — The type signature
- **Inheritance** — Base types and interfaces
- **Extension Methods** — Methods from extension classes that apply to this type
- **Members** — Constructors, properties, methods, events
:::
