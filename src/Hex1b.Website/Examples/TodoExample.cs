using Hex1b;
using Hex1b.Events;
using Hex1b.Terminal;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// A concise todo list example demonstrating lists, text input, and state.
/// This is the second example on the homepage showing layouts.
/// </summary>
public class TodoExample(ILogger<TodoExample> logger) : ReactiveExample
{
    private readonly ILogger<TodoExample> _logger = logger;

    public override string Id => "todo";
    public override string Title => "Todo List";
    public override string Description => "A simple todo list showing lists, input, and state management.";

    public override async Task RunAsync(IHex1bAppTerminalWorkloadAdapter workloadAdapter, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting todo example");

        // --- BEGIN: Code shown on homepage ---
        var items = new List<(string Text, bool Done)>
        {
            ("Learn Hex1b", true),
            ("Build a TUI", false)
        };
        var newItem = "";

        IReadOnlyList<string> Format() =>
            items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

        using var app = new Hex1bApp(
            ctx => ctx.Border(b => [
                b.HStack(h => [
                    h.Text("New task: "),
                    h.TextBox(newItem).OnTextChanged(e => newItem = e.NewText),
                    h.Button("Add").OnClick(_ => {
                        if (!string.IsNullOrWhiteSpace(newItem)) {
                            items.Add((newItem, false));
                            newItem = "";
                        }
                    })
                ]),
                new SeparatorWidget(),
                b.List(Format()).OnItemActivated(e =>
                    items[e.ActivatedIndex] = (items[e.ActivatedIndex].Text, !items[e.ActivatedIndex].Done))
            ], title: "📋 Todo"),
            new Hex1bAppOptions { WorkloadAdapter = workloadAdapter }
        );

        await app.RunAsync(cancellationToken);
        // --- END: Code shown on homepage ---
    }
}
