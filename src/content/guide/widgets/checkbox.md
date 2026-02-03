<script setup>
const basicCode = `using Hex1b;
using Hex1b.Widgets;

var state = new CheckboxState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Terms and Conditions"),
        v.Text(""),
        v.Checkbox(state.AcceptTerms ? CheckboxState.Checked : CheckboxState.Unchecked)
            .Label("I accept the terms")
            .OnToggled(e => state.AcceptTerms = !state.AcceptTerms),
        v.Text(""),
        v.Button("Continue")
            .OnClick(_ => { if (state.AcceptTerms) Console.WriteLine("Accepted!"); })
    ]))
    .Build();

await terminal.RunAsync();

class CheckboxState
{
    public bool AcceptTerms { get; set; }
}`

const statesCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Checkbox States:"),
        v.Text(""),
        v.Checkbox().Unchecked().Label("Unchecked [ ]"),
        v.Checkbox().Checked().Label("Checked [x]"),
        v.Checkbox().Indeterminate().Label("Indeterminate [-]")
    ]))
    .Build();

await terminal.RunAsync();`
</script>

# Checkbox

The Checkbox widget displays a toggleable checked/unchecked/indeterminate state with an optional label.

## Basic Usage

Create a checkbox with the `Checkbox()` extension method. Use `OnToggled()` to handle state changes.

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="checkbox-basic" exampleTitle="Checkbox Widget - Basic Usage" />

**Key features:**
- Click or press Space/Enter to toggle
- Use `Label()` to add descriptive text
- Manage state externally (checkboxes are stateless widgets)

## States

Checkboxes support three visual states:

<CodeBlock lang="csharp" :code="statesCode" command="dotnet run" example="checkbox-states" exampleTitle="Checkbox Widget - States" />

| State | Display | Usage |
|-------|---------|-------|
| `Unchecked` | `[ ]` | Default, option not selected |
| `Checked` | `[x]` | Option is selected |
| `Indeterminate` | `[-]` | Partial selection (e.g., some children selected) |

## Fluent API

| Method | Description |
|--------|-------------|
| `Checked()` | Set to checked state |
| `Unchecked()` | Set to unchecked state |
| `Indeterminate()` | Set to indeterminate state |
| `Label(string)` | Add label text after the checkbox |
| `OnToggled(handler)` | Handle toggle events |

## Keyboard Support

| Key | Action |
|-----|--------|
| Space | Toggle the checkbox |
| Enter | Toggle the checkbox |
| Tab | Move to next focusable widget |

## Related Widgets

- [ToggleSwitch](/guide/widgets/toggle-switch) — On/off switch with visual feedback
- [Tree](/guide/widgets/tree) — Hierarchical lists with multi-select checkboxes
- [Button](/guide/widgets/button) — Clickable actions
