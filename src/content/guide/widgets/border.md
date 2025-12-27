<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode → src/Hex1b.Website/Examples/BorderBasicExample.cs
  - (title example) → src/Hex1b.Website/Examples/BorderTitleExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/border-basic.cs?raw'
import titleSnippet from './snippets/border-title.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.Border(b => [
        b.Text("Welcome to Hex1b!"),
        b.Text(""),
        b.Text("This content is wrapped"),
        b.Text("in a border widget.")
    ])
));

await app.RunAsync();`

const titleCode = `using Hex1b;
using Hex1b.Widgets;

var clickCount = 0;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.Border(b => [
        b.VStack(v => [
            v.Text($"Button clicked {clickCount} times"),
            v.Text(""),
            v.Button("Click me!").OnClick(_ => clickCount++),
            v.Text(""),
            v.Text("Use Tab to focus, Enter to activate")
        ])
    ], title: "Interactive Demo")
));

await app.RunAsync();`
</script>

# BorderWidget

Draw a decorative box border around content.

BorderWidget wraps a single child widget with a visual border made from box-drawing characters. It supports multiple border styles and an optional title displayed in the top border.

## Basic Usage

Create a border around content using the fluent API:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="border-basic" exampleTitle="Border Widget - Basic Usage" />

::: tip Focus Behavior
BorderWidget is not focusable—focus passes through to the child widget inside. This allows interactive widgets like buttons and text boxes to work normally within borders.
:::

## Basic Border

The simplest border wraps content without a title:

<StaticTerminalPreview svgPath="/svg/border-basic.svg" :code="basicSnippet" />

## Border with Title

Add a title to label the bordered section. This interactive example shows a border with a clickable button inside:

<CodeBlock lang="csharp" :code="titleCode" command="dotnet run" example="border-title" exampleTitle="Border Widget - With Title" />

The title is automatically centered in the top border. If the title is too long for the available width, it will be truncated.

## Border Styles

BorderWidget uses box-drawing characters from the active theme. The default theme provides a single-line border (┌─┐│└─┘).

You can customize the border appearance through theming:

```csharp
var theme = Hex1bTheme.Create()
    .Set(BorderTheme.BorderColor, Hex1bColor.Cyan)
    .Set(BorderTheme.TitleColor, Hex1bColor.White)
    .Set(BorderTheme.TopLeftCorner, "╔")
    .Set(BorderTheme.TopRightCorner, "╗")
    .Set(BorderTheme.BottomLeftCorner, "╚")
    .Set(BorderTheme.BottomRightCorner, "╝")
    .Set(BorderTheme.HorizontalLine, "═")
    .Set(BorderTheme.VerticalLine, "║");

var app = new Hex1bApp(options => {
    options.Theme = theme;
}, ctx => /* ... */);
```

## Layout Behavior

BorderWidget adds 2 cells to both width and height (1 for each edge):

- **Measuring**: The border measures its child with constraints reduced by 2 in each dimension
- **Arranging**: The child is positioned inside the border with 1 cell padding on all sides
- **Clipping**: Content that extends beyond the inner area is clipped by default

## Theming

BorderWidget supports these theme elements:

| Element | Type | Default | Description |
|---------|------|---------|-------------|
| `BorderColor` | `Hex1bColor` | Gray | Color of the border lines |
| `TitleColor` | `Hex1bColor` | White | Color of the title text |
| `TopLeftCorner` | `string` | `"┌"` | Top-left corner character |
| `TopRightCorner` | `string` | `"┐"` | Top-right corner character |
| `BottomLeftCorner` | `string` | `"└"` | Bottom-left corner character |
| `BottomRightCorner` | `string` | `"┘"` | Bottom-right corner character |
| `HorizontalLine` | `string` | `"─"` | Horizontal border character |
| `VerticalLine` | `string` | `"│"` | Vertical border character |

## Common Patterns

### Dialog Boxes

Borders are perfect for creating dialog-style interfaces:

```csharp
ctx.Border(b => [
    b.VStack(v => [
        v.Text("Are you sure you want to delete this file?"),
        v.Text(""),
        v.HStack(h => [
            h.Button("Cancel").OnClick(_ => /* ... */),
            h.Text("  "),
            h.Button("Delete").OnClick(_ => /* ... */)
        ])
    ])
], title: "Confirm Delete")
```

### Settings Panels

Group related settings with descriptive borders:

```csharp
ctx.VStack(v => [
    v.Border(b => [
        b.VStack(v2 => [
            v2.Text("Theme: Dark"),
            v2.Text("Font Size: 14"),
            v2.Text("Line Numbers: On")
        ])
    ], title: "Editor Settings"),
    v.Text(""),
    v.Border(b => [
        b.VStack(v2 => [
            v2.Text("Auto-save: Enabled"),
            v2.Text("Backup: Daily")
        ])
    ], title: "File Settings")
])
```

## Related Widgets

- [ThemingPanelWidget](/guide/widgets/theming-panel) - Scope theme changes without a border
- [VStackWidget](/guide/widgets/vstack) - For vertically arranging multiple widgets
- [HStackWidget](/guide/widgets/hstack) - For horizontally arranging multiple widgets
