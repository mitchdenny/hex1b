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
    public int Weight { get; set; } = 1;
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
/// Manages dynamic terminal panels and focus/resize state.
/// The cloud shell panel is always index 0; dynamic panels start at index 1.
/// </summary>
public sealed class PanelManager
{
    private readonly List<TerminalPanel> _panels = [];
    private Hex1bApp? _app;

    /// <summary>Index of the currently focused panel (0 = cloud shell).</summary>
    public int FocusedIndex { get; private set; }

    public IReadOnlyList<TerminalPanel> Panels => _panels;

    public int PanelCount => _panels.Count;

    public void SetApp(Hex1bApp app) => _app = app;

    /// <summary>
    /// Registers the cloud shell as panel 0.
    /// </summary>
    public void SetCloudShellPanel(TerminalWidgetHandle handle)
    {
        if (_panels.Count == 0)
            _panels.Add(new TerminalPanel("Cloud Shell", handle, null!, Task.CompletedTask));
    }

    /// <summary>
    /// Opens a new terminal panel running docker.
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
        FocusedIndex = _panels.Count - 1;
        _app?.Invalidate();
    }

    /// <summary>Shift focus to the next panel (wraps around).</summary>
    public void FocusNext()
    {
        if (_panels.Count > 0)
        {
            FocusedIndex = (FocusedIndex + 1) % _panels.Count;
            _app?.Invalidate();
        }
    }

    /// <summary>Shift focus to the previous panel (wraps around).</summary>
    public void FocusPrevious()
    {
        if (_panels.Count > 0)
        {
            FocusedIndex = (FocusedIndex - 1 + _panels.Count) % _panels.Count;
            _app?.Invalidate();
        }
    }

    /// <summary>Expand the focused panel's weight (grow).</summary>
    public void ExpandFocused()
    {
        if (FocusedIndex >= 0 && FocusedIndex < _panels.Count)
        {
            _panels[FocusedIndex].Weight = Math.Min(_panels[FocusedIndex].Weight + 1, 5);
            _app?.Invalidate();
        }
    }

    /// <summary>Shrink the focused panel's weight.</summary>
    public void ShrinkFocused()
    {
        if (FocusedIndex >= 0 && FocusedIndex < _panels.Count)
        {
            _panels[FocusedIndex].Weight = Math.Max(_panels[FocusedIndex].Weight - 1, 1);
            _app?.Invalidate();
        }
    }

    /// <summary>Closes and removes a panel by index.</summary>
    public async Task ClosePanelAsync(int index)
    {
        if (index > 0 && index < _panels.Count) // Can't close cloud shell (index 0)
        {
            var panel = _panels[index];
            _panels.RemoveAt(index);
            if (FocusedIndex >= _panels.Count)
                FocusedIndex = _panels.Count - 1;
            await panel.DisposeAsync();
            _app?.Invalidate();
        }
    }
}
