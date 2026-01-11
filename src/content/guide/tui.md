# Terminal User Interfaces

Build rich, interactive terminal user interfaces with Hex1b's React-inspired declarative API. Create dashboards, developer tools, configuration wizards, and CLI experiences that go far beyond simple text output.

## Why Hex1b for TUIs?

Traditional terminal UI libraries require you to manage low-level screen updates, cursor positioning, and state synchronization manually. Hex1b takes a different approach:

- **Declarative API** — Describe *what* you want to render, not *how* to render it
- **Automatic diffing** — Only screen regions that change get updated
- **Constraint-based layout** — Flexible layouts that adapt to terminal size
- **Rich widget library** — Buttons, text boxes, lists, progress bars, and more
- **Theming system** — Consistent styling across your entire application

## Core Concepts

### The Render Loop

Hex1b follows a React-inspired render loop:

```
Build widgets → Reconcile → Measure → Arrange → Render → Wait for input → Repeat
```

You provide a builder function that returns widgets. Hex1b handles the rest:

```csharp
var app = new Hex1bApp(ctx =>
    ctx.Border(b => [
        b.Text("Hello from Hex1b!"),
        b.Button("Click me").OnClick(_ => Console.Beep())
    ], title: "My App")
);

await app.RunAsync();
```

### Widgets vs Nodes

Hex1b separates **configuration** from **rendering**:

| Concept | Purpose | Characteristics |
|---------|---------|-----------------|
| **Widget** | Describes what to render | Immutable, created fresh each render |
| **Node** | Manages state and renders | Mutable, persists across renders |

This separation means your UI description is always a pure function of your state, while internal state (focus, scroll position, etc.) is preserved automatically.

### Layout System

Hex1b uses a constraint-based layout system similar to SwiftUI:

```csharp
ctx.VStack(v => [
    v.Text("Header").Fixed(height: 3),    // Fixed height
    v.List(items).Fill(),                   // Fills remaining space
    v.InfoBar("Footer")                     // Content-sized
])
```

Learn more in the [Layout System](/guide/layout) guide.

## Getting Started

1. **[Getting Started](/guide/getting-started)** — Install and create your first app
2. **[Your First App](/guide/first-app)** — Build a complete todo application
3. **[Widgets & Nodes](/guide/widgets-and-nodes)** — Understand the architecture

## Widget Reference

Explore the full widget library:

### Layout Widgets
- [Stacks (HStack/VStack)](/guide/widgets/stacks) — Horizontal and vertical layouts
- [Border](/guide/widgets/containers) — Bordered containers with titles
- [Align](/guide/widgets/align) — Alignment and positioning
- [Scroll](/guide/widgets/scroll) — Scrollable content areas
- [Splitter](/guide/widgets/splitter) — Resizable split views
- [Responsive](/guide/widgets/responsive) — Breakpoint-based layouts

### Interactive Widgets
- [Button](/guide/widgets/button) — Clickable buttons
- [TextBox](/guide/widgets/textbox) — Text input fields
- [List](/guide/widgets/list) — Selectable lists
- [Picker](/guide/widgets/picker) — Dropdown selection
- [ToggleSwitch](/guide/widgets/toggle-switch) — On/off toggles
- [Navigator](/guide/widgets/navigator) — Page navigation

### Display Widgets
- [Text](/guide/widgets/text) — Rich text display
- [Progress](/guide/widgets/progress) — Progress indicators
- [Hyperlink](/guide/widgets/hyperlink) — Clickable links

### Utility Widgets
- [Rescue](/guide/widgets/rescue) — Error boundaries
- [ThemePanel](/guide/widgets/themepanel) — Theme scoping

## Related Topics

- [Input Handling](/guide/input) — Keyboard navigation and shortcuts
- [Theming](/guide/theming) — Customize appearance
- [Testing](/guide/testing) — Test your TUI applications
