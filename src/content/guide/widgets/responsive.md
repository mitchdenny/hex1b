<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode     → src/Hex1b.Website/Examples/ResponsiveBasicExample.cs
  - todoCode      → src/Hex1b.Website/Examples/ResponsiveTodoExample.cs (already exists)
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/responsive-basic.cs?raw'
import threeTierSnippet from './snippets/responsive-three-tier.cs?raw'
import layoutSwitchSnippet from './snippets/responsive-layout-switch.cs?raw'
import customConditionSnippet from './snippets/responsive-custom-condition.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("Resize your terminal to see the layout change!"),
        v.Text(""),
        v.Responsive(r => [
            r.WhenMinWidth(100, r => r.Text("Wide layout: You have plenty of space!")),
            r.WhenMinWidth(60, r => r.Text("Medium layout: Comfortable width")),
            r.Otherwise(r => r.Text("Narrow layout: Compact view"))
        ])
    ])
));

await app.RunAsync();`

const todoCode = `using Hex1b;
using Hex1b.Widgets;

var state = new TodoState();

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.Responsive(r => [
        // Extra wide: Three columns with stats
        r.WhenMinWidth(150, r => BuildExtraWideLayout(r, state)),
        
        // Wide: Two columns with details
        r.WhenMinWidth(110, r => BuildWideLayout(r, state)),
        
        // Medium: Single column with all features
        r.WhenMinWidth(70, r => BuildMediumLayout(r, state)),
        
        // Compact: Minimal layout
        r.Otherwise(r => BuildCompactLayout(r, state))
    ])
));

await app.RunAsync();

class TodoState
{
    public List<TodoItem> Items { get; } = 
    [
        new("Buy groceries", true),
        new("Review pull request", false),
        new("Write documentation", false)
    ];
    public int SelectedIndex { get; set; }
    public string NewItemText { get; set; } = "";
}

record TodoItem(string Title, bool IsComplete);`
</script>

# Responsive & Conditional

Build adaptive terminal UIs that adjust their layout based on available space.

The **ResponsiveWidget** displays the first child whose condition evaluates to true, allowing you to create layouts that adapt to different terminal sizes—similar to CSS media queries but for the terminal.

## Basic Usage

Create responsive layouts using condition helpers like `WhenMinWidth()`:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="responsive-basic" exampleTitle="Responsive - Basic Usage" />

The widget evaluates conditions during layout and displays the first matching branch. When the terminal is resized, the layout automatically updates to show the appropriate content.

::: tip How It Works
Conditions receive `(availableWidth, availableHeight)` from parent constraints during the layout phase. The first branch whose condition returns `true` becomes the active child. Only the active child is measured, arranged, and rendered.
:::

## Condition Helpers

ResponsiveWidget provides several helper methods for common responsive patterns:

### WhenMinWidth

Display content when the available width meets or exceeds a minimum:

<StaticTerminalPreview svgPath="/svg/responsive-basic.svg" :code="basicSnippet" />

```csharp
ctx.Responsive(r => [
    r.WhenMinWidth(100, r => r.Text("Wide layout")),
    r.Otherwise(r => r.Text("Narrow layout"))
])
```

### Multiple Width Tiers

Create breakpoints for different screen sizes:

<StaticTerminalPreview svgPath="/svg/responsive-three-tier.svg" :code="threeTierSnippet" />

```csharp
ctx.Responsive(r => [
    r.WhenMinWidth(120, r => r.Text("Extra wide: 120+ columns")),
    r.WhenMinWidth(80, r => r.Text("Wide: 80-119 columns")),
    r.WhenMinWidth(50, r => r.Text("Medium: 50-79 columns")),
    r.Otherwise(r => r.Text("Narrow: < 50 columns"))
])
```

**Important**: Order matters! Conditions are evaluated top-to-bottom, and the **first** matching condition wins. Always place more restrictive conditions (larger minimums) first.

### WhenWidth

Use a custom width predicate for more complex logic:

```csharp
ctx.Responsive(r => [
    r.WhenWidth(w => w > 100 && w % 2 == 0, r => r.Text("Wide and even")),
    r.WhenWidth(w => w > 100, r => r.Text("Wide")),
    r.Otherwise(r => r.Text("Narrow"))
])
```

### When - Custom Conditions

The full `When()` method accepts both width and height:

<StaticTerminalPreview svgPath="/svg/responsive-custom-condition.svg" :code="customConditionSnippet" />

```csharp
ctx.Responsive(r => [
    r.When((w, h) => w >= 100 && h >= 20, r =>
        r.Text("Large screen (100x20+)")
    ),
    r.When((w, h) => w >= 60 || h >= 15, r =>
        r.Text("Medium screen (60+ wide OR 15+ tall)")
    ),
    r.Otherwise(r => r.Text("Small screen"))
])
```

### Otherwise - Fallback

The `Otherwise()` helper creates a condition that always matches. Use it as the last branch to provide a fallback when no other conditions match:

```csharp
ctx.Responsive(r => [
    r.WhenMinWidth(80, r => r.Text("Wide")),
    r.Otherwise(r => r.Text("Fallback for narrow terminals"))
])
```

::: warning No Matching Conditions
If no conditions match and there's no `Otherwise()` fallback, the ResponsiveWidget renders nothing (returns zero size). Always provide a fallback to ensure your UI displays something.
:::

## Layout Switching

A common pattern is switching between different layout strategies based on available space:

<StaticTerminalPreview svgPath="/svg/responsive-layout-switch.svg" :code="layoutSwitchSnippet" />

**Wide terminals**: Use `HStack` to show panels side-by-side  
**Narrow terminals**: Switch to `VStack` to stack panels vertically

```csharp
ctx.Responsive(r => [
    r.WhenMinWidth(80, r => 
        r.HStack(h => [
            h.Text("Left Panel"),
            h.Text(" | "),
            h.Text("Right Panel")
        ])
    ),
    r.Otherwise(r =>
        r.VStack(v => [
            v.Text("Top Panel"),
            v.Text("─────────"),
            v.Text("Bottom Panel")
        ])
    )
])
```

## Real-World Example

Here's a responsive todo list that adapts to four different terminal sizes:

<CodeBlock lang="csharp" :code="todoCode" command="dotnet run" example="responsive-todo" exampleTitle="Responsive - Todo List" />

**150+ columns**: Three-column layout with list, add form, and statistics  
**110-149 columns**: Two-column layout with list and combined form/stats  
**70-109 columns**: Single column with all features visible  
**< 70 columns**: Minimal compact layout

Each layout function builds a completely different widget tree optimized for its size range, demonstrating how ResponsiveWidget enables truly adaptive UIs.

## ConditionalWidget

`ConditionalWidget` is the building block of responsive layouts. It pairs a condition with content:

```csharp
new ConditionalWidget(
    condition: (width, height) => width >= 80,
    content: new TextBlockWidget("Wide content")
)
```

::: tip Direct Usage Not Recommended
While you can construct `ConditionalWidget` directly, it's not meant to be used standalone—it must be inside a `ResponsiveWidget` to evaluate. Use the fluent helpers (`WhenMinWidth`, `When`, etc.) instead, which create ConditionalWidgets for you.
:::

The condition receives:
- `width` - Maximum width from parent constraints
- `height` - Maximum height from parent constraints

Return `true` if the content should be displayed, `false` otherwise.

## Focus and State

ResponsiveWidget preserves focus and state when switching between layouts:

```csharp
var state = new FormState();

ctx.Responsive(r => [
    r.WhenMinWidth(60, r =>
        r.VStack(v => [
            v.TextBox(state.Name).OnTextChanged(e => state.Name = e.NewText),
            v.Button("Submit").OnClick(_ => state.Submit())
        ])
    ),
    r.Otherwise(r =>
        r.VStack(v => [
            v.TextBox(state.Name).OnTextChanged(e => state.Name = e.NewText),
            v.Button("OK").OnClick(_ => state.Submit())  // Different label, same state
        ])
    )
])
```

When the layout switches:
- **Focus** is re-evaluated based on the new active branch
- **State** is preserved because it lives outside the widget tree
- **Reconciliation** updates only what changed in the new layout

::: warning Focus Behavior on Layout Switch
When ResponsiveWidget switches to a different branch, the focus ring is rebuilt for the new active child. If the previously focused widget no longer exists in the new layout, focus moves to the first focusable widget in the new branch.
:::

## Nesting Responsive Widgets

You can nest ResponsiveWidgets to create complex adaptive behaviors:

```csharp
ctx.Responsive(r => [
    r.WhenMinWidth(100, r =>
        // Outer responsive: wide layout
        r.HStack(h => [
            h.Text("Sidebar"),
            h.Responsive(r => [
                // Inner responsive: content adapts to remaining width
                r.WhenMinWidth(60, r => r.Text("Wide content")),
                r.Otherwise(r => r.Text("Narrow content"))
            ])
        ])
    ),
    r.Otherwise(r => r.Text("Mobile layout"))
])
```

Each ResponsiveWidget independently evaluates its conditions based on the constraints it receives from its parent.

## Common Patterns

### Dashboard with Optional Details

```csharp
ctx.Responsive(r => [
    r.WhenMinWidth(120, r =>
        r.HStack(h => [
            h.Chart(data).FillWidth(2),
            h.Details(data).FillWidth(1)  // Details only on wide screens
        ])
    ),
    r.Otherwise(r => r.Chart(data))  // Chart only on narrow screens
])
```

### Form with Inline vs. Stacked Labels

```csharp
ctx.Responsive(r => [
    r.WhenMinWidth(80, r =>
        // Wide: Labels inline with inputs
        r.VStack(v => [
            v.HStack(h => [
                h.Text("Name: ").FixedWidth(12),
                h.TextBox(state.Name)
            ]),
            v.HStack(h => [
                h.Text("Email: ").FixedWidth(12),
                h.TextBox(state.Email)
            ])
        ])
    ),
    r.Otherwise(r =>
        // Narrow: Labels above inputs
        r.VStack(v => [
            v.Text("Name:"),
            v.TextBox(state.Name),
            v.Text("Email:"),
            v.TextBox(state.Email)
        ])
    )
])
```

### Navigation with Collapsible Sidebar

```csharp
ctx.Responsive(r => [
    r.WhenMinWidth(100, r =>
        r.Splitter(
            r.NavigationMenu(items),  // Full sidebar
            r.Content(),
            leftWidth: 30
        )
    ),
    r.Otherwise(r =>
        r.VStack(v => [
            v.CompactNav(items),  // Collapsed nav
            v.Content()
        ])
    )
])
```

## Related Widgets

- [VStack & HStack](/guide/widgets/stacks) - Layout containers used within responsive branches
- [Border](/guide/widgets/containers) - Container widgets that work well with responsive layouts
- [Layout System](/guide/layout) - Understanding constraints and how responsive conditions work
