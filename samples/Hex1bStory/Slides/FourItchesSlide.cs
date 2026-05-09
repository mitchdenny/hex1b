using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// "The four itches" — the multiple problems Hex1b was born to solve all at
/// once. Presenter arrows through four motivations; selection drives the
/// detail panel on the right so we can dwell on whichever resonates.
/// </summary>
internal sealed class FourItchesSlide : ISlide
{
    public string Title => "The four itches";

    private static readonly (string Title, string[] Body)[] Items =
    [
        ("Aspire CLI E2E testing", new[]
        {
            "We needed a real end-to-end harness for the Aspire CLI.",
            "That meant scripting a terminal — driving it, snapshotting it,",
            "asserting against the rendered output. Existing pieces were",
            "either too low-level (raw VT bytes) or too coupled to a",
            "specific framework. We needed our own emulator.",
        }),
        ("Hard TUI experiences", new[]
        {
            "There were a handful of TUI experiences inside Aspire we kept",
            "putting off because the existing options forced too many",
            "trade-offs. We wanted the kind of declarative, react-style",
            "model that makes complex UIs tractable — without having to",
            "fight the terminal underneath.",
        }),
        ("7DRL 2026", new[]
        {
            "A long-standing personal itch: the 7-Day Roguelike challenge",
            "happens in early 2026, and a TUI framework with strong input,",
            "rendering and a sane widget model would be a *significant*",
            "leg up. (No, this isn't the main reason. But it sharpened",
            "the requirements.)",
        }),
        ("Aspire's interactive console story", new[]
        {
            "Aspire has always had a great web dashboard but a thin",
            "console story. With more developers leaning into the",
            "terminal again, we wanted a first-class interactive",
            "console experience — and the building blocks to make",
            "richer ones.",
        }),
    ];

    private int _selectedIndex;

    public Hex1bWidget Build(SlideContext context)
    {
        var ctx = context.Root;
        var (heading, body) = Items[_selectedIndex];

        return ctx.VStack(v =>
        [
            v.Text("The four itches"),
            v.Text("════════════════"),
            v.Text(""),
            v.Text("Why Hex1b started — December 2025."),
            v.Text(""),
            v.HStack(row =>
            [
                row.VStack(left => BuildItemList(left)).FixedWidth(34),
                row.Text("  "),
                row.VStack(right => BuildBody(right, heading, body)).Fill(),
            ]).Fill(),
            v.Text(""),
            v.Text("↑↓ to switch motivation"),
        ]).InputBindings(b =>
        {
            b.Key(Hex1bKey.UpArrow).Global().Action(c =>
            {
                if (_selectedIndex > 0) _selectedIndex--;
                c.Invalidate();
            }, "Previous itch");
            b.Key(Hex1bKey.DownArrow).Global().Action(c =>
            {
                if (_selectedIndex < Items.Length - 1) _selectedIndex++;
                c.Invalidate();
            }, "Next itch");
        });
    }

    private Hex1bWidget[] BuildItemList(WidgetContext<VStackWidget> ctx)
    {
        var rows = new Hex1bWidget[Items.Length];
        for (var i = 0; i < Items.Length; i++)
        {
            var prefix = i == _selectedIndex ? "▸ " : "  ";
            rows[i] = ctx.Text(prefix + Items[i].Title);
        }
        return rows;
    }

    private static Hex1bWidget[] BuildBody(WidgetContext<VStackWidget> ctx, string heading, string[] body)
    {
        var rows = new Hex1bWidget[body.Length + 2];
        rows[0] = ctx.Text(heading);
        rows[1] = ctx.Text(new string('─', heading.Length));
        for (var i = 0; i < body.Length; i++)
        {
            rows[i + 2] = ctx.Text(body[i]);
        }
        return rows;
    }
}
