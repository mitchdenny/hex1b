namespace Hex1b;

/// <summary>
/// Records which terminal handles have content-dependent state read while the host
/// builds, reconciles, and lays out its widget tree. Output from an observed handle must
/// rebuild the host because the tree may depend on that content.
/// </summary>
internal sealed class TerminalHostObservation
{
    private static readonly AsyncLocal<TerminalHostObservation?> s_current = new();

    private readonly object _lock = new();
    private HashSet<TerminalWidgetHandle>? _handles;
    private bool _closed;

    internal static TerminalHostObservation Begin()
    {
        var observation = new TerminalHostObservation();
        s_current.Value = observation;
        return observation;
    }

    internal static void Record(TerminalWidgetHandle handle)
    {
        var observation = s_current.Value;
        if (observation is null)
            return;
        lock (observation._lock)
        {
            if (!observation._closed)
                (observation._handles ??= new(ReferenceEqualityComparer.Instance)).Add(handle);
        }
    }

    /// <summary>Stops recording and returns the handles observed during the scope.</summary>
    internal IReadOnlySet<TerminalWidgetHandle>? End()
    {
        if (s_current.Value == this)
            s_current.Value = null;
        lock (_lock)
        {
            _closed = true;
            return _handles;
        }
    }
}
