# Composing Widgets

You've built a screen out of stacks, borders, buttons and text. Now you want to wrap a chunk of it up as a single thing — a search bar, a date picker, a "card" — and reuse it in three more places. This guide shows you how, starting from the simplest possible answer and only stepping up when you actually need more.

::: tip Read first
This page assumes you're comfortable with the [Widgets & Nodes](/guide/widgets-and-nodes) model and the fluent builder API used throughout [Your First App](/guide/getting-started). You don't need to know anything about how reconciliation works internally.
:::

## The big idea

Hex1b widgets are values. A widget is a small immutable record that *describes* what to render. You compose them by placing widgets inside other widgets:

```csharp
ctx.VStack(v => [
    v.Text("Name"),
    v.TextBox("..."),
    v.Button("Save")
])
```

Reusing a chunk of that tree is, at its core, just reusing some C# code that returns a widget. There's nothing special you have to "register" — the framework already treats every part of the tree the same way. What changes is *how you package the reuse*, and Hex1b gives you three escalating options:

| Option | When to use it |
|---|---|
| **Builder method** | One file, a snippet of UI you call in a few places. No state, no inputs of its own. |
| **Extension method on `WidgetContext<>`** | A reusable visual that should feel like a built-in (`v.MyCard(...)`). |
| **`Hex1bCompositeWidget`** | A reusable *control* with its own state, event handlers, ambient context, or rebindable input. |

Pick the smallest one that does the job. You can always graduate later.

## Option 1: Just a method

The lightest form of composition is a plain C# method that takes a builder context and returns a widget. No new types, no framework concepts:

```csharp
using Hex1b;
using Hex1b.Widgets;

static Hex1bWidget LabelledField<T>(WidgetContext<T> ctx, string label, string value)
    where T : Hex1bWidget
    => ctx.HStack(h => [
        h.Text($"{label}:"),
        h.Text(value)
    ]);
```

Use it like any other builder call:

```csharp
ctx.VStack(v => [
    LabelledField(v, "Name",  "Ada"),
    LabelledField(v, "Email", "ada@example.com"),
    LabelledField(v, "Role",  "Admin"),
])
```

The generic `WidgetContext<T>` parameter is what lets the helper work inside *any* parent — a `VStack`, an `HStack`, a `Border`, anything. You'll see this same pattern throughout the built-in widget extensions.

This style is perfect when:

- The chunk is purely presentational.
- It doesn't need to remember anything between frames.
- It only lives in one project.

If those start to feel limiting, climb the next rung.

## Option 2: An extension method that reads like a built-in

A small step up: turn the helper into an extension method on `WidgetContext<>`. Now it shows up in IntelliSense alongside `Text`, `Button`, `VStack`:

```csharp
using Hex1b;
using Hex1b.Widgets;

namespace MyApp.Widgets;

public static class FieldExtensions
{
    public static Hex1bWidget LabelledField<T>(
        this WidgetContext<T> ctx,
        string label,
        string value) where T : Hex1bWidget
        => ctx.HStack(h => [
            h.Text($"{label}:"),
            h.Text(value)
        ]);
}
```

Now the call site is fully fluent:

```csharp
ctx.VStack(v => [
    v.LabelledField("Name",  "Ada"),
    v.LabelledField("Email", "ada@example.com"),
])
```

This is the right home for **stateless, reusable visuals** that you want callers to discover naturally. Most simple "card", "field", "section header" abstractions land here.

::: tip Use the fluent API everywhere
Inside any builder lambda or composite `Build` method, prefer the fluent calls (`ctx.Text(...)`, `v.VStack(...)`) over `new TextBlockWidget(...)`. The fluent API is consistent, discoverable, and how every built-in widget composes its children.
:::

## Option 3: A composite widget with its own state

Eventually you'll want a piece of UI that:

- **Remembers things across frames** (a counter value, a debounce timer, an open/closed flag).
- **Exposes a typed configuration surface** (so callers say `v.SearchBar(placeholder: "...").OnQuery(...)`).
- **Coordinates other widgets** that share state (a date picker that internally has a header, a grid, and arrow buttons all reading the same selected date).

That's what `Hex1bCompositeWidget` is for. You write a record that describes the widget's *inputs*, override `Build`, and the framework hands you a `CompositionContext` that exposes per-instance state and ambient values.

::: warning Experimental
The composite-widget API is currently gated behind the `HEX1B_COMPOSITION` experimental diagnostic. To use it, suppress the warning in your `.csproj`:
```xml
<NoWarn>$(NoWarn);HEX1B_COMPOSITION</NoWarn>
```
The shape of the API may change before it's marked stable.
:::

### A minimal example

A self-contained counter widget — its own state, its own keybindings, no node subclass — and the small extension method that lets callers reach for it through the fluent API:

```csharp
using Hex1b;
using Hex1b.Composition;
using Hex1b.Widgets;

public sealed record CounterWidget(string Label) : Hex1bCompositeWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var state = ctx.UseState(() => new CounterState());

        return ctx.HStack(h => [
            h.Text($"{Label}: {state.Count}"),
            h.Button("+").OnClick(_ => state.Count++),
            h.Button("-").OnClick(_ => state.Count--),
        ]);
    }

    private sealed class CounterState
    {
        public int Count;
    }
}

public static class CounterWidgetExtensions
{
    public static CounterWidget Counter<T>(this WidgetContext<T> ctx, string label)
        where T : Hex1bWidget
        => new(label);
}
```

Now callers consume it the same way they consume any built-in widget:

```csharp
ctx.VStack(v => [
    v.Counter("Apples"),
    v.Counter("Oranges"),
])
```

Each instance keeps its own `CounterState`. They don't see each other.

::: tip Always ship an extension method
This is the convention every built-in widget follows, and you should follow it for your own composites too. The pattern is:

1. Define the widget record (`MyThingWidget`).
2. Add a `static class MyThingWidgetExtensions` with an extension method on `WidgetContext<T>` returning the widget.

That way, **inside any builder lambda, callers never have to write `new ...Widget(...)`** — they stay on the discoverable, IntelliSense-friendly `v.MyThing(...)` surface. Keeping `new` out of `Build` methods and builder lambdas is a hard convention in the codebase.
:::

::: info Naming conventions
Composite widgets follow the same conventions as any other Hex1b widget:
- The type ends in `Widget`.
- It's declared `record`.
- It doesn't expose `With*` methods — use bare verb-noun names like `Title(...)` and `OnClick(...)` (the `With*` prefix is reserved for `Hex1bTerminalBuilder`).

The Hex1b repo enforces these via analyzer rules HEX1B0001–HEX1B0005 internally; the analyzers aren't shipped in the public NuGet package, so external projects should treat the conventions as a code-review checklist rather than a build error.
:::

### What `CompositionContext` gives you

| Member | What it does |
|---|---|
| `UseState<T>(factory)` | Returns the same `T` instance every frame. Created lazily on first call. |
| `Provide<T>(value)` | Publishes `value` as ambient context for descendants. |
| `Use<T>()` | Pulls the nearest ambient `T` from an ancestor composite. Returns `null` if none. |
| `Require<T>()` | Like `Use<T>()` but throws if no ancestor provided one. |
| `IsNew` | `true` only on the first reconciliation pass. Useful for one-time setup. |
| `CancellationToken` | Cancels when the composite is being torn down. |

Plus the entire fluent widget builder API (`ctx.Text`, `ctx.VStack`, `ctx.Button`, …) because `CompositionContext` derives from `WidgetContext<>`.

### How `UseState` actually behaves

Each call to `UseState<T>(factory)` is keyed by the type `T`. The factory runs **once** for that composite instance; every subsequent frame returns the *same object*. That means you can mutate it freely, and the changes survive between frames:

```csharp
protected override Hex1bWidget Build(CompositionContext ctx)
{
    var state = ctx.UseState(() => new MyState());

    if (ctx.IsNew)
    {
        // First frame only — load defaults, kick off background work, etc.
        state.LoadDefaults();
    }

    state.FrameCount++;       // Mutating is fine; the object lives across frames.
    return ctx.Text($"frames: {state.FrameCount}");
}
```

Because `T` is the key, a single composite holds **at most one instance per type**. If you need multiple values, wrap them in one state class:

```csharp
private sealed class FormState
{
    public string Name = "";
    public string Email = "";
    public bool   Submitting;
}
```

State that implements `IDisposable` is disposed automatically when the composite is removed from the tree (or replaced by a different composite type at the same position).

::: warning Mutating state doesn't auto-render
Today, mutating a `UseState` object doesn't trigger a re-render on its own — you call `app.Invalidate()` (the same as anywhere else in Hex1b). The cleanest pattern is to capture an `Invalidate` callback into your state object and have its mutator methods call it (see the Counter Store example below).
:::

## Sharing state between sibling widgets

Three composites placed side by side don't see each other's state — siblings are isolated. To share state, place it on a **common ancestor** and let descendants pull it down with `Provide` / `Use`. This is the same idea as React Context, F# computation expressions' "ambient state", or .NET's `AsyncLocal<T>` — but lexically scoped to a subtree.

### A worked example

Three composites cooperate on a shared counter, with no node subclasses, no manual ancestor walks, no closure tricks:

```csharp
using Hex1b;
using Hex1b.Composition;
using Hex1b.Input;
using Hex1b.Widgets;

public sealed record AppShellWidget(Action Invalidate, Action RequestStop)
    : Hex1bCompositeWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        // Per-instance state, allocated once and reused across frames.
        var store = ctx.UseState(() => new CounterStore { Invalidate = Invalidate });

        // Publish to descendants via the typed ambient API.
        ctx.Provide(store);

        return ctx.VStack(v => [
            v.Text("Up/Down to change counter, R to reset, Q to quit"),
            v.Separator(),

            v.CounterDisplay(),
            v.CounterStatus(),
        ])
        .InputBindings(b =>
        {
            b.Key(Hex1bKey.UpArrow).Global().Action(_ => store.Increment(), "Increment");
            b.Key(Hex1bKey.DownArrow).Global().Action(_ => store.Decrement(), "Decrement");
            b.Key(Hex1bKey.R).Global().Action(_ => store.Reset(), "Reset");
            b.Key(Hex1bKey.Q).Global().Action(_ => RequestStop(), "Quit");
        });
    }
}

public sealed record CounterDisplayWidget : Hex1bCompositeWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var store = ctx.Require<CounterStore>();
        return ctx.Text($"  Count: {store.Count}");
    }
}

public sealed record CounterStatusWidget : Hex1bCompositeWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var store = ctx.Require<CounterStore>();
        var status = store.Count switch
        {
            0   => "  (idle)",
            > 0 => $"  ↑ positive ({store.Count})",
            _   => $"  ↓ negative ({store.Count})",
        };
        return ctx.Text(status);
    }
}

internal sealed class CounterStore
{
    public int Count { get; private set; }
    public required Action Invalidate { get; init; }

    public void Increment() { Count++; Invalidate(); }
    public void Decrement() { Count--; Invalidate(); }
    public void Reset()     { Count = 0; Invalidate(); }
}

// Fluent extensions — one per composite, so call sites never use `new`.

public static class CounterCompositeExtensions
{
    public static AppShellWidget AppShell<T>(
        this WidgetContext<T> ctx,
        Action invalidate,
        Action requestStop) where T : Hex1bWidget
        => new(invalidate, requestStop);

    public static CounterDisplayWidget CounterDisplay<T>(this WidgetContext<T> ctx)
        where T : Hex1bWidget
        => new();

    public static CounterStatusWidget CounterStatus<T>(this WidgetContext<T> ctx)
        where T : Hex1bWidget
        => new();
}
```

What's happening here:

1. `AppShellWidget` owns the `CounterStore` via `UseState`. It survives every reconciliation pass.
2. `Provide(store)` pins the store onto the shell's node so descendants can find it.
3. Both `CounterDisplayWidget` and `CounterStatusWidget` call `Require<CounterStore>()` and get back the *same* instance.
4. The shell's input bindings mutate the store; mutator methods fire `Invalidate`; on the next frame, both readers see the new value.

You can find this exact program under `samples/CompositionDemo` in the repo.

### Provide / Use mechanics

- **Lookup is upward only.** `Use<T>()` walks the ancestor chain looking for a composite that called `Provide<T>(...)`. Siblings cannot see each other's `Provide`s — they share an *ancestor*, not a parent/child relationship.
- **Type-keyed.** A composite holds at most one provided value per type. Calling `Provide<T>(...)` again replaces the previous one.
- **Shadowing works.** A nested composite that provides the same `T` shadows the outer value for *its* subtree only.
- **`Require<T>()` is the strict variant.** Use it when you've made an ancestor a hard prerequisite — you'll get a clear `InvalidOperationException` if someone places the consumer outside the right subtree.

## Decision guide

Walk down the list and stop at the first "yes":

1. **Does it need any state across frames?** No → **Option 1 or 2**.
2. **Does it need to coordinate other widgets in its subtree?** Yes → **Composite + `Provide`/`Use`**.
3. **Does it have meaningful inputs (a label, a callback) you want to type-check at the call site?** Yes → **Composite**, even without state.
4. **Are you mostly bundling layout?** → **Option 1 or 2** is plenty.

If you find yourself reaching for a custom `Hex1bNode` subclass just to "host" a few children, stop and try a composite first — that's exactly what composites exist to replace.

## Common patterns

### One-time async work

```csharp
protected override Hex1bWidget Build(CompositionContext ctx)
{
    var state = ctx.UseState(() => new LoaderState());

    if (ctx.IsNew)
    {
        _ = LoadAsync(state, ctx.CancellationToken);
    }

    return state.IsReady
        ? ctx.Text($"Loaded {state.Count} items")
        : ctx.Text("Loading…");
}

static async Task LoadAsync(LoaderState state, CancellationToken ct)
{
    state.Count = await FetchCountAsync(ct);
    state.IsReady = true;
    state.Invalidate();   // app.Invalidate captured into the state object
}
```

### Form context

```csharp
public sealed record FormWidget(Func<FormResult, Task> OnSubmit, Hex1bWidget Body)
    : Hex1bCompositeWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var form = ctx.UseState(() => new FormController(OnSubmit));
        ctx.Provide(form);
        return Body;
    }
}

public sealed record FormFieldWidget(string Name, string Label) : Hex1bCompositeWidget
{
    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var form = ctx.Require<FormController>();
        return ctx.HStack(h => [
            h.Text(Label),
            h.TextBox(form.GetValue(Name)).OnChanged(e => form.SetValue(Name, e.Text)),
        ]);
    }
}

public static class FormCompositeExtensions
{
    public static FormWidget Form<T>(
        this WidgetContext<T> ctx,
        Func<FormResult, Task> onSubmit,
        Hex1bWidget body) where T : Hex1bWidget
        => new(onSubmit, body);

    public static FormFieldWidget FormField<T>(
        this WidgetContext<T> ctx,
        string name,
        string label) where T : Hex1bWidget
        => new(name, label);
}
```

Callers compose entirely through the fluent API:

```csharp
ctx.Form(SubmitAsync, ctx.VStack(v => [
    v.FormField("name",  "Name"),
    v.FormField("email", "Email"),
]))
```

Any number of `FormField`s nested anywhere under a `Form` will pick up the same `FormController`.

### Theming or localisation overrides

`Provide<T>(...)` is great for ambient values that affect rendering but don't belong in widget constructors — culture info, feature flags, display density, etc. Wrap a subtree in a tiny composite that provides the value, and any descendant composite can pull it down without threading it through every layer.

## Caveats and gotchas

- **State doesn't auto-invalidate.** Mutating an object returned by `UseState` won't redraw on its own — call `app.Invalidate()`. The convention is to capture an invalidate callback into your state object so its mutators do this automatically.
- **State is per *node position*.** If you swap a composite at the same tree position for a *different composite type*, the framework disposes the old state and starts fresh. Same type → state preserved.
- **`Build` runs every frame.** Keep it cheap. Heavy work belongs inside state objects that compute once and read on subsequent frames.
- **No sibling visibility.** If two unrelated composites need to share state, hoist a common ancestor and `Provide` it. Don't try to look sideways.
- **Experimental.** The composite-widget API may change; pin a version if you're relying on it in production.

## Related reading

- [Widgets & Nodes](/guide/widgets-and-nodes) — the underlying model that composites build on.
- [StatePanelWidget](/guide/widgets/statepanel) — the right tool when you need state attached to **list items** that get reordered across frames.
- [Input Handling](/guide/input) — how to attach (and let users rebind) keys on a composite.
- [Theming](/guide/theming) — `ThemePanelWidget` is a great target to wrap up in a tiny composite.
