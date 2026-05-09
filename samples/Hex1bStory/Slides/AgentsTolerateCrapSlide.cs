using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// "Agents tolerate APIs you'd never tolerate yourself." Side-by-side
/// before/after panels for a few real (or representative) Hex1b ergonomic
/// improvements. Presenter switches case studies with 1/2/3.
/// </summary>
internal sealed class AgentsTolerateCrapSlide : ISlide
{
    public string Title => "Agents tolerate crap";

    private static readonly CaseStudy[] Cases =
    [
        new CaseStudy(
            Name: "Ambiguous callbacks",
            Before: new[]
            {
                "// Two overloads with the same arity but different",
                "// callback shapes. The agent happily writes:",
                "",
                "btn.OnClick((Action<ClickEventArgs>)(args =>",
                "{",
                "    HandleClick(args);",
                "}));",
                "",
                "// ↑ that cast is the agent being polite about a smell",
                "// in our API, not a smell in the agent.",
            },
            After: new[]
            {
                "// One overload per shape, no ambiguity.",
                "// The agent (and humans) just write:",
                "",
                "btn.OnClick(args =>",
                "{",
                "    HandleClick(args);",
                "});",
                "",
                "",
                "// We split the sync/async overloads so type",
                "// inference Just Works at the call site.",
            }),

        new CaseStudy(
            Name: "Verb-noun method names",
            Before: new[]
            {
                "// Once upon a time we had:",
                "",
                "widget.WithTitle(\"Save\");",
                "widget.WithMaxFloating(3);",
                "widget.WithDisabled(true);",
                "",
                "// `With*` reads like a builder. But widgets aren't",
                "// builders — they're immutable records. The prefix",
                "// just adds noise the agent dutifully kept typing.",
            },
            After: new[]
            {
                "// HEX1B0001 analyzer enforces it now:",
                "",
                "widget.Title(\"Save\");",
                "widget.MaxFloating(3);",
                "widget.Disabled();",
                "",
                "",
                "// `With*` is reserved for the terminal builder.",
                "// One naming rule, one analyzer, gone forever.",
            }),

        new CaseStudy(
            Name: "Reading your own samples",
            Before: new[]
            {
                "Tests pass.",
                "API docs render.",
                "Coverage is green.",
                "",
                "And the sample looks like:",
                "    new TextBoxNode(...)",
                "      .WithDelegate<Action<KeyEventArgs>>(...)",
                "      .Configure(b => b.WithCallback(...));",
                "",
                "The agent is fine with this. You won't be.",
            },
            After: new[]
            {
                "Read your samples like an end user.",
                "If it makes YOU wince, fix the API.",
                "",
                "    ctx.TextBox().OnEnter(text => …);",
                "",
                "",
                "Samples are the only honest review of",
                "ergonomics you'll ever get. Use them.",
                "",
                "(This slide deck is itself a sample.)",
            }),
    ];

    private int _index;

    public Hex1bWidget Build(SlideContext context)
    {
        var ctx = context.Root;
        var current = Cases[_index];

        return ctx.VStack(v =>
        [
            v.Text("Agents won't tell you your API sucks."),
            v.Text("Your samples will."),
            v.Text("══════════════════════════════════════════"),
            v.Text(""),
            v.Text($"Case {_index + 1} / {Cases.Length}: {current.Name}"),
            v.Text(""),
            v.HStack(row =>
            [
                row.Border(row.VStack(p => BuildLines(p, current.Before)))
                    .Title(" before ").Fill(),
                row.Text(" "),
                row.Border(row.VStack(p => BuildLines(p, current.After)))
                    .Title(" after ").Fill(),
            ]).Fill(),
            v.Text(""),
            v.Text("press 1 / 2 / 3 to switch case studies"),
        ]).InputBindings(b =>
        {
            b.Key(Hex1bKey.D1).Global().Action(c => Pick(0, c), "Case 1");
            b.Key(Hex1bKey.D2).Global().Action(c => Pick(1, c), "Case 2");
            b.Key(Hex1bKey.D3).Global().Action(c => Pick(2, c), "Case 3");
        });
    }

    private void Pick(int idx, InputBindingActionContext c)
    {
        if (idx >= 0 && idx < Cases.Length)
        {
            _index = idx;
            c.Invalidate();
        }
    }

    private static Hex1bWidget[] BuildLines(WidgetContext<VStackWidget> ctx, string[] lines)
    {
        var rows = new Hex1bWidget[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            rows[i] = ctx.Text(lines[i]);
        }
        return rows;
    }

    private sealed record CaseStudy(string Name, string[] Before, string[] After);
}
