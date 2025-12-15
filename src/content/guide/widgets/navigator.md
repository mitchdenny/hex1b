# Navigator Widget

Manage multiple screens/views with navigation state.

## Basic Usage

```csharp
public record AppState(
    NavigatorState Nav
);

new NavigatorWidget(
    state: ctx.State.Nav,
    onNavigate: nav => ctx.SetState(ctx.State with { Nav = nav }),
    routes: new Dictionary<string, Func<Hex1bWidget>>
    {
        ["home"] = () => HomeScreen(ctx),
        ["settings"] = () => SettingsScreen(ctx),
        ["about"] = () => AboutScreen(ctx)
    }
)
```

## Navigation

Push a new screen:

```csharp
new ButtonWidget("Go to Settings", () => 
    ctx.SetState(ctx.State with { 
        Nav = ctx.State.Nav.Push("settings") 
    })
)
```

Go back:

```csharp
new ButtonWidget("Back", () => 
    ctx.SetState(ctx.State with { 
        Nav = ctx.State.Nav.Pop() 
    })
)
```

## Navigator State

```csharp
public record NavigatorState(
    Stack<string> RouteStack
)
{
    public string CurrentRoute => RouteStack.Peek();
    
    public NavigatorState Push(string route) => 
        this with { RouteStack = new Stack<string>([..RouteStack, route]) };
    
    public NavigatorState Pop() => 
        RouteStack.Count > 1 
            ? this with { RouteStack = new Stack<string>(RouteStack.Skip(1)) }
            : this;
}
```

## Live Demo

<TerminalDemo exhibit="navigator" title="Navigator Demo" />
