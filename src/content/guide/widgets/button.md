<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode   → src/Hex1b.Website/Examples/ButtonBasicExample.cs
  - counterCode → src/Hex1b.Website/Examples/ButtonCounterExample.cs
  - asyncCode   → src/Hex1b.Website/Examples/ButtonAsyncExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/button-basic.cs?raw'
import focusSnippet from './snippets/button-focus.cs?raw'

const basicCode = `using Hex1b;

var state = new ButtonState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Button Examples"),
        v.Text(""),
        v.Text($"Button clicked {state.ClickCount} times"),
        v.Text(""),
        v.Button("Click me!").OnClick(_ => state.ClickCount++),
        v.Text(""),
        v.Text("Press Tab to focus, Enter or Space to activate")
    ]))
    .Build();

await terminal.RunAsync();

class ButtonState
{
    public int ClickCount { get; set; }
}`

const counterCode = `using Hex1b;

var state = new CounterState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text($"Count: {state.Count}"),
            v.Text(""),
            v.HStack(h => [
                h.Button("- Decrement").OnClick(_ => state.Count--),
                h.Text(" "),
                h.Button("+ Increment").OnClick(_ => state.Count++)
            ]),
            v.Text(""),
            v.Button("Reset").OnClick(_ => state.Count = 0)
        ])
    ], title: "Counter"))
    .Build();

await terminal.RunAsync();

class CounterState
{
    public int Count { get; set; }
}`

const asyncCode = `using Hex1b;

var state = new LoaderState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        state.App = app;
        return ctx => ctx.Border(b => [
            b.VStack(v => [
                v.Text("Async Background Work Demo"),
                v.Text(""),
                v.Text($"Status: {state.Status}"),
                v.Progress(state.Progress),
                v.Text(""),
                state.Result != null 
                    ? v.Text($"Result: {state.Result}") 
                    : v.Text(""),
                v.Text(""),
                v.Button(state.IsLoading ? "Loading..." : "Load Data")
                    .OnClick(_ => state.StartLoading())
            ])
        ], title: "Background Work");
    })
    .Build();

await terminal.RunAsync();

class LoaderState
{
    public Hex1bApp? App { get; set; }
    public string Status { get; private set; } = "Ready";
    public int Progress { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Result { get; private set; }

    public void StartLoading()
    {
        if (IsLoading || App is null) return;
        
        IsLoading = true;
        Status = "Starting...";
        Progress = 0;
        Result = null;
        
        // Trigger background work - not awaited!
        _ = DoBackgroundWorkAsync();
    }

    private async Task DoBackgroundWorkAsync()
    {
        var steps = new[] { "Connecting...", "Fetching data...", "Processing...", "Finalizing..." };

        for (int i = 0; i < steps.Length; i++)
        {
            Status = steps[i];
            Progress = (i + 1) * 25;
            App?.Invalidate(); // Tell app to re-render
            
            await Task.Delay(600); // Simulate work
        }

        Status = "Complete!";
        Progress = 100;
        Result = "Successfully loaded 42 items";
        IsLoading = false;
        App?.Invalidate();
    }
}`
</script>

# ButtonWidget

An interactive button that responds to Enter, Space, or mouse clicks.

Buttons are focusable widgets that provide visual feedback when focused and can trigger actions when activated. They're the primary way to let users perform actions in your terminal UI.

## Basic Usage

Create buttons using the fluent API and handle clicks with `OnClick`:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="button-basic" exampleTitle="Button Widget - Basic Usage" />

::: tip Navigation
Use **Tab** to move focus between buttons, and **Enter** or **Space** to activate the focused button. Mouse clicks also work in supported terminals.
:::

## Click Handlers

The `OnClick` method accepts both synchronous and asynchronous handlers.

### Synchronous Handler

For simple state updates, use a synchronous handler:

```csharp
v.Button("Save").OnClick(_ => document.Save())
```

The handler receives `ButtonClickedEventArgs` with access to:
- `Widget` - The source ButtonWidget
- `Node` - The underlying ButtonNode
- `Context` - Access to the application context for navigation, stop requests, etc.

### Asynchronous Handler

Hex1b also supports async handlers for when you need to call async APIs:

```csharp
v.Button("Fetch").OnClick(async _ => {
    var data = await httpClient.GetStringAsync(url);
    state.Data = data;
})
```

::: warning Render Loop Blocking
Click handlers execute within the render loop. While your async handler awaits, **the UI cannot update**—no re-renders, no input processing. For quick async calls (like a single HTTP request), this is usually acceptable. For longer operations, use the background work pattern shown below.
:::

### Background Work Pattern

For operations that take noticeable time, trigger the work from your state object and use `app.Invalidate()` to request re-renders as progress updates:

<CodeBlock lang="csharp" :code="asyncCode" command="dotnet run" example="button-async" exampleTitle="Button Widget - Async Work Demo" />

Key points in this pattern:

1. **Fire and forget**: The click handler calls `StartLoading()` synchronously—it doesn't await the background work
2. **State owns the work**: The `LoaderState` class manages the async operation internally
3. **Explicit invalidation**: Call `app.Invalidate()` whenever state changes to trigger a re-render
4. **UI stays responsive**: The render loop continues while work happens in the background

::: tip When to Use Each Approach
- **Synchronous**: Simple state updates (incrementing counters, toggling flags)
- **Async handler**: Quick async calls where brief blocking is acceptable (single API call)
- **Background work**: Long-running operations, multi-step processes, or anything needing progress updates
:::

## Counter Example

Here's a more complete example showing multiple buttons controlling shared state:

<CodeBlock lang="csharp" :code="counterCode" command="dotnet run" example="button-counter" exampleTitle="Button Widget - Counter Demo" />

## Focus Behavior

Buttons visually indicate their focus state:

| State | Appearance |
|-------|------------|
| Unfocused | `[ Label ]` |
| Focused | Highlighted with theme colors |

<StaticTerminalPreview svgPath="/svg/button-focus.svg" :code="focusSnippet" />

The focused button has a distinct background color (configurable via theming) to make it clear which button will be activated when pressing Enter or Space.

### Focus Navigation

- **Tab** - Move focus to the next focusable widget
- **Shift+Tab** - Move focus to the previous focusable widget
- **Enter** or **Space** - Activate the focused button
- **Mouse click** - Focus and activate the button

## Theming

Customize button appearance using theme elements:

```csharp
using Hex1b;
using Hex1b.Theming;

var theme = new Hex1bTheme("Custom")
    .Set(ButtonTheme.ForegroundColor, Hex1bColor.White)
    .Set(ButtonTheme.BackgroundColor, Hex1bColor.Blue)
    .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
    .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Yellow)
    .Set(ButtonTheme.LeftBracket, "< ")
    .Set(ButtonTheme.RightBracket, " >");

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        options.Theme = theme;
        return ctx => ctx.Button("Themed Button");
    })
    .Build();

await terminal.RunAsync();
```

### Available Theme Elements

| Element | Type | Default | Description |
|---------|------|---------|-------------|
| `ForegroundColor` | `Hex1bColor` | Default | Text color when unfocused |
| `BackgroundColor` | `Hex1bColor` | Default | Background when unfocused |
| `FocusedForegroundColor` | `Hex1bColor` | Black | Text color when focused |
| `FocusedBackgroundColor` | `Hex1bColor` | White | Background when focused |
| `LeftBracket` | `string` | `"[ "` | Left bracket decoration |
| `RightBracket` | `string` | `" ]"` | Right bracket decoration |
| `MinimumWidth` | `int` | `10` | Minimum button width |

## Input Bindings

Buttons automatically register these input bindings when a click handler is set:

| Input | Action |
|-------|--------|
| Enter | Activate button |
| Space | Activate button |
| Left mouse click | Activate button |

You can add additional bindings using the standard input binding API:

```csharp
v.Button("Save")
    .OnClick(_ => Save())
    .WithInputBindings(bindings => bindings
        .Ctrl().Key(Hex1bKey.S).Action(() => Save()))
```

## Related Widgets

- [TextWidget](/guide/widgets/text) - For non-interactive text display
- [TextBoxWidget](/guide/widgets/textbox) - For text input
- [HyperlinkWidget](/guide/widgets/hyperlink) - For clickable links
- [ListWidget](/guide/widgets/list) - For selectable lists of items
