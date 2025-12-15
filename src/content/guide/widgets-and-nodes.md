# Widgets & Nodes

Hex1b uses a two-layer architecture inspired by React: **Widgets** describe what to render, while **Nodes** manage state and perform actual rendering.

## The Separation

| Layer | Type | Mutability | Purpose |
|-------|------|------------|---------|
| **Widget** | `record` | Immutable | Describes the desired UI |
| **Node** | `class` | Mutable | Manages state, renders to terminal |

This separation enables efficient reconciliation—Hex1b can diff widgets and update only what changed.

## Widgets: The Declaration

Widgets are simple, immutable data structures:

```csharp
// A widget just holds configuration
public record ButtonWidget(string Label, Action OnClick) : Hex1bWidget;

public record TextBlockWidget(string Text) : Hex1bWidget;

public record VStackWidget(Hex1bWidget[] Children) : Hex1bWidget;
```

When you build your UI, you're constructing a tree of widgets:

```csharp
buildWidget: (ctx, ct) => 
    new VStackWidget([
        new TextBlockWidget("Hello"),
        new ButtonWidget("Click", () => Console.WriteLine("Clicked!"))
    ])
```

## Nodes: The Reality

Nodes are the actual objects that get measured, arranged, and rendered:

```csharp
public class ButtonNode : Hex1bNode
{
    // Properties updated from widget during reconciliation
    public string Label { get; set; } = "";
    public Action? OnClick { get; set; }
    
    // Mutable state preserved across reconciliations
    public bool IsFocused { get; set; }
    
    public override void Measure(Constraints constraints)
    {
        // Calculate how much space we need
        DesiredSize = new Size(Label.Length + 4, 1);
    }
    
    public override void Arrange(Rect rect)
    {
        // Position ourselves in the given rectangle
        Bounds = rect;
    }
    
    public override void Render(Hex1bRenderContext context)
    {
        // Draw to the terminal
        var style = IsFocused ? "[▶ " : "[ ";
        context.Write(Bounds.X, Bounds.Y, $"{style}{Label} ]");
    }
}
```

## Reconciliation

When you call `SetState()`, Hex1b:

1. **Builds** a new widget tree from your `buildWidget` function
2. **Diffs** the new tree against the existing node tree
3. **Updates** existing nodes with new properties, or creates new nodes
4. **Preserves** mutable state (focus, scroll position, cursor) on reused nodes

```
Widget Tree (new)          Node Tree (existing)
     VStack          →          VStackNode
       │                            │
  ┌────┴────┐               ┌───────┴───────┐
Text    Button    →    TextNode      ButtonNode
"Hi"    "Save"         ↓ update        ↓ update
                       Text="Hi"       Label="Save"
                                       IsFocused=true ← preserved!
```

## Why This Matters

1. **State Preservation**: Focus doesn't jump around when the UI re-renders
2. **Performance**: Only changed parts of the tree get updated
3. **Simplicity**: You describe the UI declaratively; Hex1b figures out the transitions

## Creating Custom Widgets

To add a custom widget:

### 1. Define the Widget

```csharp
public record ProgressBarWidget(
    double Value,       // 0.0 to 1.0
    int Width = 20
) : Hex1bWidget;
```

### 2. Define the Node

```csharp
public class ProgressBarNode : Hex1bNode
{
    public double Value { get; set; }
    public int BarWidth { get; set; }
    
    public override void Measure(Constraints constraints)
    {
        DesiredSize = new Size(BarWidth, 1);
    }
    
    public override void Arrange(Rect rect)
    {
        Bounds = rect;
    }
    
    public override void Render(Hex1bRenderContext context)
    {
        var filled = (int)(Value * BarWidth);
        var bar = new string('█', filled) + new string('░', BarWidth - filled);
        context.Write(Bounds.X, Bounds.Y, bar);
    }
}
```

### 3. Register Reconciliation

In `Hex1bApp.Reconcile()`, add a case:

```csharp
ProgressBarWidget pb => ReconcileProgressBar(pb, existingNode),
```

And the reconcile method:

```csharp
Hex1bNode ReconcileProgressBar(ProgressBarWidget widget, Hex1bNode? existing)
{
    var node = existing as ProgressBarNode ?? new ProgressBarNode();
    node.Value = widget.Value;
    node.BarWidth = widget.Width;
    return node;
}
```

## Next Steps

- [Layout System](/guide/layout) - How Measure and Arrange work
- [Reconciliation Deep Dive](/deep-dives/reconciliation) - The algorithm in detail
