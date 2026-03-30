using Hex1b;
using Hex1b.Flow;
using Hex1b.Widgets;

namespace FlowWidgetCompatDemo.Scenarios;

internal class ListScenario : IWidgetScenario
{
    private static readonly IReadOnlyList<string> s_languages =
    [
        "C#", "Python", "JavaScript", "TypeScript", "Rust",
        "Go", "Java", "Kotlin", "Swift", "Ruby",
        "C++", "C", "Haskell", "Elixir", "Scala",
        "Clojure", "F#", "Dart", "Lua", "Zig",
        "OCaml", "Erlang", "Julia", "R", "Perl",
    ];

    private string _activatedItem = "(none)";

    public string Name => "List";
    public string Description => "List widget with scrolling and item activation";
    public int? MaxHeight => 15;

    public Hex1bWidget Build(FlowStepContext ctx) =>
        ctx.VStack(v =>
        [
            v.Text($"Activated: {_activatedItem}"),
            v.List(s_languages)
                .OnItemActivated(e => _activatedItem = e.ActivatedText)
                .FillHeight(),
        ]);
}
