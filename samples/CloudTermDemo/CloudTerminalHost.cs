using Hex1b;
using Hex1b.Flow;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace CloudTermDemo;

/// <summary>
/// Creates and manages the embedded cloud terminal with a flow-based shell
/// that navigates the cloud resource hierarchy.
/// </summary>
public sealed class CloudTerminalHost : IAsyncDisposable
{
    private readonly CloudShellState _shellState;
    private readonly TutorialService _tutorial;
    private Hex1bTerminal? _terminal;
    private Task? _runTask;

    public TerminalWidgetHandle? Handle { get; private set; }

    public CloudTerminalHost(CloudShellState shellState, TutorialService tutorial)
    {
        _shellState = shellState;
        _tutorial = tutorial;
    }

    /// <summary>
    /// Builds and starts the embedded cloud terminal.
    /// </summary>
    public void Start()
    {
        if (_terminal != null)
            return;

        _terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)
            .WithScrollback()
            .WithHex1bFlow(RunShellAsync)
            .WithTerminalWidget(out var handle)
            .Build();

        Handle = handle;
        _runTask = Task.Run(async () =>
        {
            try { await _terminal.RunAsync(); }
            catch (OperationCanceledException) { }
        });
    }

    private async Task RunShellAsync(Hex1bFlowContext flow)
    {
        await flow.ShowAsync(ctx => ctx.Text("☁ Cloud Term Shell"));
        await flow.ShowAsync(ctx => ctx.Text("Type 'ls' to list resources, 'cd <name>' to navigate."));
        await flow.ShowAsync(ctx => ctx.Text(""));

        while (!flow.CancellationToken.IsCancellationRequested)
        {
            var prompt = $"{_shellState.GetPrompt()} > ";
            var input = "";

            var step = flow.Step(ctx => ctx.VStack(v =>
            [
                v.HStack(h =>
                [
                    h.Text(prompt),
                    h.TextBox(input)
                        .OnTextChanged(e => input = e.NewText)
                        .OnSubmit(e =>
                        {
                            input = e.Text;
                            ctx.Step.Complete(y => y.Text($"{prompt}{input}"));
                        })
                        .FillWidth(),
                ]),
            ]), options: opts => opts.MaxHeight = 1);

            step.RequestFocus(n => n is TextBoxNode);
            await step.WaitForCompletionAsync(flow.CancellationToken);

            var trimmed = input.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            await ExecuteCommandAsync(flow, trimmed);
        }
    }

    private async Task ExecuteCommandAsync(Hex1bFlowContext flow, string command)
    {
        var parts = command.Split(' ', 2, StringSplitOptions.TrimEntries);
        var cmd = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : "";

        switch (cmd)
        {
            case "ls":
            case "dir":
                await ListChildrenAsync(flow);
                break;

            case "cd":
                await ChangeDirectoryAsync(flow, arg);
                break;

            case "pwd":
                await flow.ShowAsync(ctx => ctx.Text(_shellState.GetPath()));
                break;

            case "help":
            case "?":
                await ShowHelpAsync(flow);
                break;

            default:
                await flow.ShowAsync(ctx => ctx.Text($"Unknown command: {cmd}. Type 'help' for available commands."));
                break;
        }

        await flow.ShowAsync(ctx => ctx.Text(""));
    }

    private async Task ListChildrenAsync(Hex1bFlowContext flow)
    {
        var node = _shellState.CurrentNode;
        if (node.Children.Count == 0)
        {
            await flow.ShowAsync(ctx => ctx.Text("  (no child resources)"));
            if (_tutorial.CurrentStep == 0)
                _tutorial.Advance();
            return;
        }

        foreach (var child in node.Children)
        {
            var desc = child.Description != null ? $"  ({child.Description})" : "";
            var line = $"  {child.Type,-18} {child.Name}{desc}";
            await flow.ShowAsync(ctx => ctx.Text(line));
        }

        if (_tutorial.CurrentStep == 0)
            _tutorial.Advance();
    }

    private async Task ChangeDirectoryAsync(Hex1bFlowContext flow, string target)
    {
        if (string.IsNullOrEmpty(target) || target == "/")
        {
            _shellState.NavigateToRoot();
            await flow.ShowAsync(ctx => ctx.Text($"  → {_shellState.GetPath()}"));
            return;
        }

        if (target == "..")
        {
            if (!_shellState.NavigateUp())
                await flow.ShowAsync(ctx => ctx.Text("  Already at root."));
            else
                await flow.ShowAsync(ctx => ctx.Text($"  → {_shellState.GetPath()}"));
            return;
        }

        if (_shellState.NavigateTo(target))
        {
            await flow.ShowAsync(ctx => ctx.Text($"  → {_shellState.GetPath()}"));
            if (_tutorial.CurrentStep == 1)
                _tutorial.Advance();
        }
        else
        {
            await flow.ShowAsync(ctx => ctx.Text($"  Not found: {target}"));
            var suggestions = _shellState.CurrentNode.Children
                .Where(c => c.Name.Contains(target, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Name)
                .Take(3);
            if (suggestions.Any())
                await flow.ShowAsync(ctx => ctx.Text($"  Did you mean: {string.Join(", ", suggestions)}?"));
        }
    }

    private static async Task ShowHelpAsync(Hex1bFlowContext flow)
    {
        await flow.ShowAsync(ctx => ctx.Text("  Available commands:"));
        await flow.ShowAsync(ctx => ctx.Text("    ls          List resources at current level"));
        await flow.ShowAsync(ctx => ctx.Text("    cd <name>   Navigate into a resource"));
        await flow.ShowAsync(ctx => ctx.Text("    cd ..       Go up one level"));
        await flow.ShowAsync(ctx => ctx.Text("    cd /        Go to root"));
        await flow.ShowAsync(ctx => ctx.Text("    pwd         Show current path"));
        await flow.ShowAsync(ctx => ctx.Text("    help        Show this help"));
    }

    public async ValueTask DisposeAsync()
    {
        _terminal?.Dispose();
        if (_runTask != null)
        {
            try { await _runTask; }
            catch (OperationCanceledException) { }
        }
    }
}
