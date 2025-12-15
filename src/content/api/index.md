# API Reference

This section contains automatically generated API documentation from the Hex1b library.

## Core Types

### Hex1bApp&lt;TState&gt;

The main application class that runs the TUI event loop.

```csharp
public class Hex1bApp<TState>
{
    public Hex1bApp(
        TState initialState,
        Func<WidgetContext<TState>, CancellationToken, Hex1bWidget> buildWidget,
        Hex1bAppOptions? options = null
    );
    
    public Task RunAsync(CancellationToken cancellationToken = default);
}
```

### WidgetContext&lt;TState&gt;

Provides access to app state and methods within the widget builder.

```csharp
public class WidgetContext<TState>
{
    public TState State { get; }
    public void SetState(TState newState);
}
```

### Hex1bAppOptions

Configuration for the Hex1b application.

```csharp
public class Hex1bAppOptions
{
    public IHex1bTerminal? Terminal { get; set; }
    public Func<Hex1bTheme>? ThemeProvider { get; set; }
    public bool EnableMouse { get; set; }
    public bool DebugReconciliation { get; set; }
    public bool DebugFocus { get; set; }
}
```

## Widgets

See the individual widget documentation:

- [Text Widgets](/guide/widgets/text)
- [Button](/guide/widgets/button)
- [TextBox](/guide/widgets/textbox)
- [List](/guide/widgets/list)
- [Layout Widgets](/guide/widgets/stacks)
- [Container Widgets](/guide/widgets/containers)
- [Navigator](/guide/widgets/navigator)

## Layout Types

### Constraints

```csharp
public record Constraints(
    int MinWidth,
    int MaxWidth,
    int MinHeight,
    int MaxHeight
);
```

### Size

```csharp
public record Size(int Width, int Height);
```

### Rect

```csharp
public record Rect(int X, int Y, int Width, int Height);
```

## Input Types

### Hex1bKeyEvent

```csharp
public record Hex1bKeyEvent(
    Hex1bKey Key,
    char Character,
    Hex1bModifiers Modifiers
);
```

### InputResult

```csharp
public enum InputResult
{
    Handled,
    Unhandled
}
```

---

::: tip Generating Full API Docs
Full API documentation can be generated from XML doc comments using tools like DocFX or xmldocmd.

```bash
dotnet build /p:GenerateDocumentationFile=true
xmldocmd src/Hex1b/bin/Release/net10.0/Hex1b.dll docs/api
```
:::
