using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// "Conformance via agents." Walks through the four conformance suites Hex1b
/// borrowed from real terminal emulators. Selecting a suite shows what was
/// hairy about it and one or two specific quirks Hex1b emulates.
/// </summary>
internal sealed class ConformanceViaAgentsSlide : ISlide
{
    public string Title => "Conformance via agents";

    private static readonly (string Family, string[] Notes)[] Items =
    [
        ("xterm", new[]
        {
            "The grandparent. Decades of behaviour codified in tests",
            "ranging from \"obviously correct\" to \"why on earth\".",
            "",
            "Notable quirks Hex1b emulates:",
            "  · DECSET 1049 alt-screen save/restore semantics",
            "  · Tab-stop reset on column 1, not column 0",
            "  · Soft-wrap pending state across resizes",
            "",
            "We asked the agent to read xterm's ctlseqs.txt and",
            "encode the cases as test fixtures. It did.",
        }),
        ("vte", new[]
        {
            "GNOME Terminal's heart. Pragmatic, modern, opinionated.",
            "",
            "Notable quirks Hex1b emulates:",
            "  · Bracketed paste with vte's specific timing",
            "  · OSC 11 background-colour query semantics",
            "  · Mouse-tracking modes 1006 + 1015 dual-decode",
            "",
            "vte's tests are extensive. We borrowed wholesale,",
            "translated to xUnit, and watched red turn green.",
        }),
        ("kitty", new[]
        {
            "The fast new kid. Strong opinions, novel protocols.",
            "",
            "Notable quirks Hex1b emulates:",
            "  · The kitty keyboard protocol (CSI u, modes 1-7)",
            "  · Image protocol: at least the placement basics",
            "  · Synchronised output via DCS = h / DCS = l",
            "",
            "Apps targeting kitty (helix, neovim) Just Work in",
            "Hex1b without realising they're not on kitty.",
        }),
        ("ghostty", new[]
        {
            "The newest, fastest, most opinionated of all.",
            "",
            "Notable quirks Hex1b emulates:",
            "  · CSI 22 / 23 t window-title save/restore",
            "  · Ghostty's flavour of synchronised output",
            "  · A few inherited xterm quirks ghostty fixed,",
            "    that we then deliberately re-broke for compat",
            "",
            "And yes — the ghostty conformance test is itself",
            "the best ghostty bug-finder we've shipped.",
        }),
    ];

    private int _selectedIndex;

    public Hex1bWidget Build(SlideContext context)
    {
        var ctx = context.Root;
        var current = Items[_selectedIndex];

        return ctx.VStack(v =>
        [
            v.Text("Conformance via agents"),
            v.Text("══════════════════════"),
            v.Text(""),
            v.Text("\"Read this conformance suite. Make our terminal pass it.\""),
            v.Text(""),
            v.HStack(row =>
            [
                row.VStack(left => BuildItemList(left)).FixedWidth(20),
                row.Text("  "),
                row.VStack(right => BuildBody(right, current.Family, current.Notes)).Fill(),
            ]).Fill(),
            v.Text(""),
            v.Text("↑↓ to switch suite  ·  Hex1b can pretend to be any of these"),
        ]).InputBindings(b =>
        {
            b.Key(Hex1bKey.UpArrow).Global().Action(c =>
            {
                if (_selectedIndex > 0) _selectedIndex--;
                c.Invalidate();
            }, "Previous suite");
            b.Key(Hex1bKey.DownArrow).Global().Action(c =>
            {
                if (_selectedIndex < Items.Length - 1) _selectedIndex++;
                c.Invalidate();
            }, "Next suite");
        });
    }

    private Hex1bWidget[] BuildItemList(WidgetContext<VStackWidget> ctx)
    {
        var rows = new Hex1bWidget[Items.Length];
        for (var i = 0; i < Items.Length; i++)
        {
            var prefix = i == _selectedIndex ? "▸ " : "  ";
            rows[i] = ctx.Text(prefix + Items[i].Family);
        }
        return rows;
    }

    private static Hex1bWidget[] BuildBody(WidgetContext<VStackWidget> ctx, string family, string[] notes)
    {
        var rows = new Hex1bWidget[notes.Length + 2];
        rows[0] = ctx.Text(family);
        rows[1] = ctx.Text(new string('─', family.Length));
        for (var i = 0; i < notes.Length; i++)
        {
            rows[i + 2] = ctx.Text(notes[i]);
        }
        return rows;
    }
}
