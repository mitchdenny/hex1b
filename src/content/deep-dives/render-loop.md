# The Render Loop

Hex1b runs a continuous loop that processes input, updates state, and renders to the terminal. Understanding this loop is key to building performant apps.

## The Loop

```
┌─────────────────────────────────────────────────────────────┐
│  1. WAIT FOR INPUT                                          │
│     Block until user presses a key or state changes         │
├─────────────────────────────────────────────────────────────┤
│  2. PROCESS INPUT                                           │
│     Route key to focused widget, execute bindings           │
├─────────────────────────────────────────────────────────────┤
│  3. BUILD WIDGETS                                           │
│     Call buildWidget(ctx) to get new widget tree            │
├─────────────────────────────────────────────────────────────┤
│  4. RECONCILE                                               │
│     Diff widgets against nodes, update/create nodes         │
├─────────────────────────────────────────────────────────────┤
│  5. MEASURE                                                 │
│     Top-down: pass constraints, collect desired sizes       │
├─────────────────────────────────────────────────────────────┤
│  6. ARRANGE                                                 │
│     Top-down: assign final positions and sizes              │
├─────────────────────────────────────────────────────────────┤
│  7. RENDER                                                  │
│     Draw nodes to render buffer                             │
├─────────────────────────────────────────────────────────────┤
│  8. FLUSH                                                   │
│     Diff buffer against previous, write ANSI to terminal    │
└─────────────────────────────────────────────────────────────┘
```

## Code Overview

```csharp
public async Task RunAsync(CancellationToken ct)
{
    // Initial render
    RebuildAndRender();
    
    while (!ct.IsCancellationRequested)
    {
        // 1. Wait for input
        var key = await _terminal.ReadKeyAsync(ct);
        
        // 2. Process input
        _inputRouter.Route(key, _rootNode);
        
        // 3-7. Rebuild and render (if state changed)
        if (_stateChanged)
        {
            RebuildAndRender();
            _stateChanged = false;
        }
    }
}

void RebuildAndRender()
{
    // 3. Build widgets
    var widget = _buildWidget(_context, _ct);
    
    // 4. Reconcile
    _rootNode = Reconcile(widget, _rootNode);
    
    // 5. Measure
    var constraints = new Constraints(0, _terminal.Width, 0, _terminal.Height);
    _rootNode.Measure(constraints);
    
    // 6. Arrange
    var bounds = new Rect(0, 0, _terminal.Width, _terminal.Height);
    _rootNode.Arrange(bounds);
    
    // 7-8. Render
    var ctx = new Hex1bRenderContext(_terminal);
    _rootNode.Render(ctx);
    ctx.Flush();
}
```

## When Rendering Happens

Rendering is triggered by:

1. **State changes** - Calling `ctx.SetState()`
2. **Terminal resize** - Window size change
3. **Focus changes** - Tab navigation
4. **Initial render** - App startup

Rendering does NOT happen for every keystroke—if input is handled without state change, no re-render occurs.

## The Render Buffer

Hex1b uses a double-buffered approach:

```
Previous Frame Buffer          New Frame Buffer
┌─────────────────────┐       ┌─────────────────────┐
│ Hello World         │       │ Hello World         │  ← same
│ [Button A] [B]      │       │ [Button A*] [B]     │  ← changed
│                     │       │                     │
└─────────────────────┘       └─────────────────────┘
                                     ↓
                              Only "A*" is written
                              to terminal
```

This minimizes terminal I/O and prevents flickering.

## ANSI Escape Sequences

Hex1b communicates with the terminal using ANSI sequences:

```
\x1b[H         - Move cursor to home
\x1b[2J        - Clear screen
\x1b[10;5H    - Move cursor to row 10, column 5
\x1b[31m      - Set foreground to red
\x1b[0m       - Reset all styles
\x1b[?25l    - Hide cursor
\x1b[?25h    - Show cursor
\x1b[?1049h  - Switch to alternate screen
\x1b[?1049l  - Switch back to main screen
```

## Alternate Screen Buffer

Hex1b uses the terminal's alternate screen buffer:

- Your shell history isn't polluted
- App has full control of the screen
- Clean exit restores original content

## Performance Considerations

### Avoid Expensive Computation in buildWidget

```csharp
// ❌ Bad: Sorts on every render
buildWidget: (ctx, ct) => 
    new ListWidget(ctx.State.Items.OrderBy(x => x.Date).ToArray())

// ✅ Good: Sort in state management
void AddItem(Item item)
{
    var items = state.Items.Append(item).OrderBy(x => x.Date).ToList();
    ctx.SetState(state with { Items = items });
}
```

### Minimize Widget Allocations

```csharp
// ❌ Bad: New closure on every render
new ButtonWidget("Save", () => ctx.SetState(...))

// ✅ Good: Capture state in a method
new ButtonWidget("Save", () => HandleSave(ctx))
```

### Debounce Rapid Updates

```csharp
// For search-as-you-type, debounce API calls
async Task OnSearchChanged(string text)
{
    ctx.SetState(state with { SearchText = text });
    
    await Task.Delay(300);  // Wait for typing to stop
    
    if (ctx.State.SearchText == text)  // Still current?
    {
        var results = await SearchApi(text);
        ctx.SetState(state with { Results = results });
    }
}
```

## Related

- [Reconciliation](/deep-dives/reconciliation) - Step 4 in detail
- [Terminal Rendering](/deep-dives/terminal-rendering) - Steps 7-8 in detail
- [Focus System](/deep-dives/focus-system) - Step 2 in detail
