# Layout System

Hex1b uses a constraint-based layout system inspired by Flutter and SwiftUI. Layout happens in two phases: **Measure** and **Arrange**.

## The Layout Flow

```
┌─────────────────────────────────────────────────────────┐
│  1. MEASURE (top-down constraints, bottom-up sizes)     │
│     Parent says: "You have 80×24 available"             │
│     Child says: "I need 40×10"                          │
├─────────────────────────────────────────────────────────┤
│  2. ARRANGE (top-down positions)                        │
│     Parent says: "You're at (5, 2) with size 40×10"     │
│     Child positions itself and its children             │
└─────────────────────────────────────────────────────────┘
```

## Constraints

Constraints define the min/max space available:

```csharp
public record Constraints(
    int MinWidth,
    int MaxWidth,
    int MinHeight,
    int MaxHeight
);
```

A widget can be given:
- **Tight constraints**: `MinWidth == MaxWidth` (must be exactly this size)
- **Loose constraints**: `MinWidth < MaxWidth` (choose within range)
- **Unbounded**: `MaxWidth = int.MaxValue` (take what you need)

## Size Hints

Widgets can specify how they want to be sized:

```csharp
public enum SizeHint
{
    Content,  // Size to fit my content
    Fill,     // Take all available space
    Fixed     // Use a specific size
}
```

Use extension methods to apply hints:

```csharp
// Take all available width, height fits content
new TextBoxWidget(value, onChange).FillWidth()

// Fixed width, fill height
new ListWidget(items).Width(30).FillHeight()

// Fill both dimensions
new PanelWidget(child).Fill()
```

## Stacks

### VStack (Vertical)

Arranges children vertically:

```csharp
new VStackWidget([
    new TextBlockWidget("Header"),        // Gets its content height
    new ListWidget(items).Fill(),         // Gets remaining space
    new TextBlockWidget("Footer")         // Gets its content height
])
```

### HStack (Horizontal)

Arranges children horizontally:

```csharp
new HStackWidget([
    new TextBlockWidget("Label").Width(20),  // Fixed 20 columns
    new TextBoxWidget(value, onChange).Fill(), // Gets remaining space
    new ButtonWidget("OK").Width(10)          // Fixed 10 columns
])
```

## The Measure Phase

During Measure, each node:
1. Receives constraints from its parent
2. Measures its children (if any)
3. Calculates its `DesiredSize`

```csharp
public override void Measure(Constraints constraints)
{
    // Measure children with modified constraints
    foreach (var child in Children)
    {
        child.Measure(constraints.WithMaxWidth(constraints.MaxWidth - padding));
    }
    
    // Calculate our desired size based on children
    var height = Children.Sum(c => c.DesiredSize.Height);
    var width = Children.Max(c => c.DesiredSize.Width) + padding;
    
    DesiredSize = new Size(
        Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
        Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
    );
}
```

## The Arrange Phase

During Arrange, each node:
1. Receives its final bounds from its parent
2. Positions its children within those bounds

```csharp
public override void Arrange(Rect rect)
{
    Bounds = rect;
    
    var y = rect.Y;
    foreach (var child in Children)
    {
        var childRect = new Rect(rect.X, y, child.DesiredSize.Width, child.DesiredSize.Height);
        child.Arrange(childRect);
        y += child.DesiredSize.Height;
    }
}
```

## Common Patterns

### Centered Content

```csharp
new VStackWidget([
    new SpacerWidget(),
    new HStackWidget([
        new SpacerWidget(),
        content,
        new SpacerWidget()
    ]),
    new SpacerWidget()
])
```

### Sidebar Layout

```csharp
new HStackWidget([
    new BorderWidget(sidebar).Width(30),
    new BorderWidget(mainContent).Fill()
])
```

### Header/Content/Footer

```csharp
new VStackWidget([
    new PanelWidget(header).Height(3),
    new PanelWidget(content).Fill(),
    new PanelWidget(footer).Height(1)
])
```

## Live Demo

<TerminalDemo exhibit="layout" title="Layout Demo" />

## Next Steps

- [Input Handling](/guide/input) - Keyboard and focus
- [Render Loop Deep Dive](/deep-dives/render-loop) - The complete cycle
