<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode   → src/Hex1b.Website/Examples/TextBoxBasicExample.cs
  - submitCode  → src/Hex1b.Website/Examples/TextBoxSubmitExample.cs
  - formCode    → src/Hex1b.Website/Examples/TextBoxFormExample.cs
  - unicodeCode → src/Hex1b.Website/Examples/TextBoxUnicodeExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/textbox-basic.cs?raw'
import focusSnippet from './snippets/textbox-focus.cs?raw'
import selectionSnippet from './snippets/textbox-selection.cs?raw'

const basicCode = `using Hex1b;

var state = new InputState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("TextBox Widget Demo"),
        v.Text("────────────────────"),
        v.Text(""),
        v.Text("Enter your name:"),
        v.TextBox(state.Input).OnTextChanged(args => state.Input = args.NewText),
        v.Text(""),
        v.Text($"You typed: {state.Input}"),
        v.Text(""),
        v.Text("Try typing, using arrow keys, Home/End, etc.")
    ]))
    .Build();

await terminal.RunAsync();

class InputState
{
    public string Input { get; set; } = "";
}`

const submitCode = `using Hex1b;

var state = new ChatState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Chat Demo"),
        v.Text("──────────"),
        v.Text(""),
        v.Text("Type a message and press Enter:"),
        v.TextBox(state.Input)
            .OnTextChanged(args => state.Input = args.NewText)
            .OnSubmit(args => {
                if (!string.IsNullOrWhiteSpace(state.Input))
                {
                    state.Messages.Add(state.Input);
                    state.Input = "";
                }
            }),
        v.Text(""),
        v.Text("Messages:"),
        ..state.Messages.TakeLast(5).Select(m => v.Text($"  • {m}"))
    ]))
    .Build();

await terminal.RunAsync();

class ChatState
{
    public string Input { get; set; } = "";
    public List<string> Messages { get; } = [];
}`

const formCode = `using Hex1b;

var state = new FormState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.HStack(h => [
                h.Text("First Name: ").FixedWidth(12),
                h.TextBox(state.FirstName)
                    .OnTextChanged(args => state.FirstName = args.NewText)
            ]),
            v.HStack(h => [
                h.Text("Last Name:  ").FixedWidth(12),
                h.TextBox(state.LastName)
                    .OnTextChanged(args => state.LastName = args.NewText)
            ]),
            v.HStack(h => [
                h.Text("Email:      ").FixedWidth(12),
                h.TextBox(state.Email)
                    .OnTextChanged(args => state.Email = args.NewText)
            ]),
            v.Text(""),
            v.Button("Submit").OnClick(_ => state.Submitted = true),
            v.Text(""),
            v.Text("Use Tab to navigate between fields")
        ])
    ], title: "Registration Form"))
    .Build();

await terminal.RunAsync();

class FormState
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Submitted { get; set; }
}`

const unicodeCode = `using Hex1b;

const string DefaultText = "Hello 🎉 日本語 émoji 🚀 中文 ✨";
var state = new UnicodeState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Unicode Text Editing"),
        v.Text("─────────────────────"),
        v.Text(""),
        v.TextBox(state.Input).OnTextChanged(args => state.Input = args.NewText),
        v.Text(""),
        v.Text("Try navigating with arrow keys, deleting emoji,"),
        v.Text("or adding your own Unicode characters!"),
        v.Text(""),
        v.Button("Reset to Default").OnClick(_ => state.Input = DefaultText)
    ]))
    .Build();

await terminal.RunAsync();

class UnicodeState
{
    public string Input { get; set; } = DefaultText;
}`
</script>

# TextBoxWidget

An editable single-line text input widget for capturing user text.

TextBox is a focusable widget that accepts keyboard input. When focused, it displays a cursor and allows users to type, navigate, select, and edit text. It's commonly used in forms, search inputs, and any scenario requiring user-provided text.

## Basic Usage

Create a text input using the fluent API and handle changes with `OnTextChanged`:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="textbox-basic" exampleTitle="TextBox Widget - Basic Usage" />

::: tip State Binding
The TextBox doesn't store state internally. You provide the current text value and update your state in the `OnTextChanged` handler. This pattern gives you full control over the text content.
:::

## Event Handlers

TextBox provides two event handlers for different use cases.

### OnTextChanged

Called whenever the text content changes (typing, deleting, pasting):

```csharp
v.TextBox(state.Text)
    .OnTextChanged(args => {
        state.Text = args.NewText;
        Console.WriteLine($"Changed from '{args.OldText}' to '{args.NewText}'");
    })
```

The `TextChangedEventArgs` provides:
- `OldText` - The text content before the change
- `NewText` - The text content after the change
- `Widget` - The source TextBoxWidget
- `Node` - The underlying TextBoxNode
- `Context` - Access to the application context

### OnSubmit

Called when the user presses Enter in the text box:

<CodeBlock lang="csharp" :code="submitCode" command="dotnet run" example="textbox-submit" exampleTitle="TextBox Widget - Submit Handler" />

The `TextSubmittedEventArgs` provides:
- `Text` - The submitted text content
- `Widget` - The source TextBoxWidget  
- `Node` - The underlying TextBoxNode
- `Context` - Access to the application context

Use `OnSubmit` for:
- Chat/message input
- Command entry
- Single-field forms
- Search boxes

## Keyboard Navigation

TextBox supports comprehensive keyboard navigation:

| Key | Action |
|-----|--------|
| Characters | Insert at cursor position |
| `Backspace` | Delete character before cursor |
| `Delete` | Delete character at cursor |
| `←` / `→` | Move cursor by one character |
| `Home` | Move cursor to start of text |
| `End` | Move cursor to end of text |
| `Shift+←` / `Shift+→` | Extend selection left/right |
| `Shift+Home` | Select from cursor to start |
| `Shift+End` | Select from cursor to end |
| `Ctrl+A` | Select all text |
| `Enter` | Submit (if `OnSubmit` handler is set) |

### Mouse Support

In terminals that support mouse input:
- **Click** - Position cursor at click location
- **Double-click** - Select all text

## Focus Behavior

TextBox visually indicates its focus state:

| State | Appearance |
|-------|------------|
| Unfocused | `[text content]` |
| Focused | `[text▌content]` with visible cursor |
| Hovered | Faint cursor preview at mouse position |

<StaticTerminalPreview svgPath="/svg/textbox-focus.svg" :code="focusSnippet" />

The focused TextBox shows:
- A block cursor at the current position
- The cursor shape changes to a blinking bar

### Focus Navigation

- **Tab** - Move focus to the next focusable widget
- **Shift+Tab** - Move focus to the previous focusable widget

## Text Selection

Users can select text using keyboard shortcuts:

<StaticTerminalPreview svgPath="/svg/textbox-selection.svg" :code="selectionSnippet" />

Selected text is highlighted with configurable colors. Operations like typing or pressing Backspace replace the selected text.

## Form Example

Here's a complete form with multiple TextBox widgets:

<CodeBlock lang="csharp" :code="formCode" command="dotnet run" example="textbox-form" exampleTitle="TextBox Widget - Form Example" />

## Unicode Support

TextBox correctly handles Unicode text including:

- **Wide characters** (CJK): 日本語, 中文, 한국어
- **Emoji**: 🎉 🚀 ✨
- **Combining characters**: é, ñ

Navigation and deletion work on grapheme clusters, so pressing Backspace on an emoji deletes the entire emoji, not individual code points.

Try it yourself—navigate through the text with arrow keys, delete some emoji, and use the reset button to restore the original content:

<CodeBlock lang="csharp" :code="unicodeCode" command="dotnet run" example="textbox-unicode" exampleTitle="TextBox Widget - Unicode Support" />

## Sizing

By default, `TextBoxWidget` reports a `Fill` width hint, so it expands to
take up whatever horizontal space its parent makes available — input feels
best on a wide editable surface. Override that default the same way you
would for any other widget:

```csharp
ctx.HStack(h => [
    h.Text("Name:"),
    h.TextBox(state.Name),                       // fills the rest of the row
]);

ctx.HStack(h => [
    h.Text("Code:"),
    h.TextBox(state.Code).FixedWidth(8),         // hugs an 8-cell field
    h.Text(" "),
    h.TextBox(state.Notes).ContentWidth(),       // sizes to its current text
]);
```

Multi-line textboxes still default to fill width; height defaults to
content (one line per visual line) unless you call `.Height(lines)`.

## Theming

Customize TextBox appearance using theme elements:

```csharp
var theme = Hex1bTheme.Create()
    .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.Black)
    .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.Yellow)
    .Set(TextBoxTheme.SelectionForegroundColor, Hex1bColor.White)
    .Set(TextBoxTheme.SelectionBackgroundColor, Hex1bColor.Blue)
    .Set(TextBoxTheme.PredictionForegroundColor, Hex1bColor.LightGray);

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
| `ForegroundColor` | `Hex1bColor` | Default | Text color (inherits global text colour) |
| `BackgroundColor` | `Hex1bColor` | Default | Background outside the field fill (rarely set; the field itself uses `FillBackgroundColor`) |
| `FocusedForegroundColor` | `Hex1bColor` | Default | Text color when focused |
| `FillBackgroundColor` | `Hex1bColor` | `rgb(40, 40, 40)` | Resting field background. Paints the chip body that delineates the text area when the textbox is unfocused. The same tone is used by `ToggleSwitchTheme.UnselectedBackgroundColor` so input surfaces share a family. |
| `FocusedFillBackgroundColor` | `Hex1bColor` | `rgb(55, 55, 55)` | Focused field background. Slightly lighter than `FillBackgroundColor` to indicate active input. |
| `CursorForegroundColor` | `Hex1bColor` | Black | Cursor text color |
| `CursorBackgroundColor` | `Hex1bColor` | White | Cursor background color |
| `SelectionForegroundColor` | `Hex1bColor` | Black | Selected text color |
| `SelectionBackgroundColor` | `Hex1bColor` | Cyan | Selection background |
| `PredictionForegroundColor` | `Hex1bColor` | Gray | Inline prediction text color. Monochrome by default for a ghost-text look; theme to any color to brand suggestions. |
| `PredictionBackgroundColor` | `Hex1bColor` | Default | Inline prediction background. `Default` follows the field fill color; set explicitly to draw the suggestion on a contrasting band. |

## Related Widgets

- [TextWidget](/guide/widgets/text) - For non-editable text display
- [ButtonWidget](/guide/widgets/button) - For clickable actions
- [ListWidget](/guide/widgets/list) - For selecting from options
