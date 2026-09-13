namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    private TerminalActivityState _activityState = TerminalActivityState.Default;
    private TerminalActivityState? _activityRestoreBaseline;
    private int _activityRestoreDepth;
    private event Action<TerminalActivityState>? ActivityStateChanged;

    /// <summary>Gets the current progress reported by OSC 9;4.</summary>
    /// <remarks>
    /// Defaults to hidden progress. RIS clears it; soft reset, screen changes, and process
    /// exit preserve it. Read a snapshot to capture progress and shell integration together.
    /// </remarks>
    public TerminalProgress Progress
    {
        get { lock (_bufferLock) return _activityState.Progress; }
    }

    /// <summary>Gets the current shell phase and latest reported command result.</summary>
    /// <remarks>
    /// Defaults to unknown, not idle. Only OSC 133 markers update this state, except RIS,
    /// which resets it. Disconnect and process exit do not imply command completion.
    /// </remarks>
    public TerminalShellIntegration ShellIntegration
    {
        get { lock (_bufferLock) return _activityState.ShellIntegration; }
    }

    /// <summary>Occurs when the reported progress changes to a distinct value.</summary>
    /// <remarks>
    /// Subscribing does not emit the baseline; read <see cref="Progress"/> for current state.
    /// This is a current-state notification, not a history of workload operations.
    /// </remarks>
    public event Action<TerminalProgress>? ProgressChanged;

    /// <summary>Occurs when the reported shell phase or latest exit code changes.</summary>
    /// <remarks>
    /// Subscribing does not emit the baseline; read <see cref="ShellIntegration"/>.
    /// Equal values are suppressed. This is not a command-started or command-completed event.
    /// </remarks>
    public event Action<TerminalShellIntegration>? ShellIntegrationChanged;

    /// <summary>Gets the working directory last reported by OSC 7.</summary>
    /// <remarks>
    /// Defaults to no reported directory. RIS clears it; soft reset, screen changes, and
    /// process exit preserve it, matching <see cref="Progress"/> and
    /// <see cref="ShellIntegration"/>.
    /// </remarks>
    public TerminalWorkingDirectory WorkingDirectory
    {
        get { lock (_bufferLock) return _activityState.WorkingDirectory; }
    }

    /// <summary>Occurs when the reported working directory changes to a distinct value.</summary>
    /// <remarks>
    /// Subscribing does not emit the baseline; read <see cref="WorkingDirectory"/> for
    /// current state.
    /// </remarks>
    public event Action<TerminalWorkingDirectory>? WorkingDirectoryChanged;

    private readonly List<TerminalCommandMark> _commandMarks = [];
    private int _commandMarkHistoryCapacity = 200;

    /// <summary>
    /// Gets the bounded history of OSC 133 command marks, oldest first.
    /// </summary>
    /// <remarks>
    /// Capacity is configured via <see cref="Hex1bTerminalOptions.CommandMarkHistoryCapacity"/>;
    /// once exceeded, the oldest marks are evicted. RIS and reflow do not remove existing
    /// entries, but their row anchors become unresolvable (see <see cref="TerminalCommandMark"/>).
    /// </remarks>
    public IReadOnlyList<TerminalCommandMark> CommandMarks
    {
        get { lock (_bufferLock) return [.. _commandMarks]; }
    }

    /// <summary>Occurs when a new OSC 133 command mark is recorded.</summary>
    /// <remarks>Subscribing does not emit prior marks; read <see cref="CommandMarks"/> for
    /// current history.</remarks>
    public event Action<TerminalCommandMark>? CommandMarkAdded;

    private void RecordCommandMarkIfPresent(string command, string parameters, string payload)
    {
        if (command != "133" || _commandMarkHistoryCapacity == 0)
            return;
        if (!TerminalActivityState.TryParseMarker(parameters, payload, out var phase, out var exitCode,
                out var rawParameters))
            return;

        EnsureTextRows();
        var mark = new TerminalCommandMark(phase, exitCode, rawParameters, _textGeneration, _textScreenRowIds[_cursorY]);
        _commandMarks.Add(mark);
        while (_commandMarks.Count > _commandMarkHistoryCapacity)
            _commandMarks.RemoveAt(0);
        CommandMarkAdded?.Invoke(mark);
    }

    internal void SubscribeActivityStateChanged(Action<TerminalActivityState> handler)
    {
        lock (_bufferLock)
        {
            ActivityStateChanged += handler;
            handler(_activityState);
        }
    }

    internal void UnsubscribeActivityStateChanged(Action<TerminalActivityState> handler)
        => ActivityStateChanged -= handler;

    internal void BeginActivityStateRestore()
    {
        lock (_bufferLock)
        {
            if (_activityRestoreDepth++ == 0)
                _activityRestoreBaseline = _activityState;
        }
    }

    internal void RestoreActivityState(TerminalProgress progress, TerminalShellIntegration shellIntegration,
        TerminalWorkingDirectory workingDirectory)
    {
        lock (_bufferLock)
        {
            SetActivityState(new TerminalActivityState(progress, shellIntegration, workingDirectory));
        }
        PresentationInvalidated?.Invoke();
    }

    internal void EndActivityStateRestore()
    {
        lock (_bufferLock)
        {
            if (_activityRestoreDepth == 0)
                throw new InvalidOperationException("No activity state restore is in progress.");
            if (--_activityRestoreDepth == 0)
            {
                var baseline = _activityRestoreBaseline!;
                _activityRestoreBaseline = null;
                NotifyActivityStateChanged(baseline, _activityState);
            }
        }
    }

    private void SetActivityState(TerminalActivityState state)
    {
        var previous = _activityState;
        if (previous == state)
            return;
        _activityState = state;
        if (_activityRestoreDepth == 0)
            NotifyActivityStateChanged(previous, state);
    }

    private void NotifyActivityStateChanged(TerminalActivityState previous, TerminalActivityState state)
    {
        if (previous == state)
            return;
        ActivityStateChanged?.Invoke(state);
        if (previous.Progress != state.Progress)
            ProgressChanged?.Invoke(state.Progress);
        if (previous.ShellIntegration != state.ShellIntegration)
            ShellIntegrationChanged?.Invoke(state.ShellIntegration);
        if (previous.WorkingDirectory != state.WorkingDirectory)
            WorkingDirectoryChanged?.Invoke(state.WorkingDirectory);
    }
}
