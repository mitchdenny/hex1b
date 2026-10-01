<script setup>
import centerSnippet from './snippets/align-center.cs?raw'
import bottomRightSnippet from './snippets/align-bottom-right.cs?raw'
import topCenterSnippet from './snippets/align-top-center.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.Align(Alignment.Center,
            b.Text("Hello, World!")
        )
    ], title: "Centered Content"))
    .Build();

await terminal.RunAsync();`

const demoCode = `using Hex1b;

var selectedAlignment = Alignment.Center;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.HSplitter(
        // Left panel: alignment selector
        ctx.Border(b => [
            b.List(["Top Left", "Top Center", "Top Right",
                    "Left Center", "Center", "Right Center",
                    "Bottom Left", "Bottom Center", "Bottom Right"])
                .OnSelectionChanged(e => {
                    selectedAlignment = e.SelectedIndex switch {
                        0 => Alignment.TopLeft,
                        1 => Alignment.TopCenter,
                        2 => Alignment.TopRight,
                        3 => Alignment.LeftCenter,
                        4 => Alignment.Center,
                        5 => Alignment.RightCenter,
                        6 => Alignment.BottomLeft,
                        7 => Alignment.BottomCenter,
                        8 => Alignment.BottomRight,
                        _ => Alignment.Center
                    };
                })
        ], title: "Alignments"),
        // Right panel: preview
        ctx.Border(b => [
            b.Align(selectedAlignment,
                b.Border(inner => [
                    inner.Text("Content")
                ])
            )
        ], title: "Preview"),
        leftWidth: 22
    ))
    .Build();

await terminal.RunAsync();`
</script>

# AlignWidget

Position child content within available space using alignment flags.

AlignWidget allows you to align a child widget horizontally (left, center, right) and/or vertically (top, center, bottom) within the available layout space. It's particularly useful for positioning content within containers like borders or panels.

## Interactive Demo

Try selecting different alignments to see how they position content:

<CodeBlock lang="csharp" :code="demoCode" command="dotnet run" example="align-demo" exampleTitle="Align Widget - Interactive Demo" />

## Basic Usage

Create an aligned widget using the fluent API:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" />

::: tip Convenience Method
For the common case of centering content, use the `Center()` method directly:
```csharp
ctx.Center(ctx.Text("Centered!"))
```
:::

## Alignment Options

AlignWidget uses the `Alignment` flags enum, which supports both individual axis alignment and convenient combinations.

### Horizontal Alignment

| Value | Description |
|-------|-------------|
| `Left` | Align to the left edge (default) |
| `Right` | Align to the right edge |
| `HCenter` | Center horizontally |

### Vertical Alignment

| Value | Description |
|-------|-------------|
| `Top` | Align to the top edge (default) |
| `Bottom` | Align to the bottom edge |
| `VCenter` | Center vertically |

### Combination Flags

For convenience, common combinations are predefined:

| Value | Equivalent To |
|-------|---------------|
| `Center` | `HCenter \| VCenter` |
| `TopLeft` | `Top \| Left` |
| `TopRight` | `Top \| Right` |
| `TopCenter` | `Top \| HCenter` |
| `BottomLeft` | `Bottom \| Left` |
| `BottomRight` | `Bottom \| Right` |
| `BottomCenter` | `Bottom \| HCenter` |
| `LeftCenter` | `Left \| VCenter` |
| `RightCenter` | `Right \| VCenter` |

## Examples

### Center Alignment

```csharp
ctx.Align(Alignment.Center, ctx.Text("Centered!"))
```

### Bottom Right Alignment

```csharp
ctx.Align(Alignment.BottomRight, ctx.Text("Bottom Right"))
```

### Custom Combination

You can combine flags for custom alignments:

```csharp
// Center horizontally at the bottom
ctx.Align(Alignment.Bottom | Alignment.HCenter, ctx.Text("Footer"))
```

## Layout Behavior

AlignWidget consumes all available space and positions its child within that space:

- **Measuring**: The child is measured with loose constraints, then the align widget reports the full available size
- **Arranging**: The child is positioned based on the alignment flags within the allocated bounds

This means AlignWidget expands to fill its parent container, which is why it works well inside borders and fixed-size containers.

## Common Patterns

### Centered Dialog

```csharp
ctx.Border(b => [
    b.Align(Alignment.Center,
        b.VStack(v => [
            v.Text("Are you sure?"),
            v.Text(""),
            v.Button("OK").OnClick(_ => /* ... */)
        ])
    )
], title: "Confirm")
```

### Status Bar at Bottom

```csharp
ctx.VStack(v => [
    v.Text("Main content here...").Fill(),
    v.Align(Alignment.BottomCenter,
        v.Text("Status: Ready")
    ).FixedHeight(1)
])
```

### Right-Aligned Button

```csharp
ctx.Border(b => [
    b.VStack(v => [
        v.Text("Form content..."),
        v.Text(""),
        v.Align(Alignment.Right,
            v.Button("Submit")
        )
    ])
], title: "Form")
```

## Related Widgets

- [VStackWidget](/guide/widgets/vstack) - Vertical layout container
- [HStackWidget](/guide/widgets/hstack) - Horizontal layout container
- [BorderWidget](/guide/widgets/border) - Container with visual border
