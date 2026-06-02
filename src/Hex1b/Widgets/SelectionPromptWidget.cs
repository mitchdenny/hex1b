using Hex1b.Composition;
using Hex1b.Input;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A Spectre-style filtered selection prompt: a focused textbox that filters
/// a list of items as the user types, with inline prediction (Right Arrow
/// autocomplete) of the top match and per-row templates that highlight the
/// substring matched by the current filter.
/// </summary>
/// <remarks>
/// <para>
/// Unlike a classic Spectre <c>SelectionPrompt</c>, this widget <i>hides</i>
/// items that don't match the filter rather than just scrolling to them.
/// </para>
/// <para>
/// Focus stays on the textbox; <see cref="Hex1bKey.UpArrow"/> /
/// <see cref="Hex1bKey.DownArrow"/> are forwarded to the list selection,
/// <see cref="Hex1bKey.Enter"/> activates the selected item. Mouse clicks on
/// the list update the selection too.
/// </para>
/// <para>
/// Item matching is case-insensitive substring matching against the string
/// returned by <see cref="SelectionPromptExtensions.ItemText{T}"/> (or
/// <c>item?.ToString()</c> by default).
/// </para>
/// </remarks>
/// <typeparam name="T">The item type.</typeparam>
public sealed record SelectionPromptWidget<T>(IReadOnlyList<T> Items) : Hex1bWidget
{
    internal Func<T, string>? ItemTextSelector { get; init; }
    internal Func<T, Task>? SelectedHandler { get; init; }
    internal int MaxVisibleItemsValue { get; init; } = 8;
    internal string PromptText { get; init; } = "Filter:";
    internal string EmptyMessage { get; init; } = "No matches.";

    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var state = ctx.UseState(() => new SelectionPromptState());
        var selector = ItemTextSelector ?? (i => i?.ToString() ?? string.Empty);

        var filter = state.Filter;
        var filtered = new List<T>(Items.Count);
        if (filter.Length == 0)
        {
            filtered.AddRange(Items);
        }
        else
        {
            foreach (var item in Items)
            {
                if (selector(item).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filtered.Add(item);
                }
            }
        }

        if (filtered.Count == 0)
        {
            state.SelectedIndex = 0;
        }
        else if (state.SelectedIndex >= filtered.Count)
        {
            state.SelectedIndex = filtered.Count - 1;
        }
        else if (state.SelectedIndex < 0)
        {
            state.SelectedIndex = 0;
        }

        var selectedHandler = SelectedHandler;
        var listHeight = Math.Max(1, Math.Min(MaxVisibleItemsValue, Math.Max(1, filtered.Count)));

        return ctx.VStack(v =>
        [
            v.Text(PromptText).FixedHeight(1),

            v.TextBox(state.Filter)
                .Predict((text, _) => Task.FromResult<string?>(PredictFirstMatch(text, Items, selector)))
                .OnTextChanged(e =>
                {
                    if (state.Filter != e.NewText)
                    {
                        state.Filter = e.NewText;
                        state.SelectedIndex = 0;
                    }
                })
                .InputBindings(b =>
                {
                    b.Key(Hex1bKey.UpArrow).Action(_ =>
                    {
                        if (filtered.Count == 0) return;
                        state.SelectedIndex = (state.SelectedIndex - 1 + filtered.Count) % filtered.Count;
                    }, "Previous match");

                    b.Key(Hex1bKey.DownArrow).Action(_ =>
                    {
                        if (filtered.Count == 0) return;
                        state.SelectedIndex = (state.SelectedIndex + 1) % filtered.Count;
                    }, "Next match");

                    b.Key(Hex1bKey.Enter).Action(async _ =>
                    {
                        if (filtered.Count == 0 || selectedHandler is null) return;
                        var item = filtered[state.SelectedIndex];
                        await selectedHandler(item).ConfigureAwait(false);
                    }, "Activate match");
                })
                .FillWidth()
                .FixedHeight(1),

            filtered.Count == 0
                ? v.Text("  " + EmptyMessage)
                : v.TypedList(filtered)
                    .SelectedIndex(state.SelectedIndex)
                    .ItemKey(i => selector(i))
                    .OnSelectionChanged(e =>
                    {
                        if (state.SelectedIndex != e.SelectedIndex)
                        {
                            state.SelectedIndex = e.SelectedIndex;
                        }
                    })
                    .ItemTemplate(rowContext => RenderRow(rowContext, filter, selector))
                    .FixedHeight(listHeight),
        ]);
    }

    private static Hex1bWidget RenderRow(
        ListItemContext<T> rowContext,
        string filter,
        Func<T, string> selector)
    {
        var text = selector(rowContext.Item);
        var marker = rowContext.IsSelected ? "▶ " : "  ";

        // No active filter -> single text span, no highlighting needed.
        if (filter.Length == 0)
        {
            return rowContext.Text(marker + text);
        }

        var matchStart = text.IndexOf(filter, StringComparison.OrdinalIgnoreCase);
        if (matchStart < 0)
        {
            return rowContext.Text(marker + text);
        }

        var matchEnd = matchStart + filter.Length;
        var before = text[..matchStart];
        var match = text[matchStart..matchEnd];
        var after = text[matchEnd..];

        return rowContext.HStack(h =>
        [
            h.Text(marker + before),
            h.ThemePanel(
                theme => theme
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow)
                    .Set(GlobalTheme.BackgroundColor, Hex1bColor.Cyan),
                h.Text(match)),
            h.Text(after),
        ]);
    }

    private static string? PredictFirstMatch(string text, IReadOnlyList<T> items, Func<T, string> selector)
    {
        if (string.IsNullOrEmpty(text)) return null;
        foreach (var item in items)
        {
            var rendered = selector(item);
            if (rendered.Length > text.Length
                && rendered.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            {
                // Return only the suffix the user hasn't typed yet — the
                // textbox appends this verbatim when Right Arrow is pressed.
                return rendered[text.Length..];
            }
        }
        return null;
    }
}

/// <summary>
/// Per-instance state for <see cref="SelectionPromptWidget{T}"/>: the current
/// filter text and the highlighted-row index within the filtered view.
/// </summary>
internal sealed class SelectionPromptState
{
    public string Filter = string.Empty;
    public int SelectedIndex = 0;
}
