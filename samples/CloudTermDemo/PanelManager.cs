using Hex1b;

namespace CloudTermDemo;

/// <summary>
/// A panel in the shell layout. Can be a terminal or a content panel (like tutorial).
/// </summary>
public sealed class ShellPanel : IAsyncDisposable
{
    public string Title { get; set; }
    public TerminalWidgetHandle? Handle { get; }
    public Hex1bTerminal? Terminal { get; }
    public int Weight { get; set; } = 1;

    /// <summary>True if this is a terminal panel, false for content panels (tutorial etc.).</summary>
    public bool IsTerminal => Handle != null;

    /// <summary>Tag for identifying special panels (e.g. "tutorial", "cloud-shell").</summary>
    public string? Tag { get; init; }

    private readonly Task _runTask;

    public ShellPanel(string title, TerminalWidgetHandle? handle, Hex1bTerminal? terminal, Task? runTask)
    {
        Title = title;
        Handle = handle;
        Terminal = terminal;
        _runTask = runTask ?? Task.CompletedTask;
    }

    /// <summary>Creates a non-terminal content panel.</summary>
    public static ShellPanel Content(string title, string? tag = null)
        => new(title, null, null, null) { Tag = tag };

    public async ValueTask DisposeAsync()
    {
        Terminal?.Dispose();
        try { await _runTask; }
        catch (OperationCanceledException) { }
    }
}

/// <summary>
/// Manages all panels (terminal + content) and focus/resize state.
/// Panel 0 is always the cloud shell. The tutorial panel is the last content panel.
/// </summary>
public sealed class PanelManager
{
    private readonly List<ShellPanel> _panels = [];
    private Hex1bApp? _app;

    /// <summary>Index of the currently focused panel.</summary>
    public int FocusedIndex { get; private set; }

    public IReadOnlyList<ShellPanel> Panels => _panels;

    public int PanelCount => _panels.Count;

    public void SetApp(Hex1bApp app) => _app = app;

    /// <summary>Registers the cloud shell as panel 0 (content panel, not terminal).</summary>
    public void SetCloudShellPanel()
    {
        if (!_panels.Any(p => p.Tag == "cloud-shell"))
        {
            _panels.Insert(0, ShellPanel.Content("Cloud Shell", tag: "cloud-shell"));
        }
    }

    /// <summary>Registers the tutorial as the last panel (if not already present).</summary>
    public void SetTutorialPanel()
    {
        if (!_panels.Any(p => p.Tag == "tutorial"))
        {
            _panels.Add(ShellPanel.Content("Tutorial", tag: "tutorial"));
        }
    }

    /// <summary>Opens a new terminal panel running docker.</summary>
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

        // Insert before tutorial (which is always last)
        var tutorialIndex = _panels.FindIndex(p => p.Tag == "tutorial");
        var insertAt = tutorialIndex >= 0 ? tutorialIndex : _panels.Count;
        _panels.Insert(insertAt, new ShellPanel(title, handle, terminal, runTask));
        FocusedIndex = insertAt;
        _app?.Invalidate();
    }

    public void FocusNext()
    {
        if (_panels.Count > 0)
        {
            FocusedIndex = (FocusedIndex + 1) % _panels.Count;
            RequestFocusOnCurrentPanel();
            _app?.Invalidate();
        }
    }

    public void FocusPrevious()
    {
        if (_panels.Count > 0)
        {
            FocusedIndex = (FocusedIndex - 1 + _panels.Count) % _panels.Count;
            RequestFocusOnCurrentPanel();
            _app?.Invalidate();
        }
    }

    private void RequestFocusOnCurrentPanel()
    {
        if (FocusedIndex < 0 || FocusedIndex >= _panels.Count || _app == null)
            return;

        var panel = _panels[FocusedIndex];
        if (panel.Handle != null)
        {
            var handle = panel.Handle;
            _app.RequestFocus(node =>
                node is Hex1b.Nodes.TerminalNode tn && tn.Handle == handle);
        }
    }

    public void ExpandFocused()
    {
        if (FocusedIndex >= 0 && FocusedIndex < _panels.Count)
        {
            _panels[FocusedIndex].Weight = Math.Min(_panels[FocusedIndex].Weight + 1, 5);
            _app?.Invalidate();
        }
    }

    public void ShrinkFocused()
    {
        if (FocusedIndex >= 0 && FocusedIndex < _panels.Count)
        {
            _panels[FocusedIndex].Weight = Math.Max(_panels[FocusedIndex].Weight - 1, 1);
            _app?.Invalidate();
        }
    }

    /// <summary>Closes the currently focused panel (can't close cloud shell).</summary>
    public async Task CloseCurrentPanelAsync()
    {
        if (FocusedIndex >= 0 && FocusedIndex < _panels.Count)
        {
            var panel = _panels[FocusedIndex];
            // Don't close cloud shell
            if (panel.Tag is "cloud-shell")
                return;

            _panels.RemoveAt(FocusedIndex);
            if (FocusedIndex >= _panels.Count)
                FocusedIndex = _panels.Count - 1;
            await panel.DisposeAsync();
            _app?.Invalidate();
        }
    }

    /// <summary>Closes and removes a panel by index.</summary>
    public async Task ClosePanelAsync(int index)
    {
        if (index >= 0 && index < _panels.Count)
        {
            var panel = _panels[index];
            if (panel.Tag is "cloud-shell")
                return;

            _panels.RemoveAt(index);
            if (FocusedIndex >= _panels.Count)
                FocusedIndex = _panels.Count - 1;
            await panel.DisposeAsync();
            _app?.Invalidate();
        }
    }
}
