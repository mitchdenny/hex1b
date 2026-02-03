# Widgets

Hex1b provides a rich library of widgets for building terminal user interfaces. Widgets are immutable configuration objects that describe what to render—the framework handles the rest.

## Layout Widgets

Widgets for organizing and positioning content.

- **[Stacks (HStack/VStack)](/guide/widgets/stacks)** — Arrange children horizontally or vertically
- **[Border](/guide/widgets/containers)** — Bordered containers with optional titles
- **[Align](/guide/widgets/align)** — Position content within available space
- **[Scroll](/guide/widgets/scroll)** — Scrollable content areas for overflow
- **[Splitter](/guide/widgets/splitter)** — Resizable split views
- **[Responsive](/guide/widgets/responsive)** — Breakpoint-based adaptive layouts

## Interactive Widgets

Widgets that respond to user input.

- **[Button](/guide/widgets/button)** — Clickable buttons with keyboard support
- **[Checkbox](/guide/widgets/checkbox)** — Toggleable checked/unchecked/indeterminate state
- **[SplitButton](/guide/widgets/split-button)** — Button with dropdown for secondary actions
- **[TextBox](/guide/widgets/textbox)** — Single and multi-line text input
- **[List](/guide/widgets/list)** — Scrollable, selectable item lists
- **[Tree](/guide/widgets/tree)** — Hierarchical data with expand/collapse and multi-select
- **[Table](/guide/widgets/table)** — Data tables with sorting, selection, and virtualization
- **[Picker](/guide/widgets/picker)** — Dropdown selection menus
- **[Slider](/guide/widgets/slider)** — Numeric value selection with keyboard and mouse
- **[ToggleSwitch](/guide/widgets/toggle-switch)** — On/off toggle controls
- **[Navigator](/guide/widgets/navigator)** — Stack-based page navigation

## Display Widgets

Widgets for presenting information.

- **[Icon](/guide/widgets/icon)** — Single character or emoji display with optional click
- **[InfoBar](/guide/widgets/infobar)** — Status bars for contextual information
- **[Notifications](/guide/widgets/notifications)** — Floating notifications with actions and drawer
- **[Text](/guide/widgets/text)** — Rich text with styling and formatting
- **[Progress](/guide/widgets/progress)** — Progress bars for known completion amounts
- **[Spinner](/guide/widgets/spinner)** — Animated activity indicators
- **[Hyperlink](/guide/widgets/hyperlink)** — Clickable terminal hyperlinks (OSC 8)
- **[QrCode](/guide/widgets/qrcode)** — Render QR codes in the terminal

## Utility Widgets

Widgets for special behaviors.

- **[Rescue](/guide/widgets/rescue)** — Error boundaries for graceful failure handling
- **[ThemePanel](/guide/widgets/themepanel)** — Scope theme overrides to subtrees
- **[Terminal](/guide/widgets/terminal)** — Embed child terminal sessions
- **[Surface](/guide/widgets/surface)** — Low-level rendering with layered compositing
