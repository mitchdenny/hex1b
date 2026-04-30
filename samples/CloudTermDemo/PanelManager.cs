using Hex1b;

namespace CloudTermDemo;

/// <summary>
/// A panel in the shell layout. Can be a terminal or a content panel.
/// </summary>
public sealed class ShellPanel : IAsyncDisposable
{
    public string Title { get; set; }
    public TerminalWidgetHandle? Handle { get; }
    public Hex1bTerminal? Terminal { get; }
    public bool IsTerminal => Handle != null;
    public string? Tag { get; init; }

    /// <summary>Arbitrary data associated with this panel (e.g. a ResourceListResult).</summary>
    public object? Data { get; init; }

    private readonly Task _runTask;

    public ShellPanel(string title, TerminalWidgetHandle? handle, Hex1bTerminal? terminal, Task? runTask)
    {
        Title = title;
        Handle = handle;
        Terminal = terminal;
        _runTask = runTask ?? Task.CompletedTask;
    }

    public static ShellPanel Content(string title, string? tag = null, object? data = null)
        => new(title, null, null, null) { Tag = tag, Data = data };

    public async ValueTask DisposeAsync()
    {
        Terminal?.Dispose();
        try { await _runTask; }
        catch (OperationCanceledException) { }
    }
}

/// <summary>
/// Manages all panels with a sliding viewport window.
/// Zoom controls how many panels are visible at once.
/// Focus navigation slides the viewport when reaching the edges.
/// </summary>
public sealed class PanelManager
{
    private readonly List<ShellPanel> _panels = [];
    private Hex1bApp? _app;

    /// <summary>Index of the currently focused panel (within all panels).</summary>
    public int FocusedIndex { get; private set; }

    /// <summary>How many panels are visible at once.</summary>
    public int Zoom { get; private set; } = 2;

    /// <summary>Index of the first visible panel in the viewport.</summary>
    public int ViewportStart { get; private set; }

    public IReadOnlyList<ShellPanel> Panels => _panels;

    /// <summary>The panels currently visible in the viewport.</summary>
    public IEnumerable<(ShellPanel Panel, int Index)> VisiblePanels
    {
        get
        {
            var end = Math.Min(ViewportStart + Zoom, _panels.Count);
            for (var i = ViewportStart; i < end; i++)
                yield return (_panels[i], i);
        }
    }

    public int PanelCount => _panels.Count;

    public void SetApp(Hex1bApp app) => _app = app;

    public void SetCloudShellPanel()
    {
        if (!_panels.Any(p => p.Tag == "cloud-shell"))
            _panels.Insert(0, ShellPanel.Content("Cloud Shell", tag: "cloud-shell"));
    }

    public void SetTutorialPanel()
    {
        if (!_panels.Any(p => p.Tag == "tutorial"))
            _panels.Add(ShellPanel.Content("Tutorial", tag: "tutorial"));
    }

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

        ShellPanel? panelRef = null;

        var runTask = Task.Run(async () =>
        {
            try { await terminal.RunAsync(); }
            catch (OperationCanceledException) { }
            finally
            {
                if (panelRef != null)
                {
                    var idx = _panels.IndexOf(panelRef);
                    if (idx >= 0)
                    {
                        _panels.RemoveAt(idx);
                        ClampState();
                        _app?.Invalidate();
                    }
                }
            }
        });

        var tutorialIndex = _panels.FindIndex(p => p.Tag == "tutorial");
        var insertAt = tutorialIndex >= 0 ? tutorialIndex : _panels.Count;
        var panel = new ShellPanel(title, handle, terminal, runTask);
        panelRef = panel;
        _panels.Insert(insertAt, panel);
        FocusedIndex = insertAt;
        EnsureFocusedVisible();
        _app?.Invalidate();
    }

    public void OpenContentPanel(string title, string tag)
    {
        var existing = _panels.FindIndex(p => p.Tag == tag);
        if (existing >= 0)
        {
            FocusedIndex = existing;
            EnsureFocusedVisible();
            _app?.Invalidate();
            return;
        }

        var tutorialIndex = _panels.FindIndex(p => p.Tag == "tutorial");
        var insertAt = tutorialIndex >= 0 ? tutorialIndex : _panels.Count;
        _panels.Insert(insertAt, ShellPanel.Content(title, tag: tag));
        FocusedIndex = insertAt;
        EnsureFocusedVisible();
        _app?.Invalidate();
    }

    /// <summary>
    /// Inserts a panel immediately to the right of the focused panel.
    /// Adjusts zoom to show both the focused panel and the new one.
    /// </summary>
    public void InsertPanelRight(string title, string? tag = null, object? data = null)
    {
        var insertAt = Math.Min(FocusedIndex + 1, _panels.Count);
        _panels.Insert(insertAt, ShellPanel.Content(title, tag: tag, data: data));

        // Ensure both the current and new panel are visible
        if (Zoom < 2) Zoom = 2;
        if (Zoom > _panels.Count) Zoom = _panels.Count;

        // Keep focus on current panel, but ensure the new one is in the viewport
        EnsureFocusedVisible();
        // Also make sure insertAt is visible
        if (insertAt >= ViewportStart + Zoom)
            ViewportStart = insertAt - Zoom + 1;
        ClampViewport();

        _app?.Invalidate();
    }

    /// <summary>Move focus to next panel, sliding viewport if at the edge.</summary>
    public void FocusNext()
    {
        if (_panels.Count == 0) return;
        FocusedIndex = (FocusedIndex + 1) % _panels.Count;
        EnsureFocusedVisible();
        RequestFocusOnCurrentPanel();
        _app?.Invalidate();
    }

    /// <summary>Move focus to previous panel, sliding viewport if at the edge.</summary>
    public void FocusPrevious()
    {
        if (_panels.Count == 0) return;
        FocusedIndex = (FocusedIndex - 1 + _panels.Count) % _panels.Count;
        EnsureFocusedVisible();
        RequestFocusOnCurrentPanel();
        _app?.Invalidate();
    }

    /// <summary>Zoom in: show fewer panels (minimum 1).</summary>
    public void ZoomIn()
    {
        Zoom = Math.Max(1, Zoom - 1);
        EnsureFocusedVisible();
        _app?.Invalidate();
    }

    /// <summary>Zoom out: show more panels (up to total count).</summary>
    public void ZoomOut()
    {
        Zoom = Math.Min(_panels.Count, Zoom + 1);
        EnsureFocusedVisible();
        _app?.Invalidate();
    }

    /// <summary>Closes the currently focused panel (can't close cloud shell).</summary>
    public async Task CloseCurrentPanelAsync()
    {
        if (FocusedIndex < 0 || FocusedIndex >= _panels.Count) return;
        var panel = _panels[FocusedIndex];
        if (panel.Tag is "cloud-shell") return;

        _panels.RemoveAt(FocusedIndex);
        await panel.DisposeAsync();
        ClampState();
        _app?.Invalidate();
    }

    /// <summary>Slide viewport so the focused panel is visible.</summary>
    private void EnsureFocusedVisible()
    {
        if (FocusedIndex < ViewportStart)
            ViewportStart = FocusedIndex;
        else if (FocusedIndex >= ViewportStart + Zoom)
            ViewportStart = FocusedIndex - Zoom + 1;
        ClampViewport();
    }

    private void ClampState()
    {
        if (FocusedIndex >= _panels.Count)
            FocusedIndex = Math.Max(0, _panels.Count - 1);
        ClampViewport();
    }

    private void ClampViewport()
    {
        if (Zoom > _panels.Count) Zoom = Math.Max(1, _panels.Count);
        if (ViewportStart + Zoom > _panels.Count)
            ViewportStart = Math.Max(0, _panels.Count - Zoom);
        if (ViewportStart < 0) ViewportStart = 0;
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
}
