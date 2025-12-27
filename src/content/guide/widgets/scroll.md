<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode → src/Hex1b.Website/Examples/ScrollExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import verticalSnippet from './snippets/scroll-vertical-basic.cs?raw'
import horizontalSnippet from './snippets/scroll-horizontal-basic.cs?raw'
import stateSnippet from './snippets/scroll-with-state.cs?raw'
import noScrollbarSnippet from './snippets/scroll-no-scrollbar.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.Border(
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
    )
));

await app.RunAsync();`

const stateCode = `using Hex1b;
using Hex1b.Widgets;

var scrollState = new ScrollState();

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text($"Position: {scrollState.Offset}/{scrollState.MaxOffset}"),
        v.Text($"Content: {scrollState.ContentSize} lines"),
        v.Text($"Viewport: {scrollState.ViewportSize} lines"),
        v.Text(""),
        v.Border(
            v.VScroll(
                inner => [
                    inner.Text("Content line 1"),
                    inner.Text("Content line 2"),
                    inner.Text("Content line 3"),
                    inner.Text("Content line 4"),
                    inner.Text("Content line 5"),
                    inner.Text("Content line 6"),
                    inner.Text("Content line 7"),
                    inner.Text("Content line 8")
                ],
                scrollState
            ),
            title: "Scrollable Area"
        ),
        v.Text(""),
        v.HStack(h => [
            h.Button("Page Up").OnClick(_ => scrollState.PageUp()),
            h.Text(" "),
            h.Button("Page Down").OnClick(_ => scrollState.PageDown()),
            h.Text(" "),
            h.Button("To Top").OnClick(_ => scrollState.ScrollToStart()),
            h.Text(" "),
            h.Button("To End").OnClick(_ => scrollState.ScrollToEnd())
        ])
    ])
));

await app.RunAsync();`
</script>

# Scroll

A container widget that provides scrolling capability for content that exceeds the available space.

The scroll widget displays a scrollbar indicator and handles keyboard navigation to move through content that doesn't fit in the visible viewport. It supports both vertical and horizontal scrolling orientations.

## Basic Usage

Create a vertical scroll widget using the fluent API. The scroll widget automatically shows a scrollbar when content exceeds the viewport:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="scroll" exampleTitle="Scroll Widget - Basic Usage" />

::: tip Keyboard Navigation
Use **↑↓** arrow keys to scroll vertically, or **←→** for horizontal scrolling. **PgUp/PgDn** jumps by pages, and **Home/End** jumps to the start or end of content.
:::

## Vertical Scrolling

The most common scrolling direction for lists, text content, and menus:

<StaticTerminalPreview svgPath="/svg/scroll-vertical-basic.svg" :code="verticalSnippet" />

Use `VScroll()` to create a vertical scroll widget. The scrollbar appears on the right side when content height exceeds the viewport height.

## Horizontal Scrolling

For wide content like tables or long text lines:

<StaticTerminalPreview svgPath="/svg/scroll-horizontal-basic.svg" :code="horizontalSnippet" />

Use `HScroll()` to create a horizontal scroll widget. The scrollbar appears at the bottom when content width exceeds the viewport width.

## Scroll State

The `ScrollState` object tracks the current scroll position and provides methods for programmatic scrolling:

<CodeBlock lang="csharp" :code="stateCode" command="dotnet run" example="scroll" exampleTitle="Scroll Widget - State Management" />

### ScrollState Properties

| Property | Type | Description |
|----------|------|-------------|
| `Offset` | `int` | Current scroll offset (read/write) |
| `ContentSize` | `int` | Total size of content in characters (read-only) |
| `ViewportSize` | `int` | Size of visible viewport in characters (read-only) |
| `MaxOffset` | `int` | Maximum scroll offset (computed) |
| `IsScrollable` | `bool` | Whether content exceeds viewport (computed) |

### ScrollState Methods

```csharp
// Scroll by single lines/columns
scrollState.ScrollUp();      // Up or left by 1
scrollState.ScrollDown();    // Down or right by 1

// Scroll by custom amounts
scrollState.ScrollUp(5);     // Up or left by 5
scrollState.ScrollDown(3);   // Down or right by 3

// Page scrolling (by viewport size)
scrollState.PageUp();        // Up or left by one page
scrollState.PageDown();      // Down or right by one page

// Jump to boundaries
scrollState.ScrollToStart(); // Jump to beginning
scrollState.ScrollToEnd();   // Jump to end
```

::: tip State Persistence
Create a `ScrollState` instance and pass it to the scroll widget to track position across re-renders. Without an explicit state, a new state is created each time, causing the scroll position to reset to the top whenever the widget rebuilds.

```csharp
// ✅ Position preserved across re-renders
var scrollState = new ScrollState();
ctx.VScroll(v => [...], scrollState)

// ❌ Position resets to top on each re-render
ctx.VScroll(v => [...])  // New state created each time
```
:::

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
| `↑` or `k` | Scroll up one line (vertical) |
| `↓` or `j` | Scroll down one line (vertical) |
| `←` or `h` | Scroll left one column (horizontal) |
| `→` or `l` | Scroll right one column (horizontal) |
| `PgUp` | Scroll up one page |
| `PgDn` | Scroll down one page |
| `Home` | Jump to start |
| `End` | Jump to end |

::: tip Vim-style Navigation
The scroll widget supports Vim-style navigation keys (`h`, `j`, `k`, `l`) in addition to arrow keys for users familiar with Vim keybindings.
:::

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

var app = new Hex1bApp(options => {
    options.Theme = theme;
}, ctx => /* ... */);
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

Each scroll widget maintains its own independent scroll position.

### Programmatic Scrolling

```csharp
var scrollState = new ScrollState();

// In event handlers
buttonNode.OnClick(_ => {
    scrollState.ScrollToEnd();
    app.Invalidate();  // Trigger re-render
});
```

## Related Widgets

- [List](/guide/widgets/list) - Selectable list with built-in scrolling
- [VStack/HStack](/guide/widgets/stacks) - Layout containers for scroll content
- [Border](/guide/widgets/containers) - Often combined with scroll for visual boundaries
- [TextBox](/guide/widgets/textbox) - Input widget with internal scrolling
