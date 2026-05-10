// SlashCommandPromptWidget — a composite that turns a plain TextBox into a
// slash-command prompt with a completion palette.
//
// Behaviour:
//   - When the text starts with "/" and the rest of the buffer (up to the
//     first space) prefix-matches a registered command, a bordered list of
//     matches appears immediately above the textbox in normal flow (it
//     pushes the transcript above it up by however many rows the list
//     needs).
//   - Up/Down arrows navigate the list; mouse hover over a row also moves
//     the selection. While the palette is open the textbox shows a /<cmd>
//     PREVIEW of the highlighted match — the user's typed prefix is preserved
//     internally and is only replaced when they actively confirm.
//   - Tab, Enter or a left-click on a row CONFIRM the highlighted command,
//     baking it into the buffer as "/<commandName> " with the cursor after
//     the trailing space (ready for arguments).
//   - Typing or backspacing while a preview is showing edits the user's
//     underlying typed prefix (we diff against the rendered preview, not
//     against the typed prefix), so the visible filter behaves naturally.
//   - Escape dismisses the palette by clearing the current input.
//   - When the palette is closed, Enter submits as normal and fires OnSubmit.
//
// Implementation note — state lifting:
//   The textbox is bound to a hoisted TextBoxState owned by this composite via
//   CompositionContext.UseState. Build is the single source of truth for what
//   appears in the textbox: it overwrites state.TextBox.Text on every render
//   to either the typed prefix (palette closed) or a preview of the
//   highlighted match (palette open). User keystrokes flow through
//   OnTextChanged, which diffs the change against the previously-rendered
//   text and applies the same delta to state.TypedPrefix. There is no
//   "controlled-mode" flag — hoisted state IS the framework-wide pattern
//   for this kind of preview/confirm UI.
//
// Implementation note — why the palette is in flow rather than a Float:
//   We originally tried to anchor a FloatWidget above the TextBox with
//   `.ExtendTop(textbox)`. In a composite whose flow content is just one
//   row tall (the textbox), the surface allocated for the composite is
//   also one row tall, and any float that arranges *outside* those bounds
//   is clipped away during compositing. Putting the palette in normal
//   flow makes the composite grow vertically when the palette is visible
//   — which is what users intuitively expect from a slash-command popup
//   anyway.
//
// All state lives in the widget's CompositionContext via UseState — there
// is no custom Hex1bNode anywhere in this file.

namespace AgenticPromptDemo;

using Hex1b;
using Hex1b.Composition;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

public sealed record SlashCommandPromptWidget(IReadOnlyList<SlashCommand> Commands)
    : Hex1bWidget
{
    // The prompt chevron rendered to the left of the textbox. Exposed so
    // tests (and any other layout-aware code) can locate the prompt row in
    // a captured screen without re-deriving the format string.
    public const string PromptChevron = " > ";

    internal Func<string, Task>? SubmitHandler { get; init; }

    public SlashCommandPromptWidget OnSubmit(Action<string> handler)
        => this with { SubmitHandler = t => { handler(t); return Task.CompletedTask; } };

    public SlashCommandPromptWidget OnSubmit(Func<string, Task> handler)
        => this with { SubmitHandler = handler };

    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var state = ctx.UseState(() => new PromptState());

        // Filter commands against the user's typed prefix (NOT against whatever
        // preview is currently rendered).
        var matches = ComputeMatches(state.TypedPrefix, Commands);
        var paletteVisible = matches.Count > 0;

        if (paletteVisible)
        {
            state.SelectedIndex = Math.Clamp(state.SelectedIndex, 0, matches.Count - 1);
        }
        else
        {
            state.SelectedIndex = 0;
        }

        // Compute what the textbox should show. While the palette is open we
        // show a "/<command>" preview of the highlighted match; otherwise we
        // show the user's literal typed prefix.
        var displayText = paletteVisible
            ? "/" + matches[state.SelectedIndex].Name
            : state.TypedPrefix;

        // Drive the textbox via its hoisted TextBoxState. Build owns the
        // displayed text — we overwrite Text on every render to keep preview
        // / typed-prefix in sync. The textbox itself is now a pure view.
        //
        // Whenever Build OVERWRITES the buffer, push the cursor to the end of
        // the new contents. This is correct for every overwrite scenario:
        //
        //   - Palette open with a new preview: cursor sits at the end of the
        //     preview so further keystrokes append (and HandleUserEdit can
        //     interpret them as suffix-add against the rendered preview).
        //   - Palette closed because the user just confirmed: cursor sits at
        //     the end of "/cmd " so the user types arguments.
        //   - Palette closed because the user submitted (Text=""): cursor at 0.
        //
        // When the user is editing TypedPrefix mid-string, Build does NOT
        // enter this branch — state.TextBox.Text already equals displayText
        // (because OnTextChanged updated TypedPrefix to match what the user
        // just typed), so we leave the cursor alone.
        if (state.TextBox.Text != displayText)
        {
            state.TextBox.Text = displayText;
            state.TextBox.CursorPosition = displayText.Length;
        }

        return ctx.VStack(v =>
        {
            var textbox = v.TextBox()
                .State(state.TextBox)
                .OnTextChanged(e => HandleUserEdit(state, e.OldText, e.NewText))
                .OnSubmit(e =>
                {
                    var text = e.Text?.Trim();
                    if (string.IsNullOrEmpty(text))
                    {
                        return;
                    }

                    SubmitHandler?.Invoke(text);
                    state.TypedPrefix = "";
                    state.TextBox.Text = "";
                });

            // Capture navigation / accept / dismiss only while the palette is open.
            // When the palette is closed these bindings are absent, so up/down do
            // nothing on a single-line textbox and Enter falls through to OnSubmit.
            if (paletteVisible)
            {
                var matchesSnapshot = matches;  // capture for handlers
                textbox = textbox.InputBindings(b =>
                {
                    b.Key(Hex1bKey.UpArrow).Action(_ =>
                    {
                        if (state.SelectedIndex > 0)
                        {
                            state.SelectedIndex--;
                        }
                    }, "Previous command");

                    b.Key(Hex1bKey.DownArrow).Action(_ =>
                    {
                        if (state.SelectedIndex < matchesSnapshot.Count - 1)
                        {
                            state.SelectedIndex++;
                        }
                    }, "Next command");

                    b.Key(Hex1bKey.Tab).Action(_ => Confirm(state, matchesSnapshot), "Accept command");
                    b.Key(Hex1bKey.Enter).Action(_ => Confirm(state, matchesSnapshot), "Accept command");

                    b.Key(Hex1bKey.Escape).Action(_ =>
                    {
                        state.TypedPrefix = "";
                        state.TextBox.Text = "";
                    }, "Dismiss palette");
                });

                return [
                    BuildPalette(v, matchesSnapshot, state),
                    BuildPromptRow(v, textbox),
                    v.Separator(),
                ];
            }

            return [
                BuildPromptRow(v, textbox),
                v.Separator(),
            ];
        });
    }

    // Translate a user edit on the visible text into an edit on TypedPrefix.
    //
    // While the palette is open the textbox shows a /<cmd> PREVIEW. We don't
    // want a keystroke to commit the preview wholesale (the user just wants
    // to keep editing their actual prefix); but we also can't trust e.NewText
    // verbatim because the OS-level change is from "preview" to
    // "preview ± edit", not from "TypedPrefix" to "TypedPrefix ± edit".
    //
    // So we compute the diff between OldText (= what was on screen before
    // the edit, i.e. the preview) and NewText (= what's there now), and
    // apply the SAME diff to TypedPrefix:
    //   - suffix added → append to TypedPrefix
    //   - suffix removed → trim same number of trailing chars from TypedPrefix
    //   - other (paste, midline edit, etc.) → fall back to NewText verbatim
    //
    // Note: when the palette is closed, OldText == TypedPrefix, so the diff
    // collapses to "TypedPrefix ± edit" naturally. The same code path works
    // for both modes.
    private static void HandleUserEdit(PromptState state, string oldText, string newText)
    {
        // Pure suffix-add: NewText starts with OldText and is longer.
        if (newText.Length > oldText.Length && newText.StartsWith(oldText, StringComparison.Ordinal))
        {
            var suffix = newText[oldText.Length..];
            state.TypedPrefix += suffix;
            return;
        }

        // Pure suffix-remove (e.g. backspace at end): OldText starts with
        // NewText and is longer. Strip the same number of trailing chars
        // from TypedPrefix, clamped to >= 0.
        if (oldText.Length > newText.Length && oldText.StartsWith(newText, StringComparison.Ordinal))
        {
            var removed = oldText.Length - newText.Length;
            var keep = Math.Max(0, state.TypedPrefix.Length - removed);
            state.TypedPrefix = state.TypedPrefix[..keep];
            return;
        }

        // Anything else (paste, midline edit, full replace) — give up on the
        // diff and adopt whatever the textbox now contains as the new typed
        // prefix. This is the safe fallback even if the palette was hiding a
        // preview, because the user's edit was big enough that they almost
        // certainly meant to overwrite.
        state.TypedPrefix = newText;
    }

    // Bake the highlighted command into TypedPrefix with a trailing space.
    // The trailing space causes ComputeMatches to return no hits on the next
    // build, which closes the palette and leaves the cursor positioned after
    // "/cmd " ready for arguments.
    private static void Confirm(PromptState state, IReadOnlyList<SlashCommand> matches)
    {
        state.TypedPrefix = "/" + matches[state.SelectedIndex].Name + " ";
    }

    // The visible prompt row: a chevron prefix on the left and the textbox
    // filling the remaining width on the right. Mirrors the look of the
    // Copilot CLI / Claude Code prompts.
    private static Hex1bWidget BuildPromptRow(WidgetContext<VStackWidget> ctx, TextBoxWidget textbox)
        => ctx.HStack(h =>
        [
            h.Text(PromptChevron),
            StyleTextBox(h, textbox).FillWidth(),
        ]);

    // The textbox already renders with the form-style fill background by
    // default, so this helper is just a passthrough kept for parity with the
    // older bracket-mode API. Generic over the parent context type so the
    // same helper works whether we're sitting in a VStack or an HStack.
    private static Hex1bWidget StyleTextBox<TParent>(WidgetContext<TParent> context, TextBoxWidget textbox)
        where TParent : Hex1bWidget
        => textbox;

    private static IReadOnlyList<SlashCommand> ComputeMatches(
        string text,
        IReadOnlyList<SlashCommand> commands)
    {
        if (string.IsNullOrEmpty(text) || text[0] != '/')
        {
            return Array.Empty<SlashCommand>();
        }

        // Once the user types past the command name (whitespace appears),
        // they're filling in arguments — close the palette.
        var query = text.AsSpan(1);
        var space = query.IndexOf(' ');
        if (space >= 0)
        {
            return Array.Empty<SlashCommand>();
        }

        var queryStr = query.ToString();
        var hits = new List<SlashCommand>();
        foreach (var cmd in commands)
        {
            if (cmd.Name.StartsWith(queryStr, StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(cmd);
            }
        }
        return hits;
    }

    private static Hex1bWidget BuildPalette(
        WidgetContext<VStackWidget> ctx,
        IReadOnlyList<SlashCommand> matches,
        PromptState state)
    {
        // Each row is wrapped in an Interactable so the mouse can drive both
        // selection (hover) and confirmation (click). Hover handlers update
        // state.SelectedIndex, which causes the next render to:
        //   - move the ❯ marker, AND
        //   - update the preview shown in the textbox.
        // Click handlers go straight to Confirm — bypassing the keyboard's
        // Enter/Tab path but doing the same thing.
        var rows = matches
            .Select((cmd, i) =>
            {
                var rowText = FormatRow(cmd, i == state.SelectedIndex);
                return (Hex1bWidget)ctx.Interactable(ic => ic.Text(rowText))
                    .OnHoverChanged(args =>
                    {
                        if (args.IsHovered)
                        {
                            state.SelectedIndex = i;
                        }
                    })
                    .OnClick(_ => Confirm(state, matches));
            })
            .ToArray();

        return ctx.Border(b => rows).Title("Commands");
    }

    private static string FormatRow(SlashCommand cmd, bool selected)
    {
        var prefix = selected ? " ❯ " : "   ";
        return $"{prefix}/{cmd.Name}    {cmd.Description}";
    }

    private sealed class PromptState
    {
        // Hoisted TextBoxState — this composite owns the textbox's mutable
        // state (text, cursor, selection). Build mutates Text on every render
        // to either the typed prefix (palette closed) or a preview of the
        // highlighted match (palette open). The textbox is bound to this
        // exact instance via TextBox().State(state.TextBox), so no
        // OnTextChanged shadow-sync or "controlled-mode" flag is needed to
        // keep the displayed text consistent with our intent.
        public TextBoxState TextBox { get; } = new();

        // The literal characters the user has typed. This is the source of
        // truth for filter matching and for what gets submitted on Enter
        // when there are no matches. While the palette is open the textbox
        // may be displaying a /<command> PREVIEW that does not match this
        // value — that's the preview/confirm separation: previews are not
        // committed until the user actively confirms (Enter / Tab / click).
        public string TypedPrefix = "";

        // Highlighted row in the palette. Updated by Up/Down keys AND by
        // mouse hover on a palette row. Clamped after match recompute.
        public int SelectedIndex;
    }
}

public sealed record SlashCommand(string Name, string Description);

public static class SlashCommandPromptExtensions
{
    public static SlashCommandPromptWidget SlashCommandPrompt<T>(
        this WidgetContext<T> context,
        IReadOnlyList<SlashCommand> commands)
        where T : Hex1bWidget
        => new(commands);
}
