<script setup>
import FlowDiagram from '../.vitepress/theme/components/FlowDiagram.vue'
const flowBasicCode = `using Hex1b;
using Hex1b.Flow;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bFlow(async flow =>
    {
        // Step 1: Ask for a name
        var name = "";
        await flow.StepAsync(
            configure: step => ctx =>
                ctx.TextBox()
                    .OnSubmit(e =>
                    {
                        name = e.Text ?? "";
                        step.Complete(y => y.Text($"  ✓ Name: {name}"));
                    }),
            options: opts => opts.MaxHeight = 3
        );

        // Step 2: Pick a color
        var colors = new[] { "Red", "Green", "Blue" };
        var color = "";
        await flow.StepAsync(
            configure: step => ctx =>
                ctx.VStack(v => [
                    v.Text("Pick a color:"),
                    v.List(colors).OnItemActivated(e =>
                    {
                        color = colors[e.ActivatedIndex];
                        step.Complete(y => y.Text($"  ✓ Color: {color}"));
                    })
                ]),
            options: opts => opts.MaxHeight = 6
        );

        // After all steps complete, write a final summary
        await flow.StepAsync(ctx =>
            ctx.Text($"Hello {name}, you picked {color}!"));
    })
    .Build();

await terminal.RunAsync();`

const flowStateCode = `using Hex1b;
using Hex1b.Flow;

var state = new SetupState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bFlow(async flow =>
    {
        // Step 1: Project name
        await flow.StepAsync(
            configure: step => ctx =>
                ctx.VStack(v => [
                    v.Text("Enter your project name:"),
                    v.TextBox(state.ProjectName)
                        .OnSubmit(e =>
                        {
                            state.ProjectName = e.Text ?? "";
                            step.Complete(y =>
                                y.Text($"  ✓ Project: {state.ProjectName}"));
                        })
                        .FillWidth()
                ]),
            options: opts => opts.MaxHeight = 4
        );

        // Step 2: Framework
        await flow.StepAsync(
            configure: step => ctx =>
                ctx.VStack(v => [
                    v.Text("Select a framework:"),
                    v.List(SetupState.Frameworks)
                        .OnItemActivated(e =>
                        {
                            state.Framework = SetupState.Frameworks[e.ActivatedIndex];
                            step.Complete(y =>
                                y.Text($"  ✓ Framework: {state.Framework}"));
                        })
                        .FixedHeight(SetupState.Frameworks.Length + 1)
                ]),
            options: opts => opts.MaxHeight = 8
        );

        // Step 3: Confirm
        await flow.StepAsync(
            configure: step => ctx =>
                ctx.VStack(v => [
                    v.Text($"Create '{state.ProjectName}' with {state.Framework}?"),
                    v.HStack(h => [
                        h.Button("Yes").OnClick(_ =>
                        {
                            state.Confirmed = true;
                            step.Complete(y =>
                                y.Text($"  ✓ Created {state.ProjectName}!"));
                        }),
                        h.Button("No").OnClick(_ =>
                        {
                            step.Complete(y => y.Text("  ✗ Cancelled."));
                        })
                    ])
                ]),
            options: opts => opts.MaxHeight = 4
        );
    })
    .Build();

await terminal.RunAsync();

// Return an exit code based on state
return state.Confirmed ? 0 : 1;

class SetupState
{
    public static readonly string[] Frameworks =
        ["ASP.NET Core", "Blazor", "Console App", "Worker Service"];

    public string ProjectName { get; set; } = "";
    public string Framework { get; set; } = "";
    public bool Confirmed { get; set; }
}`

const exitCodeCode = `using Hex1b;
using Hex1b.Flow;

var state = new CommandState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bFlow(async flow =>
    {
        await flow.StepAsync(
            configure: step => ctx =>
                ctx.VStack(v => [
                    v.Text("Delete all files in /tmp?"),
                    v.HStack(h => [
                        h.Button("Yes, delete").OnClick(_ =>
                        {
                            state.ExitCode = 0;
                            step.Complete(y => y.Text("  ✓ Deleted."));
                        }),
                        h.Button("Cancel").OnClick(_ =>
                        {
                            state.ExitCode = 1;
                            step.Complete(y => y.Text("  ✗ Cancelled."));
                        })
                    ])
                ]),
            options: opts => opts.MaxHeight = 4
        );
    })
    .Build();

await terminal.RunAsync();
return state.ExitCode;

class CommandState
{
    public int ExitCode { get; set; } = 1;
}`

const spinnerCode = `using Hex1b;
using Hex1b.Flow;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bFlow(async flow =>
    {
        var status = "Initializing...";
        var done = false;

        await flow.StepAsync(
            configure: step =>
            {
                // Start background work
                _ = Task.Run(async () =>
                {
                    var steps = new[]
                    {
                        "Installing packages...",
                        "Compiling project...",
                        "Running tests..."
                    };

                    foreach (var s in steps)
                    {
                        status = s;
                        step.Invalidate();
                        await Task.Delay(1000);
                    }

                    done = true;
                    step.Invalidate();
                    await Task.Delay(300);
                    step.Complete(y => y.Text("  ✓ All tasks complete!"));
                });

                return ctx => ctx.HStack(h => [
                    h.Spinner(),
                    h.Text($" {status}")
                ]);
            },
            options: opts => opts.MaxHeight = 2
        );
    })
    .Build();

await terminal.RunAsync();`
</script>

# Your First Flow App

Flow apps render interactive UI steps inline in the normal terminal buffer — like `npm init`, `dotnet new`, or `az login`. Each step's output stays in the terminal's scrollback history, creating a natural top-to-bottom conversation.

::: tip Looking for full-screen apps?
If you want to build a full-screen TUI that takes over the entire terminal (like `vim` or `htop`), see **[Your First App](/guide/getting-started)** instead. Flow is for sequential, inline experiences.
:::

## How Flow Works

A flow is a sequence of **steps**. Each step:
1. Reserves rows at the current cursor position
2. Runs an interactive mini-app in that space
3. When `step.Complete(builder)` is called, replaces the interactive UI with frozen output
4. Advances the cursor, and the next step begins below

The result is a series of interactive prompts that scroll naturally through the terminal.

<FlowDiagram />

## Each Step is a Full Widget

A step isn't a simplified prompt — it's a real Hex1b app running in a reserved region of the normal terminal buffer. Inside a step, you have access to the full widget system:

- **Layout**: `VStack`, `HStack`, `Border`, `Splitter`, `ScrollPanel`
- **Input**: `TextBox`, `List`, `Button`, `Picker`, `Checkbox`, `Slider`
- **Display**: `Text`, `Spinner`, `ProgressBar`, `Table`, `Tree`
- **Behavior**: Theming, focus navigation, input bindings, background tasks

The only difference from a full-screen app is where it renders — in a fixed number of rows in the normal buffer instead of the entire alternate screen.

## The State Pattern

Define a state object and build your UI as a function of that state. This is the same pattern used in full-screen apps — widgets read from the state, event handlers mutate it, and Hex1b re-renders automatically.

```csharp
class SetupState
{
    public string ProjectName { get; set; } = "";
    public string Framework { get; set; } = "";
    public bool Confirmed { get; set; }
    public int ExitCode { get; set; } = 1;
}
```

The state object is a plain class with no framework dependencies, making it easy to test independently of the UI.

::: warning All output should go through Hex1b
Avoid using `Console.WriteLine` or writing directly to `stdout` while Hex1b is running. Hex1b manages cursor positioning, screen regions, and ANSI state — direct console writes will corrupt the display. Use widgets like `Text()` for all output, and use the state object to pass results out of the UI after it exits.
:::

## Basic Example

<CodeBlock lang="csharp" :code="flowBasicCode" command="dotnet run" />

## The Step Context

The `configure` overload of `StepAsync` gives you a `Hex1bStepContext` with these methods:

| Method | Purpose |
|--------|---------|
| `Complete(builder)` | Sets frozen output and stops the step |
| `RequestStop()` | Stops without frozen output |
| `Invalidate()` | Triggers a re-render (thread-safe) |
| `RequestFocus(predicate)` | Moves focus to a matching node |

Capture the `step` context in event handlers and background tasks to control the step from anywhere.

## Step Options

Each step accepts an optional `options` callback to configure `Hex1bFlowStepOptions`:

```csharp
options: opts =>
{
    opts.MaxHeight = 6;       // Limit rows reserved for this step
    opts.EnableMouse = true;  // Enable mouse input for this step
}
```

If `MaxHeight` is not set, the step uses the full terminal height.

## Multi-Step State

Define a state class that accumulates results across steps:

<CodeBlock lang="csharp" :code="flowStateCode" command="dotnet run" />

This pattern has several advantages:
- **Testable**: You can unit test `SetupState` independently of the UI
- **Inspectable**: After the flow completes, the state object holds all results
- **Exit codes**: Derive the exit code from the final state

## Returning Exit Codes

Since the flow callback is just an async lambda, derive your exit code from the state object after the flow completes:

<CodeBlock lang="csharp" :code="exitCodeCode" command="dotnet run" />

## Background Work with Invalidate

Use `step.Invalidate()` to trigger re-renders from background threads. This is essential for progress indicators and async operations:

<CodeBlock lang="csharp" :code="spinnerCode" command="dotnet run" />

The pattern is:
1. Capture `step` in the `configure` callback
2. Start a `Task.Run` that does work and calls `step.Invalidate()` after each state change
3. Call `step.Complete(builder)` when the work finishes

## Mixing Flow and Full-Screen

Flow supports `FullScreenStepAsync` for steps that need the alternate screen buffer. The flow saves inline state before entering full-screen and restores it after:

```csharp
await flow.FullScreenStepAsync((app, options) => ctx =>
    ctx.VStack(v => [
        v.Text("Full-screen editor"),
        v.Button("Done").OnClick(_ => app.RequestStop())
    ])
);
```

This is useful for steps that need more space than inline rendering can provide — like a file picker or a diff viewer.

## Next Steps

- [Your First App](/guide/getting-started) — Build a full-screen TUI with Hex1bApp
- [Widgets & Nodes](/guide/widgets-and-nodes) — Understand the widget architecture
- [Layout System](/guide/layout) — Master constraint-based layouts
- [Input Handling](/guide/input) — Keyboard navigation and shortcuts
- [Theming](/guide/theming) — Customize the appearance of your app
- [Widgets](/guide/widgets/) — Explore the full widget library
