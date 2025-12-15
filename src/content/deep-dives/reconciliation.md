# Reconciliation

Reconciliation is the process by which Hex1b updates the node tree to match a new widget tree. This is the core algorithm that makes declarative UI possible.

## The Problem

When you call `SetState()`, your `buildWidget` function runs again and produces a new widget tree. But we can't just throw away the old node tree because:

1. **State would be lost** - Focus, cursor position, scroll offset
2. **Performance would suffer** - Recreating nodes is expensive
3. **Animations would break** - No continuity between frames

## The Solution: Diffing

Hex1b compares the new widget tree with the existing node tree:

```
New Widget Tree              Existing Node Tree
     VStack           →          VStackNode
       │                            │
  ┌────┴────┐               ┌───────┴───────┐
Text    Button    →    TextNode      ButtonNode
"Hi"    "Save"         Text="Hello"  Label="Save"
                            ↓            ↓
                       UPDATE         REUSE
                       Text="Hi"      (no change)
```

## The Algorithm

```csharp
Hex1bNode Reconcile(Hex1bWidget widget, Hex1bNode? existingNode)
{
    // Case 1: Same type - update existing node
    if (existingNode != null && CanReconcile(widget, existingNode))
    {
        UpdateNodeFromWidget(existingNode, widget);
        return existingNode;
    }
    
    // Case 2: Different type or no existing - create new
    return CreateNodeFromWidget(widget);
}

bool CanReconcile(Hex1bWidget widget, Hex1bNode node)
{
    return widget switch
    {
        TextBlockWidget => node is TextBlockNode,
        ButtonWidget => node is ButtonNode,
        VStackWidget => node is VStackNode,
        // ... etc
    };
}
```

## Children Reconciliation

For containers with children, we reconcile each child by position:

```csharp
void ReconcileChildren(Hex1bWidget[] newChildren, List<Hex1bNode> existingChildren)
{
    var result = new List<Hex1bNode>();
    
    for (int i = 0; i < newChildren.Length; i++)
    {
        var newChild = newChildren[i];
        var existing = i < existingChildren.Count ? existingChildren[i] : null;
        
        result.Add(Reconcile(newChild, existing));
    }
    
    // Truncate if new list is shorter
    Children = result;
}
```

## Keys (Coming Soon)

For reorderable lists, position-based reconciliation breaks down. Keys will allow:

```csharp
new ListWidget(
    items.Select(item => 
        new ListItemWidget(item.Id, RenderItem(item))  // Id is the key
    )
)
```

## What Gets Preserved

When a node is reused:

| Preserved | Updated |
|-----------|---------|
| `IsFocused` | `Label`, `Text`, etc. |
| `CursorPosition` | `Children` (recursively) |
| `ScrollOffset` | Style properties |
| Selection state | Event handlers |

## Performance Implications

### Good: O(n) Complexity

Unlike React's O(n³) worst case, Hex1b uses O(n) because:
- TUI trees are typically shallow
- Position-based matching is sufficient for most cases
- No cross-subtree moves are detected

### Optimization Tips

```csharp
// ❌ Bad: Recreates all buttons every render
items.Select((item, i) => 
    new ButtonWidget($"Delete {item}", () => Delete(i))
)

// ✅ Good: Stable references when possible
items.Select(item => 
    new ButtonWidget($"Delete {item.Name}", item.Delete)
)
```

## Debugging Reconciliation

Enable debug logging to see what's happening:

```csharp
var options = new Hex1bAppOptions
{
    DebugReconciliation = true
};
```

Output:
```
[Reconcile] VStackWidget → VStackNode (reuse)
[Reconcile]   TextBlockWidget → TextBlockNode (reuse, text changed)
[Reconcile]   ButtonWidget → ButtonNode (reuse, no change)
[Reconcile]   ListWidget → NEW ListNode (type mismatch)
```

## Comparison to React

| Concept | React | Hex1b |
|---------|-------|-------|
| Virtual representation | Virtual DOM | Widget tree |
| Actual representation | Real DOM | Node tree |
| Diff algorithm | Fiber reconciler | Simple positional |
| Keys | Supported | Coming soon |
| Concurrent rendering | Yes | No (single-threaded) |

## Related

- [Widgets & Nodes](/guide/widgets-and-nodes) - The data structures
- [Render Loop](/deep-dives/render-loop) - Where reconciliation fits
