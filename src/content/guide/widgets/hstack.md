<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode → src/Hex1b.Website/Examples/HStackBasicExample.cs
  - fillCode  → src/Hex1b.Website/Examples/HStackFillExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/hstack-basic.cs?raw'
import fillSnippet from './snippets/hstack-fill.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Widgets;

var name = "";
var saved = false;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.HStack(h => [
            h.Text("Name:"),
            h.Text("  "),
            h.TextBox(name)
                .OnTextChanged(args => { name = args.NewText; saved = false; })
                .Fill(),
            h.Text("  "),
            h.Button("Save").OnClick(_ => saved = true)
        ]),
        v.Text(""),
        v.Text(saved ? $"Saved: {name}" : "Enter a name and click Save"),
        v.Text(""),
        v.Text("Use Tab to navigate between fields")
    ])
));

await app.RunAsync();`

const fillCode = `using Hex1b;
using Hex1b.Widgets;

var leftClicks = 0;
var rightClicks = 0;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Two buttons sharing available width equally:"),
        v.Text(""),
        v.HStack(h => [
            h.Border(b => [
                b.VStack(v2 => [
                    v2.Text($"Left: {leftClicks}"),
                    v2.Button("Click").OnClick(_ => leftClicks++)
                ])
            ]).Fill(),
            h.Border(b => [
                b.VStack(v2 => [
                    v2.Text($"Right: {rightClicks}"),
                    v2.Button("Click").OnClick(_ => rightClicks++)
                ])
            ]).Fill()
        ]),
        v.Text(""),
        v.Text("Both borders use .Fill() for equal width")
    ])
));

await app.RunAsync();`
</script>

# HStackWidget

Arrange child widgets horizontally from left to right.

HStackWidget is a layout container that positions its children in a horizontal row. It automatically manages width distribution, allowing children to size based on content, fill available space, or use fixed widths.

## Basic Usage

Create a horizontal layout using the fluent API with collection expression syntax:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="hstack-basic" exampleTitle="HStack Widget - Basic Usage" />

::: tip Focus Management
HStackWidget manages focus for all its descendant widgets. Use **Tab** to move focus forward and **Shift+Tab** to move backward through focusable children.
:::

## Simple Horizontal Layout

Arrange widgets side by side:

<StaticTerminalPreview svgPath="/svg/hstack-basic.svg" :code="basicSnippet" />

All children are positioned from left to right with their natural content widths.

## Sizing Children

HStackWidget supports three sizing modes for children:

### Content Sizing (Default)

Children size to their natural content width:

```csharp
ctx.HStack(h => [
    h.Text("Short"),
    h.Text("Longer Text"),
    h.Text("X")
])
// Each Text widget takes exactly the width it needs
```

### Fill Sizing

Children with `.Fill()` expand to consume remaining space:

<CodeBlock lang="csharp" :code="fillCode" command="dotnet run" example="hstack-fill" exampleTitle="HStack Widget - Fill Sizing" />

When multiple children use `.Fill()`, space is distributed evenly:

```csharp
ctx.HStack(h => [
    h.Text("Left").Fill(),
    h.Text("Right").Fill()
])
// Both children get 50% of available width
```

### Fixed Sizing

Children with `.FixedWidth()` take exactly the specified width:

```csharp
ctx.HStack(h => [
    h.Text("Label").FixedWidth(20),
    h.TextBox(value, onChange).Fill()
])
// Label is always 20 cells wide
```

## Weighted Fill

Use weighted fills to distribute space proportionally:

```csharp
ctx.HStack(h => [
    h.ThemingPanel(theme => theme, sidebar).FillWidth(1),      // Gets 1/3 of space
    h.ThemingPanel(theme => theme, main).FillWidth(2)          // Gets 2/3 of space
])
```

## Layout Algorithm

HStackWidget distributes width in two passes:

1. **Fixed Pass**: Measure content-sized and fixed-width children, sum their widths
2. **Fill Pass**: Distribute remaining width among fill children based on their weights

All children receive the full height of the HStack.

## Focus Navigation

HStackWidget provides default keyboard bindings:

| Key | Action |
|-----|--------|
| Tab | Move focus to next focusable widget |
| Shift+Tab | Move focus to previous focusable widget |

Focus order follows the left-to-right order of children in the array.

## Clipping

Content that extends beyond the HStack's bounds is clipped by default. Children cannot render outside the stack's area.

To allow overflow (not recommended), you would need to configure clipping at a lower level via the layout system.

## Common Patterns

### Form Layouts

Create label-value pairs:

```csharp
ctx.VStack(v => [
    v.HStack(h => [
        h.Text("Name:").FixedWidth(15),
        h.TextBox(state.Name)
            .OnTextChanged(args => state.Name = args.NewText)
            .Fill()
    ]),
    v.HStack(h => [
        h.Text("Email:").FixedWidth(15),
        h.TextBox(state.Email)
            .OnTextChanged(args => state.Email = args.NewText)
            .Fill()
    ])
])
```

### Button Groups

Arrange multiple buttons horizontally:

```csharp
ctx.HStack(h => [
    h.Button("Save").OnClick(_ => Save()),
    h.Text("  "),
    h.Button("Cancel").OnClick(_ => Cancel()),
    h.Text("  "),
    h.Button("Help").OnClick(_ => ShowHelp())
])
```

### Toolbar Layouts

Create toolbars with left and right sections:

```csharp
ctx.HStack(h => [
    h.Text("File Edit View"),
    h.Text("").Fill(),  // Spacer pushes next items to right
    h.Text("User: Admin")
])
```

### Two-Column Layouts

Split content into side-by-side columns:

```csharp
ctx.HStack(h => [
    h.Border(b => [
        b.Text("Sidebar content")
    ]).FixedWidth(30),
    h.Border(b => [
        b.Text("Main content")
    ]).Fill()
])
```

## Combining with VStack

Build complex grid-like layouts:

```csharp
ctx.VStack(v => [
    v.HStack(h => [
        h.Text("A"),
        h.Text("B"),
        h.Text("C")
    ]),
    v.HStack(h => [
        h.Text("D"),
        h.Text("E"),
        h.Text("F")
    ])
])
```

## Performance Considerations

- HStack measures all children during layout
- Content-sized children are measured with unbounded width constraints
- Fill children are measured after fixed space is allocated
- Large numbers of children may impact layout performance

## Related Widgets

- [VStackWidget](/guide/widgets/vstack) - For vertical layouts
- [BorderWidget](/guide/widgets/border) - For adding borders to stack children
- [ThemingPanelWidget](/guide/widgets/theming-panel) - For scoping theme changes to stack children
