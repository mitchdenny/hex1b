<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode   → src/Hex1b.Website/Examples/ToggleSwitchBasicExample.cs
  - multiOptionCode → src/Hex1b.Website/Examples/ToggleSwitchMultiOptionExample.cs
  - eventCode   → src/Hex1b.Website/Examples/ToggleSwitchEventExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import focusSnippet from './snippets/toggle-switch-focus.cs?raw'
import multiSnippet from './snippets/toggle-switch-multi.cs?raw'
import binarySnippet from './snippets/toggle-switch-binary.cs?raw'
import modesSnippet from './snippets/toggle-switch-modes.cs?raw'
import settingsSnippet from './snippets/toggle-switch-settings.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Widgets;

string currentSelection = "Off";

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.VStack(v => [
        v.Text("ToggleSwitch Examples"),
        v.Text(""),
        v.Text($"Power: {currentSelection}"),
        v.Text(""),
        v.HStack(h => [
            h.Text("Status: "),
            h.ToggleSwitch(["Off", "On"])
                .OnSelectionChanged(args => currentSelection = args.SelectedOption)
        ]),
        v.Text(""),
        v.Text("Use Left/Right arrows or click to toggle")
    ])
));

await app.RunAsync();`

const multiOptionCode = `using Hex1b;
using Hex1b.Widgets;

string currentSpeed = "Normal";

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.Border(b => [
        b.VStack(v => [
            v.Text("Speed Settings"),
            v.Text(""),
            v.HStack(h => [
                h.Text("Animation Speed: ").FixedWidth(20),
                h.ToggleSwitch(["Slow", "Normal", "Fast"], selectedIndex: 1)
                    .OnSelectionChanged(args => currentSpeed = args.SelectedOption)
            ]),
            v.Text(""),
            v.Text($"Current speed: {currentSpeed}"),
            v.Text(""),
            v.Text("Use arrow keys to cycle through options")
        ])
    ], title: "Configuration")
));

await app.RunAsync();`

const eventCode = `using Hex1b;
using Hex1b.Widgets;

var eventLog = new List<string>();

var app = new Hex1bApp(ctx => Task.FromResult<Hex1bWidget>(
    ctx.Border(b => [
        b.VStack(v => [
            v.Text("Settings Panel"),
            v.Text(""),
            v.HStack(h => [
                h.Text("Theme:         ").FixedWidth(16),
                h.ToggleSwitch(["Light", "Dark"], selectedIndex: 1)
                    .OnSelectionChanged(args => 
                    {
                        eventLog.Add($"Theme changed to: {args.SelectedOption}");
                    })
            ]),
            v.Text(""),
            v.HStack(h => [
                h.Text("Notifications: ").FixedWidth(16),
                h.ToggleSwitch(["Off", "On"], selectedIndex: 1)
                    .OnSelectionChanged(args => 
                    {
                        eventLog.Add($"Notifications: {args.SelectedOption}");
                    })
            ]),
            v.Text(""),
            v.Text("Event Log:"),
            ..eventLog.TakeLast(3).Select(log => v.Text($"  • {log}"))
        ])
    ], title: "User Preferences")
));

await app.RunAsync();`
</script>

# ToggleSwitchWidget

A horizontal toggle switch that displays multiple options side-by-side and allows selection using arrow keys or mouse clicks.

ToggleSwitch is a focusable widget that presents a set of options in a compact horizontal layout. Users can navigate between options using left/right arrow keys or click directly on an option. It's commonly used for binary choices (On/Off) or selecting from a small set of mutually exclusive options (Low/Medium/High).

## Basic Usage

Create a toggle switch using the fluent API by passing an array of options. Use `OnSelectionChanged` to respond when the user changes the selection:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="toggle-switch-basic" exampleTitle="ToggleSwitch Widget - Basic Usage" />

::: tip State Management
ToggleSwitch selection state is managed internally by the node and preserved across re-renders. Use the `OnSelectionChanged` event to synchronize with your own application state when needed.
:::

## Multiple Options

ToggleSwitch isn't limited to binary choices—you can provide as many options as needed. Use the `selectedIndex` parameter to set the initial selection:

<CodeBlock lang="csharp" :code="multiOptionCode" command="dotnet run" example="toggle-switch-multi" exampleTitle="ToggleSwitch Widget - Multiple Options" />

The widget automatically wraps around when navigating with arrow keys:
- Pressing **Right** on the last option moves to the first option
- Pressing **Left** on the first option moves to the last option

## Selection Changed Event

Use `OnSelectionChanged` to respond when the user changes the selection:

<CodeBlock lang="csharp" :code="eventCode" command="dotnet run" example="toggle-switch-event" exampleTitle="ToggleSwitch Widget - Event Handling" />

The `ToggleSelectionChangedEventArgs` provides:
- `SelectedIndex` - The index of the newly selected option (0-based)
- `SelectedOption` - The text of the newly selected option
- `Widget` - The source ToggleSwitchWidget
- `Node` - The underlying ToggleSwitchNode
- `Context` - Access to the application context

## Keyboard and Mouse Navigation

ToggleSwitch supports multiple input methods:

| Input | Action |
|-------|--------|
| `←` Left Arrow | Select previous option (wraps to last) |
| `→` Right Arrow | Select next option (wraps to first) |
| **Mouse Click** | Select the clicked option |

::: tip Mouse Support
In terminals that support mouse input, users can click directly on any option to select it. The widget calculates which option was clicked based on the X position within the rendered text.
:::

## Focus Behavior

ToggleSwitch visually indicates its focus state with different bracket colors:

| State | Appearance |
|-------|------------|
| Unfocused | Subtle gray brackets `< Option >` |
| Focused | Bright brackets with highlighted selection |

<StaticTerminalPreview svgPath="/svg/toggle-switch-focus.svg" :code="focusSnippet" />

When focused:
- The selected option has a bright background (white by default)
- The brackets change to a brighter color
- Arrow key navigation is active

When unfocused:
- The selected option still shows a subtle background (gray by default)
- The brackets are dimmed
- The selection is visible but less prominent

### Focus Navigation

- **Tab** - Move focus to the next focusable widget
- **Shift+Tab** - Move focus to the previous focusable widget

## Theming

Customize ToggleSwitch appearance using theme elements:

```csharp
var theme = Hex1bTheme.Create()
    .Set(ToggleSwitchTheme.FocusedSelectedForegroundColor, Hex1bColor.Black)
    .Set(ToggleSwitchTheme.FocusedSelectedBackgroundColor, Hex1bColor.Cyan)
    .Set(ToggleSwitchTheme.UnfocusedSelectedForegroundColor, Hex1bColor.White)
    .Set(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor, Hex1bColor.DarkGray)
    .Set(ToggleSwitchTheme.LeftBracket, "[ ")
    .Set(ToggleSwitchTheme.RightBracket, " ]");

var app = new Hex1bApp(options => {
    options.Theme = theme;
}, ctx => /* ... */);
```

### Available Theme Elements

| Element | Type | Default | Description |
|---------|------|---------|-------------|
| `FocusedSelectedForegroundColor` | `Hex1bColor` | Black | Text color of selected option when focused |
| `FocusedSelectedBackgroundColor` | `Hex1bColor` | White | Background of selected option when focused |
| `UnfocusedSelectedForegroundColor` | `Hex1bColor` | Black | Text color of selected option when unfocused |
| `UnfocusedSelectedBackgroundColor` | `Hex1bColor` | Gray | Background of selected option when unfocused |
| `UnselectedForegroundColor` | `Hex1bColor` | Default | Text color of unselected options |
| `UnselectedBackgroundColor` | `Hex1bColor` | Default | Background of unselected options |
| `FocusedBracketForegroundColor` | `Hex1bColor` | White | Bracket color when focused |
| `FocusedBracketBackgroundColor` | `Hex1bColor` | Default | Bracket background when focused |
| `UnfocusedBracketForegroundColor` | `Hex1bColor` | Gray | Bracket color when unfocused |
| `UnfocusedBracketBackgroundColor` | `Hex1bColor` | Default | Bracket background when unfocused |
| `LeftBracket` | `string` | `"< "` | Left bracket decoration |
| `RightBracket` | `string` | `" >"` | Right bracket decoration |
| `Separator` | `string` | `" \| "` | Separator between options |

## Common Use Cases

### Binary Toggles

Perfect for on/off, yes/no, enabled/disabled choices:

<StaticTerminalPreview svgPath="/svg/toggle-switch-binary.svg" :code="binarySnippet" />

### Mode Selection

Choose between a small set of modes:

<StaticTerminalPreview svgPath="/svg/toggle-switch-modes.svg" :code="modesSnippet" />

### Settings Panels

Multiple toggles in a form layout:

<StaticTerminalPreview svgPath="/svg/toggle-switch-settings.svg" :code="settingsSnippet" />

## Related Widgets

- [ButtonWidget](/guide/widgets/button) - For triggering actions
- [ListWidget](/guide/widgets/list) - For selecting from larger sets of options
- [TextWidget](/guide/widgets/text) - For labels alongside toggle switches
- [StackWidgets](/guide/widgets/stacks) - For laying out multiple toggles
