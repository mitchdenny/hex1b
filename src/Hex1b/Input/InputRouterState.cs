namespace Hex1b.Input;

/// <summary>
/// Holds the chord state for an input router instance.
/// Each Hex1bApp has its own state, ensuring test isolation and multi-app support.
/// </summary>
public sealed class InputRouterState
{
    private readonly object _holdLock = new();
    private readonly Dictionary<(Hex1bKey Key, int? Code), KeyHold> _holds = [];
    private readonly Dictionary<FocusRing, Action> _holdFocusHandlers = [];
    private TaskCompletionSource<Exception> _holdFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task<Exception> HoldFailure => _holdFailure.Task;

    internal void BeginKeyHolds()
    {
        lock (_holdLock)
            _holdFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static (Hex1bKey Key, int? Code) HoldKey(Hex1bKeyEvent keyEvent)
        => keyEvent.Key == Hex1bKey.None ? (keyEvent.Key, keyEvent.UnicodeKeyCode) : (keyEvent.Key, null);

    internal void StartKeyHold(
        Hex1bKeyEvent keyEvent, FocusRing focusRing, InputBindingActionContext context,
        Func<InputBindingActionContext, CancellationToken, Task> handler)
    {
        if (keyEvent.EventType == Hex1bKeyEventType.Repeat)
            return;

        var key = HoldKey(keyEvent);
        KeyHold hold;
        TaskCompletionSource<Exception> failure;
        lock (_holdLock)
        {
            if (_holds.ContainsKey(key))
                return;
            if (!_holdFocusHandlers.ContainsKey(focusRing))
            {
                Action onChanged = () => CancelChangedHolds(focusRing);
                _holdFocusHandlers.Add(focusRing, onChanged);
                focusRing.RoutingChanged += onChanged;
            }
            hold = new KeyHold(focusRing, context.CancellationToken);
            _holds.Add(key, hold);
            failure = _holdFailure;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await handler(context, hold.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (hold.Token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                failure.TrySetResult(ex);
            }
            finally
            {
                hold.Complete();
            }
        });
    }

    internal void ReleaseKeyHold(Hex1bKeyEvent keyEvent)
    {
        KeyHold? hold;
        lock (_holdLock)
            _holds.Remove(HoldKey(keyEvent), out hold);
        hold?.Cancel();
    }

    private void CancelChangedHolds(FocusRing focusRing)
    {
        List<KeyHold> cancelled = [];
        lock (_holdLock)
        {
            foreach (var (key, hold) in _holds.ToArray())
            {
                if (ReferenceEquals(hold.FocusRing, focusRing) &&
                    (!ReferenceEquals(hold.FocusedNode, focusRing.FocusedNode) ||
                     !ReferenceEquals(hold.CapturedNode, focusRing.CapturedNode)))
                {
                    _holds.Remove(key);
                    cancelled.Add(hold);
                }
            }
        }
        CancelHolds(cancelled);
    }

    internal void CancelKeyHolds()
    {
        KeyHold[] holds;
        lock (_holdLock)
        {
            holds = [.. _holds.Values];
            _holds.Clear();
            foreach (var (focusRing, handler) in _holdFocusHandlers)
                focusRing.RoutingChanged -= handler;
            _holdFocusHandlers.Clear();
        }
        CancelHolds(holds);
    }

    private static void CancelHolds(IEnumerable<KeyHold> holds)
    {
        List<Exception>? errors = null;
        foreach (var hold in holds)
        {
            try
            {
                hold.Cancel();
            }
            catch (Exception ex)
            {
                (errors ??= []).Add(ex);
            }
        }
        if (errors is not null)
            throw new AggregateException("One or more held-key cancellation callbacks failed.", errors);
    }

    private sealed class KeyHold(FocusRing focusRing, CancellationToken cancellationToken)
    {
        private readonly object _lock = new();
        private readonly CancellationTokenSource _source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        private bool _cancelled;
        private bool _cancelling;
        private bool _completed;

        internal FocusRing FocusRing { get; } = focusRing;
        internal Hex1bNode? FocusedNode { get; } = focusRing.FocusedNode;
        internal Hex1bNode? CapturedNode { get; } = focusRing.CapturedNode;
        internal CancellationToken Token => _source.Token;

        internal void Cancel()
        {
            lock (_lock)
            {
                if (_cancelled)
                    return;
                _cancelled = true;
                _cancelling = true;
            }
            try
            {
                _source.Cancel();
            }
            finally
            {
                lock (_lock)
                {
                    _cancelling = false;
                    if (_completed)
                        _source.Dispose();
                }
            }
        }

        internal void Complete()
        {
            lock (_lock)
            {
                _completed = true;
                if (_cancelled && !_cancelling)
                    _source.Dispose();
            }
        }
    }

    /// <summary>
    /// Current chord state - the trie node we're in mid-chord, if any.
    /// </summary>
    internal ChordTrie? ChordNode { get; set; }

    /// <summary>
    /// Chord state for capture-override bindings (separate from normal chord state
    /// so override chords work independently of focus-based chord routing).
    /// </summary>
    internal ChordTrie? CaptureOverrideChordNode { get; set; }
    
    /// <summary>
    /// The path when the chord started (to detect focus changes).
    /// </summary>
    internal List<Hex1bNode>? ChordAnchorPath { get; set; }
    
    /// <summary>
    /// The layer index of the chord (which node in the path owns the chord).
    /// </summary>
    internal int ChordLayerIndex { get; set; } = -1;
    
    /// <summary>
    /// Gets whether we're currently mid-chord.
    /// </summary>
    public bool IsInChord => ChordNode != null || CaptureOverrideChordNode != null;
    
    /// <summary>
    /// Event raised when chord state changes (for UI feedback).
    /// </summary>
    public event Action? OnChordStateChanged;
    
    /// <summary>
    /// Resets the chord state.
    /// </summary>
    public void Reset()
    {
        var wasInChord = ChordNode != null || CaptureOverrideChordNode != null;
        ChordNode = null;
        CaptureOverrideChordNode = null;
        ChordAnchorPath = null;
        ChordLayerIndex = -1;
        if (wasInChord)
        {
            OnChordStateChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Notifies that the chord state changed.
    /// </summary>
    internal void NotifyChordStateChanged()
    {
        OnChordStateChanged?.Invoke();
    }
}
