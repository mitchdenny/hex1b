using Hex1b;
using Hex1b.Composition;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Widgets;

// SlashCommandPromptWidget — a composite that turns a plain TextBox into a
// slash-command prompt with a floating completion list.
//
// Behaviour:
//   - When the text starts with "/" and the rest of the buffer (up to the
//     first space) prefix-matches a registered command, a floating list of
//     matches appears above the textbox.
//   - Up/Down arrows navigate the list (input is captured by overriding the
//     textbox's bindings while the list is visible).
//   - Tab or Enter accepts the highlighted command, replacing the buffer
//     with "/<commandName> " and placing the cursor after the space.
//   - Escape dismisses the palette by clearing the current input.
//   - When the palette is closed, Enter submits as normal and fires OnSubmit.
//
// All state lives in the composite's CompositionContext via UseState — there
// is no custom Hex1bNode anywhere in this file.

namespace AgenticPromptDemo;

public sealed record SlashCommandPromptWidget(IReadOnlyList<SlashCommand> Commands)
    : Hex1bCompositeWidget
{
    internal Func<string, Task>? SubmitHandler { get; init; }

    public SlashCommandPromptWidget OnSubmit(Action<string> handler)
        => this with { SubmitHandler = t => { handler(t); return Task.CompletedTask; } };

    public SlashCommandPromptWidget OnSubmit(Func<string, Task> handler)
        => this with { SubmitHandler = handler };

    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var state = ctx.UseState(() => new PromptState());

        // Compute palette visibility / matches from the current buffer.
        var matches = ComputeMatches(state.CurrentText, Commands);
        var paletteVisible = matches.Count > 0;

        if (paletteVisible)
        {
            state.SelectedIndex = Math.Clamp(state.SelectedIndex, 0, matches.Count - 1);
        }
        else
        {
            state.SelectedIndex = 0;
        }

        // Pop and consume any pending text override produced by Accept / Escape.
        // We pass `null` to the TextBox in steady state so it owns its own
        // text + cursor; we only push text in when WE want to overwrite it.
        var pendingOverride = state.PendingTextOverride;
        state.PendingTextOverride = null;

        return ctx.VStack(v =>
        {
            // Single-line textbox driven mostly by the user; we mirror its text
            // into state on every change so palette logic can react.
            var textbox = (pendingOverride is not null
                    ? v.TextBox(pendingOverride)
                    : v.TextBox())
                .OnTextChanged(e => state.CurrentText = e.NewText)
                .OnSubmit(e =>
                {
                    var text = e.Text?.Trim();
                    if (string.IsNullOrEmpty(text))
                    {
                        return;
                    }

                    SubmitHandler?.Invoke(text);
                    state.CurrentText = "";
                    state.PendingTextOverride = "";
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

                    void Accept(InputBindingActionContext _)
                    {
                        var completion = "/" + matchesSnapshot[state.SelectedIndex].Name + " ";
                        state.CurrentText = completion;
                        state.PendingTextOverride = completion;
                    }

                    b.Key(Hex1bKey.Tab).Action(Accept, "Accept command");
                    b.Key(Hex1bKey.Enter).Action(Accept, "Accept command");

                    b.Key(Hex1bKey.Escape).Action(_ =>
                    {
                        state.CurrentText = "";
                        state.PendingTextOverride = "";
                    }, "Dismiss palette");
                });

                return [
                    v.Float(BuildPalette(v, matchesSnapshot, state.SelectedIndex))
                        .ExtendTop(textbox),
                    textbox,
                ];
            }

            return [textbox];
        });
    }

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
        int selectedIndex)
    {
        return ctx.Border(b => matches
            .Select((cmd, i) => (Hex1bWidget)b.Text(FormatRow(cmd, i == selectedIndex)))
            .ToArray()).Title("Commands");
    }

    private static string FormatRow(SlashCommand cmd, bool selected)
    {
        var prefix = selected ? " ❯ " : "   ";
        return $"{prefix}/{cmd.Name}    {cmd.Description}";
    }

    private sealed class PromptState
    {
        // Mirror of the textbox text (kept in sync via OnTextChanged).
        public string CurrentText = "";

        // When non-null, the next Build pushes this string into the textbox,
        // overwriting whatever it currently holds. Consumed each frame.
        public string? PendingTextOverride;

        // Highlighted row in the palette. Clamped after match recompute.
        public int SelectedIndex;
    }
}

public sealed record SlashCommand(string Name, string Description);

public static class SlashCommandPromptExtensions
{
    public static SlashCommandPromptWidget SlashCommandPrompt<T>(
        this WidgetContext<T> ctx,
        IReadOnlyList<SlashCommand> commands)
        where T : Hex1bWidget
        => new(commands);
}
