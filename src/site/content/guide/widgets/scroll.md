<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode → src/Hex1b.Website/Examples/ScrollBasicExample.cs
  - horizontalCode → src/Hex1b.Website/Examples/ScrollHorizontalExample.cs
  - eventCode → src/Hex1b.Website/Examples/ScrollEventExample.cs
  - trackingCode → src/Hex1b.Website/Examples/ScrollTrackingExample.cs
  - infiniteCode → src/Hex1b.Website/Examples/ScrollInfiniteExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import noScrollbarSnippet from './snippets/scroll-no-scrollbar.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(
        ctx.VScroll(
            v => [
                v.Text("═══ Scrollable Content ═══"),
                v.Text(""),
                v.Text("This content scrolls vertically."),
                v.Text("Use arrow keys ↑↓ to scroll."),
                v.Text(""),
                v.Text("Line 6"),
                v.Text("Line 7"),
                v.Text("Line 8"),
                v.Text("Line 9"),
                v.Text("Line 10"),
                v.Text("Line 11"),
                v.Text("Line 12"),
                v.Text(""),
                v.Text("── End of Content ──")
            ]
        ),
        title: "Scroll Demo"
    ))
    .Build();

await terminal.RunAsync();`

const horizontalCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(
        ctx.VStack(v => [
            v.Text("Wide content below - use ← → to scroll:"),
            v.Text(""),
            v.HScroll(
                h => [
                    h.Text("START | Column 1 | Column 2 | Column 3 | Column 4 | Column 5 | Column 6 | Column 7 | Column 8 | END"),
                ]
            ),
        ]),
        title: "Horizontal Scroll"
    ))
    .Build();

await terminal.RunAsync();`

const eventCode = `using Hex1b;

int currentOffset = 0;
int maxOffset = 0;
int contentSize = 0;
int viewportSize = 0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text($"Position: {currentOffset}/{maxOffset}"),
        v.Text($"Content: {contentSize} lines, Viewport: {viewportSize} lines"),
        v.Text(""),
        v.Border(
            v.VScroll(
                inner => [
                    inner.Text("Line 1 - Scroll to see position update"),
                    inner.Text("Line 2"),
                    inner.Text("Line 3"),
                    inner.Text("Line 4"),
                    inner.Text("Line 5"),
                    inner.Text("Line 6"),
                    inner.Text("Line 7"),
                    inner.Text("Line 8"),
                    inner.Text("Line 9"),
                    inner.Text("Line 10"),
                    inner.Text("Line 11"),
                    inner.Text("Line 12 - End"),
                ]
            ).OnScroll(args => {
                currentOffset = args.Offset;
                maxOffset = args.MaxOffset;
                contentSize = args.ContentSize;
                viewportSize = args.ViewportSize;
            }),
            title: "Scrollable Area"
        )
    ]))
    .Build();

await terminal.RunAsync();`

const trackingCode = `using Hex1b;

var items = Enumerable.Range(1, 50).Select(i => $"Item {i}").ToList();
int scrollPosition = 0;
int viewportSize = 0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        var totalContent = items.Count;
        var endVisible = Math.Min(scrollPosition + viewportSize, totalContent);
        
        return ctx.VStack(v => [
            v.Text($"Viewing: {scrollPosition + 1} - {endVisible} of {totalContent}"),
            v.Text(""),
            v.Border(
                v.VScroll(
                    inner => items.Select(item => inner.Text(item)).ToArray()
                ).OnScroll(args => {
                    scrollPosition = args.Offset;
                    viewportSize = args.ViewportSize;
                }),
                title: "Scrollable List"
            )
        ]);
    })
    .Build();

await terminal.RunAsync();`

const infiniteCode = `using Hex1b;

var loadedItems = Enumerable.Range(1, 20).Select(i => $"Item {i}").ToList();
int loadCount = 1;
string status = "Scroll down to load more...";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text($"Loaded: {loadedItems.Count} items (batch {loadCount})"),
        v.Text(status),
        v.Text(""),
        v.Border(
            v.VScroll(
                inner => loadedItems.Select(item => inner.Text(item)).ToArray()
            ).OnScroll(args => {
                // Load more when scrolled past 80%
                if (args.Progress > 0.8 && args.IsScrollable)
                {
                    loadCount++;
                    var startIndex = loadedItems.Count + 1;
                    var newItems = Enumerable.Range(startIndex, 10)
                        .Select(i => $"Item {i} (batch {loadCount})")
                        .ToList();
                    loadedItems.AddRange(newItems);
                    status = $"Loaded batch {loadCount}!";
                }
            }),
            title: "Infinite Scroll"
        )
    ]))
    .Build();

await terminal.RunAsync();`
</script>

# Scroll

A container widget that provides scrolling capability for content that exceeds the available space.

The scroll widget displays a scrollbar indicator and handles keyboard navigation to move through content that doesn't fit in the visible viewport. It supports both vertical and horizontal scrolling orientations.

## Basic Usage

Create a vertical scroll widget using the fluent API. The scroll widget automatically shows a scrollbar when content exceeds the viewport:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="scroll-basic" exampleTitle="Scroll Widget - Basic Usage" />

::: tip Keyboard Navigation
Use **↑↓** arrow keys to scroll vertically, or **←→** for horizontal scrolling. **PgUp/PgDn** jumps by pages, and **Home/End** jumps to the start or end of content.
:::

## Horizontal Scrolling

For wide content like tables or long text lines, use `HScroll()`:

<CodeBlock lang="csharp" :code="horizontalCode" command="dotnet run" example="scroll-horizontal" exampleTitle="Scroll Widget - Horizontal" />

The scrollbar appears at the bottom when content width exceeds the viewport width.

## Scroll Events

Use the `OnScroll()` event handler to react to scroll position changes. The `ScrollChangedEventArgs` provides comprehensive information about the current scroll state:

<CodeBlock lang="csharp" :code="eventCode" command="dotnet run" example="scroll-event" exampleTitle="Scroll Widget - Event Handling" />

### ScrollChangedEventArgs Properties

| Property | Type | Description |
|----------|------|-------------|
| `Offset` | `int` | Current scroll offset after the change |
| `PreviousOffset` | `int` | Scroll offset before the change |
| `ContentSize` | `int` | Total size of content in characters |
| `ViewportSize` | `int` | Size of visible viewport in characters |
| `MaxOffset` | `int` | Maximum scroll offset (computed) |
| `IsScrollable` | `bool` | Whether content exceeds viewport |
| `Progress` | `double` | Scroll position as 0.0-1.0 value |
| `IsAtStart` | `bool` | True when scrolled to the beginning |
| `IsAtEnd` | `bool` | True when scrolled to the end |

### Tracking Scroll Position

Display the current scroll position in your UI:

<CodeBlock lang="csharp" :code="trackingCode" command="dotnet run" example="scroll-tracking" exampleTitle="Scroll Widget - Position Tracking" />

### Infinite Scroll

Load more content when the user scrolls near the end:

<CodeBlock lang="csharp" :code="infiniteCode" command="dotnet run" example="scroll-infinite" exampleTitle="Scroll Widget - Infinite Scroll" />

## Scrollbar Visibility

Control whether the scrollbar is displayed:

<StaticTerminalPreview svgPath="/svg/scroll-no-scrollbar.svg" :code="noScrollbarSnippet" />

Set `showScrollbar: false` to hide the scrollbar. The content remains scrollable via keyboard, but no visual indicator appears.

```csharp
// With scrollbar (default)
ctx.VScroll(v => [...])

// Without scrollbar
ctx.VScroll(v => [...], showScrollbar: false)
```

## Keyboard Shortcuts

When the scroll widget is focused, these keys control scrolling:

| Key | Action |
|-----|--------|
| `↑` | Scroll up one line (vertical) |
| `↓` | Scroll down one line (vertical) |
| `←` | Scroll left one column (horizontal) |
| `→` | Scroll right one column (horizontal) |
| `PgUp` | Scroll up one page |
| `PgDn` | Scroll down one page |
| `Home` | Jump to start |
| `End` | Jump to end |

## Focus Management

Scroll widgets are focusable and can contain other focusable widgets:

- **Tab** moves focus from the scroll widget to the first focusable child
- **Shift+Tab** moves focus from children back to the scroll widget
- Arrow keys scroll when the scroll widget itself is focused
- Arrow keys navigate within children when a child is focused

```csharp
ctx.VScroll(
    v => [
        v.Text("Use Tab to focus the button below"),
        v.Button("Click Me").OnClick(_ => { /* ... */ }),
        v.Text("More content..."),
        v.Button("Another Button").OnClick(_ => { /* ... */ })
    ]
)
```

The scroll widget automatically scrolls to keep focused children visible within the viewport.

## Layout Behavior

By default, scroll widgets fill available space. Use layout extensions to control size:

```csharp
// Fill available space (default)
ctx.VScroll(v => [...])

// Fixed height
ctx.VScroll(v => [...]).FixedHeight(10)

// Fixed width (for horizontal scroll)
ctx.HScroll(h => [...]).FixedWidth(50)

// Combined with other constraints
ctx.VScroll(v => [...])
    .FixedHeight(15)
    .Fill()  // Fill width
```

::: warning Content Size
The scroll widget measures its child content without constraints to determine the full content size. For vertical scrolling, this means content can be arbitrarily tall; for horizontal, arbitrarily wide. Be mindful when scrolling very large datasets (e.g., thousands of items) as all content must be measured and rendered, which may impact performance. For large lists, consider virtualization patterns or pagination.
:::

## Theming

Customize scrollbar appearance using theme elements:

```csharp
var theme = Hex1bTheme.Create()
    .Set(ScrollTheme.VerticalThumbCharacter, "█")
    .Set(ScrollTheme.VerticalTrackCharacter, "░")
    .Set(ScrollTheme.ThumbColor, Hex1bColor.Cyan)
    .Set(ScrollTheme.FocusedThumbColor, Hex1bColor.Yellow)
    .Set(ScrollTheme.TrackColor, Hex1bColor.DarkGray);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        options.Theme = theme;
        return ctx => /* ... */;
    })
    .Build();

await terminal.RunAsync();
```

### Available Theme Elements

| Element | Type | Default | Description |
|---------|------|---------|-------------|
| `VerticalThumbCharacter` | `string` | `█` | Character for vertical scrollbar thumb |
| `VerticalTrackCharacter` | `string` | `░` | Character for vertical scrollbar track |
| `HorizontalThumbCharacter` | `string` | `█` | Character for horizontal scrollbar thumb |
| `HorizontalTrackCharacter` | `string` | `░` | Character for horizontal scrollbar track |
| `ThumbColor` | `Hex1bColor` | Gray | Color of scrollbar thumb |
| `FocusedThumbColor` | `Hex1bColor` | White | Color of thumb when scroll widget is focused |
| `TrackColor` | `Hex1bColor` | DarkGray | Color of scrollbar track |
| `UpArrowCharacter` | `string` | `▲` | Up arrow for vertical scrollbar |
| `DownArrowCharacter` | `string` | `▼` | Down arrow for vertical scrollbar |
| `LeftArrowCharacter` | `string` | `◀` | Left arrow for horizontal scrollbar |
| `RightArrowCharacter` | `string` | `▶` | Right arrow for horizontal scrollbar |

## Common Patterns

### Scrollable List of Items

```csharp
ctx.VScroll(
    v => items.Select(item => v.Text(item)).ToArray()
)
```

### Scrollable Text Content

```csharp
var lines = File.ReadAllLines("document.txt");
ctx.VScroll(
    v => lines.Select(line => v.Text(line)).ToArray()
)
```

### Nested Scrolling

```csharp
ctx.VStack(v => [
    v.Text("Outer container"),
    v.VScroll(
        inner => [
            inner.Text("Scrollable section 1"),
            inner.Text("Content...")
        ]
    ).FixedHeight(5),
    v.Text("Between sections"),
    v.VScroll(
        inner => [
            inner.Text("Scrollable section 2"),
            inner.Text("More content...")
        ]
    ).FixedHeight(5)
])
```

Each scroll widget maintains its own independent scroll position (managed internally by the node).

### Tracking Scroll Position for UI Display

```csharp
// Use local variables to capture scroll state for display
int scrollPosition = 0;
int totalContent = 0;

ctx.VStack(v => [
    v.Text($"Viewing: {scrollPosition + 1} - {Math.Min(scrollPosition + 10, totalContent)} of {totalContent}"),
    v.VScroll(
        inner => items.Select(item => inner.Text(item)).ToArray()
    ).OnScroll(args => {
        scrollPosition = args.Offset;
        totalContent = args.ContentSize;
    })
])
```

## Related Widgets

- [List](/guide/widgets/list) - Selectable list with built-in scrolling
- [VStack/HStack](/guide/widgets/stacks) - Layout containers for scroll content
- [Border](/guide/widgets/containers) - Often combined with scroll for visual boundaries
- [TextBox](/guide/widgets/textbox) - Input widget with internal scrolling
