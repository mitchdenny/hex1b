using Hex1b;

namespace CloudTermDemo;

/// <summary>
/// A terminal panel opened by a command (e.g., attach, connect).
/// </summary>
public sealed class TerminalPanel : IAsyncDisposable
{
    public string Title { get; }
    public TerminalWidgetHandle Handle { get; }
    public Hex1bTerminal Terminal { get; }
    private readonly Task _runTask;

    public TerminalPanel(string title, TerminalWidgetHandle handle, Hex1bTerminal terminal, Task runTask)
    {
        Title = title;
        Handle = handle;
        Terminal = terminal;
        _runTask = runTask;
    }

    public async ValueTask DisposeAsync()
    {
        Terminal.Dispose();
        try { await _runTask; }
        catch (OperationCanceledException) { }
    }
}

/// <summary>
/// Manages dynamic terminal panels spawned by commands like attach/connect.
/// ShellScreen reads from this to render the panel layout.
/// </summary>
public sealed class PanelManager
{
    private readonly List<TerminalPanel> _panels = [];
    private Hex1bApp? _app;

    public IReadOnlyList<TerminalPanel> Panels => _panels;

    public void SetApp(Hex1bApp app) => _app = app;

    /// <summary>
    /// Opens a new terminal panel running a mock process.
    /// On Windows uses powershell, on Linux/Mac uses bash.
    /// </summary>
    public void OpenTerminalPanel(string title)
    {
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)
            .WithScrollback()
            .WithPtyProcess(options =>
            {
                options.FileName = "docker";
                options.Arguments = ["run", "-it", "--rm", "ubuntu", "/bin/bash"];
            })
            .WithTerminalWidget(out var handle)
            .Build();

        var runTask = Task.Run(async () =>
        {
            try { await terminal.RunAsync(); }
            catch (OperationCanceledException) { }
        });

        _panels.Add(new TerminalPanel(title, handle, terminal, runTask));
        _app?.Invalidate();
    }

    /// <summary>
    /// Closes and removes a panel by index.
    /// </summary>
    public async Task ClosePanelAsync(int index)
    {
        if (index >= 0 && index < _panels.Count)
        {
            var panel = _panels[index];
            _panels.RemoveAt(index);
            await panel.DisposeAsync();
            _app?.Invalidate();
        }
    }
}
