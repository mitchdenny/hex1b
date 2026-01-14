<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode         → src/Hex1b.Website/Examples/ProgressBasicExample.cs
  - indeterminateCode → src/Hex1b.Website/Examples/ProgressIndeterminateExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import determinateSnippet from './snippets/progress-determinate.cs?raw'
import customRangeSnippet from './snippets/progress-custom-range.cs?raw'
import indeterminateSnippet from './snippets/progress-indeterminate.cs?raw'
import themingSnippet from './snippets/progress-theming.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Download Progress"),
        v.Progress(75),
        v.Text(""),
        v.Text("Upload Progress"),
        v.Progress(30)
    ]))
    .Build();

await terminal.RunAsync();`

const indeterminateCode = `using Hex1b;

var animationPos = 0.0;
var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        // Animate the progress bar
        _ = Task.Run(async () =>
        {
            while (await timer.WaitForNextTickAsync())
            {
                animationPos = (animationPos + 0.02) % 1.0;
                app.Invalidate();
            }
        });

        return ctx => ctx.VStack(v => [
            v.Text("Loading..."),
            v.ProgressIndeterminate(animationPos)
        ]);
    })
    .Build();

await terminal.RunAsync();`

</script>

# Progress

Display progress bars for operations with known or unknown completion amounts.

## Basic Usage

Create a simple progress bar using the fluent API. By default, progress uses a 0-100 range:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="progress-basic" exampleTitle="Progress Widget - Basic Usage" />

## Determinate Progress

Use determinate progress when you know how much of the operation is complete. The progress bar fills proportionally based on the current value:

<StaticTerminalPreview svgPath="/svg/progress-determinate.svg" :code="determinateSnippet" />

### Custom Ranges

Progress supports any numeric range, not just 0-100. This is useful for showing bytes downloaded, items processed, or any other measurable quantity:

<StaticTerminalPreview svgPath="/svg/progress-custom-range.svg" :code="customRangeSnippet" />

The range can even include negative values (e.g., for temperature scales).

## Indeterminate Progress

Use indeterminate progress when you don't know how long an operation will take. An animated segment bounces back and forth:

<CodeBlock lang="csharp" :code="indeterminateCode" command="dotnet run" example="progress-indeterminate" exampleTitle="Progress Widget - Indeterminate" />

To animate the progress bar:
1. Store an animation position (0.0 to 1.0)
2. Update it periodically with a timer
3. Call `app.Invalidate()` to trigger re-renders

<StaticTerminalPreview svgPath="/svg/progress-indeterminate.svg" :code="indeterminateSnippet" />

## Layout Behavior

By default, the progress bar fills all available horizontal space. Use layout extensions to constrain its width:

```csharp
// Full width (default)
v.Progress(50)

// Fixed width
v.Progress(50).FixedWidth(30)

// Proportional width in HStack
h.Progress(50).Fill()
```

## Theming

Customize the appearance of progress bars using the theme system:

<StaticTerminalPreview svgPath="/svg/progress-theming.svg" :code="themingSnippet" />

Available theme elements:

| Element | Default | Description |
|---------|---------|-------------|
| `FilledCharacter` | `█` | Character for completed portion |
| `EmptyCharacter` | `░` | Character for remaining portion |
| `IndeterminateCharacter` | `█` | Character for animated segment |
| `FilledForegroundColor` | Green | Color for completed portion |
| `EmptyForegroundColor` | DarkGray | Color for remaining portion |
| `IndeterminateForegroundColor` | Cyan | Color for animated segment |

## Related Widgets

- [Text](/guide/widgets/text) - For displaying status messages alongside progress
- [Layout & Stacks](/guide/widgets/stacks) - For arranging progress bars with labels
