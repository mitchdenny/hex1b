<script setup>
import basicSnippet from './snippets/panel-basic.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.Panel(
        ctx.VStack(v => [
            v.Text("Panel provides a styled"),
            v.Text("background for content.")
        ])
    )
));

await app.RunAsync();`
</script>

# PanelWidget

Provide a styled background for content.

PanelWidget wraps a single child widget with a background color, creating visual separation between UI sections. Unlike BorderWidget, it doesn't add decorative borders—just a solid background fill.

## Basic Usage

Create a panel with a background using the fluent API:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" />

::: tip Focus Behavior
PanelWidget is not focusable—focus passes through to the child widget inside. This allows interactive widgets to work normally within panels.
:::

## Basic Panel

A simple panel with default theming:

<StaticTerminalPreview svgPath="/svg/panel-basic.svg" :code="basicSnippet" />

The panel fills its entire bounds with the configured background color and renders the child widget on top.

## Layout Behavior

PanelWidget doesn't add any size—the child takes all available space:

- **Measuring**: The child is measured with the full constraints passed to the panel
- **Arranging**: The child gets the full bounds of the panel
- **Background**: The panel fills its entire bounds before rendering the child

## Theming

PanelWidget supports these theme elements:

| Element | Type | Default | Description |
|---------|------|---------|-------------|
| `ForegroundColor` | `Hex1bColor` | Default | Text color inherited by child widgets |
| `BackgroundColor` | `Hex1bColor` | Default | Background fill color |

### Customizing Panel Colors

```csharp
var theme = Hex1bTheme.Create()
    .Set(PanelTheme.BackgroundColor, Hex1bColor.DarkBlue)
    .Set(PanelTheme.ForegroundColor, Hex1bColor.White);

var app = new Hex1bApp(options => {
    options.Theme = theme;
}, ctx => 
    ctx.Panel(
        ctx.Text("White text on dark blue background")
    )
);
```

## Color Inheritance

PanelWidget sets the **inherited colors** for its child widgets:

- Child widgets without explicit colors inherit the panel's foreground/background
- This creates consistent styling for all content within the panel
- Explicit colors on child widgets override the inherited values

```csharp
ctx.Panel(
    ctx.VStack(v => [
        v.Text("This inherits panel colors"),
        v.Text("This also inherits panel colors"),
        v.Text("Custom color").Foreground(Hex1bColor.Red)  // Overrides
    ])
)
```

## Common Patterns

### Highlighting Sections

Use panels to visually separate different areas of your UI:

```csharp
ctx.VStack(v => [
    v.Panel(
        v.Text("Header Section")
    ),
    v.Text(""),
    v.Text("Main content area"),
    v.Text(""),
    v.Panel(
        v.Text("Footer Section")
    )
])
```

### Information Boxes

Create attention-grabbing information displays:

```csharp
ctx.Panel(
    ctx.VStack(v => [
        v.Text("⚠ Warning"),
        v.Text(""),
        v.Text("This action cannot be undone."),
        v.Text("Please confirm before proceeding.")
    ])
)
```

### Combining with Borders

Panels and borders work well together for layered styling:

```csharp
ctx.Border(b => [
    b.Panel(
        b.VStack(v => [
            v.Text("Title"),
            v.Text(""),
            v.Text("Content with both border and background")
        ])
    )
], title: "Styled Panel")
```

## Comparison with BorderWidget

| Feature | PanelWidget | BorderWidget |
|---------|-------------|--------------|
| Visual decoration | Background fill | Box border |
| Size overhead | None | +2 width, +2 height |
| Title support | No | Yes |
| Best for | Background colors | Visual grouping |

## Related Widgets

- [BorderWidget](/guide/widgets/border) - For decorative borders around content
- [VStackWidget](/guide/widgets/vstack) - For vertically arranging multiple widgets
- [HStackWidget](/guide/widgets/hstack) - For horizontally arranging multiple widgets
