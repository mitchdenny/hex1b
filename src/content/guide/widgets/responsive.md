<script setup>
import basicSnippet from './snippets/responsive-basic.cs?raw'
import layoutSnippet from './snippets/responsive-layout.cs?raw'
import multipleSnippet from './snippets/responsive-multiple.cs?raw'
import customSnippet from './snippets/responsive-custom.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx =>
{
    var navPanel = ctx.ThemePanel(theme => theme
        .Set(BorderTheme.BorderColor, Hex1bColor.Cyan),
        t => [
            t.Border(b => [
                b.Text("📋 Navigation"),
                b.Text("• Dashboard"),
                b.Text("• Settings")
            ], title: "Menu")
        ]);
    
    var primaryPanel = ctx.ThemePanel(theme => theme
        .Set(BorderTheme.BorderColor, Hex1bColor.Green),
        t => [
            t.Border(b => [
                b.Text("📊 Primary Content"),
                b.Text("Main view - always visible"),
                b.Text("💚 Breakpoint: >= 100")
            ], title: "Dashboard")
        ]);
    
    var secondaryPanel = ctx.ThemePanel(theme => theme
        .Set(BorderTheme.BorderColor, Hex1bColor.Yellow),
        t => [
            t.Border(b => [
                b.Text("📈 Secondary Content"),
                b.Text("Visible when width >= 120"),
                b.Text("💛 Breakpoint: >= 120")
            ], title: "Analytics")
        ]);
    
    return ctx.Responsive(r => [
        // Extra Wide: Nav | Primary + Secondary side-by-side
        r.WhenMinWidth(120, r =>
            r.HSplitter(
                navPanel,
                r.HStack(h => [
                    h.Layout(primaryPanel).FillWidth(3),
                    h.Layout(secondaryPanel).FillWidth(2)
                ]),
                leftWidth: 25
            )
        ),
        
        // Wide: Nav | Primary + Secondary stacked
        r.WhenMinWidth(100, r =>
            r.HSplitter(
                navPanel,
                r.VStack(v => [
                    v.Layout(primaryPanel).FillHeight(3),
                    v.Layout(secondaryPanel).FillHeight(2)
                ]),
                leftWidth: 25
            )
        ),
        
        // Medium: Nav | Primary only
        r.WhenMinWidth(80, r =>
            r.HSplitter(navPanel, primaryPanel, leftWidth: 25)
        ),
        
        // Narrow: All stacked
        r.Otherwise(r =>
            r.VStack(v => [
                v.Layout(navPanel).FixedHeight(10),
                v.Layout(primaryPanel).FillHeight()
            ])
        )
    ]);
});

await app.RunAsync();`
</script>

# ResponsiveWidget

Create adaptive UI layouts that change based on terminal size.

ResponsiveWidget displays the first child whose condition evaluates to true, allowing you to create terminal applications that adapt their layout based on available space. This is similar to CSS media queries or responsive web design, but for terminal user interfaces.

## Basic Usage

Create a responsive layout using the fluent API with condition builders. The example below shows a complete application layout with navigation, primary content, and secondary content panels that reorganize based on terminal width:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="responsive-basic" exampleTitle="Responsive Layout Demo" />

This demo showcases:
- **Theme panels** with color-coded borders indicating panel priority (cyan for navigation, green for primary, yellow for secondary)
- **Splitter layout** separating navigation from content in wider views
- **HStack/VStack switching** - content panels appear side-by-side in extra wide terminals (≥120 cols) and stack vertically in wide terminals (≥100 cols)
- **Progressive degradation** - secondary panel disappears in medium terminals (≥80 cols), and splitter becomes vertical stack in narrow terminals (<80 cols)

::: tip Dynamic Evaluation
Conditions are evaluated during the layout phase using the actual available space from parent constraints. This happens automatically whenever the terminal is resized or the layout changes. Try resizing your browser window in the demo above to see the layout adapt!
:::

## Simple Breakpoint

The simplest responsive pattern switches between two layouts based on terminal width:

<StaticTerminalPreview svgPath="/svg/responsive-basic-wide.svg" :code="basicSnippet" />

When the terminal is wide enough (≥80 columns), the first condition matches:

<StaticTerminalPreview svgPath="/svg/responsive-basic-narrow.svg" :code="basicSnippet" />

When the terminal is narrower (<80 columns), the `Otherwise` fallback is used.

## Responsive Layouts

A common pattern is switching between horizontal and vertical layouts based on available width:

<StaticTerminalPreview svgPath="/svg/responsive-layout-wide.svg" :code="layoutSnippet" />

In wide terminals (≥100 columns), content appears side-by-side:

<StaticTerminalPreview svgPath="/svg/responsive-layout-narrow.svg" :code="layoutSnippet" />

In narrow terminals, the same content stacks vertically to fit the available space.

## Multiple Breakpoints

Define multiple conditions to create progressively enhanced layouts:

<StaticTerminalPreview svgPath="/svg/responsive-multiple-extrawide.svg" :code="multipleSnippet" />

Extra wide terminals (≥120 columns) show three columns:

<StaticTerminalPreview svgPath="/svg/responsive-multiple-wide.svg" :code="multipleSnippet" />

Wide terminals (≥80 columns) show two columns:

<StaticTerminalPreview svgPath="/svg/responsive-multiple-medium.svg" :code="multipleSnippet" />

Medium terminals (≥40 columns) show a single column with details:

<StaticTerminalPreview svgPath="/svg/responsive-multiple-narrow.svg" :code="multipleSnippet" />

Narrow terminals (<40 columns) show minimal compact view.

::: tip Evaluation Order
Conditions are evaluated in order from top to bottom. The first matching condition wins, so place more specific conditions before less specific ones.
:::

## Condition Builders

ResponsiveWidget provides several helper methods for common patterns:

### WhenMinWidth

Test for a minimum terminal width:

```csharp
ctx.Responsive(r => [
    r.WhenMinWidth(100, r => r.Text("Wide")),
    r.Otherwise(r => r.Text("Narrow"))
])
```

### WhenWidth

Custom width-only condition:

```csharp
ctx.Responsive(r => [
    r.WhenWidth(w => w >= 80 && w < 120, r => r.Text("Medium")),
    r.WhenWidth(w => w >= 120, r => r.Text("Wide")),
    r.Otherwise(r => r.Text("Narrow"))
])
```

### When

Full control with width and height:

<StaticTerminalPreview svgPath="/svg/responsive-layout-wide.svg" :code="customSnippet" />

The `When` method receives both `availableWidth` and `availableHeight` parameters.

### Otherwise

Fallback for when no other condition matches:

```csharp
ctx.Responsive(r => [
    r.WhenMinWidth(100, r => r.Text("Specific layout")),
    r.WhenMinWidth(50, r => r.Text("Another layout")),
    r.Otherwise(r => r.Text("Default fallback"))
])
```

::: warning Always Include Otherwise
Always include an `Otherwise` branch as your last condition to ensure something is displayed even if no other condition matches.
:::

## How It Works

ResponsiveWidget works by:

1. **Reconciliation**: All child branches are reconciled into nodes upfront
2. **Evaluation**: During `Measure()`, conditions are evaluated against available space
3. **Activation**: Only the first matching branch's node is measured, arranged, and rendered
4. **Re-evaluation**: If terminal size changes, conditions are re-evaluated automatically

```
Terminal Resize → Layout Pass → Measure() → Evaluate Conditions → Render Active Branch
```

### State Preservation

Each branch maintains its own focus state and internal state:

```csharp
var listState = new ListState();

ctx.Responsive(r => [
    r.WhenMinWidth(100, r => 
        r.List(items).WithState(listState)  // Wide layout
    ),
    r.Otherwise(r => 
        r.List(items).WithState(listState)  // Narrow layout - same state
    )
])
```

When switching between branches due to resize, the shared state (like `listState`) is preserved.

::: tip State Management
For widgets with internal state (like List or TextBox), use the state management pattern to share state across responsive branches. This prevents losing focus or scroll position when the layout switches.
:::

## Layout Behavior

ResponsiveWidget has minimal overhead:

- **Measuring**: The active branch determines the size
- **Arranging**: The active branch fills the entire bounds
- **Rendering**: Only the active branch renders
- **Focus**: Only the active branch's focusable nodes are available

Inactive branches are reconciled but not measured, arranged, or rendered.

## Common Patterns

### Dashboard Layouts

```csharp
ctx.Responsive(r => [
    // Desktop: Multi-column dashboard
    r.WhenMinWidth(120, r =>
        r.HStack(h => [
            h.Border(b => statsWidget, title: "Stats").FillWidth(1),
            h.Border(b => mainWidget, title: "Main").FillWidth(2),
            h.Border(b => activityWidget, title: "Activity").FillWidth(1)
        ])
    ),
    
    // Tablet: Two-column
    r.WhenMinWidth(80, r =>
        r.HStack(h => [
            h.VStack(v => [
                v.Border(b => statsWidget, title: "Stats"),
                v.Border(b => activityWidget, title: "Activity")
            ]).FillWidth(1),
            h.Border(b => mainWidget, title: "Main").FillWidth(2)
        ])
    ),
    
    // Mobile: Single column
    r.Otherwise(r =>
        r.VStack(v => [
            v.Border(b => statsWidget, title: "Stats"),
            v.Border(b => mainWidget, title: "Main"),
            v.Border(b => activityWidget, title: "Activity")
        ])
    )
])
```

### Form Layouts

```csharp
ctx.Responsive(r => [
    // Wide: Labels and inputs side-by-side
    r.WhenMinWidth(80, r =>
        r.VStack(v => [
            v.HStack(h => [
                h.Text("Name:").FixedWidth(20),
                h.TextBox(state.Name).FillWidth()
            ]),
            v.HStack(h => [
                h.Text("Email:").FixedWidth(20),
                h.TextBox(state.Email).FillWidth()
            ])
        ])
    ),
    
    // Narrow: Labels above inputs
    r.Otherwise(r =>
        r.VStack(v => [
            v.Text("Name:"),
            v.TextBox(state.Name),
            v.Text("Email:"),
            v.TextBox(state.Email)
        ])
    )
])
```

### Detail/Master Views

```csharp
ctx.Responsive(r => [
    // Wide: Side-by-side
    r.WhenMinWidth(100, r =>
        r.HStack(h => [
            h.List(items).FillWidth(1),
            h.Border(b => detailView, title: "Details").FillWidth(2)
        ])
    ),
    
    // Narrow: List only (navigate to see details)
    r.Otherwise(r =>
        r.List(items)
    )
])
```

### Adaptive Controls

Show different levels of detail in controls:

```csharp
ctx.Responsive(r => [
    // Wide: Full control with labels
    r.WhenMinWidth(80, r =>
        r.HStack(h => [
            h.Button("Save (Ctrl+S)").OnClick(save),
            h.Button("Cancel (Esc)").OnClick(cancel),
            h.Button("Help (F1)").OnClick(help)
        ])
    ),
    
    // Narrow: Icon/short text only
    r.Otherwise(r =>
        r.HStack(h => [
            h.Button("[S]").OnClick(save),
            h.Button("[C]").OnClick(cancel),
            h.Button("[?]").OnClick(help)
        ])
    )
])
```

## Testing Responsive Layouts

Test your responsive layouts at different terminal sizes:

```csharp
[Theory]
[InlineData(120, 30)]  // Wide
[InlineData(80, 30)]   // Medium  
[InlineData(40, 30)]   // Narrow
public void ResponsiveLayout_AdaptsToTerminalSize(int width, int height)
{
    using var terminal = new Hex1bTerminal(width, height);
    using var app = new Hex1bApp(ctx =>
        ctx.Responsive(r => [
            r.WhenMinWidth(100, r => r.Text("Wide")),
            r.WhenMinWidth(60, r => r.Text("Medium")),
            r.Otherwise(r => r.Text("Narrow"))
        ])
    );
    
    // Verify the correct layout is active
    // ...
}
```

## Performance Considerations

- **Reconciliation**: All branches are reconciled once at startup
- **Switching**: Changing branches during resize is very fast (no re-reconciliation)
- **Memory**: All branches remain in memory (inactive nodes are not disposed)

For most applications, this overhead is negligible. If you have many complex branches or very deep widget trees, consider:

- Lazy loading expensive widgets only when their branch activates
- Sharing common widgets across branches
- Using fewer breakpoints

## Related Widgets

- [HStackWidget](/guide/widgets/hstack) - Horizontal layouts often used in wide breakpoints
- [VStackWidget](/guide/widgets/vstack) - Vertical layouts often used in narrow breakpoints
- [BorderWidget](/guide/widgets/border) - Often combined with responsive layouts for visual structure
- [SplitterWidget](/guide/widgets/splitter) - Divides space between panels, useful in responsive layouts
