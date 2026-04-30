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
                h.Cell("Name").Width(SizeHint.Fill),
                h.Cell("Type").Width(SizeHint.Fixed(18)),
                h.Cell("Details").Width(SizeHint.Fill),
            ])
            .Row((r, row, state) =>
            [
                r.Cell(row.Name),
                r.Cell(row.Type),
                r.Cell(row.Description ?? ""),
            ])
            .RowKey(r => r.Name)
            .Compact()
            .FillWidth()
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
        return ctx.Text($"  -> {Path}");
    }
}

/// <summary>An action result (something happened).</summary>
public sealed class ActionResult : CommandResult
{
    public List<string> Messages { get; } = [];

    public override Hex1bWidget Render<TParent>(WidgetContext<TParent> ctx)
    {
        return ctx.VStack(v => Messages.Select(m => (Hex1bWidget)v.Text(m)).ToArray());
    }
}

/// <summary>
/// A single entry in the cloud shell history.
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
/// Cloud shell widget: blue header breadcrumb, scrollable history of typed results,
/// and a styled prompt/spinner at the bottom. Commands execute asynchronously with
/// a spinner showing status updates.
/// </summary>
public sealed class CloudShellWidget
{
    private static readonly Hex1bColor HeaderBg = Hex1bColor.FromRgb(30, 80, 180);
    private static readonly Hex1bColor HeaderFg = Hex1bColor.White;
    private static readonly Hex1bColor PromptBg = Hex1bColor.FromRgb(35, 35, 45);

    private readonly CloudShellState _shellState;
    private readonly TutorialService _tutorial;
    private readonly NodeCommandRegistry _commandRegistry;
    private readonly PanelManager _panelManager;
    private readonly List<ShellHistoryEntry> _history = [];
    private string _currentInput = "";
    private bool _isExecuting;
    private string _spinnerMessage = "Running...";
    private Hex1bApp? _app;

    /// <summary>The most recent ResourceListResult, for data browsing panel.</summary>
    public ResourceListResult? LastResourceList { get; private set; }

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

        var welcome = new TextResult();
        welcome.Lines.AddRange([
            "Cloud Term Shell",
            "Type 'ls' to list resources, 'cd <name>' to navigate, 'help' for more.",
            "",
        ]);
        _history.Add(new ShellHistoryEntry(null, welcome));
    }

    public Hex1bWidget Build<TParent>(WidgetContext<TParent> ctx, Hex1bApp app)
        where TParent : Hex1bWidget
    {
        _app = app;
        var currentNode = _shellState.CurrentNode;
        var prompt = $" {currentNode.Name} > ";

        return ctx.VStack(outer =>
        [
            // Blue inverted header with context breadcrumb
            outer.ThemePanel(
                t => t
                    .Set(GlobalTheme.ForegroundColor, HeaderFg)
                    .Set(GlobalTheme.BackgroundColor, HeaderBg),
                outer.VStack(bc =>
                {
                    var chain = _shellState.GetAncestorChain();
                    var lines = new List<Hex1bWidget>();

                    if (chain.Count <= 4)
                    {
                        foreach (var node in chain)
                            lines.Add(bc.Text($" {Indent(chain, node)}{node.TypeLabel}: {node.Name}"));
                    }
                    else
                    {
                        lines.Add(bc.Text($" {chain[0].TypeLabel}: {chain[0].Name}"));
                        lines.Add(bc.Text($"   {chain[1].TypeLabel}: {chain[1].Name}"));
                        lines.Add(bc.Text($"     ..."));
                        lines.Add(bc.Text($"   {chain[^2].TypeLabel}: {chain[^2].Name}"));
                        lines.Add(bc.Text($"     {chain[^1].TypeLabel}: {chain[^1].Name}"));
                    }

                    return lines.ToArray();
                }).Height(SizeHint.Content)
            ),

            // Scrollable history
            (outer.VScrollPanel(sp =>
            {
                var widgets = new List<Hex1bWidget>();

                foreach (var entry in _history)
                {
                    if (entry.PromptLine != null)
                        widgets.Add(sp.Text(entry.PromptLine));

                    widgets.Add(entry.Result.Render(sp));
                    widgets.Add(sp.Text(""));
                }

                return widgets.ToArray();
            }) with { IsFollowing = true }).Fill(),

            // Bottom: prompt or spinner
            _isExecuting
                ? outer.HStack(h =>
                [
                    h.Spinner(SpinnerStyle.Dots),
                    h.Text($" {_spinnerMessage}"),
                ]).Height(SizeHint.Content)
                : outer.ThemePanel(
                    t => t
                        .Set(TextBoxTheme.UseFillMode, true)
                        .Set(TextBoxTheme.FillBackgroundColor, PromptBg)
                        .Set(TextBoxTheme.FocusedFillBackgroundColor, PromptBg)
                        .Set(TextBoxTheme.LeftBracket, "")
                        .Set(TextBoxTheme.RightBracket, ""),
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
                                    _ = ExecuteCommandAsync(input, prompt);

                                app.Invalidate();
                            })
                            .FillWidth(),
                    ]).Height(SizeHint.Content)
                ),
        ]);
    }

    private async Task ExecuteCommandAsync(string input, string prompt)
    {
        _isExecuting = true;
        _spinnerMessage = "Running...";
        _app?.Invalidate();

        var execContext = new CommandExecutionContext
        {
            ShellState = _shellState,
            PanelManager = _panelManager,
            Tutorial = _tutorial,
            OnStatusUpdate = msg =>
            {
                _spinnerMessage = msg;
                _app?.Invalidate();
            },
            OnIntermediateOutput = result =>
            {
                _history.Add(new ShellHistoryEntry(null, result));
                _app?.Invalidate();
            },
        };

        // Simulate network latency
        await Task.Delay(Random.Shared.Next(200, 800));

        await _commandRegistry.ExecuteAsync(input, execContext);

        var result = execContext.Result ?? new TextResult();
        if (result is ResourceListResult rlr)
            LastResourceList = rlr;

        _history.Add(new ShellHistoryEntry(
            $"{prompt}{input}",
            result
        ));

        _isExecuting = false;
        _app?.Invalidate();
        _app?.RequestFocus(n => n is TextBoxNode);
    }

    private static string Indent(List<CloudNode> chain, CloudNode node)
    {
        var depth = chain.IndexOf(node);
        return new string(' ', depth * 2);
    }
}
