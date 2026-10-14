using Hex1b;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;
using System.Diagnostics;

namespace OSC7501Demo;

internal sealed class Osc7501DemoApp(
    IReadOnlyList<ShellOption> shells,
    int terminalWidth = 80,
    Func<ShellOption, Hex1bTerminalBuilder>? createTerminal = null) : IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private readonly Stopwatch _animation = Stopwatch.StartNew();
    private Hex1bApp? _app;
    private Hex1bTerminal? _terminal;
    private TerminalWidgetHandle? _handle;
    private Task? _runTask;
    private string? _selectedId;
    private string? _error;
    private string _shellName = "";

    internal Hex1bWidget Build(RootContext context, Hex1bApp app)
    {
        _app = app;
        return context.VStack(v =>
        [
            v.Text("OSC 7501 tester - live program-status inventory"),
            _terminal is null || _handle is null
                ? BuildShellPicker(v)
                : BuildConsole(v, _terminal.ProgramStatus, _handle),
            v.InfoBar([
                "F6", "Terminal / tree",
                "Enter", "Details",
                "F10", "Quit",
                "", Volatile.Read(ref _error) ?? "Click a record; drag the divider to resize"
            ])
        ]).InputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.F6).Global().Action(_ => ToggleFocus(), "Focus terminal or tree");
            bindings.Key(Hex1bKey.F10).Global().Action(_ => app.RequestStop(), "Quit");
        });
    }

    private Hex1bWidget BuildShellPicker(WidgetContext<VStackWidget> context)
        => context.Border(context.VStack(v =>
        [
            v.Text("Choose a detected shell to launch in a PTY.").Wrap(),
            shells.Count == 0
                ? v.Text("No supported shells found on PATH. Install bash, zsh, cmd.exe or pwsh, then restart.").Wrap()
                : v.SelectionPrompt(shells)
                    .ItemText(shell => $"{shell.Name} - {shell.FileName}")
                    .FilterText(shell => shell.Name)
                    .OnSelected(StartShell)
        ])).Title("Launch shell").Fill();

    private void StartShell(ShellOption shell)
    {
        if (_terminal is not null)
            return;
        try
        {
            var builder = createTerminal?.Invoke(shell) ?? Hex1bTerminal.CreateBuilder()
                .WithPtyProcess(options =>
                {
                    options.FileName = shell.FileName;
                    options.Arguments = shell.Arguments;
                    if (OperatingSystem.IsWindows() && shell.Name == "PowerShell")
                        options.WindowsPtyMode = WindowsPtyMode.RequireProxy;
                });
            _terminal = builder.WithDimensions(terminalWidth, 24)
                .WithScrollback(10000)
                .WithTerminalWidget(out var handle)
                .Build();
            _handle = handle;
            _shellName = shell.Name;
            _error = null;
            _terminal.ProgramStatusChanged += OnProgramStatusChanged;
            _runTask = RunShellAsync(_terminal);
            _app?.RequestFocus(node => node is TerminalNode);
        }
        catch (Exception error)
        {
            _error = $"Could not launch {shell.Name}: {StatusHierarchy.DisplayText(error.Message)}";
        }
        _app?.Invalidate();
    }

    private async Task RunShellAsync(Hex1bTerminal terminal)
    {
        try
        {
            await terminal.RunAsync(_shutdown.Token);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            Volatile.Write(ref _error, $"Shell failed: {StatusHierarchy.DisplayText(error.Message)}");
        }
        finally
        {
            _app?.Invalidate();
        }
    }

    private void OnProgramStatusChanged(TerminalProgramStatusSnapshot snapshot)
        => _app?.Invalidate();

    private void ToggleFocus()
    {
        var focusTree = _app?.FocusedNode is TerminalNode;
        _app?.RequestFocus(node => focusTree ? node is TreeNode : node is TerminalNode);
    }

    private Hex1bWidget BuildConsole(
        WidgetContext<VStackWidget> context,
        TerminalProgramStatusSnapshot snapshot,
        TerminalWidgetHandle handle)
    {
        var hierarchy = StatusHierarchy.Create(snapshot);
        var tree = context.Tree(t => [BuildTreeItem(t, hierarchy)]).Fill();
        if (snapshot.Records.Any(record => record.State == TerminalProgramStatusState.Working))
            tree = tree.RedrawAfter(SpinnerStyle.Dots.Interval);
        return context.HSplitter(
            context.Border(
                context.Terminal(handle)
                    .WhenNotRunning(args => context.Text(
                        $"Shell exited (code {args.ExitCode?.ToString() ?? "unknown"}). F10 to quit.").Wrap())
                    .Fill()).Title(_shellName),
            context.VStack(v =>
            [
                v.Border(snapshot.Records.Count == 0
                    ? v.Text("No OSC 7501 reports yet. Run a reporting program in the shell.").Wrap()
                    : tree)
                    .Title($"OSC 7501 ({snapshot.Records.Count})").FillHeight(),
                v.Border(
                    v.VScrollPanel(p =>
                        BuildDetails(snapshot).Select(line => p.Text(line).Wrap()).ToArray()))
                    .Title("Selected report").FixedHeight(12)
            ]),
            leftWidth: terminalWidth).Fill();
    }

    private TreeItemWidget BuildTreeItem(TreeContext context, StatusHierarchy node)
    {
        var name = node.Id.Length == 0 ? "(root)" : node.Id[(node.Id.LastIndexOf('/') + 1)..];
        var record = node.Record;
        var label = record is null
            ? $"{name} [no report]"
            : $"{name} [{record.State}] {StatusHierarchy.DisplayText(record.Title ?? record.Message ?? "")}";
        return context.Item(label, children =>
                node.Children.Values.Select(child => BuildTreeItem(children, child)))
            .Data(node.Id)
            .Icon(StatusIcon(record?.State,
                (int)(_animation.ElapsedMilliseconds / SpinnerStyle.Dots.Interval.TotalMilliseconds)))
            .Expanded(!_collapsed.Contains(node.Id))
            .OnCollapsed(_ => _collapsed.Add(node.Id))
            .OnExpanded(_ => _collapsed.Remove(node.Id))
            .OnClicked(_ => Select(node.Id))
            .OnActivated(_ => Select(node.Id));
    }

    internal static string StatusIcon(TerminalProgramStatusState? state, int frame)
        => state switch
        {
            TerminalProgramStatusState.Idle => "○",
            TerminalProgramStatusState.Working => SpinnerStyle.Dots.GetFrame(frame),
            TerminalProgramStatusState.Done => "✓",
            TerminalProgramStatusState.Blocked => "?",
            TerminalProgramStatusState.Error => "✗",
            _ => "+"
        };

    private void Select(string id)
    {
        _selectedId = id;
        _app?.Invalidate();
    }

    private IEnumerable<string> BuildDetails(TerminalProgramStatusSnapshot snapshot)
    {
        if (_selectedId is null)
            return ["Click a tree item or focus it and press Enter to inspect its complete report."];
        var id = _selectedId.Length == 0 ? "(root)" : _selectedId;
        var record = snapshot.Find(_selectedId);
        if (record is null)
            return [$"ID: {id}", "No current report for this ID (structural parent, cleared or expired)."];
        return
        [
            $"ID: {id}",
            $"State: {record.State}",
            $"Kind: {record.Kind?.ToString() ?? "(not reported)"}",
            $"Progress: {(record.Progress is { } progress ? $"{progress}%" : "(not reported)")}",
            $"App: {StatusHierarchy.DisplayText(record.App)}",
            $"Effective app: {StatusHierarchy.DisplayText(record.EffectiveApp)}",
            $"Title: {StatusHierarchy.DisplayText(record.Title)}",
            $"Message: {StatusHierarchy.DisplayText(record.Message)}"
        ];
    }

    public async ValueTask DisposeAsync()
    {
        _app = null;
        await _shutdown.CancelAsync();
        if (_terminal is not null)
        {
            _terminal.ProgramStatusChanged -= OnProgramStatusChanged;
            await _terminal.DisposeAsync();
        }
        if (_runTask is not null)
            await _runTask;
        _shutdown.Dispose();
    }
}
