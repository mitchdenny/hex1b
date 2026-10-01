<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode       → src/Hex1b.Website/Examples/SliderBasicExample.cs
  - audioMixerCode  → src/Hex1b.Website/Examples/SliderAudioMixerExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/slider-basic.cs?raw'
import customRangeSnippet from './snippets/slider-custom-range.cs?raw'
import stepSnippet from './snippets/slider-step.cs?raw'
import audioMixerSnippet from './snippets/slider-audio-mixer.cs?raw'
import focusSnippet from './snippets/slider-focus.cs?raw'

const basicCode = `using Hex1b;

var currentValue = 50.0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Volume Control"),
        v.Text(""),
        v.Text($"Current: {currentValue:F0}%"),
        v.Slider(50)
            .OnValueChanged(e => currentValue = e.Value),
        v.Text(""),
        v.Text("Use ← → or Home/End to adjust")
    ]))
    .Build();

await terminal.RunAsync();`

const audioMixerCode = `using Hex1b;

var master = 80.0;
var music = 60.0;
var effects = 90.0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text("Audio Settings"),
            v.Text(""),
            v.HStack(h => [
                h.Text($"Master:  {master,3:F0}% "),
                h.Slider(80).OnValueChanged(e => master = e.Value).Fill()
            ]),
            v.HStack(h => [
                h.Text($"Music:   {music,3:F0}% "),
                h.Slider(60).OnValueChanged(e => music = e.Value).Fill()
            ]),
            v.HStack(h => [
                h.Text($"Effects: {effects,3:F0}% "),
                h.Slider(90).OnValueChanged(e => effects = e.Value).Fill()
            ]),
            v.Text(""),
            v.Text("Tab to switch, arrows to adjust")
        ])
    ], title: "Settings"))
    .Build();

await terminal.RunAsync();`

</script>

# Slider

Select numeric values by moving a handle along a horizontal track.

Slider is a focusable widget that allows users to select a value from a continuous or stepped range. It supports keyboard navigation (arrow keys, Home/End, PageUp/PageDown) and mouse click-to-position.

## Basic Usage

Create a slider using the fluent API. By default, sliders use a 0-100 range:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="slider-basic" exampleTitle="Slider Widget - Basic Usage" />

::: tip State Management
Slider value state is managed internally by the node and preserved across re-renders. Use the `OnValueChanged` event to synchronize with your own application state.
:::

## Custom Range

Specify custom minimum and maximum values for any numeric range:

<StaticTerminalPreview svgPath="/svg/slider-custom-range.svg" :code="customRangeSnippet" />

```csharp
// Temperature range
v.Slider(initialValue: 22, min: -10, max: 40)

// Percentage with decimals
v.Slider(initialValue: 0.5, min: 0, max: 1)

// Large values
v.Slider(initialValue: 5000, min: 0, max: 10000)
```

## Step Values

Use the `step` parameter to create discrete value increments. Values snap to the nearest step:

<StaticTerminalPreview svgPath="/svg/slider-step.svg" :code="stepSnippet" />

```csharp
// Steps of 10: 0, 10, 20, 30, ...
v.Slider(initialValue: 50, min: 0, max: 100, step: 10)

// Steps of 5: 0, 5, 10, 15, ...
v.Slider(initialValue: 25, min: 0, max: 100, step: 5)
```

When no step is specified, the slider uses continuous values with a small default step of 1% of the range.

## Keyboard Navigation

Slider supports comprehensive keyboard navigation:

| Key | Action |
|-----|--------|
| `←` Left Arrow | Decrease value by step |
| `→` Right Arrow | Increase value by step |
| `↑` Up Arrow | Increase value by step |
| `↓` Down Arrow | Decrease value by step |
| `Home` | Jump to minimum value |
| `End` | Jump to maximum value |
| `PageUp` | Increase by 10% of range |
| `PageDown` | Decrease by 10% of range |

::: tip Large Steps
The PageUp/PageDown step size is configurable via the `LargeStepPercent` property (default: 10% of range).
:::

## Mouse Support

In terminals that support mouse input, users can click anywhere on the track to jump directly to that position. The slider calculates the corresponding value based on the click position.

## Value Changed Event

Use `OnValueChanged` to respond when the user adjusts the slider:

```csharp
v.Slider(50)
    .OnValueChanged(e =>
    {
        Console.WriteLine($"Value: {e.Value}");
        Console.WriteLine($"Previous: {e.PreviousValue}");
        Console.WriteLine($"Percentage: {e.Percentage:P0}");
    })
```

The `SliderValueChangedEventArgs` provides:

| Property | Type | Description |
|----------|------|-------------|
| `Value` | `double` | The new slider value |
| `PreviousValue` | `double` | The value before the change |
| `Minimum` | `double` | The minimum of the range |
| `Maximum` | `double` | The maximum of the range |
| `Percentage` | `double` | Current value as 0.0-1.0 percentage |
| `Widget` | `SliderWidget` | The source widget |
| `Node` | `SliderNode` | The underlying node |
| `Context` | `InputBindingActionContext` | The input context |

## Audio Mixer Example

Multiple sliders with labels in a form layout:

<CodeBlock lang="csharp" :code="audioMixerCode" command="dotnet run" example="slider-audio-mixer" exampleTitle="Slider Widget - Audio Mixer" />

## Layout Behavior

By default, sliders fill all available horizontal space. Use layout extensions to constrain width:

```csharp
// Full width (default)
v.Slider(50)

// Fixed width
v.Slider(50).FixedWidth(30)

// Proportional width in HStack
h.Slider(50).Fill()
```

## Theming

Customize the slider appearance using theme elements:

```csharp
var theme = Hex1bTheme.Create()
    .Set(SliderTheme.TrackCharacter, '═')
    .Set(SliderTheme.HandleCharacter, '●')
    .Set(SliderTheme.HandleForegroundColor, Hex1bColor.Cyan)
    .Set(SliderTheme.FocusedHandleForegroundColor, Hex1bColor.Yellow)
    .Set(SliderTheme.FocusedHandleBackgroundColor, Hex1bColor.Blue);

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
| `TrackCharacter` | `char` | `─` | Character for the track line |
| `HandleCharacter` | `char` | `█` | Character for the handle/knob |
| `TrackForegroundColor` | `Hex1bColor` | Default | Track line color |
| `TrackBackgroundColor` | `Hex1bColor` | Default | Track background |
| `HandleForegroundColor` | `Hex1bColor` | Default | Handle color when unfocused |
| `HandleBackgroundColor` | `Hex1bColor` | Default | Handle background when unfocused |
| `FocusedHandleForegroundColor` | `Hex1bColor` | Black | Handle color when focused |
| `FocusedHandleBackgroundColor` | `Hex1bColor` | White | Handle background when focused |

## Focus Behavior

The slider visually indicates its focus state through the handle color:

| State | Handle Appearance |
|-------|-------------------|
| Unfocused | Default track color |
| Focused | Highlighted (white background by default) |

Use **Tab** and **Shift+Tab** to navigate focus between sliders and other focusable widgets.

## Related Widgets

- [ProgressWidget](/guide/widgets/progress) - For display-only progress indication
- [ToggleSwitchWidget](/guide/widgets/toggle-switch) - For discrete option selection
- [TextBoxWidget](/guide/widgets/textbox) - For numeric text input
- [StackWidgets](/guide/widgets/stacks) - For laying out sliders with labels
