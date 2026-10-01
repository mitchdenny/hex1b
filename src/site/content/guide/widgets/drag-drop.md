<script setup>
const basicCode = `using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

var items = new List<string> { "Apple", "Banana", "Cherry", "Date" };
string? lastAction = null;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text(" Drag & Drop Demo"),
        v.Separator(),

        v.HStack(h => [
            // Source list
            h.Border(b => [
                b.VStack(sv => [
                    sv.Text(" Fruits"),
                    sv.Separator(),
                    ..items.Select(item =>
                        sv.Draggable(item, dc =>
                            dc.Text(dc.IsDragging ? " ┄┄┄┄┄" : $" {item}"))
                    )
                ])
            ]).Fill(),

            // Drop target
            h.Droppable(dc => dc.Border(b => [
                b.VStack(dv => [
                    dv.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor,
                            dc.IsHoveredByDrag ? Hex1bColor.Green : Hex1bColor.White),
                        dv.Text(dc.IsHoveredByDrag ? " ← Drop here!" : " Drop Zone")),
                    dv.Separator(),
                    dv.Text(lastAction ?? " Drag a fruit here"),
                ])
            ]))
            .OnDrop(e => lastAction = $" Received: {e.DragData}")
            .Fill(),
        ]).Fill(),
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();`

const ghostCode = `using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

var tasks = new List<string> { "Design UI", "Write tests", "Deploy" };

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text(" Drag Ghost Demo"),
        v.Separator(),
        ..tasks.Select(task =>
            v.Draggable(task, dc =>
                dc.ThemePanel(
                    t => t.Set(BorderTheme.BorderColor,
                        dc.IsDragging
                            ? Hex1bColor.FromRgb(60, 60, 60)
                            : Hex1bColor.White),
                    dc.Border(dc.Text($" {task}"))))
            // Ghost overlay follows the cursor during drag
            .DragOverlay(dc =>
                dc.ThemePanel(
                    t => t.Set(BorderTheme.BorderColor, Hex1bColor.Cyan),
                    dc.Border(dc.Text($" 📋 {task}"))))
        )
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();`

const acceptCode = `using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

// Two types of draggable items
record Fruit(string Name);
record Vegetable(string Name);

var fruitBasket = new List<string>();
var vegBasket = new List<string>();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text(" Type-Safe Drop Targets"),
        v.Separator(),

        // Draggable items
        v.HStack(h => [
            h.Draggable(new Fruit("Apple"), dc => dc.Text(" 🍎 Apple")),
            h.Text("  "),
            h.Draggable(new Fruit("Banana"), dc => dc.Text(" 🍌 Banana")),
            h.Text("  "),
            h.Draggable(new Vegetable("Carrot"), dc => dc.Text(" 🥕 Carrot")),
        ]),
        v.Text(""),

        v.HStack(h => [
            // Only accepts Fruit
            h.Droppable(dc => dc.Border(b => [
                b.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor,
                        dc.IsHoveredByDrag
                            ? (dc.CanAcceptDrag ? Hex1bColor.Green : Hex1bColor.Red)
                            : Hex1bColor.White),
                    b.Text(dc.IsHoveredByDrag && !dc.CanAcceptDrag
                        ? " ✗ Fruits only!"
                        : $" Fruit Basket ({fruitBasket.Count})")),
            ]))
            .Accept(data => data is Fruit)
            .OnDrop(e => { if (e.DragData is Fruit f) fruitBasket.Add(f.Name); })
            .Fill(),

            // Only accepts Vegetable
            h.Droppable(dc => dc.Border(b => [
                b.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor,
                        dc.IsHoveredByDrag
                            ? (dc.CanAcceptDrag ? Hex1bColor.Green : Hex1bColor.Red)
                            : Hex1bColor.White),
                    b.Text(dc.IsHoveredByDrag && !dc.CanAcceptDrag
                        ? " ✗ Veggies only!"
                        : $" Veggie Basket ({vegBasket.Count})")),
            ]))
            .Accept(data => data is Vegetable)
            .OnDrop(e => { if (e.DragData is Vegetable v2) vegBasket.Add(v2.Name); })
            .Fill(),
        ]).Fill(),
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();`

const dropTargetCode = `using Hex1b;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

var items = new List<string> { "First", "Second", "Third" };

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text(" Drop Targets — Positional Insertion"),
        v.Separator(),

        // Draggable source
        v.Draggable("New Item", dc =>
            dc.Text(dc.IsDragging ? " ┄┄┄" : " ⊕ New Item"))
            .DragOverlay(dc => dc.Border(dc.Text(" ⊕ New Item"))),
        v.Text(""),

        // Droppable list with insertion points
        v.Droppable(dc => dc.Border(b => [
            b.VStack(sv => {
                var widgets = new List<Hex1bWidget>();

                // Drop target before first item
                widgets.Add(dc.DropTarget("pos-0", dt =>
                    dt.IsActive
                        ? dt.ThemePanel(
                            t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
                            dt.Text(" ─── insert here ───"))
                        : dt.Text("").Height(SizeHint.Fixed(0))));

                // Items interleaved with drop targets
                for (int i = 0; i < items.Count; i++)
                {
                    widgets.Add(sv.Text($" • {items[i]}"));
                    widgets.Add(dc.DropTarget($"pos-{i + 1}", dt =>
                        dt.IsActive
                            ? dt.ThemePanel(
                                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
                                dt.Text(" ─── insert here ───"))
                            : dt.Text("").Height(SizeHint.Fixed(0))));
                }

                return [.. widgets];
            })
        ]))
        .OnDropTarget(e =>
        {
            var pos = int.Parse(e.TargetId.Split('-')[1]);
            pos = Math.Min(pos, items.Count);
            items.Insert(pos, $"Item {items.Count + 1}");
        })
        .OnDrop(e => items.Add($"Item {items.Count + 1}"))
        .Fill(),
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();`

const kanbanCode = `using Hex1b;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

record KanbanTask(string Id, string Title, string Category);

var columns = new Dictionary<string, List<KanbanTask>>
{
    ["To Do"] = [
        new("1", "Design login page", "UI"),
        new("2", "Set up CI pipeline", "DevOps"),
        new("3", "Write unit tests", "Testing"),
    ],
    ["In Progress"] = [
        new("4", "Implement auth API", "Backend"),
    ],
    ["Done"] = [
        new("5", "Project setup", "DevOps"),
    ],
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text(" ◆ Kanban Board"),
        v.Separator(),
        v.HStack(h => [
            ..columns.Select(kvp => BuildColumn(h, kvp.Key, kvp.Value))
        ]).Fill(),
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();

Hex1bWidget BuildColumn(
    WidgetContext<HStackWidget> parent,
    string name,
    List<KanbanTask> tasks)
{
    return parent.Droppable(dc =>
    {
        var color = dc.IsHoveredByDrag && dc.CanAcceptDrag
            ? Hex1bColor.Green : Hex1bColor.White;

        return dc.ThemePanel(
            t => t.Set(BorderTheme.BorderColor, color),
            dc.Border(dc.VStack(v =>
            {
                var items = new List<Hex1bWidget>();
                items.Add(v.Text($" {name} ({tasks.Count})"));
                items.Add(v.Separator());

                items.Add(dc.DropTarget("pos-0", dt =>
                    dt.IsActive
                        ? dt.ThemePanel(
                            t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
                            dt.Text(" ─── insert here ───"))
                        : dt.Text("").Height(SizeHint.Fixed(0))));

                for (int i = 0; i < tasks.Count; i++)
                {
                    var task = tasks[i];
                    items.Add(v.Draggable(task, dc2 =>
                        dc2.IsDragging
                            ? dc2.ThemePanel(
                                t => t.Set(BorderTheme.BorderColor,
                                    Hex1bColor.FromRgb(60, 60, 60)),
                                dc2.Border(dc2.Text(" ┄┄┄")))
                            : dc2.Border(dc2.VStack(cv => [
                                cv.Text($" {task.Title}"),
                                cv.Text($"   [{task.Category}]"),
                            ])))
                        .DragOverlay(dc2 =>
                            dc2.Border(dc2.VStack(cv => [
                                cv.Text($" {task.Title}"),
                                cv.Text($"   [{task.Category}]"),
                            ]))));

                    items.Add(dc.DropTarget($"pos-{i + 1}", dt =>
                        dt.IsActive
                            ? dt.ThemePanel(
                                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
                                dt.Text(" ─── insert here ───"))
                            : dt.Text("").Height(SizeHint.Fixed(0))));
                }

                items.Add(v.Text("").Fill());
                return [.. items];
            })));
    })
    .Accept(data => data is KanbanTask)
    .OnDropTarget(e =>
    {
        if (e.DragData is KanbanTask task)
        {
            var srcIdx = tasks.IndexOf(task);
            foreach (var col in columns.Values) col.Remove(task);
            var pos = int.Parse(e.TargetId.Split('-')[1]);
            if (srcIdx >= 0 && pos > srcIdx) pos--;
            pos = Math.Min(pos, tasks.Count);
            tasks.Insert(pos, task);
        }
    })
    .OnDrop(e =>
    {
        if (e.DragData is KanbanTask task)
        {
            foreach (var col in columns.Values) col.Remove(task);
            tasks.Add(task);
        }
    })
    .Fill();
}`
</script>

# Drag & Drop

Hex1b provides a declarative drag-and-drop system built on top of the widget/node architecture. You define **draggable sources** and **droppable targets** using builder callbacks that receive context about the current drag state, letting your UI react dynamically to drag operations.

::: tip Mouse Required
Drag and drop requires mouse support. Enable it with `.WithMouse()` on `Hex1bTerminal.CreateBuilder()`.
:::

## Core Concepts

The drag-and-drop system has three main building blocks:

| Widget | Purpose |
|--------|---------|
| **Draggable** | Wraps content that can be picked up with the mouse |
| **Droppable** | Defines a region that can receive dropped items |
| **DropTarget** | Marks a specific insertion point within a droppable |

The flow works like this:

```
Mouse press on Draggable → Drag starts → Move over Droppable → Release → OnDrop fires
```

## Basic Usage

Create draggable items with `ctx.Draggable()` and drop zones with `ctx.Droppable()`. Both receive context objects that let you render differently based on drag state:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="drag-drop-basic" exampleTitle="Drag & Drop - Basic Usage" />

### Key Points

- **`Draggable(dragData, builder)`** — The first argument is the data payload (any `object`). The builder receives a `DraggableContext` with `IsDragging` to reflect state.
- **`Droppable(builder)`** — The builder receives a `DroppableContext` with `IsHoveredByDrag` and `CanAcceptDrag`.
- **`.OnDrop(handler)`** — Fires when an accepted item is released over the droppable. The handler receives `DropEventArgs` containing `DragData`, `LocalX`, and `LocalY`.

## Drag Ghost Overlay

By default, dragging moves the cursor but doesn't show a visual representation of what's being dragged. Use `.DragOverlay()` to create a **ghost** — a floating widget that follows the cursor:

<CodeBlock lang="csharp" :code="ghostCode" command="dotnet run" example="drag-drop-ghost" exampleTitle="Drag & Drop - Ghost Overlay" />

The overlay builder receives the same `DraggableContext` as the main builder, so you can reuse the same rendering logic. The ghost is automatically positioned near the cursor and clamped to screen bounds.

::: tip Ghost Sizing
The drag ghost is constrained to roughly one-third of the screen width. This prevents fill-width children from spanning the entire terminal when rendered as an overlay.
:::

## Accept Predicates

Use `.Accept()` to control which drag data a droppable will receive. The predicate runs during hover, and its result is exposed via `dc.CanAcceptDrag` so you can show visual rejection feedback:

<CodeBlock lang="csharp" :code="acceptCode" command="dotnet run" example="drag-drop-accept" exampleTitle="Drag & Drop - Accept Predicates" />

When the predicate returns `false`:
- `dc.CanAcceptDrag` is `false` (show rejection styling)
- `dc.IsHoveredByDrag` is still `true` (you know something is hovering)
- Releasing the mouse does **not** fire `OnDrop`

If no `.Accept()` predicate is set, all drag data is accepted.

## Drop Targets — Positional Insertion

For scenarios like reorderable lists or Kanban boards, you need to know **where** within a droppable the item should be inserted. Drop targets mark specific insertion points:

<CodeBlock lang="csharp" :code="dropTargetCode" command="dotnet run" example="drag-drop-target" exampleTitle="Drag & Drop - Drop Targets" />

### How Drop Targets Work

1. **Zero height when inactive** — Use `.Height(SizeHint.Fixed(0))` on the inactive content to prevent gaps between items
2. **Proximity activation** — During a drag, the framework activates the nearest drop target to the cursor within the hovered droppable
3. **`dt.IsActive`** — The builder callback checks this to show/hide the insertion indicator
4. **`OnDropTarget` vs `OnDrop`** — When a drop lands on an active target, `OnDropTarget` fires with the `TargetId`. If no target is active, `OnDrop` fires as a fallback

### Always-Visible Drop Targets

You can also render drop targets that are always visible (e.g., dim separator lines) by returning visible content when `dt.IsActive` is `false`:

```csharp
dc.DropTarget("pos-0", dt =>
    dt.IsActive
        ? dt.ThemePanel(
            t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
            dt.Text(" ─── insert here ───"))
        : dt.ThemePanel(
            t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(60, 60, 60)),
            dt.Text(" ─── ─── ─── ───")))
```

## Context API Reference

### DraggableContext

Available in `Draggable()` and `DragOverlay()` builder callbacks:

| Property | Type | Description |
|----------|------|-------------|
| `IsDragging` | `bool` | Whether this item is currently being dragged |
| `DragData` | `object` | The drag payload for this item |

### DroppableContext

Available in `Droppable()` builder callbacks:

| Property | Type | Description |
|----------|------|-------------|
| `IsHoveredByDrag` | `bool` | Whether a drag is hovering over this region |
| `CanAcceptDrag` | `bool` | Whether the hovered data passes the Accept predicate |
| `HoveredDragData` | `object?` | The drag payload currently hovering (null if none) |

| Method | Returns | Description |
|--------|---------|-------------|
| `DropTarget(id, builder)` | `DropTargetWidget` | Creates an insertion point within this droppable |

### DropTargetContext

Available in `DropTarget()` builder callbacks:

| Property | Type | Description |
|----------|------|-------------|
| `IsActive` | `bool` | Whether this is the nearest target to the cursor |
| `DragData` | `object?` | The drag payload currently being dragged (null if none) |

## Event Args Reference

### DropEventArgs

Received by `OnDrop` handlers:

| Property | Type | Description |
|----------|------|-------------|
| `DragData` | `object` | The drag payload from the source |
| `Source` | `DraggableNode` | The node that initiated the drag |
| `LocalX` | `int` | Drop X position relative to the droppable's bounds |
| `LocalY` | `int` | Drop Y position relative to the droppable's bounds |

### DropTargetEventArgs

Received by `OnDropTarget` handlers:

| Property | Type | Description |
|----------|------|-------------|
| `TargetId` | `string` | The ID of the drop target that received the drop |
| `DragData` | `object` | The drag payload from the source |
| `Source` | `DraggableNode` | The node that initiated the drag |

## Full Example: Kanban Board

Here's a complete Kanban board with three columns, drag ghosts, and positional insertion via drop targets:

<CodeBlock lang="csharp" :code="kanbanCode" command="dotnet run" example="drag-drop-kanban" exampleTitle="Drag & Drop - Kanban Board" />

This demonstrates:
- **Cards as draggable sources** with placeholder rendering while dragging
- **Columns as droppable targets** with visual hover feedback
- **Drop targets between cards** for positional insertion
- **Same-column reordering** with correct index adjustment
- **Cross-column moves** with `OnDrop` fallback

## Fluent API Summary

```csharp
// Draggable
ctx.Draggable(dragData, dc => /* widget */)
    .DragOverlay(dc => /* ghost widget */);

// Droppable
ctx.Droppable(dc => /* widget */)
    .Accept(data => /* predicate */)
    .OnDrop(e => /* handle drop */)
    .OnDropTarget(e => /* handle positional drop */);

// Drop Target (inside a Droppable builder)
dc.DropTarget("target-id", dt => /* widget */);
```

## Related Widgets

- [Surface](/guide/widgets/surface) — Low-level rendering for rich drag ghost content
- [ThemePanel](/guide/widgets/themepanel) — Style drag states with contextual colors
- [Stacks (HStack/VStack)](/guide/widgets/stacks) — Layout containers for arranging draggable items
