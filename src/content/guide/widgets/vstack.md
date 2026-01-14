<script setup>
import basicSnippet from './snippets/vstack-basic.cs?raw'
import fillSnippet from './snippets/vstack-fill.cs?raw'

const basicCode = `using Hex1b;

var state = new AppState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Welcome to My App"),
        v.Text(""),
        v.Button("Start").OnClick(_ => state.Start()),
        v.Button("Settings").OnClick(_ => state.ShowSettings()),
        v.Button("Quit").OnClick(args => args.Context.RequestStop())
    ]))
    .Build();

await terminal.RunAsync();

class AppState
{
    public void Start() { /* ... */ }
    public void ShowSettings() { /* ... */ }
}`
</script>

# VStackWidget

Arrange child widgets vertically from top to bottom.

VStackWidget is a layout container that positions its children in a vertical column. It automatically manages height distribution, allowing children to size based on content, fill available space, or use fixed heights.

## Basic Usage

Create a vertical layout using the fluent API with collection expression syntax:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" />

::: tip Focus Management
VStackWidget manages focus for all its descendant widgets. Use **Tab** to move focus forward and **Shift+Tab** to move backward through focusable children.
:::

## Simple Vertical Layout

Stack widgets top to bottom:

<StaticTerminalPreview svgPath="/svg/vstack-basic.svg" :code="basicSnippet" />

All children are positioned from top to bottom with their natural content heights.

## Sizing Children

VStackWidget supports three sizing modes for children:

### Content Sizing (Default)

Children size to their natural content height:

```csharp
ctx.VStack(v => [
    v.Text("Single line"),
    v.Text("Text that wraps to multiple lines when the width is constrained").Wrap(),
    v.Text("Another single line")
])
// Each widget takes exactly the height it needs
```

### Fill Sizing

Children with `.Fill()` expand to consume remaining space:

<StaticTerminalPreview svgPath="/svg/vstack-fill.svg" :code="fillSnippet" />

This is commonly used for scrollable content areas that should take all remaining vertical space.

When multiple children use `.Fill()`, space is distributed evenly:

```csharp
ctx.VStack(v => [
    v.Panel(top).Fill(),
    v.Panel(bottom).Fill()
])
// Both panels get 50% of available height
```

### Fixed Sizing

Children with `.FixedHeight()` take exactly the specified height:

```csharp
ctx.VStack(v => [
    v.Text("Header").FixedHeight(3),
    v.List(items, onSelect).Fill(),
    v.Text("Footer").FixedHeight(1)
])
// Header is always 3 rows, footer always 1 row
```

## Weighted Fill

Use weighted fills to distribute space proportionally:

```csharp
ctx.VStack(v => [
    v.Panel(header).FillHeight(1),      // Gets 1/4 of space
    v.Panel(main).FillHeight(3)         // Gets 3/4 of space
])
```

## Layout Algorithm

VStackWidget distributes height in two passes:

1. **Fixed Pass**: Measure content-sized and fixed-height children, sum their heights
2. **Fill Pass**: Distribute remaining height among fill children based on their weights

All children receive the full width of the VStack.

## Focus Navigation

VStackWidget provides default keyboard bindings:

| Key | Action |
|-----|--------|
| Tab | Move focus to next focusable widget |
| Shift+Tab | Move focus to previous focusable widget |

Focus order follows the top-to-bottom order of children in the array.

## Clipping

Content that extends beyond the VStack's bounds is clipped by default. Children cannot render outside the stack's area.

To allow overflow (not recommended), you would need to configure clipping at a lower level via the layout system.

## Common Patterns

### Application Layouts

Create header-content-footer layouts:

```csharp
ctx.VStack(v => [
    v.Text("═══ My Application ═══"),
    v.Text(""),
    v.Border(b => [
        b.Text("Main content area")
    ]).Fill(),
    v.Text(""),
    v.Text("Status: Ready")
])
```

### Menu Lists

Stack menu items vertically:

```csharp
ctx.Border(b => [
    b.VStack(v => [
        v.Text("Main Menu"),
        v.Text(""),
        v.Button("New File").OnClick(_ => NewFile()),
        v.Button("Open File").OnClick(_ => OpenFile()),
        v.Button("Save File").OnClick(_ => SaveFile()),
        v.Text(""),
        v.Button("Exit").OnClick(args => args.Context.RequestStop())
    ])
], title: "File")
```

### Form Layouts

Arrange form fields vertically:

```csharp
ctx.VStack(v => [
    v.Text("User Registration"),
    v.Text(""),
    v.Text("Name:"),
    v.TextBox(state.Name).OnTextChanged(args => state.Name = args.NewText),
    v.Text(""),
    v.Text("Email:"),
    v.TextBox(state.Email).OnTextChanged(args => state.Email = args.NewText),
    v.Text(""),
    v.HStack(h => [
        h.Button("Submit").OnClick(_ => state.Submit()),
        h.Text("  "),
        h.Button("Cancel").OnClick(_ => state.Cancel())
    ])
])
```

### Split Views

Create vertically split content areas:

```csharp
ctx.Border(b => [
    b.VStack(v => [
        v.Border(b2 => [
            b2.Text("Top Panel")
        ]).FillHeight(2),
        v.Border(b2 => [
            b2.Text("Bottom Panel (larger)")
        ]).FillHeight(3)
    ])
])
```

## Text Wrapping Behavior

When a TextWidget inside a VStack has `.Wrap()` enabled:

1. VStack passes its current width constraint to the text widget
2. Text widget measures how many lines it needs at that width
3. VStack includes the wrapped height in its total height calculation

This ensures wrapped text fits properly in the layout:

```csharp
ctx.VStack(v => [
    v.Text("Title"),
    v.Text(""),
    v.Text(
        "This long description will wrap to multiple lines " +
        "based on the available width of the VStack."
    ).Wrap()
])
```

## Combining with HStack

Build complex grid-like layouts:

```csharp
ctx.VStack(v => [
    v.HStack(h => [
        h.Border(b => [b.Text("A")]).Fill(),
        h.Border(b => [b.Text("B")]).Fill()
    ]).FixedHeight(5),
    v.HStack(h => [
        h.Border(b => [b.Text("C")]).Fill(),
        h.Border(b => [b.Text("D")]).Fill()
    ]).Fill()
])
```

## Performance Considerations

- VStack measures all children during layout
- Content-sized children are measured with the stack's width and unbounded height
- Fill children are measured after fixed space is allocated
- Text wrapping calculations can impact performance with many wrapped widgets

## Related Widgets

- [HStackWidget](/guide/widgets/hstack) - For horizontal layouts
- [BorderWidget](/guide/widgets/border) - For adding borders to stack children
- [ThemePanelWidget](/guide/widgets/themepanel) - For scoped theming of child widgets
