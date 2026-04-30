using Hex1b;
using Hex1b.Flow;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace CloudTermDemo;

/// <summary>
/// Creates and manages the embedded cloud terminal with a flow-based shell
/// that navigates the cloud resource hierarchy using System.CommandLine.
/// </summary>
public sealed class CloudTerminalHost : IAsyncDisposable
{
    private readonly CloudShellState _shellState;
    private readonly TutorialService _tutorial;
    private readonly NodeCommandRegistry _commandRegistry;
    private readonly PanelManager _panelManager;
    private Hex1bTerminal? _terminal;
    private Task? _runTask;

    public TerminalWidgetHandle? Handle { get; private set; }

    public CloudTerminalHost(
        CloudShellState shellState,
        TutorialService tutorial,
        NodeCommandRegistry commandRegistry,
        PanelManager panelManager)
    {
        _shellState = shellState;
        _tutorial = tutorial;
        _commandRegistry = commandRegistry;
        _panelManager = panelManager;
    }

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
        await flow.ShowAsync(ctx => ctx.Text("Type 'ls' to list resources, 'cd <name>' to navigate, 'help' for more."));
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

            var execContext = new CommandExecutionContext
            {
                ShellState = _shellState,
                PanelManager = _panelManager,
                Tutorial = _tutorial,
            };

            var outputLines = await _commandRegistry.ExecuteAsync(trimmed, execContext);

            foreach (var line in outputLines)
                await flow.ShowAsync(ctx => ctx.Text(line));

            await flow.ShowAsync(ctx => ctx.Text(""));
        }
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
