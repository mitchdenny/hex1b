<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode           → src/Hex1b.Website/Examples/RescueBasicExample.cs
  - customFallbackCode  → src/Hex1b.Website/Examples/RescueCustomFallbackExample.cs
  - eventHandlersCode   → src/Hex1b.Website/Examples/RescueEventHandlersExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/rescue-basic.cs?raw'
import vstackSnippet from './snippets/rescue-vstack.cs?raw'
import onRescueSnippet from './snippets/rescue-on-rescue.cs?raw'
import onResetSnippet from './snippets/rescue-on-reset.cs?raw'
import customFallbackSnippet from './snippets/rescue-custom-fallback.cs?raw'
import friendlySnippet from './snippets/rescue-friendly.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Rescue(v => [
        v.Text("Application content here"),
        v.Text(""),
        v.Text("Click the button to trigger an error."),
        v.Text("The Rescue widget will catch it and show"),
        v.Text("a fallback UI with error details."),
        v.Text(""),
        v.Button("Click me").OnClick(_ => {
            // If this throws, Rescue catches it
            throw new InvalidOperationException("Oops! Something went wrong.");
        })
    ]))
    .Build();

await terminal.RunAsync();`

const customFallbackCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Rescue(v => [
        v.Text("Custom Fallback Demo"),
        v.Text(""),
        v.Text("This uses WithFallback() to provide"),
        v.Text("a custom error UI instead of the default."),
        v.Text(""),
        v.Button("Trigger Error").OnClick(_ => {
            throw new InvalidOperationException("Something went wrong!");
        })
    ])
    .WithFallback(rescue => rescue.Border(b => [
        b.VStack(inner => [
            inner.Text("🔥 Custom Error Handler 🔥"),
            inner.Text(""),
            inner.Text(\`Error Type: \${rescue.Exception.GetType().Name}\`),
            inner.Text(\`Phase: \${rescue.ErrorPhase}\`),
            inner.Text(""),
            inner.Text("Message:"),
            inner.Text(\`  \${rescue.Exception.Message}\`),
            inner.Text(""),
            inner.Button("🔄 Try Again").OnClick(_ => rescue.Reset()),
        ])
    ], title: "Oops!")))
    .Build();

await terminal.RunAsync();`

const eventHandlersCode = `using Hex1b;

var state = new EventState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Event Handlers Demo"),
        v.Text(""),
        v.Text(\$"Errors caught: {state.ErrorCount}"),
        v.Text(\$"Resets triggered: {state.ResetCount}"),
        v.Text(""),
        v.Rescue(inner => [
            inner.Text("Click the button to trigger an error."),
            inner.Text("Watch the counters above update!"),
            inner.Text(""),
            inner.Button("Trigger Error").OnClick(_ => {
                throw new Exception("Test error");
            })
        ])
        .OnRescue(e => {
            state.ErrorCount++;
            // In a real app: logger.LogError(e.Exception, "Error");
        })
        .OnReset(_ => {
            state.ResetCount++;
            // In a real app: ResetApplicationState();
        })
    ]))
    .Build();

await terminal.RunAsync();

class EventState
{
    public int ErrorCount { get; set; }
    public int ResetCount { get; set; }
}`
</script>

# RescueWidget

Catch exceptions and display a fallback UI, similar to React's ErrorBoundary.

RescueWidget wraps a child widget and catches any exceptions that occur during the widget lifecycle (Reconcile, Measure, Arrange, or Render phases). When an error occurs, it displays a fallback UI instead of crashing the application, allowing users to retry or continue.

## Basic Usage

Wrap potentially error-prone content with Rescue:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="rescue-basic" exampleTitle="Rescue Widget - Basic Usage" />

::: tip Error Boundary Pattern
RescueWidget follows the "error boundary" pattern from React. Each Rescue widget acts as a boundary—errors propagate up until they hit a Rescue widget, which then displays the fallback instead of its normal content.
:::

## Wrapping Content

### Single Widget

Wrap any widget that might throw:

```csharp
ctx.Rescue(
    ctx.SomeWidget() // This widget may throw
)
```

### VStack Shorthand

Rescue provides a convenient shorthand for wrapping multiple widgets in a VStack:

```csharp
ctx.Rescue(v => [
    v.Text("Some content"),
    v.Button("Click me").OnClick(_ => DoSomething())
])
```

This is equivalent to:

```csharp
ctx.Rescue(
    ctx.VStack(v => [
        v.Text("Some content"),
        v.Button("Click me").OnClick(_ => DoSomething())
    ])
)
```

## Event Handlers

RescueWidget provides event handlers for logging, telemetry, and state management:

<CodeBlock lang="csharp" :code="eventHandlersCode" command="dotnet run" example="rescue-event-handlers" exampleTitle="Rescue Widget - Event Handlers" />

### OnRescue

Called when an exception is caught. Use this for logging or telemetry:

```csharp
ctx.Rescue(
    ctx.SomeWidget()
)
.OnRescue(e => {
    logger.LogError(e.Exception, "Error in {Phase}", e.Phase);
})
```

The `RescueEventArgs` provides:

| Property | Type | Description |
|----------|------|-------------|
| `Exception` | `Exception` | The exception that was caught |
| `Phase` | `RescueErrorPhase` | When the error occurred (Reconcile, Measure, Arrange, Render) |
| `Widget` | `RescueWidget` | The source widget |
| `Node` | `RescueNode` | The rescue node |

### OnReset

Called after the user triggers a retry (clicks the Retry button). Use this to reset application state:

```csharp
ctx.Rescue(
    ctx.SomeWidget()
)
.OnRescue(e => logger.LogError(e.Exception, "Error caught"))
.OnReset(e => {
    logger.LogInformation("User retried after {Phase} error", e.Phase);
    ResetApplicationState();
})
```

Both handlers support async versions:

```csharp
.OnRescue(async e => {
    await telemetry.TrackExceptionAsync(e.Exception);
})
.OnReset(async e => {
    await ResetStateAsync();
})
```

## Custom Fallback UI

### WithFallback

Override the default fallback with your own UI using `RescueContext`:

<CodeBlock lang="csharp" :code="customFallbackCode" command="dotnet run" example="rescue-custom-fallback" exampleTitle="Rescue Widget - Custom Fallback" />

### RescueContext

The `RescueContext` passed to `WithFallback` extends `WidgetContext<VStackWidget>` and provides:

| Property/Method | Type | Description |
|-----------------|------|-------------|
| `Exception` | `Exception` | The exception that was caught |
| `ErrorPhase` | `RescueErrorPhase` | When the error occurred |
| `Reset()` | `void` | Clears error state and retries the child |

Since `RescueContext` is a widget context, you can use all the standard widget methods:

```csharp
.WithFallback(rescue => rescue.Border(b => [
    b.VStack(v => [
        v.Text($"Error during {rescue.ErrorPhase}:"),
        v.Scroll(s => s.Text(rescue.Exception.ToString()).Wrap()).Height(10),
        v.Button("Retry").OnClick(_ => rescue.Reset())
    ])
], title: "Error"))
```

## Error Phases

Exceptions can occur at different points in the widget lifecycle:

| Phase | When |
|-------|------|
| `Build` | During widget construction (inside the builder function) |
| `Reconcile` | During widget tree reconciliation (building nodes from widgets) |
| `Measure` | When calculating widget sizes |
| `Arrange` | When positioning widgets within their bounds |
| `Render` | When drawing to the terminal |

The phase information helps diagnose where problems occur:

```csharp
.OnRescue(e => {
    if (e.Phase == RescueErrorPhase.Render)
    {
        logger.LogWarning("Render error - may be terminal compatibility issue");
    }
})
```

## Production Mode

### RescueFriendly

In production, you typically don't want to show stack traces to users. Use `RescueFriendly` or set `ShowDetails = false`:

```csharp
// Shorthand - hides exception details
ctx.RescueFriendly(
    ctx.SomeWidget()
)

// Equivalent to:
ctx.Rescue(ctx.SomeWidget()) with { ShowDetails = false }
```

The default fallback adapts based on `ShowDetails`:

- **ShowDetails = true** (default in DEBUG): Shows exception type, message, and stack trace
- **ShowDetails = false** (default in RELEASE): Shows a user-friendly "Something went wrong" message

## Theming

RescueWidget uses `RescueTheme` for styling the default fallback. The fallback is wrapped in a `ThemePanelWidget` that maps rescue theme elements to standard widget themes.

| Element | Default | Description |
|---------|---------|-------------|
| `BackgroundColor` | Dark red | Fallback background |
| `ForegroundColor` | Light red | Text color |
| `BorderColor` | Bright red | Border lines |
| `TitleColor` | White | Border title |
| `ButtonBackgroundColor` | Dark red | Retry button background |
| `ButtonForegroundColor` | Light red | Retry button text |
| `ButtonFocusedBackgroundColor` | Bright red | Focused button background |
| `ButtonFocusedForegroundColor` | White | Focused button text |

The border uses double-line box characters (╔═╗║╚╝) to visually distinguish error states.

Customize via theme:

```csharp
var theme = Hex1bTheme.Create()
    .Set(RescueTheme.BackgroundColor, Hex1bColor.FromRgb(50, 0, 0))
    .Set(RescueTheme.BorderColor, Hex1bColor.Yellow);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        options.Theme = theme;
        return ctx => /* ... */;
    })
    .Build();

await terminal.RunAsync();
```

## Common Patterns

### Top-Level Error Boundary

Wrap your entire application to catch any unhandled errors:

```csharp
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Rescue(v => [
        v.Navigator(/* your app routes */)
    ])
    .OnRescue(e => logger.LogCritical(e.Exception, "Unhandled error")))
    .Build();

await terminal.RunAsync();
```

### Component-Level Boundaries

Isolate errors to specific components so the rest of the UI continues working:

```csharp
ctx.HStack(h => [
    h.Border(b => [
        b.Rescue(b.SomeUnstableWidget())
    ], title: "Widget A"),

    h.Border(b => [
        b.Rescue(b.AnotherWidget())
    ], title: "Widget B")
])
```

If Widget A throws, Widget B continues to render normally.

### With Async State Reset

Clean up resources or reset state when retrying:

```csharp
ctx.Rescue(
    ctx.DataViewer(data)
)
.OnRescue(async e => {
    await connection.CloseAsync();
    await logger.LogErrorAsync(e.Exception);
})
.OnReset(async e => {
    data = await RefreshDataAsync();
    await connection.ReconnectAsync();
})
```

### Conditional Fallback

Customize fallback based on the error type:

```csharp
ctx.Rescue(
    ctx.NetworkWidget()
)
.WithFallback(rescue => {
    if (rescue.Exception is HttpRequestException)
    {
        return rescue.VStack(v => [
            v.Text("Network error - check your connection"),
            v.Button("Retry").OnClick(_ => rescue.Reset())
        ]);
    }

    // Default for other errors
    return rescue.VStack(v => [
        v.Text($"Error: {rescue.Exception.Message}"),
        v.Button("Retry").OnClick(_ => rescue.Reset())
    ]);
})
```

## Related Widgets

- [ThemePanelWidget](/guide/widgets/themepanel) - Used internally to apply rescue theming
- [BorderWidget](/guide/widgets/border) - Used in the default fallback UI
- [ScrollWidget](/guide/widgets/scroll) - Used to display scrollable stack traces
