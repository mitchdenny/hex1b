using Hex1b;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace CloudTermDemo;

/// <summary>
/// A typed command result that knows how to render itself as a widget.
/// </summary>
public abstract class CommandResult
{
    public abstract Hex1bWidget Render<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget;
}

/// <summary>Plain text output lines.</summary>
public sealed class TextResult : CommandResult
{
    public List<string> Lines { get; } = [];

    public override Hex1bWidget Render<TParent>(WidgetContext<TParent> ctx)
    {
        return ctx.VStack(v => Lines.Select(l => (Hex1bWidget)v.Text(l)).ToArray());
    }
}

/// <summary>A table of resources (name, type, description).</summary>
public sealed class ResourceListResult : CommandResult
{
    public record ResourceRow(string Type, string Name, string? Description);
    public List<ResourceRow> Rows { get; } = [];

    public override Hex1bWidget Render<TParent>(WidgetContext<TParent> ctx)
    {
        return ctx.Table((IReadOnlyList<ResourceRow>)Rows)
            .Header(h =>
            [
                h.Cell("Type").Width(SizeHint.Fixed(18)),
                h.Cell("Name").Width(SizeHint.Fill),
                h.Cell("Details").Width(SizeHint.Fill),
            ])
            .Row((r, row, state) =>
            [
                r.Cell(row.Type),
                r.Cell(row.Name),
                r.Cell(row.Description ?? ""),
            ])
            .Compact()
            .ContentHeight();
    }
}

/// <summary>A key-value detail card for a single resource.</summary>
public sealed class DetailResult : CommandResult
{
    public List<(string Key, string Value)> Fields { get; } = [];

    public override Hex1bWidget Render<TParent>(WidgetContext<TParent> ctx)
    {
        return ctx.VStack(v =>
            Fields.Select(f => (Hex1bWidget)v.Text($"  {f.Key,-12} {f.Value}")).ToArray()
        );
    }
}

/// <summary>A navigation result (shows the new path).</summary>
public sealed class NavigationResult : CommandResult
{
    public string Path { get; init; } = "";
    public string? Error { get; init; }
    public List<string>? Suggestions { get; init; }

    public override Hex1bWidget Render<TParent>(WidgetContext<TParent> ctx)
    {
        if (Error != null)
        {
            var widgets = new List<Hex1bWidget> { ctx.Text($"  {Error}") };
            if (Suggestions is { Count: > 0 })
                widgets.Add(ctx.Text($"  Did you mean: {string.Join(", ", Suggestions)}?"));
            return ctx.VStack(v => widgets.ToArray());
        }
        return ctx.Text($"  → {Path}");
    }
}

/// <summary>An action result (something happened — panel opened, restart triggered, etc.).</summary>
public sealed class ActionResult : CommandResult
{
    public List<string> Messages { get; } = [];

    public override Hex1bWidget Render<TParent>(WidgetContext<TParent> ctx)
    {
        return ctx.VStack(v => Messages.Select(m => (Hex1bWidget)v.Text(m)).ToArray());
    }
}

/// <summary>
/// A single entry in the cloud shell history — the prompt line and typed result.
/// </summary>
public sealed class ShellHistoryEntry
{
    public string? PromptLine { get; }
    public CommandResult Result { get; }

    public ShellHistoryEntry(string? promptLine, CommandResult result)
    {
        PromptLine = promptLine;
        Result = result;
    }
}

/// <summary>
/// Builds the cloud shell widget tree: a scrollable history of typed command results
/// with a prompt TextBox at the bottom.
/// </summary>
public sealed class CloudShellWidget
{
    private readonly CloudShellState _shellState;
    private readonly TutorialService _tutorial;
    private readonly NodeCommandRegistry _commandRegistry;
    private readonly PanelManager _panelManager;
    private readonly List<ShellHistoryEntry> _history = [];
    private string _currentInput = "";

    public CloudShellWidget(
        CloudShellState shellState,
        TutorialService tutorial,
        NodeCommandRegistry commandRegistry,
        PanelManager panelManager)
    {
        _shellState = shellState;
        _tutorial = tutorial;
        _commandRegistry = commandRegistry;
        _panelManager = panelManager;

        // Welcome message
        var welcome = new TextResult();
        welcome.Lines.AddRange([
            "☁ Cloud Term Shell",
            "Type 'ls' to list resources, 'cd <name>' to navigate, 'help' for more.",
            "",
        ]);
        _history.Add(new ShellHistoryEntry(null, welcome));
    }

    public Hex1bWidget Build<TParent>(WidgetContext<TParent> ctx, Hex1bApp app)
        where TParent : Hex1bWidget
    {
        var prompt = $"{_shellState.GetPrompt()} > ";

        return ctx.VStack(outer =>
        [
            // Scrollable history
            outer.VScrollPanel(sp =>
            {
                var widgets = new List<Hex1bWidget>();

                foreach (var entry in _history)
                {
                    if (entry.PromptLine != null)
                    {
                        widgets.Add(sp.Text(entry.PromptLine));
                    }

                    widgets.Add(entry.Result.Render(sp));
                    widgets.Add(sp.Text("")); // spacer between entries
                }

                return widgets.ToArray();
            }).Fill(),

            // Prompt line at bottom
            outer.HStack(h =>
            [
                h.Text(prompt),
                h.TextBox(_currentInput)
                    .OnTextChanged(e => _currentInput = e.NewText)
                    .OnSubmit(e =>
                    {
                        var input = e.Text.Trim();
                        _currentInput = "";

                        if (!string.IsNullOrEmpty(input))
                        {
                            var execContext = new CommandExecutionContext
                            {
                                ShellState = _shellState,
                                PanelManager = _panelManager,
                                Tutorial = _tutorial,
                            };

                            _commandRegistry.ExecuteAsync(input, execContext).GetAwaiter().GetResult();

                            _history.Add(new ShellHistoryEntry(
                                $"{prompt}{input}",
                                execContext.Result ?? new TextResult()
                            ));
                        }

                        app.Invalidate();
                    })
                    .FillWidth(),
            ]).Height(SizeHint.Content),
        ]);
    }
}
