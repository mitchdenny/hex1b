# StatePanelWidget

Anchor child widget identity to a state object, enabling state preservation across list reorders and animations.

StatePanelWidget solves a fundamental problem in declarative UI: when items in a list are reordered, added, or removed, how do you keep each item's state (focus, animation progress, scroll position) attached to the right data? StatePanelWidget uses **reference identity** of a state object to match nodes across reconciliation frames, instead of relying on position.

## Basic Usage

Wrap each item in a list with a StatePanel, passing the item's view model as the state key:

```csharp
using Hex1b;

var items = new List<ItemModel> { /* ... */ };

var app = new Hex1bApp(ctx =>
    ctx.VStack(v => items.Select(item =>
        v.StatePanel(item, sp =>
            sp.Text($"{item.Name}: {item.Value}")
        )
    ).ToArray())
);

await app.RunAsync();
```

::: tip Reference Identity
The state key is compared by **reference identity** (`ReferenceEquals`), not by value equality. Use a stable reference-type object (e.g., a view model instance). Value types or freshly-boxed objects will not match across frames.
:::

## How Identity Resolution Works

StatePanelWidget resolves node identity in two ways:

1. **Nested under another StatePanel**: Looks up the state key in the ancestor's registry dictionary (keyed by reference equality). This is the primary mechanism for lists.
2. **Standalone**: Falls back to positional matching with a reference identity check on the state key.

When items are reordered, the registry ensures each item's node (and all its state) follows the data:

```
Frame 1:  [A, B, C]  →  NodeA, NodeB, NodeC
Frame 2:  [C, A, B]  →  NodeC, NodeA, NodeB  (nodes follow their keys)
```

Nodes that disappear from the list are **swept** — their stored state (including animations) is disposed.

## Generic State Storage

StatePanelContext provides `GetState<T>(factory)` — a generic mechanism for subsystems to store per-identity state. The factory is called once on first access; subsequent calls return the same instance:

```csharp
ctx.StatePanel(viewModel, sp =>
{
    // Any reference type can be stored as state
    var myState = sp.GetState(() => new MyCustomState());
    myState.Counter++;

    return sp.Text($"Counter: {myState.Counter}");
});
```

State persists across reconciliation frames for the same identity key. When a key is swept (item removed from list), any stored state implementing `IDisposable` is disposed.

## Animations

The animation system layers on top of `GetState<T>()` via the `GetAnimations()` extension method:

```csharp
using Hex1b;
using Hex1b.Animation;

ctx.StatePanel(item, sp =>
{
    var slide = sp.GetAnimations().Get<NumericAnimator<double>>("slide", a =>
    {
        a.From = 0.0;
        a.To = 100.0;
        a.Duration = TimeSpan.FromMilliseconds(600);
        a.EasingFunction = Easing.EaseOutCubic;
    });

    var barWidth = (int)slide.Value;
    var bar = new string('█', barWidth / 5) + new string('░', 20 - barWidth / 5);

    return sp.Text($"{item.Name} [{bar}]");
});
```

Key animation behaviors:
- **Persistence**: Animations survive list reorders — each item keeps its own animation progress
- **Auto-advance**: Animations are ticked once per reconciliation frame automatically
- **Auto-schedule**: While any animation is running, re-renders are scheduled at ~60fps
- **Cleanup**: When an item is swept, its animations are disposed

### Available Animators

| Type | Purpose |
|------|---------|
| `NumericAnimator<int>` | Interpolate integer values |
| `NumericAnimator<float>` | Interpolate float values |
| `NumericAnimator<double>` | Interpolate double values |

### Easing Functions

| Function | Curve |
|----------|-------|
| `Easing.Linear` | Constant speed |
| `Easing.EaseInQuad` | Accelerate from zero |
| `Easing.EaseOutQuad` | Decelerate to zero |
| `Easing.EaseInOutQuad` | Accelerate then decelerate |
| `Easing.EaseInCubic` | Stronger acceleration |
| `Easing.EaseOutCubic` | Stronger deceleration |
| `Easing.EaseInOutCubic` | Stronger both |

### Animator Lifecycle

```csharp
var anim = sp.GetAnimations().Get<NumericAnimator<double>>("name", a =>
{
    a.From = 0.0;
    a.To = 100.0;
    a.Duration = TimeSpan.FromMilliseconds(500);
    a.Repeat = true;     // Loop continuously
}, autoStart: true);     // Started automatically (default)

// Control methods
anim.Pause();
anim.Resume();
anim.Reset();            // Back to start, stopped
anim.Restart();          // Reset + Start
anim.AnimateTo(50.0);   // Retarget from current value
```

## Elapsed Time

`StatePanelContext.Elapsed` provides the time since the last reconciliation frame. Subsystems can use this for custom time-based logic beyond the built-in animation system:

```csharp
ctx.StatePanel(model, sp =>
{
    // sp.Elapsed is TimeSpan since last frame
    var timer = sp.GetState(() => new StopwatchState());
    timer.Total += sp.Elapsed;

    return sp.Text($"Elapsed: {timer.Total.TotalSeconds:F1}s");
});
```

## Layout Behavior

StatePanelWidget has **no visual presence** — it is layout-invisible:

- **Measuring**: Passes constraints directly to child
- **Arranging**: Passes bounds directly to child
- **Rendering**: Renders child directly
- **Focus**: Passes through to focusable children
- **No size overhead**: Adds zero pixels to the layout

## Related Widgets

- [EffectPanel](/guide/widgets/effectpanel) — Visual post-processing effects (pairs well with StatePanel for animated effects)
- [ThemePanel](/guide/widgets/themepanel) — Scoped theme mutations
- [Containers](/guide/widgets/containers) — Other container widgets
