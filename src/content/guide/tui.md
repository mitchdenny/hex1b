# Terminal User Interfaces

Build rich, interactive terminal user interfaces with Hex1b's React-inspired declarative API. Create dashboards, developer tools, configuration wizards, and CLI experiences that go far beyond simple text output.

## Why Hex1b for TUIs?

Traditional terminal UI libraries require you to manage low-level screen updates, cursor positioning, and state synchronization manually. Hex1b takes a different approach:

- **Declarative API** — Describe *what* you want to render, not *how* to render it
- **Automatic diffing** — Only screen regions that change get updated
- **Constraint-based layout** — Flexible layouts that adapt to terminal size
- **Rich widget library** — Buttons, text boxes, lists, progress bars, and more
- **Theming system** — Consistent styling across your entire application

## The Basics

You provide a builder function that returns widgets. Hex1b handles the rest:

```csharp
await Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.Border(b => [
            b.Text("Hello from Hex1b!"),
            b.Button("Click me").OnClick(_ => Console.Beep())
        ], title: "My App"))
    .RunAsync();
```

## Building TUIs

1. **[Your First App](/guide/getting-started)** — Install Hex1b and create your first app
2. **[Widgets & Nodes](/guide/widgets-and-nodes)** — Understand the architecture
3. **[Layout System](/guide/layout)** — Master constraint-based layouts
4. **[Input Handling](/guide/input)** — Keyboard navigation and shortcuts
5. **[Theming](/guide/theming)** — Customize appearance

## Reference

- **[Widgets](/guide/widgets/)** — Explore the full widget library

## Related Topics

- [Testing](/guide/testing) — Test your TUI applications
