<!--
  NOTE: This documentation describes a proposed DrawerWidget that is not yet implemented.
  Live demos will be available once the widget is added to the Hex1b library.
  WebSocket examples should be created in src/Hex1b.Website/Examples/ when implementing.
-->
<script setup>
import collapsedSnippet from './snippets/drawer-collapsed.cs?raw'
import expandedSnippet from './snippets/drawer-expanded.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Widgets;

var drawerExpanded = false;

var app = new Hex1bApp(ctx =>
    ctx.Drawer(
        isExpanded: drawerExpanded,
        onToggle: expanded => drawerExpanded = expanded,
        header: ctx.Text("📁 Files"),
        content: ctx.VStack(v => [
            v.Text("Documents"),
            v.Text("Downloads"),
            v.Text("Pictures"),
            v.Text("Videos")
        ])
    )
);

await app.RunAsync();`

const overlayCode = `using Hex1b;
using Hex1b.Widgets;

var drawerExpanded = false;

var app = new Hex1bApp(ctx =>
    ctx.ZStack(z => [
        // Main content behind the drawer
        z.VStack(v => [
            v.Text("Main Application Content"),
            v.Text(""),
            v.Text("This content is always visible."),
            v.Text("The drawer overlays on top when expanded.")
        ]),
        // Overlay drawer
        z.Drawer(
            isExpanded: drawerExpanded,
            onToggle: expanded => drawerExpanded = expanded,
            header: ctx.Text("⚙️ Settings"),
            content: ctx.VStack(v => [
                v.Text("Theme: Dark"),
                v.Text("Font Size: 14"),
                v.Text("Auto-save: On")
            ]),
            mode: DrawerMode.Overlay
        )
    ])
);

await app.RunAsync();`

const positionCode = `using Hex1b;
using Hex1b.Widgets;

var leftExpanded = true;
var rightExpanded = false;

var app = new Hex1bApp(ctx =>
    ctx.HStack(h => [
        // Left drawer (docked)
        h.Drawer(
            isExpanded: leftExpanded,
            onToggle: expanded => leftExpanded = expanded,
            header: ctx.Text("◀ Explorer"),
            content: ctx.VStack(v => [
                v.Text("src/"),
                v.Text("  Program.cs"),
                v.Text("  App.cs"),
                v.Text("tests/")
            ]),
            position: DrawerPosition.Left
        ),
        // Main content
        h.VStack(v => [
            v.Text("Editor Pane").Fill()
        ]).Fill(),
        // Right drawer
        h.Drawer(
            isExpanded: rightExpanded,
            onToggle: expanded => rightExpanded = expanded,
            header: ctx.Text("▶ Properties"),
            content: ctx.VStack(v => [
                v.Text("Name: Program.cs"),
                v.Text("Size: 2.4 KB"),
                v.Text("Modified: Today")
            ]),
            position: DrawerPosition.Right
        )
    ])
);

await app.RunAsync();`
</script>

# DrawerWidget

An expandable/collapsible panel that can contain arbitrary content, perfect for sidebars, settings panels, and navigation menus.

DrawerWidget provides a toggleable container that transitions between a compact collapsed state (showing just a header with an expander button) and an expanded state that reveals the full content. Drawers can be docked within their parent layout or displayed as an overlay above other content.

## Basic Usage

Create a simple drawer with a header and expandable content:

::: warning Widget Not Yet Implemented
The DrawerWidget is a proposed widget that is not yet implemented in Hex1b. This documentation serves as a specification for the widget's intended behavior. Live demos will be available once the widget is added to the library.
:::

```csharp-vue
{{ basicCode }}
```

The drawer displays a header row with a toggle button. Clicking the button or pressing Enter/Space when focused expands or collapses the content area.

::: tip Keyboard Navigation
- **Tab** to focus the drawer toggle button
- **Enter** or **Space** to toggle expansion
- **Tab** again to navigate into expanded content
- **Escape** to collapse and return focus to the toggle
:::

## Drawer States

### Collapsed State

When collapsed, the drawer shows only its header with an expander indicator:

<StaticTerminalPreview svgPath="/svg/drawer-collapsed.svg" :code="collapsedSnippet" />

### Expanded State

When expanded, the drawer reveals its full content area:

<StaticTerminalPreview svgPath="/svg/drawer-expanded.svg" :code="expandedSnippet" />

## Drawer Modes

DrawerWidget supports two display modes for different UI patterns:

### Docked Mode (Default)

In docked mode, the drawer is part of the normal layout flow. When expanded, it pushes adjacent content aside:

```csharp
ctx.Drawer(
    isExpanded: state.IsExpanded,
    onToggle: expanded => state.IsExpanded = expanded,
    header: ctx.Text("Navigation"),
    content: ctx.VStack(v => [
        v.Button("Home"),
        v.Button("Settings"),
        v.Button("Help")
    ]),
    mode: DrawerMode.Docked  // Default
)
```

This is ideal for:
- Sidebars that resize the main content area
- Navigation panels in IDE-style layouts
- Collapsible sections in forms

### Overlay Mode

In overlay mode, the expanded drawer renders above other content without affecting the layout:

```csharp-vue
{{ overlayCode }}
```

This is ideal for:
- Mobile-style slide-out menus
- Settings panels that don't resize content
- Quick-access tools that overlay the main view

::: warning Overlay Z-Order
When using overlay mode, ensure the drawer is rendered after the content it should overlay. In a ZStack, later children render on top.
:::

## Drawer Position

Control which edge the drawer anchors to with the `position` parameter:

```csharp-vue
{{ positionCode }}
```

| Position | Collapsed | Expanded | Use Case |
|----------|-----------|----------|----------|
| `Left` | Shows right-pointing arrow | Expands rightward | File explorers, navigation |
| `Right` | Shows left-pointing arrow | Expands leftward | Properties panels, details |
| `Top` | Shows down-pointing arrow | Expands downward | Toolbars, menus |
| `Bottom` | Shows up-pointing arrow | Expands upward | Status panels, consoles |

## Sizing

### Collapsed Size

The collapsed size is determined by the header content plus the toggle button:

```csharp
ctx.Drawer(
    header: ctx.Text("📁 Files"),  // Header determines collapsed height
    // ...
)
```

### Expanded Size

Control the expanded size using the `expandedSize` parameter:

```csharp
// Fixed expanded width (for left/right position)
ctx.Drawer(
    expandedSize: 30,  // 30 columns when expanded
    // ...
)

// Fixed expanded height (for top/bottom position)
ctx.Drawer(
    position: DrawerPosition.Top,
    expandedSize: 10,  // 10 rows when expanded
    // ...
)
```

For responsive sizing, use `SizeHint.Fill`:

```csharp
ctx.Drawer(
    expandedSizeHint: SizeHint.Fill,  // Takes available space
    // ...
)
```

## Focus Behavior

DrawerWidget participates in the focus system:

1. **Toggle button is focusable**: Tab navigation includes the drawer's toggle
2. **Content focus**: When expanded, Tab moves focus into the drawer content
3. **Collapse on Escape**: Pressing Escape collapses the drawer and returns focus to the toggle
4. **Focus preservation**: Re-expanding restores focus to the last focused item within

```csharp
ctx.Drawer(
    isExpanded: state.IsExpanded,
    onToggle: expanded => {
        state.IsExpanded = expanded;
        if (!expanded) {
            // Optional: Handle collapse event
            state.LastFocusedItem = null;
        }
    },
    header: ctx.Text("Menu"),
    content: ctx.VStack(v => [
        v.Button("Option A"),  // Can receive focus when expanded
        v.Button("Option B"),
        v.Button("Option C")
    ])
)
```

## Theming

Customize the drawer appearance using theme elements:

```csharp
var theme = Hex1bTheme.Create()
    .Set(DrawerTheme.HeaderBackground, Hex1bColor.DarkBlue)
    .Set(DrawerTheme.HeaderForeground, Hex1bColor.White)
    .Set(DrawerTheme.ContentBackground, Hex1bColor.FromRgb(30, 30, 40))
    .Set(DrawerTheme.ExpandedIndicator, "▼")
    .Set(DrawerTheme.CollapsedIndicatorLeft, "▶")
    .Set(DrawerTheme.CollapsedIndicatorRight, "◀")
    .Set(DrawerTheme.CollapsedIndicatorTop, "▼")
    .Set(DrawerTheme.CollapsedIndicatorBottom, "▲")
    .Set(DrawerTheme.BorderColor, Hex1bColor.Gray);

var app = new Hex1bApp(options => {
    options.Theme = theme;
}, ctx => /* ... */);
```

### Available Theme Elements

| Element | Type | Default | Description |
|---------|------|---------|-------------|
| `HeaderBackground` | `Hex1bColor` | Default | Background color of the header row |
| `HeaderForeground` | `Hex1bColor` | Default | Text color of the header |
| `ContentBackground` | `Hex1bColor` | Default | Background color of expanded content |
| `BorderColor` | `Hex1bColor` | Gray | Color of the drawer border |
| `ExpandedIndicator` | `string` | `"▼"` | Character shown when expanded |
| `CollapsedIndicatorLeft` | `string` | `"▶"` | Indicator for left-position collapsed |
| `CollapsedIndicatorRight` | `string` | `"◀"` | Indicator for right-position collapsed |
| `CollapsedIndicatorTop` | `string` | `"▼"` | Indicator for top-position collapsed |
| `CollapsedIndicatorBottom` | `string` | `"▲"` | Indicator for bottom-position collapsed |
| `FocusedIndicatorColor` | `Hex1bColor` | Cyan | Indicator color when toggle is focused |

## Common Patterns

### IDE-Style Sidebar

```csharp
ctx.HSplitter(
    ctx.Drawer(
        isExpanded: state.ExplorerOpen,
        onToggle: open => state.ExplorerOpen = open,
        header: ctx.Text("Explorer"),
        content: ctx.VStack(v => [
            v.Text("📁 src"),
            v.Text("  📄 Program.cs"),
            v.Text("  📄 App.cs"),
            v.Text("📁 tests")
        ]),
        position: DrawerPosition.Left,
        expandedSize: 25
    ),
    ctx.VStack(v => [
        v.Text("Editor content here...")
    ])
)
```

### Settings Panel with Sections

```csharp
ctx.VStack(v => [
    v.Drawer(
        isExpanded: state.GeneralOpen,
        onToggle: open => state.GeneralOpen = open,
        header: ctx.Text("⚙️ General"),
        content: ctx.VStack(c => [
            c.Text("Theme: Dark"),
            c.Text("Language: English")
        ])
    ),
    v.Drawer(
        isExpanded: state.EditorOpen,
        onToggle: open => state.EditorOpen = open,
        header: ctx.Text("📝 Editor"),
        content: ctx.VStack(c => [
            c.Text("Font: Cascadia Code"),
            c.Text("Tab Size: 4")
        ])
    ),
    v.Drawer(
        isExpanded: state.KeybindingsOpen,
        onToggle: open => state.KeybindingsOpen = open,
        header: ctx.Text("⌨️ Keybindings"),
        content: ctx.VStack(c => [
            c.Text("Ctrl+S: Save"),
            c.Text("Ctrl+Z: Undo")
        ])
    )
])
```

### Mobile-Style Navigation

```csharp
ctx.ZStack(z => [
    // Main app content
    z.VStack(v => [
        v.HStack(h => [
            h.Button("☰").OnClick(_ => state.MenuOpen = true),
            h.Text("My App").Fill()
        ]),
        v.Text("App content here...").Fill()
    ]),
    // Overlay navigation drawer
    state.MenuOpen 
        ? z.Drawer(
            isExpanded: true,
            onToggle: open => state.MenuOpen = open,
            header: ctx.Text("Menu"),
            content: ctx.VStack(c => [
                c.Button("Home").OnClick(_ => Navigate("home")),
                c.Button("Profile").OnClick(_ => Navigate("profile")),
                c.Button("Settings").OnClick(_ => Navigate("settings")),
                c.Button("Logout").OnClick(_ => Logout())
            ]),
            mode: DrawerMode.Overlay,
            position: DrawerPosition.Left
        )
        : z.Empty()
])
```

## Related Widgets

- [SplitterWidget](/guide/widgets/splitter) - For resizable two-pane layouts
- [BorderWidget](/guide/widgets/border) - For adding borders around content
- [ScrollWidget](/guide/widgets/scroll) - For scrollable content within drawers
- [VStackWidget](/guide/widgets/stacks) - For arranging drawer content vertically
