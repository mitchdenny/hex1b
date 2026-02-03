<script setup>
const basicCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.HStack(h => [
        h.Icon("🏠"),
        h.Text(" Home"),
        h.Text("  |  "),
        h.Icon("⚙️"),
        h.Text(" Settings"),
        h.Text("  |  "),
        h.Icon("❓"),
        h.Text(" Help")
    ]))
    .Build();

await terminal.RunAsync();`

const clickableCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Click an icon:"),
        v.Text(""),
        v.HStack(h => [
            h.Icon("▶️").OnClick(_ => Console.WriteLine("Play!")),
            h.Text(" "),
            h.Icon("⏸️").OnClick(_ => Console.WriteLine("Pause!")),
            h.Text(" "),
            h.Icon("⏹️").OnClick(_ => Console.WriteLine("Stop!"))
        ])
    ]))
    .Build();

await terminal.RunAsync();`
</script>

# Icon

The Icon widget displays a single character or short string (like an emoji) that can optionally respond to clicks.

## Basic Usage

Use the `Icon()` extension method to display icons inline with other content.

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="icon-basic" exampleTitle="Icon Widget - Basic Usage" />

**Key features:**
- Display emoji, Unicode symbols, or short text
- Automatically measures to single-character width (or emoji width)
- Lightweight widget for decorative or indicator purposes

## Clickable Icons

Attach an `OnClick()` handler to make icons interactive.

<CodeBlock lang="csharp" :code="clickableCode" command="dotnet run" example="icon-click" exampleTitle="Icon Widget - Clickable Icons" />

When a click handler is attached, the icon:
- Responds to mouse clicks
- Becomes focusable for keyboard navigation
- Can be activated with Enter/Space when focused

## API Reference

| Method | Description |
|--------|-------------|
| `Icon(string)` | Create an icon with the specified character/emoji |
| `OnClick(handler)` | Handle click events (sync or async) |

## Common Patterns

### Status Indicators

```csharp
ctx.HStack(h => [
    h.Icon(status.IsOnline ? "🟢" : "🔴"),
    h.Text($" {status.Name}")
])
```

### Action Buttons

```csharp
ctx.HStack(h => [
    h.Icon("✏️").OnClick(_ => Edit()),
    h.Text(" "),
    h.Icon("🗑️").OnClick(_ => Delete())
])
```

### With Tree Items

Icons are commonly used with Tree items to indicate file types:

```csharp
t.Item("Documents").Icon("📁")
t.Item("README.md").Icon("📄")
```

## Related Widgets

- [Button](/guide/widgets/button) — Full button with label and keyboard support
- [Text](/guide/widgets/text) — Display text content
- [Tree](/guide/widgets/tree) — Tree items with icon prefixes
