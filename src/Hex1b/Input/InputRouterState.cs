namespace Hex1b.Input;

/// <summary>
/// Holds chord and held-key state for an input router instance.
/// Each Hex1bApp has its own state, ensuring test isolation and multi-app support.
/// </summary>
public sealed class InputRouterState
{
    private readonly object _holdLock = new();
    private readonly Dictionary<(Hex1bKey Key, int? Code), KeyHold> _holds = [];
    private readonly HashSet<KeyHold> _liveHolds = [];
    private readonly Dictionary<FocusRing, Action> _holdFocusHandlers = [];
    private TaskCompletionSource<Exception> _holdFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task<Exception> HoldFailure => _holdFailure.Task;

    internal void BeginKeyHolds()
    {
        lock (_holdLock)
            _holdFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    internal static (Hex1bKey Key, int? Code) KeyIdentity(Hex1bKeyEvent keyEvent)
        => keyEvent.Key == Hex1bKey.None ? (keyEvent.Key, keyEvent.UnicodeKeyCode) : (keyEvent.Key, null);

    internal void StartKeyHold(
        Hex1bKeyEvent keyEvent, FocusRing focusRing, InputBindingActionContext context,
        Func<InputBindingActionContext, CancellationToken, Task> handler)
    {
        if (keyEvent.EventType != Hex1bKeyEventType.Press)
            return;

        lock (_holdLock)
        {
            var key = KeyIdentity(keyEvent);
            if (_holds.ContainsKey(key))
                return;
            if (!_holdFocusHandlers.ContainsKey(focusRing))
            {
                Action onChanged = () => CancelChangedHolds(focusRing);
                _holdFocusHandlers.Add(focusRing, onChanged);
                focusRing.RoutingChanged += onChanged;
            }
            var hold = new KeyHold(focusRing, context.CancellationToken, _holdFailure);
            _holds.Add(key, hold);
            _liveHolds.Add(hold);
            hold.Completion = RunHoldAsync(hold, context, handler);
        }
    }

    private async Task RunHoldAsync(KeyHold hold, InputBindingActionContext context,
        Func<InputBindingActionContext, CancellationToken, Task> handler)
    {
        await hold.RunAsync(context, handler).ConfigureAwait(false);
        lock (_holdLock)
            _liveHolds.Remove(hold);
    }

    internal void ReleaseKeyHold(Hex1bKeyEvent keyEvent)
    {
        KeyHold? hold;
        lock (_holdLock)
            _holds.Remove(KeyIdentity(keyEvent), out hold);
        hold?.Cancel();
    }

    private void CancelChangedHolds(FocusRing focusRing)
    {
        lock (_holdLock)
        {
            foreach (var (key, hold) in _holds.ToArray())
            {
                if (ReferenceEquals(hold.FocusRing, focusRing) &&
                    (!ReferenceEquals(hold.FocusedNode, focusRing.FocusedNode) ||
                     !ReferenceEquals(hold.CapturedNode, focusRing.CapturedNode)))
                {
                    _holds.Remove(key);
                    hold.Cancel();
                }
            }
        }
    }

    internal void CancelKeyHolds()
    {
        lock (_holdLock)
        {
            foreach (var hold in _liveHolds)
                hold.Cancel();
            _holds.Clear();
            foreach (var (focusRing, handler) in _holdFocusHandlers)
                focusRing.RoutingChanged -= handler;
            _holdFocusHandlers.Clear();
        }
    }

    internal async Task StopKeyHoldsAsync()
    {
        CancelKeyHolds();
        Task[] pending;
        lock (_holdLock)
            pending = _liveHolds.Select(h => h.Completion).ToArray();
        await Task.WhenAll(pending).ConfigureAwait(false);
        if (HoldFailure.IsCompleted)
            throw new InvalidOperationException("A held-key input handler failed.", await HoldFailure.ConfigureAwait(false));
    }

    private sealed class KeyHold
    {
        private readonly object _lock = new();
        private readonly CancellationTokenSource _source = new();
        private readonly CancellationTokenRegistration _registration;
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<Exception> _failure;
        private Task? _cancellation;
        private bool _disposed;

        internal FocusRing FocusRing { get; }
        internal Hex1bNode? FocusedNode { get; }
        internal Hex1bNode? CapturedNode { get; }
        internal Task Completion { get; set; } = Task.CompletedTask;

        internal KeyHold(FocusRing focusRing, CancellationToken cancellationToken,
            TaskCompletionSource<Exception> failure)
        {
            FocusRing = focusRing;
            FocusedNode = focusRing.FocusedNode;
            CapturedNode = focusRing.CapturedNode;
            _failure = failure;
            // Do not link sources: synchronous parent cancellation would run user registrations inline.
            _registration = cancellationToken.UnsafeRegister(static state => ((KeyHold)state!).Cancel(), this);
        }

        internal void Cancel()
        {
            lock (_lock)
            {
                if (_disposed || _cancellation is not null)
                    return;
                _cancellation = ObserveCancellationAsync(_source.CancelAsync());
                _released.TrySetResult();
            }
        }

        private async Task ObserveCancellationAsync(Task cancellation)
        {
            try
            {
                await cancellation.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _failure.TrySetResult(ex);
            }
        }

        internal async Task RunAsync(InputBindingActionContext context,
            Func<InputBindingActionContext, CancellationToken, Task> handler)
        {
            var token = _source.Token;
            try
            {
                await Task.Run(() => handler(context, token)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _failure.TrySetResult(ex);
            }

            await _released.Task.ConfigureAwait(false);
            Task cancellation;
            lock (_lock)
                cancellation = _cancellation!;
            try
            {
                await cancellation.ConfigureAwait(false);
            }
            finally
            {
                _registration.Unregister();
                lock (_lock)
                {
                    _disposed = true;
                    _source.Dispose();
                }
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
