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
- **[TextBox](/guide/widgets/textbox)** — Single and multi-line text input
- **[List](/guide/widgets/list)** — Scrollable, selectable item lists
- **[Picker](/guide/widgets/picker)** — Dropdown selection menus
- **[ToggleSwitch](/guide/widgets/toggle-switch)** — On/off toggle controls
- **[Navigator](/guide/widgets/navigator)** — Stack-based page navigation

## Display Widgets

Widgets for presenting information.

- **[Text](/guide/widgets/text)** — Rich text with styling and formatting
- **[Progress](/guide/widgets/progress)** — Progress bars and spinners
- **[Hyperlink](/guide/widgets/hyperlink)** — Clickable terminal hyperlinks (OSC 8)

## Utility Widgets

Widgets for special behaviors.

- **[Rescue](/guide/widgets/rescue)** — Error boundaries for graceful failure handling
- **[ThemePanel](/guide/widgets/themepanel)** — Scope theme overrides to subtrees
- **[Terminal](/guide/widgets/terminal)** — Embed child terminal sessions
